using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

#pragma warning disable IDE0130
namespace EffectLib
#pragma warning restore IDE0130
{
    public sealed class CoatingConfig
    {
        public System.Func<bool> AllowCoating { get; init; }

        public System.Func<int> MaxCharges { get; init; }

        public System.Func<float> EffectMultiplier { get; init; }

        public System.Func<CollectibleObject, bool> IsCoatableWeapon { get; init; }

        public System.Func<CollectibleObject, bool> IsCoatableProjectile { get; init; }

        public System.Func<string, bool> IsEffectCoatable { get; init; }

        public System.Func<ItemStack, (string EffectId, float PotencyMul)?> ResolveLiquidEffect { get; init; }

        public Action<string, Entity, float> ApplySideEffects { get; init; }

        public System.Func<string, EntityPlayer, EffectContext, string> GetBlockReason { get; init; }

        public System.Func<bool> AllowBarrelCoating { get; init; }

        public System.Func<float> BarrelConsumeLitres { get; init; }

        public System.Func<float> BarrelCheckLitres { get; init; }

        public System.Func<CollectibleObject, bool> CombatOverhaulManagesWeapon { get; init; }
        public System.Func<ItemStack, bool> CombatOverhaulManagesProjectile { get; init; }

        public System.Func<
            ItemStack,
            (string EffectId, string ItemCode, float Multiplier, int Charges)?
        > ReadCombatOverhaulCoat { get; init; }

        public Action<ItemSlot, string, string, float, int> WriteCombatOverhaulWeaponCoat { get; init; }
        public Action<ItemStack, string, string, float> WriteCombatOverhaulProjectileCoat { get; init; }
    }

    public static class CoatingPolicy
    {
        private static CoatingConfig config = new();

        public static void Configure(CoatingConfig hooks) => config = hooks ?? new();

        public static bool AllowCoating() => config.AllowCoating?.Invoke() ?? true;

        public static int MaxCharges() => config.MaxCharges?.Invoke() ?? 5;

        public static float EffectMultiplier() => config.EffectMultiplier?.Invoke() ?? 1f;

        public static bool IsCoatableWeapon(CollectibleObject col) =>
            config.IsCoatableWeapon?.Invoke(col) ?? false;

        public static bool IsCoatableProjectile(CollectibleObject col) =>
            config.IsCoatableProjectile?.Invoke(col) ?? false;

        public static bool IsEffectCoatable(string effectId) =>
            config.IsEffectCoatable?.Invoke(effectId) ?? true;

        public static bool AllowBarrelCoating() => config.AllowBarrelCoating?.Invoke() ?? true;

        public static float BarrelConsumeLitres() => config.BarrelConsumeLitres?.Invoke() ?? 0.25f;

        public static float BarrelCheckLitres() => config.BarrelCheckLitres?.Invoke() ?? 0.24f;

        public static (string EffectId, float PotencyMul)? ResolveLiquidEffect(ItemStack stack) =>
            (config.ResolveLiquidEffect ?? DefaultResolveLiquidEffect)(stack);

        public static void ApplySideEffects(string effectId, Entity target, float multiplier) =>
            config.ApplySideEffects?.Invoke(effectId, target, multiplier);

        public static string GetBlockReason(string effectId, EntityPlayer player, EffectContext ctx) =>
            config.GetBlockReason?.Invoke(effectId, player, ctx);

        public static bool CombatOverhaulManagesWeapon(CollectibleObject col) =>
            config.CombatOverhaulManagesWeapon?.Invoke(col) ?? false;

        public static bool CombatOverhaulManagesProjectile(ItemStack stack) =>
            config.CombatOverhaulManagesProjectile?.Invoke(stack) ?? false;

        public static (string EffectId, string ItemCode, float Multiplier, int Charges)? ReadCombatOverhaulCoat(
            ItemStack stack
        ) => config.ReadCombatOverhaulCoat?.Invoke(stack);

        public static void WriteCombatOverhaulWeaponCoat(
            ItemSlot slot,
            string effectId,
            string itemCode,
            float multiplier,
            int charges
        ) => config.WriteCombatOverhaulWeaponCoat?.Invoke(slot, effectId, itemCode, multiplier, charges);

        public static void WriteCombatOverhaulProjectileCoat(
            ItemStack stack,
            string effectId,
            string itemCode,
            float multiplier
        ) => config.WriteCombatOverhaulProjectileCoat?.Invoke(stack, effectId, itemCode, multiplier);

        private static (string EffectId, float PotencyMul)? DefaultResolveLiquidEffect(ItemStack stack)
        {
            CollectibleObject content = stack?.Collectible;
            JsonObject def = content?.Attributes?["effectinfo"];
            if (def?.Exists != true)
                return null;

            string effectId = def["effectId"].AsString()?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(effectId))
                return null;

            if (!EffectRegistry.IsRegistered(effectId))
                JsonEffectDefinition.RegisterFrom(effectId, content.Code.Domain, def, content.Code);

            return (effectId, 1f);
        }
    }
}
