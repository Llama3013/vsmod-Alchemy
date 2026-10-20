using System;
using System.Collections.Generic;
using EffectLib;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    public static class CoatingInteraction
    {
        private static readonly Dictionary<long, long> holdStartMs = [];
        private static readonly HashSet<long> notifiedEntities = [];

        public static void HandleIdle(
            ItemSlot coatSlot,
            EntityAgent byEntity,
            string potionId,
            float potencyMul,
            float durationMul,
            string itemCode,
            System.Func<ItemSlot, bool> consumeSource
        )
        {
            if (!AlchemyConfig.Loaded.AllowWeaponCoating)
                return;

            bool eligible =
                byEntity.LeftHandItemSlot == coatSlot
                && byEntity.Controls.RightMouseDown
                && byEntity.Controls.ShiftKey
                && byEntity.Controls.HandUse == EnumHandInteract.None;

            if (byEntity.World.Side == EnumAppSide.Client)
            {
                HandleClientAnimation(byEntity, potionId, eligible);
                return;
            }

            HandleServerCoating(coatSlot, byEntity, potionId, potencyMul, durationMul, eligible, itemCode, consumeSource);
        }

        private static void HandleClientAnimation(EntityAgent byEntity, string potionId, bool eligible)
        {
            if (!eligible)
                return;

            ItemSlot mainSlot = byEntity.RightHandItemSlot;
            if (mainSlot?.Itemstack == null || string.IsNullOrEmpty(potionId) || !PotionConsumableLogic.IsCoatingAllowed(potionId))
                return;

            CollectibleObject col = mainSlot.Itemstack.Collectible;
            bool isProjectile = CoatingPolicy.IsCoatableProjectile(col);

            if (!isProjectile && !CoatingPolicy.IsCoatableWeapon(col))
                return;

            bool coatable;
            if (isProjectile)
            {
                coatable = !CoatedEffects.HasProjectileCoat(mainSlot.Itemstack);
            }
            else
            {
                CoatedEffects.ReadWeaponCoat(mainSlot.Itemstack, out _, out _, out _, out int charges);
                coatable = charges < AlchemyConfig.Loaded.WeaponCoatCharges;
            }

            if (coatable)
                byEntity.AnimManager?.StartAnimation("eat");
        }

        private static void HandleServerCoating(
            ItemSlot coatSlot,
            EntityAgent byEntity,
            string potionId,
            float potencyMul,
            float durationMul,
            bool eligible,
            string itemCode,
            System.Func<ItemSlot, bool> consumeSource
        )
        {
            long entityId = byEntity.EntityId;

            if (!eligible)
            {
                holdStartMs.Remove(entityId);
                notifiedEntities.Remove(entityId);
                return;
            }

            ItemSlot mainHandSlot = byEntity.RightHandItemSlot;
            if (mainHandSlot?.Itemstack == null)
            {
                holdStartMs.Remove(entityId);
                return;
            }

            CollectibleObject col = mainHandSlot.Itemstack.Collectible;
            bool isProjectile = CoatingPolicy.IsCoatableProjectile(col);

            if (!isProjectile && !CoatingPolicy.IsCoatableWeapon(col))
            {
                holdStartMs.Remove(entityId);
                return;
            }

            if (string.IsNullOrEmpty(potionId) || !PotionConsumableLogic.IsCoatingAllowed(potionId))
            {
                if (notifiedEntities.Add(entityId) && byEntity is EntityPlayer { Player: IServerPlayer serverPlayer })
                    EffectLang.Send(serverPlayer, "alchemy:coating-not-allowed");
                holdStartMs.Remove(entityId);
                return;
            }

            if (!holdStartMs.TryGetValue(entityId, out long startMs))
            {
                holdStartMs[entityId] = Environment.TickCount64;
                return;
            }

            if ((Environment.TickCount64 - startMs) / 1000f < AlchemyConfig.Loaded.WeaponCoatApplyTime)
                return;

            holdStartMs.Remove(entityId);

            ApplyCoating(
                coatSlot,
                mainHandSlot,
                byEntity,
                potionId,
                AlchemyConfig.Loaded.WeaponCoatEffectMultiplier * potencyMul,
                durationMul,
                itemCode,
                consumeSource
            );
        }

        public static void ApplyCoating(
            ItemSlot coatSlot,
            ItemSlot mainHandSlot,
            EntityAgent byEntity,
            string potionId,
            float coatMultiplier,
            float durationMul,
            string itemCode,
            System.Func<ItemSlot, bool> consumeSource
        )
        {
            if (byEntity is not EntityPlayer playerEntity)
                return;

            bool isProjectile = CoatingPolicy.IsCoatableProjectile(mainHandSlot.Itemstack.Collectible);
            IServerPlayer serverPlayer = playerEntity.Player as IServerPlayer;

            if (isProjectile && CoatedEffects.HasProjectileCoat(mainHandSlot.Itemstack))
            {
                EffectLang.Send(serverPlayer, "alchemy:arrow-already-coated");
                return;
            }

            string existingId = null;
            float existingMultiplier = 0f;
            float existingDurationMul = 1f;
            int existingCharges = 0;
            if (!isProjectile)
                CoatedEffects.ReadWeaponCoat(
                    mainHandSlot.Itemstack,
                    out existingId,
                    out existingMultiplier,
                    out existingDurationMul,
                    out existingCharges
                );

            if (!isProjectile)
            {
                if (
                    !string.IsNullOrEmpty(existingId)
                    && (
                        existingId != potionId
                        || Math.Abs(existingMultiplier - coatMultiplier) > 0.001f
                        || Math.Abs(existingDurationMul - durationMul) > 0.001f
                    )
                )
                {
                    EffectLang.Send(serverPlayer, "alchemy:coating-conflict");
                    return;
                }

                if (existingCharges >= AlchemyConfig.Loaded.WeaponCoatCharges)
                {
                    EffectLang.Send(serverPlayer, "alchemy:coating-max-charges");
                    return;
                }
            }

            string displayName = Lang.Get(itemCode);

            if (!consumeSource(coatSlot))
                return;

            coatSlot.MarkDirty();
            byEntity.World.PlaySoundAt(
                new AssetLocation("game:sounds/effect/squish1"),
                byEntity,
                null,
                true,
                18f
            );

            if (isProjectile)
            {
                ItemStack coatedArrow = mainHandSlot.TakeOut(1);
                CoatedEffects.WriteProjectileCoat(coatedArrow, potionId, itemCode, coatMultiplier, durationMul);
                mainHandSlot.MarkDirty();

                if (!playerEntity.TryGiveItemStack(coatedArrow))
                    byEntity.World.SpawnItemEntity(coatedArrow, byEntity.Pos.XYZ);
            }
            else
            {
                CoatedEffects.WriteWeaponCoat(
                    mainHandSlot,
                    potionId,
                    itemCode,
                    coatMultiplier,
                    durationMul,
                    existingCharges + 1
                );
            }

            playerEntity.Player?.InventoryManager?.BroadcastHotbarSlot();

            if (isProjectile)
                EffectLang.Send(serverPlayer, "alchemy:arrow-coated", displayName);
            else
                EffectLang.Send(serverPlayer, "alchemy:weapon-coated", displayName, existingCharges + 1);
        }
    }
}
