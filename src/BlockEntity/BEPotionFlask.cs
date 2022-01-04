using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Alchemy
{
    public class BlockEntityBottle : BlockEntityContainer
    {
        public override InventoryBase Inventory => inv;
        InventoryGeneric inv;
        public override string InventoryClassName => "potionflask";

        public BlockEntityBottle()
        {
            inv = new InventoryGeneric(1, null, null);
        }

        BlockPotionFlask ownBlock;
        MeshData currentMesh;

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

        public ItemStack GetContent()
        {
            return inv[0].Itemstack;
        }


        internal void SetContent(ItemStack stack)
        {
            inv[0].Itemstack = stack;
            MarkDirty(true);
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

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            if (Api?.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }

        

        internal MeshData GenMesh()
        {
            MeshData mesh = ownBlock.GenMesh(Api as ICoreClientAPI, GetContent(), Pos);

            return mesh;
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (currentMesh == null || ownBlock.Code.Path.Contains("clay")) return false;
            mesher.AddMeshData(currentMesh.Clone().Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, 0, 0));
            return true;
        }

    }
}