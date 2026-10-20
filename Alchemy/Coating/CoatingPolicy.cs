using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    public static class CoatingPolicy
    {
        private static ICoreAPI api;
        private static TagSet coatableWeaponTagSet;
        private static bool coatableWeaponTagSetBuilt;
        private static string[] coatableProjectileCodes;

        // Also runs per world, so a new world never reuses the last one's tag ids.
        public static void Init(ICoreAPI coreApi)
        {
            api = coreApi;
            InvalidateCaches();
        }

        public static void InvalidateCaches()
        {
            coatableWeaponTagSetBuilt = false;
            coatableProjectileCodes = null;
        }

        // Weapons are matched by vanilla item/block tags
        // (default "weapon-melee"), since arrows have no reliable tag as of
        // VS 1.22.7 they're matched by wildcard item code instead (default "*arrow*").
        public static bool IsCoatableWeapon(CollectibleObject col) =>
            col?.Tags != null
            && TryGetCoatableWeaponTagSet(out TagSet tagSet)
            && col.Tags.Overlaps(tagSet);

        private static bool TryGetCoatableWeaponTagSet(out TagSet tagSet)
        {
            if (!coatableWeaponTagSetBuilt && api != null)
            {
                api.CollectibleTagRegistry.TryCreateTagSetAndLogIssues(
                    out coatableWeaponTagSet,
                    (ReadOnlySpan<string>)SplitList(AlchemyConfig.Loaded.CoatableWeaponTags)
                );
                coatableWeaponTagSetBuilt = true;
            }
            tagSet = coatableWeaponTagSet;
            return coatableWeaponTagSetBuilt;
        }

        public static bool IsCoatableProjectile(CollectibleObject col)
        {
            if (col?.Code == null)
                return false;

            string[] codes = coatableProjectileCodes ??= SplitList(AlchemyConfig.Loaded.CoatableProjectilesCodes);
            return codes.Length > 0 && WildcardUtil.Match(codes, col.Code.ToString());
        }

        private static string[] SplitList(string list) =>
            [.. (list ?? "").Split(',').Select(entry => entry.Trim()).Where(entry => entry.Length > 0)];
    }
}
