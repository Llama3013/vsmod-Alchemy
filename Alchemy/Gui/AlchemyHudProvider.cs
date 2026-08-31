using System;
using System.Collections.Generic;
using EffectLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// Feeds Alchemy specifics into EffectLib's shared HUD: the grow/shrink row, which has no
    /// duration and so is not a tracked effect, and the potion item used as a fallback icon.
    /// </summary>
    public sealed class AlchemyHudProvider : IHudEffectProvider
    {
        public static readonly AlchemyHudProvider Instance = new();

        private Dictionary<string, ItemStack> iconStacks;

        private AlchemyHudProvider() { }

        public IEnumerable<string> WatchedKeys => ["potionSizeDelta"];

        public IEnumerable<HudEffectRow> GetRows(EntityPlayer entity)
        {
            float sizeDelta = entity.WatchedAttributes.GetFloat("potionSizeDelta");
            if (Math.Abs(sizeDelta) <= 0.001f)
                yield break;

            bool growing = sizeDelta > 0;
            string label = Lang.GetIfExists(growing ? "alchemy:grow" : "alchemy:shrink");

            yield return new HudEffectRow
            {
                Id = growing ? "growpotionid" : "shrinkpotionid",
                Name = label,
                Endless = true,
                ChangeToken = sizeDelta,
                ExtraLines = [$"{label}: {sizeDelta:+0.0#;-0.0#}"],
            };
        }

        // Alchemy's icons are named after the potion without the "potionid" suffix.
        public AssetLocation GetIconTexture(string effectId)
        {
            if (EffectRegistry.DomainOf(effectId) != PotionEffects.Domain)
                return null;

            string shortId = effectId.EndsWith("potionid", StringComparison.Ordinal)
                ? effectId[..^"potionid".Length]
                : effectId;

            return new AssetLocation(
                PotionEffects.Domain,
                "textures/hud/effects/" + shortId + ".png"
            );
        }

        public ItemStack GetIconStack(ICoreClientAPI capi, string effectId)
        {
            if (iconStacks == null)
            {
                iconStacks = [];
                foreach (Item item in capi.World.Items)
                {
                    if (PotionConsumableLogic.TryReadPotionId(item, out string pid))
                        iconStacks.TryAdd(pid, new ItemStack(item));
                }
            }
            return iconStacks.TryGetValue(effectId, out ItemStack stack) ? stack : null;
        }
    }
}
