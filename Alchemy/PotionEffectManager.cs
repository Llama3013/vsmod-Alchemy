using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace Alchemy
{
    public sealed class PotionEffectManager(EntityPlayer entity)
    {
        private const string PersistKey = "alchemyEffects";

        private readonly EntityPlayer entity = entity;
        private readonly ICoreAPI api = entity.Api;
        private readonly Dictionary<string, ActiveEffect> active = [];

        private float originalFallDamageMultiplier = 1f;
        private bool originalCanClimbAnywhere;
        private bool originalFreeMove;

        public bool IsActive(string id) => active.ContainsKey(id);

        public bool HasAnyActive => active.Count > 0;

        public bool CanRefresh(string id) =>
            AlchemyConfig.Loaded.AllowPotionRefresh
            && active.TryGetValue(id, out ActiveEffect activeEffect)
            && !(
                activeEffect.Effect.Context.TickSec > 0
                && Math.Abs(activeEffect.Effect.Context.Health) > float.Epsilon
            );

        public bool TryApplyPotion(string id, PotionContext ctx, string name, bool resume = false)
        {
            try
            {
                if (IsActive(id) || entity.WatchedAttributes.GetLong(id) != 0)
                {
                    if (CanRefresh(id))
                    {
                        RemoveEffect(id, false);
                    }
                    else
                    {
                        api.Logger.Debug(
                            "Cannot apply potion for potionId {0}, it is currently already applied!",
                            id
                        );
                        return false;
                    }
                }

                TempEffect effect = new(id, ctx);
                effect.Apply(entity, resume);

                if (!resume && effect.Context.Respawn)
                {
                    UtilityEffects.ApplyRecallPotion(entity.Player as IServerPlayer, entity, api);
                }
                if (!resume && effect.Context.Reshape)
                {
                    UtilityEffects.ApplyReshapePotion(entity.Player as IServerPlayer);
                }
                if (!resume && Math.Abs(effect.Context.RetainedNutrition) > float.Epsilon)
                {
                    UtilityEffects.ApplyNutritionPotion(entity, effect.Context.RetainedNutrition);
                }
                if (!resume && Math.Abs(effect.Context.TemporalStabilityGain) > float.Epsilon)
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
                if (!resume && Math.Abs(effect.Context.SizeChange) > float.Epsilon)
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

                if (
                    effect.Context.CanFly
                    && AlchemyConfig.Loaded.AllowFlightPotion
                    && entity.Player is IServerPlayer flyPlayer
                )
                {
                    if (!resume)
                        originalFreeMove = flyPlayer.WorldData.FreeMove;
                    flyPlayer.WorldData.FreeMove = true;
                    flyPlayer.BroadcastPlayerData();
                }

                long handle;

                if (effect.Context.Duration > 0)
                {
                    handle = entity.World.RegisterCallback(
                        dt => RemoveEffect(id),
                        effect.Context.Duration * 1000
                    );
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

                active[id] = new ActiveEffect(effect, handle, false, name)
                {
                    ApplyMs = entity.World.ElapsedMilliseconds,
                };
                entity.WatchedAttributes.SetLong(id, handle);
                SaveEffectRecord(id, ctx, name);

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

        public void RemoveEffect(string id, bool notify = true)
        {
            if (!active.TryGetValue(id, out ActiveEffect activeEffect))
                return;

            if (notify)
            {
                IServerPlayer serverPlayer = entity?.Player as IServerPlayer;
                serverPlayer?.SendMessage(
                    GlobalConstants.InfoLogChatGroup,
                    Lang.Get("alchemy:effect-lose", activeEffect.PotionName),
                    EnumChatType.Notification
                );
            }

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

            if (
                activeEffect.Effect.Context.CanFly
                && AlchemyConfig.Loaded.AllowFlightPotion
                && entity?.Player is IServerPlayer flyPlayer
            )
            {
                flyPlayer.WorldData.FreeMove = originalFreeMove;
                flyPlayer.BroadcastPlayerData();
            }
            activeEffect.Effect.Remove(entity);

            active.Remove(id);
            entity.WatchedAttributes.RemoveAttribute(id);

            ITreeAttribute tree = entity.WatchedAttributes.GetTreeAttribute(PersistKey);
            if (tree != null)
            {
                tree.RemoveAttribute(id);
                entity.WatchedAttributes.MarkPathDirty(PersistKey);
            }
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

            entity.WatchedAttributes.RemoveAttribute(PersistKey);
        }

        public void Suspend()
        {
            if (active.Count == 0)
                return;

            ITreeAttribute tree = entity.WatchedAttributes.GetTreeAttribute(PersistKey);
            long nowMs = entity.World.ElapsedMilliseconds;

            foreach (KeyValuePair<string, ActiveEffect> pair in active)
            {
                ActiveEffect activeEffect = pair.Value;

                if (activeEffect.IsTicking)
                    entity.World.UnregisterGameTickListener(activeEffect.ListenerId);
                else
                    entity.World.UnregisterCallback(activeEffect.ListenerId);

                int remainingSec =
                    activeEffect.Effect.Context.Duration
                    - (int)((nowMs - activeEffect.ApplyMs) / 1000);
                tree?.GetTreeAttribute(pair.Key)?.SetInt("remainingSec", Math.Max(remainingSec, 1));
            }

            if (tree != null)
                entity.WatchedAttributes.MarkPathDirty(PersistKey);

            active.Clear();
        }

        public void RestoreEffects()
        {
            ITreeAttribute tree = entity.WatchedAttributes.GetTreeAttribute(PersistKey);
            if (tree != null)
            {
                foreach (string id in tree.Select(pair => pair.Key).ToList())
                {
                    ITreeAttribute record = tree.GetTreeAttribute(id);
                    int remainingSec = record?.GetInt("remainingSec") ?? 0;
                    PotionContext ctx =
                        remainingSec > 0
                            ? PotionRegistry.BuildPotionDef(id, record.GetFloat("strengthMul", 1f))
                            : null;

                    if (ctx == null || ctx.Duration <= 0)
                    {
                        tree.RemoveAttribute(id);
                        continue;
                    }

                    ctx.Duration = Math.Min(remainingSec, ctx.Duration);

                    entity.WatchedAttributes.RemoveAttribute(id);

                    if (ctx.CanFly)
                        originalFreeMove = record.GetBool("origFreeMove");

                    if (!TryApplyPotion(id, ctx, record.GetString("name", ""), resume: true))
                        tree.RemoveAttribute(id);
                }

                entity.WatchedAttributes.MarkPathDirty(PersistKey);
            }

            CleanStaleState();
        }

        private void CleanStaleState()
        {
            List<string> staleAttributes =
            [
                .. entity.WatchedAttributes.Keys.Where(key =>
                    key.EndsWith("potionid", StringComparison.OrdinalIgnoreCase)
                    && !active.ContainsKey(key)
                ),
            ];

            foreach (string attr in staleAttributes)
            {
                entity.WatchedAttributes.RemoveAttribute(attr);
            }

            if (!active.ContainsKey("glowpotionid"))
                entity.WatchedAttributes.RemoveAttribute("glowStrength");

            float sizeDelta = entity.WatchedAttributes.GetFloat("potionSizeDelta", 0f);
            if (Math.Abs(sizeDelta) < 0.001f)
                UtilityEffects.ResetPlayerSize(entity);
        }

        private void SaveEffectRecord(string id, PotionContext ctx, string name)
        {
            ITreeAttribute tree = entity.WatchedAttributes.GetOrAddTreeAttribute(PersistKey);
            ITreeAttribute record = tree.GetOrAddTreeAttribute(id);
            record.SetString("name", name ?? "");
            record.SetFloat("strengthMul", ctx.StrengthMul);
            record.SetInt("remainingSec", ctx.Duration);
            record.SetLong("appliedAt", entity.World.ElapsedMilliseconds);
            if (ctx.CanFly)
                record.SetBool("origFreeMove", originalFreeMove);
            entity.WatchedAttributes.MarkPathDirty(PersistKey);
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
        public long ApplyMs;
    }
}
