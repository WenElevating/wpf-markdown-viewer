namespace WpfMarkdownEditor.Wpf.Localization;

public sealed class SupportedLanguage : IEquatable<SupportedLanguage>
{
    public static readonly SupportedLanguage English = new(
        "en-US",
        "Language.English",
        "pack://application:,,,/WpfMarkdownEditor.Wpf;component/Resources/Localization.en-US.xaml");

    public static readonly SupportedLanguage Chinese = new(
        "zh-CN",
        "Language.Chinese",
        "pack://application:,,,/WpfMarkdownEditor.Wpf;component/Resources/Localization.zh-CN.xaml");

    public static IReadOnlyList<SupportedLanguage> All { get; } = [English, Chinese];

    public SupportedLanguage(string code, string displayKey, string resourceUri)
    {
        Code = code;
        DisplayKey = displayKey;
        ResourceUri = resourceUri;
    }

    public string Code { get; }

    public string DisplayKey { get; }

    public string ResourceUri { get; }

    public static SupportedLanguage? FromCode(string? code) =>
        All.FirstOrDefault(language => string.Equals(language.Code, code, StringComparison.Ordinal));

    public bool Equals(SupportedLanguage? other) =>
        other is not null && string.Equals(Code, other.Code, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as SupportedLanguage);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Code);

    public override string ToString() => Code;
}
