using System.IO;

namespace WpfMarkdownEditor.Sample.ViewModels;

public sealed class WorkspaceViewModel : ObservableObject, IDisposable
{
    private readonly FolderWorkspaceService _folderWorkspaceService;
    private WorkspaceTreeNode? _root;
    private string? _workspaceFolderPath;
    private Dictionary<string, WorkspaceTreeNode> _workspaceIndex = new(StringComparer.OrdinalIgnoreCase);
    private bool _isSelectingNode;
    private CancellationTokenSource? _folderScanCts;

    public WorkspaceViewModel(FolderWorkspaceService folderWorkspaceService)
    {
        _folderWorkspaceService = folderWorkspaceService;
    }

    public event EventHandler<bool>? SelectionStateChanged;

    public WorkspaceTreeNode? Root
    {
        get => _root;
        private set => SetProperty(ref _root, value);
    }

    public string? WorkspaceFolderPath
    {
        get => _workspaceFolderPath;
        private set => SetProperty(ref _workspaceFolderPath, value);
    }

    public IReadOnlyDictionary<string, WorkspaceTreeNode> WorkspaceIndex => _workspaceIndex;

    public bool IsSelectingNode => _isSelectingNode;

    public IReadOnlyList<WorkspaceTreeNode> RootChildren => Root?.Children ?? [];

    public async Task<FolderWorkspaceResult?> LoadFolderAsync(
        string folderPath,
        CancellationToken cancellationToken = default)
    {
        _folderScanCts?.Cancel();
        _folderScanCts?.Dispose();
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _folderScanCts = linkedCts;

        try
        {
            var result = await _folderWorkspaceService.ScanShallowAsync(folderPath, linkedCts.Token);
            if (linkedCts.IsCancellationRequested)
                return null;

            ApplyWorkspaceResult(folderPath, result);
            return result;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            if (ReferenceEquals(_folderScanCts, linkedCts))
                _folderScanCts = null;

            linkedCts.Dispose();
        }
    }

    public async Task<WorkspaceTreeNode?> LoadCurrentFileDirectoryAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (_workspaceIndex.TryGetValue(fullPath, out var existingNode))
        {
            SelectNode(existingNode);
            return existingNode;
        }

        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
            return null;

        var result = await LoadFolderAsync(directory, cancellationToken);
        if (result is null || !_workspaceIndex.TryGetValue(fullPath, out var node))
            return null;

        SelectNode(node);
        return node;
    }

    public async Task LoadNodeChildrenAsync(
        WorkspaceTreeNode node,
        CancellationToken cancellationToken = default)
    {
        if (!node.IsDirectory || node.ChildrenLoaded)
            return;

        var result = await _folderWorkspaceService.ScanShallowAsync(node.FullPath, cancellationToken);
        node.Children.Clear();
        foreach (var child in result.Root.Children)
            node.Children.Add(child);

        node.ChildrenLoaded = true;
        MergeWorkspaceIndex(node);
        OnPropertyChanged(nameof(WorkspaceIndex));
        OnPropertyChanged(nameof(RootChildren));
    }

    public void SelectNode(WorkspaceTreeNode node)
    {
        _isSelectingNode = true;
        SelectionStateChanged?.Invoke(this, true);
        try
        {
            foreach (var indexedNode in _workspaceIndex.Values)
                indexedNode.IsSelected = false;

            for (var current = node; current is not null; current = current.Parent)
                current.IsExpanded = true;

            node.IsSelected = true;
        }
        finally
        {
            _isSelectingNode = false;
            SelectionStateChanged?.Invoke(this, false);
        }
    }

    public void RemoveNode(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!_workspaceIndex.Remove(fullPath, out var node))
            return;

        node.Parent?.Children.Remove(node);
        OnPropertyChanged(nameof(WorkspaceIndex));
        OnPropertyChanged(nameof(RootChildren));
    }

    public void Dispose()
    {
        _folderScanCts?.Cancel();
        _folderScanCts?.Dispose();
        _folderScanCts = null;
    }

    private void ApplyWorkspaceResult(string folderPath, FolderWorkspaceResult result)
    {
        WorkspaceFolderPath = Path.GetFullPath(folderPath);
        Root = result.Root;
        _workspaceIndex = BuildShallowWorkspaceIndex(result.Root);
        OnPropertyChanged(nameof(WorkspaceIndex));
        OnPropertyChanged(nameof(RootChildren));
    }

    private static Dictionary<string, WorkspaceTreeNode> BuildShallowWorkspaceIndex(WorkspaceTreeNode root)
    {
        var index = new Dictionary<string, WorkspaceTreeNode>(StringComparer.OrdinalIgnoreCase)
        {
            [root.FullPath] = root
        };

        foreach (var child in root.Children)
            index[child.FullPath] = child;

        return index;
    }

    private void MergeWorkspaceIndex(WorkspaceTreeNode node)
    {
        void Visit(WorkspaceTreeNode current)
        {
            _workspaceIndex[current.FullPath] = current;
            foreach (var child in current.Children)
                Visit(child);
        }

        Visit(node);
    }
}
