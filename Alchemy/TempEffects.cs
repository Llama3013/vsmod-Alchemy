using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace Alchemy
{
    public sealed class TempEffect
    {
        private const string modCode = "potionmod";

        public readonly string EffectId;
        public readonly PotionContext Context;

        public TempEffect(string effectId, PotionContext ctx)
        {
            EffectId = effectId;
            Context = ctx;
        }

        public void Apply(EntityPlayer entity)
        {
            foreach (KeyValuePair<string, float> stat in Context.EffectList)
            {
                if (stat.Key == "maxhealthExtraPoints")
                {
                    entity.Stats.Set(
                        stat.Key,
                        modCode,
                        (14f + entity.Stats.GetBlended("maxhealthExtraPoints")) * stat.Value,
                        false
                    );
                    EntityBehaviorHealth ebh = entity.GetBehavior<EntityBehaviorHealth>();
                    ebh.MarkDirty();
                }
                else
                    entity.Stats.Set(stat.Key, modCode, stat.Value, false);
            }
            // This will apply health at the start of a potion for ensure no tick health potions still function and will provide instant health from potion
            ApplyHealth(entity);
        }

        public void Remove(EntityPlayer entity)
        {
            foreach (string stat in Context.EffectList.Keys)
            {
                entity.Stats.Remove(stat, modCode);
                if (stat == "maxhealthExtraPoints")
                    entity.GetBehavior<EntityBehaviorHealth>().MarkDirty();
            }
        }

        public void Tick(EntityPlayer entity)
        {
            if (Context.TickSec <= 0)
                return;
            ApplyHealth(entity);
        }

        private void ApplyHealth(EntityPlayer entity)
        {
            if (Math.Abs(Context.Health) <= float.Epsilon)
                return;

            float wearableHealEffect = 0f;

            if (Context.IgnoreArmour)
            {
                ITreeAttribute statsTree = entity.WatchedAttributes
                    .GetTreeAttribute("stats")
                    ?.GetTreeAttribute("healingeffectivness");

                if (statsTree != null)
                    wearableHealEffect = statsTree.GetFloat("wearablemod");

                if (Math.Abs(wearableHealEffect) > float.Epsilon)
                    entity.Stats.Set("healingeffectivness", "wearablemod", 0f, false);
            }

            entity.ReceiveDamage(
                new DamageSource
                {
                    Source = EnumDamageSource.Internal,
                    Type = Context.Health > 0 ? EnumDamageType.Heal : EnumDamageType.Poison
                },
                Math.Abs(Context.Health)
            );

            if (Math.Abs(wearableHealEffect) > float.Epsilon)
                entity.Stats.Set("healingeffectivness", "wearablemod", wearableHealEffect, false);
        }
    }
}
