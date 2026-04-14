using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;
using Microsoft.Win32;
using WpfMarkdownEditor.Wpf.Theming;
using WpfMarkdownEditor.Sample.Helpers;

namespace WpfMarkdownEditor.Sample;

public partial class MainWindow : Window
{
    private bool _isDarkTheme;

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

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        IntPtr hWnd = new WindowInteropHelper(this).Handle;

        // Enable Mica
        int backdropType = (int)Win32Interop.DWM_SYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW;
        Win32Interop.DwmSetWindowAttribute(hWnd, Win32Interop.DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));

        // Enable Immersive Dark Mode
        int darkMode = 1;
        Win32Interop.DwmSetWindowAttribute(hWnd, Win32Interop.DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
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
        if (!_isDarkTheme) return;
        _isDarkTheme = false;
        BtnLight.Tag = "Active";
        BtnDark.Tag = null;
        ApplyWindowTheme(false);
        Editor.ApplyTheme(EditorTheme.Light);
        StatusText.Text = "Light theme";
    }

    private void OnDarkTheme(object sender, RoutedEventArgs e)
    {
        if (_isDarkTheme) return;
        _isDarkTheme = true;
        BtnDark.Tag = "Active";
        BtnLight.Tag = null;
        ApplyWindowTheme(true);
        Editor.ApplyTheme(EditorTheme.Dark);
        StatusText.Text = "Dark theme";
    }

    private void OnTogglePreview(object sender, RoutedEventArgs e)
    {
        Editor.ShowPreview = !Editor.ShowPreview;
        StatusText.Text = Editor.ShowPreview ? "Preview visible" : "Preview hidden";
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void OnMaximizeClick(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
            SystemCommands.RestoreWindow(this);
        else
            SystemCommands.MaximizeWindow(this);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);

    private void OnTitleBarMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
private void ApplyWindowTheme(bool dark)
{
    var r = Application.Current.Resources;

    // Window with slight transparency for Mica to shine through
    SetBrushColor(r, "WindowBackgroundBrush", dark ? "#E0202020" : "#E0F3F3F3");
    SetBrushColor(r, "WindowBorderBrush", dark ? "#30FFFFFF" : "#40000000"); // Semi-transparent border

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
}
