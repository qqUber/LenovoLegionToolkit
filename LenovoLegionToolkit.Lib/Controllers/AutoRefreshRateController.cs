using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Controllers;

public class AutoRefreshRateController(
    ApplicationSettings settings,
    RefreshRateFeature refreshRateFeature,
    PowerStateListener powerStateListener)
{
    private bool _started;

    public async Task StartAsync()
    {
        if (_started)
            return;

        powerStateListener.Changed += PowerStateListener_Changed;
        _started = true;

        // Apply initial state based on current power status
        if (settings.Store.AutoRefreshRateEnabled)
        {
            await ApplyRefreshRateAsync().ConfigureAwait(false);
        }
    }

    public Task StopAsync()
    {
        powerStateListener.Changed -= PowerStateListener_Changed;
        _started = false;
        return Task.CompletedTask;
    }

    private async void PowerStateListener_Changed(object? sender, PowerStateListener.ChangedEventArgs e)
    {
        if (!settings.Store.AutoRefreshRateEnabled)
            return;

        if (e.PowerStateEvent != PowerStateEvent.StatusChange || !e.PowerAdapterStateChanged)
            return;

        await ApplyRefreshRateAsync().ConfigureAwait(false);
    }

    private async Task ApplyRefreshRateAsync()
    {
        try
        {
            var powerStatus = await Power.IsPowerAdapterConnectedAsync().ConfigureAwait(false);
            var targetRate = powerStatus == PowerAdapterStatus.Connected
                ? settings.Store.OnACRefreshRate
                : settings.Store.OnBatteryRefreshRate;

            if (targetRate is null)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Target refresh rate not configured for {powerStatus}");
                return;
            }

            var currentRate = await refreshRateFeature.GetStateAsync().ConfigureAwait(false);
            if (currentRate.Equals(targetRate.Value))
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Refresh rate already at {currentRate}");
                return;
            }

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Switching refresh rate from {currentRate} to {targetRate} [powerStatus={powerStatus}]");

            await refreshRateFeature.SetStateAsync(targetRate.Value).ConfigureAwait(false);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Refresh rate switched to {targetRate}");
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to switch refresh rate", ex);
        }
    }
}
