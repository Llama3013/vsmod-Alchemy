using System;
using System.Collections.Generic;
using System.Linq;

namespace EffectLib
{
    public sealed class EffectPurge(IEnumerable<string> domains = null, IEnumerable<string> effectIds = null)
    {
        public const string AnyDomain = "*";

        private readonly HashSet<string> domains = new(domains ?? [], StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> effectIds = new(effectIds ?? [], StringComparer.OrdinalIgnoreCase);

        public IReadOnlyCollection<string> Domains => domains;

        public IReadOnlyCollection<string> EffectIds => effectIds;

        public static EffectPurge Everything { get; } = new([AnyDomain]);

        public static EffectPurge ForDomain(string domain) => new([domain]);

        public bool IsEverything => domains.Contains(AnyDomain);

        public bool CoversDomain(string domain) => IsEverything || domains.Contains(domain);

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
