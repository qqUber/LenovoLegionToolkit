using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.WPF.Controls;

public abstract class AbstractRefreshingControl : UserControl
{
    private CancellationTokenSource? _refreshCts;
    private volatile bool _isRefreshing;
    private bool _hasInitiallyLoaded;

    protected bool IsRefreshing => _isRefreshing;

    protected virtual bool DisablesWhileRefreshing => true;

    protected AbstractRefreshingControl()
    {
        IsEnabled = false;

        Loaded += RefreshingControl_Loaded;
        IsVisibleChanged += RefreshingControl_IsVisibleChanged;
        Unloaded += RefreshingControl_Unloaded;
    }

    private async void RefreshingControl_Loaded(object sender, RoutedEventArgs e)
    {
        OnFinishedLoading();
        
        // Always refresh on load. The control is being shown, so it needs data.
        await RefreshAsync();
        _hasInitiallyLoaded = true;
    }

    private void RefreshingControl_Unloaded(object sender, RoutedEventArgs e)
    {
        // Cancel any pending refresh when control is unloaded
        _refreshCts?.Cancel();
        _refreshCts = null;
        // Don't reset _hasInitiallyLoaded here - it causes issues with Frame navigation
    }

    protected abstract void OnFinishedLoading();

    private async void RefreshingControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // Skip if we haven't finished initial loading yet (Loaded event will handle it)
        if (!_hasInitiallyLoaded)
            return;
            
        if (IsVisible)
            await RefreshAsync();
        else
        {
            // Cancel refresh when control becomes invisible
            _refreshCts?.Cancel();
        }
    }

    protected async Task RefreshAsync()
    {
        // Prevent concurrent refreshes
        if (_isRefreshing)
            return;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Refreshing control... [feature={GetType().Name}]");

        _isRefreshing = true;
        var exceptions = false;

        // Cancel any previous refresh
        _refreshCts?.Cancel();
        _refreshCts = new CancellationTokenSource();
        var token = _refreshCts.Token;

        try
        {
            if (DisablesWhileRefreshing)
                IsEnabled = false;

            // Always create a fresh task - don't cache
            await OnRefreshAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Expected when control becomes invisible
            return;
        }
        catch (NotSupportedException)
        {
            exceptions = true;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Unsupported. [feature={GetType().Name}]");
        }
        catch (Exception ex)
        {
            exceptions = true;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Exception when refreshing control. [feature={GetType().Name}]", ex);
        }
        finally
        {
            _isRefreshing = false;

            if (!token.IsCancellationRequested)
            {
                if (exceptions)
                    Visibility = Visibility.Collapsed;
                else
                    IsEnabled = true;
            }
        }
    }

    protected abstract Task OnRefreshAsync();
}
