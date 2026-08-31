using Vintagestory.API.Config;

namespace EffectLib
{
    /// <summary>
    /// Resolves effect-facing lang keys against the domain that registered the effect, falling
    /// back to EffectLib's own translations. This is what lets a mod keep shipping its
    /// strings under its own domain while EffectLib owns the code that displays them.
    /// </summary>
    public static class EffectLang
    {
        /// <summary>
        /// Looks up <c>&lt;domain of effectId&gt;:<paramref name="key"/></c>, then
        /// <c>effectlib:<paramref name="key"/></c>, and finally returns the bare key so a
        /// missing translation shows something recognisable instead of nothing.
        /// </summary>
        public static string Get(string effectId, string key, params object[] args) =>
            GetForDomain(EffectRegistry.DomainOf(effectId), key, args);

        public static string GetForDomain(string domain, string key, params object[] args)
        {
            string owned = $"{domain}:{key}";
            if (Lang.HasTranslation(owned, false, false))
                return Lang.Get(owned, args);

            string shared = $"{EffectRegistry.DefaultDomain}:{key}";
            return Lang.HasTranslation(shared, false, false) ? Lang.Get(shared, args) : key;
        }

        /// <summary>
        /// The player-facing name of an effect, or null when it has none. An id that already
        /// carries a domain (<c>mymod:haste</c>) is looked up as a lang key directly; a bare id
        /// (<c>speedpotionid</c>) is resolved against the domain that registered it. This is
        /// what lets a JSON-only mod name its effect with one lang key and nothing else.
        /// </summary>
        public static string NameIfExists(string effectId)
        {
            if (string.IsNullOrWhiteSpace(effectId))
                return null;

            if (effectId.Contains(':') && Lang.HasTranslation(effectId, false, false))
                return Lang.Get(effectId);

            return GetIfExists(effectId, effectId);
        }

        /// <summary>As <see cref="NameIfExists"/>, falling back to the raw id.</summary>
        public static string Name(string effectId) => NameIfExists(effectId) ?? effectId;

        /// <summary>
        /// As <see cref="Get"/>, but returns null when neither domain has the key, for
        /// optional lines that should be omitted rather than shown raw.
        /// </summary>
        public static string GetIfExists(string effectId, string key)
        {
            string domain = EffectRegistry.DomainOf(effectId);

            string owned = $"{domain}:{key}";
            if (Lang.HasTranslation(owned, false, false))
                return Lang.Get(owned);

            string shared = $"{EffectRegistry.DefaultDomain}:{key}";
            return Lang.HasTranslation(shared, false, false) ? Lang.Get(shared) : null;
        }
    }
}
