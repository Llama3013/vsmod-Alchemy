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
    //Add perish time to potions but potion flasks have low perish rates or do not perish
    public class BlockPotionFlask : BlockLiquidContainerTopOpened
    {
        public Dictionary<string, float> effectList = new();
        public string potionId = "";
        public int duration = 0;
        public int tickSec = 0;
        public float health = 0;

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

        public MeshData GenMesh(ICoreClientAPI capi, ItemStack contentStack)
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
                try
                {
                    JsonObject potion = contentStack.ItemAttributes["potioninfo"];
                    if (potion.Exists == true)
                    {
                        string strength = contentStack.Item.Variant["strength"] is string str
                            ? string.Intern(str)
                            : "none";
                        potionId = potion["potionId"].AsString();
                        duration = potion["duration"].AsInt();
                        try
                        {
                            JsonObject tickPotion = contentStack.ItemAttributes["tickpotioninfo"];
                            if (tickPotion.Exists == true)
                            {
                                tickSec = tickPotion["ticksec"].AsInt();
                                health = tickPotion["health"].AsFloat();
                                switch (strength)
                                {
                                    case "strong":
                                        health = MathF.Round(health * 3, 2);
                                        break;

                                    case "medium":
                                        health = MathF.Round(health * 2, 2);
                                        break;

                                    default:
                                        break;
                                }
                                //api.Logger.Debug("potion {0}, {1}, potionId, duration);
                            }
                            else
                            {
                                tickSec = 0;
                                health = 0;
                            }
                        }
                        catch (Exception e)
                        {
                            api.World.Logger.Error(
                                "Failed loading potion effects for potion {0}. Will ignore. Exception: {1}",
                                Code,
                                e
                            );
                            tickSec = 0;
                            health = 0;
                        }
                        try
                        {
                            JsonObject effects = contentStack.ItemAttributes["effects"];
                            if (effects.Exists == true)
                            {
                                effectList = effects.AsObject<Dictionary<string, float>>();
                                switch (strength)
                                {
                                    case "strong":
                                        foreach (string effect in effectList.Keys.ToList())
                                        {
                                            effectList[effect] = MathF.Round(
                                                effectList[effect] * 3,
                                                2
                                            );
                                        }
                                        break;

                                    case "medium":
                                        foreach (string effect in effectList.Keys.ToList())
                                        {
                                            effectList[effect] *= MathF.Round(
                                                effectList[effect] * 2,
                                                2
                                            );
                                        }
                                        break;

                                    default:
                                        break;
                                }
                            }
                            else
                            {
                                effectList.Clear();
                            }
                        }
                        catch (Exception e)
                        {
                            api.World.Logger.Error(
                                "Failed loading potion effects for potion {0}. Will ignore. Exception: {1}",
                                Code,
                                e
                            );
                            effectList.Clear();
                        }
                    }
                }
                catch (Exception e)
                {
                    api.World.Logger.Error(
                        "Failed loading potion info for potion {0}. Will ignore. Exception: {1}",
                        Code,
                        e
                    );
                    potionId = "";
                    duration = 0;
                }

                if (potionId != "" && potionId != null)
                {
                    //api.Logger.Debug("potion {0}, {1}", effectList.Count, potionId);
                    //api.Logger.Debug("[Potion] check if drinkable {0}", byEntity.WatchedAttributes.GetLong(potionId));
                    /* This checks if the potion effect callback is on */
                    if (byEntity.WatchedAttributes.GetLong(potionId) == 0)
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
                    return;
            }

            if (preventDefault)
                return;

            ItemStack contentStack = GetContent(slot.Itemstack);
            if (
                secondsUsed > 1.45f
                && byEntity.World.Side == EnumAppSide.Server
                && contentStack != null
            )
            {
                if (potionId != "" && byEntity.WatchedAttributes.GetLong(potionId) == 0)
                {
                    TempEffect potionEffect = new();
                    if ((byEntity as EntityPlayer).Player is IServerPlayer sPlayer)
                    {
                        if (potionId == "recallpotionid")
                        {
                            if (api.Side.IsServer())
                            {
                                FuzzyEntityPos spawn = sPlayer.GetSpawnPosition(false);
                                byEntity.TeleportTo(spawn);
                            }
                        }
                        else if (potionId == "nutritionpotionid")
                        {
                            ITreeAttribute hungerTree = byEntity.WatchedAttributes.GetTreeAttribute(
                                "hunger"
                            );
                            if (hungerTree != null)
                            {
                                float totalSatiety =
                                    (
                                        hungerTree.GetFloat("fruitLevel")
                                        + hungerTree.GetFloat("vegetableLevel")
                                        + hungerTree.GetFloat("grainLevel")
                                        + hungerTree.GetFloat("proteinLevel")
                                        + hungerTree.GetFloat("dairyLevel")
                                    ) * 0.9f;
                                hungerTree.SetFloat("fruitLevel", Math.Max(totalSatiety / 5, 0));
                                hungerTree.SetFloat(
                                    "vegetableLevel",
                                    Math.Max(totalSatiety / 5, 0)
                                );
                                hungerTree.SetFloat("grainLevel", Math.Max(totalSatiety / 5, 0));
                                hungerTree.SetFloat("proteinLevel", Math.Max(totalSatiety / 5, 0));
                                hungerTree.SetFloat("dairyLevel", Math.Max(totalSatiety / 5, 0));
                                byEntity.WatchedAttributes.MarkPathDirty("hunger");
                            }
                        }
                        else if (potionId == "temporalpotionid")
                        {
                            byEntity
                                .GetBehavior<EntityBehaviorTemporalStabilityAffected>()
                                .OwnStability += 0.2;
                        }
                        else if (tickSec != 0)
                        {
                            potionEffect.TempTickEntityStats(
                                byEntity as EntityPlayer,
                                effectList,
                                duration,
                                potionId,
                                tickSec,
                                health
                            );
                        }
                        else
                        {
                            potionEffect.TempEntityStats(
                                byEntity as EntityPlayer,
                                effectList,
                                duration,
                                potionId
                            );
                        }
                        sPlayer.SendMessage(
                            GlobalConstants.InfoLogChatGroup,
                            "You feel the effects of the " + contentStack.GetName(),
                            EnumChatType.Notification
                        );
                    }

                    SplitStackAndPerformAction(
                        byEntity,
                        slot,
                        (stack) => TryTakeLiquid(stack, 0.25f)?.StackSize ?? 0
                    );
                    slot.MarkDirty();

                    if (byEntity is not EntityPlayer entityPlayer)
                    {
                        potionId = "";
                        duration = 0;
                        tickSec = 0;
                        health = 0;
                        effectList.Clear();
                        return;
                    }
                    entityPlayer.Player.InventoryManager.BroadcastHotbarSlot();
                }
            }
            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
        }

        #endregion Interaction

        private int SplitStackAndPerformAction(
            Entity byEntity,
            ItemSlot slot,
            System.Func<ItemStack, int> action
        )
        {
            if (slot.Itemstack.StackSize == 1)
            {
                int moved = action(slot.Itemstack);

                if (moved > 0)
                {
                    int maxstacksize = slot.Itemstack.Collectible.MaxStackSize;

                    (byEntity as EntityPlayer)?.WalkInventory(
                        (pslot) =>
                        {
                            if (
                                pslot.Empty
                                || pslot is ItemSlotCreative
                                || pslot.StackSize == pslot.Itemstack.Collectible.MaxStackSize
                            )
                                return true;
                            int mergableq = slot.Itemstack.Collectible.GetMergableQuantity(
                                slot.Itemstack,
                                pslot.Itemstack,
                                EnumMergePriority.DirectMerge
                            );
                            if (mergableq == 0)
                                return true;

                            var selfLiqBlock =
                                slot.Itemstack.Collectible as BlockLiquidContainerBase;
                            var invLiqBlock =
                                pslot.Itemstack.Collectible as BlockLiquidContainerBase;

                            if (
                                (selfLiqBlock?.GetContent(slot.Itemstack)?.StackSize ?? 0)
                                != (invLiqBlock?.GetContent(pslot.Itemstack)?.StackSize ?? 0)
                            )
                                return true;

                            slot.Itemstack.StackSize += mergableq;
                            pslot.TakeOut(mergableq);

                            slot.MarkDirty();
                            pslot.MarkDirty();
                            return true;
                        }
                    );
                }

                return moved;
            }
            else
            {
                ItemStack containerStack = slot.Itemstack.Clone();
                containerStack.StackSize = 1;

                int moved = action(containerStack);

                if (moved > 0)
                {
                    slot.TakeOut(1);
                    if (
                        (byEntity as EntityPlayer)?.Player.InventoryManager.TryGiveItemstack(
                            containerStack,
                            true
                        ) != true
                    )
                    {
                        api.World.SpawnItemEntity(containerStack, byEntity.SidedPos.XYZ);
                    }

                    slot.MarkDirty();
                }

                return moved;
            }
        }

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
            this.capi = capi;
            this.forContents = forContents;
            this.contentTexture = contentTexture;
            corkTextPos = capi.BlockTextureAtlas.GetPosition(flask, "topper", true);
            blockTextPos = capi.BlockTextureAtlas.GetPosition(flask, "glass", true);
            bracingTextPos = capi.BlockTextureAtlas.GetPosition(flask, "bracing", true);
        }

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                if (textureCode == "topper" && corkTextPos != null)
                    return corkTextPos;
                if (textureCode == "glass" && blockTextPos != null)
                    return blockTextPos;
                if (textureCode == "bracing" && bracingTextPos != null)
                    return bracingTextPos;
                if (contentTextPos == null)
                {
                    int textureSubId = ObjectCacheUtil.GetOrCreate(
                        capi,
                        "contenttexture-" + contentTexture?.ToString() ?? "unkown",
                        () =>
                        {
                            int id = 0;

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
                                catch
                                {
                                    capi.World.Logger.Error("Error on insert texture");
                                }
                                bmp.Dispose();
                            }

                            return id;
                        }
                    );

                    contentTextPos = capi.BlockTextureAtlas.Positions[textureSubId];
                }

                return contentTextPos;
            }
        }

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
    }
}