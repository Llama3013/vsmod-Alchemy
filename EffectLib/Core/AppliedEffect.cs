using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace EffectLib
{
    public sealed class AppliedEffect(string effectId, EffectContext ctx)
    {
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
            if (resume)
                return;

            bool tickingHealth = Context.TickSec > 0 && Math.Abs(Context.Health) > float.Epsilon;

            if (tickingHealth && Context.IsEndless)
            {
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
