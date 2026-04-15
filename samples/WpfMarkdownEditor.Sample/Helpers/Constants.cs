namespace WpfMarkdownEditor.Sample.Helpers;

public static class Constants
{
    public const string WelcomeMarkdown = """
        # Welcome to WPF Markdown Editor

        ## Features

        - **Real-time preview** with less than 50ms latency
        - *Italic* and **bold** text
        - `Inline code` support
        - [Links](https://example.com)

        ## Code Block

        ```csharp
        public class HelloWorld
        {
            public static void Main()
            {
                Console.WriteLine("Hello, Markdown!");
            }
        }
        ```

        ## Table

        | Feature | Status |
        | ------- | ------ |
        | Parser | Done |
        | Renderer | Done |
        | Theme | Done |

        ## Blockquote

        > This is a blockquote.
        > It supports **inline formatting**.

        ---

        *Built with .NET 8 and WPF. Zero external dependencies.*
        """;
}
