using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace EffectLib
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// A HUD entry that is not backed by a running <see cref="EffectManager"/> effect - for
    /// state a mod tracks itself but still wants shown alongside real effects.
    /// </summary>
    public sealed class HudEffectRow
    {
        /// <summary>Effect id, used for the icon lookup and as the row's identity.</summary>
        public string Id { get; set; }

        /// <summary>Display name. Falls back to the id when empty.</summary>
        public string Name { get; set; }

        /// <summary>True for rows with no expiry, which show no countdown.</summary>
        public bool Endless { get; set; } = true;

        /// <summary>Remaining seconds. Ignored when <see cref="Endless"/>.</summary>
        public int RemainingSec { get; set; }

        /// <summary>Extra tooltip lines shown under the name.</summary>
        public string[] ExtraLines { get; set; }

        /// <summary>
        /// Any value that changes when the row's content changes. The HUD rebuilds the row
        /// when this differs from what it last saw.
        /// </summary>
        public float ChangeToken { get; set; }
    }

    /// <summary>
    /// Lets a mod contribute rows and icons to the shared effect HUD. Every member has a
    /// default, so a provider only implements what it needs.
    /// </summary>
    public interface IHudEffectProvider
    {
        /// <summary>
        /// WatchedAttributes keys on the local player that should make the HUD re-read rows.
        /// </summary>
        IEnumerable<string> WatchedKeys => [];

        /// <summary>Extra rows to show. Called whenever a watched key changes.</summary>
        IEnumerable<HudEffectRow> GetRows(EntityPlayer entity) => [];

        /// <summary>
        /// Texture to draw for <paramref name="effectId"/>, or null to let the HUD use its
        /// default of <c>&lt;domain&gt;:textures/hud/effects/&lt;id&gt;.png</c>.
        /// </summary>
        AssetLocation GetIconTexture(string effectId) => null;

        /// <summary>
        /// Item to render as the icon when no texture was found, or null for none.
        /// </summary>
        ItemStack GetIconStack(ICoreClientAPI capi, string effectId) => null;
    }

    /// <summary>Registry of <see cref="IHudEffectProvider"/>s. Client side only.</summary>
    public static class EffectHud
    {
        private static readonly List<IHudEffectProvider> providers = [];

        public static IReadOnlyList<IHudEffectProvider> Providers => providers;

        public static void Register(IHudEffectProvider provider)
        {
            if (provider != null && !providers.Contains(provider))
                providers.Add(provider);
        }

        public static void Unregister(IHudEffectProvider provider) => providers.Remove(provider);
    }
}
