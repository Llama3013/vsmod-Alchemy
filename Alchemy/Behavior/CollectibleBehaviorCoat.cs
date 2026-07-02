using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    public class CollectibleBehaviorCoat(CollectibleObject collObj) : CollectibleBehavior(collObj)
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
            string potionId = attrs.GetString("coatedPotionId");
            if (string.IsNullOrEmpty(potionId))
                return;

            string coatedItemCode = attrs.GetString("coatedItemCode");
            string potionName = !string.IsNullOrEmpty(coatedItemCode)
                ? Lang.Get(coatedItemCode)
                : potionId;
            bool isArrow = PotionConsumableLogic.IsCoatableProjectile(inSlot.Itemstack.Collectible);
            dsc.Append(string.Format("<font color=\"{0}\">", "#b8bb00"));
            if (isArrow)
                dsc.Append(Lang.Get("alchemy:arrow-coated", potionName));
            else
                dsc.Append(
                    Lang.Get("alchemy:weapon-coated", potionName, attrs.GetInt("coatCharges"))
                );
            if (!PotionConsumableLogic.IsCoatingAllowed(potionId))
                dsc.Append(" (" + Lang.Get("alchemy:disabled") + ")");
            dsc.AppendLine("</font>");
        }
    }
}
