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
/// </summary>
public sealed class HardwareMonitorService : IDisposable
{
    private static HardwareMonitorService? _instance;
    private static readonly object _lock = new();

    public static HardwareMonitorService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new HardwareMonitorService();
                }
            }
            return _instance;
        }
    }

    private readonly Computer _computer;
    private readonly object _readingsLock = new();
    private SensorReadings _cachedReadings = new();
    private bool _isOpen;
    private CancellationTokenSource? _updateCts;
    private Task? _updateTask;
    private volatile bool _isUpdating;

    public class SensorReadings
    {
        // CPU
        public float? CpuPackagePower { get; set; }
        public float? CpuTemperature { get; set; }
        
        // GPU
        public float? GpuPower { get; set; }
        public float? GpuTemperature { get; set; }
        public float? GpuMemoryTotal { get; set; } // VRAM in MB
        public float? GpuMemoryUsed { get; set; }
        public float? GpuHotSpotTemperature { get; set; }
        
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
                GpuPower = GpuPower,
                GpuTemperature = GpuTemperature,
                GpuMemoryTotal = GpuMemoryTotal,
                GpuMemoryUsed = GpuMemoryUsed,
                GpuHotSpotTemperature = GpuHotSpotTemperature,
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
            IsGpuEnabled = true,
            IsStorageEnabled = true,
            IsMotherboardEnabled = true,
            IsMemoryEnabled = true
        };
    }

    public void Open()
    {
        if (!_isOpen)
        {
            try
            {
                _computer.Open();
                _isOpen = true;
                StartBackgroundUpdates();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open hardware monitor: {ex.Message}");
            }
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
            catch { }
        }
    }
    
    private void StartBackgroundUpdates()
    {
        if (_updateTask != null) return;
        
        _updateCts = new CancellationTokenSource();
        var token = _updateCts.Token;
        
        _updateTask = Task.Run(async () =>
        {
            // Initial update
            await UpdateReadingsAsync();
            
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1500, token); // Update every 1.5 seconds
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
        try
        {
            _updateTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch { }
        
        _updateCts?.Dispose();
        _updateCts = null;
        _updateTask = null;
    }
    
    private Task UpdateReadingsAsync()
    {
        if (_isUpdating || !_isOpen) return Task.CompletedTask;
        
        _isUpdating = true;
        
        try
        {
            var readings = new SensorReadings();

            foreach (var hardware in _computer.Hardware)
            {
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
                        ProcessStorage(hardware, readings);
                        break;

                    case HardwareType.Motherboard:
                        ProcessMotherboard(hardware, readings);
                        break;
                }
            }
            
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
                                                 || sensor.Name.Contains("Core (Tctl/Tdie)", StringComparison.OrdinalIgnoreCase):
                    readings.CpuTemperature = sensor.Value;
                    break;
            }
        }

        // Also check sub-hardware
        foreach (var subHardware in hardware.SubHardware)
        {
            subHardware.Update();
            ProcessCpu(subHardware, readings);
        }
    }

    private static void ProcessGpu(IHardware hardware, SensorReadings readings)
    {
        foreach (var sensor in hardware.Sensors)
        {
            switch (sensor.SensorType)
            {
                case SensorType.Power when sensor.Name.Contains("GPU", StringComparison.OrdinalIgnoreCase)
                                           || sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase)
                                           || sensor.Name.Contains("Total", StringComparison.OrdinalIgnoreCase):
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

                case SensorType.SmallData when sensor.Name.Contains("Memory Used", StringComparison.OrdinalIgnoreCase):
                    readings.GpuMemoryUsed = sensor.Value;
                    break;
                    
                // Some GPUs report memory as Data instead of SmallData
                case SensorType.Data when sensor.Name.Contains("Memory Total", StringComparison.OrdinalIgnoreCase):
                    readings.GpuMemoryTotal = sensor.Value * 1024; // Convert GB to MB
                    break;
            }
        }

        // Check sub-hardware
        foreach (var subHardware in hardware.SubHardware)
        {
            subHardware.Update();
            ProcessGpu(subHardware, readings);
        }
    }

    private static void ProcessStorage(IHardware hardware, SensorReadings readings)
    {
        float? temp = null;

        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.SensorType == SensorType.Temperature)
            {
                temp = sensor.Value;
                break;
            }
        }

        if (temp.HasValue)
        {
            readings.DiskTemperatures.Add((hardware.Name, temp));
        }
    }

    private static void ProcessMotherboard(IHardware hardware, SensorReadings readings)
    {
        foreach (var subHardware in hardware.SubHardware)
        {
            subHardware.Update();

            foreach (var sensor in subHardware.Sensors)
            {
                if (sensor.SensorType == SensorType.Temperature)
                {
                    if (sensor.Name.Contains("PCH", StringComparison.OrdinalIgnoreCase))
                    {
                        readings.PchTemperature = sensor.Value;
                    }
                    else if (readings.MotherboardTemperature == null)
                    {
                        readings.MotherboardTemperature = sensor.Value;
                    }
                }
            }
        }
    }

    public void Dispose()
    {
        Close();
        _instance = null;
    }
}
