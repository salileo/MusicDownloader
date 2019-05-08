using System;
using System.Windows;

namespace MusicDownloader
{
    class ErrorLog
    {
        public static void Show(string message)
        {
            MessageBox.Show(message);
        }

        public static void Show(string message, Exception excp)
        {
            MessageBox.Show(message + "\n\nIssue :\n" + excp.Message);
        }
    }
}
