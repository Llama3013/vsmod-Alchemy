﻿using EffectLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Alchemy
{
    public static class PotionConsumableLogic
    {
        private static TagSet coatableWeaponTagSet;
        private static bool coatableWeaponTagSetCached;

        public const string AttributeKey = "effectinfo";

        public const float IntoxicationMax = 1.1f;
        public const float PsychedelicMax = 2.0f;

        public static bool TryReadPotionId(CollectibleObject collectible, out string potionId)
        {
            JsonObject potion = collectible?.Attributes?[AttributeKey];
            potionId = potion?.Exists == true ? potion["effectId"].AsString()?.ToLowerInvariant() : null;

            if (string.IsNullOrWhiteSpace(potionId))
            {
                potionId = null;
                return false;
            }

            return true;
        }

        public static bool TryReadPotionInfo(
            ItemStack stack,
            out string potionId,
            out string strength
        )
        {
            strength = "weak";

            if (!TryReadPotionId(stack?.Collectible, out potionId))
            {
                potionId = null;
                return false;
            }

            stack.Collectible?.Variant?.TryGetValue("strength", out strength);
            strength ??= "weak";

            return true;
        }

        // The ~25 built-in potions are reserved in EffectRegistry at startup (see
        // AlchemyMod.RegisterEffectsOnce), so this is exactly "is this one of them, with
        // delivery driven by the Allow{Drinking,Throwing,Coating}<Name> config" vs. "everything
        // else, JSON-defined or not, with delivery driven by EffectRegistry's generic channels".
        private static bool IsCodeOwned(string potionId) => EffectRegistry.IsReserved(potionId);

        // Drinking and throwing default to allowed (an effect that declares no channels allows
        // all of them); coating does not, since most potions never opt into it - see
        // EffectRegistry.HasExplicitChannels.
        internal static bool IsDrinkingAllowed(string potionId) =>
            IsCodeOwned(potionId)
                ? PotionDefinitions.AllowsDrinking(potionId)
                : EffectRegistry.AllowsChannel(potionId, "drink");

        internal static bool IsThrowableAllowed(string potionId) =>
            IsCodeOwned(potionId)
                ? PotionDefinitions.AllowsThrowing(potionId)
                : EffectRegistry.AllowsChannel(potionId, "throw");

        internal static bool IsCoatingAllowed(string potionId) =>
            IsCodeOwned(potionId)
                ? PotionDefinitions.AllowsCoating(potionId)
                : EffectRegistry.HasExplicitChannels(potionId)
                    && EffectRegistry.AllowsChannel(potionId, "coat");

        internal static string GetPotionGroup(string potionId) =>
            IsCodeOwned(potionId)
                ? PotionDefinitions.GroupOf(potionId)
                : EffectRegistry.GroupOf(potionId) ?? "none";

        // The effect manager is the only record of what is running, so ask it rather than
        // scanning attribute names.
        internal static HashSet<string> GetActivePotionIds(EntityPlayer player)
        {
            EffectManager manager = player?.GetBehavior<EntityBehaviorPlayerEffects>()?.Manager;
            return manager == null
                ? new(StringComparer.OrdinalIgnoreCase)
                : new(manager.ActiveIds, StringComparer.OrdinalIgnoreCase);
        }

        internal static string CheckPotionExclusivity(EntityPlayer player, string incomingPotionId)
        {
            if (!AlchemyConfig.Loaded.AllowPotionExclusivity)
                return null;

            string incomingGroup = GetPotionGroup(incomingPotionId);
            if (string.Equals(incomingGroup, "none", StringComparison.OrdinalIgnoreCase))
                return null;

            HashSet<string> active = GetActivePotionIds(player);
            active.Remove(incomingPotionId);

            // Only potions that participate in exclusivity (not "none") can conflict.
            List<string> activeGroups =
            [
                .. active
                    .Select(GetPotionGroup)
                    .Where(g => !string.Equals(g, "none", StringComparison.OrdinalIgnoreCase)),
            ];
            if (activeGroups.Count == 0)
                return null;

            if (string.Equals(incomingGroup, "solo", StringComparison.OrdinalIgnoreCase))
                return "alchemy:exclusivity-solo-incoming";

            if (activeGroups.Any(g => string.Equals(g, "solo", StringComparison.OrdinalIgnoreCase)))
                return "alchemy:exclusivity-solo-active";

            if (
                activeGroups.Any(g =>
                    string.Equals(g, incomingGroup, StringComparison.OrdinalIgnoreCase)
                )
            )
                return "alchemy:exclusivity-group-conflict";

            return null;
        }

        public static float GetStrengthMultiplier(string strength)
        {
            return strength switch
            {
                "strong" => AlchemyConfig.Loaded.StrongPotionMultiplier,
                "medium" => AlchemyConfig.Loaded.MediumPotionMultiplier,
                _ => AlchemyConfig.Loaded.WeakPotionMultiplier,
            };
        }

        public static (
            float damage,
            float intox,
            float psych,
            float satLoss
        ) GetDrinkingSideEffectTotals(string potionId, float strengthMul)
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;

            (float damage, float intox, float psych, float satLoss) =
                PotionDefinitions.DrinkingSideEffects(potionId);

            float mul = AlchemyConfig.Loaded.SideEffectStrengthMultiplier ? strengthMul : 1f;
            float totalIntoxChange = cfg.DrinkingPotionIntoxicationAmount + intox * mul;
            float totalPsychChange = cfg.DrinkingPotionPsychedelicAmount + psych * mul;
            float totalSatChange = cfg.DrinkingPotionSaturationLossAmount + satLoss * mul;
            float totalHealthChange = cfg.DrinkingPotionDamageAmount + damage * mul;

            return (totalHealthChange, totalIntoxChange, totalPsychChange, totalSatChange);
        }

        internal static void ApplySideEffects(Entity entity, string potionId, float strengthMul)
        {
            (
                float totalHealthChange,
                float totalIntoxChange,
                float totalPsychChange,
                float totalSatChange
            ) = GetDrinkingSideEffectTotals(potionId, strengthMul);

            if (entity is EntityPlayer playerEntity)
            {
                if (Math.Abs(totalIntoxChange) > float.Epsilon)
                {
                    float current = playerEntity.WatchedAttributes.GetFloat("intoxication");
                    playerEntity.WatchedAttributes.SetFloat(
                        "intoxication",
                        Math.Clamp(current + totalIntoxChange, 0f, IntoxicationMax)
                    );
                }
                if (Math.Abs(totalPsychChange) > float.Epsilon)
                {
                    float current = playerEntity.WatchedAttributes.GetFloat("psychedelic");
                    playerEntity.WatchedAttributes.SetFloat(
                        "psychedelic",
                        Math.Clamp(current + totalPsychChange, 0f, PsychedelicMax)
                    );
                }
                if (Math.Abs(totalSatChange) > float.Epsilon)
                    playerEntity.ReceiveSaturation(totalSatChange);
            }

            if (Math.Abs(totalHealthChange) > float.Epsilon)
                entity.ReceiveDamage(
                    new()
                    {
                        Source = EnumDamageSource.Internal,
                        Type = totalHealthChange < float.Epsilon ? EnumDamageType.Heal : EnumDamageType.Poison,
                        IgnoreInvFrames = true,
                    },
                    Math.Abs(totalHealthChange)
                );
        }

        internal static bool HasWeaponTag(ICoreAPI api, CollectibleObject col)
        {
            if (!coatableWeaponTagSetCached)
            {
                List<string> tagList =
                [
                    .. AlchemyConfig
                        .Loaded.CoatableWeaponTags.Split(',')
                        .Select(t => t.Trim())
                        .Where(t => t.Length > 0),
                ];
                api.CollectibleTagRegistry.TryCreateTagSet(out coatableWeaponTagSet, tagList);
                coatableWeaponTagSetCached = true;
            }
            return col.Tags.Overlaps(coatableWeaponTagSet);
        }

        public static bool IsCoatableProjectile(CollectibleObject col)
        {
            if (col?.Code == null)
                return false;

            string[] codes =
            [
                .. AlchemyConfig
                    .Loaded.CoatableProjectilesCodes.Split(',')
                    .Select(c => c.Trim())
                    .Where(c => c.Length > 0),
            ];
            return WildcardUtil.Match(codes, col.Code.ToString());
        }

        // Combines TryReadPotionInfo, IsDrinkingAllowed and GetStrengthMultiplier for a
        // resolved stack - the item/liquid consumable behaviors differ only in how they get to
        // that stack in the first place, so everything past that point is shared here.
        // Resolves what a stack is - not whether the caller's delivery method may use it; that
        // is a separate per-channel question (IsDrinkingAllowed/IsThrowableAllowed/IsCoatingAllowed)
        // each behavior checks for itself, since the same potion can be coat-only, throw-only,
        // or any combination. A JSON-only potion (not code-owned) registers itself here the first
        // time anything resolves it, the same way EffectLib's own generic behaviors self-register
        // from an "effectinfo" attribute - this is what lets a coat-only, non-drinkable potion
        // like throwableacid.json's still work without ever going through a drink interaction.
        internal static bool TryResolvePotion(
            ItemStack stack,
            out string potionId,
            out float potencyMul
        )
        {
            potencyMul = 1f;

            if (!TryReadPotionInfo(stack, out potionId, out string strength))
            {
                potionId = null;
                return false;
            }

            potencyMul = GetStrengthMultiplier(strength);

            if (!EffectRegistry.IsRegistered(potionId))
            {
                JsonObject def = stack.Collectible?.Attributes?[AttributeKey];
                if (def?.Exists == true)
                    JsonEffectDefinition.RegisterFrom(
                        potionId,
                        PotionEffects.Domain,
                        def,
                        stack.Collectible.Code
                    );
            }

            return true;
        }

        // Consume-time scaling by healing effectiveness is identical for drinking and eating -
        // only the base time (config-driven, per source) differs.
        internal static float ScaleConsumeTime(float baseTime, EntityAgent byEntity)
        {
            if (!AlchemyConfig.Loaded.ScalePotionTimeWithHealing)
                return baseTime;

            float maxTime = baseTime * AlchemyConfig.Loaded.PotionConsumeMaxTimeMultiplier;
            float healingEffectiveness = byEntity.Stats.GetBlended("healingeffectivness");
            healingEffectiveness = Math.Clamp(healingEffectiveness, 0, 2) - 1;

            if (healingEffectiveness < 0)
                return baseTime + (baseTime - maxTime) * healingEffectiveness;
            if (healingEffectiveness > 0)
                return baseTime * (1 - healingEffectiveness);
            return baseTime;
        }

        private static bool IsReshapeReentry(EntityAgent byEntity, EffectContext ctx) =>
            ctx?.Reshape == true && byEntity.WatchedAttributes.GetBool("allowcharselonce");

        private static bool IsRecallOnVessel(EntityAgent byEntity, EffectContext ctx) =>
            ctx?.Respawn == true
            && byEntity.MountedOn?.MountSupplier?.OnEntity?.HasBehavior("seatable") == true;

        private static bool IsPotionAlreadyActive(EntityAgent byEntity, string potionId)
        {
            if (byEntity is not EntityPlayer player)
                return false;

            EffectManager manager = player.GetBehavior<EntityBehaviorPlayerEffects>()?.Manager;

            if (manager?.CanRefresh(potionId) == true)
                return false;

            return manager?.IsActive(potionId) == true;
        }

        private static bool IsAnyPotionActiveAndLimited(EntityAgent byEntity)
        {
            if (!AlchemyConfig.Loaded.OnlyOnePotionAtATime)
                return false;

            if (byEntity is not EntityPlayer player)
                return false;

            return player.GetBehavior<EntityBehaviorPlayerEffects>()?.Manager.HasAnyActive == true;
        }

        // A lang key naming why this potion should be refused, or null to allow it. Shared by
        // both consumable behaviors - neither the reshape/vessel edge cases nor the
        // already-active/limit/exclusivity checks depend on where the potion came from.
        internal static string GetPotionBlockReason(
            EntityAgent byEntity,
            string potionId,
            EffectContext ctx
        )
        {
            if (byEntity.World.Side != EnumAppSide.Server)
                return null;

            if (IsReshapeReentry(byEntity, ctx))
                return "alchemy:reshape-block";
            if (IsRecallOnVessel(byEntity, ctx))
                return "alchemy:boat-block";

            // A purging potion always goes through - refusing it over an effect it is about to
            // clear anyway would be surprising.
            if (ctx.ResetsEffects)
                return null;

            if (IsPotionAlreadyActive(byEntity, potionId))
                return "alchemy:potion-already-active";
            if (IsAnyPotionActiveAndLimited(byEntity))
                return "alchemy:potion-limit-active";

            if (byEntity is EntityPlayer exclusivityPlayer)
            {
                string exclusivityBlock = CheckPotionExclusivity(exclusivityPlayer, potionId);
                if (exclusivityBlock != null)
                    return exclusivityBlock;
            }

            return null;
        }

        // A coated hit only ever checks exclusivity, not the reshape/vessel/already-active/limit
        // cases GetPotionBlockReason also covers - none of those make sense for a repeatable
        // weapon hit (a poisoned blade should be able to re-poison the same target).
        internal static string GetCoatingBlockReason(
            EntityPlayer player,
            string potionId,
            EffectContext ctx
        ) => ctx.ResetsEffects ? null : CheckPotionExclusivity(player, potionId);

        // Purges, gates on a size change, applies the effect, then the drinking side effects
        // and the gain message. Shared by both consumable behaviors.
        internal static bool ApplyPotionEffect(
            EntityAgent byEntity,
            string potionId,
            EffectContext ctx,
            string displayName
        )
        {
            if (
                byEntity is not EntityPlayer playerEntity
                || playerEntity.Player is not IServerPlayer serverPlayer
            )
                return false;

            EntityBehaviorPlayerEffects behavior = playerEntity.GetBehavior<EntityBehaviorPlayerEffects>();
            if (behavior == null)
                return false;

            if (ctx.ResetsEffects)
            {
                // Scoped to Alchemy's own effects unless the potion names other domains, so a
                // purging brew never wipes effects belonging to another mod.
                behavior.Manager.PurgeFor(potionId, ctx);
            }

            if (
                Math.Abs(ctx.SizeChange) > float.Epsilon
                && !UtilityEffects.CanApplySizeChange(playerEntity, ctx.SizeChange)
            )
            {
                serverPlayer.SendMessage(
                    GlobalConstants.InfoLogChatGroup,
                    Lang.Get(ctx.SizeChange > 0 ? "alchemy:size-at-max" : "alchemy:size-at-min"),
                    EnumChatType.Notification
                );
                return false;
            }

            if (!behavior.Manager.TryApply(potionId, ctx, displayName))
                return false;

            ApplySideEffects(playerEntity, potionId, ctx.PotencyMul);

            serverPlayer.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                Lang.Get(ctx.Reshape ? "alchemy:reshape-gain" : "alchemy:effect-gain", displayName),
                EnumChatType.Notification
            );

            return true;
        }

        // The full tooltip for a resolved potion. Shared by both consumable behaviors, which
        // differ only in how they resolve (potionId, potencyMul) in the first place.
        internal static void AppendPotionTooltip(StringBuilder dsc, string potionId, float potencyMul)
        {
            EffectContext ctx = EffectRegistry.Build(potionId, potencyMul);
            if (ctx == null)
                return;

            if (ctx.StatModifiers != null)
            {
                int headerStart = dsc.Length;
                dsc.AppendLine(Lang.Get("alchemy:potion-when-used"));
                int headerEnd = dsc.Length;
                if (ctx.StatModifiers.TryGetValue("rangedWeaponsAcc", out float rWvalue) && rWvalue != 0f)
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-archer-accuracy-effect",
                            Math.Round(rWvalue * 100, 0)
                        )
                    );
                if (
                    ctx.StatModifiers.TryGetValue("animalLootDropRate", out float aLValue)
                    && aLValue != 0f
                )
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-animal-loot-effect", Math.Round(aLValue * 100, 0))
                    );
                if (
                    ctx.StatModifiers.TryGetValue("animalHarvestingTime", out float ahValue)
                    && ahValue != 0f
                )
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-animal-harvest-effect",
                            Math.Round(ahValue * 100, 0)
                        )
                    );
                if (
                    ctx.StatModifiers.TryGetValue("animalSeekingRange", out float aSValue)
                    && aSValue != 0f
                )
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-animal-seek-effect", Math.Round(aSValue * 100, 0))
                    );
                if (
                    ctx.StatModifiers.TryGetValue("maxhealthExtraPoints", out float mHEValue)
                    && mHEValue != 0f
                )
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-max-health-effect", Math.Round(mHEValue * 100, 0))
                    );
                if (ctx.StatModifiers.TryGetValue("forageDropRate", out float fDValue) && fDValue != 0f)
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-forage-amount-effect",
                            Math.Round(fDValue * 100, 0)
                        )
                    );
                if (
                    ctx.StatModifiers.TryGetValue("healingeffectivness", out float hEValue)
                    && hEValue != 0f
                )
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-heal-effectiveness-effect",
                            Math.Round(hEValue * 100, 0)
                        )
                    );
                if (ctx.StatModifiers.TryGetValue("hungerrate", out float hRValue) && hRValue != 0f)
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-hunger-rate-effect", Math.Round(hRValue * 100, 0))
                    );
                if (
                    ctx.StatModifiers.TryGetValue("meleeWeaponsDamage", out float mWValue)
                    && mWValue != 0f
                )
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-melee-damage-effect", Math.Round(mWValue * 100, 0))
                    );
                if (
                    ctx.StatModifiers.TryGetValue("mechanicalsDamage", out float mDValue)
                    && mDValue != 0f
                )
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-mech-damage-effect", Math.Round(mDValue * 100, 0))
                    );
                if (ctx.StatModifiers.TryGetValue("miningSpeedMul", out float mSValue) && mSValue != 0f)
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-mining-speed-effect", Math.Round(mSValue * 100, 0))
                    );
                if (ctx.StatModifiers.TryGetValue("oreDropRate", out float oDValue) && oDValue != 0f)
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-ore-amount-effect", Math.Round(oDValue * 100, 0))
                    );
                if (
                    ctx.StatModifiers.TryGetValue("rangedWeaponsDamage", out float rWDValue)
                    && rWDValue != 0f
                )
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-archer-damage-effect",
                            Math.Round(rWDValue * 100, 0)
                        )
                    );
                if (
                    ctx.StatModifiers.TryGetValue("rangedWeaponsSpeed", out float rWSValue)
                    && rWSValue != 0f
                )
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-archer-speed-effect",
                            Math.Round(rWSValue * 100, 0)
                        )
                    );
                if (
                    ctx.StatModifiers.TryGetValue("rustyGearDropRate", out float rGDValue)
                    && rGDValue != 0f
                )
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-gear-amount-effect", Math.Round(rGDValue * 100, 0))
                    );
                if (ctx.StatModifiers.TryGetValue("walkspeed", out float wSValue) && wSValue != 0f)
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-walk-speed-effect", Math.Round(wSValue * 100, 0))
                    );
                if (
                    ctx.StatModifiers.TryGetValue("vesselContentsDropRate", out float vCDValue)
                    && vCDValue != 0f
                )
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-vessel-amount-effect",
                            Math.Round(vCDValue * 100, 0)
                        )
                    );
                if (
                    ctx.StatModifiers.TryGetValue("wildCropDropRate", out float wCDValue)
                    && wCDValue != 0f
                )
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-wild-crop-effect", Math.Round(wCDValue * 100, 0))
                    );
                if (
                    ctx.StatModifiers.TryGetValue("wholeVesselLootChance", out float wVLValue)
                    && wVLValue != 0f
                )
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-whole-vessel-effect",
                            Math.Round(wVLValue * 100, 0)
                        )
                    );
                if (
                    ctx.StatModifiers.TryGetValue("health", out float healthValue)
                    && healthValue is > 0.01f or < -0.01f
                )
                    dsc.AppendLine(Lang.Get("alchemy:potion-single-health-effect", healthValue));

                if (ctx.Respawn)
                    dsc.AppendLine(Lang.Get("alchemy:potion-recall-effect"));
                if (ctx.GlowStrength > 0)
                    dsc.AppendLine(Lang.Get("alchemy:potion-glow-effect"));
                if (ctx.WaterBreathe)
                    dsc.AppendLine(Lang.Get("alchemy:potion-waterbreathe-effect"));
                if (ctx.ColdResist)
                    dsc.AppendLine(Lang.Get("alchemy:potion-coldresist-effect"));
                if (ctx.TemporalStabilityGain > 0)
                    dsc.AppendLine(Lang.Get("alchemy:potion-temporal-effect"));
                if (ctx.RetainedNutrition > 0)
                    dsc.AppendLine(Lang.Get("alchemy:potion-nutrition-effect"));
                if (ctx.Reshape)
                    dsc.AppendLine(Lang.Get("alchemy:potion-reshape-effect"));
                if (ctx.SizeChange > 0)
                    dsc.AppendLine(Lang.Get("alchemy:potion-grow-effect"));
                if (ctx.SizeChange < 0)
                    dsc.AppendLine(Lang.Get("alchemy:potion-shrink-effect"));
                if (ctx.FallDamageReduction > 0)
                    dsc.AppendLine(Lang.Get("alchemy:potion-fall-effect"));
                if (ctx.CanClimbAnywhere)
                    dsc.AppendLine(Lang.Get("alchemy:potion-climb-effect"));
                if (ctx.CanFly)
                    dsc.AppendLine(Lang.Get("alchemy:potion-flight-effect"));
                if (ctx.NoGravity)
                    dsc.AppendLine(Lang.Get("alchemy:potion-nogravity-effect"));
                if (Math.Abs(ctx.KnockbackResistance) > float.Epsilon)
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-knockback-resist-effect",
                            $"{ctx.KnockbackResistance * 100:+0;-0;0}"
                        )
                    );
                if (ctx.NoFallDamage)
                    dsc.AppendLine(Lang.Get("alchemy:potion-no-fall-damage-effect"));
                if (ctx.DisableClimbing)
                    dsc.AppendLine(Lang.Get("alchemy:potion-no-climb-effect"));
                if (Math.Abs(ctx.ClimbTouchDistance) > float.Epsilon)
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-climb-reach-effect",
                            $"{ctx.ClimbTouchDistance:+0.##;-0.##;0}"
                        )
                    );
                if (Math.Abs(ctx.Weight) > float.Epsilon)
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-weight-effect", $"{ctx.Weight:+0.#;-0.#;0}")
                    );
                if (ctx.ResetsEffects)
                    dsc.AppendLine(Lang.Get("alchemy:potion-purge-effect"));

                if (dsc.Length == headerEnd)
                    dsc.Remove(headerStart, headerEnd - headerStart);
            }

            if (ctx.Health is > 0.01f or < -0.01f)
                dsc.AppendLine(Lang.Get("alchemy:potion-health-effect", Math.Round(ctx.Health, 2)));
            if (ctx.TickSec != 0)
                dsc.AppendLine(Lang.Get("alchemy:potion-tick-duration", ctx.TickSec));
            if (ctx.IsEndless)
                dsc.AppendLine(Lang.Get("effectlib:duration-endless"));
            else if (ctx.Duration != 0)
                dsc.AppendLine(Lang.Get("alchemy:potion-duration", ctx.Duration));

            (float sideDamage, float sideIntox, float sidePsych, float sideSatLoss) =
                GetDrinkingSideEffectTotals(potionId, potencyMul);
            if (
                Math.Abs(sideDamage) > float.Epsilon
                || Math.Abs(sideIntox) > float.Epsilon
                || Math.Abs(sidePsych) > float.Epsilon
                || Math.Abs(sideSatLoss) > float.Epsilon
            )
            {
                dsc.AppendLine(Lang.Get("alchemy:potion-side-effects-header"));
                if (Math.Abs(sideDamage) > float.Epsilon)
                    dsc.AppendLine(
                        Lang.Get(
                            sideDamage < 0
                                ? "alchemy:potion-side-heal-effect"
                                : "alchemy:potion-side-damage-effect",
                            Math.Round(Math.Abs(sideDamage), 2)
                        )
                    );
                if (Math.Abs(sideIntox) > float.Epsilon)
                    dsc.AppendLine(
                        Lang.Get(
                            sideIntox < 0
                                ? "alchemy:potion-side-intoxication-reduce-effect"
                                : "alchemy:potion-side-intoxication-effect",
                            Math.Round(
                                Math.Abs(sideIntox) / IntoxicationMax * 100,
                                0
                            )
                        )
                    );
                if (Math.Abs(sidePsych) > float.Epsilon)
                    dsc.AppendLine(
                        Lang.Get(
                            sidePsych < 0
                                ? "alchemy:potion-side-psychedelic-reduce-effect"
                                : "alchemy:potion-side-psychedelic-effect",
                            Math.Round(
                                Math.Abs(sidePsych) / PsychedelicMax * 100,
                                0
                            )
                        )
                    );
                if (Math.Abs(sideSatLoss) > float.Epsilon)
                    dsc.AppendLine(
                        Lang.Get(
                            sideSatLoss < 0
                                ? "alchemy:potion-side-saturation-loss-effect"
                                : "alchemy:potion-side-saturation-gain-effect",
                            Math.Round(Math.Abs(sideSatLoss), 0)
                        )
                    );
            }
        }
    }
}
