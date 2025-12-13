namespace Alchemy.ModConfig
{
    public class AlchemyConfig
    {
        public static AlchemyConfig Loaded { get; set; } = new AlchemyConfig();
        public string Comment { get; } =
            "Set any potions you want to Allow to true. This will remove them from the multiplayer/singleplayer server. Make sure to remove any potions/potion bases that are in your world before disabling otherwise the world will provide some errors that can probably be ignored. Changing this field won't do anything.";
        public bool AllowRecallPotion { get; set; } = true;
        public bool AllowGlowPotion { get; set; } = true;
        public bool AllowWaterBreathePotion { get; set; } = true;
        public bool AllowNutritionPotion { get; set; } = true;
        public bool AllowTemporalPotion { get; set; } = true;

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
        public bool AllowReshapePotion { get; set; } = true;

        public bool AllowHerbballs { get; set; } = true;
        public bool AllowMediumPotions { get; set; } = true;
        public bool AllowStrongPotions { get; set; } = true;

        // public bool AllowCuttings { get; set; } = true;

        public bool AllowClayFlasks { get; set; } = true;
        public bool AllowSmallFlasks { get; set; } = true;
        public bool AllowMediumFlasks { get; set; } = true;
        public bool AllowLargeFlasks { get; set; } = true;

        public bool AllowHerbRackMolds { get; set; } = true;
        public bool AllowHerbRacks { get; set; } = true;

        // public bool AllowDecorativeRacks { get; set; } = true;

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
    }
}
