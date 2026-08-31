using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace EffectLib
{
    /// <summary>
    /// Extension point for behaviour EffectLib cannot express on its own. EffectLib applies
    /// stat modifiers, health, the entity-property capabilities and the built-in utility effects
    /// (teleport, reshape, nutrition, temporal stability, size - see
    /// <see cref="UtilityEffectHandler"/>) itself. This interface is for anything beyond that a
    /// content or code mod wants an effect to do, registered by the owning mod.
    /// </summary>
    public interface IEffectHandler
    {
        /// <summary>
        /// A new effect has started on <paramref name="entity"/>. Not called when an effect is
        /// resumed from a save, so one-shot actions do not fire again on every login.
        /// </summary>
        void OnApplied(EntityPlayer entity, string effectId, EffectContext ctx);

        /// <summary>An effect has ended, whether by expiry, refresh or removal.</summary>
        void OnRemoved(EntityPlayer entity, string effectId, EffectContext ctx);

        /// <summary>
        /// Effects have been cleared in bulk - on death, on login/disconnect when effects are
        /// not retained, or by an effect that purges others. Handlers should undo lasting state
        /// they own, but only for domains <paramref name="scope"/> actually covers.
        /// </summary>
        void OnCleared(EntityPlayer entity, EffectPurge scope);

        /// <summary>
        /// Persisted effects have been resumed on login and stale state cleaned up. Handlers
        /// should re-establish or repair state they own to match what actually resumed.
        /// </summary>
        void OnRestored(EntityPlayer entity);
    }

    /// <summary>Registry of <see cref="IEffectHandler"/>s. Handlers are called in registration order.</summary>
    public static class EffectHandlers
    {
        private static readonly List<IEffectHandler> handlers = [];
        private static readonly object sync = new();

        /// <summary>
        /// How many handlers are registered, including EffectLib's own built-in
        /// <see cref="UtilityEffectHandler"/>. Never zero once <c>ModSystem.EffectLibMod</c> has
        /// started.
        /// </summary>
        public static int Count
        {
            get
            {
                lock (sync)
                {
                    return handlers.Count;
                }
            }
        }

        public static void Register(IEffectHandler handler)
        {
            if (handler == null)
                return;

            lock (sync)
            {
                if (!handlers.Contains(handler))
                    handlers.Add(handler);
            }
        }

        public static void Unregister(IEffectHandler handler)
        {
            if (handler == null)
                return;

            lock (sync)
            {
                handlers.Remove(handler);
            }
        }

        internal static void Applied(
            EntityPlayer entity,
            string effectId,
            EffectContext ctx,
            ILogger logger
        ) => Dispatch(h => h.OnApplied(entity, effectId, ctx), logger);

        internal static void Removed(
            EntityPlayer entity,
            string effectId,
            EffectContext ctx,
            ILogger logger
        ) => Dispatch(h => h.OnRemoved(entity, effectId, ctx), logger);

        internal static void Cleared(EntityPlayer entity, EffectPurge scope, ILogger logger) =>
            Dispatch(h => h.OnCleared(entity, scope), logger);

        internal static void Restored(EntityPlayer entity, ILogger logger) =>
            Dispatch(h => h.OnRestored(entity), logger);

        // One failing handler must not stop the others, nor abort the effect itself.
        private static void Dispatch(Action<IEffectHandler> call, ILogger logger)
        {
            IEffectHandler[] snapshot;
            lock (sync)
            {
                if (handlers.Count == 0)
                    return;
                snapshot = [.. handlers];
            }

            foreach (IEffectHandler handler in snapshot)
            {
                try
                {
                    call(handler);
                }
                catch (Exception err)
                {
                    logger?.Error(
                        "[EffectLib] Effect handler {0} threw. Continuing with the remaining handlers.",
                        handler.GetType().FullName
                    );
                    logger?.Error(err);
                }
            }
        }
    }
}
