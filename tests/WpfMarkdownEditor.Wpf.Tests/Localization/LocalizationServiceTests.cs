using System.Globalization;
using WpfMarkdownEditor.Wpf.Localization;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.Localization;

public sealed class LocalizationServiceTests
{
    [Fact]
    public void FallbackLocalizer_WorksWithoutWpfApplication()
    {
        var localizer = FallbackStringLocalizer.Instance;

        Assert.Equal(SupportedLanguage.English, localizer.CurrentLanguage);
        Assert.Equal("Cancel", localizer.GetString("Common.Cancel"));
        Assert.Equal("Missing.Key", localizer.GetString("Missing.Key"));
    }

    [Fact]
    public void Format_UsesTemplateAndFailsSoftWhenArgumentsMismatch()
    {
        var localizer = FallbackStringLocalizer.Instance;

        Assert.Equal("Loaded: readme.md", localizer.Format("Status.FileLoaded", "readme.md"));
        Assert.Equal("Loaded: {0}", localizer.Format("Status.FileLoaded"));
    }

    [Fact]
    public void SetLanguage_RaisesEventOnlyForEffectiveChanges()
    {
        var service = new LocalizationService();
        var events = new List<LanguageChangedEventArgs>();
        service.LanguageChanged += (_, e) => events.Add(e);

        service.SetLanguage(SupportedLanguage.English);
        service.SetLanguage(new SupportedLanguage("en-US", "Language.English", "pack://duplicate.xaml"));
        service.SetLanguage(SupportedLanguage.Chinese);

        Assert.Equal(2, events.Count);
        Assert.Null(events[0].OldLanguage);
        Assert.Equal(SupportedLanguage.English, events[0].NewLanguage);
        Assert.Equal(SupportedLanguage.English, events[1].OldLanguage);
        Assert.Equal(SupportedLanguage.Chinese, events[1].NewLanguage);
    }

    [Fact]
    public void SetLanguage_CanSwitchRapidlyAndKeepsCurrentLanguage()
    {
        var service = new LocalizationService();

        service.SetLanguage(SupportedLanguage.English);
        service.SetLanguage(SupportedLanguage.Chinese);
        service.SetLanguage(SupportedLanguage.English);

        Assert.Equal(SupportedLanguage.English, service.CurrentLanguage);
        Assert.Equal("Cancel", service.GetString("Common.Cancel"));
    }

    [Fact]
    public void GetDefaultLanguage_UsesChineseForChineseCultureAndEnglishOtherwise()
    {
        Assert.Equal(SupportedLanguage.Chinese, LocalizationService.GetDefaultLanguage(new CultureInfo("zh-CN")));
        Assert.Equal(SupportedLanguage.Chinese, LocalizationService.GetDefaultLanguage(new CultureInfo("zh-Hans")));
        Assert.Equal(SupportedLanguage.English, LocalizationService.GetDefaultLanguage(new CultureInfo("en-US")));
    }
}
