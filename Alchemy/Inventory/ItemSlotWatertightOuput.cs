using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    public class ItemSlotWatertightOutput(InventoryBase inventory, float capacityLitres = 6)
        : ItemSlotOutput(inventory)
    {
        public float capacityLitres = capacityLitres;

        public override bool CanTake()
        {
            if (!Empty && itemstack.Collectible.IsLiquid())
                return false;
            return base.CanTake();
        }

        public override void ActivateSlot(ItemSlot sourceSlot, ref ItemStackMoveOperation op)
        {
            if (op.MouseButton == EnumMouseButton.Right)
                ActivateSlotRightClick(sourceSlot, ref op);
            else
                base.ActivateSlot(sourceSlot, ref op);
        }

        protected override void ActivateSlotRightClick(
            ItemSlot sourceSlot,
            ref ItemStackMoveOperation op
        )
        {
            IWorldAccessor world = inventory.Api.World;

            if (sourceSlot.Itemstack?.Block is BlockLiquidContainerBase liqCntBlock)
            {
                if (Empty)
                    return;

                ItemStack contentStack = liqCntBlock.GetContent(sourceSlot.Itemstack);

                float toMoveLitres = op.ShiftDown
                    ? liqCntBlock.CapacityLitres
                    : liqCntBlock.TransferSizeLitres;
                var srcProps = BlockLiquidContainerBase.GetContainableProps(Itemstack);
                float availableLitres = StackSize / (srcProps?.ItemsPerLitre ?? 1);

                toMoveLitres *= sourceSlot.Itemstack.StackSize;
                toMoveLitres = Math.Min(toMoveLitres, availableLitres);

                if (contentStack == null)
                {
                    int moved = liqCntBlock.TryPutLiquid(
                        sourceSlot.Itemstack,
                        Itemstack,
                        toMoveLitres / sourceSlot.Itemstack.StackSize
                    );
                    TakeOut(moved * sourceSlot.Itemstack.StackSize);
                    MarkDirty();
                }
                else
                {
                    if (
                        itemstack.Equals(
                            world,
                            contentStack,
                            GlobalConstants.IgnoredStackAttributes
                        )
                    )
                    {
                        int moved = liqCntBlock.TryPutLiquid(
                            sourceSlot.Itemstack,
                            liqCntBlock.GetContent(sourceSlot.Itemstack),
                            toMoveLitres / sourceSlot.Itemstack.StackSize
                        );
                        TakeOut(moved * sourceSlot.Itemstack.StackSize);
                        MarkDirty();
                        return;
                    }
                }

                return;
            }

            if (
                itemstack != null
                && sourceSlot.Itemstack?.ItemAttributes?["contentItem2BlockCodes"].Exists == true
            )
            {
                string outBlockCode = sourceSlot
                    .Itemstack.ItemAttributes["contentItem2BlockCodes"][
                        itemstack.Collectible.Code.ToShortString()
                    ]
                    .AsString();

                if (outBlockCode != null)
                {
                    ItemStack outBlockStack = new ItemStack(
                        world.GetBlock(
                            AssetLocation.Create(
                                outBlockCode,
                                sourceSlot.Itemstack.Collectible.Code.Domain
                            )
                        )
                    );

                    if (sourceSlot.StackSize == 1)
                    {
                        sourceSlot.Itemstack = outBlockStack;
                    }
                    else
                    {
                        sourceSlot.Itemstack.StackSize--;
                        if (!op.ActingPlayer.InventoryManager.TryGiveItemstack(outBlockStack))
                        {
                            world.SpawnItemEntity(outBlockStack, op.ActingPlayer.Entity.Pos.XYZ);
                        }
                    }

                    sourceSlot.MarkDirty();
                    TakeOut(1);
                }

                return;
            }

            if (
                sourceSlot.Itemstack?.ItemAttributes?["contentItem2BlockCodes"].Exists == true
                || sourceSlot.Itemstack?.ItemAttributes?["contentItemCode"].AsString() != null
            )
                return;

            base.ActivateSlotRightClick(sourceSlot, ref op);
        }
    }
}
