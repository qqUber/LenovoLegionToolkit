using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Extensions;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Windows;
using LenovoLegionToolkit.WPF.Windows.Utils;

using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Utils;

public class NotificationsManager
{
    private static Dispatcher Dispatcher => Application.Current.Dispatcher;

    private readonly ApplicationSettings _settings;
    private readonly List<INotificationWindow?> _windows = [];

    // Notification type categories for settings lookup
    private static readonly HashSet<NotificationType> AcAdapterTypes = [
        NotificationType.ACAdapterConnected,
        NotificationType.ACAdapterConnectedLowWattage,
        NotificationType.ACAdapterDisconnected
    ];

    private static readonly HashSet<NotificationType> CapsNumLockTypes = [
        NotificationType.CapsLockOn,
        NotificationType.CapsLockOff,
        NotificationType.NumLockOn,
        NotificationType.NumLockOff
    ];

    private static readonly HashSet<NotificationType> PowerModeTypes = [
        NotificationType.PowerModeQuiet,
        NotificationType.PowerModeBalance,
        NotificationType.PowerModePerformance,
        NotificationType.PowerModeExtreme,
        NotificationType.PowerModeGodMode
    ];

    private static readonly HashSet<NotificationType> KeyboardBacklightTypes = [
        NotificationType.PanelLogoLightingOn,
        NotificationType.PanelLogoLightingOff,
        NotificationType.PortLightingOn,
        NotificationType.PortLightingOff,
        NotificationType.RGBKeyboardBacklightOff,
        NotificationType.RGBKeyboardBacklightChanged,
        NotificationType.SpectrumBacklightChanged,
        NotificationType.SpectrumBacklightOff,
        NotificationType.SpectrumBacklightPresetChanged,
        NotificationType.WhiteKeyboardBacklightOff,
        NotificationType.WhiteKeyboardBacklightChanged
    ];

    private static readonly HashSet<NotificationType> OffTypes = [
        NotificationType.ACAdapterDisconnected,
        NotificationType.CapsLockOff,
        NotificationType.CameraOff,
        NotificationType.FnLockOff,
        NotificationType.MicrophoneOff,
        NotificationType.NumLockOff,
        NotificationType.PanelLogoLightingOff,
        NotificationType.PortLightingOff,
        NotificationType.RGBKeyboardBacklightOff,
        NotificationType.SpectrumBacklightOff,
        NotificationType.TouchpadOff,
        NotificationType.WhiteKeyboardBacklightOff
    ];

    // Types that use Args as display text
    private static readonly HashSet<NotificationType> ArgsDisplayTypes = [
        NotificationType.AutomationNotification,
        NotificationType.PowerModeQuiet,
        NotificationType.PowerModeBalance,
        NotificationType.PowerModePerformance,
        NotificationType.PowerModeExtreme,
        NotificationType.PowerModeGodMode,
        NotificationType.RefreshRate,
        NotificationType.RGBKeyboardBacklightOff,
        NotificationType.RGBKeyboardBacklightChanged,
        NotificationType.SmartKeyDoublePress,
        NotificationType.SmartKeySinglePress
    ];

    // Symbol mapping
    private static readonly Dictionary<NotificationType, SymbolRegular> SymbolMap = new()
    {
        [NotificationType.ACAdapterConnected] = SymbolRegular.BatteryCharge24,
        [NotificationType.ACAdapterConnectedLowWattage] = SymbolRegular.BatteryCharge24,
        [NotificationType.ACAdapterDisconnected] = SymbolRegular.BatteryCharge24,
        [NotificationType.AutomationNotification] = SymbolRegular.Rocket24,
        [NotificationType.CapsLockOn] = SymbolRegular.KeyboardShiftUppercase24,
        [NotificationType.CapsLockOff] = SymbolRegular.KeyboardShiftUppercase24,
        [NotificationType.CameraOn] = SymbolRegular.Camera24,
        [NotificationType.CameraOff] = SymbolRegular.Camera24,
        [NotificationType.FnLockOn] = SymbolRegular.Keyboard24,
        [NotificationType.FnLockOff] = SymbolRegular.Keyboard24,
        [NotificationType.MicrophoneOn] = SymbolRegular.Mic24,
        [NotificationType.MicrophoneOff] = SymbolRegular.Mic24,
        [NotificationType.NumLockOn] = SymbolRegular.Keyboard12324,
        [NotificationType.NumLockOff] = SymbolRegular.Keyboard12324,
        [NotificationType.PanelLogoLightingOn] = SymbolRegular.LightbulbCircle24,
        [NotificationType.PanelLogoLightingOff] = SymbolRegular.LightbulbCircle24,
        [NotificationType.PortLightingOn] = SymbolRegular.UsbPlug24,
        [NotificationType.PortLightingOff] = SymbolRegular.UsbPlug24,
        [NotificationType.PowerModeQuiet] = SymbolRegular.Gauge24,
        [NotificationType.PowerModeBalance] = SymbolRegular.Gauge24,
        [NotificationType.PowerModePerformance] = SymbolRegular.Gauge24,
        [NotificationType.PowerModeExtreme] = SymbolRegular.Gauge24,
        [NotificationType.PowerModeGodMode] = SymbolRegular.Gauge24,
        [NotificationType.RefreshRate] = SymbolRegular.DesktopPulse24,
        [NotificationType.RGBKeyboardBacklightOff] = SymbolRegular.Lightbulb24,
        [NotificationType.RGBKeyboardBacklightChanged] = SymbolRegular.Lightbulb24,
        [NotificationType.SmartKeyDoublePress] = SymbolRegular.StarEmphasis24,
        [NotificationType.SmartKeySinglePress] = SymbolRegular.Star24,
        [NotificationType.SpectrumBacklightChanged] = SymbolRegular.Lightbulb24,
        [NotificationType.SpectrumBacklightOff] = SymbolRegular.Lightbulb24,
        [NotificationType.SpectrumBacklightPresetChanged] = SymbolRegular.Lightbulb24,
        [NotificationType.TouchpadOn] = SymbolRegular.Tablet24,
        [NotificationType.TouchpadOff] = SymbolRegular.Tablet24,
        [NotificationType.UpdateAvailable] = SymbolRegular.ArrowRepeatAll24,
        [NotificationType.WhiteKeyboardBacklightOff] = SymbolRegular.Lightbulb24,
        [NotificationType.WhiteKeyboardBacklightChanged] = SymbolRegular.Lightbulb24
    };

    // Resource text mapping for static notifications
    private static readonly Dictionary<NotificationType, Func<string>> TextMap = new()
    {
        [NotificationType.ACAdapterConnected] = () => Resource.Notification_ACAdapterConnected,
        [NotificationType.ACAdapterConnectedLowWattage] = () => Resource.Notification_ACAdapterConnectedLowWattage,
        [NotificationType.ACAdapterDisconnected] = () => Resource.Notification_ACAdapterDisconnected,
        [NotificationType.CapsLockOn] = () => Resource.Notification_CapsLockOn,
        [NotificationType.CapsLockOff] = () => Resource.Notification_CapsLockOff,
        [NotificationType.CameraOn] = () => Resource.Notification_CameraOn,
        [NotificationType.CameraOff] = () => Resource.Notification_CameraOff,
        [NotificationType.FnLockOn] = () => Resource.Notification_FnLockOn,
        [NotificationType.FnLockOff] = () => Resource.Notification_FnLockOff,
        [NotificationType.MicrophoneOn] = () => Resource.Notification_MicrophoneOn,
        [NotificationType.MicrophoneOff] = () => Resource.Notification_MicrophoneOff,
        [NotificationType.NumLockOn] = () => Resource.Notification_NumLockOn,
        [NotificationType.NumLockOff] = () => Resource.Notification_NumLockOff,
        [NotificationType.PanelLogoLightingOn] = () => Resource.Notification_PanelLogoLightingOn,
        [NotificationType.PanelLogoLightingOff] = () => Resource.Notification_PanelLogoLightingOff,
        [NotificationType.PortLightingOn] = () => Resource.Notification_PortLightingOn,
        [NotificationType.PortLightingOff] = () => Resource.Notification_PortLightingOff,
        [NotificationType.TouchpadOn] = () => Resource.Notification_TouchpadOn,
        [NotificationType.TouchpadOff] = () => Resource.Notification_TouchpadOff
    };

    public NotificationsManager(ApplicationSettings settings)
    {
        _settings = settings;
        MessagingCenter.Subscribe<NotificationMessage>(this, OnNotificationReceived);
    }

    private void OnNotificationReceived(NotificationMessage notification)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Notification {notification} received");

            if (_settings.Store.DontShowNotifications)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Notifications are disabled.");
                return;
            }

            if (FullscreenHelper.IsAnyApplicationFullscreen() && !_settings.Store.NotificationAlwaysOnTop)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Some application is in fullscreen.");
                return;
            }

            if (!IsNotificationAllowed(notification.Type))
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Notification type {notification.Type} is disabled.");
                return;
            }

            var symbol = GetSymbol(notification.Type);
            var overlaySymbol = GetOverlaySymbol(notification.Type);
            var text = GetNotificationText(notification);
            var symbolTransform = GetSymbolTransform(notification.Type, overlaySymbol);
            var clickAction = GetClickAction(notification.Type);

            ShowNotification(symbol, overlaySymbol, symbolTransform, text, clickAction);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Notification {notification} shown.");
        });
    }

    private bool IsNotificationAllowed(NotificationType type)
    {
        var notifications = _settings.Store.Notifications;

        if (AcAdapterTypes.Contains(type)) return notifications.ACAdapter;
        if (CapsNumLockTypes.Contains(type)) return notifications.CapsNumLock;
        if (PowerModeTypes.Contains(type)) return notifications.PowerMode;
        if (KeyboardBacklightTypes.Contains(type)) return notifications.KeyboardBacklight;

        return type switch
        {
            NotificationType.AutomationNotification => notifications.AutomationNotification,
            NotificationType.CameraOn or NotificationType.CameraOff => notifications.CameraLock,
            NotificationType.FnLockOn or NotificationType.FnLockOff => notifications.FnLock,
            NotificationType.MicrophoneOn or NotificationType.MicrophoneOff => notifications.Microphone,
            NotificationType.RefreshRate => notifications.RefreshRate,
            NotificationType.SmartKeyDoublePress or NotificationType.SmartKeySinglePress => notifications.SmartKey,
            NotificationType.TouchpadOn or NotificationType.TouchpadOff => notifications.TouchpadLock,
            NotificationType.UpdateAvailable => notifications.UpdateAvailable,
            _ => true
        };
    }

    private static SymbolRegular GetSymbol(NotificationType type) =>
        SymbolMap.TryGetValue(type, out var symbol) ? symbol : SymbolRegular.Info24;

    private static SymbolRegular? GetOverlaySymbol(NotificationType type) =>
        OffTypes.Contains(type) ? SymbolRegular.Line24 : null;

    private static string GetNotificationText(NotificationMessage notification)
    {
        var type = notification.Type;
        var args = notification.Args;
        var firstArg = args?.FirstOrDefault()?.ToString() ?? string.Empty;

        // Types that display Args directly
        if (ArgsDisplayTypes.Contains(type))
            return firstArg;

        // Types with format strings
        return type switch
        {
            NotificationType.SpectrumBacklightChanged => string.Format(Resource.Notification_SpectrumKeyboardBacklight_Brightness, firstArg),
            NotificationType.SpectrumBacklightOff => string.Format(Resource.Notification_SpectrumKeyboardBacklight_Backlight, firstArg),
            NotificationType.SpectrumBacklightPresetChanged => string.Format(Resource.Notification_SpectrumKeyboardBacklight_Profile, firstArg),
            NotificationType.UpdateAvailable => string.Format(Resource.Notification_UpdateAvailable, firstArg),
            NotificationType.WhiteKeyboardBacklightOff => string.Format(Resource.Notification_WhiteKeyboardBacklight, firstArg),
            NotificationType.WhiteKeyboardBacklightChanged => string.Format(Resource.Notification_WhiteKeyboardBacklight, firstArg),
            _ => TextMap.TryGetValue(type, out var textFunc) ? textFunc() : string.Empty
        };
    }

    private static Action<SymbolIcon>? GetSymbolTransform(NotificationType type, SymbolRegular? overlaySymbol)
    {
        Action<SymbolIcon>? transform = type switch
        {
            NotificationType.PowerModeQuiet => si => si.Foreground = PowerModeState.Quiet.GetSolidColorBrush(),
            NotificationType.PowerModePerformance => si => si.Foreground = PowerModeState.Performance.GetSolidColorBrush(),
            NotificationType.PowerModeExtreme => si => si.Foreground = PowerModeState.Extreme.GetSolidColorBrush(),
            NotificationType.PowerModeGodMode => si => si.Foreground = PowerModeState.GodMode.GetSolidColorBrush(),
            _ => null
        };

        if (transform is null && overlaySymbol is not null)
            transform = si => si.SetResourceReference(Control.ForegroundProperty, "TextFillColorSecondaryBrush");

        return transform;
    }

    private static Action? GetClickAction(NotificationType type) => type switch
    {
        NotificationType.UpdateAvailable => UpdateAvailableAction,
        _ => null
    };

    private int GetNotificationDuration() => _settings.Store.NotificationDuration switch
    {
        NotificationDuration.Short => 500,
        NotificationDuration.Long => 2500,
        NotificationDuration.Normal => 1000,
        _ => 1000
    };

    private void ShowNotification(SymbolRegular symbol, SymbolRegular? overlaySymbol, Action<SymbolIcon>? symbolTransform, string text, Action? clickAction)
    {
        if (App.Current.MainWindow is not MainWindow mainWindow)
            return;

        CloseExistingWindows();

        var duration = GetNotificationDuration();
        var screens = GetTargetScreens();

        foreach (var screen in screens)
        {
            var window = CreateNotificationWindow(mainWindow, symbol, overlaySymbol, symbolTransform, text, clickAction, screen);
            window.Show(duration);
            _windows.Add(window);
        }
    }

    private void CloseExistingWindows()
    {
        if (_windows.Count == 0) return;

        foreach (var window in _windows)
            window?.Close(true);

        _windows.Clear();
    }

    private IEnumerable<ScreenInfo> GetTargetScreens()
    {
        ScreenHelper.UpdateScreenInfos();

        if (_settings.Store.NotificationOnAllScreens)
            return ScreenHelper.Screens;

        var primaryScreen = ScreenHelper.PrimaryScreen;
        return primaryScreen.HasValue ? [primaryScreen.Value] : [];
    }

    private INotificationWindow CreateNotificationWindow(
        MainWindow mainWindow,
        SymbolRegular symbol,
        SymbolRegular? overlaySymbol,
        Action<SymbolIcon>? symbolTransform,
        string text,
        Action? clickAction,
        ScreenInfo screen)
    {
        var nw = new NotificationWindow(symbol, overlaySymbol, symbolTransform, text, clickAction, screen, _settings.Store.NotificationPosition)
        {
            Owner = mainWindow
        };

        if (_settings.Store.NotificationAlwaysOnTop)
        {
            var bitmap = nw.GetBitmapView();
            return new NotificationAoTWindow(bitmap, screen, _settings.Store.NotificationPosition);
        }

        return nw;
    }

    private static void UpdateAvailableAction()
    {
        if (App.Current.MainWindow is not MainWindow mainWindow)
            return;

        mainWindow.BringToForeground();
        mainWindow.ShowUpdateWindow();
    }
}
