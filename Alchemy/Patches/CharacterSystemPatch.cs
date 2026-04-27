using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Alchemy.Patches
{
    [HarmonyPatch(typeof(CharacterSystem), "onCharacterSelection")]
    internal static class CharacterSystemPatch
    {
        public static void Postfix(IServerPlayer fromPlayer, CharacterSelectionPacket p)
        {
            if (!p.DidSelect) return;
            if (fromPlayer.Entity is not EntityPlayer player) return;

            // I have to clear stale potion size state. I can't call ResetPlayerSize here because
            // skinConfig was already marked dirty (before this postfix runs), so PlayerModelLib
            // has already updated the collision box to the new model's dimensions — this is needed 
            // so it doesn't just break that. I zero out the attributes so future potions re-snapshot cleanly.
            UtilityEffects.ClearSizeState(player);
        }
    }
}
