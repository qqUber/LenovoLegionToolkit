namespace LenovoLegionToolkit.Lib.Extensions;

public static class SpectrumKeyboardBacklightEffectTypeExtensions
{

    public static bool IsAllLightsEffect(this SpectrumKeyboardBacklightEffectType type) => type switch
    {
        SpectrumKeyboardBacklightEffectType.AudioBounce => true,
        SpectrumKeyboardBacklightEffectType.AudioRipple => true,
        SpectrumKeyboardBacklightEffectType.AuroraSync => true,
        SpectrumKeyboardBacklightEffectType.Temperature => true,
        SpectrumKeyboardBacklightEffectType.Disco => true,
        SpectrumKeyboardBacklightEffectType.Lightning => true,
        SpectrumKeyboardBacklightEffectType.Christmas => true,
        _ => false
    };

    public static bool IsWholeKeyboardEffect(this SpectrumKeyboardBacklightEffectType type) => type switch
    {
        SpectrumKeyboardBacklightEffectType.Type => true,
        SpectrumKeyboardBacklightEffectType.Ripple => true,
        _ => false
    };

    /// <summary>
    /// Returns true if this effect is software-driven (rendered by app, not firmware)
    /// </summary>
    public static bool IsSoftwareEffect(this SpectrumKeyboardBacklightEffectType type) => type switch
    {
        SpectrumKeyboardBacklightEffectType.AuroraSync => true,
        SpectrumKeyboardBacklightEffectType.Temperature => true,
        SpectrumKeyboardBacklightEffectType.Disco => true,
        SpectrumKeyboardBacklightEffectType.Lightning => true,
        SpectrumKeyboardBacklightEffectType.Christmas => true,
        _ => false
    };
}
