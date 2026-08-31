using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    //Add perish time to potions but potion flasks have low perish rates or do not perish
    public class BlockPotionFlask : BlockLiquidContainerTopOpened, IContainedMeshSource
    {
        #region Render

        public override void OnBeforeRender(
            ICoreClientAPI capi,
            ItemStack itemstack,
            EnumItemRenderTarget target,
            ref ItemRenderInfo renderinfo
        )
        {
            if (Code.Path.Contains("clay"))
                return;

            ItemStack contentStack = GetContent(itemstack);
            if (
                contentStack == null
                || contentStack.StackSize <= 0
                || contentStack?.Collectible?.Code == null
            )
                return;

            Dictionary<int, MultiTextureMeshRef> meshrefs;

            if (capi.ObjectCache.TryGetValue(meshRefsCacheKey, out object obj))
            {
                meshrefs = obj as Dictionary<int, MultiTextureMeshRef>;
            }
            else
            {
                capi.ObjectCache[meshRefsCacheKey] = meshrefs = [];
            }

            int hashcode = GetStackCacheHashCode(contentStack);

            if (!meshrefs.TryGetValue(hashcode, out MultiTextureMeshRef meshRef))
            {
                MeshData meshdata = GenMesh(capi, contentStack);
                meshrefs[hashcode] = meshRef = capi.Render.UploadMultiTextureMesh(meshdata);
            }

            renderinfo.ModelRef = meshRef;
        }

        private MeshData origContainerMesh;
        private const float LiquidSurfaceAlpha = 0.8f;

        // I need this for potion flasks to render the liquid contents while placed in ground storage otherwise it will use the base GenMesh which will look full.
        public new MeshData GenMesh(
            ItemSlot slot,
            ITextureAtlasAPI targetAtlas,
            BlockPos atBlockPos
        )
        {
            return GenMesh(api as ICoreClientAPI, GetContent(slot.Itemstack), atBlockPos);
        }

        public new MeshData GenMesh(
            ICoreClientAPI capi,
            ItemStack contentStack,
            BlockPos forBlockPos = null
        )
        {
            if (origContainerMesh == null)
            {
                Shape shape = Vintagestory.API.Common.Shape.TryGet(
                    capi,
                    emptyShapeLoc.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/")
                );
                if (shape == null)
                {
                    capi.World.Logger.Error(
                        "Empty shape {0} not found. Liquid container {1} will be invisible.",
                        emptyShapeLoc,
                        Code
                    );
                    return new MeshData();
                }
                capi.Tesselator.TesselateShape(
                    this,
                    shape,
                    out origContainerMesh,
                    new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ)
                );
            }

            MeshData containerMesh = origContainerMesh.Clone();

            if (Code.Path.Contains("clay"))
                return containerMesh;

            if (contentStack != null)
            {
                WaterTightContainableProps props = GetContainableProps(contentStack);
                if (props == null)
                {
                    capi.World.Logger.Error(
                        "Contents ('{0}') has no liquid properties, contents of liquid container {1} will be invisible.",
                        contentStack.GetName(),
                        Code
                    );
                    return containerMesh;
                }
                if (props.Texture == null || this == null)
                    return containerMesh;
                FlaskTextureSource contentSource = new(capi, contentStack, props.Texture, this);

                float level = contentStack.StackSize / props.ItemsPerLitre;
                Shape shape;
                if (Code.Path.Contains("flask-normal"))
                {
                    if (level > 0 && level <= 0.25)
                    {
                        shape = capi
                            .Assets.TryGet("alchemy:shapes/block/glass/flask-liquid-1.json")
                            .ToObject<Shape>();
                    }
                    else if (level <= 0.5)
                    {
                        shape = capi
                            .Assets.TryGet("alchemy:shapes/block/glass/flask-liquid-2.json")
                            .ToObject<Shape>();
                    }
                    else if (level < 1)
                    {
                        shape = capi
                            .Assets.TryGet("alchemy:shapes/block/glass/flask-liquid-3.json")
                            .ToObject<Shape>();
                    }
                    else
                    {
                        shape = capi
                            .Assets.TryGet("alchemy:shapes/block/glass/flask-liquid.json")
                            .ToObject<Shape>();
                    }
                }
                else if (Code.Path.Contains("flask-round"))
                {
                    if (level < 1)
                    {
                        shape = capi
                            .Assets.TryGet("alchemy:shapes/block/glass/roundflask-liquid-1.json")
                            .ToObject<Shape>();
                    }
                    else
                    {
                        shape = capi
                            .Assets.TryGet("alchemy:shapes/block/glass/roundflask-liquid.json")
                            .ToObject<Shape>();
                    }
                }
                else if (Code.Path.Contains("throwableflask"))
                {
                    shape = capi
                        .Assets.TryGet(
                            "alchemy:shapes/block/glass/throwable-roundflask-liquid.json"
                        )
                        .ToObject<Shape>();
                }
                else
                {
                    shape =
                        level > 0
                            ? capi
                                .Assets.TryGet("alchemy:shapes/block/glass/tubeflask-liquid.json")
                                .ToObject<Shape>()
                            : null;
                }

                if (shape == null)
                {
                    capi.World.Logger.Error(
                        "Content shape not found. Contents of liquid container {0} will be invisible.",
                        Code
                    );
                    return containerMesh;
                }

                capi.Tesselator.TesselateShape(
                    "potionflask",
                    shape,
                    out containerMesh,
                    contentSource,
                    new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ),
                    props.GlowLevel
                );

                if (forBlockPos == null && !props.IsOpaque)
                {
                    TextureAtlasPosition contentPos = contentSource["content"];
                    if (contentPos != null)
                    {
                        float[] uv = containerMesh.Uv;
                        byte[] rgba = containerMesh.Rgba;
                        for (int i = 0; i < containerMesh.VerticesCount; i++)
                        {
                            float u = uv[i * 2];
                            float v = uv[i * 2 + 1];
                            if (
                                u >= contentPos.x1
                                && u <= contentPos.x2
                                && v >= contentPos.y1
                                && v <= contentPos.y2
                            )
                            {
                                rgba[i * 4 + 3] = (byte)(rgba[i * 4 + 3] * LiquidSurfaceAlpha);
                            }
                        }
                    }
                }
            }

            return containerMesh;
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            if (api is not ICoreClientAPI capi)
                return;

            if (capi.ObjectCache.TryGetValue(meshRefsCacheKey, out object obj))
            {
                Dictionary<int, MultiTextureMeshRef> meshrefs =
                    obj as Dictionary<int, MultiTextureMeshRef>;

                foreach (KeyValuePair<int, MultiTextureMeshRef> val in meshrefs)
                {
                    val.Value.Dispose();
                }

                capi.ObjectCache.Remove(meshRefsCacheKey);
            }
        }

        // Replace empty with ctrl + shift to avoid accidental spilling
        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            WorldInteraction[] baseInteractions = base.GetHeldInteractionHelp(inSlot);

            for (int i = 0; i < baseInteractions.Length; i++)
            {
                if (baseInteractions[i].ActionLangCode == "heldhelp-empty")
                {
                    baseInteractions[i].HotKeyCodes = ["ctrl", "shift"];
                    break;
                }
            }

            if (!AlchemyConfig.Loaded.AllowWeaponCoating)
                return baseInteractions;

            return
            [
                .. baseInteractions,
                new WorldInteraction
                {
                    ActionLangCode = "alchemy:heldhelp-coat-weapon",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCodes = ["shift"],
                },
            ];
        }

        #endregion Render

        #region Interaction

        // This is needed as a workaround for now because there is no such OnHeldIdle override on behaviors and hopefully it will be implemented into VS eventually
        public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
        {
            base.OnHeldIdle(slot, byEntity);
            foreach (CollectibleBehavior bh in CollectibleBehaviors)
                if (bh is EffectLib.CollectibleBehaviorCoatSource coat)
                {
                    coat.CoatingIdle(slot, byEntity);
                    return;
                }
        }

        public override void OnHeldInteractStart(
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel,
            bool firstEvent,
            ref EnumHandHandling handling
        )
        {
            if (blockSel != null && byEntity.Controls.CtrlKey && byEntity.Controls.ShiftKey)
            {
                byEntity.Controls.ShiftKey = false;

                base.OnHeldInteractStart(
                    slot,
                    byEntity,
                    blockSel,
                    entitySel,
                    firstEvent,
                    ref handling
                );

                return;
            }

            // Prevent accidental spilling unless CTRL + SHIFT are both held
            if (blockSel != null && byEntity.Controls.CtrlKey && !byEntity.Controls.ShiftKey)
            {
                handling = EnumHandHandling.PreventDefaultAction;
                return;
            }

            // BlockLiquidContainerTopOpened intercepts blockSel interactions for liquid transfer
            // and does not dispatch CollectibleBehaviors. Explicitly run the consumable behavior
            // first so drinking takes priority over liquid transfer when the flask holds a potion.
            if (blockSel != null && !byEntity.Controls.ShiftKey)
            {
                foreach (CollectibleBehavior bh in CollectibleBehaviors)
                {
                    if (bh is PotionConsumableLiquidBehavior)
                    {
                        EnumHandling bhHandling = EnumHandling.PassThrough;
                        bh.OnHeldInteractStart(
                            slot,
                            byEntity,
                            blockSel,
                            entitySel,
                            firstEvent,
                            ref handling,
                            ref bhHandling
                        );
                        if (bhHandling != EnumHandling.PassThrough)
                            return;
                        break;
                    }
                }
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }

        public override string GetItemDescText()
        {
            return Lang.Get("alchemy:blockdesc-potionflask", CapacityLitres) + "\n";
        }

        public override void TryMergeStacks(ItemStackMergeOperation op)
        {
            ItemStack sourceStack = GetContent(op.SourceSlot.Itemstack);
            ItemStack sinkStack = GetContent(op.SinkSlot.Itemstack);
            if (
                op.SourceSlot.Itemstack.StackSize > 1
                && sourceStack != null
                && sinkStack != null
                && sourceStack.StackSize != sinkStack.StackSize
            )
                return;

            base.TryMergeStacks(op);
        }

        #endregion Interaction
    }

    public class FlaskTextureSource(
        ICoreClientAPI capi,
        ItemStack forContents,
        CompositeTexture contentTexture,
        Block flask
    ) : ITexPositionSource
    {
        public ItemStack ForContents { get; set; } =
            forContents ?? throw new ArgumentNullException(nameof(forContents));

        private readonly ICoreClientAPI capi =
            capi ?? throw new ArgumentNullException(nameof(capi));

        private TextureAtlasPosition contentTextPos;
        private readonly TextureAtlasPosition blockTextPos = capi.BlockTextureAtlas.GetPosition(
            flask,
            "glass"
        );
        private readonly TextureAtlasPosition corkTextPos = capi.BlockTextureAtlas.GetPosition(
            flask,
            "topper",
            true
        );
        private readonly TextureAtlasPosition bracingTextPos = capi.BlockTextureAtlas.GetPosition(
            flask,
            "bracing",
            true
        );

        private readonly CompositeTexture contentTexture =
            contentTexture ?? throw new ArgumentNullException(nameof(contentTexture));

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                if (textureCode != null)
                {
                    if (textureCode == "topper" && corkTextPos != null)
                        return corkTextPos;
                    if (textureCode == "glass" && blockTextPos != null)
                        return blockTextPos;
                    if (textureCode == "bracing" && bracingTextPos != null)
                        return bracingTextPos;
                }
                if (contentTextPos == null)
                {
                    if (contentTexture.Baked == null)
                        contentTexture.Bake(capi.Assets);

                    capi.BlockTextureAtlas.GetOrInsertTexture(
                        contentTexture.Baked.BakedName,
                        out _,
                        out contentTextPos
                    );
                }
                return contentTextPos ?? capi.BlockTextureAtlas.UnknownTexturePosition;
            }
        }

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
    }
}
