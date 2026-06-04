using System.IO;
using Microsoft.Extensions.DependencyInjection;
using WpfMarkdownEditor.Sample;
using WpfMarkdownEditor.Sample.Services;
using WpfMarkdownEditor.Sample.ViewModels;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.Sample.DependencyInjection;

public sealed class SampleServiceCollectionExtensionsTests
{
    [Fact]
    public void AddWpfMarkdownEditorSample_ResolvesMainWindowWithInjectedViewModel()
    {
        WpfTestHost.Run(() =>
        {
            var services = new ServiceCollection();
            services.AddWpfMarkdownEditorSample(
                Path.Combine(Path.GetTempPath(), "WpfMarkdownEditor.SampleServiceCollectionTests", Guid.NewGuid().ToString("N")));
            using var provider = services.BuildServiceProvider();

            var window = provider.GetRequiredService<MainWindow>();
            try
            {
                Assert.IsType<MainWindowViewModel>(window.DataContext);
            }
            finally
            {
                window.Close();
            }
        });
    }

}
