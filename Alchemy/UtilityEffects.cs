using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Alchemy
{
    internal static class UtilityEffects
    {
        private static float ResolveBaseHeight(EntityPlayer entity)
        {
            float stored = entity.WatchedAttributes.GetFloat("potionBaseHeight", 0f);
            return stored >= 0.1f ? stored : entity.CollisionBox.Y2;
        }

        public static bool CanApplySizeChange(EntityPlayer entity, float delta)
        {
            if (!AlchemyConfig.Loaded.AllowGrowPotion && !AlchemyConfig.Loaded.AllowShrinkPotion)
                return false;
            if (Math.Abs(delta) <= float.Epsilon)
                return false;

            float currentIntent = entity.WatchedAttributes.GetFloat("potionSizeDelta", 0f);
            float baseHeight = ResolveBaseHeight(entity);
            float currentHeight = GameMath.Clamp(
                baseHeight + currentIntent,
                AlchemyConfig.Loaded.GrowShrinkMinHeight,
                AlchemyConfig.Loaded.GrowShrinkMaxHeight
            );

            return delta > 0
                ? currentHeight < AlchemyConfig.Loaded.GrowShrinkMaxHeight - 0.001f
                : currentHeight > AlchemyConfig.Loaded.GrowShrinkMinHeight + 0.001f;
        }

        // This stuff is annoying to debug, gotta make sure to set a lot of this in the correct order. Still isn't perfect but good enough.
        public static bool ApplySizeChange(EntityPlayer entity, float delta)
        {
            if (!CanApplySizeChange(entity, delta))
                return false;

            // On first application snapshot the player's actual current size as the base,
            // so race mods or other size-altering mods are respected.
            float currentIntent = entity.WatchedAttributes.GetFloat("potionSizeDelta", 0f);
            if (entity.WatchedAttributes.GetFloat("potionBaseHeight", 0f) < 0.1f)
            {
                float naturalHeight = entity.CollisionBox.Y2;
                entity.WatchedAttributes.SetFloat("potionBaseHeight", naturalHeight);
                entity.WatchedAttributes.SetFloat(
                    "potionBaseWidth",
                    entity.Properties.CollisionBoxSize.X
                );
                // PlayerModelLib sets Properties.EyeHeight on both sides during entity init,
                // accounting for each model's actual ratio and size-slider clamping.
                // Fallback to the VS standard ratio only if EyeHeight wasn't set (shouldn't happen).
                float eyeH = (float)entity.Properties.EyeHeight;
                entity.WatchedAttributes.SetFloat(
                    "potionBaseEyeHeight",
                    eyeH > 0.01f ? eyeH : naturalHeight * 0.9054f
                );
                // Store PlayerModelLib's visual scale so we scale on top of it, not from 1.0.
                entity.WatchedAttributes.SetFloat(
                    "potionBaseClientSize",
                    entity.Properties.Client?.Size ?? 1.0f
                );
                if (AlchemyMod.PlayerModelLibPresent)
                {
                    entity.WatchedAttributes.SetFloat(
                        "potionBaseEntitySize",
                        entity.WatchedAttributes.GetFloat("entitySize", 1.0f)
                    );
                }
            }

            entity.WatchedAttributes.SetFloat("potionSizeDelta", currentIntent + delta);
            entity.WatchedAttributes.MarkPathDirty("potionSizeDelta");
            return true;
        }

        public static void ResetPlayerSize(EntityPlayer entity)
        {
            if (!AlchemyConfig.Loaded.AllowGrowPotion && !AlchemyConfig.Loaded.AllowShrinkPotion)
                return;

            float potionBaseHeight = entity.WatchedAttributes.GetFloat("potionBaseHeight", 0f);
            if (potionBaseHeight < 0.1f)
                return;

            entity.WatchedAttributes.SetFloat("potionSizeDelta", 0f);
            entity.WatchedAttributes.MarkPathDirty("potionSizeDelta");

            if (AlchemyMod.PlayerModelLibPresent)
            {
                float baseEntitySize = entity.WatchedAttributes.GetFloat(
                    "potionBaseEntitySize",
                    0f
                );
                if (baseEntitySize > 0.01f)
                {
                    entity.WatchedAttributes.SetFloat("entitySize", baseEntitySize);
                    entity.WatchedAttributes.MarkPathDirty("entitySize");
                }
            }

            // Direct server-side reset for immediate physics; the client resets via ApplySize.
            entity.CollisionBox.Y2 = potionBaseHeight;
            entity.SelectionBox.Y2 = potionBaseHeight;
            float baseEyeHeight = entity.WatchedAttributes.GetFloat(
                "potionBaseEyeHeight",
                potionBaseHeight * 0.9054f
            );
            entity.Properties.EyeHeight = baseEyeHeight;
            if (entity.Properties.Client != null)
            {
                float baseClientSize = entity.WatchedAttributes.GetFloat(
                    "potionBaseClientSize",
                    1.0f
                );
                entity.Properties.Client.Size = baseClientSize > 0.01f ? baseClientSize : 1.0f;
            }
            entity.WatchedAttributes.MarkPathDirty("potionSizeDelta");
        }

        public static void ApplyNutritionPotion(EntityAgent byEntity, float retainedNutrition)
        {
            ITreeAttribute hungerTree = byEntity.WatchedAttributes.GetTreeAttribute("hunger");
            if (hungerTree != null)
            {
                float maxSaturation = hungerTree.GetFloat("maxsaturation");
                float totalSatiety =
                    (
                        hungerTree.GetFloat("fruitLevel")
                        + hungerTree.GetFloat("vegetableLevel")
                        + hungerTree.GetFloat("grainLevel")
                        + hungerTree.GetFloat("proteinLevel")
                        + hungerTree.GetFloat("dairyLevel")
                    ) * retainedNutrition;

                hungerTree.SetFloat(
                    "fruitLevel",
                    Math.Min(Math.Max(totalSatiety / 5, 0), maxSaturation)
                );
                hungerTree.SetFloat(
                    "vegetableLevel",
                    Math.Min(Math.Max(totalSatiety / 5, 0), maxSaturation)
                );
                hungerTree.SetFloat(
                    "grainLevel",
                    Math.Min(Math.Max(totalSatiety / 5, 0), maxSaturation)
                );
                hungerTree.SetFloat(
                    "proteinLevel",
                    Math.Min(Math.Max(totalSatiety / 5, 0), maxSaturation)
                );
                hungerTree.SetFloat(
                    "dairyLevel",
                    Math.Min(Math.Max(totalSatiety / 5, 0), maxSaturation)
                );
                byEntity.WatchedAttributes.MarkPathDirty("hunger");
            }
        }

        public static void ApplyRecallPotion(
            IServerPlayer serverPlayer,
            EntityAgent byEntity,
            ICoreAPI api
        )
        {
            if (api.Side.IsServer())
            {
                FuzzyEntityPos spawn = serverPlayer.GetSpawnPosition(false);
                byEntity.TeleportTo(spawn);
            }
        }

        public static void ApplyTemporalPotion(EntityAgent byEntity, float stabilityGain)
        {
            EntityBehaviorTemporalStabilityAffected stabilityBehavior =
                byEntity.GetBehavior<EntityBehaviorTemporalStabilityAffected>();
            if (stabilityBehavior == null)
                return;
            stabilityBehavior.OwnStability += stabilityGain;
        }

        public static void ApplyReshapePotion(IServerPlayer serverPlayer)
        {
            serverPlayer.Entity.WatchedAttributes.SetBool("allowcharselonce", true);
            // AlchemyMod.serverChannel?.SendPacket(new OpenCharSelPacket(), serverPlayer);
        }

        // Zeroes potion size WatchedAttributes without touching the collision box.
        // Use when the model changes externally (e.g. char select) so the new model
        // keeps control of collision box dimensions.
        public static void ClearSizeState(EntityPlayer entity)
        {
            entity.WatchedAttributes.SetFloat("potionBaseHeight", 0f);
            entity.WatchedAttributes.SetFloat("potionBaseWidth", 0f);
            entity.WatchedAttributes.SetFloat("potionSizeDelta", 0f);
            entity.WatchedAttributes.SetFloat("potionBaseEntitySize", 0f);
            entity.WatchedAttributes.SetFloat("potionBaseClientSize", 0f);
            entity.WatchedAttributes.MarkPathDirty("potionSizeDelta");
        }
    }
}
