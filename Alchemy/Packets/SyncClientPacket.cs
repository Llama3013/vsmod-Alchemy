using System;
using ProtoBuf;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    [Serializable]
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class SyncClientPacket
    {
        public bool AllowRecallPotion;
        public bool AllowGlowPotion;
        public bool AllowWaterBreathePotion;
        public bool AllowColdResistPotion;
        public bool AllowNutritionPotion;
        public bool AllowTemporalPotion;

        public bool AllowArcherPotion;
        public bool AllowHealingEffectPotion;
        public bool AllowHungerEnhancePotion;
        public bool AllowHungerSupressPotion;
        public bool AllowHunterPotion;
        public bool AllowLooterPotion;
        public bool AllowMeleePotion;
        public bool AllowMiningPotion;
        public bool AllowPoisonPotion;
        public bool AllowPredatorPotion;
        public bool AllowRegenPotion;
        public bool AllowScentMaskPotion;
        public bool AllowSpeedPotion;
        public bool AllowVitalityPotion;
        public bool AllowReshapePotion;
        public bool AllowGrowPotion;
        public bool AllowShrinkPotion;
        public bool AllowFallPotion;
        public bool AllowClimbPotion;
        public bool AllowFlightPotion;

        public bool AllowReshapePotionRecipe;
        public bool AllowGrowPotionRecipe;
        public bool AllowShrinkPotionRecipe;
        public bool AllowFallPotionRecipe;
        public bool AllowClimbPotionRecipe;
        public bool AllowToxicMushrooms;
        public bool AllowPsychedelicMushrooms;

        public bool AllowHerbballs;
        public bool AllowMediumPotions;
        public bool AllowStrongPotions;

        public bool RecallPotionStrongRecipe;
        public bool GlowPotionStrongRecipe;
        public bool WaterBreathePotionStrongRecipe;
        public bool ColdResistPotionStrongRecipe;
        public bool NutritionPotionStrongRecipe;
        public bool TemporalPotionStrongRecipe;
        public bool ReshapePotionStrongRecipe;
        public bool GrowPotionStrongRecipe;
        public bool ShrinkPotionStrongRecipe;
        public bool FallPotionStrongRecipe;
        public bool ClimbPotionStrongRecipe;

        // public bool AllowCuttings;

        public bool AllowClayFlasks;
        public bool AllowSmallFlasks;
        public bool AllowMediumFlasks;
        public bool AllowLargeFlasks;
        public bool AllowGlassThrowableFlasks;
        public bool AllowClayThrowableFlasks;

        public bool AllowHerbRackMolds;
        public bool AllowHerbRacks;

        // public bool AllowDecorativeRacks;

        public bool AllowWeaponCoating;
        public bool AllowCauldronInVanillaFirepit;
        public bool SideEffectStrengthMultiplier;

        public bool AllowCoatingArcher;
        public bool AllowCoatingHealingEffect;
        public bool AllowCoatingHungerEnhance;
        public bool AllowCoatingHungerSupress;
        public bool AllowCoatingHunter;
        public bool AllowCoatingLooter;
        public bool AllowCoatingMelee;
        public bool AllowCoatingMining;
        public bool AllowCoatingPoison;
        public bool AllowCoatingPredator;
        public bool AllowCoatingRegen;
        public bool AllowCoatingScentMask;
        public bool AllowCoatingSpeed;
        public bool AllowCoatingVitality;
        public bool AllowCoatingRecall;
        public bool AllowCoatingGlow;
        public bool AllowCoatingWaterBreathe;
        public bool AllowCoatingColdResist;
        public bool AllowCoatingNutrition;
        public bool AllowCoatingTemporal;
        public bool AllowCoatingReshape;
        public bool AllowCoatingGrow;
        public bool AllowCoatingShrink;
        public bool AllowCoatingFall;
        public bool AllowCoatingClimb;
        public bool AllowCoatingFlight;

        public float WeakPotionMultiplier;
        public float MediumPotionMultiplier;
        public float StrongPotionMultiplier;

        public int WeaponCoatCharges;
        public float WeaponCoatApplyTime;
        public float PotionDrinkTime;
        public float PotionEatTime;
        public bool ScalePotionTimeWithHealing;
        public float PotionConsumeMaxTimeMultiplier;
        public float FallPotionDamageReduction;
        public int FallPotionDuration;
        public int ClimbPotionDuration;
        public int FlightPotionDuration;
        public float GrowPotionSizeChange;
        public float ShrinkPotionSizeChange;

        public float ArcherPotionAcc;
        public float ArcherPotionDamage;
        public float ArcherPotionSpeed;
        public int ArcherPotionDuration;
        public float ArcherPotionWalkSpeed;
        public float ArcherPotionMeleeDamage;
        public float ArcherPotionHealingEffectiveness;
        public float ArcherPotionHungerRate;
        public float ArcherPotionAnimalLootDrop;
        public float ArcherPotionAnimalSeekingRange;
        public float ArcherPotionForageDrop;
        public float ArcherPotionWildCropDrop;
        public float ArcherPotionGearDrop;
        public float ArcherPotionVesselContentsDrop;
        public float ArcherPotionMiningSpeed;
        public float ArcherPotionOreDrop;
        public float ArcherPotionMaxHealth;
        public float ArcherPotionHealth;
        public int ArcherPotionHealthTickSec;

        public float HealingEffectPotionValue;
        public int HealingEffectPotionDuration;
        public float HealingEffectPotionWalkSpeed;
        public float HealingEffectPotionMeleeDamage;
        public float HealingEffectPotionRangedAccuracy;
        public float HealingEffectPotionRangedDamage;
        public float HealingEffectPotionRangedSpeed;
        public float HealingEffectPotionHungerRate;
        public float HealingEffectPotionAnimalLootDrop;
        public float HealingEffectPotionAnimalSeekingRange;
        public float HealingEffectPotionForageDrop;
        public float HealingEffectPotionWildCropDrop;
        public float HealingEffectPotionGearDrop;
        public float HealingEffectPotionVesselContentsDrop;
        public float HealingEffectPotionMiningSpeed;
        public float HealingEffectPotionOreDrop;
        public float HealingEffectPotionMaxHealth;
        public float HealingEffectPotionHealth;
        public int HealingEffectPotionHealthTickSec;

        public float HungerEnhancePotionValue;
        public int HungerEnhancePotionDuration;
        public float HungerEnhancePotionWalkSpeed;
        public float HungerEnhancePotionMeleeDamage;
        public float HungerEnhancePotionRangedAccuracy;
        public float HungerEnhancePotionRangedDamage;
        public float HungerEnhancePotionRangedSpeed;
        public float HungerEnhancePotionHealingEffectiveness;
        public float HungerEnhancePotionAnimalLootDrop;
        public float HungerEnhancePotionAnimalSeekingRange;
        public float HungerEnhancePotionForageDrop;
        public float HungerEnhancePotionWildCropDrop;
        public float HungerEnhancePotionGearDrop;
        public float HungerEnhancePotionVesselContentsDrop;
        public float HungerEnhancePotionMiningSpeed;
        public float HungerEnhancePotionOreDrop;
        public float HungerEnhancePotionMaxHealth;
        public float HungerEnhancePotionHealth;
        public int HungerEnhancePotionHealthTickSec;

        public float HungerSupressPotionValue;
        public int HungerSupressPotionDuration;
        public float HungerSupressPotionWalkSpeed;
        public float HungerSupressPotionMeleeDamage;
        public float HungerSupressPotionRangedAccuracy;
        public float HungerSupressPotionRangedDamage;
        public float HungerSupressPotionRangedSpeed;
        public float HungerSupressPotionHealingEffectiveness;
        public float HungerSupressPotionAnimalLootDrop;
        public float HungerSupressPotionAnimalSeekingRange;
        public float HungerSupressPotionForageDrop;
        public float HungerSupressPotionWildCropDrop;
        public float HungerSupressPotionGearDrop;
        public float HungerSupressPotionVesselContentsDrop;
        public float HungerSupressPotionMiningSpeed;
        public float HungerSupressPotionOreDrop;
        public float HungerSupressPotionMaxHealth;
        public float HungerSupressPotionHealth;
        public int HungerSupressPotionHealthTickSec;

        public float HunterPotionAnimalDrop;
        public float HunterPotionAnimalSeek;
        public float HunterPotionForageDrop;
        public float HunterPotionWildDrop;
        public int HunterPotionDuration;
        public float HunterPotionWalkSpeed;
        public float HunterPotionMeleeDamage;
        public float HunterPotionRangedAccuracy;
        public float HunterPotionRangedDamage;
        public float HunterPotionRangedSpeed;
        public float HunterPotionHealingEffectiveness;
        public float HunterPotionHungerRate;
        public float HunterPotionGearDrop;
        public float HunterPotionVesselContentsDrop;
        public float HunterPotionMiningSpeed;
        public float HunterPotionOreDrop;
        public float HunterPotionMaxHealth;
        public float HunterPotionHealth;
        public int HunterPotionHealthTickSec;

        public float LooterPotionForageDrop;
        public float LooterPotionGearDrop;
        public float LooterPotionVesselContentDrop;
        public float LooterPotionWildDrop;
        public int LooterPotionDuration;
        public float LooterPotionWalkSpeed;
        public float LooterPotionMeleeDamage;
        public float LooterPotionRangedAccuracy;
        public float LooterPotionRangedDamage;
        public float LooterPotionRangedSpeed;
        public float LooterPotionHealingEffectiveness;
        public float LooterPotionHungerRate;
        public float LooterPotionAnimalLootDrop;
        public float LooterPotionAnimalSeekingRange;
        public float LooterPotionMiningSpeed;
        public float LooterPotionOreDrop;
        public float LooterPotionMaxHealth;
        public float LooterPotionHealth;
        public int LooterPotionHealthTickSec;

        public float MeleePotionDamage;
        public int MeleePotionDuration;
        public float MeleePotionWalkSpeed;
        public float MeleePotionRangedAccuracy;
        public float MeleePotionRangedDamage;
        public float MeleePotionRangedSpeed;
        public float MeleePotionHealingEffectiveness;
        public float MeleePotionHungerRate;
        public float MeleePotionAnimalLootDrop;
        public float MeleePotionAnimalSeekingRange;
        public float MeleePotionForageDrop;
        public float MeleePotionWildCropDrop;
        public float MeleePotionGearDrop;
        public float MeleePotionVesselContentsDrop;
        public float MeleePotionMiningSpeed;
        public float MeleePotionOreDrop;
        public float MeleePotionMaxHealth;
        public float MeleePotionHealth;
        public int MeleePotionHealthTickSec;

        public float MiningPotionSpeed;
        public float MiningPotionOreDrop;
        public int MiningPotionDuration;
        public float MiningPotionWalkSpeed;
        public float MiningPotionMeleeDamage;
        public float MiningPotionRangedAccuracy;
        public float MiningPotionRangedDamage;
        public float MiningPotionRangedSpeed;
        public float MiningPotionHealingEffectiveness;
        public float MiningPotionHungerRate;
        public float MiningPotionAnimalLootDrop;
        public float MiningPotionAnimalSeekingRange;
        public float MiningPotionForageDrop;
        public float MiningPotionWildCropDrop;
        public float MiningPotionGearDrop;
        public float MiningPotionVesselContentsDrop;
        public float MiningPotionMaxHealth;
        public float MiningPotionHealthExtra;
        public int MiningPotionHealthExtraTickSec;

        public float PoisonPotionHealth;
        public int PoisonPotionTickSec;
        public int PoisonPotionDuration;
        public bool PoisonPotionIgnoreArmour;
        public float PoisonPotionWalkSpeed;
        public float PoisonPotionMeleeDamage;
        public float PoisonPotionRangedAccuracy;
        public float PoisonPotionRangedDamage;
        public float PoisonPotionRangedSpeed;
        public float PoisonPotionHealingEffectiveness;
        public float PoisonPotionHungerRate;
        public float PoisonPotionAnimalLootDrop;
        public float PoisonPotionAnimalSeekingRange;
        public float PoisonPotionForageDrop;
        public float PoisonPotionWildCropDrop;
        public float PoisonPotionGearDrop;
        public float PoisonPotionVesselContentsDrop;
        public float PoisonPotionMiningSpeed;
        public float PoisonPotionOreDrop;
        public float PoisonPotionMaxHealth;

        public float PredatorPotionAnimalSeek;
        public int PredatorPotionDuration;
        public float PredatorPotionWalkSpeed;
        public float PredatorPotionMeleeDamage;
        public float PredatorPotionRangedAccuracy;
        public float PredatorPotionRangedDamage;
        public float PredatorPotionRangedSpeed;
        public float PredatorPotionHealingEffectiveness;
        public float PredatorPotionHungerRate;
        public float PredatorPotionAnimalLootDrop;
        public float PredatorPotionForageDrop;
        public float PredatorPotionWildCropDrop;
        public float PredatorPotionGearDrop;
        public float PredatorPotionVesselContentsDrop;
        public float PredatorPotionMiningSpeed;
        public float PredatorPotionOreDrop;
        public float PredatorPotionMaxHealth;
        public float PredatorPotionHealth;
        public int PredatorPotionHealthTickSec;

        public float RegenPotionHealth;
        public int RegenPotionTickSec;
        public int RegenPotionDuration;
        public bool RegenPotionIgnoreArmour;
        public float RegenPotionWalkSpeed;
        public float RegenPotionMeleeDamage;
        public float RegenPotionRangedAccuracy;
        public float RegenPotionRangedDamage;
        public float RegenPotionRangedSpeed;
        public float RegenPotionHealingEffectiveness;
        public float RegenPotionHungerRate;
        public float RegenPotionAnimalLootDrop;
        public float RegenPotionAnimalSeekingRange;
        public float RegenPotionForageDrop;
        public float RegenPotionWildCropDrop;
        public float RegenPotionGearDrop;
        public float RegenPotionVesselContentsDrop;
        public float RegenPotionMiningSpeed;
        public float RegenPotionOreDrop;
        public float RegenPotionMaxHealth;

        public float ScentMaskPotionAnimalSeek;
        public int ScentMaskPotionDuration;
        public float ScentMaskPotionWalkSpeed;
        public float ScentMaskPotionMeleeDamage;
        public float ScentMaskPotionRangedAccuracy;
        public float ScentMaskPotionRangedDamage;
        public float ScentMaskPotionRangedSpeed;
        public float ScentMaskPotionHealingEffectiveness;
        public float ScentMaskPotionHungerRate;
        public float ScentMaskPotionAnimalLootDrop;
        public float ScentMaskPotionForageDrop;
        public float ScentMaskPotionWildCropDrop;
        public float ScentMaskPotionGearDrop;
        public float ScentMaskPotionVesselContentsDrop;
        public float ScentMaskPotionMiningSpeed;
        public float ScentMaskPotionOreDrop;
        public float ScentMaskPotionMaxHealth;
        public float ScentMaskPotionHealth;
        public int ScentMaskPotionHealthTickSec;

        public float SpeedPotionValue;
        public int SpeedPotionDuration;
        public float SpeedPotionMeleeDamage;
        public float SpeedPotionRangedAccuracy;
        public float SpeedPotionRangedDamage;
        public float SpeedPotionRangedSpeed;
        public float SpeedPotionHealingEffectiveness;
        public float SpeedPotionHungerRate;
        public float SpeedPotionAnimalLootDrop;
        public float SpeedPotionAnimalSeekingRange;
        public float SpeedPotionForageDrop;
        public float SpeedPotionWildCropDrop;
        public float SpeedPotionGearDrop;
        public float SpeedPotionVesselContentsDrop;
        public float SpeedPotionMiningSpeed;
        public float SpeedPotionOreDrop;
        public float SpeedPotionMaxHealth;
        public float SpeedPotionHealth;
        public int SpeedPotionHealthTickSec;

        public float VitalityPotionMaxHealth;
        public int VitalityPotionDuration;
        public float VitalityPotionWalkSpeed;
        public float VitalityPotionMeleeDamage;
        public float VitalityPotionRangedAccuracy;
        public float VitalityPotionRangedDamage;
        public float VitalityPotionRangedSpeed;
        public float VitalityPotionHealingEffectiveness;
        public float VitalityPotionHungerRate;
        public float VitalityPotionAnimalLootDrop;
        public float VitalityPotionAnimalSeekingRange;
        public float VitalityPotionForageDrop;
        public float VitalityPotionWildCropDrop;
        public float VitalityPotionGearDrop;
        public float VitalityPotionVesselContentsDrop;
        public float VitalityPotionMiningSpeed;
        public float VitalityPotionOreDrop;
        public float VitalityPotionHealth;
        public int VitalityPotionHealthTickSec;

        public int GlowPotionDuration;
        public int GlowPotionStrength;
        public int WaterBreathePotionDuration;
        public int ColdResistPotionDuration;
        public float NutritionPotionRetainedNutrition;
        public float StabilityPotionTemporalStabilityGain;

        public float DrinkingPotionIntoxicationAmount;
        public float DrinkingPotionPsychedelicAmount;
        public float DrinkingPotionSaturationLossAmount;
        public float DrinkingPotionDamageAmount;

        public float ArcherPotionDrinkingDamage;
        public float ArcherPotionDrinkingIntoxication;
        public float ArcherPotionDrinkingPsychedelic;
        public float ArcherPotionDrinkingSaturationLoss;

        public float HealingEffectPotionDrinkingDamage;
        public float HealingEffectPotionDrinkingIntoxication;
        public float HealingEffectPotionDrinkingPsychedelic;
        public float HealingEffectPotionDrinkingSaturationLoss;

        public float HungerEnhancePotionDrinkingDamage;
        public float HungerEnhancePotionDrinkingIntoxication;
        public float HungerEnhancePotionDrinkingPsychedelic;
        public float HungerEnhancePotionDrinkingSaturationLoss;

        public float HungerSupressPotionDrinkingDamage;
        public float HungerSupressPotionDrinkingIntoxication;
        public float HungerSupressPotionDrinkingPsychedelic;
        public float HungerSupressPotionDrinkingSaturationLoss;

        public float HunterPotionDrinkingDamage;
        public float HunterPotionDrinkingIntoxication;
        public float HunterPotionDrinkingPsychedelic;
        public float HunterPotionDrinkingSaturationLoss;

        public float LooterPotionDrinkingDamage;
        public float LooterPotionDrinkingIntoxication;
        public float LooterPotionDrinkingPsychedelic;
        public float LooterPotionDrinkingSaturationLoss;

        public float MeleePotionDrinkingDamage;
        public float MeleePotionDrinkingIntoxication;
        public float MeleePotionDrinkingPsychedelic;
        public float MeleePotionDrinkingSaturationLoss;

        public float MiningPotionDrinkingDamage;
        public float MiningPotionDrinkingIntoxication;
        public float MiningPotionDrinkingPsychedelic;
        public float MiningPotionDrinkingSaturationLoss;

        public float PoisonPotionDrinkingDamage;
        public float PoisonPotionDrinkingIntoxication;
        public float PoisonPotionDrinkingPsychedelic;
        public float PoisonPotionDrinkingSaturationLoss;

        public float PredatorPotionDrinkingDamage;
        public float PredatorPotionDrinkingIntoxication;
        public float PredatorPotionDrinkingPsychedelic;
        public float PredatorPotionDrinkingSaturationLoss;

        public float RegenPotionDrinkingDamage;
        public float RegenPotionDrinkingIntoxication;
        public float RegenPotionDrinkingPsychedelic;
        public float RegenPotionDrinkingSaturationLoss;

        public float ScentMaskPotionDrinkingDamage;
        public float ScentMaskPotionDrinkingIntoxication;
        public float ScentMaskPotionDrinkingPsychedelic;
        public float ScentMaskPotionDrinkingSaturationLoss;

        public float SpeedPotionDrinkingDamage;
        public float SpeedPotionDrinkingIntoxication;
        public float SpeedPotionDrinkingPsychedelic;
        public float SpeedPotionDrinkingSaturationLoss;

        public float VitalityPotionDrinkingDamage;
        public float VitalityPotionDrinkingIntoxication;
        public float VitalityPotionDrinkingPsychedelic;
        public float VitalityPotionDrinkingSaturationLoss;

        public float GlowPotionDrinkingDamage;
        public float GlowPotionDrinkingIntoxication;
        public float GlowPotionDrinkingPsychedelic;
        public float GlowPotionDrinkingSaturationLoss;

        public float WaterBreathePotionDrinkingDamage;
        public float WaterBreathePotionDrinkingIntoxication;
        public float WaterBreathePotionDrinkingPsychedelic;
        public float WaterBreathePotionDrinkingSaturationLoss;

        public float ColdResistPotionDrinkingDamage;
        public float ColdResistPotionDrinkingIntoxication;
        public float ColdResistPotionDrinkingPsychedelic;
        public float ColdResistPotionDrinkingSaturationLoss;

        public float NutritionPotionDrinkingDamage;
        public float NutritionPotionDrinkingIntoxication;
        public float NutritionPotionDrinkingPsychedelic;
        public float NutritionPotionDrinkingSaturationLoss;

        public float RecallPotionDrinkingDamage;
        public float RecallPotionDrinkingIntoxication;
        public float RecallPotionDrinkingPsychedelic;
        public float RecallPotionDrinkingSaturationLoss;

        public float TemporalPotionDrinkingDamage;
        public float TemporalPotionDrinkingIntoxication;
        public float TemporalPotionDrinkingPsychedelic;
        public float TemporalPotionDrinkingSaturationLoss;

        public float ReshapePotionDrinkingDamage;
        public float ReshapePotionDrinkingIntoxication;
        public float ReshapePotionDrinkingPsychedelic;
        public float ReshapePotionDrinkingSaturationLoss;

        public float GrowPotionDrinkingDamage;
        public float GrowPotionDrinkingIntoxication;
        public float GrowPotionDrinkingPsychedelic;
        public float GrowPotionDrinkingSaturationLoss;

        public float ShrinkPotionDrinkingDamage;
        public float ShrinkPotionDrinkingIntoxication;
        public float ShrinkPotionDrinkingPsychedelic;
        public float ShrinkPotionDrinkingSaturationLoss;

        public float FallPotionDrinkingDamage;
        public float FallPotionDrinkingIntoxication;
        public float FallPotionDrinkingPsychedelic;
        public float FallPotionDrinkingSaturationLoss;

        public float ClimbPotionDrinkingDamage;
        public float ClimbPotionDrinkingIntoxication;
        public float ClimbPotionDrinkingPsychedelic;
        public float ClimbPotionDrinkingSaturationLoss;

        public float FlightPotionDrinkingDamage;
        public float FlightPotionDrinkingIntoxication;
        public float FlightPotionDrinkingPsychedelic;
        public float FlightPotionDrinkingSaturationLoss;
    }
}
