using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace EffectLib
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    [HarmonyPatch(typeof(CharacterSystem), "onCharacterSelection")]
    internal static class CharacterReselectPatch
    {
        public static void Postfix(IServerPlayer fromPlayer, CharacterSelectionPacket p)
        {
            if (!p.DidSelect)
                return;
            if (fromPlayer.Entity is not EntityPlayer player)
                return;

            // Clear stale size-effect state rather than resetting it: skinConfig was already
            // marked dirty (before this postfix runs), so PlayerModelLib has already updated the
            // collision box to the new model's dimensions, and a full ResetPlayerSize would fight
            // that. Zeroing the attributes just lets a future size effect re-snapshot cleanly.
            UtilityEffects.ClearSizeState(player);
        }
    }
}
