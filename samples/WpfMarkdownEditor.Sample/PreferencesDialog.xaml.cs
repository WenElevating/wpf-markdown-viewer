using System.Windows;
using WpfMarkdownEditor.Wpf.Localization;

namespace WpfMarkdownEditor.Sample;

public partial class PreferencesDialog : Window
{
    private sealed record LanguageOption(string Name, SupportedLanguage Language);

    public SupportedLanguage SelectedLanguage { get; private set; }

    public string SelectedThemeName { get; private set; }

    public bool OpenTranslationSettingsRequested { get; private set; }

    public PreferencesDialog(LocalizationService localizer, SupportedLanguage currentLanguage, string currentThemeName)
    {
        InitializeComponent();

        var languageOptions = SupportedLanguage.All
            .Select(language => new LanguageOption(localizer.GetString(language.DisplayKey), language))
            .ToList();

        LanguageBox.ItemsSource = languageOptions;
        LanguageBox.SelectedItem = languageOptions.First(option => option.Language.Equals(currentLanguage));
        ThemeBox.ItemsSource = ThemeCatalog.Themes;
        ThemeBox.SelectedItem = ThemeCatalog.Themes.First(theme => theme.Name == currentThemeName);

        SelectedLanguage = currentLanguage;
        SelectedThemeName = currentThemeName;
    }

    private void OnTranslationSettingsClick(object sender, RoutedEventArgs e)
    {
        OpenTranslationSettingsRequested = true;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (LanguageBox.SelectedItem is LanguageOption language)
            SelectedLanguage = language.Language;

        if (ThemeBox.SelectedItem is ThemeEntry theme)
            SelectedThemeName = theme.Name;

        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
