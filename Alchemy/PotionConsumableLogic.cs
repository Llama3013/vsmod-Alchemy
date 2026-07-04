using System;
using System.Collections.Generic;
using System.Linq;
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
        private static readonly Dictionary<long, long> coatHoldStartMs = [];
        private static readonly HashSet<long> coatNotifiedEntities = [];
        private static TagSet coatableWeaponTagSet;
        private static bool coatableWeaponTagSetCached;

        public const float CoatHoldDurationSec = 1.5f;
        public const float DefaultConsumeTime = 1.5f;

        public const float IntoxicationMax = 1.1f;
        public const float PsychedelicMax = 2.0f;

        public static bool TryReadPotionInfo(
            ItemStack stack,
            out string potionId,
            out string strength
        )
        {
            potionId = null;
            strength = "weak";

            JsonObject potion = stack?.ItemAttributes?["potioninfo"];
            potionId = potion?.Exists == true ? potion["potionId"].AsString() : null;

            if (string.IsNullOrWhiteSpace(potionId))
            {
                potionId = null;
                return false;
            }

            stack.Collectible?.Variant?.TryGetValue("strength", out strength);
            strength ??= "weak";

            return true;
        }

        public static bool HasEnoughSource(
            CollectibleObject collObj,
            string source,
            ItemSlot slot,
            float checkLitres
        )
        {
            if (source != "liquidcontent")
                return slot?.Itemstack?.StackSize >= 1;

            if (collObj is not BlockLiquidContainerBase container)
                return false;

            return container.GetCurrentLitres(slot.Itemstack) >= checkLitres;
        }

        public static bool ConsumeSource(
            CollectibleObject collObj,
            string source,
            ItemSlot slot,
            EntityAgent byEntity,
            float consumeLitres
        )
        {
            if (source == "liquidcontent")
            {
                if (collObj is not BlockLiquidContainerBase container)
                    return false;

                EntityPlayer player = byEntity as EntityPlayer;
                int consumed = container.SplitStackAndPerformAction(
                    player,
                    slot,
                    stack => container.TryTakeLiquid(stack, consumeLitres)?.StackSize ?? 0
                );
                slot.MarkDirty();
                player?.Player?.InventoryManager?.BroadcastHotbarSlot();
                return consumed > 0;
            }

            ItemStack taken = slot.TakeOut(1);
            slot.MarkDirty();
            return taken?.StackSize > 0;
        }

        internal static bool IsThrowableAllowed(string potionId) => IsCoatingAllowed(potionId);

        internal static bool IsCoatingAllowed(string potionId)
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;
            return potionId switch
            {
                "archerpotionid" => cfg.AllowCoatingArcher,
                "healingeffectpotionid" => cfg.AllowCoatingHealingEffect,
                "hungerenhancepotionid" => cfg.AllowCoatingHungerEnhance,
                "hungersupresspotionid" => cfg.AllowCoatingHungerSupress,
                "hunterpotionid" => cfg.AllowCoatingHunter,
                "looterpotionid" => cfg.AllowCoatingLooter,
                "meleepotionid" => cfg.AllowCoatingMelee,
                "miningpotionid" => cfg.AllowCoatingMining,
                "poisontickpotionid" => cfg.AllowCoatingPoison,
                "predatorpotionid" => cfg.AllowCoatingPredator,
                "regentickpotionid" => cfg.AllowCoatingRegen,
                "scentmaskpotionid" => cfg.AllowCoatingScentMask,
                "speedpotionid" => cfg.AllowCoatingSpeed,
                "vitalitypotionid" => cfg.AllowCoatingVitality,
                "recallpotionid" => cfg.AllowCoatingRecall,
                "glowpotionid" => cfg.AllowCoatingGlow,
                "waterbreathepotionid" => cfg.AllowCoatingWaterBreathe,
                "coldresistpotionid" => cfg.AllowCoatingColdResist,
                "nutritionpotionid" => cfg.AllowCoatingNutrition,
                "temporalpotionid" => cfg.AllowCoatingTemporal,
                "reshapepotionid" => cfg.AllowCoatingReshape,
                "growpotionid" => cfg.AllowCoatingGrow,
                "shrinkpotionid" => cfg.AllowCoatingShrink,
                "fallpotionid" => cfg.AllowCoatingFall,
                "climbpotionid" => cfg.AllowCoatingClimb,
                "flightpotionid" => cfg.AllowCoatingFlight,
                _ => false,
            };
        }

        internal static string GetPotionGroup(string potionId)
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;
            return potionId switch
            {
                "archerpotionid" => cfg.ArcherPotionGroup,
                "healingeffectpotionid" => cfg.HealingEffectPotionGroup,
                "hungerenhancepotionid" => cfg.HungerEnhancePotionGroup,
                "hungersupresspotionid" => cfg.HungerSupressPotionGroup,
                "hunterpotionid" => cfg.HunterPotionGroup,
                "looterpotionid" => cfg.LooterPotionGroup,
                "meleepotionid" => cfg.MeleePotionGroup,
                "miningpotionid" => cfg.MiningPotionGroup,
                "poisontickpotionid" => cfg.PoisonPotionGroup,
                "predatorpotionid" => cfg.PredatorPotionGroup,
                "regentickpotionid" => cfg.RegenPotionGroup,
                "scentmaskpotionid" => cfg.ScentMaskPotionGroup,
                "speedpotionid" => cfg.SpeedPotionGroup,
                "vitalitypotionid" => cfg.VitalityPotionGroup,
                "recallpotionid" => cfg.RecallPotionGroup,
                "glowpotionid" => cfg.GlowPotionGroup,
                "waterbreathepotionid" => cfg.WaterBreathePotionGroup,
                "coldresistpotionid" => cfg.ColdResistPotionGroup,
                "nutritionpotionid" => cfg.NutritionPotionGroup,
                "temporalpotionid" => cfg.TemporalPotionGroup,
                "reshapepotionid" => cfg.ReshapePotionGroup,
                "growpotionid" => cfg.GrowPotionGroup,
                "shrinkpotionid" => cfg.ShrinkPotionGroup,
                "fallpotionid" => cfg.FallPotionGroup,
                "climbpotionid" => cfg.ClimbPotionGroup,
                "flightpotionid" => cfg.FlightPotionGroup,
                _ => "none",
            };
        }

        internal static HashSet<string> GetActivePotionIds(EntityPlayer player)
        {
            HashSet<string> result = new(StringComparer.OrdinalIgnoreCase);
            if (player?.WatchedAttributes == null)
                return result;

            foreach (string key in player.WatchedAttributes.Keys)
            {
                if (
                    key.EndsWith("potionid", StringComparison.OrdinalIgnoreCase)
                    && player.WatchedAttributes.GetLong(key) != 0
                )
                    result.Add(key);
            }
            return result;
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

        public static bool HandleWeaponCoatingIdle(
            ICoreAPI api,
            ItemSlot coatSlot,
            EntityAgent byEntity,
            string potionId,
            string strength,
            string itemCode,
            System.Func<ItemSlot, bool> consumeCoating,
            float consumeTime = CoatHoldDurationSec
        )
        {
            if (!AlchemyConfig.Loaded.AllowWeaponCoating)
                return false;

            bool eligible =
                byEntity.LeftHandItemSlot == coatSlot
                && byEntity.Controls.RightMouseDown
                && byEntity.Controls.ShiftKey
                && byEntity.Controls.HandUse == EnumHandInteract.None;

            if (byEntity.World.Side == EnumAppSide.Client)
            {
                HandleClientAnimation(api, byEntity, potionId, eligible);
                return false;
            }

            return HandleServerCoating(
                api,
                coatSlot,
                byEntity,
                potionId,
                strength,
                eligible,
                itemCode,
                consumeCoating,
                consumeTime
            );
        }

        private static void HandleClientAnimation(
            ICoreAPI api,
            EntityAgent byEntity,
            string potionId,
            bool eligible
        )
        {
            if (!eligible)
                return;

            ItemSlot mainSlot = byEntity.RightHandItemSlot;

            if (
                mainSlot?.Itemstack == null
                || string.IsNullOrEmpty(potionId)
                || !IsCoatingAllowed(potionId)
            )
                return;

            CollectibleObject col = mainSlot.Itemstack.Collectible;

            bool isArrow = IsCoatableProjectile(col);

            if (!isArrow && !HasWeaponTag(api, col))
                return;

            bool coatable;
            if (isArrow)
            {
                coatable = CombatOverhaulCompat.ShouldUseProjectileBuffStorage(mainSlot.Itemstack)
                    ? !CombatOverhaulCompat.TryGetCoating(
                        mainSlot.Itemstack,
                        out _,
                        out _,
                        out _,
                        out _
                    )
                    : string.IsNullOrEmpty(
                        mainSlot.Itemstack.Attributes.GetString("coatedPotionId")
                    );
            }
            else
            {
                int charges = mainSlot.Itemstack.Attributes.GetInt("coatCharges");
                if (
                    CombatOverhaulCompat.ShouldUseBuffStorage(col)
                    && CombatOverhaulCompat.TryGetCoating(
                        mainSlot.Itemstack,
                        out _,
                        out _,
                        out _,
                        out int buffCharges
                    )
                )
                    charges = buffCharges;
                coatable = charges < AlchemyConfig.Loaded.WeaponCoatCharges;
            }

            if (coatable)
            {
                byEntity.AnimManager?.StartAnimation("eat");
            }
        }

        private static bool HandleServerCoating(
            ICoreAPI api,
            ItemSlot coatSlot,
            EntityAgent byEntity,
            string potionId,
            string strength,
            bool eligible,
            string itemCode,
            System.Func<ItemSlot, bool> consumeCoating,
            float consumeTime
        )
        {
            long entityId = byEntity.EntityId;

            if (!eligible)
            {
                coatHoldStartMs.Remove(entityId);
                coatNotifiedEntities.Remove(entityId);
                return false;
            }

            ItemSlot mainHandSlot = byEntity.RightHandItemSlot;

            if (mainHandSlot?.Itemstack == null)
            {
                coatHoldStartMs.Remove(entityId);
                return false;
            }

            CollectibleObject col = mainHandSlot.Itemstack.Collectible;

            bool isArrow = IsCoatableProjectile(col);

            if (!isArrow && !HasWeaponTag(api, col))
            {
                coatHoldStartMs.Remove(entityId);
                return false;
            }

            if (string.IsNullOrEmpty(potionId) || !IsCoatingAllowed(potionId))
            {
                if (
                    coatNotifiedEntities.Add(entityId)
                    && byEntity is EntityPlayer { Player: IServerPlayer serverPlayer }
                )
                    serverPlayer.SendMessage(
                        GlobalConstants.InfoLogChatGroup,
                        Lang.Get("alchemy:coating-not-allowed"),
                        EnumChatType.Notification
                    );
                coatHoldStartMs.Remove(entityId);
                return false;
            }

            if (!coatHoldStartMs.TryGetValue(entityId, out long startMs))
            {
                coatHoldStartMs[entityId] = Environment.TickCount64;
                return false;
            }

            // I couldn't find a better way to handle the timing of the coating action while
            // using OnHeldIdle and couldn't derive an action on the offhand for
            // OnHeldInteract, so this is a bit jank but it works. Basically I check if the
            // player has been holding the coating for long enough, and if so I apply the coating
            // and consume the potion. If they stop holding before the time is up then nothing
            // happens and they can try again.
            if ((Environment.TickCount64 - startMs) / 1000f < consumeTime)
                return false;

            coatHoldStartMs.Remove(entityId);

            float strengthMul = GetStrengthMultiplier(strength);

            ApplyCoating(
                coatSlot,
                mainHandSlot,
                byEntity,
                potionId,
                AlchemyConfig.Loaded.WeaponCoatEffectMultiplier * strengthMul,
                itemCode,
                consumeCoating
            );

            return true;
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

            (float damage, float intox, float psych, float satLoss) = potionId switch
            {
                "archerpotionid" => (
                    cfg.ArcherPotionDrinkingDamage,
                    cfg.ArcherPotionDrinkingIntoxication,
                    cfg.ArcherPotionDrinkingPsychedelic,
                    cfg.ArcherPotionDrinkingSaturationLoss
                ),
                "healingeffectpotionid" => (
                    cfg.HealingEffectPotionDrinkingDamage,
                    cfg.HealingEffectPotionDrinkingIntoxication,
                    cfg.HealingEffectPotionDrinkingPsychedelic,
                    cfg.HealingEffectPotionDrinkingSaturationLoss
                ),
                "hungerenhancepotionid" => (
                    cfg.HungerEnhancePotionDrinkingDamage,
                    cfg.HungerEnhancePotionDrinkingIntoxication,
                    cfg.HungerEnhancePotionDrinkingPsychedelic,
                    cfg.HungerEnhancePotionDrinkingSaturationLoss
                ),
                "hungersupresspotionid" => (
                    cfg.HungerSupressPotionDrinkingDamage,
                    cfg.HungerSupressPotionDrinkingIntoxication,
                    cfg.HungerSupressPotionDrinkingPsychedelic,
                    cfg.HungerSupressPotionDrinkingSaturationLoss
                ),
                "hunterpotionid" => (
                    cfg.HunterPotionDrinkingDamage,
                    cfg.HunterPotionDrinkingIntoxication,
                    cfg.HunterPotionDrinkingPsychedelic,
                    cfg.HunterPotionDrinkingSaturationLoss
                ),
                "looterpotionid" => (
                    cfg.LooterPotionDrinkingDamage,
                    cfg.LooterPotionDrinkingIntoxication,
                    cfg.LooterPotionDrinkingPsychedelic,
                    cfg.LooterPotionDrinkingSaturationLoss
                ),
                "meleepotionid" => (
                    cfg.MeleePotionDrinkingDamage,
                    cfg.MeleePotionDrinkingIntoxication,
                    cfg.MeleePotionDrinkingPsychedelic,
                    cfg.MeleePotionDrinkingSaturationLoss
                ),
                "miningpotionid" => (
                    cfg.MiningPotionDrinkingDamage,
                    cfg.MiningPotionDrinkingIntoxication,
                    cfg.MiningPotionDrinkingPsychedelic,
                    cfg.MiningPotionDrinkingSaturationLoss
                ),
                "poisontickpotionid" => (
                    cfg.PoisonPotionDrinkingDamage,
                    cfg.PoisonPotionDrinkingIntoxication,
                    cfg.PoisonPotionDrinkingPsychedelic,
                    cfg.PoisonPotionDrinkingSaturationLoss
                ),
                "predatorpotionid" => (
                    cfg.PredatorPotionDrinkingDamage,
                    cfg.PredatorPotionDrinkingIntoxication,
                    cfg.PredatorPotionDrinkingPsychedelic,
                    cfg.PredatorPotionDrinkingSaturationLoss
                ),
                "regentickpotionid" => (
                    cfg.RegenPotionDrinkingDamage,
                    cfg.RegenPotionDrinkingIntoxication,
                    cfg.RegenPotionDrinkingPsychedelic,
                    cfg.RegenPotionDrinkingSaturationLoss
                ),
                "scentmaskpotionid" => (
                    cfg.ScentMaskPotionDrinkingDamage,
                    cfg.ScentMaskPotionDrinkingIntoxication,
                    cfg.ScentMaskPotionDrinkingPsychedelic,
                    cfg.ScentMaskPotionDrinkingSaturationLoss
                ),
                "speedpotionid" => (
                    cfg.SpeedPotionDrinkingDamage,
                    cfg.SpeedPotionDrinkingIntoxication,
                    cfg.SpeedPotionDrinkingPsychedelic,
                    cfg.SpeedPotionDrinkingSaturationLoss
                ),
                "vitalitypotionid" => (
                    cfg.VitalityPotionDrinkingDamage,
                    cfg.VitalityPotionDrinkingIntoxication,
                    cfg.VitalityPotionDrinkingPsychedelic,
                    cfg.VitalityPotionDrinkingSaturationLoss
                ),
                "glowpotionid" => (
                    cfg.GlowPotionDrinkingDamage,
                    cfg.GlowPotionDrinkingIntoxication,
                    cfg.GlowPotionDrinkingPsychedelic,
                    cfg.GlowPotionDrinkingSaturationLoss
                ),
                "waterbreathepotionid" => (
                    cfg.WaterBreathePotionDrinkingDamage,
                    cfg.WaterBreathePotionDrinkingIntoxication,
                    cfg.WaterBreathePotionDrinkingPsychedelic,
                    cfg.WaterBreathePotionDrinkingSaturationLoss
                ),
                "coldresistpotionid" => (
                    cfg.ColdResistPotionDrinkingDamage,
                    cfg.ColdResistPotionDrinkingIntoxication,
                    cfg.ColdResistPotionDrinkingPsychedelic,
                    cfg.ColdResistPotionDrinkingSaturationLoss
                ),
                "nutritionpotionid" => (
                    cfg.NutritionPotionDrinkingDamage,
                    cfg.NutritionPotionDrinkingIntoxication,
                    cfg.NutritionPotionDrinkingPsychedelic,
                    cfg.NutritionPotionDrinkingSaturationLoss
                ),
                "recallpotionid" => (
                    cfg.RecallPotionDrinkingDamage,
                    cfg.RecallPotionDrinkingIntoxication,
                    cfg.RecallPotionDrinkingPsychedelic,
                    cfg.RecallPotionDrinkingSaturationLoss
                ),
                "temporalpotionid" => (
                    cfg.TemporalPotionDrinkingDamage,
                    cfg.TemporalPotionDrinkingIntoxication,
                    cfg.TemporalPotionDrinkingPsychedelic,
                    cfg.TemporalPotionDrinkingSaturationLoss
                ),
                "reshapepotionid" => (
                    cfg.ReshapePotionDrinkingDamage,
                    cfg.ReshapePotionDrinkingIntoxication,
                    cfg.ReshapePotionDrinkingPsychedelic,
                    cfg.ReshapePotionDrinkingSaturationLoss
                ),
                "growpotionid" => (
                    cfg.GrowPotionDrinkingDamage,
                    cfg.GrowPotionDrinkingIntoxication,
                    cfg.GrowPotionDrinkingPsychedelic,
                    cfg.GrowPotionDrinkingSaturationLoss
                ),
                "shrinkpotionid" => (
                    cfg.ShrinkPotionDrinkingDamage,
                    cfg.ShrinkPotionDrinkingIntoxication,
                    cfg.ShrinkPotionDrinkingPsychedelic,
                    cfg.ShrinkPotionDrinkingSaturationLoss
                ),
                "fallpotionid" => (
                    cfg.FallPotionDrinkingDamage,
                    cfg.FallPotionDrinkingIntoxication,
                    cfg.FallPotionDrinkingPsychedelic,
                    cfg.FallPotionDrinkingSaturationLoss
                ),
                "climbpotionid" => (
                    cfg.ClimbPotionDrinkingDamage,
                    cfg.ClimbPotionDrinkingIntoxication,
                    cfg.ClimbPotionDrinkingPsychedelic,
                    cfg.ClimbPotionDrinkingSaturationLoss
                ),
                "flightpotionid" => (
                    cfg.FlightPotionDrinkingDamage,
                    cfg.FlightPotionDrinkingIntoxication,
                    cfg.FlightPotionDrinkingPsychedelic,
                    cfg.FlightPotionDrinkingSaturationLoss
                ),
                _ => (0f, 0f, 0f, 0f),
            };

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
                    new DamageSource
                    {
                        Source = EnumDamageSource.Internal,
                        Type = totalHealthChange < 0 ? EnumDamageType.Heal : EnumDamageType.Poison,
                    },
                    totalHealthChange
                );
        }

        private static bool HasWeaponTag(ICoreAPI api, CollectibleObject col)
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

        private static bool barrelCoating;

        private static string GetContentLangKey(CollectibleObject col)
        {
            if (col?.Code == null)
                return "";
            string typePrefix = col is Vintagestory.API.Common.Block ? "block" : "item";
            return $"{col.Code.Domain}:{typePrefix}-{col.Code.Path}";
        }

        public static bool TryCoatInBarrel(ICoreAPI api, ItemSlot itemSlot, ItemSlot liquidSlot)
        {
            if (barrelCoating)
                return false;
            if (
                !AlchemyConfig.Loaded.AllowWeaponCoating || !AlchemyConfig.Loaded.AllowBarrelCoating
            )
                return false;
            if (itemSlot?.Itemstack == null || liquidSlot?.Itemstack == null)
                return false;

            if (!TryReadPotionInfo(liquidSlot.Itemstack, out string potionId, out string strength))
                return false;
            if (string.IsNullOrEmpty(potionId) || !IsCoatingAllowed(potionId))
                return false;

            CollectibleObject col = itemSlot.Itemstack.Collectible;
            if (col?.Code == null)
                return false;

            bool isArrow = IsCoatableProjectile(col);
            if (!isArrow && !HasWeaponTag(api, col))
                return false;

            WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(
                liquidSlot.Itemstack
            );
            if (props == null || props.ItemsPerLitre <= 0)
                return false;

            float consumeLitres = AlchemyConfig.Loaded.WeaponCoatConsumeLitres;
            float checkLitres = AlchemyConfig.Loaded.WeaponCoatCheckLitres;
            if (consumeLitres <= 0)
                return false;

            float availableLitres = liquidSlot.Itemstack.StackSize / props.ItemsPerLitre;
            float coatMultiplier =
                AlchemyConfig.Loaded.WeaponCoatEffectMultiplier * GetStrengthMultiplier(strength);
            string itemCode = GetContentLangKey(liquidSlot.Itemstack.Collectible);

            barrelCoating = true;
            try
            {
                if (isArrow)
                    return CoatBarrelArrows(
                        itemSlot,
                        liquidSlot,
                        props,
                        availableLitres,
                        consumeLitres,
                        checkLitres,
                        potionId,
                        itemCode,
                        coatMultiplier
                    );

                return CoatBarrelWeapon(
                    itemSlot,
                    liquidSlot,
                    props,
                    availableLitres,
                    consumeLitres,
                    checkLitres,
                    potionId,
                    itemCode,
                    coatMultiplier
                );
            }
            finally
            {
                barrelCoating = false;
            }
        }

        private static bool CoatBarrelWeapon(
            ItemSlot itemSlot,
            ItemSlot liquidSlot,
            WaterTightContainableProps props,
            float availableLitres,
            float consumeLitres,
            float checkLitres,
            string potionId,
            string itemCode,
            float coatMultiplier
        )
        {
            bool useBuffStorage = CombatOverhaulCompat.ShouldUseBuffStorage(
                itemSlot.Itemstack.Collectible
            );
            ReadWeaponCoat(
                itemSlot.Itemstack,
                useBuffStorage,
                out string existingId,
                out float existingMultiplier,
                out int charges
            );

            if (
                !string.IsNullOrEmpty(existingId)
                && (
                    existingId != potionId || Math.Abs(existingMultiplier - coatMultiplier) > 0.001f
                )
            )
                return false;

            int maxCharges = AlchemyConfig.Loaded.WeaponCoatCharges;
            if (charges >= maxCharges)
                return false;

            int chargesToAdd = 0;
            float litres = availableLitres;
            while (charges + chargesToAdd < maxCharges && litres >= checkLitres)
            {
                chargesToAdd++;
                litres -= consumeLitres;
            }

            if (chargesToAdd <= 0)
                return false;

            ConsumeBarrelLitres(liquidSlot, props, consumeLitres * chargesToAdd);

            SetWeaponCoat(
                itemSlot,
                useBuffStorage,
                potionId,
                itemCode,
                coatMultiplier,
                charges + chargesToAdd
            );
            return true;
        }

        private static bool CoatBarrelArrows(
            ItemSlot itemSlot,
            ItemSlot liquidSlot,
            WaterTightContainableProps props,
            float availableLitres,
            float consumeLitres,
            float checkLitres,
            string potionId,
            string itemCode,
            float coatMultiplier
        )
        {
            bool useBuffStorage = CombatOverhaulCompat.ShouldUseProjectileBuffStorage(
                itemSlot.Itemstack
            );

            if (HasArrowCoat(itemSlot.Itemstack, useBuffStorage))
                return false;

            int stackSize = itemSlot.Itemstack.StackSize;
            if (availableLitres < checkLitres * stackSize)
                return false;

            ConsumeBarrelLitres(liquidSlot, props, consumeLitres * stackSize);

            SetArrowCoat(itemSlot.Itemstack, useBuffStorage, potionId, itemCode, coatMultiplier);
            itemSlot.MarkDirty();
            return true;
        }

        private static void ConsumeBarrelLitres(
            ItemSlot liquidSlot,
            WaterTightContainableProps props,
            float litres
        )
        {
            int itemsToRemove = (int)Math.Round(litres * props.ItemsPerLitre);
            if (itemsToRemove < 1)
                itemsToRemove = 1;

            liquidSlot.TakeOut(itemsToRemove);
            liquidSlot.MarkDirty();
        }

        private static void ReadWeaponCoat(
            ItemStack stack,
            bool useBuffStorage,
            out string potionId,
            out float multiplier,
            out int charges
        )
        {
            if (
                useBuffStorage
                && CombatOverhaulCompat.TryGetCoating(
                    stack,
                    out potionId,
                    out _,
                    out multiplier,
                    out charges
                )
            )
                return;

            ITreeAttribute attrs = stack.Attributes;
            potionId = attrs.GetString("coatedPotionId");
            multiplier = attrs.GetFloat("coatMultiplier");
            charges = attrs.GetInt("coatCharges");
        }

        private static void SetWeaponCoat(
            ItemSlot slot,
            bool useBuffStorage,
            string potionId,
            string itemCode,
            float multiplier,
            int charges
        )
        {
            if (useBuffStorage)
            {
                CombatOverhaulCompat.SetCoating(slot, potionId, itemCode, multiplier, charges);
                return;
            }

            ITreeAttribute attrs = slot.Itemstack.Attributes;
            attrs.SetString("coatedPotionId", potionId);
            attrs.SetString("coatedItemCode", itemCode);
            attrs.SetFloat("coatMultiplier", multiplier);
            attrs.SetInt("coatCharges", charges);
            slot.MarkDirty();
        }

        private static bool HasArrowCoat(ItemStack stack, bool useBuffStorage)
        {
            return useBuffStorage
                ? CombatOverhaulCompat.TryGetCoating(stack, out _, out _, out _, out _)
                : !string.IsNullOrEmpty(stack.Attributes.GetString("coatedPotionId"));
        }

        private static void SetArrowCoat(
            ItemStack stack,
            bool useBuffStorage,
            string potionId,
            string itemCode,
            float multiplier
        )
        {
            if (useBuffStorage)
            {
                CombatOverhaulCompat.SetProjectileCoating(stack, potionId, itemCode, multiplier);
                return;
            }

            ITreeAttribute attrs = stack.Attributes;
            attrs.SetString("coatedPotionId", potionId);
            attrs.SetString("coatedItemCode", itemCode);
            attrs.SetFloat("coatMultiplier", multiplier);
        }

        private static void ApplyCoating(
            ItemSlot coatSlot,
            ItemSlot mainHandSlot,
            EntityAgent byEntity,
            string potionId,
            float coatMultiplier,
            string itemCode,
            System.Func<ItemSlot, bool> consumeCoating
        )
        {
            if (byEntity is not EntityPlayer playerEntity)
                return;

            bool isArrow = IsCoatableProjectile(mainHandSlot.Itemstack.Collectible);
            IServerPlayer serverPlayer = playerEntity.Player as IServerPlayer;

            bool useArrowBuffStorage =
                isArrow
                && CombatOverhaulCompat.ShouldUseProjectileBuffStorage(mainHandSlot.Itemstack);

            if (isArrow && HasArrowCoat(mainHandSlot.Itemstack, useArrowBuffStorage))
            {
                serverPlayer?.SendMessage(
                    GlobalConstants.InfoLogChatGroup,
                    Lang.Get("alchemy:arrow-already-coated"),
                    EnumChatType.Notification
                );
                return;
            }

            bool useBuffStorage =
                !isArrow
                && CombatOverhaulCompat.ShouldUseBuffStorage(mainHandSlot.Itemstack.Collectible);

            string existingId = null;
            float existingMultiplier = 0f;
            int existingCharges = 0;
            if (!isArrow)
                ReadWeaponCoat(
                    mainHandSlot.Itemstack,
                    useBuffStorage,
                    out existingId,
                    out existingMultiplier,
                    out existingCharges
                );

            if (!isArrow)
            {
                if (
                    !string.IsNullOrEmpty(existingId)
                    && (
                        existingId != potionId
                        || Math.Abs(existingMultiplier - coatMultiplier) > 0.001f
                    )
                )
                {
                    serverPlayer?.SendMessage(
                        GlobalConstants.InfoLogChatGroup,
                        Lang.Get("alchemy:coating-conflict"),
                        EnumChatType.Notification
                    );
                    return;
                }

                if (existingCharges >= AlchemyConfig.Loaded.WeaponCoatCharges)
                {
                    serverPlayer?.SendMessage(
                        GlobalConstants.InfoLogChatGroup,
                        Lang.Get("alchemy:coating-max-charges"),
                        EnumChatType.Notification
                    );
                    return;
                }
            }

            string displayName = Lang.Get(itemCode);

            int consumed = consumeCoating(coatSlot) ? 1 : 0;

            if (consumed == 0)
                return;

            coatSlot.MarkDirty();
            byEntity.World.PlaySoundAt(
                new AssetLocation("game:sounds/effect/squish1"),
                byEntity,
                null,
                true,
                18f
            );

            if (isArrow)
            {
                ItemStack coatedArrow = mainHandSlot.TakeOut(1);
                SetArrowCoat(coatedArrow, useArrowBuffStorage, potionId, itemCode, coatMultiplier);
                mainHandSlot.MarkDirty();

                if (!playerEntity.TryGiveItemStack(coatedArrow))
                    byEntity.World.SpawnItemEntity(coatedArrow, byEntity.Pos.XYZ);
            }
            else
            {
                SetWeaponCoat(
                    mainHandSlot,
                    useBuffStorage,
                    potionId,
                    itemCode,
                    coatMultiplier,
                    existingCharges + 1
                );
            }

            playerEntity.Player.InventoryManager.BroadcastHotbarSlot();

            string msg = isArrow
                ? Lang.Get("alchemy:arrow-coated", displayName)
                : Lang.Get("alchemy:weapon-coated", displayName, existingCharges + 1);
            serverPlayer.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                msg,
                EnumChatType.Notification
            );
        }

        public static bool TryProcessPotionEffects(
            EntityAgent byEntity,
            PotionData data,
            ICoreAPI api
        )
        {
            if (byEntity.World.Side != EnumAppSide.Server)
                return false;

            if (byEntity is not EntityPlayer playerEntity)
                return false;

            if (playerEntity.Player is not IServerPlayer serverPlayer)
                return false;

            if (string.IsNullOrWhiteSpace(data.PotionId))
                return false;

            EntityBehaviorPotionEffect behavior =
                playerEntity.GetBehavior<EntityBehaviorPotionEffect>();

            if (behavior == null)
                return false;

            float strengthMul = GetStrengthMultiplier(data.Strength);

            PotionContext ctx = PotionRegistry.BuildPotionDef(data.PotionId, strengthMul);

            if (ctx == null)
            {
                api.Logger.Error("No potion definition for potionId of: {0}", data.PotionId);

                return false;
            }

            if (!behavior.Manager.TryApplyPotion(data.PotionId, ctx, data.DisplayName))
            {
                return false;
            }

            ApplySideEffects(playerEntity, data.PotionId, strengthMul);

            serverPlayer.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                Lang.Get(
                    data.PotionId == "reshapepotionid"
                        ? "alchemy:reshape-gain"
                        : "alchemy:effect-gain",
                    data.DisplayName
                ),
                EnumChatType.Notification
            );

            return true;
        }
    }
}
