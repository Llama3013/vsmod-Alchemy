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
    public class BlockEntityCauldronFirepit : BlockEntityFirepit
    {
        public override string DialogTitle => Lang.Get("alchemy:block-potioncauldron");

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            EnsureCauldronInSlot();
            Inventory[2] = new ItemSlotWatertightOutput(Inventory, 50);
            Inventory.SlotModified += OnCauldronSlotModified;
            if (api.Side == EnumAppSide.Server)
                RegisterGameTickListener(OnSpoonCheck, 200);
        }

        private void OnSpoonCheck(float dt)
        {
            if (Inventory is not InventorySmelting inv)
                return;

            bool hasIngredients = false;
            foreach (ItemSlot slot in inv.CookingSlots)
                if (!slot.Empty)
                {
                    hasIngredients = true;
                    break;
                }

            if (!hasIngredients)
            {
                inputStackCookingTime = 0;
                return;
            }

            if (Block is not BlockCauldronFirepit cauldronBlock)
                return;

            CookingRecipe recipe = cauldronBlock.GetMatchingCookingRecipe(
                Api.World,
                cauldronBlock.GetCookingStacks(inv),
                out int quantityServings
            );

            if (recipe == null)
            {
                inputStackCookingTime = 0;
                return;
            }

            ItemStack expectedOutput = recipe.CooksInto?.ResolvedItemstack;
            if (expectedOutput != null && !outputSlot.Empty)
            {
                ItemStack outTest = expectedOutput.Clone();
                outTest.StackSize *= quantityServings;
                if (
                    !outputSlot.Itemstack.Equals(
                        Api.World,
                        outTest,
                        GlobalConstants.IgnoredStackAttributes
                    )
                    || outputSlot.Itemstack.StackSize + outTest.StackSize
                        > outputSlot.MaxSlotStackSize
                )
                {
                    inputStackCookingTime = 0;
                    return;
                }
            }

            if (
                recipe.CooksInto?.Code?.Domain == "alchemy"
                && GetSpoonBlockReason(Inventory[1].Itemstack, inv) != null
            )
            {
                inputStackCookingTime = 0;
            }
        }

        private string GetSpoonBlockReason(ItemStack cauldronStack, InventorySmelting inv)
        {
            if (cauldronStack?.Attributes.HasAttribute("stirringSpoon") != true)
                return "alchemy:cauldron-warning-no-spoon";

            ItemStack spoonStack = (
                cauldronStack.Attributes["stirringSpoon"] as ItemstackAttribute
            )?.value;
            bool isGlassSpoon =
                spoonStack?.Collectible?.Code?.Path?.StartsWith("stirringspoon-glass") == true;
            if (isGlassSpoon)
                return null;

            if (Block is BlockCauldronFirepit cauldronBlock)
            {
                CookingRecipe recipe = cauldronBlock.GetMatchingCookingRecipe(
                    Api.World,
                    cauldronBlock.GetCookingStacks(inv),
                    out _
                );
                if (recipe?.CooksInto?.Code?.Path?.Contains("strong") == true)
                    return "alchemy:cauldron-warning-glass-spoon";
            }
            return null;
        }

        private string GetSpoonStatusVtml()
        {
            if (Inventory is not InventorySmelting inv)
                return "";

            bool hasIngredients = false;
            foreach (ItemSlot slot in inv.CookingSlots)
                if (!slot.Empty)
                {
                    hasIngredients = true;
                    break;
                }

            ItemStack cauldronStack = Inventory[1].Itemstack;
            bool hasSpoon = cauldronStack?.Attributes.HasAttribute("stirringSpoon") == true;

            if (!hasSpoon)
                return $"<font color=\"#ff4444\">{Lang.Get("alchemy:cauldron-warning-no-spoon")}</font>";

            ItemStack spoonStack = (
                cauldronStack.Attributes["stirringSpoon"] as ItemstackAttribute
            )?.value?.Clone();
            spoonStack?.ResolveBlockOrItem(Api.World);
            string spoonName = spoonStack?.GetName() ?? "Stirring Spoon";
            string spoonLine = $"<font color=\"#88cc88\">{spoonName}</font>";

            string blockKey = hasIngredients ? GetSpoonBlockReason(cauldronStack, inv) : null;
            if (blockKey != null)
                return spoonLine + "\n" + $"<font color=\"#ff8844\">{Lang.Get(blockKey)}</font>";

            return spoonLine;
        }

        public override void FromTreeAttributes(
            ITreeAttribute tree,
            IWorldAccessor worldForResolving
        )
        {
            base.FromTreeAttributes(tree, worldForResolving);
            if (Api?.Side == EnumAppSide.Client && invDialog?.IsOpened() == true)
                SetDialogValues(invDialog.Attributes);
        }

        private void OnCauldronSlotModified(int _)
        {
            if (Api is ICoreClientAPI && invDialog?.IsOpened() == true)
                SetDialogValues(invDialog.Attributes);
        }

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (Api.Side == EnumAppSide.Client)
            {
                toggleInventoryDialogClient(
                    byPlayer,
                    () =>
                    {
                        SyncedTreeAttribute dtree = new();
                        SetDialogValues(dtree);
                        return new GuiDialogCauldronFirepit(
                            DialogTitle,
                            Inventory,
                            Pos,
                            dtree,
                            Api as ICoreClientAPI
                        );
                    }
                );
            }
            return true;
        }

        public void EnsureCauldronInSlot()
        {
            if (Api == null)
                return;
            if (Inventory[1].Itemstack?.Block is BlockCauldronFirepit)
                return;

            Block block = Api.World.BlockAccessor.GetBlock(Pos);
            if (block is not BlockCauldronFirepit cauldronBlock)
                return;
            if (cauldronBlock.Stage < 5)
                return;

            float savedTemp =
                Inventory[1].Itemstack != null
                    ? Inventory[1]
                        .Itemstack.Collectible.GetTemperature(Api.World, Inventory[1].Itemstack)
                    : 20f;

            Inventory[1].Itemstack = new ItemStack(block, 1);
            if (savedTemp > 20f)
                Inventory[1]
                    .Itemstack.Collectible.SetTemperature(
                        Api.World,
                        Inventory[1].Itemstack,
                        savedTemp,
                        false
                    );
            Inventory[1].MarkDirty();
        }

        void SetDialogValues(ITreeAttribute dialogTree)
        {
            dialogTree.SetFloat("furnaceTemperature", furnaceTemperature);
            dialogTree.SetInt("maxTemperature", maxTemperature);
            dialogTree.SetFloat("oreCookingTime", inputStackCookingTime);
            dialogTree.SetFloat("maxFuelBurnTime", maxFuelBurnTime);
            dialogTree.SetFloat("fuelBurnTime", fuelBurnTime);
            dialogTree.SetFloat("oreTemperature", InputStackTemp);

            InventorySmelting inv = (InventorySmelting)Inventory;
            if (inputSlot.Itemstack != null)
            {
                float meltingDuration = inputSlot.Itemstack.Collectible.GetMeltingDuration(
                    Api.World,
                    inv,
                    inputSlot
                );

                dialogTree.SetFloat("oreTemperature", InputStackTemp);
                dialogTree.SetFloat("maxOreCookingTime", meltingDuration);
            }
            else
            {
                dialogTree.RemoveAttribute("oreTemperature");
            }

            string outputText = inv.GetOutputText();
            if (Block is BlockCauldronFirepit cauldronBlock && !outputSlot.Empty)
            {
                CookingRecipe recipe = cauldronBlock.GetMatchingCookingRecipe(
                    Api.World,
                    cauldronBlock.GetCookingStacks(inv),
                    out int qty
                );
                if (recipe?.CooksInto?.ResolvedItemstack != null)
                {
                    ItemStack outTest = recipe.CooksInto.ResolvedItemstack.Clone();
                    outTest.StackSize *= qty;
                    if (
                        !outputSlot.Itemstack.Equals(
                            Api.World,
                            outTest,
                            GlobalConstants.IgnoredStackAttributes
                        )
                        || outputSlot.Itemstack.StackSize + outTest.StackSize
                            > outputSlot.MaxSlotStackSize
                    )
                    {
                        outputText = Lang.Get("alchemy:cauldron-warning-output-full");
                    }
                }
            }
            dialogTree.SetString("outputText", outputText);
            dialogTree.SetString("spoonStatus", GetSpoonStatusVtml());
            dialogTree.SetInt("haveCookingContainer", inv.HaveCookingContainer ? 1 : 0);
            dialogTree.SetInt("quantityCookingSlots", inv.CookingSlots.Length);

            (invDialog as GuiDialogCauldronFirepit)?.RefreshOutputText();
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator) =>
            false;

        public override void OnBlockRemoved()
        {
            if (Api?.Side == EnumAppSide.Server)
            {
                ItemStack cauldronStack = Inventory[1].Itemstack;
                if (cauldronStack?.Attributes.HasAttribute("stirringSpoon") == true)
                {
                    ItemStack stickStack = (
                        cauldronStack.Attributes["stirringSpoon"] as ItemstackAttribute
                    )?.value?.Clone();
                    if (stickStack != null)
                    {
                        stickStack.ResolveBlockOrItem(Api.World);
                        if (
                            stickStack.StackSize > 0
                            && (stickStack.Item != null || stickStack.Block != null)
                        )
                            Api.World.SpawnItemEntity(stickStack, Pos.ToVec3d().Add(0.5, 1.0, 0.5));
                    }
                }

                // construct1 = grass only, construct2 = grass+1 firewood, construct3 = grass+2, construct4 = grass+3
                int constructStage = Block?.Variant["burnstate"] switch
                {
                    "construct1" => 1,
                    "construct2" => 2,
                    "construct3" => 3,
                    "construct4" => 4,
                    _ => 0,
                };
                if (constructStage >= 1)
                {
                    Item grass = Api.World.GetItem(new AssetLocation("drygrass"));
                    if (grass != null)
                        Api.World.SpawnItemEntity(
                            new ItemStack(grass),
                            Pos.ToVec3d().Add(0.5, 0.5, 0.5)
                        );
                }
                int firewoodCount = constructStage - 1;
                if (firewoodCount > 0)
                {
                    Item firewood = Api.World.GetItem(new AssetLocation("firewood"));
                    if (firewood != null)
                        Api.World.SpawnItemEntity(
                            new ItemStack(firewood, firewoodCount),
                            Pos.ToVec3d().Add(0.5, 0.5, 0.5)
                        );
                }
            }

            Inventory[1].Itemstack = null;
            base.OnBlockRemoved();
        }
    }
}
