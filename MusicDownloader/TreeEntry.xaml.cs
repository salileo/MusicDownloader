using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.IO;
using System.Diagnostics;

namespace MusicDownloader
{
    /// <summary>
    /// Interaction logic for TreeEntry.xaml
    /// </summary>
    public partial class TreeEntry : UserControl
    {
        private TreeViewItem parent;
        private Node_Common context;
        public TreeEntry()
        {
            InitializeComponent();

            this.Loaded += new RoutedEventHandler(TreeEntry_Loaded);
            this.DataContextChanged += new DependencyPropertyChangedEventHandler(TreeEntry_DataContextChanged);
        }

        private void TreeEntry_Loaded(object sender, RoutedEventArgs e)
        {
            if (parent == null)
                parent = Utils.FindAnchestor<TreeViewItem>(this);
        }

        private void TreeEntry_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (parent == null)
                parent = Utils.FindAnchestor<TreeViewItem>(this);

            context = (e.NewValue as Node_Common);
            if (parent != null)
            {
                ContextMenu menu = new ContextMenu();
                menu.DataContext = context;

                if (context != null)
                {
                    MenuItem item = null;

                    item = new MenuItem();
                    item.Header = context.Name;
                    item.IsEnabled = false;
                    menu.Items.Add(item);

                    menu.Items.Add(new Separator());

                    item = new MenuItem();
                    item.Header = "Add to downloads";
                    item.Click += new RoutedEventHandler(Add_Item);
                    menu.Items.Add(item);

                    item = new MenuItem();
                    item.Header = "Open containing folder";
                    item.Click += new RoutedEventHandler(Open_ContainingFolder);
                    menu.Items.Add(item);

                    item = new MenuItem();
                    item.Header = "Open file/folder";
                    {
                        Binding bnd = null;
                        try
                        {
                            bnd = new Binding("IsExisting");
                            bnd.Source = context;
                        }
                        catch (Exception)
                        {
                            bnd = null;
                        }

                        item.SetBinding(MenuItem.IsEnabledProperty, bnd);
                    }
                    item.Click += new RoutedEventHandler(Open_FileOrFolder);
                    menu.Items.Add(item);

                    item = new MenuItem();
                    item.Header = "Parse file/folder";
                    item.Click += new RoutedEventHandler(Parse_FileOrFolder);
                    menu.Items.Add(item);

                    item = new MenuItem();
                    item.Header = "Parse children file/folder";
                    if (context.NodeType == Node_Common.Type.T_DIR)
                    {
                        Binding bnd = null;
                        try
                        {
                            bnd = new Binding("IsParsed");
                            bnd.Source = context;
                        }
                        catch (Exception)
                        {
                            bnd = null;
                        }

                        item.SetBinding(MenuItem.IsEnabledProperty, bnd);
                    }
                    else
                    {
                        item.IsEnabled = false;
                    }
                    item.Click += new RoutedEventHandler(ParseChildren_FileOrFolder);
                    menu.Items.Add(item);
                }

                parent.ContextMenu = menu;
                parent.ContextMenu.Opened += new RoutedEventHandler(ContextMenu_Opened);
            }
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (parent != null)
                parent.IsSelected = true;
        }

        private void Add_Item(object sender, RoutedEventArgs e)
        {
            if (context != null)
                (App.Current.MainWindow as MainWindow).m_downloadList.Add(context);
        }

        private void Open_ContainingFolder(object sender, RoutedEventArgs e)
        {
            if (context != null)
            {
                try
                {
                    string path = Path.GetDirectoryName(context.PathOnDisk);
                    Process.Start("explorer.exe", path);
                }
                catch (Exception exp)
                {
                    ErrorLog.Show("Open folder failed.", exp);
                }
            }
        }

        private void Open_FileOrFolder(object sender, RoutedEventArgs e)
        {
            if (context != null)
            {
                try
                {
                    Process.Start("explorer.exe", context.PathOnDisk);
                }
                catch (Exception exp)
                {
                    ErrorLog.Show("Open file failed.", exp);
                }
            }
        }

        private void Parse_FileOrFolder(object sender, RoutedEventArgs e)
        {
            if (context != null)
                context.NeedToPopulate = true;
        }

        private void ParseChildren_FileOrFolder(object sender, RoutedEventArgs e)
        {
            if ((context != null) && (context is Node_Directory))
            {
                foreach (Node_Common node in (context as Node_Directory).Children)
                    node.NeedToPopulate = true;
            }
        }
    }
}
