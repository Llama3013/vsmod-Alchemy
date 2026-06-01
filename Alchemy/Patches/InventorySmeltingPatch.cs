using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    [HarmonyPatch(typeof(InventorySmelting), "get_HaveCookingContainer")]
    internal static class PatchInventorySmeltingHaveCookingContainer
    {
        public static void Postfix(InventorySmelting __instance, ref bool __result)
        {
            if (!__result)
                return;
            if (AlchemyConfig.Loaded.AllowCauldronInVanillaFirepit)
                return;
            if (__instance[1]?.Itemstack?.Block is not BlockCauldronFirepit)
                return;

            ICoreAPI api = __instance.Api;
            if (api?.World?.BlockAccessor == null || __instance.pos == null)
                return;

            if (
                api.World.BlockAccessor.GetBlockEntity(__instance.pos) is BlockEntityCauldronFirepit
            )
                return;

            __result = false;
        }
    }
}
