namespace LenovoLegionToolkit.WPF;

public enum DashboardGroupType
{
    Power,
    Graphics,
    Display,
    Other,
    Custom
}

public enum SensorsLayout
{
    Cards,
    Compact
}

public enum DashboardItem
{
    PowerMode,
    AlwaysOnUsb,
    InstantBoot,
    HybridMode,
    DiscreteGpu,
    OverclockDiscreteGpu,
    PanelLogoBacklight,
    PortsBacklight,
    Resolution,
    RefreshRate,
    DpiScale,
    Hdr,
    OverDrive,
    TurnOffMonitors,
    Microphone,
    FlipToStart,
    TouchpadLock,
    FnLock,
    WinKeyLock,
    WhiteKeyboardBacklight
}

public enum SnackbarType
{
    Success,
    Warning,
    Error,
    Info
}
