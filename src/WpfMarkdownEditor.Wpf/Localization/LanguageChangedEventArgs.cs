namespace WpfMarkdownEditor.Wpf.Localization;

public sealed class LanguageChangedEventArgs : EventArgs
{
    public LanguageChangedEventArgs(SupportedLanguage? oldLanguage, SupportedLanguage newLanguage)
    {
        OldLanguage = oldLanguage;
        NewLanguage = newLanguage;
    }

    public SupportedLanguage? OldLanguage { get; }

    public SupportedLanguage NewLanguage { get; }
}
