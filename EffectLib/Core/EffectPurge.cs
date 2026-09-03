using System;
using System.Collections.Generic;
using System.Linq;

namespace EffectLib
{
    /// <summary>
    /// Which effects a purge is allowed to clear. A purge defaults to the domain of the effect
    /// that triggered it, so one mod's "clears all effects" item cannot wipe another mod's
    /// effects unless it deliberately asks to.
    /// </summary>
    public sealed class EffectPurge(IEnumerable<string> domains = null, IEnumerable<string> effectIds = null)
    {
        /// <summary>Wildcard usable in <see cref="Domains"/> to mean every domain.</summary>
        public const string AnyDomain = "*";

        private readonly HashSet<string> domains = new(domains ?? [], StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> effectIds = new(effectIds ?? [], StringComparer.OrdinalIgnoreCase);

        /// <summary>Domains whose effects are cleared. May contain <see cref="AnyDomain"/>.</summary>
        public IReadOnlyCollection<string> Domains => domains;

        /// <summary>Individual effect ids cleared on top of <see cref="Domains"/>.</summary>
        public IReadOnlyCollection<string> EffectIds => effectIds;

        /// <summary>Clears everything, whichever mod owns it.</summary>
        public static EffectPurge Everything { get; } = new([AnyDomain]);

        /// <summary>Clears only the effects registered by one mod.</summary>
        public static EffectPurge ForDomain(string domain) => new([domain]);

        /// <summary>True when this purge clears every domain.</summary>
        public bool IsEverything => domains.Contains(AnyDomain);

        /// <summary>True when effects belonging to <paramref name="domain"/> are in scope.</summary>
        public bool CoversDomain(string domain) => IsEverything || domains.Contains(domain);

        /// <summary>True when <paramref name="effectId"/> should be cleared.</summary>
        public bool Covers(string effectId) =>
            effectId != null
            && (
                IsEverything
                || effectIds.Contains(effectId)
                || domains.Contains(EffectRegistry.DomainOf(effectId))
            );

        public override string ToString() =>
            IsEverything
                ? "everything"
                : string.Join(
                    ", ",
                    domains.Select(d => d + ":*").Concat(effectIds).DefaultIfEmpty("nothing")
                );
    }
}
