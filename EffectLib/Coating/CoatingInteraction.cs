using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace EffectLib
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// The "hold a coating source in your off hand, shift+right-click, wait" interaction that
    /// applies a coating to whatever's in the main hand. Driven from a collectible's
    /// <c>OnHeldIdle</c> - see <see cref="CollectibleBehaviorCoatSource"/> - since there is no
    /// held-interact equivalent for an off-hand item.
    /// </summary>
    public static class CoatingInteraction
    {
        private static readonly Dictionary<long, long> holdStartMs = [];
        private static readonly HashSet<long> notifiedEntities = [];

        /// <summary>
        /// Call every tick the coating source item is idle in hand. Resolves and applies the
        /// coating once the hold completes; <paramref name="consumeSource"/> is called exactly
        /// once, at that point, to consume whatever backs the coating.
        /// </summary>
        public static void HandleIdle(
            ICoreAPI api,
            ItemSlot coatSlot,
            EntityAgent byEntity,
            string effectId,
            float potencyMul,
            string itemCode,
            System.Func<ItemSlot, bool> consumeSource,
            float consumeTime
        )
        {
            if (!CoatingPolicy.AllowCoating())
                return;

            bool eligible =
                byEntity.LeftHandItemSlot == coatSlot
                && byEntity.Controls.RightMouseDown
                && byEntity.Controls.ShiftKey
                && byEntity.Controls.HandUse == EnumHandInteract.None;

            if (byEntity.World.Side == EnumAppSide.Client)
            {
                HandleClientAnimation(byEntity, effectId, eligible);
                return;
            }

            HandleServerCoating(coatSlot, byEntity, effectId, potencyMul, eligible, itemCode, consumeSource, consumeTime);
        }

        private static void HandleClientAnimation(EntityAgent byEntity, string effectId, bool eligible)
        {
            if (!eligible)
                return;

            ItemSlot mainSlot = byEntity.RightHandItemSlot;
            if (mainSlot?.Itemstack == null || string.IsNullOrEmpty(effectId) || !CoatingPolicy.IsEffectCoatable(effectId))
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
                CoatedEffects.ReadWeaponCoat(mainSlot.Itemstack, out _, out _, out int charges);
                coatable = charges < CoatingPolicy.MaxCharges();
            }

            if (coatable)
                byEntity.AnimManager?.StartAnimation("eat");
        }

        private static void HandleServerCoating(
            ItemSlot coatSlot,
            EntityAgent byEntity,
            string effectId,
            float potencyMul,
            bool eligible,
            string itemCode,
            System.Func<ItemSlot, bool> consumeSource,
            float consumeTime
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

            if (string.IsNullOrEmpty(effectId) || !CoatingPolicy.IsEffectCoatable(effectId))
            {
                if (notifiedEntities.Add(entityId) && byEntity is EntityPlayer { Player: IServerPlayer serverPlayer })
                    serverPlayer.SendMessage(
                        GlobalConstants.InfoLogChatGroup,
                        EffectLang.Get(effectId, "coating-not-allowed"),
                        EnumChatType.Notification
                    );
                holdStartMs.Remove(entityId);
                return;
            }

            if (!holdStartMs.TryGetValue(entityId, out long startMs))
            {
                holdStartMs[entityId] = Environment.TickCount64;
                return;
            }

            // Times the hold outside of any interact-hold API, since there is none for an
            // off-hand item. If they let go before the time is up, nothing happens and they can
            // try again.
            if ((Environment.TickCount64 - startMs) / 1000f < consumeTime)
                return;

            holdStartMs.Remove(entityId);

            ApplyCoating(
                coatSlot,
                mainHandSlot,
                byEntity,
                effectId,
                CoatingPolicy.EffectMultiplier() * potencyMul,
                itemCode,
                consumeSource
            );
        }

        /// <summary>
        /// Finishes a coating application: consumes the source, and writes the coating onto
        /// whatever's in the main hand (a single arrow split off the stack, or an added charge
        /// on a weapon).
        /// </summary>
        public static void ApplyCoating(
            ItemSlot coatSlot,
            ItemSlot mainHandSlot,
            EntityAgent byEntity,
            string effectId,
            float coatMultiplier,
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
                serverPlayer?.SendMessage(
                    GlobalConstants.InfoLogChatGroup,
                    EffectLang.Get(effectId, "arrow-already-coated"),
                    EnumChatType.Notification
                );
                return;
            }

            string existingId = null;
            float existingMultiplier = 0f;
            int existingCharges = 0;
            if (!isProjectile)
                CoatedEffects.ReadWeaponCoat(
                    mainHandSlot.Itemstack,
                    out existingId,
                    out existingMultiplier,
                    out existingCharges
                );

            if (!isProjectile)
            {
                if (
                    !string.IsNullOrEmpty(existingId)
                    && (existingId != effectId || Math.Abs(existingMultiplier - coatMultiplier) > 0.001f)
                )
                {
                    serverPlayer?.SendMessage(
                        GlobalConstants.InfoLogChatGroup,
                        EffectLang.Get(effectId, "coating-conflict"),
                        EnumChatType.Notification
                    );
                    return;
                }

                if (existingCharges >= CoatingPolicy.MaxCharges())
                {
                    serverPlayer?.SendMessage(
                        GlobalConstants.InfoLogChatGroup,
                        EffectLang.Get(effectId, "coating-max-charges"),
                        EnumChatType.Notification
                    );
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
                CoatedEffects.WriteProjectileCoat(coatedArrow, effectId, itemCode, coatMultiplier);
                mainHandSlot.MarkDirty();

                if (!playerEntity.TryGiveItemStack(coatedArrow))
                    byEntity.World.SpawnItemEntity(coatedArrow, byEntity.Pos.XYZ);
            }
            else
            {
                CoatedEffects.WriteWeaponCoat(
                    mainHandSlot,
                    effectId,
                    itemCode,
                    coatMultiplier,
                    existingCharges + 1
                );
            }

            playerEntity.Player.InventoryManager.BroadcastHotbarSlot();

            string msg = isProjectile
                ? EffectLang.Get(effectId, "arrow-coated", displayName)
                : EffectLang.Get(effectId, "weapon-coated", displayName, existingCharges + 1);
            serverPlayer.SendMessage(GlobalConstants.InfoLogChatGroup, msg, EnumChatType.Notification);
        }
    }
}
