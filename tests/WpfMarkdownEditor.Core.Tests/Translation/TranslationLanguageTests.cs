using Xunit;
using WpfMarkdownEditor.Core.Translation;

namespace WpfMarkdownEditor.Core.Tests.Translation;

public class TranslationLanguageTests
{
    [Theory]
    [InlineData(TranslationLanguage.English, "English")]
    [InlineData(TranslationLanguage.Chinese, "中文")]
    [InlineData(TranslationLanguage.Japanese, "日本語")]
    [InlineData(TranslationLanguage.Korean, "한국어")]
    public void DisplayName_ReturnsExpectedString(TranslationLanguage language, string expected)
    {
        Assert.Equal(expected, language.DisplayName());
    }
}
