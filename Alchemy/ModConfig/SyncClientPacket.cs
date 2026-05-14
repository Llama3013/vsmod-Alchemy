using System;
using ProtoBuf;

namespace Alchemy.ModConfig
{
    [Serializable]
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class SyncClientPacket
    {
        public bool AllowRecallPotion;
        public bool AllowGlowPotion;
        public bool AllowWaterBreathePotion;
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
        public bool AllowReshapePotionRecipe;
        public bool AllowGrowPotionRecipe;
        public bool AllowShrinkPotionRecipe;
        public bool AllowToxicMushrooms;
        public bool AllowPsychedelicMushrooms;

        public bool AllowHerbballs;
        public bool AllowMediumPotions;
        public bool AllowStrongPotions;
        // public bool AllowCuttings;

        public bool AllowClayFlasks;
        public bool AllowSmallFlasks;
        public bool AllowMediumFlasks;
        public bool AllowLargeFlasks;

        public bool AllowHerbRackMolds;
        public bool AllowHerbRacks;
        // public bool AllowDecorativeRacks;

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
        public bool AllowCoatingNutrition;
        public bool AllowCoatingTemporal;
        public bool AllowCoatingReshape;
        public bool AllowCoatingGrow;
        public bool AllowCoatingShrink;

        public float WeakPotionMultiplier;
        public float MediumPotionMultiplier;
        public float StrongPotionMultiplier;

        public float ArcherPotionAcc;
        public float ArcherPotionDamage;
        public float ArcherPotionSpeed;
        public int ArcherPotionDuration;
        public float HealingEffectPotionValue;
        public int HealingEffectPotionDuration;
        public float HungerEnhancePotionValue;
        public int HungerEnhancePotionDuration;
        public float HungerSupressPotionValue;
        public int HungerSupressPotionDuration;
        public float HunterPotionAnimalDrop;
        public float HunterPotionAnimalSeek;
        public float HunterPotionForageDrop;
        public float HunterPotionWildDrop;
        public int HunterPotionDuration;
        public float LooterPotionForageDrop;
        public float LooterPotionGearDrop;
        public float LooterPotionVesselContentDrop;
        public float LooterPotionWildDrop;
        public int LooterPotionDuration;
        public float MeleePotionDamage;
        public int MeleePotionDuration;
        public float MiningPotionSpeed;
        public float MiningPotionOreDrop;
        public int MiningPotionDuration;
        public float PoisonPotionHealth;
        public int PoisonPotionTickSec;
        public int PoisonPotionDuration;
        public bool PoisonPotionIgnoreArmour;
        public float PredatorPotionAnimalSeek;
        public int PredatorPotionDuration;
        public float RegenPotionHealth;
        public int RegenPotionTickSec;
        public int RegenPotionDuration;
        public bool RegenPotionIgnoreArmour;
        public float ScentMaskPotionAnimalSeek;
        public int ScentMaskPotionDuration;
        public float SpeedPotionValue;
        public int SpeedPotionDuration;
        public float VitalityPotionMaxHealth;
        public int VitalityPotionDuration;
        public int GlowPotionDuration;
        public int WaterBreathePotionDuration;
        public float NutritionPotionRetainedNutrition;
        public float StabilityPotionTemporalStabilityGain;
        public int GlowPotionStrength;
    }
}
