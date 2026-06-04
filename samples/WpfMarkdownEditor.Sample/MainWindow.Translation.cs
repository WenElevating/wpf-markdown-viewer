using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using WpfMarkdownEditor.Core.Translation;
using WpfMarkdownEditor.Wpf.Controls;
using WpfMarkdownEditor.Wpf.Dialogs;

namespace WpfMarkdownEditor.Sample;

public partial class MainWindow
{
    private static string GetTranslationSettingsDirectory()
        => System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WpfMarkdownEditor.Sample");

    private void OpenTranslationSettings()
    {
        var coordinator = _translationCoordinator;
        var activeProvider = coordinator.ActiveProvider ?? "Baidu";
        var existingConfig = coordinator.LoadProviderConfig(activeProvider);
        var dialog = new TranslationConfigDialog(
            isFirstRun: false,
            preselectedProvider: activeProvider,
            existingConfig: existingConfig,
            localizer: _localizationService)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
            coordinator.SaveConfig(dialog.SavedConfig!);
    }

    private void OnTranslatePopupOpened(object? sender, EventArgs e)
    {
        var coordinator = _translationCoordinator;
        var activeProvider = coordinator.ActiveProvider;
        if (activeProvider == null || coordinator.LoadActiveProviderConfig()?.IsComplete != true)
        {
            ToolsPopup.IsOpen = false;
            var dialog = new TranslationConfigDialog(isFirstRun: true, localizer: _localizationService)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                coordinator.SaveConfig(dialog.SavedConfig!);
                activeProvider = dialog.SavedConfig!.ProviderName;
                EngineBaiduRadio.IsChecked = activeProvider == "Baidu";
                EngineOpenAIRadio.IsChecked = activeProvider == "OpenAI";
            }
        }
    }

    private void OnEngineRadioChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;

        var coordinator = _translationCoordinator;
        var newEngine = EngineBaiduRadio.IsChecked == true ? "Baidu" : "OpenAI";
        var config = coordinator.LoadProviderConfig(newEngine);

        if (config?.IsComplete != true)
        {
            ToolsPopup.IsOpen = false;
            var dialog = new TranslationConfigDialog(
                isFirstRun: false,
                preselectedProvider: newEngine,
                existingConfig: config,
                localizer: _localizationService)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
                coordinator.SaveConfig(dialog.SavedConfig!);
        }
        else
        {
            coordinator.SetActiveProvider(newEngine);
        }
    }

    private void OnTranslationSettings(object sender, RoutedEventArgs e)
    {
        ToolsPopup.IsOpen = false;
        OpenTranslationSettings();
    }

    private async Task TranslateDocumentAsync(TranslationLanguage targetLanguage)
    {
        var coordinator = _translationCoordinator;
        if (_isClosed || coordinator.IsTranslating) return;

        var activeProvider = coordinator.ActiveProvider;
        if (activeProvider == null || coordinator.LoadActiveProviderConfig()?.IsComplete != true)
        {
            ToolsPopup.IsOpen = false;
            var dialog = new TranslationConfigDialog(
                isFirstRun: activeProvider == null,
                preselectedProvider: activeProvider,
                existingConfig: activeProvider != null ? coordinator.LoadProviderConfig(activeProvider) : null,
                localizer: _localizationService)
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true) return;

            coordinator.SaveConfig(dialog.SavedConfig!);
            activeProvider = dialog.SavedConfig!.ProviderName;

            EngineBaiduRadio.IsChecked = activeProvider == "Baidu";
            EngineOpenAIRadio.IsChecked = activeProvider == "OpenAI";
        }

        CancelTranslateBtn.Visibility = Visibility.Visible;
        TranslateLanguagePanel.Visibility = Visibility.Collapsed;
        ToolsPopup.IsOpen = false;

        _progressOverlay = new TranslationProgressOverlay(_localizationService);
        _progressOverlay.CancelRequested += OnOverlayCancel;
        _progressOverlay.RetryRequested += OnOverlayRetry;
        _progressOverlay.CloseRequested += OnOverlayClose;

        var editorRootGrid = (Grid)Editor.Content;
        Grid.SetColumnSpan(_progressOverlay, editorRootGrid.ColumnDefinitions.Count);
        editorRootGrid.Children.Add(_progressOverlay);
        _progressOverlay.Show();

        var progress = new Progress<TranslationProgress>(p => _progressOverlay.UpdateProgress(p));

        try
        {
            var result = await coordinator.TranslateAsync(
                Editor.Markdown,
                targetLanguage,
                progress,
                _windowLifetimeCts.Token);
            if (_isClosed)
                return;

            Editor.RenderTranslatedPreview(result.TranslatedText);

            ClearTranslationBtn.Visibility = Visibility.Visible;
            SetStatus(
                "Status.TranslationPreview",
                GetTranslationLanguageDisplayName(result.DetectedSourceLanguage),
                GetTranslationLanguageDisplayName(targetLanguage));
            _progressOverlay.Hide();
        }
        catch (OperationCanceledException)
        {
            if (_isClosed)
                return;

            SetStatus("Status.TranslationCancelled");
            _progressOverlay.Hide();
        }
        catch (ObjectDisposedException) when (_isClosed)
        {
        }
        catch (TimeoutException ex)
        {
            if (_isClosed)
                return;

            _progressOverlay.ShowError(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            if (_isClosed)
                return;

            _progressOverlay.ShowError(_localizationService.Format("Error.Network", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            if (_isClosed)
                return;

            _progressOverlay.ShowError(ex.Message);
        }
        finally
        {
            if (!_isClosed)
            {
                CancelTranslateBtn.Visibility = Visibility.Collapsed;
                TranslateLanguagePanel.Visibility = Visibility.Visible;
            }
        }
    }

    private string GetTranslationLanguageDisplayName(TranslationLanguage language) => language switch
    {
        TranslationLanguage.English => _localizationService.GetString("Language.English"),
        TranslationLanguage.Chinese => _localizationService.GetString("Language.Chinese"),
        TranslationLanguage.Japanese => _localizationService.GetString("Translation.Language.Japanese"),
        TranslationLanguage.Korean => _localizationService.GetString("Translation.Language.Korean"),
        _ => language.ToString()
    };

    private void OnTranslateToEnglish(object sender, RoutedEventArgs e) => _ = TranslateDocumentAsync(TranslationLanguage.English);
    private void OnTranslateToChinese(object sender, RoutedEventArgs e) => _ = TranslateDocumentAsync(TranslationLanguage.Chinese);
    private void OnTranslateToJapanese(object sender, RoutedEventArgs e) => _ = TranslateDocumentAsync(TranslationLanguage.Japanese);
    private void OnTranslateToKorean(object sender, RoutedEventArgs e) => _ = TranslateDocumentAsync(TranslationLanguage.Korean);

    private void OnCancelTranslate(object sender, RoutedEventArgs e)
    {
        _translationCoordinator.Cancel();
        ToolsPopup.IsOpen = false;
    }

    private void OnClearTranslation(object sender, RoutedEventArgs e)
    {
        ToolsPopup.IsOpen = false;
        Editor.ClearTranslatedPreview();
        ClearTranslationBtn.Visibility = Visibility.Collapsed;
        SetStatus("Status.TranslationCleared");
    }

    private void OnOverlayCancel(object? sender, EventArgs e) => _translationCoordinator.Cancel();
    private void OnOverlayRetry(object? sender, EventArgs e)
        => _ = TranslateDocumentAsync(_translationCoordinator.LastTargetLanguage);

    private void OnOverlayClose(object? sender, EventArgs e)
    {
        RemoveProgressOverlay();
    }

    private void RemoveProgressOverlay()
    {
        _progressOverlay?.Hide();
        if (_progressOverlay?.Parent is Panel panel)
            panel.Children.Remove(_progressOverlay);

        if (_progressOverlay is not null)
        {
            _progressOverlay.CancelRequested -= OnOverlayCancel;
            _progressOverlay.RetryRequested -= OnOverlayRetry;
            _progressOverlay.CloseRequested -= OnOverlayClose;
            _progressOverlay = null;
        }
    }
}
