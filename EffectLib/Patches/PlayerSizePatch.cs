using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

#pragma warning disable IDE0130
namespace EffectLib
#pragma warning restore IDE0130
{
    [HarmonyPatch(typeof(Entity), "Initialize")]
    internal static class PlayerSizePatch
    {
        public static void Postfix(Entity __instance)
        {
            if (__instance is not EntityPlayer player)
                return;

            UtilityEffects.ApplySizeToEntity(player);
            player.WatchedAttributes.RegisterModifiedListener(
                UtilityEffects.SizeDeltaAttr,
                () => UtilityEffects.ApplySizeToEntity(player)
            );
        }
    }
}
