using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace EffectLib
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// Storage for a coating carried on an item stack, and the dispatch that applies it to
    /// whatever gets hit. See <see cref="CoatingPolicy"/> for how a mod configures this, and
    /// <see cref="CoatingInteraction"/>/<see cref="BarrelCoating"/> for how a coating gets onto
    /// the item stack in the first place.
    /// </summary>
    public static class CoatedEffects
    {
        // Kept as the Alchemy 2.x names for save compatibility - these live on the item stack's
        // own attributes, not a player's, so they persist in inventories across the split.
        public const string KeyEffectId = "coatedPotionId";
        public const string KeyItemCode = "coatedItemCode";
        public const string KeyMultiplier = "coatMultiplier";
        public const string KeyCharges = "coatCharges";

        /// <summary>A lang key or literal name for a coating's source item, for tooltips and messages.</summary>
        public static string ResolveDisplayName(string itemCodeOrLangKey, string fallback) =>
            string.IsNullOrEmpty(itemCodeOrLangKey) ? fallback : Lang.Get(itemCodeOrLangKey);

        /// <summary>The generic "domain:item-path" / "domain:block-path" convention Vintage Story itself uses for names.</summary>
        public static string DefaultItemCode(CollectibleObject col)
        {
            if (col?.Code == null)
                return "";
            string typePrefix = col is Block ? "block" : "item";
            return $"{col.Code.Domain}:{typePrefix}-{col.Code.Path}";
        }

        // ----- Read/write a coating: Combat Overhaul's buff store when it owns the weapon,
        // otherwise the item stack's attributes. -----

        public static void ReadWeaponCoat(
            ItemStack stack,
            out string effectId,
            out float multiplier,
            out int charges
        )
        {
            if (
                CoatingPolicy.CombatOverhaulManagesWeapon(stack.Collectible)
                && CoatingPolicy.ReadCombatOverhaulCoat(stack) is { } alt
            )
            {
                (effectId, _, multiplier, charges) = alt;
                return;
            }

            ITreeAttribute attrs = stack.Attributes;
            effectId = attrs.GetString(KeyEffectId);
            multiplier = attrs.GetFloat(KeyMultiplier);
            charges = attrs.GetInt(KeyCharges);
        }

        public static void WriteWeaponCoat(
            ItemSlot slot,
            string effectId,
            string itemCode,
            float multiplier,
            int charges
        )
        {
            if (CoatingPolicy.CombatOverhaulManagesWeapon(slot.Itemstack.Collectible))
            {
                CoatingPolicy.WriteCombatOverhaulWeaponCoat(slot, effectId, itemCode, multiplier, charges);
                return;
            }

            ITreeAttribute attrs = slot.Itemstack.Attributes;
            attrs.SetString(KeyEffectId, effectId);
            attrs.SetString(KeyItemCode, itemCode ?? "");
            attrs.SetFloat(KeyMultiplier, multiplier);
            attrs.SetInt(KeyCharges, charges);
            slot.MarkDirty();
        }

        public static bool HasProjectileCoat(ItemStack stack)
        {
            return CoatingPolicy.CombatOverhaulManagesProjectile(stack)
                ? CoatingPolicy.ReadCombatOverhaulCoat(stack) != null
                : !string.IsNullOrEmpty(stack.Attributes.GetString(KeyEffectId));
        }

        public static void WriteProjectileCoat(
            ItemStack stack,
            string effectId,
            string itemCode,
            float multiplier
        )
        {
            if (CoatingPolicy.CombatOverhaulManagesProjectile(stack))
            {
                CoatingPolicy.WriteCombatOverhaulProjectileCoat(stack, effectId, itemCode, multiplier);
                return;
            }

            ITreeAttribute attrs = stack.Attributes;
            attrs.SetString(KeyEffectId, effectId);
            attrs.SetString(KeyItemCode, itemCode ?? "");
            attrs.SetFloat(KeyMultiplier, multiplier);
        }

        // ----- On-hit consumption of a stack-stored coating. Combat Overhaul coatings are not
        // touched here - it delivers those from its own on-hit code. -----

        internal static void ClearStackCoat(ITreeAttribute attrs)
        {
            attrs.RemoveAttribute(KeyEffectId);
            attrs.RemoveAttribute(KeyItemCode);
            attrs.RemoveAttribute(KeyMultiplier);
            attrs.RemoveAttribute(KeyCharges);
        }

        /// <summary>
        /// Consumes one charge of a stack-stored weapon coating on hit, if any is present.
        /// Returns what to apply, or null if there was nothing to consume.
        /// </summary>
        internal static (string EffectId, float Multiplier, string ItemCode)? TryConsumeWeaponCharge(
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

            // Left untouched (not decremented, not cleared) when the effect is currently
            // disallowed, so re-enabling it later leaves the charge available again.
            if (!CoatingPolicy.IsEffectCoatable(effectId))
                return null;

            float multiplier = attrs.GetFloat(KeyMultiplier);
            string itemCode = attrs.GetString(KeyItemCode);

            charges--;
            if (charges <= 0)
                ClearStackCoat(attrs);
            else
                attrs.SetInt(KeyCharges, charges);
            slot.MarkDirty();

            return (effectId, multiplier, itemCode);
        }

        /// <summary>Consumes a stack-stored projectile coating on impact, if any is present.</summary>
        internal static (string EffectId, float Multiplier, string ItemCode)? TryConsumeProjectileCoat(
            ItemStack projectileStack
        )
        {
            ITreeAttribute attrs = projectileStack.Attributes;
            string effectId = attrs.GetString(KeyEffectId);
            if (string.IsNullOrEmpty(effectId))
                return null;

            // Left untouched when the effect is currently disallowed, matching TryConsumeWeaponCharge.
            if (!CoatingPolicy.IsEffectCoatable(effectId))
                return null;

            float multiplier = attrs.GetFloat(KeyMultiplier);
            string itemCode = attrs.GetString(KeyItemCode);
            ClearStackCoat(attrs);

            return (effectId, multiplier, itemCode);
        }

        // ----- Applying a coating's effect to whatever got hit -----

        /// <summary>
        /// Applies a coated effect to <paramref name="entity"/>. Players go through the same
        /// <see cref="EffectManager"/> a consumed potion would; other entities get the closest
        /// generic approximation (instant/ticking health, timed stat modifiers), since they have
        /// no effect manager of their own.
        /// </summary>
        public static void Apply(string effectId, Entity entity, float multiplier, string displayName)
        {
            if (entity == null || !entity.Alive)
                return;

            CoatingPolicy.ApplySideEffects(effectId, entity, multiplier);

            if (entity is EntityPlayer playerEntity)
            {
                EffectManager manager = EntityBehaviorPlayerEffects.ManagerFor(playerEntity);
                EffectContext ctx = manager == null ? null : EffectRegistry.Build(effectId, multiplier);
                if (ctx == null)
                    return;

                string blockReason = CoatingPolicy.GetBlockReason(effectId, playerEntity, ctx);
                if (blockReason != null)
                {
                    (playerEntity.Player as IServerPlayer)?.SendMessage(
                        GlobalConstants.InfoLogChatGroup,
                        Lang.Get(blockReason),
                        EnumChatType.Notification
                    );
                    return;
                }

                if (manager.TryApply(effectId, ctx, displayName))
                {
                    (playerEntity.Player as IServerPlayer)?.SendMessage(
                        GlobalConstants.InfoLogChatGroup,
                        EffectLang.Get(effectId, "effect-gain", displayName),
                        EnumChatType.Notification
                    );
                }
            }
            else if (entity is EntityAgent agent)
            {
                EffectContext ctx = EffectRegistry.Build(effectId, multiplier);
                if (ctx == null)
                    return;

                if (ctx.TickSec > 0 && Math.Abs(ctx.Health) > float.Epsilon)
                    ApplyTickEffect(agent, ctx);
                else if (Math.Abs(ctx.Health) > float.Epsilon)
                    ApplyInstantHealth(agent, ctx);

                if (ctx.StatModifiers.Count > 0 && ctx.Duration > 0)
                    ApplyStatEffect(agent, ctx);
            }
        }

        private static void ApplyInstantHealth(EntityAgent agent, EffectContext ctx)
        {
            agent.ReceiveDamage(
                new DamageSource
                {
                    Source = EnumDamageSource.Internal,
                    Type = ctx.ResolveDamageType(),
                    IgnoreInvFrames = true,
                },
                Math.Abs(ctx.Health)
            );
        }

        private static void ApplyTickEffect(EntityAgent agent, EffectContext ctx)
        {
            if (Math.Abs(ctx.Health) <= float.Epsilon)
                return;

            if (agent.HasBehavior<EntityBehaviorHealthOverTime>())
                agent.GetBehavior<EntityBehaviorHealthOverTime>()
                    .Refresh(ctx.Health, ctx.TickSec, ctx.Duration, ctx.ResolveDamageType());
            else
            {
                EntityBehaviorHealthOverTime b = new(agent);
                agent.AddBehavior(b);
                b.Setup(ctx.Health, ctx.TickSec, ctx.Duration, ctx.ResolveDamageType());
            }
        }

        private static void ApplyStatEffect(EntityAgent agent, EffectContext ctx)
        {
            const string subkey = "weaponcoat";
            foreach (KeyValuePair<string, float> stat in ctx.StatModifiers)
                agent.Stats.Set(stat.Key, subkey, stat.Value, false);

            long agentId = agent.EntityId;
            List<string> effectKeys = [.. ctx.StatModifiers.Keys];
            agent.World.RegisterCallback(
                _ =>
                {
                    if (agent.World.GetEntityById(agentId) is not EntityAgent target)
                        return;
                    foreach (string key in effectKeys)
                        target.Stats.Remove(key, subkey);
                },
                ctx.Duration * 1000
            );
        }
    }
}
