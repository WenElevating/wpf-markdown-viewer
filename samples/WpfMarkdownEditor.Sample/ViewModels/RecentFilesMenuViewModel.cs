using System.IO;

namespace WpfMarkdownEditor.Sample.ViewModels;

public sealed class RecentFilesMenuViewModel : ObservableObject, IDisposable
{
    private const int DisplayPathMaxLength = 56;
    private readonly RecentFilesService _recentFilesService;
    private IReadOnlyList<RecentFileEntry> _entries = [];
    private bool _isCacheLoaded;
    private CancellationTokenSource? _loadCts;
    private CancellationTokenSource? _hoverCts;
    private CancellationTokenSource? _closeCts;

    public RecentFilesMenuViewModel(RecentFilesService recentFilesService)
    {
        _recentFilesService = recentFilesService;
    }

    public IReadOnlyList<RecentFileEntry> Entries
    {
        get => _entries;
        private set => SetProperty(ref _entries, value);
    }

    public bool IsCacheLoaded
    {
        get => _isCacheLoaded;
        private set => SetProperty(ref _isCacheLoaded, value);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        _loadCts?.Cancel();
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loadCts = linkedCts;

        try
        {
            var entries = await _recentFilesService.LoadFilesSnapshotAsync(linkedCts.Token);
            if (linkedCts.IsCancellationRequested)
                return;

            Entries = entries;
            IsCacheLoaded = true;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(_loadCts, linkedCts))
                _loadCts = null;

            linkedCts.Dispose();
        }
    }

    public CancellationTokenSource StartHoverDelay()
    {
        CancelHoverDelay();
        _hoverCts = new CancellationTokenSource();
        return _hoverCts;
    }

    public void CancelHoverDelay()
    {
        _hoverCts?.Cancel();
    }

    public void CompleteHoverDelay(CancellationTokenSource cts)
    {
        if (!ReferenceEquals(_hoverCts, cts))
            return;

        _hoverCts = null;
        cts.Dispose();
    }

    public CancellationTokenSource StartCloseDelay()
    {
        CancelCloseDelay();
        _closeCts = new CancellationTokenSource();
        return _closeCts;
    }

    public void CancelCloseDelay()
    {
        _closeCts?.Cancel();
    }

    public void CompleteCloseDelay(CancellationTokenSource cts)
    {
        if (!ReferenceEquals(_closeCts, cts))
            return;

        _closeCts = null;
        cts.Dispose();
    }

    public void CancelLoad() => _loadCts?.Cancel();

    public void AddOrRefresh(string path)
    {
        _recentFilesService.AddOrRefreshFile(path);
        var fullPath = Path.GetFullPath(path);
        Entries = Entries
            .Where(entry => !string.Equals(entry.Path, fullPath, StringComparison.OrdinalIgnoreCase))
            .Prepend(new RecentFileEntry(fullPath, DateTime.UtcNow))
            .Take(20)
            .ToList();
    }

    public void RemoveFromCache(string path)
    {
        var fullPath = Path.GetFullPath(path);
        Entries = Entries
            .Where(entry => !string.Equals(entry.Path, fullPath, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public void Clear()
    {
        CancelLoad();
        _recentFilesService.ClearFiles();
        Entries = [];
        IsCacheLoaded = true;
    }

    public static string FormatDisplayPath(string path)
    {
        if (path.Length <= DisplayPathMaxLength)
            return path;

        var root = Path.GetPathRoot(path);
        var fileName = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(fileName))
            return path;

        return $"{root}...{Path.DirectorySeparatorChar}{fileName}";
    }

    public void Dispose()
    {
        _loadCts?.Cancel();
        _hoverCts?.Cancel();
        _closeCts?.Cancel();
        _loadCts = null;
        _hoverCts = null;
        _closeCts = null;
    }
}
