using HarmonyLib;
using Vintagestory.API.Common;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    // Behaviors never receive OnHeldIdle, so coat sources are driven from here - otherwise only
    // item classes that forward it themselves could coat.
    [HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.OnHeldIdle))]
    internal static class CoatSourceIdlePatch
    {
        public static void Postfix(CollectibleObject __instance, ItemSlot slot, EntityAgent byEntity)
        {
            foreach (CollectibleBehavior behavior in __instance.CollectibleBehaviors)
            {
                if (behavior is PotionCoatSourceBehavior coatSource)
                {
                    coatSource.CoatingIdle(slot, byEntity);
                    return;
                }
            }
        }
    }
}
