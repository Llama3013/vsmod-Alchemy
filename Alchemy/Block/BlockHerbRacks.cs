using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    public class BlockHerbRacks : Block
    {
        public override bool DoPartialSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override bool OnBlockInteractStart(
            IWorldAccessor world,
            IPlayer byPlayer,
            BlockSelection blockSel
        )
        {
            if (
                world.BlockAccessor.GetBlockEntity(blockSel.Position)
                is BlockEntityHerbRacks beherbrack
            )
                return beherbrack.OnInteract(byPlayer, blockSel);

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}
