using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace EffectLib
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    // Delivers a coated melee weapon's effect on hit, one charge at a time. Stack-stored
    // coatings only; Combat Overhaul weapons never reach this patch and deliver their own.
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
            if (!CoatingPolicy.AllowCoating())
                return;
            if (world.Side != EnumAppSide.Server)
                return;
            if (itemslot?.Itemstack == null)
                return;
            if (attackedEntity == null || !attackedEntity.Alive)
                return;

            (string effectId, float multiplier, string itemCode)? consumed =
                CoatedEffects.TryConsumeWeaponCharge(itemslot);
            if (consumed == null)
                return;

            (string effectId, float multiplier, string itemCode) = consumed.Value;
            string displayName = CoatedEffects.ResolveDisplayName(itemCode, effectId);
            CoatedEffects.Apply(effectId, attackedEntity, multiplier, displayName);
        }
    }
}
