using HarmonyLib;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    //This harmony patch allows the water breathing potion to work
    [HarmonyPatch(typeof(EntityBehaviorBreathe), "Oxygen", MethodType.Getter)]
    public static class BehaviorBreathePatch
    {
        public static void Postfix(EntityBehaviorBreathe __instance, ref float __result)
        {
            if (__instance.entity.WatchedAttributes.GetLong("waterbreathepotionid") == 0)
            {
                return;
            }
            ITreeAttribute oxygenTree = __instance.entity.WatchedAttributes.GetTreeAttribute(
                "oxygen"
            );
            if (oxygenTree == null)
            {
                return;
            }
            float currOxygen = oxygenTree.GetFloat("maxoxygen");
            __result = currOxygen;
        }
    }
}
