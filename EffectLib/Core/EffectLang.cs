using Vintagestory.API.Config;

namespace EffectLib
{
    public static class EffectLang
    {
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

        public static string NameIfExists(string effectId)
        {
            if (string.IsNullOrWhiteSpace(effectId))
                return null;

            if (effectId.Contains(':') && Lang.HasTranslation(effectId, false, false))
                return Lang.Get(effectId);

            return GetIfExists(effectId, effectId);
        }

        public static string Name(string effectId) => NameIfExists(effectId) ?? effectId;

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
