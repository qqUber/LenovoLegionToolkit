using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Humanizer;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Extensions;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Utils;

using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Pages;

public partial class BatteryPage
{
    private readonly ApplicationSettings _settings = IoCContainer.Resolve<ApplicationSettings>();
    private readonly IFeature<BatteryState> _batteryModeFeature = IoCContainer.Resolve<IFeature<BatteryState>>();
    private readonly IFeature<BatteryNightChargeState> _nightChargeFeature = IoCContainer.Resolve<IFeature<BatteryNightChargeState>>();
    private readonly RefreshRateFeature _refreshRateFeature = IoCContainer.Resolve<RefreshRateFeature>();
    private readonly AutoRefreshRateController _autoRefreshRateController = IoCContainer.Resolve<AutoRefreshRateController>();

    private CancellationTokenSource? _cts;
    private Task? _refreshTask;
    
    private ComboBox? _batteryModeComboBox;
    private ToggleSwitch? _nightChargeToggle;
    private RefreshRate[]? _refreshRates;
    private bool _isInitialized;

    public BatteryPage()
    {
        InitializeComponent();
        
        IsVisibleChanged += BatteryPage_IsVisibleChanged;
        Loaded += BatteryPage_Loaded;
    }

    private async void BatteryPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isInitialized) return;
        _isInitialized = true;
        
        await InitializeBatteryModeControls();
    }

    private async Task InitializeBatteryModeControls()
    {
        // Initialize Battery Mode ComboBox
        try
        {
            if (await _batteryModeFeature.IsSupportedAsync())
            {
                _batteryModeComboBox = new ComboBox { MinWidth = 165 };
                var states = await _batteryModeFeature.GetAllStatesAsync();
                _batteryModeComboBox.SetItems(states, await _batteryModeFeature.GetStateAsync(), v => v.GetDisplayName());
                _batteryModeComboBox.SelectionChanged += BatteryModeComboBox_SelectionChanged;
                _batteryModeHeader.Accessory = _batteryModeComboBox;
                _batteryModeCard.Icon = SymbolRegular.BatteryCharge24;
            }
            else
            {
                _batteryModeCard.Visibility = Visibility.Collapsed;
            }
        }
        catch
        {
            _batteryModeCard.Visibility = Visibility.Collapsed;
        }

        // Initialize Night Charge Toggle
        try
        {
            if (await _nightChargeFeature.IsSupportedAsync())
            {
                _nightChargeToggle = new ToggleSwitch();
                var currentState = await _nightChargeFeature.GetStateAsync();
                _nightChargeToggle.IsChecked = currentState == BatteryNightChargeState.On;
                _nightChargeToggle.Checked += NightChargeToggle_Changed;
                _nightChargeToggle.Unchecked += NightChargeToggle_Changed;
                _nightChargeHeader.Accessory = _nightChargeToggle;
            }
            else
            {
                _nightChargeCard.Visibility = Visibility.Collapsed;
            }
        }
        catch
        {
            _nightChargeCard.Visibility = Visibility.Collapsed;
        }

        // Initialize Auto Refresh Rate Switching
        try
        {
            if (await _refreshRateFeature.IsSupportedAsync())
            {
                var allRates = await _refreshRateFeature.GetAllStatesAsync();
                // Only keep 60Hz and 144Hz options
                _refreshRates = System.Linq.Enumerable.ToArray(
                    allRates.Where(r => r.Frequency == 60 || r.Frequency == 144)
                );
                
                if (_refreshRates.Length < 2)
                {
                    // If we don't have both 60 and 144, hide the feature
                    _autoRefreshRateCard.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // Populate On Battery ComboBox
                    _onBatteryRefreshRateComboBox.Items.Clear();
                    foreach (var rate in _refreshRates)
                    {
                        _onBatteryRefreshRateComboBox.Items.Add(rate.DisplayName);
                    }
                    
                    // Populate On AC ComboBox
                    _onACRefreshRateComboBox.Items.Clear();
                    foreach (var rate in _refreshRates)
                    {
                        _onACRefreshRateComboBox.Items.Add(rate.DisplayName);
                    }

                    // Default: 60Hz for battery, 144Hz for AC
                    var idx60 = Array.FindIndex(_refreshRates, r => r.Frequency == 60);
                    var idx144 = Array.FindIndex(_refreshRates, r => r.Frequency == 144);

                    // Set saved values or defaults
                    var savedOnBattery = _settings.Store.OnBatteryRefreshRate;
                    var savedOnAC = _settings.Store.OnACRefreshRate;

                    if (savedOnBattery.HasValue)
                    {
                        var idx = Array.FindIndex(_refreshRates, r => r.Frequency == savedOnBattery.Value.Frequency);
                        _onBatteryRefreshRateComboBox.SelectedIndex = idx >= 0 ? idx : idx60;
                    }
                    else
                    {
                        _onBatteryRefreshRateComboBox.SelectedIndex = idx60 >= 0 ? idx60 : 0;
                        // Save default
                        _settings.Store.OnBatteryRefreshRate = _refreshRates[_onBatteryRefreshRateComboBox.SelectedIndex];
                    }
                    
                    if (savedOnAC.HasValue)
                    {
                        var idx = Array.FindIndex(_refreshRates, r => r.Frequency == savedOnAC.Value.Frequency);
                        _onACRefreshRateComboBox.SelectedIndex = idx >= 0 ? idx : idx144;
                    }
                    else
                    {
                        _onACRefreshRateComboBox.SelectedIndex = idx144 >= 0 ? idx144 : _refreshRates.Length - 1;
                        // Save default
                        _settings.Store.OnACRefreshRate = _refreshRates[_onACRefreshRateComboBox.SelectedIndex];
                    }
                    
                    _settings.SynchronizeStore();

                    // Set toggle state
                    _autoRefreshRateToggle.IsChecked = _settings.Store.AutoRefreshRateEnabled;

                    // Wire up events
                    _onBatteryRefreshRateComboBox.SelectionChanged += OnBatteryRefreshRateComboBox_SelectionChanged;
                    _onACRefreshRateComboBox.SelectionChanged += OnACRefreshRateComboBox_SelectionChanged;
                    _autoRefreshRateToggle.Checked += AutoRefreshRateToggle_Changed;
                    _autoRefreshRateToggle.Unchecked += AutoRefreshRateToggle_Changed;
                }
            }
            else
            {
                _autoRefreshRateCard.Visibility = Visibility.Collapsed;
            }
        }
        catch
        {
            _autoRefreshRateCard.Visibility = Visibility.Collapsed;
        }
    }

    private async void BatteryModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_batteryModeComboBox?.TryGetSelectedItem(out BatteryState state) == true)
        {
            try
            {
                await _batteryModeFeature.SetStateAsync(state);
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Failed to set battery mode.", ex);
            }
        }
    }

    private async void NightChargeToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_nightChargeToggle == null) return;
        
        try
        {
            var newState = _nightChargeToggle.IsChecked == true 
                ? BatteryNightChargeState.On 
                : BatteryNightChargeState.Off;
            await _nightChargeFeature.SetStateAsync(newState);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to set night charge mode.", ex);
        }
    }

    private void OnBatteryRefreshRateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshRates == null || _onBatteryRefreshRateComboBox.SelectedIndex < 0) return;

        var selectedRate = _refreshRates[_onBatteryRefreshRateComboBox.SelectedIndex];
        _settings.Store.OnBatteryRefreshRate = selectedRate;
        _settings.SynchronizeStore();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"On battery refresh rate set to {selectedRate.Frequency}Hz");
    }

    private void OnACRefreshRateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshRates == null || _onACRefreshRateComboBox.SelectedIndex < 0) return;

        var selectedRate = _refreshRates[_onACRefreshRateComboBox.SelectedIndex];
        _settings.Store.OnACRefreshRate = selectedRate;
        _settings.SynchronizeStore();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"On AC refresh rate set to {selectedRate.Frequency}Hz");
    }

    private async void AutoRefreshRateToggle_Changed(object sender, RoutedEventArgs e)
    {
        var isEnabled = _autoRefreshRateToggle.IsChecked == true;
        _settings.Store.AutoRefreshRateEnabled = isEnabled;
        _settings.SynchronizeStore();

        if (isEnabled)
        {
            // Start controller and apply immediately
            await _autoRefreshRateController.StartAsync();
            
            // Also apply the correct rate for current power state right now
            try
            {
                var powerStatus = await Power.IsPowerAdapterConnectedAsync();
                var targetRate = powerStatus == PowerAdapterStatus.Connected
                    ? _settings.Store.OnACRefreshRate
                    : _settings.Store.OnBatteryRefreshRate;
                    
                if (targetRate.HasValue)
                {
                    await _refreshRateFeature.SetStateAsync(targetRate.Value);
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Applied refresh rate {targetRate.Value.Frequency}Hz for {powerStatus}");
                }
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Failed to apply refresh rate on toggle", ex);
            }
        }
        else
        {
            await _autoRefreshRateController.StopAsync();
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Auto refresh rate switching {(isEnabled ? "enabled" : "disabled")}");
    }

    private async void BatteryPage_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            Refresh();
            return;
        }

        if (_cts is not null)
            await _cts.CancelAsync();

        _cts = null;

        if (_refreshTask is not null)
            await _refreshTask;

        _refreshTask = null;
    }

    private void Refresh()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        var token = _cts.Token;

        _refreshTask = Task.Run(async () =>
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Battery information refresh started...");

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var batteryInfo = Battery.GetBatteryInformation();
                    var powerAdapterStatus = await Power.IsPowerAdapterConnectedAsync();
                    var onBatterySince = Battery.GetOnBatterySince();
                    _ = Dispatcher.BeginInvoke(() => Set(batteryInfo, powerAdapterStatus, onBatterySince));

                    await Task.Delay(TimeSpan.FromSeconds(2), token);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Battery information refresh failed.", ex);
                }
            }

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Battery information refresh stopped.");
        }, token);
    }

    private void Set(BatteryInformation batteryInfo, PowerAdapterStatus powerAdapterStatus, DateTime? onBatterySince)
    {
        // Update battery fill bar (max width 60)
        var fillWidth = Math.Max(4, batteryInfo.BatteryPercentage / 100.0 * 60);
        _batteryFill.Width = fillWidth;
        
        // Update battery fill color based on level
        if (batteryInfo.BatteryPercentage <= 20)
            _batteryFill.Background = new SolidColorBrush(Color.FromRgb(232, 17, 35)); // Red
        else if (batteryInfo.BatteryPercentage <= 40)
            _batteryFill.Background = new SolidColorBrush(Color.FromRgb(255, 185, 0)); // Yellow/Orange
        else
            _batteryFill.Background = (Brush)FindResource("SystemFillColorSuccessBrush");

        _percentRemaining.Text = $"{batteryInfo.BatteryPercentage}%";
        _status.Text = GetStatusText(batteryInfo);
        
        // Time remaining
        if (!batteryInfo.IsCharging && batteryInfo.BatteryLifeRemaining > 0)
        {
            var time = TimeSpan.FromSeconds(batteryInfo.BatteryLifeRemaining).Humanize(2, Resource.Culture);
            _timeRemaining.Text = string.Format(Resource.BatteryPage_Remaining, time);
            _timeRemaining.Visibility = Visibility.Visible;
        }
        else if (batteryInfo.IsCharging && batteryInfo.FullBatteryLifeRemaining > 0)
        {
            var time = TimeSpan.FromSeconds(batteryInfo.FullBatteryLifeRemaining).Humanize(2, Resource.Culture);
            _timeRemaining.Text = string.Format(Resource.BatteryPage_ToFullCharge, time);
            _timeRemaining.Visibility = Visibility.Visible;
        }
        else
        {
            _timeRemaining.Visibility = Visibility.Collapsed;
        }

        _lowBattery.Visibility = batteryInfo.IsLowBattery ? Visibility.Visible : Visibility.Collapsed;
        _lowWattageCharger.Visibility = powerAdapterStatus == PowerAdapterStatus.ConnectedLowWattage ? Visibility.Visible : Visibility.Collapsed;

        // Health ring
        var health = batteryInfo.BatteryHealth;
        _healthRing.Progress = health;
        _healthPercentText.Text = $"{health:0}%";

        // Temperature
        _batteryTemperatureText.Text = batteryInfo.BatteryTemperatureC is not null 
            ? GetTemperatureText(batteryInfo.BatteryTemperatureC) 
            : "-";

        // Discharge rate - positive (+) when charging, negative (-) when discharging
        var dischargeSign = batteryInfo.IsCharging ? "+" : "-";
        _batteryDischargeRateText.Text = $"{dischargeSign}{Math.Abs(batteryInfo.DischargeRate / 1000.0):0.00} W";
        _batteryMinDischargeRateText.Text = $"{dischargeSign}{Math.Abs(batteryInfo.MinDischargeRate / 1000.0):0.00} W";
        _batteryMaxDischargeRateText.Text = $"{dischargeSign}{Math.Abs(batteryInfo.MaxDischargeRate / 1000.0):0.00} W";

        // Cycle count
        _batteryCycleCountText.Text = $"{batteryInfo.CycleCount}";

        // Capacity details
        _batteryCapacityText.Text = $"{batteryInfo.EstimateChargeRemaining / 1000.0:0.00} Wh";
        _batteryFullChargeCapacityText.Text = $"{batteryInfo.FullChargeCapacity / 1000.0:0.00} Wh";
        _batteryDesignCapacityText.Text = $"{batteryInfo.DesignCapacity / 1000.0:0.00} Wh";
        _batteryHealthText.Text = $"{health:0.0}%";

        // Capacity bar
        if (batteryInfo.DesignCapacity > 0)
        {
            var fullChargePercent = (double)batteryInfo.FullChargeCapacity / batteryInfo.DesignCapacity * 100;
            var currentPercent = (double)batteryInfo.EstimateChargeRemaining / batteryInfo.DesignCapacity * 100;
            _capacityBar.Value = Math.Min(100, fullChargePercent);
            _currentCapacityBar.Value = Math.Min(100, currentPercent);
        }

        // On battery since
        if (!batteryInfo.IsCharging && onBatterySince.HasValue)
        {
            var onBatterySinceValue = onBatterySince.Value;
            var dateText = onBatterySinceValue.ToString("G", Resource.Culture);
            var duration = DateTime.Now.Subtract(onBatterySinceValue);
            _onBatterySinceText.Text = $"{dateText} ({duration.Humanize(2, Resource.Culture, minUnit: TimeUnit.Second)})";
        }
        else
        {
            _onBatterySinceText.Text = "-";
        }

        // Dates
        if (batteryInfo.ManufactureDate is not null)
            _batteryManufactureDateText.Text = batteryInfo.ManufactureDate?.ToString(LocalizationHelper.ShortDateFormat) ?? "-";
        else
            _batteryManufactureDateCardControl.Visibility = Visibility.Collapsed;

        if (batteryInfo.FirstUseDate is not null)
            _batteryFirstUseDateText.Text = batteryInfo.FirstUseDate?.ToString(LocalizationHelper.ShortDateFormat) ?? "-";
        else
            _batteryFirstUseDateCardControl.Visibility = Visibility.Collapsed;
    }

    private static string GetStatusText(BatteryInformation batteryInfo)
    {
        if (batteryInfo.IsCharging)
        {
            if (batteryInfo.DischargeRate > 0)
                return Resource.BatteryPage_ACAdapterConnectedAndCharging;

            return Resource.BatteryPage_ACAdapterConnectedNotCharging;
        }

        return Resource.BatteryPage_OnBattery;
    }

    private string GetTemperatureText(double? temperature)
    {
        if (temperature is null)
            return "-";

        if (_settings.Store.TemperatureUnit == TemperatureUnit.F)
        {
            temperature *= 9.0 / 5.0;
            temperature += 32;
            return $"{temperature:0.0} {Resource.Fahrenheit}";
        }

        return $"{temperature:0.0} {Resource.Celsius}";
    }
}
