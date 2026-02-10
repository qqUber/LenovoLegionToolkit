# Release Notes - v1.6.0

## 🚀 New Features

### Added: System Diagnostics Window
- New diagnostics window to check system compatibility
- Displays Windows version information
- Shows WMI interface availability for fan control
- Checks driver installation status
- Identifies conflicting processes (Vantage, Legion Zone, Hotkeys)
- Verifies administrator rights

### Added: Close Button to Custom Mode Window
- Added X (close) button to Custom God Mode window header
- Allows users to close the window without saving changes
- Improved UX consistency with other windows

---

## 🔄 Updates

### .NET Framework Migration
- **Upgraded from .NET 8.0 to .NET 9.0**
- Better Windows 11 compatibility
- Improved performance and security

### Dependency Updates
- **Microsoft.Windows.CsWin32**: 0.3.183 → 0.3.205
- **LibreHardwareMonitorLib**: 0.9.5 → 0.9.6-pre625

---

## 🐛 Bug Fixes

### Enhanced WMI Error Handling
- Improved error messages for fan control operations
- Better exception handling in `WMI.LenovoFanMethod.cs`
- Added detailed logging for fan table data retrieval
- Added `ExistsAsync()` method for WMI availability checking

### Fixed: Fan Table Data Logging
- Added trace logging for raw fan table data
- Improved debugging capabilities for fan curve issues

---

## 📁 Files Changed

| File | Type | Description |
|------|------|-------------|
| `DriverDiagnostics.cs` | New | System diagnostics utility |
| `Windows11Compatibility.cs` | New | Windows 11 detection utilities |
| `DiagnosticsWindow.xaml` | New | Diagnostics UI |
| `DiagnosticsWindow.xaml.cs` | New | Diagnostics code-behind |
| `GodModeSettingsWindow.xaml` | Feature | Added close button |
| `GodModeSettingsWindow.xaml.cs` | Feature | Close button handler |
| `WMI.LenovoFanMethod.cs` | Bug Fix | Enhanced error handling |
| `GodModeControllerV2.cs` | Bug Fix | Added fan table logging |
| `LenovoLegionToolkit.Lib.csproj` | Update | NuGet package updates |
| `LenovoLegionToolkit.WPF.csproj` | Update | .NET 9.0 migration |
| `make_installer.iss` | Update | Version 1.6.0 |

---

## 🔍 Technical Details

### WMI Error Handling Enhancement
```csharp
try
{
    // Fan operation
    await method.InvokeAsync(...);
}
catch (ManagementException ex)
{
    if (Log.Instance.IsTraceEnabled)
        Log.Instance.Trace($"Fan operation failed: {ex.ErrorCode} - {ex.Message}");
    throw new InvalidOperationException($"Fan control error: {ex.Message}", ex);
}
```

### Diagnostics System
```csharp
public class DriverDiagnostics
{
    public bool WmiAvailable { get; set; }
    public bool DriversInstalled { get; set; }
    public bool FanTableDataAvailable { get; set; }
    public bool FanMethodAvailable { get; set; }
    public List<string> ConflictingProcesses { get; set; }
}
```

---

## ✅ Build Status

- **Version**: 1.6.0
- **Errors**: 0
- **Warnings**: 0
- **.NET**: 9.0
- **Installer**: `build_installer/LOQToolkitSetup_v1.6.0.exe`

---

## 📝 Migration Notes

This version is based on **LenovoLegionToolkit v2.26.1** by BartoszCichecki.

**Original repository**: https://github.com/BartoszCichecki/LenovoLegionToolkit  
**Fork repository**: https://github.com/varun875/Varun-LLT

---

# Release Notes - v1.5.1

## 🐛 Bug Fixes

### Fixed: Power Mode Notification Display
- **Issue**: Notifications showed `System.Object[]` instead of the actual power mode name (Quiet, Balanced, Performance)
- **Fix**: Corrected argument extraction in `NotificationsManager.cs` to properly display power mode names

### Fixed: Quiet Mode Power Plan Mapping
- **Issue**: When switching to Quiet mode, Windows power mode stayed on "Balanced" instead of switching to "Best Power Efficiency" like Lenovo Vantage
- **Fix**: Added intelligent default power mode mappings:
  - 🔇 **Quiet** → Best Power Efficiency
  - ⚖️ **Balance** → Balanced  
  - 🚀 **Performance** → Best Performance
  - ⚡ **Extreme/GodMode** → Best Performance

### Added: Close Button on Notifications
- Added X (close) button to notification popups for manual dismissal
- Added close button to main window snackbar notifications
- Set notifications to be hidden by default on startup

---

## ⚡ Performance Improvements

### Faster Power Mode Switching
- Power mode and power plan updates now run in parallel (`Task.WhenAll`)
- Reduced latency when switching between power modes

### Optimized Notifications Manager
- Replaced switch expressions with `HashSet` and `Dictionary` lookups for O(1) performance
- Reduced code duplication across notification handling

---

## 🔧 Code Refactoring

### NotificationsManager.cs (Major Refactor)
- Data-driven approach using `HashSet` and `Dictionary` for notification types
- Extracted helper methods for single responsibility:
  - `IsNotificationAllowed()`, `GetSymbol()`, `GetOverlaySymbol()`
  - `GetNotificationText()`, `GetSymbolTransform()`, `GetClickAction()`
  - `GetNotificationDuration()`, `GetTargetScreens()`, `CreateNotificationWindow()`
- Easier to add new notification types in the future

### PowerModeListener.cs
- Replaced large switch statement with dictionary lookup
- Parallelized power mode and plan updates

### TrayHelper.cs
- Extracted `GetPowerModeDisplayText()` helper to eliminate code duplication

### SnackbarHelper.cs
- Extracted `GetSnackbar()` helper method
- Improved timeout calculation with clearer naming

### AbstractRefreshingControl.cs
- Added `CancelPendingRefresh()` helper
- Exposed `RefreshCancellationToken` for derived classes
- Cleaner event handler naming

### WindowsPowerModeController.cs
- Added `GetDefaultWindowsPowerMode()` for sensible power mode defaults

---

## 📁 Files Changed

| File | Type | Description |
|------|------|-------------|
| `NotificationsManager.cs` | Refactor | Complete rewrite with data-driven approach |
| `PowerModeListener.cs` | Refactor + Perf | Dictionary lookup, parallel updates |
| `WindowsPowerModeController.cs` | Bug Fix | Default power mode mappings |
| `TrayHelper.cs` | Refactor | Extracted helper method |
| `SnackbarHelper.cs` | Refactor | Cleaner code structure |
| `AbstractRefreshingControl.cs` | Refactor | Better cancellation handling |
| `NotificationWindow.cs` | Feature | Added close button |
| `MainWindow.xaml` | Feature | Added snackbar close button |
| `MainWindow.xaml.cs` | Feature | Close button handler |
| `WpfUiCompat.cs` | Feature | Snackbar Show/Hide methods |

---

## 🔍 Technical Details

### Notification Text Fix
**Before:**
```csharp
NotificationType.PowerModeQuiet => notification.Args?.ToString() ?? string.Empty
// Output: "System.Object[]"
```

**After:**
```csharp
var firstArg = args?.FirstOrDefault()?.ToString() ?? string.Empty;
if (ArgsDisplayTypes.Contains(type))
    return firstArg;
// Output: "Quiet"
```

### Power Mode Defaults
```csharp
private static WindowsPowerMode GetDefaultWindowsPowerMode(PowerModeState state) => state switch
{
    PowerModeState.Quiet => WindowsPowerMode.BestPowerEfficiency,
    PowerModeState.Performance => WindowsPowerMode.BestPerformance,
    PowerModeState.Extreme => WindowsPowerMode.BestPerformance,
    PowerModeState.GodMode => WindowsPowerMode.BestPerformance,
    _ => WindowsPowerMode.Balanced
};
```

### Parallel Power Updates
```csharp
// Before: Sequential
await windowsPowerModeController.SetPowerModeAsync(value);
await windowsPowerPlanController.SetPowerPlanAsync(value);

// After: Parallel (faster)
await Task.WhenAll(
    windowsPowerModeController.SetPowerModeAsync(value),
    windowsPowerPlanController.SetPowerPlanAsync(value)
);
```

---

## ✅ Build Status

- **Errors**: 0
- **Warnings**: 0
- **Build Time**: ~13 seconds
