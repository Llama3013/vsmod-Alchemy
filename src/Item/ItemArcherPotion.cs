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
    public class ItemArcherPotion : Item
    {
        EntityAgent potionEntity;

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            /*This checks if the potion effect callback is on*/
            if (byEntity.WatchedAttributes.GetLong("archerpotionid") == 0)
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
                if (attr != null && attr["archer"].Exists && attr["accuracy"].Exists && attr["bowspeed"].Exists && attr["duration"].Exists)
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
                potionEntity = byEntity;

                JsonObject attr = slot.Itemstack.Collectible.Attributes;
                float archer = attr["archer"].AsFloat();
                float accuracy = attr["accuracy"].AsFloat();
                float bowSpeed = attr["bowspeed"].AsFloat();
                float duration = attr["duration"].AsFloat();

                long potionListenerId = potionEntity.World.RegisterCallback(onPotionCall, (1000 * (int)duration));

                /*This saves the listenerId for registerCallback to the player's stats so I unregister it later*/
                potionEntity.WatchedAttributes.SetLong("archerpotionid", potionListenerId);

                /*These three lines adds the attribute amount to the player's stats*/
                potionEntity.Stats.Set("rangedWeaponsDamage", "potionmod", archer, false);
                potionEntity.Stats.Set("rangedWeaponsAcc", "potionmod", accuracy, false);
                potionEntity.Stats.Set("rangedWeaponsSpeed", "potionmod", bowSpeed, false);

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
                    IServerPlayer player = (potionEntity.World.PlayerByUid((potionEntity as EntityPlayer).PlayerUID) as IServerPlayer);
                    player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the archer potion.", EnumChatType.Notification);
                }
            }
        }

        private void onPotionCall(float dt)
        {
            /*These four lines reset the character back to what they were before the potion*/
            potionEntity.Stats.Set("rangedWeaponsDamage", "potionmod", 0, false);
            potionEntity.Stats.Set("rangedWeaponsAcc", "potionmod", 0, false);
            potionEntity.Stats.Set("rangedWeaponsSpeed", "potionmod", 0, false);
            potionEntity.WatchedAttributes.SetLong("archerpotionid", 0);

            if (potionEntity is EntityPlayer)
            {
                IServerPlayer player = (potionEntity.World.PlayerByUid((potionEntity as EntityPlayer).PlayerUID) as IServerPlayer);
                player.SendMessage(GlobalConstants.InfoLogChatGroup, "You feel the effects of the archer potion dissipate.", EnumChatType.Notification);
            }
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            JsonObject attr = inSlot.Itemstack.Collectible.Attributes;
            if (attr != null && attr["archer"].Exists && attr["accuracy"].Exists && attr["bowspeed"].Exists && attr["duration"].Exists)
            {
                float archer = attr["archer"].AsFloat();
                float accuracy = attr["accuracy"].AsFloat();
                float bowspeed = attr["bowspeed"].AsFloat();
                float duration = attr["duration"].AsFloat();
                dsc.AppendLine(Lang.Get("When used: +{0}% ranged damage, +{1}% ranged accuracy and +{2} ranged bow speed. Lasts for {3} seconds.", archer*100, accuracy*100, bowspeed*100, duration));
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