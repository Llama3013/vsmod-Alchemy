using System;
using System.Collections.Generic;
using System.Text;
using Alchemy.Behavior;
using Alchemy.Block;
using Alchemy.ModConfig;
using Alchemy.Utility;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Alchemy.Item
{
    public class ItemPotion : Vintagestory.API.Common.Item
    {
        private string potionId = "";
        private string strength;

        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity forEntity)
        {
            return "eat";
        }

        public override void OnGroundIdle(EntityItem entityItem)
        {
            if (entityItem.Itemstack.Item.MatterState == EnumMatterState.Liquid)
            {
                //If liquid use OnGroundIdle from ItemLiquidPortion code
                entityItem.Die(EnumDespawnReason.Removed);

                if (entityItem.World.Side == EnumAppSide.Server)
                {
                    WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(
                        entityItem.Itemstack
                    );
                    float litres = entityItem.Itemstack.StackSize / props.ItemsPerLitre;

                    entityItem.World.SpawnCubeParticles(
                        entityItem.Pos.XYZ,
                        entityItem.Itemstack,
                        0.75f,
                        (int)(litres * 2),
                        0.45f
                    );
                    entityItem.World.PlaySoundAt(
                        new AssetLocation("sounds/environment/smallsplash"),
                        (float)entityItem.Pos.X,
                        (float)entityItem.Pos.Y,
                        (float)entityItem.Pos.Z,
                        null
                    );
                }
            }

            base.OnGroundIdle(entityItem);
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            strength = Variant?["strength"] ?? "weak";
            JsonObject potion = Attributes?["potioninfo"];
            potionId = potion?["potionId"].AsString();
            if (string.IsNullOrWhiteSpace(potionId))
            {
                api.Logger.Debug(
                    "{0} has no potionid, therefore it will never give effects",
                    Code.GetName()
                );
            }
        }

        public override void OnHeldInteractStart(
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel,
            bool firstEvent,
            ref EnumHandHandling handling
        )
        {
            if (
                PotionConsumableLogic.HandleDrinkStart(
                    byEntity,
                    potionId,
                    "eat",
                    () => playEatSound(byEntity, "eat", 1),
                    ref handling
                )
            )
            {
                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }

        public override bool OnHeldInteractStep(
            float secondsUsed,
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel
        )
        {
            return PotionConsumableLogic.HandleDrinkStep(secondsUsed, slot, byEntity, false);
        }

        public override void OnHeldInteractStop(
            float secondsUsed,
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel
        )
        {
            PotionConsumableLogic.HandleDrinkStop(
                secondsUsed,
                byEntity,
                slot,
                potionId,
                strength,
                () =>
                {
                    slot.TakeOut(1);
                    slot.MarkDirty();
                    return true;
                },
                slot.Itemstack.GetName,
                api
            );

            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
        }

        public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
        {
            base.OnHeldIdle(slot, byEntity);

            if (!Code.Path.Contains("herbball"))
                return;

            PotionConsumableLogic.HandleWeaponCoatingIdle(
                api,
                slot,
                byEntity,
                potionId,
                strength,
                s => s.Itemstack.GetName(),
                s =>
                {
                    s.TakeOut(1);
                    s.MarkDirty();
                    return true;
                }
            );
        }

        public bool TryProcessPotionEffects(EntityAgent byEntity, ItemStack itemStack)
        {
            if (byEntity.World.Side != EnumAppSide.Server)
                return false;

            if (byEntity is not EntityPlayer playerEntity)
                return false;

            if (playerEntity.Player is not IServerPlayer serverPlayer)
                return false;

            if (string.IsNullOrWhiteSpace(potionId))
                return false;

            if (itemStack == null)
                return false;

            EntityBehaviorPotionEffect behavior =
                playerEntity.GetBehavior<EntityBehaviorPotionEffect>();
            if (behavior == null)
                return false;

            float strengthMul = strength switch
            {
                "strong" => AlchemyConfig.Loaded.StrongPotionMultiplier,
                "medium" => AlchemyConfig.Loaded.MediumPotionMultiplier,
                _ => AlchemyConfig.Loaded.WeakPotionMultiplier,
            };
            PotionContext ctx = PotionRegistry.BuildPotionDef(potionId, strengthMul);
            if (ctx == null)
            {
                api.Logger.Error("No potion definition for potionId of: {0}", potionId);
                return false;
            }

            if (!behavior.Manager.TryApplyPotion(potionId, ctx, itemStack.GetName()))
            {
                return false;
            }

            serverPlayer.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                Lang.Get("alchemy:effect-gain", itemStack.GetName()),
                EnumChatType.Notification
            );
            return true;
        }

        public override void GetHeldItemInfo(
            ItemSlot inSlot,
            StringBuilder dsc,
            IWorldAccessor world,
            bool withDebugInfo
        )
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            if (string.IsNullOrWhiteSpace(potionId))
            {
                return;
            }
            float strengthMul = strength switch
            {
                "strong" => AlchemyConfig.Loaded.StrongPotionMultiplier,
                "medium" => AlchemyConfig.Loaded.MediumPotionMultiplier,
                _ => AlchemyConfig.Loaded.WeakPotionMultiplier,
            };
            PotionContext ctx = PotionRegistry.BuildPotionDef(potionId, strengthMul);
            if (ctx == null)
            {
                return;
            }

            if (ctx.Effects != null)
            {
                dsc.AppendLine(Lang.Get("alchemy:potion-when-used"));
                if (ctx.Effects.TryGetValue("rangedWeaponsAcc", out float rWvalue))
                {
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-archer-accuracy-effect",
                            Math.Round(rWvalue * 100, 0)
                        )
                    );
                }
                if (ctx.Effects.TryGetValue("animalLootDropRate", out float aLValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-animal-loot-effect", Math.Round(aLValue * 100, 0))
                    );
                }
                if (ctx.Effects.TryGetValue("animalHarvestingTime", out float ahValue))
                {
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-animal-harvest-effect",
                            Math.Round(ahValue * 100, 0)
                        )
                    );
                }
                if (ctx.Effects.TryGetValue("animalSeekingRange", out float aSValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-animal-seek-effect", Math.Round(aSValue * 100, 0))
                    );
                }
                if (ctx.Effects.TryGetValue("maxhealthExtraPoints", out float mHEValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-max-health-effect", Math.Round(mHEValue * 100, 0))
                    );
                }
                if (ctx.Effects.TryGetValue("forageDropRate", out float fDValue))
                {
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-forage-amount-effect",
                            Math.Round(fDValue * 100, 0)
                        )
                    );
                }
                if (ctx.Effects.TryGetValue("healingeffectivness", out float hEValue))
                {
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-heal-effectiveness-effect",
                            Math.Round(hEValue * 100, 0)
                        )
                    );
                }
                if (ctx.Effects.TryGetValue("hungerrate", out float hRValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-hunger-rate-effect", Math.Round(hRValue * 100, 0))
                    );
                }
                if (ctx.Effects.TryGetValue("meleeWeaponsDamage", out float mWValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-melee-damage-effect", Math.Round(mWValue * 100, 0))
                    );
                }
                if (ctx.Effects.TryGetValue("mechanicalsDamage", out float mDValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-mech-damage-effect", Math.Round(mDValue * 100, 0))
                    );
                }
                if (ctx.Effects.TryGetValue("miningSpeedMul", out float mSValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-mining-speed-effect", Math.Round(mSValue * 100, 0))
                    );
                }
                if (ctx.Effects.TryGetValue("oreDropRate", out float oDValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-ore-amount-effect", Math.Round(oDValue * 100, 0))
                    );
                }
                if (ctx.Effects.TryGetValue("rangedWeaponsDamage", out float rWDValue))
                {
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-archer-damage-effect",
                            Math.Round(rWDValue * 100, 0)
                        )
                    );
                }
                if (ctx.Effects.TryGetValue("rangedWeaponsSpeed", out float rWSValue))
                {
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-archer-speed-effect",
                            Math.Round(rWSValue * 100, 0)
                        )
                    );
                }
                if (ctx.Effects.TryGetValue("rustyGearDropRate", out float rGDValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-gear-amount-effect", Math.Round(rGDValue * 100, 0))
                    );
                }
                if (ctx.Effects.TryGetValue("walkspeed", out float wSValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-walk-speed-effect", Math.Round(wSValue * 100, 0))
                    );
                }
                if (ctx.Effects.TryGetValue("vesselContentsDropRate", out float vCDValue))
                {
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-vessel-amount-effect",
                            Math.Round(vCDValue * 100, 0)
                        )
                    );
                }
                if (ctx.Effects.TryGetValue("wildCropDropRate", out float wCDValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-wild-crop-effect", Math.Round(wCDValue * 100, 0))
                    );
                }
                if (ctx.Effects.TryGetValue("wholeVesselLootChance", out float wVLValue))
                {
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-whole-vessel-effect",
                            Math.Round(wVLValue * 100, 0)
                        )
                    );
                }

                if (
                    ctx.Effects.TryGetValue("health", out float healthValue)
                    && healthValue is > 0.01f or < -0.01f
                )
                {
                    dsc.AppendLine(Lang.Get("alchemy:potion-single-health-effect", healthValue));
                }
            }

            if (ctx.Health is > 0.01f or < -0.01f)
            {
                dsc.AppendLine(Lang.Get("alchemy:potion-health-effect", Math.Round(ctx.Health, 2)));
            }
            if (ctx.TickSec != 0)
            {
                dsc.AppendLine(Lang.Get("alchemy:potion-tick-duration", ctx.TickSec));
            }
            if (ctx.Duration != 0)
            {
                dsc.AppendLine(Lang.Get("alchemy:potion-duration", ctx.Duration));
            }
        }
    }
}
