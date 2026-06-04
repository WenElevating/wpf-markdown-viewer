# Agent Guide

This repository is a .NET/WPF markdown editor. Treat this file as the first
document to read before changing code. The goal is to help an agent find the
right module quickly, make small maintainable changes, and verify them before
claiming the task is done.

## Project Map

- `src/WpfMarkdownEditor.Core` contains markdown parsing, inline/block models,
  and translation-safe markdown segment extraction. It must not depend on WPF.
- `src/WpfMarkdownEditor.Wpf` contains reusable WPF controls, rendering,
  themes, dialogs, localization, image loading, syntax highlighting, and
  translation providers.
- `src/WpfMarkdownEditor.Converters` contains conversion helpers that bridge
  markdown and WPF document types.
- `samples/WpfMarkdownEditor.Sample` is the runnable desktop app and integration
  surface for menus, settings, dialogs, and end-user workflows.
- `tests/*` mirrors the production projects. Add focused regression tests near
  the behavior being changed.
- `docs/superpowers/specs` stores design notes for larger features. Read the
  relevant spec before changing a feature that already has one.
- `assets` and `sources` contain static assets, screenshots, and reference
  material.

## Build And Run

- `dotnet build WpfMarkdownEditor.sln --no-restore`
- `dotnet test WpfMarkdownEditor.sln --no-restore`
- `dotnet test tests/WpfMarkdownEditor.Core.Tests/WpfMarkdownEditor.Core.Tests.csproj --filter <TestClassOrName>`
- `dotnet test tests/WpfMarkdownEditor.Wpf.Tests/WpfMarkdownEditor.Wpf.Tests.csproj --filter <TestClassOrName>`
- `dotnet run --project samples/WpfMarkdownEditor.Sample/WpfMarkdownEditor.Sample.csproj`
- `build-installer.bat` builds the Windows installer through `setup.iss`.

If a build reports locked DLLs, check for a running sample app first and stop it
before rebuilding.

## Development Workflow

1. Inspect the affected module before editing. Use `rg` or structural tools to
   find existing patterns, tests, and nearby helpers.
2. For bugs, reproduce or identify the failing path first. Add or update a
   regression test before the production fix whenever the behavior can be tested.
3. Make the smallest change that solves the problem. Prefer existing helpers and
   project patterns over new abstractions.
4. Keep responsibilities separated by layer: parsing in Core, WPF rendering in
   Wpf, app orchestration in Sample, conversion in Converters.
5. Verify with the narrowest relevant tests first, then run broader tests when
   the change touches shared behavior.
6. Review your own diff before finalizing. Remove dead code, accidental churn,
   unrelated formatting, debug logging, and half-finished abstractions.

Do not implement by piling more logic into one large file. If a change needs
state management, parsing, rendering, IO, and UI wiring, split those concerns
into named types with clear contracts and focused tests.

## Architecture Rules

- Core stays UI-agnostic. Do not reference WPF types from
  `WpfMarkdownEditor.Core`.
- WPF application code should follow MVVM. Views and code-behind should focus on
  UI event wiring, binding setup, and view coordination. State management,
  commands, orchestration, persistence, parsing, rendering decisions, and
  provider behavior belong in view models, services, or dedicated components.
- WPF rendering should keep parser models and visual elements loosely coupled.
  Rendering helpers belong under `src/WpfMarkdownEditor.Wpf/Rendering`.
- Avoid IO-heavy and compute-heavy work on the WPF UI thread. Use asynchronous
  services or background work for expensive operations, cache image and
  translation work where appropriate, and marshal UI access back through the UI
  thread `Dispatcher` when background work needs to update WPF objects.
- If a module supports multiple vendors or providers, program against
  interfaces so provider-specific behavior stays isolated behind clear
  contracts.
- Localization strings live in resource dictionaries and localization services,
  not hard-coded across controls.
- Translation providers must keep credentials and user-specific settings out of
  source control.
- Avoid new dependencies unless the task explicitly requires one and the
  trade-off is documented.

## Maintainability Rules

- Keep files focused. If a file is becoming hard to scan, extract a cohesive
  helper, renderer, service, model, or test fixture instead of appending another
  unrelated section.
- Avoid oversized modules. Try to keep each production `.cs` class under 500
  lines of code; test code is exempt from this guideline.
- If a production file exceeds 800 lines of code, add new functionality by
  creating a new module or file instead of extending the large file, unless
  there is a strong documented reason not to.
- Treat large, cross-cutting files as sensitive integration surfaces. This is
  especially true for `samples/WpfMarkdownEditor.Sample/MainWindow.xaml.cs`,
  which should stay focused on UI work. Except for trivial changes, avoid adding
  new standalone methods there; create a view model, service, coordinator, or
  feature-local module instead.
- When extracting code from a large module, move the related tests and relevant
  module or type documentation with the new implementation when practical. The
  conditions that prove the behavior correct should stay close to the code they
  describe.
- Prefer clear names over comments. Add comments only for non-obvious WPF,
  threading, caching, or parser edge cases.
- Do not mix unrelated refactors with feature or bug-fix changes.
- Do not duplicate parsing, rendering, localization, settings, or retry logic.
  Search for existing implementations first.
- Preserve user edits in the working tree. Never revert unrelated changes unless
  explicitly asked.
- Keep public APIs conservative. If changing a public type or behavior, update
  tests and call sites together.

## Testing Principles

- Tests use xUnit.
- Core parser and translation extraction tests belong under
  `tests/WpfMarkdownEditor.Core.Tests`.
- WPF control, rendering, localization, image loading, syntax highlighting, and
  translation provider tests belong under `tests/WpfMarkdownEditor.Wpf.Tests`.
- Converter behavior belongs under `tests/WpfMarkdownEditor.Converters.Tests`.
- WPF tests that instantiate UI elements must run on an STA thread, following
  the existing WPF test helpers.
- Name tests with the pattern `MethodOrFeature_Condition_ExpectedResult`.
- For visual/rendering fixes, test the state transition that failed, such as
  initial render, async image completion, edit refresh, cut/paste, or layout
  invalidation.
- For documentation-only changes, run `git diff --check`. For code changes, run
  the relevant focused tests and usually the full solution test suite.

## Code Review Checklist

Before finalizing any code change, check:

- The changed files match the module boundary described above.
- The diff is small enough to review and does not include unrelated cleanup.
- New behavior has regression coverage or a clear reason why it cannot.
- UI-thread, async, cancellation, caching, and disposal behavior are intentional.
- No credentials, local paths, generated artifacts, or user settings were added.
- Public names and file locations make the design easy for the next agent to
  find.
- The implementation removes duplication or keeps it contained; it does not
  create a new catch-all utility or oversized manager.

## Git And Commit Guidance

- Check `git status --short --branch` before and after edits.
- Keep commits focused on one reason for change.
- Use an intent-based subject that explains why the change exists.
- Include verification evidence in the final response or commit message.
- If committing, prefer the repository's structured trailer style when useful:
  `Constraint`, `Rejected`, `Confidence`, `Scope-risk`, `Directive`, `Tested`,
  and `Not-tested`.

## Known Sensitive Areas

- Markdown preview rendering and image loading are sensitive to WPF layout and
  asynchronous update behavior. Preserve stable image hosts, URL-level caching,
  and incremental rendering unless you have a tested replacement.
- Translation must preserve markdown structure while translating only intended
  text segments.
- The sample app is not throwaway code; it is the primary integration surface
  for real user workflows.

When in doubt, choose the boring maintainable path: understand the existing
boundary, add a focused test, make a narrow change, and verify it.
