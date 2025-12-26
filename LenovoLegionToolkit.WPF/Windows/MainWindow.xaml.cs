using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.SoftwareDisabler;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Extensions;
using LenovoLegionToolkit.WPF.Pages;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Utils;
using LenovoLegionToolkit.WPF.Windows.Utils;
using Microsoft.Xaml.Behaviors.Core;
using Windows.Win32;
using Windows.Win32.System.Threading;
using Wpf.Ui.Controls;
#if !DEBUG
using System.Reflection;
using LenovoLegionToolkit.Lib.Extensions;
#endif

#pragma warning disable CA1416

namespace LenovoLegionToolkit.WPF.Windows;

public partial class MainWindow
{
    private readonly ApplicationSettings _applicationSettings = IoCContainer.Resolve<ApplicationSettings>();
    private readonly SpecialKeyListener _specialKeyListener = IoCContainer.Resolve<SpecialKeyListener>();
    private readonly VantageDisabler _vantageDisabler = IoCContainer.Resolve<VantageDisabler>();
    private readonly LegionZoneDisabler _legionZoneDisabler = IoCContainer.Resolve<LegionZoneDisabler>();
    private readonly FnKeysDisabler _fnKeysDisabler = IoCContainer.Resolve<FnKeysDisabler>();
    private readonly UpdateChecker _updateChecker = IoCContainer.Resolve<UpdateChecker>();

    private TrayHelper? _trayHelper;

    public bool TrayTooltipEnabled { get; init; } = true;
    public bool DisableConflictingSoftwareWarning { get; set; }
    public bool SuppressClosingEventHandler { get; set; }

    public Snackbar Snackbar => Snackbar;

    public MainWindow()
    {
        InitializeComponent();

        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
        IsVisibleChanged += MainWindow_IsVisibleChanged;
        Loaded += MainWindow_Loaded;
        SourceInitialized += MainWindow_SourceInitialized;
        StateChanged += MainWindow_StateChanged;

#if DEBUG
        Title += Debugger.IsAttached ? " [DEBUGGER ATTACHED]" : " [DEBUG]";
#else
        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        if (version is not null && version.IsBeta())
            Title += " [BETA]";
#endif

        if (Log.Instance.IsTraceEnabled)
        {
            Title += " [LOGGING ENABLED]";
            // _openLogIndicator.Visibility = Visibility.Visible;
        }
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e) => RestoreSize();

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _contentGrid.Visibility = Visibility.Visible;

        SmartKeyHelper.Instance.BringToForeground = () => Dispatcher.Invoke(BringToForeground);

        _specialKeyListener.Changed += (_, args) =>
        {
            if (args.SpecialKey == SpecialKey.FnN)
                Dispatcher.Invoke(BringToForeground);
        };

            // Defer heavier startup work to let the UI render quickly
            _ = InitializePostLoadAsync();

        InputBindings.Add(new KeyBinding(new ActionCommand(_navigationStore.NavigateToNext), Key.Tab, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new ActionCommand(_navigationStore.NavigateToPrevious), Key.Tab, ModifierKeys.Control | ModifierKeys.Shift));

        var key = (int)Key.D1;
        foreach (var item in _navigationStore.Items.OfType<NavigationItem>())
        {
            if (item.PageTag is not null)
                InputBindings.Add(new KeyBinding(new ActionCommand(() => _navigationStore.Navigate(item.PageTag)), (Key)key++, ModifierKeys.Control));
        }

        var trayHelper = new TrayHelper(_navigationStore, BringToForeground, TrayTooltipEnabled);
        await trayHelper.InitializeAsync();
        trayHelper.MakeVisible();
        _trayHelper = trayHelper;
    }

    private async Task InitializePostLoadAsync()
    {
        // Yield to allow first render before heavier work
        await Task.Yield();

        try
        {
            // Run in parallel where possible
            var backgroundTask = ApplyCustomBackgroundDeferredAsync();
            var keyboardSupportTask = EnsureKeyboardSupportAsync();

            LoadDeviceInfo();
            UpdateIndicators();
            CheckForUpdates();

            await Task.WhenAll(backgroundTask, keyboardSupportTask).ConfigureAwait(true);
        }
        catch
        {
            // Swallow to avoid impacting startup; optional logging can be added here.
        }
    }

    private Task ApplyCustomBackgroundDeferredAsync()
    {
        return Dispatcher.InvokeAsync(ApplyCustomBackground, DispatcherPriority.Background).Task;
    }

    private async Task EnsureKeyboardSupportAsync()
    {
        var isSupported = await KeyboardBacklightPage.IsSupportedAsync().ConfigureAwait(false);
        if (isSupported)
            return;

        await Dispatcher.InvokeAsync(() => _navigationStore.Items.Remove(_keyboardItem));
    }

    private async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        SaveSize();

        if (SuppressClosingEventHandler)
            return;

        if (_applicationSettings.Store.MinimizeOnClose)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Hiding to tray...");

            e.Cancel = true;
            SendToTray();
        }
        else
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Closing...");

            await App.Current.ShutdownAsync();
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs args)
    {
        _trayHelper?.Dispose();
        _trayHelper = null;
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Window state changed to {WindowState}");

        switch (WindowState)
        {
            case WindowState.Minimized:
                SetEfficiencyMode(true);
                // Pause sensor updates when minimized to save resources
                HardwareMonitorService.Instance.SetUpdateMode(HardwareMonitorService.UpdateMode.Paused);
                SendToTray();
                break;
            case WindowState.Normal:
            case WindowState.Maximized:
                SetEfficiencyMode(false);
                // Resume normal sensor updates
                HardwareMonitorService.Instance.SetUpdateMode(HardwareMonitorService.UpdateMode.Normal);
                if (WindowState == WindowState.Normal)
                    BringToForeground();
                break;
        }
    }

    private void UpdateMaximizeIcon()
    {
        _maximizeIcon.Symbol = WindowState == WindowState.Maximized
            ? SymbolRegular.SquareMultiple24
            : SymbolRegular.Maximize24;
        _maximizeButton.ToolTip = WindowState == WindowState.Maximized ? "Restore" : "Maximize";
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        UpdateMaximizeIcon();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            MaximizeButton_Click(sender, e);
        }
        else if (e.LeftButton == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch { /* Ignore */ }
        }
    }

    private void MainWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!IsVisible)
            return;

        CheckForUpdates();
    }

    private void OpenLogIndicator_Click(object sender, MouseButtonEventArgs e) => OpenLog();

    private void OpenLogIndicator_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not Key.Enter and not Key.Space)
            return;

        OpenLog();
    }

    private void DeviceInfoIndicator_Click(object sender, MouseButtonEventArgs e) => ShowDeviceInfoWindow();

    private void DeviceInfoIndicator_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not Key.Enter and not Key.Space)
            return;

        ShowDeviceInfoWindow();
    }

    private void UpdateIndicator_Click(object sender, RoutedEventArgs e) => ShowUpdateWindow();

    private void UpdateIndicator_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not Key.Enter and not Key.Space)
            return;

        ShowUpdateWindow();
    }

    private void LoadDeviceInfo()
    {
        Task.Run(async () =>
        {
            try
            {
                var machineInfo = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
                await Dispatcher.InvokeAsync(() =>
                {
                    _deviceInfoIndicator.Text = machineInfo.Model;
                    _deviceInfoBorder.Visibility = Visibility.Visible;
                });
            }
            catch
            {
                // Swallow to avoid crashing UI; optionally add logging here.
            }
        });
    }

    private void UpdateIndicators()
    {
        if (DisableConflictingSoftwareWarning)
            return;

        _vantageDisabler.OnRefreshed += (_, e) => Dispatcher.Invoke(() =>
        {
            _vantageIndicator.Visibility = e.Status == SoftwareStatus.Enabled ? Visibility.Visible : Visibility.Collapsed;
        });

        _legionZoneDisabler.OnRefreshed += (_, e) => Dispatcher.Invoke(() =>
        {
            _legionZoneIndicator.Visibility = e.Status == SoftwareStatus.Enabled ? Visibility.Visible : Visibility.Collapsed;
        });

        _fnKeysDisabler.OnRefreshed += (_, e) => Dispatcher.Invoke(() =>
        {
            _fnKeysIndicator.Visibility = e.Status == SoftwareStatus.Enabled ? Visibility.Visible : Visibility.Collapsed;
        });

        Task.Run(async () =>
        {
            _ = await _vantageDisabler.GetStatusAsync().ConfigureAwait(false);
            _ = await _legionZoneDisabler.GetStatusAsync().ConfigureAwait(false);
            _ = await _fnKeysDisabler.GetStatusAsync().ConfigureAwait(false);
        });
    }

    public void CheckForUpdates(bool manualCheck = false) => _ = CheckForUpdatesAsync(manualCheck);

    private async Task CheckForUpdatesAsync(bool manualCheck)
    {
        try
        {
            var result = await _updateChecker.CheckAsync(manualCheck).ConfigureAwait(true);

            if (result is null)
            {
                _updateIndicator.Visibility = Visibility.Collapsed;

                if (manualCheck && WindowState != WindowState.Minimized)
                {
                    switch (_updateChecker.Status)
                    {
                        case UpdateCheckStatus.Success:
                            await SnackbarHelper.ShowAsync(Resource.MainWindow_CheckForUpdates_Success_Title);
                            break;
                        case UpdateCheckStatus.RateLimitReached:
                            await SnackbarHelper.ShowAsync(Resource.MainWindow_CheckForUpdates_Error_Title, Resource.MainWindow_CheckForUpdates_Error_ReachedRateLimit_Message, SnackbarType.Error);
                            break;
                        case UpdateCheckStatus.Error:
                            await SnackbarHelper.ShowAsync(Resource.MainWindow_CheckForUpdates_Error_Title, Resource.MainWindow_CheckForUpdates_Error_Unknown_Message, SnackbarType.Error);
                            break;
                    }
                }
            }
            else
            {
                var versionNumber = result.ToString(3);

                _updateIndicatorText.Text =
                    string.Format(Resource.MainWindow_UpdateAvailableWithVersion, versionNumber);
                _updateIndicator.Visibility = Visibility.Visible;

                if (WindowState == WindowState.Minimized)
                    MessagingCenter.Publish(new NotificationMessage(NotificationType.UpdateAvailable, versionNumber));
            }
        }
        catch
        {
            // Swallow errors to avoid UI disruption; optionally add logging here.
        }
    }

    private void RestoreSize()
    {
        if (!_applicationSettings.Store.WindowSize.HasValue)
            return;

        Width = Math.Max(MinWidth, _applicationSettings.Store.WindowSize.Value.Width);
        Height = Math.Max(MinHeight, _applicationSettings.Store.WindowSize.Value.Height);

        ScreenHelper.UpdateScreenInfos();
        var primaryScreen = ScreenHelper.PrimaryScreen;

        if (!primaryScreen.HasValue)
            return;

        var desktopWorkingArea = primaryScreen.Value.WorkArea;
        Left = (desktopWorkingArea.Width - Width) / 2 + desktopWorkingArea.Left;
        Top = (desktopWorkingArea.Height - Height) / 2 + desktopWorkingArea.Top;
    }

    private void SaveSize()
    {
        _applicationSettings.Store.WindowSize = WindowState != WindowState.Normal
            ? new(RestoreBounds.Width, RestoreBounds.Height)
            : new(Width, Height);
        _applicationSettings.SynchronizeStore();
    }

    private void BringToForeground() => WindowExtensions.BringToForeground(this);

    private static void OpenLog()
    {
        try
        {
            if (!Directory.Exists(Folders.AppData))
                return;

            Process.Start("explorer", Log.Instance.LogPath);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to open log.", ex);
        }
    }

    private void ShowDeviceInfoWindow()
    {
        var window = new DeviceInformationWindow { Owner = this };
        window.ShowDialog();
    }

    public void ShowUpdateWindow()
    {
        var window = new UpdateWindow { Owner = this };
        window.ShowDialog();
    }

    public void SendToTray()
    {
        if (!_applicationSettings.Store.MinimizeToTray)
        {
            // Even if MinimizeToTray is disabled, still hide the window to keep it cached
            Hide();
            ShowInTaskbar = false;
            return;
        }

        SetEfficiencyMode(true);
        Hide();
        ShowInTaskbar = false;
    }

    private static unsafe void SetEfficiencyMode(bool enabled)
    {
        var ptr = IntPtr.Zero;

        try
        {
            var priorityClass = enabled
                ? PROCESS_CREATION_FLAGS.IDLE_PRIORITY_CLASS
                : PROCESS_CREATION_FLAGS.NORMAL_PRIORITY_CLASS;
            PInvoke.SetPriorityClass(PInvoke.GetCurrentProcess(), priorityClass);

            var state = new PROCESS_POWER_THROTTLING_STATE
            {
                Version = PInvoke.PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                ControlMask = PInvoke.PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                StateMask = enabled ? PInvoke.PROCESS_POWER_THROTTLING_EXECUTION_SPEED : 0,
            };

            var size = Marshal.SizeOf<PROCESS_POWER_THROTTLING_STATE>();
            ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(state, ptr, false);

            PInvoke.SetProcessInformation(PInvoke.GetCurrentProcess(),
                PROCESS_INFORMATION_CLASS.ProcessPowerThrottling,
                ptr.ToPointer(),
                (uint)size);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private System.Windows.Threading.DispatcherTimer? _slideshowTimer;
    private int _currentSlideshowIndex;
    private System.Collections.Generic.List<string>? _shuffledImages;

    public void ApplyCustomBackground()
    {
        try
        {
            var enabled = _applicationSettings.Store.CustomBackgroundEnabled;
            var bgType = _applicationSettings.Store.CustomBackgroundType;

            // Stop slideshow timer if running
            _slideshowTimer?.Stop();

            if (!enabled)
            {
                _backgroundImage.Visibility = Visibility.Collapsed;
                _backgroundTint.Visibility = Visibility.Collapsed;
                _backgroundGradient.Visibility = Visibility.Collapsed;
                return;
            }

            switch (bgType)
            {
                case CustomBackgroundType.Image:
                    ApplyImageBackground();
                    break;
                case CustomBackgroundType.Video:
                    ApplyVideoBackground();
                    break;
                case CustomBackgroundType.Slideshow:
                    ApplySlideshowBackground();
                    break;
                case CustomBackgroundType.Preset:
                    ApplyPresetBackground();
                    break;
            }

            ApplyBackgroundEffects();
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to apply custom background.", ex);

            _backgroundImage.Visibility = Visibility.Collapsed;
            _backgroundTint.Visibility = Visibility.Collapsed;
            _backgroundGradient.Visibility = Visibility.Collapsed;
        }
    }

    private void ApplyImageBackground()
    {
        var path = _applicationSettings.Store.CustomBackgroundPath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            _backgroundImage.Visibility = Visibility.Collapsed;
            return;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = 1920; // Limit decoded size to reduce memory
        bitmap.EndInit();
        bitmap.Freeze();

        _backgroundImage.Source = bitmap;
        _backgroundImage.Visibility = Visibility.Visible;
        _backgroundGradient.Visibility = Visibility.Collapsed;
    }

    private void ApplyVideoBackground()
    {
        // Video backgrounds use the same path mechanism as images
        // The MediaElement would need to be added to MainWindow.xaml
        // For now, fall back to image behavior
        ApplyImageBackground();
    }

    private void ApplySlideshowBackground()
    {
        var images = _applicationSettings.Store.SlideshowImages;
        if (images.Count == 0)
        {
            _backgroundImage.Visibility = Visibility.Collapsed;
            return;
        }

        // Initialize shuffled list if shuffle is enabled
        if (_applicationSettings.Store.SlideshowShuffle)
        {
            if (_shuffledImages == null || _shuffledImages.Count != images.Count)
            {
                _shuffledImages = images.OrderBy(_ => Guid.NewGuid()).ToList();
            }
        }
        else
        {
            _shuffledImages = images.ToList();
        }

        // Show first/current image
        ShowSlideshowImage(_currentSlideshowIndex % _shuffledImages.Count);

        // Start timer
        _slideshowTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_applicationSettings.Store.SlideshowIntervalSeconds)
        };
        _slideshowTimer.Tick += (_, _) =>
        {
            _currentSlideshowIndex++;
            if (_shuffledImages != null && _shuffledImages.Count > 0)
                ShowSlideshowImage(_currentSlideshowIndex % _shuffledImages.Count);
        };
        _slideshowTimer.Start();
    }

    private void ShowSlideshowImage(int index)
    {
        if (_shuffledImages == null || index >= _shuffledImages.Count)
            return;

        var path = _shuffledImages[index];
        if (!File.Exists(path))
            return;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = 1920; // Limit decoded size to reduce memory
        bitmap.EndInit();
        bitmap.Freeze();

        _backgroundImage.Source = bitmap;
        _backgroundImage.Visibility = Visibility.Visible;
        _backgroundGradient.Visibility = Visibility.Collapsed;
    }

    private void ApplyPresetBackground()
    {
        var preset = _applicationSettings.Store.SelectedPreset;
        _backgroundImage.Visibility = Visibility.Collapsed;

        if (string.IsNullOrEmpty(preset) || preset == "None")
        {
            _backgroundGradient.Visibility = Visibility.Collapsed;
            return;
        }

        var gradient = preset switch
        {
            "Gradient1" => CreateGradientBrush("#8B5CF6", "#3B82F6"),  // Purple Haze
            "Gradient2" => CreateGradientBrush("#0EA5E9", "#22D3EE"),  // Ocean Blue
            "Gradient3" => CreateGradientBrush("#F97316", "#EF4444"),  // Sunset
            "Gradient4" => CreateGradientBrush("#22C55E", "#10B981"),  // Forest
            "Gradient5" => CreateGradientBrush("#1E293B", "#334155"),  // Midnight
            _ => null
        };

        if (gradient != null)
        {
            _backgroundGradient.Fill = gradient;
            _backgroundGradient.Opacity = _applicationSettings.Store.CustomBackgroundOpacity * 3; // Presets need more visibility
            _backgroundGradient.Visibility = Visibility.Visible;
        }
        else
        {
            _backgroundGradient.Visibility = Visibility.Collapsed;
        }
    }

    private static LinearGradientBrush CreateGradientBrush(string color1, string color2)
    {
        return new LinearGradientBrush(
            (Color)ColorConverter.ConvertFromString(color1),
            (Color)ColorConverter.ConvertFromString(color2),
            45);
    }

    private void ApplyBackgroundEffects()
    {
        _backgroundImage.Opacity = _applicationSettings.Store.CustomBackgroundOpacity;

        if (_applicationSettings.Store.CustomBackgroundBlur && _backgroundImage.Visibility == Visibility.Visible)
        {
            _backgroundImage.Effect = new BlurEffect { Radius = _applicationSettings.Store.CustomBackgroundBlurRadius };
        }
        else
        {
            _backgroundImage.Effect = null;
        }

        if (_applicationSettings.Store.CustomBackgroundTint.HasValue)
        {
            var tint = _applicationSettings.Store.CustomBackgroundTint.Value;
            _backgroundTint.Fill = new SolidColorBrush(Color.FromArgb(80, tint.R, tint.G, tint.B));
            _backgroundTint.Visibility = Visibility.Visible;
        }
        else
        {
            _backgroundTint.Visibility = Visibility.Collapsed;
        }
    }
}
