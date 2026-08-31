using EffectLib;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// Alchemy's item-form potions (herb balls, carried portions) on top of EffectLib's generic
    /// <see cref="CollectibleBehaviorEffectItem"/>. Every hook here only resolves a potion id or
    /// injects an <see cref="AlchemyConfig"/> value into a calculation or gate that
    /// <see cref="PotionConsumableLogic"/> owns - the hold-interact flow, progress bar and
    /// effect application itself are inherited unchanged. See
    /// <see cref="PotionConsumableLiquidBehavior"/> for the flask/liquid-container form.
    /// </summary>
    public class PotionConsumableBehavior(CollectibleObject collObj)
        : CollectibleBehaviorEffectItem(collObj)
    {
        // RegisterOwnEffect is inherited unchanged: the base reads this item's own "effectinfo"
        // attribute exactly like a JSON-only mod's would. For the ~25 built-in potions that
        // attribute exists purely so TryReadPotionId can look up the id (PotionEffects.RegisterAll
        // already registered the real builder in code), so the base's registration attempt is a
        // harmless no-op there - EffectRegistry.Reserve (see AlchemyMod) refuses it silently.
        // For a JSON-only potion (e.g. throwableacid.json) it's what actually registers it.

        protected override bool TryResolveEffect(
            ItemSlot slot,
            EntityAgent byEntity,
            out string effectId,
            out float potencyMul
        ) =>
            PotionConsumableLogic.TryResolvePotion(slot.Itemstack, out effectId, out potencyMul)
            && PotionConsumableLogic.IsDrinkingAllowed(effectId);

        protected override float GetConsumeTime(EntityAgent byEntity) =>
            PotionConsumableLogic.ScaleConsumeTime(AlchemyConfig.Loaded.PotionEatTime, byEntity);

        protected override string GetBlockReason(
            ItemSlot slot,
            EntityAgent byEntity,
            string effectId,
            EffectContext ctx
        ) =>
            !HasEnoughSource(slot)
                ? "alchemy:not-enough-potion"
                : PotionConsumableLogic.GetPotionBlockReason(byEntity, effectId, ctx);

        protected override bool ApplyEffect(
            ItemSlot slot,
            EntityAgent byEntity,
            string effectId,
            EffectContext ctx
        ) =>
            PotionConsumableLogic.ApplyPotionEffect(
                byEntity,
                effectId,
                ctx,
                slot.Itemstack?.GetName() ?? effectId
            );

        public override void GetHeldItemInfo(
            ItemSlot slot,
            StringBuilder dsc,
            IWorldAccessor world,
            bool withDebugInfo
        )
        {
            if (TryResolveEffect(slot, null, out string potionId, out float potencyMul))
                PotionConsumableLogic.AppendPotionTooltip(dsc, potionId, potencyMul);
        }
    }
}
