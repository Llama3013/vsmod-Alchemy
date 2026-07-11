using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    // Blocks mounting while the flight flask is active. Otherwise unmounting clears
    // the player's FreeMove state and the flight effect ends up lost.
    [HarmonyPatch(typeof(EntityAgent), nameof(EntityAgent.TryMount))]
    public static class EntityAgentMountPatch
    {
        public static bool Prefix(EntityAgent __instance, ref bool __result)
        {
            if (
                __instance is EntityPlayer player
                && player.WatchedAttributes.GetLong("flightpotionid") != 0
            )
            {
                if (player.Player is IServerPlayer serverPlayer)
                {
                    serverPlayer.SendMessage(
                        GlobalConstants.InfoLogChatGroup,
                        Lang.Get("alchemy:flight-mount-block"),
                        EnumChatType.Notification
                    );
                }

                __result = false;
                return false;
            }

            return true;
        }
    }
}
