using System.Windows;
using System.Windows.Controls;
using WpfMarkdownEditor.Wpf.Controls;

namespace WpfMarkdownEditor.Sample.Controls;

public partial class FormattingToolbar : UserControl
{
    public static readonly DependencyProperty EditorProperty =
        DependencyProperty.Register(nameof(Editor), typeof(MarkdownEditor), typeof(FormattingToolbar),
            new PropertyMetadata(null));

    public MarkdownEditor? Editor
    {
        get => (MarkdownEditor?)GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    public event Action<bool>? ThemeChanged;

    public FormattingToolbar()
    {
        InitializeComponent();
    }

    public void UpdateEditorBinding()
    {
        if (Editor != null)
        {
            PreviewToggle.IsChecked = Editor.ShowPreview;
        }
    }

    // Format group
    private void OnBold(object sender, RoutedEventArgs e) => Editor?.WrapSelection("**", "**");
    private void OnItalic(object sender, RoutedEventArgs e) => Editor?.WrapSelection("*", "*");
    private void OnCode(object sender, RoutedEventArgs e) => Editor?.WrapSelection("`", "`");
    private void OnStrikethrough(object sender, RoutedEventArgs e) => Editor?.WrapSelection("~~", "~~");

    // Heading group
    private void OnH1(object sender, RoutedEventArgs e) => Editor?.InsertAtCursor("# ");
    private void OnH2(object sender, RoutedEventArgs e) => Editor?.InsertAtCursor("## ");
    private void OnH3(object sender, RoutedEventArgs e) => Editor?.InsertAtCursor("### ");

    // List group
    private void OnUnorderedList(object sender, RoutedEventArgs e) => Editor?.InsertAtCursor("- ");
    private void OnOrderedList(object sender, RoutedEventArgs e) => Editor?.InsertAtCursor("1. ");
    private void OnBlockquote(object sender, RoutedEventArgs e) => Editor?.InsertAtCursor("> ");

    // Insert group (template insertion, no dialogs)
    private void OnInsertLink(object sender, RoutedEventArgs e) => Editor?.InsertAtCursor("[text](url)");
    private void OnInsertImage(object sender, RoutedEventArgs e) => Editor?.InsertAtCursor("![alt](url)");
    private void OnInsertTable(object sender, RoutedEventArgs e) => Editor?.InsertAtCursor("| Header | Header |\n| ------ | ------ |\n| Cell | Cell |");

    // Theme toggle
    private void OnLightTheme(object sender, RoutedEventArgs e)
    {
        if (BtnLight.Tag?.ToString() == "Active") return;
        BtnLight.Tag = "Active";
        BtnDark.Tag = null;
        ThemeChanged?.Invoke(false);
    }

    private void OnDarkTheme(object sender, RoutedEventArgs e)
    {
        if (BtnDark.Tag?.ToString() == "Active") return;
        BtnDark.Tag = "Active";
        BtnLight.Tag = null;
        ThemeChanged?.Invoke(true);
    }

    // Preview toggle
    private void OnTogglePreview(object sender, RoutedEventArgs e)
    {
        if (Editor != null)
        {
            Editor.ShowPreview = PreviewToggle.IsChecked == true;
        }
    }
}
