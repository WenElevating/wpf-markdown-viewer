using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using WpfMarkdownEditor.Wpf.Theming;

namespace WpfMarkdownEditor.Sample;

public partial class MainWindow : Window
{
    private bool _isDarkTheme;

    public MainWindow()
    {
        InitializeComponent();

        // Apply GitHub theme by default
        Editor.ApplyTheme(EditorTheme.GitHub);

        Editor.Markdown = """
            # Welcome to WPF Markdown Editor

            ## Features

            - **Real-time preview** with less than 50ms latency
            - *Italic* and **bold** text
            - `Inline code` support
            - [Links](https://example.com)

            ## Code Block

            ```csharp
            public class HelloWorld
            {
                public static void Main()
                {
                    Console.WriteLine("Hello, Markdown!");
                }
            }
            ```

            ## Table

            | Feature | Status |
            | ------- | ------ |
            | Parser | Done |
            | Renderer | Done |
            | Theme | Done |

            ## Blockquote

            > This is a blockquote.
            > It supports **inline formatting**.

            ---

            *Built with .NET 8 and WPF. Zero external dependencies.*
            """;
    }

    #region File Operations

    private void OnOpenFile(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Markdown files (*.md)|*.md|All files (*.*)|*.*",
            DefaultExt = ".md"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                Editor.LoadFile(dialog.FileName);
                StatusText.Text = $"Loaded: {dialog.FileName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load file: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void OnSaveFile(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Markdown files (*.md)|*.md|All files (*.*)|*.*",
            DefaultExt = ".md"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await Editor.SaveFileAsync(dialog.FileName);
                StatusText.Text = $"Saved: {dialog.FileName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save file: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    #endregion

    #region Theme

    private void OnLightTheme(object sender, RoutedEventArgs e)
    {
        if (!_isDarkTheme) return;
        _isDarkTheme = false;
        BtnLight.Tag = "Active";
        BtnDark.Tag = null;
        ApplyWindowTheme(false);
        Editor.ApplyTheme(EditorTheme.GitHub);
        StatusText.Text = "GitHub Light theme";
    }

    private void OnDarkTheme(object sender, RoutedEventArgs e)
    {
        if (_isDarkTheme) return;
        _isDarkTheme = true;
        BtnDark.Tag = "Active";
        BtnLight.Tag = null;
        ApplyWindowTheme(true);
        Editor.ApplyTheme(EditorTheme.GitHubDark);
        StatusText.Text = "GitHub Dark theme";
    }

    private void ApplyWindowTheme(bool dark)
    {
        var r = Application.Current.Resources;
        SetBrushColor(r, "WindowBackgroundBrush", dark ? "#1E1E1E" : "#FFFFFF");
        SetBrushColor(r, "SurfaceBackgroundBrush", dark ? "#282828" : "#FAFAFA");
        SetBrushColor(r, "CardBackgroundBrush", dark ? "#2D2D2D" : "#FFFFFF");
        SetBrushColor(r, "TextPrimaryBrush", dark ? "#FFFFFF" : "#1A1A1A");
        SetBrushColor(r, "TextSecondaryBrush", dark ? "#9E9E9E" : "#616161");
        SetBrushColor(r, "AccentBrush", dark ? "#60CDFF" : "#005FB8");
        SetBrushColor(r, "DividerBrush", dark ? "#3D3D3D" : "#E5E5E5");
        SetBrushColor(r, "HoverBackgroundBrush", dark ? "#383838" : "#F5F5F5");
        SetBrushColor(r, "PressedBackgroundBrush", dark ? "#434343" : "#E8E8E8");
        SetBrushColor(r, "SegmentBackgroundBrush", dark ? "#404040" : "#E0E0E0");
    }

    private static void SetBrushColor(ResourceDictionary r, string key, string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        r[key] = new SolidColorBrush(color);
    }

    #endregion

    #region Formatting Toolbar

    private void OnHeading1(object sender, RoutedEventArgs e) => Editor.ToggleLinePrefix("#");
    private void OnHeading2(object sender, RoutedEventArgs e) => Editor.ToggleLinePrefix("##");
    private void OnHeading3(object sender, RoutedEventArgs e) => Editor.ToggleLinePrefix("###");
    private void OnBold(object sender, RoutedEventArgs e) => Editor.WrapSelection("**", "**");
    private void OnItalic(object sender, RoutedEventArgs e) => Editor.WrapSelection("*", "*");
    private void OnStrikethrough(object sender, RoutedEventArgs e) => Editor.WrapSelection("~~", "~~");
    private void OnInlineCode(object sender, RoutedEventArgs e) => Editor.WrapSelection("`", "`");
    private void OnLink(object sender, RoutedEventArgs e) => Editor.WrapSelection("[", "](url)");
    private void OnQuote(object sender, RoutedEventArgs e) => Editor.ToggleLinePrefix(">");
    private void OnUnorderedList(object sender, RoutedEventArgs e) => Editor.ToggleLinePrefix("-");
    private void OnOrderedList(object sender, RoutedEventArgs e) => Editor.ToggleLinePrefix("1.");
    private void OnCodeBlock(object sender, RoutedEventArgs e) => Editor.WrapSelection("```\n", "\n```");
    private void OnTable(object sender, RoutedEventArgs e) => Editor.InsertText("\n| Column 1 | Column 2 | Column 3 |\n| -------- | -------- | -------- |\n| Cell 1   | Cell 2   | Cell 3   |\n");
    private void OnHorizontalRule(object sender, RoutedEventArgs e) => Editor.InsertText("\n---\n");

    #endregion
}
