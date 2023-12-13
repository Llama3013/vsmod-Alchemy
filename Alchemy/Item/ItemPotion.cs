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
        public Dictionary<string, float> dic = new();
        public string potionId;
        public int duration;
        public int tickSec = 0;
        public float health;

        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity forEntity)
        {
            return "eat";
        }

        public override void OnGroundIdle(EntityItem entityItem)
        {
            if (entityItem.Itemstack.Item.MatterState == EnumMatterState.Liquid)
            {
                entityItem.Die(EnumDespawnReason.Removed);

                if (entityItem.World.Side == EnumAppSide.Server)
                {
                    WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(entityItem.Itemstack);
                    float litres = entityItem.Itemstack.StackSize / props.ItemsPerLitre;

                    entityItem.World.SpawnCubeParticles(entityItem.SidedPos.XYZ, entityItem.Itemstack, 0.75f, (int)(litres * 2), 0.45f);
                    entityItem.World.PlaySoundAt(new AssetLocation("sounds/environment/smallsplash"), (float)entityItem.SidedPos.X, (float)entityItem.SidedPos.Y, (float)entityItem.SidedPos.Z, null);
                }
            }

            base.OnGroundIdle(entityItem);
        }
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            string strength = Variant["strength"] is string str ? string.Intern(str) : "none";
            JsonObject potion = Attributes?["potioninfo"];
            if (potion?.Exists == true)
            {
                try
                {
                    potionId = potion["potionId"].AsString();
                }
                catch (Exception e)
                {
                    api.World.Logger.Error("Failed loading potion effects for potion {0}. Will ignore. Exception: {1}", Code, e);
                    potionId = "";
                }

                try
                {
                    duration = potion["duration"].AsInt();
                    //api.Logger.Debug("potion {0}, {1}, {2}", potionId, duration);
                }
                catch (Exception e)
                {
                    api.World.Logger.Error("Failed loading potion effects for potion {0}. Will ignore. Exception: {1}", Code, e);
                    duration = 0;
                }
            }
            JsonObject tickPotion = Attributes?["tickpotioninfo"];
            if (tickPotion?.Exists == true)
            {
                try
                {
                    tickSec = tickPotion["ticksec"].AsInt();
                    health = tickPotion["health"].AsFloat();
                    switch (strength)
                    {
                        case "strong":
                            health *= 3;
                            break;
                        case "medium":
                            health *= 2;
                            break;
                        default:
                            break;
                    }
                    //api.Logger.Debug("potion {0}, {1}, {2}", potionId, duration);
                }
                catch (Exception e)
                {
                    api.World.Logger.Error("Failed loading potion effects for potion {0}. Will ignore. Exception: {1}", Code, e);
                    tickSec = 0;
                    health = 0;
                }
            }
            JsonObject effects = Attributes?["effects"];
            if (effects?.Exists == true)
            {
                try
                {
                    dic = effects.AsObject<Dictionary<string, float>>();
                    switch (strength)
                    {
                        case "strong":
                            foreach (var k in dic.Keys.ToList())
                            {
                                dic[k] *= 3;
                            }
                            break;
                        case "medium":
                            foreach (var k in dic.Keys.ToList())
                            {
                                dic[k] *= 2;
                            }
                            break;
                        default:
                            break;
                    }
                }
                catch (Exception e)
                {
                    api.World.Logger.Error("Failed loading potion effects for potion {0}. Will ignore. Exception: {1}", Code, e);
                    dic.Clear();
                }
            }
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            //api.Logger.Debug("potion {0}, {1}", dic.Count, potionId);
            if (potionId != "" && potionId != null)
            {
                string poop = potionId;
                //api.Logger.Debug("[Potion] check if drinkable {0}", byEntity.WatchedAttributes.GetLong(potionId));
                /* This checks if the potion effect callback is on */
                if (byEntity.WatchedAttributes.GetLong(potionId) == 0)
                {
                    byEntity.World.RegisterCallback((dt) =>
                    {
                        if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemInteract)
                        {
                            if (Code.Path.Contains("portion"))
                            {
                                byEntity.World.PlaySoundAt(new AssetLocation("alchemy:sounds/player/drink"), byEntity);
                            }
                            else
                            {
                                byEntity.PlayEntitySound("eat", (byEntity as EntityPlayer)?.Player);
                            }
                        }
                    }, 200);
                    handling = EnumHandHandling.PreventDefault;
                    return;
                }
            }
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            Vec3d pos = byEntity.Pos.AheadCopy(0.4f).XYZ.Add(byEntity.LocalEyePos);
            pos.Y -= 0.4f;

            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new();
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

                byEntity.Controls.UsingHeldItemTransformBefore = tf;


                return secondsUsed <= 1.5f;
            }
            return true;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (secondsUsed > 1.45f && byEntity.World.Side == EnumAppSide.Server)
            {
                TempEffect potionEffect = new();
                if (potionId == "recallpotionid" || potionId == "nutritionpotionid")
                {

                }
                else if (tickSec == 0)
                {
                    potionEffect.TempEntityStats((byEntity as EntityPlayer), dic, "potionmod", duration, potionId);
                }
                else
                {
                    potionEffect.TempTickEntityStats((byEntity as EntityPlayer), dic, "potionmod", duration, potionId, tickSec, health);
                }
                if (byEntity is EntityPlayer)
                {
                    IServerPlayer player = (byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID) as IServerPlayer);
                    if (potionId == "recallpotionid")
                    {
                        if (api.Side.IsServer())
                        {
                            FuzzyEntityPos spawn = player.GetSpawnPosition(false);
                            byEntity.TeleportTo(spawn);
                        }
                        player.SendMessage(
                            GlobalConstants.InfoLogChatGroup,
                            "You feel the effects of the " + slot.Itemstack.GetName(),
                            EnumChatType.Notification
                        );
                    }
                    else if (potionId == "nutritionpotionid")
                    {
                        ITreeAttribute hungerTree = byEntity.WatchedAttributes.GetTreeAttribute("hunger");
                        if (hungerTree != null)
                        {
                            float fruitLevel = hungerTree.GetFloat("fruitLevel");
                            float vegetableLevel = hungerTree.GetFloat("vegetableLevel");
                            float grainLevel = hungerTree.GetFloat("grainLevel");
                            float proteinLevel = hungerTree.GetFloat("proteinLevel");
                            float dairyLevel = hungerTree.GetFloat("dairyLevel");
                            byEntity.World.Logger.Debug("fruit level: {0}", fruitLevel);
                            byEntity.World.Logger.Debug("vegetableLevel: {0}", vegetableLevel);
                            byEntity.World.Logger.Debug("grainLevel: {0}", grainLevel);
                            byEntity.World.Logger.Debug("proteinLevel: {0}", proteinLevel);
                            byEntity.World.Logger.Debug("dairyLevel: {0}", dairyLevel);
                        }
                    }
                    else
                    {
                        player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the " + slot.Itemstack.GetName(), EnumChatType.Notification);
                    }
                }

                slot.TakeOut(1);
                slot.MarkDirty();
            }
        }


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            if (dic != null)
            {
                dsc.AppendLine(Lang.Get("\n"));
                if (dic.TryGetValue("rangedWeaponsAcc", out float rWvalue))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% ranged accuracy", rWvalue * 100));
                }
                if (dic.TryGetValue("animalLootDropRate", out float aLValue))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% more animal loot", aLValue * 100));
                }
                if (dic.TryGetValue("animalHarvestingTime", out float ahValue))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% faster animal harvest", ahValue * 100));
                }
                if (dic.TryGetValue("animalSeekingRange", out float aSValue))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% animal seek range", aSValue * 100));
                }
                if (dic.TryGetValue("maxhealthExtraPoints", out float mHEValue))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0} extra max health", mHEValue));
                }
                if (dic.TryGetValue("forageDropRate", out float fDValue))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% more forage amount", fDValue * 100));
                }
                if (dic.TryGetValue("healingeffectivness", out float hEValue))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% healing effectiveness", hEValue * 100));
                }
                if (dic.TryGetValue("hungerrate", out float hRValue))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% hunger rate", hRValue * 100));
                }
                if (dic.TryGetValue("meleeWeaponsDamage", out float mWValue))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% melee damage", mWValue * 100));
                }
                if (dic.TryGetValue("mechanicalsDamage", out float mDValue))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% mechanincal damage (not sure if works)", mDValue * 100));
                }
                if (dic.TryGetValue("miningSpeedMul", out float mSValue))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% mining speed", mSValue * 100));
                }
                if (dic.TryGetValue("oreDropRate", out float oDValue))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% more ore", oDValue * 100));
                }
                if (dic.TryGetValue("rangedWeaponsDamage", out float rWDValue))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% ranged damage", rWDValue * 100));
                }
                if (dic.TryGetValue("rangedWeaponsSpeed", out float rWSValue))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% ranged speed", rWSValue * 100));
                }
                if (dic.TryGetValue("rustyGearDropRate", out float rGDValue))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% more gears from metal piles", rGDValue * 100));
                }
                if (dic.TryGetValue("walkspeed", out float wSValue))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% walk speed", wSValue * 100));
                }
                if (dic.TryGetValue("vesselContentsDropRate", out float vCDValue))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% more vessel contents", vCDValue * 100));
                }
                if (dic.TryGetValue("wildCropDropRate", out float wCDValue))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% wild crop", wCDValue * 100));
                }
                if (dic.TryGetValue("wholeVesselLootChance", out float wVLValue))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% chance to get whole vessel", wVLValue * 100));
                }
            }

            if (duration != 0)
            {
                dsc.AppendLine(Lang.Get("and lasts for {0} seconds", duration));
            }
            if (health != 0)
            {
                dsc.AppendLine(Lang.Get("When potion is used: {0} health", health));
            }
            if (tickSec != 0)
            {
                dsc.AppendLine(Lang.Get("every {0} seconds", tickSec));
            }
        }
    }
}