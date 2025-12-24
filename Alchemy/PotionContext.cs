using System.Collections.Generic;

namespace Alchemy
{
    public sealed class PotionContext
    {
        public Dictionary<string, float> Effects { get; } = [];

        public float StrengthMul { get; set; }
        public int Duration { get; set; }

        // Healing specific
        public float Health { get; set; }
        public int TickSec { get; set; }
        public bool IgnoreArmour { get; set; }

        // Utility Effects
        public float RetainedNutrition { get; set; }
        public float TemporalStabilityGain { get; set; }
        public int GlowStrength { get; set; }
        // These two are not inside the config as they would just render potions useless
        public bool Respawn { get; set; }
        public bool Reshape { get; set; }

        public void AddEffect(string key, float baseValue)
        {
            Effects.Add(key, baseValue * StrengthMul);
        }

        public void SetHealth(float healthWithoutMul)
        {
            Health = healthWithoutMul * StrengthMul;
        }
    }
}
