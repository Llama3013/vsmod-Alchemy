using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace EffectLib
{
    /// <summary>One running instance of an effect on one player.</summary>
    public sealed class AppliedEffect(string effectId, EffectContext ctx)
    {
        // Stat modifier subkey. Kept as "potionmod-" for save compatibility with Alchemy 2.x -
        // renaming it would orphan every stat modifier already stored on existing players.
        private string ModCode => "potionmod-" + EffectId;

        public readonly string EffectId = effectId;
        public readonly EffectContext Context = ctx;

        public void Apply(EntityPlayer entity, bool resume = false)
        {
            foreach (KeyValuePair<string, float> stat in Context.StatModifiers)
            {
                if (stat.Key == "maxhealthExtraPoints")
                {
                    EntityBehaviorHealth ebh = entity.GetBehavior<EntityBehaviorHealth>();
                    float baseMax = ebh.BaseMaxHealth;
                    Dictionary<string, float> MaxHealthModifiers = ebh.MaxHealthModifiers;
                    if (MaxHealthModifiers != null)
                    {
                        foreach (KeyValuePair<string, float> val in MaxHealthModifiers)
                            baseMax += val.Value;
                    }
                    baseMax += entity.Stats.GetBlended("maxhealthExtraPoints") - 1;
                    float extraHealth = baseMax * stat.Value;
                    entity.Stats.Set(stat.Key, ModCode, extraHealth, false);
                    ebh.MarkDirty();
                }
                else
                    entity.Stats.Set(stat.Key, ModCode, stat.Value, false);
            }
            // Health change is applied via vanilla health system which already continues after restart so it is skipped
            if (resume)
                return;

            bool tickingHealth = Context.TickSec > 0 && Math.Abs(Context.Health) > float.Epsilon;

            if (tickingHealth && Context.IsEndless)
            {
                // The engine's ticking damage source needs a finite duration to spread ticks
                // across, so an endless damage/heal over time is driven by EffectManager calling
                // ApplyHealthTick on a repeat listener instead. Nothing to apply here.
            }
            else if (tickingHealth)
            {
                int ticks = Math.Max(1, (int)(Context.Duration / Context.TickSec));
                entity.ReceiveDamage(
                    new()
                    {
                        Source = EnumDamageSource.Internal,
                        Type = Context.ResolveDamageType(),
                        Duration = TimeSpan.FromSeconds(Context.Duration),
                        TicksPerDuration = ticks,
                        IgnoreInvFrames = true,
                    },
                    Math.Abs(Context.Health * ticks)
                );
            }
            else
            {
                ApplyHealth(entity);
            }
        }

        /// <summary>
        /// Applies one interval's worth of the health change. Used by <see cref="EffectManager"/>
        /// to drive an endless damage- or heal-over-time, where the engine's finite ticking
        /// damage source cannot be used.
        /// </summary>
        public void ApplyHealthTick(EntityPlayer entity) => ApplyHealth(entity);

        public void Remove(EntityPlayer entity)
        {
            foreach (string stat in Context.StatModifiers.Keys)
            {
                entity.Stats.Remove(stat, ModCode);
                if (stat == "maxhealthExtraPoints")
                    entity.GetBehavior<EntityBehaviorHealth>().MarkDirty();
            }
        }

        public void Tick(EntityPlayer _) { }

        private void ApplyHealth(EntityPlayer entity)
        {
            if (Math.Abs(Context.Health) <= float.Epsilon)
                return;

            entity.ReceiveDamage(
                new DamageSource
                {
                    Source = EnumDamageSource.Internal,
                    Type = Context.ResolveDamageType(),
                    IgnoreInvFrames = true,
                },
                Math.Abs(Context.Health)
            );
        }
    }
}
