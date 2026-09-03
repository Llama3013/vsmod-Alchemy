using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace EffectLib
{
    /// <summary>
    /// The resolved description of one effect at one potency. Built fresh on every application
    /// by the <see cref="EffectBuilder"/> registered for the effect id, so a builder is free to
    /// read live config values.
    /// </summary>
    public sealed class EffectContext
    {
        /// <summary>
        /// <see cref="Duration"/> sentinel for an effect that never expires on its own: it runs
        /// until the player dies, logs out without retention, or it is removed explicitly.
        /// Distinct from <c>0</c>, which is a one-shot that fires once and is not tracked.
        /// </summary>
        public const int EndlessDuration = -1;

        public Dictionary<string, float> StatModifiers { get; } = [];

        public float PotencyMul { get; set; }

        /// <summary>
        /// Seconds the effect runs for. <c>0</c> is a one-shot (fired once, not tracked);
        /// <see cref="EndlessDuration"/> runs with no timer until death or removal; any positive
        /// value expires after that many seconds.
        /// </summary>
        public int Duration { get; set; }

        /// <summary>True when <see cref="Duration"/> is <see cref="EndlessDuration"/>.</summary>
        public bool IsEndless => Duration == EndlessDuration;

        /// <summary>Clears other running effects when this one is applied.</summary>
        public bool ResetsEffects { get; set; }

        /// <summary>
        /// Domains cleared by <see cref="ResetsEffects"/>. Empty means only the domain that
        /// registered this effect, so a purge never reaches another mod's effects by accident.
        /// Add <see cref="EffectPurge.AnyDomain"/> to clear everything.
        /// </summary>
        public List<string> ResetDomains { get; } = [];

        /// <summary>Individual effect ids cleared on top of <see cref="ResetDomains"/>.</summary>
        public List<string> ResetEffectIds { get; } = [];

        // Healing specific
        public float Health { get; set; }

        /// <summary>Seconds between health ticks. Fractional values are allowed.</summary>
        public float TickSec { get; set; }

        /// <summary>
        /// Seconds between repeats of the one-shot parts (respawn, reshape, nutrition, temporal
        /// stability, size). 0 applies them once. Health repeats via <see cref="TickSec"/>
        /// instead, which the engine drives.
        /// </summary>
        public float RepeatSec { get; set; }

        public EnumDamageType? DamageType { get; set; }

        public EnumDamageType ResolveDamageType() =>
            DamageType ?? (Health > 0 ? EnumDamageType.Heal : EnumDamageType.Poison);

        // Utility effects - world-interacting side effects carried out by EffectLib's own
        // built-in handler (see UtilityEffects/UtilityEffectHandler), as opposed to the simple
        // per-tick entity-property effects above and the stat modifiers on EffectPrimitives.
        public float RetainedNutrition { get; set; }
        public float TemporalStabilityGain { get; set; }
        public bool Respawn { get; set; }
        public bool Reshape { get; set; }
        public float SizeChange { get; set; }

        /// <summary>
        /// Height bounds for <see cref="SizeChange"/>, in blocks. 0 means "use EffectLib's
        /// default range". Captured into the player's saved state the first time a size change
        /// is applied, so later config changes do not retroactively affect a player already
        /// mid-effect.
        /// </summary>
        public float SizeMinHeight { get; set; }
        public float SizeMaxHeight { get; set; }

        // Continuous capabilities, reconciled from all active effects on every state refresh
        public int GlowStrength { get; set; }
        public bool WaterBreathe { get; set; }
        public bool ColdResist { get; set; }
        public float FallDamageReduction { get; set; }
        public bool CanClimbAnywhere { get; set; }
        public bool CanFly { get; set; }
        public float KnockbackResistance { get; set; }
        public bool NoFallDamage { get; set; }
        public bool DisableClimbing { get; set; }
        public float ClimbTouchDistance { get; set; }
        public float Weight { get; set; }
        public bool NoGravity { get; set; }

        public void AddStat(string key, float baseValue)
        {
            if (Math.Abs(baseValue) <= float.Epsilon)
                return;
            StatModifiers.Add(key, baseValue * PotencyMul);
        }

        public void SetHealth(float healthWithoutMul)
        {
            Health = healthWithoutMul * PotencyMul;
        }
    }
}
