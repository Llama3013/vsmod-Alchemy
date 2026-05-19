using System;
using System.Collections.Generic;
using System.Linq;
using Alchemy.ModConfig;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace Alchemy
{
    public sealed class PotionEffectManager(EntityPlayer entity)
    {
        private readonly EntityPlayer entity = entity;
        private readonly ICoreAPI api = entity.Api;
        private readonly Dictionary<string, ActiveEffect> active = [];

        private float originalFallDamageMultiplier = 1f;
        private bool originalCanClimbAnywhere;

        public bool IsActive(string id) => active.ContainsKey(id);

        public bool TryApplyPotion(string id, PotionContext ctx, string name)
        {
            try
            {
                if (IsActive(id) || entity.WatchedAttributes.GetLong(id) != 0)
                {
                    api.Logger.Debug(
                        "Cannot apply potion for potionId {0}, it is currently already applied!",
                        id
                    );
                    return false;
                }

                TempEffect effect = new(id, ctx);
                effect.Apply(entity);

                if (effect.Context.Respawn)
                {
                    UtilityEffects.ApplyRecallPotion(entity.Player as IServerPlayer, entity, api);
                }
                if (effect.Context.Reshape)
                {
                    UtilityEffects.ApplyReshapePotion(entity.Player as IServerPlayer);
                }
                if (Math.Abs(effect.Context.RetainedNutrition) > float.Epsilon)
                {
                    UtilityEffects.ApplyNutritionPotion(entity, effect.Context.RetainedNutrition);
                }
                if (Math.Abs(effect.Context.TemporalStabilityGain) > float.Epsilon)
                {
                    UtilityEffects.ApplyTemporalPotion(
                        entity,
                        effect.Context.TemporalStabilityGain
                    );
                }
                if (effect.Context.GlowStrength > 0)
                {
                    entity.WatchedAttributes.SetInt("glowStrength", effect.Context.GlowStrength);
                }
                if (Math.Abs(effect.Context.SizeChange) > float.Epsilon)
                {
                    UtilityEffects.ApplySizeChange(entity, effect.Context.SizeChange);
                }
                if (
                    Math.Abs(effect.Context.FallDamageReduction) > float.Epsilon
                    && AlchemyConfig.Loaded.AllowFallPotion
                )
                {
                    originalFallDamageMultiplier = entity.Properties.FallDamageMultiplier;
                    entity.Properties.FallDamageMultiplier =
                        1f - Math.Min(effect.Context.FallDamageReduction, 1f);
                }

                if (effect.Context.CanClimbAnywhere && AlchemyConfig.Loaded.AllowClimbPotion)
                {
                    originalCanClimbAnywhere = entity.Properties.CanClimbAnywhere;
                    entity.Properties.CanClimbAnywhere = true;
                }

                long handle;
                bool ticking;

                if (effect.Context.TickSec > 0)
                {
                    handle = entity.World.RegisterGameTickListener(
                        dt => Tick(id),
                        effect.Context.TickSec * 1000
                    );
                    ticking = true;
                }
                else if (effect.Context.Duration > 0)
                {
                    handle = entity.World.RegisterCallback(
                        dt => RemoveEffect(id),
                        effect.Context.Duration * 1000
                    );
                    ticking = false;
                }
                else
                {
                    // Instant potion: set a brief WatchedAttributes lock so the
                    // OnHeldInteractStop guard (GetLong != 0) should stop double consume potion
                    // Might need better fix but works for now and doesn't cause any issues that I know of
                    long tempHandle = entity.World.RegisterCallback(
                        dt => entity.WatchedAttributes.RemoveAttribute(id),
                        500
                    );
                    entity.WatchedAttributes.SetLong(id, tempHandle);
                    return true;
                }

                active[id] = new ActiveEffect(effect, handle, ticking, name);
                entity.WatchedAttributes.SetLong(id, handle);

                return true;
            }
            catch (Exception err)
            {
                // Probably don't need a try catch but will leave this here just in case
                api.Logger.Error("Potion of {0}, could not be applied. An error occurred", id);
                api.Logger.Error(err);
                return false;
            }
        }

        private void Tick(string id)
        {
            if (!active.TryGetValue(id, out ActiveEffect activeEffect))
                return;

            activeEffect.Elapsed++;

            activeEffect.Effect.Tick(entity);

            if (activeEffect.Elapsed >= activeEffect.Effect.Context.Duration)
                RemoveEffect(id);
        }

        public void RemoveEffect(string id)
        {
            if (!active.TryGetValue(id, out ActiveEffect activeEffect))
                return;

            IServerPlayer serverPlayer = entity?.Player as IServerPlayer;
            serverPlayer?.SendMessage(
                GlobalConstants.InfoLogChatGroup,
                Lang.Get("alchemy:effect-lose", activeEffect.PotionName),
                EnumChatType.Notification
            );

            if (activeEffect.IsTicking)
                entity.World.UnregisterGameTickListener(activeEffect.ListenerId);
            else
                entity.World.UnregisterCallback(activeEffect.ListenerId);

            if (activeEffect.Effect.Context.GlowStrength > 0)
            {
                entity.WatchedAttributes.RemoveAttribute("glowStrength");
            }
            if (activeEffect.Effect.Context.FallDamageReduction > 0)
            {
                entity.Properties.FallDamageMultiplier = originalFallDamageMultiplier;
            }

            if (
                activeEffect.Effect.Context.CanClimbAnywhere
                && AlchemyConfig.Loaded.AllowClimbPotion
            )
            {
                entity.Properties.CanClimbAnywhere = originalCanClimbAnywhere;
            }
            activeEffect.Effect.Remove(entity);

            active.Remove(id);
            entity.WatchedAttributes.RemoveAttribute(id);
        }

        public void RemoveAll()
        {
            foreach (string id in active.Keys.ToList())
            {
                RemoveEffect(id);
            }

            // Might be needed to remove potion listener ids from watched attributes
            List<string> potionAttributes =
            [
                .. entity.WatchedAttributes.Keys.Where(key =>
                    key.EndsWith("potionid", StringComparison.OrdinalIgnoreCase)
                ),
            ];

            foreach (string attr in potionAttributes)
            {
                entity.WatchedAttributes.RemoveAttribute(attr);
            }
        }
    }

    internal sealed record ActiveEffect(
        TempEffect Effect,
        long ListenerId,
        bool IsTicking,
        string PotionName
    )
    {
        public int Elapsed;
    }
}
