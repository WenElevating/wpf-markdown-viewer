using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace WpfMarkdownEditor.Sample.Helpers;

/// <summary>
/// Provides smooth GridLength animation for sidebar collapse/expand.
/// WPF doesn't support animating GridLength directly, so this helper
/// uses DoubleAnimation with a per-frame callback to update ColumnDefinition.Width.
/// </summary>
public static class GridLengthAnimation
{
    /// <summary>
    /// Animates a ColumnDefinition's Width from its current value to a target value.
    /// </summary>
    public static void AnimateColumnWidth(ColumnDefinition column, double targetWidth, double durationMs = 200)
    {
        var currentWidth = column.ActualWidth;

        if (Math.Abs(currentWidth - targetWidth) < 0.5)
        {
            column.Width = new GridLength(targetWidth);
            return;
        }

        var animation = new DoubleAnimation
        {
            From = currentWidth,
            To = targetWidth,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        var dummy = new FrameworkElement();

        animation.CurrentTimeInvalidated += (s, e) =>
        {
            if (s is Clock clock && clock.CurrentProgress.HasValue)
            {
                var progress = clock.CurrentProgress.Value;
                var currentValue = currentWidth + (targetWidth - currentWidth) * progress;
                column.Width = new GridLength(Math.Max(0, currentValue));
            }
        };

        animation.Completed += (s, e) =>
        {
            column.Width = new GridLength(targetWidth);
            dummy.BeginAnimation(FrameworkElement.WidthProperty, null);
        };

        dummy.BeginAnimation(FrameworkElement.WidthProperty, animation);
    }
}
