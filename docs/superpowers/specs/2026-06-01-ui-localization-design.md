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
| `src/WpfMarkdownEditor.Wpf/Localization/SupportedLanguage.cs` | Defines `English` and `Chinese`. |
| `src/WpfMarkdownEditor.Wpf/Localization/IStringLocalizer.cs` | Backend-safe interface for dynamic text. |
| `src/WpfMarkdownEditor.Wpf/Localization/LocalizationService.cs` | Current language, language change event, resource dictionary application, string lookup, formatted lookup. |
| `src/WpfMarkdownEditor.Wpf/Resources/Localization.en-US.xaml` | English UI string resources. |
| `src/WpfMarkdownEditor.Wpf/Resources/Localization.zh-CN.xaml` | Chinese UI string resources. |

Proposed sample additions:

| File | Purpose |
| --- | --- |
| `samples/WpfMarkdownEditor.Sample/LocalizationSettingsService.cs` | Reads and writes the selected language in local app data. |

The sample app initializes localization during startup, before showing `MainWindow`:

1. Read the saved language setting.
2. If missing or invalid, infer the default from `CultureInfo.CurrentUICulture`.
3. Apply the language through `LocalizationService`.
4. Create the main window.

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

`TranslationLanguage.DisplayName()` can remain as a self-name helper, but UI menus should use localizable keys where the display language matters. For example, English UI may show `Chinese`; Chinese UI may show `中文`.

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

XAML text should use dynamic resources where practical:

```xml
Content="{DynamicResource Loc.Common.Cancel}"
ToolTip="{DynamicResource Loc.Editor.ZoomIn}"
```

Code-generated text should be refreshed with explicit methods such as `RefreshLocalizedText()` and `BuildLanguageList()`. These methods should be small and scoped to the owning component.

## Performance

Localization must not enter high-frequency rendering or parsing paths.

- Load only the active language resource dictionary.
- Replace the language dictionary only when the user changes language.
- Prefer WPF `DynamicResource` updates for static XAML text.
- Avoid scanning the whole visual tree during language changes.
- Recompute dynamic text only for controls that own it.
- Read localization settings once during startup.
- Write localization settings only when the user manually changes language.
- Do not trigger Markdown parser, renderer, syntax highlighter, preview rerender, editor text replacement, theme reset, or caret reset during language switching.

## Error Handling

- If the saved language setting is missing, use the system-derived default.
- If the saved language setting is malformed, ignore it and use the system-derived default.
- If a localization key is missing, return the key itself rather than throwing at runtime.
- If a format string is invalid, return the key or unformatted template in a fail-soft way.
- If a translation provider returns an error message, do not translate that provider text. Wrap it in a localized UI template such as `Network error: {0}` / `网络错误：{0}`.

## Testing

Add focused tests for the service boundaries:

- `LocalizationService` applies languages, returns strings, formats parameters, and falls back for missing keys.
- `LocalizationSettingsService` persists and reads language choices, and falls back when the settings file is malformed.
- `TranslationService` progress messages use the injected `IStringLocalizer`.
- At least one WPF-level test covers language switching for a dynamic UI owner, such as the translation progress overlay or a menu/list refresh method.

Run:

```powershell
dotnet test WpfMarkdownEditor.sln
```

## Acceptance Criteria

- With no saved setting, the app chooses Chinese on Chinese systems and English on other systems.
- `View > Language` switches the visible application UI immediately.
- The selected language persists across restarts.
- Dynamic backend messages are generated through `IStringLocalizer`.
- Markdown document content and preview rendering are unchanged by language switching.
- Language switching does not reset theme, editor text, caret position, or the active file.
- The app does not crash on missing settings, malformed settings, missing localization keys, or provider error messages.
- All new and existing tests pass.
