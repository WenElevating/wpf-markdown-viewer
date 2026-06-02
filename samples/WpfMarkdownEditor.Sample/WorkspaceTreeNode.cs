using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfMarkdownEditor.Sample;

public sealed class WorkspaceTreeNode : INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _isSelected;
    private bool _childrenLoaded;

    public required string Name { get; init; }

    public required string FullPath { get; init; }

    public bool IsDirectory { get; init; }

    public ObservableCollection<WorkspaceTreeNode> Children { get; } = [];

    public WorkspaceTreeNode? Parent { get; init; }

    public bool ChildrenLoaded
    {
        get => _childrenLoaded;
        set => SetField(ref _childrenLoaded, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField(ref bool field, bool value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
            return;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
