using System;
using Vintagestory.API.Common;

namespace EffectLib
{
    /// <summary>
    /// EffectLib's own built-in <see cref="IEffectHandler"/>, always registered by
    /// <see cref="ModSystem.EffectLibMod"/>. Carries out <see cref="UtilityEffects"/> so
    /// respawn, reshape, nutrition, temporal stability and size effects work for any mod built
    /// on EffectLib, with nothing extra to register.
    /// </summary>
    internal sealed class UtilityEffectHandler : IEffectHandler
    {
        public static readonly UtilityEffectHandler Instance = new();

        private UtilityEffectHandler() { }

        public void OnApplied(EntityPlayer entity, string effectId, EffectContext ctx)
        {
            if (ctx.Respawn)
                UtilityEffects.ApplyRespawn(entity);
            if (ctx.Reshape)
                UtilityEffects.ApplyReshape(entity);
            if (Math.Abs(ctx.RetainedNutrition) > float.Epsilon)
                UtilityEffects.ApplyNutrition(entity, ctx.RetainedNutrition);
            if (Math.Abs(ctx.TemporalStabilityGain) > float.Epsilon)
                UtilityEffects.ApplyTemporalStability(entity, ctx.TemporalStabilityGain);
            if (Math.Abs(ctx.SizeChange) > float.Epsilon)
                UtilityEffects.ApplySizeChange(entity, ctx, EffectRegistry.DomainOf(effectId));
        }

        // Size is deliberately not undone here: grow and shrink last until the player dies or
        // effects are cleared, which OnCleared handles.
        public void OnRemoved(EntityPlayer entity, string effectId, EffectContext ctx) { }

        /// <summary>
        /// Grow and shrink never expire on their own, so a clear is what undoes them - including
        /// a purging brew. Ignored when the purge does not cover the domain that applied the
        /// size change, so one mod's purge leaves another mod's resized player alone.
        /// </summary>
        public void OnCleared(EntityPlayer entity, EffectPurge scope) =>
            UtilityEffects.ResetSizeIfCovered(entity, scope);

        public void OnRestored(EntityPlayer entity)
        {
            float sizeDelta = entity.WatchedAttributes.GetFloat("potionSizeDelta", 0f);

            if (Math.Abs(sizeDelta) < 0.001f)
            {
                // Nothing left to hold the player at a changed size, so drop the stored base.
                UtilityEffects.ResetPlayerSize(entity);
            }
            else
            {
                // Re-broadcast so the client re-applies the stored size after a login.
                entity.WatchedAttributes.MarkPathDirty("potionSizeDelta");
            }
        }
    }
}
