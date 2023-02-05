using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using System.Text;
using Vintagestory.API.Config;

namespace Alchemy
{
    public class BlockEntityPotionFlask : BlockEntityLiquidContainer
    {
        public override string InventoryClassName => "potionflask";

        BlockPotionFlask ownBlock;
        MeshData currentMesh;

        public BlockEntityPotionFlask()
        {
            inventory = new InventoryGeneric(1, null, null);
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            ownBlock = Block as BlockPotionFlask;
            if (Api.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }

        public override void OnBlockBroken(IPlayer byPlayer = null)
        {
            // Don't drop inventory contents
        }

        public override void OnBlockPlaced(ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);

            if (Api.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }

        internal MeshData GenMesh()
        {
            if (ownBlock == null || ownBlock.Code.Path.Contains("clay"))
                return null;

            MeshData mesh = ownBlock.GenMesh(Api as ICoreClientAPI, GetContent(), Pos);

            return mesh;
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (currentMesh == null || ownBlock.Code.Path.Contains("clay"))
                return false;
            mesher.AddMeshData(currentMesh.Clone().Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, 0, 0));
            return true;
        }

        public override void FromTreeAttributes(
            ITreeAttribute tree,
            IWorldAccessor worldForResolving
        )
        {
            base.FromTreeAttributes(tree, worldForResolving);

            if (Api?.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            ItemSlot slot = inventory[0];

            if (slot.Empty)
            {
                dsc.AppendLine(Lang.Get("Empty"));
            }
            else
            {
                dsc.AppendLine(
                    Lang.Get(
                        "Contents: {0}x{1}",
                        slot.Itemstack.StackSize,
                        slot.Itemstack.GetName()
                    )
                );
            }
        }
    }
}
