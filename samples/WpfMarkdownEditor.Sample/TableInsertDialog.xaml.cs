using System.Windows;
using System.Windows.Input;

namespace WpfMarkdownEditor.Sample;

public partial class TableInsertDialog : Window
{
    private int _rows = 2;
    private int _columns = 3;

    public (int Rows, int Columns)? Result { get; private set; }

    public TableInsertDialog()
    {
        InitializeComponent();
    }

    private void OnRowsUp(object sender, RoutedEventArgs e)
    {
        if (_rows < 20) { _rows++; RowsText.Text = _rows.ToString(); }
    }

    private void OnRowsDown(object sender, RoutedEventArgs e)
    {
        if (_rows > 1) { _rows--; RowsText.Text = _rows.ToString(); }
    }

    private void OnColsUp(object sender, RoutedEventArgs e)
    {
        if (_columns < 10) { _columns++; ColumnsText.Text = _columns.ToString(); }
    }

    private void OnColsDown(object sender, RoutedEventArgs e)
    {
        if (_columns > 1) { _columns--; ColumnsText.Text = _columns.ToString(); }
    }

    private void OnInsert(object sender, RoutedEventArgs e)
    {
        Result = (_rows, _columns);
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        Result = null;
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
