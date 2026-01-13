using Autofac;
using LenovoLegionToolkit.Lib.AutoListeners;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Controllers.GodMode;
using LenovoLegionToolkit.Lib.Controllers.Sensors;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Features.FlipToStart;
using LenovoLegionToolkit.Lib.Features.Hybrid;
using LenovoLegionToolkit.Lib.Features.Hybrid.Notify;
using LenovoLegionToolkit.Lib.Features.InstantBoot;
using LenovoLegionToolkit.Lib.Features.OverDrive;
using LenovoLegionToolkit.Lib.Features.PanelLogo;
using LenovoLegionToolkit.Lib.Features.WhiteKeyboardBacklight;
using LenovoLegionToolkit.Lib.Integrations;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.Lib.PackageDownloader;
using LenovoLegionToolkit.Lib.Services;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.SoftwareDisabler;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib;

public class IoCModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.Register<HttpClientFactory>();

        builder.Register<FnKeysDisabler>();
        builder.Register<LegionZoneDisabler>();
        builder.Register<LegionSpaceDisabler>();
        builder.Register<VantageDisabler>();

        builder.Register<ApplicationSettings>();
        builder.Register<BalanceModeSettings>();
        builder.Register<GodModeSettings>();
        builder.Register<GPUOverclockSettings>();
        builder.Register<IntegrationsSettings>();
        builder.Register<PackageDownloaderSettings>();
        builder.Register<RGBKeyboardSettings>();
        builder.Register<SpectrumKeyboardSettings>();
        builder.Register<SunriseSunsetSettings>();
        builder.Register<UpdateCheckSettings>();

        builder.Register<AlwaysOnUSBFeature>();
        builder.Register<BatteryFeature>();
        builder.Register<BatteryNightChargeFeature>();
        builder.Register<DpiScaleFeature>();
        builder.Register<FlipToStartFeature>();
        builder.Register<FlipToStartCapabilityFeature>(true);
        builder.Register<FlipToStartUEFIFeature>(true);
        builder.Register<FnLockFeature>();
        builder.Register<GSyncFeature>();
        builder.Register<HDRFeature>();
        builder.Register<HybridModeFeature>();
        builder.Register<IGPUModeFeature>();
        builder.Register<IGPUModeCapabilityFeature>(true);
        builder.Register<IGPUModeFeatureFlagsFeature>(true);
        builder.Register<IGPUModeGamezoneFeature>(true);
        builder.Register<InstantBootFeature>();
        builder.Register<InstantBootFeatureFlagsFeature>(true);
        builder.Register<InstantBootCapabilityFeature>(true);
        builder.Register<MicrophoneFeature>();
        builder.Register<OneLevelWhiteKeyboardBacklightFeature>();
        builder.Register<OverDriveFeature>();
        builder.Register<OverDriveGameZoneFeature>(true);
        builder.Register<OverDriveCapabilityFeature>(true);
        builder.Register<PanelLogoBacklightFeature>();
        builder.Register<PanelLogoSpectrumBacklightFeature>(true);
        builder.Register<PanelLogoLenovoLightingBacklightFeature>(true);
        builder.Register<PortsBacklightFeature>();
        builder.Register<PowerModeFeature>();
        builder.Register<RefreshRateFeature>();
        builder.Register<ResolutionFeature>();
        builder.Register<SpeakerFeature>();
        builder.Register<TouchpadLockFeature>();
        builder.Register<WhiteKeyboardBacklightFeature>();
        builder.Register<WhiteKeyboardDriverBacklightFeature>(true);
        builder.Register<WhiteKeyboardLenovoLightingBacklightFeature>(true);
        builder.Register<WinKeyFeature>();

        builder.Register<DGPUNotify>();
        builder.Register<DGPUCapabilityNotify>(true);
        builder.Register<DGPUFeatureFlagsNotify>(true);
        builder.Register<DGPUGamezoneNotify>(true);
        // Listeners are registered WITHOUT auto-activation to prevent WMI blocking during startup
        // They will be started manually in App.xaml.cs after the window is shown
        builder.Register<DisplayBrightnessListener>();
        builder.Register<DisplayConfigurationListener>();
        builder.Register<DriverKeyListener>();
        builder.Register<LightingChangeListener>();
        builder.Register<NativeWindowsMessageListener>();
        builder.Register<PowerModeListener>();
        builder.Register<PowerStateListener>();
        builder.Register<RGBKeyboardBacklightListener>();
        builder.Register<SessionLockUnlockListener>();
        builder.Register<SpecialKeyListener>();
        builder.Register<SystemThemeListener>();
        builder.Register<ThermalModeListener>();
        builder.Register<WinKeyListener>();

        builder.Register<GameAutoListener>();
        builder.Register<InstanceStartedEventAutoAutoListener>();
        builder.Register<InstanceStoppedEventAutoAutoListener>();
        builder.Register<ProcessAutoListener>();
        builder.Register<TimeAutoListener>();
        builder.Register<UserInactivityAutoListener>();
        builder.Register<WiFiAutoListener>();

        builder.Register<AIController>();
        builder.Register<AutoRefreshRateController>();
        builder.Register<DisplayBrightnessController>();
        builder.Register<GodModeController>();
        builder.Register<GodModeControllerV1>(true);
        builder.Register<GodModeControllerV2>(true);
        builder.Register<GPUController>();
        builder.Register<GPUOverclockController>();
        builder.Register<RGBKeyboardBacklightController>();
        builder.Register<SensorsController>();
        builder.Register<SensorsControllerV1>(true);
        builder.Register<SensorsControllerV2>(true);
        builder.Register<SensorsControllerV3>(true);
        builder.Register<SmartFnLockController>();
        builder.Register<SpectrumKeyboardBacklightController>();
        builder.Register<WindowsPowerModeController>();
        builder.Register<WindowsPowerPlanController>();

        builder.Register<UpdateChecker>();
        builder.Register<WarrantyChecker>();

        builder.Register<PackageDownloaderFactory>();
        builder.Register<PCSupportPackageDownloader>();
        builder.Register<VantagePackageDownloader>();

        builder.Register<HWiNFOIntegration>();

        builder.Register<SunriseSunset>();

        builder.Register<BatteryDischargeRateMonitorService>();
        builder.Register<KeyboardBacklightTimeoutService>();
    }
}
