using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace EffectLib
{
    public static class JsonEffectDefinition
    {
        public static void Apply(EffectContext ctx, JsonObject def)
        {
            if (ctx == null || def == null)
                return;

            ctx.ResetsEffects = def["resetsEffects"].AsBool();
            ctx.Duration = def["duration"].AsInt();
            if (ctx.Duration < 0)
                ctx.Duration = EffectContext.EndlessDuration;
            ctx.RepeatSec = def["repeatSec"].AsFloat();

            string[] resetDomains = def["resetDomains"].AsArray<string>(null);
            if (resetDomains != null)
                ctx.ResetDomains.AddRange(resetDomains.Where(d => !string.IsNullOrWhiteSpace(d)));

            string[] resetEffectIds = def["resetEffectIds"].AsArray<string>(null);
            if (resetEffectIds != null)
                ctx.ResetEffectIds.AddRange(
                    resetEffectIds.Where(id => !string.IsNullOrWhiteSpace(id))
                );

            Dictionary<string, float> stats = def["stats"]
                .AsObject<Dictionary<string, float>>(null);
            if (stats != null)
            {
                foreach (KeyValuePair<string, float> stat in stats)
                    ctx.AddStat(stat.Key, stat.Value);
            }

            float health = def["health"].AsFloat();
            if (Math.Abs(health) > float.Epsilon)
            {
                ctx.SetHealth(health);
                ctx.TickSec = def["tickSec"].AsFloat();

                string damageType = def["damageType"].AsString();
                if (
                    !string.IsNullOrWhiteSpace(damageType)
                    && Enum.TryParse(damageType, true, out EnumDamageType parsed)
                )
                    ctx.DamageType = parsed;
            }

            ctx.GlowStrength = def["glowStrength"].AsInt();
            ctx.TemporalStabilityGain = def["temporalStabilityGain"].AsFloat();
            ctx.RetainedNutrition = def["retainedNutrition"].AsFloat();
            ctx.SizeChange = def["sizeChange"].AsFloat();
            ctx.SizeMinHeight = def["sizeMinHeight"].AsFloat();
            ctx.SizeMaxHeight = def["sizeMaxHeight"].AsFloat();
            ctx.FallDamageReduction = def["fallDamageReduction"].AsFloat();

            ctx.WaterBreathe = def["waterBreathe"].AsBool();
            ctx.ColdResist = def["coldResist"].AsBool();
            ctx.CanClimbAnywhere = def["canClimbAnywhere"].AsBool();
            ctx.CanFly = def["canFly"].AsBool();
            ctx.Respawn = def["respawn"].AsBool();
            ctx.Reshape = def["reshape"].AsBool();

            ctx.KnockbackResistance = def["knockbackResistance"].AsFloat();
            ctx.NoFallDamage = def["noFallDamage"].AsBool();
            ctx.DisableClimbing = def["disableClimbing"].AsBool();
            ctx.ClimbTouchDistance = def["climbTouchDistance"].AsFloat();
            ctx.Weight = def["weight"].AsFloat();
            ctx.NoGravity = def["noGravity"].AsBool();
        }

        public static void RegisterFrom(
            string effectId,
            string domain,
            JsonObject def,
            AssetLocation iconSource = null
        )
        {
            JsonObject definition = def;
            string[] channels = def["channels"].AsArray<string>(null);
            string exclusivityGroup = def["exclusivityGroup"].AsString();

            string hudIcon = def["hudIcon"].AsString();
            AssetLocation iconTexture = string.IsNullOrWhiteSpace(hudIcon)
                ? null
                : AssetLocation.Create(hudIcon, string.IsNullOrWhiteSpace(domain) ? EffectRegistry.DefaultDomain : domain);

            EffectRegistry.Register(
                effectId,
                ctx => Apply(ctx, definition),
                domain,
                iconSource,
                channels,
                exclusivityGroup,
                iconTexture
            );
        }
    }
}
