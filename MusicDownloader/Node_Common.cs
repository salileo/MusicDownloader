using System;
using System.ComponentModel;
using System.Windows.Media.Imaging;
using System.IO;

namespace MusicDownloader
{
    public class Node_Common : INotifyPropertyChanged
    {
        public enum Type { T_DIR, T_SONG };

        #region CommonProperties
        public Type NodeType { get { return m_type; } }
        public string Name { get { return m_name; } }
        public string URL { get { return m_url; } }
        public BitmapImage Icon
        {
            get
            {
                if (IsParsing)
                    return Utils.GetWaitIcon();
                else if (IsDownloading)
                    return Utils.GetDownIcon();
                else if (IsExisting)
                    return Utils.GetTickIcon();
                else
                    return null;
            }
        }
        public Node_Directory Parent
        {
            get { return m_parent; }
            set
            {
                m_parent = value;
                NotifyPropertyChanged("Parent");
            }
        }
        public bool IsParsed
        {
            get { return m_isParsed; }
            set
            {
                m_isParsed = value;
                NotifyPropertyChanged("IsParsed");
            }
        }
        public bool IsParsing
        {
            get { return m_isParsing; }
            set
            {
                m_isParsing = value;
                NotifyPropertyChanged("IsParsing");
            }
        }
        public bool IsDownloading
        {
            get { return m_isDownloading; }
            set
            {
                m_isDownloading = value;
                NotifyPropertyChanged("IsDownloading");
            }
        }
        public string PathOnDisk
        {
            get
            {
                string basePath = (App.Current.MainWindow as MainWindow).c_destPath.Text;
                return (basePath + "\\" + FullPath);
            }
        }
        #endregion

        #region VirtualProperties
        public virtual string FullPath
        {
            get { throw new NotImplementedException(); }
        }
        public virtual bool IsExisting
        {
            get { throw new NotImplementedException(); }
        }
        public virtual bool NeedToPopulate
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }
        public virtual string FilterString
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }
        public virtual ICollectionView ChildrenView
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }
        #endregion

        #region ProtectMembers
        protected object m_lockObj;
        #endregion

        #region PrivateMembers
        private Type m_type;
        private string m_name;
        private string m_url;
        private Node_Directory m_parent;
        private bool m_isParsing;
        private bool m_isParsed;
        private bool m_isDownloading;
        #endregion

        public Node_Common(Type type, string name, string url)
        {
            m_type = type;
            m_name = name;
            m_url = url;
            m_parent = null;

            m_isDownloading = false;
            m_isParsing = false;
            m_isParsed = false;
            m_lockObj = new object();

            DestinationPathTracker.Instance.PropertyChanged += new PropertyChangedEventHandler(DestinationPathTracker_PropertyChanged);
            this.PropertyChanged += new PropertyChangedEventHandler(Self_PropertyChanged);
        }

        #region PrivateMethods
        private void Self_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "Parent":
                    NotifyPropertyChanged("FullPath");
                    break;
                case "FullPath":
                    NotifyPropertyChanged("IsExisting");
                    break;
                case "IsExisting":
                    NotifyPropertyChanged("Icon");
                    break;
                case "IsDownloading":
                    NotifyPropertyChanged("IsExisting");
                    NotifyPropertyChanged("Icon");
                    break;
                case "IsParsing":
                case "IsParsed":
                    NotifyPropertyChanged("Icon");
                    break;
                case "Mp3Path":
                    NotifyPropertyChanged("FullPath");
                    break;
                default:
                    break;
            }
        }

        private void DestinationPathTracker_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            NotifyPropertyChanged("IsExisting");
        }
        #endregion

        protected delegate void ParsingStartedDelegate(object result);
        protected delegate void ParsingCompleteDelegate(object result);

        public virtual void Init()
        {
            throw new NotImplementedException();
        }

        public virtual void ParsePage(DownloaderInfo asyncInfo)
        {
            throw new NotImplementedException();
        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
