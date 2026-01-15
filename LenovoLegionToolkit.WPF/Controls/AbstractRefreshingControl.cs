using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.WPF.Controls;

/// <summary>
/// Base control that automatically refreshes its data when loaded or becomes visible.
/// Supports cancellation when control is unloaded or becomes invisible.
/// </summary>
public abstract class AbstractRefreshingControl : UserControl
{
    private CancellationTokenSource? _refreshCts;
    private volatile bool _isRefreshing;
    private bool _hasInitiallyLoaded;

    protected bool IsRefreshing => _isRefreshing;
    protected virtual bool DisablesWhileRefreshing => true;
    protected CancellationToken RefreshCancellationToken => _refreshCts?.Token ?? CancellationToken.None;

    protected AbstractRefreshingControl()
    {
        IsEnabled = false;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        OnFinishedLoading();
        await RefreshAsync();
        _hasInitiallyLoaded = true;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CancelPendingRefresh();
    }

    private async void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!_hasInitiallyLoaded)
            return;

        if (IsVisible)
            await RefreshAsync();
        else
            CancelPendingRefresh();
    }

    protected abstract void OnFinishedLoading();
    protected abstract Task OnRefreshAsync();

    protected async Task RefreshAsync()
    {
        if (_isRefreshing)
            return;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Refreshing control... [feature={GetType().Name}]");

        _isRefreshing = true;
        var hadExceptions = false;

        CancelPendingRefresh();
        _refreshCts = new CancellationTokenSource();
        var token = _refreshCts.Token;

        try
        {
            if (DisablesWhileRefreshing)
                IsEnabled = false;

            await OnRefreshAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return; // Expected when control becomes invisible
        }
        catch (NotSupportedException)
        {
            hadExceptions = true;
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Unsupported. [feature={GetType().Name}]");
        }
        catch (Exception ex)
        {
            hadExceptions = true;
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Exception when refreshing control. [feature={GetType().Name}]", ex);
        }
        finally
        {
            _isRefreshing = false;

            if (!token.IsCancellationRequested)
            {
                Visibility = hadExceptions ? Visibility.Collapsed : Visibility.Visible;
                IsEnabled = !hadExceptions;
            }
        }
    }

    private void CancelPendingRefresh()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = null;
    }
}
