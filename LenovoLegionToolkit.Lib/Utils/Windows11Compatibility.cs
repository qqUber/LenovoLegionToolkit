using System;
using Microsoft.Win32;

namespace LenovoLegionToolkit.Lib.Utils;

public static class Windows11Compatibility
{
    public static bool IsWindows11()
    {
        try
        {
            var version = Environment.OSVersion.Version;
            // Windows 11 is version 10.0.22000 or higher
            return version.Major >= 10 && version.Build >= 22000;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsSecureBootEnabled()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
            var value = key?.GetValue("UEFISecureBootEnabled");
            return value is int intValue && intValue == 1;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsTPMEnabled()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\TPM\WMI");
            return key != null;
        }
        catch
        {
            return false;
        }
    }

    public static string GetWindowsVersion()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var displayVersion = key?.GetValue("DisplayVersion") as string;
            var build = key?.GetValue("CurrentBuild") as string;
            return $"{displayVersion} (Build {build})";
        }
        catch
        {
            return "Unknown";
        }
    }

    public static class DriverSignatureEnforcement
    {
        public static bool IsEnabled()
        {
            try
            {
                // Check if driver signature enforcement is enabled
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\CI\Config");
                var value = key?.GetValue("VulnerableDriverBlocklistEnable");
                return value is int intValue && intValue == 1;
            }
            catch
            {
                return true; // Assume enabled if we can't check
            }
        }
    }
}
