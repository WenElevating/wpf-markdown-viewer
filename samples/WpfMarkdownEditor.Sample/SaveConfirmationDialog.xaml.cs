using System.Windows;
using System.Windows.Input;
using WpfMarkdownEditor.Wpf.Localization;

namespace WpfMarkdownEditor.Sample;

public enum SaveConfirmationResult
{
    Save,
    DontSave,
    Cancel
}

public partial class SaveConfirmationDialog : Window
{
    private readonly IStringLocalizer _localizer;
    private readonly string _fileName;

    public SaveConfirmationResult Result { get; private set; } = SaveConfirmationResult.Cancel;

    public SaveConfirmationDialog(string fileName, IStringLocalizer? localizer = null)
    {
        _fileName = fileName;
        _localizer = localizer ?? FallbackStringLocalizer.Instance;
        InitializeComponent();
        RefreshLocalizedText();
    }

    private void RefreshLocalizedText()
    {
        Title = _localizer.GetString("Dialog.SaveChanges.Title");
        MessageText.Text = _localizer.Format("Dialog.SaveChanges.Message", _fileName);
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        Result = SaveConfirmationResult.Save;
        DialogResult = true;
    }

    private void OnDontSave(object sender, RoutedEventArgs e)
    {
        Result = SaveConfirmationResult.DontSave;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        Result = SaveConfirmationResult.Cancel;
        DialogResult = false;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            OnCancel(this, e);
        }
        base.OnPreviewKeyDown(e);
    }
}
