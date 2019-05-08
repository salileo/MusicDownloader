using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Data;
using System.Windows.Threading;

namespace MusicDownloader
{
    public class Node_Directory : Node_Common
    {
        #region OverridedProperties
        public override string FullPath
        {
            get
            {
                string path = Name;
                Node_Common current = Parent;
                while (current != null)
                {
                    path = current.Name + "\\" + path;
                    current = current.Parent;
                }

                return path;
            }
        }
        public override bool IsExisting
        {
            get
            {
                bool exists = false;
                try
                {
                    string basePath = (App.Current.MainWindow as MainWindow).c_destPath.Text;
                    DirectoryInfo dir_info = new DirectoryInfo(PathOnDisk);
                    exists = dir_info.Exists;
                }
                catch (Exception)
                {
                    exists = false;
                }

                return exists;
            }
        }
        public override bool NeedToPopulate
        {
            get
            {
                return m_needToPopulate;
            }
            set
            {
                m_needToPopulate = value;
                NotifyPropertyChanged("NeedToPopulate");

                if (m_needToPopulate && !IsParsed && !IsParsing &&
                    ((m_worker == null) || (!m_worker.IsBusy)))
                {
                    if (m_worker == null)
                    {
                        m_worker = new BackgroundWorker();
                        m_worker.WorkerSupportsCancellation = false;
                        m_worker.WorkerReportsProgress = false;
                        m_worker.DoWork += new DoWorkEventHandler(worker_DoWork);
                    }

                    m_worker.RunWorkerAsync(this);
                }
            }
        }
        public override string FilterString
        {
            get { return m_filterStringOriginal; }
            set
            {
                string lowerCase = value.ToLower();
                if (lowerCase != m_filterStringLowerCase)
                {
                    m_filterStringOriginal = value;
                    m_filterStringLowerCase = lowerCase;
                    m_childrenView.View.Refresh();
                }
            }
        }
        public override ICollectionView ChildrenView { get { return m_childrenView.View; } }
        #endregion

        #region PublicProperties
        public ObservableCollection<Node_Common> Children { get { return m_children; } }
        #endregion

        #region PrivateMembers
        private ObservableCollection<Node_Common> m_children;
        private CollectionViewSource m_childrenView;
        private string m_filterStringOriginal;
        private string m_filterStringLowerCase;
        private bool m_needToPopulate;
        private BackgroundWorker m_worker;
        #endregion

        public Node_Directory(string name, string url) : base(Node_Common.Type.T_DIR, name, url)
        {
            m_children = new ObservableCollection<Node_Common>();
        }

        public override void Init()
        {
            m_children.Clear();
            m_needToPopulate = true;

            Node_Song dummySong = new Node_Song("Dummy", "Dummy");
            dummySong.Init();
            m_children.Add(dummySong);

            if (m_childrenView == null)
            {
                m_childrenView = new CollectionViewSource();
                m_childrenView.Source = m_children;
                m_childrenView.Filter += new FilterEventHandler(childrenView_Filter);
            }
        }

        #region PrivateMethods
        void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            Node_Common node = e.Argument as Node_Common;
            DownloaderInfo info = new DownloaderInfo(m_worker, e, null);
            node.ParsePage(info);
        }

        private void childrenView_Filter(object sender, FilterEventArgs e)
        {
            Node_Common node = e.Item as Node_Common;
            if (string.IsNullOrEmpty(m_filterStringLowerCase) ||
                node.Name.ToLower().Contains(m_filterStringLowerCase))
            {
                e.Accepted = true;
            }
            else
            {
                e.Accepted = false;
            }
        }

        private void ParsingStarted(object result)
        {
            IsParsing = true;
            IsParsed = false;
            NeedToPopulate = true;
            m_children.Clear();
        }

        private void ParsingComplete(object result)
        {
            IsParsing = false;
            IsParsed = true;
        }

        private void ParsingFailed(object result)
        {
            IsParsing = false;
            IsParsed = false;
            if (m_children.Count == 0)
            {
                Node_Song dummySong = new Node_Song("Dummy", "Dummy");
                m_children.Add(dummySong);
            }
        }
        #endregion

        public override void ParsePage(DownloaderInfo asyncInfo)
        {
            if (IsParsed)
                return;

            lock (m_lockObj)
            {
                App.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ParsingStartedDelegate(ParsingStarted), null);

                bool succeeded = false;
                Exception exception = null;
                try
                {
                    succeeded = ParsePageInternal(asyncInfo);
                }
                catch (Exception exp)
                {
                    exception = exp;
                }

                if (succeeded)
                    App.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ParsingCompleteDelegate(ParsingComplete), null);
                else
                    App.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ParsingCompleteDelegate(ParsingFailed), null);

                if (exception != null)
                    throw exception;
            }
        }

        private delegate void AddChildDelegate(Node_Common node);
        private void AddChildNow(Node_Common node)
        {
            node.Init();
            m_children.Add(node);
            NotifyPropertyChanged("Children");
        }
        private void AddChild(Node_Common node)
        {
            App.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new AddChildDelegate(AddChildNow), node);
        }

        private bool ParsePageInternal(DownloaderInfo asyncInfo)
        {
            if (IsParsed)
                return true;

            if (asyncInfo.Worker.CancellationPending)
            {
                asyncInfo.WorkArgs.Cancel = true;
                return false;
            }

            if (asyncInfo.ProgressCallback != null)
                asyncInfo.ProgressCallback("Starting page download for folder '" + Name + "'.", 0, null);

            StringBuilder sb = null;
            HttpWebResponse response = null;
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(URL);
                response = (HttpWebResponse)request.GetResponse();
                Stream responseStream = response.GetResponseStream();
                sb = new StringBuilder();
                Int32 bufSize = 1024 * 100;
                Int32 bytesRead = 0;
                byte[] buf = new byte[bufSize];

                if (asyncInfo.Worker.CancellationPending)
                {
                    response.Close();
                    asyncInfo.WorkArgs.Cancel = true;
                    return false;
                }

                if (asyncInfo.ProgressCallback != null)
                    asyncInfo.ProgressCallback("Reading page for folder '" + Name + "'.", 20, null);

                long total = 0;
                if (responseStream.CanSeek)
                {
                    total = responseStream.Length;
                }
                else
                {
                    total = 500 * 1024; //assuming 500 KB
                }

                while ((bytesRead = responseStream.Read(buf, 0, bufSize)) != 0)
                {
                    sb.Append(Encoding.UTF8.GetString(buf, 0, bytesRead));

                    if (asyncInfo.Worker.CancellationPending)
                    {
                        response.Close();
                        asyncInfo.WorkArgs.Cancel = true;
                        return false;
                    }

                    if (asyncInfo.ProgressCallback != null)
                        asyncInfo.ProgressCallback("Reading page for folder '" + Name + "'.", 20 + (((double)sb.Length / (double)total) * 30), null);
                }

                response.Close();
            }
            catch (Exception exp)
            {
                if (response != null)
                    response.Close();

                throw new Exception("Page downloading failed.\n  " + exp.Message);
            }

            response = null;

            string html_data = sb.ToString();

            if (asyncInfo.Worker.CancellationPending)
            {
                asyncInfo.WorkArgs.Cancel = true;
                return false;
            }

            if (asyncInfo.ProgressCallback != null)
                asyncInfo.ProgressCallback("Parsing page for folder '" + Name + "'.", 50, null);

            try
            {
                //first search for subfolders
                List<string> div_sections = Utils.PartitionString(html_data, false, "<div id=\"categories\">", "</div>");

                if (div_sections.Count > 0)
                {
                    if (div_sections.Count != 1)
                        throw new Exception("Error while parsing categories.");

                    List<string> li_sections = Utils.PartitionString(div_sections[0], false, "<li>", "</li>");
                    if (li_sections.Count == 0)
                        throw new Exception("Error while evaluating sub-folders.");

                    if (asyncInfo.Worker.CancellationPending)
                    {
                        asyncInfo.WorkArgs.Cancel = true;
                        return false;
                    }

                    if (asyncInfo.ProgressCallback != null)
                        asyncInfo.ProgressCallback("Parsing page for folder '" + Name + "'.", 60, null);

                    double count = 0;
                    foreach (string li_entry in li_sections)
                    {
                        List<string> href = Utils.PartitionString(li_entry, false, "href=\"", "\"");
                        List<string> name = Utils.PartitionString(li_entry, false, "\">", "</a>");

                        if ((href.Count != 1) || (name.Count != 1))
                            throw new Exception("Error while fetching folder name and href.");

                        string folder_name = Utils.RemoveWhitespaces(name[0]);
                        string folder_url = Utils.RemoveWhitespaces(href[0]);

                        if (string.IsNullOrEmpty(folder_name) || string.IsNullOrEmpty(folder_url))
                            throw new Exception("Missing name/url for a folder.");

                        Node_Directory subfolder = new Node_Directory(folder_name, folder_url);
                        subfolder.Parent = this;
                        AddChild(subfolder);
                        count++;

                        if (asyncInfo.ProgressCallback != null)
                            asyncInfo.ProgressCallback("Parsing page for folder '" + Name + "'.", 60 + (((double)count / (double)li_sections.Count) * 40), subfolder);

                        if (asyncInfo.Worker.CancellationPending)
                        {
                            asyncInfo.WorkArgs.Cancel = true;
                            return false;
                        }
                    }
                }
                else
                {
                    //next search for songs
                    List<string> tr_sections = Utils.PartitionString(html_data, false, "<tr>", "</tr>");
                    if (tr_sections.Count == 0)
                        throw new Exception("Error while evaluating songs.");

                    if (asyncInfo.Worker.CancellationPending)
                    {
                        asyncInfo.WorkArgs.Cancel = true;
                        return false;
                    }

                    if (asyncInfo.ProgressCallback != null)
                        asyncInfo.ProgressCallback("Parsing page for folder '" + Name + "'.", 60, null);

                    double count = 0;
                    foreach (string tr_entry in tr_sections)
                    {
                        if (tr_entry.Contains("checkbox") && tr_entry.Contains("img src"))
                        {
                            List<string> td_sections = Utils.PartitionString(tr_entry, false, "<td ", "</td>");
                            if (td_sections.Count == 0)
                                throw new Exception("Error while evaluating song hrefs.");

                            foreach (string td_entry in td_sections)
                            {
                                if (td_entry.Contains("<small>"))
                                {
                                    List<string> href = Utils.PartitionString(td_entry, false, "href=\"", "\"");
                                    List<string> name = Utils.PartitionString(td_entry, false, "\">", "</a>");

                                    if ((href.Count != 1) || (name.Count != 1))
                                        throw new Exception("Error while fetching song name and href.");

                                    string song_name = Utils.RemoveWhitespaces(name[0]);
                                    string song_url = Utils.RemoveWhitespaces(href[0]);

                                    if (string.IsNullOrEmpty(song_name) || string.IsNullOrEmpty(song_url))
                                        throw new Exception("Missing name/url for a song.");

                                    Node_Song song = new Node_Song(song_name, song_url);
                                    song.Parent = this;
                                    AddChild(song);

                                    if (asyncInfo.ProgressCallback != null)
                                        asyncInfo.ProgressCallback("Parsing page for folder '" + Name + "'.", 60 + (((double)count / (double)tr_sections.Count) * 40), song);
                                }

                                if (asyncInfo.Worker.CancellationPending)
                                {
                                    asyncInfo.WorkArgs.Cancel = true;
                                    return false;
                                }
                            }
                        }

                        count++;

                        if (asyncInfo.Worker.CancellationPending)
                        {
                            asyncInfo.WorkArgs.Cancel = true;
                            return false;
                        }

                        if (asyncInfo.ProgressCallback != null)
                            asyncInfo.ProgressCallback("Parsing page for folder '" + Name + "'.", 60 + (((double)count / (double)tr_sections.Count) * 40), null);
                    }
                }
            }
            catch (Exception exp)
            {
                throw exp;
            }

            if (asyncInfo.ProgressCallback != null)
                asyncInfo.ProgressCallback("Done processing page for folder '" + Name + "'.", 100, null);

            return true;
        }
    }
}
