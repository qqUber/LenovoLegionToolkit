using System;
using System.Management;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Humanizer;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers.Sensors;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Settings;
using LenovoLegionToolkit.WPF.Utils;

using MenuItem = LenovoLegionToolkit.WPF.Compat.MenuItem;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

public partial class SensorsControl
{
    private readonly ISensorsController _controller = IoCContainer.Resolve<ISensorsController>();
    private readonly ApplicationSettings _applicationSettings = IoCContainer.Resolve<ApplicationSettings>();
    private readonly DashboardSettings _dashboardSettings = IoCContainer.Resolve<DashboardSettings>();
    private readonly HardwareMonitorService _hwMonitor = HardwareMonitorService.Instance;

    private CancellationTokenSource? _cts;
    private Task? _refreshTask;
    private bool _isSupported;
    private bool _supportChecked;
    
    // Cache temperature unit to avoid repeated lookups
    private TemperatureUnit _cachedTempUnit;
    private SensorsLayout _currentLayout;
    
    // Cache for system info to avoid blocking UI
    private string _cachedDiskUsage = "-";
    private string _cachedMemUtilization = "-";
    private double _cachedMemUtilizationPercent;
    private string _cachedBatteryState = "-";
    private string _cachedBatteryLevel = "-";
    private readonly object _cacheLock = new();

    public SensorsControl()
    {
        InitializeComponent();
        InitializeContextMenu();
        
        _cachedTempUnit = _applicationSettings.Store.TemperatureUnit;
        _currentLayout = _dashboardSettings.Store.SensorsLayout;
        ApplyLayout();

        IsVisibleChanged += SensorsControl_IsVisibleChanged;
    }

    private void ApplyLayout()
    {
        if (_currentLayout == SensorsLayout.Compact)
        {
            _cardsLayout.Visibility = Visibility.Collapsed;
            _compactLayout.Visibility = Visibility.Visible;
        }
        else
        {
            _cardsLayout.Visibility = Visibility.Visible;
            _compactLayout.Visibility = Visibility.Collapsed;
        }
    }

    private void InitializeContextMenu()
    {
        ContextMenu = new ContextMenu();
        ContextMenu.Items.Add(new MenuItem { Header = Resource.SensorsControl_RefreshInterval, IsEnabled = false });

        foreach (var interval in new[] { 1, 2, 3, 5 })
        {
            var item = new MenuItem
            {
                SymbolIcon = _dashboardSettings.Store.SensorsRefreshIntervalSeconds == interval ? SymbolRegular.Checkmark24 : SymbolRegular.Empty,
                Header = TimeSpan.FromSeconds(interval).Humanize(culture: Resource.Culture)
            };
            item.Click += (_, _) =>
            {
                _dashboardSettings.Store.SensorsRefreshIntervalSeconds = interval;
                _dashboardSettings.SynchronizeStore();
                InitializeContextMenu();
            };
            ContextMenu.Items.Add(item);
        }
    }

    private async void SensorsControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            _cachedTempUnit = _applicationSettings.Store.TemperatureUnit;
            
            // Check if layout changed
            if (_currentLayout != _dashboardSettings.Store.SensorsLayout)
            {
                _currentLayout = _dashboardSettings.Store.SensorsLayout;
                ApplyLayout();
            }
            
            Refresh();
            return;
        }

        if (_cts is not null)
            await _cts.CancelAsync();

        _cts = null;

        if (_refreshTask is not null)
            await _refreshTask;

        _refreshTask = null;

        // Use BeginInvoke for non-blocking UI update
        _ = Dispatcher.BeginInvoke(() => UpdateValues(SensorsData.Empty));
    }

    private void Refresh()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        var token = _cts.Token;

        _refreshTask = Task.Run(async () =>
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Sensors refresh started...");

            // Cache support check result
            if (!_supportChecked)
            {
                _isSupported = await _controller.IsSupportedAsync().ConfigureAwait(false);
                _supportChecked = true;
            }

            if (!_isSupported)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Sensors not supported.");

                _ = Dispatcher.BeginInvoke(() => Visibility = Visibility.Collapsed);
                return;
            }

            await _controller.PrepareAsync().ConfigureAwait(false);

            var refreshInterval = TimeSpan.FromSeconds(_dashboardSettings.Store.SensorsRefreshIntervalSeconds);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var data = await _controller.GetDataAsync().ConfigureAwait(false);
                    
                    // Update system info cache in background (these calls can be slow)
                    UpdateSystemInfoCache();
                    
                    // Use BeginInvoke for non-blocking UI updates
                    _ = Dispatcher.BeginInvoke(() => UpdateValues(data));
                    
                    await Task.Delay(refreshInterval, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Sensors refresh failed.", ex);

                    _ = Dispatcher.BeginInvoke(() => UpdateValues(SensorsData.Empty));
                    
                    // Add small delay on error to prevent tight loop
                    await Task.Delay(1000, token).ConfigureAwait(false);
                }
            }

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Sensors refresh stopped.");
        }, token);
    }
    
    /// <summary>
    /// Updates system info cache on background thread to avoid UI blocking.
    /// </summary>
    private void UpdateSystemInfoCache()
    {
        try
        {
            // Disk usage
            var diskUsage = GetDiskUsage();
            
            // Memory info
            var memInfo = GetMemoryInfo();
            var memUtil = $"{memInfo.usedPercent:0}%";
            
            // Battery info
            var batteryInfo = Battery.GetBatteryInformation();
            var batteryState = batteryInfo.IsCharging ? "Plugged" : "Unplugged";
            var batteryLevel = $"{batteryInfo.BatteryPercentage}%";
            
            lock (_cacheLock)
            {
                _cachedDiskUsage = diskUsage;
                _cachedMemUtilization = memUtil;
                _cachedMemUtilizationPercent = memInfo.usedPercent;
                _cachedBatteryState = batteryState;
                _cachedBatteryLevel = batteryLevel;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating system info cache: {ex.Message}");
        }
    }

    private void UpdateValues(SensorsData data)
    {
        if (_currentLayout == SensorsLayout.Compact)
        {
            UpdateCompactValues(data);
        }
        else
        {
            UpdateCardsValues(data);
        }
    }

    private void UpdateCardsValues(SensorsData data)
    {
        UpdateValue(_cpuUtilizationBar, _cpuUtilizationLabel, data.CPU.MaxUtilization, data.CPU.Utilization,
            $"{data.CPU.Utilization}%");
        UpdateValue(_cpuCoreClockBar, _cpuCoreClockLabel, data.CPU.MaxCoreClock, data.CPU.CoreClock,
            $"{data.CPU.CoreClock / 1000.0:0.0} {Resource.GHz}", $"{data.CPU.MaxCoreClock / 1000.0:0.0} {Resource.GHz}");
        UpdateValue(_cpuTemperatureBar, _cpuTemperatureLabel, data.CPU.MaxTemperature, data.CPU.Temperature,
            GetTemperatureText(data.CPU.Temperature), GetTemperatureText(data.CPU.MaxTemperature));
        UpdateValue(_cpuFanSpeedBar, _cpuFanSpeedLabel, data.CPU.MaxFanSpeed, data.CPU.FanSpeed,
            $"{data.CPU.FanSpeed} {Resource.RPM}", $"{data.CPU.MaxFanSpeed} {Resource.RPM}");

        UpdateValue(_gpuUtilizationBar, _gpuUtilizationLabel, data.GPU.MaxUtilization, data.GPU.Utilization,
            $"{data.GPU.Utilization} %");
        UpdateValue(_gpuCoreClockBar, _gpuCoreClockLabel, data.GPU.MaxCoreClock, data.GPU.CoreClock,
            $"{data.GPU.CoreClock} {Resource.MHz}", $"{data.GPU.MaxCoreClock} {Resource.MHz}");
        UpdateValue(_gpuMemoryClockBar, _gpuMemoryClockLabel, data.GPU.MaxMemoryClock, data.GPU.MemoryClock,
            $"{data.GPU.MemoryClock} {Resource.MHz}", $"{data.GPU.MaxMemoryClock} {Resource.MHz}");
        UpdateValue(_gpuTemperatureBar, _gpuTemperatureLabel, data.GPU.MaxTemperature, data.GPU.Temperature,
            GetTemperatureText(data.GPU.Temperature), GetTemperatureText(data.GPU.MaxTemperature));
        UpdateValue(_gpuFanSpeedBar, _gpuFanSpeedLabel, data.GPU.MaxFanSpeed, data.GPU.FanSpeed,
            $"{data.GPU.FanSpeed} {Resource.RPM}", $"{data.GPU.MaxFanSpeed} {Resource.RPM}");
    }

    private void UpdateCompactValues(SensorsData data)
    {
        // Get enhanced sensor data from LibreHardwareMonitor
        HardwareMonitorService.SensorReadings? hwReadings = null;
        try
        {
            hwReadings = _hwMonitor.GetReadings();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting hardware readings: {ex.Message}");
        }

        // CPU values
        _cpuUtilizationCompact.Text = data.CPU.Utilization >= 0 ? $"{data.CPU.Utilization}%" : "-";
        _cpuCoreClockCompact.Text = data.CPU.CoreClock >= 0 ? $"{data.CPU.CoreClock / 1000.0:0.0} {Resource.GHz}" : "-";
        _cpuFanCompact.Text = data.CPU.FanSpeed >= 0 ? $"{data.CPU.FanSpeed} {Resource.RPM}" : "-";
        _cpuTempCompact.Text = data.CPU.Temperature >= 0 ? GetTemperatureText(data.CPU.Temperature) : "-";
        
        // CPU Power from LibreHardwareMonitor
        _cpuPowerCompact.Text = hwReadings?.CpuPackagePower.HasValue == true 
            ? $"{hwReadings.CpuPackagePower.Value:0.0} W" 
            : "-";

        // GPU values
        _gpuUtilizationCompact.Text = data.GPU.Utilization >= 0 ? $"{data.GPU.Utilization}%" : "-";
        _gpuCoreClockCompact.Text = data.GPU.CoreClock >= 0 ? $"{data.GPU.CoreClock} {Resource.MHz}" : "-";
        _gpuFanCompact.Text = data.GPU.FanSpeed >= 0 ? $"{data.GPU.FanSpeed} {Resource.RPM}" : "-";
        _gpuCoreTempCompact.Text = data.GPU.Temperature >= 0 ? GetTemperatureText(data.GPU.Temperature) : "-";
        
        // GPU VRAM from LibreHardwareMonitor
        if (hwReadings?.GpuMemoryTotal.HasValue == true)
        {
            var vramMB = hwReadings.GpuMemoryTotal.Value;
            _gpuVramCompact.Text = vramMB >= 1024 ? $"{vramMB / 1024:0.0} GB" : $"{vramMB:0} MB";
        }
        else
        {
            _gpuVramCompact.Text = "-";
        }

        // Motherboard - use CPU fan speed as system fan
        _moboFanCompact.Text = data.CPU.FanSpeed >= 0 ? $"{data.CPU.FanSpeed} {Resource.RPM}" : "-";
        
        // PCH temp from LibreHardwareMonitor
        _moboPchTempCompact.Text = hwReadings?.PchTemperature.HasValue == true 
            ? GetTemperatureText(hwReadings.PchTemperature.Value) 
            : "-";

        // Memory utilization from cached values (updated on background thread)
        string memUtil;
        double memPercent;
        lock (_cacheLock)
        {
            memUtil = _cachedMemUtilization;
            memPercent = _cachedMemUtilizationPercent;
        }
        _memUtilizationCompact.Text = memUtil;
        _memUtilizationBarCompact.Value = memPercent;
        _memTempCompact.Text = "-"; // Memory temp not typically available

        // Battery info from cached values
        string batteryState, batteryLevel;
        lock (_cacheLock)
        {
            batteryState = _cachedBatteryState;
            batteryLevel = _cachedBatteryLevel;
        }
        _batteryStateCompact.Text = batteryState;
        _batteryLevelCompact.Text = batteryLevel;

        // Disk info - use cached disk usage, temps from LibreHardwareMonitor (already cached)
        string diskUsage;
        lock (_cacheLock)
        {
            diskUsage = _cachedDiskUsage;
        }
        _diskUsedCompact.Text = diskUsage;

        if (hwReadings is not null && hwReadings.DiskTemperatures.Count > 0)
        {
            var disk1Temp = hwReadings.DiskTemperatures[0].Temperature;
            _disk1TempCompact.Text = disk1Temp.HasValue 
                ? GetTemperatureText(disk1Temp.Value) 
                : "-";
                
            if (hwReadings.DiskTemperatures.Count > 1)
            {
                var disk2Temp = hwReadings.DiskTemperatures[1].Temperature;
                _disk2TempCompact.Text = disk2Temp.HasValue
                    ? GetTemperatureText(disk2Temp.Value) 
                    : "N/A";
            }
            else
            {
                _disk2TempCompact.Text = "N/A";
            }
        }
        else
        {
            _disk1TempCompact.Text = "-";
            _disk2TempCompact.Text = "N/A";
        }
    }

    private static string GetDiskUsage()
    {
        try
        {
            var drives = System.IO.DriveInfo.GetDrives();
            long totalUsed = 0;
            long totalSize = 0;
            
            foreach (var drive in drives)
            {
                if (drive.IsReady && drive.DriveType == System.IO.DriveType.Fixed)
                {
                    totalUsed += drive.TotalSize - drive.AvailableFreeSpace;
                    totalSize += drive.TotalSize;
                }
            }
            
            if (totalSize > 0)
            {
                var usedGB = totalUsed / (1024.0 * 1024.0 * 1024.0);
                var totalGB = totalSize / (1024.0 * 1024.0 * 1024.0);
                return $"{usedGB:0}/{totalGB:0} GB";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting disk usage: {ex.Message}");
        }
        
        return "-";
    }

    private static string GetGpuVramSize()
    {
        try
        {
            // Try to get dedicated GPU (NVIDIA/AMD) VRAM first
            using var searcher = new ManagementObjectSearcher("SELECT AdapterRAM, Name FROM Win32_VideoController");
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString() ?? "";
                var adapterRam = obj["AdapterRAM"];
                
                // Skip integrated GPUs (Intel, AMD APU)
                if (name.Contains("Intel", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Radeon(TM) Graphics", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Vega", StringComparison.OrdinalIgnoreCase))
                    continue;
                
                if (adapterRam != null)
                {
                    var ramBytes = Convert.ToUInt64(adapterRam);
                    // AdapterRAM is capped at 4GB in WMI, check for dedicated GPU with higher VRAM
                    if (ramBytes > 0)
                    {
                        // For GPUs with more than 4GB, WMI returns 4GB (0xFFFFFFFF overflow)
                        // Try to get from registry or assume common sizes
                        var ramMB = ramBytes / (1024 * 1024);
                        if (ramMB >= 4096)
                        {
                            // Check registry for actual VRAM size
                            var actualVram = GetVramFromRegistry(name);
                            if (actualVram > 0)
                            {
                                return $"{actualVram} MB";
                            }
                        }
                        return $"{ramMB} MB";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting GPU VRAM size: {ex.Message}");
        }
        
        return "-";
    }

    private static long GetVramFromRegistry(string gpuName)
    {
        try
        {
            // Try NVIDIA path
            if (gpuName.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
                gpuName.Contains("GeForce", StringComparison.OrdinalIgnoreCase) ||
                gpuName.Contains("RTX", StringComparison.OrdinalIgnoreCase) ||
                gpuName.Contains("GTX", StringComparison.OrdinalIgnoreCase))
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0001");
                if (key != null)
                {
                    var memSize = key.GetValue("HardwareInformation.qwMemorySize");
                    if (memSize != null)
                    {
                        var bytes = Convert.ToUInt64(memSize);
                        return (long)(bytes / (1024 * 1024));
                    }
                }
            }
            
            // Try AMD path
            using var amdKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0001");
            if (amdKey != null)
            {
                var memSize = amdKey.GetValue("HardwareInformation.MemorySize");
                if (memSize != null)
                {
                    var bytes = Convert.ToUInt64(memSize);
                    return (long)(bytes / (1024 * 1024));
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error reading VRAM from registry: {ex.Message}");
        }
        
        return 0;
    }

    private static (double totalGB, double usedGB, double usedPercent) GetMemoryInfo()
    {
        var computerInfo = new Microsoft.VisualBasic.Devices.ComputerInfo();
        var totalBytes = computerInfo.TotalPhysicalMemory;
        var availableBytes = computerInfo.AvailablePhysicalMemory;
        var usedBytes = totalBytes - availableBytes;
        
        var totalGB = totalBytes / (1024.0 * 1024.0 * 1024.0);
        var usedGB = usedBytes / (1024.0 * 1024.0 * 1024.0);
        var usedPercent = (usedBytes / (double)totalBytes) * 100.0;
        
        return (totalGB, usedGB, usedPercent);
    }

    private string GetTemperatureText(double temperature)
    {
        if (_cachedTempUnit == TemperatureUnit.F)
        {
            temperature = temperature * 1.8 + 32;
            return $"{temperature:0} {Resource.Fahrenheit}";
        }

        return $"{temperature:0} {Resource.Celsius}";
    }

    private static void UpdateValue(RangeBase bar, ContentControl label, double max, double value, string text, string? toolTipText = null)
    {
        // Early exit if values haven't changed significantly (reduces UI thrashing)
        if (label.Tag is double oldValue && Math.Abs(oldValue - value) < 0.1 && bar.Maximum == max)
            return;

        if (max < 0 || value < 0)
        {
            bar.Minimum = 0;
            bar.Maximum = 1;
            bar.Value = 0;
            label.Content = "-";
            label.ToolTip = null;
            label.Tag = 0d;
        }
        else
        {
            bar.Minimum = 0;
            bar.Maximum = max;
            bar.Value = value;
            label.Content = text;
            label.ToolTip = toolTipText is null ? null : string.Format(Resource.SensorsControl_Maximum, toolTipText);
            label.Tag = value;
        }
    }
}
