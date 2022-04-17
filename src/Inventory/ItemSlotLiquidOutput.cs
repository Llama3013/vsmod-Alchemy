using Vintagestory.API.Common;
using Vintagestory.GameContent;
using System;
using Vintagestory.API.Config;

namespace Alchemy
{
    public class ItemSlotLiquidOutput : ItemSlotWatertight
    {

        public ItemSlotLiquidOutput(InventoryBase inventory, float capacityLitres) : base(inventory)
        {
            this.capacityLitres = capacityLitres;
        }

        public override bool CanHold(ItemSlot itemstackFromSourceSlot)
        {
            return false;
        }

        public override bool CanTakeFrom(ItemSlot sourceSlot, EnumMergePriority priority = EnumMergePriority.AutoMerge)
        {
            return false;
        }


        public override void ActivateSlot(ItemSlot sourceSlot, ref ItemStackMoveOperation op)
        {
            if (Empty && sourceSlot.Empty) return;

            switch (op.MouseButton)
            {
                case EnumMouseButton.Right:
                    ActivateSlotRightClick(sourceSlot, ref op);
                    return;
            }
        }
    }
}
