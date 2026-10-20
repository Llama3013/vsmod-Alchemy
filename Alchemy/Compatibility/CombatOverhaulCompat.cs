using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    public static class CombatOverhaulCompat
    {
        public static bool Active { get; private set; }

        internal static System.Func<CollectibleObject, bool> isCoManagedWeapon;
        internal static System.Func<ItemStack, bool> isCoManagedProjectile;
        internal static System.Func<
            ItemStack,
            (string EffectId, string ItemCode, float Multiplier, float DurationMul, int Charges)?
        > getCoating;
        internal static Action<ItemSlot, string, string, float, float, int> applyCoatingBuff;
        internal static Action<ItemStack, string, string, float, float> applyProjectileCoatingBuff;
        internal static Action shutdownImpl;

        private const string WeaponBuffSystemTypeName =
            "CombatOverhaul.WeaponBuffs.WeaponBuffSystem";

        private const string CompatAssemblyName = "AlchemyCombatOverhaulCompat";
        private const string CompatEntryTypeName =
            "Alchemy.CombatOverhaulCompatBridge.CompatEntry";

        private static Assembly compatAssembly;

        private static Assembly overhaulAssembly;
        private static bool resolveHooked;

        public static void Init(ICoreAPI api)
        {
            if (Active)
                return;

            ModSystem buffSystemMs = api.ModLoader.GetModSystem(WeaponBuffSystemTypeName);
            if (buffSystemMs == null)
            {
                api.Logger.Notification(
                    "[Alchemy] Combat Overhaul weapon buff system not found; weapon coatings "
                        + "use the built-in on-stack path only. (Is the buff-system mod installed "
                        + "and is the type name correct for your version?)"
                );
                return;
            }

            try
            {
                overhaulAssembly = buffSystemMs.GetType().Assembly;
                if (compatAssembly == null)
                {
                    HookAssemblyResolve();
                    compatAssembly = LoadEmbeddedCompatAssembly();
                }
                BindCompatEntry(api);
                Active = true;
                api.Logger.Notification(
                    "[Alchemy] Combat Overhaul weapon coating compatibility enabled"
                );
            }
            catch (Exception e)
            {
                // Older overhaullib versions without the weapon buff system land here
                api.Logger.Warning(
                    "[Alchemy] Could not enable Combat Overhaul weapon coating compatibility, "
                        + "weapon coatings will not work on Combat Overhaul weapons: {0}",
                    e
                );
            }
        }

        public static void Shutdown()
        {
            if (!Active)
                return;
            Active = false;
            try
            {
                shutdownImpl?.Invoke();
            }
            catch
            {
                // Nothing useful to do if overhaullib already tore itself down
            }
            isCoManagedWeapon = null;
            isCoManagedProjectile = null;
            getCoating = null;
            applyCoatingBuff = null;
            applyProjectileCoatingBuff = null;
            shutdownImpl = null;
        }

        public static bool ShouldUseBuffStorage(CollectibleObject collectible)
        {
            return Active
                && collectible != null
                && (isCoManagedWeapon?.Invoke(collectible) ?? false);
        }

        public static bool TryGetCoating(
            ItemStack stack,
            out string effectId,
            out string itemCode,
            out float multiplier,
            out float durationMul,
            out int charges
        )
        {
            effectId = null;
            itemCode = null;
            multiplier = 0f;
            durationMul = 1f;
            charges = 0;

            if (!Active || stack == null)
                return false;

            (string EffectId, string ItemCode, float Multiplier, float DurationMul, int Charges)? coating =
                getCoating?.Invoke(stack);
            if (coating == null)
                return false;

            (effectId, itemCode, multiplier, durationMul, charges) = coating.Value;
            return !string.IsNullOrEmpty(effectId);
        }

        public static void SetCoating(
            ItemSlot slot,
            string effectId,
            string itemCode,
            float multiplier,
            float durationMul,
            int charges
        )
        {
            if (!Active)
                return;
            applyCoatingBuff?.Invoke(slot, effectId, itemCode, multiplier, durationMul, charges);
        }

        public static bool ShouldUseProjectileBuffStorage(ItemStack stack)
        {
            return Active && stack != null && (isCoManagedProjectile?.Invoke(stack) ?? false);
        }

        public static void SetProjectileCoating(
            ItemStack stack,
            string effectId,
            string itemCode,
            float multiplier,
            float durationMul
        )
        {
            if (!Active)
                return;
            applyProjectileCoatingBuff?.Invoke(stack, effectId, itemCode, multiplier, durationMul);
        }

        private static void HookAssemblyResolve()
        {
            if (resolveHooked)
                return;
            resolveHooked = true;
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                string name = new AssemblyName(args.Name).Name;
                if (name == "OverhaullibLegacyCompat")
                    return overhaulAssembly;
                if (name == CompatAssemblyName)
                    return compatAssembly;
                return null;
            };
        }

        private static Assembly LoadEmbeddedCompatAssembly()
        {
            Assembly host = typeof(CombatOverhaulCompat).Assembly;
            string resourceName = null;
            foreach (string name in host.GetManifestResourceNames())
            {
                if (name.EndsWith(CompatAssemblyName + ".dll", StringComparison.OrdinalIgnoreCase))
                {
                    resourceName = name;
                    break;
                }
            }
            if (resourceName == null)
                throw new InvalidOperationException(
                    $"Embedded resource {CompatAssemblyName}.dll not found in {host.GetName().Name}.dll"
                );

            using Stream stream = host.GetManifestResourceStream(resourceName);
            return AssemblyLoadContext.Default.LoadFromStream(stream);
        }

        private static void BindCompatEntry(ICoreAPI api)
        {
            Type entry =
                compatAssembly.GetType(CompatEntryTypeName)
                ?? throw new InvalidOperationException(
                    $"Type {CompatEntryTypeName} not found in {CompatAssemblyName}.dll"
                );

            isCoManagedWeapon = (System.Func<CollectibleObject, bool>)
                Delegate.CreateDelegate(
                    typeof(System.Func<CollectibleObject, bool>),
                    entry.GetMethod("IsCoManagedWeapon")
                );
            isCoManagedProjectile = (System.Func<ItemStack, bool>)
                Delegate.CreateDelegate(
                    typeof(System.Func<ItemStack, bool>),
                    entry.GetMethod("IsCoManagedProjectile")
                );
            getCoating = (System.Func<
                ItemStack,
                (string EffectId, string ItemCode, float Multiplier, float DurationMul, int Charges)?
            >)
                Delegate.CreateDelegate(
                    typeof(System.Func<ItemStack, (string, string, float, float, int)?>),
                    entry.GetMethod("GetCoating")
                );
            applyCoatingBuff =
                (Action<ItemSlot, string, string, float, float, int>)
                    Delegate.CreateDelegate(
                        typeof(Action<ItemSlot, string, string, float, float, int>),
                        entry.GetMethod("ApplyCoatingBuff")
                    );
            applyProjectileCoatingBuff =
                (Action<ItemStack, string, string, float, float>)
                    Delegate.CreateDelegate(
                        typeof(Action<ItemStack, string, string, float, float>),
                        entry.GetMethod("ApplyProjectileCoatingBuff")
                    );
            shutdownImpl = (Action)
                Delegate.CreateDelegate(typeof(Action), entry.GetMethod("Shutdown"));

            // Hooks passed as delegate args (rather than the compat assembly calling
            // CoatedEffects/AlchemyConfig directly) so AlchemyCombatOverhaulCompat never needs a
            // compile-time reference back to Alchemy.csproj - Alchemy.csproj already references
            // it the other way (to embed its built dll as a resource), and a reference in both
            // directions is a circular project dependency MSBuild can't build.
            entry.GetMethod("Init")
                .Invoke(
                    null,
                    [
                        api,
                        (System.Func<bool>)(() => AlchemyConfig.Loaded.AllowWeaponCoating),
                        (System.Func<float>)(() => AlchemyConfig.Loaded.WeaponCoatEffectMultiplier),
                        (System.Func<string, bool>)PotionConsumableLogic.IsCoatingAllowed,
                        (System.Action<string, Entity, float, float, string, string>)(
                            (potionId, target, multiplier, durationMul, displayName, itemCode) =>
                                CoatedEffects.Apply(
                                    potionId,
                                    target,
                                    multiplier,
                                    displayName,
                                    CoatedEffects.SourceFromItemCode(itemCode),
                                    durationMul
                                )
                        ),
                    ]
                );
        }
    }
}
