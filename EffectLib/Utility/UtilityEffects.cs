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

        private const string LegacyDomain = "alchemy";

        private static float ResolveBaseHeight(EntityPlayer entity)
        {
            float stored = entity.WatchedAttributes.GetFloat(KeyBaseHeight, 0f);
            return stored >= 0.1f ? stored : entity.CollisionBox.Y2;
        }

        // The effect's own bounds narrow the range, but can never widen past the server's hard
        // MinPlayerHeight/MaxPlayerHeight - computed fresh per effect.
        private static (float min, float max) EffectiveSizeBounds(EffectContext ctx)
        {
            float hardMin = EffectLibConfig.Loaded.MinPlayerHeight;
            float hardMax = EffectLibConfig.Loaded.MaxPlayerHeight;
            float itemMin = ctx.SizeMinHeight > 0.05f ? ctx.SizeMinHeight : hardMin;
            float itemMax = ctx.SizeMaxHeight > 0.05f ? ctx.SizeMaxHeight : hardMax;
            return (GameMath.Clamp(itemMin, hardMin, hardMax), GameMath.Clamp(itemMax, hardMin, hardMax));
        }

        public static bool CanApplySizeChange(EntityPlayer entity, EffectContext ctx)
        {
            if (!EffectPolicy.IsAllowed(EffectCapability.Resize))
                return false;
            if (Math.Abs(ctx.SizeChange) <= float.Epsilon)
                return false;

            float currentIntent = entity.WatchedAttributes.GetFloat(SizeDeltaAttr, 0f);
            float baseHeight = ResolveBaseHeight(entity);
            (float min, float max) = EffectiveSizeBounds(ctx);
            float currentHeight = GameMath.Clamp(baseHeight + currentIntent, min, max);

            return ctx.SizeChange > 0 ? currentHeight < max - 0.001f : currentHeight > min + 0.001f;
        }

        public static bool ApplySizeChange(EntityPlayer entity, EffectContext ctx, string domain)
        {
            if (!CanApplySizeChange(entity, ctx))
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
            }

            entity.WatchedAttributes.SetFloat(SizeDeltaAttr, currentIntent + ctx.SizeChange);
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

            // Just re-rendering an already-accumulated delta here, no effect in scope - the hard
            // limits are the only bound that still applies.
            float newHeight = GameMath.Clamp(
                baseHeight + sizeDelta,
                EffectLibConfig.Loaded.MinPlayerHeight,
                EffectLibConfig.Loaded.MaxPlayerHeight
            );
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

        // Blocks re-triggering reshape before .charsel is used.
        public static bool IsReshapeReentry(EntityAgent byEntity, EffectContext ctx) =>
            ctx.Reshape
            && ctx.BlockReshapeReentry
            && byEntity.WatchedAttributes.GetBool("allowcharselonce");

        // Blocks recall while mounted on a vessel (would strand the vessel).
        public static bool IsRecallOnVessel(EntityAgent byEntity, EffectContext ctx) =>
            ctx.Respawn
            && ctx.BlockRecallOnVessel
            && byEntity.MountedOn?.MountSupplier?.OnEntity?.HasBehavior("seatable") == true;

        // Blocks recall while mounted on a ridden animal - off by default, opt in per effect.
        public static bool IsRecallOnMount(EntityAgent byEntity, EffectContext ctx) =>
            ctx.Respawn
            && ctx.BlockRecallOnMount
            && byEntity.MountedOn?.MountSupplier?.OnEntity?.HasBehavior("mountable") == true;

        // Blocks a grow/shrink effect once the player is already at that size limit.
        public static bool IsSizeAtLimit(EntityAgent byEntity, EffectContext ctx) =>
            Math.Abs(ctx.SizeChange) > float.Epsilon
            && byEntity is EntityPlayer player
            && !CanApplySizeChange(player, ctx);

        // True if the limit hit is this effect's own (narrower) bound rather than the server's
        // hard MinPlayerHeight/MaxPlayerHeight.
        private static bool IsEffectOwnSizeLimit(EffectContext ctx, bool growing)
        {
            (float min, float max) = EffectiveSizeBounds(ctx);
            return growing
                ? max < EffectLibConfig.Loaded.MaxPlayerHeight - 0.001f
                : min > EffectLibConfig.Loaded.MinPlayerHeight + 0.001f;
        }

        // Shared reshape/recall/size block check - lang key or null. Used by direct consume and coating.
        public static string GetBlockReason(EntityAgent byEntity, EffectContext ctx)
        {
            if (IsReshapeReentry(byEntity, ctx))
                return "effectlib:reshape-block";
            if (IsRecallOnVessel(byEntity, ctx))
                return "effectlib:boat-block";
            if (IsRecallOnMount(byEntity, ctx))
                return "effectlib:mount-block";
            if (IsSizeAtLimit(byEntity, ctx))
            {
                bool growing = ctx.SizeChange > 0;
                bool ownLimit = IsEffectOwnSizeLimit(ctx, growing);
                return growing
                    ? (ownLimit ? "effectlib:effect-size-at-max" : "effectlib:size-at-max")
                    : (ownLimit ? "effectlib:effect-size-at-min" : "effectlib:size-at-min");
            }
            return null;
        }
    }
}
