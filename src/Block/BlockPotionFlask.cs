using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using System.Text;

namespace Alchemy
{
    //Add perish time to potions but potion flasks have low perish rates or do not perish
    public class BlockPotionFlask : BlockLiquidContainerTopOpened
    {
        LiquidTopOpenContainerProps Props;

        protected override string meshRefsCacheKey => Code.ToShortString() + "meshRefs";
        protected override float liquidYTranslatePerLitre => liquidMaxYTranslate / CapacityLitres;
        Dictionary<string, float> maxEssenceDic;
        public Dictionary<string, float> essencesDic = new Dictionary<string, float>();
        public float duration = 0;
        public int tickSec = 0;
        public float health = 0;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (Attributes?["liquidContainerProps"].Exists == true)
            {
                Props = Attributes["liquidContainerProps"].AsObject<LiquidTopOpenContainerProps>(null, Code.Domain);
            }
            try
            {
                IAsset maxEssences = api.Assets.TryGet("alchemy:config/essences.json");
                if (maxEssences != null)
                {
                    maxEssenceDic = maxEssences.ToObject<Dictionary<string, float>>();
                }
            }
            catch (Exception e)
            {
                api.World.Logger.Error("Failed loading potion effects for potion {0}. Will ignore. Exception: {1}", Code, e);
            }
        }

        public new MeshData GenMesh(ICoreClientAPI capi, ItemStack contentStack, BlockPos forBlockPos = null)
        {
            if (this == null || Code.Path.Contains("clay")) return null;
            Shape shape = null;
            MeshData flaskmesh = null;


            if (contentStack != null)
            {
                WaterTightContainableProps props = GetContainableProps(contentStack);
                if (props == null) return null;

                FlaskTextureSource contentSource = new FlaskTextureSource(capi, contentStack, props.Texture, this);

                float level = contentStack.StackSize / props.ItemsPerLitre;
                if (Code.Path.Contains("flask-normal"))
                {
                    if (level == 0)
                    {
                        shape = capi.Assets.TryGet(emptyShapeLoc).ToObject<Shape>();
                    }
                    else if (level <= 0.25)
                    {
                        shape = capi.Assets.TryGet("alchemy:shapes/block/glass/flask-liquid-1.json").ToObject<Shape>();
                    }
                    else if (level <= 0.5)
                    {
                        shape = capi.Assets.TryGet("alchemy:shapes/block/glass/flask-liquid-2.json").ToObject<Shape>();
                    }
                    else if (level <= 0.75)
                    {
                        shape = capi.Assets.TryGet("alchemy:shapes/block/glass/flask-liquid-3.json").ToObject<Shape>();
                    }
                    else if (level > 0.75)
                    {
                        shape = capi.Assets.TryGet("alchemy:shapes/block/glass/flask-liquid.json").ToObject<Shape>();
                    }
                }
                else if (Code.Path.Contains("flask-round"))
                {
                    if (level == 0)
                    {
                        shape = capi.Assets.TryGet(emptyShapeLoc).ToObject<Shape>();
                    }
                    else if (level <= 0.5)
                    {
                        shape = capi.Assets.TryGet("alchemy:shapes/block/glass/roundflask-liquid-1.json").ToObject<Shape>();
                    }
                    else if (level > 0.5)
                    {
                        shape = capi.Assets.TryGet("alchemy:shapes/block/glass/roundflask-liquid.json").ToObject<Shape>();
                    }
                }
                else
                {
                    if (level == 0)
                    {
                        shape = capi.Assets.TryGet(emptyShapeLoc).ToObject<Shape>();
                    }
                    else if (level > 0)
                    {
                        shape = capi.Assets.TryGet("alchemy:shapes/block/glass/tubeflask-liquid.json").ToObject<Shape>();
                    }
                }

                capi.Tesselator.TesselateShape("potionflask", shape, out flaskmesh, contentSource, new Vec3f(Shape.rotateX, Shape.rotateY, Shape.rotateZ));
            }

            return flaskmesh;
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            if (Code.Path.Contains("clay")) return;
            Dictionary<string, MeshRef> meshrefs = null;

            object obj;
            if (capi.ObjectCache.TryGetValue(meshRefsCacheKey, out obj))
            {
                meshrefs = obj as Dictionary<string, MeshRef>;
            }
            else
            {
                capi.ObjectCache[meshRefsCacheKey] = meshrefs = new Dictionary<string, MeshRef>();
            }

            ItemStack contentStack = GetContent(itemstack);
            if (contentStack == null) return;

            MeshRef meshRef = null;

            if (!meshrefs.TryGetValue(contentStack.Collectible.Code.Path + Code.Path + contentStack.StackSize, out meshRef))
            {
                MeshData meshdata = GenMesh(capi, contentStack);
                if (meshdata == null) return;

                meshrefs[contentStack.Collectible.Code.Path + Code.Path + contentStack.StackSize] = meshRef = capi.Render.UploadMesh(meshdata);

            }

            renderinfo.ModelRef = meshRef;
        }

        public override void OnUnloaded(ICoreAPI api)
        {
            ICoreClientAPI capi = api as ICoreClientAPI;
            if (capi == null) return;

            object obj;
            if (capi.ObjectCache.TryGetValue(meshRefsCacheKey, out obj))
            {
                Dictionary<string, MeshRef> meshrefs = obj as Dictionary<string, MeshRef>;

                foreach (var val in meshrefs)
                {
                    val.Value.Dispose();
                }

                capi.ObjectCache.Remove(meshRefsCacheKey);
            }
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            ItemSlot hotbarSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (!hotbarSlot.Empty && hotbarSlot.Itemstack.Collectible.Attributes?.IsTrue("handleLiquidContainerInteract") == true)
            {
                EnumHandHandling handling = EnumHandHandling.NotHandled;
                hotbarSlot.Itemstack.Collectible.OnHeldInteractStart(hotbarSlot, byPlayer.Entity, blockSel, null, true, ref handling);
                if (handling == EnumHandHandling.PreventDefault || handling == EnumHandHandling.PreventDefaultAction) return true;
            }

            if (hotbarSlot.Empty || !(hotbarSlot.Itemstack.Collectible is ILiquidInterface)) return base.OnBlockInteractStart(world, byPlayer, blockSel);


            CollectibleObject obj = hotbarSlot.Itemstack.Collectible;

            bool singleTake = byPlayer.WorldData.EntityControls.Sneak;
            bool singlePut = byPlayer.WorldData.EntityControls.Sprint;

            if (obj is ILiquidSource objLso && !singleTake)
            {
                var contentStackToMove = objLso.GetContent(hotbarSlot.Itemstack);

                float litres = singlePut ? Props.TransferSizeLitres : Props.CapacityLitres;
                int moved = TryPutLiquid(blockSel.Position, contentStackToMove, litres);

                if (moved > 0)
                {
                    objLso.TryTakeContent(hotbarSlot.Itemstack, moved);
                    DoLiquidMovedEffects(byPlayer, contentStackToMove, moved, EnumLiquidDirection.Pour);
                    return true;
                }
            }

            if (obj is ILiquidSink objLsi && !singlePut)
            {
                ItemStack owncontentStack = GetContent(blockSel.Position);

                if (owncontentStack == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

                var liquidStackForParticles = owncontentStack.Clone();

                float litres = singleTake ? Props.TransferSizeLitres : Props.CapacityLitres;
                int moved;

                if (hotbarSlot.Itemstack.StackSize == 1)
                {
                    moved = objLsi.TryPutLiquid(hotbarSlot.Itemstack, owncontentStack, litres);
                }
                else
                {
                    ItemStack containerStack = hotbarSlot.Itemstack.Clone();
                    containerStack.StackSize = 1;
                    moved = objLsi.TryPutLiquid(containerStack, owncontentStack, litres);

                    if (moved > 0)
                    {
                        hotbarSlot.TakeOut(1);
                        if (!byPlayer.InventoryManager.TryGiveItemstack(containerStack, true))
                        {
                            api.World.SpawnItemEntity(containerStack, byPlayer.Entity.SidedPos.XYZ);
                        }
                    }
                }

                if (moved > 0)
                {
                    TryTakeContent(blockSel.Position, moved);
                    DoLiquidMovedEffects(byPlayer, liquidStackForParticles, moved, EnumLiquidDirection.Fill);
                    return true;
                }
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            ItemStack contentStack = GetContent(slot.Itemstack);
            if (contentStack != null)
            {
                if (contentStack.MatchesSearchText(byEntity.World, "potion"))
                {
                    try
                    {
                        essencesDic.Clear();
                        ITreeAttribute potion = contentStack.Attributes;
                        foreach (var essence in maxEssenceDic.Keys.ToList())
                        {
                            if (potion.TryGetFloat("potion" + essence) != null)
                            {
                                if (!essencesDic.ContainsKey(essence)) essencesDic.Add(essence, 0);
                                essencesDic[essence] = potion.GetFloat("potion" + essence);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        api.World.Logger.Error("Failed loading potion effects for potion {0}. Will ignore. Exception: {1}", Code, e);
                    }
                    //api.Logger.Debug("potion {0}, {1}", essencesDic.Count, potionId);
                    //api.Logger.Debug("[Potion] check if drinkable {0}", byEntity.WatchedAttributes.GetLong(potionId));
                    /* This checks if the potion effect callback is on */
                    if (essencesDic.Count > 0)
                    {
                        if (byEntity.WatchedAttributes.TryGetLong("potionid") == null || byEntity.WatchedAttributes.TryGetLong("potionid") == 0)
                        {
                            //api.Logger.Debug("potion {0}", byEntity.WatchedAttributes.GetLong(potionId));
                            byEntity.World.RegisterCallback((dt) => playEatSound(byEntity, "drink", 1), 500);
                            handling = EnumHandHandling.PreventDefault;
                        }
                    }
                    return;
                }
            }
            else
            {
                tickSec = 0;
                essencesDic.Clear();
            }
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            Vec3d pos = byEntity.Pos.AheadCopy(0.4f).XYZ.Add(byEntity.LocalEyePos);
            pos.Y -= 0.4f;

            IPlayer player = (byEntity as EntityPlayer).Player;


            if (byEntity.World is IClientWorldAccessor)
            {
                ModelTransform tf = new ModelTransform();
                tf.Origin.Set(1.1f, 0.5f, 0.5f);
                tf.EnsureDefaultValues();

                tf.Translation.X -= Math.Min(1.7f, secondsUsed * 4 * 1.8f) / FpHandTransform.ScaleXYZ.X;
                tf.Translation.Y += Math.Min(0.4f, secondsUsed * 1.8f) / FpHandTransform.ScaleXYZ.X;
                tf.Scale = 1 + Math.Min(0.5f, secondsUsed * 4 * 1.8f) / FpHandTransform.ScaleXYZ.X;
                tf.Rotation.X += Math.Min(40f, secondsUsed * 350 * 0.75f) / FpHandTransform.ScaleXYZ.X;

                if (secondsUsed > 0.5f)
                {
                    tf.Translation.Y += GameMath.Sin(30 * secondsUsed) / 10 / FpHandTransform.ScaleXYZ.Y;
                }

                byEntity.Controls.UsingHeldItemTransformBefore = tf;

                return secondsUsed <= 1.5f;
            }
            return true;
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            ItemStack content = GetContent(slot.Itemstack);
            if (secondsUsed > 1.45f && byEntity.World.Side == EnumAppSide.Server && content != null)
            {
                if (essencesDic.TryGetValue("duration", out duration))
                {
                    if (!essencesDic.ContainsKey("health"))
                    {
                        TempEffect potionEffect = new TempEffect();
                        potionEffect.tempEntityStats((byEntity as EntityPlayer), essencesDic);
                    }
                    else
                    {
                        TempEffect potionEffect = new TempEffect();
                        potionEffect.tempTickEntityStats((byEntity as EntityPlayer), essencesDic, tickSec, essencesDic["health"]);
                    }
                    if (byEntity is EntityPlayer)
                    {
                        IServerPlayer sPlayer = (byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID) as IServerPlayer);
                        if (essencesDic.ContainsKey("recall"))
                        {
                            if (api.Side.IsServer())
                            {
                                FuzzyEntityPos spawn = sPlayer.GetSpawnPosition(false);
                                byEntity.TeleportTo(spawn);
                            }
                        }
                        sPlayer.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the " + content.GetName(), EnumChatType.Notification);
                    }
                    IPlayer player = null;
                    if (byEntity is EntityPlayer) player = byEntity.World.PlayerByUid(((EntityPlayer)byEntity).PlayerUID);
                    if (slot.StackSize > 1)
                    {
                        ItemStack newStack = slot.Itemstack.Clone();
                        newStack.StackSize = slot.StackSize - 1;
                        ItemStack newContent = GetContent(newStack);
                        api.World.SpawnItemEntity(newStack, byEntity.Pos.XYZ);
                        slot.TakeOut(slot.StackSize - 1);
                    }
                    splitStackAndPerformAction(byEntity, slot, (stack) => TryTakeLiquid(stack, 0.25f)?.StackSize ?? 0);
                    slot.MarkDirty();

                    EntityPlayer entityPlayer = byEntity as EntityPlayer;
                    if (entityPlayer == null)
                    {
                        return;
                    }
                    entityPlayer.Player.InventoryManager.BroadcastHotbarSlot();
                }
            }
            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
        }

        private int splitStackAndPerformAction(Entity byEntity, ItemSlot slot, System.Func<ItemStack, int> action)
        {
            if (slot.Itemstack.StackSize == 1)
            {
                int moved = action(slot.Itemstack);

                if (moved > 0)
                {
                    int maxstacksize = slot.Itemstack.Collectible.MaxStackSize;

                    (byEntity as EntityPlayer)?.WalkInventory((pslot) =>
                    {
                        if (pslot.Empty || pslot is ItemSlotCreative || pslot.StackSize == pslot.Itemstack.Collectible.MaxStackSize) return true;
                        int mergableq = slot.Itemstack.Collectible.GetMergableQuantity(slot.Itemstack, pslot.Itemstack, EnumMergePriority.DirectMerge);
                        if (mergableq == 0) return true;

                        var selfLiqBlock = slot.Itemstack.Collectible as BlockLiquidContainerBase;
                        var invLiqBlock = pslot.Itemstack.Collectible as BlockLiquidContainerBase;

                        if ((selfLiqBlock?.GetContent(slot.Itemstack)?.StackSize ?? 0) != (invLiqBlock?.GetContent(pslot.Itemstack)?.StackSize ?? 0)) return true;

                        slot.Itemstack.StackSize += mergableq;
                        pslot.TakeOut(mergableq);

                        slot.MarkDirty();
                        pslot.MarkDirty();
                        return true;
                    });
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
                    /*if ((byEntity as EntityPlayer)?.Player.InventoryManager.TryGiveItemstack(containerStack, true) != true)
                    {*/
                        api.World.SpawnItemEntity(containerStack, byEntity.SidedPos.XYZ);
                    //}
                }

                return moved;
            }
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            ItemStack content = GetContent(inSlot.Itemstack);
            if (content != null)
            {
                content.Collectible.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
                ITreeAttribute potion = (ITreeAttribute)content.Attributes;
                if (potion != null)
                {
                    try
                    {
                        essencesDic.Clear();
                        foreach (var essence in maxEssenceDic.Keys.ToList())
                        {
                            if (potion.TryGetFloat("potion" + essence) != null)
                            {
                                if (!essencesDic.ContainsKey(essence)) essencesDic.Add(essence, 0);
                                essencesDic[essence] = potion.GetFloat("potion" + essence);
                            }
                        }

                        //api.Logger.Debug("potion {0}, {1}, {2}", potionId, duration);
                    }
                    catch (Exception e)
                    {
                        api.World.Logger.Error("Failed loading potion effects for potion {0}. Will ignore. Exception: {1}", Code, e);
                        duration = 0;
                    }
                }
                if (essencesDic != null)
                {
                    dsc.AppendLine(Lang.Get("\n"));
                    float value;
                    if (essencesDic.TryGetValue("rangedWeaponsAcc", out value))
                    {
                        dsc.AppendLine(Lang.Get("When potion is used: {0}% ranged accuracy", value * 100));
                    }
                    if (essencesDic.TryGetValue("animalLootDropRate", out value))
                    {
                        dsc.AppendLine(Lang.Get("When potion is used: {0}% more animal loot", value * 100));
                    }
                    if (essencesDic.TryGetValue("animalHarvestingTime", out value))
                    {
                        dsc.AppendLine(Lang.Get("When potion is used: {0}% faster animal harvest", value * 100));
                    }
                    if (essencesDic.TryGetValue("animalSeekingRange", out value))
                    {
                        dsc.AppendLine(Lang.Get("When potion is used: {0}% animal seek range", value * 100));
                    }
                    if (essencesDic.TryGetValue("maxhealthExtraPoints", out value))
                    {
                        dsc.AppendLine(Lang.Get("When potion is used: {0} extra max health points", value));
                    }
                    if (essencesDic.TryGetValue("forageDropRate", out value))
                    {
                        dsc.AppendLine(Lang.Get("When potion is used: {0}% more forage amount", value * 100));
                    }
                    if (essencesDic.TryGetValue("healingeffectivness", out value))
                    {
                        dsc.AppendLine(Lang.Get("When potion is used: {0}% healing effectiveness", value * 100));
                    }
                    if (essencesDic.TryGetValue("hungerrate", out value))
                    {
                        dsc.AppendLine(Lang.Get("When potion is used: {0}% hunger rate", value * 100));
                    }
                    if (essencesDic.TryGetValue("meleeWeaponsDamage", out value))
                    {
                        dsc.AppendLine(Lang.Get("When potion is used: {0}% melee damage", value * 100));
                    }
                    if (essencesDic.TryGetValue("mechanicalsDamage", out value))
                    {
                        dsc.AppendLine(Lang.Get("When potion is used: {0}% mechanincal damage (not sure if works)", value * 100));
                    }
                    if (essencesDic.TryGetValue("miningSpeedMul", out value))
                    {
                        dsc.AppendLine(Lang.Get("When potion is used: {0}% mining speed", value * 100));
                    }
                    if (essencesDic.TryGetValue("oreDropRate", out value))
                    {
                        dsc.AppendLine(Lang.Get("When potion is used: {0}% more ore", value * 100));
                    }
                    if (essencesDic.TryGetValue("rangedWeaponsDamage", out value))
                    {
                        dsc.AppendLine(Lang.Get("When potion is used: {0}% ranged damage", value * 100));
                    }
                    if (essencesDic.TryGetValue("rangedWeaponsSpeed", out value))
                    {
                        dsc.AppendLine(Lang.Get("When potion is used: {0}% ranged speed", value * 100));
                    }
                    if (essencesDic.TryGetValue("rustyGearDropRate", out value))
                    {
                        dsc.AppendLine(Lang.Get("When potion is used: {0}% more gears from metal piles", value * 100));
                    }
                    if (essencesDic.TryGetValue("walkspeed", out value))
                    {
                        dsc.AppendLine(Lang.Get("When potion is used: {0}% walk speed", value * 100));
                    }
                    if (essencesDic.TryGetValue("vesselContentsDropRate", out value))
                    {
                        dsc.AppendLine(Lang.Get("When potion is used: {0}% more vessel contents", value * 100));
                    }
                    if (essencesDic.TryGetValue("wildCropDropRate", out value))
                    {
                        dsc.AppendLine(Lang.Get("When potion is used: {0}% wild crop", value * 100));
                    }
                    if (essencesDic.TryGetValue("wholeVesselLootChance", out value))
                    {
                        dsc.AppendLine(Lang.Get("When potion is used: {0}% chance to get whole vessel", value * 100));
                    }
                    if (essencesDic.TryGetValue("glow", out value))
                    {
                        dsc.AppendLine(Lang.Get("When potion is used: player starts to glow"));
                    }
                    if (essencesDic.TryGetValue("recall", out value))
                    {
                        dsc.AppendLine(Lang.Get("When potion is used: player teleports home"));
                    }
                    if (essencesDic.TryGetValue("duration", out value))
                    {
                        dsc.Append(Lang.Get(" and lasts for {0} seconds", value));
                    }
                    if (essencesDic.TryGetValue("health", out value))
                    {
                        dsc.Append(Lang.Get(" and lasts for {0} seconds", value));
                    }
                }
            }
        }
    }

    public class FlaskTextureSource : ITexPositionSource
    {
        public ItemStack forContents;
        private ICoreClientAPI capi;

        TextureAtlasPosition contentTextPos;
        TextureAtlasPosition blockTextPos;
        TextureAtlasPosition corkTextPos;
        TextureAtlasPosition bracingTextPos;
        CompositeTexture contentTexture;

        public FlaskTextureSource(ICoreClientAPI capi, ItemStack forContents, CompositeTexture contentTexture, Block flask)
        {
            this.capi = capi;
            this.forContents = forContents;
            this.contentTexture = contentTexture;
            this.corkTextPos = capi.BlockTextureAtlas.GetPosition(flask, "topper");
            this.blockTextPos = capi.BlockTextureAtlas.GetPosition(flask, "glass");
            this.bracingTextPos = capi.BlockTextureAtlas.GetPosition(flask, "bracing");
        }

        public TextureAtlasPosition this[string textureCode]
        {
            get
            {
                if (textureCode == "topper" && corkTextPos != null) return corkTextPos;
                if (textureCode == "glass" && blockTextPos != null) return blockTextPos;
                if (textureCode == "bracing" && bracingTextPos != null) return bracingTextPos;
                if (contentTextPos == null)
                {
                    int textureSubId;

                    textureSubId = ObjectCacheUtil.GetOrCreate<int>(capi, "contenttexture-" + contentTexture.ToString(), () =>
                    {
                        TextureAtlasPosition texPos;
                        int id = 0;

                        BitmapRef bmp = capi.Assets.TryGet(contentTexture.Base.Clone().WithPathPrefixOnce("textures/").WithPathAppendixOnce(".png"))?.ToBitmap(capi);
                        if (bmp != null)
                        {
                            capi.BlockTextureAtlas.InsertTexture(bmp, out id, out texPos);
                            bmp.Dispose();
                        }

                        return id;
                    });

                    contentTextPos = capi.BlockTextureAtlas.Positions[textureSubId];
                }

                return contentTextPos;
            }
        }

        public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
    }
}