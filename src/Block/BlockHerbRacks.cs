using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Alchemy
{
    public class BlockHerbRacks : Block
    {
        public override bool
        DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }

        public override bool
        OnBlockInteractStart(
            IWorldAccessor world,
            IPlayer byPlayer,
            BlockSelection blockSel
        )
        {
            BlockEntityHerbRacks beherbrack =
                world.BlockAccessor.GetBlockEntity(blockSel.Position) as
                BlockEntityHerbRacks;
            if (beherbrack != null)
                return beherbrack.OnInteract(byPlayer, blockSel);

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}
