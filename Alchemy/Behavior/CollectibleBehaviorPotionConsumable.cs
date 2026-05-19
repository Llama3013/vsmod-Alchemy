using System;
using System.Text;
using Alchemy.ModConfig;
using Alchemy.Systems;
using Alchemy.Utility;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Alchemy.Behavior
{
    public class PotionConsumableBehavior(CollectibleObject collObj) : CollectibleBehavior(collObj)
    {
        private string source;
        private string animation;
        private string sound;
        private float consumeLitres;
        private float consumeTime;

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            source = properties["source"].AsString("item");
            animation = properties["animation"].AsString("eat");
            sound = properties["sound"].AsString("alchemy:sounds/player/drink");
            consumeLitres = properties["consumeLitres"].AsFloat(0.25f);
            float defaultConsumeTime =
                source == "liquidcontent"
                    ? AlchemyConfig.Loaded.PotionDrinkTime
                    : AlchemyConfig.Loaded.PotionEatTime;
            consumeTime = properties["consumeTime"].AsFloat(defaultConsumeTime);
        }

        private bool TryGetPotionData(ItemSlot slot, out PotionData data)
        {
            data = null;

            if (source == "liquidcontent")
            {
                if (collObj is not BlockLiquidContainerBase container)
                    return false;

                ItemStack content = container.GetContent(slot.Itemstack);
                if (content == null)
                    return false;

                JsonObject potion = content.ItemAttributes?["potioninfo"];
                string potionId = potion?.Exists == true ? potion["potionId"].AsString() : null;

                if (string.IsNullOrWhiteSpace(potionId))
                    return false;

                string strength = "weak";
                content.Collectible?.Variant?.TryGetValue("strength", out strength);

                data = new PotionData
                {
                    PotionId = potionId,
                    Strength = strength ?? "weak",
                    DisplayName = content.GetName(),
                    SourceStack = content,
                };
                return true;
            }
            else
            {
                JsonObject potion = slot.Itemstack.ItemAttributes?["potioninfo"];
                string potionId = potion?.Exists == true ? potion["potionId"].AsString() : null;

                if (string.IsNullOrWhiteSpace(potionId))
                    return false;

                string strength = "weak";
                slot.Itemstack.Collectible?.Variant?.TryGetValue("strength", out strength);

                data = new PotionData
                {
                    PotionId = potionId,
                    Strength = strength ?? "weak",
                    DisplayName = slot.Itemstack.GetName(),
                    SourceStack = slot.Itemstack,
                };
                return true;
            }
        }

        private bool ConsumePotion(ItemSlot slot, EntityAgent byEntity)
        {
            if (byEntity.World.Side != EnumAppSide.Server)
                return false;

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
            else
            {
                slot.TakeOut(1);
                slot.MarkDirty();
                return true;
            }
        }

        public bool CanConsume(ItemSlot slot, EntityAgent byEntity)
        {
            if (!TryGetPotionData(slot, out PotionData data))
                return false;
            return byEntity.WatchedAttributes.GetLong(data.PotionId) == 0;
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

            // Recall potion can't be used while mounted on a sailed boat
            if (
                data.PotionId == "recallpotionid"
                && byEntity.World.Side == EnumAppSide.Server
                && byEntity.MountedOn?.MountSupplier?.OnEntity?.Code?.Path is string boatPath
                && WildcardUtil.Match("boat-sailed-*", boatPath)
            )
            {
                if (byEntity is EntityPlayer { Player: IServerPlayer serverPlayer })
                    serverPlayer.SendMessage(
                        GlobalConstants.InfoLogChatGroup,
                        Lang.Get("alchemy:boat-block"),
                        EnumChatType.Notification
                    );
                handling = EnumHandHandling.PreventDefaultAction;
                bhHandling = EnumHandling.PreventDefault;
                return;
            }

            if (
                !PotionConsumableLogic.HandleDrinkStart(
                    byEntity,
                    data.PotionId,
                    animation,
                    sound,
                    ref handling,
                    consumeTime
                )
            )
                return;

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
            return PotionConsumableLogic.HandleDrinkStep(
                secondsUsed,
                slot,
                byEntity,
                true,
                consumeTime
            );
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
            PotionConsumableLogic.HandleDrinkStop(
                secondsUsed,
                byEntity,
                data,
                () => ConsumePotion(slot, byEntity),
                byEntity.Api,
                consumeTime
            );
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
                dsc.AppendLine(Lang.Get("alchemy:potion-when-used"));
                if (ctx.Effects.TryGetValue("rangedWeaponsAcc", out float rWvalue))
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-archer-accuracy-effect",
                            Math.Round(rWvalue * 100, 0)
                        )
                    );
                if (ctx.Effects.TryGetValue("animalLootDropRate", out float aLValue))
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-animal-loot-effect", Math.Round(aLValue * 100, 0))
                    );
                if (ctx.Effects.TryGetValue("animalHarvestingTime", out float ahValue))
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-animal-harvest-effect",
                            Math.Round(ahValue * 100, 0)
                        )
                    );
                if (ctx.Effects.TryGetValue("animalSeekingRange", out float aSValue))
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-animal-seek-effect", Math.Round(aSValue * 100, 0))
                    );
                if (ctx.Effects.TryGetValue("maxhealthExtraPoints", out float mHEValue))
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-max-health-effect", Math.Round(mHEValue * 100, 0))
                    );
                if (ctx.Effects.TryGetValue("forageDropRate", out float fDValue))
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-forage-amount-effect",
                            Math.Round(fDValue * 100, 0)
                        )
                    );
                if (ctx.Effects.TryGetValue("healingeffectivness", out float hEValue))
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-heal-effectiveness-effect",
                            Math.Round(hEValue * 100, 0)
                        )
                    );
                if (ctx.Effects.TryGetValue("hungerrate", out float hRValue))
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-hunger-rate-effect", Math.Round(hRValue * 100, 0))
                    );
                if (ctx.Effects.TryGetValue("meleeWeaponsDamage", out float mWValue))
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-melee-damage-effect", Math.Round(mWValue * 100, 0))
                    );
                if (ctx.Effects.TryGetValue("mechanicalsDamage", out float mDValue))
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-mech-damage-effect", Math.Round(mDValue * 100, 0))
                    );
                if (ctx.Effects.TryGetValue("miningSpeedMul", out float mSValue))
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-mining-speed-effect", Math.Round(mSValue * 100, 0))
                    );
                if (ctx.Effects.TryGetValue("oreDropRate", out float oDValue))
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-ore-amount-effect", Math.Round(oDValue * 100, 0))
                    );
                if (ctx.Effects.TryGetValue("rangedWeaponsDamage", out float rWDValue))
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-archer-damage-effect",
                            Math.Round(rWDValue * 100, 0)
                        )
                    );
                if (ctx.Effects.TryGetValue("rangedWeaponsSpeed", out float rWSValue))
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-archer-speed-effect",
                            Math.Round(rWSValue * 100, 0)
                        )
                    );
                if (ctx.Effects.TryGetValue("rustyGearDropRate", out float rGDValue))
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-gear-amount-effect", Math.Round(rGDValue * 100, 0))
                    );
                if (ctx.Effects.TryGetValue("walkspeed", out float wSValue))
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-walk-speed-effect", Math.Round(wSValue * 100, 0))
                    );
                if (ctx.Effects.TryGetValue("vesselContentsDropRate", out float vCDValue))
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-vessel-amount-effect",
                            Math.Round(vCDValue * 100, 0)
                        )
                    );
                if (ctx.Effects.TryGetValue("wildCropDropRate", out float wCDValue))
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-wild-crop-effect", Math.Round(wCDValue * 100, 0))
                    );
                if (ctx.Effects.TryGetValue("wholeVesselLootChance", out float wVLValue))
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
                    dsc.AppendLine(Lang.Get("alchemy:itemdesc-utilitypotionportion-recall"));
                if (ctx.GlowStrength > 0)
                    dsc.AppendLine(Lang.Get("alchemy:itemdesc-utilitypotionportion-glow"));
                if (ctx.WaterBreathe)
                    dsc.AppendLine(Lang.Get("alchemy:itemdesc-utilitypotionportion-waterbreathe"));
                if (ctx.TemporalStabilityGain > 0)
                    dsc.AppendLine(Lang.Get("alchemy:itemdesc-utilitypotionportion-temporal"));
                if (ctx.RetainedNutrition > 0)
                    dsc.AppendLine(Lang.Get("alchemy:itemdesc-utilitypotionportion-nutrition"));
                if (ctx.Reshape)
                    dsc.AppendLine(Lang.Get("alchemy:itemdesc-utilitypotionportion-reshape"));
                if (ctx.SizeChange > 0)
                    dsc.AppendLine(Lang.Get("alchemy:itemdesc-utilitypotionportion-grow"));
                if (ctx.SizeChange < 0)
                    dsc.AppendLine(Lang.Get("alchemy:itemdesc-utilitypotionportion-shrink"));
                if (ctx.FallDamageReduction > 0)
                    dsc.AppendLine(Lang.Get("alchemy:itemdesc-utilitypotionportion-fall"));
                if (ctx.CanClimbAnywhere)
                    dsc.AppendLine(Lang.Get("alchemy:itemdesc-utilitypotionportion-climb"));
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
