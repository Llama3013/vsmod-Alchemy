using System;
using System.Text;
using Alchemy.Behavior;
using Alchemy.ModConfig;
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
        private float strengthMul = 1f;

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
                        entityItem.SidedPos.XYZ,
                        entityItem.Itemstack,
                        0.75f,
                        (int)(litres * 2),
                        0.45f
                    );
                    entityItem.World.PlaySoundAt(
                        new AssetLocation("sounds/environment/smallsplash"),
                        (float)entityItem.SidedPos.X,
                        (float)entityItem.SidedPos.Y,
                        (float)entityItem.SidedPos.Z,
                        null
                    );
                }
            }

            base.OnGroundIdle(entityItem);
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            strengthMul = Variant?["strength"] switch
            {
                "strong" => AlchemyConfig.Loaded.StrongPotionMultiplier,
                "medium" => AlchemyConfig.Loaded.MediumPotionMultiplier,
                _ => AlchemyConfig.Loaded.WeakPotionMultiplier
            };
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
            /* This checks if the potion effect callback is on */
            if (
                !string.IsNullOrWhiteSpace(potionId)
                && byEntity.WatchedAttributes.GetLong(potionId) == 0
            )
            {
                byEntity.World.RegisterCallback(
                    (dt) =>
                    {
                        if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemInteract)
                        {
                            if (Code.Path.Contains("portion"))
                            {
                                playEatSound(byEntity, "drink", 1);
                            }
                            else
                            {
                                playEatSound(byEntity, "eat", 1);
                            }
                        }
                    },
                    200
                );
                byEntity.AnimManager?.StartAnimation("eat");
                handling = EnumHandHandling.PreventDefault;
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
            Vec3d pos = byEntity.Pos.AheadCopy(0.4f).XYZ.Add(byEntity.LocalEyePos);
            pos.Y -= 0.4f;

            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new();
                tf.Origin.Set(1.1f, 0.5f, 0.5f);
                tf.EnsureDefaultValues();

                tf.Translation.X -=
                    Math.Min(1.7f, secondsUsed * 4 * 1.8f) / FpHandTransform.ScaleXYZ.X;
                tf.Translation.Y += Math.Min(0.4f, secondsUsed * 1.8f) / FpHandTransform.ScaleXYZ.X;
                tf.Scale = 1 + Math.Min(0.5f, secondsUsed * 4 * 1.8f) / FpHandTransform.ScaleXYZ.X;
                tf.Rotation.X +=
                    Math.Min(40f, secondsUsed * 350 * 0.75f) / FpHandTransform.ScaleXYZ.X;

                if (secondsUsed > 0.5f)
                {
                    tf.Translation.Y +=
                        GameMath.Sin(30 * secondsUsed) / 10 / FpHandTransform.ScaleXYZ.Y;
                }

                return secondsUsed <= 1.5f;
            }
            return true;
        }

        public override void OnHeldInteractStop(
            float secondsUsed,
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel
        )
        {
            if (secondsUsed > 1.45f && TryProcessPotionEffects(byEntity, slot.Itemstack))
            {
                slot.TakeOut(1);
                slot.MarkDirty();
            }

            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
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

            PotionEffectBehavior behavior = playerEntity.GetBehavior<PotionEffectBehavior>();
            if (behavior == null)
                return false;

            switch (potionId)
            {
                case "nutritionpotionid":
                    UtilityEffects.ApplyNutritionPotion(byEntity);
                    break;

                case "recallpotionid":
                    UtilityEffects.ApplyRecallPotion(serverPlayer, byEntity, api);
                    break;

                case "temporalpotionid":
                    UtilityEffects.ApplyTemporalPotion(byEntity);
                    break;

                case "reshapepotionid":
                    UtilityEffects.ApplyReshapePotion(serverPlayer);
                    break;
                default:
                {
                    PotionContext ctx = PotionRegistry.BuildPotionDef(potionId, strengthMul);
                    if (ctx == null)
                    {
                        api.Logger.Error("No potion definition for potionId {0}", potionId);
                        return false;
                    }

                    if (!behavior.Manager.TryApplyPotion(potionId, ctx, itemStack.GetName()))
                    {
                        return false;
                    }

                    break;
                }
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
            PotionContext potionDef = PotionRegistry.BuildPotionDef(potionId, strengthMul);
            if (potionDef == null)
                return;

            if (potionDef.EffectList != null)
            {
                dsc.AppendLine(Lang.Get("alchemy:potion-when-used"));
                if (potionDef.EffectList.TryGetValue("rangedWeaponsAcc", out float rWvalue))
                {
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-archer-accuracy-effect",
                            Math.Round(rWvalue * 100, 0)
                        )
                    );
                }
                if (potionDef.EffectList.TryGetValue("animalLootDropRate", out float aLValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-animal-loot-effect", Math.Round(aLValue * 100, 0))
                    );
                }
                if (potionDef.EffectList.TryGetValue("animalHarvestingTime", out float ahValue))
                {
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-animal-harvest-effect",
                            Math.Round(ahValue * 100, 0)
                        )
                    );
                }
                if (potionDef.EffectList.TryGetValue("animalSeekingRange", out float aSValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-animal-seek-effect", Math.Round(aSValue * 100, 0))
                    );
                }
                if (potionDef.EffectList.TryGetValue("maxhealthExtraPoints", out float mHEValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-max-health-effect", Math.Round(mHEValue * 100, 0))
                    );
                }
                if (potionDef.EffectList.TryGetValue("forageDropRate", out float fDValue))
                {
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-forage-amount-effect",
                            Math.Round(fDValue * 100, 0)
                        )
                    );
                }
                if (potionDef.EffectList.TryGetValue("healingeffectivness", out float hEValue))
                {
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-heal-effectiveness-effect",
                            Math.Round(hEValue * 100, 0)
                        )
                    );
                }
                if (potionDef.EffectList.TryGetValue("hungerrate", out float hRValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-hunger-rate-effect", Math.Round(hRValue * 100, 0))
                    );
                }
                if (potionDef.EffectList.TryGetValue("meleeWeaponsDamage", out float mWValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-melee-damage-effect", Math.Round(mWValue * 100, 0))
                    );
                }
                if (potionDef.EffectList.TryGetValue("mechanicalsDamage", out float mDValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-mech-damage-effect", Math.Round(mDValue * 100, 0))
                    );
                }
                if (potionDef.EffectList.TryGetValue("miningSpeedMul", out float mSValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-mining-speed-effect", Math.Round(mSValue * 100, 0))
                    );
                }
                if (potionDef.EffectList.TryGetValue("oreDropRate", out float oDValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-ore-amount-effect", Math.Round(oDValue * 100, 0))
                    );
                }
                if (potionDef.EffectList.TryGetValue("rangedWeaponsDamage", out float rWDValue))
                {
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-archer-damage-effect",
                            Math.Round(rWDValue * 100, 0)
                        )
                    );
                }
                if (potionDef.EffectList.TryGetValue("rangedWeaponsSpeed", out float rWSValue))
                {
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-archer-speed-effect",
                            Math.Round(rWSValue * 100, 0)
                        )
                    );
                }
                if (potionDef.EffectList.TryGetValue("rustyGearDropRate", out float rGDValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-gear-amount-effect", Math.Round(rGDValue * 100, 0))
                    );
                }
                if (potionDef.EffectList.TryGetValue("walkspeed", out float wSValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-walk-speed-effect", Math.Round(wSValue * 100, 0))
                    );
                }
                if (potionDef.EffectList.TryGetValue("vesselContentsDropRate", out float vCDValue))
                {
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-vessel-amount-effect",
                            Math.Round(vCDValue * 100, 0)
                        )
                    );
                }
                if (potionDef.EffectList.TryGetValue("wildCropDropRate", out float wCDValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-wild-crop-effect", Math.Round(wCDValue * 100, 0))
                    );
                }
                if (potionDef.EffectList.TryGetValue("wholeVesselLootChance", out float wVLValue))
                {
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-whole-vessel-effect",
                            Math.Round(wVLValue * 100, 0)
                        )
                    );
                }

                if (
                    potionDef.EffectList.TryGetValue("health", out float healthValue)
                    && healthValue is > 0.01f or < -0.01f
                )
                {
                    dsc.AppendLine(Lang.Get("alchemy:potion-single-health-effect", healthValue));
                }
            }

            if (potionDef.Health is > 0.01f or < -0.01f)
            {
                dsc.AppendLine(Lang.Get("alchemy:potion-health-effect", potionDef.Health));
            }
            if (potionDef.TickSec != 0)
            {
                dsc.AppendLine(Lang.Get("alchemy:potion-tick-duration", potionDef.TickSec));
            }
            if (potionDef.Duration != 0)
            {
                dsc.AppendLine(Lang.Get("alchemy:potion-duration", potionDef.Duration));
            }
        }
    }
}
