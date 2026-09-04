using System;
using Vintagestory.API.Common;

namespace EffectLib
{
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

        public void OnRemoved(EntityPlayer entity, string effectId, EffectContext ctx) { }

        public void OnCleared(EntityPlayer entity, EffectPurge scope) =>
            UtilityEffects.ResetSizeIfCovered(entity, scope);

        public void OnRestored(EntityPlayer entity)
        {
            float sizeDelta = entity.WatchedAttributes.GetFloat(UtilityEffects.SizeDeltaAttr, 0f);

            if (Math.Abs(sizeDelta) < 0.001f)
            {
                UtilityEffects.ResetPlayerSize(entity);
            }
            else
            {
                entity.WatchedAttributes.MarkPathDirty(UtilityEffects.SizeDeltaAttr);
            }
        }
    }
}
