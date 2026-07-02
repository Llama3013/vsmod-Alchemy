using System.Linq;
using System.Reflection;
using Vintagestory.API.Datastructures;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    public class AlchemyConfig
    {
        public static AlchemyConfig Loaded { get; set; } = new AlchemyConfig();

        private static readonly PropertyInfo[] SyncProps =
        [
            .. typeof(AlchemyConfig)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite),
        ];

        public void WriteToWorldConfig(ITreeAttribute cfg)
        {
            foreach (PropertyInfo prop in SyncProps)
            {
                switch (prop.GetValue(this))
                {
                    case bool b:
                        cfg.SetBool(prop.Name, b);
                        break;
                    case int i:
                        cfg.SetInt(prop.Name, i);
                        break;
                    case float f:
                        cfg.SetFloat(prop.Name, f);
                        break;
                    case string s:
                        cfg.SetString(prop.Name, s);
                        break;
                }
            }
        }

        // I have to make sure that the client reads the config at LevelFinalize or later otherwise the client will read client config instead of server config, I wish I learnt this way earlier
        public void ReadFromWorldConfig(ITreeAttribute cfg)
        {
            foreach (PropertyInfo prop in SyncProps)
            {
                switch (prop.GetValue(this))
                {
                    case bool b:
                        prop.SetValue(this, cfg.GetBool(prop.Name, b));
                        break;
                    case int i:
                        prop.SetValue(this, cfg.GetInt(prop.Name, i));
                        break;
                    case float f:
                        prop.SetValue(this, cfg.GetFloat(prop.Name, f));
                        break;
                    case string s:
                        prop.SetValue(this, cfg.GetString(prop.Name, s));
                        break;
                }
            }
        }

        public string Comment { get; } =
            "Set any potions you want to Allow to true. This will remove them from the multiplayer/singleplayer server. Make sure to remove any potions/potion bases that are in your world before disabling otherwise the world will provide some errors that can probably be ignored. Changing this field won't do anything.";
        public bool AllowRecallPotion { get; set; } = true;
        public bool AllowGlowPotion { get; set; } = true;
        public bool AllowWaterBreathePotion { get; set; } = true;
        public bool AllowColdResistPotion { get; set; } = true;
        public bool AllowNutritionPotion { get; set; } = true;
        public bool AllowTemporalPotion { get; set; } = true;
        public bool AllowReshapePotion { get; set; } = true;
        public bool AllowGrowPotion { get; set; } = true;
        public bool AllowShrinkPotion { get; set; } = true;

        public bool AllowArcherPotion { get; set; } = true;
        public bool AllowHealingEffectPotion { get; set; } = true;
        public bool AllowHungerEnhancePotion { get; set; } = true;
        public bool AllowHungerSupressPotion { get; set; } = true;
        public bool AllowHunterPotion { get; set; } = true;
        public bool AllowLooterPotion { get; set; } = true;
        public bool AllowMeleePotion { get; set; } = true;
        public bool AllowMiningPotion { get; set; } = true;
        public bool AllowPoisonPotion { get; set; } = true;
        public bool AllowPredatorPotion { get; set; } = true;
        public bool AllowRegenPotion { get; set; } = true;
        public bool AllowScentMaskPotion { get; set; } = true;
        public bool AllowSpeedPotion { get; set; } = true;
        public bool AllowVitalityPotion { get; set; } = true;
        public bool AllowReshapePotionRecipe { get; set; } = true;
        public bool AllowGrowPotionRecipe { get; set; } = true;
        public bool AllowShrinkPotionRecipe { get; set; } = true;
        public bool AllowToxicMushrooms { get; set; } = true;
        public bool AllowPsychedelicMushrooms { get; set; } = true;

        public bool RecallPotionStrongRecipe { get; set; } = true;
        public bool GlowPotionStrongRecipe { get; set; } = false;
        public bool WaterBreathePotionStrongRecipe { get; set; } = false;
        public bool ColdResistPotionStrongRecipe { get; set; } = false;
        public bool NutritionPotionStrongRecipe { get; set; } = false;
        public bool TemporalPotionStrongRecipe { get; set; } = true;
        public bool ReshapePotionStrongRecipe { get; set; } = true;
        public bool GrowPotionStrongRecipe { get; set; } = false;
        public bool ShrinkPotionStrongRecipe { get; set; } = false;
        public bool FallPotionStrongRecipe { get; set; } = false;
        public bool ClimbPotionStrongRecipe { get; set; } = false;

        public bool AllowHerbballs { get; set; } = true;
        public bool AllowMediumPotions { get; set; } = true;
        public bool AllowStrongPotions { get; set; } = true;

        // public bool AllowCuttings { get; set; } = true;

        public bool AllowClayFlasks { get; set; } = true;
        public bool AllowSmallFlasks { get; set; } = true;
        public bool AllowMediumFlasks { get; set; } = true;
        public bool AllowLargeFlasks { get; set; } = true;
        public bool AllowGlassThrowableFlasks { get; set; } = true;
        public bool AllowClayThrowableFlasks { get; set; } = true;

        public bool AllowHerbRackMolds { get; set; } = true;
        public bool AllowHerbRacks { get; set; } = true;

        public bool AllowPotionBrewingCauldron { get; set; } = true;
        public bool AllowCauldronInVanillaFirepit { get; set; } = false;

        // public bool AllowPotionBrewingCauldronMold { get; set; } = true;

        // public bool AllowDecorativeRacks { get; set; } = true;

        public bool AllowWeaponCoating { get; set; } = true;
        public bool AllowBarrelCoating { get; set; } = true;
        public bool AllowVanillaContainerDrinking { get; set; } = false;
        public bool OnlyOnePotionAtATime { get; set; } = false;
        public bool AllowPotionRefresh { get; set; } = false;
        public bool SideEffectStrengthMultiplier { get; set; } = true;

        public bool AllowPotionExclusivity { get; set; } = false;

        public string ArcherPotionGroup { get; set; } = "combat";
        public string MeleePotionGroup { get; set; } = "combat";
        public string VitalityPotionGroup { get; set; } = "combat";
        public string SpeedPotionGroup { get; set; } = "combat";

        public string HungerEnhancePotionGroup { get; set; } = "survival";
        public string HungerSupressPotionGroup { get; set; } = "survival";
        public string HealingEffectPotionGroup { get; set; } = "survival";
        public string RegenPotionGroup { get; set; } = "survival";

        public string HunterPotionGroup { get; set; } = "utility";
        public string PredatorPotionGroup { get; set; } = "utility";
        public string ScentMaskPotionGroup { get; set; } = "utility";
        public string LooterPotionGroup { get; set; } = "utility";
        public string MiningPotionGroup { get; set; } = "utility";

        public string NutritionPotionGroup { get; set; } = "special";
        public string GlowPotionGroup { get; set; } = "special";
        public string WaterBreathePotionGroup { get; set; } = "special";
        public string ColdResistPotionGroup { get; set; } = "special";
        public string FallPotionGroup { get; set; } = "special";
        public string ClimbPotionGroup { get; set; } = "special";
        public string FlightPotionGroup { get; set; } = "special";

        public string TemporalPotionGroup { get; set; } = "solo";
        public string RecallPotionGroup { get; set; } = "solo";
        public string ReshapePotionGroup { get; set; } = "solo";

        public string PoisonPotionGroup { get; set; } = "none";
        public string GrowPotionGroup { get; set; } = "none";
        public string ShrinkPotionGroup { get; set; } = "none";

        public bool AllowCoatingArcher { get; set; } = false;
        public bool AllowCoatingHealingEffect { get; set; } = false;
        public bool AllowCoatingHungerEnhance { get; set; } = false;
        public bool AllowCoatingHungerSupress { get; set; } = false;
        public bool AllowCoatingHunter { get; set; } = false;
        public bool AllowCoatingLooter { get; set; } = false;
        public bool AllowCoatingMelee { get; set; } = false;
        public bool AllowCoatingMining { get; set; } = false;
        public bool AllowCoatingPoison { get; set; } = true;
        public bool AllowCoatingPredator { get; set; } = false;
        public bool AllowCoatingRegen { get; set; } = true;
        public bool AllowCoatingScentMask { get; set; } = false;
        public bool AllowCoatingSpeed { get; set; } = false;
        public bool AllowCoatingVitality { get; set; } = false;
        public bool AllowCoatingRecall { get; set; } = false;
        public bool AllowCoatingGlow { get; set; } = false;
        public bool AllowCoatingWaterBreathe { get; set; } = false;
        public bool AllowCoatingColdResist { get; set; } = false;
        public bool AllowCoatingNutrition { get; set; } = false;
        public bool AllowCoatingTemporal { get; set; } = false;
        public bool AllowCoatingReshape { get; set; } = false;
        public bool AllowCoatingGrow { get; set; } = false;
        public bool AllowCoatingShrink { get; set; } = false;
        public bool AllowCoatingFall { get; set; } = false;
        public bool AllowCoatingClimb { get; set; } = false;
        public bool AllowCoatingFlight { get; set; } = false;

        public bool AllowFallPotion { get; set; } = true;
        public bool AllowFallPotionRecipe { get; set; } = true;
        public float FallPotionDamageReduction { get; set; } = 0.75f;
        public int FallPotionDuration { get; set; } = 600;

        public bool AllowClimbPotion { get; set; } = true;
        public bool AllowClimbPotionRecipe { get; set; } = true;
        public int ClimbPotionDuration { get; set; } = 300;

        public bool AllowFlightPotion { get; set; } = true;
        public int FlightPotionDuration { get; set; } = 300;

        public float WeakPotionMultiplier { get; set; } = 1.0f;
        public float MediumPotionMultiplier { get; set; } = 2.0f;
        public float StrongPotionMultiplier { get; set; } = 3.0f;

        public float ArcherPotionAcc { get; set; } = 0.05f;
        public float ArcherPotionDamage { get; set; } = 0.2f;
        public float ArcherPotionSpeed { get; set; } = 0.2f;
        public int ArcherPotionDuration { get; set; } = 600;
        public float HealingEffectPotionValue { get; set; } = 0.3f;
        public int HealingEffectPotionDuration { get; set; } = 600;
        public float HungerEnhancePotionValue { get; set; } = 0.3f;
        public int HungerEnhancePotionDuration { get; set; } = 600;
        public float HungerSupressPotionValue { get; set; } = -0.3f;
        public int HungerSupressPotionDuration { get; set; } = 600;
        public float HunterPotionAnimalDrop { get; set; } = 0.2f;
        public float HunterPotionAnimalSeek { get; set; } = -0.05f;
        public float HunterPotionForageDrop { get; set; } = 0.2f;
        public float HunterPotionWildDrop { get; set; } = 0.2f;
        public int HunterPotionDuration { get; set; } = 600;
        public float LooterPotionForageDrop { get; set; } = 0.2f;
        public float LooterPotionGearDrop { get; set; } = 0.2f;
        public float LooterPotionVesselContentDrop { get; set; } = 0.3f;
        public float LooterPotionWildDrop { get; set; } = 0.2f;
        public int LooterPotionDuration { get; set; } = 600;
        public float MeleePotionDamage { get; set; } = 0.3f;
        public int MeleePotionDuration { get; set; } = 600;
        public float MiningPotionSpeed { get; set; } = 0.3f;
        public float MiningPotionOreDrop { get; set; } = 0.15f;
        public int MiningPotionDuration { get; set; } = 600;
        public float PoisonPotionHealth { get; set; } = -0.5f;
        public int PoisonPotionTickSec { get; set; } = 3;
        public int PoisonPotionDuration { get; set; } = 30;
        public bool PoisonPotionIgnoreArmour { get; set; } = true;
        public float PredatorPotionAnimalSeek { get; set; } = 0.4f;
        public int PredatorPotionDuration { get; set; } = 600;
        public float RegenPotionHealth { get; set; } = 0.5f;
        public int RegenPotionTickSec { get; set; } = 3;
        public int RegenPotionDuration { get; set; } = 30;
        public bool RegenPotionIgnoreArmour { get; set; } = true;
        public float ScentMaskPotionAnimalSeek { get; set; } = -0.2f;
        public int ScentMaskPotionDuration { get; set; } = 600;
        public float SpeedPotionValue { get; set; } = 0.25f;
        public int SpeedPotionDuration { get; set; } = 300;
        public float VitalityPotionMaxHealth { get; set; } = 0.25f;
        public int VitalityPotionDuration { get; set; } = 300;
        public int GlowPotionDuration { get; set; } = 1000;
        public int WaterBreathePotionDuration { get; set; } = 1000;
        public int ColdResistPotionDuration { get; set; } = 1000;
        public float NutritionPotionRetainedNutrition { get; set; } = 0.9f;
        public float StabilityPotionTemporalStabilityGain { get; set; } = 0.2f;
        public int GlowPotionStrength { get; set; } = 15;

        public float GrowPotionSizeChange { get; set; } = 0.5f;
        public float ShrinkPotionSizeChange { get; set; } = -0.5f;
        public float GrowShrinkMinHeight { get; set; } = 0.5f;
        public float GrowShrinkMaxHeight { get; set; } = 3.0f;

        public int WeaponCoatCharges { get; set; } = 5;
        public float WeaponCoatEffectMultiplier { get; set; } = 0.9f;
        public float WeaponCoatApplyTime { get; set; } = 2f;
        public float PotionDrinkTime { get; set; } = 1.5f;
        public float PotionEatTime { get; set; } = 1.5f;
        public bool ScalePotionTimeWithHealing { get; set; } = false;
        public float PotionConsumeMaxTimeMultiplier { get; set; } = 3.0f;

        public float PotionConsumeLitres { get; set; } = 0.25f;
        public float PotionDrinkCheckLitres { get; set; } = 0.24f;
        public float WeaponCoatConsumeLitres { get; set; } = 0.25f;
        public float WeaponCoatCheckLitres { get; set; } = 0.24f;

        public string CoatableWeaponTags { get; set; } = "weapon-melee";
        public string CoatableProjectilesCodes { get; set; } = "*arrow*";

        public float ThrowableFlaskSplashRadius { get; set; } = 2.5f;
        public float ThrowableFlaskEffectMultiplier { get; set; } = 0.5f;

        public float DrinkingPotionIntoxicationAmount { get; set; } = 0f;
        public float DrinkingPotionPsychedelicAmount { get; set; } = 0f;
        public float DrinkingPotionSaturationLossAmount { get; set; } = 0f;
        public float DrinkingPotionDamageAmount { get; set; } = 0f;

        // Archer
        public float ArcherPotionWalkSpeed { get; set; } = 0f;
        public float ArcherPotionMeleeDamage { get; set; } = 0f;
        public float ArcherPotionHealingEffectiveness { get; set; } = 0f;
        public float ArcherPotionHungerRate { get; set; } = 0f;
        public float ArcherPotionAnimalLootDrop { get; set; } = 0f;
        public float ArcherPotionAnimalSeekingRange { get; set; } = 0f;
        public float ArcherPotionForageDrop { get; set; } = 0f;
        public float ArcherPotionWildCropDrop { get; set; } = 0f;
        public float ArcherPotionGearDrop { get; set; } = 0f;
        public float ArcherPotionVesselContentsDrop { get; set; } = 0f;
        public float ArcherPotionMiningSpeed { get; set; } = 0f;
        public float ArcherPotionOreDrop { get; set; } = 0f;
        public float ArcherPotionMaxHealth { get; set; } = 0f;
        public float ArcherPotionHealth { get; set; } = 0f;
        public int ArcherPotionHealthTickSec { get; set; } = 0;

        // HealingEffect
        public float HealingEffectPotionWalkSpeed { get; set; } = 0f;
        public float HealingEffectPotionMeleeDamage { get; set; } = 0f;
        public float HealingEffectPotionRangedAccuracy { get; set; } = 0f;
        public float HealingEffectPotionRangedDamage { get; set; } = 0f;
        public float HealingEffectPotionRangedSpeed { get; set; } = 0f;
        public float HealingEffectPotionHungerRate { get; set; } = 0f;
        public float HealingEffectPotionAnimalLootDrop { get; set; } = 0f;
        public float HealingEffectPotionAnimalSeekingRange { get; set; } = 0f;
        public float HealingEffectPotionForageDrop { get; set; } = 0f;
        public float HealingEffectPotionWildCropDrop { get; set; } = 0f;
        public float HealingEffectPotionGearDrop { get; set; } = 0f;
        public float HealingEffectPotionVesselContentsDrop { get; set; } = 0f;
        public float HealingEffectPotionMiningSpeed { get; set; } = 0f;
        public float HealingEffectPotionOreDrop { get; set; } = 0f;
        public float HealingEffectPotionMaxHealth { get; set; } = 0f;
        public float HealingEffectPotionHealth { get; set; } = 0f;
        public int HealingEffectPotionHealthTickSec { get; set; } = 0;

        // HungerEnhance
        public float HungerEnhancePotionWalkSpeed { get; set; } = 0f;
        public float HungerEnhancePotionMeleeDamage { get; set; } = 0f;
        public float HungerEnhancePotionRangedAccuracy { get; set; } = 0f;
        public float HungerEnhancePotionRangedDamage { get; set; } = 0f;
        public float HungerEnhancePotionRangedSpeed { get; set; } = 0f;
        public float HungerEnhancePotionHealingEffectiveness { get; set; } = 0f;
        public float HungerEnhancePotionAnimalLootDrop { get; set; } = 0f;
        public float HungerEnhancePotionAnimalSeekingRange { get; set; } = 0f;
        public float HungerEnhancePotionForageDrop { get; set; } = 0f;
        public float HungerEnhancePotionWildCropDrop { get; set; } = 0f;
        public float HungerEnhancePotionGearDrop { get; set; } = 0f;
        public float HungerEnhancePotionVesselContentsDrop { get; set; } = 0f;
        public float HungerEnhancePotionMiningSpeed { get; set; } = 0f;
        public float HungerEnhancePotionOreDrop { get; set; } = 0f;
        public float HungerEnhancePotionMaxHealth { get; set; } = 0f;
        public float HungerEnhancePotionHealth { get; set; } = 0f;
        public int HungerEnhancePotionHealthTickSec { get; set; } = 0;

        // HungerSupress
        public float HungerSupressPotionWalkSpeed { get; set; } = 0f;
        public float HungerSupressPotionMeleeDamage { get; set; } = 0f;
        public float HungerSupressPotionRangedAccuracy { get; set; } = 0f;
        public float HungerSupressPotionRangedDamage { get; set; } = 0f;
        public float HungerSupressPotionRangedSpeed { get; set; } = 0f;
        public float HungerSupressPotionHealingEffectiveness { get; set; } = 0f;
        public float HungerSupressPotionAnimalLootDrop { get; set; } = 0f;
        public float HungerSupressPotionAnimalSeekingRange { get; set; } = 0f;
        public float HungerSupressPotionForageDrop { get; set; } = 0f;
        public float HungerSupressPotionWildCropDrop { get; set; } = 0f;
        public float HungerSupressPotionGearDrop { get; set; } = 0f;
        public float HungerSupressPotionVesselContentsDrop { get; set; } = 0f;
        public float HungerSupressPotionMiningSpeed { get; set; } = 0f;
        public float HungerSupressPotionOreDrop { get; set; } = 0f;
        public float HungerSupressPotionMaxHealth { get; set; } = 0f;
        public float HungerSupressPotionHealth { get; set; } = 0f;
        public int HungerSupressPotionHealthTickSec { get; set; } = 0;

        // Hunter
        public float HunterPotionWalkSpeed { get; set; } = 0f;
        public float HunterPotionMeleeDamage { get; set; } = 0f;
        public float HunterPotionRangedAccuracy { get; set; } = 0f;
        public float HunterPotionRangedDamage { get; set; } = 0f;
        public float HunterPotionRangedSpeed { get; set; } = 0f;
        public float HunterPotionHealingEffectiveness { get; set; } = 0f;
        public float HunterPotionHungerRate { get; set; } = 0f;
        public float HunterPotionGearDrop { get; set; } = 0f;
        public float HunterPotionVesselContentsDrop { get; set; } = 0f;
        public float HunterPotionMiningSpeed { get; set; } = 0f;
        public float HunterPotionOreDrop { get; set; } = 0f;
        public float HunterPotionMaxHealth { get; set; } = 0f;
        public float HunterPotionHealth { get; set; } = 0f;
        public int HunterPotionHealthTickSec { get; set; } = 0;

        // Looter
        public float LooterPotionWalkSpeed { get; set; } = 0f;
        public float LooterPotionMeleeDamage { get; set; } = 0f;
        public float LooterPotionRangedAccuracy { get; set; } = 0f;
        public float LooterPotionRangedDamage { get; set; } = 0f;
        public float LooterPotionRangedSpeed { get; set; } = 0f;
        public float LooterPotionHealingEffectiveness { get; set; } = 0f;
        public float LooterPotionHungerRate { get; set; } = 0f;
        public float LooterPotionAnimalLootDrop { get; set; } = 0f;
        public float LooterPotionAnimalSeekingRange { get; set; } = 0f;
        public float LooterPotionMiningSpeed { get; set; } = 0f;
        public float LooterPotionOreDrop { get; set; } = 0f;
        public float LooterPotionMaxHealth { get; set; } = 0f;
        public float LooterPotionHealth { get; set; } = 0f;
        public int LooterPotionHealthTickSec { get; set; } = 0;

        // Melee
        public float MeleePotionWalkSpeed { get; set; } = 0f;
        public float MeleePotionRangedAccuracy { get; set; } = 0f;
        public float MeleePotionRangedDamage { get; set; } = 0f;
        public float MeleePotionRangedSpeed { get; set; } = 0f;
        public float MeleePotionHealingEffectiveness { get; set; } = 0f;
        public float MeleePotionHungerRate { get; set; } = 0f;
        public float MeleePotionAnimalLootDrop { get; set; } = 0f;
        public float MeleePotionAnimalSeekingRange { get; set; } = 0f;
        public float MeleePotionForageDrop { get; set; } = 0f;
        public float MeleePotionWildCropDrop { get; set; } = 0f;
        public float MeleePotionGearDrop { get; set; } = 0f;
        public float MeleePotionVesselContentsDrop { get; set; } = 0f;
        public float MeleePotionMiningSpeed { get; set; } = 0f;
        public float MeleePotionOreDrop { get; set; } = 0f;
        public float MeleePotionMaxHealth { get; set; } = 0f;
        public float MeleePotionHealth { get; set; } = 0f;
        public int MeleePotionHealthTickSec { get; set; } = 0;

        // Mining
        public float MiningPotionWalkSpeed { get; set; } = 0f;
        public float MiningPotionMeleeDamage { get; set; } = 0f;
        public float MiningPotionRangedAccuracy { get; set; } = 0f;
        public float MiningPotionRangedDamage { get; set; } = 0f;
        public float MiningPotionRangedSpeed { get; set; } = 0f;
        public float MiningPotionHealingEffectiveness { get; set; } = 0f;
        public float MiningPotionHungerRate { get; set; } = 0f;
        public float MiningPotionAnimalLootDrop { get; set; } = 0f;
        public float MiningPotionAnimalSeekingRange { get; set; } = 0f;
        public float MiningPotionForageDrop { get; set; } = 0f;
        public float MiningPotionWildCropDrop { get; set; } = 0f;
        public float MiningPotionGearDrop { get; set; } = 0f;
        public float MiningPotionVesselContentsDrop { get; set; } = 0f;
        public float MiningPotionMaxHealth { get; set; } = 0f;
        public float MiningPotionHealthExtra { get; set; } = 0f;
        public int MiningPotionHealthExtraTickSec { get; set; } = 0;

        // Poison (health damage is primary — extra stats only)
        public float PoisonPotionWalkSpeed { get; set; } = 0f;
        public float PoisonPotionMeleeDamage { get; set; } = 0f;
        public float PoisonPotionRangedAccuracy { get; set; } = 0f;
        public float PoisonPotionRangedDamage { get; set; } = 0f;
        public float PoisonPotionRangedSpeed { get; set; } = 0f;
        public float PoisonPotionHealingEffectiveness { get; set; } = 0f;
        public float PoisonPotionHungerRate { get; set; } = 0f;
        public float PoisonPotionAnimalLootDrop { get; set; } = 0f;
        public float PoisonPotionAnimalSeekingRange { get; set; } = 0f;
        public float PoisonPotionForageDrop { get; set; } = 0f;
        public float PoisonPotionWildCropDrop { get; set; } = 0f;
        public float PoisonPotionGearDrop { get; set; } = 0f;
        public float PoisonPotionVesselContentsDrop { get; set; } = 0f;
        public float PoisonPotionMiningSpeed { get; set; } = 0f;
        public float PoisonPotionOreDrop { get; set; } = 0f;
        public float PoisonPotionMaxHealth { get; set; } = 0f;

        // Predator
        public float PredatorPotionWalkSpeed { get; set; } = 0f;
        public float PredatorPotionMeleeDamage { get; set; } = 0f;
        public float PredatorPotionRangedAccuracy { get; set; } = 0f;
        public float PredatorPotionRangedDamage { get; set; } = 0f;
        public float PredatorPotionRangedSpeed { get; set; } = 0f;
        public float PredatorPotionHealingEffectiveness { get; set; } = 0f;
        public float PredatorPotionHungerRate { get; set; } = 0f;
        public float PredatorPotionAnimalLootDrop { get; set; } = 0f;
        public float PredatorPotionForageDrop { get; set; } = 0f;
        public float PredatorPotionWildCropDrop { get; set; } = 0f;
        public float PredatorPotionGearDrop { get; set; } = 0f;
        public float PredatorPotionVesselContentsDrop { get; set; } = 0f;
        public float PredatorPotionMiningSpeed { get; set; } = 0f;
        public float PredatorPotionOreDrop { get; set; } = 0f;
        public float PredatorPotionMaxHealth { get; set; } = 0f;
        public float PredatorPotionHealth { get; set; } = 0f;
        public int PredatorPotionHealthTickSec { get; set; } = 0;

        // Regen (health regen is primary — extra stats only)
        public float RegenPotionWalkSpeed { get; set; } = 0f;
        public float RegenPotionMeleeDamage { get; set; } = 0f;
        public float RegenPotionRangedAccuracy { get; set; } = 0f;
        public float RegenPotionRangedDamage { get; set; } = 0f;
        public float RegenPotionRangedSpeed { get; set; } = 0f;
        public float RegenPotionHealingEffectiveness { get; set; } = 0f;
        public float RegenPotionHungerRate { get; set; } = 0f;
        public float RegenPotionAnimalLootDrop { get; set; } = 0f;
        public float RegenPotionAnimalSeekingRange { get; set; } = 0f;
        public float RegenPotionForageDrop { get; set; } = 0f;
        public float RegenPotionWildCropDrop { get; set; } = 0f;
        public float RegenPotionGearDrop { get; set; } = 0f;
        public float RegenPotionVesselContentsDrop { get; set; } = 0f;
        public float RegenPotionMiningSpeed { get; set; } = 0f;
        public float RegenPotionOreDrop { get; set; } = 0f;
        public float RegenPotionMaxHealth { get; set; } = 0f;

        // ScentMask
        public float ScentMaskPotionWalkSpeed { get; set; } = 0f;
        public float ScentMaskPotionMeleeDamage { get; set; } = 0f;
        public float ScentMaskPotionRangedAccuracy { get; set; } = 0f;
        public float ScentMaskPotionRangedDamage { get; set; } = 0f;
        public float ScentMaskPotionRangedSpeed { get; set; } = 0f;
        public float ScentMaskPotionHealingEffectiveness { get; set; } = 0f;
        public float ScentMaskPotionHungerRate { get; set; } = 0f;
        public float ScentMaskPotionAnimalLootDrop { get; set; } = 0f;
        public float ScentMaskPotionForageDrop { get; set; } = 0f;
        public float ScentMaskPotionWildCropDrop { get; set; } = 0f;
        public float ScentMaskPotionGearDrop { get; set; } = 0f;
        public float ScentMaskPotionVesselContentsDrop { get; set; } = 0f;
        public float ScentMaskPotionMiningSpeed { get; set; } = 0f;
        public float ScentMaskPotionOreDrop { get; set; } = 0f;
        public float ScentMaskPotionMaxHealth { get; set; } = 0f;
        public float ScentMaskPotionHealth { get; set; } = 0f;
        public int ScentMaskPotionHealthTickSec { get; set; } = 0;

        // Speed
        public float SpeedPotionMeleeDamage { get; set; } = 0f;
        public float SpeedPotionRangedAccuracy { get; set; } = 0f;
        public float SpeedPotionRangedDamage { get; set; } = 0f;
        public float SpeedPotionRangedSpeed { get; set; } = 0f;
        public float SpeedPotionHealingEffectiveness { get; set; } = 0f;
        public float SpeedPotionHungerRate { get; set; } = 0f;
        public float SpeedPotionAnimalLootDrop { get; set; } = 0f;
        public float SpeedPotionAnimalSeekingRange { get; set; } = 0f;
        public float SpeedPotionForageDrop { get; set; } = 0f;
        public float SpeedPotionWildCropDrop { get; set; } = 0f;
        public float SpeedPotionGearDrop { get; set; } = 0f;
        public float SpeedPotionVesselContentsDrop { get; set; } = 0f;
        public float SpeedPotionMiningSpeed { get; set; } = 0f;
        public float SpeedPotionOreDrop { get; set; } = 0f;
        public float SpeedPotionMaxHealth { get; set; } = 0f;
        public float SpeedPotionHealth { get; set; } = 0f;
        public int SpeedPotionHealthTickSec { get; set; } = 0;

        // Vitality
        public float VitalityPotionWalkSpeed { get; set; } = 0f;
        public float VitalityPotionMeleeDamage { get; set; } = 0f;
        public float VitalityPotionRangedAccuracy { get; set; } = 0f;
        public float VitalityPotionRangedDamage { get; set; } = 0f;
        public float VitalityPotionRangedSpeed { get; set; } = 0f;
        public float VitalityPotionHealingEffectiveness { get; set; } = 0f;
        public float VitalityPotionHungerRate { get; set; } = 0f;
        public float VitalityPotionAnimalLootDrop { get; set; } = 0f;
        public float VitalityPotionAnimalSeekingRange { get; set; } = 0f;
        public float VitalityPotionForageDrop { get; set; } = 0f;
        public float VitalityPotionWildCropDrop { get; set; } = 0f;
        public float VitalityPotionGearDrop { get; set; } = 0f;
        public float VitalityPotionVesselContentsDrop { get; set; } = 0f;
        public float VitalityPotionMiningSpeed { get; set; } = 0f;
        public float VitalityPotionOreDrop { get; set; } = 0f;
        public float VitalityPotionHealth { get; set; } = 0f;
        public int VitalityPotionHealthTickSec { get; set; } = 0;

        // Per-potion drinking side effects (additive with global blanket, scaled by strength)
        // Archer
        public float ArcherPotionDrinkingDamage { get; set; } = 0f;
        public float ArcherPotionDrinkingIntoxication { get; set; } = 0f;
        public float ArcherPotionDrinkingPsychedelic { get; set; } = 0f;
        public float ArcherPotionDrinkingSaturationLoss { get; set; } = 0f;

        // HealingEffect
        public float HealingEffectPotionDrinkingDamage { get; set; } = 0f;
        public float HealingEffectPotionDrinkingIntoxication { get; set; } = 0f;
        public float HealingEffectPotionDrinkingPsychedelic { get; set; } = 0f;
        public float HealingEffectPotionDrinkingSaturationLoss { get; set; } = 0f;

        // HungerEnhance
        public float HungerEnhancePotionDrinkingDamage { get; set; } = 0f;
        public float HungerEnhancePotionDrinkingIntoxication { get; set; } = 0f;
        public float HungerEnhancePotionDrinkingPsychedelic { get; set; } = 0f;
        public float HungerEnhancePotionDrinkingSaturationLoss { get; set; } = 0f;

        // HungerSupress
        public float HungerSupressPotionDrinkingDamage { get; set; } = 0f;
        public float HungerSupressPotionDrinkingIntoxication { get; set; } = 0f;
        public float HungerSupressPotionDrinkingPsychedelic { get; set; } = 0f;
        public float HungerSupressPotionDrinkingSaturationLoss { get; set; } = 0f;

        // Hunter
        public float HunterPotionDrinkingDamage { get; set; } = 0f;
        public float HunterPotionDrinkingIntoxication { get; set; } = 0f;
        public float HunterPotionDrinkingPsychedelic { get; set; } = 0f;
        public float HunterPotionDrinkingSaturationLoss { get; set; } = 0f;

        // Looter
        public float LooterPotionDrinkingDamage { get; set; } = 0f;
        public float LooterPotionDrinkingIntoxication { get; set; } = 0f;
        public float LooterPotionDrinkingPsychedelic { get; set; } = 0f;
        public float LooterPotionDrinkingSaturationLoss { get; set; } = 0f;

        // Melee
        public float MeleePotionDrinkingDamage { get; set; } = 0f;
        public float MeleePotionDrinkingIntoxication { get; set; } = 0f;
        public float MeleePotionDrinkingPsychedelic { get; set; } = 0f;
        public float MeleePotionDrinkingSaturationLoss { get; set; } = 0f;

        // Mining
        public float MiningPotionDrinkingDamage { get; set; } = 0f;
        public float MiningPotionDrinkingIntoxication { get; set; } = 0f;
        public float MiningPotionDrinkingPsychedelic { get; set; } = 0f;
        public float MiningPotionDrinkingSaturationLoss { get; set; } = 0f;

        // Poison
        public float PoisonPotionDrinkingDamage { get; set; } = 0f;
        public float PoisonPotionDrinkingIntoxication { get; set; } = 0f;
        public float PoisonPotionDrinkingPsychedelic { get; set; } = 0f;
        public float PoisonPotionDrinkingSaturationLoss { get; set; } = 0f;

        // Predator
        public float PredatorPotionDrinkingDamage { get; set; } = 0f;
        public float PredatorPotionDrinkingIntoxication { get; set; } = 0f;
        public float PredatorPotionDrinkingPsychedelic { get; set; } = 0f;
        public float PredatorPotionDrinkingSaturationLoss { get; set; } = 0f;

        // Regen
        public float RegenPotionDrinkingDamage { get; set; } = 0f;
        public float RegenPotionDrinkingIntoxication { get; set; } = 0f;
        public float RegenPotionDrinkingPsychedelic { get; set; } = 0f;
        public float RegenPotionDrinkingSaturationLoss { get; set; } = 0f;

        // ScentMask
        public float ScentMaskPotionDrinkingDamage { get; set; } = 0f;
        public float ScentMaskPotionDrinkingIntoxication { get; set; } = 0f;
        public float ScentMaskPotionDrinkingPsychedelic { get; set; } = 0f;
        public float ScentMaskPotionDrinkingSaturationLoss { get; set; } = 0f;

        // Speed
        public float SpeedPotionDrinkingDamage { get; set; } = 0f;
        public float SpeedPotionDrinkingIntoxication { get; set; } = 0f;
        public float SpeedPotionDrinkingPsychedelic { get; set; } = 0f;
        public float SpeedPotionDrinkingSaturationLoss { get; set; } = 0f;

        // Vitality
        public float VitalityPotionDrinkingDamage { get; set; } = 0f;
        public float VitalityPotionDrinkingIntoxication { get; set; } = 0f;
        public float VitalityPotionDrinkingPsychedelic { get; set; } = 0f;
        public float VitalityPotionDrinkingSaturationLoss { get; set; } = 0f;

        // Glow
        public float GlowPotionDrinkingDamage { get; set; } = 0f;
        public float GlowPotionDrinkingIntoxication { get; set; } = 0f;
        public float GlowPotionDrinkingPsychedelic { get; set; } = 0f;
        public float GlowPotionDrinkingSaturationLoss { get; set; } = 0f;

        // WaterBreathe
        public float WaterBreathePotionDrinkingDamage { get; set; } = 0f;
        public float WaterBreathePotionDrinkingIntoxication { get; set; } = 0f;
        public float WaterBreathePotionDrinkingPsychedelic { get; set; } = 0f;
        public float WaterBreathePotionDrinkingSaturationLoss { get; set; } = 0f;

        // ColdResist
        public float ColdResistPotionDrinkingDamage { get; set; } = 0f;
        public float ColdResistPotionDrinkingIntoxication { get; set; } = 0f;
        public float ColdResistPotionDrinkingPsychedelic { get; set; } = 0f;
        public float ColdResistPotionDrinkingSaturationLoss { get; set; } = 0f;

        // Nutrition
        public float NutritionPotionDrinkingDamage { get; set; } = 0f;
        public float NutritionPotionDrinkingIntoxication { get; set; } = 0f;
        public float NutritionPotionDrinkingPsychedelic { get; set; } = 0f;
        public float NutritionPotionDrinkingSaturationLoss { get; set; } = 0f;

        // Recall
        public float RecallPotionDrinkingDamage { get; set; } = 0f;
        public float RecallPotionDrinkingIntoxication { get; set; } = 0f;
        public float RecallPotionDrinkingPsychedelic { get; set; } = 0f;
        public float RecallPotionDrinkingSaturationLoss { get; set; } = 0f;

        // Temporal
        public float TemporalPotionDrinkingDamage { get; set; } = 0f;
        public float TemporalPotionDrinkingIntoxication { get; set; } = 0f;
        public float TemporalPotionDrinkingPsychedelic { get; set; } = 0f;
        public float TemporalPotionDrinkingSaturationLoss { get; set; } = 0f;

        // Reshape
        public float ReshapePotionDrinkingDamage { get; set; } = 0f;
        public float ReshapePotionDrinkingIntoxication { get; set; } = 0f;
        public float ReshapePotionDrinkingPsychedelic { get; set; } = 0f;
        public float ReshapePotionDrinkingSaturationLoss { get; set; } = 0f;

        // Grow
        public float GrowPotionDrinkingDamage { get; set; } = 0f;
        public float GrowPotionDrinkingIntoxication { get; set; } = 0f;
        public float GrowPotionDrinkingPsychedelic { get; set; } = 0f;
        public float GrowPotionDrinkingSaturationLoss { get; set; } = 0f;

        // Shrink
        public float ShrinkPotionDrinkingDamage { get; set; } = 0f;
        public float ShrinkPotionDrinkingIntoxication { get; set; } = 0f;
        public float ShrinkPotionDrinkingPsychedelic { get; set; } = 0f;
        public float ShrinkPotionDrinkingSaturationLoss { get; set; } = 0f;

        // Fall
        public float FallPotionDrinkingDamage { get; set; } = 0f;
        public float FallPotionDrinkingIntoxication { get; set; } = 0f;
        public float FallPotionDrinkingPsychedelic { get; set; } = 0f;
        public float FallPotionDrinkingSaturationLoss { get; set; } = 0f;

        // Climb
        public float ClimbPotionDrinkingDamage { get; set; } = 0f;
        public float ClimbPotionDrinkingIntoxication { get; set; } = 0f;
        public float ClimbPotionDrinkingPsychedelic { get; set; } = 0f;
        public float ClimbPotionDrinkingSaturationLoss { get; set; } = 0f;

        // Flight
        public float FlightPotionDrinkingDamage { get; set; } = 0f;
        public float FlightPotionDrinkingIntoxication { get; set; } = 0f;
        public float FlightPotionDrinkingPsychedelic { get; set; } = 0f;
        public float FlightPotionDrinkingSaturationLoss { get; set; } = 0f;
    }
}
