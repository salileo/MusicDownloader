using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
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
            m_needToPopulate = false;

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
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
                string finalURL = "https://api.gigahost123.com/api/listS3?bucket=mp3.gigahost123.com&path=" + HttpUtility.UrlEncode(this.URL);
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(finalURL);
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

            string json_data = sb.ToString();

            if (asyncInfo.Worker.CancellationPending)
            {
                asyncInfo.WorkArgs.Cancel = true;
                return false;
            }

            if (asyncInfo.ProgressCallback != null)
                asyncInfo.ProgressCallback("Parsing page for folder '" + Name + "'.", 50, null);

            try
            {
                Online_Page page = JsonConvert.DeserializeObject<Online_Page>(json_data);

                if (page.folders.Length > 0)
                {
                    if (asyncInfo.Worker.CancellationPending)
                    {
                        asyncInfo.WorkArgs.Cancel = true;
                        return false;
                    }

                    if (asyncInfo.ProgressCallback != null)
                        asyncInfo.ProgressCallback("Parsing page for folder '" + Name + "'.", 60, null);

                    double count = 0;
                    foreach (Online_Folder folder_entry in page.folders)
                    {
                        string folder_url = "/" + folder_entry.Prefix;
                        string[] parts = folder_url.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                        string folder_name = parts[parts.Length - 1];

                        if (string.IsNullOrEmpty(folder_url) || string.IsNullOrEmpty(folder_name))
                            throw new Exception("Missing name/url for a folder.");

                        Node_Directory subfolder = new Node_Directory(folder_name, folder_url);
                        subfolder.Parent = this;
                        AddChild(subfolder);
                        count++;

                        if (asyncInfo.ProgressCallback != null)
                            asyncInfo.ProgressCallback("Parsing page for folder '" + Name + "'.", 60 + (((double)count / (double)page.folders.Length) * 20), subfolder);

                        if (asyncInfo.Worker.CancellationPending)
                        {
                            asyncInfo.WorkArgs.Cancel = true;
                            return false;
                        }
                    }
                }

                if (page.files.Length > 0)
                {
                    if (asyncInfo.Worker.CancellationPending)
                    {
                        asyncInfo.WorkArgs.Cancel = true;
                        return false;
                    }

                    if (asyncInfo.ProgressCallback != null)
                        asyncInfo.ProgressCallback("Parsing page for folder '" + Name + "'.", 80, null);

                    double count = 0;
                    foreach (Online_File file_entry in page.files)
                    {
                        string song_url = "/" + file_entry.Key;
                        string[] parts = song_url.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                        string song_name = parts[parts.Length - 1];

                        if (string.IsNullOrEmpty(song_url) || string.IsNullOrEmpty(song_name))
                            throw new Exception("Missing name/url for a song.");

                        Node_Song song = new Node_Song(song_name, song_url);
                        song.Parent = this;
                        AddChild(song);
                        count++;

                        if (asyncInfo.ProgressCallback != null)
                            asyncInfo.ProgressCallback("Parsing page for folder '" + Name + "'.", 80 + (((double)count / (double)page.files.Length) * 20), song);

                        if (asyncInfo.Worker.CancellationPending)
                        {
                            asyncInfo.WorkArgs.Cancel = true;
                            return false;
                        }
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

    class Online_Folder
    {
        public string Prefix;
    }

    class Online_File
    {
        public string Key;
        public string LastModified;
        public string ETag;
        public int Size;
        public string StorageClass;
    }

    class Online_Page
    {
        public Online_File[] files;
        public Online_Folder[] folders;
    }
}
