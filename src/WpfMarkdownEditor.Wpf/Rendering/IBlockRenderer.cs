using WpfMarkdownEditor.Core.Parsing;

namespace WpfMarkdownEditor.Wpf.Rendering;

/// <summary>
/// Renders a specific block type to a WPF FlowDocument Block element.
/// </summary>
public interface IBlockRenderer
{
    System.Windows.Documents.Block Render(Core.Parsing.Block block);
}
