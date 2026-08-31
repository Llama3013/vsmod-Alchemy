using HarmonyLib;
using Vintagestory.GameContent;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace EffectLib
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    // Makes EffectContext.ColdResist (EffectAttr.ColdResist) actually stop the player getting
    // cold, by clamping their body temperature reading to normal.
    [HarmonyPatch(typeof(EntityBehaviorBodyTemperature), "CurBodyTemperature", MethodType.Getter)]
    internal static class ColdResistPatch
    {
        public static void Postfix(EntityBehaviorBodyTemperature __instance, ref float __result)
        {
            if (!__instance.entity.WatchedAttributes.GetBool(EffectAttr.ColdResist))
                return;

            if (__result < __instance.NormalBodyTemperature)
                __result = __instance.NormalBodyTemperature;
        }
    }
}
