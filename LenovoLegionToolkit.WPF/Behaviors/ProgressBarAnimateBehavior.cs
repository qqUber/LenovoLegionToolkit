using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using Microsoft.Xaml.Behaviors;

namespace LenovoLegionToolkit.WPF.Behaviors;

public class ProgressBarAnimateBehavior : Behavior<ProgressBar>
{
    private bool _isAnimating;
    private double _lastValue = double.MinValue;
    
    // Reuse animation object to reduce allocations
    private DoubleAnimation? _animation;

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.ValueChanged += ProgressBar_ValueChanged;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.ValueChanged -= ProgressBar_ValueChanged;
        _animation = null;
    }

    private void ProgressBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is not ProgressBar progressBar)
            return;

        if (_isAnimating)
            return;

        // Skip animation for small changes (reduces CPU usage)
        if (Math.Abs(e.NewValue - _lastValue) < 0.5)
        {
            progressBar.Value = e.NewValue;
            return;
        }

        _lastValue = e.NewValue;
        _isAnimating = true;

        // Reuse or create animation
        _animation ??= new DoubleAnimation
        {
            Duration = new Duration(TimeSpan.FromMilliseconds(150)), // Faster animation
            FillBehavior = FillBehavior.Stop,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        
        _animation.From = e.OldValue;
        _animation.To = e.NewValue;
        _animation.Completed += Completed;

        progressBar.BeginAnimation(RangeBase.ValueProperty, _animation, HandoffBehavior.SnapshotAndReplace);

        e.Handled = true;
    }

    private void Completed(object? sender, EventArgs e)
    {
        _isAnimating = false;
        if (_animation is not null)
            _animation.Completed -= Completed;
    }
}
