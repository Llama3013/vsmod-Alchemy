using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    public class PotionConsumableBehavior(CollectibleObject collObj) : CollectibleBehavior(collObj)
    {
        private string source;
        private string animation;
        private string sound;
        private float consumeLitres;
        private float drinkCheckLitres;
        private float consumeTime;
        private static readonly HashSet<long> drinkResolvedEntities = [];

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            source = properties["source"].AsString("item");
            animation = properties["animation"].AsString("eat");
            sound = properties["sound"].AsString("alchemy:sounds/player/drink");
            consumeLitres = properties["consumeLitres"]
                .AsFloat(AlchemyConfig.Loaded.PotionConsumeLitres);
            drinkCheckLitres = properties["drinkCheckLitres"]
                .AsFloat(AlchemyConfig.Loaded.PotionDrinkCheckLitres);
            float defaultConsumeTime =
                source == "liquidcontent"
                    ? AlchemyConfig.Loaded.PotionDrinkTime
                    : AlchemyConfig.Loaded.PotionEatTime;
            consumeTime = properties["consumeTime"].AsFloat(defaultConsumeTime);
        }

        private bool TryGetPotionData(ItemSlot slot, out PotionData data)
        {
            data = null;

            ItemStack stack =
                source == "liquidcontent"
                    ? (collObj as BlockLiquidContainerBase)?.GetContent(slot.Itemstack)
                    : slot.Itemstack;

            if (
                !PotionConsumableLogic.TryReadPotionInfo(
                    stack,
                    out string potionId,
                    out string strength
                )
            )
                return false;

            data = new PotionData
            {
                PotionId = potionId,
                Strength = strength,
                DisplayName = stack.GetName(),
                SourceStack = stack,
            };
            return true;
        }

        private bool ConsumePotion(ItemSlot slot, EntityAgent byEntity)
        {
            if (byEntity.World.Side != EnumAppSide.Server)
                return false;

            return PotionConsumableLogic.ConsumeSource(
                collObj,
                source,
                slot,
                byEntity,
                consumeLitres
            );
        }

        public bool CanConsume(ItemSlot slot, EntityAgent byEntity)
        {
            if (!TryGetPotionData(slot, out PotionData data))
                return false;
            return byEntity.WatchedAttributes.GetLong(data.PotionId) == 0;
        }

        private bool HasEnoughToDrink(ItemSlot slot)
        {
            return PotionConsumableLogic.HasEnoughSource(collObj, source, slot, drinkCheckLitres);
        }

        private static bool IsReshapeReentry(EntityAgent byEntity, PotionData data) =>
            data.PotionId == "reshapepotionid"
            && byEntity.WatchedAttributes.GetBool("allowcharselonce");

        private static bool IsRecallOnSailedBoat(EntityAgent byEntity, PotionData data) =>
            data.PotionId == "recallpotionid"
            && byEntity.MountedOn?.MountSupplier?.OnEntity?.Code?.Path is string boatPath
            && WildcardUtil.Match("boat-sailed-*", boatPath);

        private string GetDrinkBlockReason(ItemSlot slot, EntityAgent byEntity, PotionData data)
        {
            if (!HasEnoughToDrink(slot))
                return "alchemy:not-enough-potion";

            if (byEntity.World.Side != EnumAppSide.Server)
                return null;

            if (IsReshapeReentry(byEntity, data))
                return "alchemy:reshape-block";

            if (IsRecallOnSailedBoat(byEntity, data))
                return "alchemy:boat-block";

            return null;
        }

        private static bool IsPotionAlreadyActive(EntityAgent byEntity, PotionData data)
        {
            if (byEntity is not EntityPlayer player)
                return false;

            PotionEffectManager manager = player.GetBehavior<EntityBehaviorPotionEffect>()?.Manager;

            if (manager?.CanRefresh(data.PotionId) == true)
                return false;

            return player.WatchedAttributes.GetLong(data.PotionId) != 0
                || manager?.IsActive(data.PotionId) == true;
        }

        private static bool IsAnyPotionActiveAndLimited(EntityAgent byEntity)
        {
            if (!AlchemyConfig.Loaded.OnlyOnePotionAtATime)
                return false;

            if (byEntity is not EntityPlayer player)
                return false;

            return player.GetBehavior<EntityBehaviorPotionEffect>()?.Manager.HasAnyActive == true;
        }

        private static void DenyDrink(EntityAgent byEntity, string langKey)
        {
            byEntity.PlayEntitySound("smallhurt", (byEntity as EntityPlayer)?.Player);

            if (langKey != null && byEntity is EntityPlayer { Player: IServerPlayer serverPlayer })
                serverPlayer.SendMessage(
                    GlobalConstants.InfoLogChatGroup,
                    Lang.Get(langKey),
                    EnumChatType.Notification
                );
        }

        public override void OnHeldInteractStart(
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel,
            bool firstEvent,
            ref EnumHandHandling handling,
            ref EnumHandling bhHandling
        )
        {
            // Don't intercept shift+block interactions for liquid containers (allows filling from water)
            if (source == "liquidcontent" && blockSel != null && byEntity.Controls.ShiftKey)
                return;

            if (!TryGetPotionData(slot, out PotionData data))
                return;

            // This stops occasional double notification
            if (byEntity.World.Side == EnumAppSide.Server)
                drinkResolvedEntities.Remove(byEntity.EntityId);

            byEntity.World.RegisterCallback(
                dt =>
                {
                    if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemInteract)
                        byEntity.PlayEntitySound(sound, (byEntity as EntityPlayer)?.Player);
                },
                200
            );

            // This is used to adapt animations to drink/eat time. I'm unsure if its necessary so for now I will leave it commented
            // var animsByCode = byEntity.Properties?.Client?.AnimationsByMetaCode;
            // if (
            //     byEntity.AnimManager != null
            //     && animsByCode != null
            //     && animsByCode.TryGetValue(animation, out AnimationMetaData animdata)
            // )
            // {
            //     float speed = 1.0f / consumeTime;
            //     AnimationMetaData scaled = animdata.Clone();
            //     scaled.AnimationSpeed = speed;
            //     byEntity.AnimManager.ResetAnimation(animation);
            //     byEntity.AnimManager.StartAnimation(scaled);

            //     // The TP dispatch above starts the FP variant from AnimationsByMetaCode at its
            //     // original speed. Override it with a scaled clone so FP and TP stay in sync.
            //     if (animsByCode.TryGetValue(animation + "-fp", out AnimationMetaData fpAnimdata))
            //     {
            //         AnimationMetaData scaledFp = fpAnimdata.Clone();
            //         scaledFp.AnimationSpeed = speed;
            //         byEntity.AnimManager.StartAnimation(scaledFp);
            //     }
            // }
            // else
            // {
            byEntity.AnimManager?.StartAnimation(animation);
            // }

            handling = EnumHandHandling.PreventDefault;
            bhHandling = EnumHandling.PreventDefault;
        }

        public override bool OnHeldInteractStep(
            float secondsUsed,
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel,
            ref EnumHandling handling
        )
        {
            if (!TryGetPotionData(slot, out _))
                return base.OnHeldInteractStep(
                    secondsUsed,
                    slot,
                    byEntity,
                    blockSel,
                    entitySel,
                    ref handling
                );

            handling = EnumHandling.PreventDefault;
            if (secondsUsed > 0.5f && (int)(30 * secondsUsed) % 7 == 1)
            {
                Vec3d pos = byEntity.Pos.AheadCopy(0.4f).XYZ.Add(byEntity.LocalEyePos);
                pos.Y -= 0.4f;

                byEntity.World.SpawnCubeParticles(
                    pos,
                    slot.Itemstack,
                    0.3f,
                    4,
                    0.5f,
                    (byEntity as EntityPlayer)?.Player
                );
            }

            return secondsUsed <= consumeTime;
        }

        public override void OnHeldInteractStop(
            float secondsUsed,
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel,
            ref EnumHandling handling
        )
        {
            if (!TryGetPotionData(slot, out PotionData data))
                return;

            handling = EnumHandling.PreventDefault;

            // These three if statements stops occasional double notification
            if (byEntity.World.Side != EnumAppSide.Server)
                return;

            if (secondsUsed <= consumeTime - 0.05f)
                return;

            if (!drinkResolvedEntities.Add(byEntity.EntityId))
                return;

            string blockReason = GetDrinkBlockReason(slot, byEntity, data);
            if (blockReason != null)
            {
                DenyDrink(byEntity, blockReason);
                return;
            }

            if (IsPotionAlreadyActive(byEntity, data))
            {
                DenyDrink(byEntity, "alchemy:potion-already-active");
                return;
            }

            if (IsAnyPotionActiveAndLimited(byEntity))
            {
                DenyDrink(byEntity, "alchemy:potion-limit-active");
                return;
            }

            if (PotionConsumableLogic.TryProcessPotionEffects(byEntity, data, byEntity.Api))
                ConsumePotion(slot, byEntity);
            else
                DenyDrink(byEntity, null);
        }

        public override void GetHeldItemInfo(
            ItemSlot slot,
            StringBuilder dsc,
            IWorldAccessor world,
            bool withDebugInfo
        )
        {
            if (!TryGetPotionData(slot, out PotionData data))
                return;

            float strengthMul = PotionConsumableLogic.GetStrengthMultiplier(data.Strength);
            PotionContext ctx = PotionRegistry.BuildPotionDef(data.PotionId, strengthMul);
            if (ctx == null)
                return;

            if (ctx.Effects != null)
            {
                int headerStart = dsc.Length;
                dsc.AppendLine(Lang.Get("alchemy:potion-when-used"));
                int headerEnd = dsc.Length;
                if (ctx.Effects.TryGetValue("rangedWeaponsAcc", out float rWvalue) && rWvalue != 0f)
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-archer-accuracy-effect",
                            Math.Round(rWvalue * 100, 0)
                        )
                    );
                if (
                    ctx.Effects.TryGetValue("animalLootDropRate", out float aLValue)
                    && aLValue != 0f
                )
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-animal-loot-effect", Math.Round(aLValue * 100, 0))
                    );
                if (
                    ctx.Effects.TryGetValue("animalHarvestingTime", out float ahValue)
                    && ahValue != 0f
                )
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-animal-harvest-effect",
                            Math.Round(ahValue * 100, 0)
                        )
                    );
                if (
                    ctx.Effects.TryGetValue("animalSeekingRange", out float aSValue)
                    && aSValue != 0f
                )
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-animal-seek-effect", Math.Round(aSValue * 100, 0))
                    );
                if (
                    ctx.Effects.TryGetValue("maxhealthExtraPoints", out float mHEValue)
                    && mHEValue != 0f
                )
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-max-health-effect", Math.Round(mHEValue * 100, 0))
                    );
                if (ctx.Effects.TryGetValue("forageDropRate", out float fDValue) && fDValue != 0f)
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-forage-amount-effect",
                            Math.Round(fDValue * 100, 0)
                        )
                    );
                if (
                    ctx.Effects.TryGetValue("healingeffectivness", out float hEValue)
                    && hEValue != 0f
                )
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-heal-effectiveness-effect",
                            Math.Round(hEValue * 100, 0)
                        )
                    );
                if (ctx.Effects.TryGetValue("hungerrate", out float hRValue) && hRValue != 0f)
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-hunger-rate-effect", Math.Round(hRValue * 100, 0))
                    );
                if (
                    ctx.Effects.TryGetValue("meleeWeaponsDamage", out float mWValue)
                    && mWValue != 0f
                )
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-melee-damage-effect", Math.Round(mWValue * 100, 0))
                    );
                if (
                    ctx.Effects.TryGetValue("mechanicalsDamage", out float mDValue)
                    && mDValue != 0f
                )
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-mech-damage-effect", Math.Round(mDValue * 100, 0))
                    );
                if (ctx.Effects.TryGetValue("miningSpeedMul", out float mSValue) && mSValue != 0f)
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-mining-speed-effect", Math.Round(mSValue * 100, 0))
                    );
                if (ctx.Effects.TryGetValue("oreDropRate", out float oDValue) && oDValue != 0f)
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-ore-amount-effect", Math.Round(oDValue * 100, 0))
                    );
                if (
                    ctx.Effects.TryGetValue("rangedWeaponsDamage", out float rWDValue)
                    && rWDValue != 0f
                )
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-archer-damage-effect",
                            Math.Round(rWDValue * 100, 0)
                        )
                    );
                if (
                    ctx.Effects.TryGetValue("rangedWeaponsSpeed", out float rWSValue)
                    && rWSValue != 0f
                )
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-archer-speed-effect",
                            Math.Round(rWSValue * 100, 0)
                        )
                    );
                if (
                    ctx.Effects.TryGetValue("rustyGearDropRate", out float rGDValue)
                    && rGDValue != 0f
                )
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-gear-amount-effect", Math.Round(rGDValue * 100, 0))
                    );
                if (ctx.Effects.TryGetValue("walkspeed", out float wSValue) && wSValue != 0f)
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-walk-speed-effect", Math.Round(wSValue * 100, 0))
                    );
                if (
                    ctx.Effects.TryGetValue("vesselContentsDropRate", out float vCDValue)
                    && vCDValue != 0f
                )
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-vessel-amount-effect",
                            Math.Round(vCDValue * 100, 0)
                        )
                    );
                if (
                    ctx.Effects.TryGetValue("wildCropDropRate", out float wCDValue)
                    && wCDValue != 0f
                )
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-wild-crop-effect", Math.Round(wCDValue * 100, 0))
                    );
                if (
                    ctx.Effects.TryGetValue("wholeVesselLootChance", out float wVLValue)
                    && wVLValue != 0f
                )
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-whole-vessel-effect",
                            Math.Round(wVLValue * 100, 0)
                        )
                    );
                if (
                    ctx.Effects.TryGetValue("health", out float healthValue)
                    && healthValue is > 0.01f or < -0.01f
                )
                    dsc.AppendLine(Lang.Get("alchemy:potion-single-health-effect", healthValue));

                if (ctx.Respawn)
                    dsc.AppendLine(Lang.Get("alchemy:potion-recall-effect"));
                if (ctx.GlowStrength > 0)
                    dsc.AppendLine(Lang.Get("alchemy:potion-glow-effect"));
                if (ctx.WaterBreathe)
                    dsc.AppendLine(Lang.Get("alchemy:potion-waterbreathe-effect"));
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

                if (dsc.Length == headerEnd)
                    dsc.Remove(headerStart, headerEnd - headerStart);
            }

            if (ctx.Health is > 0.01f or < -0.01f)
                dsc.AppendLine(Lang.Get("alchemy:potion-health-effect", Math.Round(ctx.Health, 2)));
            if (ctx.TickSec != 0)
                dsc.AppendLine(Lang.Get("alchemy:potion-tick-duration", ctx.TickSec));
            if (ctx.Duration != 0)
                dsc.AppendLine(Lang.Get("alchemy:potion-duration", ctx.Duration));
        }
    }
}
