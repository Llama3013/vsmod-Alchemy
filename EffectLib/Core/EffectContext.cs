using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace EffectLib
{
    public sealed class EffectContext
    {
        public const int EndlessDuration = -1;

        public Dictionary<string, float> StatModifiers { get; } = [];

        public float PotencyMul { get; set; }

        public int Duration { get; set; }

        public bool IsEndless => Duration == EndlessDuration;

        public bool ResetsEffects { get; set; }

        public List<string> ResetDomains { get; } = [];

        public List<string> ResetEffectIds { get; } = [];

        public float Health { get; set; }

        public float TickSec { get; set; }

        public float RepeatSec { get; set; }

        public EnumDamageType? DamageType { get; set; }

        public EnumDamageType ResolveDamageType() =>
            DamageType ?? (Health > 0 ? EnumDamageType.Heal : EnumDamageType.Poison);

        public float RetainedNutrition { get; set; }
        public float TemporalStabilityGain { get; set; }
        public bool Respawn { get; set; }
        public bool Reshape { get; set; }
        public float SizeChange { get; set; }

        public float SizeMinHeight { get; set; }
        public float SizeMaxHeight { get; set; }

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
