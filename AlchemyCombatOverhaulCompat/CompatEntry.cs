using System;
using CombatOverhaul.Inputs;
using CombatOverhaul.WeaponBuffs;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

namespace Alchemy.CombatOverhaulCompatBridge
{
    public static class CompatEntry
    {
        internal const string BuffCode = "weaponcoat";
        internal const string BuffSourceModId = "alchemy";
        private const string BuffInstanceId = "alchemy-weaponcoat";

        // Mirrors Alchemy.CoatedEffects' attribute keys. Kept as local literals rather than a
        // shared constant: Alchemy.csproj embeds this project's built dll as a resource, so a
        // compile-time reference the other way (this project -> Alchemy.csproj) would recreate
        // the circular project dependency that used to break `dotnet clean`/`build`. Keep in sync
        // by hand with Alchemy.CoatedEffects.
        private const string KeyEffectId = "coatedEffectId";
        private const string KeyItemCode = "coatedItemCode";
        private const string KeyCharges = "coatCharges";
        private const string KeyMultiplier = "coatMultiplier";
        private const string KeyDurationMul = "coatDurationMul";

        private static WeaponBuffSystem buffSystem;
        private static readonly WeaponCoatBuffProvider provider = new();

        // Wired up from Alchemy.CombatOverhaulCompat.BindCompatEntry via reflection, so this
        // project never needs a compile-time reference back to Alchemy.
        // Explicitly System.Func/System.Action: Vintagestory.API.Common also declares its own
        // Func<>/Action<> delegate types, which are ambiguous with the BCL ones by simple name.
        internal static System.Func<bool> allowCoating;
        internal static System.Func<float> effectMultiplier;
        internal static System.Func<string, bool> isEffectCoatable;
        internal static System.Action<string, Entity, float, float, string, string> applyEffect;

        public static void Init(
            ICoreAPI api,
            System.Func<bool> allowCoating,
            System.Func<float> effectMultiplier,
            System.Func<string, bool> isEffectCoatable,
            System.Action<string, Entity, float, float, string, string> applyEffect
        )
        {
            CompatEntry.allowCoating = allowCoating;
            CompatEntry.effectMultiplier = effectMultiplier;
            CompatEntry.isEffectCoatable = isEffectCoatable;
            CompatEntry.applyEffect = applyEffect;

            buffSystem =
                api.ModLoader.GetModSystem<WeaponBuffSystem>()
                ?? throw new InvalidOperationException(
                    "WeaponBuffSystem mod system not found in overhaullib"
                );
            buffSystem.RegisterProvider(provider);
        }

        public static void Shutdown()
        {
            buffSystem?.UnregisterProvider(provider);
            buffSystem = null;
        }

        public static bool IsCoManagedWeapon(CollectibleObject collectible) =>
            collectible is IHasWeaponLogic;

        public static bool IsCoManagedProjectile(ItemStack stack) =>
            buffSystem?.IsProjectileBuffTarget(stack) ?? false;

        public static (string, string, float, float, int)? GetCoating(ItemStack stack)
        {
            if (buffSystem == null)
                return null;

            foreach (WeaponBuffInstance buff in buffSystem.GetBuffs(stack))
            {
                if (
                    buff.Code != BuffCode
                    || buff.SourceModId != BuffSourceModId
                    || buff.Data == null
                )
                    continue;

                return (
                    buff.Data.GetString("effectId", ""),
                    buff.Data.GetString("itemCode", ""),
                    buff.Data.GetFloat("multiplier", effectMultiplier?.Invoke() ?? 1f),
                    buff.Data.GetFloat("durationMul", 1f),
                    buff.UsesRemaining ?? 0
                );
            }

            return null;
        }

        public static void ApplyCoatingBuff(
            ItemSlot slot,
            string effectId,
            string itemCode,
            float multiplier,
            float durationMul,
            int charges
        )
        {
            if (buffSystem == null || slot?.Itemstack == null)
                return;

            TreeAttribute data = new();
            data.SetString("effectId", effectId);
            data.SetString("itemCode", itemCode);
            data.SetFloat("multiplier", multiplier);
            data.SetFloat("durationMul", durationMul);

            buffSystem.ApplyBuff(
                slot,
                new WeaponBuffDefinition
                {
                    Code = BuffCode,
                    SourceModId = BuffSourceModId,
                    DisplayNameLangCode = itemCode,
                    Uses = charges,
                    ConsumeOn =
                    [
                        WeaponBuffConsumptionTrigger.MeleeHit,
                        WeaponBuffConsumptionTrigger.ProjectileHit,
                    ],
                    Data = data,
                },
                new WeaponBuffApplyOptions { InstanceId = BuffInstanceId }
            );

            ITreeAttribute attrs = slot.Itemstack.Attributes;
            attrs.RemoveAttribute(KeyEffectId);
            attrs.RemoveAttribute(KeyItemCode);
            attrs.RemoveAttribute(KeyCharges);
            attrs.RemoveAttribute(KeyMultiplier);
            attrs.RemoveAttribute(KeyDurationMul);
            slot.MarkDirty();
        }

        public static void ApplyProjectileCoatingBuff(
            ItemStack stack,
            string effectId,
            string itemCode,
            float multiplier,
            float durationMul
        )
        {
            if (buffSystem == null || stack == null)
                return;

            TreeAttribute data = new();
            data.SetString("effectId", effectId);
            data.SetString("itemCode", itemCode);
            data.SetFloat("multiplier", multiplier);
            data.SetFloat("durationMul", durationMul);
            data.SetBool("isProjectile", true);

            buffSystem.ApplyProjectileBuff(
                stack,
                new WeaponBuffDefinition
                {
                    Code = BuffCode,
                    SourceModId = BuffSourceModId,
                    DisplayNameLangCode = itemCode,
                    Uses = 1,
                    ConsumeOn = [WeaponBuffConsumptionTrigger.ProjectileHit],
                    Data = data,
                },
                new WeaponBuffApplyOptions { InstanceId = BuffInstanceId }
            );

            ITreeAttribute attrs = stack.Attributes;
            attrs.RemoveAttribute(KeyEffectId);
            attrs.RemoveAttribute(KeyItemCode);
            attrs.RemoveAttribute(KeyMultiplier);
            attrs.RemoveAttribute(KeyDurationMul);
        }
    }

    internal sealed class WeaponCoatBuffProvider : WeaponBuffProvider
    {
        public override bool Handles(WeaponBuffQueryContext context, WeaponBuffInstance buff) =>
            buff.Code == CompatEntry.BuffCode && buff.SourceModId == CompatEntry.BuffSourceModId;

        public override void ModifyMeleeDamage(
            WeaponBuffDamageContext context,
            WeaponBuffInstance buff
        ) => ApplyCoatEffect(context.Target, buff);

        public override void ModifyRangedDamage(
            WeaponBuffDamageContext context,
            WeaponBuffInstance buff
        ) => ApplyCoatEffect(context.Target, buff);

        private static void ApplyCoatEffect(Entity target, WeaponBuffInstance buff)
        {
            if (!(CompatEntry.allowCoating?.Invoke() ?? true))
                return;

            if (target == null || !target.Alive || target.World.Side != EnumAppSide.Server)
                return;

            string effectId = buff.Data?.GetString("effectId");
            if (
                string.IsNullOrEmpty(effectId)
                || !(CompatEntry.isEffectCoatable?.Invoke(effectId) ?? true)
            )
                return;

            float multiplier = buff.Data.GetFloat(
                "multiplier",
                CompatEntry.effectMultiplier?.Invoke() ?? 1f
            );
            float durationMul = buff.Data.GetFloat("durationMul", 1f);
            string itemCode = buff.Data.GetString("itemCode");
            string displayName = string.IsNullOrEmpty(itemCode) ? effectId : Lang.Get(itemCode);
            CompatEntry.applyEffect?.Invoke(effectId, target, multiplier, durationMul, displayName, itemCode);
        }

        public override void AppendTooltip(
            WeaponBuffTooltipContext context,
            WeaponBuffInstance buff
        )
        {
            string effectId = buff.Data?.GetString("effectId");
            if (string.IsNullOrEmpty(effectId))
                return;

            string itemCode = buff.Data.GetString("itemCode");
            string effectName = string.IsNullOrEmpty(itemCode) ? effectId : Lang.Get(itemCode);
            context.Description.Append("<font color=\"#b8bb00\">");
            context.Description.Append(
                buff.Data.GetBool("isProjectile")
                    ? Lang.Get("alchemy:arrow-coated", effectName)
                    : Lang.Get("alchemy:weapon-coated", effectName, buff.UsesRemaining ?? 0)
            );
            if (!(CompatEntry.isEffectCoatable?.Invoke(effectId) ?? true))
                context.Description.Append(" (" + Lang.Get("alchemy:coating-disabled-suffix") + ")");
            context.Description.AppendLine("</font>");
        }
    }
}
