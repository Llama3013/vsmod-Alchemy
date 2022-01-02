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
    public class BlockEntityPotionFlask : BlockEntityBucket
    {
        
        MeshData currentMesh;
        BlockBucket ownBlock;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            ownBlock = Block as BlockBucket;
            if (Api.Side == EnumAppSide.Client)
            {
                currentMesh = GenMesh();
                MarkDirty(true);
            }
        }

        internal MeshData GenMesh() {
            if (this.ownBlock == null) return null;
            
            MeshData mesh = ownBlock.GenMesh(Api as ICoreClientAPI, GetContent(), Pos);

            if (mesh.CustomInts != null)
            {
                for (int i = 0; i < mesh.CustomInts.Count; i++)
                {
                    mesh.CustomInts.Values[i] |= 1 << 27; // Disable water wavy
                }
            }

            return mesh;
        }
    }
}