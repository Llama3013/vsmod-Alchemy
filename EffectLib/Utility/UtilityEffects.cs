using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace EffectLib
{
    /// <summary>
    /// Built-in "utility" effects: world-interacting side effects (teleport to spawn, character
    /// reshape, nutrition retention, temporal stability, grow/shrink) as opposed to the simple
    /// per-tick entity-property effects and stat modifiers in <see cref="AtomicEffects"/>. These
    /// always work, with no dependent mod needing to register anything - see
    /// <see cref="UtilityEffectHandler"/>, EffectLib's own built-in <see cref="IEffectHandler"/>.
    /// </summary>
    public static class UtilityEffects
    {
        /// <summary>
        /// Whether the "playermodellib" mod is loaded. When present it drives an entity's visual
        /// scale via the "entitySize" attribute, so size changes are layered on top of that
        /// instead of overwriting it. Set once by <see cref="ModSystem.EffectLibMod"/>.
        /// </summary>
        public static bool PlayerModelLibPresent { get; internal set; }

        /// <summary>Height bounds used when an effect does not request its own via <see cref="EffectContext.SizeMinHeight"/>.</summary>
        public const float DefaultMinHeight = 0.2f;
        public const float DefaultMaxHeight = 10f;

        // WatchedAttributes keys. potionSizeDelta/potionBase* are kept for save compatibility
        // with Alchemy 2.x, which wrote them before EffectLib existed. potionSizeDomain and the
        // min/max bounds are new: absent on old saves, so they default to Alchemy's own domain
        // and to the constants above, matching pre-migration behaviour exactly.
        private const string KeySizeDelta = "potionSizeDelta";
        private const string KeyBaseHeight = "potionBaseHeight";
        private const string KeyBaseWidth = "potionBaseWidth";
        private const string KeyBaseEyeHeight = "potionBaseEyeHeight";
        private const string KeyBaseClientSize = "potionBaseClientSize";
        private const string KeyBaseEntitySize = "potionBaseEntitySize";
        private const string KeySizeDomain = "potionSizeDomain";
        private const string KeySizeMinHeight = "potionSizeMinHeight";
        private const string KeySizeMaxHeight = "potionSizeMaxHeight";

        // No saved size effect predates EffectLib other than Alchemy's, so an absent domain
        // stamp (pre-migration save) is assumed to be Alchemy's.
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

        public static bool CanApplySizeChange(EntityPlayer entity, float delta)
        {
            if (!EffectPolicy.IsAllowed(EffectCapability.Resize))
                return false;
            if (Math.Abs(delta) <= float.Epsilon)
                return false;

            float currentIntent = entity.WatchedAttributes.GetFloat(KeySizeDelta, 0f);
            float baseHeight = ResolveBaseHeight(entity);
            (float min, float max) = StoredSizeBounds(entity);
            float currentHeight = GameMath.Clamp(baseHeight + currentIntent, min, max);

            return delta > 0 ? currentHeight < max - 0.001f : currentHeight > min + 0.001f;
        }

        /// <summary>
        /// Grows or shrinks <paramref name="entity"/> by <paramref name="delta"/> blocks, owned
        /// by <paramref name="domain"/> for purge scoping. Bounds default to
        /// <see cref="DefaultMinHeight"/>/<see cref="DefaultMaxHeight"/> when the context does
        /// not request its own.
        /// </summary>
        public static bool ApplySizeChange(EntityPlayer entity, EffectContext ctx, string domain)
        {
            float delta = ctx.SizeChange;
            if (!CanApplySizeChange(entity, delta))
                return false;

            // On first application snapshot the player's actual current size, bounds and owning
            // domain, so race mods or other size-altering mods are respected and a later config
            // change does not retroactively affect a player already mid-effect.
            float currentIntent = entity.WatchedAttributes.GetFloat(KeySizeDelta, 0f);
            if (entity.WatchedAttributes.GetFloat(KeyBaseHeight, 0f) < 0.1f)
            {
                float naturalHeight = entity.CollisionBox.Y2;
                entity.WatchedAttributes.SetFloat(KeyBaseHeight, naturalHeight);
                entity.WatchedAttributes.SetFloat(
                    KeyBaseWidth,
                    entity.Properties.CollisionBoxSize.X
                );
                // PlayerModelLib sets Properties.EyeHeight on both sides during entity init,
                // accounting for each model's actual ratio and size-slider clamping.
                // Fallback to the VS standard ratio only if EyeHeight wasn't set (shouldn't happen).
                float eyeH = (float)entity.Properties.EyeHeight;
                entity.WatchedAttributes.SetFloat(
                    KeyBaseEyeHeight,
                    eyeH > 0.01f ? eyeH : naturalHeight * 0.9054f
                );
                // Store PlayerModelLib's visual scale so we scale on top of it, not from 1.0.
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

            entity.WatchedAttributes.SetFloat(KeySizeDelta, currentIntent + delta);
            entity.WatchedAttributes.MarkPathDirty(KeySizeDelta);
            return true;
        }

        /// <summary>Undoes a size change, but only if <paramref name="scope"/> covers the domain that applied it.</summary>
        public static void ResetSizeIfCovered(EntityPlayer entity, EffectPurge scope)
        {
            string domain = entity.WatchedAttributes.GetString(KeySizeDomain, LegacyDomain);
            if (scope.CoversDomain(domain))
                ResetPlayerSize(entity);
        }

        public static void ResetPlayerSize(EntityPlayer entity)
        {
            float potionBaseHeight = entity.WatchedAttributes.GetFloat(KeyBaseHeight, 0f);
            if (potionBaseHeight < 0.1f)
                return;

            entity.WatchedAttributes.SetFloat(KeySizeDelta, 0f);
            entity.WatchedAttributes.MarkPathDirty(KeySizeDelta);

            if (PlayerModelLibPresent)
            {
                float baseEntitySize = entity.WatchedAttributes.GetFloat(KeyBaseEntitySize, 0f);
                if (baseEntitySize > 0.01f)
                {
                    entity.WatchedAttributes.SetFloat("entitySize", baseEntitySize);
                    entity.WatchedAttributes.MarkPathDirty("entitySize");
                }
            }

            // Direct server-side reset for immediate physics; the client resets via ApplySizeToEntity.
            entity.CollisionBox.Y2 = potionBaseHeight;
            entity.SelectionBox.Y2 = potionBaseHeight;
            float baseEyeHeight = entity.WatchedAttributes.GetFloat(
                KeyBaseEyeHeight,
                potionBaseHeight * 0.9054f
            );
            entity.Properties.EyeHeight = baseEyeHeight;
            if (entity.Properties.Client != null)
            {
                float baseClientSize = entity.WatchedAttributes.GetFloat(KeyBaseClientSize, 1.0f);
                entity.Properties.Client.Size = baseClientSize > 0.01f ? baseClientSize : 1.0f;
            }
            entity.WatchedAttributes.MarkPathDirty(KeySizeDelta);
        }

        /// <summary>
        /// Zeroes potion size WatchedAttributes without touching the collision box. Use when the
        /// model changes externally (e.g. char select) so the new model keeps control of
        /// collision box dimensions.
        /// </summary>
        public static void ClearSizeState(EntityPlayer entity)
        {
            entity.WatchedAttributes.SetFloat(KeyBaseHeight, 0f);
            entity.WatchedAttributes.SetFloat(KeyBaseWidth, 0f);
            entity.WatchedAttributes.SetFloat(KeySizeDelta, 0f);
            entity.WatchedAttributes.SetFloat(KeyBaseEntitySize, 0f);
            entity.WatchedAttributes.SetFloat(KeyBaseClientSize, 0f);
            entity.WatchedAttributes.MarkPathDirty(KeySizeDelta);
        }

        /// <summary>
        /// Recomputes collision box, eye height and visual scale from the persisted size delta.
        /// Driven automatically by a WatchedAttributes listener on both server and client - see
        /// EffectLib.Patches.PlayerSizePatch - but safe to call again, e.g. once a client is
        /// fully loaded and WatchedAttributes sync is guaranteed to have arrived.
        /// </summary>
        public static void ApplySizeToEntity(EntityPlayer entity)
        {
            float baseHeight = entity.WatchedAttributes.GetFloat(KeyBaseHeight, 0f);
            if (baseHeight < 0.1f)
                return;

            float delta = entity.WatchedAttributes.GetFloat(KeySizeDelta, 0f);
            float baseEyeHeight = entity.WatchedAttributes.GetFloat(
                KeyBaseEyeHeight,
                baseHeight * 0.9054f
            );

            (float min, float max) = StoredSizeBounds(entity);
            float newHeight = GameMath.Clamp(baseHeight + delta, min, max);
            float scale = newHeight / baseHeight;

            // Scale width proportionally from the snapshotted base width so both dimensions
            // grow/shrink together. Fall back to the current X if no base was stored (old saves).
            // Update Properties so that any future updateColSelBoxes() call keeps the scaled
            // dimensions. SetCollisionBox/SetSelectionBox also update the origin boxes.
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
