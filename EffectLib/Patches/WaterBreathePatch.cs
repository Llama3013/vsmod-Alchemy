using HarmonyLib;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

#pragma warning disable IDE0130
namespace EffectLib
#pragma warning restore IDE0130
{
    [HarmonyPatch(typeof(EntityBehaviorBreathe), "Oxygen", MethodType.Getter)]
    internal static class WaterBreathePatch
    {
        public static void Postfix(EntityBehaviorBreathe __instance, ref float __result)
        {
            if (!__instance.entity.WatchedAttributes.GetBool(EffectAttr.WaterBreathe))
                return;

            ITreeAttribute oxygenTree = __instance.entity.WatchedAttributes.GetTreeAttribute(
                "oxygen"
            );
            if (oxygenTree == null)
                return;

            __result = oxygenTree.GetFloat("maxoxygen");
        }
    }
}
