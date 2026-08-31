using EffectLib;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// Alchemy's flask/liquid-container potions on top of EffectLib's generic
    /// <see cref="CollectibleBehaviorEffectLiquid"/>. Same shape as
    /// <see cref="PotionConsumableBehavior"/> - every hook only resolves a potion id or injects
    /// an <see cref="AlchemyConfig"/> value, sharing the same calculations and gates in
    /// <see cref="PotionConsumableLogic"/>. Litres, the hold-interact flow, progress bar and
    /// effect application itself are inherited unchanged.
    /// </summary>
    public class PotionConsumableLiquidBehavior(CollectibleObject collObj)
        : CollectibleBehaviorEffectLiquid(collObj)
    {
        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            sound = properties["sound"].AsString("alchemy:sounds/player/drink");
            consumeLitres = properties["consumeLitres"].AsFloat(AlchemyConfig.Loaded.PotionConsumeLitres);
            checkLitres = properties["checkLitres"].AsFloat(AlchemyConfig.Loaded.PotionDrinkCheckLitres);
        }

        // RegisterOwnEffect needs no override here either - CollectibleBehaviorEffectLiquid's own
        // default is already a no-op (content varies per fill, resolved lazily by
        // TryResolveEffect below instead).

        protected override bool TryResolveEffect(
            ItemSlot slot,
            EntityAgent byEntity,
            out string effectId,
            out float potencyMul
        )
        {
            ItemStack content = (collObj as BlockLiquidContainerBase)?.GetContent(slot.Itemstack);
            return PotionConsumableLogic.TryResolvePotion(content, out effectId, out potencyMul)
                && PotionConsumableLogic.IsDrinkingAllowed(effectId);
        }

        protected override float GetConsumeTime(EntityAgent byEntity) =>
            PotionConsumableLogic.ScaleConsumeTime(AlchemyConfig.Loaded.PotionDrinkTime, byEntity);

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
        )
        {
            ItemStack content = (collObj as BlockLiquidContainerBase)?.GetContent(slot.Itemstack);
            return PotionConsumableLogic.ApplyPotionEffect(
                byEntity,
                effectId,
                ctx,
                content?.GetName() ?? effectId
            );
        }

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
