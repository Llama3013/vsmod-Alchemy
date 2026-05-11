using System;
using System.Collections.Generic;
using System.Text;
using Alchemy.Item;
using Alchemy.ModConfig;
using Alchemy.Utility;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Alchemy.Block
{
    //Add perish time to potions but potion flasks have low perish rates or do not perish
    public class BlockPotionFlask : BlockLiquidContainerTopOpened
    {
        private const string potionInfo = "potioninfo";

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

            return baseInteractions;
        }

        #endregion Render

        #region Interaction

        private static readonly Dictionary<long, long> coatHoldStartMs = [];
        private const float CoatHoldDurationSec = 1.5f;

        internal static readonly HashSet<string> AllowedCoatingPotionIds =
        [
            "poisontickpotionid",
            "regentickpotionid",
        ];

        private TagSet weaponMeleeTag;
        private bool weaponMeleeTagCached;

        private TagSet GetWeaponMeleeTag()
        {
            if (!weaponMeleeTagCached)
            {
                weaponMeleeTagCached = true;
                api.CollectibleTagRegistry.TryCreateTagSet(
                    out weaponMeleeTag,
                    new List<string> { "weapon-melee" }
                );
            }
            return weaponMeleeTag;
        }

        public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
        {
            base.OnHeldIdle(slot, byEntity);

            ItemStack contentStack = GetContent(slot.Itemstack);

            JsonObject potion = contentStack?.ItemAttributes?[potionInfo];

            string potionId = potion?.Exists == true ? potion["potionId"].AsString() : null;

            string potionStrength = "weak";

            contentStack?.Collectible?.Variant?.TryGetValue("strength", out potionStrength);

            PotionConsumableLogic.HandleWeaponCoatingIdle(
                api,
                slot,
                byEntity,
                potionId,
                potionStrength,
                s =>
                {
                    return GetContent(s.Itemstack)?.GetName()
                        ?? Lang.Get($"alchemy:coatname-{potionId}");
                },
                s =>
                {
                    EntityPlayer player = byEntity as EntityPlayer;

                    int consumed = SplitStackAndPerformAction(
                        player,
                        s,
                        stack => TryTakeLiquid(stack, 0.25f)?.StackSize ?? 0
                    );

                    s.MarkDirty();

                    return consumed > 0;
                }
            );
        }

        public override void OnHeldInteractStart(
            ItemSlot itemslot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel,
            bool firstEvent,
            ref EnumHandHandling handHandling
        )
        {
            if (blockSel != null && byEntity.Controls.CtrlKey && byEntity.Controls.ShiftKey)
            {
                byEntity.Controls.ShiftKey = false;

                base.OnHeldInteractStart(
                    itemslot,
                    byEntity,
                    blockSel,
                    entitySel,
                    firstEvent,
                    ref handHandling
                );

                return;
            }

            ItemStack contentStack = GetContent(itemslot.Itemstack);

            if (contentStack != null && !byEntity.Controls.ShiftKey)
            {
                JsonObject potion = contentStack.ItemAttributes?[potionInfo];

                if (potion?.Exists ?? false)
                {
                    string potionId = potion["potionId"].AsString();

                    if (
                        potionId == "recallpotionid"
                        && byEntity.MountedOn?.MountSupplier?.OnEntity?.Code?.Path != null
                        && WildcardUtil.Match(
                            "boat-sailed-*",
                            byEntity.MountedOn.MountSupplier.OnEntity.Code.Path
                        )
                        && byEntity.World.Side == EnumAppSide.Server
                    )
                    {
                        EntityPlayer playerEntity = byEntity as EntityPlayer;

                        IServerPlayer serverPlayer = playerEntity?.Player as IServerPlayer;

                        serverPlayer.SendMessage(
                            GlobalConstants.InfoLogChatGroup,
                            Lang.Get("alchemy:boat-block"),
                            EnumChatType.Notification
                        );

                        return;
                    }

                    if (
                        PotionConsumableLogic.HandleDrinkStart(
                            byEntity,
                            potionId,
                            "drink",
                            () => playEatSound(byEntity, "drink", 1),
                            ref handHandling
                        )
                    )
                    {
                        return;
                    }
                }
            }

            // Prevent accidental spilling unless CTRL + SHIFT are both held
            if (blockSel != null && byEntity.Controls.CtrlKey && !byEntity.Controls.ShiftKey)
            {
                handHandling = EnumHandHandling.PreventDefaultAction;
                return;
            }

            base.OnHeldInteractStart(
                itemslot,
                byEntity,
                blockSel,
                entitySel,
                firstEvent,
                ref handHandling
            );
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

        public override bool OnHeldInteractStep(
            float secondsUsed,
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel
        )
        {
            return PotionConsumableLogic.HandleDrinkStep(secondsUsed, slot, byEntity, true);
        }

        public override void OnHeldInteractStop(
            float secondsUsed,
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel
        )
        {
            if (
                HandleCollectibleBehaviorsForDrink(secondsUsed, slot, byEntity, blockSel, entitySel)
            )
            {
                return;
            }

            ItemStack contentStack = GetContent(slot.Itemstack);

            if (contentStack?.Item is ItemPotion potion)
            {
                JsonObject potionData = contentStack.ItemAttributes?[potionInfo];

                string potionId =
                    potionData?.Exists == true ? potionData["potionId"].AsString() : null;

                string strength = "weak";

                contentStack.Collectible?.Variant?.TryGetValue("strength", out strength);

                PotionConsumableLogic.HandleDrinkStop(
                    secondsUsed,
                    byEntity,
                    slot,
                    potionId,
                    strength,
                    () =>
                    {
                        EntityPlayer playerEntity = byEntity as EntityPlayer;

                        int consumed = SplitStackAndPerformAction(
                            playerEntity,
                            slot,
                            stack => TryTakeLiquid(stack, 0.25f)?.StackSize ?? 0
                        );

                        slot.MarkDirty();

                        if (playerEntity?.Player != null)
                        {
                            playerEntity.Player.InventoryManager.BroadcastHotbarSlot();
                        }

                        return consumed > 0;
                    },
                    () => contentStack.GetName(),
                    api
                );
            }

            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
        }

        private bool HandleCollectibleBehaviorsForDrink(
            float secondsUsed,
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel
        )
        {
            bool preventDefault = false;

            foreach (CollectibleBehavior behavior in CollectibleBehaviors)
            {
                EnumHandling handled = EnumHandling.PassThrough;

                behavior.OnHeldInteractStop(
                    secondsUsed,
                    slot,
                    byEntity,
                    blockSel,
                    entitySel,
                    ref handled
                );
                if (handled != EnumHandling.PassThrough)
                    preventDefault = true;

                if (handled == EnumHandling.PreventSubsequent)
                    return true;
            }

            return preventDefault;
        }

        #endregion Interaction

        public override void GetHeldItemInfo(
            ItemSlot inSlot,
            StringBuilder dsc,
            IWorldAccessor world,
            bool withDebugInfo
        )
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            ItemStack contentStack = GetContent(inSlot.Itemstack);
            contentStack?.Collectible.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        }
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
