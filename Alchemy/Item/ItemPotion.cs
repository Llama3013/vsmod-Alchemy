using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Alchemy
{
    public class ItemPotion : Item
    {
        private Dictionary<string, float> effectList = [];
        private string potionId = "";
        private bool ignoreArmour = false;

        private int duration,
            tickSec = 0;

        private float health = 0f;

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
            string strength = !string.IsNullOrWhiteSpace(Variant?["strength"])
                ? string.Intern(Variant?["strength"])
                : "none";
            JsonObject potion = Attributes?["potioninfo"];
            if (potion?.Exists ?? false)
            {
                potionId = potion["potionId"].AsString();
                ignoreArmour = potion["ignoreArmour"].AsBool(false);
            }
            if (string.IsNullOrWhiteSpace(potionId))
                return;
            duration = potion["duration"].AsInt(0);
            JsonObject tickPotion = Attributes?["tickpotioninfo"];
            if (tickPotion?.Exists ?? false)
            {
                tickSec = tickPotion["ticksec"].AsInt();
                health = tickPotion["health"].AsFloat();
                switch (strength)
                {
                    case "strong":
                        health = MathF.Round(health * 3, 2);
                        break;

                    case "medium":
                        health = MathF.Round(health * 2, 2);
                        break;

                    default:
                        break;
                }
                //api.Logger.Debug("potion {0}, {1}, {2}", potionId, duration);
            }
            JsonObject effects = Attributes?["effects"];
            if (effects?.Exists ?? true)
            {
                effectList = effects.AsObject<Dictionary<string, float>>();
                switch (strength)
                {
                    case "strong":
                        foreach (string effect in effectList.Keys.ToList())
                        {
                            effectList[effect] = MathF.Round(effectList[effect] * 3, 2);
                        }
                        break;

                    case "medium":
                        foreach (string effect in effectList.Keys.ToList())
                        {
                            effectList[effect] = MathF.Round(effectList[effect] * 2, 2);
                        }
                        break;

                    default:
                        break;
                }
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
            api.Logger.Debug("potion {0}, {1}, {2}", effectList.Count, potionId, byEntity.WatchedAttributes.GetLong(potionId));
            //api.Logger.Debug("[Potion] check if drinkable {0}", byEntity.WatchedAttributes.GetLong(potionId));
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
            return;
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
                ModelTransform tf = new ModelTransform();
                tf.Origin.Set(1.1f, 0.5f, 0.5f);
                tf.EnsureDefaultValues();

                tf.Translation.X -= Math.Min(1.7f, secondsUsed * 4 * 1.8f) / FpHandTransform.ScaleXYZ.X;
                tf.Translation.Y += Math.Min(0.4f, secondsUsed * 1.8f) / FpHandTransform.ScaleXYZ.X;
                tf.Scale = 1 + Math.Min(0.5f, secondsUsed * 4 * 1.8f) / FpHandTransform.ScaleXYZ.X;
                tf.Rotation.X += Math.Min(40f, secondsUsed * 350 * 0.75f) / FpHandTransform.ScaleXYZ.X;

                if (secondsUsed > 0.5f)
                {
                    tf.Translation.Y += GameMath.Sin(30 * secondsUsed) / 10 / FpHandTransform.ScaleXYZ.Y;
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
            if (
                secondsUsed > 1.45f
                && byEntity.World.Side == EnumAppSide.Server
                && byEntity is EntityPlayer playerEntity
                && playerEntity.Player is IServerPlayer serverPlayer
            )
            {
                if (
                    !string.IsNullOrWhiteSpace(potionId)
                    && byEntity.WatchedAttributes.GetLong(potionId) == 0
                )
                {
                    switch (potionId)
                    {
                        case "nutritionpotionid":
                            {
                                ITreeAttribute hungerTree = byEntity.WatchedAttributes.GetTreeAttribute(
                                    "hunger"
                                );
                                if (hungerTree != null)
                                {
                                    float totalSatiety =
                                        (
                                            hungerTree.GetFloat("fruitLevel")
                                            + hungerTree.GetFloat("vegetableLevel")
                                            + hungerTree.GetFloat("grainLevel")
                                            + hungerTree.GetFloat("proteinLevel")
                                            + hungerTree.GetFloat("dairyLevel")
                                        ) * 0.9f;
                                    hungerTree.SetFloat("fruitLevel", Math.Max(totalSatiety / 5, 0));
                                    hungerTree.SetFloat(
                                        "vegetableLevel",
                                        Math.Max(totalSatiety / 5, 0)
                                    );
                                    hungerTree.SetFloat("grainLevel", Math.Max(totalSatiety / 5, 0));
                                    hungerTree.SetFloat("proteinLevel", Math.Max(totalSatiety / 5, 0));
                                    hungerTree.SetFloat("dairyLevel", Math.Max(totalSatiety / 5, 0));
                                    byEntity.WatchedAttributes.MarkPathDirty("hunger");
                                }
                                break;
                            }
                        case "recallpotionid":
                            {
                                if (api.Side.IsServer())
                                {
                                    FuzzyEntityPos spawn = serverPlayer.GetSpawnPosition(false);
                                    byEntity.TeleportTo(spawn);
                                }
                                break;
                            }
                        case "temporalpotionid":
                            {
                                byEntity
                                    .GetBehavior<EntityBehaviorTemporalStabilityAffected>()
                                    .OwnStability += 0.2;
                                break;
                            }
                        default:
                            {
                                TempEffect potionEffect = new();
                                if (tickSec != 0)
                                {
                                    potionEffect.TempTickEntityStats(
                                        playerEntity,
                                        effectList,
                                        duration,
                                        potionId,
                                        tickSec,
                                        health,
                                        ignoreArmour
                                    );
                                }
                                else
                                {
                                    potionEffect.TempEntityStats(
                                        playerEntity,
                                        effectList,
                                        duration,
                                        potionId
                                    );
                                }
                                break;
                            }
                    }

                    serverPlayer.SendMessage(
                        GlobalConstants.InfoLogChatGroup,
                        Lang.Get("alchemy:effect-gain", slot.Itemstack.GetName()),
                        EnumChatType.Notification
                    );
                    slot.TakeOut(1);
                    slot.MarkDirty();
                }
            }
            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
        }

        public override void GetHeldItemInfo(
            ItemSlot inSlot,
            StringBuilder dsc,
            IWorldAccessor world,
            bool withDebugInfo
        )
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            if (effectList != null)
            {
                dsc.AppendLine(Lang.Get("alchemy:potion-when-used"));
                if (effectList.TryGetValue("rangedWeaponsAcc", out float rWvalue))
                {
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-archer-accuracy-effect",
                            Math.Round(rWvalue * 100, 0)
                        )
                    );
                }
                if (effectList.TryGetValue("animalLootDropRate", out float aLValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-animal-loot-effect", Math.Round(aLValue * 100, 0))
                    );
                }
                if (effectList.TryGetValue("animalHarvestingTime", out float ahValue))
                {
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-animal-harvest-effect",
                            Math.Round(ahValue * 100, 0)
                        )
                    );
                }
                if (effectList.TryGetValue("animalSeekingRange", out float aSValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-animal-seek-effect", Math.Round(aSValue * 100, 0))
                    );
                }
                if (effectList.TryGetValue("maxhealthExtraPoints", out float mHEValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-max-health-effect", Math.Round(mHEValue * 100, 0))
                    );
                }
                if (effectList.TryGetValue("forageDropRate", out float fDValue))
                {
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-forage-amount-effect",
                            Math.Round(fDValue * 100, 0)
                        )
                    );
                }
                if (effectList.TryGetValue("healingeffectivness", out float hEValue))
                {
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-heal-effectiveness-effect",
                            Math.Round(hEValue * 100, 0)
                        )
                    );
                }
                if (effectList.TryGetValue("hungerrate", out float hRValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-hunger-rate-effect", Math.Round(hRValue * 100, 0))
                    );
                }
                if (effectList.TryGetValue("meleeWeaponsDamage", out float mWValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-melee-damage-effect", Math.Round(mWValue * 100, 0))
                    );
                }
                if (effectList.TryGetValue("mechanicalsDamage", out float mDValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-mech-damage-effect", Math.Round(mDValue * 100, 0))
                    );
                }
                if (effectList.TryGetValue("miningSpeedMul", out float mSValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-mining-speed-effect", Math.Round(mSValue * 100, 0))
                    );
                }
                if (effectList.TryGetValue("oreDropRate", out float oDValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-ore-amount-effect", Math.Round(oDValue * 100, 0))
                    );
                }
                if (effectList.TryGetValue("rangedWeaponsDamage", out float rWDValue))
                {
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-archer-damage-effect",
                            Math.Round(rWDValue * 100, 0)
                        )
                    );
                }
                if (effectList.TryGetValue("rangedWeaponsSpeed", out float rWSValue))
                {
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-archer-speed-effect",
                            Math.Round(rWSValue * 100, 0)
                        )
                    );
                }
                if (effectList.TryGetValue("rustyGearDropRate", out float rGDValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-gear-amount-effect", Math.Round(rGDValue * 100, 0))
                    );
                }
                if (effectList.TryGetValue("walkspeed", out float wSValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-walk-speed-effect", Math.Round(wSValue * 100, 0))
                    );
                }
                if (effectList.TryGetValue("vesselContentsDropRate", out float vCDValue))
                {
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-vessel-amount-effect",
                            Math.Round(vCDValue * 100, 0)
                        )
                    );
                }
                if (effectList.TryGetValue("wildCropDropRate", out float wCDValue))
                {
                    dsc.AppendLine(
                        Lang.Get("alchemy:potion-wild-crop-effect", Math.Round(wCDValue * 100, 0))
                    );
                }
                if (effectList.TryGetValue("wholeVesselLootChance", out float wVLValue))
                {
                    dsc.AppendLine(
                        Lang.Get(
                            "alchemy:potion-whole-vessel-effect",
                            Math.Round(wVLValue * 100, 0)
                        )
                    );
                }
            }

            if (health != 0)
            {
                dsc.AppendLine(Lang.Get("alchemy:potion-health-effect", health));
            }
            if (tickSec != 0)
            {
                dsc.AppendLine(Lang.Get("alchemy:potion-tick-duration", tickSec));
            }
            if (duration != 0)
            {
                dsc.AppendLine(Lang.Get("alchemy:potion-duration", duration));
            }
        }
    }
}