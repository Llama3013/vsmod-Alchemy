using EffectLib;
using Vintagestory.API.Common;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// Alchemy's item-form coating sources (herb balls) on top of EffectLib's generic
    /// <see cref="CollectibleBehaviorCoatSource"/>. Resolves a potion id/strength and injects
    /// Alchemy's config into the consume time, the same way
    /// <see cref="PotionConsumableBehavior"/> does for drinking. See
    /// <see cref="PotionCoatSourceLiquidBehavior"/> for the flask/liquid-container form.
    /// </summary>
    public class PotionCoatSourceBehavior(CollectibleObject collObj)
        : CollectibleBehaviorCoatSource(collObj)
    {
        protected override void RegisterOwnEffect() { }

        protected override float GetConsumeTime() => AlchemyConfig.Loaded.WeaponCoatApplyTime;

        protected override bool TryResolveEffect(
            ItemStack sourceStack,
            out string effectId,
            out float potencyMul
        ) =>
            PotionConsumableLogic.TryResolvePotion(sourceStack, out effectId, out potencyMul)
            && PotionConsumableLogic.IsCoatingAllowed(effectId);
    }
}
