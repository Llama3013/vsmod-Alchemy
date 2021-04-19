using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.GameContent;
using Vintagestory.API.Server;

namespace Alchemy
{
    public class PotionEffect
    {
        PotionAttrClass attrClass;
        Entity entity;
        ICoreAPI api;
        string potionName;

        public void PotionCheck(Entity byEntity, ItemSlot slot, PotionAttrClass attr, ICoreAPI aapi)
        {
            potionName = slot.Itemstack.GetName();
            attrClass = attr;
            entity = byEntity;
            api = aapi;

            if (attrClass.accuracy != 0)
            {
                entity.Stats.Set("rangedWeaponsAcc", "potionmod", attrClass.accuracy, false);
            }
            if (attrClass.animalloot != 0)
            {
                entity.Stats.Set("animalLootDropRate", "potionmod", attrClass.animalloot, false);
            }
            if (attrClass.animalharvest != 0)
            {
                entity.Stats.Set("animalHarvestingTime", "potionmod", attrClass.animalharvest, false);
            }
            if (attrClass.animalseek != 0)
            {
                entity.Stats.Set("animalSeekingRange", "potionmod", attrClass.animalseek, false);
            }
            if (attrClass.extrahealth != 0)
            {
                entity.Stats.Set("maxhealthExtraPoints", "potionmod", attrClass.extrahealth, false);
                EntityBehaviorHealth ebh = entity.GetBehavior<EntityBehaviorHealth>();
                ebh.MarkDirty();
            }
            if (attrClass.forage != 0)
            {
                entity.Stats.Set("forageDropRate", "potionmod", attrClass.forage, false);
            }
            if (attrClass.healingeffect != 0)
            {
                entity.Stats.Set("healingeffectivness", "potionmod", attrClass.healingeffect, false);
            }
            if (attrClass.hunger != 0)
            {
                entity.Stats.Set("hungerrate", "potionmod", attrClass.hunger, false);
            }
            if (attrClass.mechdamage != 0)
            {
                entity.Stats.Set("mechanicalsDamage", "potionmod", attrClass.mechdamage, false);
            }
            if (attrClass.melee != 0)
            {
                entity.Stats.Set("meleeWeaponsDamage", "potionmod", attrClass.melee, false);
            }
            if (attrClass.mining != 0)
            {
                entity.Stats.Set("miningSpeedMul", "potionmod", attrClass.mining, false);
            }
            if (attrClass.ore != 0)
            {
                entity.Stats.Set("oreDropRate", "potionmod", attrClass.ore, false);
            }
            if (attrClass.rangeddamage != 0)
            {
                entity.Stats.Set("rangedWeaponsDamage", "potionmod", attrClass.rangeddamage, false);
            }
            if (attrClass.rangedspeed != 0)
            {
                entity.Stats.Set("rangedWeaponsSpeed", "potionmod", attrClass.rangedspeed, false);
            }
            if (attrClass.rustygear != 0)
            {
                entity.Stats.Set("rustyGearDropRate", "potionmod", attrClass.rustygear, false);
            }
            if (attrClass.speed != 0)
            {
                entity.Stats.Set("walkspeed", "potionmod", attrClass.speed, false);
            }
            if (attrClass.vesselcontent != 0)
            {
                entity.Stats.Set("vesselContentsDropRate", "potionmod", attrClass.vesselcontent, false);
            }
            if (attrClass.wildcrop != 0)
            {
                entity.Stats.Set("wildCropDropRate", "potionmod", attrClass.wildcrop, false);
            }

            if (attrClass.duration != 0 && attrClass.ticksec != 0 && attrClass.health != 0)
            {
                long potionListenerId = entity.World.RegisterGameTickListener(onPotionTick, 1000);

                /*This saves the listenerId for registerCallback to the player's stats so I unregister it later*/
                entity.WatchedAttributes.SetLong(attrClass.potionid, potionListenerId);
            }
            else if (attrClass.duration != 0)
            {
                long potionListenerId = entity.World.RegisterCallback(onPotionCall, (1000 * attrClass.duration));
                /*This saves the listenerId for registerCallback to the player's stats so I unregister it later*/
                entity.WatchedAttributes.SetLong(attrClass.potionid, potionListenerId);
            }

            if (entity is EntityPlayer)
            {
                IServerPlayer player = (entity.World.PlayerByUid((entity as EntityPlayer).PlayerUID) as IServerPlayer);
                player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the " + potionName, EnumChatType.Notification);
            }

            Block emptyFlask = api.World.GetBlock(AssetLocation.Create(attrClass.drankBlockCode, slot.Itemstack.Collectible.Code.Domain));
            ItemStack emptyStack = new ItemStack(emptyFlask);
            /*Gives player an empty flask if last in stack or drops an empty flask at players feet*/
            if (slot.Itemstack.StackSize <= 1)
            {
                slot.Itemstack = emptyStack;
            }
            else
            {
                IPlayer player = (entity as EntityPlayer)?.Player;

                slot.TakeOut(1);
                if (!player.InventoryManager.TryGiveItemstack(emptyStack, true))
                {
                    entity.World.SpawnItemEntity(emptyStack, entity.SidedPos.XYZ);
                }
            }

            slot.MarkDirty();
        }
        private int tickCnt;

        void onPotionTick(float dt)
        {
            tickCnt++;

            /*This if statement passes every tickSec amount of seconds*/
            if (attrClass.ticksec != 0)
            {
                if (tickCnt % attrClass.ticksec == 0)
                {
                    //api.Logger.Debug("Potion tickSec: {0}", attrClass.ticksec);
                    float health = attrClass.health;
                    entity.ReceiveDamage(new DamageSource()
                    {
                        Source = EnumDamageSource.Internal,
                        Type = health > 0 ? EnumDamageType.Heal : EnumDamageType.Poison
                    }, Math.Abs(health));
                }
            }

            /*This if statement passes when duration amount of seconds pass*/
            if (tickCnt >= attrClass.duration)
            {
                resetPotions();
                long potionListenerId = entity.WatchedAttributes.GetLong(attrClass.potionid);
                //api.Logger.Debug("[Potion] gameticklistenerid to be reset: {0} and entity id {1}", potionListenerId.ToString(), (entity as EntityPlayer).PlayerUID);
                entity.World.UnregisterGameTickListener(potionListenerId);
                /*This resets the potion listenerId that is attached to the player*/
                entity.WatchedAttributes.RemoveAttribute(attrClass.potionid);
                tickCnt = 0;
            }
        }

        void onPotionCall(float dt)
        {
            resetPotions();
            if (attrClass.potionid != "")
            {
                //api.Logger.Debug("[Potion] potionid to be reset: {0} and entity id {1}", attrClass.potionid, (entity as EntityPlayer).PlayerUID);
                entity.WatchedAttributes.RemoveAttribute(attrClass.potionid);
            }
        }

        void resetPotions()
        {
            if (attrClass.accuracy != 0)
            {
                entity.Stats.Set("rangedWeaponsAcc", "potionmod", 0, false);
            }
            if (attrClass.animalloot != 0)
            {
                entity.Stats.Set("animalLootDropRate", "potionmod", 0, false);
            }
            if (attrClass.animalharvest != 0)
            {
                entity.Stats.Set("animalHarvestingTime", "potionmod", 0, false);
            }
            if (attrClass.animalseek != 0)
            {
                entity.Stats.Set("animalSeekingRange", "potionmod", 0, false);
            }
            if (attrClass.extrahealth != 0)
            {
                entity.Stats.Set("maxhealthExtraPoints", "potionmod", 0, false);
                EntityBehaviorHealth ebh = entity.GetBehavior<EntityBehaviorHealth>();
                ebh.MarkDirty();
            }
            if (attrClass.forage != 0)
            {
                entity.Stats.Set("forageDropRate", "potionmod", 0, false);
            }
            if (attrClass.healingeffect != 0)
            {
                entity.Stats.Set("healingeffectivness", "potionmod", 0, false);
            }
            if (attrClass.hunger != 0)
            {
                entity.Stats.Set("hungerrate", "potionmod", 0, false);
            }
            if (attrClass.mechdamage != 0)
            {
                entity.Stats.Set("mechanicalsDamage", "potionmod", 0, false);
            }
            if (attrClass.melee != 0)
            {
                entity.Stats.Set("meleeWeaponsDamage", "potionmod", 0, false);
            }
            if (attrClass.mining != 0)
            {
                entity.Stats.Set("miningSpeedMul", "potionmod", 0, false);
            }
            if (attrClass.ore != 0)
            {
                entity.Stats.Set("oreDropRate", "potionmod", 0, false);
            }
            if (attrClass.rangeddamage != 0)
            {
                entity.Stats.Set("rangedWeaponsDamage", "potionmod", 0, false);
            }
            if (attrClass.rangedspeed != 0)
            {
                entity.Stats.Set("rangedWeaponsSpeed", "potionmod", 0, false);
            }
            if (attrClass.rustygear != 0)
            {
                entity.Stats.Set("rustyGearDropRate", "potionmod", 0, false);
            }
            if (attrClass.speed != 0)
            {
                entity.Stats.Set("walkspeed", "potionmod", 0, false);
            }
            if (attrClass.vesselcontent != 0)
            {
                entity.Stats.Set("vesselContentsDropRate", "potionmod", 0, false);
            }
            if (attrClass.wildcrop != 0)
            {
                entity.Stats.Set("wildCropDropRate", "potionmod", 0, false);
            }

            if (entity is EntityPlayer)
            {
                IServerPlayer player = (entity.World.PlayerByUid((entity as EntityPlayer).PlayerUID) as IServerPlayer);
                player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the " + potionName + " dissipate.", EnumChatType.Notification);
            }
        }
    }
}