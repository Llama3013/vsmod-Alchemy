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

            TempEffect tempEffect = new TempEffect();
            tempEffect.resetAllTempStats((player.Entity as EntityPlayer), "potionmod");
            tempEffect.resetAllListeners((player.Entity as EntityPlayer), "potionid", "tickpotionid");

            if (potionReseted)
            {
                player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of all of your potions dissipate.", EnumChatType.Notification);
            }

            base.OnEntityDeath(damageSourceForDeath);
        }
    }
}