using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Controllers.GodMode;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Features;

public class PowerModeUnavailableWithoutACException(PowerModeState powerMode) : Exception
{
    public PowerModeState PowerMode { get; } = powerMode;
}

public class PowerModeFeature(
    GodModeController godModeController,
    Lazy<GPUOverclockController> gpuOverclockControllerLazy,
    WindowsPowerModeController windowsPowerModeController,
    WindowsPowerPlanController windowsPowerPlanController,
    ThermalModeListener thermalModeListener,
    PowerModeListener powerModeListener)
    : AbstractWmiFeature<PowerModeState>(WMI.LenovoGameZoneData.GetSmartFanModeAsync, WMI.LenovoGameZoneData.SetSmartFanModeAsync, WMI.LenovoGameZoneData.IsSupportSmartFanAsync, 1)
{
    public bool AllowAllPowerModesOnBattery { get; set; }

    // Track if Extreme mode is currently active
    private bool _isExtremeActive;

    // Lazy resolve to break circular dependency
    private GPUOverclockController GpuOverclockController => gpuOverclockControllerLazy.Value;

    public override async Task<PowerModeState[]> GetAllStatesAsync()
    {
        var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);

        var states = new List<PowerModeState> { PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance };

        // Check if Extreme mode (CPU OC) is supported
        try
        {
            var supportsCpuOc = await WMI.LenovoGameZoneData.IsSupportCpuOCAsync().ConfigureAwait(false);
            var supportsGpuOc = await WMI.LenovoGameZoneData.IsSupportGpuOCAsync().ConfigureAwait(false);
            if (supportsCpuOc > 0 || supportsGpuOc > 0)
            {
                states.Add(PowerModeState.Extreme);
            }
        }
        catch
        {
            // Extreme mode not supported
        }

        if (mi.Properties.SupportsGodMode)
        {
            states.Add(PowerModeState.GodMode);
        }

        return [.. states];
    }

    /// <summary>
    /// Gets the effective power mode state, accounting for Extreme mode override.
    /// </summary>
    public async Task<PowerModeState> GetEffectiveStateAsync()
    {
        var baseState = await GetStateAsync().ConfigureAwait(false);

        // If we're in Performance mode and Extreme is active, return Extreme
        if (baseState == PowerModeState.Performance && _isExtremeActive)
        {
            return PowerModeState.Extreme;
        }

        return baseState;
    }

    /// <summary>
    /// Returns true if Extreme mode is currently active.
    /// </summary>
    public bool IsExtremeModeActive => _isExtremeActive;

    public override async Task SetStateAsync(PowerModeState state)
    {
        var allStates = await GetAllStatesAsync().ConfigureAwait(false);
        if (!allStates.Contains(state))
            throw new InvalidOperationException($"Unsupported power mode {state}");

        if (state is PowerModeState.Performance or PowerModeState.Extreme or PowerModeState.GodMode
            && !AllowAllPowerModesOnBattery
            && await Power.IsPowerAdapterConnectedAsync().ConfigureAwait(false) is PowerAdapterStatus.Disconnected)
            throw new PowerModeUnavailableWithoutACException(state);

        var currentState = await GetEffectiveStateAsync().ConfigureAwait(false);

        var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);

        // Determine the actual WMI power mode to set
        var wmiState = state == PowerModeState.Extreme ? PowerModeState.Performance : state;

        if (mi.Properties.HasQuietToPerformanceModeSwitchingBug && currentState == PowerModeState.Quiet && wmiState == PowerModeState.Performance)
        {
            thermalModeListener.SuppressNext();
            await base.SetStateAsync(PowerModeState.Balance).ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
        }

        if (mi.Properties.HasGodModeToOtherModeSwitchingBug && currentState == PowerModeState.GodMode && wmiState != PowerModeState.GodMode)
        {
            thermalModeListener.SuppressNext();

            switch (wmiState)
            {
                case PowerModeState.Quiet:
                    await base.SetStateAsync(PowerModeState.Performance).ConfigureAwait(false);
                    break;
                case PowerModeState.Balance:
                    await base.SetStateAsync(PowerModeState.Quiet).ConfigureAwait(false);
                    break;
                case PowerModeState.Performance:
                    await base.SetStateAsync(PowerModeState.Balance).ConfigureAwait(false);
                    break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
        }

        thermalModeListener.SuppressNext();
        await base.SetStateAsync(wmiState).ConfigureAwait(false);

        // Handle Extreme mode OC settings
        await HandleExtremeModeAsync(state).ConfigureAwait(false);

        await powerModeListener.NotifyAsync(state).ConfigureAwait(false);
    }

    private async Task HandleExtremeModeAsync(PowerModeState state)
    {
        if (state == PowerModeState.Extreme)
        {
            // Check if this is an AMD processor - AMD models only get GPU OC, not CPU OC
            // AMD HS processors don't support CPU OC, only HX does (and that's inconsistent)
            var isAmd = await IsAmdProcessorAsync().ConfigureAwait(false);

            if (!isAmd)
            {
                // Enable CPU OC via WMI (CPU +100MHz boost) - Intel only
                try
                {
                    await WMI.LenovoGameZoneData.SetCpuOCStatusAsync(1).ConfigureAwait(false);

                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Extreme mode: CPU OC enabled (Intel)");
                }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Failed to enable CPU OC for Extreme mode", ex);
                }
            }
            else
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Extreme mode: Skipping CPU OC for AMD processor");
            }

            // Apply GPU/Memory OC (GPU +50MHz, Memory +100MHz) - Works for both Intel and AMD
            try
            {
                var extremeOcInfo = new GPUOverclockInfo(50, 100);
                GpuOverclockController.SaveState(true, extremeOcInfo);
                await GpuOverclockController.ApplyStateAsync().ConfigureAwait(false);

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Extreme mode: GPU OC applied - Core +50MHz, Memory +100MHz");
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Failed to apply GPU OC for Extreme mode", ex);
            }

            _isExtremeActive = true;
        }
        else
        {
            // Disable Extreme mode OC settings if it was previously active
            if (_isExtremeActive)
            {
                // Check if this is an AMD processor
                var isAmd = await IsAmdProcessorAsync().ConfigureAwait(false);

                if (!isAmd)
                {
                    try
                    {
                        // Disable CPU OC - Intel only
                        await WMI.LenovoGameZoneData.SetCpuOCStatusAsync(0).ConfigureAwait(false);

                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"Extreme mode: CPU OC disabled (Intel)");
                    }
                    catch (Exception ex)
                    {
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"Failed to disable CPU OC", ex);
                    }
                }

                // Reset GPU OC to zero
                try
                {
                    var zeroOcInfo = GPUOverclockInfo.Zero;
                    GpuOverclockController.SaveState(false, zeroOcInfo);
                    await GpuOverclockController.ApplyStateAsync(true).ConfigureAwait(false);

                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Extreme mode: GPU OC reset");
                }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Failed to reset GPU OC", ex);
                }

                _isExtremeActive = false;
            }
        }
    }

    private static async Task<bool> IsAmdProcessorAsync()
    {
        try
        {
            var manufacturer = await WMI.Win32.Processor.GetManufacturerAsync().ConfigureAwait(false);
            return manufacturer.Contains("AMD", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public async Task EnsureCorrectWindowsPowerSettingsAreSetAsync()
    {
        var state = await GetEffectiveStateAsync().ConfigureAwait(false);
        await windowsPowerModeController.SetPowerModeAsync(state).ConfigureAwait(false);
        await windowsPowerPlanController.SetPowerPlanAsync(state, true).ConfigureAwait(false);
    }

    public async Task EnsureGodModeStateIsAppliedAsync()
    {
        var state = await GetEffectiveStateAsync().ConfigureAwait(false);
        if (state != PowerModeState.GodMode)
            return;

        await godModeController.ApplyStateAsync().ConfigureAwait(false);
    }
}
