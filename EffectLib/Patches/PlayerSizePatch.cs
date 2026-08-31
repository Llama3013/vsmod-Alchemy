using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace EffectLib
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    // Keeps the player's collision box, eye height, and visual scale in sync with the size
    // WatchedAttributes UtilityEffects writes. Runs on both server and client via
    // WatchedAttributes sync.
    [HarmonyPatch(typeof(Entity), "Initialize")]
    internal static class PlayerSizePatch
    {
        public static void Postfix(Entity __instance)
        {
            if (__instance is not EntityPlayer player)
                return;

            UtilityEffects.ApplySizeToEntity(player);
            player.WatchedAttributes.RegisterModifiedListener(
                "potionSizeDelta",
                () => UtilityEffects.ApplySizeToEntity(player)
            );
        }
    }
}
