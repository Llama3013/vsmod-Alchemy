using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace EffectLib
{
    public interface IEffectHandler
    {
        void OnApplied(EntityPlayer entity, string effectId, EffectContext ctx);

        void OnRemoved(EntityPlayer entity, string effectId, EffectContext ctx);

        void OnCleared(EntityPlayer entity, EffectPurge scope);

        void OnRestored(EntityPlayer entity);
    }

    public static class EffectHandlers
    {
        private static readonly List<IEffectHandler> handlers = [];

        public static void Register(IEffectHandler handler)
        {
            if (handler != null && !handlers.Contains(handler))
                handlers.Add(handler);
        }

        public static void Unregister(IEffectHandler handler)
        {
            if (handler != null)
                handlers.Remove(handler);
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

        private static void Dispatch(Action<IEffectHandler> call, ILogger logger)
        {
            foreach (IEffectHandler handler in handlers)
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
