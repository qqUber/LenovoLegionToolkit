using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Features.WhiteKeyboardBacklight;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Services;

/// <summary>
/// Service that monitors user activity and turns off keyboard backlight after a period of inactivity.
/// When the user becomes active again, the backlight is restored.
/// </summary>
public class KeyboardBacklightTimeoutService : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    private readonly ApplicationSettings _settings;
    private readonly RGBKeyboardBacklightController _rgbController;
    private readonly SpectrumKeyboardBacklightController _spectrumController;
    private readonly WhiteKeyboardBacklightFeature _whiteBacklightFeature;

    private Timer? _checkTimer;
    private bool _isBacklightOff;
    private RGBKeyboardBacklightPreset? _savedRgbPreset;
    private WhiteKeyboardBacklightState? _savedWhiteState;
    private bool _disposed;

    public KeyboardBacklightTimeoutService(
        ApplicationSettings settings,
        RGBKeyboardBacklightController rgbController,
        SpectrumKeyboardBacklightController spectrumController,
        WhiteKeyboardBacklightFeature whiteBacklightFeature)
    {
        _settings = settings;
        _rgbController = rgbController;
        _spectrumController = spectrumController;
        _whiteBacklightFeature = whiteBacklightFeature;
    }

    public void Start()
    {
        Stop();

        if (!_settings.Store.KeyboardBacklightTimeoutEnabled)
            return;

        var checkIntervalMs = Math.Max(1000, _settings.Store.KeyboardBacklightTimeoutSeconds * 100);
        _checkTimer = new Timer(CheckActivity, null, checkIntervalMs, checkIntervalMs);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Keyboard backlight timeout service started. Timeout: {_settings.Store.KeyboardBacklightTimeoutSeconds}s");
    }

    public void Stop()
    {
        _checkTimer?.Dispose();
        _checkTimer = null;
        _isBacklightOff = false;
        _savedRgbPreset = null;
        _savedWhiteState = null;
    }

    public void Restart()
    {
        Stop();
        Start();
    }

    private async void CheckActivity(object? state)
    {
        try
        {
            if (!_settings.Store.KeyboardBacklightTimeoutEnabled)
            {
                Stop();
                return;
            }

            var idleTimeMs = GetIdleTimeMs();
            var timeoutMs = _settings.Store.KeyboardBacklightTimeoutSeconds * 1000;

            if (idleTimeMs >= timeoutMs && !_isBacklightOff)
            {
                // User has been idle for longer than timeout - turn off backlight
                await TurnOffBacklightAsync().ConfigureAwait(false);
            }
            else if (idleTimeMs < 1000 && _isBacklightOff)
            {
                // User became active again - restore backlight
                await RestoreBacklightAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Error in keyboard backlight timeout check", ex);
        }
    }

    private static uint GetIdleTimeMs()
    {
        var lastInputInfo = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        
        if (!GetLastInputInfo(ref lastInputInfo))
            return 0;

        return (uint)Environment.TickCount - lastInputInfo.dwTime;
    }

    private async Task TurnOffBacklightAsync()
    {
        try
        {
            // Try RGB keyboard first
            if (await _rgbController.IsSupportedAsync().ConfigureAwait(false))
            {
                var currentState = await _rgbController.GetStateAsync().ConfigureAwait(false);
                if (currentState.SelectedPreset != RGBKeyboardBacklightPreset.Off)
                {
                    _savedRgbPreset = currentState.SelectedPreset;
                    await _rgbController.SetPresetAsync(RGBKeyboardBacklightPreset.Off).ConfigureAwait(false);
                    
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"RGB keyboard backlight turned off due to inactivity");
                }
            }

            // Try white keyboard backlight
            if (await _whiteBacklightFeature.IsSupportedAsync().ConfigureAwait(false))
            {
                var currentState = await _whiteBacklightFeature.GetStateAsync().ConfigureAwait(false);
                if (currentState != WhiteKeyboardBacklightState.Off)
                {
                    _savedWhiteState = currentState;
                    await _whiteBacklightFeature.SetStateAsync(WhiteKeyboardBacklightState.Off).ConfigureAwait(false);
                    
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"White keyboard backlight turned off due to inactivity");
                }
            }

            _isBacklightOff = true;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Error turning off keyboard backlight", ex);
        }
    }

    private async Task RestoreBacklightAsync()
    {
        try
        {
            // Restore RGB keyboard
            if (_savedRgbPreset.HasValue && await _rgbController.IsSupportedAsync().ConfigureAwait(false))
            {
                await _rgbController.SetPresetAsync(_savedRgbPreset.Value).ConfigureAwait(false);
                
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"RGB keyboard backlight restored to {_savedRgbPreset.Value}");
            }

            // Restore white keyboard backlight
            if (_savedWhiteState.HasValue && await _whiteBacklightFeature.IsSupportedAsync().ConfigureAwait(false))
            {
                await _whiteBacklightFeature.SetStateAsync(_savedWhiteState.Value).ConfigureAwait(false);
                
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"White keyboard backlight restored to {_savedWhiteState.Value}");
            }

            _isBacklightOff = false;
            _savedRgbPreset = null;
            _savedWhiteState = null;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Error restoring keyboard backlight", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _disposed = true;
    }
}
