using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace EffectLib
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// The weapon/arrow coating hooks a mod supplies to EffectLib. Every hook is optional; one
    /// left unset falls back to a permissive or no-op default. Build one and install it once
    /// with <see cref="CoatingPolicy.Configure"/>, the same way <see cref="EffectPolicy"/> is.
    /// </summary>
    public sealed class CoatingConfig
    {
        /// <summary>Master on/off switch, checked before anything else. Default: allowed.</summary>
        public System.Func<bool> AllowCoating { get; init; }

        /// <summary>Charges a single coating application can carry. Default: 5.</summary>
        public System.Func<int> MaxCharges { get; init; }

        /// <summary>
        /// Flat dampening applied on top of the potency a coating was made with - a coated hit
        /// is typically weaker than drinking the same effect. Default: 1 (no dampening).
        /// </summary>
        public System.Func<float> EffectMultiplier { get; init; }

        /// <summary>Whether a collectible can be coated as a melee weapon. Default: never.</summary>
        public System.Func<CollectibleObject, bool> IsCoatableWeapon { get; init; }

        /// <summary>Whether a collectible can be coated as a projectile. Default: never.</summary>
        public System.Func<CollectibleObject, bool> IsCoatableProjectile { get; init; }

        /// <summary>
        /// Whether a specific effect id may currently be delivered by coating - the per-effect
        /// flag, not the master switch. Default: always allowed.
        /// </summary>
        public System.Func<string, bool> IsEffectCoatable { get; init; }

        /// <summary>
        /// Resolves a liquid stack (e.g. a barrel's contents) to an (effect id, potency) pair,
        /// or null if it carries none. Default reads the same <c>effectinfo</c> schema
        /// <see cref="CollectibleBehaviorEffectLiquid"/> does, at potency 1, registering it on
        /// first sight - so barrel coating works out of the box for a JSON-only mod.
        /// </summary>
        public System.Func<ItemStack, (string EffectId, float PotencyMul)?> ResolveLiquidEffect { get; init; }

        /// <summary>
        /// Runs once per hit before anything else, whether or not the main effect applies.
        /// Default no-op. Alchemy uses it for the drinking-style side effects a coated hit carries.
        /// </summary>
        public Action<string, Entity, float> ApplySideEffects { get; init; }

        /// <summary>
        /// A lang key naming why the main effect should not apply to this target, or null to
        /// allow it. Default never blocks. Alchemy uses it for exclusivity groups.
        /// </summary>
        public System.Func<string, EntityPlayer, EffectContext, string> GetBlockReason { get; init; }

        // ----- Barrel coating: coats every weapon/arrow in a barrel from the liquid also in it -----

        /// <summary>Whether barrel coating is allowed at all. Default: allowed.</summary>
        public System.Func<bool> AllowBarrelCoating { get; init; }

        /// <summary>Litres of liquid one barrel coating consumes. Default: 0.25.</summary>
        public System.Func<float> BarrelConsumeLitres { get; init; }

        /// <summary>Litres that must be present for a barrel coating to proceed. Default: 0.24.</summary>
        public System.Func<float> BarrelCheckLitres { get; init; }

        // Combat Overhaul's weapons bypass EffectLib's on-hit patches, so it keeps their
        // coatings in its own buff store and delivers them itself. Unset = coating on the stack.

        public System.Func<CollectibleObject, bool> CombatOverhaulManagesWeapon { get; init; }
        public System.Func<ItemStack, bool> CombatOverhaulManagesProjectile { get; init; }

        public System.Func<
            ItemStack,
            (string EffectId, string ItemCode, float Multiplier, int Charges)?
        > ReadCombatOverhaulCoat { get; init; }

        public Action<ItemSlot, string, string, float, int> WriteCombatOverhaulWeaponCoat { get; init; }
        public Action<ItemStack, string, string, float> WriteCombatOverhaulProjectileCoat { get; init; }
    }

    /// <summary>
    /// Weapon/arrow coating behaviour EffectLib ships no config or game-specific knowledge of.
    /// The owning mod installs a <see cref="CoatingConfig"/> once via <see cref="Configure"/>;
    /// each accessor here just calls the matching hook, or returns its default when unset.
    /// </summary>
    public static class CoatingPolicy
    {
        private static CoatingConfig config = new();

        /// <summary>Installs the coating hooks. Pass null to reset to all-default behaviour.</summary>
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

        // The out-of-the-box liquid resolver: reads the same effectinfo schema
        // CollectibleBehaviorEffectLiquid does, at potency 1, registering it on first sight.
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
