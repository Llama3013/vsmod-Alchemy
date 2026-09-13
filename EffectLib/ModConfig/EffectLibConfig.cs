#pragma warning disable IDE0130
namespace EffectLib
#pragma warning restore IDE0130
{
    public class EffectLibConfig
    {
        public static EffectLibConfig Loaded { get; set; } = new();

        // Hard floor/ceiling for any size-changing effect - never exceeded even if an effect's
        // own JSON/config asks for a wider range.
        public float MinPlayerHeight { get; set; } = UtilityEffects.DefaultMinHeight;
        public float MaxPlayerHeight { get; set; } = UtilityEffects.DefaultMaxHeight;
    }
}
