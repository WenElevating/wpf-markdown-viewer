using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using WpfMarkdownEditor.Sample;
using WpfMarkdownEditor.Sample.Services;
using WpfMarkdownEditor.Wpf.Controls;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.FileMenu;

[Collection("SampleMainWindow")]
public sealed class EditMenuCommandTargetTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "WpfMarkdownEditor.EditMenuCommandTargetTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void SelectAllButton_ResolvesCommandTargetToEditorTextBox()
    {
        WpfTestHost.Run(() =>
        {
            using var provider = CreateProvider();
            var window = provider.GetRequiredService<MainWindow>();
            try
            {
                var editor = Assert.IsType<MarkdownEditor>(window.FindName("Editor"));
                var editPopup = Assert.IsType<Popup>(window.FindName("EditPopup"));
                editPopup.IsOpen = true;
                DrainDispatcher();

                var selectAllButton = FindVisualDescendants<Button>(editPopup.Child)
                    .Single(button => Equals(button.Command, ApplicationCommands.SelectAll));

                Assert.Same(editor.TextBox, selectAllButton.CommandTarget);
            }
            finally
            {
                window.Close();
            }
        });
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private ServiceProvider CreateProvider()
    {
        Directory.CreateDirectory(_directory);
        var services = new ServiceCollection();
        services.AddWpfMarkdownEditorSample(_directory);
        return services.BuildServiceProvider();
    }

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject? root)
        where T : DependencyObject
    {
        if (root is null)
            yield break;

        if (root is T match)
            yield return match;

        var children = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < children; i++)
        {
            foreach (var descendant in FindVisualDescendants<T>(VisualTreeHelper.GetChild(root, i)))
                yield return descendant;
        }
    }

    private static void DrainDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
