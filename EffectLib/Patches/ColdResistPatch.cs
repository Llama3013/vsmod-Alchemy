using HarmonyLib;
using Vintagestory.GameContent;

#pragma warning disable IDE0130
namespace EffectLib
#pragma warning restore IDE0130
{
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
