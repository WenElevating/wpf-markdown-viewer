using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Threading;
using WpfMarkdownEditor.Wpf.Controls;
using WpfMarkdownEditor.Wpf.Localization;
using Xunit;

namespace WpfMarkdownEditor.Wpf.Tests.Controls;

public sealed class MarkdownEditorAutomationTests
{
    [Fact]
    public void MarkdownEditor_InitializesAutomationPeersForPrimarySurfaces()
    {
        WpfTestHost.Run(() =>
        {
            using var editor = CreateEditorWithEnglishLocalizer();
            Measure(editor);

            AssertPeer(
                editor,
                "MarkdownEditor",
                "Markdown editor",
                AutomationControlType.Custom);
            AssertPeer(
                editor.TextBox,
                "MarkdownEditor_SourceTextBox",
                "Markdown source",
                AutomationControlType.Edit);
            AssertPeer(
                editor.PreviewViewer,
                "MarkdownEditor_PreviewViewer",
                "Rendered preview");
            AssertPeer(
                editor.ZoomOutBtn,
                "MarkdownEditor_ZoomOutButton",
                "Zoom Out",
                AutomationControlType.Button);
            AssertPeer(
                editor.ZoomSlider,
                "MarkdownEditor_ZoomSlider",
                "Zoom level",
                AutomationControlType.Slider);
            AssertPeer(
                editor.ZoomInBtn,
                "MarkdownEditor_ZoomInButton",
                "Zoom In",
                AutomationControlType.Button);
            AssertPeer(
                editor.ZoomResetBtn,
                "MarkdownEditor_ZoomResetButton",
                "Reset Zoom",
                AutomationControlType.Button);
        });
    }

    [Fact]
    public void SetLocalizer_WhenLanguageChanges_RefreshesAutomationNames()
    {
        WpfTestHost.Run(() =>
        {
            using var editor = new MarkdownEditor();
            var localizer = new LocalizationService();
            localizer.SetLanguage(SupportedLanguage.English);
            editor.SetLocalizer(localizer);

            var englishSourceName = AutomationProperties.GetName(editor.TextBox);

            localizer.SetLanguage(SupportedLanguage.Chinese);

            Assert.Equal(localizer.GetString("Editor.MarkdownSource"), AutomationProperties.GetName(editor.TextBox));
            Assert.Equal(localizer.GetString("Editor.Preview"), AutomationProperties.GetName(editor.PreviewViewer));
            Assert.Equal(localizer.GetString("Editor.ZoomLevel"), AutomationProperties.GetName(editor.ZoomSlider));
            Assert.NotEqual(englishSourceName, AutomationProperties.GetName(editor.TextBox));
        });
    }

    private static MarkdownEditor CreateEditorWithEnglishLocalizer()
    {
        var editor = new MarkdownEditor();
        var localizer = new LocalizationService();
        localizer.SetLanguage(SupportedLanguage.English);
        editor.SetLocalizer(localizer);
        return editor;
    }

    private static void AssertPeer(
        UIElement element,
        string automationId,
        string name,
        AutomationControlType? controlType = null)
    {
        var peer = UIElementAutomationPeer.CreatePeerForElement(element);

        Assert.NotNull(peer);
        Assert.Equal(automationId, peer.GetAutomationId());
        Assert.Equal(name, peer.GetName());
        if (controlType is not null)
            Assert.Equal(controlType, peer.GetAutomationControlType());
    }

    private static void Measure(FrameworkElement element)
    {
        element.Measure(new Size(800, 600));
        element.Arrange(new Rect(0, 0, 800, 600));
        element.UpdateLayout();
        DrainDispatcher();
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
