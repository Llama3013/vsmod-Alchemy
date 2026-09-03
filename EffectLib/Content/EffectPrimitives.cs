using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace EffectLib
{
    /// <summary>What a primitive's magnitude argument means.</summary>
    public enum EffectValueKind
    {
        /// <summary>On or off. The magnitude is ignored.</summary>
        Flag,

        /// <summary>A multiplier or offset, usually a fraction such as 0.2 for +20%.</summary>
        Number,

        /// <summary>A whole number, rounded from the magnitude.</summary>
        Whole,
    }

    /// <summary>One primitive of <see cref="EffectContext"/>, addressable on its own.</summary>
    /// <param name="Name">Command-facing name, always lower case.</param>
    /// <param name="Kind">How the magnitude argument is read.</param>
    /// <param name="Instant">
    /// True for one-shot effects that fire once and hold no duration (a teleport, a health
    /// change). False for effects that stay applied until they expire.
    /// </param>
    /// <param name="Description">One line shown by the list command.</param>
    /// <param name="Capability">
    /// <see cref="EffectCapability"/> this primitive falls under, or null if it is never gated.
    /// A server owner can switch the capability off, in which case applying it does nothing.
    /// </param>
    /// <param name="Apply">
    /// Sets the single field this primitive owns. <see cref="EffectContext.PotencyMul"/> is
    /// already set to the requested magnitude.
    /// </param>
    public sealed record EffectPrimitive(
        string Name,
        EffectValueKind Kind,
        bool Instant,
        string Description,
        string Capability,
        Action<EffectContext> Apply
    );

    /// <summary>
    /// The individual effects EffectLib can apply on their own - one field of
    /// <see cref="EffectContext"/> at a time - as opposed to a whole registered effect that
    /// bundles several. Each is registered under an <c>efflib:&lt;name&gt;</c> id so it can be
    /// granted, tracked, persisted and resumed like any other. The admin commands drive these.
    /// </summary>
    public static class EffectPrimitives
    {
        /// <summary>Prefix for every primitive effect id.</summary>
        public const string IdPrefix = "efflib:";

        /// <summary>Prefix for an arbitrary entity stat, e.g. <c>efflib:stat:walkspeed</c>.</summary>
        public const string StatIdPrefix = IdPrefix + "stat:";

        /// <summary>
        /// Duration a primitive is built with when nothing overrides it. The commands always set
        /// their own, and a resumed primitive takes its length from the save record, so this
        /// only applies if code calls <see cref="EffectRegistry.Build"/> for a primitive and
        /// applies it without a duration of its own.
        /// </summary>
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

            // One-shot: carried out once on application and then done with.
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

        /// <summary>Every built-in primitive, excluding the open-ended <c>stat:</c> family.</summary>
        public static IReadOnlyList<EffectPrimitive> All => all;

        public static EffectPrimitive Get(string name) =>
            name != null && byName.TryGetValue(name, out EffectPrimitive e) ? e : null;

        /// <summary>The effect id for a primitive name, or for a stat via <c>stat:name</c>.</summary>
        public static string IdFor(string name) => IdPrefix + name.ToLowerInvariant();

        /// <summary>True for ids this class owns - every <c>efflib:</c> id.</summary>
        public static bool IsPrimitiveId(string effectId) =>
            effectId != null && effectId.StartsWith(IdPrefix, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Turns a freshly built primitive context into a repeating one: a health primitive
        /// becomes a damage/heal-over-time driven by the engine's ticking, anything else re-runs
        /// its one-shot every <paramref name="intervalSec"/>. The commands call this; a resumed
        /// primitive reads the resulting <see cref="EffectContext.TickSec"/>/<see cref="EffectContext.RepeatSec"/>
        /// straight back from its save record.
        /// </summary>
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

            // Stat names are open-ended, so they cannot be enumerated up front. Resolving them
            // on demand also means a saved stat effect still rebuilds after a server restart.
            EffectRegistry.AddResolver(statResolver);
        }

        // Held as one instance so re-running RegisterAll (once per side, and again on a world
        // reload) does not stack up duplicate resolvers.
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
                    // Unit magnitude; AddStat scales it by PotencyMul.
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
