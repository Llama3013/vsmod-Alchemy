using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace EffectLib
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// Tooltip for a weapon or arrow currently carrying a coating - add
    /// <c>{ "name": "Coatable" }</c> to a collectible's <c>behaviors</c> to show it. Reads the
    /// coating off the stack, like the on-hit patches; a coating kept in a combat mod's own
    /// buff store shows its tooltip through that mod instead.
    /// </summary>
    public class CollectibleBehaviorCoatable(CollectibleObject collObj) : CollectibleBehavior(collObj)
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
            string effectId = attrs.GetString(CoatedEffects.KeyEffectId);
            if (string.IsNullOrEmpty(effectId))
                return;

            string itemCode = attrs.GetString(CoatedEffects.KeyItemCode);
            string displayName = CoatedEffects.ResolveDisplayName(itemCode, effectId);
            bool isProjectile = CoatingPolicy.IsCoatableProjectile(inSlot.Itemstack.Collectible);

            dsc.Append(string.Format("<font color=\"{0}\">", "#b8bb00"));
            dsc.Append(
                isProjectile
                    ? EffectLang.Get(effectId, "arrow-coated", displayName)
                    : EffectLang.Get(effectId, "weapon-coated", displayName, attrs.GetInt(CoatedEffects.KeyCharges))
            );
            if (!CoatingPolicy.IsEffectCoatable(effectId))
                dsc.Append(" (" + Lang.Get("effectlib:coating-disabled-suffix") + ")");
            dsc.AppendLine("</font>");
        }
    }
}
