using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Alchemy.Block
{
    public class BlockHerbRacks : Vintagestory.API.Common.Block
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
            if (
                world.BlockAccessor.GetBlockEntity(blockSel.Position)
                is BlockEntity.BlockEntityHerbRacks beherbrack
            )
                return beherbrack.OnInteract(byPlayer, blockSel);

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}
