using Vintagestory.API.Common;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
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
