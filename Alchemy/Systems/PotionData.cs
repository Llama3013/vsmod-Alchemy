using Vintagestory.API.Common;

namespace Alchemy.Systems
{
    public class PotionData
    {
        public string PotionId;
        public string Strength;
        public string DisplayName;

        public ItemStack SourceStack;

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(PotionId)
            && !string.IsNullOrWhiteSpace(Strength)
            && SourceStack != null;
    }
}
