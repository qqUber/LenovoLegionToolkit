using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Integrations;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.SoftwareDisabler;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.CLI;
using LenovoLegionToolkit.WPF.Extensions;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Settings;
using LenovoLegionToolkit.WPF.Utils;
using LenovoLegionToolkit.WPF.Windows;
using LenovoLegionToolkit.WPF.Windows.Settings;

namespace LenovoLegionToolkit.WPF.Pages;

public partial class SettingsPage
{
    private readonly ApplicationSettings _settings = IoCContainer.Resolve<ApplicationSettings>();
    private readonly IntegrationsSettings _integrationsSettings = IoCContainer.Resolve<IntegrationsSettings>();
    private readonly DashboardSettings _dashboardSettings = IoCContainer.Resolve<DashboardSettings>();

    private readonly VantageDisabler _vantageDisabler = IoCContainer.Resolve<VantageDisabler>();
    private readonly LegionZoneDisabler _legionZoneDisabler = IoCContainer.Resolve<LegionZoneDisabler>();
    private readonly FnKeysDisabler _fnKeysDisabler = IoCContainer.Resolve<FnKeysDisabler>();
    private readonly PowerModeFeature _powerModeFeature = IoCContainer.Resolve<PowerModeFeature>();
    private readonly RGBKeyboardBacklightController _rgbKeyboardBacklightController = IoCContainer.Resolve<RGBKeyboardBacklightController>();
    private readonly ThemeManager _themeManager = IoCContainer.Resolve<ThemeManager>();
    private readonly HWiNFOIntegration _hwinfoIntegration = IoCContainer.Resolve<HWiNFOIntegration>();
    private readonly IpcServer _ipcServer = IoCContainer.Resolve<IpcServer>();
    private readonly UpdateChecker _updateChecker = IoCContainer.Resolve<UpdateChecker>();
    private readonly UpdateCheckSettings _updateCheckSettings = IoCContainer.Resolve<UpdateCheckSettings>();

    private bool _isRefreshing;

    public SettingsPage()
    {
        InitializeComponent();

        IsVisibleChanged += SettingsPage_IsVisibleChanged;

        _themeManager.ThemeApplied += ThemeManager_ThemeApplied;
    }

    private async void SettingsPage_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
            await RefreshAsync();
    }

    private void ThemeManager_ThemeApplied(object? sender, EventArgs e)
    {
        if (!_isRefreshing)
            UpdateAccentColorPicker();
    }

    private async Task RefreshAsync()
    {
        _isRefreshing = true;

        var loadingTask = Task.Delay(TimeSpan.FromMilliseconds(500));

        var languages = LocalizationHelper.Languages.OrderBy(LocalizationHelper.LanguageDisplayName, StringComparer.InvariantCultureIgnoreCase).ToArray();
        var language = await LocalizationHelper.GetLanguageAsync();
        if (languages.Length > 1)
        {
            _langComboBox.SetItems(languages, language, LocalizationHelper.LanguageDisplayName);
            _langComboBox.Visibility = Visibility.Visible;
        }
        else
        {
            _langCardControl.Visibility = Visibility.Collapsed;
        }

        _temperatureComboBox.SetItems(Enum.GetValues<TemperatureUnit>(), _settings.Store.TemperatureUnit, t => t switch
        {
            TemperatureUnit.C => Resource.Celsius,
            TemperatureUnit.F => Resource.Fahrenheit,
            _ => new ArgumentOutOfRangeException(nameof(t))
        });
        _themeComboBox.SetItems(Enum.GetValues<Theme>(), _settings.Store.Theme, t => t.GetDisplayName());

        UpdateAccentColorPicker();
        _accentColorSourceComboBox.SetItems(Enum.GetValues<AccentColorSource>(), _settings.Store.AccentColorSource, t => t.GetDisplayName());

        _sensorsLayoutComboBox.SetItems(Enum.GetValues<SensorsLayout>(), _dashboardSettings.Store.SensorsLayout, t => t switch
        {
            SensorsLayout.Cards => Resource.SensorsLayout_Cards,
            SensorsLayout.Compact => Resource.SensorsLayout_Compact,
            _ => throw new ArgumentOutOfRangeException(nameof(t))
        });

        _autorunComboBox.SetItems(Enum.GetValues<AutorunState>(), Autorun.State, t => t.GetDisplayName());
        _minimizeToTrayToggle.IsChecked = _settings.Store.MinimizeToTray;
        _minimizeOnCloseToggle.IsChecked = _settings.Store.MinimizeOnClose;

        var vantageStatus = await _vantageDisabler.GetStatusAsync();
        _vantageCard.Visibility = vantageStatus != SoftwareStatus.NotFound ? Visibility.Visible : Visibility.Collapsed;
        _vantageToggle.IsChecked = vantageStatus == SoftwareStatus.Disabled;

        var legionZoneStatus = await _legionZoneDisabler.GetStatusAsync();
        _legionZoneCard.Visibility = legionZoneStatus != SoftwareStatus.NotFound ? Visibility.Visible : Visibility.Collapsed;
        _legionZoneToggle.IsChecked = legionZoneStatus == SoftwareStatus.Disabled;

        var fnKeysStatus = await _fnKeysDisabler.GetStatusAsync();
        _fnKeysCard.Visibility = fnKeysStatus != SoftwareStatus.NotFound ? Visibility.Visible : Visibility.Collapsed;
        _fnKeysToggle.IsChecked = fnKeysStatus == SoftwareStatus.Disabled;

        _smartFnLockComboBox.SetItems([ModifierKey.None, ModifierKey.Alt, ModifierKey.Alt | ModifierKey.Ctrl | ModifierKey.Shift],
            _settings.Store.SmartFnLockFlags,
            m => m is ModifierKey.None ? Resource.Off : m.GetFlagsDisplayName(ModifierKey.None));

        _smartKeySinglePressActionCard.Visibility = fnKeysStatus != SoftwareStatus.Enabled ? Visibility.Visible : Visibility.Collapsed;
        _smartKeyDoublePressActionCard.Visibility = fnKeysStatus != SoftwareStatus.Enabled ? Visibility.Visible : Visibility.Collapsed;

        _notificationsCard.Visibility = fnKeysStatus != SoftwareStatus.Enabled ? Visibility.Visible : Visibility.Collapsed;
        _excludeRefreshRatesCard.Visibility = fnKeysStatus != SoftwareStatus.Enabled ? Visibility.Visible : Visibility.Collapsed;
        _synchronizeBrightnessToAllPowerPlansToggle.IsChecked = _settings.Store.SynchronizeBrightnessToAllPowerPlans;
        _onBatterySinceResetToggle.IsChecked = _settings.Store.ResetBatteryOnSinceTimerOnReboot;

        _bootLogoCard.Visibility = await BootLogo.IsSupportedAsync() ? Visibility.Visible : Visibility.Collapsed;

        if (_updateChecker.Disable)
        {
            _updateTextBlock.Visibility = Visibility.Collapsed;
            _checkUpdatesCard.Visibility = Visibility.Collapsed;
            _updateCheckFrequencyCard.Visibility = Visibility.Collapsed;
        }
        else
        {
            _checkUpdatesButton.Visibility = Visibility.Visible;
            _updateCheckFrequencyComboBox.Visibility = Visibility.Visible;
            _updateCheckFrequencyComboBox.SetItems(Enum.GetValues<UpdateCheckFrequency>(), _updateCheckSettings.Store.UpdateCheckFrequency, t => t.GetDisplayName());
        }

        try
        {
            var mi = await Compatibility.GetMachineInformationAsync();
            if (mi.Features[CapabilityID.GodModeFnQSwitchable])
            {
                _godModeFnQSwitchableCard.Visibility = Visibility.Visible;
                _godModeFnQSwitchableToggle.IsChecked = await WMI.LenovoOtherMethod.GetFeatureValueAsync(CapabilityID.GodModeFnQSwitchable) == 1;
            }
            else
            {
                _godModeFnQSwitchableCard.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            _godModeFnQSwitchableCard.Visibility = Visibility.Collapsed;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to get GodModeFnQSwitchable status.", ex);
        }

        _powerModeMappingComboBox.SetItems(Enum.GetValues<PowerModeMappingMode>(), _settings.Store.PowerModeMappingMode, t => t.GetDisplayName());

        var isPowerModeFeatureSupported = await _powerModeFeature.IsSupportedAsync();
        _powerModeMappingCard.Visibility = isPowerModeFeatureSupported ? Visibility.Visible : Visibility.Collapsed;
        _powerModesCard.Visibility = _settings.Store.PowerModeMappingMode == PowerModeMappingMode.WindowsPowerMode && isPowerModeFeatureSupported ? Visibility.Visible : Visibility.Collapsed;
        _windowsPowerPlansCard.Visibility = _settings.Store.PowerModeMappingMode == PowerModeMappingMode.WindowsPowerPlan && isPowerModeFeatureSupported ? Visibility.Visible : Visibility.Collapsed;
        _windowsPowerPlansControlPanelCard.Visibility = _settings.Store.PowerModeMappingMode == PowerModeMappingMode.WindowsPowerPlan && isPowerModeFeatureSupported ? Visibility.Visible : Visibility.Collapsed;

        _onBatterySinceResetToggle.Visibility = Visibility.Visible;

        _hwinfoIntegrationToggle.IsChecked = _integrationsSettings.Store.HWiNFO;
        _cliInterfaceToggle.IsChecked = _integrationsSettings.Store.CLI;
        _cliPathToggle.IsChecked = SystemPath.HasCLI();

        await loadingTask;

        _temperatureComboBox.Visibility = Visibility.Visible;
        _themeComboBox.Visibility = Visibility.Visible;
        _sensorsLayoutComboBox.Visibility = Visibility.Visible;
        _autorunComboBox.Visibility = Visibility.Visible;
        _minimizeToTrayToggle.Visibility = Visibility.Visible;
        _minimizeOnCloseToggle.Visibility = Visibility.Visible;
        _vantageToggle.Visibility = Visibility.Visible;
        _legionZoneToggle.Visibility = Visibility.Visible;
        _fnKeysToggle.Visibility = Visibility.Visible;
        _smartFnLockComboBox.Visibility = Visibility.Visible;
        _synchronizeBrightnessToAllPowerPlansToggle.Visibility = Visibility.Visible;
        _godModeFnQSwitchableToggle.Visibility = Visibility.Visible;
        _powerModeMappingComboBox.Visibility = Visibility.Visible;
        _hwinfoIntegrationToggle.Visibility = Visibility.Visible;
        _cliInterfaceToggle.Visibility = Visibility.Visible;
        _cliPathToggle.Visibility = Visibility.Visible;

        // Custom Background settings
        _backgroundTypeComboBox.SetItems(Enum.GetValues<CustomBackgroundType>(), _settings.Store.CustomBackgroundType);
        _customBackgroundToggle.IsChecked = _settings.Store.CustomBackgroundEnabled;
        _videoBackgroundToggle.IsChecked = _settings.Store.CustomBackgroundEnabled;
        _backgroundOpacitySlider.Value = _settings.Store.CustomBackgroundOpacity;
        _backgroundOpacityText.Text = $"{(int)(_settings.Store.CustomBackgroundOpacity * 100)}%";
        _backgroundBlurToggle.IsChecked = _settings.Store.CustomBackgroundBlur;
        _blurRadiusSlider.Value = _settings.Store.CustomBackgroundBlurRadius;
        _blurRadiusText.Text = $"{(int)_settings.Store.CustomBackgroundBlurRadius}px";
        _blurRadiusCard.Visibility = _settings.Store.CustomBackgroundBlur ? Visibility.Visible : Visibility.Collapsed;
        
        // Slideshow settings
        _slideshowShuffleToggle.IsChecked = _settings.Store.SlideshowShuffle;
        UpdateSlideshowIntervalComboBox();
        UpdateSlideshowCountText();
        
        // Tint color picker
        if (_settings.Store.CustomBackgroundTint.HasValue)
        {
            var tint = _settings.Store.CustomBackgroundTint.Value;
            _backgroundTintPicker.SelectedColor = System.Windows.Media.Color.FromRgb(tint.R, tint.G, tint.B);
        }
        
        // Initialize preset gallery
        InitializePresetGallery();
        
        UpdateBackgroundButtonStates();
        UpdateBackgroundTypeVisibility();

        _isRefreshing = false;
    }

    private async void LangComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshing)
            return;

        if (!_langComboBox.TryGetSelectedItem(out CultureInfo? cultureInfo) || cultureInfo is null)
            return;

        await LocalizationHelper.SetLanguageAsync(cultureInfo);

        App.Current.RestartMainWindow();
    }

    private void TemperatureComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshing)
            return;

        if (!_temperatureComboBox.TryGetSelectedItem(out TemperatureUnit temperatureUnit))
            return;

        _settings.Store.TemperatureUnit = temperatureUnit;
        _settings.SynchronizeStore();
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshing)
            return;

        if (!_themeComboBox.TryGetSelectedItem(out Theme state))
            return;

        _settings.Store.Theme = state;
        _settings.SynchronizeStore();

        _themeManager.Apply();
    }

    private void AccentColorPicker_Changed(object sender, EventArgs e)
    {
        if (_isRefreshing)
            return;

        if (_settings.Store.AccentColorSource != AccentColorSource.Custom)
            return;

        _settings.Store.AccentColor = _accentColorPicker.SelectedColor.ToRGBColor();
        _settings.SynchronizeStore();

        _themeManager.Apply();
    }

    private void AccentColorSourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshing)
            return;

        if (!_accentColorSourceComboBox.TryGetSelectedItem(out AccentColorSource state))
            return;

        _settings.Store.AccentColorSource = state;
        _settings.SynchronizeStore();

        UpdateAccentColorPicker();

        _themeManager.Apply();
    }

    private void SensorsLayoutComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshing)
            return;

        if (!_sensorsLayoutComboBox.TryGetSelectedItem(out SensorsLayout layout))
            return;

        _dashboardSettings.Store.SensorsLayout = layout;
        _dashboardSettings.SynchronizeStore();
    }

    private void SelectBackgroundButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Resource.SettingsPage_CustomBackground_SelectImage,
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            _settings.Store.CustomBackgroundPath = dialog.FileName;
            _settings.SynchronizeStore();
            UpdateBackgroundButtonStates();
            ApplyBackgroundToMainWindow();
        }
    }

    private void ClearBackgroundButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.Store.CustomBackgroundPath = null;
        _settings.Store.CustomBackgroundEnabled = false;
        _settings.SynchronizeStore();
        
        _customBackgroundToggle.IsChecked = false;
        UpdateBackgroundButtonStates();
        ApplyBackgroundToMainWindow();
    }

    private void CustomBackgroundToggle_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_settings.Store.CustomBackgroundPath))
        {
            _customBackgroundToggle.IsChecked = false;
            return;
        }

        _settings.Store.CustomBackgroundEnabled = _customBackgroundToggle.IsChecked == true;
        _settings.SynchronizeStore();
        ApplyBackgroundToMainWindow();
    }

    private void BackgroundOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isRefreshing || _backgroundOpacityText == null)
            return;

        _settings.Store.CustomBackgroundOpacity = _backgroundOpacitySlider.Value;
        _settings.SynchronizeStore();
        
        _backgroundOpacityText.Text = $"{(int)(_backgroundOpacitySlider.Value * 100)}%";
        ApplyBackgroundToMainWindow();
    }

    private void BackgroundBlurToggle_Click(object sender, RoutedEventArgs e)
    {
        _settings.Store.CustomBackgroundBlur = _backgroundBlurToggle.IsChecked == true;
        _settings.SynchronizeStore();
        _blurRadiusCard.Visibility = _backgroundBlurToggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ApplyBackgroundToMainWindow();
    }

    private void BlurRadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isRefreshing || _blurRadiusText == null)
            return;

        _settings.Store.CustomBackgroundBlurRadius = _blurRadiusSlider.Value;
        _settings.SynchronizeStore();
        
        _blurRadiusText.Text = $"{(int)_blurRadiusSlider.Value}px";
        ApplyBackgroundToMainWindow();
    }

    private void BackgroundTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshing)
            return;

        if (!_backgroundTypeComboBox.TryGetSelectedItem(out CustomBackgroundType backgroundType))
            return;

        _settings.Store.CustomBackgroundType = backgroundType;
        _settings.SynchronizeStore();
        UpdateBackgroundTypeVisibility();
        ApplyBackgroundToMainWindow();
    }

    private void UpdateBackgroundTypeVisibility()
    {
        var bgType = _settings.Store.CustomBackgroundType;
        
        _imageCard.Visibility = bgType == CustomBackgroundType.Image ? Visibility.Visible : Visibility.Collapsed;
        _videoCard.Visibility = bgType == CustomBackgroundType.Video ? Visibility.Visible : Visibility.Collapsed;
        _slideshowCard.Visibility = bgType == CustomBackgroundType.Slideshow ? Visibility.Visible : Visibility.Collapsed;
        _presetCard.Visibility = bgType == CustomBackgroundType.Preset ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SelectVideoButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Video",
            Filter = "Video files (*.mp4;*.webm;*.wmv;*.avi)|*.mp4;*.webm;*.wmv;*.avi|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            _settings.Store.CustomBackgroundPath = dialog.FileName;
            _settings.Store.CustomBackgroundEnabled = true;
            _settings.SynchronizeStore();
            _videoBackgroundToggle.IsChecked = true;
            UpdateBackgroundButtonStates();
            ApplyBackgroundToMainWindow();
        }
    }

    private void AddSlideshowImagesButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Add Images",
            Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var fileName in dialog.FileNames)
            {
                if (!_settings.Store.SlideshowImages.Contains(fileName))
                    _settings.Store.SlideshowImages.Add(fileName);
            }
            _settings.Store.CustomBackgroundEnabled = _settings.Store.SlideshowImages.Count > 0;
            _settings.SynchronizeStore();
            UpdateSlideshowCountText();
            ApplyBackgroundToMainWindow();
        }
    }

    private void ClearSlideshowButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.Store.SlideshowImages.Clear();
        _settings.Store.CustomBackgroundEnabled = false;
        _settings.SynchronizeStore();
        UpdateSlideshowCountText();
        ApplyBackgroundToMainWindow();
    }

    private void SlideshowIntervalComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshing)
            return;

        if (_slideshowIntervalComboBox.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var interval))
        {
            _settings.Store.SlideshowIntervalSeconds = interval;
            _settings.SynchronizeStore();
            ApplyBackgroundToMainWindow();
        }
    }

    private void SlideshowShuffleToggle_Click(object sender, RoutedEventArgs e)
    {
        _settings.Store.SlideshowShuffle = _slideshowShuffleToggle.IsChecked == true;
        _settings.SynchronizeStore();
        ApplyBackgroundToMainWindow();
    }

    private void UpdateSlideshowCountText()
    {
        var count = _settings.Store.SlideshowImages.Count;
        _slideshowCountText.Text = $"{count} image{(count != 1 ? "s" : "")}";
    }

    private void UpdateSlideshowIntervalComboBox()
    {
        var interval = _settings.Store.SlideshowIntervalSeconds;
        foreach (ComboBoxItem item in _slideshowIntervalComboBox.Items)
        {
            if (int.TryParse(item.Tag?.ToString(), out var itemInterval) && itemInterval == interval)
            {
                _slideshowIntervalComboBox.SelectedItem = item;
                break;
            }
        }
    }

    private void InitializePresetGallery()
    {
        _presetGallery.Children.Clear();
        
        var presets = new[]
        {
            ("None", (System.Windows.Media.Brush)System.Windows.Media.Brushes.Transparent),
            ("Gradient1", CreateGradient("#8B5CF6", "#3B82F6")),  // Purple Haze
            ("Gradient2", CreateGradient("#0EA5E9", "#22D3EE")),  // Ocean Blue
            ("Gradient3", CreateGradient("#F97316", "#EF4444")),  // Sunset
            ("Gradient4", CreateGradient("#22C55E", "#10B981")),  // Forest
            ("Gradient5", CreateGradient("#1E293B", "#334155")),  // Midnight
        };

        foreach (var (name, brush) in presets)
        {
            var border = new Border
            {
                Width = 80,
                Height = 50,
                Margin = new Thickness(0, 0, 8, 8),
                CornerRadius = new CornerRadius(8),
                Background = brush,
                Cursor = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(_settings.Store.SelectedPreset == name ? 2 : 0),
                Tag = name
            };
            border.SetResourceReference(Border.BorderBrushProperty, "SystemAccentColorPrimaryBrush");
            border.MouseLeftButtonDown += PresetBorder_Click;
            
            _presetGallery.Children.Add(border);
        }
    }

    private static System.Windows.Media.LinearGradientBrush CreateGradient(string color1, string color2)
    {
        return new System.Windows.Media.LinearGradientBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color1),
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color2),
            45);
    }

    private void PresetBorder_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not Border border || border.Tag is not string presetName)
            return;

        _settings.Store.SelectedPreset = presetName;
        _settings.Store.CustomBackgroundEnabled = presetName != "None";
        _settings.SynchronizeStore();
        
        // Update selection visuals
        foreach (Border child in _presetGallery.Children)
        {
            child.BorderThickness = new Thickness(child.Tag?.ToString() == presetName ? 2 : 0);
        }
        
        ApplyBackgroundToMainWindow();
    }

    private void BackgroundTintPicker_Changed(object sender, EventArgs e)
    {
        if (_isRefreshing)
            return;

        var color = _backgroundTintPicker.SelectedColor;
        _settings.Store.CustomBackgroundTint = new RGBColor(color.R, color.G, color.B);
        _settings.SynchronizeStore();
        ApplyBackgroundToMainWindow();
    }

    private void ClearTintButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.Store.CustomBackgroundTint = null;
        _settings.SynchronizeStore();
        _backgroundTintPicker.SelectedColor = System.Windows.Media.Colors.Transparent;
        ApplyBackgroundToMainWindow();
    }

    private void UpdateBackgroundButtonStates()
    {
        var hasBackground = !string.IsNullOrEmpty(_settings.Store.CustomBackgroundPath);
        _clearBackgroundButton.IsEnabled = hasBackground;
        _customBackgroundToggle.IsEnabled = hasBackground;
        
        if (hasBackground)
        {
            var fileName = System.IO.Path.GetFileName(_settings.Store.CustomBackgroundPath);
            _selectBackgroundButton.Content = fileName?.Length > 20 ? fileName[..17] + "..." : fileName;
        }
        else
        {
            _selectBackgroundButton.Content = Resource.SettingsPage_CustomBackground_SelectImage;
        }
    }

    private void ApplyBackgroundToMainWindow()
    {
        if (Application.Current.MainWindow is MainWindow mainWindow)
        {
            mainWindow.ApplyCustomBackground();
        }
    }

    private void UpdateAccentColorPicker()
    {
        _accentColorPicker.Visibility = _settings.Store.AccentColorSource == AccentColorSource.Custom ? Visibility.Visible : Visibility.Collapsed;
        _accentColorPicker.SelectedColor = _themeManager.GetAccentColor().ToColor();
    }

    private void AutorunComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshing)
            return;

        if (!_autorunComboBox.TryGetSelectedItem(out AutorunState state))
            return;

        Autorun.Set(state);
    }

    private void SmartFnLockComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshing)
            return;

        if (!_smartFnLockComboBox.TryGetSelectedItem(out ModifierKey modifierKey))
            return;

        _settings.Store.SmartFnLockFlags = modifierKey;
        _settings.SynchronizeStore();
    }

    private void SmartKeySinglePressActionCard_Click(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing)
            return;

        var window = new SelectSmartKeyPipelinesWindow { Owner = Window.GetWindow(this) };
        window.ShowDialog();
    }

    private void SmartKeyDoublePressActionCard_Click(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing)
            return;

        var window = new SelectSmartKeyPipelinesWindow(isDoublePress: true) { Owner = Window.GetWindow(this) };
        window.ShowDialog();
    }

    private void MinimizeToTrayToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing)
            return;

        var state = _minimizeToTrayToggle.IsChecked;
        if (state is null)
            return;

        _settings.Store.MinimizeToTray = state.Value;
        _settings.SynchronizeStore();
    }

    private void MinimizeOnCloseToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing)
            return;

        var state = _minimizeOnCloseToggle.IsChecked;
        if (state is null)
            return;

        _settings.Store.MinimizeOnClose = state.Value;
        _settings.SynchronizeStore();
    }

    private async void VantageToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing)
            return;

        _vantageToggle.IsEnabled = false;

        var state = _vantageToggle.IsChecked;
        if (state is null)
            return;

        if (state.Value)
        {
            try
            {
                await _vantageDisabler.DisableAsync();
            }
            catch
            {
                await SnackbarHelper.ShowAsync(Resource.SettingsPage_DisableVantage_Error_Title, Resource.SettingsPage_DisableVantage_Error_Message, SnackbarType.Error);
                return;
            }

            try
            {
                if (await _rgbKeyboardBacklightController.IsSupportedAsync())
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Setting light control owner and restoring preset...");

                    await _rgbKeyboardBacklightController.SetLightControlOwnerAsync(true, true);
                }
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Couldn't set light control owner or current preset.", ex);
            }

            try
            {
                var controller = IoCContainer.Resolve<SpectrumKeyboardBacklightController>();
                if (await controller.IsSupportedAsync())
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Starting Aurora if needed...");

                    var result = await controller.StartAuroraIfNeededAsync();
                    if (result)
                    {
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"Aurora started.");
                    }
                    else
                    {
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"Aurora not needed.");
                    }
                }
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Couldn't start Aurora if needed.", ex);
            }
        }
        else
        {
            try
            {
                if (await _rgbKeyboardBacklightController.IsSupportedAsync())
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Setting light control owner...");

                    await _rgbKeyboardBacklightController.SetLightControlOwnerAsync(false);
                }
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Couldn't set light control owner.", ex);
            }

            try
            {
                if (IoCContainer.TryResolve<SpectrumKeyboardBacklightController>() is { } spectrumKeyboardBacklightController)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Making sure Aurora is stopped...");

                    if (await spectrumKeyboardBacklightController.IsSupportedAsync())
                        await spectrumKeyboardBacklightController.StopAuroraIfNeededAsync();
                }
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Couldn't stop Aurora.", ex);
            }

            try
            {
                await _vantageDisabler.EnableAsync();
            }
            catch
            {
                await SnackbarHelper.ShowAsync(Resource.SettingsPage_EnableVantage_Error_Title, Resource.SettingsPage_EnableVantage_Error_Message, SnackbarType.Error);
                return;
            }
        }

        _vantageToggle.IsEnabled = true;
    }

    private async void LegionZoneToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing)
            return;

        _legionZoneToggle.IsEnabled = false;

        var state = _legionZoneToggle.IsChecked;
        if (state is null)
            return;

        if (state.Value)
        {
            try
            {
                await _legionZoneDisabler.DisableAsync();
            }
            catch
            {
                await SnackbarHelper.ShowAsync(Resource.SettingsPage_DisableLegionZone_Error_Title, Resource.SettingsPage_DisableLegionZone_Error_Message, SnackbarType.Error);
                return;
            }
        }
        else
        {
            try
            {
                await _legionZoneDisabler.EnableAsync();
            }
            catch
            {
                await SnackbarHelper.ShowAsync(Resource.SettingsPage_EnableLegionZone_Error_Title, Resource.SettingsPage_EnableLegionZone_Error_Message, SnackbarType.Error);
                return;
            }
        }

        _legionZoneToggle.IsEnabled = true;
    }

    private async void FnKeysToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing)
            return;

        _fnKeysToggle.IsEnabled = false;

        var state = _fnKeysToggle.IsChecked;
        if (state is null)
            return;

        if (state.Value)
        {
            try
            {
                await _fnKeysDisabler.DisableAsync();
            }
            catch
            {
                await SnackbarHelper.ShowAsync(Resource.SettingsPage_DisableLenovoHotkeys_Error_Title, Resource.SettingsPage_DisableLenovoHotkeys_Error_Message, SnackbarType.Error);
                return;
            }
        }
        else
        {
            try
            {
                await _fnKeysDisabler.EnableAsync();
            }
            catch
            {
                await SnackbarHelper.ShowAsync(Resource.SettingsPage_EnableLenovoHotkeys_Error_Title, Resource.SettingsPage_EnableLenovoHotkeys_Error_Message, SnackbarType.Error);
                return;
            }
        }

        _fnKeysToggle.IsEnabled = true;

        _smartKeySinglePressActionCard.Visibility = state.Value ? Visibility.Visible : Visibility.Collapsed;
        _smartKeyDoublePressActionCard.Visibility = state.Value ? Visibility.Visible : Visibility.Collapsed;
        _notificationsCard.Visibility = state.Value ? Visibility.Visible : Visibility.Collapsed;
        _excludeRefreshRatesCard.Visibility = state.Value ? Visibility.Visible : Visibility.Collapsed;
    }

    private void NotificationsCard_Click(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing)
            return;

        var window = new NotificationsSettingsWindow { Owner = Window.GetWindow(this) };
        window.ShowDialog();
    }

    private void ExcludeRefreshRates_Click(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing)
            return;

        var window = new ExcludeRefreshRatesWindow { Owner = Window.GetWindow(this) };
        window.ShowDialog();
    }

    private void SynchronizeBrightnessToAllPowerPlansToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing)
            return;

        var state = _synchronizeBrightnessToAllPowerPlansToggle.IsChecked;
        if (state is null)
            return;

        _settings.Store.SynchronizeBrightnessToAllPowerPlans = state.Value;
        _settings.SynchronizeStore();
    }

    private void BootLogo_Click(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing)
            return;

        var window = new BootLogoWindow { Owner = Window.GetWindow(this) };
        window.ShowDialog();
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing)
            return;

        if (App.Current.MainWindow is not MainWindow mainWindow)
            return;

        mainWindow.CheckForUpdates(true);
        await SnackbarHelper.ShowAsync(Resource.SettingsPage_CheckUpdates_Started_Title, type: SnackbarType.Info);
    }

    private void UpdateCheckFrequencyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshing)
            return;

        if (!_updateCheckFrequencyComboBox.TryGetSelectedItem(out UpdateCheckFrequency frequency))
            return;

        _updateCheckSettings.Store.UpdateCheckFrequency = frequency;
        _updateCheckSettings.SynchronizeStore();
        _updateChecker.UpdateMinimumTimeSpanForRefresh();
    }

    private async void GodModeFnQSwitchableToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing)
            return;

        var state = _godModeFnQSwitchableToggle.IsChecked;
        if (state is null)
            return;

        _godModeFnQSwitchableToggle.IsEnabled = false;

        await WMI.LenovoOtherMethod.SetFeatureValueAsync(CapabilityID.GodModeFnQSwitchable, state.Value ? 1 : 0);

        _godModeFnQSwitchableToggle.IsEnabled = true;
    }

    private async void PowerModeMappingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshing)
            return;

        if (!_powerModeMappingComboBox.TryGetSelectedItem(out PowerModeMappingMode powerModeMappingMode))
            return;

        _settings.Store.PowerModeMappingMode = powerModeMappingMode;
        _settings.SynchronizeStore();

        var isPowerModeFeatureSupported = await _powerModeFeature.IsSupportedAsync();
        _powerModesCard.Visibility = _settings.Store.PowerModeMappingMode == PowerModeMappingMode.WindowsPowerMode && isPowerModeFeatureSupported ? Visibility.Visible : Visibility.Collapsed;
        _windowsPowerPlansCard.Visibility = _settings.Store.PowerModeMappingMode == PowerModeMappingMode.WindowsPowerPlan && isPowerModeFeatureSupported ? Visibility.Visible : Visibility.Collapsed;
        _windowsPowerPlansControlPanelCard.Visibility = _settings.Store.PowerModeMappingMode == PowerModeMappingMode.WindowsPowerPlan && isPowerModeFeatureSupported ? Visibility.Visible : Visibility.Collapsed;
    }

    private void WindowsPowerPlans_Click(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing)
            return;

        var window = new WindowsPowerPlansWindow { Owner = Window.GetWindow(this) };
        window.ShowDialog();
    }

    private void PowerModes_Click(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing)
            return;

        var window = new WindowsPowerModesWindow { Owner = Window.GetWindow(this) };
        window.ShowDialog();
    }

    private void WindowsPowerPlansControlPanel_Click(object sender, RoutedEventArgs e)
    {
        Process.Start("control", "/name Microsoft.PowerOptions");
    }

    private void OnBatterySinceResetToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing)
            return;

        var state = _onBatterySinceResetToggle.IsChecked;
        if (state is null)
            return;

        _settings.Store.ResetBatteryOnSinceTimerOnReboot = state.Value;
        _settings.SynchronizeStore();
    }

    private async void HWiNFOIntegrationToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing)
            return;

        _integrationsSettings.Store.HWiNFO = _hwinfoIntegrationToggle.IsChecked ?? false;
        _integrationsSettings.SynchronizeStore();

        await _hwinfoIntegration.StartStopIfNeededAsync();
    }

    private async void CLIInterfaceToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing)
            return;

        _integrationsSettings.Store.CLI = _cliInterfaceToggle.IsChecked ?? false;
        _integrationsSettings.SynchronizeStore();

        await _ipcServer.StartStopIfNeededAsync();
    }

    private void CLIPathToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing)
            return;

        SystemPath.SetCLI(_cliPathToggle.IsChecked ?? false);
    }
}
