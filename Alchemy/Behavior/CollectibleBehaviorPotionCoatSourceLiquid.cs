using EffectLib;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// Alchemy's flask/liquid-container coating sources on top of EffectLib's generic
    /// <see cref="CollectibleBehaviorCoatSourceLiquid"/>. Same shape as
    /// <see cref="PotionCoatSourceBehavior"/> - resolves a potion id/strength and injects
    /// Alchemy's config into consume time and litres.
    /// </summary>
    public class PotionCoatSourceLiquidBehavior(CollectibleObject collObj)
        : CollectibleBehaviorCoatSourceLiquid(collObj)
    {
        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            consumeLitres = properties["consumeLitres"].AsFloat(AlchemyConfig.Loaded.WeaponCoatConsumeLitres);
            checkLitres = properties["checkLitres"].AsFloat(AlchemyConfig.Loaded.WeaponCoatCheckLitres);
        }

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
