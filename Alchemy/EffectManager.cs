using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace Alchemy
{
    // WatchedAttributes keys that RefreshEffectState writes and the Harmony patches read
    public static class EffectAttr
    {
        public const string WaterBreathe = "effectlib:waterBreathe";
        public const string ColdResist = "effectlib:coldResist";
        public const string CanFly = "effectlib:canFly";
        public const string CanClimb = "effectlib:canClimb";
        public const string GlowStrength = "effectlib:glowStrength";
    }

    public sealed class EffectManager(EntityPlayer entity)
    {
        private const string PersistKey = "alchemyEffects";

        private readonly EntityPlayer entity = entity;
        private readonly ICoreAPI api = entity.Api;
        private readonly Dictionary<string, ActiveEffect> active = [];

        private float? baselineFallDamageMultiplier;
        private bool? baselineCanClimbAnywhere;
        private bool? baselineFreeMove;

        public bool IsActive(string id) => active.ContainsKey(id);

        public bool HasAnyActive => active.Count > 0;

        public bool CanRefresh(string id) =>
            AlchemyConfig.Loaded.AllowPotionRefresh
            && active.TryGetValue(id, out ActiveEffect activeEffect)
            && !(
                activeEffect.Effect.Context.TickSec > 0
                && Math.Abs(activeEffect.Effect.Context.Health) > float.Epsilon
            );

        public bool TryApply(string id, EffectContext ctx, string name, bool resume = false)
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

                AppliedEffect effect = new(id, ctx);
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
                if (!resume && Math.Abs(effect.Context.SizeChange) > float.Epsilon)
                {
                    UtilityEffects.ApplySizeChange(entity, effect.Context.SizeChange);
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
                RefreshEffectState();
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
                    Lang.Get("alchemy:effect-lose", activeEffect.DisplayName),
                    EnumChatType.Notification
                );
            }

            if (activeEffect.IsTicking)
                entity.World.UnregisterGameTickListener(activeEffect.ListenerId);
            else
                entity.World.UnregisterCallback(activeEffect.ListenerId);

            activeEffect.Effect.Remove(entity);

            active.Remove(id);
            entity.WatchedAttributes.RemoveAttribute(id);

            ITreeAttribute tree = entity.WatchedAttributes.GetTreeAttribute(PersistKey);
            if (tree != null)
            {
                tree.RemoveAttribute(id);
                entity.WatchedAttributes.MarkPathDirty(PersistKey);
            }

            RefreshEffectState();
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

            RefreshEffectState();
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

            RefreshEffectState();
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
                    EffectContext ctx =
                        remainingSec > 0
                            ? EffectRegistry.Build(id, record.GetFloat("strengthMul", 1f))
                            : null;

                    if (ctx == null || ctx.Duration <= 0)
                    {
                        tree.RemoveAttribute(id);
                        continue;
                    }

                    ctx.Duration = Math.Min(remainingSec, ctx.Duration);

                    entity.WatchedAttributes.RemoveAttribute(id);

                    if (ctx.CanFly)
                        baselineFreeMove ??= record.GetBool("origFreeMove");

                    if (!TryApply(id, ctx, record.GetString("name", ""), resume: true))
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

            entity.WatchedAttributes.RemoveAttribute(LegacyGlowStrength);

            RefreshEffectState();

            float sizeDelta = entity.WatchedAttributes.GetFloat("potionSizeDelta", 0f);
            if (Math.Abs(sizeDelta) < 0.001f)
                UtilityEffects.ResetPlayerSize(entity);
        }

        // Not sure if I will keep this but this is to stop old named glowStrength from being stuck in attributes
        private const string LegacyGlowStrength = "glowStrength";

        private void RefreshEffectState()
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;
            bool waterBreathe = false;
            bool coldResist = false;
            bool canFly = false;
            bool canClimb = false;
            int glowStrength = 0;
            float fallDamageReduction = 0f;

            foreach (ActiveEffect activeEffect in active.Values)
            {
                EffectContext ctx = activeEffect.Effect.Context;
                waterBreathe |= ctx.WaterBreathe;
                coldResist |= ctx.ColdResist;
                canFly |= ctx.CanFly && cfg.AllowFlightPotion;
                canClimb |= ctx.CanClimbAnywhere && cfg.AllowClimbPotion;
                if (ctx.GlowStrength > glowStrength)
                    glowStrength = ctx.GlowStrength;
                // Strongest wins rather than stacking, so two fall potions cannot overwrite baseline
                if (cfg.AllowFallPotion && ctx.FallDamageReduction > fallDamageReduction)
                    fallDamageReduction = ctx.FallDamageReduction;
            }

            SetOrRemoveBool(EffectAttr.WaterBreathe, waterBreathe);
            SetOrRemoveBool(EffectAttr.ColdResist, coldResist);
            SetOrRemoveBool(EffectAttr.CanFly, canFly);
            SetOrRemoveBool(EffectAttr.CanClimb, canClimb);

            if (glowStrength > 0)
                entity.WatchedAttributes.SetInt(EffectAttr.GlowStrength, glowStrength);
            else
                entity.WatchedAttributes.RemoveAttribute(EffectAttr.GlowStrength);

            SyncFallDamage(fallDamageReduction);
            SyncClimbing(canClimb);
            SyncFreeMove(canFly);
        }

        private void SyncFallDamage(float reduction)
        {
            if (reduction > 0f)
            {
                baselineFallDamageMultiplier ??= entity.Properties.FallDamageMultiplier;
                entity.Properties.FallDamageMultiplier = 1f - Math.Min(reduction, 1f);
            }
            else if (baselineFallDamageMultiplier.HasValue)
            {
                entity.Properties.FallDamageMultiplier = baselineFallDamageMultiplier.Value;
                baselineFallDamageMultiplier = null;
            }
        }

        private void SyncClimbing(bool canClimb)
        {
            if (canClimb)
            {
                baselineCanClimbAnywhere ??= entity.Properties.CanClimbAnywhere;
                entity.Properties.CanClimbAnywhere = true;
            }
            else if (baselineCanClimbAnywhere.HasValue)
            {
                entity.Properties.CanClimbAnywhere = baselineCanClimbAnywhere.Value;
                baselineCanClimbAnywhere = null;
            }
        }

        private void SyncFreeMove(bool canFly)
        {
            if (entity?.Player is not IServerPlayer flyPlayer)
                return;

            bool target;

            if (canFly)
            {
                baselineFreeMove ??= flyPlayer.WorldData.FreeMove;
                target = true;
            }
            else
            {
                if (!baselineFreeMove.HasValue)
                    return;

                target = baselineFreeMove.Value;
                baselineFreeMove = null;
            }

            if (flyPlayer.WorldData.FreeMove == target)
                return;

            flyPlayer.WorldData.FreeMove = target;
            flyPlayer.BroadcastPlayerData();
        }

        private void SetOrRemoveBool(string key, bool value)
        {
            if (value)
                entity.WatchedAttributes.SetBool(key, true);
            else
                entity.WatchedAttributes.RemoveAttribute(key);
        }

        private void SaveEffectRecord(string id, EffectContext ctx, string name)
        {
            ITreeAttribute tree = entity.WatchedAttributes.GetOrAddTreeAttribute(PersistKey);
            ITreeAttribute record = tree.GetOrAddTreeAttribute(id);
            record.SetString("name", name ?? "");
            record.SetFloat("strengthMul", ctx.PotencyMul);
            record.SetInt("remainingSec", ctx.Duration);
            record.SetLong("appliedAt", entity.World.ElapsedMilliseconds);
            if (ctx.CanFly)
                record.SetBool("origFreeMove", baselineFreeMove ?? false);
            entity.WatchedAttributes.MarkPathDirty(PersistKey);
        }
    }

    internal sealed record ActiveEffect(
        AppliedEffect Effect,
        long ListenerId,
        bool IsTicking,
        string DisplayName
    )
    {
        public int Elapsed;
        public long ApplyMs;
    }
}
