using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace EffectLib
{
    /// <summary>
    /// WatchedAttributes keys that <see cref="EffectManager"/> writes. Harmony patches and
    /// client-side code in any mod may read these to react to an active capability.
    /// </summary>
    public static class EffectAttr
    {
        public const string WaterBreathe = "effectlib:waterBreathe";
        public const string ColdResist = "effectlib:coldResist";
        public const string CanFly = "effectlib:canFly";
        public const string CanClimb = "effectlib:canClimb";
        public const string GlowStrength = "effectlib:glowStrength";
        public const string NoGravity = "effectlib:noGravity";
    }

    /// <summary>
    /// Tracks the effects running on one player: applies them, expires them, persists them
    /// across disconnects and keeps the derived entity properties in sync.
    /// Server side only - obtained via <see cref="EntityBehaviorPlayerEffects.Manager"/>.
    /// </summary>
    public sealed class EffectManager(EntityPlayer entity)
    {
        // Persisted under this key on the player. Kept as "alchemyEffects" for save
        // compatibility with Alchemy 2.x, which stored effects here before EffectLib existed.
        public const string PersistKey = "alchemyEffects";

        private readonly EntityPlayer entity = entity;
        private readonly ICoreAPI api = entity.Api;
        private readonly Dictionary<string, ActiveEffect> active = [];

        // Instant effects never enter 'active' or the saved tree, so a short in-memory lock is
        // what stops a single use being consumed twice. Server side only, never persisted.
        private readonly HashSet<string> pendingInstant = [];

        // Every entity-property capability is restored from a baseline captured the first time
        // it is touched, so RefreshEffectState can be re-run at any point (apply, remove, resume)
        // without drifting. Never mutate these properties outside a Sync* method.
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

        /// <summary>Ids of every effect currently running, for callers that want to inspect state.</summary>
        public IReadOnlyCollection<string> ActiveIds => active.Keys;

        /// <summary>Snapshot of what is running, for commands and other read-only inspection.</summary>
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

        /// <summary>
        /// Whether re-applying <paramref name="id"/> should restart it. Ticking health effects
        /// are never refreshed, because the vanilla health system owns their remaining ticks
        /// and restarting would stack a second damage-over-time on top.
        /// </summary>
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

                // One-shot side effects belong to the owning mod, and must not fire again when
                // an effect is resumed from a save.
                if (!resume)
                {
                    EffectHandlers.Applied(entity, id, ctx, api.Logger);
                }

                // Continuous capabilities (fly, climb, fall, knockback, weight, gravity, glow...)
                // are not applied here - RefreshEffectState below recomputes them from every
                // active effect at once, which keeps stacking and removal symmetric.

                if (effect.Context.Duration == 0)
                {
                    // Instant effect: hold a brief lock so a single use cannot be consumed twice.
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
                    // An endless damage/heal over time: the engine's ticking damage source needs
                    // a finite window, so drive it ourselves, one health tick per interval.
                    ticking = true;
                    handle = entity.World.RegisterGameTickListener(
                        _ => effect.ApplyHealthTick(entity),
                        (int)Math.Max(1, effect.Context.TickSec * 1000)
                    );
                }
                else if (effect.Context.RepeatSec > 0)
                {
                    // Repeating one-shots need a listener that re-fires and, unless endless, expires.
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
                // An endless continuous effect (fly, glow, a stat) needs no listener at all -
                // it is reconciled by RefreshEffectState and only ever leaves on death, logout
                // without retention, or an explicit removal.

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
                // Probably don't need a try catch but will leave this here just in case
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

        // Fires the one-shot parts again for a repeating effect, and expires it once its
        // duration is up. Only used when EffectContext.RepeatSec is set.
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

        /// <summary>
        /// Clears the effects <paramref name="scope"/> covers and tells handlers, so they can
        /// undo lasting state for those domains too. Effects outside the scope keep running,
        /// which is what stops one mod's purge from wiping another mod's effects.
        /// </summary>
        public void Purge(EffectPurge scope)
        {
            scope ??= EffectPurge.Everything;

            foreach (string id in active.Keys.Where(scope.Covers).ToList())
            {
                RemoveEffect(id);
            }

            if (scope.IsEverything)
            {
                // Nothing can be left behind, so drop the whole tree rather than record by record.
                RemoveIfPresent(PersistKey);
                pendingInstant.Clear();
            }

            RefreshEffectState();
            EffectHandlers.Cleared(entity, scope, api.Logger);
        }

        /// <summary>Clears every effect from every mod. Used on death and on login/disconnect
        /// when effects are not retained.</summary>
        public void ResetAll() => Purge(EffectPurge.Everything);

        /// <summary>
        /// Clears effects the way <paramref name="ctx"/> asks. Defaults to the domain that owns
        /// <paramref name="effectId"/>, so a purging item only clears its own mod's effects
        /// unless it names others.
        /// </summary>
        public void PurgeFor(string effectId, EffectContext ctx)
        {
            IEnumerable<string> domains =
                ctx.ResetDomains.Count > 0
                    ? ctx.ResetDomains
                    : [EffectRegistry.DomainOf(effectId)];

            Purge(new EffectPurge(domains, ctx.ResetEffectIds));
        }

        /// <summary>
        /// Stops the running timers but keeps the persisted records, recording how much time
        /// each effect has left. Used on disconnect so effects survive to the next login.
        /// </summary>
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

                // An endless effect is stored as such so it resumes endless, not clamped to a
                // remaining time it never had.
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

        /// <summary>Restarts effects saved by <see cref="Suspend"/> (or left behind by a crash).</summary>
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
                        // A primitive carries nothing but id + potency through the registry, so
                        // its length is whatever the save says - the builder's default is not it.
                        ctx.Duration = remainingSec;
                    }
                    else if (ctx.Duration <= 0)
                    {
                        // A registered effect that no longer defines a duration for itself.
                        tree.RemoveAttribute(id);
                        continue;
                    }
                    else
                    {
                        // Resume with the lesser of the saved remaining time and the duration the
                        // effect currently defines, so a shortened config takes effect on resume.
                        ctx.Duration = Math.Min(remainingSec, ctx.Duration);
                    }

                    // The repeat shape of a primitive (a DoT's interval and damage type) is not
                    // reconstructible from its id, so it comes back from the record.
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
            // The saved tree is the only record of what is running, so anything in it that did
            // not restart above is already gone by this point. Nothing else to reconcile.
            RemoveIfPresent(LegacyGlowStrength);
            CleanLegacyHandleKeys();

            RefreshEffectState();
            EffectHandlers.Restored(entity, api.Logger);
        }

        /// <summary>
        /// Deletes the flat per-effect keys Alchemy 2.x wrote alongside the effects tree. They
        /// are no longer written, so this only ever finds something on the first login after
        /// upgrading, and removing them is skipped entirely when there are none - a top-level
        /// removal forces a full attribute resync on the client.
        /// </summary>
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

        // Alchemy 2.x wrote the glow level to a bare "glowStrength" key. Clear it on login so
        // an upgraded save does not leave players permanently glowing.
        private const string LegacyGlowStrength = "glowStrength";

        /// <summary>
        /// Recomputes every continuous capability from all active effects and writes the
        /// result. Safe to call at any time - it is the only thing allowed to touch the
        /// entity properties it owns.
        /// </summary>
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
                // Strongest wins rather than stacking, so two fall effects cannot overwrite baseline
                if (allowFall && ctx.FallDamageReduction > fallDamageReduction)
                    fallDamageReduction = ctx.FallDamageReduction;
                // These are offsets from the entity's baseline, so they do stack
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

        // Applies an additive offset on top of a baseline captured on first use, so repeated
        // refreshes are idempotent and the baseline is restored once nothing requests an offset.
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

            // A registered effect rebuilds its own repeat shape from its builder on resume; a
            // primitive has no builder state, so its repeat shape is persisted here.
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

        // Reapplies the interval/damage-type a repeating primitive was given, from its save
        // record - the id alone cannot reconstruct it.
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

    /// <summary>A running effect as seen from outside <see cref="EffectManager"/>.</summary>
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
