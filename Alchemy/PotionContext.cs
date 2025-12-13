using System.Collections.Generic;

namespace Alchemy
{
    public sealed class PotionContext
    {
        private readonly Dictionary<string, float> _effectList = [];
        public Dictionary<string, float> EffectList => _effectList;

        public float StrengthMul { get; set; }
        public float Health { get; set; }
        public int TickSec { get; set; }
        public int Duration { get; set; }
        public bool IgnoreArmour { get; set; }

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
