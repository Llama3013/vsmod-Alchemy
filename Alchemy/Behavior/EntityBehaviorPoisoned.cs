using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace Alchemy.Behavior
{
    public class EntityBehaviorPoisoned(Entity entity) : EntityBehavior(entity)
    {
        private float dmgPerTick;
        private int tickSec;
        private bool ignoreArmour;
        private bool done;
        private float tickAccum;
        private float durationAccum;
        private float durationSec;

        public override string PropertyName() => "alchemyPoisoned";

        public void Setup(float dmgPerTick, int tickSec, int durationSec, bool ignoreArmour)
        {
            this.dmgPerTick = dmgPerTick;
            this.tickSec = tickSec;
            this.durationSec = durationSec;
            this.ignoreArmour = ignoreArmour;
            tickAccum = 0f;
            durationAccum = 0f;
            done = false;
        }

        public void Refresh(float dmgPerTick, int tickSec, int durationSec, bool ignoreArmour)
        {
            this.dmgPerTick = dmgPerTick;
            this.tickSec = tickSec;
            this.durationSec = durationSec;
            this.ignoreArmour = ignoreArmour;
            durationAccum = 0f;
            done = false;
        }

        public override void OnGameTick(float dt)
        {
            if (done || entity.World.Side != EnumAppSide.Server)
                return;

            durationAccum += dt;
            if (durationAccum >= durationSec)
            {
                done = true;
                return;
            }

            tickAccum += dt;
            if (tickAccum >= tickSec)
            {
                tickAccum -= tickSec;
                ApplyHealthChange();
            }
        }

        private TagSetFast mechanicalEntityTag;
        private bool mechanicalEntityTagCached;

        private TagSetFast GetMechanicalEntityTag()
        {
            if (!mechanicalEntityTagCached)
            {
                mechanicalEntityTagCached = true;
                entity.Api.EntityTagRegistry.TryCreateTagSet(
                    out mechanicalEntityTag,
                    new List<string> { "mechanical" }
                );
            }
            return mechanicalEntityTag;
        }

        private void ApplyHealthChange()
        {
            if (entity.Tags.Overlaps(GetMechanicalEntityTag()))
            {
                done = true;
                return;
            }
            if (!entity.Alive)
            {
                done = true;
                return;
            }

            float healthAmount = Math.Abs(dmgPerTick);
            if (healthAmount > float.Epsilon)
            {
                DamageSource src = new()
                {
                    Source = EnumDamageSource.Internal,
                    Type = dmgPerTick > float.Epsilon ? EnumDamageType.Heal : EnumDamageType.Poison,
                };

                if (ignoreArmour && entity is EntityPlayer ep)
                {
                    ITreeAttribute statsTree = ep
                        .WatchedAttributes.GetTreeAttribute("stats")
                        ?.GetTreeAttribute("healingeffectivness");

                    float wearableHealEffect = statsTree?.GetFloat("wearablemod") ?? 0f;

                    if (Math.Abs(wearableHealEffect) > float.Epsilon)
                        ep.Stats.Set("healingeffectivness", "wearablemod", 0f, false);

                    entity.ReceiveDamage(src, healthAmount);

                    if (Math.Abs(wearableHealEffect) > float.Epsilon)
                        ep.Stats.Set(
                            "healingeffectivness",
                            "wearablemod",
                            wearableHealEffect,
                            false
                        );
                }
                else
                {
                    entity.ReceiveDamage(src, healthAmount);
                }
            }
        }
    }
}
