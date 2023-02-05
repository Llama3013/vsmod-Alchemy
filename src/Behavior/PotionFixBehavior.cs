using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace Alchemy
{
    public class PotionFixBehavior : EntityBehavior
    {

        public PotionFixBehavior(Entity entity) : base(entity)
        {
            
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
        
        public override string PropertyName()
        {
            return "PotionFixBehavior";
        }
    }
}