using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Cairo;

namespace Alchemy
{
    public class GuiDialogCauld : GuiDialogBlockEntity
    {
        EnumPosFlag screenPos;
        ElementBounds ingredSlotBounds;

        protected override double FloatyDialogPosition => 0.6;
        protected override double FloatyDialogAlign => 0.8;

        public override double DrawOrder => 0.2;


        public GuiDialogCauld(string dialogTitle, InventoryBase inventory, BlockPos blockEntityPos, ICoreClientAPI capi) : base(dialogTitle, inventory, blockEntityPos, capi)
        {
            if (IsDuplicate) return;

        }

        void SetupDialog()
        {
            ElementBounds cauldBoundsLeft = ElementBounds.Fixed(0, 30, 200, 400);
            ElementBounds cauldBoundsRight = ElementBounds.Fixed(150, 30, 200, 400);

            ingredSlotBounds = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 30 + 45, 5, 1);
            ingredSlotBounds.fixedHeight += 10;


            ElementBounds fullnessMeterBounds = ElementBounds.Fixed(320, 30, 40, 200);
            ElementBounds liquidOutMeterBounds = ElementBounds.Fixed(320, 230, 40, 45);

            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(cauldBoundsLeft, cauldBoundsRight);

            // 3. Finally Dialog
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
                .WithFixedAlignmentOffset(IsRight(screenPos) ? -GuiStyle.DialogToScreenPadding : GuiStyle.DialogToScreenPadding, 0)
                .WithAlignment(IsRight(screenPos) ? EnumDialogArea.RightMiddle : EnumDialogArea.LeftMiddle)
            ;

            ClearComposers();
            SingleComposer = capi.Gui
                .CreateCompo("blockentitycauld" + BlockEntityPosition, dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
                .BeginChildElements(bgBounds)
                    .AddItemSlotGrid(Inventory, SendInvPacket, 5, new int[] { 0, 1, 2, 3, 4 }, ingredSlotBounds, "ingredSlots")
                    .AddItemSlotGrid(Inventory, SendInvPacket, 1, new int[] { 5 }, liquidOutMeterBounds, "liquidSlotOut")
                    .AddSmallButton("Mix", onMixClick, ElementBounds.Fixed(0, 30, 130, 25), EnumButtonStyle.Normal, EnumTextOrientation.Center)

                    .AddInset(fullnessMeterBounds.ForkBoundingParent(2, 2, 2, 2), 2)
                    .AddDynamicCustomDraw(fullnessMeterBounds, fullnessMeterDraw, "liquidBar")

                    .AddDynamicText(getContentsText(), CairoFont.WhiteDetailText(), ElementBounds.Fixed(0, 130, 200, 400), "contentText")

                .EndChildElements()
            .Compose();
        }


        string getContentsText()
        {
            string contents = "Contents:";

            if (Inventory[0].Empty && Inventory[1].Empty && Inventory[2].Empty && Inventory[3].Empty && Inventory[4].Empty && Inventory[5].Empty && Inventory[6].Empty) contents += "\nNone.";
            else
            {
                BlockEntityCauld becauld = capi.World.BlockAccessor.GetBlockEntity(BlockEntityPosition) as BlockEntityCauld;
                if (!Inventory[6].Empty)
                {
                    ItemStack stack = Inventory[6].Itemstack;
                    WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(stack);

                    if (props != null)
                    {
                        string incontainername = Lang.Get("incontainer-" + stack.Class.ToString().ToLowerInvariant() + "-" + stack.Collectible.Code.Path);
                        contents += "\n" + Lang.Get(props.MaxStackSize > 0 ? "Potion in: {0}x of {1}" : "{0} litres of {1}", (float)stack.StackSize / props.ItemsPerLitre, incontainername);
                    }
                    else
                    {
                        contents += "\n" + Lang.Get("{0}x of {1}", stack.StackSize, stack.GetName());
                    }
                }
                if (!Inventory[5].Empty)
                {
                    ItemStack stack = Inventory[5].Itemstack;
                    WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(stack);

                    if (props != null)
                    {
                        string incontainername = Lang.Get("incontainer-" + stack.Class.ToString().ToLowerInvariant() + "-" + stack.Collectible.Code.Path);
                        contents += "\n" + Lang.Get(props.MaxStackSize > 0 ? "Potion out: {0}x of {1}" : "{0} litres of {1}", (float)stack.StackSize / props.ItemsPerLitre, incontainername);
                    }
                    else
                    {
                        contents += "\n" + Lang.Get("{0}x of {1}", stack.StackSize, stack.GetName());
                    }
                }

                Dictionary<string, float> essencesDic = new Dictionary<string, float>();
                Dictionary<string, float> maxEssenceDic = new Dictionary<string, float>();
                try
                {
                    IAsset maxEssences = capi.Assets.TryGet("alchemy:config/essences.json");
                    if (maxEssences != null)
                    {
                        maxEssenceDic = maxEssences.ToObject<Dictionary<string, float>>();
                    }
                }
                catch (Exception e)
                {
                    capi.World.Logger.Error("Failed loading potion effects for potion {0}. Will ignore. Exception: {1}", e);
                }
                for (int i = 0; i < 5; i++)
                {
                    if (!Inventory[i].Empty)
                    {
                        JsonObject essences = Inventory[i].Itemstack.ItemAttributes?["potionessences"];
                        foreach (var essence in maxEssenceDic.Keys.ToList())
                        {
                            if (essences[essence].Exists)
                            {
                                if (!essencesDic.ContainsKey(essence)) essencesDic.Add(essence, 0);
                                essencesDic[essence] = (essencesDic[essence] + essences[essence].AsFloat() < maxEssenceDic[essence]) ? essencesDic[essence] += essences[essence].AsFloat() : maxEssenceDic[essence];
                                if (essencesDic[essence] == 0.0f) essencesDic.Remove(essence);
                            }
                        }
                    }
                }
                foreach (var essence in essencesDic.Keys.ToList())
                {
                    if (essence == "duration")
                    {
                        contents += "\n" + Lang.Get("increased duration by {0}", essencesDic[essence]);
                    }
                    else
                    {
                        contents += "\n" + Lang.Get("{0} units of {1} essence", essencesDic[essence], essence);
                    }
                }
                if (!Inventory[5].Empty)
                {
                    contents += "\n\nEmpty output to mix different new potions";
                }


            }

            return contents;
        }

        public void UpdateContents()
        {
            SingleComposer.GetCustomDraw("liquidBar").Redraw();
            SingleComposer.GetDynamicText("contentText").SetNewText(getContentsText());
        }

        private void fullnessMeterDraw(Context ctx, ImageSurface surface, ElementBounds currentBounds)
        {
            ItemSlot liquidSlot = Inventory[6] as ItemSlot;
            if (liquidSlot.Empty) return;

            BlockEntityCauld becauld = capi.World.BlockAccessor.GetBlockEntity(BlockEntityPosition) as BlockEntityCauld;
            float itemsPerLitre = 1f;
            int capacity = becauld.capacityLitres;

            WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(liquidSlot.Itemstack);
            if (props != null)
            {
                itemsPerLitre = props.ItemsPerLitre;
                capacity = Math.Max(capacity, props.MaxStackSize);
            }

            float fullnessRelative = liquidSlot.StackSize / itemsPerLitre / capacity;

            double offY = (1 - fullnessRelative) * currentBounds.InnerHeight;

            ctx.Rectangle(0, offY, currentBounds.InnerWidth, currentBounds.InnerHeight - offY);

            CompositeTexture tex = props?.Texture ?? liquidSlot.Itemstack.Collectible.Attributes?["inContainerTexture"].AsObject<CompositeTexture>(null, liquidSlot.Itemstack.Collectible.Code.Domain);
            if (tex != null)
            {
                ctx.Save();
                Matrix m = ctx.Matrix;
                m.Scale(GuiElement.scaled(3), GuiElement.scaled(3));
                ctx.Matrix = m;

                AssetLocation loc = tex.Base.Clone().WithPathAppendixOnce(".png");
                GuiElement.fillWithPattern(capi, ctx, loc, true, false, tex.Alpha);

                ctx.Restore();
            }
        }



        private bool onMixClick()
        {
            BlockEntityCauld becauld = capi.World.BlockAccessor.GetBlockEntity(BlockEntityPosition) as BlockEntityCauld;
            if (becauld == null) return true;

            becauld.MixCauld();
            capi.Network.SendBlockEntityPacket(BlockEntityPosition.X, BlockEntityPosition.Y, BlockEntityPosition.Z, 1337);
            Vec3d pos = BlockEntityPosition.ToVec3d().Add(0.5, 0.5, 0.5);
            capi.World.PlaySoundAt(new AssetLocation("sounds/player/seal"), pos.X, pos.Y, pos.Z, null);

            return true;
        }


        private void SendInvPacket(object packet)
        {
            capi.Network.SendBlockEntityPacket(BlockEntityPosition.X, BlockEntityPosition.Y, BlockEntityPosition.Z, packet);
        }


        private void OnTitleBarClose()
        {
            TryClose();
        }




        public override void OnGuiOpened()
        {
            base.OnGuiOpened();

            screenPos = GetFreePos("smallblockgui");
            OccupyPos("smallblockgui", screenPos);
            SetupDialog();
        }

        public override void OnGuiClosed()
        {

            base.OnGuiClosed();

            FreePos("smallblockgui", screenPos);
        }



    }
}
