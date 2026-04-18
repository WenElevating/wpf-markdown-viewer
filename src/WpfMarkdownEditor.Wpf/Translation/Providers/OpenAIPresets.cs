namespace WpfMarkdownEditor.Wpf.Translation.Providers;

public sealed record OpenAIServicePreset(string Name, string Endpoint, string DefaultModel);

public static class OpenAIPresets
{
    public static IReadOnlyList<OpenAIServicePreset> All { get; } =
    [
        new("Tongyi Qwen", "https://dashscope.aliyuncs.com/compatible-mode/v1", "qwen-turbo"),
        new("Zhipu GLM", "https://open.bigmodel.cn/api/paas/v4", "glm-4-flash"),
        new("DeepSeek", "https://api.deepseek.com/v1", "deepseek-chat"),
        new("OpenAI", "https://api.openai.com/v1", "gpt-4o-mini"),
    ];

    public static OpenAIServicePreset? FindByName(string name) =>
        All.FirstOrDefault(p => p.Name == name);
}
