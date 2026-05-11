using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Alchemy.Behavior
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

            var attrs = inSlot.Itemstack.Attributes;
            string potionId = attrs.GetString("coatedPotionId");
            if (string.IsNullOrEmpty(potionId))
                return;

            string potionName = attrs.GetString("coatedDisplayName");
            if (string.IsNullOrEmpty(potionName))
                potionName = Lang.Get($"alchemy:coatname-{potionId}");
            bool isArrow = inSlot.Itemstack.Collectible.Code.Path.Contains("arrow");
            dsc.Append(string.Format("<font color=\"{0}\">", "#b8bb00"));
            if (isArrow)
                dsc.Append(Lang.Get("alchemy:arrow-coated", potionName));
            else
                dsc.Append(
                    Lang.Get("alchemy:weapon-coated", potionName, attrs.GetInt("coatCharges"))
                );
            dsc.AppendLine("</font>");
        }
    }
}
