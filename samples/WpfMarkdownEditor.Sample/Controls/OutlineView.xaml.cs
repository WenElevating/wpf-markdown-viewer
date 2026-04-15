using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WpfMarkdownEditor.Wpf.Controls;
using WpfMarkdownEditor.Wpf.Events;
using WpfMarkdownEditor.Wpf.Models;

namespace WpfMarkdownEditor.Sample.Controls;

public partial class OutlineView : UserControl
{
    public static readonly DependencyProperty ShowHeaderProperty =
        DependencyProperty.Register(nameof(ShowHeader), typeof(bool),
            typeof(OutlineView), new PropertyMetadata(true));

    public bool ShowHeader
    {
        get => (bool)GetValue(ShowHeaderProperty);
        set => SetValue(ShowHeaderProperty, value);
    }

    private MarkdownEditor? _editor;
    private List<OutlineItem> _outlineItems = [];

    public OutlineView()
    {
        InitializeComponent();
    }

    public void BindEditor(MarkdownEditor? editor)
    {
        // Unsubscribe from old editor
        if (_editor != null)
        {
            _editor.OutlineChanged -= OnOutlineChanged;
        }

        _editor = editor;

        // Subscribe to new editor
        if (_editor != null)
        {
            _editor.OutlineChanged += OnOutlineChanged;
        }

        // Clear outline when no editor
        if (_editor == null)
        {
            _outlineItems.Clear();
            OutlineList.ItemsSource = null;
        }
    }

    private void OnOutlineChanged(object? sender, WpfMarkdownEditor.Wpf.Events.OutlineChangedEventArgs e)
    {
        _outlineItems = e.Outline;
        OutlineList.ItemsSource = _outlineItems;
    }

    private void OnOutlineItemSelected(object sender, SelectionChangedEventArgs e)
    {
        if (OutlineList.SelectedItem is OutlineItem outlineItem && _editor != null)
        {
            _editor.ScrollToLine(outlineItem.LineNumber);
        }
    }
}

/// <summary>
/// Converts heading level to left margin for indentation.
/// Level 1 = 0px, Level 2 = 12px, Level 3 = 24px, etc.
/// </summary>
public class LevelToMarginConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length > 0 && values[0] is int level)
        {
            // Indent by 12px per level, starting from 0 for level 1
            return new Thickness((level - 1) * 12, 0, 0, 0);
        }
        return new Thickness(0);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
