using WpfMarkdownEditor.Core.Translation;
using WpfMarkdownEditor.Wpf.Services;

namespace WpfMarkdownEditor.Sample.Services;

public interface ITranslationProviderFactory
{
    ITranslationProvider Create(ProviderConfig config);
}
