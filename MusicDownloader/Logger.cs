
namespace MusicDownloader
{
    public class Logger
    {
        private MainWindow m_parent;
        private object m_lockObj;
        private string m_data;

        public Logger(MainWindow obj)
        {
            m_parent = obj;
            m_lockObj = new object();
        }

        public void Addtext(string data)
        {
            lock(m_lockObj)
            {
                m_data += data;
            }
        }

        public void UpdateText()
        {
            lock (m_lockObj)
            {
                m_parent.c_log.Text += m_data;
                m_parent.c_log.ScrollToEnd();
                m_data = string.Empty;
            }
        }
    }
}
