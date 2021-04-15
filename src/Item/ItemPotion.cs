using System;
using System.Text;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Util;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using System.Collections.Generic;

namespace Alchemy
{
    public class ItemPotion : Item
    {
        
        Dictionary<string, float> potionsDate = new Dictionary<string, float>();
        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity forEntity)
        {
            return "eat";
        }

        EntityAgent potionEntity;
        JsonObject attr;

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            attr = slot.Itemstack.Collectible.Attributes;
            if (attr != null && attr["potionId"].Exists)
            {
                //api.Logger.Debug("[Potion] check if drinkable {0} and {1}", attr["potionId"].ToString(), byEntity.WatchedAttributes.GetLong(attr["potionId"].ToString()));
                /*This checks if the potion effect callback is on*/
                if (byEntity.WatchedAttributes.GetLong(attr["potionId"].ToString()) == 0)
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

        string potionName;
        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (secondsUsed > 1.45f && byEntity.World.Side == EnumAppSide.Server)
            {
                potionName = slot.Itemstack.GetName();
                potionEntity = byEntity;

                if (attr["accuracy"].Exists)
                {
                    potionEntity.Stats.Set("rangedWeaponsAcc", "potionmod", attr["accuracy"].AsFloat(), false);
                }
                if (attr["animalloot"].Exists)
                {
                    potionEntity.Stats.Set("animalLootDropRate", "potionmod", attr["animalloot"].AsFloat(), false);
                }
                if (attr["animalharvest"].Exists)
                {
                    potionEntity.Stats.Set("animalHarvestingTime", "potionmod", attr["animalharvest"].AsFloat(), false);
                }
                if (attr["animalseek"].Exists)
                {
                    potionEntity.Stats.Set("animalSeekingRange", "potionmod", attr["animalseek"].AsFloat(), false);
                }
                if (attr["extrahealth"].Exists)
                {
                    potionEntity.Stats.Set("maxhealthExtraPoints", "potionmod", attr["extrahealth"].AsFloat(), false);
                    EntityBehaviorHealth ebh = potionEntity.GetBehavior<EntityBehaviorHealth>();
                    ebh.MarkDirty();
                }
                if (attr["forage"].Exists)
                {
                    potionEntity.Stats.Set("forageDropRate", "potionmod", attr["forage"].AsFloat(), false);
                }
                if (attr["healingeffect"].Exists)
                {
                    potionEntity.Stats.Set("healingeffectivness", "potionmod", attr["healingeffect"].AsFloat(), false);
                }
                if (attr["hunger"].Exists)
                {
                    potionEntity.Stats.Set("hungerrate", "potionmod", attr["hunger"].AsFloat(), false);
                }
                if (attr["mechdamage"].Exists)
                {
                    potionEntity.Stats.Set("mechanicalsDamage", "potionmod", attr["mechdamage"].AsFloat(), false);
                }
                if (attr["melee"].Exists)
                {
                    potionEntity.Stats.Set("meleeWeaponsDamage", "potionmod", attr["melee"].AsFloat(), false);
                }
                if (attr["mining"].Exists)
                {
                    potionEntity.Stats.Set("miningSpeedMul", "potionmod", attr["mining"].AsFloat(), false);
                }
                if (attr["ore"].Exists)
                {
                    potionEntity.Stats.Set("oreDropRate", "potionmod", attr["ore"].AsFloat(), false);
                }
                if (attr["rangeddamage"].Exists)
                {
                    potionEntity.Stats.Set("rangedWeaponsDamage", "potionmod", attr["rangeddamage"].AsFloat(), false);
                }
                if (attr["rangedspeed"].Exists)
                {
                    potionEntity.Stats.Set("rangedWeaponsSpeed", "potionmod", attr["rangedspeed"].AsFloat(), false);
                }
                if (attr["rustygear"].Exists)
                {
                    potionEntity.Stats.Set("rustyGearDropRate", "potionmod", attr["rustygear"].AsFloat(), false);
                }
                if (attr["speed"].Exists)
                {
                    potionEntity.Stats.Set("walkspeed", "potionmod", attr["speed"].AsFloat(), false);
                }
                if (attr["vesselcontent"].Exists)
                {
                    potionEntity.Stats.Set("vesselContentsDropRate", "potionmod", attr["vesselcontent"].AsFloat(), false);
                }
                if (attr["wildcrop"].Exists)
                {
                    potionEntity.Stats.Set("wildCropDropRate", "potionmod", attr["wildcrop"].AsFloat(), false);
                }

                if (attr["duration"].Exists && attr["ticksec"].Exists && attr["health"].Exists)
                {
                    long potionListenerId = api.World.RegisterGameTickListener(onPotionTick, 1000);

                    /*This saves the listenerId for registerCallback to the player's stats so I unregister it later*/
                    potionEntity.WatchedAttributes.SetLong(attr["potionId"].ToString(), potionListenerId);
                }
                else if (attr["duration"].Exists)
                {
                    int duration = attr["duration"].AsInt();
                    long potionListenerId = potionEntity.World.RegisterCallback(onPotionCall, (1000 * duration));
                    /*This saves the listenerId for registerCallback to the player's stats so I unregister it later*/
                    potionEntity.WatchedAttributes.SetLong(attr["potionId"].ToString(), potionListenerId);
                }

                if (potionEntity is EntityPlayer)
                {
                    IServerPlayer player = (potionEntity.World.PlayerByUid((potionEntity as EntityPlayer).PlayerUID) as IServerPlayer);
                    player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the " + potionName, EnumChatType.Notification);
                }

                /*These three lines adds the attribute amount to the player's stats*/

                Block emptyFlask = api.World.GetBlock(AssetLocation.Create(slot.Itemstack.Collectible.Attributes["drankBlockCode"].AsString(), slot.Itemstack.Collectible.Code.Domain));
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

        int tickCnt = 0;
        private void onPotionTick(float dt)
        {
            tickCnt++;

            int tickSec = attr["ticksec"].AsInt();
            //api.Logger.Debug("Potion tickSec: {0}", tickSec);
            /*This if statement passes every tickSec amount of seconds*/
            if (tickSec != 0)
            {
                if (tickCnt % tickSec == 0)
                {
                    float health = attr["health"].AsFloat();
                    potionEntity.ReceiveDamage(new DamageSource()
                    {
                        Source = EnumDamageSource.Internal,
                        Type = health > 0 ? EnumDamageType.Heal : EnumDamageType.Poison
                    }, Math.Abs(health));
                }
            }

            int duration = attr["duration"].AsInt();
            /*This if statement passes when duration amount of seconds pass*/
            if (tickCnt >= duration)
            {
                resetPotions();
                long potionListenerId = potionEntity.WatchedAttributes.GetLong(attr["potionId"].ToString());
                //api.Logger.Debug("[Potion] gameticklistenerid to be reset: {0}", potionListenerId.ToString());
                potionEntity.World.UnregisterGameTickListener(potionListenerId);
                /*This resets the potion listenerId that is attached to the player*/
                potionEntity.WatchedAttributes.RemoveAttribute(attr["potionId"].ToString());
                tickCnt = 0;
            }
        }

        private void onPotionCall(float dt)
        {
            resetPotions();
            if (attr["potionId"].Exists)
            {
                potionEntity.WatchedAttributes.RemoveAttribute(attr["potionId"].ToString());
            }
        }

        private void resetPotions()
        {
            if (attr["accuracy"].Exists)
            {
                //api.Logger.Debug("[Potion] accuracy before: {0}", potionEntity.Stats.GetBlended("rangedWeaponsAcc"));
                potionEntity.Stats.Set("rangedWeaponsAcc", "potionmod", 0, false);
                //api.Logger.Debug("[Potion] accuracy after: {0}", potionEntity.Stats.GetBlended("rangedWeaponsAcc"));
            }
            if (attr["animalloot"].Exists)
            {
                //api.Logger.Debug("[Potion] animalloot before: {0}", potionEntity.Stats.GetBlended("animalLootDropRate"));
                potionEntity.Stats.Set("animalLootDropRate", "potionmod", 0, false);
                //api.Logger.Debug("[Potion] animalloot after: {0}", potionEntity.Stats.GetBlended("animalLootDropRate"));
            }
            if (attr["animalharvest"].Exists)
            {
                //api.Logger.Debug("[Potion] animalharvest before: {0}", potionEntity.Stats.GetBlended("animalHarvestingTime"));
                potionEntity.Stats.Set("animalHarvestingTime", "potionmod", 0, false);
                //api.Logger.Debug("[Potion] animalharvest after: {0}", potionEntity.Stats.GetBlended("animalHarvestingTime"));
            }
            if (attr["animalseek"].Exists)
            {
                //api.Logger.Debug("[Potion] animalseek before: {0}", potionEntity.Stats.GetBlended("animalSeekingRange"));
                potionEntity.Stats.Set("animalSeekingRange", "potionmod", 0, false);
                //api.Logger.Debug("[Potion] animalseek after: {0}", potionEntity.Stats.GetBlended("animalSeekingRange"));
            }
            if (attr["extrahealth"].Exists)
            {
                //api.Logger.Debug("[Potion] extrahealth before: {0}", potionEntity.Stats.GetBlended("maxhealthExtraPoints"));
                potionEntity.Stats.Set("maxhealthExtraPoints", "potionmod", 0, false);
                EntityBehaviorHealth ebh = potionEntity.GetBehavior<EntityBehaviorHealth>();
                ebh.MarkDirty();
                //api.Logger.Debug("[Potion] extrahealth after: {0}", potionEntity.Stats.GetBlended("maxhealthExtraPoints"));
            }
            if (attr["forage"].Exists)
            {
                //api.Logger.Debug("[Potion] forage before: {0}", potionEntity.Stats.GetBlended("forageDropRate"));
                potionEntity.Stats.Set("forageDropRate", "potionmod", 0, false);
                //api.Logger.Debug("[Potion] forage after: {0}", potionEntity.Stats.GetBlended("forageDropRate"));
            }
            if (attr["healingeffect"].Exists)
            {
                //api.Logger.Debug("[Potion] healingeffect before: {0}", potionEntity.Stats.GetBlended("healingeffectivness"));
                potionEntity.Stats.Set("healingeffectivness", "potionmod", 0, false);
                //api.Logger.Debug("[Potion] healingeffect after: {0}", potionEntity.Stats.GetBlended("healingeffectivness"));
            }
            if (attr["hunger"].Exists)
            {
                //api.Logger.Debug("[Potion] hunger before: {0}", potionEntity.Stats.GetBlended("hungerrate"));
                potionEntity.Stats.Set("hungerrate", "potionmod", 0, false);
                //api.Logger.Debug("[Potion] hunger after: {0}", potionEntity.Stats.GetBlended("hungerrate"));
            }
            if (attr["mechdamage"].Exists)
            {
                //api.Logger.Debug("[Potion] mechdamage before: {0}", potionEntity.Stats.GetBlended("mechanicalsDamage"));
                potionEntity.Stats.Set("mechanicalsDamage", "potionmod", 0, false);
                //api.Logger.Debug("[Potion] mechdamage after: {0}", potionEntity.Stats.GetBlended("mechanicalsDamage"));
            }
            if (attr["melee"].Exists)
            {
                //api.Logger.Debug("[Potion] melee before: {0}", potionEntity.Stats.GetBlended("meleeWeaponsDamage"));
                potionEntity.Stats.Set("meleeWeaponsDamage", "potionmod", 0, false);
                //api.Logger.Debug("[Potion] melee before: {0}", potionEntity.Stats.GetBlended("meleeWeaponsDamage"));
            }
            if (attr["mining"].Exists)
            {
                //api.Logger.Debug("[Potion] mining before: {0}", potionEntity.Stats.GetBlended("miningSpeedMul"));
                potionEntity.Stats.Set("miningSpeedMul", "potionmod", 0, false);
                //api.Logger.Debug("[Potion] mining after: {0}", potionEntity.Stats.GetBlended("miningSpeedMul"));
            }
            if (attr["ore"].Exists)
            {
                //api.Logger.Debug("[Potion] ore before: {0}", potionEntity.Stats.GetBlended("oreDropRate"));
                potionEntity.Stats.Set("oreDropRate", "potionmod", 0, false);
                //api.Logger.Debug("[Potion] ore after: {0}", potionEntity.Stats.GetBlended("oreDropRate"));
            }
            if (attr["rangeddamage"].Exists)
            {
                //api.Logger.Debug("[Potion] rangeddamage before: {0}", potionEntity.Stats.GetBlended("rangedWeaponsDamage"));
                potionEntity.Stats.Set("rangedWeaponsDamage", "potionmod", 0, false);
                //api.Logger.Debug("[Potion] rangeddamage after: {0}", potionEntity.Stats.GetBlended("rangedWeaponsDamage"));
            }
            if (attr["rangedspeed"].Exists)
            {
                //api.Logger.Debug("[Potion] rangedspeed before: {0}", potionEntity.Stats.GetBlended("rangedWeaponsSpeed"));
                potionEntity.Stats.Set("rangedWeaponsSpeed", "potionmod", 0, false);
                //api.Logger.Debug("[Potion] rangedspeed after: {0}", potionEntity.Stats.GetBlended("rangedWeaponsSpeed"));
            }
            if (attr["rustygear"].Exists)
            {
                //api.Logger.Debug("[Potion] rustygear before: {0}", potionEntity.Stats.GetBlended("rustyGearDropRate"));
                potionEntity.Stats.Set("rustyGearDropRate", "potionmod", 0, false);
                //api.Logger.Debug("[Potion] rustygear after: {0}", potionEntity.Stats.GetBlended("rustyGearDropRate"));
            }
            if (attr["speed"].Exists)
            {
                //api.Logger.Debug("[Potion] speed before: {0}", potionEntity.Stats.GetBlended("walkspeed"));
                potionEntity.Stats.Set("walkspeed", "potionmod", 0, false);
                //api.Logger.Debug("[Potion] speed after: {0}", potionEntity.Stats.GetBlended("walkspeed"));
            }
            if (attr["vesselcontent"].Exists)
            {
                //api.Logger.Debug("[Potion] speed before: {0}", potionEntity.Stats.GetBlended("vesselContentsDropRate"));
                potionEntity.Stats.Set("vesselContentsDropRate", "potionmod", 0, false);
                //api.Logger.Debug("[Potion] speed after: {0}", potionEntity.Stats.GetBlended("vesselContentsDropRate"));
            }
            if (attr["wildcrop"].Exists)
            {
                ////api.Logger.Debug("[Potion] wildcrop before: {0}", potionEntity.Stats.GetBlended("wildCropDropRate"));
                potionEntity.Stats.Set("wildCropDropRate", "potionmod", 0, false);
                ////api.Logger.Debug("[Potion] wildcrop after: {0}", potionEntity.Stats.GetBlended("wildCropDropRate"));
            }

            if (potionEntity is EntityPlayer)
            {
                IServerPlayer player = (potionEntity.World.PlayerByUid((potionEntity as EntityPlayer).PlayerUID) as IServerPlayer);
                player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the " + potionName + " dissipate.", EnumChatType.Notification);
            }
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            JsonObject attr = inSlot.Itemstack.Collectible.Attributes;
            if (attr != null)
            {
                if (attr["accuracy"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% ranged accuracy", attr["accuracy"].AsFloat() * 100));
                }
                if (attr["animalloot"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% more animal loot", attr["animalloot"].AsFloat() * 100));
                }
                if (attr["animalharvest"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% faster animal harvest", attr["animalharvest"].AsFloat() * 100));
                }
                if (attr["animalseek"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% animal seek range", attr["animalseek"].AsFloat() * 100));
                }
                if (attr["extrahealth"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0} extra max health", attr["extrahealth"].AsFloat()));
                }
                if (attr["forage"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% more forage amount", attr["forage"].AsFloat() * 100));
                }
                if (attr["healingeffect"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% healing effectiveness", attr["healingeffect"].AsFloat() * 100));
                }
                if (attr["hunger"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% hunger rate", attr["hunger"].AsFloat() * 100));
                }
                if (attr["melee"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% melee damage", attr["melee"].AsFloat() * 100));
                }
                if (attr["mechdamage"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% mechanincal damage (not sure if works)", attr["mechdamage"].AsFloat() * 100));
                }
                if (attr["mining"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% mining speed", attr["mining"].AsFloat() * 100));
                }
                if (attr["ore"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% more ore", attr["ore"].AsFloat() * 100));
                }
                if (attr["rangeddamage"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% ranged damage", attr["rangeddamage"].AsFloat() * 100));
                }
                if (attr["rangedspeed"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% ranged speed", attr["rangedspeed"].AsFloat() * 100));
                }
                if (attr["rustygear"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% more gears from metal piles", attr["rustygear"].AsFloat() * 100));
                }
                if (attr["speed"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% walk speed", attr["speed"].AsFloat() * 100));
                }
                if (attr["vesselcontent"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% more vessel contents", attr["vesselcontent"].AsFloat() * 100));
                }
                if (attr["wildcrop"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% wild crop", attr["wildcrop"].AsFloat() * 100));
                }
                if (attr["health"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0} health", attr["health"].AsFloat()));
                }
                if (attr["duration"].Exists)
                {
                    dsc.AppendLine(Lang.Get("and lasts for {0} seconds", attr["duration"].AsInt()));
                }
                if (attr["ticksec"].Exists)
                {
                    dsc.AppendLine(Lang.Get("every {0} seconds", attr["ticksec"].AsInt()));
                }
            }
        }


        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[] {
                new WorldInteraction()
                {
                    /* The ActionLangCode should be heldhelp-drink but it is not working atm */
                    ActionLangCode = "Drink",
                    MouseButton = EnumMouseButton.Right
                }
            }.Append(base.GetHeldInteractionHelp(inSlot));
        }
    }
}