using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Alchemy.Block
{
    public class BlockPotionBrewingCauldron : BlockCookingContainer, IInFirepitRendererSupplier
    {
        IInFirepitRenderer IInFirepitRendererSupplier.GetRendererWhenInFirepit(
            ItemStack stack,
            BlockEntityFirepit firepit,
            bool forOutputSlot
        )
        {
            if (api is ICoreClientAPI capi)
                return new CauldronInFirepitRenderer(capi, stack, firepit.Pos, firepit);
            return null;
        }

        public override void DoSmelt(
            IWorldAccessor world,
            ISlotProvider cookingSlotsProvider,
            ItemSlot inputSlot,
            ItemSlot outputSlot
        )
        {
            ItemStack[] stacks = GetCookingStacks(cookingSlotsProvider);
            CookingRecipe recipe = GetMatchingCookingRecipe(
                world,
                stacks,
                out int quantityServings
            );

            if (recipe?.CooksInto == null)
                return;
            if (quantityServings < 1 || quantityServings > MaxServingSize)
                return;

            ItemStack outstack = recipe.CooksInto.ResolvedItemstack?.Clone();
            if (outstack == null)
                return;

            outstack.StackSize *= quantityServings;

            for (int i = 0; i < cookingSlotsProvider.Slots.Length; i++)
            {
                cookingSlotsProvider.Slots[i].Itemstack = i == 0 ? outstack : null;
            }
        }

        // I need these two functions overridden otherwise breaking a cauldron will crash the game
        public override int GetRandomColor(
            ICoreClientAPI capi,
            BlockPos pos,
            BlockFacing facing,
            int rndIndex = -1
        )
        {
            string texKey = Attributes?["randomColorTexture"].AsString("metal") ?? "metal";
            return capi.BlockTextureAtlas.GetRandomColor(
                Textures[texKey].Baked.TextureSubId,
                rndIndex
            );
        }

        public override int GetRandomColor(ICoreClientAPI capi, ItemStack stack)
        {
            string texKey = Attributes?["randomColorTexture"].AsString("metal") ?? "metal";
            return capi.BlockTextureAtlas.GetRandomColor(Textures[texKey].Baked.TextureSubId);
        }
    }
}
