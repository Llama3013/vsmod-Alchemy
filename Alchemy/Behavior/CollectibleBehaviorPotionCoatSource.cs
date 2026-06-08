using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    public class PotionCoatSourceBehavior(CollectibleObject collObj) : CollectibleBehavior(collObj)
    {
        private string source;
        private float consumeLitres;
        private float checkLitres;
        private float consumeTime;

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            source = properties["source"].AsString("item");
            consumeLitres = properties["consumeLitres"]
                .AsFloat(AlchemyConfig.Loaded.WeaponCoatConsumeLitres);
            checkLitres = properties["checkLitres"]
                .AsFloat(AlchemyConfig.Loaded.WeaponCoatCheckLitres);
            consumeTime = properties["consumeTime"]
                .AsFloat(AlchemyConfig.Loaded.WeaponCoatApplyTime);
        }

        private static string GetLangKey(CollectibleObject col)
        {
            if (col?.Code == null)
                return "";
            string typePrefix = col is Vintagestory.API.Common.Block ? "block" : "item";
            return $"{col.Code.Domain}:{typePrefix}-{col.Code.Path}";
        }

        public void CoatingIdle(ItemSlot slot, EntityAgent byEntity)
        {
            if (source == "liquidcontent")
            {
                if (collObj is not BlockLiquidContainerBase container)
                    return;

                ItemStack contentStack = container.GetContent(slot.Itemstack);
                PotionConsumableLogic.TryReadPotionInfo(
                    contentStack,
                    out string potionId,
                    out string strength
                );

                PotionConsumableLogic.HandleWeaponCoatingIdle(
                    byEntity.Api,
                    slot,
                    byEntity,
                    potionId,
                    strength,
                    GetLangKey(contentStack?.Collectible),
                    s =>
                    {
                        if (!PotionConsumableLogic.HasEnoughSource(collObj, source, s, checkLitres))
                            return false;
                        return PotionConsumableLogic.ConsumeSource(
                            collObj,
                            source,
                            s,
                            byEntity,
                            consumeLitres
                        );
                    },
                    consumeTime
                );
            }
            else
            {
                PotionConsumableLogic.TryReadPotionInfo(
                    slot.Itemstack,
                    out string potionId,
                    out string strength
                );

                PotionConsumableLogic.HandleWeaponCoatingIdle(
                    byEntity.Api,
                    slot,
                    byEntity,
                    potionId,
                    strength,
                    GetLangKey(slot.Itemstack.Collectible),
                    s =>
                    {
                        if (!PotionConsumableLogic.HasEnoughSource(collObj, source, s, checkLitres))
                            return false;
                        return PotionConsumableLogic.ConsumeSource(
                            collObj,
                            source,
                            s,
                            byEntity,
                            consumeLitres
                        );
                    },
                    consumeTime
                );
            }
        }
    }
}
