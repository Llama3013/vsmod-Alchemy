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
            (string PotionId, string ItemCode, float Multiplier, int Charges)?
        > getCoating;
        internal static Action<ItemSlot, string, string, float, int> applyCoatingBuff;
        internal static Action<ItemStack, string, string, float> applyProjectileCoatingBuff;
        internal static Action shutdownImpl;

        private const string WeaponBuffSystemTypeName =
            "CombatOverhaul.WeaponBuffs.WeaponBuffSystem";

        private const string CompatAssemblyName = "AlchemyCombatOverhaulCompat";
        private const string CompatEntryTypeName = "Alchemy.CombatOverhaulCompatBridge.CompatEntry";

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
                        + "will use legacy behaviour only. (Is the buff-system mod installed and "
                        + "is the type name correct for your version?)"
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
            out string potionId,
            out string itemCode,
            out float multiplier,
            out int charges
        )
        {
            potionId = null;
            itemCode = null;
            multiplier = 0f;
            charges = 0;

            if (!Active || stack == null)
                return false;

            (string PotionId, string ItemCode, float Multiplier, int Charges)? coating =
                getCoating?.Invoke(stack);
            if (coating == null)
                return false;

            (potionId, itemCode, multiplier, charges) = coating.Value;
            return !string.IsNullOrEmpty(potionId);
        }

        public static void SetCoating(
            ItemSlot slot,
            string potionId,
            string itemCode,
            float multiplier,
            int charges
        )
        {
            if (!Active)
                return;
            applyCoatingBuff?.Invoke(slot, potionId, itemCode, multiplier, charges);
        }

        public static bool ShouldUseProjectileBuffStorage(ItemStack stack)
        {
            return Active && stack != null && (isCoManagedProjectile?.Invoke(stack) ?? false);
        }

        public static void SetProjectileCoating(
            ItemStack stack,
            string potionId,
            string itemCode,
            float multiplier
        )
        {
            if (!Active)
                return;
            applyProjectileCoatingBuff?.Invoke(stack, potionId, itemCode, multiplier);
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
                    $"Embedded resource {CompatAssemblyName}.dll not found in alchemy.dll"
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
                (string PotionId, string ItemCode, float Multiplier, int Charges)?
            >)
                Delegate.CreateDelegate(
                    typeof(System.Func<ItemStack, (string, string, float, int)?>),
                    entry.GetMethod("GetCoating")
                );
            applyCoatingBuff =
                (Action<ItemSlot, string, string, float, int>)
                    Delegate.CreateDelegate(
                        typeof(Action<ItemSlot, string, string, float, int>),
                        entry.GetMethod("ApplyCoatingBuff")
                    );
            applyProjectileCoatingBuff =
                (Action<ItemStack, string, string, float>)
                    Delegate.CreateDelegate(
                        typeof(Action<ItemStack, string, string, float>),
                        entry.GetMethod("ApplyProjectileCoatingBuff")
                    );
            shutdownImpl = (Action)
                Delegate.CreateDelegate(typeof(Action), entry.GetMethod("Shutdown"));

            entry
                .GetMethod("Init")
                .Invoke(
                    null,
                    [
                        api,
                        (System.Func<string, bool>)PotionConsumableLogic.IsCoatingAllowed,
                        (() => AlchemyConfig.Loaded.AllowWeaponCoating),
                        (() => AlchemyConfig.Loaded.WeaponCoatEffectMultiplier),
                        (System.Func<string, string, string>)EffectLib.CoatedEffects.ResolveDisplayName,
                        (Action<string, Entity, float, string>)EffectLib.CoatedEffects.Apply,
                    ]
                );
        }
    }
}
