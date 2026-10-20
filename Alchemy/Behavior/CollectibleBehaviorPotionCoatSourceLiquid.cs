using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// Flask/liquid-container form of <see cref="PotionCoatSourceBehavior"/> - coats from the
    /// container's contents, consuming litres instead of items.
    /// </summary>
    public class PotionCoatSourceLiquidBehavior(CollectibleObject collObj)
        : PotionCoatSourceBehavior(collObj)
    {
        private float consumeLitres;
        private float checkLitres;

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            consumeLitres = properties["consumeLitres"].AsFloat(AlchemyConfig.Loaded.WeaponCoatConsumeLitres);
            checkLitres = properties["checkLitres"].AsFloat(AlchemyConfig.Loaded.WeaponCoatCheckLitres);
        }

        protected override ItemStack GetSourceStack(ItemSlot slot) =>
            (collObj as BlockLiquidContainerBase)?.GetContent(slot.Itemstack);

        protected override bool HasEnoughSource(ItemSlot slot) =>
            collObj is BlockLiquidContainerBase container
            && container.GetCurrentLitres(slot.Itemstack) >= checkLitres;

        protected override bool ConsumeSource(ItemSlot slot, EntityAgent byEntity)
        {
            if (collObj is not BlockLiquidContainerBase container)
                return false;

            EntityPlayer player = byEntity as EntityPlayer;
            int consumed = container.SplitStackAndPerformAction(
                player,
                slot,
                stack => container.TryTakeLiquid(stack, consumeLitres)?.StackSize ?? 0
            );
            slot.MarkDirty();
            player?.Player?.InventoryManager?.BroadcastHotbarSlot();
            return consumed > 0;
        }
    }
}
