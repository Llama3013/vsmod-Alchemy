namespace Alchemy
{
    public class AlchemyConfig
    {
        public string Comment { private get; set; } = "Set any potions you want to disable to true. This will remove them from the multiplayer/singleplayer server. Make sure to remove any potions/potion bases that are in your world before disabling otherwise the world will provide some errors that can probably be ignored. Changing this field won't do anything.";
        public bool DisableRecallPotion { get; set; } = false;
        public bool DisableGlowPotion { get; set; } = false;
        public bool DisableWaterBreathePotion { get; set; } = false;
        public bool DisableNutritionPotion { get; set; } = false;
        public bool DisableTemporalPotion { get; set; } = false;

        public bool DisableArcherPotion { get; set; } = false;
        public bool DisableHealingEffectPotion { get; set; } = false;
        public bool DisableHungerEnhancePotion { get; set; } = false;
        public bool DisableHungerSupressPotion { get; set; } = false;
        public bool DisableHunterPotion { get; set; } = false;
        public bool DisableLooterPotion { get; set; } = false;
        public bool DisableMeleePotion { get; set; } = false;
        public bool DisableMiningPotion { get; set; } = false;
        public bool DisablePoisonPotion { get; set; } = false;
        public bool DisablePredatorPotion { get; set; } = false;
        public bool DisableRegenPotion { get; set; } = false;
        public bool DisableScentMaskPotion { get; set; } = false;
        public bool DisableSpeedPotion { get; set; } = false;
        public bool DisableVitalityPotion { get; set; } = false;
        public bool DisableDebugPotions { get; set; } = true;

        //public bool DisableClayFlask { get; set; } = false;
        //public bool DisableLargeFlask { get; set; } = false;
        //public bool DisableMediumFlask { get; set; } = false;
        //public bool DisableSmallFlask { get; set; } = false;

        //public bool DisableHerbRack { get; set; } = false;
    }
}