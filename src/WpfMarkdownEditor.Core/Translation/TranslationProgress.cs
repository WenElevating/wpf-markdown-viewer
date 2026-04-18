namespace WpfMarkdownEditor.Core.Translation;

public enum TranslationStage
{
    Connecting,
    Translating,
    Completed,
    Failed
}

public sealed record TranslationProgress(
    TranslationStage Stage,
    int CurrentSegment,
    int TotalSegments,
    string Message);
