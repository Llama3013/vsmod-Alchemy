using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace EffectLib
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    // Blocks mounting while EffectContext.CanFly (EffectAttr.CanFly) is active. Otherwise
    // unmounting clears the player's FreeMove state and the flight effect ends up lost.
    [HarmonyPatch(typeof(EntityAgent), nameof(EntityAgent.TryMount))]
    internal static class FlightMountBlockPatch
    {
        public static bool Prefix(EntityAgent __instance, ref bool __result)
        {
            if (__instance is not EntityPlayer player || !player.WatchedAttributes.GetBool(EffectAttr.CanFly))
                return true;

            if (player.Player is IServerPlayer serverPlayer)
                serverPlayer.SendMessage(
                    GlobalConstants.InfoLogChatGroup,
                    Lang.Get("effectlib:flight-mount-block"),
                    EnumChatType.Notification
                );

            __result = false;
            return false;
        }
    }
}
