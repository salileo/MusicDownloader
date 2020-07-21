using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Windows.Threading;

namespace MusicDownloader
{
    public class Node_Song : Node_Common
    {
        #region OverridedProperties
        public override string FullPath
        {
            get
            {
                string path;
                if (string.IsNullOrEmpty(Mp3Path))
                {
                    path = Name + ".mp3";
                }
                else
                {
                    Uri mp3 = new Uri(Mp3Path);
                    path = Path.GetFileName(mp3.LocalPath.Replace("\"", ""));
                }

                if (!path.EndsWith("mp3") && !path.EndsWith("wma"))
                    path += ".mp3";

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
                    FileInfo file_info = new FileInfo(PathOnDisk);
                    exists = file_info.Exists;
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
        public override string FilterString { get; set; }
        public override ICollectionView ChildrenView { get; set; }
        #endregion

        #region PublicProperties
        public string Mp3Path
        {
            get { return m_mp3path; }
            private set
            {
                m_mp3path = value;
                NotifyPropertyChanged("Mp3Path");
            }
        }
        #endregion

        #region PrivateMembers
        private string m_mp3path;
        private bool m_needToPopulate;
        private BackgroundWorker m_worker;
        #endregion

        public Node_Song(string name, string url) : base(Node_Common.Type.T_SONG, name, url)
        {
            m_mp3path = string.Empty;
        }

        public override void Init()
        {
            //nothing to do here
        }

        #region PrivateMethods
        void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            Node_Common node = e.Argument as Node_Common;
            DownloaderInfo info = new DownloaderInfo(m_worker, e, null);
            node.ParsePage(info);
        }

        private void ParsingStarted(object result)
        {
            IsParsing = true;
            IsParsed = false;
        }

        private void ParsingComplete(object result)
        {
            IsParsing = false;
            IsParsed = true;
            Mp3Path = result as string;
        }

        private void ParsingFailed(object result)
        {
            IsParsing = false;
            IsParsed = false;
            Mp3Path = string.Empty;
        }

        private bool ParsePageInternal(DownloaderInfo asyncInfo, ref string path)
        {
            if (IsParsed)
                return true;

            if (asyncInfo.Worker.CancellationPending)
            {
                asyncInfo.WorkArgs.Cancel = true;
                return false;
            }

            if (asyncInfo.ProgressCallback != null)
                asyncInfo.ProgressCallback("Starting page download for song '" + Name + "'.", 0, null);

            string mp3path = string.Empty;
            NotifyPropertyChanged("Mp3Path");

            StringBuilder sb = null;
            HttpWebResponse response = null;
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
                string finalURL = "https://api.gigahost123.com/api/getDownloadUrl?bucket=mp3.gigahost123.com&key=" + HttpUtility.UrlEncode(this.URL);
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
                    asyncInfo.ProgressCallback("Reading page for song '" + Name + "'.", 20, null);

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
                        asyncInfo.ProgressCallback("Reading page for song '" + Name + "'.", 20 + (((double)sb.Length / (double)total) * 30), null);
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
                asyncInfo.ProgressCallback("Parsing page for song '" + Name + "'.", 50, null);

            try
            {
                Online_Song song = JsonConvert.DeserializeObject<Online_Song>(json_data);
                if (asyncInfo.Worker.CancellationPending)
                {
                    asyncInfo.WorkArgs.Cancel = true;
                    return false;
                }

                mp3path = song.url;
                NotifyPropertyChanged("Mp3Path");

                if (asyncInfo.Worker.CancellationPending)
                {
                    asyncInfo.WorkArgs.Cancel = true;
                    return false;
                }

                if (string.IsNullOrEmpty(mp3path))
                    throw (new Exception("No song link found."));

                path = mp3path;
            }
            catch(Exception exp)
            {
                throw exp;
            }

            if (asyncInfo.ProgressCallback != null)
                asyncInfo.ProgressCallback("Done processing page for song '" + Name + "'.", 100, null);

            return true;
        }

        private void DownloadStarted(object result)
        {
            IsDownloading = true;
        }

        private void DownloadComplete(object result)
        {
            IsDownloading = false;
        }

        private void DownloadInternal(string destinationPath, DownloaderInfo asyncInfo)
        {
            if (asyncInfo.Worker.CancellationPending)
            {
                asyncInfo.WorkArgs.Cancel = true;
                return;
            }

            if (asyncInfo.ProgressCallback != null)
                asyncInfo.ProgressCallback("Initializing data download for song '" + Name + "'.", 0, null);

            if (string.IsNullOrEmpty(Mp3Path))
                throw new Exception("No song to download.");

            if (asyncInfo.Worker.CancellationPending)
            {
                asyncInfo.WorkArgs.Cancel = true;
                return;
            }

            string tempFile = string.Empty;
            try
            {
                tempFile = Path.GetTempFileName();
            }
            catch (Exception exp)
            {
                throw new Exception("Temp file creation failed.\n  " + exp.Message);
            }

            if (asyncInfo.Worker.CancellationPending)
            {
                try { File.Delete(tempFile); }
                catch (Exception) { }

                asyncInfo.WorkArgs.Cancel = true;
                return;
            }

            if (asyncInfo.ProgressCallback != null)
                asyncInfo.ProgressCallback("Starting data download for song '" + Name + "'.", 10, null);

            HttpWebResponse response = null;
            FileStream outputFile = null;
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Mp3Path);
                response = (HttpWebResponse)request.GetResponse();
                Stream responseStream = response.GetResponseStream();

                if (asyncInfo.Worker.CancellationPending)
                {
                    response.Close();

                    try { File.Delete(tempFile); }
                    catch (Exception) { }

                    asyncInfo.WorkArgs.Cancel = true;
                    return;
                }

                if (asyncInfo.ProgressCallback != null)
                    asyncInfo.ProgressCallback("Creating output file for song '" + Name + "'.", 20, null);

                outputFile = new FileStream(tempFile, FileMode.Create, FileAccess.Write);
                Int32 bufSize = 1024 * 100;
                Int32 bytesRead = 0;
                byte[] buf = new byte[bufSize];

                long total = 0;
                if (responseStream.CanSeek)
                {
                    total = responseStream.Length;
                }
                else
                {
                    total = 10 * 1024 * 1024; //assuming 10 MB
                }

                while ((bytesRead = responseStream.Read(buf, 0, bufSize)) != 0)
                {
                    outputFile.Write(buf, 0, bytesRead);

                    if (asyncInfo.Worker.CancellationPending)
                    {
                        response.Close();
                        outputFile.Close();

                        try { File.Delete(tempFile); }
                        catch (Exception) { }

                        asyncInfo.WorkArgs.Cancel = true;
                        return;
                    }

                    if (asyncInfo.ProgressCallback != null)
                        asyncInfo.ProgressCallback("Downloading data for song '" + Name + "'.", 20 + (((double)outputFile.Length / (double)total) * 80), null);
                }

                outputFile.Close();
                response.Close();
            }
            catch (Exception exp)
            {
                if (response != null)
                    response.Close();

                if (outputFile != null)
                {
                    outputFile.Close();

                    try { File.Delete(tempFile); }
                    catch (Exception) { }
                }

                throw new Exception("Song download from '" + URL + "' failed.\n  " + exp.Message.Replace("\n", "\n  "));
            }

            response = null;
            outputFile = null;

            if (asyncInfo.Worker.CancellationPending)
            {
                try { File.Delete(tempFile); }
                catch (Exception) { }

                asyncInfo.WorkArgs.Cancel = true;
                return;
            }

            string pathOnDisk = destinationPath + "\\" + FullPath;
            string pathOfParent = Path.GetDirectoryName(pathOnDisk);

            try
            {
                System.IO.Directory.CreateDirectory(pathOfParent);
            }
            catch (Exception exp)
            {
                throw new Exception("Destination directory (" + pathOfParent + ") creation failed.\n  " + exp.Message);
            }

            if (asyncInfo.Worker.CancellationPending)
            {
                try { File.Delete(tempFile); }
                catch (Exception) { }

                asyncInfo.WorkArgs.Cancel = true;
                return;
            }

            try
            {
                //delete the existing file if it exists
                File.Delete(pathOnDisk);
            }
            catch (Exception)
            {
                //don't need to do anything here
            }

            try
            {
                File.Move(tempFile, pathOnDisk);
            }
            catch (Exception exp)
            {
                throw new Exception("Copying of temp file to final file failed.\n  " + exp.Message);
            }

            if (asyncInfo.ProgressCallback != null)
                asyncInfo.ProgressCallback("Done downloading for song '" + Name + "'.", 100, null);
        }
        #endregion

        public override void ParsePage(DownloaderInfo asyncInfo)
        {
            if (IsParsed)
                return;

            lock (m_lockObj)
            {
                App.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ParsingStartedDelegate(ParsingStarted), null);

                string path = string.Empty;
                bool succeeded = false;
                Exception exception = null;
                try
                {
                    succeeded = ParsePageInternal(asyncInfo, ref path);
                }
                catch(Exception exp)
                {
                    exception = exp;
                }

                if(succeeded)
                    App.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ParsingCompleteDelegate(ParsingComplete), path);
                else
                    App.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ParsingCompleteDelegate(ParsingFailed), null);

                if (exception != null)
                    throw exception;
            }
        }

        public void Download(string destinationPath, DownloaderInfo asyncInfo)
        {
            lock (m_lockObj)
            {
                App.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ParsingStartedDelegate(DownloadStarted), null);

                Exception exception = null;
                try
                {
                    DownloadInternal(destinationPath, asyncInfo);
                }
                catch (Exception exp)
                {
                    exception = exp;
                }

                App.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new ParsingCompleteDelegate(DownloadComplete), null);

                if (exception != null)
                    throw exception;
            }
        }
    }

    class Online_Song
    {
        public string url;
    }
}
