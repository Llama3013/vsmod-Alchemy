using HarmonyLib;
using Vintagestory.GameContent;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    [HarmonyPatch(typeof(EntityBehaviorBodyTemperature), "CurBodyTemperature", MethodType.Getter)]
    public static class BehaviorBodyTemperaturePatch
    {
        public static void Postfix(EntityBehaviorBodyTemperature __instance, ref float __result)
        {
            if (!__instance.entity.WatchedAttributes.GetBool(EffectAttr.ColdResist))
            {
                return;
            }
            if (__result < __instance.NormalBodyTemperature)
            {
                __result = __instance.NormalBodyTemperature;
            }
        }
    }
}
