using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Compat;
using LenovoLegionToolkit.WPF.Resources;

namespace LenovoLegionToolkit.WPF.Windows.Utils;

public partial class DiagnosticsWindow
{
    public DiagnosticsWindow()
    {
        InitializeComponent();
    }

    private async void DiagnosticsWindow_Loaded(object _, RoutedEventArgs e)
    {
        await LoadDiagnosticsAsync();
    }

    private async Task LoadDiagnosticsAsync()
    {
        var report = await DriverDiagnostics.GenerateReportAsync().ConfigureAwait(false);
        var windowsVersion = Windows11Compatibility.GetWindowsVersion();
        var isWindows11 = Windows11Compatibility.IsWindows11();
        var secureBoot = Windows11Compatibility.IsSecureBootEnabled();
        var tpm = Windows11Compatibility.IsTPMEnabled();

        // Windows Info
        _windowsVersionLabel.Text = windowsVersion;
        _windows11Label.Text = isWindows11 ? "Yes" : "No";

        // System Requirements
        _secureBootLabel.Text = secureBoot ? "Enabled" : "Disabled";
        _tpmLabel.Text = tpm ? "Enabled" : "Disabled";
        _adminLabel.Text = report.HasAdminRights ? "Yes" : "No";

        // Drivers
        _wmiLabel.Text = report.WMIAvailable ? "Available" : "Not Available";
        _fanTableDataLabel.Text = report.FanTableDataAvailable ? "Available" : "Not Available";
        _fanTableDataCountLabel.Text = report.FanTableDataAvailable ? $"({report.FanTableDataCount} entries)" : "";
        _fanMethodLabel.Text = report.FanMethodAvailable ? "Available" : "Not Available";
        _energyManagementLabel.Text = report.EnergyManagementInstalled ? "Installed" : "Not Installed";
        _gamingFeatureLabel.Text = report.GamingFeatureDriverInstalled ? "Installed" : "Not Installed";

        // Conflicting Processes
        _vantageLabel.Text = report.VantageRunning ? "Running" : "Not Running";
        _legionZoneLabel.Text = report.LegionZoneRunning ? "Running" : "Not Running";

        // Recommendations
        _recommendationsItemsControl.ItemsSource = report.Recommendations;
    }

    private void CloseButton_Click(object _, RoutedEventArgs e)
    {
        Close();
    }
}
