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
