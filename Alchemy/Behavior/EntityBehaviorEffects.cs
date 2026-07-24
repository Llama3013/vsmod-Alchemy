using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    public class EntityBehaviorEffects : EntityBehavior
    {
        public EntityBehaviorEffects(Entity entity)
            : base(entity)
        {
            if (entity is EntityPlayer ep && entity.World.Side == EnumAppSide.Server)
            {
                Manager = new EffectManager(ep);
            }
        }

        public EffectManager Manager { get; private set; }

        public override string PropertyName() => "potionEffects";
    }
}
