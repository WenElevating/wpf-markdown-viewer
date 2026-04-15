using System.Windows;
using System.Windows.Controls;
using WpfMarkdownEditor.Wpf.Controls;

namespace WpfMarkdownEditor.Sample.Controls;

public partial class SidePanel : UserControl
{
    private string _activeTab = "Explorer";

    public event Action<string>? FileSelected;

    public SidePanel()
    {
        InitializeComponent();
        ExplorerContent.FileSelected += path => FileSelected?.Invoke(path);
    }

    public void BindEditor(MarkdownEditor? editor)
        => OutlineContent.BindEditor(editor);

    public void SetActiveTab(string tabName)
    {
        _activeTab = tabName;
        ExplorerContent.Visibility = tabName == "Explorer" ? Visibility.Visible : Visibility.Collapsed;
        OutlineContent.Visibility  = tabName == "Outline"  ? Visibility.Visible : Visibility.Collapsed;
        OpenFolderButton.Visibility = tabName == "Explorer" ? Visibility.Visible : Visibility.Collapsed;
        UpdateTabVisualState();
    }

    private void OnExplorerTabClick(object sender, RoutedEventArgs e) => SetActiveTab("Explorer");
    private void OnOutlineTabClick(object sender, RoutedEventArgs e)  => SetActiveTab("Outline");

    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
        => ExplorerContent.OpenFolder();

    private void UpdateTabVisualState()
    {
        bool explorerActive = _activeTab == "Explorer";

        // Active indicator
        ExplorerActiveIndicator.Visibility = explorerActive ? Visibility.Visible : Visibility.Collapsed;
        OutlineActiveIndicator.Visibility  = explorerActive ? Visibility.Collapsed : Visibility.Visible;

        // Icon + text foreground
        var activeFg   = (System.Windows.Media.Brush)FindResource("AccentDefaultBrush");
        var inactiveFg = (System.Windows.Media.Brush)FindResource("TextTertiaryBrush");
        var activePrimary = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush");

        ExplorerTabIcon.Foreground = explorerActive ? activeFg   : inactiveFg;
        ExplorerTabText.Foreground = explorerActive ? activePrimary : inactiveFg;
        ExplorerTabText.FontWeight = explorerActive ? FontWeights.SemiBold : FontWeights.Normal;

        OutlineTabIcon.Foreground = explorerActive ? inactiveFg : activeFg;
        OutlineTabText.Foreground = explorerActive ? inactiveFg : activePrimary;
        OutlineTabText.FontWeight = explorerActive ? FontWeights.Normal : FontWeights.SemiBold;
    }
}
