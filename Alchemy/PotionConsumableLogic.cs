using System;
using System.Collections.Generic;
using Alchemy.Behavior;
using Alchemy.ModConfig;
using Alchemy.Systems;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Alchemy.Utility
{
    public static class PotionConsumableLogic
    {
        private static readonly Dictionary<long, long> coatHoldStartMs = [];

        public const float CoatHoldDurationSec = 1.5f;
        public const float DefaultConsumeTime = 1.5f;

        internal static bool IsCoatingAllowed(string potionId)
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;
            return potionId switch
            {
                "archerpotionid" => cfg.AllowCoatingArcher,
                "healingeffectpotionid" => cfg.AllowCoatingHealingEffect,
                "hungerenhancepotionid" => cfg.AllowCoatingHungerEnhance,
                "hungersupresspotionid" => cfg.AllowCoatingHungerSupress,
                "hunterpotionid" => cfg.AllowCoatingHunter,
                "looterpotionid" => cfg.AllowCoatingLooter,
                "meleepotionid" => cfg.AllowCoatingMelee,
                "miningpotionid" => cfg.AllowCoatingMining,
                "poisontickpotionid" => cfg.AllowCoatingPoison,
                "predatorpotionid" => cfg.AllowCoatingPredator,
                "regentickpotionid" => cfg.AllowCoatingRegen,
                "scentmaskpotionid" => cfg.AllowCoatingScentMask,
                "speedpotionid" => cfg.AllowCoatingSpeed,
                "vitalitypotionid" => cfg.AllowCoatingVitality,
                "recallpotionid" => cfg.AllowCoatingRecall,
                "glowpotionid" => cfg.AllowCoatingGlow,
                "waterbreathepotionid" => cfg.AllowCoatingWaterBreathe,
                "nutritionpotionid" => cfg.AllowCoatingNutrition,
                "temporalpotionid" => cfg.AllowCoatingTemporal,
                "reshapepotionid" => cfg.AllowCoatingReshape,
                "growpotionid" => cfg.AllowCoatingGrow,
                "shrinkpotionid" => cfg.AllowCoatingShrink,
                _ => false,
            };
        }

        public static bool HandleWeaponCoatingIdle(
            ICoreAPI api,
            ItemSlot coatSlot,
            EntityAgent byEntity,
            string potionId,
            string strength,
            string itemCodePath,
            System.Func<ItemSlot, bool> consumeCoating,
            float consumeTime = CoatHoldDurationSec
        )
        {
            if (!AlchemyConfig.Loaded.AllowWeaponCoating)
                return false;

            bool eligible =
                byEntity.LeftHandItemSlot == coatSlot
                && byEntity.Controls.RightMouseDown
                && byEntity.Controls.ShiftKey
                && byEntity.Controls.HandUse == EnumHandInteract.None;

            if (byEntity.World.Side == EnumAppSide.Client)
            {
                HandleClientAnimation(api, byEntity, potionId, eligible);
                return false;
            }

            return HandleServerCoating(
                api,
                coatSlot,
                byEntity,
                potionId,
                strength,
                eligible,
                itemCodePath,
                consumeCoating,
                consumeTime
            );
        }

        private static void HandleClientAnimation(
            ICoreAPI api,
            EntityAgent byEntity,
            string potionId,
            bool eligible
        )
        {
            if (!eligible)
                return;

            ItemSlot mainSlot = byEntity.RightHandItemSlot;

            if (
                mainSlot?.Itemstack == null
                || string.IsNullOrEmpty(potionId)
                || !IsCoatingAllowed(potionId)
            )
                return;

            CollectibleObject col = mainSlot.Itemstack.Collectible;

            bool isArrow = col.Code.Path.Contains("arrow");

            if (!isArrow && !HasWeaponTag(api, col))
                return;

            bool coatable = isArrow
                ? string.IsNullOrEmpty(mainSlot.Itemstack.Attributes.GetString("coatedPotionId"))
                : mainSlot.Itemstack.Attributes.GetInt("coatCharges")
                    < AlchemyConfig.Loaded.WeaponCoatCharges;

            if (coatable)
            {
                byEntity.AnimManager?.StartAnimation("eat");
            }
        }

        private static bool HandleServerCoating(
            ICoreAPI api,
            ItemSlot coatSlot,
            EntityAgent byEntity,
            string potionId,
            string strength,
            bool eligible,
            string itemCodePath,
            System.Func<ItemSlot, bool> consumeCoating,
            float consumeTime
        )
        {
            long entityId = byEntity.EntityId;

            if (!eligible)
            {
                coatHoldStartMs.Remove(entityId);
                return false;
            }

            ItemSlot mainHandSlot = byEntity.RightHandItemSlot;

            if (mainHandSlot?.Itemstack == null)
            {
                coatHoldStartMs.Remove(entityId);
                return false;
            }

            CollectibleObject col = mainHandSlot.Itemstack.Collectible;

            bool isArrow = col.Code.Path.Contains("arrow");

            if (!isArrow && !HasWeaponTag(api, col))
            {
                coatHoldStartMs.Remove(entityId);
                return false;
            }

            if (string.IsNullOrEmpty(potionId) || !IsCoatingAllowed(potionId))
            {
                coatHoldStartMs.Remove(entityId);
                return false;
            }

            if (!coatHoldStartMs.TryGetValue(entityId, out long startMs))
            {
                coatHoldStartMs[entityId] = Environment.TickCount64;
                return false;
            }

            // I couldn't find a better way to handle the timing of the coating action while using OnHeldIdle and couldn't derive an action on the offhand for OnHeldInteract, so this is a bit jank but it works. Basically I check if the player has been holding the coating for long enough, and if so I apply the coating and consume the potion. If they stop holding before the time is up then nothing happens and they can try again.
            if ((Environment.TickCount64 - startMs) / 1000f < consumeTime)
                return false;

            coatHoldStartMs.Remove(entityId);

            float strengthMul = GetStrengthMultiplier(strength);

            ApplyCoating(
                coatSlot,
                mainHandSlot,
                byEntity,
                potionId,
                AlchemyConfig.Loaded.WeaponCoatEffectMultiplier * strengthMul,
                itemCodePath,
                consumeCoating
            );

            return true;
        }

        public static float GetStrengthMultiplier(string strength)
        {
            return strength switch
            {
                "strong" => AlchemyConfig.Loaded.StrongPotionMultiplier,
                "medium" => AlchemyConfig.Loaded.MediumPotionMultiplier,
                _ => AlchemyConfig.Loaded.WeakPotionMultiplier,
            };
        }

        private static bool HasWeaponTag(ICoreAPI api, CollectibleObject col)
        {
            api.CollectibleTagRegistry.TryCreateTagSet(
                out TagSet tagSet,
                new List<string> { "weapon-melee" }
            );

            return col.Tags.Overlaps(tagSet);
        }

        private static void ApplyCoating(
            ItemSlot coatSlot,
            ItemSlot mainHandSlot,
            EntityAgent byEntity,
            string potionId,
            float coatMultiplier,
            string itemCodePath,
            System.Func<ItemSlot, bool> consumeCoating
        )
        {
            if (byEntity is not EntityPlayer playerEntity)
                return;

            bool isArrow = mainHandSlot.Itemstack.Collectible.Code.Path.Contains("arrow");

            if (
                isArrow
                && !string.IsNullOrEmpty(
                    mainHandSlot.Itemstack.Attributes.GetString("coatedPotionId")
                )
            )
                return;

            if (
                !isArrow
                && mainHandSlot.Itemstack.Attributes.GetInt("coatCharges")
                    >= AlchemyConfig.Loaded.WeaponCoatCharges
            )
                return;

            if (!isArrow)
            {
                string existingId = mainHandSlot.Itemstack.Attributes.GetString("coatedPotionId");
                if (
                    !string.IsNullOrEmpty(existingId)
                    && (
                        existingId != potionId
                        || Math.Abs(
                            mainHandSlot.Itemstack.Attributes.GetFloat("coatMultiplier")
                                - coatMultiplier
                        ) > 0.001f
                    )
                )
                    return;
            }

            string displayName = Lang.Get($"alchemy:item-{itemCodePath}");

            int consumed = consumeCoating(coatSlot) ? 1 : 0;

            if (consumed == 0)
                return;

            coatSlot.MarkDirty();
            byEntity.World.PlaySoundAt(
                new AssetLocation("game:sounds/effect/squish1"),
                byEntity,
                null,
                true,
                18f
            );

            if (isArrow)
            {
                ItemStack coatedArrow = mainHandSlot.TakeOut(1);
                coatedArrow.Attributes.SetString("coatedPotionId", potionId);
                coatedArrow.Attributes.SetString("coatedDisplayName", displayName);
                coatedArrow.Attributes.SetFloat("coatMultiplier", coatMultiplier);
                mainHandSlot.MarkDirty();

                if (!playerEntity.TryGiveItemStack(coatedArrow))
                    byEntity.World.SpawnItemEntity(coatedArrow, byEntity.Pos.XYZ);
            }
            else
            {
                ITreeAttribute attrs = mainHandSlot.Itemstack.Attributes;
                attrs.SetString("coatedPotionId", potionId);
                attrs.SetString("coatedDisplayName", displayName);
                attrs.SetFloat("coatMultiplier", coatMultiplier);
                attrs.SetInt("coatCharges", attrs.GetInt("coatCharges") + 1);
                mainHandSlot.MarkDirty();
            }

            playerEntity.Player.InventoryManager.BroadcastHotbarSlot();

            if (playerEntity.Player is IServerPlayer serverPlayer)
            {
                string msg = isArrow
                    ? Lang.Get("alchemy:arrow-coated", displayName)
                    : Lang.Get(
                        "alchemy:weapon-coated",
                        displayName,
                        mainHandSlot.Itemstack?.Attributes.GetInt("coatCharges") ?? 0
                    );
                serverPlayer.SendMessage(
                    GlobalConstants.InfoLogChatGroup,
                    msg,
                    EnumChatType.Notification
                );
            }
        }

        public static bool HandleDrinkStart(
            EntityAgent byEntity,
            string potionId,
            string animation,
            Action playSound,
            ref EnumHandHandling handling
        )
        {
            if (
                string.IsNullOrWhiteSpace(potionId)
                || byEntity.WatchedAttributes.GetLong(potionId) != 0
            )
            {
                return false;
            }

            byEntity.World.RegisterCallback(
                dt =>
                {
                    if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemInteract)
                    {
                        playSound?.Invoke();
                    }
                },
                200
            );

            byEntity.AnimManager?.StartAnimation(animation);

            handling = EnumHandHandling.PreventDefault;

            return true;
        }

        public static bool HandleDrinkStep(
            float secondsUsed,
            ItemSlot slot,
            EntityAgent byEntity,
            bool spawnParticles,
            float consumeTime = DefaultConsumeTime
        )
        {
            if (spawnParticles && secondsUsed > 0.5f && (int)(30 * secondsUsed) % 7 == 1)
            {
                Vec3d pos = byEntity.Pos.AheadCopy(0.4f).XYZ.Add(byEntity.LocalEyePos);

                pos.Y -= 0.4f;

                byEntity.World.SpawnCubeParticles(
                    pos,
                    slot.Itemstack,
                    0.3f,
                    4,
                    0.5f,
                    (byEntity as EntityPlayer)?.Player
                );
            }

            return secondsUsed <= consumeTime;
        }

        public static bool HandleDrinkStop(
            float secondsUsed,
            EntityAgent byEntity,
            PotionData data,
            Func<bool> consumeAction,
            ICoreAPI api,
            float consumeTime = DefaultConsumeTime
        )
        {
            if (secondsUsed <= consumeTime - 0.05f)
                return false;

            if (!TryProcessPotionEffects(byEntity, data, api))
            {
                return false;
            }

            return consumeAction();
        }

        public static bool TryProcessPotionEffects(
            EntityAgent byEntity,
            PotionData data,
            ICoreAPI api
        )
        {
            if (byEntity.World.Side != EnumAppSide.Server)
                return false;

            if (byEntity is not EntityPlayer playerEntity)
                return false;

            if (playerEntity.Player is not IServerPlayer serverPlayer)
                return false;

            if (string.IsNullOrWhiteSpace(data.PotionId))
                return false;

            EntityBehaviorPotionEffect behavior =
                playerEntity.GetBehavior<EntityBehaviorPotionEffect>();

            if (behavior == null)
                return false;

            float strengthMul = GetStrengthMultiplier(data.Strength);

            PotionContext ctx = PotionRegistry.BuildPotionDef(data.PotionId, strengthMul);

            if (ctx == null)
            {
                api.Logger.Error("No potion definition for potionId of: {0}", data.PotionId);

                return false;
            }

            if (!behavior.Manager.TryApplyPotion(data.PotionId, ctx, data.DisplayName))
            {
                return false;
            }

            serverPlayer.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                Lang.Get("alchemy:effect-gain", data.DisplayName),
                EnumChatType.Notification
            );

            return true;
        }
    }
}
