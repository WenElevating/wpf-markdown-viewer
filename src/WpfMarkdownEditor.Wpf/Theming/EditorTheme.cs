using System.Windows.Media;

namespace WpfMarkdownEditor.Wpf.Theming;

/// <summary>
/// Defines visual styling for the Markdown editor. Immutable — create new instances for changes.
/// </summary>
public sealed class EditorTheme
{
    public string Name { get; init; } = "Default";

    // Document colors
    public Color BackgroundColor { get; init; } = Colors.White;
    public Color ForegroundColor { get; init; } = Colors.Black;

    // Typography
    public FontFamily BodyFont { get; init; } = new("Segoe UI");
    public FontFamily HeadingFont { get; init; } = new("Segoe UI Semibold");
    public FontFamily CodeFont { get; init; } = new("Consolas");

    // Block-specific colors
    public Color HeadingColor { get; init; } = Color.FromRgb(0x1a, 0x1a, 0x1a);
    public Color CursorColor { get; init; } = Colors.Black;
    public Color CodeBackground { get; init; } = Color.FromRgb(0xf5, 0xf5, 0xf5);
    public Color CodeForeground { get; init; } = Color.FromRgb(0x24, 0x24, 0x24);
    public Color BlockquoteBorder { get; init; } = Color.FromRgb(0xdd, 0xdd, 0xdd);
    public Color BlockquoteBackground { get; init; } = Color.FromRgb(0xf9, 0xf9, 0xf9);
    public Color LinkColor { get; init; } = Color.FromRgb(0x00, 0x66, 0xcc);
    public Color TableHeaderBackground { get; init; } = Color.FromRgb(0xf0, 0xf0, 0xf0);
    public Color TableBorderColor { get; init; } = Color.FromRgb(0xdd, 0xdd, 0xdd);
    public Color ThematicBreakColor { get; init; } = Color.FromRgb(0xee, 0xee, 0xee);

    // Spacing
    public double ParagraphSpacing { get; init; } = 12;
    public double HeadingMarginTop { get; init; } = 24;
    public double HeadingMarginBottom { get; init; } = 8;
    public double BlockquotePaddingLeft { get; init; } = 16;
    public double BlockquoteBorderWidth { get; init; } = 4;

    // Built-in themes
    public static EditorTheme Light { get; } = new() { Name = "Light" };

    public static EditorTheme Dark { get; } = new()
    {
        Name = "Dark",
        BackgroundColor = Color.FromRgb(0x1e, 0x1e, 0x1e),
        ForegroundColor = Color.FromRgb(0xd4, 0xd4, 0xd4),
        HeadingColor = Color.FromRgb(0xff, 0xff, 0xff),
        CursorColor = Colors.White,
        CodeBackground = Color.FromRgb(0x2d, 0x2d, 0x2d),
        CodeForeground = Color.FromRgb(0xd4, 0xd4, 0xd4),
        BlockquoteBackground = Color.FromRgb(0x28, 0x28, 0x28),
        BlockquoteBorder = Color.FromRgb(0x55, 0x55, 0x55),
        LinkColor = Color.FromRgb(0x6c, 0xb6, 0xff),
        TableHeaderBackground = Color.FromRgb(0x2d, 0x2d, 0x2d),
        TableBorderColor = Color.FromRgb(0x55, 0x55, 0x55),
        ThematicBreakColor = Color.FromRgb(0x55, 0x55, 0x55),
    };
}
