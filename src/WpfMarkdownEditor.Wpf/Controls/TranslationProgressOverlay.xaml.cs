using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WpfMarkdownEditor.Core.Translation;

namespace WpfMarkdownEditor.Wpf.Controls;

public sealed partial class TranslationProgressOverlay : IDisposable
{
    private DispatcherTimer? _elapsedTimer;
    private DateTime _startTime;
    private bool _isError;

    public event EventHandler? CancelRequested;
    public event EventHandler? RetryRequested;
    public event EventHandler? CloseRequested;

    public TranslationProgressOverlay()
    {
        InitializeComponent();
    }

    public void Show()
    {
        _isError = false;
        Visibility = Visibility.Visible;
        _startTime = DateTime.Now;
        ErrorText.Visibility = Visibility.Collapsed;
        RetryButton.Visibility = Visibility.Collapsed;
        CloseButton.Visibility = Visibility.Collapsed;
        CancelButton.Visibility = Visibility.Visible;
        ProgressBar.IsIndeterminate = true;
        StartSpinAnimation();
        StartElapsedTimer();
        AnimateCardIn();
    }

    public void Hide()
    {
        StopSpinAnimation();
        _elapsedTimer?.Stop();
        AnimateCardOut(() => { Visibility = Visibility.Collapsed; });
    }

    public void UpdateProgress(TranslationProgress progress)
    {
        if (_isError) return;

        StatusTitle.Text = progress.Stage switch
        {
            TranslationStage.Connecting => "Connecting...",
            TranslationStage.Translating => "Translating...",
            TranslationStage.Completed => "Done!",
            _ => StatusTitle.Text
        };

        StatusDetail.Text = progress.Message;

        if (progress.TotalSegments > 0)
        {
            ProgressBar.IsIndeterminate = false;
            ProgressBar.Maximum = progress.TotalSegments;
            ProgressBar.Value = progress.CurrentSegment;
        }
    }

    public void ShowError(string message, bool canRetry = true)
    {
        _isError = true;
        StatusTitle.Text = "Translation Failed";
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
        RetryButton.Visibility = canRetry ? Visibility.Visible : Visibility.Collapsed;
        CloseButton.Visibility = Visibility.Visible;
        CancelButton.Visibility = Visibility.Collapsed;
        ProgressBar.IsIndeterminate = false;
        ProgressBar.Value = 0;
        StopSpinAnimation();
        _elapsedTimer?.Stop();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => CancelRequested?.Invoke(this, EventArgs.Empty);
    private void OnRetryClick(object sender, RoutedEventArgs e) => RetryRequested?.Invoke(this, EventArgs.Empty);
    private void OnCloseClick(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void StartSpinAnimation()
    {
        var animation = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.5))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        SpinTransform.BeginAnimation(RotateTransform.AngleProperty, animation);
    }

    private void StopSpinAnimation()
        => SpinTransform.BeginAnimation(RotateTransform.AngleProperty, null);

    private void StartElapsedTimer()
    {
        _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _elapsedTimer.Tick += (_, _) =>
        {
            var elapsed = DateTime.Now - _startTime;
            ElapsedText.Text = $"{(int)elapsed.TotalSeconds}s elapsed";
        };
        _elapsedTimer.Start();
    }

    private void AnimateCardIn()
    {
        var opacityAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
        var scaleAnim = new DoubleAnimation(0.95, 1, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        Card.BeginAnimation(OpacityProperty, opacityAnim);
        Card.RenderTransform = new ScaleTransform();
        Card.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        Card.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
    }

    private void AnimateCardOut(Action onComplete)
    {
        var opacityAnim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
        opacityAnim.Completed += (_, _) => onComplete();
        Card.BeginAnimation(OpacityProperty, opacityAnim);
    }

    public void Dispose()
    {
        _elapsedTimer?.Stop();
        StopSpinAnimation();
    }
}
