using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Alchemy
{

    public class BlockPotion : Block
    {
        public Dictionary<string, float> dic = new Dictionary<string, float>();
        public string potionId;
        public string drankBlockCode;
        public int duration;
        public int tickSec = 0;
        public float health;

        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity forEntity)
        {
            return "eat";
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            string strength = Variant["strength"] is string str ? string.Intern(str) : "weak";
            JsonObject potion = Attributes?["potioninfo"];
            if (potion?.Exists == true)
            {
                try
                {
                    potionId = potion["potionId"].AsString();
                    drankBlockCode = potion["drankBlockCode"].AsString();
                    duration = potion["duration"].AsInt();
                    //api.Logger.Debug("potion {0}, {1}, {2}", potionId, drankBlockCode, duration);
                }
                catch (Exception e)
                {
                    api.World.Logger.Error("Failed loading potion effects for potion {0}. Will ignore. Exception: {1}", Code, e);
                    potionId = "";
                    drankBlockCode = "";
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
                    //api.Logger.Debug("potion {0}, {1}, {2}", potionId, drankBlockCode, duration);
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
            if (potionId != "")
            {
                //api.Logger.Debug("[Potion] check if drinkable {0}", byEntity.WatchedAttributes.GetLong(potionId));
                /* This checks if the potion effect callback is on */
                if (byEntity.WatchedAttributes.GetLong(potionId) == 0)
                {
                    byEntity.World.RegisterCallback((dt) =>
                    {
                        if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemInteract)
                        {
                            byEntity.World.PlaySoundAt(new AssetLocation("alchemy:sounds/player/drink"), byEntity);
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
                TempEffect potionEffect = new TempEffect();
                if (tickSec == 0)
                {
                    potionEffect.tempEntityStats((byEntity as EntityPlayer), dic, "potionmod", duration, potionId);
                }
                else
                {
                    potionEffect.tempTickEntityStats((byEntity as EntityPlayer), dic, "potionmod", duration, potionId, tickSec, health);
                }
                if (byEntity is EntityPlayer)
                {
                    IServerPlayer player = (byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID) as IServerPlayer);
                    player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the " + slot.Itemstack.GetName(), EnumChatType.Notification);
                }
                Block emptyFlask = byEntity.World.GetBlock(AssetLocation.Create(drankBlockCode, slot.Itemstack.Collectible.Code.Domain));
                ItemStack emptyStack = new ItemStack(emptyFlask);
                /*Gives player an empty flask if last in stack or drops an empty flask at players feet*/
                if (slot.Itemstack.StackSize <= 1)
                {
                    slot.Itemstack = emptyStack;
                }
                else
                {
                    IPlayer player = (byEntity as EntityPlayer)?.Player;

                    slot.TakeOut(1);
                    if (!player.InventoryManager.TryGiveItemstack(emptyStack, true))
                    {
                        byEntity.World.SpawnItemEntity(emptyStack, byEntity.SidedPos.XYZ);
                    }
                }

                slot.MarkDirty();
            }
        }


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            if (dic != null)
            {
                if (dic.ContainsKey("rangedWeaponsAcc"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% ranged accuracy", dic["rangedWeaponsAcc"] * 100));
                }
                if (dic.ContainsKey("animalLootDropRate"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% more animal loot", dic["animalLootDropRate"] * 100));
                }
                if (dic.ContainsKey("animalHarvestingTime"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% faster animal harvest", dic["animalHarvestingTime"] * 100));
                }
                if (dic.ContainsKey("animalSeekingRange"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% animal seek range", dic["animalSeekingRange"] * 100));
                }
                if (dic.ContainsKey("maxhealthExtraPoints"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0} extra max health", dic["maxhealthExtraPoints"]));
                }
                if (dic.ContainsKey("forageDropRate"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% more forage amount", dic["forageDropRate"] * 100));
                }
                if (dic.ContainsKey("healingeffectivness"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% healing effectiveness", dic["healingeffectivness"] * 100));
                }
                if (dic.ContainsKey("hungerrate"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% hunger rate", dic["hungerrate"] * 100));
                }
                if (dic.ContainsKey("meleeWeaponsDamage"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% melee damage", dic["meleeWeaponsDamage"] * 100));
                }
                if (dic.ContainsKey("mechanicalsDamage"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% mechanincal damage (not sure if works)", dic["mechanicalsDamage"] * 100));
                }
                if (dic.ContainsKey("miningSpeedMul"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% mining speed", dic["miningSpeedMul"] * 100));
                }
                if (dic.ContainsKey("oreDropRate"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% more ore", dic["oreDropRate"] * 100));
                }
                if (dic.ContainsKey("rangedWeaponsDamage"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% ranged damage", dic["rangedWeaponsDamage"] * 100));
                }
                if (dic.ContainsKey("rangedWeaponsSpeed"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% ranged speed", dic["rangedWeaponsSpeed"] * 100));
                }
                if (dic.ContainsKey("rustyGearDropRate"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% more gears from metal piles", dic["rustyGearDropRate"] * 100));
                }
                if (dic.ContainsKey("walkspeed"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% walk speed", dic["walkspeed"] * 100));
                }
                if (dic.ContainsKey("vesselContentsDropRate"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% more vessel contents", dic["vesselContentsDropRate"] * 100));
                }
                if (dic.ContainsKey("wildCropDropRate"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% wild crop", dic["wildCropDropRate"] * 100));
                }
                if (dic.ContainsKey("wholeVesselLootChance"))
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% chance to get whole vessel", dic["wholeVesselLootChance"] * 100));
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

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return new WorldInteraction[] {
                    /* The ActionLangCode should be heldhelp-drink but it is not working atm */
                    new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-potion-*-drink",
                    MouseButton = EnumMouseButton.Right,
                }
            }.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
    }
}