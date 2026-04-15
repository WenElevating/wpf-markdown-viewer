using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfMarkdownEditor.Wpf.Controls;

namespace WpfMarkdownEditor.Sample.Helpers;

public class TabItem : INotifyPropertyChanged
{
    public string FilePath { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;

    private string _markdownContent = string.Empty;
    public string MarkdownContent
    {
        get => _markdownContent;
        set { _markdownContent = value; OnPropertyChanged(); }
    }

    private bool _isDirty;
    public bool IsDirty
    {
        get => _isDirty;
        set { _isDirty = value; OnPropertyChanged(); }
    }

    public DateTime LastAccessed { get; set; } = DateTime.UtcNow;

    private MarkdownEditor? _editor;
    public MarkdownEditor? Editor
    {
        get => _editor;
        set { _editor = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsEvicted)); }
    }

    public bool IsEvicted => _editor == null;

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set { _isActive = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
