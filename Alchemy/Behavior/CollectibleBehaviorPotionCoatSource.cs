using Alchemy.Utility;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace Alchemy.Behavior
{
    public class PotionCoatSourceBehavior(CollectibleObject collObj) : CollectibleBehavior(collObj)
    {
        private string source;
        private float consumeLitres;
        private float consumeTime;

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            source = properties["source"].AsString("item");
            consumeLitres = properties["consumeLitres"].AsFloat(0.25f);
            consumeTime = properties["consumeTime"]
                .AsFloat(PotionConsumableLogic.CoatHoldDurationSec);
        }

        public void CoatingIdle(ItemSlot slot, EntityAgent byEntity)
        {
            if (source == "liquidcontent")
            {
                if (collObj is not BlockLiquidContainerBase container)
                    return;

                ItemStack contentStack = container.GetContent(slot.Itemstack);
                JsonObject potion = contentStack?.ItemAttributes?["potioninfo"];
                string potionId = potion?.Exists == true ? potion["potionId"].AsString() : null;
                string strength = "weak";
                contentStack?.Collectible?.Variant?.TryGetValue("strength", out strength);

                PotionConsumableLogic.HandleWeaponCoatingIdle(
                    byEntity.Api,
                    slot,
                    byEntity,
                    potionId,
                    strength,
                    contentStack?.Collectible?.Code?.Path ?? "",
                    s =>
                    {
                        int consumed = container.SplitStackAndPerformAction(
                            byEntity as EntityPlayer,
                            s,
                            stack => container.TryTakeLiquid(stack, consumeLitres)?.StackSize ?? 0
                        );
                        s.MarkDirty();
                        return consumed > 0;
                    },
                    consumeTime
                );
            }
            else
            {
                JsonObject potion = slot.Itemstack.ItemAttributes?["potioninfo"];
                string potionId = potion?.Exists == true ? potion["potionId"].AsString() : null;
                string strength = "weak";
                slot.Itemstack.Collectible?.Variant?.TryGetValue("strength", out strength);

                PotionConsumableLogic.HandleWeaponCoatingIdle(
                    byEntity.Api,
                    slot,
                    byEntity,
                    potionId,
                    strength,
                    slot.Itemstack.Collectible?.Code?.Path ?? "",
                    s =>
                    {
                        s.TakeOut(1);
                        s.MarkDirty();
                        return true;
                    },
                    consumeTime
                );
            }
        }
    }
}
