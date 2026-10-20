using Vintagestory.API.Common;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// Held in the off hand, coats the main hand's weapon or arrows with this item's potion while
    /// the player holds sneak + right mouse. Driven by <see cref="CoatSourceIdlePatch"/>. See
    /// <see cref="PotionCoatSourceLiquidBehavior"/> for the flask/liquid-container form.
    /// </summary>
    public class PotionCoatSourceBehavior(CollectibleObject collObj) : CollectibleBehavior(collObj)
    {
        protected virtual ItemStack GetSourceStack(ItemSlot slot) => slot.Itemstack;

        protected virtual bool HasEnoughSource(ItemSlot slot) => (slot.Itemstack?.StackSize ?? 0) > 0;

        protected virtual bool ConsumeSource(ItemSlot slot, EntityAgent byEntity)
        {
            slot.TakeOut(1);
            slot.MarkDirty();
            return true;
        }

        public void CoatingIdle(ItemSlot slot, EntityAgent byEntity)
        {
            ItemStack sourceStack = GetSourceStack(slot);
            PotionConsumableLogic.TryResolvePotion(
                sourceStack,
                out string potionId,
                out float potencyMul,
                out float durationMul
            );

            CoatingInteraction.HandleIdle(
                slot,
                byEntity,
                potionId,
                potencyMul,
                durationMul,
                CoatedEffects.DefaultItemCode(sourceStack?.Collectible),
                s => HasEnoughSource(s) && ConsumeSource(s, byEntity)
            );
        }
    }
}
