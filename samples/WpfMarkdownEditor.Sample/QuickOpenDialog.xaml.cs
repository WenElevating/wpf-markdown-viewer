using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WpfMarkdownEditor.Sample;

public partial class QuickOpenDialog : Window
{
    private readonly QuickOpenService _service = new();
    private readonly IReadOnlyList<QuickOpenItem> _items;

    public QuickOpenItem? SelectedItem { get; private set; }

    public QuickOpenDialog(IReadOnlyList<QuickOpenItem> items)
    {
        InitializeComponent();
        _items = items;
        ResultsList.ItemsSource = _service.Filter(_items, "");
        ResultsList.SelectedIndex = ResultsList.Items.Count > 0 ? 0 : -1;
        Loaded += (_, _) =>
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
        };
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        ResultsList.ItemsSource = _service.Filter(_items, SearchBox.Text);
        ResultsList.SelectedIndex = ResultsList.Items.Count > 0 ? 0 : -1;
    }

    private void OnDialogKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
        else if (e.Key == Key.Enter)
        {
            AcceptSelection();
        }
    }

    private void OnResultDoubleClick(object sender, MouseButtonEventArgs e) => AcceptSelection();

    private void AcceptSelection()
    {
        if (ResultsList.SelectedItem is not QuickOpenItem item)
            return;

        SelectedItem = item;
        DialogResult = true;
        Close();
    }
}
