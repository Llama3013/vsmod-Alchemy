using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

#pragma warning disable IDE0130
namespace EffectLib
#pragma warning restore IDE0130
{
    public class EntityBehaviorPlayerEffects : EntityBehavior
    {
        public EntityBehaviorPlayerEffects(Entity entity)
            : base(entity)
        {
            if (entity is EntityPlayer ep && entity.World.Side == EnumAppSide.Server)
            {
                Manager = new EffectManager(ep);
            }
        }

        public EffectManager Manager { get; private set; }

        public override string PropertyName() => "effectlibPlayerEffects";

        public static EffectManager ManagerFor(EntityPlayer entity)
        {
            if (entity?.Properties == null)
                return null;

            if (!entity.HasBehavior<EntityBehaviorPlayerEffects>())
                entity.AddBehavior(new EntityBehaviorPlayerEffects(entity));

            return entity.GetBehavior<EntityBehaviorPlayerEffects>()?.Manager;
        }
    }
}
