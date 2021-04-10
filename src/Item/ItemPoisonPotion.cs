using System;
using System.Text;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;

namespace Alchemy
{
    public class ItemPoisonPotion : Item
    {
        int tickCnt = 0;

        EntityAgent potionEntity;
        JsonObject attr;

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            /*This checks if the potion effect callback is on*/
            if (byEntity.Stats.GetBlended("poisonpotionid") == 1)
            {
                byEntity.World.RegisterCallback((dt) =>
                {
                    if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemInteract)
                    {
                        byEntity.World.PlaySoundAt(new AssetLocation("alchemy:sounds/player/drink"), byEntity);
                    }
                }, 200);

                JsonObject attr = slot.Itemstack.Collectible.Attributes;
                /*This checks that the potion has the required json attributes to continue*/
                if (attr != null && attr["poison"].Exists && attr["duration"].Exists && attr["tickSec"].Exists)
                {
                    handling = EnumHandHandling.PreventDefault;
                    return;
                }

                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            }
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
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
                attr = slot.Itemstack.Collectible.Attributes;

                potionEntity = byEntity;

                long potionListenerId = api.World.RegisterGameTickListener(onPotionTick, 1000);

                /*This saves the listenerId for registerCallback to the player's stats so I unregister it later*/
                potionEntity.Stats.Set("poisonpotionid", "potionmod", potionListenerId, false);

                Block emptyFlask = api.World.GetBlock(AssetLocation.Create(slot.Itemstack.Collectible.Attributes["drankBlockCode"].AsString(), slot.Itemstack.Collectible.Code.Domain));
                ItemStack emptyStack = new ItemStack(emptyFlask);
                /*Gives player an empty flask if last in stack or drops an empty flask at players feet*/
                if (slot.Itemstack.StackSize <= 1)
                {
                    slot.Itemstack = emptyStack;
                }
                else
                {
                    IPlayer player = (byEntity as EntityPlayer)?.Player;

                    slot.TakeOut(1);
                    if (!player.InventoryManager.TryGiveItemstack(emptyStack, true))
                    {
                        byEntity.World.SpawnItemEntity(emptyStack, byEntity.SidedPos.XYZ);
                    }
                }

                slot.MarkDirty();

                if (potionEntity is EntityPlayer)
                {
                    IServerPlayer sPlayer = (potionEntity.World.PlayerByUid((potionEntity as EntityPlayer).PlayerUID) as IServerPlayer);
                    sPlayer.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the poison potion.", EnumChatType.Notification);
                }
            }
        }

        private void onPotionTick(float dt)
        {
            tickCnt++;

            float tickSec = attr["tickSec"].AsFloat();
            /*This if statement passes every tickSec amount of seconds*/
            if (tickCnt % tickSec == 0)
            {
                float poison = attr["poison"].AsFloat();
                potionEntity.ReceiveDamage(new DamageSource()
                {
                    Source = EnumDamageSource.Internal,
                    Type = poison > 0 ? EnumDamageType.Poison : EnumDamageType.Heal
                }, Math.Abs(poison));
            }

            float duration = attr["duration"].AsFloat();
            /*This if statement passes when duration amount of seconds pass*/
            if (tickCnt >= duration)
            {
                /*This resets the potion listenerId that is attached to the player*/
                potionEntity.Stats.Set("poisonpotionid", "potionmod", 0, false);
                tickCnt = 0;

                if (potionEntity is EntityPlayer)
                {
                    IServerPlayer player = (potionEntity.World.PlayerByUid((potionEntity as EntityPlayer).PlayerUID) as IServerPlayer);
                    player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the poison potion dissipate.", EnumChatType.Notification);
                }
            }
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            JsonObject attr = inSlot.Itemstack.Collectible.Attributes;
            if (attr != null && attr["poison"].Exists && attr["duration"].Exists && attr["tickSec"].Exists)
            {
                float poison = attr["poison"].AsFloat();
                float tickSec = attr["tickSec"].AsFloat();
                float duration = attr["duration"].AsFloat();
                dsc.AppendLine(Lang.Get("When used: -{0} hp every {1} of seconds. Lasts for {2} seconds.", poison, tickSec, duration));
            }
        }


        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[] {
                new WorldInteraction()
                {
                    ActionLangCode = "heldhelp-drink",
                    MouseButton = EnumMouseButton.Right,
                }
            }.Append(base.GetHeldInteractionHelp(inSlot));
        }
    }
}