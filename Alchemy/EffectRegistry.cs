using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Alchemy
{
    public delegate void EffectBuilder(EffectContext ctx);

    public static class EffectRegistry
    {
        private static readonly ConcurrentDictionary<string, EffectBuilder> builders = new();

        public static IReadOnlyDictionary<string, EffectBuilder> Builders => builders;

        public static void Register(string effectId, EffectBuilder builder)
        {
            if (string.IsNullOrWhiteSpace(effectId) || builder == null)
                return;
            builders[effectId] = builder;
        }

        public static bool IsRegistered(string effectId) =>
            !string.IsNullOrWhiteSpace(effectId) && builders.ContainsKey(effectId);

        public static EffectContext Build(string effectId, float potencyMul)
        {
            if (string.IsNullOrWhiteSpace(effectId))
                return null;
            if (!builders.TryGetValue(effectId, out EffectBuilder builder))
                return null;

            EffectContext def = new() { PotencyMul = potencyMul };

            builder(def);
            return def;
        }
    }
}
