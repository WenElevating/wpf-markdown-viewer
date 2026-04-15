using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfMarkdownEditor.Sample.Helpers;

namespace WpfMarkdownEditor.Sample.Controls;

public partial class TabBar : UserControl
{
    public TabBar()
    {
        InitializeComponent();
    }

    public TabManager? TabManager
    {
        get => (TabManager?)GetValue(TabManagerProperty);
        set => SetValue(TabManagerProperty, value);
    }

    public static readonly DependencyProperty TabManagerProperty =
        DependencyProperty.Register(
            nameof(TabManager),
            typeof(TabManager),
            typeof(TabBar),
            new PropertyMetadata(null, OnTabManagerChanged));

    public Helpers.TabItem? ActiveTab
    {
        get => (Helpers.TabItem?)GetValue(ActiveTabProperty);
        set => SetValue(ActiveTabProperty, value);
    }

    public static readonly DependencyProperty ActiveTabProperty =
        DependencyProperty.Register(
            nameof(ActiveTab),
            typeof(Helpers.TabItem),
            typeof(TabBar),
            new PropertyMetadata(null));

    private static void OnTabManagerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TabBar tabBar && e.NewValue is TabManager manager)
        {
            tabBar.DataContext = manager;
            tabBar.ActiveTab = manager.ActiveTab;
            manager.ActiveTabChanged += (s, args) => tabBar.ActiveTab = manager.ActiveTab;
        }
    }

    private void OnTabClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is Helpers.TabItem tab && TabManager != null)
        {
            TabManager.SwitchTab(tab);
        }
    }

    private void OnCloseTab(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Helpers.TabItem tab && TabManager != null)
        {
            _ = TabManager.CloseTabAsync(tab, Window.GetWindow(this));
        }
    }

    private void OnNewTab(object sender, RoutedEventArgs e)
    {
        if (TabManager != null)
        {
            TabManager.NewTab("Untitled", string.Empty);
        }
    }
}
