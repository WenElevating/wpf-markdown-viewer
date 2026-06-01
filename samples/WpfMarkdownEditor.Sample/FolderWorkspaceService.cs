using System.IO;

namespace WpfMarkdownEditor.Sample;

public sealed record FolderScanOptions(int MaxMarkdownFiles = 5000, int MaxEntries = 50000, int MaxDepth = 12);

public sealed record FolderWorkspaceResult(
    WorkspaceTreeNode Root,
    bool IsTruncated,
    int MarkdownFileCount,
    int InspectedEntryCount,
    int SkippedDirectoryCount);

public sealed class FolderWorkspaceService
{
    private static readonly HashSet<string> MarkdownExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md",
        ".markdown",
        ".mdown"
    };

    private static readonly HashSet<string> SkippedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        "bin",
        "obj"
    };

    private readonly FolderScanOptions _options;

    public FolderWorkspaceService() : this(new FolderScanOptions()) { }

    public FolderWorkspaceService(FolderScanOptions options)
    {
        _options = options;
    }

    public Task<FolderWorkspaceResult> ScanAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Scan(folderPath, cancellationToken), cancellationToken);
    }

    private FolderWorkspaceResult Scan(string folderPath, CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(folderPath);
        var root = new WorkspaceTreeNode
        {
            Name = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            FullPath = fullPath,
            IsDirectory = true
        };
        var context = new ScanContext();
        ScanDirectory(root, fullPath, depth: 0, context, cancellationToken);
        return new FolderWorkspaceResult(
            root,
            context.IsTruncated,
            context.MarkdownFileCount,
            context.InspectedEntryCount,
            context.SkippedDirectoryCount);
    }

    private bool ScanDirectory(
        WorkspaceTreeNode parent,
        string directory,
        int depth,
        ScanContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (depth >= _options.MaxDepth)
        {
            context.IsTruncated = true;
            return true;
        }

        IEnumerable<string> entries;
        try
        {
            entries = Directory.EnumerateFileSystemEntries(directory);
        }
        catch (UnauthorizedAccessException)
        {
            context.SkippedDirectoryCount++;
            return false;
        }
        catch (IOException)
        {
            context.SkippedDirectoryCount++;
            return false;
        }

        var directories = new List<WorkspaceTreeNode>();
        var files = new List<WorkspaceTreeNode>();

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.InspectedEntryCount++;
            if (context.InspectedEntryCount > _options.MaxEntries)
            {
                context.IsTruncated = true;
                break;
            }

            if (Directory.Exists(entry))
            {
                if (ShouldSkipDirectory(entry))
                    continue;

                var node = new WorkspaceTreeNode
                {
                    Name = Path.GetFileName(entry),
                    FullPath = Path.GetFullPath(entry),
                    IsDirectory = true,
                    Parent = parent
                };
                if (ScanDirectory(node, entry, depth + 1, context, cancellationToken))
                    directories.Add(node);
                continue;
            }

            if (!MarkdownExtensions.Contains(Path.GetExtension(entry)))
                continue;

            if (context.MarkdownFileCount >= _options.MaxMarkdownFiles)
            {
                context.IsTruncated = true;
                break;
            }

            context.MarkdownFileCount++;
            files.Add(new WorkspaceTreeNode
            {
                Name = Path.GetFileName(entry),
                FullPath = Path.GetFullPath(entry),
                IsDirectory = false,
                Parent = parent
            });
        }

        foreach (var node in directories.OrderBy(node => node.Name, StringComparer.OrdinalIgnoreCase))
            parent.Children.Add(node);
        foreach (var node in files.OrderBy(node => node.Name, StringComparer.OrdinalIgnoreCase))
            parent.Children.Add(node);

        return parent.Children.Count > 0;
    }

    private static bool ShouldSkipDirectory(string path)
    {
        var name = Path.GetFileName(path);
        if (SkippedDirectoryNames.Contains(name))
            return true;

        try
        {
            return (File.GetAttributes(path) & FileAttributes.Hidden) == FileAttributes.Hidden;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    private sealed class ScanContext
    {
        public bool IsTruncated { get; set; }

        public int MarkdownFileCount { get; set; }

        public int InspectedEntryCount { get; set; }

        public int SkippedDirectoryCount { get; set; }
    }
}
