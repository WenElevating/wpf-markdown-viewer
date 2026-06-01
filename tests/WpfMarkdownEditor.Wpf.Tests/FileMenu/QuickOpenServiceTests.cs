using WpfMarkdownEditor.Sample;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.FileMenu;

public sealed class QuickOpenServiceTests
{
    [Fact]
    public void Filter_ReturnsRecentAndWorkspaceMatches()
    {
        var service = new QuickOpenService();
        var items = new[]
        {
            new QuickOpenItem("README.md", "C:\\Repo\\README.md", QuickOpenSource.Workspace),
            new QuickOpenItem("notes.md", "C:\\Docs\\notes.md", QuickOpenSource.Recent),
            new QuickOpenItem("draft.txt", "C:\\Docs\\draft.txt", QuickOpenSource.Recent)
        };

        var result = service.Filter(items, "note");

        var item = Assert.Single(result);
        Assert.Equal("notes.md", item.Name);
    }

    [Fact]
    public void BuildItems_DeduplicatesByPathWithWorkspaceFirst()
    {
        var service = new QuickOpenService();
        var recent = new[] { new RecentFileEntry("C:\\Repo\\README.md", DateTime.UtcNow) };
        var root = new WorkspaceTreeNode { Name = "Repo", FullPath = "C:\\Repo", IsDirectory = true };
        root.Children.Add(new WorkspaceTreeNode
        {
            Name = "README.md",
            FullPath = "C:\\Repo\\README.md",
            IsDirectory = false,
            Parent = root
        });

        var result = service.BuildItems(recent, root);

        var item = Assert.Single(result);
        Assert.Equal(QuickOpenSource.Workspace, item.Source);
    }
}
