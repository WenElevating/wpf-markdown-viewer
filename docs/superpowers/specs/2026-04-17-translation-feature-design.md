# Translation Feature Design

## Overview

Add a multi-engine translation feature to the WPF Markdown Editor that translates the entire document content. Users select only the target language; source language is auto-detected by the translation provider. Translation replaces the editor content directly and supports Ctrl+Z undo.

## Requirements

- **Target languages**: English, Chinese, Japanese, Korean (fixed app-level list; source auto-detected)
- **Translation scope**: Entire document
- **Translation engines**: Multi-provider pluggable architecture
- **Initial providers**: Baidu Translate, OpenAI-compatible (covers Tongyi Qwen, Zhipu GLM, DeepSeek, OpenAI, etc.)
- **API Key management**: User-provided keys, stored encrypted via Windows DPAPI
- **UI entry**: Toolbar dropdown button in `MainWindow.xaml` (sample app)
- **First-run experience**: Forced engine selection + configuration, then translate immediately
- **Undo**: Ctrl+Z restores original text via BeginChange/EndChange grouping

## Architecture

### Core Interface (in Core project)

The Core project defines only the abstraction — no network or HTTP concerns.

```csharp
// src/WpfMarkdownEditor.Core/Translation/TranslationLanguage.cs
public enum TranslationLanguage
{
    English,
    Chinese,
    Japanese,
    Korean
}

// src/WpfMarkdownEditor.Core/Translation/ITranslationProvider.cs
public interface ITranslationProvider
{
    string Name { get; }
    bool IsConfigured { get; }

    Task<TranslationResult> TranslateAsync(
        string text,
        TranslationLanguage targetLanguage,
        CancellationToken cancellationToken);
}

// src/WpfMarkdownEditor.Core/Translation/TranslationResult.cs
public record TranslationResult(
    string TranslatedText,
    TranslationLanguage DetectedSourceLanguage);
```

Note: Target languages are a fixed app-level enum, not provider-specific. This avoids UI complexity when switching engines and provides compile-time type safety.

### Service & Provider Layer (in Wpf project)

All HTTP/network logic lives in the Wpf project, following the same pattern as `IImageResolver` (Core) / `ImageLoader` (Wpf).

```csharp
// src/WpfMarkdownEditor.Wpf/Translation/TranslationService.cs
// Orchestrates provider selection, retry, and returns result to caller.
// Does NOT hold a reference to TextBox or any UI control.
// Caller (MainWindow) receives TranslationResult and updates UI.
public class TranslationService
{
    ITranslationProvider CurrentProvider { get; }
    Task<TranslationResult> TranslateAsync(string text, TranslationLanguage targetLanguage, CancellationToken ct);
}
```

### File Layout

```
src/WpfMarkdownEditor.Core/
├── Translation/
│   ├── TranslationLanguage.cs        # Language enum
│   ├── ITranslationProvider.cs      # Interface only
│   └── TranslationResult.cs         # Result record

src/WpfMarkdownEditor.Wpf/
├── Translation/
│   ├── TranslationService.cs         # Orchestration (retry, provider selection)
│   └── Providers/
│       ├── BaiduTranslateProvider.cs
│       └── OpenAICompatibleProvider.cs
├── Services/
│   └── TranslationSettingsService.cs # DPAPI-encrypted config persistence
├── Dialogs/
│   ├── TranslationConfigDialog.xaml  # Modal Window for engine config
│   └── TranslationConfigDialog.xaml.cs

samples/WpfMarkdownEditor.Sample/
├── MainWindow.xaml                   # Translation dropdown added to toolbar here
└── MainWindow.xaml.cs                # Click handlers + translation coordination
```

## Provider Implementations

### BaiduTranslateProvider

- **API**: `https://fanyi-api.baidu.com/api/trans/vip/translate`
- **Source language**: `auto` (auto-detect)
- **Auth**: MD5 signature (`appid + query + salt + secretKey`)
- **Rate limit**: 1 QPS (standard), 10 QPS (advanced)
- **Max per request**: 6000 characters; longer documents split into segments

**Markdown handling strategy**: Send each paragraph as raw text (including markdown markers like #, *, -, >) directly to Baidu. The API will translate the natural language portions while generally preserving structural markers. Known limitation: inline markdown (bold, links, images) may not be perfectly preserved. For best markdown fidelity, users should prefer the OpenAI-compatible provider.

**Segmentation for long documents**: Split at paragraph boundaries (double newlines). Send each segment sequentially with a 1.1-second delay between requests to respect the 1 QPS rate limit. Report progress per segment (e.g., "Translating... 3/10 segments").

### OpenAICompatibleProvider

- **API**: OpenAI Chat Completions compatible format
- **Auth**: Bearer token (API Key)
- **Source language**: Auto-detected by the model
- **Prompt**: "Translate the following text to {targetLanguage.DisplayName()}. Preserve all Markdown formatting exactly as-is." (enum converted to display name via helper method)
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

Added to the toolbar in `samples/WpfMarkdownEditor.Sample/MainWindow.xaml`, positioned before the Theme Picker:

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
3. User selects engine -> show config for that engine only.

   **Baidu Translate config:**
   ```
   ┌─ Configure Baidu Translate ─────────┐
   │                                     │
   │  App ID:     [________________]     │
   │  Secret Key: [________________]     │
   │                                     │
   │  [Save & Translate]  [Cancel]       │
   └─────────────────────────────────────┘
   ```

   **OpenAI Compatible config:**
   ```
   ┌─ Configure OpenAI Compatible ───────┐
   │                                     │
   │  Service: [Tongyi Qwen  v]         │
   │           (Tongyi Qwen / Zhipu GLM │
   │            / DeepSeek / OpenAI /    │
   │            Custom)                  │
   │                                     │
   │  API Address: [auto-filled by above]│
   │  API Key:     [________________]    │
   │  Model:       [auto-filled, editable]│
   │                                     │
   │  [Save & Translate]  [Cancel]       │
   └─────────────────────────────────────┘
   ```
   Selecting a pre-configured service auto-fills API Address and Model.
   Selecting "Custom" enables manual entry for all fields.

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

### Coordination (MainWindow.xaml.cs)

`MainWindow.xaml.cs` orchestrates the flow. `TranslationService` is a pure service with no UI references. The flow:

```
User clicks "-> Language"
    │
    ├─ Engine configured? ──No──> Show config dialog ──> Config saved
    │                                                        │
    │<───────────────────────────────────────────────────────┘
    │ Yes
    ▼
Update UI: status bar "Translating...", translate button becomes "Cancel"
Store CancellationTokenSource for cancellation support
    │
    ▼
var result = await TranslationService.TranslateAsync(
    editor.Markdown, targetLanguage, cancellationToken);
    │                                   │
    │ Success                           │ Failure
    ▼                                   ▼
Dispatcher.Invoke(() => {           Show error in status bar
  textBox.BeginChange();            Original text unchanged
  textBox.SelectAll();
  textBox.SelectedText = result.TranslatedText;
  textBox.EndChange();
});
Status bar: "Translated (detected: {result.DetectedSourceLanguage}) -> {targetLanguage}"
    │
    ▼
User can Ctrl+Z to undo (single undo unit via BeginChange/EndChange)
```

### Cancel Button Behavior

The translate toolbar button toggles between two states:
- **Idle**: Shows "Translate v" dropdown with language options
- **Translating**: Shows a "Cancel Translation" button (no dropdown). Clicking it calls `CancellationTokenSource.Cancel()`. When translation completes or is cancelled, the button reverts to the idle dropdown state.

The `CancellationTokenSource` is held by `MainWindow` and disposed after each translation operation completes.

### Undo Support

Use `TextBox.BeginChange()` / `EndChange()` to group the text replacement into a single undo unit:

```csharp
textBox.BeginChange();
textBox.SelectAll();
textBox.SelectedText = result.TranslatedText;
textBox.EndChange();
```

This guarantees Ctrl+Z restores the entire original text in one step. No need to set `UndoLimit` as the pre-translation state is always the most recent undo entry.

### Thread Marshaling

All UI updates after the async translation completes are marshaled to the UI thread via `Dispatcher.Invoke()`. This follows the same pattern as `RenderPreview` in the existing codebase which uses `DispatcherTimer`.

## Error Handling

| Scenario | Response |
|----------|----------|
| Network unreachable | Status bar: "Cannot connect to translation service" |
| Invalid API Key | Status bar: "Invalid API Key, please check settings" |
| Insufficient balance | Status bar: "Translation service balance insufficient" |
| Rate limit exceeded | Auto-retry with backoff (max 3 retries, 2s/4s/8s) |
| Document too long | Baidu: auto-segment with 1.1s inter-segment delay; OpenAI: send full document |
| User cancels | CancellationTokenSource.Cancel(), original text unchanged |

## API Key Security

- Stored at `{AppContext.BaseDirectory}/translation.json` (consistent with existing file storage conventions)
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
- [ ] After translation: Ctrl+Z restores original text in one step
- [ ] Network error: error message shown, text unchanged
- [ ] Invalid API Key: error message shown
- [ ] Cancel translation during progress
- [ ] Toolbar UI works with all theme switches
- [ ] Baidu Translate: long document segments correctly with progress
