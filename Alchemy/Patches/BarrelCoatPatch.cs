using System;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    [HarmonyPatch(typeof(BlockEntityBarrel), "FindMatchingRecipe", typeof(IPlayer))]
    internal static class BarrelCoatPatch
    {
        [HarmonyPostfix]
        public static void Postfix(BlockEntityBarrel __instance)
        {
            if (!AlchemyConfig.Loaded.AllowWeaponCoating || !AlchemyConfig.Loaded.AllowBarrelCoating)
                return;

            ICoreAPI api = __instance.Api;
            if (api == null || api.Side != EnumAppSide.Server)
                return;

            try
            {
                InventoryBase inv = __instance.Inventory;
                if (inv == null || inv.Count < 2)
                    return;

                BarrelCoating.TryCoatInBarrel(inv[0], inv[1]);
            }
            catch (Exception e)
            {
                api.Logger.Error(
                    "[Alchemy] Barrel coating failed, if this occurs a lot consider disabling barrel coating: {0}",
                    e
                );
            }
        }
    }
}
