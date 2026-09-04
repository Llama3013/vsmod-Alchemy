using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace EffectLib
{
    public static class UtilityEffects
    {
        public static bool PlayerModelLibPresent { get; internal set; }

        public const float DefaultMinHeight = 0.2f;
        public const float DefaultMaxHeight = 10f;

        public const string SizeDeltaAttr = "effectlib:sizeDelta";

        private const string KeyBaseHeight = "effectlib:baseHeight";
        private const string KeyBaseWidth = "effectlib:baseWidth";
        private const string KeyBaseEyeHeight = "effectlib:baseEyeHeight";
        private const string KeyBaseClientSize = "effectlib:baseClientSize";
        private const string KeyBaseEntitySize = "effectlib:baseEntitySize";
        private const string KeySizeDomain = "effectlib:sizeDomain";
        private const string KeySizeMinHeight = "effectlib:sizeMinHeight";
        private const string KeySizeMaxHeight = "effectlib:sizeMaxHeight";

        private const string LegacyDomain = "alchemy";

        private static float ResolveBaseHeight(EntityPlayer entity)
        {
            float stored = entity.WatchedAttributes.GetFloat(KeyBaseHeight, 0f);
            return stored >= 0.1f ? stored : entity.CollisionBox.Y2;
        }

        private static (float min, float max) StoredSizeBounds(EntityPlayer entity)
        {
            float min = entity.WatchedAttributes.GetFloat(KeySizeMinHeight, 0f);
            float max = entity.WatchedAttributes.GetFloat(KeySizeMaxHeight, 0f);
            return (
                min >= 0.05f ? min : DefaultMinHeight,
                max >= 0.05f ? max : DefaultMaxHeight
            );
        }

        public static bool CanApplySizeChange(EntityPlayer entity, float sizeDelta)
        {
            if (!EffectPolicy.IsAllowed(EffectCapability.Resize))
                return false;
            if (Math.Abs(sizeDelta) <= float.Epsilon)
                return false;

            float currentIntent = entity.WatchedAttributes.GetFloat(SizeDeltaAttr, 0f);
            float baseHeight = ResolveBaseHeight(entity);
            (float min, float max) = StoredSizeBounds(entity);
            float currentHeight = GameMath.Clamp(baseHeight + currentIntent, min, max);

            return sizeDelta > 0 ? currentHeight < max - 0.001f : currentHeight > min + 0.001f;
        }

        public static bool ApplySizeChange(EntityPlayer entity, EffectContext ctx, string domain)
        {
            float sizeDelta = ctx.SizeChange;
            if (!CanApplySizeChange(entity, sizeDelta))
                return false;

            float currentIntent = entity.WatchedAttributes.GetFloat(SizeDeltaAttr, 0f);
            if (entity.WatchedAttributes.GetFloat(KeyBaseHeight, 0f) < 0.1f)
            {
                float naturalHeight = entity.CollisionBox.Y2;
                entity.WatchedAttributes.SetFloat(KeyBaseHeight, naturalHeight);
                entity.WatchedAttributes.SetFloat(
                    KeyBaseWidth,
                    entity.Properties.CollisionBoxSize.X
                );
                float eyeH = (float)entity.Properties.EyeHeight;
                entity.WatchedAttributes.SetFloat(
                    KeyBaseEyeHeight,
                    eyeH > 0.01f ? eyeH : naturalHeight * 0.9054f
                );
                entity.WatchedAttributes.SetFloat(
                    KeyBaseClientSize,
                    entity.Properties.Client?.Size ?? 1.0f
                );
                if (PlayerModelLibPresent)
                {
                    entity.WatchedAttributes.SetFloat(
                        KeyBaseEntitySize,
                        entity.WatchedAttributes.GetFloat("entitySize", 1.0f)
                    );
                }

                entity.WatchedAttributes.SetString(KeySizeDomain, domain ?? LegacyDomain);
                entity.WatchedAttributes.SetFloat(
                    KeySizeMinHeight,
                    ctx.SizeMinHeight > 0.05f ? ctx.SizeMinHeight : DefaultMinHeight
                );
                entity.WatchedAttributes.SetFloat(
                    KeySizeMaxHeight,
                    ctx.SizeMaxHeight > 0.05f ? ctx.SizeMaxHeight : DefaultMaxHeight
                );
            }

            entity.WatchedAttributes.SetFloat(SizeDeltaAttr, currentIntent + sizeDelta);
            entity.WatchedAttributes.MarkPathDirty(SizeDeltaAttr);
            return true;
        }

        public static void ResetSizeIfCovered(EntityPlayer entity, EffectPurge scope)
        {
            string domain = entity.WatchedAttributes.GetString(KeySizeDomain, LegacyDomain);
            if (scope.CoversDomain(domain))
                ResetPlayerSize(entity);
        }

        public static void ResetPlayerSize(EntityPlayer entity)
        {
            float baseHeight = entity.WatchedAttributes.GetFloat(KeyBaseHeight, 0f);
            if (baseHeight < 0.1f)
                return;

            entity.WatchedAttributes.SetFloat(SizeDeltaAttr, 0f);
            entity.WatchedAttributes.MarkPathDirty(SizeDeltaAttr);

            if (PlayerModelLibPresent)
            {
                float baseEntitySize = entity.WatchedAttributes.GetFloat(KeyBaseEntitySize, 0f);
                if (baseEntitySize > 0.01f)
                {
                    entity.WatchedAttributes.SetFloat("entitySize", baseEntitySize);
                    entity.WatchedAttributes.MarkPathDirty("entitySize");
                }
            }

            entity.CollisionBox.Y2 = baseHeight;
            entity.SelectionBox.Y2 = baseHeight;
            float baseEyeHeight = entity.WatchedAttributes.GetFloat(
                KeyBaseEyeHeight,
                baseHeight * 0.9054f
            );
            entity.Properties.EyeHeight = baseEyeHeight;
            if (entity.Properties.Client != null)
            {
                float baseClientSize = entity.WatchedAttributes.GetFloat(KeyBaseClientSize, 1.0f);
                entity.Properties.Client.Size = baseClientSize > 0.01f ? baseClientSize : 1.0f;
            }
            entity.WatchedAttributes.MarkPathDirty(SizeDeltaAttr);
        }

        public static void ClearSizeState(EntityPlayer entity)
        {
            entity.WatchedAttributes.SetFloat(KeyBaseHeight, 0f);
            entity.WatchedAttributes.SetFloat(KeyBaseWidth, 0f);
            entity.WatchedAttributes.SetFloat(SizeDeltaAttr, 0f);
            entity.WatchedAttributes.SetFloat(KeyBaseEntitySize, 0f);
            entity.WatchedAttributes.SetFloat(KeyBaseClientSize, 0f);
            entity.WatchedAttributes.MarkPathDirty(SizeDeltaAttr);
        }

        public static void ApplySizeToEntity(EntityPlayer entity)
        {
            float baseHeight = entity.WatchedAttributes.GetFloat(KeyBaseHeight, 0f);
            if (baseHeight < 0.1f)
                return;

            float sizeDelta = entity.WatchedAttributes.GetFloat(SizeDeltaAttr, 0f);
            float baseEyeHeight = entity.WatchedAttributes.GetFloat(
                KeyBaseEyeHeight,
                baseHeight * 0.9054f
            );

            (float min, float max) = StoredSizeBounds(entity);
            float newHeight = GameMath.Clamp(baseHeight + sizeDelta, min, max);
            float scale = newHeight / baseHeight;

            float baseWidth = entity.WatchedAttributes.GetFloat(KeyBaseWidth, 0f);
            float newWidth =
                baseWidth > 0.01f ? baseWidth * scale : entity.Properties.CollisionBoxSize.X;

            entity.Properties.CollisionBoxSize.X = newWidth;
            entity.Properties.CollisionBoxSize.Y = newHeight;
            entity.SetCollisionBox(newWidth, newHeight);

            if (entity.Properties.SelectionBoxSize != null)
            {
                entity.Properties.SelectionBoxSize.X = newWidth;
                entity.Properties.SelectionBoxSize.Y = newHeight;
            }
            entity.SetSelectionBox(newWidth, newHeight);

            entity.Properties.EyeHeight = baseEyeHeight * scale;

            if (entity.Properties.Client != null)
            {
                float baseClientSize = entity.WatchedAttributes.GetFloat(KeyBaseClientSize, 0f);
                entity.Properties.Client.Size =
                    baseClientSize > 0.01f ? baseClientSize * scale : scale;
            }
        }

        public static void ApplyNutrition(EntityAgent byEntity, float retainedNutrition)
        {
            ITreeAttribute hungerTree = byEntity.WatchedAttributes.GetTreeAttribute("hunger");
            if (hungerTree == null)
                return;

            float maxSaturation = hungerTree.GetFloat("maxsaturation");
            float totalSatiety =
                (
                    hungerTree.GetFloat("fruitLevel")
                    + hungerTree.GetFloat("vegetableLevel")
                    + hungerTree.GetFloat("grainLevel")
                    + hungerTree.GetFloat("proteinLevel")
                    + hungerTree.GetFloat("dairyLevel")
                ) * retainedNutrition;
            float perCategory = Math.Min(Math.Max(totalSatiety / 5, 0), maxSaturation);

            hungerTree.SetFloat("fruitLevel", perCategory);
            hungerTree.SetFloat("vegetableLevel", perCategory);
            hungerTree.SetFloat("grainLevel", perCategory);
            hungerTree.SetFloat("proteinLevel", perCategory);
            hungerTree.SetFloat("dairyLevel", perCategory);
            byEntity.WatchedAttributes.MarkPathDirty("hunger");
        }

        public static void ApplyRespawn(EntityPlayer entity)
        {
            if (entity.Player is not IServerPlayer serverPlayer || !entity.Api.Side.IsServer())
                return;

            FuzzyEntityPos spawn = serverPlayer.GetSpawnPosition(false);
            entity.TeleportTo(spawn);
        }

        public static void ApplyTemporalStability(EntityAgent byEntity, float stabilityGain)
        {
            EntityBehaviorTemporalStabilityAffected stabilityBehavior =
                byEntity.GetBehavior<EntityBehaviorTemporalStabilityAffected>();
            if (stabilityBehavior == null)
                return;
            stabilityBehavior.OwnStability += stabilityGain;
        }

        public static void ApplyReshape(EntityPlayer entity)
        {
            if (entity.Player is not IServerPlayer)
                return;
            entity.WatchedAttributes.SetBool("allowcharselonce", true);
        }
    }
}
