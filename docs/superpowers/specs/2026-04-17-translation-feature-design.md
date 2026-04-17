# Translation Feature Design

## Overview

Add a multi-engine translation feature to the WPF Markdown Editor that translates the entire document content. Users select only the target language; source language is auto-detected by the translation provider. Translation replaces the editor content directly and supports Ctrl+Z undo.

## Requirements

- **Language pairs**: Chinese <-> English, Chinese <-> Japanese, Chinese <-> Korean (source auto-detected)
- **Translation scope**: Entire document
- **Translation engines**: Multi-provider pluggable architecture
- **Initial providers**: Baidu Translate, OpenAI-compatible (covers Tongyi Qwen, Zhipu GLM, DeepSeek, OpenAI, etc.)
- **API Key management**: User-provided keys, stored encrypted via Windows DPAPI
- **UI entry**: Toolbar dropdown button
- **First-run experience**: Forced engine selection + configuration, then translate immediately
- **Undo**: Leverage WPF TextBox native Undo (Ctrl+Z restores original text)

## Architecture

### Core Interface

```csharp
public interface ITranslationProvider
{
    string Name { get; }
    bool IsConfigured { get; }
    IReadOnlyList<string> SupportedTargetLanguages { get; }

    Task<TranslationResult> TranslateAsync(
        string text,
        string targetLanguage,
        CancellationToken cancellationToken);
}

public record TranslationResult(
    string TranslatedText,
    string DetectedSourceLanguage,
    int CharactersProcessed);
```

### Service Layer

```
TranslationService
├── Manages current provider selection
├── Calls provider translation
├── Reports progress (status bar)
└── Handles errors + retry (max 3 retries)

TranslationSettingsService
├── Persists API keys encrypted (DPAPI)
├── Stores current provider selection
└── Validates configuration
```

### File Layout

```
src/WpfMarkdownEditor.Core/
├── Translation/
│   ├── ITranslationProvider.cs
│   ├── TranslationResult.cs
│   ├── TranslationService.cs
│   └── Providers/
│       ├── BaiduTranslateProvider.cs
│       └── OpenAICompatibleProvider.cs

src/WpfMarkdownEditor.Wpf/
├── Services/
│   └── TranslationSettingsService.cs
├── Controls/
│   └── TranslationConfigDialog.xaml (.cs)
```

## Provider Implementations

### BaiduTranslateProvider

- **API**: `https://fanyi-api.baidu.com/api/trans/vip/translate`
- **Source language**: `auto` (auto-detect)
- **Auth**: MD5 signature (`appid + query + salt + secretKey`)
- **Rate limit**: 1 QPS (standard), 10 QPS (advanced)
- **Max per request**: 6000 characters; longer documents split into segments
- **Markdown handling**: Split by paragraphs, translate each, preserve markdown syntax markers (#, *, -, >), reassemble

### OpenAICompatibleProvider

- **API**: OpenAI Chat Completions compatible format
- **Auth**: Bearer token (API Key)
- **Source language**: Auto-detected by the model
- **Prompt**: "Translate the following text to {targetLanguage}. Preserve all Markdown formatting exactly as-is."
- **Pre-configured endpoints**:

| Service | Endpoint |
|---------|----------|
| Tongyi Qwen | `https://dashscope.aliyuncs.com/compatible-mode/v1` |
| Zhipu GLM | `https://open.bigmodel.cn/api/paas/v4` |
| DeepSeek | `https://api.deepseek.com/v1` |
| OpenAI | `https://api.openai.com/v1` |
| Custom | User-specified URL |

- **Markdown handling**: Model naturally preserves markdown formatting
- **Long documents**: Sent as a single request (model context window dependent)

### NuGet Dependencies

No external NuGet packages required. Both providers use `HttpClient` for REST API calls and `System.Security.Cryptography` for DPAPI encryption.

## UI Design

### Toolbar Dropdown Button

Positioned before the Theme Picker in the toolbar:

```
[...formatting buttons] | [Translate v] | [Theme v]

[Translate v] dropdown:
├ Engine: * Baidu Translate  o OpenAI Compatible   (radio buttons)
├ ──────────
├ -> English
├ -> Chinese
├ -> Japanese
├ -> Korean
├ ──────────
└ Translation Settings...
```

- Current engine shown as checked radio button
- Target languages always visible
- "Translation Settings..." opens the full config dialog

### First-Run Flow

1. User clicks any "-> Language" option
2. No engine configured -> show engine selection dialog:
   ```
   ┌─ Select Translation Engine ─────────┐
   │                                     │
   │  o Baidu Translate                  │
   │  o OpenAI Compatible                │
   │    (Qwen/Zhipu/DeepSeek/OpenAI...)  │
   │                                     │
   │  [Next]  [Cancel]                   │
   └─────────────────────────────────────┘
   ```
3. User selects engine -> show config for that engine only:
   ```
   ┌─ Configure Baidu Translate ─────────┐
   │                                     │
   │  App ID:     [________________]     │
   │  Secret Key: [________________]     │
   │                                     │
   │  [Save & Translate]  [Cancel]       │
   └─────────────────────────────────────┘
   ```
4. Save configuration -> immediately start translation

### Subsequent Use Flow

1. User clicks "-> Language"
2. Current engine configured? -> Yes -> translate directly
3. Current engine not configured? -> show config dialog for that engine

### Engine Switching

- Switch engine via radio buttons in dropdown
- If new engine is configured -> switch immediately
- If new engine is not configured -> show config dialog

## Translation Flow

```
User clicks "-> Language"
    │
    ├─ Engine configured? ──No──> Show config dialog ──> Config saved
    │                                                        │
    │<───────────────────────────────────────────────────────┘
    │ Yes
    ▼
Show "Translating..." in status bar
Toolbar button changes to "Cancel Translation"
    │
    ▼
Call ITranslationProvider.TranslateAsync(text, targetLanguage, cancellationToken)
    │                                   │
    │ Success                           │ Failure
    ▼                                   ▼
Replace editor text              Show error in status bar
Show completion in status bar    Original text unchanged
(includes detected source lang)
    │
    ▼
User can Ctrl+Z to undo
```

### Undo Support

WPF TextBox has built-in undo. Before translation, `TextBox.Text` is in the undo stack. After replacing text via `SelectAll()` + `SelectedText`, the replacement becomes a single undo unit. User presses Ctrl+Z to restore original text.

## Error Handling

| Scenario | Response |
|----------|----------|
| Network unreachable | Status bar: "Cannot connect to translation service" |
| Invalid API Key | Status bar: "Invalid API Key, please check settings" |
| Insufficient balance | Status bar: "Translation service balance insufficient" |
| Rate limit exceeded | Auto-retry with backoff (max 3 retries) |
| Document too long | Baidu: auto-segment; OpenAI: send full document |
| User cancels | CancellationToken cancels, original text unchanged |

## API Key Security

- Stored in `%APPDATA%/WpfMarkdownEditor/translation.json`
- Encrypted using Windows DPAPI (`System.Security.Cryptography.ProtectedData`)
- Scope: `DataProtectionScope.CurrentUser` (only decryptable by same Windows user)
- File format: JSON with encrypted values per provider

## Testing Strategy

### Unit Tests

| Target | Tests |
|--------|-------|
| `BaiduTranslateProvider` | Signature algorithm, segmentation logic, error code parsing |
| `OpenAICompatibleProvider` | Prompt construction, preset config loading, response parsing |
| `TranslationService` | Engine selection logic, cancellation handling, retry strategy |
| `TranslationSettingsService` | Encrypt/decrypt round-trip, config validation |

### Integration Tests

| Target | Tests |
|--------|-------|
| API calls | Mock HttpClient, verify request format |
| Full flow | Mock Provider -> TranslationService -> verify result |

### Manual Test Checklist

- [ ] First-run: engine selection dialog appears
- [ ] Baidu Translate: config save + translation execution
- [ ] OpenAI Compatible: config save + translation execution
- [ ] Engine switching: unconfigured engine triggers config dialog
- [ ] After translation: Ctrl+Z restores original text
- [ ] Network error: error message shown, text unchanged
- [ ] Invalid API Key: error message shown
- [ ] Cancel translation during progress
- [ ] Toolbar UI works with all theme switches
