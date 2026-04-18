namespace WpfMarkdownEditor.Wpf.Translation;

public sealed record RetryPolicy(int MaxRetries = 3, int DelayMs = 2000);
