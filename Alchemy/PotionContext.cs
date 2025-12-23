using System.Collections.Generic;

namespace Alchemy
{
    public sealed class PotionContext
    {
        private readonly Dictionary<string, float> _effectList = [];
        public Dictionary<string, float> EffectList => _effectList;

        public float StrengthMul { get; set; }
        public int Duration { get; set; }

        // Healing specific
        public float Health { get; set; }
        public int TickSec { get; set; }
        public bool IgnoreArmour { get; set; }

        // Utility Effects
        public float NutritionPotionRetainedNutrition { get; set; }
        public float StabilityPotionTemporalStabilityGain { get; set; }
        // These two are not inside the config as they would just render potions useless
        public bool Respawn { get; set; }
        public bool Reshape { get; set; }

        public void AddEffect(string key, float baseValue)
        {
            _effectList.Add(key, baseValue * StrengthMul);
        }

        public void SetHealth(float healthWithoutMul)
        {
            Health = healthWithoutMul * StrengthMul;
        }
    }
}
