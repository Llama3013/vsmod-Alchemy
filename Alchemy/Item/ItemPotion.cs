using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    public class ItemPotion : Item
    {
        public override string GetHeldTpUseAnimation(ItemSlot activeHotbarSlot, Entity forEntity)
        {
            return "eat";
        }

        // This is needed as a workaround for now because there is no such OnHeldIdle override on behaviors and hopefully it will be implemented into VS eventually
        public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
        {
            base.OnHeldIdle(slot, byEntity);
            foreach (CollectibleBehavior bh in CollectibleBehaviors)
                if (bh is PotionCoatSourceBehavior coat)
                {
                    coat.CoatingIdle(slot, byEntity);
                    return;
                }
        }

        public override void OnGroundIdle(EntityItem entityItem)
        {
            if (entityItem.Itemstack.Item.MatterState == EnumMatterState.Liquid)
            {
                //If liquid use OnGroundIdle from ItemLiquidPortion code
                entityItem.Die(EnumDespawnReason.Removed);

                if (entityItem.World.Side == EnumAppSide.Server)
                {
                    WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(
                        entityItem.Itemstack
                    );
                    float litres = entityItem.Itemstack.StackSize / props.ItemsPerLitre;

                    entityItem.World.SpawnCubeParticles(
                        entityItem.Pos.XYZ,
                        entityItem.Itemstack,
                        0.75f,
                        (int)(litres * 2),
                        0.45f
                    );
                    entityItem.World.PlaySoundAt(
                        new AssetLocation("sounds/environment/smallsplash"),
                        (float)entityItem.Pos.X,
                        (float)entityItem.Pos.Y,
                        (float)entityItem.Pos.Z,
                        null
                    );
                }
            }

            base.OnGroundIdle(entityItem);
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            if (!AlchemyConfig.Loaded.AllowWeaponCoating)
                return base.GetHeldInteractionHelp(inSlot);
            return
            [
                new WorldInteraction
                {
                    ActionLangCode = "alchemy:heldhelp-coat-weapon",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCodes = ["shift"],
                },
            ];
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            JsonObject potion = Attributes?["potioninfo"];
            string potionId = potion?["potionId"].AsString();
            if (string.IsNullOrWhiteSpace(potionId))
            {
                api.Logger.Debug(
                    "{0} has no potionid, therefore it will never give effects",
                    Code.GetName()
                );
            }
        }
    }
}
