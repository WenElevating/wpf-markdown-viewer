using System.Windows.Media;
using WpfMarkdownEditor.Wpf.Theming;

namespace WpfMarkdownEditor.Sample;

public sealed record ThemeEntry(string Name, EditorTheme Theme, Color DotColor, Color DotBorder, bool IsDark);

public static class ThemeCatalog
{
    public static IReadOnlyList<ThemeEntry> Themes { get; } =
    [
        new("GitHub", EditorTheme.GitHub, Color.FromRgb(0xfa, 0xfb, 0xfc), Color.FromRgb(0xd0, 0xd7, 0xde), false),
        new("GitHub Dark", EditorTheme.GitHubDark, Color.FromRgb(0x0d, 0x11, 0x17), Color.FromRgb(0x30, 0x36, 0x3d), true),
        new("Claude", EditorTheme.Claude, Color.FromRgb(0xfa, 0xf9, 0xf6), Color.FromRgb(0xd9, 0x77, 0x57), false),
        new("Claude Dark", EditorTheme.ClaudeDark, Color.FromRgb(0x1c, 0x1c, 0x1e), Color.FromRgb(0xd9, 0x77, 0x57), true),
        new("Light", EditorTheme.Light, Color.FromRgb(0xff, 0xff, 0xff), Color.FromRgb(0xdd, 0xdd, 0xdd), false),
        new("Dark", EditorTheme.Dark, Color.FromRgb(0x1e, 0x1e, 0x1e), Color.FromRgb(0x55, 0x55, 0x55), true),
    ];
}
