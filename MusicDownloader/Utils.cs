using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MusicDownloader
{
    class Utils
    {
        static BitmapImage waitBitmap = null;
        static BitmapImage tickBitmap = null;
        static BitmapImage downBitmap = null;

        public static BitmapImage GetWaitIcon()
        {
            if(waitBitmap == null)
            {
                waitBitmap = new BitmapImage();
                waitBitmap.BeginInit();
                waitBitmap.UriSource = new Uri("Icons/wait.png", UriKind.Relative);
                waitBitmap.EndInit();
            }

            return waitBitmap;
        }

        public static BitmapImage GetTickIcon()
        {
            if (tickBitmap == null)
            {
                tickBitmap = new BitmapImage();
                tickBitmap.BeginInit();
                tickBitmap.UriSource = new Uri("Icons/tick.png", UriKind.Relative);
                tickBitmap.EndInit();
            }

            return tickBitmap;
        }

        public static BitmapImage GetDownIcon()
        {
            if (downBitmap == null)
            {
                downBitmap = new BitmapImage();
                downBitmap.BeginInit();
                downBitmap.UriSource = new Uri("Icons/down.png", UriKind.Relative);
                downBitmap.EndInit();
            }

            return downBitmap;
        }

        public static string RemoveWhitespaces(string data)
		{
            //need to remove ' ', '\t', '\n', '\r'
            data = data.Trim();
            return data;
		}

		public static List<string> PartitionString(string data, Boolean add_tags, string start_tag, string end_tag)
		{
            List<string> result = new List<string>();
			if(string.IsNullOrEmpty(data) || string.IsNullOrEmpty(start_tag))
				return result;

            Int32 start_tag_pos_1 = data.IndexOf(start_tag);
			while(start_tag_pos_1 >= 0)
			{
				string sub_data = string.Empty;
				Int32 start_tag_pos_2 = data.IndexOf(start_tag, (start_tag_pos_1 + start_tag.Length));
				
				if(start_tag_pos_2 >= 0)
					sub_data = data.Substring(start_tag_pos_1, (start_tag_pos_2 - start_tag_pos_1));
				else
					sub_data = data.Substring(start_tag_pos_1);

                if (!string.IsNullOrEmpty(end_tag))
				{
					Int32 end_tag_pos_1 = sub_data.IndexOf(end_tag, start_tag.Length);
					if(end_tag_pos_1 >= 0)
					{
						sub_data = sub_data.Substring(0, (end_tag_pos_1 + end_tag.Length));

						if(!add_tags)
							sub_data = sub_data.Substring(start_tag.Length, (sub_data.Length - start_tag.Length - end_tag.Length));

                        result.Add(sub_data);
					}
				}
				else
				{
					if(!add_tags)
						sub_data = sub_data.Substring(start_tag.Length);

                    result.Add(sub_data);
				}
				
				start_tag_pos_1 = start_tag_pos_2;
			} 
			
			return result;
		}

        public static T FindAnchestor<T>(DependencyObject current)
            where T : DependencyObject
        {
            do
            {
                if (current is T)
                {
                    return (T)current;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            while (current != null);
            return null;
        }
    }

    class DestinationPathTracker : INotifyPropertyChanged
    {
        private static DestinationPathTracker m_instance;
        public static DestinationPathTracker Instance
        {
            get
            {
                if (m_instance == null)
                    m_instance = new DestinationPathTracker();

                return m_instance;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
