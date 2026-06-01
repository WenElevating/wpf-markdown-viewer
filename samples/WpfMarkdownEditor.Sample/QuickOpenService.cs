namespace WpfMarkdownEditor.Sample;

public enum QuickOpenSource
{
    Recent,
    Workspace
}

public sealed record QuickOpenItem(string Name, string Path, QuickOpenSource Source);

public sealed class QuickOpenService
{
    public IReadOnlyList<QuickOpenItem> BuildItems(IEnumerable<RecentFileEntry> recentFiles, WorkspaceTreeNode? workspaceRoot)
    {
        var items = new Dictionary<string, QuickOpenItem>(StringComparer.OrdinalIgnoreCase);
        if (workspaceRoot is not null)
            AddWorkspaceFiles(workspaceRoot, items);

        foreach (var file in recentFiles)
        {
            if (!items.ContainsKey(file.Path))
                items[file.Path] = new QuickOpenItem(System.IO.Path.GetFileName(file.Path), file.Path, QuickOpenSource.Recent);
        }

        return items.Values.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public IReadOnlyList<QuickOpenItem> Filter(IEnumerable<QuickOpenItem> items, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return items.Take(50).ToList();

        return items
            .Where(item => item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                           || item.Path.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(50)
            .ToList();
    }

    private static void AddWorkspaceFiles(WorkspaceTreeNode node, IDictionary<string, QuickOpenItem> items)
    {
        if (!node.IsDirectory)
        {
            items[node.FullPath] = new QuickOpenItem(node.Name, node.FullPath, QuickOpenSource.Workspace);
            return;
        }

        foreach (var child in node.Children)
            AddWorkspaceFiles(child, items);
    }
}
