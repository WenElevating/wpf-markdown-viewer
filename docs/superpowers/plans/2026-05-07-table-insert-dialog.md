# Table Insert Dialog Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the hardcoded table insertion with a dialog that lets users pick row/column counts via number spinners.

**Architecture:** New `TableInsertDialog` window in the Sample project, following the same borderless dialog pattern as `SaveConfirmationDialog`. The `OnTable` handler in `MainWindow.xaml.cs` opens the dialog and generates the markdown table from the result.

**Tech Stack:** WPF (.NET 8), no new dependencies.

---

## File Structure

| File | Action | Responsibility |
|------|--------|----------------|
| `samples/WpfMarkdownEditor.Sample/TableInsertDialog.xaml` | Create | Dialog XAML — title, two number spinners, Cancel/Insert buttons |
| `samples/WpfMarkdownEditor.Sample/TableInsertDialog.xaml.cs` | Create | Dialog code-behind — spinner logic, Result property, Escape key |
| `samples/WpfMarkdownEditor.Sample/MainWindow.xaml.cs` | Modify | Replace `OnTable`, add `GenerateTable` helper |

---

### Task 1: Create TableInsertDialog XAML

**Files:**
- Create: `samples/WpfMarkdownEditor.Sample/TableInsertDialog.xaml`

- [ ] **Step 1: Create the dialog XAML file**

```xml
<Window x:Class="WpfMarkdownEditor.Sample.TableInsertDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Insert Table"
        Width="320" SizeToContent="Height"
        WindowStartupLocation="CenterOwner"
        ResizeMode="NoResize"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        ShowInTaskbar="False">
    <Border Background="{DynamicResource CardBackgroundBrush}"
            CornerRadius="12" Padding="24"
            BorderBrush="{DynamicResource DividerBrush}" BorderThickness="1"
            Margin="12">
        <Border.Effect>
            <DropShadowEffect ShadowDepth="4" Direction="270" Opacity="0.2" BlurRadius="16" Color="#000000"/>
        </Border.Effect>
        <StackPanel>
            <!-- Header -->
            <StackPanel Orientation="Horizontal" Margin="0,0,0,16">
                <TextBlock Text="&#xE943;" FontFamily="Segoe MDL2 Assets" FontSize="20"
                           Foreground="{DynamicResource AccentBrush}" VerticalAlignment="Center" Margin="0,0,12,0"/>
                <TextBlock Text="Insert Table" FontFamily="Segoe UI Variable, Segoe UI" FontSize="16"
                           FontWeight="SemiBold" Foreground="{DynamicResource TextPrimaryBrush}" VerticalAlignment="Center"/>
            </StackPanel>

            <!-- Rows -->
            <TextBlock Text="Rows" FontFamily="Segoe UI Variable, Segoe UI" FontSize="12"
                       Foreground="{DynamicResource TextSecondaryBrush}" Margin="0,0,0,4"/>
            <Border Background="{DynamicResource SurfaceBackgroundBrush}" CornerRadius="6"
                    BorderBrush="{DynamicResource DividerBrush}" BorderThickness="1" Margin="0,0,0,12">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>
                    <TextBox x:Name="RowsText" Grid.Column="0" Text="2" IsReadOnly="True"
                             VerticalAlignment="Center" VerticalContentAlignment="Center"
                             Background="Transparent" BorderThickness="0"
                             FontFamily="Segoe UI Variable, Segoe UI" FontSize="13"
                             Foreground="{DynamicResource TextPrimaryBrush}"
                             CaretBrush="{DynamicResource TextPrimaryBrush}"
                             Padding="12,6" Focusable="False"/>
                    <RepeatButton x:Name="RowsDown" Grid.Column="1" Content="&#xE70E;"
                                  FontFamily="Segoe MDL2 Assets" FontSize="8"
                                  Padding="8,4" Margin="0,2,1,2" Cursor="Hand"
                                  Background="Transparent" BorderThickness="0"
                                  Foreground="{DynamicResource TextSecondaryBrush}"
                                  Click="OnRowsDown"/>
                    <RepeatButton x:Name="RowsUp" Grid.Column="2" Content="&#xE70D;"
                                  FontFamily="Segoe MDL2 Assets" FontSize="8"
                                  Padding="8,4" Margin="0,2,2,2" Cursor="Hand"
                                  Background="Transparent" BorderThickness="0"
                                  Foreground="{DynamicResource TextSecondaryBrush}"
                                  Click="OnRowsUp"/>
                </Grid>
            </Border>

            <!-- Columns -->
            <TextBlock Text="Columns" FontFamily="Segoe UI Variable, Segoe UI" FontSize="12"
                       Foreground="{DynamicResource TextSecondaryBrush}" Margin="0,0,0,4"/>
            <Border Background="{DynamicResource SurfaceBackgroundBrush}" CornerRadius="6"
                    BorderBrush="{DynamicResource DividerBrush}" BorderThickness="1" Margin="0,0,0,20">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>
                    <TextBox x:Name="ColumnsText" Grid.Column="0" Text="3" IsReadOnly="True"
                             VerticalAlignment="Center" VerticalContentAlignment="Center"
                             Background="Transparent" BorderThickness="0"
                             FontFamily="Segoe UI Variable, Segoe UI" FontSize="13"
                             Foreground="{DynamicResource TextPrimaryBrush}"
                             CaretBrush="{DynamicResource TextPrimaryBrush}"
                             Padding="12,6" Focusable="False"/>
                    <RepeatButton x:Name="ColsDown" Grid.Column="1" Content="&#xE70E;"
                                  FontFamily="Segoe MDL2 Assets" FontSize="8"
                                  Padding="8,4" Margin="0,2,1,2" Cursor="Hand"
                                  Background="Transparent" BorderThickness="0"
                                  Foreground="{DynamicResource TextSecondaryBrush}"
                                  Click="OnColsDown"/>
                    <RepeatButton x:Name="ColsUp" Grid.Column="2" Content="&#xE70D;"
                                  FontFamily="Segoe MDL2 Assets" FontSize="8"
                                  Padding="8,4" Margin="0,2,2,2" Cursor="Hand"
                                  Background="Transparent" BorderThickness="0"
                                  Foreground="{DynamicResource TextSecondaryBrush}"
                                  Click="OnColsUp"/>
                </Grid>
            </Border>

            <!-- Buttons -->
            <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
                <Button x:Name="CancelBtn" Content="Cancel" Click="OnCancel"
                        Margin="0,0,8,0" Cursor="Hand">
                    <Button.Template>
                        <ControlTemplate TargetType="Button">
                            <Border x:Name="Bd" Padding="16,8" CornerRadius="6" Cursor="Hand"
                                    Background="{DynamicResource HoverBackgroundBrush}">
                                <TextBlock Text="{Binding Content, RelativeSource={RelativeSource TemplatedParent}}"
                                           FontFamily="Segoe UI Variable, Segoe UI" FontSize="13"
                                           Foreground="{DynamicResource TextSecondaryBrush}"
                                           HorizontalAlignment="Center" VerticalAlignment="Center"/>
                            </Border>
                            <ControlTemplate.Triggers>
                                <Trigger Property="IsMouseOver" Value="True">
                                    <Setter TargetName="Bd" Property="Background" Value="{DynamicResource PressedBackgroundBrush}"/>
                                </Trigger>
                            </ControlTemplate.Triggers>
                        </ControlTemplate>
                    </Button.Template>
                </Button>
                <Button x:Name="InsertBtn" Content="Insert" Click="OnInsert" Cursor="Hand">
                    <Button.Template>
                        <ControlTemplate TargetType="Button">
                            <Border x:Name="Bd" Padding="16,8" CornerRadius="6" Cursor="Hand"
                                    Background="{DynamicResource AccentBrush}">
                                <TextBlock Text="{Binding Content, RelativeSource={RelativeSource TemplatedParent}}"
                                           FontFamily="Segoe UI Variable, Segoe UI" FontSize="13"
                                           Foreground="White" FontWeight="SemiBold"
                                           HorizontalAlignment="Center" VerticalAlignment="Center"/>
                            </Border>
                            <ControlTemplate.Triggers>
                                <Trigger Property="IsMouseOver" Value="True">
                                    <Setter TargetName="Bd" Property="Opacity" Value="0.9"/>
                                </Trigger>
                            </ControlTemplate.Triggers>
                        </ControlTemplate>
                    </Button.Template>
                </Button>
            </StackPanel>
        </StackPanel>
    </Border>
</Window>
```

- [ ] **Step 2: Commit**

```bash
git add samples/WpfMarkdownEditor.Sample/TableInsertDialog.xaml
git commit -m "feat: add TableInsertDialog XAML"
```

---

### Task 2: Create TableInsertDialog code-behind

**Files:**
- Create: `samples/WpfMarkdownEditor.Sample/TableInsertDialog.xaml.cs`

- [ ] **Step 1: Create the code-behind file**

```csharp
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
```

- [ ] **Step 2: Commit**

```bash
git add samples/WpfMarkdownEditor.Sample/TableInsertDialog.xaml.cs
git commit -m "feat: add TableInsertDialog code-behind with spinner logic"
```

---

### Task 3: Wire up MainWindow OnTable handler

**Files:**
- Modify: `samples/WpfMarkdownEditor.Sample/MainWindow.xaml.cs` (line 384 and add new method)

- [ ] **Step 1: Replace the OnTable method and add GenerateTable helper**

In `MainWindow.xaml.cs`, replace line 384:

```csharp
private void OnTable(object sender, RoutedEventArgs e) => Editor.InsertText("\n| Column 1 | Column 2 | Column 3 |\n| -------- | -------- | -------- |\n| Cell 1   | Cell 2   | Cell 3   |\n");
```

With:

```csharp
private void OnTable(object sender, RoutedEventArgs e)
{
    InsertPopup.IsOpen = false;
    var dialog = new TableInsertDialog { Owner = this };
    if (dialog.ShowDialog() == true && dialog.Result is (int rows, int cols))
    {
        Editor.InsertText(GenerateTable(rows, cols));
    }
}

private static string GenerateTable(int dataRows, int columns)
{
    var sb = new System.Text.StringBuilder();
    sb.Append('\n');

    // Header
    sb.Append("| ");
    sb.Append(string.Join(" | ", Enumerable.Range(1, columns).Select(i => $"Column {i}")));
    sb.Append(" |\n");

    // Separator
    sb.Append("| ");
    sb.Append(string.Join(" | ", Enumerable.Repeat("--------", columns)));
    sb.Append(" |\n");

    // Data rows
    var cellIndex = 1;
    for (var r = 0; r < dataRows; r++)
    {
        sb.Append("| ");
        sb.Append(string.Join(" | ", Enumerable.Range(0, columns).Select(_ => $"Cell {cellIndex++}")));
        sb.Append(" |\n");
    }

    return sb.ToString();
}
```

- [ ] **Step 2: Build and verify no compilation errors**

Run: `dotnet build D:/AIProject/wpf-markdown-viewer/samples/WpfMarkdownEditor.Sample`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add samples/WpfMarkdownEditor.Sample/MainWindow.xaml.cs
git commit -m "feat: wire TableInsertDialog into Insert > Table menu"
```

---

### Task 4: Build and manual verification

**Files:** None changed — verification only.

- [ ] **Step 1: Full solution build**

Run: `dotnet build D:/AIProject/wpf-markdown-viewer`
Expected: Build succeeded, 0 errors.

- [ ] **Step 2: Run existing tests**

Run: `dotnet test D:/AIProject/wpf-markdown-viewer`
Expected: All tests pass.

- [ ] **Step 3: Manual smoke test**

Run the sample app and verify:
1. Click Insert > Table — dialog appears
2. Rows default 2, Columns default 3
3. Click spinners — values change within [1-20] for rows, [1-10] for columns
4. Click Cancel — dialog closes, no text inserted
5. Click Insert — correct markdown table inserted at cursor
6. Press Escape — dialog closes (same as Cancel)
7. Switch theme — dialog respects theme colors
