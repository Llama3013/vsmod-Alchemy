using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    [HarmonyPatch(typeof(EntityProjectileBase), "ImpactOnEntity")]
    internal static class ProjectileImpactCoatPatch
    {
        [HarmonyPostfix]
        public static void Postfix(EntityProjectileBase __instance, Entity target)
        {
            if (!AlchemyConfig.Loaded.AllowWeaponCoating)
                return;
            if (__instance.World.Side != EnumAppSide.Server)
                return;

            ItemStack projectileStack = __instance.ProjectileStack;
            if (projectileStack == null)
                return;

            (string potionId, float multiplier, float durationMul, string itemCode)? consumed =
                CoatedEffects.TryConsumeProjectileCoat(projectileStack);
            if (consumed == null)
                return;

            if (target == null || !target.Alive)
                return;

            (string potionId, float multiplier, float durationMul, string itemCode) = consumed.Value;
            CoatedEffects.Apply(
                potionId,
                target,
                multiplier,
                CoatedEffects.ResolveDisplayName(itemCode, potionId),
                CoatedEffects.SourceFromItemCode(itemCode),
                durationMul
            );
        }
    }

    [HarmonyPatch(typeof(EntityProjectile), "OnCollided")]
    internal static class ProjectileTerrainCoatPatch
    {
        [HarmonyPostfix]
        public static void Postfix(EntityProjectile __instance)
        {
            if (!AlchemyConfig.Loaded.AllowWeaponCoating)
                return;
            if (__instance.World.Side != EnumAppSide.Server)
                return;

            ITreeAttribute attrs = __instance.ProjectileStack?.Attributes;
            if (attrs != null)
                CoatedEffects.ClearStackCoat(attrs);
        }
    }
}
