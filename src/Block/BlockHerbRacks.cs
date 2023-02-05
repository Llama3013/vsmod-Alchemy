using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Alchemy
{
    public class BlockHerbRacks : Block
    {
        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override bool OnBlockInteractStart(
            IWorldAccessor world,
            IPlayer byPlayer,
            BlockSelection blockSel
        )
        {
            BlockEntityHerbRacks beherbrack =
                world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityHerbRacks;
            if (beherbrack != null)
                return beherbrack.OnInteract(byPlayer, blockSel);

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}
