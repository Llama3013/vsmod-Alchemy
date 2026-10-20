// Maybe change recipe for archer flask to not require orange mallow since its semi rare for normal climate worlds
/*json block glow
vertexFlags: {
    glowLevel: 255
},*/
/* Quick reference to all attributes that change the characters Stats:
   healingeffectivness, maxhealthExtraPoints, walkspeed, hungerrate, rangedWeaponsAcc, rangedWeaponsSpeed
   rangedWeaponsDamage, meleeWeaponsDamage, mechanicalsDamage, animalLootDropRate, forageDropRate, wildCropDropRate
   vesselContentsDropRate, oreDropRate, rustyGearDropRate, miningSpeedMul, animalSeekingRange, armorDurabilityLoss, bowDrawingStrength, wholeVesselLootChance, temporalGearTLRepairCost, animalHarvestingTime*/
using System.Linq;
using System.Reflection;
using EffectLib;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
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
            RegisterClasses(api);
            RegisterEffectsOnce(api.Logger);

            CoatingPolicy.Init(api);
            CombatOverhaulCompat.Init(api);

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

                EffectRegistry.Reserve(PotionDefinitions.All.Select(def => def.Id));

                effectsRegistered = true;
            }
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
            base.AssetsFinalize(api);

            if (api.Side != EnumAppSide.Client)
                return;

            AttachCoatableBehaviors(api);
        }

        private static void AttachCoatableBehaviors(ICoreAPI api)
        {
            foreach (CollectibleObject obj in api.World.Collectibles)
            {
                if (obj?.Code == null || obj.CollectibleBehaviors.Any(b => b is CoatableBehavior))
                    continue;

                if (!CoatingPolicy.IsCoatableWeapon(obj) && !CoatingPolicy.IsCoatableProjectile(obj))
                    continue;

                obj.CollectibleBehaviors = [.. obj.CollectibleBehaviors, new CoatableBehavior(obj)];
            }
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            // JSON-defined potions register themselves via PotionConsumableBehavior's OnLoaded.
            api.Network
                .GetChannel(ConfigSyncChannelName)
                .SetMessageHandler<AlchemyConfigSyncPacket>(packet =>
                {
                    AlchemyConfig.Loaded.ApplySyncPacket(packet);
                    // The server's coatable tags/codes may differ from this client's own config.
                    CoatingPolicy.InvalidateCaches();
                    AttachCoatableBehaviors(api);
                });
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
            harmony?.UnpatchAll(HarmonyId);
            harmony = null;

            // remove our player join listener so we don't create memory leaks
            if (api is ICoreServerAPI sapi)
                sapi.Event.PlayerJoin -= SendConfigSync;

            CombatOverhaulCompat.Shutdown();

            // Deliberately not unregistering from EffectLib: both are process-wide statics that
            // Start re-establishes, and dropping them here would disable every handler-driven
            // potion for any world loaded later in the same session.
        }
    }
}
