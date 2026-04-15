using System.Windows;
using WpfMarkdownEditor.Sample.Helpers;

namespace WpfMarkdownEditor.Sample.Controls;

public partial class SaveDialog : Window
{
    public SaveResult Result { get; private set; } = SaveResult.Cancel;

    public SaveDialog()
    {
        InitializeComponent();
    }

    public SaveDialog(string fileName) : this()
    {
        MessageText.Text = $"是否保存对 {fileName} 的更改？";
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        Result = SaveResult.Save;
        Close();
    }

    private void OnDiscard(object sender, RoutedEventArgs e)
    {
        Result = SaveResult.Discard;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        Result = SaveResult.Cancel;
        Close();
    }

    public static SaveResult Show(string fileName, Window owner)
    {
        var dialog = new SaveDialog(fileName)
        {
            Owner = owner
        };
        dialog.ShowDialog();
        return dialog.Result;
    }
}
