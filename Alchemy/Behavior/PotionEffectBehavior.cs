using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace Alchemy.Behavior
{
    public class PotionEffectBehavior : EntityBehavior
    {
        public PotionEffectBehavior(Entity entity) : base(entity)
        {
            if (entity is EntityPlayer ep && entity.World.Side == EnumAppSide.Server)
            {
                Manager = new PotionEffectManager(ep);
            }
        }

        public PotionEffectManager Manager { get; private set; }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            Manager.RemoveAll();
        }

        public override string PropertyName() => "potionEffects";
    }
}
