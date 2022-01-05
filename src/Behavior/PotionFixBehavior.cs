using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

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

            TempEffect tempEffect = new TempEffect();
            tempEffect.resetAllTempStats((player.Entity as EntityPlayer), "potionmod");
            tempEffect.resetAllListeners((player.Entity as EntityPlayer), "potionid", "tickpotionid");

            base.OnEntityDeath(damageSourceForDeath);
        }
    }
}