using WpfMarkdownEditor.Wpf.Localization;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.Localization;

public sealed class SupportedLanguageTests
{
    [Fact]
    public void Equality_UsesLanguageCode()
    {
        var first = new SupportedLanguage("en-US", "Language.English", "pack://test/one.xaml");
        var second = new SupportedLanguage("en-US", "Different.DisplayKey", "pack://test/two.xaml");

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void FromCode_ReturnsKnownLanguage()
    {
        Assert.Equal(SupportedLanguage.Chinese, SupportedLanguage.FromCode("zh-CN"));
        Assert.Equal(SupportedLanguage.English, SupportedLanguage.FromCode("en-US"));
        Assert.Null(SupportedLanguage.FromCode("fr-FR"));
    }
}
