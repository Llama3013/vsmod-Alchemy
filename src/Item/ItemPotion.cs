using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Alchemy
{
    public class ItemPotion : Item
    {
        public override void OnGroundIdle(EntityItem entityItem)
        {
            entityItem.Die(EnumDespawnReason.Removed);

            if (entityItem.World.Side == EnumAppSide.Server)
            {
                WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(entityItem.Itemstack);
                float litres = (float)entityItem.Itemstack.StackSize / props.ItemsPerLitre;

                entityItem.World.SpawnCubeParticles(entityItem.SidedPos.XYZ, entityItem.Itemstack, 0.75f, (int)(litres * 2), 0.45f);
                entityItem.World.PlaySoundAt(new AssetLocation("sounds/environment/smallsplash"), (float)entityItem.SidedPos.X, (float)entityItem.SidedPos.Y, (float)entityItem.SidedPos.Z, null);
            }


            base.OnGroundIdle(entityItem);

        }
        public Dictionary<string, float> essencesDic = new Dictionary<string, float>();
        public int duration;
        public int tickSec = 0;
        public float health;

        Dictionary<string, float> maxEssenceDic;

        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity forEntity)
        {
            return "eat";
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            try
            {
                IAsset maxEssences = api.Assets.TryGet("alchemy:config/essences.json");
                if (maxEssences != null)
                {
                    maxEssenceDic = maxEssences.ToObject<Dictionary<string, float>>();
                }
                //api.Logger.Debug("potion {0}, {1}, {2}", potionId, duration);
            }
            catch (Exception e)
            {
                api.World.Logger.Error("Failed loading potion effects for potion {0}. Will ignore. Exception: {1}", Code, e);
            }
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            try
            {
                ITreeAttribute potion = (ITreeAttribute)slot.Itemstack.Attributes;
                if (potion != null)
                {

                    essencesDic.Clear();
                    foreach (var essence in maxEssenceDic.Keys.ToList())
                    {
                        if (potion.TryGetFloat("potion" + essence) != null)
                        {
                            if (!essencesDic.ContainsKey(essence)) essencesDic.Add(essence, 0);
                            essencesDic[essence] = potion.GetFloat("potion" + essence);
                        }
                    }

                    //api.Logger.Debug("potion {0}, {1}, {2}", potionId, duration);
                }
            }
            catch (Exception e)
            {
                api.World.Logger.Error("Failed loading potion effects for potion {0}. Will ignore. Exception: {1}", Code, e);
                duration = 0;
            }
            //api.Logger.Debug("potion {0}, {1}", essencesDic.Count, potionId);
            //api.Logger.Debug("[Potion] check if drinkable {0}", byEntity.WatchedAttributes.GetLong(potionId));
            /* This checks if the potion effect callback is on */
            if (essencesDic.Count > 0)
            {
                if (byEntity.WatchedAttributes.GetLong("potionid") == 0)
                {
                    byEntity.World.RegisterCallback((dt) => playEatSound(byEntity, "drink", 1), 500);
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

            IPlayer player = (byEntity as EntityPlayer).Player;


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

                byEntity.Controls.UsingHeldItemTransformBefore = tf;


                return secondsUsed <= 1.5f;
            }
            return true;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (secondsUsed > 1.45f && byEntity.World.Side == EnumAppSide.Server)
            {
                if (tickSec == 0)
                {
                    TempEffect potionEffect = new TempEffect();
                    potionEffect.tempEntityStats((byEntity as EntityPlayer), essencesDic);
                }
                else
                {
                    TempEffect potionEffect = new TempEffect();
                    potionEffect.tempTickEntityStats((byEntity as EntityPlayer), essencesDic, tickSec, health);
                }
                if (byEntity is EntityPlayer)
                {
                    IServerPlayer player = (byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID) as IServerPlayer);
                    if (essencesDic.ContainsKey("recall"))
                    {
                        if (api.Side.IsServer())
                        {
                            FuzzyEntityPos spawn = player.GetSpawnPosition(false);
                            byEntity.TeleportTo(spawn);
                        }
                        player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the " + slot.Itemstack.GetName(), EnumChatType.Notification);
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
            ITreeAttribute potion = (ITreeAttribute)inSlot.Itemstack.Attributes;
            if (potion != null)
            {
                try
                {
                    essencesDic.Clear();
                    foreach (var essence in maxEssenceDic.Keys.ToList())
                    {
                        if (potion.TryGetFloat("potion" + essence) != null)
                        {
                            if (!essencesDic.ContainsKey(essence)) essencesDic.Add(essence, 0);
                            essencesDic[essence] = potion.GetFloat("potion" + essence);
                        }
                    }

                    //api.Logger.Debug("potion {0}, {1}, {2}", potionId, duration);
                }
                catch (Exception e)
                {
                    api.World.Logger.Error("Failed loading potion effects for potion {0}. Will ignore. Exception: {1}", Code, e);
                    duration = 0;
                }
            }
            if (essencesDic != null)
            {
                dsc.AppendLine(Lang.Get("\n"));
                float value;
                if (essencesDic.TryGetValue("rangedWeaponsAcc", out value))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% ranged accuracy", value * 100));
                }
                if (essencesDic.TryGetValue("animalLootDropRate", out value))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% more animal loot", value * 100));
                }
                if (essencesDic.TryGetValue("animalHarvestingTime", out value))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% faster animal harvest", value * 100));
                }
                if (essencesDic.TryGetValue("animalSeekingRange", out value))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% animal seek range", value * 100));
                }
                if (essencesDic.TryGetValue("maxhealthExtraPoints", out value))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0} extra max health points", value));
                }
                if (essencesDic.TryGetValue("forageDropRate", out value))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% more forage amount", value * 100));
                }
                if (essencesDic.TryGetValue("healingeffectivness", out value))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% healing effectiveness", value * 100));
                }
                if (essencesDic.TryGetValue("hungerrate", out value))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% hunger rate", value * 100));
                }
                if (essencesDic.TryGetValue("meleeWeaponsDamage", out value))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% melee damage", value * 100));
                }
                if (essencesDic.TryGetValue("mechanicalsDamage", out value))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% mechanincal damage (not sure if works)", value * 100));
                }
                if (essencesDic.TryGetValue("miningSpeedMul", out value))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% mining speed", value * 100));
                }
                if (essencesDic.TryGetValue("oreDropRate", out value))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% more ore", value * 100));
                }
                if (essencesDic.TryGetValue("rangedWeaponsDamage", out value))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% ranged damage", value * 100));
                }
                if (essencesDic.TryGetValue("rangedWeaponsSpeed", out value))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% ranged speed", value * 100));
                }
                if (essencesDic.TryGetValue("rustyGearDropRate", out value))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% more gears from metal piles", value * 100));
                }
                if (essencesDic.TryGetValue("walkspeed", out value))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% walk speed", value * 100));
                }
                if (essencesDic.TryGetValue("vesselContentsDropRate", out value))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% more vessel contents", value * 100));
                }
                if (essencesDic.TryGetValue("wildCropDropRate", out value))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% wild crop", value * 100));
                }
                if (essencesDic.TryGetValue("wholeVesselLootChance", out value))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% chance to get whole vessel", value * 100));
                }
                if (essencesDic.TryGetValue("glow", out value))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: player starts to glow"));
                }
                if (essencesDic.TryGetValue("recall", out value))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: player teleports home"));
                }
                if (essencesDic.TryGetValue("duration", out value))
                {
                    dsc.Append(Lang.Get(" and lasts for {0} seconds", value));
                }
                if (essencesDic.TryGetValue("health", out value))
                {
                    dsc.Append(Lang.Get(" and lasts for {0} seconds", value));
                }
            }
        }
    }
}