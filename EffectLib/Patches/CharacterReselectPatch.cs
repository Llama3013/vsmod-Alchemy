using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

#pragma warning disable IDE0130
namespace EffectLib
#pragma warning restore IDE0130
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

            UtilityEffects.ClearSizeState(player);
        }
    }
}
