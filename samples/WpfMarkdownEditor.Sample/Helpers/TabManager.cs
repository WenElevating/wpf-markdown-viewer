using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Win32;
using WpfMarkdownEditor.Wpf.Controls;
using WpfMarkdownEditor.Wpf.Theming;
using WpfMarkdownEditor.Sample.Controls;

namespace WpfMarkdownEditor.Sample.Helpers;

public enum SaveResult
{
    Save,
    Discard,
    Cancel
}

public class TabManager
{
    private readonly Dictionary<string, TabItem> _pathToTab = new();
    private readonly int _maxActiveEditors = 15;

    public ObservableCollection<TabItem> Tabs { get; } = new();

    private TabItem? _activeTab;
    public TabItem? ActiveTab
    {
        get => _activeTab;
        private set
        {
            if (_activeTab != value)
            {
                // Clear previous active
                if (_activeTab != null)
                    _activeTab.IsActive = false;

                _activeTab = value;

                // Set new active
                if (_activeTab != null)
                    _activeTab.IsActive = true;

                ActiveTabChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public event EventHandler? ActiveTabChanged;

    public TabItem NewTab(string title, string content)
    {
        var tab = new TabItem
        {
            FileName = title,
            FilePath = string.Empty,
            MarkdownContent = content,
            Editor = new MarkdownEditor(),
            LastAccessed = DateTime.UtcNow
        };

        Tabs.Add(tab);
        ActiveTab = tab;
        CheckEviction();

        return tab;
    }

    public TabItem OpenFile(string path)
    {
        // Check if file is already open
        if (_pathToTab.TryGetValue(path, out var existingTab))
        {
            SwitchTab(existingTab);
            return existingTab;
        }

        // Create new tab for file
        var tab = new TabItem
        {
            FileName = System.IO.Path.GetFileName(path),
            FilePath = path,
            MarkdownContent = System.IO.File.ReadAllText(path),
            Editor = new MarkdownEditor(),
            LastAccessed = DateTime.UtcNow
        };

        _pathToTab[path] = tab;
        Tabs.Add(tab);
        ActiveTab = tab;
        CheckEviction();

        return tab;
    }

    public void SwitchTab(TabItem tab)
    {
        if (tab.Editor == null)
        {
            RestoreEvictedTab(tab, EditorTheme.Light);
        }

        ActiveTab = tab;
        tab.LastAccessed = DateTime.UtcNow;
        CheckEviction();
    }

    public async Task<SaveResult> CloseTabAsync(TabItem tab, Window ownerWindow)
    {
        if (tab.IsDirty)
        {
            var result = ShowSaveDialog(tab.FileName, ownerWindow);
            if (result == SaveResult.Cancel)
            {
                return SaveResult.Cancel;
            }

            if (result == SaveResult.Save)
            {
                if (!string.IsNullOrEmpty(tab.FilePath))
                {
                    await tab.Editor!.SaveFileAsync(tab.FilePath);
                    tab.IsDirty = false;
                }
                else
                {
                    var saveDialog = new SaveFileDialog
                    {
                        Filter = "Markdown files (*.md)|*.md|All files (*.*)|*.*",
                        DefaultExt = ".md",
                        FileName = tab.FileName
                    };

                    if (saveDialog.ShowDialog() == true)
                    {
                        await tab.Editor!.SaveFileAsync(saveDialog.FileName);

                        // Update tab by removing and re-adding with correct path
                        var updatedTab = new TabItem
                        {
                            FilePath = saveDialog.FileName,
                            FileName = System.IO.Path.GetFileName(saveDialog.FileName),
                            MarkdownContent = tab.MarkdownContent,
                            Editor = tab.Editor,
                            IsDirty = false,
                            LastAccessed = tab.LastAccessed
                        };

                        var index = Tabs.IndexOf(tab);
                        Tabs.Remove(tab);
                        _pathToTab.Remove(tab.FilePath);
                        Tabs.Insert(index, updatedTab);
                        _pathToTab[saveDialog.FileName] = updatedTab;

                        if (ActiveTab == tab)
                            ActiveTab = updatedTab;
                    }
                    else
                    {
                        return SaveResult.Cancel;
                    }
                }
            }
        }

        // Remove tab
        Tabs.Remove(tab);
        if (!string.IsNullOrEmpty(tab.FilePath))
        {
            _pathToTab.Remove(tab.FilePath);
        }

        // Dispose editor if exists
        if (tab.Editor != null)
        {
            tab.Editor.Dispose();
            tab.Editor = null;
        }

        // Set new active tab if any remain
        if (ActiveTab == tab && Tabs.Count > 0)
        {
            ActiveTab = Tabs[^1];
        }
        else if (Tabs.Count == 0)
        {
            ActiveTab = null;
        }

        return SaveResult.Save;
    }

    public Task<SaveResult> CloseActiveTabAsync(Window ownerWindow)
    {
        if (ActiveTab == null)
            return Task.FromResult(SaveResult.Cancel);

        return CloseTabAsync(ActiveTab, ownerWindow);
    }

    private void CheckEviction()
    {
        var activeEditors = Tabs.Where(t => t.Editor != null).ToList();

        if (activeEditors.Count <= _maxActiveEditors)
            return;

        // Find oldest non-dirty, non-active tab
        var candidate = activeEditors
            .Where(t => t != ActiveTab && !t.IsDirty)
            .OrderBy(t => t.LastAccessed)
            .FirstOrDefault();

        if (candidate != null && candidate.Editor != null)
        {
            // Save content before evicting
            candidate.MarkdownContent = candidate.Editor.Markdown;

            // Dispose editor
            candidate.Editor.Dispose();
            candidate.Editor = null;
        }
    }

    private void RestoreEvictedTab(TabItem tab, EditorTheme theme)
    {
        if (tab.Editor != null)
            return;

        tab.Editor = new MarkdownEditor();
        tab.Editor.Markdown = tab.MarkdownContent;

        if (theme != default)
        {
            tab.Editor.ApplyTheme(theme);
        }
    }

    private SaveResult ShowSaveDialog(string fileName, Window ownerWindow)
    {
        return SaveDialog.Show(fileName, ownerWindow);
    }
}
