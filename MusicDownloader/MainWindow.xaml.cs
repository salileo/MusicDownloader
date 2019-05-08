using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Collections.Generic;

namespace MusicDownloader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static string m_version = ProductVersion.VersionInfo.Version;

        public ObservableCollection<Node_Common> m_downloadList;
        private Node_Common m_rootNode;
        private Downloader m_downloader;

        public MainWindow()
        {
            GenericUpdater.Updater.DoUpdate("MusicDownloader", m_version, "MusicDownloader_Version.xml", "MusicDownloader.msi", new GenericUpdater.Updater.UpdateFiredDelegate(UpdateFired));
            InitializeComponent();

            m_rootNode = null;
            m_downloader = new Downloader(this);
            m_downloadList = new ObservableCollection<Node_Common>();

            c_destPath.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            c_list.ItemsSource = m_downloadList;
            c_category.SelectedIndex = 0;
        }

        private void UpdateFired()
        {
            this.Close();
        }

        private void DestinationBrowse_Clicked(object sender, RoutedEventArgs e)
        {
            try
            {
                FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
                folderBrowserDialog.ShowNewFolderButton = true;
                folderBrowserDialog.Description = "Select the destination directory.";

                System.Windows.Forms.DialogResult result = folderBrowserDialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    c_destPath.Text = folderBrowserDialog.SelectedPath;
                }
            }
            catch (Exception exc)
            {
                ErrorLog.Show("Could not update the destination path.", exc);
            }
        }

        private void DestinationFolder_TextChanged(object sender, TextChangedEventArgs e)
        {
            //if (Directory.Exists(c_destPath.Text))
            DestinationPathTracker.Instance.NotifyPropertyChanged("");
        }

        private void DownloadOrCancel_Clicked(object sender, RoutedEventArgs e)
        {
            if (m_downloader.Downloading)
            {
                m_downloader.Stop();
            }
            else
            {
                if (string.IsNullOrEmpty(c_destPath.Text))
                {
                    ErrorLog.Show("No destination folder specified.");
                    return;
                }

                if (m_downloadList.Count > 0)
                {
                    m_downloader.Start(c_destPath.Text, m_downloadList);
                }
            }
        }

        private void Exit_Clicked(object sender, RoutedEventArgs e)
        {
            //cancel any ongoing requests
            if (m_downloader.Downloading)
                DownloadOrCancel_Clicked(null, null);

            this.Close();
        }

        private Node_Directory m_Movies;
        private Node_Directory m_Movies2;
        private Node_Directory m_Pop;
        private Node_Directory m_Artist;
        private Node_Directory m_Artist2;
        private Node_Directory m_Singles;
        private Node_Directory m_Oldies;
        private Node_Directory m_Indian;
        private Node_Directory m_Bhangra;
        private Node_Directory m_Ghazals;
        private Node_Directory m_Instrumental;
        private Node_Directory m_Pakistani;
        private void Category_Changed(object sender, SelectionChangedEventArgs e)
        {
            ComboBoxItem selected = e.AddedItems[0] as ComboBoxItem;
            string name = selected.Content.ToString();
            Node_Directory root = null;

            switch (name)
            {
                case "Movies":
                    if (m_Movies == null)
                    {
                        m_Movies = new Node_Directory(name, "http://www.apunkabollywood.net/browser/category/view/347/movies");
                        m_Movies.Init();
                    }
                    root = m_Movies;
                    break;
                case "Movies (part 2)":
                    if (m_Movies2 == null)
                    {
                        m_Movies2 = new Node_Directory(name, "http://www.apunkabollywood.net/browser/category/view/953/movies");
                        m_Movies2.Init();
                    }
                    root = m_Movies2;
                    break;
                case "Pop":
                    if (m_Pop == null)
                    {
                        m_Pop = new Node_Directory(name, "http://www.apunkabollywood.net/browser/category/view/10/pop");
                        m_Pop.Init();
                    }
                    root = m_Pop;
                    break;
                case "Artist":
                    if (m_Artist == null)
                    {
                        m_Artist = new Node_Directory(name, "http://www.apunkabollywood.net/browser/category/view/247/artists");
                        m_Artist.Init();
                    }
                    root = m_Artist;
                    break;
                case "Artist (part 2)":
                    if (m_Artist2 == null)
                    {
                        m_Artist2 = new Node_Directory(name, "http://www.apunkabollywood.net/browser/category/view/781/artist");
                        m_Artist2.Init();
                    }
                    root = m_Artist2;
                    break;
                case "Singles":
                    if (m_Singles == null)
                    {
                        m_Singles = new Node_Directory(name, "http://www.apunkabollywood.net/browser/category/view/3/exclusive-singles-&-promo-songs");
                        m_Singles.Init();
                    }
                    root = m_Singles;
                    break;
                case "Oldies":
                    if (m_Oldies == null)
                    {
                        m_Oldies = new Node_Directory(name, "http://www.apunkabollywood.net/browser/category/view/771/oldies");
                        m_Oldies.Init();
                    }
                    root = m_Oldies;
                    break;
                case "Indian":
                    if (m_Indian == null)
                    {
                        m_Indian = new Node_Directory(name, "http://www.apunkabollywood.net/browser/category/view/2/indian");
                        m_Indian.Init();
                    }
                    root = m_Indian;
                    break;
                case "Bhangra":
                    if (m_Bhangra == null)
                    {
                        m_Bhangra = new Node_Directory(name, "http://www.apunkabollywood.net/browser/category/view/267/bhangra");
                        m_Bhangra.Init();
                    }
                    root = m_Bhangra;
                    break;
                case "Ghazals":
                    if (m_Ghazals == null)
                    {
                        m_Ghazals = new Node_Directory(name, "http://www.apunkabollywood.net/browser/category/view/1124/ghazals");
                        m_Ghazals.Init();
                    }
                    root = m_Ghazals;
                    break;
                case "Instrumental":
                    if (m_Instrumental == null)
                    {
                        m_Instrumental = new Node_Directory(name, "http://www.apunkabollywood.net/browser/category/view/5/instrumentals");
                        m_Instrumental.Init();
                    }
                    root = m_Instrumental;
                    break;
                case "Pakistani":
                    if (m_Pakistani == null)
                    {
                        m_Pakistani = new Node_Directory(name, "http://www.apunkabollywood.net/browser/category/view/780/pakistani");
                        m_Pakistani.Init();
                    }
                    root = m_Pakistani;
                    break;
                default:
                    ErrorLog.Show("Invalid category.");
                    return;
            }

            m_rootNode = root;
            c_tree.Items.Clear();
            c_filter.Text = "";
            c_tree.Items.Add(m_rootNode);
        }

        private void Filter_Changed(object sender, TextChangedEventArgs e)
        {
            if (m_rootNode != null)
            {
                if (m_rootNode is Node_Directory)
                    (m_rootNode as Node_Directory).FilterString = c_filter.Text;
            }
        }

        private void AddItem_Clicked(object sender, RoutedEventArgs e)
        {
            Node_Common selected = c_tree.SelectedItem as Node_Common;
            if (selected != null)
                m_downloadList.Add(selected);
        }

        private void ListBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                m_downloadList.Remove(c_list.SelectedItem as Node_Common);
            }
        }

        public void TreeView_SelectItem(Node_Common node)
        {
            try
            {
                List<Node_Common> nodes = new List<Node_Common>();
                while (node != null)
                {
                    nodes.Add(node);
                    node = node.Parent;
                }

                nodes.Reverse();

                TreeViewItem item = null;
                while (nodes.Count > 0)
                {
                    Node_Common current = nodes[0];
                    nodes.RemoveAt(0);

                    DependencyObject obj = null;
                    if (item == null)
                    {
                        obj = c_tree.ItemContainerGenerator.ContainerFromItem(current);
                        item = obj as TreeViewItem;
                    }
                    else
                    {
                        obj = item.ItemContainerGenerator.ContainerFromItem(current);
                        item = obj as TreeViewItem;
                    }
                }

                MethodInfo selectMethod = typeof(TreeViewItem).GetMethod("Select", BindingFlags.NonPublic | BindingFlags.Instance);
                selectMethod.Invoke(item, new object[] { true });
            }
            catch { }
        }

        Point startPoint;
        private void c_tree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            startPoint = e.GetPosition(null);
        }

        private void c_tree_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // Get the current mouse position
            Point mousePos = e.GetPosition(null);
            Vector diff = startPoint - mousePos;

            if (e.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                 Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                // Get the dragged ListViewItem
                System.Windows.Controls.TreeView treeView = sender as System.Windows.Controls.TreeView;
                TreeViewItem treeViewItem = Utils.FindAnchestor<TreeViewItem>((DependencyObject)e.OriginalSource);

                // Find the data behind the TreeViewItem
                Node_Common node = null;
                if (treeViewItem != null)
                {
                    if (treeViewItem.Header is Node_Common)
                        node = treeViewItem.Header as Node_Common;
                    else if (treeViewItem.DataContext is Node_Common)
                        node = treeViewItem.DataContext as Node_Common;
                }

                if (node != null)
                {
                    // Initialize the drag & drop operation
                    System.Windows.DataObject dragData = new System.Windows.DataObject("myNode", node);
                    DragDrop.DoDragDrop(treeViewItem, dragData, System.Windows.DragDropEffects.Copy);
                }
            }
        }

        private void c_list_DragEnter(object sender, System.Windows.DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("myNode"))
                e.Effects = System.Windows.DragDropEffects.None;
        }

        private void c_list_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent("myNode"))
            {
                Node_Common node = e.Data.GetData("myNode") as Node_Common;
                m_downloadList.Add(node);
            }
        }
    }
}
