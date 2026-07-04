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

        private static WeaponBuffSystem buffSystem;
        private static readonly WeaponCoatBuffProvider provider = new();

        internal static System.Func<string, bool> isCoatingAllowed;
        internal static Func<bool> allowWeaponCoating;
        internal static Func<float> defaultCoatMultiplier;
        internal static System.Func<string, string, string> resolveDisplayName;
        internal static Action<string, Entity, float, string> applyCoatEffect;

        public static void Init(
            ICoreAPI api,
            System.Func<string, bool> isCoatingAllowed,
            Func<bool> allowWeaponCoating,
            Func<float> defaultCoatMultiplier,
            System.Func<string, string, string> resolveDisplayName,
            Action<string, Entity, float, string> applyCoatEffect
        )
        {
            CompatEntry.isCoatingAllowed = isCoatingAllowed;
            CompatEntry.allowWeaponCoating = allowWeaponCoating;
            CompatEntry.defaultCoatMultiplier = defaultCoatMultiplier;
            CompatEntry.resolveDisplayName = resolveDisplayName;
            CompatEntry.applyCoatEffect = applyCoatEffect;

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

        public static bool IsCoManagedWeapon(CollectibleObject collectible)
        {
            return collectible is IHasWeaponLogic;
        }

        public static bool IsCoManagedProjectile(ItemStack stack)
        {
            return buffSystem?.IsProjectileBuffTarget(stack) ?? false;
        }

        public static (string, string, float, int)? GetCoating(ItemStack stack)
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
                    buff.Data.GetString("potionId", ""),
                    buff.Data.GetString("itemCode", ""),
                    buff.Data.GetFloat("multiplier", defaultCoatMultiplier()),
                    buff.UsesRemaining ?? 0
                );
            }

            return null;
        }

        public static void ApplyCoatingBuff(
            ItemSlot slot,
            string potionId,
            string itemCode,
            float multiplier,
            int charges
        )
        {
            if (buffSystem == null || slot?.Itemstack == null)
                return;

            TreeAttribute data = new();
            data.SetString("potionId", potionId);
            data.SetString("itemCode", itemCode);
            data.SetFloat("multiplier", multiplier);

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
            attrs.RemoveAttribute("coatedPotionId");
            attrs.RemoveAttribute("coatedItemCode");
            attrs.RemoveAttribute("coatCharges");
            attrs.RemoveAttribute("coatMultiplier");
            slot.MarkDirty();
        }

        public static void ApplyProjectileCoatingBuff(
            ItemStack stack,
            string potionId,
            string itemCode,
            float multiplier
        )
        {
            if (buffSystem == null || stack == null)
                return;

            TreeAttribute data = new();
            data.SetString("potionId", potionId);
            data.SetString("itemCode", itemCode);
            data.SetFloat("multiplier", multiplier);
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
            attrs.RemoveAttribute("coatedPotionId");
            attrs.RemoveAttribute("coatedItemCode");
            attrs.RemoveAttribute("coatMultiplier");
        }
    }

    internal sealed class WeaponCoatBuffProvider : WeaponBuffProvider
    {
        public override bool Handles(WeaponBuffQueryContext context, WeaponBuffInstance buff)
        {
            return buff.Code == CompatEntry.BuffCode
                && buff.SourceModId == CompatEntry.BuffSourceModId;
        }

        public override void ModifyMeleeDamage(
            WeaponBuffDamageContext context,
            WeaponBuffInstance buff
        )
        {
            ApplyCoatEffect(context.Target, buff);
        }

        public override void ModifyRangedDamage(
            WeaponBuffDamageContext context,
            WeaponBuffInstance buff
        )
        {
            ApplyCoatEffect(context.Target, buff);
        }

        private static void ApplyCoatEffect(Entity target, WeaponBuffInstance buff)
        {
            if (!CompatEntry.allowWeaponCoating())
                return;

            if (target == null || !target.Alive || target.World.Side != EnumAppSide.Server)
                return;

            string potionId = buff.Data?.GetString("potionId");
            if (string.IsNullOrEmpty(potionId) || !CompatEntry.isCoatingAllowed(potionId))
                return;

            float multiplier = buff.Data.GetFloat(
                "multiplier",
                CompatEntry.defaultCoatMultiplier()
            );
            string displayName = CompatEntry.resolveDisplayName(
                buff.Data.GetString("itemCode"),
                potionId
            );
            CompatEntry.applyCoatEffect(potionId, target, multiplier, displayName);
        }

        public override void AppendTooltip(
            WeaponBuffTooltipContext context,
            WeaponBuffInstance buff
        )
        {
            string potionId = buff.Data?.GetString("potionId");
            if (string.IsNullOrEmpty(potionId))
                return;

            string itemCode = buff.Data.GetString("itemCode");
            string potionName = string.IsNullOrEmpty(itemCode) ? potionId : Lang.Get(itemCode);
            context.Description.Append(string.Format("<font color=\"{0}\">", "#b8bb00"));
            if (buff.Data.GetBool("isProjectile"))
                context.Description.Append(Lang.Get("alchemy:arrow-coated", potionName));
            else
                context.Description.Append(
                    Lang.Get("alchemy:weapon-coated", potionName, buff.UsesRemaining ?? 0)
                );
            if (!CompatEntry.isCoatingAllowed(potionId))
                context.Description.Append(" (" + Lang.Get("alchemy:disabled") + ")");
            context.Description.AppendLine("</font>");
        }
    }
}
