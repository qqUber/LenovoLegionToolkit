using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Security.Principal;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System.Management;

namespace LenovoLegionToolkit.Lib.Utils;

public class DriverDiagnostics
{
    public class DiagnosticReport
    {
        public bool WMIAvailable { get; set; }
        public bool EnergyManagementInstalled { get; set; }
        public bool GamingFeatureDriverInstalled { get; set; }
        public bool VantageRunning { get; set; }
        public bool LegionZoneRunning { get; set; }
        public bool HasAdminRights { get; set; }
        public bool IsWindows11 { get; set; }
        public bool SecureBootEnabled { get; set; }
        public bool TPMEnabled { get; set; }
        public bool FanTableDataAvailable { get; set; }
        public bool FanMethodAvailable { get; set; }
        public string WindowsVersion { get; set; } = "";
        public string FanTableDataCount { get; set; } = "0";
        public List<string> MissingDrivers { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
    }

    public static async Task<DiagnosticReport> GenerateReportAsync()
    {
        var report = new DiagnosticReport();

        try
        {
            // Check WMI availability
            report.WMIAvailable = await CheckWMIAvailabilityAsync().ConfigureAwait(false);

            // Check WMI fan table data availability
            report.FanTableDataAvailable = await CheckFanTableDataAvailabilityAsync().ConfigureAwait(false);
            report.FanTableDataCount = await GetFanTableDataCountAsync().ConfigureAwait(false);

            // Check WMI fan method availability
            report.FanMethodAvailable = await CheckFanMethodAvailabilityAsync().ConfigureAwait(false);

            // Check drivers
            report.EnergyManagementInstalled = await CheckDriverAsync("Lenovo Energy Management").ConfigureAwait(false);
            report.GamingFeatureDriverInstalled = await CheckDriverAsync("Lenovo Vantage Gaming Feature Driver").ConfigureAwait(false);

            // Check running software
            report.VantageRunning = IsProcessRunning("LenovoVantage");
            report.LegionZoneRunning = IsProcessRunning("LegionZone");

            // Check admin rights
            report.HasAdminRights = HasAdministratorRights();

            // Windows 11 specific checks
            report.IsWindows11 = Windows11Compatibility.IsWindows11();
            report.SecureBootEnabled = Windows11Compatibility.IsSecureBootEnabled();
            report.TPMEnabled = Windows11Compatibility.IsTPMEnabled();
            report.WindowsVersion = Windows11Compatibility.GetWindowsVersion();

            // Generate recommendations
            GenerateRecommendations(report);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Error generating diagnostic report", ex);
        }

        return report;
    }

    private static async Task<bool> CheckWMIAvailabilityAsync()
    {
        try
        {
            return await WMI.LenovoGameZoneData.ExistsAsync().ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> CheckDriverAsync(string driverName)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_PnPSignedDriver WHERE DeviceName LIKE '%" + driverName + "%'");
            var collection = await Task.Run(() => searcher.Get()).ConfigureAwait(false);
            return collection.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> CheckFanTableDataAvailabilityAsync()
    {
        try
        {
            return await WMI.LenovoFanTableData.ReadAsync().ConfigureAwait(false) is { } data && data.Any();
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string> GetFanTableDataCountAsync()
    {
        try
        {
            var data = await WMI.LenovoFanTableData.ReadAsync().ConfigureAwait(false);
            var count = data?.Count() ?? 0;
            return count.ToString();
        }
        catch
        {
            return "Error";
        }
    }

    private static async Task<bool> CheckFanMethodAvailabilityAsync()
    {
        try
        {
            return await WMI.LenovoFanMethod.ExistsAsync().ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsProcessRunning(string processName)
    {
        try
        {
            return Process.GetProcessesByName(processName).Any();
        }
        catch
        {
            return false;
        }
    }

    private static bool HasAdministratorRights()
    {
        try
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static void GenerateRecommendations(DiagnosticReport report)
    {
        if (report.IsWindows11)
        {
            report.Recommendations.Add($"ℹ️ Windows 11 detected: {report.WindowsVersion}");
            
            if (report.SecureBootEnabled)
            {
                report.Recommendations.Add("ℹ️ Secure Boot is enabled. This may affect some low-level operations.");
            }
        }

        if (!report.WMIAvailable)
        {
            report.Recommendations.Add("❌ WMI interface not available. Install required Lenovo drivers.");
        }

        if (!report.FanTableDataAvailable)
        {
            report.Recommendations.Add("❌ Fan table data not available. This may indicate incompatible hardware or missing drivers.");
        }

        if (!report.FanMethodAvailable)
        {
            report.Recommendations.Add("❌ Fan control method not available. This may indicate incompatible hardware or missing drivers.");
        }

        if (!report.EnergyManagementInstalled)
        {
            report.MissingDrivers.Add("Lenovo Energy Management");
            report.Recommendations.Add("⚠️ Install Lenovo Energy Management driver from Lenovo support website.");
        }

        if (!report.GamingFeatureDriverInstalled)
        {
            report.MissingDrivers.Add("Lenovo Vantage Gaming Feature Driver");
            report.Recommendations.Add("⚠️ Install Lenovo Vantage Gaming Feature Driver from Lenovo support website.");
        }

        if (report.VantageRunning)
        {
            report.Recommendations.Add("⚠️ Lenovo Vantage is running. Disable it in Settings > Software Disabler.");
        }

        if (report.LegionZoneRunning)
        {
            report.Recommendations.Add("⚠️ Legion Zone is running. Disable it in Settings > Software Disabler.");
        }

        if (!report.HasAdminRights)
        {
            report.Recommendations.Add("⚠️ Application is not running as administrator. Some features may not work.");
        }

        if (report.WMIAvailable && report.FanTableDataAvailable && report.FanMethodAvailable &&
            report.EnergyManagementInstalled && report.GamingFeatureDriverInstalled && 
            !report.VantageRunning && !report.LegionZoneRunning && report.HasAdminRights)
        {
            report.Recommendations.Add("✅ All checks passed. System is ready for fan control.");
        }
    }
}
