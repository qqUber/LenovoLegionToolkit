using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LenovoLegionToolkit.WPF.Controls;

public class LoadableControl : UserControl
{
    private readonly ContentPresenter _contentPresenter = new();

    // Use custom Windows 11-style spinning loader
    private readonly SpinningLoader _spinningLoader = new()
    {
        VerticalAlignment = VerticalAlignment.Top,
        HorizontalAlignment = HorizontalAlignment.Center,
    };

    private bool _isLoading = true;

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            UpdateLoadingState();
        }
    }

    public bool IsIndeterminate { get; set; } = true;  // Keep for compatibility

    public double Progress { get; set; }  // Keep for compatibility

    public double IndicatorWidth
    {
        get => _spinningLoader.Width;
        set => _spinningLoader.Width = value;
    }

    public double IndicatorHeight
    {
        get => _spinningLoader.Height;
        set => _spinningLoader.Height = value;
    }

    public HorizontalAlignment IndicatorHorizontalAlignment
    {
        get => _spinningLoader.HorizontalAlignment;
        set => _spinningLoader.HorizontalAlignment = value;
    }

    public VerticalAlignment IndicatorVerticalAlignment
    {
        get => _spinningLoader.VerticalAlignment;
        set => _spinningLoader.VerticalAlignment = value;
    }

    public Thickness IndicatorMargin
    {
        get => _spinningLoader.Margin;
        set => _spinningLoader.Margin = value;
    }

    public Visibility ContentVisibilityWhileLoading { get; set; } = Visibility.Hidden;

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);

        _contentPresenter.Content = Content;

        var grid = new Grid();
        grid.Children.Add(_contentPresenter);
        grid.Children.Add(_spinningLoader);

        UpdateLoadingState();

        Content = grid;
    }

    private void UpdateLoadingState()
    {
        _contentPresenter.Visibility = IsLoading ? ContentVisibilityWhileLoading : Visibility.Visible;
        _spinningLoader.Visibility = IsLoading ? Visibility.Visible : Visibility.Hidden;
    }
}

