using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfMarkdownEditor.Wpf.Localization;
using WpfMarkdownEditor.Wpf.Services;
using WpfMarkdownEditor.Wpf.Translation.Providers;

namespace WpfMarkdownEditor.Wpf.Dialogs;

public sealed partial class TranslationConfigDialog : Window
{
    public ProviderConfig? SavedConfig { get; private set; }

    private readonly string? _preselectedProvider;
    private readonly IStringLocalizer _localizer;

    public TranslationConfigDialog(
        bool isFirstRun = true,
        string? preselectedProvider = null,
        ProviderConfig? existingConfig = null,
        IStringLocalizer? localizer = null)
    {
        _localizer = localizer ?? FallbackStringLocalizer.Instance;
        InitializeComponent();

        _preselectedProvider = preselectedProvider;

        ServiceComboBox.Items.Add(_localizer.GetString("Dialog.TranslationConfig.Custom"));
        foreach (var preset in OpenAIPresets.All)
            ServiceComboBox.Items.Insert(0, preset.Name);
        ServiceComboBox.SelectedIndex = 0;

        if (isFirstRun)
        {
            EngineSelectionPanel.Visibility = Visibility.Visible;
            BaiduConfigPanel.Visibility = Visibility.Collapsed;
            OpenAIConfigPanel.Visibility = Visibility.Collapsed;
            DialogTitle.Text = _localizer.GetString("Dialog.TranslationConfig.SelectEngine");
        }
        else
        {
            EngineSelectionPanel.Visibility = Visibility.Collapsed;
            ShowConfigForProvider(preselectedProvider ?? "Baidu", existingConfig);
        }
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void OnDialogPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.V || Keyboard.Modifiers != ModifierKeys.Control) return;

        switch (Keyboard.FocusedElement)
        {
            case TextBox tb:
                tb.Paste();
                e.Handled = true;
                break;
            case PasswordBox pb:
                pb.Password = Clipboard.GetText();
                e.Handled = true;
                break;
        }
    }

    private void OnBaiduCardClick(object sender, MouseButtonEventArgs e)
    {
        EngineBaidu.IsChecked = true;
        UpdateEngineCardHighlight();
    }

    private void OnOpenAICardClick(object sender, MouseButtonEventArgs e)
    {
        EngineOpenAI.IsChecked = true;
        UpdateEngineCardHighlight();
    }

    private void UpdateEngineCardHighlight()
    {
        var isBaidu = EngineBaidu.IsChecked == true;
        BaiduCard.BorderBrush = isBaidu
            ? (Brush)FindResource("AccentBrush")
            : (Brush)FindResource("DividerBrush");
        BaiduCard.Background = isBaidu
            ? (Brush)FindResource("HoverBackgroundBrush")
            : Brushes.Transparent;
        BaiduCard.BorderThickness = isBaidu ? new Thickness(2) : new Thickness(1);

        OpenAICard.BorderBrush = !isBaidu
            ? (Brush)FindResource("AccentBrush")
            : (Brush)FindResource("DividerBrush");
        OpenAICard.Background = !isBaidu
            ? (Brush)FindResource("HoverBackgroundBrush")
            : Brushes.Transparent;
        OpenAICard.BorderThickness = !isBaidu ? new Thickness(2) : new Thickness(1);
    }

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        var selectedProvider = EngineBaidu.IsChecked == true ? "Baidu" : "OpenAI";
        EngineSelectionPanel.Visibility = Visibility.Collapsed;
        ShowConfigForProvider(selectedProvider);
    }

    private void ShowConfigForProvider(string providerName, ProviderConfig? existingConfig = null)
    {
        if (providerName == "Baidu")
        {
            DialogTitle.Text = _localizer.GetString("Dialog.TranslationConfig.BaiduName");
            BaiduConfigPanel.Visibility = Visibility.Visible;
            OpenAIConfigPanel.Visibility = Visibility.Collapsed;

            // Populate existing config
            if (existingConfig != null)
            {
                BaiduAppId.Text = existingConfig.AppId ?? "";
                BaiduSecretKey.Password = existingConfig.SecretKey ?? "";
            }
        }
        else
        {
            DialogTitle.Text = _localizer.GetString("Dialog.TranslationConfig.OpenAIName");
            OpenAIConfigPanel.Visibility = Visibility.Visible;
            BaiduConfigPanel.Visibility = Visibility.Collapsed;

            // Populate existing config
            if (existingConfig != null)
            {
                ApiEndpoint.Text = existingConfig.ApiEndpoint ?? "";
                ApiKey.Password = existingConfig.ApiKey ?? "";
                ModelName.Text = existingConfig.ModelName ?? "";

                // Match preset if endpoint matches
                var matchingPreset = OpenAIPresets.All.FirstOrDefault(p => p.Endpoint == existingConfig.ApiEndpoint);
                if (matchingPreset != null)
                    ServiceComboBox.SelectedItem = matchingPreset.Name;
                else
                    ServiceComboBox.SelectedItem = _localizer.GetString("Dialog.TranslationConfig.Custom");
            }
        }
    }

    private void OnServiceChanged(object sender, RoutedEventArgs e)
    {
        var selected = ServiceComboBox.SelectedItem as string;
        var preset = OpenAIPresets.FindByName(selected ?? "");
        if (preset != null)
        {
            ApiEndpoint.Text = preset.Endpoint;
            ModelName.Text = preset.DefaultModel;
            ApiEndpoint.IsReadOnly = true;
        }
        else
        {
            ApiEndpoint.IsReadOnly = false;
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (BaiduConfigPanel.Visibility == Visibility.Visible)
        {
            if (string.IsNullOrWhiteSpace(BaiduAppId.Text) || string.IsNullOrWhiteSpace(BaiduSecretKey.Password))
            {
                ShowValidationError(_localizer.GetString("Dialog.TranslationConfig.Validation.Baidu"));
                return;
            }
            SavedConfig = new ProviderConfig("Baidu")
            {
                AppId = BaiduAppId.Text.Trim(),
                SecretKey = BaiduSecretKey.Password
            };
        }
        else
        {
            if (string.IsNullOrWhiteSpace(ApiEndpoint.Text) || string.IsNullOrWhiteSpace(ApiKey.Password))
            {
                ShowValidationError(_localizer.GetString("Dialog.TranslationConfig.Validation.OpenAI"));
                return;
            }
            SavedConfig = new ProviderConfig("OpenAI")
            {
                ApiEndpoint = ApiEndpoint.Text.Trim(),
                ApiKey = ApiKey.Password,
                ModelName = string.IsNullOrWhiteSpace(ModelName.Text) ? "gpt-4o-mini" : ModelName.Text.Trim()
            };
        }

        DialogResult = true;
    }

    private void OnLinkClick(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void ShowValidationError(string message)
    {
        // Flash the border red briefly — simple inline validation
        MessageBox.Show(message, _localizer.GetString("Common.Validation"), MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
