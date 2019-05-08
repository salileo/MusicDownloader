using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using System.Windows;

namespace MusicDownloader
{
    public class ProgressStatus
    {
        public string TotalProgressString;
        public double TotalProgressPercentage;

        public string CurrentProgressString;
        public double CurrentProgressPercentage;

        public object NodeData;
    }

    public class FailedNode
    {
        public Node_Common Node;
        public string FailureReason;
    }

    public class DownloaderInfo
    {
        public BackgroundWorker Worker
        {
            get { return m_worker; }
        }

        public DoWorkEventArgs WorkArgs
        {
            get { return m_workArgs; }
        }

        public Downloader.ReportProgressFunc ProgressCallback
        {
            get { return m_progressCallback; }
        }

        private BackgroundWorker m_worker;
        private DoWorkEventArgs m_workArgs;
        private Downloader.ReportProgressFunc m_progressCallback;

        public DownloaderInfo(BackgroundWorker worker, DoWorkEventArgs workArgs, Downloader.ReportProgressFunc progressCallback)
        {
            m_worker = worker;
            m_workArgs = workArgs;
            m_progressCallback = progressCallback;
        }
    }

    public class Downloader
    {
        public delegate void ReportProgressFunc(string status, double percentage, object data);

        public bool Downloading
        {
            get
            {
                return m_isDownloading;
            }

            set
            {
                m_isDownloading = value;
                if (!m_isDownloading)
                {
                    m_parent.c_downloadButton.Content = "Start Downloading";
                    m_parent.c_gridTop.IsEnabled = true;
                    m_parent.c_category.IsEnabled = true;
                    m_parent.c_addItem.IsEnabled = true;
                    m_parent.c_list.IsEnabled = true;
                    m_parent.c_currentProgress.Value = 0;
                    m_parent.c_currentProgressText.Text = "";
                    m_parent.c_totalProgress.Value = 0;
                    m_parent.c_totalProgressText.Text = "";
                }
                else
                {
                    m_parent.c_downloadButton.Content = "Cancel Downloading";
                    m_parent.c_gridTop.IsEnabled = false;
                    m_parent.c_category.IsEnabled = false;
                    m_parent.c_addItem.IsEnabled = false;
                    m_parent.c_list.IsEnabled = false;
                    m_parent.c_currentProgress.Value = 0;
                    m_parent.c_currentProgressText.Text = "";
                    m_parent.c_totalProgress.Value = 0;
                    m_parent.c_totalProgressText.Text = "";
                }
            }
        }

        private MainWindow m_parent;
        private BackgroundWorker m_downloadWorker;
        private Logger m_logger;
        private bool m_isDownloading;
        private string m_destinationPath;
        private List<FailedNode> errorFolders;
        private List<FailedNode> errorSongs;
        private ProgressStatus m_status;

        public Downloader(MainWindow obj)
        {
            m_parent = obj;

            errorFolders = new List<FailedNode>();
            errorSongs = new List<FailedNode>();

            m_downloadWorker = new BackgroundWorker();
            m_downloadWorker.WorkerReportsProgress = true;
            m_downloadWorker.WorkerSupportsCancellation = true;
            m_downloadWorker.DoWork += new DoWorkEventHandler(DownloadWorker_DoWork);
            m_downloadWorker.ProgressChanged += new ProgressChangedEventHandler(DownloadWorker_ProgressChanged);
            m_downloadWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(DownloadWorker_RunWorkerCompleted);

            m_status = new ProgressStatus();
            m_status.TotalProgressString = string.Empty;
            m_status.TotalProgressPercentage = 0;
            m_status.CurrentProgressString = string.Empty;
            m_status.CurrentProgressPercentage = 0;

            m_logger = new Logger(obj);

            Downloading = false;
        }

        public void Start(string destinationPath, ObservableCollection<Node_Common> nodes)
        {
            if(nodes.Count == 0)
            {
                ErrorLog.Show("Nothing specified to download.");
                return;
            }

            Downloading = true;
            m_destinationPath = destinationPath;
            m_downloadWorker.RunWorkerAsync(nodes);
        }

        public void Stop()
        {
            //we will update the Downloading property in the completed callback
            m_downloadWorker.CancelAsync();
        }

        private void ShowErrorList()
        {
            if ((errorFolders.Count == 0) && (errorSongs.Count == 0))
                return;

            StringBuilder folders = new StringBuilder();
            StringBuilder songs = new StringBuilder();

            foreach (FailedNode node in errorFolders)
            {
                folders.Append(node.Node.Name + "(" + node.FailureReason + ")" + "\n");
            }

            foreach (FailedNode node in errorSongs)
            {
                songs.Append(node.Node.Name + "(" + node.FailureReason + ")" + "\n");
            }

            string msg = "\n\nThe following items could not be downloaded :\n\n";
            msg += "Folders :\n";
            msg += folders.ToString();
            msg += "\n";
            msg += "Songs :\n";
            msg += songs.ToString();
            msg += "\n\n";

            m_logger.Addtext(msg);
            m_logger.UpdateText();
            
            MessageBox.Show("Some of the items could not be downloaded. Please see the logs for details.", "Download Errors");
        }

        private void AddFailureNode(Node_Common node, string reason)
        {
            FailedNode errNode = new FailedNode();
            errNode.Node = node;
            errNode.FailureReason = reason;

            if (node.NodeType == Node_Common.Type.T_DIR)
                errorFolders.Add(errNode);
            else if (node.NodeType == Node_Common.Type.T_SONG)
                errorSongs.Add(errNode);
        }

        private void ReportProgressPrimary(string status, double percentage)
        {
            m_status.TotalProgressString = status;
            m_status.TotalProgressPercentage = percentage;
            m_downloadWorker.ReportProgress((Int32)m_status.TotalProgressPercentage, m_status);
        }

        private void ReportProgressSecondary(string status, double percentage, object nodeData)
        {
            m_status.CurrentProgressString = status;
            m_status.CurrentProgressPercentage = percentage;
            m_downloadWorker.ReportProgress((Int32)m_status.TotalProgressPercentage, m_status);
        }

        private void DownloadWorker_DoWork(object sender, DoWorkEventArgs workArgs)
        {
            errorFolders.Clear();
            errorSongs.Clear();

            List<Node_Common> downloadNodes = new List<Node_Common>();
            foreach (Node_Common node in (workArgs.Argument as ObservableCollection<Node_Common>))
                downloadNodes.Add(node);

            downloadNodes.Reverse();

            if (m_downloadWorker.CancellationPending)
            {
                workArgs.Cancel = true;
                return;
            }

            ReportProgressPrimary("Starting downloads.", 0);

            Stack<Node_Common> nodesToProcess = new Stack<Node_Common>();
            foreach (Node_Common node in downloadNodes)
            {
                if (m_downloadWorker.CancellationPending)
                {
                    workArgs.Cancel = true;
                    return;
                }

                m_logger.Addtext("Got download node - " + node.Name + " (" + node.URL + ")\n");
                nodesToProcess.Push(node);
            }

            if (m_downloadWorker.CancellationPending)
            {
                workArgs.Cancel = true;
                return;
            }

            double count = 0;
            double total = nodesToProcess.Count;

            while (nodesToProcess.Count > 0)
            {
                Node_Common node = nodesToProcess.Pop();

                if (m_downloadWorker.CancellationPending)
                {
                    workArgs.Cancel = true;
                    return;
                }

                if ((node.NodeType == Node_Common.Type.T_SONG) && (node.IsParsed))
                {
                    Node_Song song = node as Node_Song;
                    m_logger.Addtext("Downloading song - " + song.Name + " (" + song.Mp3Path + ")\n");
                    ReportProgressPrimary("Downloading Song - " + song.Name, 10 + ((count / total) * 90));
                    count++;

                    try
                    {
                        DownloaderInfo info = new DownloaderInfo(m_downloadWorker, workArgs, ReportProgressSecondary);
                        song.Download(m_destinationPath, info);
                        RemoveNodeFromUIList(song);
                    }
                    catch (Exception exp)
                    {
                        AddFailureNode(song, exp.Message); 
                        continue;
                    }
                }
                else
                {
                    m_logger.Addtext("Processing subnodes of - " + node.Name + "\n");
                    ReportProgressPrimary("Gathering song list - " + node.Name, 10 + ((count / total) * 90));
                    count++;

                    try
                    {
                        DownloaderInfo info = new DownloaderInfo(m_downloadWorker, workArgs, ReportProgressSecondary);
                        node.ParsePage(info);
                        RemoveNodeFromUIList(node);
                    }
                    catch (Exception exp)
                    {
                        AddFailureNode(node, exp.Message);
                        continue;
                    }

                    if (node.NodeType == Node_Common.Type.T_DIR)
                    {
                        List<Node_Common> subNodes = new List<Node_Common>();
                        foreach (Node_Common subnode in (node as Node_Directory).Children)
                        {
                            if (m_downloadWorker.CancellationPending)
                            {
                                workArgs.Cancel = true;
                                return;
                            }

                            m_logger.Addtext("Added subnode for download - " + subnode.Name + " (" + subnode.URL + ")\n");
                            subNodes.Add(subnode);
                        }

                        subNodes.Reverse();
                        foreach (Node_Common subnode in subNodes)
                        {
                            nodesToProcess.Push(subnode);
                            AddNodeToUIList(subnode);
                        }

                        total += subNodes.Count;
                    }
                    else if (node.NodeType == Node_Common.Type.T_SONG)
                    {
                        m_logger.Addtext("Added song for download - " + node.Name + " (" + (node as Node_Song).Mp3Path + ")\n");
                        nodesToProcess.Push(node);
                        AddNodeToUIList(node);
                        total++;
                    }
                }
            }

            ReportProgressPrimary("Done downloading songs.", 100);
            workArgs.Result = true;
        }

        private void DownloadWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ProgressStatus status = e.UserState as ProgressStatus;
            m_parent.c_totalProgressText.Text = status.TotalProgressString;
            m_parent.c_totalProgress.Value = status.TotalProgressPercentage;
            m_parent.c_currentProgressText.Text = status.CurrentProgressString;
            m_parent.c_currentProgress.Value = status.CurrentProgressPercentage;
            m_logger.UpdateText();
        }

        private void DownloadWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                ErrorLog.Show("Downloading cancelled.");
            }
            else if (e.Error != null)
            {
                ErrorLog.Show("Downloading failed.", e.Error);
            }
            else
            {
                ShowErrorList();
                ErrorLog.Show("Downloading finished successfully.");
            }

            Downloading = false;
        }

        private delegate void UpdateUIDispatcher(Node_Common node);
        private void AddNodeToUIList(Node_Common node)
        {
            App.Current.Dispatcher.Invoke(new UpdateUIDispatcher(AddNodeNow), node);
        }

        private void AddNodeNow(Node_Common node)
        {
            (App.Current.MainWindow as MainWindow).m_downloadList.Insert(0, node);
        }

        private void RemoveNodeFromUIList(Node_Common node)
        {
            App.Current.Dispatcher.Invoke(new UpdateUIDispatcher(RemoveNodeNow), node);
        }

        private void RemoveNodeNow(Node_Common node)
        {
            (App.Current.MainWindow as MainWindow).m_downloadList.Remove(node);
        }
    }
}
