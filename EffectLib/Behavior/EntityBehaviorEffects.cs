using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace EffectLib
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// Hosts the <see cref="EffectManager"/> for a player. Added programmatically once the
    /// player is ready, not through entity JSON.
    /// </summary>
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

        /// <summary>The player's effect manager, or null on the client.</summary>
        public EffectManager Manager { get; private set; }

        public override string PropertyName() => "effectlibEffects";

        /// <summary>
        /// Returns the manager for <paramref name="entity"/>, adding the behavior if it is not
        /// there yet. Null on the client, where effects are only displayed, not tracked.
        /// </summary>
        public static EffectManager ManagerFor(EntityPlayer entity)
        {
            if (entity?.Properties == null)
                return null;

            if (!entity.HasBehavior<EntityBehaviorEffects>())
                entity.AddBehavior(new EntityBehaviorEffects(entity));

            return entity.GetBehavior<EntityBehaviorEffects>()?.Manager;
        }
    }
}
