using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Alchemy
{
    public class PotionFixBehavior : EntityBehavior
    {
        private ModConfig config;

        public PotionFixBehavior(Entity entity, ModConfig config) : base(entity)
        {
            this.config = config;
        }

        public override string PropertyName()
        {
            return "PotionFixBehavior";
        }

        private IServerPlayer GetIServerPlayer()
        {
            return this.entity.World.PlayerByUid((this.entity as EntityPlayer).PlayerUID) as IServerPlayer;
        }

        /* This override is to add the behavior to the player of when they die they also reset all of their potion effects */
        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            IServerPlayer player = GetIServerPlayer();

            bool potionReseted = false;
            string potionId = "potionid";
            string tickPotionId = "tickpotionid";
            string[] attributeKey = entity.WatchedAttributes.Keys;
            int attributeAmnt = entity.WatchedAttributes.Count;
            for (int i = 0; attributeAmnt > i; i++)
            {
                if (attributeKey[i].Contains(potionId))
                {
                    try
                    {
                        long potionListenerId = entity.WatchedAttributes.GetLong(attributeKey[i]);
                        if (potionListenerId != 0)
                        {
                            potionReseted = true;
                            if (attributeKey[i].Contains(tickPotionId))
                            {
                                entity.World.UnregisterGameTickListener(potionListenerId);
                            }
                            else
                            {
                                entity.World.UnregisterCallback(potionListenerId);
                            }
                            entity.WatchedAttributes.RemoveAttribute(attributeKey[i]);
                        }
                    }
                    catch (InvalidCastException)
                    {
                        entity.WatchedAttributes.RemoveAttribute(attributeKey[i]);
                    }
                }
            }

            entity.Stats.Set("healingeffectivness", "potionmod", 0, false);
            entity.Stats.Set("maxhealthExtraPoints", "potionmod", 0, false);
            EntityBehaviorHealth ebh = entity.GetBehavior<EntityBehaviorHealth>();
            ebh.MarkDirty();
            entity.Stats.Set("walkspeed", "potionmod", 0, false);
            entity.Stats.Set("hungerrate", "potionmod", 0, false);
            entity.Stats.Set("rangedWeaponsAcc", "potionmod", 0, false);
            entity.Stats.Set("miningSpeedMul", "potionmod", 0, false);
            entity.Stats.Set("walkspeed", "potionmod", 0, false);
            entity.Stats.Set("rangedWeaponsSpeed", "potionmod", 0, false);
            entity.Stats.Set("rangedWeaponsDamage", "potionmod", 0, false);
            entity.Stats.Set("meleeWeaponsDamage", "potionmod", 0, false);
            entity.Stats.Set("mechanicalsDamage", "potionmod", 0, false);
            entity.Stats.Set("animalLootDropRate", "potionmod", 0, false);
            entity.Stats.Set("forageDropRate", "potionmod", 0, false);
            entity.Stats.Set("vesselContentsDropRate", "potionmod", 0, false);
            entity.Stats.Set("wildCropDropRate", "potionmod", 0, false);
            entity.Stats.Set("oreDropRate", "potionmod", 0, false);
            entity.Stats.Set("rustyGearDropRate", "potionmod", 0, false);
            entity.Stats.Set("miningSpeedMul", "potionmod", 0, false);
            entity.Stats.Set("animalSeekingRange", "potionmod", 0, false);
            entity.Stats.Set("animalHarvestingTime", "potionmod", 0, false);

            if (potionReseted)
            {
                player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of all of your potions dissipate.", EnumChatType.Notification);
            }

            base.OnEntityDeath(damageSourceForDeath);
        }
    }
}