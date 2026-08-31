using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace EffectLib
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// Everything about weapon/arrow coating that EffectLib ships no config or game-specific
    /// knowledge of. The owning mod installs these once, the same way <see cref="EffectPolicy"/>
    /// is installed - unset hooks have permissive or no-op defaults, so a mod that never touches
    /// this still gets working (if unconfigurable) coating.
    /// </summary>
    public static class CoatingPolicy
    {
        /// <summary>Master on/off switch, checked before anything else. Default: allowed.</summary>
        public static Func<bool> AllowCoating = () => true;

        /// <summary>Charges a single coating application can carry. Default: 5.</summary>
        public static Func<int> MaxCharges = () => 5;

        /// <summary>
        /// A flat dampening factor applied on top of the potency a coating was made with (a
        /// coated hit is typically weaker than drinking the same effect). Multiplied together
        /// with the potency the coating source resolved. Default: 1 (no dampening).
        /// </summary>
        public static Func<float> EffectMultiplier = () => 1f;

        /// <summary>Whether a collectible can be coated as a melee weapon. Default: never.</summary>
        public static System.Func<CollectibleObject, bool> IsCoatableWeapon = _ => false;

        /// <summary>Whether a collectible can be coated as a projectile. Default: never.</summary>
        public static System.Func<CollectibleObject, bool> IsCoatableProjectile = _ => false;

        /// <summary>
        /// Whether a specific effect id is currently allowed to be delivered via coating - the
        /// per-effect delivery flag, not the master switch above. Default: always allowed.
        /// </summary>
        public static System.Func<string, bool> IsEffectCoatable = _ => true;

        /// <summary>
        /// Resolves a liquid stack (e.g. a barrel's contents) to an (effect id, potency) pair,
        /// or null if it does not carry one. Default reads the same <c>effectinfo</c> attribute
        /// schema <see cref="CollectibleBehaviorEffectLiquid"/> does, at potency 1, registering
        /// it the first time it is seen - so this works out of the box for a JSON-only mod.
        /// </summary>
        public static System.Func<ItemStack, (string EffectId, float PotencyMul)?> ResolveLiquidEffect =
            stack =>
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
            };

        // ----- Alternate storage, e.g. a combat mod's own weapon-buff system. When active,
        // coating data is kept there instead of on the item stack's own attributes, so that
        // mod's own on-hit logic (not EffectLib's) delivers the effect. All default to "no
        // alternate storage", i.e. everything lives on the item stack directly. -----

        public static System.Func<CollectibleObject, bool> UsesAlternateWeaponStorage = _ => false;
        public static System.Func<ItemStack, bool> UsesAlternateProjectileStorage = _ => false;

        /// <summary>Reads a coating from alternate storage, or null if there is none / storage is inactive.</summary>
        public static System.Func<ItemStack, (string EffectId, string ItemCode, float Multiplier, int Charges)?> TryReadAlternateWeapon =
            _ => null;

        public static Action<ItemSlot, string, string, float, int> WriteAlternateWeapon = delegate { };
        public static Action<ItemStack, string, string, float> WriteAlternateProjectile = delegate { };

        // ----- Extra per-hit hooks a mod can layer on top of the generic application -----

        /// <summary>
        /// Runs once per hit, before anything else - independent of whether the main effect
        /// ends up applying. Default no-op. (Alchemy uses this for its drinking-style side
        /// effects, which a coated hit also carries.)
        /// </summary>
        public static Action<string, Entity, float> ApplySideEffects = delegate { };

        /// <summary>
        /// A lang key naming why the main effect should not apply to this target, or null to
        /// allow it. Default never blocks. (Alchemy uses this for exclusivity groups.)
        /// </summary>
        public static System.Func<string, EntityPlayer, EffectContext, string> GetBlockReason =
            (_, _, _) => null;
    }
}
