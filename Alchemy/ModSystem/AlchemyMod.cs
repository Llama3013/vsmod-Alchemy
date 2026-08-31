// Maybe change recipe for archer flask to not require orange mallow since its semi rare for normal climate worlds
/*json block glow
vertexFlags: {
    glowLevel: 255
},*/
/* Quick reference to all attributes that change the characters Stats:
   healingeffectivness, maxhealthExtraPoints, walkspeed, hungerrate, rangedWeaponsAcc, rangedWeaponsSpeed
   rangedWeaponsDamage, meleeWeaponsDamage, mechanicalsDamage, animalLootDropRate, forageDropRate, wildCropDropRate
   vesselContentsDropRate, oreDropRate, rustyGearDropRate, miningSpeedMul, animalSeekingRange, armorDurabilityLoss, bowDrawingStrength, wholeVesselLootChance, temporalGearTLRepairCost, animalHarvestingTime*/
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EffectLib;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    public class AlchemyMod : ModSystem
    {
        // private static GuiDialogCreateCharacter createCharDlg;
        private ICoreAPI api;
        private const string HarmonyId = "llama3013.Alchemy";
        private const string ConfigSyncChannelName = "alchemyconfigsync";
        private Harmony harmony;

        public override void Start(ICoreAPI api)
        {
            this.api = api;
            base.Start(api);
            api.Logger.Debug("[Potion] Start");

            if (!Harmony.HasAnyPatches(HarmonyId))
            {
                harmony = new Harmony(HarmonyId);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
            }
            CombatOverhaulCompat.Init(api);
            RegisterClasses(api);
            RegisterEffectsOnce(api.Logger);
            RegisterWithEffectLib(api);

            // World config isn't a reliable sync channel for runtime values a client needs to
            // read back (tooltips, behavior calculations) - PlayerJoin below sends this instead.
            api.Network
                .RegisterChannel(ConfigSyncChannelName)
                .RegisterMessageType<AlchemyConfigSyncPacket>();
        }

        private static readonly object effectRegistrationLock = new();
        private static bool effectsRegistered;

        private static void RegisterEffectsOnce(ILogger logger)
        {
            lock (effectRegistrationLock)
            {
                if (effectsRegistered)
                    return;

                PotionEffects.RegisterAll();
                PotionDefinitions.Validate(logger);

                // Claims the ~25 built-in potion ids so a JSON-defined effect (scanned by a
                // behavior's own OnLoaded, or a content patch like throwableacid.json) can never
                // silently take one over - see PotionConsumableLogic.IsCodeOwned.
                EffectRegistry.Reserve(PotionDefinitions.All.Select(def => def.Id));

                effectsRegistered = true;
            }
        }

        // Runs on every Start rather than once. The game creates a ModSystem per side and can
        // recreate them on a world reload, so a one-shot guard here would leave EffectLib
        // pointed at a stale gate or HUD provider for the rest of the process. Both calls
        // below are idempotent.
        private static void RegisterWithEffectLib(ICoreAPI api)
        {
            // EffectLib ships no config of its own, so point its capability gate at ours.
            EffectPolicy.SetGate(capability =>
                capability switch
                {
                    EffectCapability.Fly => AlchemyConfig.Loaded.AllowFlightPotion,
                    EffectCapability.Climb => AlchemyConfig.Loaded.AllowClimbPotion,
                    EffectCapability.Fall => AlchemyConfig.Loaded.AllowFallPotion,
                    EffectCapability.Refresh => AlchemyConfig.Loaded.AllowPotionRefresh,
                    EffectCapability.RetainOnDisconnect =>
                        AlchemyConfig.Loaded.RetainEffectsOnDisconnect,
                    EffectCapability.Resize =>
                        AlchemyConfig.Loaded.AllowGrowPotion || AlchemyConfig.Loaded.AllowShrinkPotion,
                    _ => true,
                }
            );

            // Recall, reshape, nutrition, temporal and resizing are carried out by EffectLib's
            // own built-in handler now - nothing to register here for them.

            // Feeds the shared HUD Alchemy's grow/shrink row and potion icons.
            EffectHud.Register(AlchemyHudProvider.Instance);

            RegisterCoatingWithEffectLib(api);
        }

        // Same idempotency note as RegisterWithEffectLib above.
        private static void RegisterCoatingWithEffectLib(ICoreAPI api)
        {
            CoatingPolicy.AllowCoating = () => AlchemyConfig.Loaded.AllowWeaponCoating;
            CoatingPolicy.MaxCharges = () => AlchemyConfig.Loaded.WeaponCoatCharges;
            CoatingPolicy.EffectMultiplier = () => AlchemyConfig.Loaded.WeaponCoatEffectMultiplier;
            CoatingPolicy.IsCoatableWeapon = col => PotionConsumableLogic.HasWeaponTag(api, col);
            CoatingPolicy.IsCoatableProjectile = PotionConsumableLogic.IsCoatableProjectile;
            CoatingPolicy.IsEffectCoatable = PotionConsumableLogic.IsCoatingAllowed;
            CoatingPolicy.ResolveLiquidEffect = stack =>
                PotionConsumableLogic.TryResolvePotion(stack, out string id, out float mul)
                    ? (id, mul)
                    : null;

            // Drinking-style side effects and exclusivity groups apply to a coated hit too.
            CoatingPolicy.ApplySideEffects = (potionId, entity, mul) =>
                PotionConsumableLogic.ApplySideEffects(entity, potionId, mul);
            CoatingPolicy.GetBlockReason = (potionId, player, ctx) =>
                PotionConsumableLogic.GetCoatingBlockReason(player, potionId, ctx);

            BarrelCoatingConfig.AllowBarrelCoating = () => AlchemyConfig.Loaded.AllowBarrelCoating;
            BarrelCoatingConfig.ConsumeLitres = () => AlchemyConfig.Loaded.WeaponCoatConsumeLitres;
            BarrelCoatingConfig.CheckLitres = () => AlchemyConfig.Loaded.WeaponCoatCheckLitres;

            // Combat Overhaul-managed weapons/arrows keep their coating in its own weapon-buff
            // storage instead, so its own on-hit logic (not EffectLib's) delivers the effect.
            CoatingPolicy.UsesAlternateWeaponStorage = CombatOverhaulCompat.ShouldUseBuffStorage;
            CoatingPolicy.UsesAlternateProjectileStorage =
                CombatOverhaulCompat.ShouldUseProjectileBuffStorage;
            CoatingPolicy.TryReadAlternateWeapon = stack =>
                CombatOverhaulCompat.TryGetCoating(stack, out string id, out string code, out float mul, out int charges)
                    ? (id, code, mul, charges)
                    : null;
            CoatingPolicy.WriteAlternateWeapon = CombatOverhaulCompat.SetCoating;
            CoatingPolicy.WriteAlternateProjectile = CombatOverhaulCompat.SetProjectileCoating;
        }

        public static void RegisterClasses(ICoreAPI api)
        {
            api.RegisterBlockClass("BlockPotionFlask", typeof(BlockPotionFlask));
            api.RegisterBlockEntityClass("BlockEntityPotionFlask", typeof(BlockEntityPotionFlask));
            api.RegisterItemClass("ItemPotion", typeof(ItemPotion));
            api.RegisterBlockClass("BlockHerbRacks", typeof(BlockHerbRacks));
            api.RegisterBlockEntityClass("HerbRacks", typeof(BlockEntityHerbRacks));
            api.RegisterCollectibleBehaviorClass(
                "PotionConsumable",
                typeof(PotionConsumableBehavior)
            );
            api.RegisterCollectibleBehaviorClass(
                "PotionConsumableLiquid",
                typeof(PotionConsumableLiquidBehavior)
            );
            api.RegisterCollectibleBehaviorClass(
                "PotionCoat",
                typeof(EffectLib.CollectibleBehaviorCoatable)
            );
            api.RegisterCollectibleBehaviorClass(
                "PotionCoatSource",
                typeof(PotionCoatSourceBehavior)
            );
            api.RegisterCollectibleBehaviorClass(
                "PotionCoatSourceLiquid",
                typeof(PotionCoatSourceLiquidBehavior)
            );
            api.RegisterItemClass("ItemStirringSpoon", typeof(ItemStirringSpoon));
            api.RegisterBlockClass("BlockCauldronFirepit", typeof(BlockCauldronFirepit));
            api.RegisterBlockClass("BlockThrowablePotionFlask", typeof(BlockThrowablePotionFlask));
            api.RegisterEntity("EntityThrownPotionFlask", typeof(EntityThrownPotionFlask));
            api.RegisterBlockEntityClass(
                "BlockEntityCauldronFirepit",
                typeof(BlockEntityCauldronFirepit)
            );
        }

        public override void StartPre(ICoreAPI api)
        {
            string cfgFileName = "alchemy.json";
            try
            {
                AlchemyConfig fromDisk;
                if ((fromDisk = api.LoadModConfig<AlchemyConfig>(cfgFileName)) == null)
                {
                    api.StoreModConfig(AlchemyConfig.Loaded, cfgFileName);
                }
                else
                {
                    AlchemyConfig.Loaded = fromDisk;
                }
            }
            catch
            {
                api.Logger.Error("Failed to load mod config. Reverting to default settings.");
                api.StoreModConfig(AlchemyConfig.Loaded, cfgFileName);
            }

            AlchemyConfig.Loaded.WriteToWorldConfig(api.World.Config);

            api.Logger.Debug("Loaded alchemy mod config into world properties.");

            base.StartPre(api);
        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            if (api.Side != EnumAppSide.Client)
                return;

            List<string> tagList =
            [
                .. AlchemyConfig
                    .Loaded.CoatableWeaponTags.Split(',')
                    .Select(t => t.Trim())
                    .Where(t => t.Length > 0),
            ];

            api.CollectibleTagRegistry.TryCreateTagSet(out TagSet coatableTags, tagList);

            foreach (CollectibleObject obj in api.World.Collectibles)
            {
                if (obj?.Code == null)
                    continue;

                // Coatable weapons are tag-matched; coatable projectiles are wildcard-code matched
                // against the admin-configured CoatableProjectilesCodes (default "*arrow*").
                bool isCoatable =
                    obj.Tags.Overlaps(coatableTags)
                    || PotionConsumableLogic.IsCoatableProjectile(obj);

                if (!isCoatable)
                    continue;

                obj.CollectibleBehaviors =
                [
                    .. obj.CollectibleBehaviors,
                    new EffectLib.CollectibleBehaviorCoatable(obj),
                ];
            }
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            // Size resync and the CanClimbAnywhere mirror are EffectLib's own concern now -
            // handled by EffectLibMod.StartClientSide for every mod built on it, not just this one.
            // JSON-defined potions register themselves too, via PotionConsumableBehavior's own
            // OnLoaded (EffectLib's generic self-registration) - nothing to scan here either.
            api.Network
                .GetChannel(ConfigSyncChannelName)
                .SetMessageHandler<AlchemyConfigSyncPacket>(packet =>
                    AlchemyConfig.Loaded.ApplySyncPacket(packet)
                );
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            // Attaching the effect behavior, resuming on login, suspending on disconnect and
            // clearing on death are all handled by EffectLib's own ModSystem.
            api.Event.PlayerJoin += SendConfigSync;

            base.StartServerSide(api);
        }

        private void SendConfigSync(IServerPlayer player)
        {
            ((ICoreServerAPI)api).Network
                .GetChannel(ConfigSyncChannelName)
                .SendPacket(AlchemyConfig.Loaded.ToSyncPacket(), player);
        }

        public override void Dispose()
        {
            CombatOverhaulCompat.Shutdown();
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;

            // remove our player join listener so we don't create memory leaks
            if (api is ICoreServerAPI sapi)
                sapi.Event.PlayerJoin -= SendConfigSync;

            // Deliberately not unregistering from EffectLib: both are process-wide statics that
            // Start re-establishes, and dropping them here would disable every handler-driven
            // potion for any world loaded later in the same session.
        }
    }
}
