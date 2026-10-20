using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    [HarmonyPatch(typeof(CollectibleObject), "OnAttackingWith")]
    internal static class WeaponCoatPatch
    {
        [HarmonyPostfix]
        public static void Postfix(
            IWorldAccessor world,
            Entity byEntity,
            Entity attackedEntity,
            ItemSlot itemslot
        )
        {
            if (!AlchemyConfig.Loaded.AllowWeaponCoating)
                return;
            if (world.Side != EnumAppSide.Server)
                return;
            if (itemslot?.Itemstack == null)
                return;
            if (attackedEntity == null || !attackedEntity.Alive)
                return;

            (string potionId, float multiplier, float durationMul, string itemCode)? consumed =
                CoatedEffects.TryConsumeWeaponCharge(itemslot);
            if (consumed == null)
                return;

            (string potionId, float multiplier, float durationMul, string itemCode) = consumed.Value;
            CoatedEffects.Apply(
                potionId,
                attackedEntity,
                multiplier,
                CoatedEffects.ResolveDisplayName(itemCode, potionId),
                CoatedEffects.SourceFromItemCode(itemCode),
                durationMul
            );
        }
    }
}
