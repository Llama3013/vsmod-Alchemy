using System;
using System.Collections.Generic;
using Alchemy.ModConfig;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Alchemy.Block
{
    //Add perish time to potions but potion flasks have low perish rates or do not perish
    public class BlockPotionFlask : BlockLiquidContainerTopOpened
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
        private Shape contentShape;
        private Shape liquidContentShape;

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
                Shape shape = props.IsOpaque ? contentShape : liquidContentShape;
                AssetLocation loc = props.IsOpaque ? contentShapeLoc : liquidContentShapeLoc;
                if (shape == null)
                {
                    shape = Vintagestory.API.Common.Shape.TryGet(
                        capi,
                        loc.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/")
                    );

                    if (props.IsOpaque)
                        contentShape = shape;
                    else
                        liquidContentShape = shape;
                }
                //If a shape is found and a block position is set then use base game tesselation
                if (shape != null && forBlockPos != null)
                {
                    capi.Tesselator.TesselateShape(
                        GetType().Name,
                        shape,
                        out MeshData contentMesh,
                        contentSource,
                        new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ),
                        props.GlowLevel
                    );

                    contentMesh.Translate(
                        0,
                        GameMath.Min(
                            liquidMaxYTranslate,
                            contentStack.StackSize / props.ItemsPerLitre * liquidYTranslatePerLitre
                        ),
                        0
                    );

                    if (props.ClimateColorMap != null)
                    {
                        int col;
                        if (forBlockPos != null)
                        {
                            col = capi.World.ApplyColorMapOnRgba(
                                props.ClimateColorMap,
                                null,
                                ColorUtil.WhiteArgb,
                                forBlockPos.X,
                                forBlockPos.Y,
                                forBlockPos.Z,
                                false
                            );
                        }
                        else
                        {
                            col = capi.World.ApplyColorMapOnRgba(
                                props.ClimateColorMap,
                                null,
                                ColorUtil.WhiteArgb,
                                196,
                                128,
                                false
                            );
                        }

                        byte[] rgba = ColorUtil.ToBGRABytes(col);

                        for (int i = 0; i < contentMesh.Rgba.Length; i++)
                        {
                            contentMesh.Rgba[i] = (byte)((contentMesh.Rgba[i] * rgba[i % 4]) / 255);
                        }
                    }

                    for (int i = 0; i < contentMesh.FlagsCount; i++)
                    {
                        contentMesh.Flags[i] = contentMesh.Flags[i] & ~(1 << 12); // Remove water waving flag
                    }

                    containerMesh.AddMeshData(contentMesh);

                    // Water flags
                    if (forBlockPos != null)
                    {
                        containerMesh.CustomInts = new(containerMesh.FlagsCount)
                        {
                            Count = containerMesh.FlagsCount,
                        };
                        containerMesh.CustomInts.Values.Fill(0x4000000); // light foam only

                        containerMesh.CustomFloats = new(containerMesh.FlagsCount * 2)
                        {
                            Count = containerMesh.FlagsCount * 2,
                        };
                    }
                    return containerMesh;
                }
                else
                {
                    //This is need to render flasks with liquid in inventory
                    float level = contentStack.StackSize / props.ItemsPerLitre;
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
                                .Assets.TryGet(
                                    "alchemy:shapes/block/glass/roundflask-liquid-1.json"
                                )
                                .ToObject<Shape>();
                        }
                        else
                        {
                            shape = capi
                                .Assets.TryGet("alchemy:shapes/block/glass/roundflask-liquid.json")
                                .ToObject<Shape>();
                        }
                    }
                    else
                    {
                        if (level > 0)
                        {
                            shape = capi
                                .Assets.TryGet("alchemy:shapes/block/glass/tubeflask-liquid.json")
                                .ToObject<Shape>();
                        }
                    }
                }
                if (shape == null)
                {
                    capi.World.Logger.Error(
                        "Content shape {0} not found. Contents of liquid container {1} will be invisible.",
                        loc,
                        Code
                    );
                    return containerMesh;
                }

                capi.Tesselator.TesselateShape(
                    "potionflask",
                    shape,
                    out containerMesh,
                    contentSource,
                    new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ)
                );
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
            if (byEntity.Controls.ShiftKey && byEntity.Controls.RightMouseDown)
                foreach (CollectibleBehavior bh in CollectibleBehaviors)
                    if (bh is Behavior.PotionCoatSourceBehavior coat)
                        coat.CoatingIdle(slot, byEntity);
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
                    if (bh is Behavior.PotionConsumableBehavior)
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
        Vintagestory.API.Common.Block flask
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
            "topper"
        );
        private readonly TextureAtlasPosition bracingTextPos = capi.BlockTextureAtlas.GetPosition(
            flask,
            "bracing"
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
                    int textureSubId = ObjectCacheUtil.GetOrCreate(
                        capi,
                        "contenttexture-" + contentTexture?.ToString() ?? "unknown",
                        () =>
                        {
                            int id = -1;

                            BitmapRef bmp = capi
                                .Assets.TryGet(
                                    contentTexture
                                        ?.Base.Clone()
                                        .WithPathPrefixOnce("textures/")
                                        .WithPathAppendixOnce(".png")
                                        ?? new AssetLocation(
                                            "alchemy:textures/item/potion/black_potion.png"
                                        )
                                )
                                ?.ToBitmap(capi);

                            if (bmp != null)
                            {
                                try
                                {
                                    _ = capi.BlockTextureAtlas.InsertTexture(
                                        bmp,
                                        out id,
                                        out TextureAtlasPosition _
                                    );
                                }
                                catch (Exception ex)
                                {
                                    capi.World.Logger.Error(
                                        $"Error inserting texture: {ex.Message}"
                                    );
                                    id = -1;
                                }
                                bmp.Dispose();
                            }
                            else
                            {
                                capi.World.Logger.Warning("Bitmap for content texture is null.");
                            }

                            return id;
                        }
                    );

                    // Check if the index is valid
                    if (textureSubId >= 0 && textureSubId < capi.BlockTextureAtlas.Positions.Length)
                    {
                        contentTextPos = capi.BlockTextureAtlas.Positions[textureSubId];
                    }
                    else
                    {
                        capi.World.Logger.Error(
                            $"Invalid textureSubId: {textureSubId}. Positions length: {capi.BlockTextureAtlas.Positions.Length}"
                        );
                        contentTextPos = null;
                    }
                }
                return contentTextPos;
            }
        }

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
    }
}
