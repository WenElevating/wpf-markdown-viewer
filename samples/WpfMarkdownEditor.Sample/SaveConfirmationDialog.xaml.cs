using System.Windows;
using System.Windows.Input;

namespace WpfMarkdownEditor.Sample;

public enum SaveConfirmationResult
{
    Save,
    DontSave,
    Cancel
}

public partial class SaveConfirmationDialog : Window
{
    public SaveConfirmationResult Result { get; private set; } = SaveConfirmationResult.Cancel;

    public SaveConfirmationDialog(string fileName)
    {
        InitializeComponent();
        MessageText.Text = $"Do you want to save changes to \"{fileName}\"?";
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
