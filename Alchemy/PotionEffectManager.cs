using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace Alchemy
{
    public sealed class PotionEffectManager(EntityPlayer entity)
    {
        private readonly EntityPlayer entity = entity;

        private readonly Dictionary<string, ActiveEffect> active = [];
        public bool TryApplyPotion(string id, PotionContext ctx)
        {
            if (active.ContainsKey(id))
                return false;

            TempEffect effect = new(id, ctx);
            effect.Apply(entity);

            long handle;
            bool ticking;

            if (ctx.TickSec > 0)
            {
                handle = entity.World.RegisterGameTickListener(
                    dt => Tick(id),
                    ctx.TickSec * 1000
                );
                ticking = true;
            }
            else if (ctx.Duration > 0)
            {
                handle = entity.World.RegisterCallback(
                    dt => RemoveEffect(id),
                    ctx.Duration * 1000
                );
                ticking = false;
            }
            else
            {
                // Instant or broken potion, no need for listeners
                return true;
            }

            active[id] = new ActiveEffect(effect, handle, ticking);
            entity.WatchedAttributes.SetLong(id, handle);

            return true;
        }

        private void Tick(string id)
        {
            if (!active.TryGetValue(id, out ActiveEffect activeEffect))
                return;

            activeEffect.Elapsed++;

            activeEffect.Effect.Tick(entity);

            if (activeEffect.Elapsed >= activeEffect.Effect.Context.Duration)
                RemoveEffect(id);
        }

        public void RemoveEffect(string id)
        {
            if (!active.TryGetValue(id, out ActiveEffect activeEffect))
                return;

            if (activeEffect.IsTicking)
                entity.World.UnregisterGameTickListener(activeEffect.ListenerId);
            else
                entity.World.UnregisterCallback(activeEffect.ListenerId);

            active.Remove(id);
            entity.WatchedAttributes.RemoveAttribute(id);
        }

        public void RemoveAll()
        {
            foreach (string id in active.Keys.ToList())
                RemoveEffect(id);
        }
    }

    internal sealed record ActiveEffect(
        TempEffect Effect,
        long ListenerId, 
        bool IsTicking
    )
    {
        public int Elapsed;
    }
}
