# UI Localization Design

## Summary

Add application UI localization with a language selector that supports English and Chinese at runtime. The feature covers the sample application shell and the reusable WPF library UI, while keeping Markdown document content and Markdown preview rendering outside the localization boundary.

## Current State

The project has two different language-related concerns today:

- Markdown translation translates document content through providers such as Baidu and OpenAI-compatible services.
- Application UI text is mostly hardcoded in XAML and code-behind across the sample app and `WpfMarkdownEditor.Wpf`.

This design is only for application UI localization. It does not change Markdown parsing, rendering, document text, translated preview text, or provider-returned content.

## Goals

- Add `View > Language` with `English` and `中文` choices.
- Use the system UI language on first launch: Chinese systems default to Chinese; all other systems default to English.
- Persist the user's manual language choice in the sample application's local settings directory.
- Allow runtime switching without restarting the app.
- Provide a backend-friendly localization interface for dynamic messages generated outside XAML.
- Cover the full visible application UI: sample main window, sample dialogs, library controls, library dialogs, tooltips, status messages, file dialog filters, message boxes, and translation progress UI.
- Keep the implementation efficient: language switching must not trigger Markdown reparse, preview rerender, editor content replacement, theme reset, or cursor reset.

## Non-goals

- Do not localize Markdown source text.
- Do not localize rendered Markdown preview content.
- Do not localize translated preview content produced by translation providers.
- Do not localize user data such as file names, paths, API keys, model names, or custom service names.
- Do not make the core parser or renderer depend on localization services.
- Do not add more languages in this change, though the design should leave room for later expansion.

## Architecture

Localization lives in `WpfMarkdownEditor.Wpf` as a reusable UI service. Persistence stays in the sample application so the library does not silently own host application state.

Proposed WPF library additions:

| File | Purpose |
| --- | --- |
| `src/WpfMarkdownEditor.Wpf/Localization/SupportedLanguage.cs` | Defines supported language descriptors for `en-US` and `zh-CN`. |
| `src/WpfMarkdownEditor.Wpf/Localization/IStringLocalizer.cs` | Backend-safe interface for dynamic text. |
| `src/WpfMarkdownEditor.Wpf/Localization/LocalizationService.cs` | Current language, language change event, resource dictionary application, string lookup, formatted lookup. |
| `src/WpfMarkdownEditor.Wpf/Resources/Localization.en-US.xaml` | English UI string resources. |
| `src/WpfMarkdownEditor.Wpf/Resources/Localization.zh-CN.xaml` | Chinese UI string resources. |

Proposed sample additions:

| File | Purpose |
| --- | --- |
| `samples/WpfMarkdownEditor.Sample/LocalizationSettingsService.cs` | Reads and writes the selected language in local app data. |

The settings service should persist the selected language code string, such as `en-US` or `zh-CN`. The backing file may be JSON or another simple local format, but malformed settings include unreadable content, a missing language code, or a code that is not in the supported language registry.

The sample app initializes localization during startup, before showing `MainWindow`:

1. Read the saved language setting.
2. If missing or invalid, infer the default from `CultureInfo.CurrentUICulture`.
3. After `Application.Current` is available, apply the language through `LocalizationService`.
4. Create the main window.

`SupportedLanguage` should be a small immutable descriptor rather than a closed enum. It should expose a stable code such as `en-US` or `zh-CN`, a resource dictionary URI, and a display key. The initial registry contains only English and Chinese, but the rest of the design should depend on language codes instead of switch-heavy enum logic where practical.

`SupportedLanguage` equality must be based on the stable language code using ordinal string comparison. It should implement `IEquatable<SupportedLanguage>` and override `Equals` and `GetHashCode`. This lets `SetLanguage` detect no-op language changes even if two different descriptor instances represent the same code.

Resource keys should use dot-separated namespaces:

- `Common.*` for shared commands and captions.
- `MainWindow.*` for sample shell text.
- `Editor.*` for `MarkdownEditor` UI text.
- `Dialog.<DialogName>.*` for dialog-specific text.
- `Translation.*` for translation feature UI and progress messages.
- `Status.*` for status bar templates.
- `Error.*` for user-facing error templates.

Keys should describe intent rather than control names when possible, for example `Common.Cancel`, `Editor.ZoomIn`, and `Status.FileLoaded`.

## Backend Interface

Dynamic text must use a service interface rather than direct English or Chinese string literals:

```csharp
public interface IStringLocalizer
{
    SupportedLanguage CurrentLanguage { get; }
    string GetString(string key);
    string Format(string key, params object[] args);
}
```

`LocalizationService` implements this interface. Services and code-behind can receive `IStringLocalizer` through constructors or properties. If a library service is used without an injected localizer, it should fall back to a default English localizer so host apps are not forced to initialize WPF application resources.

The fallback localizer must be independent from `Application.Current.Resources`. It should be an immutable, thread-safe singleton or an instance that owns a read-only English string map. It must not mutate WPF resources, subscribe to language changes, or share mutable state with the application-level localization service.

The WPF application should use one application-level `LocalizationService` instance. Create it once in `App.xaml.cs`, keep ownership there, and pass it to windows and services that need localization. Avoid mixing this with a global static `Instance` access pattern; one ownership path keeps the mutable resource-dictionary state clear. There must not be multiple mutable services racing to modify `Application.Current.Resources`.

Language changes use a typed event:

```csharp
public event EventHandler<LanguageChangedEventArgs>? LanguageChanged;

public sealed class LanguageChangedEventArgs : EventArgs
{
    public SupportedLanguage? OldLanguage { get; }
    public SupportedLanguage NewLanguage { get; }
}
```

Subscribers should use `args.NewLanguage` for selection state and refresh logic instead of rereading global state.

On the first effective `SetLanguage` call, `OldLanguage` is `null`. Subsequent effective changes carry the previous language in `OldLanguage`.

Expected usage:

```csharp
localizer.GetString("Common.Cancel");
localizer.Format("Status.FileLoaded", filePath);
localizer.Format("Translation.Progress.ConnectingToProvider", providerName);
```

This makes runtime-generated messages localizable without scanning the visual tree or hardcoding UI text in backend code.

## Dynamic Text Rules

Runtime text falls into three categories:

| Category | Rule |
| --- | --- |
| Fixed UI templates | Use localization keys and `Format`, for example `Loaded: {0}` or `Network error: {0}`. |
| Dynamic controls | Rebuild or refresh only the affected controls on `LanguageChanged`, for example language menu items, history empty states, outline empty states, and translation menu labels. |
| User or external data | Do not translate. Insert as parameters into localized templates. |

`TranslationLanguage.DisplayName()` can remain only for non-UI contexts such as logs, diagnostics, and debugging. All user-facing language names must go through `IStringLocalizer`. For example, English UI may show `Chinese`; Chinese UI may show `中文`.

Table insertion currently writes document content such as `Column 1` and `Cell 1`. Because this content becomes Markdown document data, it is outside automatic UI localization. This change should not rewrite existing document content on language switch.

## UI Coverage

The implementation should localize:

- Main window title suffix and menu headers: File, Edit, Paragraph, Format, Insert, View, Tools.
- Menu items, sidebar labels, empty states, search result messages, status bar messages, and tooltips.
- `View > Themes` label and new `View > Language` label.
- Save confirmation dialog.
- Table insert dialog.
- `MarkdownEditor` context menu and preview toolbar tooltips.
- Translation configuration dialog.
- Translation progress overlay.
- MessageBox captions and message templates.
- Open and save file dialog filters.

The implementation should not localize:

- Theme brand names such as GitHub and Claude.
- User file names and paths.
- Markdown editor text, Markdown preview content, and translated preview content.
- Provider names and custom service names.
- API endpoint, API key, and model fields entered by the user.

## Language Selection

The language selector belongs in the existing `View` menu, below the theme list:

- Add a divider below the theme list.
- Add a `Language` section label.
- Add radio-style options for `English` and `中文`.
- Selecting an option updates the active language immediately, closes the menu, and saves the setting.

The active language radio option must stay selected after startup and after language changes.

## Runtime Refresh

Language changes follow this flow:

1. User selects a language from `View > Language`.
2. Sample calls `LocalizationService.SetLanguage(language)`.
3. The service replaces the active localization resource dictionary and raises `LanguageChanged`.
4. Sample persists the language setting.
5. Each subscribed window or control refreshes only its own dynamic text.

`SetLanguage` must mutate `Application.Current.Resources` on the WPF UI thread. The service owns that responsibility: if `Application.Current` exists and the caller is not on the UI thread, `SetLanguage` should marshal the resource update and event raise through `Application.Current.Dispatcher.Invoke` or `InvokeAsync`. Callers may invoke `SetLanguage` from any thread, but subscribers should expect `LanguageChanged` to be raised on the UI thread when a WPF application exists. If no WPF application is available, the service may update only its current language and non-WPF string map.

`SetLanguage` must be a no-op when the supplied language code equals the current language code. The no-op path must not replace resource dictionaries, must not raise `LanguageChanged`, and must not trigger dynamic UI refresh work.

Controls must avoid strong-reference event leaks when subscribing to language changes. Use `WeakEventManager` for `LanguageChanged`, or explicitly unsubscribe in `Unloaded` or `Dispose` for controls that own a clear lifecycle. This requirement applies to windows, dialogs, overlays, and reusable controls.

XAML text should use dynamic resources where practical:

```xml
Content="{DynamicResource Loc.Common.Cancel}"
ToolTip="{DynamicResource Loc.Editor.ZoomIn}"
```

Code-generated text should be refreshed with explicit methods such as `RefreshLocalizedText()` and `BuildLanguageList()`. These methods should be small and scoped to the owning component.

File dialog filters and captions should be read from the localizer every time the dialog is opened. Dialog instances should not be cached with previously localized filter strings.

## Performance

Localization must not enter high-frequency rendering or parsing paths.

- Load only the active language resource dictionary.
- Replace the language dictionary only when the user changes language.
- Prefer WPF `DynamicResource` updates for static XAML text.
- Avoid scanning the whole visual tree during language changes.
- Recompute dynamic text only for controls that own it.
- Keep language change handling idempotent so rapid repeated changes such as English -> Chinese -> English do not leave duplicate subscriptions or stale selected menu items.
- Read localization settings once during startup.
- Write localization settings only when the user manually changes language.
- Do not trigger Markdown parser, renderer, syntax highlighter, preview rerender, editor text replacement, theme reset, or caret reset during language switching.

## Error Handling

- If the saved language setting is missing, use the system-derived default.
- If the saved language setting is malformed, ignore it and use the system-derived default.
- If a localization key is missing, return the key itself rather than throwing at runtime.
- If a format string is invalid or the provided argument count does not match the template, return the unformatted template if available, otherwise return the key. Do not throw from `Format` during normal UI rendering.
- If a translation provider returns an error message, do not translate that provider text. Wrap it in a localized UI template such as `Network error: {0}` / `网络错误：{0}`.

## Testing

Add focused tests for the service boundaries:

- `LocalizationService` applies languages, returns strings, formats parameters, and falls back for missing keys.
- `LocalizationService` handles rapid repeated language changes without duplicate events or stale current language state.
- `SupportedLanguage` equality is based on the language code, so two descriptors with the same code compare equal.
- `LanguageChanged` includes old and new languages and is raised only when the effective language code changes.
- The fallback English localizer can be constructed and called without a WPF application context, and doing so does not mutate or depend on application-level `LocalizationService` state.
- `LocalizationSettingsService` persists and reads language choices, and falls back when the settings file is malformed.
- `TranslationService` progress messages use the injected `IStringLocalizer`.
- At least one UI-owner test covers language switching for a dynamic text owner, such as the translation progress overlay or a menu/list refresh method. Prefer a small presenter/helper or directly testable refresh method so the behavior can be verified without showing a real window; use interactive WPF automation only if that lighter seam cannot cover the behavior.

Run:

```powershell
dotnet test WpfMarkdownEditor.sln
```

## Acceptance Criteria

- With no saved setting, the app chooses Chinese on Chinese systems and English on other systems.
- `View > Language` switches the visible application UI immediately.
- `View > Language` radio selection accurately reflects the current language immediately after switching and after restart.
- The selected language persists across restarts.
- Dynamic backend messages are generated through `IStringLocalizer`.
- Markdown document content and preview rendering are unchanged by language switching.
- Language switching does not reset theme, editor text, caret position, or the active file.
- The app does not crash on missing settings, malformed settings, missing localization keys, or provider error messages.
- All new and existing tests pass.
