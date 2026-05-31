using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    public class GuiDialogCauldronFirepit : GuiDialogBlockEntity
    {
        private bool haveCookingContainer;
        private string currentOutputText;
        private ElementBounds cookingSlotsSlotBounds;
        private long lastRedrawMs;
        private EnumPosFlag screenPos;

        protected override double FloatyDialogPosition => 0.6;
        protected override double FloatyDialogAlign => 0.8;
        public override double DrawOrder => 0.2;

        public GuiDialogCauldronFirepit(
            string dlgTitle,
            InventoryBase Inventory,
            BlockPos bePos,
            SyncedTreeAttribute tree,
            ICoreClientAPI capi
        )
            : base(dlgTitle, Inventory, bePos, capi)
        {
            if (IsDuplicate)
                return;
            tree.OnModified.Add(new TreeModifiedListener() { listener = OnAttributesModified });
            Attributes = tree;
        }

        private void OnInventorySlotModified(int slotid)
        {
            // Direct call can cause InvalidOperationException
            capi.Event.EnqueueMainThreadTask(SetupDialog, "setupcauldrondlg");
        }

        private void SetupDialog()
        {
            ItemSlot hoveredSlot = capi.World.Player.InventoryManager.CurrentHoveredSlot;
            if (hoveredSlot != null && hoveredSlot.Inventory?.InventoryID != Inventory?.InventoryID)
                hoveredSlot = null;

            string newOutputText = Attributes.GetString("outputText", "");
            bool newHaveCookingContainer = Attributes.GetInt("haveCookingContainer") > 0;

            GuiElementDynamicText outputTextElem;

            if (haveCookingContainer == newHaveCookingContainer && SingleComposer != null)
            {
                outputTextElem = SingleComposer.GetDynamicText("outputText");
                outputTextElem.Font.WithFontSize(14);
                outputTextElem.SetNewText(newOutputText, true);
                SingleComposer.GetCustomDraw("symbolDrawer").Redraw();

                haveCookingContainer = newHaveCookingContainer;
                currentOutputText = newOutputText;

                outputTextElem.Bounds.fixedOffsetY = 0;

                if (outputTextElem.QuantityTextLines > 2)
                {
                    outputTextElem.Bounds.fixedOffsetY =
                        -outputTextElem.Font.GetFontExtents().Height / RuntimeEnv.GUIScale * 0.65;
                    outputTextElem.Font.WithFontSize(12);
                    outputTextElem.RecomposeText();
                }
                outputTextElem.Bounds.CalcWorldBounds();
                UpdateSpoonStatus();
                return;
            }

            haveCookingContainer = newHaveCookingContainer;
            currentOutputText = newOutputText;
            int qCookingSlots = Attributes.GetInt("quantityCookingSlots");

            ElementBounds stoveBounds = ElementBounds.Fixed(0, 0, 210, 250);

            ElementBounds spoonStatusBounds = ElementBounds.Fixed(0, 30, 210, 35);

            ElementBounds outputTextBounds = ElementBounds.Fixed(0, 65, 210, 45);

            cookingSlotsSlotBounds = ElementStdBounds.SlotGrid(
                EnumDialogArea.None,
                0,
                115,
                4,
                qCookingSlots / 4
            );
            cookingSlotsSlotBounds.fixedHeight += 10;

            double top = cookingSlotsSlotBounds.fixedHeight + cookingSlotsSlotBounds.fixedY;

            ElementBounds fuelSlotBounds = ElementStdBounds.SlotGrid(
                EnumDialogArea.None,
                0,
                top + 57,
                1,
                1
            );
            ElementBounds outputSlotBounds = ElementStdBounds.SlotGrid(
                EnumDialogArea.None,
                153,
                top,
                1,
                1
            );

            // 2. Around all that is 10 pixel padding
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(
                GuiStyle.ElementToDialogPadding
            );
            bgBounds.BothSizing = ElementSizing.FitToChildren;
            bgBounds.WithChildren(stoveBounds);

            // 3. Finally Dialog
            ElementBounds dialogBounds = ElementStdBounds
                .AutosizedMainDialog.WithFixedAlignmentOffset(
                    IsRight(screenPos)
                        ? -GuiStyle.DialogToScreenPadding
                        : GuiStyle.DialogToScreenPadding,
                    0
                )
                .WithAlignment(
                    IsRight(screenPos) ? EnumDialogArea.RightMiddle : EnumDialogArea.LeftMiddle
                );

            if (!capi.Settings.Bool["immersiveMouseMode"])
            {
                dialogBounds.fixedOffsetY +=
                    (stoveBounds.fixedHeight + 65 + (haveCookingContainer ? 25 : 0))
                    * YOffsetMul(screenPos);
                dialogBounds.fixedOffsetX += (stoveBounds.fixedWidth + 10) * XOffsetMul(screenPos);
            }

            int[] cookingSlotIds = new int[qCookingSlots];
            for (int i = 0; i < qCookingSlots; i++)
                cookingSlotIds[i] = 3 + i;

            SingleComposer = capi
                .Gui.CreateCompo("blockcauldron" + BlockEntityPosition, dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
                .BeginChildElements(bgBounds)
                .AddRichtext("", CairoFont.WhiteDetailText(), spoonStatusBounds, "spoonStatus")
                .AddDynamicCustomDraw(stoveBounds, OnBgDraw, "symbolDrawer")
                .AddDynamicText("", CairoFont.WhiteDetailText(), outputTextBounds, "outputText")
                .AddIf(haveCookingContainer)
                .AddItemSlotGrid(
                    Inventory,
                    SendInvPacket,
                    4,
                    cookingSlotIds,
                    cookingSlotsSlotBounds,
                    "ingredientSlots"
                )
                .EndIf()
                .AddItemSlotGrid(Inventory, SendInvPacket, 1, [0], fuelSlotBounds, "fuelslot")
                .AddDynamicText(
                    "",
                    CairoFont.WhiteDetailText(),
                    fuelSlotBounds.RightCopy(10, 10).WithFixedSize(60, 25),
                    "fueltemp"
                )
                .AddDynamicText(
                    "",
                    CairoFont.WhiteDetailText(),
                    ElementBounds.Fixed(75, top + 17, 60, 20),
                    "oretemp"
                )
                .AddItemSlotGrid(Inventory, SendInvPacket, 1, [2], outputSlotBounds, "outputslot")
                .EndChildElements()
                .Compose();

            lastRedrawMs = capi.ElapsedMilliseconds;

            if (hoveredSlot != null)
                SingleComposer.OnMouseMove(new MouseEvent(capi.Input.MouseX, capi.Input.MouseY));

            outputTextElem = SingleComposer.GetDynamicText("outputText");
            outputTextElem.SetNewText(currentOutputText, true);
            outputTextElem.Bounds.fixedOffsetY = 0;
            if (outputTextElem.QuantityTextLines > 2)
            {
                outputTextElem.Bounds.fixedOffsetY =
                    -outputTextElem.Font.GetFontExtents().Height / RuntimeEnv.GUIScale * 0.65;
                outputTextElem.Font.WithFontSize(12);
                outputTextElem.RecomposeText();
            }
            outputTextElem.Bounds.CalcWorldBounds();
            UpdateSpoonStatus();
        }

        public void RefreshOutputText()
        {
            if (!IsOpened())
                return;
            string newText = Attributes.GetString("outputText", "");
            if (newText == currentOutputText)
                return;
            currentOutputText = newText;

            GuiElementDynamicText elem = SingleComposer?.GetDynamicText("outputText");
            if (elem == null)
                return;

            elem.Font.WithFontSize(14);
            elem.SetNewText(newText, true);
            elem.Bounds.fixedOffsetY = 0;
            if (elem.QuantityTextLines > 2)
            {
                elem.Bounds.fixedOffsetY =
                    -elem.Font.GetFontExtents().Height / RuntimeEnv.GUIScale * 0.65;
                elem.Font.WithFontSize(12);
                elem.RecomposeText();
            }
            elem.Bounds.CalcWorldBounds();
        }

        private void UpdateSpoonStatus()
        {
            string vtml = Attributes.GetString("spoonStatus", "");
            SingleComposer
                ?.GetRichtext("spoonStatus")
                ?.SetNewText(vtml, CairoFont.WhiteDetailText());
        }

        private void OnAttributesModified()
        {
            if (!IsOpened())
                return;

            float ftemp = Attributes.GetFloat("furnaceTemperature");
            float otemp = Attributes.GetFloat("oreTemperature");

            string fuelTemp = ftemp.ToString("#");
            string oreTemp = otemp.ToString("#");

            fuelTemp += fuelTemp.Length > 0 ? "°C" : "";
            oreTemp += oreTemp.Length > 0 ? "°C" : "";

            if (ftemp > 0 && ftemp <= 20)
                fuelTemp = Lang.Get("Cold");
            if (otemp > 0 && otemp <= 20)
                oreTemp = Lang.Get("Cold");

            SingleComposer.GetDynamicText("fueltemp").SetNewText(fuelTemp);
            SingleComposer.GetDynamicText("oretemp").SetNewText(oreTemp);
            UpdateSpoonStatus();

            if (capi.ElapsedMilliseconds - lastRedrawMs > 500)
            {
                SingleComposer?.GetCustomDraw("symbolDrawer")?.Redraw();
                lastRedrawMs = capi.ElapsedMilliseconds;
            }
        }

        private void OnBgDraw(Context ctx, ImageSurface surface, ElementBounds currentBounds)
        {
            double top = cookingSlotsSlotBounds.fixedHeight + cookingSlotsSlotBounds.fixedY;

            // 1. Fire
            ctx.Save();
            Matrix m = ctx.Matrix;
            m.Translate(GuiElement.scaled(5), GuiElement.scaled(top));
            m.Scale(GuiElement.scaled(0.25), GuiElement.scaled(0.25));
            ctx.Matrix = m;
            capi.Gui.Icons.DrawFlame(ctx);

            double dy =
                210
                - 210
                    * (
                        Attributes.GetFloat("fuelBurnTime", 0)
                        / Attributes.GetFloat("maxFuelBurnTime", 1)
                    );
            ctx.Rectangle(0, dy, 200, 210 - dy);
            ctx.Clip();
            LinearGradient gradient = new(0, GuiElement.scaled(250), 0, 0);
            gradient.AddColorStop(0, new Color(1, 1, 0, 1));
            gradient.AddColorStop(1, new Color(1, 0, 0, 1));
            ctx.SetSource(gradient);
            capi.Gui.Icons.DrawFlame(ctx, 0, false, false);
            gradient.Dispose();
            ctx.Restore();

            // 2. Arrow Right
            ctx.Save();
            m = ctx.Matrix;
            m.Translate(GuiElement.scaled(63), GuiElement.scaled(top + 2));
            m.Scale(GuiElement.scaled(0.6), GuiElement.scaled(0.6));
            ctx.Matrix = m;
            capi.Gui.Icons.DrawArrowRight(ctx, 2);

            double cookingRel =
                Attributes.GetFloat("oreCookingTime") / Attributes.GetFloat("maxOreCookingTime", 1);
            ctx.Rectangle(5, 0, 125 * cookingRel, 100);
            ctx.Clip();
            gradient = new LinearGradient(0, 0, 200, 0);
            gradient.AddColorStop(0, new Color(0, 0.4, 0, 1));
            gradient.AddColorStop(1, new Color(0.2, 0.6, 0.2, 1));
            ctx.SetSource(gradient);
            capi.Gui.Icons.DrawArrowRight(ctx, 0, false, false);
            gradient.Dispose();
            ctx.Restore();
        }

        private void SendInvPacket(object packet)
        {
            capi.Network.SendBlockEntityPacket(
                BlockEntityPosition.X,
                BlockEntityPosition.Y,
                BlockEntityPosition.Z,
                packet
            );
        }

        private void OnTitleBarClose() => TryClose();

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            Inventory.SlotModified += OnInventorySlotModified;
            screenPos = GetFreePos("smallblockgui");
            OccupyPos("smallblockgui", screenPos);
            SetupDialog();
        }

        public override void OnGuiClosed()
        {
            Inventory.SlotModified -= OnInventorySlotModified;
            SingleComposer.GetSlotGrid("fuelslot").OnGuiClosed(capi);
            SingleComposer.GetSlotGrid("outputslot").OnGuiClosed(capi);
            SingleComposer.GetSlotGrid("ingredientSlots")?.OnGuiClosed(capi);
            base.OnGuiClosed();
            FreePos("smallblockgui", screenPos);
        }
    }
}
