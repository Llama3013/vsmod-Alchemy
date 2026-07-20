using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    public class EntityBehaviorCoatedPotionEffect(Entity entity) : EntityBehavior(entity)
    {
        private float dmgPerTick;
        private int tickSec;
        private bool done;
        private float tickAccum;
        private float durationAccum;
        private float durationSec;

        public override string PropertyName() => "alchemyPoisoned";

        public void Setup(float dmgPerTick, int tickSec, int durationSec)
        {
            this.dmgPerTick = dmgPerTick;
            this.tickSec = tickSec;
            this.durationSec = durationSec;
            tickAccum = 0f;
            durationAccum = 0f;
            done = false;
        }

        public void Refresh(float dmgPerTick, int tickSec, int durationSec)
        {
            this.dmgPerTick = dmgPerTick;
            this.tickSec = tickSec;
            this.durationSec = durationSec;
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
                entity.World.RegisterCallback(_ => entity.RemoveBehavior(this), 1);
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
                entity.ReceiveDamage(src, healthAmount);
            }
        }
    }
}
