using System;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

#pragma warning disable IDE0130
namespace EffectLib
#pragma warning restore IDE0130
{
    public static class BarrelCoating
    {
        private static bool coating;

        public static bool TryCoatInBarrel(ItemSlot itemSlot, ItemSlot liquidSlot)
        {
            if (coating)
                return false;
            if (!CoatingPolicy.AllowCoating() || !CoatingPolicy.AllowBarrelCoating())
                return false;
            if (itemSlot?.Itemstack == null || liquidSlot?.Itemstack == null)
                return false;

            (string effectId, float potencyMul)? resolved = CoatingPolicy.ResolveLiquidEffect(
                liquidSlot.Itemstack
            );
            if (resolved == null)
                return false;
            (string effectId, float potencyMul) = resolved.Value;
            if (string.IsNullOrEmpty(effectId) || !CoatingPolicy.IsEffectCoatable(effectId))
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

            float consumeLitres = CoatingPolicy.BarrelConsumeLitres();
            float checkLitres = CoatingPolicy.BarrelCheckLitres();
            if (consumeLitres <= 0)
                return false;

            float availableLitres = liquidSlot.Itemstack.StackSize / props.ItemsPerLitre;
            float coatMultiplier = CoatingPolicy.EffectMultiplier() * potencyMul;
            string itemCode = CoatedEffects.DefaultItemCode(liquidSlot.Itemstack.Collectible);

            coating = true;
            try
            {
                return isProjectile
                    ? CoatArrows(itemSlot, liquidSlot, props, availableLitres, consumeLitres, checkLitres, effectId, itemCode, coatMultiplier)
                    : CoatWeapon(itemSlot, liquidSlot, props, availableLitres, consumeLitres, checkLitres, effectId, itemCode, coatMultiplier);
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
            string effectId,
            string itemCode,
            float coatMultiplier
        )
        {
            CoatedEffects.ReadWeaponCoat(itemSlot.Itemstack, out string existingId, out float existingMultiplier, out int charges);

            if (
                !string.IsNullOrEmpty(existingId)
                && (existingId != effectId || Math.Abs(existingMultiplier - coatMultiplier) > 0.001f)
            )
                return false;

            int maxCharges = CoatingPolicy.MaxCharges();
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
            CoatedEffects.WriteWeaponCoat(itemSlot, effectId, itemCode, coatMultiplier, charges + chargesToAdd);
            return true;
        }

        private static bool CoatArrows(
            ItemSlot itemSlot,
            ItemSlot liquidSlot,
            WaterTightContainableProps props,
            float availableLitres,
            float consumeLitres,
            float checkLitres,
            string effectId,
            string itemCode,
            float coatMultiplier
        )
        {
            if (CoatedEffects.HasProjectileCoat(itemSlot.Itemstack))
                return false;

            int stackSize = itemSlot.Itemstack.StackSize;
            if (availableLitres < checkLitres * stackSize)
                return false;

            ConsumeLitres(liquidSlot, props, consumeLitres * stackSize);
            CoatedEffects.WriteProjectileCoat(itemSlot.Itemstack, effectId, itemCode, coatMultiplier);
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
