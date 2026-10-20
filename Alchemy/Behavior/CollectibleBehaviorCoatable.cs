using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// Shows a weapon or arrow's current coating in its tooltip. Attached automatically to
    /// everything <see cref="CoatingPolicy"/> treats as coatable.
    /// </summary>
    public class CoatableBehavior(CollectibleObject collObj) : CollectibleBehavior(collObj)
    {
        public override void GetHeldItemInfo(
            ItemSlot inSlot,
            StringBuilder dsc,
            IWorldAccessor world,
            bool withDebugInfo
        )
        {
            if (inSlot?.Itemstack == null)
                return;

            ITreeAttribute attrs = inSlot.Itemstack.Attributes;
            string potionId = attrs.GetString(CoatedEffects.KeyEffectId);
            if (string.IsNullOrEmpty(potionId))
                return;

            string displayName = CoatedEffects.ResolveDisplayName(attrs.GetString(CoatedEffects.KeyItemCode), potionId);
            bool isProjectile = CoatingPolicy.IsCoatableProjectile(inSlot.Itemstack.Collectible);

            dsc.Append("<font color=\"#b8bb00\">");
            dsc.Append(
                isProjectile
                    ? Lang.Get("alchemy:arrow-coated", displayName)
                    : Lang.Get("alchemy:weapon-coated", displayName, attrs.GetInt(CoatedEffects.KeyCharges))
            );
            if (!PotionConsumableLogic.IsCoatingAllowed(potionId))
                dsc.Append(" (" + Lang.Get("alchemy:coating-disabled-suffix") + ")");
            dsc.AppendLine("</font>");
        }
    }
}
