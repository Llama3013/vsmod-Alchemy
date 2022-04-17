using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Alchemy
{
    public class BlockCauld : BlockLiquidContainerBase, ILiquidSource, ILiquidSink
    {

        public override bool AllowHeldLiquidTransfer => true;

        public override int GetContainerSlotId(BlockPos pos)
        {
            BlockEntityCauld becontainer = api.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityCauld;

            if (becontainer == null) return 6;

            if (!becontainer.Inventory[5].Empty)
            {
                becontainer.isFull = true;
                return 5;
            }
            becontainer.isFull = false;
            return 6;
        }

        public override int GetContainerSlotId(ItemStack containerStack)
        {
            ItemStack[] contentStacks = GetContents(api.World, containerStack);
            int id = (contentStacks != null && contentStacks.Length > 0 && containerStack.StackSize != 0) ? Math.Min(contentStacks.Length - 1, 5) : Math.Min(contentStacks.Length - 1, 6);
            return id;
        }

        #region Render
        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            Dictionary<int, MeshRef> meshrefs;

            object obj;
            if (capi.ObjectCache.TryGetValue("cauldMeshRefs", out obj))
            {
                meshrefs = obj as Dictionary<int, MeshRef>;
            }
            else
            {
                capi.ObjectCache["cauldMeshRefs"] = meshrefs = new Dictionary<int, MeshRef>();
            }

            ItemStack[] contentStacks = GetContents(capi.World, itemstack);
            if (contentStacks == null || contentStacks.Length == 0) return;

            int hashcode = GetCauldHashCode(contentStacks[5], contentStacks[6]);

            MeshRef meshRef = null;

            if (!meshrefs.TryGetValue(hashcode, out meshRef))
            {
                MeshData meshdata = GenMesh(contentStacks[5], contentStacks[6]);
                meshrefs[hashcode] = meshRef = capi.Render.UploadMesh(meshdata);
            }

            renderinfo.ModelRef = meshRef;
        }

        public int GetCauldHashCode(ItemStack contentStack, ItemStack liquidStack)
        {
            string s = contentStack.StackSize + "x" + contentStack.Collectible.Code.ToShortString();
            return s.GetHashCode();
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            if (capi == null) return;

            object obj;
            if (capi.ObjectCache.TryGetValue("cauldMeshRefs", out obj))
            {
                Dictionary<int, MeshRef> meshrefs = obj as Dictionary<int, MeshRef>;

                foreach (var val in meshrefs)
                {
                    val.Value.Dispose();
                }

                capi.ObjectCache.Remove("cauldMeshRefs");
            }
        }

        // Override to drop the cauld empty and drop its contents instead
        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            if (world.Side == EnumAppSide.Server && (byPlayer == null || byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative))
            {
                ItemStack[] drops = new ItemStack[] { new ItemStack(this) };

                for (int i = 0; i < drops.Length; i++)
                {
                    world.SpawnItemEntity(drops[i], new Vec3d(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5), null);
                }

                world.PlaySoundAt(Sounds.GetBreakSound(byPlayer), pos.X, pos.Y, pos.Z, byPlayer);
            }

            if (EntityClass != null)
            {
                BlockEntity entity = world.BlockAccessor.GetBlockEntity(pos);
                if (entity != null)
                {
                    entity.OnBlockBroken();
                }
            }

            world.BlockAccessor.SetBlock(0, pos);
        }

        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {

        }

        public MeshData GenMesh(ItemStack contentStack, ItemStack liquidContentStack, BlockPos forBlockPos = null)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;

            Shape shape = capi.Assets.TryGet("shapes/block/wood/barrel/empty.json").ToObject<Shape>();
            MeshData barrelMesh;
            capi.Tesselator.TesselateShape(this, shape, out barrelMesh);
            MeshData contentMesh = getContentMesh(contentStack, forBlockPos, "contents.json");
            if (contentMesh != null) barrelMesh.AddMeshData(contentMesh);

            bool isopaque = liquidContentStack?.ItemAttributes?["waterTightContainerProps"]?["isopaque"].AsBool(false) == true;
            bool isliquid = liquidContentStack?.ItemAttributes?["waterTightContainerProps"].Exists == true;
            if (liquidContentStack != null && (isliquid || contentStack == null))
            {
                string shapefilename = isliquid && !isopaque ? "liquidcontents.json" : "contents.json";
                contentMesh = getContentMesh(liquidContentStack, forBlockPos, shapefilename);
                if (contentMesh != null) barrelMesh.AddMeshData(contentMesh);
            }

            if (forBlockPos != null)
            {
                // Water flags
                barrelMesh.CustomInts = new CustomMeshDataPartInt(barrelMesh.FlagsCount);
                barrelMesh.CustomInts.Values.Fill(0x4000000); // light foam only
                barrelMesh.CustomInts.Count = barrelMesh.FlagsCount;

                barrelMesh.CustomFloats = new CustomMeshDataPartFloat(barrelMesh.FlagsCount * 2);
                barrelMesh.CustomFloats.Count = barrelMesh.FlagsCount * 2;
            }


            return barrelMesh;
        }

        #endregion

        protected MeshData getContentMesh(ItemStack stack, BlockPos forBlockPos, string shapefilename)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;

            WaterTightContainableProps props = GetContainableProps(stack);
            ITexPositionSource contentSource;
            float fillHeight;

            if (props != null)
            {
                contentSource = new ContainerTextureSource(capi, stack, props.Texture);
                fillHeight = GameMath.Min(1f, stack.StackSize / props.ItemsPerLitre / Math.Max(50, props.MaxStackSize)) * 10f / 16f;

                if (props.Texture == null) return null;
            }
            else
            {
                contentSource = getContentTexture(capi, stack, out fillHeight);
            }


            if (stack != null && contentSource != null)
            {
                Shape shape = capi.Assets.TryGet("shapes/block/wood/barrel/" + shapefilename).ToObject<Shape>();
                MeshData contentMesh;
                capi.Tesselator.TesselateShape("barrel", shape, out contentMesh, contentSource, new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ));

                contentMesh.Translate(0, fillHeight, 0);

                if (props?.ClimateColorMap != null)
                {
                    int col = capi.World.ApplyColorMapOnRgba(props.ClimateColorMap, null, ColorUtil.WhiteArgb, 196, 128, false);
                    if (forBlockPos != null)
                    {
                        col = capi.World.ApplyColorMapOnRgba(props.ClimateColorMap, null, ColorUtil.WhiteArgb, forBlockPos.X, forBlockPos.Y, forBlockPos.Z, false);
                    }

                    byte[] rgba = ColorUtil.ToBGRABytes(col);

                    for (int i = 0; i < contentMesh.Rgba.Length; i++)
                    {
                        contentMesh.Rgba[i] = (byte)((contentMesh.Rgba[i] * rgba[i % 4]) / 255);
                    }
                }


                return contentMesh;
            }

            return null;
        }

        public static ITexPositionSource getContentTexture(ICoreClientAPI capi, ItemStack stack, out float fillHeight)
        {
            ITexPositionSource contentSource = null;
            fillHeight = 0;

            JsonObject obj = stack?.ItemAttributes?["inContainerTexture"];
            if (obj != null && obj.Exists)
            {
                contentSource = new ContainerTextureSource(capi, stack, obj.AsObject<CompositeTexture>());
                fillHeight = GameMath.Min(10 / 16f, 0.7f * stack.StackSize / stack.Collectible.MaxStackSize);
            }
            else
            {
                if (stack?.Block != null && (stack.Block.DrawType == EnumDrawType.Cube || stack.Block.Shape.Base.Path.Contains("basic/cube")) && capi.BlockTextureAtlas.GetPosition(stack.Block, "up", true) != null)
                {
                    contentSource = new BlockTopTextureSource(capi, stack.Block);
                    fillHeight = GameMath.Min(10 / 16f, 0.7f * stack.StackSize / stack.Collectible.MaxStackSize);
                }
                else if (stack != null)
                {

                    if (stack.Class == EnumItemClass.Block)
                    {
                        if (stack.Block.Textures.Count > 1) return null;

                        contentSource = new ContainerTextureSource(capi, stack, stack.Block.Textures.FirstOrDefault().Value);
                    }
                    else
                    {
                        if (stack.Item.Textures.Count > 1) return null;

                        contentSource = new ContainerTextureSource(capi, stack, stack.Item.FirstTexture);
                    }


                    fillHeight = GameMath.Min(10 / 16f, 0.7f * stack.StackSize / stack.Collectible.MaxStackSize);
                }
            }

            return contentSource;
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-place",
                    HotKeyCode = "sneak",
                    MouseButton = EnumMouseButton.Right,
                    ShouldApply = (wi, bs, es) => {
                        return true;
                    }
                }
            };
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (Attributes?["capacityLitres"].Exists == true)
            {
                capacityLitresFromAttributes = Attributes["capacityLitres"].AsInt(50);
            }


            if (api.Side != EnumAppSide.Client) return;
            ICoreClientAPI capi = api as ICoreClientAPI;

            interactions = ObjectCacheUtil.GetOrCreate(api, "liquidContainerBase", () =>
            {
                List<ItemStack> liquidContainerStacks = new List<ItemStack>();

                foreach (CollectibleObject obj in api.World.Collectibles)
                {
                    if (obj is ILiquidSource || obj is ILiquidSink || obj is BlockWateringCan)
                    {
                        List<ItemStack> stacks = obj.GetHandBookStacks(capi);
                        if (stacks != null) liquidContainerStacks.AddRange(stacks);
                    }
                }

                ItemStack[] lstacks = liquidContainerStacks.ToArray();

                return new WorldInteraction[] {
                    new WorldInteraction()
                    {
                        ActionLangCode = "blockhelp-bucket-rightclick",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = lstacks,
                        GetMatchingStacks = (wi, bs, ws) =>
                        {
                            BlockEntityCauld becauld = api.World.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityCauld;
                            return lstacks;
                        }
                    }
                };
            });
        }

        public override void OnHeldInteractStart(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
        {
            base.OnHeldInteractStart(itemslot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
        }


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityCauld becauld = null;
            if (blockSel.Position != null)
            {
                becauld = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityCauld;
            }

            bool handled = base.OnBlockInteractStart(world, byPlayer, blockSel);

            if (!handled && !byPlayer.WorldData.EntityControls.Sneak && blockSel.Position != null)
            {
                if (becauld != null)
                {
                    becauld.OnBlockInteract(byPlayer);
                }

                return true;
            }

            return handled;
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            ItemStack[] contentStacks = GetContents(world, inSlot.Itemstack);

            if (contentStacks != null && contentStacks.Length > 0)
            {
                ItemStack itemstack = contentStacks[0] == null ? contentStacks[1] : contentStacks[0];
                if (itemstack != null) dsc.Append(", " + Lang.Get("{0}x {1}", itemstack.StackSize, itemstack.GetName()));
            }
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            string text = "";

            float litres = GetCurrentLitres(pos);
            if (litres <= 0) text = "";

            BlockEntityCauld becauld = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCauld;
            if (becauld != null)
            {
                ItemSlot outslot = becauld.Inventory[5];
                ItemSlot inslot = becauld.Inventory[6];
                if (text.Length > 0) text += "\n";
                else text += Lang.Get("Contents:");
                if (inslot.Empty && outslot.Empty)
                {
                    text += "\nEmpty";
                }
                else
                {
                    if (!outslot.Empty)
                    {
                        WaterTightContainableProps outprops = BlockLiquidContainerBase.GetContainableProps(outslot.Itemstack);
                        text += "\n" + Lang.Get("Potion out: {0} Litres of {1}", outslot.Itemstack.StackSize / outprops.ItemsPerLitre, outslot.Itemstack.GetName());

                    }

                    if (!inslot.Empty)
                    {
                        WaterTightContainableProps inprops = BlockLiquidContainerBase.GetContainableProps(inslot.Itemstack);
                        text += "\n" + Lang.Get("Potion in: {0} Litres of {1}", becauld.Inventory[6].Itemstack.StackSize / inprops.ItemsPerLitre, becauld.Inventory[6].Itemstack.GetName());
                    }
                    if (becauld.isFull)
                    {
                        text += "\nEmpty output to mix new potions";
                    }
                }

            }


            return text;
        }


        public override void TryFillFromBlock(EntityItem byEntityItem, BlockPos pos)
        {
            // Don't fill when dropped as item in water
        }


    }
}
