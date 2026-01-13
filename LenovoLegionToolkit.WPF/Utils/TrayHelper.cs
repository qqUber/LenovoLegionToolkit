using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation;
using LenovoLegionToolkit.Lib.Automation.Pipeline;
using LenovoLegionToolkit.Lib.Controllers.Sensors;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Assets;
using LenovoLegionToolkit.WPF.Resources;

using Wpf.Ui.Controls;
using Wpf.Ui.Controls.Interfaces;
using MenuItem = LenovoLegionToolkit.WPF.Compat.MenuItem;

namespace LenovoLegionToolkit.WPF.Utils;

public class TrayHelper : IDisposable
{
    private const string NAVIGATION_TAG = "navigation";
    private const string STATIC_TAG = "static";
    private const string AUTOMATION_TAG = "automation";
    private const string POWER_MODE_TAG = "powermode";

    private readonly ThemeManager _themeManager = IoCContainer.Resolve<ThemeManager>();
    private readonly AutomationProcessor _automationProcessor = IoCContainer.Resolve<AutomationProcessor>();
    private readonly PowerModeFeature _powerModeFeature = IoCContainer.Resolve<PowerModeFeature>();
    private readonly PowerModeListener _powerModeListener = IoCContainer.Resolve<PowerModeListener>();
    private readonly SensorsController _sensorsController = IoCContainer.Resolve<SensorsController>();

    private readonly ContextMenu _contextMenu = new()
    {
        FontSize = 14
    };

    private readonly Action _bringToForeground;
    private readonly bool _trayTooltipEnabled;

    private NotifyIcon? _notifyIcon;
    private CancellationTokenSource? _tooltipUpdateCts;
    private PowerModeState _currentPowerMode = PowerModeState.Balance;

    public TrayHelper(INavigation navigation, Action bringToForeground, bool trayTooltipEnabled)
    {
        _bringToForeground = bringToForeground;
        _trayTooltipEnabled = trayTooltipEnabled;

        InitializePowerModeItems();
        InitializeStaticItems(navigation);

        var notifyIcon = new NotifyIcon
        {
            Icon = AssetResources.icon,
            Text = Resource.AppName
        };

        notifyIcon.ContextMenu = _contextMenu;
        notifyIcon.OnClick += (_, _) => _bringToForeground();
        _notifyIcon = notifyIcon;

        _themeManager.ThemeApplied += (_, _) => _contextMenu.Resources = Application.Current.Resources;
        _powerModeListener.Changed += PowerModeListener_Changed;
    }

    public async Task InitializeAsync()
    {
        // Get initial power mode
        try
        {
            _currentPowerMode = await _powerModeFeature.GetStateAsync();
            UpdateTooltip();
        }
        catch { /* Ignore */ }

        var pipelines = await _automationProcessor.GetPipelinesAsync();
        pipelines = pipelines.Where(p => p.Trigger is null).ToList();
        SetAutomationItems(pipelines);

        _automationProcessor.PipelinesChanged += (_, p) => SetAutomationItems(p);

        // Start tooltip update timer for temperature
        if (_trayTooltipEnabled)
        {
            StartTooltipUpdates();
        }
    }

    private void InitializePowerModeItems()
    {
        // Power Mode header
        var headerItem = new MenuItem
        {
            Header = "⚡ Power Mode",
            Tag = POWER_MODE_TAG,
            IsEnabled = false,
            FontWeight = FontWeights.SemiBold
        };
        _contextMenu.Items.Add(headerItem);

        // Power mode options
        var powerModes = new[]
        {
            (PowerModeState.Quiet, "🔇 Quiet", SymbolRegular.WeatherMoon24),
            (PowerModeState.Balance, "⚖️ Balanced", SymbolRegular.ScaleFill24),
            (PowerModeState.Performance, "🚀 Performance", SymbolRegular.Flash24)
        };

        foreach (var (mode, name, icon) in powerModes)
        {
            var item = new MenuItem
            {
                Header = name,
                SymbolIcon = icon,
                Tag = POWER_MODE_TAG
            };
            item.Click += async (_, _) => await SetPowerModeAsync(mode);
            _contextMenu.Items.Add(item);
        }

        _contextMenu.Items.Add(new Separator { Tag = POWER_MODE_TAG });
    }

    private void InitializeStaticItems(INavigation navigation)
    {
        foreach (var navigationItem in navigation.Items.OfType<NavigationItem>())
        {
            var navigationMenuItem = new MenuItem
            {
                SymbolIcon = navigationItem.Icon,
                Header = navigationItem.Content,
                Tag = NAVIGATION_TAG
            };
            navigationMenuItem.Click += async (_, _) =>
            {
                _contextMenu.IsOpen = false;
                _bringToForeground();

                await Task.Delay(TimeSpan.FromMilliseconds(500));
                if (navigationItem.PageTag is not null)
                    navigation.Navigate(navigationItem.PageTag);
            };
            _contextMenu.Items.Add(navigationMenuItem);
        }

        _contextMenu.Items.Add(new Separator { Tag = NAVIGATION_TAG });

        var openMenuItem = new MenuItem { Header = Resource.Open, Tag = STATIC_TAG };
        openMenuItem.Click += (_, _) =>
        {
            _contextMenu.IsOpen = false;
            _bringToForeground();
        };
        _contextMenu.Items.Add(openMenuItem);

        var closeMenuItem = new MenuItem { Header = Resource.Close, Tag = STATIC_TAG };
        closeMenuItem.Click += async (_, _) =>
        {
            _contextMenu.IsOpen = false;
            await App.Current.ShutdownAsync();
        };
        _contextMenu.Items.Add(closeMenuItem);
    }

    private void SetAutomationItems(List<AutomationPipeline> pipelines)
    {
        foreach (var item in _contextMenu.Items.OfType<Control>().Where(mi => AUTOMATION_TAG.Equals(mi.Tag)).ToArray())
            _contextMenu.Items.Remove(item);

        pipelines = pipelines.Where(p => p.Trigger is null).Reverse().ToList();

        if (pipelines.Count != 0)
        {
            // Find the position after power mode items
            var insertIndex = _contextMenu.Items.OfType<Control>()
                .TakeWhile(c => POWER_MODE_TAG.Equals(c.Tag))
                .Count();
            
            _contextMenu.Items.Insert(insertIndex, new Separator { Tag = AUTOMATION_TAG });
        }

        foreach (var pipeline in pipelines)
        {
            var icon = Enum.TryParse<SymbolRegular>(pipeline.IconName, out var iconParsed)
                ? iconParsed
                : SymbolRegular.Play24;

            var item = new MenuItem
            {
                SymbolIcon = icon,
                Header = pipeline.Name ?? Resource.Unnamed,
                Tag = AUTOMATION_TAG
            };
            item.Click += async (_, _) =>
            {
                try
                {
                    await _automationProcessor.RunNowAsync(pipeline);
                }
                catch {  /* Ignored. */ }
            };

            // Insert after power mode items
            var insertIndex = _contextMenu.Items.OfType<Control>()
                .TakeWhile(c => POWER_MODE_TAG.Equals(c.Tag))
                .Count();
            _contextMenu.Items.Insert(insertIndex, item);
        }
    }

    private async Task SetPowerModeAsync(PowerModeState mode)
    {
        try
        {
            await _powerModeFeature.SetStateAsync(mode);
            _currentPowerMode = mode;
            UpdateTooltip();
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to set power mode to {mode}", ex);
        }
    }

    private void PowerModeListener_Changed(object? sender, PowerModeListener.ChangedEventArgs e)
    {
        _currentPowerMode = e.State;
        Application.Current.Dispatcher.BeginInvoke(UpdateTooltip);
    }

    private void StartTooltipUpdates()
    {
        _tooltipUpdateCts = new CancellationTokenSource();
        var token = _tooltipUpdateCts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                    await Application.Current.Dispatcher.InvokeAsync(async () => await UpdateTooltipWithTempsAsync());
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Ignore errors
                }
            }
        }, token);
    }

    private void UpdateTooltip()
    {
        if (_notifyIcon is null)
            return;

        var powerModeText = _currentPowerMode switch
        {
            PowerModeState.Quiet => "Quiet",
            PowerModeState.Balance => "Balanced",
            PowerModeState.Performance => "Performance",
            PowerModeState.GodMode => "Custom",
            _ => "Unknown"
        };

        _notifyIcon.Text = $"{Resource.AppName}\n⚡ {powerModeText}";
    }

    private async Task UpdateTooltipWithTempsAsync()
    {
        if (_notifyIcon is null)
            return;

        try
        {
            var powerModeText = _currentPowerMode switch
            {
                PowerModeState.Quiet => "Quiet",
                PowerModeState.Balance => "Balanced",
                PowerModeState.Performance => "Performance",
                PowerModeState.GodMode => "Custom",
                _ => "Unknown"
            };

            var sensorData = await _sensorsController.GetDataAsync();
            var cpuTemp = sensorData.CPU.Temperature;
            var gpuTemp = sensorData.GPU.Temperature;

            var tempInfo = "";
            if (cpuTemp > 0 || gpuTemp > 0)
            {
                tempInfo = $"\n🌡️ CPU: {cpuTemp}°C | GPU: {gpuTemp}°C";
            }

            // Tooltip max is 63 characters, so keep it concise
            var tooltip = $"{Resource.AppName}\n⚡ {powerModeText}{tempInfo}";
            if (tooltip.Length > 63)
                tooltip = tooltip[..63];

            _notifyIcon.Text = tooltip;
        }
        catch
        {
            // Fall back to simple tooltip
            UpdateTooltip();
        }
    }

    public void MakeVisible()
    {
        if (_notifyIcon is null)
            return;

        _notifyIcon.Visible = true;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        _tooltipUpdateCts?.Cancel();
        _tooltipUpdateCts?.Dispose();
        _tooltipUpdateCts = null;

        _powerModeListener.Changed -= PowerModeListener_Changed;

        if (_notifyIcon is not null)
            _notifyIcon.Visible = false;

        _notifyIcon?.Dispose();
        _notifyIcon = null;
    }
}
