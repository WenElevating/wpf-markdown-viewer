using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace WpfMarkdownEditor.Sample.Controls;

public partial class FileTreeView : System.Windows.Controls.UserControl
{
    public static readonly System.Windows.DependencyProperty ShowHeaderProperty =
        System.Windows.DependencyProperty.Register(nameof(ShowHeader), typeof(bool),
            typeof(FileTreeView), new System.Windows.PropertyMetadata(true));

    public bool ShowHeader
    {
        get => (bool)GetValue(ShowHeaderProperty);
        set => SetValue(ShowHeaderProperty, value);
    }

    public event Action<string>? FileSelected;

    private CancellationTokenSource? _scanCts;

    public FileTreeView()
    {
        InitializeComponent();
    }

    public void OpenFolder()
    {
        try
        {
            var dialog = new OpenFolderDialog
            {
                Title = "选择文件夹"
            };

            if (dialog.ShowDialog() == true)
            {
                var path = dialog.FolderName;
                ScanDirectoryAsync(path, null);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error opening folder: {ex.Message}");
        }
    }

    private void OnOpenFolder(object sender, System.Windows.RoutedEventArgs e) => OpenFolder();

    private async void ScanDirectoryAsync(string path, FileTreeNode? parentNode)
    {
        // Cancel previous scan
        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;

        try
        {
            var rootNode = parentNode ?? new FileTreeNode
            {
                Name = Path.GetFileName(path),
                FullPath = path,
                IsDirectory = true,
                IsLoaded = true
            };

            if (parentNode == null)
            {
                // Root level - clear tree and add root node
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    FileTree.Items.Clear();
                    FileTree.Items.Add(rootNode);
                });
            }

            // Scan directory in background
            var (directories, files) = await Task.Run(() => EnumerateDirectory(path, token), token);

            if (token.IsCancellationRequested)
                return;

            // Populate on UI thread
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                rootNode.Children.Clear();
                rootNode.IsLoading = false;

                foreach (var dir in directories)
                {
                    rootNode.Children.Add(dir);
                }

                foreach (var file in files)
                {
                    rootNode.Children.Add(file);
                }

                // Refresh tree view
                if (parentNode != null)
                {
                    var treeViewItem = GetTreeViewItem(parentNode);
                    treeViewItem?.Items.Refresh();
                }
                else
                {
                    FileTree.Items.Refresh();
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error scanning directory: {ex.Message}");
        }
    }

    private (List<FileTreeNode> directories, List<FileTreeNode> files) EnumerateDirectory(string path, CancellationToken token)
    {
        var directories = new List<FileTreeNode>();
        var files = new List<FileTreeNode>();

        if (!Directory.Exists(path))
            return (directories, files);

        try
        {
            // Enumerate directories
            foreach (var dir in Directory.EnumerateDirectories(path))
            {
                if (token.IsCancellationRequested)
                    break;

                var dirName = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(dirName) || dirName.StartsWith('.'))
                    continue;

                directories.Add(new FileTreeNode
                {
                    Name = dirName,
                    FullPath = dir,
                    IsDirectory = true,
                    IsLoaded = false
                });
            }

            // Enumerate .md files
            foreach (var file in Directory.EnumerateFiles(path, "*.md"))
            {
                if (token.IsCancellationRequested)
                    break;

                var fileName = Path.GetFileName(file);
                if (string.IsNullOrEmpty(fileName) || fileName.StartsWith('.'))
                    continue;

                files.Add(new FileTreeNode
                {
                    Name = fileName,
                    FullPath = file,
                    IsDirectory = false,
                    IsLoaded = true
                });
            }

            // Sort by name
            directories.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            files.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        }
        catch (UnauthorizedAccessException)
        {
            // Skip directories we can't access
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error enumerating {path}: {ex.Message}");
        }

        return (directories, files);
    }

    private void OnTreeViewItemExpanded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (e.OriginalSource is not System.Windows.Controls.TreeViewItem treeViewItem ||
            treeViewItem.DataContext is not FileTreeNode node)
            return;

        if (node.IsDirectory && !node.IsLoaded && !node.IsLoading)
        {
            node.IsLoading = true;
            treeViewItem.Items.Refresh();
            ScanDirectoryAsync(node.FullPath, node);
        }
    }

    private void OnTreeViewItemSelected(object sender, System.Windows.RoutedEventArgs e)
    {
        if (e.OriginalSource is not System.Windows.Controls.TreeViewItem treeViewItem ||
            treeViewItem.DataContext is not FileTreeNode node)
            return;

        if (!node.IsDirectory)
        {
            FileSelected?.Invoke(node.FullPath);
        }
    }

    private System.Windows.Controls.TreeViewItem? GetTreeViewItem(FileTreeNode node)
    {
        return FileTree.ItemContainerGenerator.ContainerFromItem(node) as System.Windows.Controls.TreeViewItem;
    }

    public class FileTreeNode : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _fullPath = string.Empty;
        private bool _isDirectory;
        private bool _isLoaded;
        private bool _isLoading;

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        public string FullPath
        {
            get => _fullPath;
            set
            {
                if (_fullPath != value)
                {
                    _fullPath = value;
                    OnPropertyChanged(nameof(FullPath));
                }
            }
        }

        public bool IsDirectory
        {
            get => _isDirectory;
            set
            {
                if (_isDirectory != value)
                {
                    _isDirectory = value;
                    OnPropertyChanged(nameof(IsDirectory));
                }
            }
        }

        public bool IsLoaded
        {
            get => _isLoaded;
            set
            {
                if (_isLoaded != value)
                {
                    _isLoaded = value;
                    OnPropertyChanged(nameof(IsLoaded));
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged(nameof(IsLoading));
                }
            }
        }

        public ObservableCollection<FileTreeNode> Children { get; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString() => Name;
    }
}
