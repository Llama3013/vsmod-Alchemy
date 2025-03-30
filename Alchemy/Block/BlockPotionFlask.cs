using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.API.Util;

namespace Alchemy
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
            Dictionary<int, MultiTextureMeshRef> meshrefs;

            if (capi.ObjectCache.TryGetValue(meshRefsCacheKey, out object obj))
            {
                meshrefs = obj as Dictionary<int, MultiTextureMeshRef>;
            }
            else
            {
                capi.ObjectCache[meshRefsCacheKey] = meshrefs =
                    new Dictionary<int, MultiTextureMeshRef>();
            }

            ItemStack contentStack = GetContent(itemstack);
            if (contentStack == null)
                return;

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
                            Count = containerMesh.FlagsCount
                        };
                        containerMesh.CustomInts.Values.Fill(0x4000000); // light foam only

                        containerMesh.CustomFloats = new(containerMesh.FlagsCount * 2)
                        {
                            Count = containerMesh.FlagsCount * 2
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
                            shape = capi.Assets
                                .TryGet("alchemy:shapes/block/glass/flask-liquid-1.json")
                                .ToObject<Shape>();
                        }
                        else if (level <= 0.5)
                        {
                            shape = capi.Assets
                                .TryGet("alchemy:shapes/block/glass/flask-liquid-2.json")
                                .ToObject<Shape>();
                        }
                        else if (level < 1)
                        {
                            shape = capi.Assets
                                .TryGet("alchemy:shapes/block/glass/flask-liquid-3.json")
                                .ToObject<Shape>();
                        }
                        else
                        {
                            shape = capi.Assets
                                .TryGet("alchemy:shapes/block/glass/flask-liquid.json")
                                .ToObject<Shape>();
                        }
                    }
                    else if (Code.Path.Contains("flask-round"))
                    {
                        if (level < 1)
                        {
                            shape = capi.Assets
                                .TryGet("alchemy:shapes/block/glass/roundflask-liquid-1.json")
                                .ToObject<Shape>();
                        }
                        else
                        {
                            shape = capi.Assets
                                .TryGet("alchemy:shapes/block/glass/roundflask-liquid.json")
                                .ToObject<Shape>();
                        }
                    }
                    else
                    {
                        if (level > 0)
                        {
                            shape = capi.Assets
                                .TryGet("alchemy:shapes/block/glass/tubeflask-liquid.json")
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

                foreach (var val in meshrefs)
                {
                    val.Value.Dispose();
                }

                capi.ObjectCache.Remove(meshRefsCacheKey);
            }
        }

        #endregion Render

        #region Interaction

        public override void OnHeldInteractStart(
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel,
            bool firstEvent,
            ref EnumHandHandling handling
        )
        {
            ItemStack contentStack = GetContent(slot.Itemstack);
            if (contentStack != null && !byEntity.Controls.Sprint && !byEntity.Controls.Sneak)
            {
                JsonObject potion = contentStack.ItemAttributes?["potioninfo"];
                if (potion?.Exists ?? false)
                {
                    string potionId = potion["potionId"].AsString();
                    //api.Logger.Debug("[Potion] potionId {0}", potionId);
                    //api.Logger.Debug("[Potion] drinkable if number is zero: {0}", byEntity.WatchedAttributes.GetLong(potionId));
                    if (potionId == "recallpotionid" && byEntity.MountedOn?.MountSupplier?.OnEntity?.Code?.Path != null && WildcardUtil.Match("boat-sailed-*", byEntity.MountedOn.MountSupplier.OnEntity.Code.Path) && byEntity.World.Side == EnumAppSide.Server)
                    {
                        var playerEntity = byEntity as EntityPlayer;
                        var serverPlayer = playerEntity?.Player as IServerPlayer;
                        serverPlayer.SendMessage(
                            GlobalConstants.InfoLogChatGroup,
                            Lang.Get("alchemy:boat-block"),
                            EnumChatType.Notification
                        );
                        return;
                    }
                    /* This checks if the potion effect callback is on */
                    if (
                        !string.IsNullOrWhiteSpace(potionId)
                        && byEntity.WatchedAttributes.GetLong(potionId) == 0
                    )
                    {
                        //api.Logger.Debug("potion {0}", byEntity.WatchedAttributes.GetLong(potionId));
                        byEntity.World.RegisterCallback(
                            (dt) => playEatSound(byEntity, "drink", 1),
                            500
                        );
                        byEntity.AnimManager?.StartAnimation("eat");
                        handling = EnumHandHandling.PreventDefault;
                        return;
                    }
                }
            }
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            return;
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
            bool result = true;
            bool preventDefault = false;

            foreach (CollectibleBehavior behavior in CollectibleBehaviors)
            {
                EnumHandling handled = EnumHandling.PassThrough;

                bool behaviorResult = behavior.OnHeldInteractStep(
                    secondsUsed,
                    slot,
                    byEntity,
                    blockSel,
                    entitySel,
                    ref handled
                );
                if (handled != EnumHandling.PassThrough)
                {
                    result &= behaviorResult;
                    preventDefault = true;
                }

                if (handled == EnumHandling.PreventSubsequent)
                    return result;
            }

            if (preventDefault)
                return result;

            Vec3d pos = byEntity.Pos.AheadCopy(0.4f).XYZ;
            pos.X += byEntity.LocalEyePos.X;
            pos.Y += byEntity.LocalEyePos.Y - 0.4f;
            pos.Z += byEntity.LocalEyePos.Z;

            if (secondsUsed > 0.5f && (int)(30 * secondsUsed) % 7 == 1)
            {
                byEntity.World.SpawnCubeParticles(
                    pos,
                    slot.Itemstack,
                    0.3f,
                    4,
                    0.5f,
                    (byEntity as EntityPlayer)?.Player
                );
            }

            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new();

                tf.EnsureDefaultValues();

                tf.Origin.Set(0f, 0, 0f);

                if (secondsUsed > 0.5f)
                {
                    tf.Translation.Y = Math.Min(0.02f, GameMath.Sin(20 * secondsUsed) / 10);
                }

                tf.Translation.X -= Math.Min(1f, secondsUsed * 4 * 1.57f);
                tf.Translation.Y -= Math.Min(0.05f, secondsUsed * 2);

                tf.Rotation.X += Math.Min(30f, secondsUsed * 350);
                tf.Rotation.Y += Math.Min(80f, secondsUsed * 350);

                byEntity.Controls.UsingHeldItemTransformAfter = tf;

                return secondsUsed <= 1.5f;
            }

            // Let the client decide when he is done eating
            return true;
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
                return;

            ItemStack contentStack = GetContent(slot.Itemstack);
            if (
                IsValidPotionUsage(
                    secondsUsed,
                    byEntity,
                    contentStack,
                    out EntityPlayer playerEntity,
                    out IServerPlayer serverPlayer
                )
            )
            {
                ProcessPotionEffects(contentStack, byEntity, playerEntity, serverPlayer);
                SplitStackAndPerformAction(
                    playerEntity,
                    slot,
                    stack => TryTakeLiquid(stack, 0.25f)?.StackSize ?? 0
                );
                slot.MarkDirty();
                playerEntity.Player.InventoryManager.BroadcastHotbarSlot();
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

        private static bool IsValidPotionUsage(
            float secondsUsed,
            EntityAgent byEntity,
            ItemStack contentStack,
            out EntityPlayer playerEntity,
            out IServerPlayer serverPlayer
        )
        {
            playerEntity = byEntity as EntityPlayer;
            serverPlayer = playerEntity?.Player as IServerPlayer;

            return secondsUsed > 1.45f
                && byEntity.World.Side == EnumAppSide.Server
                && contentStack != null
                && playerEntity != null
                && serverPlayer != null;
        }

        private void ProcessPotionEffects(
            ItemStack contentStack,
            EntityAgent byEntity,
            EntityPlayer playerEntity,
            IServerPlayer serverPlayer
        )
        {
            JsonObject potion = contentStack.ItemAttributes?["potioninfo"];
            if (potion?.Exists ?? false)
            {
                string potionId = potion["potionId"].AsString();
                bool ignoreArmour = potion["ignoreArmour"].AsBool(false);

                if (
                    !string.IsNullOrWhiteSpace(potionId)
                    && byEntity.WatchedAttributes.GetLong(potionId) == 0
                )
                {
                    switch (potionId)
                    {
                        case "nutritionpotionid":
                            UtilityEffects.ApplyNutritionPotion(byEntity);
                            break;

                        case "recallpotionid":
                            UtilityEffects.ApplyRecallPotion(serverPlayer, byEntity, api);
                            break;

                        case "temporalpotionid":
                            UtilityEffects.ApplyTemporalPotion(byEntity);
                            break;

                        default:
                            ApplyCustomPotion(contentStack, playerEntity, potionId, ignoreArmour);
                            break;
                    }

                    serverPlayer.SendMessage(
                        GlobalConstants.InfoLogChatGroup,
                        Lang.Get("alchemy:effect-gain", contentStack.GetName()),
                        EnumChatType.Notification
                    );
                }
            }
        }


        private static void ApplyCustomPotion(
            ItemStack contentStack,
            EntityPlayer playerEntity,
            string potionId,
            bool ignoreArmour
        )
        {
            TempEffect potionEffect = new();
            string strength = contentStack.Item.Variant?["strength"] ?? "none";
            int duration = contentStack.ItemAttributes?["potioninfo"]?["duration"].AsInt(0) ?? 0;
            JsonObject tickPotion = contentStack.ItemAttributes?["tickpotioninfo"];
            int tickSec = tickPotion?["ticksec"].AsInt() ?? 0;
            float health = tickPotion?["health"].AsFloat() ?? 0;

            AdjustPotionStrength(ref health, strength);

            Dictionary<string, float> effectList = GetPotionEffects(contentStack, strength);

            if (tickSec != 0)
            {
                potionEffect.TempTickEntityStats(
                    playerEntity,
                    effectList,
                    duration,
                    potionId,
                    tickSec,
                    health,
                    ignoreArmour
                );
            }
            else
            {
                potionEffect.TempEntityStats(playerEntity, effectList, duration, potionId);
            }
        }

        private static void AdjustPotionStrength(ref float health, string strength)
        {
            switch (strength)
            {
                case "strong":
                    health = MathF.Round(health * 3, 2);
                    break;

                case "medium":
                    health = MathF.Round(health * 2, 2);
                    break;
            }
        }

        private static Dictionary<string, float> GetPotionEffects(
            ItemStack contentStack,
            string strength
        )
        {
            Dictionary<string, float> effectList =
                contentStack.ItemAttributes?["effects"]?.AsObject<Dictionary<string, float>>()
                ?? new();

            switch (strength)
            {
                case "strong":
                    foreach (string effect in effectList.Keys.ToList())
                    {
                        effectList[effect] = MathF.Round(effectList[effect] * 3, 2);
                    }
                    break;

                case "medium":
                    foreach (string effect in effectList.Keys.ToList())
                    {
                        effectList[effect] = MathF.Round(effectList[effect] * 2, 2);
                    }
                    break;
            }

            return effectList;
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

    public class FlaskTextureSource : ITexPositionSource
    {
        public ItemStack forContents;

        private readonly ICoreClientAPI capi;

        private TextureAtlasPosition contentTextPos;
        private readonly TextureAtlasPosition blockTextPos;
        private readonly TextureAtlasPosition corkTextPos;
        private readonly TextureAtlasPosition bracingTextPos;
        private readonly CompositeTexture contentTexture;

        public FlaskTextureSource(
            ICoreClientAPI capi,
            ItemStack forContents,
            CompositeTexture contentTexture,
            Block flask
        )
        {
            this.capi = capi ?? throw new ArgumentNullException(nameof(capi));
            this.forContents = forContents ?? throw new ArgumentNullException(nameof(forContents));
            this.contentTexture =
                contentTexture ?? throw new ArgumentNullException(nameof(contentTexture));
            corkTextPos = capi.BlockTextureAtlas.GetPosition(flask, "topper");
            blockTextPos = capi.BlockTextureAtlas.GetPosition(flask, "glass");
            bracingTextPos = capi.BlockTextureAtlas.GetPosition(flask, "bracing");
        }

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

                            BitmapRef bmp = capi.Assets
                                .TryGet(
                                    contentTexture?.Base
                                        .Clone()
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
                                    capi.BlockTextureAtlas.InsertTexture(
                                        bmp,
                                        out id,
                                        out TextureAtlasPosition texPos
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