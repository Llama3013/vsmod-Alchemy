using System;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    public static class BarrelCoating
    {
        private static bool coating;

        public static bool TryCoatInBarrel(ItemSlot itemSlot, ItemSlot liquidSlot)
        {
            if (coating)
                return false;
            if (!AlchemyConfig.Loaded.AllowWeaponCoating || !AlchemyConfig.Loaded.AllowBarrelCoating)
                return false;
            if (itemSlot?.Itemstack == null || liquidSlot?.Itemstack == null)
                return false;

            if (
                !PotionConsumableLogic.TryResolvePotion(
                    liquidSlot.Itemstack,
                    out string potionId,
                    out float potencyMul,
                    out float durationMul
                )
                || !PotionConsumableLogic.IsCoatingAllowed(potionId)
            )
                return false;

            CollectibleObject col = itemSlot.Itemstack.Collectible;
            if (col?.Code == null)
                return false;

            bool isProjectile = CoatingPolicy.IsCoatableProjectile(col);
            if (!isProjectile && !CoatingPolicy.IsCoatableWeapon(col))
                return false;

            WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(
                liquidSlot.Itemstack
            );
            if (props == null || props.ItemsPerLitre <= 0)
                return false;

            float consumeLitres = AlchemyConfig.Loaded.WeaponCoatConsumeLitres;
            float checkLitres = AlchemyConfig.Loaded.WeaponCoatCheckLitres;
            if (consumeLitres <= 0)
                return false;

            float availableLitres = liquidSlot.Itemstack.StackSize / props.ItemsPerLitre;
            float coatMultiplier = AlchemyConfig.Loaded.WeaponCoatEffectMultiplier * potencyMul;
            string itemCode = CoatedEffects.DefaultItemCode(liquidSlot.Itemstack.Collectible);

            coating = true;
            try
            {
                return isProjectile
                    ? CoatArrows(itemSlot, liquidSlot, props, availableLitres, consumeLitres, checkLitres, potionId, itemCode, coatMultiplier, durationMul)
                    : CoatWeapon(itemSlot, liquidSlot, props, availableLitres, consumeLitres, checkLitres, potionId, itemCode, coatMultiplier, durationMul);
            }
            finally
            {
                coating = false;
            }
        }

        private static bool CoatWeapon(
            ItemSlot itemSlot,
            ItemSlot liquidSlot,
            WaterTightContainableProps props,
            float availableLitres,
            float consumeLitres,
            float checkLitres,
            string potionId,
            string itemCode,
            float coatMultiplier,
            float durationMul
        )
        {
            CoatedEffects.ReadWeaponCoat(
                itemSlot.Itemstack,
                out string existingId,
                out float existingMultiplier,
                out float existingDurationMul,
                out int charges
            );

            if (
                !string.IsNullOrEmpty(existingId)
                && (
                    existingId != potionId
                    || Math.Abs(existingMultiplier - coatMultiplier) > 0.001f
                    || Math.Abs(existingDurationMul - durationMul) > 0.001f
                )
            )
                return false;

            int maxCharges = AlchemyConfig.Loaded.WeaponCoatCharges;
            if (charges >= maxCharges)
                return false;

            int chargesToAdd = 0;
            float litres = availableLitres;
            while (charges + chargesToAdd < maxCharges && litres >= checkLitres)
            {
                chargesToAdd++;
                litres -= consumeLitres;
            }

            if (chargesToAdd <= 0)
                return false;

            ConsumeLitres(liquidSlot, props, consumeLitres * chargesToAdd);
            CoatedEffects.WriteWeaponCoat(itemSlot, potionId, itemCode, coatMultiplier, durationMul, charges + chargesToAdd);
            return true;
        }

        private static bool CoatArrows(
            ItemSlot itemSlot,
            ItemSlot liquidSlot,
            WaterTightContainableProps props,
            float availableLitres,
            float consumeLitres,
            float checkLitres,
            string potionId,
            string itemCode,
            float coatMultiplier,
            float durationMul
        )
        {
            if (CoatedEffects.HasProjectileCoat(itemSlot.Itemstack))
                return false;

            int stackSize = itemSlot.Itemstack.StackSize;
            if (availableLitres < checkLitres * stackSize)
                return false;

            ConsumeLitres(liquidSlot, props, consumeLitres * stackSize);
            CoatedEffects.WriteProjectileCoat(itemSlot.Itemstack, potionId, itemCode, coatMultiplier, durationMul);
            itemSlot.MarkDirty();
            return true;
        }

        private static void ConsumeLitres(ItemSlot liquidSlot, WaterTightContainableProps props, float litres)
        {
            int itemsToRemove = Math.Max(1, (int)Math.Round(litres * props.ItemsPerLitre));
            liquidSlot.TakeOut(itemsToRemove);
            liquidSlot.MarkDirty();
        }
    }
}
