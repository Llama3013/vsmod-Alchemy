using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace EffectLib
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    // Delivers a coated projectile's effect on impact - see CoatedEffects for why this
    // deliberately only ever finds something for a legacy-stored coating.
    [HarmonyPatch(typeof(EntityProjectileBase), "ImpactOnEntity")]
    internal static class ProjectileImpactCoatPatch
    {
        [HarmonyPostfix]
        public static void Postfix(EntityProjectileBase __instance, Entity target)
        {
            if (!CoatingPolicy.AllowCoating())
                return;
            if (__instance.World.Side != EnumAppSide.Server)
                return;

            ItemStack projectileStack = __instance.ProjectileStack;
            if (projectileStack == null)
                return;

            (string effectId, float multiplier, string itemCode)? consumed =
                CoatedEffects.TryConsumeProjectileCoat(projectileStack);
            if (consumed == null)
                return;

            if (target == null || !target.Alive)
                return;

            (string effectId, float multiplier, string itemCode) = consumed.Value;
            string displayName = CoatedEffects.ResolveDisplayName(itemCode, effectId);
            CoatedEffects.Apply(effectId, target, multiplier, displayName);
        }
    }

    // A coated projectile that misses and lands in terrain loses its coating rather than
    // keeping it indefinitely.
    [HarmonyPatch(typeof(EntityProjectile), "OnCollided")]
    internal static class ProjectileTerrainCoatPatch
    {
        [HarmonyPostfix]
        public static void Postfix(EntityProjectile __instance)
        {
            if (!CoatingPolicy.AllowCoating())
                return;
            if (__instance.World.Side != EnumAppSide.Server)
                return;

            ITreeAttribute attrs = __instance.ProjectileStack?.Attributes;
            if (attrs != null)
                CoatedEffects.ClearLegacyCoat(attrs);
        }
    }
}
