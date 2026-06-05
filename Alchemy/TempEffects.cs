using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Alchemy
{
    public sealed class TempEffect(string effectId, PotionContext ctx)
    {
        private const string modCode = "potionmod";

        public readonly string EffectId = effectId;
        public readonly PotionContext Context = ctx;

        public void Apply(EntityPlayer entity)
        {
            foreach (KeyValuePair<string, float> stat in Context.Effects)
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
                    entity.Stats.Set(stat.Key, modCode, extraHealth, false);
                    ebh.MarkDirty();
                }
                else
                    entity.Stats.Set(stat.Key, modCode, stat.Value, false);
            }
            if (Context.TickSec > 0 && Math.Abs(Context.Health) > float.Epsilon)
            {
                int ticks = Context.Duration / Context.TickSec;
                entity.ReceiveDamage(
                    new DamageSource
                    {
                        Source = EnumDamageSource.Internal,
                        Type = Context.Health > 0 ? EnumDamageType.Heal : EnumDamageType.Poison,
                        Duration = TimeSpan.FromSeconds(Context.Duration),
                        TicksPerDuration = ticks,
                    },
                    Math.Abs(Context.Health * ticks)
                );
            }
            else
            {
                ApplyHealth(entity);
            }
        }

        public void Remove(EntityPlayer entity)
        {
            foreach (string stat in Context.Effects.Keys)
            {
                entity.Stats.Remove(stat, modCode);
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
                    Type = Context.Health > 0 ? EnumDamageType.Heal : EnumDamageType.Poison,
                },
                Math.Abs(Context.Health)
            );
        }
    }
}
