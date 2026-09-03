using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace EffectLib
{
    /// <summary>
    /// Fills in an <see cref="EffectContext"/> for one application of an effect. Called every
    /// time the effect is applied, so it may read config that changed since startup.
    /// <see cref="EffectContext.PotencyMul"/> is already set when the builder runs.
    /// </summary>
    public delegate void EffectBuilder(EffectContext ctx);

    /// <summary>An effect id together with the mod that registered it.</summary>
    /// <param name="Id">Effect id, also the key used in saved player state.</param>
    /// <param name="Domain">
    /// Mod domain used to resolve this effect's lang keys and HUD icon. EffectLib looks for
    /// <c>&lt;Domain&gt;:textures/hud/effects/&lt;id&gt;.png</c> and prefers <c>&lt;Domain&gt;:</c>
    /// lang keys over its own.
    /// </param>
    /// <param name="IconSource">
    /// Code of the collectible this effect came from, used as the HUD icon when no explicit
    /// <paramref name="IconTexture"/> is set - so a JSON-only mod shows the wand, flask or herb
    /// the player actually used. Null when nothing item-shaped registered the effect.
    /// </param>
    /// <param name="IconTexture">
    /// An explicit HUD icon texture, from the effect's <c>hudIcon</c> JSON field or the
    /// <c>iconTexture</c> register argument. Takes precedence over <paramref name="IconSource"/>.
    /// </param>
    /// <param name="Channels">
    /// Named delivery methods this effect may be granted through (a mod's own vocabulary -
    /// EffectLib does not know what "drink" or "throw" mean). Null/empty means every channel is
    /// allowed, the same permissive-by-default rule <see cref="EffectPolicy"/> uses. See
    /// <see cref="AllowsChannel"/>.
    /// </param>
    /// <param name="ExclusivityGroup">
    /// A mod-defined group name for effects that should not run alongside each other. EffectLib
    /// only stores and returns it via <see cref="GroupOf"/> - checking it against what else is
    /// active is left to the owning mod, since "conflict" can mean different things to different
    /// mods (Alchemy's own <c>solo</c>/matching-group rules are one example, not a built-in one).
    /// </param>
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

        // Ids come from JSON and from saved player state, so matching ignores case.
        private static readonly ConcurrentDictionary<string, EffectRegistration> entries = new(
            StringComparer.OrdinalIgnoreCase
        );

        public static IReadOnlyDictionary<string, EffectRegistration> Registrations => entries;

        // Ids a mod has claimed as its own (typically code-defined, config-backed effects) so a
        // JSON definition - scanned or self-registered by a behavior - can never silently steal
        // one. Independent of registration order: reserving an id blocks Register regardless of
        // whether anything has registered it yet.
        private static readonly HashSet<string> reserved = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Claims <paramref name="ids"/> so <see cref="Register"/> refuses them from anything
        /// else from now on. Call once at startup, before any JSON scanning or self-registering
        /// behavior could reach the same ids - order relative to those does not otherwise matter.
        /// </summary>
        public static void Reserve(IEnumerable<string> ids)
        {
            foreach (string id in ids)
                if (!string.IsNullOrWhiteSpace(id))
                    reserved.Add(id);
        }

        public static bool IsReserved(string effectId) =>
            effectId != null && reserved.Contains(effectId);

        /// <summary>
        /// Registers (or replaces) the builder for <paramref name="effectId"/>. Refused with a
        /// debug-level log (not a warning - this is the expected outcome whenever a code-owned
        /// item's own JSON attribute also self-registers) if the id is <see cref="Reserve"/>d.
        /// <paramref name="domain"/> should be the registering mod's id so its lang keys and
        /// HUD icons are found.
        /// </summary>
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

        // Consulted when an id is not in the table, so families of effects whose ids are not
        // known ahead of time (an arbitrary entity stat, say) can still be rebuilt after a
        // restart when a saved player brings the id back. Added once at startup, before
        // anything builds - the entries dictionary above absorbs the resolved results.
        private static readonly List<System.Func<string, EffectRegistration>> resolvers = [];

        /// <summary>
        /// Adds a fallback that turns an unknown id into a registration, or returns null if it
        /// does not recognise the id. A resolved id is cached, so a resolver runs once per id.
        /// </summary>
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

        /// <summary>The domain that registered <paramref name="effectId"/>, or the EffectLib default.</summary>
        public static string DomainOf(string effectId) => Resolve(effectId)?.Domain ?? DefaultDomain;

        /// <summary>
        /// Code of the collectible that registered <paramref name="effectId"/>, or null. The HUD
        /// draws it as the icon when no <see cref="IconTextureOf"/> is set.
        /// </summary>
        public static AssetLocation IconSourceOf(string effectId) => Resolve(effectId)?.IconSource;

        /// <summary>The explicit HUD icon texture set for <paramref name="effectId"/>, or null.</summary>
        public static AssetLocation IconTextureOf(string effectId) => Resolve(effectId)?.IconTexture;

        /// <summary>
        /// Whether <paramref name="effectId"/> may be granted through the named
        /// <paramref name="channel"/>. True when the effect declared no channels at all - see
        /// <see cref="EffectRegistration.Channels"/> - so a mod with only one delivery method
        /// never needs to think about this.
        /// </summary>
        public static bool AllowsChannel(string effectId, string channel)
        {
            IReadOnlyCollection<string> channels = Resolve(effectId)?.Channels;
            return channels == null || channels.Contains(channel);
        }

        /// <summary>
        /// Whether <paramref name="effectId"/> declared its own channel list at all. Use this
        /// alongside <see cref="AllowsChannel"/> when a channel should default to *off* rather
        /// than the usual permissive default - e.g. Alchemy's weapon coating, which most potions
        /// don't opt into.
        /// </summary>
        public static bool HasExplicitChannels(string effectId) =>
            Resolve(effectId)?.Channels is { Count: > 0 };

        /// <summary>The exclusivity group <paramref name="effectId"/> registered under, or null for none.</summary>
        public static string GroupOf(string effectId) => Resolve(effectId)?.ExclusivityGroup;

        /// <summary>
        /// Builds the context for one application, or null when the id is not registered -
        /// which happens legitimately when a save holds effects from a since-removed mod.
        /// </summary>
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
