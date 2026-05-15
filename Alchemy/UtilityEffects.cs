using System;
using Alchemy.ModConfig;
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
        // This stuff is annoying to debug, gotta make sure to set a lot of this in the correct order. Still isn't perfect but good enough.
        public static void ApplySizeChange(EntityPlayer entity, float delta)
        {
            if (!AlchemyConfig.Loaded.AllowGrowPotion && !AlchemyConfig.Loaded.AllowShrinkPotion)
                return;

            // On first application snapshot the player's actual current size as the base,
            // so race mods or other size-altering mods are respected.
            float currentDelta = entity.WatchedAttributes.GetFloat("potionSizeDelta", 0f);
            if (Math.Abs(currentDelta) < 0.001f)
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
                if (ModSystem.AlchemyMod.PlayerModelLibPresent)
                {
                    entity.WatchedAttributes.SetFloat(
                        "potionBaseEntitySize",
                        entity.WatchedAttributes.GetFloat("entitySize", 1.0f)
                    );
                }
            }

            float baseHeight = entity.WatchedAttributes.GetFloat(
                "potionBaseHeight",
                entity.CollisionBox.Y2
            );
            float newHeight = GameMath.Clamp(
                baseHeight + currentDelta + delta,
                AlchemyConfig.Loaded.GrowShrinkMinHeight,
                AlchemyConfig.Loaded.GrowShrinkMaxHeight
            );
            float newDelta = newHeight - baseHeight;
            if (Math.Abs(newDelta - currentDelta) < 0.001f)
                return; // already at min/max limit — nothing would change

            entity.WatchedAttributes.SetFloat("potionSizeDelta", newDelta);
            entity.WatchedAttributes.MarkPathDirty("potionSizeDelta");
        }

        public static void ResetPlayerSize(EntityPlayer entity)
        {
            if (!AlchemyConfig.Loaded.AllowGrowPotion && !AlchemyConfig.Loaded.AllowShrinkPotion)
                return;

            float potionBaseHeight = entity.WatchedAttributes.GetFloat("potionBaseHeight", 0f);
            if (potionBaseHeight < 0.1f)
                return;

            // Zero the delta WITHOUT clearing potionBaseHeight first. ApplySize fires on both
            // server and client via the listener: with delta=0 and baseHeight still valid it
            // resets to baseHeight+0=baseHeight. Clearing potionBaseHeight before MarkPathDirty
            // would make the client's ApplySize return early (baseHeight<0.1f) and leave the
            // client's collision box at the modified size.
            entity.WatchedAttributes.SetFloat("potionSizeDelta", 0f);
            entity.WatchedAttributes.MarkPathDirty("potionSizeDelta");

            if (ModSystem.AlchemyMod.PlayerModelLibPresent)
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
                entity.WatchedAttributes.SetFloat("potionBaseEntitySize", 0f);
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
            entity.WatchedAttributes.SetFloat("potionBaseClientSize", 0f);
            entity.WatchedAttributes.SetFloat("potionBaseHeight", 0f);
            entity.WatchedAttributes.SetFloat("potionBaseWidth", 0f);
            entity.WatchedAttributes.SetFloat("potionBaseEyeHeight", 0f);
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
            byEntity.GetBehavior<EntityBehaviorTemporalStabilityAffected>().OwnStability +=
                stabilityGain;
        }

        public static void ApplyReshapePotion(IServerPlayer serverPlayer)
        {
            serverPlayer.Entity.WatchedAttributes.SetBool("allowcharselonce", true);
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
