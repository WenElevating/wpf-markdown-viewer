using System.Windows;
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

    // Editor-specific styling (thinner, softer for readability)
    public Color EditorForegroundColor { get; init; } = Color.FromRgb(0x24, 0x29, 0x2f);
    public Color EditorCaretColor { get; init; } = Color.FromRgb(0x24, 0x29, 0x2f);
    public FontWeight EditorFontWeight { get; init; } = FontWeights.Light;

    // Typography
    public FontFamily BodyFont { get; init; } = new("Segoe UI");
    public FontFamily HeadingFont { get; init; } = new("Segoe UI Semibold");
    public FontFamily CodeFont { get; init; } = new("Consolas");

    // Block-specific colors
    public Color HeadingColor { get; init; } = Color.FromRgb(0x1a, 0x1a, 0x1a);
    public Color CodeBackground { get; init; } = Color.FromRgb(0xf5, 0xf5, 0xf5);
    public Color CodeForeground { get; init; } = Color.FromRgb(0x24, 0x24, 0x24);
    public Color BlockquoteBorder { get; init; } = Color.FromRgb(0xdd, 0xdd, 0xdd);
    public Color BlockquoteBackground { get; init; } = Color.FromRgb(0xf9, 0xf9, 0xf9);
    public Color LinkColor { get; init; } = Color.FromRgb(0x00, 0x66, 0xcc);
    public Color TableHeaderBackground { get; init; } = Color.FromRgb(0xf0, 0xf0, 0xf0);
    public Color TableBorderColor { get; init; } = Color.FromRgb(0xdd, 0xdd, 0xdd);
    public Color ThematicBreakColor { get; init; } = Color.FromRgb(0xee, 0xee, 0xee);

    // GitHub-style rendering
    public Color HeadingBorderColor { get; init; } = Color.FromRgb(0xd8, 0xde, 0xe4);
    public bool ShowHeadingBorders { get; init; } = false;
    public Color InlineCodeBackground { get; init; } = Color.FromRgb(0xef, 0xf1, 0xf3);
    public Color InlineCodeForeground { get; init; } = Color.FromRgb(0x24, 0x24, 0x24);
    public Color CodeBlockBorderColor { get; init; } = Color.FromRgb(0xd0, 0xd7, 0xde);
    public Color TableAltRowBackground { get; init; } = Color.FromRgb(0xf6, 0xf8, 0xfa);

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
        EditorCaretColor = Color.FromRgb(0xd4, 0xd4, 0xd4),
        HeadingColor = Color.FromRgb(0xff, 0xff, 0xff),
        CodeBackground = Color.FromRgb(0x2d, 0x2d, 0x2d),
        CodeForeground = Color.FromRgb(0xd4, 0xd4, 0xd4),
        BlockquoteBackground = Color.FromRgb(0x28, 0x28, 0x28),
        BlockquoteBorder = Color.FromRgb(0x55, 0x55, 0x55),
        LinkColor = Color.FromRgb(0x6c, 0xb6, 0xff),
        TableHeaderBackground = Color.FromRgb(0x2d, 0x2d, 0x2d),
        TableBorderColor = Color.FromRgb(0x55, 0x55, 0x55),
        ThematicBreakColor = Color.FromRgb(0x55, 0x55, 0x55),
        InlineCodeBackground = Color.FromRgb(0x2d, 0x2d, 0x2d),
        InlineCodeForeground = Color.FromRgb(0xd4, 0xd4, 0xd4),
        CodeBlockBorderColor = Color.FromRgb(0x55, 0x55, 0x55),
        TableAltRowBackground = Color.FromRgb(0x28, 0x28, 0x28),
    };

    /// <summary>Typora-inspired GitHub Light theme matching github.com markdown rendering.</summary>
    public static EditorTheme GitHub { get; } = new()
    {
        Name = "GitHub",
        BackgroundColor = Color.FromRgb(0xfa, 0xfb, 0xfc),
        ForegroundColor = Color.FromRgb(0x24, 0x29, 0x2f),
        EditorCaretColor = Color.FromRgb(0x24, 0x29, 0x2f),
        BodyFont = new("Segoe UI"),
        HeadingFont = new("Segoe UI Semibold"),
        CodeFont = new("Consolas"),
        HeadingColor = Color.FromRgb(0x1f, 0x23, 0x28),
        CodeBackground = Color.FromRgb(0xf6, 0xf8, 0xfa),
        CodeForeground = Color.FromRgb(0x24, 0x29, 0x2f),
        BlockquoteBorder = Color.FromRgb(0xd0, 0xd7, 0xde),
        BlockquoteBackground = Color.FromRgb(0xf6, 0xf8, 0xfa),
        LinkColor = Color.FromRgb(0x09, 0x69, 0xda),
        TableHeaderBackground = Color.FromRgb(0xf6, 0xf8, 0xfa),
        TableBorderColor = Color.FromRgb(0xd0, 0xd7, 0xde),
        ThematicBreakColor = Color.FromRgb(0xd8, 0xde, 0xe4),
        HeadingBorderColor = Color.FromRgb(0xd8, 0xde, 0xe4),
        ShowHeadingBorders = true,
        InlineCodeBackground = Color.FromRgb(0xef, 0xf1, 0xf3),
        InlineCodeForeground = Color.FromRgb(0x24, 0x29, 0x2f),
        CodeBlockBorderColor = Color.FromRgb(0xd0, 0xd7, 0xde),
        TableAltRowBackground = Color.FromRgb(0xf6, 0xf8, 0xfa),
        ParagraphSpacing = 16,
        HeadingMarginTop = 24,
        HeadingMarginBottom = 12,
        BlockquotePaddingLeft = 16,
        BlockquoteBorderWidth = 4,
    };

    /// <summary>Typora-inspired GitHub Dark theme.</summary>
    public static EditorTheme GitHubDark { get; } = new()
    {
        Name = "GitHub Dark",
        BackgroundColor = Color.FromRgb(0x0d, 0x11, 0x17),
        ForegroundColor = Color.FromRgb(0xe6, 0xed, 0xf3),
        EditorForegroundColor = Color.FromRgb(0xe6, 0xed, 0xf3),
        EditorCaretColor = Color.FromRgb(0xe6, 0xed, 0xf3),
        BodyFont = new("Segoe UI"),
        HeadingFont = new("Segoe UI Semibold"),
        CodeFont = new("Consolas"),
        HeadingColor = Color.FromRgb(0xf0, 0xf6, 0xfc),
        CodeBackground = Color.FromRgb(0x16, 0x1b, 0x22),
        CodeForeground = Color.FromRgb(0xe6, 0xed, 0xf3),
        BlockquoteBorder = Color.FromRgb(0x3b, 0x43, 0x4b),
        BlockquoteBackground = Color.FromRgb(0x16, 0x1b, 0x22),
        LinkColor = Color.FromRgb(0x58, 0xa6, 0xff),
        TableHeaderBackground = Color.FromRgb(0x16, 0x1b, 0x22),
        TableBorderColor = Color.FromRgb(0x30, 0x36, 0x3d),
        ThematicBreakColor = Color.FromRgb(0x21, 0x26, 0x2d),
        HeadingBorderColor = Color.FromRgb(0x21, 0x26, 0x2d),
        ShowHeadingBorders = true,
        InlineCodeBackground = Color.FromRgb(0x16, 0x1b, 0x22),
        InlineCodeForeground = Color.FromRgb(0xe6, 0xed, 0xf3),
        CodeBlockBorderColor = Color.FromRgb(0x30, 0x36, 0x3d),
        TableAltRowBackground = Color.FromRgb(0x16, 0x1b, 0x22),
        ParagraphSpacing = 16,
        HeadingMarginTop = 24,
        HeadingMarginBottom = 12,
        BlockquotePaddingLeft = 16,
        BlockquoteBorderWidth = 4,
    };

    /// <summary>Claude-inspired warm terracotta theme.</summary>
    public static EditorTheme Claude { get; } = new()
    {
        Name = "Claude",
        BackgroundColor = Color.FromRgb(0xfa, 0xf9, 0xf6),
        ForegroundColor = Color.FromRgb(0x2d, 0x2d, 0x2d),
        EditorForegroundColor = Color.FromRgb(0x2d, 0x2d, 0x2d),
        EditorCaretColor = Color.FromRgb(0x2d, 0x2d, 0x2d),
        BodyFont = new("Segoe UI"),
        HeadingFont = new("Segoe UI Semibold"),
        CodeFont = new("Consolas"),
        HeadingColor = Color.FromRgb(0x1a, 0x1a, 0x1a),
        CodeBackground = Color.FromRgb(0xf0, 0xed, 0xe8),
        CodeForeground = Color.FromRgb(0x2d, 0x2d, 0x2d),
        BlockquoteBorder = Color.FromRgb(0xd9, 0x77, 0x57),
        BlockquoteBackground = Color.FromRgb(0xfd, 0xf5, 0xf0),
        LinkColor = Color.FromRgb(0xc4, 0x62, 0x2d),
        TableHeaderBackground = Color.FromRgb(0xf0, 0xed, 0xe8),
        TableBorderColor = Color.FromRgb(0xe0, 0xd9, 0xd0),
        ThematicBreakColor = Color.FromRgb(0xe0, 0xd9, 0xd0),
        HeadingBorderColor = Color.FromRgb(0xe0, 0xd9, 0xd0),
        ShowHeadingBorders = true,
        InlineCodeBackground = Color.FromRgb(0xf0, 0xed, 0xe8),
        InlineCodeForeground = Color.FromRgb(0x2d, 0x2d, 0x2d),
        CodeBlockBorderColor = Color.FromRgb(0xe0, 0xd9, 0xd0),
        TableAltRowBackground = Color.FromRgb(0xf5, 0xf0, 0xeb),
        ParagraphSpacing = 16,
        HeadingMarginTop = 24,
        HeadingMarginBottom = 12,
        BlockquotePaddingLeft = 16,
        BlockquoteBorderWidth = 4,
    };

    /// <summary>Claude Dark — warm terracotta accent on deep charcoal.</summary>
    public static EditorTheme ClaudeDark { get; } = new()
    {
        Name = "Claude Dark",
        BackgroundColor = Color.FromRgb(0x1c, 0x1c, 0x1e),
        ForegroundColor = Color.FromRgb(0xe8, 0xe2, 0xd9),
        EditorForegroundColor = Color.FromRgb(0xe8, 0xe2, 0xd9),
        EditorCaretColor = Color.FromRgb(0xe8, 0xe2, 0xd9),
        BodyFont = new("Segoe UI"),
        HeadingFont = new("Segoe UI Semibold"),
        CodeFont = new("Consolas"),
        HeadingColor = Color.FromRgb(0xff, 0xff, 0xff),
        CodeBackground = Color.FromRgb(0x2a, 0x28, 0x26),
        CodeForeground = Color.FromRgb(0xe8, 0xe2, 0xd9),
        BlockquoteBorder = Color.FromRgb(0xd9, 0x77, 0x57),
        BlockquoteBackground = Color.FromRgb(0x26, 0x22, 0x20),
        LinkColor = Color.FromRgb(0xe8, 0x91, 0x5a),
        TableHeaderBackground = Color.FromRgb(0x2a, 0x28, 0x26),
        TableBorderColor = Color.FromRgb(0x3a, 0x36, 0x32),
        ThematicBreakColor = Color.FromRgb(0x3a, 0x36, 0x32),
        HeadingBorderColor = Color.FromRgb(0x3a, 0x36, 0x32),
        ShowHeadingBorders = true,
        InlineCodeBackground = Color.FromRgb(0x2a, 0x28, 0x26),
        InlineCodeForeground = Color.FromRgb(0xe8, 0xe2, 0xd9),
        CodeBlockBorderColor = Color.FromRgb(0x3a, 0x36, 0x32),
        TableAltRowBackground = Color.FromRgb(0x24, 0x22, 0x20),
        ParagraphSpacing = 16,
        HeadingMarginTop = 24,
        HeadingMarginBottom = 12,
        BlockquotePaddingLeft = 16,
        BlockquoteBorderWidth = 4,
    };
}
