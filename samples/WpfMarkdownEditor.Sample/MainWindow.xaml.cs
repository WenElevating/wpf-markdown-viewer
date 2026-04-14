using System.Windows;
using Microsoft.Win32;
using WpfMarkdownEditor.Wpf.Theming;

namespace WpfMarkdownEditor.Sample;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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

    private void OnLightTheme(object sender, RoutedEventArgs e)
    {
        Editor.ApplyTheme(EditorTheme.Light);
        StatusText.Text = "Theme: Light";
    }

    private void OnDarkTheme(object sender, RoutedEventArgs e)
    {
        Editor.ApplyTheme(EditorTheme.Dark);
        StatusText.Text = "Theme: Dark";
    }

    private void OnTogglePreview(object sender, RoutedEventArgs e)
    {
        Editor.ShowPreview = !Editor.ShowPreview;
        StatusText.Text = Editor.ShowPreview ? "Preview: Visible" : "Preview: Hidden";
    }
}
