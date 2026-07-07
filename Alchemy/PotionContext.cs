using System;
using System.Collections.Generic;

namespace Alchemy
{
    public sealed class PotionContext
    {
        public Dictionary<string, float> Effects { get; } = [];

        public float StrengthMul { get; set; }
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

        // These are not inside the config as they would just render potions useless
        public bool Respawn { get; set; }
        public bool WaterBreathe { get; set; }
        public bool ColdResist { get; set; }
        public bool Reshape { get; set; }
        public float SizeChange { get; set; }
        public float FallDamageReduction { get; set; }
        public bool CanClimbAnywhere { get; set; }
        public bool CanFly { get; set; }

        public void AddEffect(string key, float baseValue)
        {
            if (Math.Abs(baseValue) <= float.Epsilon)
                return;
            Effects.Add(key, baseValue * StrengthMul);
        }

        public void SetHealth(float healthWithoutMul)
        {
            Health = healthWithoutMul * StrengthMul;
        }
    }
}
