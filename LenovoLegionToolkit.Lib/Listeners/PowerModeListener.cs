using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Controllers.GodMode;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using LenovoLegionToolkit.Lib.System.Management;

namespace LenovoLegionToolkit.Lib.Listeners;

public class PowerModeListener(
    GodModeController godModeController,
    WindowsPowerModeController windowsPowerModeController,
    WindowsPowerPlanController windowsPowerPlanController)
    : AbstractWMIListener<PowerModeListener.ChangedEventArgs, PowerModeState, int>(WMI.LenovoGameZoneSmartFanModeEvent.Listen), INotifyingListener<PowerModeListener.ChangedEventArgs, PowerModeState>
{
    // Map power mode states to notification types
    private static readonly Dictionary<PowerModeState, NotificationType> NotificationTypeMap = new()
    {
        [PowerModeState.Quiet] = NotificationType.PowerModeQuiet,
        [PowerModeState.Balance] = NotificationType.PowerModeBalance,
        [PowerModeState.Performance] = NotificationType.PowerModePerformance,
        [PowerModeState.Extreme] = NotificationType.PowerModeExtreme,
        [PowerModeState.GodMode] = NotificationType.PowerModeGodMode
    };

    public class ChangedEventArgs(PowerModeState state) : EventArgs
    {
        public PowerModeState State { get; } = state;
    }

    protected override PowerModeState GetValue(int value) => (PowerModeState)(value - 1);

    protected override ChangedEventArgs GetEventArgs(PowerModeState value) => new(value);

    protected override async Task OnChangedAsync(PowerModeState value)
    {
        await ChangeDependenciesAsync(value).ConfigureAwait(false);
        PublishNotification(value);
    }

    public async Task NotifyAsync(PowerModeState value)
    {
        await ChangeDependenciesAsync(value).ConfigureAwait(false);
        RaiseChanged(value);
    }

    private async Task ChangeDependenciesAsync(PowerModeState value)
    {
        if (value is PowerModeState.GodMode)
            await godModeController.ApplyStateAsync().ConfigureAwait(false);

        // Run power mode and plan updates in parallel for faster response
        await Task.WhenAll(
            windowsPowerModeController.SetPowerModeAsync(value),
            windowsPowerPlanController.SetPowerPlanAsync(value)
        ).ConfigureAwait(false);
    }

    private static void PublishNotification(PowerModeState value)
    {
        if (NotificationTypeMap.TryGetValue(value, out var notificationType))
        {
            MessagingCenter.Publish(new NotificationMessage(notificationType, value.GetDisplayName()));
        }
    }
}
