using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Vintagestory.API.Common;

namespace EffectLib
{
    /// <summary>What an atomic effect's magnitude argument means.</summary>
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
    public sealed record AtomicEffect(
        string Name,
        EffectValueKind Kind,
        bool Instant,
        string Description,
        string Capability,
        Action<EffectContext> Apply
    );

    /// <summary>
    /// The individual effects EffectLib can apply, as opposed to a whole registered effect that
    /// bundles several of them. Each is registered as an effect id of its own so it can be
    /// granted, tracked, persisted and resumed like any other.
    /// </summary>
    public static class AtomicEffects
    {
        /// <summary>Prefix for every atomic effect id.</summary>
        public const string IdPrefix = "efflib:";

        /// <summary>Prefix for an arbitrary entity stat, e.g. <c>efflib:stat:walkspeed</c>.</summary>
        public const string StatIdPrefix = IdPrefix + "stat:";

        /// <summary>
        /// Nominal duration baked into every timed atomic effect. Resuming a saved effect
        /// clamps the remaining time to the registered duration, so this has to be comfortably
        /// longer than anything an admin would hand out or the timer would be cut short on login.
        /// </summary>
        public const int NominalDurationSec = 86400;

        private static readonly AtomicEffect[] all =
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

        private static readonly Dictionary<string, AtomicEffect> byName = all.ToDictionary(
            e => e.Name,
            StringComparer.OrdinalIgnoreCase
        );

        /// <summary>Every built-in atomic effect, excluding the open-ended stat family.</summary>
        public static IReadOnlyList<AtomicEffect> All => all;

        public static AtomicEffect Get(string name) =>
            name != null && byName.TryGetValue(name, out AtomicEffect e) ? e : null;

        /// <summary>The effect id for an atomic effect name, or for a stat via <c>stat:name</c>.</summary>
        public static string IdFor(string name) => IdPrefix + name.ToLowerInvariant();

        /// <summary>
        /// Id for a repeating form of an atomic effect: it fires every <paramref name="intervalSec"/>
        /// for its duration. For <c>health</c> this is a damage- or heal-over-time, driven by the
        /// engine's own ticking; everything else re-runs its one-shot each interval.
        /// The interval lives in the id so the effect can be rebuilt after a restart.
        /// </summary>
        public static string RepeatingIdFor(string name, float intervalSec, EnumDamageType? damageType = null)
        {
            string id = $"{IdFor(name)}:{intervalSec.ToString(CultureInfo.InvariantCulture)}";
            return damageType.HasValue ? $"{id}:{damageType.Value}".ToLowerInvariant() : id;
        }

        /// <summary>True for ids this class owns.</summary>
        public static bool IsAtomicId(string effectId) =>
            effectId != null && effectId.StartsWith(IdPrefix, StringComparison.OrdinalIgnoreCase);

        internal static void RegisterAll()
        {
            foreach (AtomicEffect effect in all)
            {
                AtomicEffect captured = effect;
                EffectRegistry.Register(IdFor(captured.Name), ctx => Build(ctx, captured));
            }

            // Stat names are open-ended, so they cannot be enumerated up front. Resolving them
            // on demand also means a saved stat effect still rebuilds after a server restart.
            EffectRegistry.AddResolver(statResolver);
            EffectRegistry.AddResolver(repeatingResolver);
        }

        // Recognises "efflib:<name>:<interval>[:<damagetype>]" - the repeating form of an
        // atomic effect. Held as one instance so repeated registration does not stack resolvers.
        private static readonly System.Func<string, EffectRegistration> repeatingResolver = effectId =>
        {
            if (!IsAtomicId(effectId))
                return null;

            string[] parts = effectId[IdPrefix.Length..].Split(':');
            if (parts.Length is < 2 or > 3)
                return null;

            AtomicEffect effect = Get(parts[0]);
            if (effect == null)
                return null;

            if (
                !float.TryParse(
                    parts[1],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float intervalSec
                )
                || intervalSec <= 0f
            )
                return null;

            EnumDamageType? damageType =
                parts.Length == 3 && Enum.TryParse(parts[2], true, out EnumDamageType parsed)
                    ? parsed
                    : null;

            return new EffectRegistration(
                effectId,
                EffectRegistry.DefaultDomain,
                ctx =>
                {
                    ctx.Duration = NominalDurationSec;
                    effect.Apply(ctx);

                    if (Math.Abs(ctx.Health) > float.Epsilon)
                    {
                        // Health repeats through the engine's ticking damage source.
                        ctx.TickSec = intervalSec;
                        if (damageType.HasValue)
                            ctx.DamageType = damageType;
                    }
                    else
                    {
                        // Everything else re-runs its one-shot on our own timer.
                        ctx.RepeatSec = intervalSec;
                    }
                }
            );
        };

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
                    ctx.Duration = NominalDurationSec;
                    // Unit magnitude; AddStat scales it by PotencyMul.
                    ctx.AddStat(statName, 1f);
                }
            );
        };

        private static void Build(EffectContext ctx, AtomicEffect effect)
        {
            ctx.Duration = effect.Instant ? 0 : NominalDurationSec;
            effect.Apply(ctx);
        }
    }
}
