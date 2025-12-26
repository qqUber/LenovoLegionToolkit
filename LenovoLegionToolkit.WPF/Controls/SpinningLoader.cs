using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace LenovoLegionToolkit.WPF.Controls;

/// <summary>
/// A Windows 11-style spinning dots loading indicator.
/// </summary>
public class SpinningLoader : UserControl
{
    private readonly Canvas _canvas;
    private readonly Storyboard _storyboard;
    private const int DotCount = 5;
    private const double DotSize = 6;
    private const double Radius = 20;

    public SpinningLoader()
    {
        Width = 56;
        Height = 56;
        
        _canvas = new Canvas
        {
            Width = 56,
            Height = 56
        };
        
        _storyboard = new Storyboard
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        
        CreateDots();
        
        Content = _canvas;
        
        Loaded += (_, _) => _storyboard.Begin();
        Unloaded += (_, _) => _storyboard.Stop();
        IsVisibleChanged += (_, e) =>
        {
            if ((bool)e.NewValue)
                _storyboard.Resume();
            else
                _storyboard.Pause();
        };
    }

    private void CreateDots()
    {
        var centerX = Width / 2;
        var centerY = Height / 2;
        
        for (int i = 0; i < DotCount; i++)
        {
            var dot = new Ellipse
            {
                Width = DotSize,
                Height = DotSize,
                Fill = new SolidColorBrush(Color.FromRgb(96, 165, 250)), // Nice blue color
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            
            // Position dot at the top of the circle
            Canvas.SetLeft(dot, centerX - DotSize / 2);
            Canvas.SetTop(dot, centerY - Radius - DotSize / 2);
            
            // Create transform group for rotation around center
            var transformGroup = new TransformGroup();
            var rotateTransform = new RotateTransform(0, 0, Radius);
            transformGroup.Children.Add(rotateTransform);
            dot.RenderTransform = transformGroup;
            
            // Create rotation animation with delay for each dot
            var animation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromSeconds(1.2),
                RepeatBehavior = RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromMilliseconds(i * 80), // Staggered start
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            
            Storyboard.SetTarget(animation, dot);
            Storyboard.SetTargetProperty(animation, new PropertyPath("(UIElement.RenderTransform).(TransformGroup.Children)[0].(RotateTransform.Angle)"));
            
            _storyboard.Children.Add(animation);
            
            // Fade animation for trail effect
            var opacityAnimation = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromSeconds(1.2),
                RepeatBehavior = RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromMilliseconds(i * 80)
            };
            
            opacityAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromPercent(0)));
            opacityAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(0.3, KeyTime.FromPercent(0.5)));
            opacityAnimation.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromPercent(1)));
            
            Storyboard.SetTarget(opacityAnimation, dot);
            Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("Opacity"));
            
            _storyboard.Children.Add(opacityAnimation);
            
            _canvas.Children.Add(dot);
        }
    }
}
