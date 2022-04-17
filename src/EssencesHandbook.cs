using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Alchemy
{
    public class ModSystemEssence : ModSystem
    {
        ICoreClientAPI capi;

        GuiDialogEssence dialog;


        public override bool ShouldLoad(EnumAppSide side)
        {
            return side == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            this.capi = api;

            api.Input.RegisterHotKey("essence", "Show Essence Handbook", GlKeys.Z, HotkeyType.GUIOrOtherControls);
            api.Input.SetHotKeyHandler("essence", OnHelpHotkey);

            api.Event.LevelFinalize += Event_LevelFinalize;
            api.RegisterLinkProtocol("essencehandbook", onHandBookLinkClicked);
            api.RegisterLinkProtocol("essencehandbooksearch", onHandBookSearchLinkClicked);
        }

        private void onHandBookSearchLinkClicked(LinkTextComponent comp)
        {
            string text = comp.Href.Substring("essencehandbooksearch://".Length);
            if (!dialog.IsOpened()) dialog.TryOpen();

            dialog.Search(text);
        }

        private void onHandBookLinkClicked(LinkTextComponent comp)
        {
            string target = comp.Href.Substring("essencehandbook://".Length);
            if (!dialog.IsOpened()) dialog.TryOpen();

            dialog.OpenDetailPageFor(target);
        }

        private void Event_LevelFinalize()
        {
            dialog = new GuiDialogEssence(capi);
        }

        private bool OnHelpHotkey(KeyCombination key)
        {
            if (dialog.IsOpened())
            {
                dialog.TryClose();
            }
            else
            {
                dialog.TryOpen();
                // dunno why
                dialog.ignoreNextKeyPress = true;

                if (capi.World.Player.InventoryManager.CurrentHoveredSlot?.Itemstack != null)
                {
                    ItemStack stack = capi.World.Player.InventoryManager.CurrentHoveredSlot.Itemstack;
                    string pageCode = GuiHandbookItemStackPage.PageCodeForStack(stack);

                    if (!dialog.OpenDetailPageFor(pageCode))
                    {
                        dialog.OpenDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(new ItemStack(stack.Collectible)));
                    }
                }

                if (capi.World.Player.Entity.Controls.Sneak && capi.World.Player.CurrentBlockSelection != null)
                {
                    BlockPos pos = capi.World.Player.CurrentBlockSelection.Position;
                    ItemStack stack = capi.World.BlockAccessor.GetBlock(pos).OnPickBlock(capi.World, pos);

                    string pageCode = GuiHandbookItemStackPage.PageCodeForStack(stack);

                    if (!dialog.OpenDetailPageFor(pageCode))
                    {
                        dialog.OpenDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(new ItemStack(stack.Collectible)));
                    }
                }
            }

            return true;
        }

        public override void Dispose()
        {
            base.Dispose();
            dialog?.Dispose();
        }

    }
}
