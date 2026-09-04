using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace EffectLib
{
    public delegate void EffectBuilder(EffectContext ctx);

    public sealed record EffectRegistration(
        string Id,
        string Domain,
        EffectBuilder Builder,
        AssetLocation IconSource = null,
        IReadOnlyCollection<string> Channels = null,
        string ExclusivityGroup = null,
        AssetLocation IconTexture = null
    );

    public static class EffectRegistry
    {
        public const string DefaultDomain = "effectlib";

        private static readonly ConcurrentDictionary<string, EffectRegistration> entries = new(
            StringComparer.OrdinalIgnoreCase
        );

        public static IReadOnlyDictionary<string, EffectRegistration> Registrations => entries;

        private static readonly HashSet<string> reserved = new(StringComparer.OrdinalIgnoreCase);

        public static void Reserve(IEnumerable<string> ids)
        {
            foreach (string id in ids)
                if (!string.IsNullOrWhiteSpace(id))
                    reserved.Add(id);
        }

        public static bool IsReserved(string effectId) =>
            effectId != null && reserved.Contains(effectId);

        public static void Register(
            string effectId,
            EffectBuilder builder,
            string domain = DefaultDomain,
            AssetLocation iconSource = null,
            IEnumerable<string> channels = null,
            string exclusivityGroup = null,
            AssetLocation iconTexture = null
        )
        {
            if (string.IsNullOrWhiteSpace(effectId) || builder == null)
                return;

            if (reserved.Contains(effectId))
                return;

            HashSet<string> channelSet =
                channels == null ? null : new HashSet<string>(channels, StringComparer.OrdinalIgnoreCase);

            entries[effectId] = new EffectRegistration(
                effectId,
                string.IsNullOrWhiteSpace(domain) ? DefaultDomain : domain,
                builder,
                iconSource,
                channelSet is { Count: > 0 } ? channelSet : null,
                string.IsNullOrWhiteSpace(exclusivityGroup) ? null : exclusivityGroup,
                iconTexture
            );
        }

        private static readonly List<System.Func<string, EffectRegistration>> resolvers = [];

        public static void AddResolver(System.Func<string, EffectRegistration> resolver)
        {
            if (resolver != null && !resolvers.Contains(resolver))
                resolvers.Add(resolver);
        }

        private static EffectRegistration Resolve(string effectId)
        {
            if (string.IsNullOrWhiteSpace(effectId))
                return null;

            if (entries.TryGetValue(effectId, out EffectRegistration entry))
                return entry;

            foreach (System.Func<string, EffectRegistration> resolver in resolvers)
            {
                EffectRegistration resolved = resolver(effectId);
                if (resolved?.Builder != null)
                {
                    entries[effectId] = resolved;
                    return resolved;
                }
            }

            return null;
        }

        public static bool IsRegistered(string effectId) => Resolve(effectId) != null;

        public static string DomainOf(string effectId) => Resolve(effectId)?.Domain ?? DefaultDomain;

        public static AssetLocation IconSourceOf(string effectId) => Resolve(effectId)?.IconSource;

        public static AssetLocation IconTextureOf(string effectId) => Resolve(effectId)?.IconTexture;

        public static bool AllowsChannel(string effectId, string channel)
        {
            IReadOnlyCollection<string> channels = Resolve(effectId)?.Channels;
            return channels == null || channels.Contains(channel);
        }

        public static bool HasExplicitChannels(string effectId) =>
            Resolve(effectId)?.Channels is { Count: > 0 };

        public static string GroupOf(string effectId) => Resolve(effectId)?.ExclusivityGroup;

        public static EffectContext Build(string effectId, float potencyMul)
        {
            EffectRegistration entry = Resolve(effectId);
            if (entry == null)
                return null;

            EffectContext def = new() { PotencyMul = potencyMul };

            entry.Builder(def);
            return def;
        }
    }
}
