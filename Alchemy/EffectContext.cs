using System;
using System.Collections.Generic;

namespace Alchemy
{
    public sealed class EffectContext
    {
        public Dictionary<string, float> StatModifiers { get; } = [];

        public float PotencyMul { get; set; }
        public int Duration { get; set; }
        public bool ResetsEffects { get; set; }

        // Healing specific
        public float Health { get; set; }
        public int TickSec { get; set; }
        public bool IgnoreArmour { get; set; }

        // Utility Effects
        public float RetainedNutrition { get; set; }
        public float TemporalStabilityGain { get; set; }
        public int GlowStrength { get; set; }
        public bool Respawn { get; set; }
        public bool WaterBreathe { get; set; }
        public bool ColdResist { get; set; }
        public bool Reshape { get; set; }
        public float SizeChange { get; set; }
        public float FallDamageReduction { get; set; }
        public bool CanClimbAnywhere { get; set; }
        public bool CanFly { get; set; }

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
