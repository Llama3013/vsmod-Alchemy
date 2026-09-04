using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace EffectLib
{
    public enum EffectValueKind
    {
        Flag,

        Number,

        Whole,
    }

    public sealed record EffectPrimitive(
        string Name,
        EffectValueKind Kind,
        bool Instant,
        string Description,
        string Capability,
        Action<EffectContext> Apply
    );

    public static class EffectPrimitives
    {
        public const string IdPrefix = "efflib:";

        public const string StatIdPrefix = IdPrefix + "stat:";

        public const int DefaultDurationSec = 600;

        private static readonly EffectPrimitive[] all =
        [
            new("waterbreathe", EffectValueKind.Flag, false, "Breathe underwater", null,
                ctx => ctx.WaterBreathe = true),
            new("coldresist", EffectValueKind.Flag, false, "Immune to cold", null,
                ctx => ctx.ColdResist = true),
            new("fly", EffectValueKind.Flag, false, "Free movement (flight)", EffectCapability.Fly,
                ctx => ctx.CanFly = true),
            new("nogravity", EffectValueKind.Flag, false, "Unaffected by gravity", EffectCapability.Fly,
                ctx => ctx.NoGravity = true),
            new("climb", EffectValueKind.Flag, false, "Climb any surface", EffectCapability.Climb,
                ctx => ctx.CanClimbAnywhere = true),
            new("noclimb", EffectValueKind.Flag, false, "Cannot climb at all", EffectCapability.Climb,
                ctx => ctx.DisableClimbing = true),
            new("nofalldamage", EffectValueKind.Flag, false, "Takes no fall damage", EffectCapability.Fall,
                ctx => ctx.NoFallDamage = true),
            new("glow", EffectValueKind.Whole, false, "Emit light, 0-255", null,
                ctx => ctx.GlowStrength = (int)Math.Round(ctx.PotencyMul)),
            new("falldamagereduction", EffectValueKind.Number, false,
                "Cut fall damage by a fraction, 0-1", EffectCapability.Fall,
                ctx => ctx.FallDamageReduction = ctx.PotencyMul),
            new("knockbackresistance", EffectValueKind.Number, false,
                "Resist knockback, offset from normal", null,
                ctx => ctx.KnockbackResistance = ctx.PotencyMul),
            new("climbreach", EffectValueKind.Number, false,
                "Climbing reach, offset in blocks", EffectCapability.Climb,
                ctx => ctx.ClimbTouchDistance = ctx.PotencyMul),
            new("weight", EffectValueKind.Number, false, "Body weight, offset in kg", null,
                ctx => ctx.Weight = ctx.PotencyMul),

            new("health", EffectValueKind.Number, true,
                "Change health once, negative to damage", null,
                ctx => ctx.SetHealth(1f)),
            new("nutrition", EffectValueKind.Number, true,
                "Keep a fraction of current nutrition", null,
                ctx => ctx.RetainedNutrition = ctx.PotencyMul),
            new("temporalstability", EffectValueKind.Number, true,
                "Add to temporal stability", null,
                ctx => ctx.TemporalStabilityGain = ctx.PotencyMul),
            new("size", EffectValueKind.Number, true, "Grow or shrink the player", null,
                ctx => ctx.SizeChange = ctx.PotencyMul),
            new("respawn", EffectValueKind.Flag, true, "Teleport to spawn", null,
                ctx => ctx.Respawn = true),
            new("reshape", EffectValueKind.Flag, true, "Reopen character customisation", null,
                ctx => ctx.Reshape = true),
        ];

        private static readonly Dictionary<string, EffectPrimitive> byName = all.ToDictionary(
            e => e.Name,
            StringComparer.OrdinalIgnoreCase
        );

        public static IReadOnlyList<EffectPrimitive> All => all;

        public static EffectPrimitive Get(string name) =>
            name != null && byName.TryGetValue(name, out EffectPrimitive e) ? e : null;

        public static string IdFor(string name) => IdPrefix + name.ToLowerInvariant();

        public static bool IsPrimitiveId(string effectId) =>
            effectId != null && effectId.StartsWith(IdPrefix, StringComparison.OrdinalIgnoreCase);

        public static void MakeRepeating(
            EffectContext ctx,
            float intervalSec,
            EnumDamageType? damageType = null
        )
        {
            if (ctx == null || intervalSec <= 0f)
                return;

            if (Math.Abs(ctx.Health) > float.Epsilon)
            {
                ctx.TickSec = intervalSec;
                if (damageType.HasValue)
                    ctx.DamageType = damageType;
            }
            else
            {
                ctx.RepeatSec = intervalSec;
            }
        }

        internal static void RegisterAll()
        {
            foreach (EffectPrimitive effect in all)
            {
                EffectPrimitive captured = effect;
                EffectRegistry.Register(IdFor(captured.Name), ctx => Build(ctx, captured));
            }

            EffectRegistry.AddResolver(statResolver);
        }

        private static readonly System.Func<string, EffectRegistration> statResolver = effectId =>
        {
            if (!effectId.StartsWith(StatIdPrefix, StringComparison.OrdinalIgnoreCase))
                return null;

            string statName = effectId[StatIdPrefix.Length..];
            if (string.IsNullOrWhiteSpace(statName))
                return null;

            return new EffectRegistration(
                effectId,
                EffectRegistry.DefaultDomain,
                ctx =>
                {
                    ctx.Duration = DefaultDurationSec;
                    ctx.AddStat(statName, 1f);
                }
            );
        };

        private static void Build(EffectContext ctx, EffectPrimitive effect)
        {
            ctx.Duration = effect.Instant ? 0 : DefaultDurationSec;
            effect.Apply(ctx);
        }
    }
}
