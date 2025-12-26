using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibreHardwareMonitor.Hardware;

namespace LenovoLegionToolkit.WPF.Utils;

/// <summary>
/// Service for accessing hardware sensors via LibreHardwareMonitor.
/// Uses background thread updates with caching for smooth UI performance.
/// Supports pausing updates when app is minimized to save resources.
/// </summary>
public sealed class HardwareMonitorService : IDisposable
{
    private static readonly Lazy<HardwareMonitorService> _lazyInstance = new(() => new HardwareMonitorService());

    public static HardwareMonitorService Instance => _lazyInstance.Value;

    private readonly Computer _computer;
    private readonly object _readingsLock = new();
    private SensorReadings _cachedReadings = new();
    private bool _isOpen;
    private CancellationTokenSource? _updateCts;
    private Task? _updateTask;
    private volatile bool _isUpdating;
    private volatile bool _isPaused;
    private volatile int _updateIntervalMs = 1500; // Default 1.5 seconds
    private DateTime _lastFullUpdate = DateTime.MinValue;

    // Update modes for power efficiency
    public enum UpdateMode
    {
        Normal,     // Full updates every 1.5 seconds
        Reduced,    // Reduced updates every 5 seconds (when minimized)
        Paused      // No updates (when in tray for extended time)
    }

    public class SensorReadings
    {
        // CPU
        public float? CpuPackagePower { get; set; }
        public float? CpuTemperature { get; set; }
        public float? CpuUtilization { get; set; }
        public float? CpuCoreClock { get; set; } // in GHz
        public float? CpuFanSpeed { get; set; } // RPM
        
        // GPU
        public float? GpuPower { get; set; }
        public float? GpuTemperature { get; set; }
        public float? GpuMemoryTotal { get; set; } // VRAM in MB
        public float? GpuMemoryUsed { get; set; }
        public float? GpuHotSpotTemperature { get; set; }
        public float? GpuUtilization { get; set; }
        public float? GpuCoreClock { get; set; } // in MHz
        public float? GpuFanSpeed { get; set; } // RPM
        
        // Memory
        public float? MemoryUtilization { get; set; }
        public float? MemoryUsed { get; set; } // in MB
        public float? MemoryTotal { get; set; } // in MB
        
        // Storage
        public List<(string Name, float? Temperature)> DiskTemperatures { get; set; } = new();
        
        // Motherboard
        public float? PchTemperature { get; set; }
        public float? MotherboardTemperature { get; set; }
        
        public SensorReadings Clone()
        {
            return new SensorReadings
            {
                CpuPackagePower = CpuPackagePower,
                CpuTemperature = CpuTemperature,
                CpuUtilization = CpuUtilization,
                CpuCoreClock = CpuCoreClock,
                CpuFanSpeed = CpuFanSpeed,
                GpuPower = GpuPower,
                GpuTemperature = GpuTemperature,
                GpuMemoryTotal = GpuMemoryTotal,
                GpuMemoryUsed = GpuMemoryUsed,
                GpuHotSpotTemperature = GpuHotSpotTemperature,
                GpuUtilization = GpuUtilization,
                GpuCoreClock = GpuCoreClock,
                GpuFanSpeed = GpuFanSpeed,
                MemoryUtilization = MemoryUtilization,
                MemoryUsed = MemoryUsed,
                MemoryTotal = MemoryTotal,
                PchTemperature = PchTemperature,
                MotherboardTemperature = MotherboardTemperature,
                DiskTemperatures = new List<(string, float?)>(DiskTemperatures)
            };
        }
    }


    private HardwareMonitorService()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = false, // We'll enable this later
            IsStorageEnabled = false, // We'll enable this later
            IsMotherboardEnabled = false, // We'll enable this later
            IsMemoryEnabled = true // Enable RAM immediately
        };
    }

    public void Open()
    {
        if (!_isOpen)
        {
            _isOpen = true;
            
            // Fast path: Initialize CPU/RAM immediately on background thread
            // This usually takes < 50ms
            Task.Run(async () =>
            {
                try
                {
                    _computer.Open();
                    
                    // Fire first update immediately for CPU/RAM
                    await QuickUpdateAsync();
                    
                    // Start the full update loop
                    StartBackgroundUpdates();
                    
                    // Lazily enable heavy components in parallel
                    _ = Task.Run(() => EnableHeavyComponentsAsync());
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to open hardware monitor: {ex.Message}");
                    _isOpen = false;
                }
            });
        }
    }
    
    private void EnableHeavyComponentsAsync()
    {
        try
        {
            // Enable GPU, Storage, Motherboard one by one to avoid massive lag spike
            _computer.IsGpuEnabled = true;

            // Sleep briefly to let UI breathe
            Thread.Sleep(100);
            
            _computer.IsStorageEnabled = true;
            _computer.IsMotherboardEnabled = true;
        }
        catch (Exception ex)
        {
             System.Diagnostics.Debug.WriteLine($"Failed to enable heavy components: {ex.Message}");
        }
    }

    public void Close()
    {
        StopBackgroundUpdates();
        
        if (_isOpen)
        {
            try
            {
                _computer.Close();
                _isOpen = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error closing hardware monitor: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Sets the update mode for power efficiency.
    /// Call with Reduced when app is minimized, Paused when in tray.
    /// </summary>
    public void SetUpdateMode(UpdateMode mode)
    {
        switch (mode)
        {
            case UpdateMode.Normal:
                _isPaused = false;
                _updateIntervalMs = 1500;
                break;
            case UpdateMode.Reduced:
                _isPaused = false;
                _updateIntervalMs = 5000; // 5 seconds when minimized
                break;
            case UpdateMode.Paused:
                _isPaused = true;
                break;
        }
        
        System.Diagnostics.Debug.WriteLine($"HardwareMonitor update mode: {mode}");
    }
    
    private void StartBackgroundUpdates()
    {
        if (_updateTask != null) return;
        
        _updateCts = new CancellationTokenSource();
        var token = _updateCts.Token;
        
        _updateTask = Task.Run(async () =>
        {
            // Do quick initial update (CPU/Memory only for fast startup)
            await QuickUpdateAsync();
            
            // Start full updates in background
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_updateIntervalMs, token);
                    
                    // Skip update if paused
                    if (_isPaused)
                        continue;
                        
                    await UpdateReadingsAsync();
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Background update error: {ex.Message}");
                    await Task.Delay(2000, token);
                }
            }
        }, token);
    }
    
    private void StopBackgroundUpdates()
    {
        _updateCts?.Cancel();

        var task = _updateTask;
        if (task != null)
        {
            // Allow background loop to finish without blocking the caller; observe faults.
            _ = task.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
            _ = Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(2)));
        }
        
        _updateCts?.Dispose();
        _updateCts = null;
        _updateTask = null;
    }

    /// <summary>
    /// Quick initial update - only CPU and Memory for fast startup.
    /// GPU is deferred as it takes longer to initialize.
    /// </summary>
    private Task QuickUpdateAsync()
    {
        if (!_isOpen) return Task.CompletedTask;
        
        try
        {
            var readings = new SensorReadings();

            foreach (var hardware in _computer.Hardware)
            {
                // Only update CPU and Memory for quick startup
                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    hardware.Update();
                    ProcessCpu(hardware, readings);
                }
                else if (hardware.HardwareType == HardwareType.Memory)
                {
                    hardware.Update();
                    ProcessMemory(hardware, readings);
                }
            }
            
            lock (_readingsLock)
            {
                _cachedReadings = readings;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Quick update error: {ex.Message}");
        }
        
        return Task.CompletedTask;
    }
    
    private Task UpdateReadingsAsync()
    {
        if (_isUpdating || !_isOpen) return Task.CompletedTask;
        
        _isUpdating = true;
        
        try
        {
            var readings = new SensorReadings();
            var now = DateTime.UtcNow;
            var doFullUpdate = true;

            foreach (var hardware in _computer.Hardware)
            {
                // Just calling Update() is enough now; heavy components are enabled lazily
                hardware.Update();

                switch (hardware.HardwareType)
                {
                    case HardwareType.Cpu:
                        ProcessCpu(hardware, readings);
                        break;

                    case HardwareType.GpuNvidia:
                    case HardwareType.GpuAmd:
                    case HardwareType.GpuIntel:
                        // Skip integrated GPU if discrete already found
                        if (hardware.HardwareType == HardwareType.GpuIntel && readings.GpuMemoryTotal.HasValue)
                            continue;
                        ProcessGpu(hardware, readings);
                        break;

                    case HardwareType.Storage:
                        if (doFullUpdate)
                            ProcessStorage(hardware, readings);
                        break;

                    case HardwareType.Motherboard:
                        if (doFullUpdate)
                            ProcessMotherboard(hardware, readings);
                        break;

                    case HardwareType.Memory:
                        ProcessMemory(hardware, readings);
                        break;
                }
            }
            
            if (doFullUpdate)
                _lastFullUpdate = now;
            
            // Thread-safe update of cached readings
            lock (_readingsLock)
            {
                _cachedReadings = readings;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error reading hardware sensors: {ex.Message}");
        }
        finally
        {
            _isUpdating = false;
        }
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns cached sensor readings immediately (non-blocking).
    /// Readings are updated in background every 1.5 seconds.
    /// </summary>
    public SensorReadings GetReadings()
    {
        if (!_isOpen)
        {
            Open();
        }

        lock (_readingsLock)
        {
            return _cachedReadings.Clone();
        }
    }

    private static void ProcessCpu(IHardware hardware, SensorReadings readings)
    {
        foreach (var sensor in hardware.Sensors)
        {
            switch (sensor.SensorType)
            {
                case SensorType.Power when sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase):
                    readings.CpuPackagePower = sensor.Value;
                    break;
                case SensorType.Temperature when sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) 
                                               || sensor.Name.Contains("Core (Tctl", StringComparison.OrdinalIgnoreCase):
                    readings.CpuTemperature = sensor.Value;
                    break;
                case SensorType.Load when sensor.Name.Contains("Total", StringComparison.OrdinalIgnoreCase):
                    readings.CpuUtilization = sensor.Value;
                    break;
                case SensorType.Clock when sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) 
                                        && !sensor.Name.Contains("Bus", StringComparison.OrdinalIgnoreCase):
                    // Store max core clock in GHz
                    var clockGHz = (sensor.Value ?? 0) / 1000f;
                    if (!readings.CpuCoreClock.HasValue || clockGHz > readings.CpuCoreClock)
                        readings.CpuCoreClock = clockGHz;
                    break;
                case SensorType.Fan:
                    readings.CpuFanSpeed = sensor.Value;
                    break;
            }
        }
    }

    
    private static void ProcessGpu(IHardware hardware, SensorReadings readings)
    {
        // Skip integrated GPUs when looking for discrete GPU data
        if (hardware.HardwareType == HardwareType.GpuIntel)
        {
            // Only use Intel GPU if no other GPU data found
            if (readings.GpuTemperature.HasValue)
                return;
        }

        foreach (var sensor in hardware.Sensors)
        {
            switch (sensor.SensorType)
            {
                case SensorType.Power when sensor.Name.Contains("GPU", StringComparison.OrdinalIgnoreCase) 
                                        || sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase):
                    readings.GpuPower = sensor.Value;
                    break;
                case SensorType.Temperature when sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)
                                               || sensor.Name.Equals("GPU", StringComparison.OrdinalIgnoreCase):
                    readings.GpuTemperature = sensor.Value;
                    break;
                case SensorType.Temperature when sensor.Name.Contains("Hot Spot", StringComparison.OrdinalIgnoreCase):
                    readings.GpuHotSpotTemperature = sensor.Value;
                    break;
                case SensorType.SmallData when sensor.Name.Contains("Memory Total", StringComparison.OrdinalIgnoreCase)
                                            || sensor.Name.Contains("GPU Memory Total", StringComparison.OrdinalIgnoreCase):
                    readings.GpuMemoryTotal = sensor.Value;
                    break;
                case SensorType.SmallData when sensor.Name.Contains("Memory Used", StringComparison.OrdinalIgnoreCase)
                                            || sensor.Name.Contains("GPU Memory Used", StringComparison.OrdinalIgnoreCase):
                    readings.GpuMemoryUsed = sensor.Value;
                    break;
                case SensorType.Load when sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)
                                       || sensor.Name.Equals("GPU", StringComparison.OrdinalIgnoreCase):
                    readings.GpuUtilization = sensor.Value;
                    break;
                case SensorType.Clock when sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase):
                    readings.GpuCoreClock = sensor.Value;
                    break;
                case SensorType.Fan:
                    readings.GpuFanSpeed = sensor.Value;
                    break;
            }
        }
    }

    
    private static void ProcessStorage(IHardware hardware, SensorReadings readings)
    {
        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.SensorType == SensorType.Temperature)
            {
                readings.DiskTemperatures.Add((hardware.Name, sensor.Value));
                break; // Only first temp per drive
            }
        }
    }

    private static void ProcessMotherboard(IHardware hardware, SensorReadings readings)
    {
        // Check subhardware for SuperIO chip readings
        foreach (var subHardware in hardware.SubHardware)
        {
            subHardware.Update();
            foreach (var sensor in subHardware.Sensors)
            {
                switch (sensor.SensorType)
                {
                    case SensorType.Temperature:
                        var name = sensor.Name.ToLowerInvariant();
                        if (name.Contains("pch") || name.Contains("chipset"))
                        {
                            readings.PchTemperature = sensor.Value;
                        }
                        else if (name.Contains("motherboard") || name.Contains("system") || name.Contains("board"))
                        {
                            readings.MotherboardTemperature = sensor.Value;
                        }
                        break;
                }
            }
        }
        
        // Also check main hardware sensors
        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.SensorType == SensorType.Temperature)
            {
                var name = sensor.Name.ToLowerInvariant();
                if (name.Contains("pch") || name.Contains("chipset"))
                {
                    readings.PchTemperature = sensor.Value;
                }
                else if (!readings.MotherboardTemperature.HasValue && 
                        (name.Contains("motherboard") || name.Contains("system") || name.Contains("board")))
                {
                    readings.MotherboardTemperature = sensor.Value;
                }
            }
        }
    }

    private static void ProcessMemory(IHardware hardware, SensorReadings readings)
    {
        foreach (var sensor in hardware.Sensors)
        {
            switch (sensor.SensorType)
            {
                case SensorType.Load when sensor.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase):
                    readings.MemoryUtilization = sensor.Value;
                    break;
                case SensorType.Data when sensor.Name.Contains("Used", StringComparison.OrdinalIgnoreCase):
                    // Convert GB to MB
                    readings.MemoryUsed = (sensor.Value ?? 0) * 1024;
                    break;
                case SensorType.Data when sensor.Name.Contains("Available", StringComparison.OrdinalIgnoreCase):
                    // We'll calculate total from used + available if needed
                    break;
            }
        }
    }

    public void Dispose()
    {
        Close();
    }
}
