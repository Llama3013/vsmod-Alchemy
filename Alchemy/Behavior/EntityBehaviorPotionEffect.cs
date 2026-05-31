using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    public class EntityBehaviorPotionEffect : EntityBehavior
    {
        public EntityBehaviorPotionEffect(Entity entity)
            : base(entity)
        {
            if (entity is EntityPlayer ep && entity.World.Side == EnumAppSide.Server)
            {
                Manager = new PotionEffectManager(ep);
            }
        }

        public PotionEffectManager Manager { get; private set; }

        public override string PropertyName() => "potionEffects";
    }
}
