using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace EffectLib
{
    public static class EffectAttr
    {
        public const string WaterBreathe = "effectlib:waterBreathe";
        public const string ColdResist = "effectlib:coldResist";
        public const string CanFly = "effectlib:canFly";
        public const string CanClimb = "effectlib:canClimb";
        public const string GlowStrength = "effectlib:glowStrength";
        public const string NoGravity = "effectlib:noGravity";
    }

    public sealed class EffectManager(EntityPlayer entity)
    {
        public const string PersistKey = "alchemyEffects";

        private readonly EntityPlayer entity = entity;
        private readonly ICoreAPI api = entity.Api;
        private readonly Dictionary<string, ActiveEffect> active = [];

        private readonly HashSet<string> pendingInstant = [];

        private float? baselineFallDamageMultiplier;
        private bool? baselineFallDamage;
        private bool? baselineCanClimbAnywhere;
        private bool? baselineCanClimb;
        private bool? baselineFreeMove;
        private EnumHabitat? baselineHabitat;
        private float? baselineKnockbackResistance;
        private float? baselineClimbTouchDistance;
        private float? baselineWeight;

        public bool IsActive(string id) => active.ContainsKey(id);

        public bool HasAnyActive => active.Count > 0;

        public IReadOnlyCollection<string> ActiveIds => active.Keys;

        public List<ActiveEffectInfo> GetActiveEffects()
        {
            long nowMs = entity.World.ElapsedMilliseconds;

            return
            [
                .. active.Select(pair => new ActiveEffectInfo(
                    pair.Key,
                    pair.Value.DisplayName,
                    pair.Value.Effect.Context.IsEndless
                        ? 0
                        : Math.Max(
                            0,
                            pair.Value.Effect.Context.Duration
                                - (int)((nowMs - pair.Value.ApplyMs) / 1000)
                        ),
                    pair.Value.Effect.Context.PotencyMul,
                    pair.Value.Effect.Context.IsEndless
                )),
            ];
        }

        public bool CanRefresh(string id) =>
            EffectPolicy.IsAllowed(EffectCapability.Refresh)
            && active.TryGetValue(id, out ActiveEffect activeEffect)
            && !(
                activeEffect.Effect.Context.TickSec > 0
                && Math.Abs(activeEffect.Effect.Context.Health) > float.Epsilon
            );

        public bool TryApply(string id, EffectContext ctx, string name, bool resume = false)
        {
            try
            {
                if (IsActive(id) || pendingInstant.Contains(id))
                {
                    if (CanRefresh(id))
                    {
                        RemoveEffect(id, false);
                    }
                    else
                    {
                        api.Logger.Debug(
                            "Cannot apply effect {0}, it is currently already applied!",
                            id
                        );
                        return false;
                    }
                }

                AppliedEffect effect = new(id, ctx);
                effect.Apply(entity, resume);

                if (!resume)
                {
                    EffectHandlers.Applied(entity, id, ctx, api.Logger);
                }

                if (effect.Context.Duration == 0)
                {
                    pendingInstant.Add(id);
                    entity.World.RegisterCallback(dt => pendingInstant.Remove(id), 500);
                    return true;
                }

                bool endless = effect.Context.IsEndless;
                bool tickingHealth =
                    effect.Context.TickSec > 0 && Math.Abs(effect.Context.Health) > float.Epsilon;

                long handle = 0;
                bool ticking = false;

                if (endless && tickingHealth)
                {
                    ticking = true;
                    handle = entity.World.RegisterGameTickListener(
                        _ => effect.ApplyHealthTick(entity),
                        (int)Math.Max(1, effect.Context.TickSec * 1000)
                    );
                }
                else if (effect.Context.RepeatSec > 0)
                {
                    ticking = true;
                    handle = entity.World.RegisterGameTickListener(
                        _ => RepeatTick(id),
                        (int)Math.Max(1, effect.Context.RepeatSec * 1000)
                    );
                }
                else if (!endless)
                {
                    handle = entity.World.RegisterCallback(
                        dt => RemoveEffect(id),
                        effect.Context.Duration * 1000
                    );
                }

                active[id] = new ActiveEffect(effect, handle, ticking, name)
                {
                    ApplyMs = entity.World.ElapsedMilliseconds,
                };
                RefreshEffectState();
                SaveEffectRecord(id, ctx, name);

                return true;
            }
            catch (Exception err)
            {
                api.Logger.Error("Effect {0} could not be applied. An error occurred", id);
                api.Logger.Error(err);
                return false;
            }
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
                    EffectLang.Get(id, "effect-lose", activeEffect.DisplayName),
                    EnumChatType.Notification
                );
            }

            if (activeEffect.ListenerId != 0)
            {
                if (activeEffect.IsTicking)
                    entity.World.UnregisterGameTickListener(activeEffect.ListenerId);
                else
                    entity.World.UnregisterCallback(activeEffect.ListenerId);
            }

            activeEffect.Effect.Remove(entity);

            active.Remove(id);
            pendingInstant.Remove(id);

            ITreeAttribute tree = entity.WatchedAttributes.GetTreeAttribute(PersistKey);
            if (tree != null)
            {
                tree.RemoveAttribute(id);
                entity.WatchedAttributes.MarkPathDirty(PersistKey);
            }

            EffectHandlers.Removed(entity, id, activeEffect.Effect.Context, api.Logger);

            RefreshEffectState();
        }

        private void RepeatTick(string id)
        {
            if (!active.TryGetValue(id, out ActiveEffect activeEffect))
                return;

            EffectContext ctx = activeEffect.Effect.Context;
            if (!ctx.IsEndless)
            {
                int elapsedSec = (int)(
                    (entity.World.ElapsedMilliseconds - activeEffect.ApplyMs) / 1000
                );
                if (elapsedSec >= ctx.Duration)
                {
                    RemoveEffect(id);
                    return;
                }
            }

            EffectHandlers.Applied(entity, id, ctx, api.Logger);
        }

        public void Purge(EffectPurge scope)
        {
            scope ??= EffectPurge.Everything;

            foreach (string id in active.Keys.Where(scope.Covers).ToList())
            {
                RemoveEffect(id);
            }

            if (scope.IsEverything)
            {
                RemoveIfPresent(PersistKey);
                pendingInstant.Clear();
            }

            RefreshEffectState();
            EffectHandlers.Cleared(entity, scope, api.Logger);
        }

        public void ResetAll() => Purge(EffectPurge.Everything);

        public void PurgeFor(string effectId, EffectContext ctx)
        {
            IEnumerable<string> domains =
                ctx.ResetDomains.Count > 0
                    ? ctx.ResetDomains
                    : [EffectRegistry.DomainOf(effectId)];

            Purge(new EffectPurge(domains, ctx.ResetEffectIds));
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

                if (activeEffect.ListenerId != 0)
                {
                    if (activeEffect.IsTicking)
                        entity.World.UnregisterGameTickListener(activeEffect.ListenerId);
                    else
                        entity.World.UnregisterCallback(activeEffect.ListenerId);
                }

                int remainingSec = activeEffect.Effect.Context.IsEndless
                    ? EffectContext.EndlessDuration
                    : Math.Max(
                        activeEffect.Effect.Context.Duration
                            - (int)((nowMs - activeEffect.ApplyMs) / 1000),
                        1
                    );
                tree?.GetTreeAttribute(pair.Key)?.SetInt("remainingSec", remainingSec);
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
                    bool endless = remainingSec == EffectContext.EndlessDuration;
                    EffectContext ctx =
                        remainingSec > 0 || endless
                            ? EffectRegistry.Build(id, record.GetFloat("strengthMul", 1f))
                            : null;

                    if (ctx == null)
                    {
                        tree.RemoveAttribute(id);
                        continue;
                    }

                    bool primitive = EffectPrimitives.IsPrimitiveId(id);

                    if (endless || ctx.IsEndless)
                    {
                        ctx.Duration = EffectContext.EndlessDuration;
                    }
                    else if (primitive)
                    {
                        ctx.Duration = remainingSec;
                    }
                    else if (ctx.Duration <= 0)
                    {
                        tree.RemoveAttribute(id);
                        continue;
                    }
                    else
                    {
                        ctx.Duration = Math.Min(remainingSec, ctx.Duration);
                    }

                    if (primitive)
                        RestoreRepeatShape(ctx, record);

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
            RemoveIfPresent(LegacyGlowStrength);
            CleanLegacyHandleKeys();

            RefreshEffectState();
            EffectHandlers.Restored(entity, api.Logger);
        }

        private void CleanLegacyHandleKeys()
        {
            List<string> legacy =
            [
                .. entity.WatchedAttributes.Keys.Where(key =>
                    key != PersistKey
                    && key.EndsWith("potionid", StringComparison.OrdinalIgnoreCase)
                ),
            ];

            foreach (string key in legacy)
            {
                entity.WatchedAttributes.RemoveAttribute(key);
            }

            if (legacy.Count > 0)
                api.Logger.Notification(
                    "[EffectLib] Cleared {0} legacy effect key(s) from {1}.",
                    legacy.Count,
                    entity.Player?.PlayerName ?? entity.EntityId.ToString()
                );
        }

        private const string LegacyGlowStrength = "glowStrength";

        private void RefreshEffectState()
        {
            bool allowFly = EffectPolicy.IsAllowed(EffectCapability.Fly);
            bool allowClimb = EffectPolicy.IsAllowed(EffectCapability.Climb);
            bool allowFall = EffectPolicy.IsAllowed(EffectCapability.Fall);

            bool waterBreathe = false;
            bool coldResist = false;
            bool canFly = false;
            bool canClimb = false;
            int glowStrength = 0;
            float fallDamageReduction = 0f;
            bool noFallDamage = false;
            bool disableClimbing = false;
            bool noGravity = false;
            float knockbackResistance = 0f;
            float climbTouchDistance = 0f;
            float weight = 0f;

            foreach (ActiveEffect activeEffect in active.Values)
            {
                EffectContext ctx = activeEffect.Effect.Context;
                waterBreathe |= ctx.WaterBreathe;
                coldResist |= ctx.ColdResist;
                canFly |= ctx.CanFly && allowFly;
                canClimb |= ctx.CanClimbAnywhere && allowClimb;
                noFallDamage |= ctx.NoFallDamage && allowFall;
                disableClimbing |= ctx.DisableClimbing && allowClimb;
                noGravity |= ctx.NoGravity && allowFly;
                if (ctx.GlowStrength > glowStrength)
                    glowStrength = ctx.GlowStrength;
                if (allowFall && ctx.FallDamageReduction > fallDamageReduction)
                    fallDamageReduction = ctx.FallDamageReduction;
                knockbackResistance += ctx.KnockbackResistance;
                if (allowClimb)
                    climbTouchDistance += ctx.ClimbTouchDistance;
                weight += ctx.Weight;
            }

            SetEffectBool(EffectAttr.WaterBreathe, waterBreathe);
            SetEffectBool(EffectAttr.ColdResist, coldResist);
            SetEffectBool(EffectAttr.CanFly, canFly);
            SetEffectBool(EffectAttr.CanClimb, canClimb);
            SetEffectBool(EffectAttr.NoGravity, noGravity);
            SetEffectInt(EffectAttr.GlowStrength, glowStrength);

            SyncFallDamage(fallDamageReduction);
            SyncClimbing(canClimb);
            SyncFreeMove(canFly);
            SyncNoFallDamage(noFallDamage);
            SyncDisableClimbing(disableClimbing);
            SyncNoGravity(noGravity);
            SyncOffset(
                ref baselineKnockbackResistance,
                knockbackResistance,
                () => entity.Properties.KnockbackResistance,
                value => entity.Properties.KnockbackResistance = value
            );
            SyncOffset(
                ref baselineClimbTouchDistance,
                climbTouchDistance,
                () => entity.Properties.ClimbTouchDistance,
                value => entity.Properties.ClimbTouchDistance = value
            );
            SyncOffset(
                ref baselineWeight,
                weight,
                () => entity.Properties.Weight,
                value => entity.Properties.Weight = value
            );
        }

        private static void SyncOffset(
            ref float? baseline,
            float total,
            Func<float> read,
            Action<float> write
        )
        {
            if (Math.Abs(total) > float.Epsilon)
            {
                baseline ??= read();
                write(baseline.Value + total);
            }
            else if (baseline.HasValue)
            {
                write(baseline.Value);
                baseline = null;
            }
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

        private void SyncNoFallDamage(bool noFallDamage)
        {
            if (noFallDamage)
            {
                baselineFallDamage ??= entity.Properties.FallDamage;
                entity.Properties.FallDamage = false;
            }
            else if (baselineFallDamage.HasValue)
            {
                entity.Properties.FallDamage = baselineFallDamage.Value;
                baselineFallDamage = null;
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

        private void SyncDisableClimbing(bool disableClimbing)
        {
            if (disableClimbing)
            {
                baselineCanClimb ??= entity.Properties.CanClimb;
                entity.Properties.CanClimb = false;
            }
            else if (baselineCanClimb.HasValue)
            {
                entity.Properties.CanClimb = baselineCanClimb.Value;
                baselineCanClimb = null;
            }
        }

        private void SyncNoGravity(bool noGravity)
        {
            if (noGravity)
            {
                baselineHabitat ??= entity.Properties.Habitat;
                entity.Properties.Habitat = EnumHabitat.Air;
            }
            else if (baselineHabitat.HasValue)
            {
                entity.Properties.Habitat = baselineHabitat.Value;
                baselineHabitat = null;
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

        private void SetEffectBool(string key, bool value)
        {
            if (!value && !entity.WatchedAttributes.HasAttribute(key))
                return;
            if (entity.WatchedAttributes.GetBool(key) != value)
                entity.WatchedAttributes.SetBool(key, value);
        }

        private void SetEffectInt(string key, int value)
        {
            if (value == 0 && !entity.WatchedAttributes.HasAttribute(key))
                return;
            if (entity.WatchedAttributes.GetInt(key) != value)
                entity.WatchedAttributes.SetInt(key, value);
        }

        private void RemoveIfPresent(string key)
        {
            if (entity.WatchedAttributes.HasAttribute(key))
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

            if (EffectPrimitives.IsPrimitiveId(id))
            {
                if (ctx.RepeatSec > 0f)
                    record.SetFloat("repeatSec", ctx.RepeatSec);
                if (ctx.TickSec > 0f)
                    record.SetFloat("tickSec", ctx.TickSec);
                if (ctx.DamageType.HasValue)
                    record.SetString("damageType", ctx.DamageType.Value.ToString());
            }

            entity.WatchedAttributes.MarkPathDirty(PersistKey);
        }

        private static void RestoreRepeatShape(EffectContext ctx, ITreeAttribute record)
        {
            float repeatSec = record.GetFloat("repeatSec");
            if (repeatSec > 0f)
                ctx.RepeatSec = repeatSec;

            float tickSec = record.GetFloat("tickSec");
            if (tickSec > 0f)
                ctx.TickSec = tickSec;

            if (
                record.HasAttribute("damageType")
                && Enum.TryParse(record.GetString("damageType"), true, out EnumDamageType dt)
            )
                ctx.DamageType = dt;
        }
    }

    public sealed record ActiveEffectInfo(
        string Id,
        string DisplayName,
        int RemainingSec,
        float PotencyMul,
        bool Endless = false
    );

    internal sealed record ActiveEffect(
        AppliedEffect Effect,
        long ListenerId,
        bool IsTicking,
        string DisplayName
    )
    {
        public long ApplyMs;
    }
}
