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

namespace Alchemy
{
    public class BlockPotionFlask : BlockBucket
    {
        public override float CapacityLitres => 0.5f;
        protected override string meshRefsCacheKey => "flaskMeshRefs";
        protected override AssetLocation emptyShapeLoc => new AssetLocation("shapes/block/glass/flask-liquid.json");
        protected override AssetLocation contentShapeLoc => new AssetLocation("shapes/block/glass/flask-liquid.json");

        protected override float liquidMaxYTranslate => 7 / 16f;



        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            bool val = base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack);

            if (val)
            {
                BlockEntityBucket bect = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityBucket;
                if (bect != null)
                {
                    BlockPos targetPos = blockSel.DidOffset ? blockSel.Position.AddCopy(blockSel.Face.Opposite) : blockSel.Position;
                    double dx = byPlayer.Entity.Pos.X - (targetPos.X + blockSel.HitPosition.X);
                    double dz = byPlayer.Entity.Pos.Z - (targetPos.Z + blockSel.HitPosition.Z);
                    float angleHor = (float)Math.Atan2(dx, dz);

                    float deg22dot5rad = GameMath.PIHALF / 4;
                    float roundRad = ((int)Math.Round(angleHor / deg22dot5rad)) * deg22dot5rad;
                    bect.MeshAngle = roundRad;
                }
            }

            return val;
        }

        public bool isAlchContainer = false;
        public Dictionary<string, float> dic = new Dictionary<string, float>();
        public string potionId;
        public int duration;
        public int tickSec = 0;
        public float health;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (Attributes?["alchemycontainer"].Exists == true)
            {
                try
                {
                    isAlchContainer = Attributes["alchemycontainer"].AsBool();
                }
                catch (Exception e)
                {
                    api.World.Logger.Error("Failed loading alchemy container {0}. Will ignore. Exception: {1}", Code, e);
                    isAlchContainer = false;
                }
            }
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            return;
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            ItemStack contentStack = GetContent(slot.Itemstack);
            if (contentStack != null)
            {


                string strength = Variant["strength"] is string str ? string.Intern(str) : "none";
                try
                {
                    JsonObject potion = contentStack.ItemAttributes?["potioninfo"];
                    if (potion?.Exists == true)
                    {

                        potionId = potion["potionId"].AsString();
                        duration = potion["duration"].AsInt();
                    }
                }
                catch (Exception e)
                {
                    api.World.Logger.Error("Failed loading potion effects for potion {0}. Will ignore. Exception: {1}", Code, e);
                    potionId = "";
                    duration = 0;
                }
                try
                {
                    JsonObject tickPotion = contentStack.ItemAttributes?["tickpotioninfo"];
                    if (tickPotion?.Exists == true)
                    {

                        tickSec = tickPotion["ticksec"].AsInt();
                        health = tickPotion["health"].AsFloat();
                        switch (strength)
                        {
                            case "strong":
                                health *= 3;
                                break;
                            case "medium":
                                health *= 2;
                                break;
                            default:
                                break;
                        }
                        //api.Logger.Debug("potion {0}, {1}, potionId, duration);
                    }
                }
                catch (Exception e)
                {
                    api.World.Logger.Error("Failed loading potion effects for potion {0}. Will ignore. Exception: {1}", Code, e);
                    tickSec = 0;
                    health = 0;
                }
                try
                {
                    JsonObject effects = contentStack.ItemAttributes?["effects"];
                    if (effects?.Exists == true)
                    {

                        dic = effects.AsObject<Dictionary<string, float>>();
                        switch (strength)
                        {
                            case "strong":
                                foreach (var k in dic.Keys.ToList())
                                {
                                    dic[k] *= 3;
                                }
                                break;
                            case "medium":
                                foreach (var k in dic.Keys.ToList())
                                {
                                    dic[k] *= 2;
                                }
                                break;
                            default:
                                break;
                        }
                    }
                }
                catch (Exception e)
                {
                    api.World.Logger.Error("Failed loading potion effects for potion {0}. Will ignore. Exception: {1}", Code, e);
                    dic.Clear();
                }

                //api.Logger.Debug("potion {0}, {1}", dic.Count, potionId);
                if (potionId != "")
                {
                    //api.Logger.Debug("[Potion] check if drinkable {0}", byEntity.WatchedAttributes.GetLong(potionId));
                    /* This checks if the potion effect callback is on */
                    if (byEntity.WatchedAttributes.GetLong(potionId) == 0)
                    {
                        byEntity.World.RegisterCallback((dt) => playEatSound(byEntity, "drink", 1), 500);
                        handling = EnumHandHandling.PreventDefault;
                        return;
                    }
                }
            }
            else
            {
                return;
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
            if (secondsUsed > 1.45f && byEntity.World.Side == EnumAppSide.Server)
            {
                ItemStack content = GetContent(slot.Itemstack);
                if (potionId == "recallpotionid")
                {

                }
                else if (tickSec == 0)
                {
                    TempEffect potionEffect = new TempEffect();
                    potionEffect.tempEntityStats((byEntity as EntityPlayer), dic, "potionmod", duration, potionId);
                }
                else
                {
                    TempEffect potionEffect = new TempEffect();
                    potionEffect.tempTickEntityStats((byEntity as EntityPlayer), dic, "potionmod", duration, potionId, tickSec, health);
                }
                if (byEntity is EntityPlayer)
                {
                    IServerPlayer player = (byEntity.World.PlayerByUid((byEntity as EntityPlayer).PlayerUID) as IServerPlayer);
                    if (potionId == "recallpotionid")
                    {
                        FuzzyEntityPos spawn = player.GetSpawnPosition(false);
                        byEntity.TeleportTo(spawn);
                    }
                    else
                    {
                        player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the " + content.GetName(), EnumChatType.Notification);
                    }
                }
                bool empty;
                if (content.StackSize <= 1) {
                    content = null;
                    SetContent(slot.Itemstack, content);
                    empty = slot.Empty;
                    potionId = "";
                } else {
                    content.StackSize = content.StackSize - 1;
                    SetContent(slot.Itemstack, content);
                }
                slot.MarkDirty();
                EntityPlayer entityPlayer = byEntity as EntityPlayer;
                if (entityPlayer == null)
                {
                    return;
                }
                entityPlayer.Player.InventoryManager.BroadcastHotbarSlot();
            }
        }
    }
}