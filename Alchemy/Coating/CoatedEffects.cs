using System;
using EffectLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    public static class CoatedEffects
    {
        public const string KeyEffectId = "coatedEffectId";
        public const string KeyItemCode = "coatedItemCode";
        public const string KeyMultiplier = "coatMultiplier";
        public const string KeyDurationMul = "coatDurationMul";
        public const string KeyCharges = "coatCharges";

        public static string ResolveDisplayName(string itemCodeOrLangKey, string fallback) =>
            string.IsNullOrEmpty(itemCodeOrLangKey) ? fallback : Lang.Get(itemCodeOrLangKey);

        public static string DefaultItemCode(CollectibleObject col)
        {
            if (col?.Code == null)
                return "";
            string typePrefix = col is Block ? "block" : "item";
            return $"{col.Code.Domain}:{typePrefix}-{col.Code.Path}";
        }

        // Turns a DefaultItemCode lang code ("domain:item-path") back into the item's own code,
        // so a coating builds from its source item's definition of the effect.
        public static AssetLocation SourceFromItemCode(string itemCode)
        {
            if (string.IsNullOrEmpty(itemCode))
                return null;

            AssetLocation code = new(itemCode);
            foreach (string prefix in new[] { "item-", "block-" })
                if (code.Path.StartsWith(prefix, StringComparison.Ordinal))
                    return new AssetLocation(code.Domain, code.Path[prefix.Length..]);

            return null;
        }

        public static void ReadWeaponCoat(
            ItemStack stack,
            out string effectId,
            out float multiplier,
            out float durationMul,
            out int charges
        )
        {
            if (
                CombatOverhaulCompat.ShouldUseBuffStorage(stack.Collectible)
                && CombatOverhaulCompat.TryGetCoating(
                    stack,
                    out string coEffectId,
                    out _,
                    out float coMultiplier,
                    out float coDurationMul,
                    out int coCharges
                )
            )
            {
                effectId = coEffectId;
                multiplier = coMultiplier;
                durationMul = coDurationMul;
                charges = coCharges;
                return;
            }

            ITreeAttribute attrs = stack.Attributes;
            effectId = attrs.GetString(KeyEffectId);
            multiplier = attrs.GetFloat(KeyMultiplier);
            durationMul = attrs.GetFloat(KeyDurationMul, 1f);
            charges = attrs.GetInt(KeyCharges);
        }

        public static void WriteWeaponCoat(
            ItemSlot slot,
            string effectId,
            string itemCode,
            float multiplier,
            float durationMul,
            int charges
        )
        {
            if (CombatOverhaulCompat.ShouldUseBuffStorage(slot.Itemstack.Collectible))
            {
                CombatOverhaulCompat.SetCoating(slot, effectId, itemCode, multiplier, durationMul, charges);
                return;
            }

            ITreeAttribute attrs = slot.Itemstack.Attributes;
            attrs.SetString(KeyEffectId, effectId);
            attrs.SetString(KeyItemCode, itemCode ?? "");
            attrs.SetFloat(KeyMultiplier, multiplier);
            attrs.SetFloat(KeyDurationMul, durationMul);
            attrs.SetInt(KeyCharges, charges);
            slot.MarkDirty();
        }

        public static bool HasProjectileCoat(ItemStack stack)
        {
            return CombatOverhaulCompat.ShouldUseProjectileBuffStorage(stack)
                ? CombatOverhaulCompat.TryGetCoating(stack, out _, out _, out _, out _, out _)
                : !string.IsNullOrEmpty(stack.Attributes.GetString(KeyEffectId));
        }

        public static void WriteProjectileCoat(
            ItemStack stack,
            string effectId,
            string itemCode,
            float multiplier,
            float durationMul
        )
        {
            if (CombatOverhaulCompat.ShouldUseProjectileBuffStorage(stack))
            {
                CombatOverhaulCompat.SetProjectileCoating(stack, effectId, itemCode, multiplier, durationMul);
                return;
            }

            ITreeAttribute attrs = stack.Attributes;
            attrs.SetString(KeyEffectId, effectId);
            attrs.SetString(KeyItemCode, itemCode ?? "");
            attrs.SetFloat(KeyMultiplier, multiplier);
            attrs.SetFloat(KeyDurationMul, durationMul);
        }

        internal static void ClearStackCoat(ITreeAttribute attrs)
        {
            attrs.RemoveAttribute(KeyEffectId);
            attrs.RemoveAttribute(KeyItemCode);
            attrs.RemoveAttribute(KeyMultiplier);
            attrs.RemoveAttribute(KeyDurationMul);
            attrs.RemoveAttribute(KeyCharges);
        }

        internal static (string EffectId, float Multiplier, float DurationMul, string ItemCode)? TryConsumeWeaponCharge(
            ItemSlot slot
        )
        {
            ITreeAttribute attrs = slot.Itemstack.Attributes;
            string effectId = attrs.GetString(KeyEffectId);
            if (string.IsNullOrEmpty(effectId))
                return null;

            int charges = attrs.GetInt(KeyCharges);
            if (charges <= 0)
            {
                ClearStackCoat(attrs);
                slot.MarkDirty();
                return null;
            }

            if (!PotionConsumableLogic.IsCoatingAllowed(effectId))
                return null;

            float multiplier = attrs.GetFloat(KeyMultiplier);
            float durationMul = attrs.GetFloat(KeyDurationMul, 1f);
            string itemCode = attrs.GetString(KeyItemCode);

            charges--;
            if (charges <= 0)
                ClearStackCoat(attrs);
            else
                attrs.SetInt(KeyCharges, charges);
            slot.MarkDirty();

            return (effectId, multiplier, durationMul, itemCode);
        }

        internal static (string EffectId, float Multiplier, float DurationMul, string ItemCode)? TryConsumeProjectileCoat(
            ItemStack projectileStack
        )
        {
            ITreeAttribute attrs = projectileStack.Attributes;
            string effectId = attrs.GetString(KeyEffectId);
            if (string.IsNullOrEmpty(effectId))
                return null;

            if (!PotionConsumableLogic.IsCoatingAllowed(effectId))
                return null;

            float multiplier = attrs.GetFloat(KeyMultiplier);
            float durationMul = attrs.GetFloat(KeyDurationMul, 1f);
            string itemCode = attrs.GetString(KeyItemCode);
            ClearStackCoat(attrs);

            return (effectId, multiplier, durationMul, itemCode);
        }

        // Coated hits and thrown flasks. Drinking-style side effects and exclusivity groups apply,
        // the already-active/limit checks don't (see GetCoatingBlockReason).
        public static void Apply(
            string potionId,
            Entity entity,
            float multiplier,
            string displayName,
            AssetLocation source,
            float durationMul
        )
        {
            if (entity == null || !entity.Alive)
                return;

            PotionConsumableLogic.ApplySideEffects(entity, potionId, multiplier);
            EntityEffects.Apply(
                potionId,
                entity,
                multiplier,
                displayName,
                source,
                durationMul,
                (player, ctx) => PotionConsumableLogic.GetCoatingBlockReason(player, potionId, ctx)
            );
        }
    }
}
