/*json block glow
vertexFlags: {
    glowLevel: 255
},*/
/* Quick reference to all attributes that change the characters Stats:
   healingeffectivness, maxhealthExtraPoints, walkspeed, hungerrate, rangedWeaponsAcc, rangedWeaponsSpeed
   rangedWeaponsDamage, meleeWeaponsDamage, mechanicalsDamage, animalLootDropRate, forageDropRate, wildCropDropRate
   vesselContentsDropRate, oreDropRate, rustyGearDropRate, miningSpeedMul, animalSeekingRange, armorDurabilityLoss, bowDrawingStrength, wholeVesselLootChance, temporalGearTLRepairCost, animalHarvestingTime*/

namespace Alchemy.ModSystem
{
    using HarmonyLib;
    using System.Reflection;
    using Vintagestory.API.Client;
    using Vintagestory.API.Common;
    using Vintagestory.API.Server;
    using Alchemy.ModConfig;
    using Alchemy.Behavior;

    public class AlchemyMod : ModSystem
    {
        private IServerNetworkChannel serverChannel;
        private ICoreAPI api;

        public override void Start(ICoreAPI api)
        {
            this.api = api;
            base.Start(api);
            api.Logger.Debug("[Potion] Start");

            Harmony harmony = new("llama3013.Alchemy");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            RegisterClasses(api);
        }

        public static void RegisterClasses(ICoreAPI api)
        {
            api.RegisterBlockClass("BlockPotionFlask", typeof(Alchemy.Block.BlockPotionFlask));
            api.RegisterBlockEntityClass(
                "BlockEntityPotionFlask",
                typeof(Alchemy.BlockEntity.BlockEntityPotionFlask)
            );
            api.RegisterItemClass("ItemPotion", typeof(Alchemy.Item.ItemPotion));
            api.RegisterBlockClass("BlockHerbRacks", typeof(Alchemy.Block.BlockHerbRacks));
            api.RegisterBlockEntityClass(
                "HerbRacks",
                typeof(Alchemy.BlockEntity.BlockEntityHerbRacks)
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

            api.World.Config.SetBool("AllowArcherPotion", AlchemyConfig.Loaded.AllowArcherPotion);
            api.World.Config.SetBool("AllowGlowPotion", AlchemyConfig.Loaded.AllowGlowPotion);
            api.World.Config.SetBool(
                "AllowHealingEffectPotion",
                AlchemyConfig.Loaded.AllowHealingEffectPotion
            );
            api.World.Config.SetBool(
                "AllowHungerEnhancePotion",
                AlchemyConfig.Loaded.AllowHungerEnhancePotion
            );
            api.World.Config.SetBool(
                "AllowHungerSupressPotion",
                AlchemyConfig.Loaded.AllowHungerSupressPotion
            );
            api.World.Config.SetBool("AllowHunterPotion", AlchemyConfig.Loaded.AllowHunterPotion);
            api.World.Config.SetBool("AllowLooterPotion", AlchemyConfig.Loaded.AllowLooterPotion);
            api.World.Config.SetBool("AllowMeleePotion", AlchemyConfig.Loaded.AllowMeleePotion);
            api.World.Config.SetBool("AllowMiningPotion", AlchemyConfig.Loaded.AllowMiningPotion);
            api.World.Config.SetBool(
                "AllowNutritionPotion",
                AlchemyConfig.Loaded.AllowNutritionPotion
            );
            api.World.Config.SetBool("AllowPoisonPotion", AlchemyConfig.Loaded.AllowPoisonPotion);
            api.World.Config.SetBool(
                "AllowPredatorPotion",
                AlchemyConfig.Loaded.AllowPredatorPotion
            );
            api.World.Config.SetBool("AllowRecallPotion", AlchemyConfig.Loaded.AllowRecallPotion);
            api.World.Config.SetBool("AllowRegenPotion", AlchemyConfig.Loaded.AllowRegenPotion);
            api.World.Config.SetBool(
                "AllowScentMaskPotion",
                AlchemyConfig.Loaded.AllowScentMaskPotion
            );
            api.World.Config.SetBool("AllowSpeedPotion", AlchemyConfig.Loaded.AllowSpeedPotion);
            api.World.Config.SetBool(
                "AllowTemporalPotion",
                AlchemyConfig.Loaded.AllowTemporalPotion
            );
            api.World.Config.SetBool(
                "AllowVitalityPotion",
                AlchemyConfig.Loaded.AllowVitalityPotion
            );
            api.World.Config.SetBool(
                "AllowWaterBreathePotion",
                AlchemyConfig.Loaded.AllowWaterBreathePotion
            );

            api.World.Config.SetBool("AllowHerbballs", AlchemyConfig.Loaded.AllowHerbballs);
            api.World.Config.SetBool("AllowMediumPotions", AlchemyConfig.Loaded.AllowMediumPotions);
            api.World.Config.SetBool("AllowStrongPotions", AlchemyConfig.Loaded.AllowStrongPotions);
            // api.World.Config.SetBool("AllowCuttings", AlchemyConfig.Loaded.AllowCuttings);

            api.World.Config.SetBool("AllowClayFlasks", AlchemyConfig.Loaded.AllowClayFlasks);
            api.World.Config.SetBool("AllowSmallFlasks", AlchemyConfig.Loaded.AllowSmallFlasks);
            api.World.Config.SetBool("AllowMediumFlasks", AlchemyConfig.Loaded.AllowMediumFlasks);
            api.World.Config.SetBool("AllowLargeFlasks", AlchemyConfig.Loaded.AllowLargeFlasks);

            api.World.Config.SetBool("AllowHerbRackMolds", AlchemyConfig.Loaded.AllowHerbRackMolds);
            api.World.Config.SetBool("AllowHerbRacks", AlchemyConfig.Loaded.AllowHerbRacks);
            // api.World.Config.SetBool(
            //     "AllowDecorativeRacks",
            //     AlchemyConfig.Loaded.AllowDecorativeRacks
            // );

            api.Logger.Debug("Loaded alchemy mod config into world properties.");

            base.StartPre(api);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            api.Network
                .RegisterChannel("alchemy")
                .RegisterMessageType<SyncClientPacket>()
                .SetMessageHandler<SyncClientPacket>(packet =>
                {
                    AlchemyConfig.Loaded.AllowArcherPotion = packet.AllowArcherPotion;
                    Mod.Logger.Event(
                        $"Received AllowArcherPotion of {packet.AllowArcherPotion} from server"
                    );

                    AlchemyConfig.Loaded.AllowGlowPotion = packet.AllowGlowPotion;
                    Mod.Logger.Event(
                        $"Received AllowGlowPotion of {packet.AllowGlowPotion} from server"
                    );

                    AlchemyConfig.Loaded.AllowHealingEffectPotion = packet.AllowHealingEffectPotion;
                    Mod.Logger.Event(
                        $"Received AllowHealingEffectPotion of {packet.AllowHealingEffectPotion} from server"
                    );

                    AlchemyConfig.Loaded.AllowHungerEnhancePotion = packet.AllowHungerEnhancePotion;
                    Mod.Logger.Event(
                        $"Received AllowHungerEnhancePotion of {packet.AllowHungerEnhancePotion} from server"
                    );

                    AlchemyConfig.Loaded.AllowHungerSupressPotion = packet.AllowHungerSupressPotion;
                    Mod.Logger.Event(
                        $"Received AllowHungerSupressPotion of {packet.AllowHungerSupressPotion} from server"
                    );

                    AlchemyConfig.Loaded.AllowHunterPotion = packet.AllowHunterPotion;
                    Mod.Logger.Event(
                        $"Received AllowHunterPotion of {packet.AllowHunterPotion} from server"
                    );

                    AlchemyConfig.Loaded.AllowLooterPotion = packet.AllowLooterPotion;
                    Mod.Logger.Event(
                        $"Received AllowLooterPotion of {packet.AllowLooterPotion} from server"
                    );

                    AlchemyConfig.Loaded.AllowMeleePotion = packet.AllowMeleePotion;
                    Mod.Logger.Event(
                        $"Received AllowMeleePotion of {packet.AllowMeleePotion} from server"
                    );

                    AlchemyConfig.Loaded.AllowMiningPotion = packet.AllowMiningPotion;
                    Mod.Logger.Event(
                        $"Received AllowMiningPotion of {packet.AllowMiningPotion} from server"
                    );

                    AlchemyConfig.Loaded.AllowNutritionPotion = packet.AllowNutritionPotion;
                    Mod.Logger.Event(
                        $"Received AllowNutritionPotion of {packet.AllowNutritionPotion} from server"
                    );

                    AlchemyConfig.Loaded.AllowPoisonPotion = packet.AllowPoisonPotion;
                    Mod.Logger.Event(
                        $"Received AllowPoisonPotion of {packet.AllowPoisonPotion} from server"
                    );

                    AlchemyConfig.Loaded.AllowPredatorPotion = packet.AllowPredatorPotion;
                    Mod.Logger.Event(
                        $"Received AllowPredatorPotion of {packet.AllowPredatorPotion} from server"
                    );

                    AlchemyConfig.Loaded.AllowRecallPotion = packet.AllowRecallPotion;
                    Mod.Logger.Event(
                        $"Received AllowRecallPotion of {packet.AllowRecallPotion} from server"
                    );

                    AlchemyConfig.Loaded.AllowRegenPotion = packet.AllowRegenPotion;
                    Mod.Logger.Event(
                        $"Received AllowRegenPotion of {packet.AllowRegenPotion} from server"
                    );

                    AlchemyConfig.Loaded.AllowScentMaskPotion = packet.AllowScentMaskPotion;
                    Mod.Logger.Event(
                        $"Received AllowScentMaskPotion of {packet.AllowScentMaskPotion} from server"
                    );

                    AlchemyConfig.Loaded.AllowSpeedPotion = packet.AllowSpeedPotion;
                    Mod.Logger.Event(
                        $"Received AllowSpeedPotion of {packet.AllowSpeedPotion} from server"
                    );

                    AlchemyConfig.Loaded.AllowTemporalPotion = packet.AllowTemporalPotion;
                    Mod.Logger.Event(
                        $"Received AllowTemporalPotion of {packet.AllowTemporalPotion} from server"
                    );

                    AlchemyConfig.Loaded.AllowVitalityPotion = packet.AllowVitalityPotion;
                    Mod.Logger.Event(
                        $"Received AllowVitalityPotion of {packet.AllowVitalityPotion} from server"
                    );

                    AlchemyConfig.Loaded.AllowWaterBreathePotion = packet.AllowWaterBreathePotion;
                    Mod.Logger.Event(
                        $"Received AllowWaterBreathePotion of {packet.AllowWaterBreathePotion} from server"
                    );

                    AlchemyConfig.Loaded.AllowReshapePotion = packet.AllowReshapePotion;
                    Mod.Logger.Event(
                        $"Received AllowReshapePotion of {packet.AllowReshapePotion} from server"
                    );

                    AlchemyConfig.Loaded.AllowHerbballs = packet.AllowHerbballs;
                    Mod.Logger.Event(
                        $"Received AllowHerbballs of {packet.AllowHerbballs} from server"
                    );
                    AlchemyConfig.Loaded.AllowMediumPotions = packet.AllowMediumPotions;
                    Mod.Logger.Event(
                        $"Received AllowMediumPotions of {packet.AllowMediumPotions} from server"
                    );
                    AlchemyConfig.Loaded.AllowStrongPotions = packet.AllowStrongPotions;
                    Mod.Logger.Event(
                        $"Received AllowStrongPotions of {packet.AllowStrongPotions} from server"
                    );
                    // AlchemyConfig.Loaded.AllowCuttings = packet.AllowCuttings;
                    // Mod.Logger.Event(
                    //     $"Received AllowCuttings of {packet.AllowCuttings} from server"
                    // );

                    AlchemyConfig.Loaded.AllowClayFlasks = packet.AllowClayFlasks;
                    Mod.Logger.Event(
                        $"Received AllowClayFlasks of {packet.AllowClayFlasks} from server"
                    );

                    AlchemyConfig.Loaded.AllowSmallFlasks = packet.AllowSmallFlasks;
                    Mod.Logger.Event(
                        $"Received AllowSmallFlasks of {packet.AllowSmallFlasks} from server"
                    );

                    AlchemyConfig.Loaded.AllowMediumFlasks = packet.AllowMediumFlasks;
                    Mod.Logger.Event(
                        $"Received AllowMediumFlasks of {packet.AllowMediumFlasks} from server"
                    );

                    AlchemyConfig.Loaded.AllowLargeFlasks = packet.AllowLargeFlasks;
                    Mod.Logger.Event(
                        $"Received AllowLargeFlasks of {packet.AllowLargeFlasks} from server"
                    );

                    AlchemyConfig.Loaded.AllowHerbRackMolds = packet.AllowHerbRackMolds;
                    Mod.Logger.Event(
                        $"Received AllowHerbRackMolds of {packet.AllowHerbRackMolds} from server"
                    );
                    AlchemyConfig.Loaded.AllowHerbRacks = packet.AllowHerbRacks;
                    Mod.Logger.Event(
                        $"Received AllowHerbRacks of {packet.AllowHerbRacks} from server"
                    );
                    // AlchemyConfig.Loaded.AllowDecorativeRacks = packet.AllowDecorativeRacks;
                    // Mod.Logger.Event(
                    //     $"Received AllowDecorativeRacks of {packet.AllowDecorativeRacks} from server"
                    // );

                    AlchemyConfig.Loaded.WeakPotionMultiplier = packet.WeakPotionMultiplier;
                    Mod.Logger.Event(
                        $"Received WeakPotionMultiplier of {packet.WeakPotionMultiplier} from server"
                    );
                    AlchemyConfig.Loaded.MediumPotionMultiplier = packet.MediumPotionMultiplier;
                    Mod.Logger.Event(
                        $"Received MediumPotionMultiplier of {packet.MediumPotionMultiplier} from server"
                    );
                    AlchemyConfig.Loaded.StrongPotionMultiplier = packet.StrongPotionMultiplier;
                    Mod.Logger.Event(
                        $"Received StrongPotionMultiplier of {packet.StrongPotionMultiplier} from server"
                    );

                    AlchemyConfig.Loaded.ArcherPotionAcc = packet.ArcherPotionAcc;
                    Mod.Logger.Event(
                        $"Received ArcherPotionAcc of {packet.ArcherPotionAcc} from server"
                    );
                    AlchemyConfig.Loaded.ArcherPotionDamage = packet.ArcherPotionDamage;
                    Mod.Logger.Event(
                        $"Received ArcherPotionDamage of {packet.ArcherPotionDamage} from server"
                    );
                    AlchemyConfig.Loaded.ArcherPotionSpeed = packet.ArcherPotionSpeed;
                    Mod.Logger.Event(
                        $"Received ArcherPotionSpeed of {packet.ArcherPotionSpeed} from server"
                    );
                    AlchemyConfig.Loaded.ArcherPotionDuration = packet.ArcherPotionDuration;
                    Mod.Logger.Event(
                        $"Received ArcherPotionDuration of {packet.ArcherPotionDuration} from server"
                    );
                    AlchemyConfig.Loaded.HealingEffectPotionValue = packet.HealingEffectPotionValue;
                    Mod.Logger.Event(
                        $"Received HealingEffectPotionValue of {packet.HealingEffectPotionValue} from server"
                    );
                    AlchemyConfig.Loaded.HealingEffectPotionDuration =
                        packet.HealingEffectPotionDuration;
                    Mod.Logger.Event(
                        $"Received HealingEffectPotionDuration of {packet.HealingEffectPotionDuration} from server"
                    );
                    AlchemyConfig.Loaded.HungerEnhancePotionValue = packet.HungerEnhancePotionValue;
                    Mod.Logger.Event(
                        $"Received HungerEnhancePotionValue of {packet.HungerEnhancePotionValue} from server"
                    );
                    AlchemyConfig.Loaded.HungerEnhancePotionDuration =
                        packet.HungerEnhancePotionDuration;
                    Mod.Logger.Event(
                        $"Received HungerEnhancePotionDuration of {packet.HungerEnhancePotionDuration} from server"
                    );
                    AlchemyConfig.Loaded.HungerSupressPotionValue = packet.HungerSupressPotionValue;
                    Mod.Logger.Event(
                        $"Received HungerSupressPotionValue of {packet.HungerSupressPotionValue} from server"
                    );
                    AlchemyConfig.Loaded.HungerSupressPotionDuration =
                        packet.HungerSupressPotionDuration;
                    Mod.Logger.Event(
                        $"Received HungerSupressPotionDuration of {packet.HungerSupressPotionDuration} from server"
                    );
                    AlchemyConfig.Loaded.HunterPotionAnimalDrop = packet.HunterPotionAnimalDrop;
                    Mod.Logger.Event(
                        $"Received HunterPotionAnimalDrop of {packet.HunterPotionAnimalDrop} from server"
                    );
                    AlchemyConfig.Loaded.HunterPotionAnimalSeek = packet.HunterPotionAnimalSeek;
                    Mod.Logger.Event(
                        $"Received HunterPotionAnimalSeek of {packet.HunterPotionAnimalSeek} from server"
                    );
                    AlchemyConfig.Loaded.HunterPotionForageDrop = packet.HunterPotionForageDrop;
                    Mod.Logger.Event(
                        $"Received HunterPotionForageDrop of {packet.HunterPotionForageDrop} from server"
                    );
                    AlchemyConfig.Loaded.HunterPotionWildDrop = packet.HunterPotionWildDrop;
                    Mod.Logger.Event(
                        $"Received HunterPotionWildDrop of {packet.HunterPotionWildDrop} from server"
                    );
                    AlchemyConfig.Loaded.HunterPotionDuration = packet.HunterPotionDuration;
                    Mod.Logger.Event(
                        $"Received HunterPotionDuration of {packet.HunterPotionDuration} from server"
                    );
                    AlchemyConfig.Loaded.LooterPotionForageDrop = packet.LooterPotionForageDrop;
                    Mod.Logger.Event(
                        $"Received LooterPotionForageDrop of {packet.LooterPotionForageDrop} from server"
                    );
                    AlchemyConfig.Loaded.LooterPotionGearDrop = packet.LooterPotionGearDrop;
                    Mod.Logger.Event(
                        $"Received LooterPotionGearDrop of {packet.LooterPotionGearDrop} from server"
                    );
                    AlchemyConfig.Loaded.LooterPotionVesselContentDrop =
                        packet.LooterPotionVesselContentDrop;
                    Mod.Logger.Event(
                        $"Received LooterPotionVesselContentDrop of {packet.LooterPotionVesselContentDrop} from server"
                    );
                    AlchemyConfig.Loaded.LooterPotionWildDrop = packet.LooterPotionWildDrop;
                    Mod.Logger.Event(
                        $"Received LooterPotionWildDrop of {packet.LooterPotionWildDrop} from server"
                    );
                    AlchemyConfig.Loaded.LooterPotionDuration = packet.LooterPotionDuration;
                    Mod.Logger.Event(
                        $"Received LooterPotionDuration of {packet.LooterPotionDuration} from server"
                    );
                    AlchemyConfig.Loaded.MeleePotionDamage = packet.MeleePotionDamage;
                    Mod.Logger.Event(
                        $"Received MeleePotionDamage of {packet.MeleePotionDamage} from server"
                    );
                    AlchemyConfig.Loaded.MeleePotionDuration = packet.MeleePotionDuration;
                    Mod.Logger.Event(
                        $"Received MeleePotionDuration of {packet.MeleePotionDuration} from server"
                    );
                    AlchemyConfig.Loaded.MiningPotionSpeed = packet.MiningPotionSpeed;
                    Mod.Logger.Event(
                        $"Received MiningPotionSpeed of {packet.MiningPotionSpeed} from server"
                    );
                    AlchemyConfig.Loaded.MiningPotionOreDrop = packet.MiningPotionOreDrop;
                    Mod.Logger.Event(
                        $"Received MiningPotionOreDrop of {packet.MiningPotionOreDrop} from server"
                    );
                    AlchemyConfig.Loaded.MiningPotionDuration = packet.MiningPotionDuration;
                    Mod.Logger.Event(
                        $"Received MiningPotionDuration of {packet.MiningPotionDuration} from server"
                    );
                    AlchemyConfig.Loaded.PoisonPotionHealth = packet.PoisonPotionHealth;
                    Mod.Logger.Event(
                        $"Received PoisonPotionHealth of {packet.PoisonPotionHealth} from server"
                    );
                    AlchemyConfig.Loaded.PoisonPotionTickSec = packet.PoisonPotionTickSec;
                    Mod.Logger.Event(
                        $"Received PoisonPotionTickSec of {packet.PoisonPotionTickSec} from server"
                    );
                    AlchemyConfig.Loaded.PoisonPotionDuration = packet.PoisonPotionDuration;
                    Mod.Logger.Event(
                        $"Received PoisonPotionDuration of {packet.PoisonPotionDuration} from server"
                    );
                    AlchemyConfig.Loaded.PoisonPotionIgnoreArmour = packet.PoisonPotionIgnoreArmour;
                    Mod.Logger.Event(
                        $"Received PoisonPotionIgnoreArmour of {packet.PoisonPotionIgnoreArmour} from server"
                    );
                    AlchemyConfig.Loaded.PredatorPotionAnimalSeek = packet.PredatorPotionAnimalSeek;
                    Mod.Logger.Event(
                        $"Received PredatorPotionAnimalSeek of {packet.PredatorPotionAnimalSeek} from server"
                    );
                    AlchemyConfig.Loaded.PredatorPotionDuration = packet.PredatorPotionDuration;
                    Mod.Logger.Event(
                        $"Received PredatorPotionDuration of {packet.PredatorPotionDuration} from server"
                    );
                    AlchemyConfig.Loaded.RegenPotionHealth = packet.RegenPotionHealth;
                    Mod.Logger.Event(
                        $"Received RegenPotionHealth of {packet.RegenPotionHealth} from server"
                    );
                    AlchemyConfig.Loaded.RegenPotionTickSec = packet.RegenPotionTickSec;
                    Mod.Logger.Event(
                        $"Received RegenPotionTickSec of {packet.RegenPotionTickSec} from server"
                    );
                    AlchemyConfig.Loaded.RegenPotionDuration = packet.RegenPotionDuration;
                    Mod.Logger.Event(
                        $"Received RegenPotionDuration of {packet.RegenPotionDuration} from server"
                    );
                    AlchemyConfig.Loaded.RegenPotionIgnoreArmour = packet.RegenPotionIgnoreArmour;
                    Mod.Logger.Event(
                        $"Received RegenPotionIgnoreArmour of {packet.RegenPotionIgnoreArmour} from server"
                    );
                    AlchemyConfig.Loaded.ScentMaskPotionAnimalSeek =
                        packet.ScentMaskPotionAnimalSeek;
                    Mod.Logger.Event(
                        $"Received ScentMaskPotionAnimalSeek of {packet.ScentMaskPotionAnimalSeek} from server"
                    );
                    AlchemyConfig.Loaded.ScentMaskPotionDuration = packet.ScentMaskPotionDuration;
                    Mod.Logger.Event(
                        $"Received ScentMaskPotionDuration of {packet.ScentMaskPotionDuration} from server"
                    );
                    AlchemyConfig.Loaded.SpeedPotionValue = packet.SpeedPotionValue;
                    Mod.Logger.Event(
                        $"Received SpeedPotionValue of {packet.SpeedPotionValue} from server"
                    );
                    AlchemyConfig.Loaded.SpeedPotionDuration = packet.SpeedPotionDuration;
                    Mod.Logger.Event(
                        $"Received SpeedPotionDuration of {packet.SpeedPotionDuration} from server"
                    );
                    AlchemyConfig.Loaded.VitalityPotionMaxHealth = packet.VitalityPotionMaxHealth;
                    Mod.Logger.Event(
                        $"Received VitalityPotionMaxHealth of {packet.VitalityPotionMaxHealth} from server"
                    );
                    AlchemyConfig.Loaded.VitalityPotionDuration = packet.VitalityPotionDuration;
                    Mod.Logger.Event(
                        $"Received VitalityPotionDuration of {packet.VitalityPotionDuration} from server"
                    );
                    AlchemyConfig.Loaded.GlowPotionDuration = packet.GlowPotionDuration;
                    Mod.Logger.Event(
                        $"Received GlowPotionDuration of {packet.GlowPotionDuration} from server"
                    );
                    AlchemyConfig.Loaded.WaterBreathePotionDuration =
                        packet.WaterBreathePotionDuration;
                    Mod.Logger.Event(
                        $"Received WaterBreathePotionDuration of {packet.WaterBreathePotionDuration} from server"
                    );
                });
        }

        /* This override is to add the PotionFixBehavior to the player and to reset all of the potion stats to default */
        public override void StartServerSide(ICoreServerAPI api)
        {
            // send connecting players the config settings
            api.Event.PlayerJoin += OnPlayerJoin; // add method so we can remove it in dispose to prevent memory leaks
            // register network channel to send data to clients
            serverChannel = api.Network
                .RegisterChannel("alchemy")
                .RegisterMessageType<SyncClientPacket>()
                .SetMessageHandler<SyncClientPacket>(
                    (player, packet) => {
                        /* do nothing. idk why this handler is even needed, but it is */
                    }
                );
            api.Event.PlayerNowPlaying += iServerPlayer =>
            {
                if (iServerPlayer.Entity is not null)
                {
                    EntityPlayer entity = iServerPlayer.Entity;
                    entity.AddBehavior(new PotionFixBehavior(entity));

                    api.Logger.VerboseDebug(
                        "[Potion] Adding PotionFixBehavior to spawned EntityPlayer"
                    );
                    EntityPlayer player = iServerPlayer.Entity;
                    TempEffect.ResetAllTempStats(player);
                    TempEffect.ResetAllAttrListeners(player, "potionid", "tickpotionid");
                    api.Logger.VerboseDebug("potion player ready");
                }
            };
            base.StartServerSide(api);
        }

        private void OnPlayerJoin(IServerPlayer player)
        {
            // send the connecting player the settings it needs to be synced
            serverChannel.SendPacket(
                new SyncClientPacket
                {
                    AllowArcherPotion = AlchemyConfig.Loaded.AllowArcherPotion,
                    AllowGlowPotion = AlchemyConfig.Loaded.AllowGlowPotion,
                    AllowHealingEffectPotion = AlchemyConfig.Loaded.AllowHealingEffectPotion,
                    AllowHungerEnhancePotion = AlchemyConfig.Loaded.AllowHungerEnhancePotion,
                    AllowHungerSupressPotion = AlchemyConfig.Loaded.AllowHungerSupressPotion,
                    AllowHunterPotion = AlchemyConfig.Loaded.AllowHunterPotion,
                    AllowLooterPotion = AlchemyConfig.Loaded.AllowLooterPotion,
                    AllowMeleePotion = AlchemyConfig.Loaded.AllowMeleePotion,
                    AllowMiningPotion = AlchemyConfig.Loaded.AllowMiningPotion,
                    AllowNutritionPotion = AlchemyConfig.Loaded.AllowNutritionPotion,
                    AllowPoisonPotion = AlchemyConfig.Loaded.AllowPoisonPotion,
                    AllowPredatorPotion = AlchemyConfig.Loaded.AllowPredatorPotion,
                    AllowRecallPotion = AlchemyConfig.Loaded.AllowRecallPotion,
                    AllowRegenPotion = AlchemyConfig.Loaded.AllowRegenPotion,
                    AllowScentMaskPotion = AlchemyConfig.Loaded.AllowScentMaskPotion,
                    AllowSpeedPotion = AlchemyConfig.Loaded.AllowSpeedPotion,
                    AllowTemporalPotion = AlchemyConfig.Loaded.AllowTemporalPotion,
                    AllowVitalityPotion = AlchemyConfig.Loaded.AllowVitalityPotion,
                    AllowWaterBreathePotion = AlchemyConfig.Loaded.AllowWaterBreathePotion,
                    AllowReshapePotion = AlchemyConfig.Loaded.AllowReshapePotion,
                    AllowHerbballs = AlchemyConfig.Loaded.AllowHerbballs,
                    AllowMediumPotions = AlchemyConfig.Loaded.AllowMediumPotions,
                    AllowStrongPotions = AlchemyConfig.Loaded.AllowStrongPotions,
                    // AllowCuttings = AlchemyConfig.Loaded.AllowCuttings,

                    AllowClayFlasks = AlchemyConfig.Loaded.AllowClayFlasks,
                    AllowSmallFlasks = AlchemyConfig.Loaded.AllowSmallFlasks,
                    AllowMediumFlasks = AlchemyConfig.Loaded.AllowMediumFlasks,
                    AllowLargeFlasks = AlchemyConfig.Loaded.AllowLargeFlasks,
                    AllowHerbRackMolds = AlchemyConfig.Loaded.AllowHerbRackMolds,
                    AllowHerbRacks = AlchemyConfig.Loaded.AllowHerbRacks,
                    // AllowDecorativeRacks = AlchemyConfig.Loaded.AllowDecorativeRacks,

                    WeakPotionMultiplier = AlchemyConfig.Loaded.WeakPotionMultiplier,
                    MediumPotionMultiplier = AlchemyConfig.Loaded.MediumPotionMultiplier,
                    StrongPotionMultiplier = AlchemyConfig.Loaded.StrongPotionMultiplier,
                    ArcherPotionAcc = AlchemyConfig.Loaded.ArcherPotionAcc,
                    ArcherPotionDamage = AlchemyConfig.Loaded.ArcherPotionDamage,
                    ArcherPotionSpeed = AlchemyConfig.Loaded.ArcherPotionSpeed,
                    ArcherPotionDuration = AlchemyConfig.Loaded.ArcherPotionDuration,
                    HealingEffectPotionValue = AlchemyConfig.Loaded.HealingEffectPotionValue,
                    HealingEffectPotionDuration = AlchemyConfig.Loaded.HealingEffectPotionDuration,
                    HungerEnhancePotionValue = AlchemyConfig.Loaded.HungerEnhancePotionValue,
                    HungerEnhancePotionDuration = AlchemyConfig.Loaded.HungerEnhancePotionDuration,
                    HungerSupressPotionValue = AlchemyConfig.Loaded.HungerSupressPotionValue,
                    HungerSupressPotionDuration = AlchemyConfig.Loaded.HungerSupressPotionDuration,
                    HunterPotionAnimalDrop = AlchemyConfig.Loaded.HunterPotionAnimalDrop,
                    HunterPotionAnimalSeek = AlchemyConfig.Loaded.HunterPotionAnimalSeek,
                    HunterPotionForageDrop = AlchemyConfig.Loaded.HunterPotionForageDrop,
                    HunterPotionWildDrop = AlchemyConfig.Loaded.HunterPotionWildDrop,
                    HunterPotionDuration = AlchemyConfig.Loaded.HunterPotionDuration,
                    LooterPotionForageDrop = AlchemyConfig.Loaded.LooterPotionForageDrop,
                    LooterPotionGearDrop = AlchemyConfig.Loaded.LooterPotionGearDrop,
                    LooterPotionVesselContentDrop = AlchemyConfig
                        .Loaded
                        .LooterPotionVesselContentDrop,
                    LooterPotionWildDrop = AlchemyConfig.Loaded.LooterPotionWildDrop,
                    LooterPotionDuration = AlchemyConfig.Loaded.LooterPotionDuration,
                    MeleePotionDamage = AlchemyConfig.Loaded.MeleePotionDamage,
                    MeleePotionDuration = AlchemyConfig.Loaded.MeleePotionDuration,
                    MiningPotionSpeed = AlchemyConfig.Loaded.MiningPotionSpeed,
                    MiningPotionOreDrop = AlchemyConfig.Loaded.MiningPotionOreDrop,
                    MiningPotionDuration = AlchemyConfig.Loaded.MiningPotionDuration,
                    PoisonPotionHealth = AlchemyConfig.Loaded.PoisonPotionHealth,
                    PoisonPotionTickSec = AlchemyConfig.Loaded.PoisonPotionTickSec,
                    PoisonPotionDuration = AlchemyConfig.Loaded.PoisonPotionDuration,
                    PoisonPotionIgnoreArmour = AlchemyConfig.Loaded.PoisonPotionIgnoreArmour,
                    PredatorPotionAnimalSeek = AlchemyConfig.Loaded.PredatorPotionAnimalSeek,
                    PredatorPotionDuration = AlchemyConfig.Loaded.PredatorPotionDuration,
                    RegenPotionHealth = AlchemyConfig.Loaded.RegenPotionHealth,
                    RegenPotionTickSec = AlchemyConfig.Loaded.RegenPotionTickSec,
                    RegenPotionDuration = AlchemyConfig.Loaded.RegenPotionDuration,
                    RegenPotionIgnoreArmour = AlchemyConfig.Loaded.RegenPotionIgnoreArmour,
                    ScentMaskPotionAnimalSeek = AlchemyConfig.Loaded.ScentMaskPotionAnimalSeek,
                    ScentMaskPotionDuration = AlchemyConfig.Loaded.ScentMaskPotionDuration,
                    SpeedPotionValue = AlchemyConfig.Loaded.SpeedPotionValue,
                    SpeedPotionDuration = AlchemyConfig.Loaded.SpeedPotionDuration,
                    VitalityPotionMaxHealth = AlchemyConfig.Loaded.VitalityPotionMaxHealth,
                    VitalityPotionDuration = AlchemyConfig.Loaded.VitalityPotionDuration,
                    GlowPotionDuration = AlchemyConfig.Loaded.GlowPotionDuration,
                    WaterBreathePotionDuration = AlchemyConfig.Loaded.WaterBreathePotionDuration
                },
                player
            );
        }

        public override void Dispose()
        {
            // remove our player join listener so we dont create memory leaks
            if (api is ICoreServerAPI sapi)
            {
                sapi.Event.PlayerJoin -= OnPlayerJoin;
            }
        }
    }
}
