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
            return entity.World.PlayerByUid((entity as EntityPlayer).PlayerUID) as IServerPlayer;
        }

        /* This override is to add the behavior to the player of when they die they also reset all of their potion effects */

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            IServerPlayer player = GetIServerPlayer();

            TempEffect.ResetAllTempStats(player.Entity);
            TempEffect.ResetAllListeners(player.Entity, "potionid", "tickpotionid");

            base.OnEntityDeath(damageSourceForDeath);
        }

        public override string PropertyName()
        {
            return "PotionFixBehavior";
        }
    }
}