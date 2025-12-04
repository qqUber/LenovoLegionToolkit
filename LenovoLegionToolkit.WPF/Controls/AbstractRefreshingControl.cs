using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.WPF.Controls;

public abstract class AbstractRefreshingControl : UserControl
{
    private Task? _refreshTask;
    private CancellationTokenSource? _refreshCts;
    private volatile bool _isRefreshing;

    protected bool IsRefreshing => _isRefreshing;

    protected virtual bool DisablesWhileRefreshing => true;

    protected AbstractRefreshingControl()
    {
        IsEnabled = false;

        Loaded += RefreshingControl_Loaded;
        IsVisibleChanged += RefreshingControl_IsVisibleChanged;
        Unloaded += RefreshingControl_Unloaded;
    }

    private void RefreshingControl_Loaded(object sender, RoutedEventArgs e)
    {
        OnFinishedLoading();
    }

    private void RefreshingControl_Unloaded(object sender, RoutedEventArgs e)
    {
        // Cancel any pending refresh when control is unloaded
        _refreshCts?.Cancel();
        _refreshCts = null;
    }

    protected abstract void OnFinishedLoading();

    private async void RefreshingControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
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

            _refreshTask ??= OnRefreshAsync();
            await _refreshTask.ConfigureAwait(true);
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
            _refreshTask = null;
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
