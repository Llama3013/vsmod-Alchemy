using System;
using System.Text;
using Vintagestory.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Alchemy
{

    public class PotionAttrClass
    {
        public string potionid = "";
        public float accuracy = 0f;
        public float animalloot = 0f;
        public float animalharvest = 0f;
        public float animalseek = 0f;
        public float extrahealth = 0f;
        public float forage = 0f;
        public float healingeffect = 0f;
        public float hunger = 0f;
        public float melee = 0f;
        public float mechdamage = 0f;
        public float mining = 0f;
        public float ore = 0f;
        public float rangeddamage = 0f;
        public float rangedspeed = 0f;
        public float rustygear = 0f;
        public float speed = 0f;
        public float vesselcontent = 0f;
        public float wildcrop = 0f;
        public int duration = 0;
        public int ticksec = 0;
        public float health = 0f;
        public string drankBlockCode = "";
    }

    public class ItemPotion : Item
    {
        public PotionAttrClass attrClass;
        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity forEntity)
        {
            return "eat";
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            JsonObject jsonObj = Attributes;
            if (jsonObj?.Exists == true)
            {
                try
                {
                    attrClass = jsonObj.AsObject<PotionAttrClass>();
                }
                catch (Exception e)
                {
                    api.World.Logger.Error("Failed loading statModifiers for item/block {0}. Will ignore. Exception: {1}", Code, e);
                    attrClass = null;
                }
            }
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (attrClass != null && attrClass.potionid != "")
            {
                //api.Logger.Debug("[Potion] check if drinkable {0} and {1}", attrClass.potionid, byEntity.WatchedAttributes.GetLong(attrClass.potionid));
                /*This checks if the potion effect callback is on*/
                if (byEntity.WatchedAttributes.GetLong(attrClass.potionid) == 0)
                {
                    byEntity.World.RegisterCallback((dt) =>
                    {
                        if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemInteract)
                        {
                            byEntity.World.PlaySoundAt(new AssetLocation("alchemy:sounds/player/drink"), byEntity);
                        }
                    }, 200);
                    handling = EnumHandHandling.PreventDefault;
                    return;
                }
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
                PotionEffect potionEffect = new PotionEffect();
                potionEffect.PotionCheck(byEntity, slot, attrClass, api);
            }
        }


        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            JsonObject attr = inSlot.Itemstack.Collectible.Attributes;
            if (attr != null)
            {
                if (attr["accuracy"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% ranged accuracy", attr["accuracy"].AsFloat() * 100));
                }
                if (attr["animalloot"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% more animal loot", attr["animalloot"].AsFloat() * 100));
                }
                if (attr["animalharvest"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% faster animal harvest", attr["animalharvest"].AsFloat() * 100));
                }
                if (attr["animalseek"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% animal seek range", attr["animalseek"].AsFloat() * 100));
                }
                if (attr["extrahealth"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0} extra max health", attr["extrahealth"].AsFloat()));
                }
                if (attr["forage"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% more forage amount", attr["forage"].AsFloat() * 100));
                }
                if (attr["healingeffect"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% healing effectiveness", attr["healingeffect"].AsFloat() * 100));
                }
                if (attr["hunger"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0}% hunger rate", attr["hunger"].AsFloat() * 100));
                }
                if (attr["melee"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% melee damage", attr["melee"].AsFloat() * 100));
                }
                if (attr["mechdamage"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% mechanincal damage (not sure if works)", attr["mechdamage"].AsFloat() * 100));
                }
                if (attr["mining"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% mining speed", attr["mining"].AsFloat() * 100));
                }
                if (attr["ore"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% more ore", attr["ore"].AsFloat() * 100));
                }
                if (attr["rangeddamage"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% ranged damage", attr["rangeddamage"].AsFloat() * 100));
                }
                if (attr["rangedspeed"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% ranged speed", attr["rangedspeed"].AsFloat() * 100));
                }
                if (attr["rustygear"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% more gears from metal piles", attr["rustygear"].AsFloat() * 100));
                }
                if (attr["speed"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% walk speed", attr["speed"].AsFloat() * 100));
                }
                if (attr["vesselcontent"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% more vessel contents", attr["vesselcontent"].AsFloat() * 100));
                }
                if (attr["wildcrop"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: +{0}% wild crop", attr["wildcrop"].AsFloat() * 100));
                }
                if (attr["health"].Exists)
                {
                    dsc.AppendLine(Lang.Get("When potion is used: {0} health", attr["health"].AsFloat()));
                }
                if (attr["duration"].Exists)
                {
                    dsc.AppendLine(Lang.Get("and lasts for {0} seconds", attr["duration"].AsInt()));
                }
                if (attr["ticksec"].Exists)
                {
                    dsc.AppendLine(Lang.Get("every {0} seconds", attr["ticksec"].AsInt()));
                }
            }
        }


        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return new WorldInteraction[] {
                new WorldInteraction()
                {
                    /* The ActionLangCode should be heldhelp-drink but it is not working atm */
                    ActionLangCode = "Drink",
                    MouseButton = EnumMouseButton.Right
                }
            }.Append(base.GetHeldInteractionHelp(inSlot));
        }
    }
}