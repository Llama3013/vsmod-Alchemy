namespace Alchemy
{
    public static class PotionEffects
    {
        public static void RegisterAll()
        {
            EffectRegistry.Register("archerpotionid", ApplyArcherPotion);
            EffectRegistry.Register("healingeffectpotionid", ApplyHealingEffectPotion);
            EffectRegistry.Register("hungerenhancepotionid", ApplyHungerEnhancePotion);
            EffectRegistry.Register("hungersupresspotionid", ApplyHungerSupressPotion);
            EffectRegistry.Register("hunterpotionid", ApplyHunterPotion);
            EffectRegistry.Register("looterpotionid", ApplyLooterPotion);
            EffectRegistry.Register("meleepotionid", ApplyMeleePotion);
            EffectRegistry.Register("miningpotionid", ApplyMiningPotion);
            EffectRegistry.Register("poisontickpotionid", ApplyPoisonPotion);
            EffectRegistry.Register("predatorpotionid", ApplyPredatorPotion);
            EffectRegistry.Register("regentickpotionid", ApplyRegenPotion);
            EffectRegistry.Register("scentmaskpotionid", ApplyScentMaskPotion);
            EffectRegistry.Register("speedpotionid", ApplySpeedPotion);
            EffectRegistry.Register("vitalitypotionid", ApplyVitalityPotion);
            EffectRegistry.Register("glowpotionid", ApplyGlowPotion);
            EffectRegistry.Register("waterbreathepotionid", ApplyWaterBreathePotion);
            EffectRegistry.Register("coldresistpotionid", ApplyColdResistPotion);
            EffectRegistry.Register("nutritionpotionid", ApplyNutritionPotion);
            EffectRegistry.Register("recallpotionid", ApplyRecallPotion);
            EffectRegistry.Register("temporalpotionid", ApplyTemporalPotion);
            EffectRegistry.Register("reshapepotionid", ApplyReshapePotion);
            EffectRegistry.Register("growpotionid", ApplyGrowPotion);
            EffectRegistry.Register("shrinkpotionid", ApplyShrinkPotion);
            EffectRegistry.Register("fallpotionid", ApplyFallPotion);
            EffectRegistry.Register("climbpotionid", ApplyClimbPotion);
            EffectRegistry.Register("flightpotionid", ApplyFlightPotion);
        }

        private static void ApplyArcherPotion(EffectContext ctx)
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;
            ctx.ResetsEffects = cfg.ArcherPotionResetsEffects;
            ctx.AddStat("rangedWeaponsAcc", cfg.ArcherPotionAcc);
            ctx.AddStat("rangedWeaponsDamage", cfg.ArcherPotionDamage);
            ctx.AddStat("rangedWeaponsSpeed", cfg.ArcherPotionSpeed);
            ctx.Duration = cfg.ArcherPotionDuration;
            ctx.AddStat("walkspeed", cfg.ArcherPotionWalkSpeed);
            ctx.AddStat("meleeWeaponsDamage", cfg.ArcherPotionMeleeDamage);
            ctx.AddStat("healingeffectivness", cfg.ArcherPotionHealingEffectiveness);
            ctx.AddStat("hungerrate", cfg.ArcherPotionHungerRate);
            ctx.AddStat("animalLootDropRate", cfg.ArcherPotionAnimalLootDrop);
            ctx.AddStat("animalSeekingRange", cfg.ArcherPotionAnimalSeekingRange);
            ctx.AddStat("forageDropRate", cfg.ArcherPotionForageDrop);
            ctx.AddStat("wildCropDropRate", cfg.ArcherPotionWildCropDrop);
            ctx.AddStat("rustyGearDropRate", cfg.ArcherPotionGearDrop);
            ctx.AddStat("vesselContentsDropRate", cfg.ArcherPotionVesselContentsDrop);
            ctx.AddStat("miningSpeedMul", cfg.ArcherPotionMiningSpeed);
            ctx.AddStat("oreDropRate", cfg.ArcherPotionOreDrop);
            ctx.AddStat("maxhealthExtraPoints", cfg.ArcherPotionMaxHealth);
            if (cfg.ArcherPotionHealth != 0f)
            {
                ctx.Health += cfg.ArcherPotionHealth;
                if (ctx.TickSec == 0 && cfg.ArcherPotionHealthTickSec > 0)
                    ctx.TickSec = cfg.ArcherPotionHealthTickSec;
            }
        }

        private static void ApplyHealingEffectPotion(EffectContext ctx)
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;
            ctx.ResetsEffects = cfg.HealingEffectPotionResetsEffects;
            ctx.AddStat("healingeffectivness", cfg.HealingEffectPotionValue);
            ctx.Duration = cfg.HealingEffectPotionDuration;
            ctx.AddStat("walkspeed", cfg.HealingEffectPotionWalkSpeed);
            ctx.AddStat("meleeWeaponsDamage", cfg.HealingEffectPotionMeleeDamage);
            ctx.AddStat("rangedWeaponsAcc", cfg.HealingEffectPotionRangedAccuracy);
            ctx.AddStat("rangedWeaponsDamage", cfg.HealingEffectPotionRangedDamage);
            ctx.AddStat("rangedWeaponsSpeed", cfg.HealingEffectPotionRangedSpeed);
            ctx.AddStat("hungerrate", cfg.HealingEffectPotionHungerRate);
            ctx.AddStat("animalLootDropRate", cfg.HealingEffectPotionAnimalLootDrop);
            ctx.AddStat("animalSeekingRange", cfg.HealingEffectPotionAnimalSeekingRange);
            ctx.AddStat("forageDropRate", cfg.HealingEffectPotionForageDrop);
            ctx.AddStat("wildCropDropRate", cfg.HealingEffectPotionWildCropDrop);
            ctx.AddStat("rustyGearDropRate", cfg.HealingEffectPotionGearDrop);
            ctx.AddStat("vesselContentsDropRate", cfg.HealingEffectPotionVesselContentsDrop);
            ctx.AddStat("miningSpeedMul", cfg.HealingEffectPotionMiningSpeed);
            ctx.AddStat("oreDropRate", cfg.HealingEffectPotionOreDrop);
            ctx.AddStat("maxhealthExtraPoints", cfg.HealingEffectPotionMaxHealth);
            if (cfg.HealingEffectPotionHealth != 0f)
            {
                ctx.Health += cfg.HealingEffectPotionHealth;
                if (ctx.TickSec == 0 && cfg.HealingEffectPotionHealthTickSec > 0)
                    ctx.TickSec = cfg.HealingEffectPotionHealthTickSec;
            }
        }

        private static void ApplyHungerEnhancePotion(EffectContext ctx)
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;
            ctx.ResetsEffects = cfg.HungerEnhancePotionResetsEffects;
            ctx.AddStat("hungerrate", cfg.HungerEnhancePotionValue);
            ctx.Duration = cfg.HungerEnhancePotionDuration;
            ctx.AddStat("walkspeed", cfg.HungerEnhancePotionWalkSpeed);
            ctx.AddStat("meleeWeaponsDamage", cfg.HungerEnhancePotionMeleeDamage);
            ctx.AddStat("rangedWeaponsAcc", cfg.HungerEnhancePotionRangedAccuracy);
            ctx.AddStat("rangedWeaponsDamage", cfg.HungerEnhancePotionRangedDamage);
            ctx.AddStat("rangedWeaponsSpeed", cfg.HungerEnhancePotionRangedSpeed);
            ctx.AddStat("healingeffectivness", cfg.HungerEnhancePotionHealingEffectiveness);
            ctx.AddStat("animalLootDropRate", cfg.HungerEnhancePotionAnimalLootDrop);
            ctx.AddStat("animalSeekingRange", cfg.HungerEnhancePotionAnimalSeekingRange);
            ctx.AddStat("forageDropRate", cfg.HungerEnhancePotionForageDrop);
            ctx.AddStat("wildCropDropRate", cfg.HungerEnhancePotionWildCropDrop);
            ctx.AddStat("rustyGearDropRate", cfg.HungerEnhancePotionGearDrop);
            ctx.AddStat("vesselContentsDropRate", cfg.HungerEnhancePotionVesselContentsDrop);
            ctx.AddStat("miningSpeedMul", cfg.HungerEnhancePotionMiningSpeed);
            ctx.AddStat("oreDropRate", cfg.HungerEnhancePotionOreDrop);
            ctx.AddStat("maxhealthExtraPoints", cfg.HungerEnhancePotionMaxHealth);
            if (cfg.HungerEnhancePotionHealth != 0f)
            {
                ctx.Health += cfg.HungerEnhancePotionHealth;
                if (ctx.TickSec == 0 && cfg.HungerEnhancePotionHealthTickSec > 0)
                    ctx.TickSec = cfg.HungerEnhancePotionHealthTickSec;
            }
        }

        private static void ApplyHungerSupressPotion(EffectContext ctx)
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;
            ctx.ResetsEffects = cfg.HungerSupressPotionResetsEffects;
            ctx.AddStat("hungerrate", cfg.HungerSupressPotionValue);
            ctx.Duration = cfg.HungerSupressPotionDuration;
            ctx.AddStat("walkspeed", cfg.HungerSupressPotionWalkSpeed);
            ctx.AddStat("meleeWeaponsDamage", cfg.HungerSupressPotionMeleeDamage);
            ctx.AddStat("rangedWeaponsAcc", cfg.HungerSupressPotionRangedAccuracy);
            ctx.AddStat("rangedWeaponsDamage", cfg.HungerSupressPotionRangedDamage);
            ctx.AddStat("rangedWeaponsSpeed", cfg.HungerSupressPotionRangedSpeed);
            ctx.AddStat("healingeffectivness", cfg.HungerSupressPotionHealingEffectiveness);
            ctx.AddStat("animalLootDropRate", cfg.HungerSupressPotionAnimalLootDrop);
            ctx.AddStat("animalSeekingRange", cfg.HungerSupressPotionAnimalSeekingRange);
            ctx.AddStat("forageDropRate", cfg.HungerSupressPotionForageDrop);
            ctx.AddStat("wildCropDropRate", cfg.HungerSupressPotionWildCropDrop);
            ctx.AddStat("rustyGearDropRate", cfg.HungerSupressPotionGearDrop);
            ctx.AddStat("vesselContentsDropRate", cfg.HungerSupressPotionVesselContentsDrop);
            ctx.AddStat("miningSpeedMul", cfg.HungerSupressPotionMiningSpeed);
            ctx.AddStat("oreDropRate", cfg.HungerSupressPotionOreDrop);
            ctx.AddStat("maxhealthExtraPoints", cfg.HungerSupressPotionMaxHealth);
            if (cfg.HungerSupressPotionHealth != 0f)
            {
                ctx.Health += cfg.HungerSupressPotionHealth;
                if (ctx.TickSec == 0 && cfg.HungerSupressPotionHealthTickSec > 0)
                    ctx.TickSec = cfg.HungerSupressPotionHealthTickSec;
            }
        }

        private static void ApplyHunterPotion(EffectContext ctx)
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;
            ctx.ResetsEffects = cfg.HunterPotionResetsEffects;
            ctx.AddStat("animalLootDropRate", cfg.HunterPotionAnimalDrop);
            ctx.AddStat("animalSeekingRange", cfg.HunterPotionAnimalSeek);
            ctx.AddStat("forageDropRate", cfg.HunterPotionForageDrop);
            ctx.AddStat("wildCropDropRate", cfg.HunterPotionWildDrop);
            ctx.Duration = cfg.HunterPotionDuration;
            ctx.AddStat("walkspeed", cfg.HunterPotionWalkSpeed);
            ctx.AddStat("meleeWeaponsDamage", cfg.HunterPotionMeleeDamage);
            ctx.AddStat("rangedWeaponsAcc", cfg.HunterPotionRangedAccuracy);
            ctx.AddStat("rangedWeaponsDamage", cfg.HunterPotionRangedDamage);
            ctx.AddStat("rangedWeaponsSpeed", cfg.HunterPotionRangedSpeed);
            ctx.AddStat("healingeffectivness", cfg.HunterPotionHealingEffectiveness);
            ctx.AddStat("hungerrate", cfg.HunterPotionHungerRate);
            ctx.AddStat("rustyGearDropRate", cfg.HunterPotionGearDrop);
            ctx.AddStat("vesselContentsDropRate", cfg.HunterPotionVesselContentsDrop);
            ctx.AddStat("miningSpeedMul", cfg.HunterPotionMiningSpeed);
            ctx.AddStat("oreDropRate", cfg.HunterPotionOreDrop);
            ctx.AddStat("maxhealthExtraPoints", cfg.HunterPotionMaxHealth);
            if (cfg.HunterPotionHealth != 0f)
            {
                ctx.Health += cfg.HunterPotionHealth;
                if (ctx.TickSec == 0 && cfg.HunterPotionHealthTickSec > 0)
                    ctx.TickSec = cfg.HunterPotionHealthTickSec;
            }
        }

        private static void ApplyLooterPotion(EffectContext ctx)
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;
            ctx.ResetsEffects = cfg.LooterPotionResetsEffects;
            ctx.AddStat("forageDropRate", cfg.LooterPotionForageDrop);
            ctx.AddStat("rustyGearDropRate", cfg.LooterPotionGearDrop);
            ctx.AddStat("vesselContentsDropRate", cfg.LooterPotionVesselContentDrop);
            ctx.AddStat("wildCropDropRate", cfg.LooterPotionWildDrop);
            ctx.Duration = cfg.LooterPotionDuration;
            ctx.AddStat("walkspeed", cfg.LooterPotionWalkSpeed);
            ctx.AddStat("meleeWeaponsDamage", cfg.LooterPotionMeleeDamage);
            ctx.AddStat("rangedWeaponsAcc", cfg.LooterPotionRangedAccuracy);
            ctx.AddStat("rangedWeaponsDamage", cfg.LooterPotionRangedDamage);
            ctx.AddStat("rangedWeaponsSpeed", cfg.LooterPotionRangedSpeed);
            ctx.AddStat("healingeffectivness", cfg.LooterPotionHealingEffectiveness);
            ctx.AddStat("hungerrate", cfg.LooterPotionHungerRate);
            ctx.AddStat("animalLootDropRate", cfg.LooterPotionAnimalLootDrop);
            ctx.AddStat("animalSeekingRange", cfg.LooterPotionAnimalSeekingRange);
            ctx.AddStat("miningSpeedMul", cfg.LooterPotionMiningSpeed);
            ctx.AddStat("oreDropRate", cfg.LooterPotionOreDrop);
            ctx.AddStat("maxhealthExtraPoints", cfg.LooterPotionMaxHealth);
            if (cfg.LooterPotionHealth != 0f)
            {
                ctx.Health += cfg.LooterPotionHealth;
                if (ctx.TickSec == 0 && cfg.LooterPotionHealthTickSec > 0)
                    ctx.TickSec = cfg.LooterPotionHealthTickSec;
            }
        }

        private static void ApplyMeleePotion(EffectContext ctx)
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;
            ctx.ResetsEffects = cfg.MeleePotionResetsEffects;
            ctx.AddStat("meleeWeaponsDamage", cfg.MeleePotionDamage);
            ctx.Duration = cfg.MeleePotionDuration;
            ctx.AddStat("walkspeed", cfg.MeleePotionWalkSpeed);
            ctx.AddStat("rangedWeaponsAcc", cfg.MeleePotionRangedAccuracy);
            ctx.AddStat("rangedWeaponsDamage", cfg.MeleePotionRangedDamage);
            ctx.AddStat("rangedWeaponsSpeed", cfg.MeleePotionRangedSpeed);
            ctx.AddStat("healingeffectivness", cfg.MeleePotionHealingEffectiveness);
            ctx.AddStat("hungerrate", cfg.MeleePotionHungerRate);
            ctx.AddStat("animalLootDropRate", cfg.MeleePotionAnimalLootDrop);
            ctx.AddStat("animalSeekingRange", cfg.MeleePotionAnimalSeekingRange);
            ctx.AddStat("forageDropRate", cfg.MeleePotionForageDrop);
            ctx.AddStat("wildCropDropRate", cfg.MeleePotionWildCropDrop);
            ctx.AddStat("rustyGearDropRate", cfg.MeleePotionGearDrop);
            ctx.AddStat("vesselContentsDropRate", cfg.MeleePotionVesselContentsDrop);
            ctx.AddStat("miningSpeedMul", cfg.MeleePotionMiningSpeed);
            ctx.AddStat("oreDropRate", cfg.MeleePotionOreDrop);
            ctx.AddStat("maxhealthExtraPoints", cfg.MeleePotionMaxHealth);
            if (cfg.MeleePotionHealth != 0f)
            {
                ctx.Health += cfg.MeleePotionHealth;
                if (ctx.TickSec == 0 && cfg.MeleePotionHealthTickSec > 0)
                    ctx.TickSec = cfg.MeleePotionHealthTickSec;
            }
        }

        private static void ApplyMiningPotion(EffectContext ctx)
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;
            ctx.ResetsEffects = cfg.MiningPotionResetsEffects;
            ctx.AddStat("miningSpeedMul", cfg.MiningPotionSpeed);
            ctx.AddStat("oreDropRate", cfg.MiningPotionOreDrop);
            ctx.Duration = cfg.MiningPotionDuration;
            ctx.AddStat("walkspeed", cfg.MiningPotionWalkSpeed);
            ctx.AddStat("meleeWeaponsDamage", cfg.MiningPotionMeleeDamage);
            ctx.AddStat("rangedWeaponsAcc", cfg.MiningPotionRangedAccuracy);
            ctx.AddStat("rangedWeaponsDamage", cfg.MiningPotionRangedDamage);
            ctx.AddStat("rangedWeaponsSpeed", cfg.MiningPotionRangedSpeed);
            ctx.AddStat("healingeffectivness", cfg.MiningPotionHealingEffectiveness);
            ctx.AddStat("hungerrate", cfg.MiningPotionHungerRate);
            ctx.AddStat("animalLootDropRate", cfg.MiningPotionAnimalLootDrop);
            ctx.AddStat("animalSeekingRange", cfg.MiningPotionAnimalSeekingRange);
            ctx.AddStat("forageDropRate", cfg.MiningPotionForageDrop);
            ctx.AddStat("wildCropDropRate", cfg.MiningPotionWildCropDrop);
            ctx.AddStat("rustyGearDropRate", cfg.MiningPotionGearDrop);
            ctx.AddStat("vesselContentsDropRate", cfg.MiningPotionVesselContentsDrop);
            ctx.AddStat("maxhealthExtraPoints", cfg.MiningPotionMaxHealth);
            if (cfg.MiningPotionHealthExtra != 0f)
            {
                ctx.Health += cfg.MiningPotionHealthExtra;
                if (ctx.TickSec == 0 && cfg.MiningPotionHealthExtraTickSec > 0)
                    ctx.TickSec = cfg.MiningPotionHealthExtraTickSec;
            }
        }

        private static void ApplyPoisonPotion(EffectContext ctx)
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;
            ctx.ResetsEffects = cfg.PoisonPotionResetsEffects;
            ctx.SetHealth(cfg.PoisonPotionHealth);
            ctx.TickSec = cfg.PoisonPotionTickSec;
            ctx.Duration = cfg.PoisonPotionDuration;
            ctx.AddStat("walkspeed", cfg.PoisonPotionWalkSpeed);
            ctx.AddStat("meleeWeaponsDamage", cfg.PoisonPotionMeleeDamage);
            ctx.AddStat("rangedWeaponsAcc", cfg.PoisonPotionRangedAccuracy);
            ctx.AddStat("rangedWeaponsDamage", cfg.PoisonPotionRangedDamage);
            ctx.AddStat("rangedWeaponsSpeed", cfg.PoisonPotionRangedSpeed);
            ctx.AddStat("healingeffectivness", cfg.PoisonPotionHealingEffectiveness);
            ctx.AddStat("hungerrate", cfg.PoisonPotionHungerRate);
            ctx.AddStat("animalLootDropRate", cfg.PoisonPotionAnimalLootDrop);
            ctx.AddStat("animalSeekingRange", cfg.PoisonPotionAnimalSeekingRange);
            ctx.AddStat("forageDropRate", cfg.PoisonPotionForageDrop);
            ctx.AddStat("wildCropDropRate", cfg.PoisonPotionWildCropDrop);
            ctx.AddStat("rustyGearDropRate", cfg.PoisonPotionGearDrop);
            ctx.AddStat("vesselContentsDropRate", cfg.PoisonPotionVesselContentsDrop);
            ctx.AddStat("miningSpeedMul", cfg.PoisonPotionMiningSpeed);
            ctx.AddStat("oreDropRate", cfg.PoisonPotionOreDrop);
            ctx.AddStat("maxhealthExtraPoints", cfg.PoisonPotionMaxHealth);
        }

        private static void ApplyPredatorPotion(EffectContext ctx)
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;
            ctx.ResetsEffects = cfg.PredatorPotionResetsEffects;
            ctx.AddStat("animalSeekingRange", cfg.PredatorPotionAnimalSeek);
            ctx.Duration = cfg.PredatorPotionDuration;
            ctx.AddStat("walkspeed", cfg.PredatorPotionWalkSpeed);
            ctx.AddStat("meleeWeaponsDamage", cfg.PredatorPotionMeleeDamage);
            ctx.AddStat("rangedWeaponsAcc", cfg.PredatorPotionRangedAccuracy);
            ctx.AddStat("rangedWeaponsDamage", cfg.PredatorPotionRangedDamage);
            ctx.AddStat("rangedWeaponsSpeed", cfg.PredatorPotionRangedSpeed);
            ctx.AddStat("healingeffectivness", cfg.PredatorPotionHealingEffectiveness);
            ctx.AddStat("hungerrate", cfg.PredatorPotionHungerRate);
            ctx.AddStat("animalLootDropRate", cfg.PredatorPotionAnimalLootDrop);
            ctx.AddStat("forageDropRate", cfg.PredatorPotionForageDrop);
            ctx.AddStat("wildCropDropRate", cfg.PredatorPotionWildCropDrop);
            ctx.AddStat("rustyGearDropRate", cfg.PredatorPotionGearDrop);
            ctx.AddStat("vesselContentsDropRate", cfg.PredatorPotionVesselContentsDrop);
            ctx.AddStat("miningSpeedMul", cfg.PredatorPotionMiningSpeed);
            ctx.AddStat("oreDropRate", cfg.PredatorPotionOreDrop);
            ctx.AddStat("maxhealthExtraPoints", cfg.PredatorPotionMaxHealth);
            if (cfg.PredatorPotionHealth != 0f)
            {
                ctx.Health += cfg.PredatorPotionHealth;
                if (ctx.TickSec == 0 && cfg.PredatorPotionHealthTickSec > 0)
                    ctx.TickSec = cfg.PredatorPotionHealthTickSec;
            }
        }

        private static void ApplyRegenPotion(EffectContext ctx)
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;
            ctx.ResetsEffects = cfg.RegenPotionResetsEffects;
            ctx.SetHealth(cfg.RegenPotionHealth);
            ctx.TickSec = cfg.RegenPotionTickSec;
            ctx.Duration = cfg.RegenPotionDuration;
            ctx.AddStat("walkspeed", cfg.RegenPotionWalkSpeed);
            ctx.AddStat("meleeWeaponsDamage", cfg.RegenPotionMeleeDamage);
            ctx.AddStat("rangedWeaponsAcc", cfg.RegenPotionRangedAccuracy);
            ctx.AddStat("rangedWeaponsDamage", cfg.RegenPotionRangedDamage);
            ctx.AddStat("rangedWeaponsSpeed", cfg.RegenPotionRangedSpeed);
            ctx.AddStat("healingeffectivness", cfg.RegenPotionHealingEffectiveness);
            ctx.AddStat("hungerrate", cfg.RegenPotionHungerRate);
            ctx.AddStat("animalLootDropRate", cfg.RegenPotionAnimalLootDrop);
            ctx.AddStat("animalSeekingRange", cfg.RegenPotionAnimalSeekingRange);
            ctx.AddStat("forageDropRate", cfg.RegenPotionForageDrop);
            ctx.AddStat("wildCropDropRate", cfg.RegenPotionWildCropDrop);
            ctx.AddStat("rustyGearDropRate", cfg.RegenPotionGearDrop);
            ctx.AddStat("vesselContentsDropRate", cfg.RegenPotionVesselContentsDrop);
            ctx.AddStat("miningSpeedMul", cfg.RegenPotionMiningSpeed);
            ctx.AddStat("oreDropRate", cfg.RegenPotionOreDrop);
            ctx.AddStat("maxhealthExtraPoints", cfg.RegenPotionMaxHealth);
        }

        private static void ApplyScentMaskPotion(EffectContext ctx)
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;
            ctx.ResetsEffects = cfg.ScentMaskPotionResetsEffects;
            ctx.AddStat("animalSeekingRange", cfg.ScentMaskPotionAnimalSeek);
            ctx.Duration = cfg.ScentMaskPotionDuration;
            ctx.AddStat("walkspeed", cfg.ScentMaskPotionWalkSpeed);
            ctx.AddStat("meleeWeaponsDamage", cfg.ScentMaskPotionMeleeDamage);
            ctx.AddStat("rangedWeaponsAcc", cfg.ScentMaskPotionRangedAccuracy);
            ctx.AddStat("rangedWeaponsDamage", cfg.ScentMaskPotionRangedDamage);
            ctx.AddStat("rangedWeaponsSpeed", cfg.ScentMaskPotionRangedSpeed);
            ctx.AddStat("healingeffectivness", cfg.ScentMaskPotionHealingEffectiveness);
            ctx.AddStat("hungerrate", cfg.ScentMaskPotionHungerRate);
            ctx.AddStat("animalLootDropRate", cfg.ScentMaskPotionAnimalLootDrop);
            ctx.AddStat("forageDropRate", cfg.ScentMaskPotionForageDrop);
            ctx.AddStat("wildCropDropRate", cfg.ScentMaskPotionWildCropDrop);
            ctx.AddStat("rustyGearDropRate", cfg.ScentMaskPotionGearDrop);
            ctx.AddStat("vesselContentsDropRate", cfg.ScentMaskPotionVesselContentsDrop);
            ctx.AddStat("miningSpeedMul", cfg.ScentMaskPotionMiningSpeed);
            ctx.AddStat("oreDropRate", cfg.ScentMaskPotionOreDrop);
            ctx.AddStat("maxhealthExtraPoints", cfg.ScentMaskPotionMaxHealth);
            if (cfg.ScentMaskPotionHealth != 0f)
            {
                ctx.Health += cfg.ScentMaskPotionHealth;
                if (ctx.TickSec == 0 && cfg.ScentMaskPotionHealthTickSec > 0)
                    ctx.TickSec = cfg.ScentMaskPotionHealthTickSec;
            }
        }

        private static void ApplySpeedPotion(EffectContext ctx)
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;
            ctx.ResetsEffects = cfg.SpeedPotionResetsEffects;
            ctx.AddStat("walkspeed", cfg.SpeedPotionValue);
            ctx.Duration = cfg.SpeedPotionDuration;
            ctx.AddStat("meleeWeaponsDamage", cfg.SpeedPotionMeleeDamage);
            ctx.AddStat("rangedWeaponsAcc", cfg.SpeedPotionRangedAccuracy);
            ctx.AddStat("rangedWeaponsDamage", cfg.SpeedPotionRangedDamage);
            ctx.AddStat("rangedWeaponsSpeed", cfg.SpeedPotionRangedSpeed);
            ctx.AddStat("healingeffectivness", cfg.SpeedPotionHealingEffectiveness);
            ctx.AddStat("hungerrate", cfg.SpeedPotionHungerRate);
            ctx.AddStat("animalLootDropRate", cfg.SpeedPotionAnimalLootDrop);
            ctx.AddStat("animalSeekingRange", cfg.SpeedPotionAnimalSeekingRange);
            ctx.AddStat("forageDropRate", cfg.SpeedPotionForageDrop);
            ctx.AddStat("wildCropDropRate", cfg.SpeedPotionWildCropDrop);
            ctx.AddStat("rustyGearDropRate", cfg.SpeedPotionGearDrop);
            ctx.AddStat("vesselContentsDropRate", cfg.SpeedPotionVesselContentsDrop);
            ctx.AddStat("miningSpeedMul", cfg.SpeedPotionMiningSpeed);
            ctx.AddStat("oreDropRate", cfg.SpeedPotionOreDrop);
            ctx.AddStat("maxhealthExtraPoints", cfg.SpeedPotionMaxHealth);
            if (cfg.SpeedPotionHealth != 0f)
            {
                ctx.Health += cfg.SpeedPotionHealth;
                if (ctx.TickSec == 0 && cfg.SpeedPotionHealthTickSec > 0)
                    ctx.TickSec = cfg.SpeedPotionHealthTickSec;
            }
        }

        private static void ApplyVitalityPotion(EffectContext ctx)
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;
            ctx.ResetsEffects = cfg.VitalityPotionResetsEffects;
            ctx.AddStat("maxhealthExtraPoints", cfg.VitalityPotionMaxHealth);
            ctx.Duration = cfg.VitalityPotionDuration;
            ctx.AddStat("walkspeed", cfg.VitalityPotionWalkSpeed);
            ctx.AddStat("meleeWeaponsDamage", cfg.VitalityPotionMeleeDamage);
            ctx.AddStat("rangedWeaponsAcc", cfg.VitalityPotionRangedAccuracy);
            ctx.AddStat("rangedWeaponsDamage", cfg.VitalityPotionRangedDamage);
            ctx.AddStat("rangedWeaponsSpeed", cfg.VitalityPotionRangedSpeed);
            ctx.AddStat("healingeffectivness", cfg.VitalityPotionHealingEffectiveness);
            ctx.AddStat("hungerrate", cfg.VitalityPotionHungerRate);
            ctx.AddStat("animalLootDropRate", cfg.VitalityPotionAnimalLootDrop);
            ctx.AddStat("animalSeekingRange", cfg.VitalityPotionAnimalSeekingRange);
            ctx.AddStat("forageDropRate", cfg.VitalityPotionForageDrop);
            ctx.AddStat("wildCropDropRate", cfg.VitalityPotionWildCropDrop);
            ctx.AddStat("rustyGearDropRate", cfg.VitalityPotionGearDrop);
            ctx.AddStat("vesselContentsDropRate", cfg.VitalityPotionVesselContentsDrop);
            ctx.AddStat("miningSpeedMul", cfg.VitalityPotionMiningSpeed);
            ctx.AddStat("oreDropRate", cfg.VitalityPotionOreDrop);
            if (cfg.VitalityPotionHealth != 0f)
            {
                ctx.Health += cfg.VitalityPotionHealth;
                if (ctx.TickSec == 0 && cfg.VitalityPotionHealthTickSec > 0)
                    ctx.TickSec = cfg.VitalityPotionHealthTickSec;
            }
        }

        private static void ApplyGlowPotion(EffectContext ctx)
        {
            ctx.ResetsEffects = AlchemyConfig.Loaded.GlowPotionResetsEffects;
            ctx.Duration = AlchemyConfig.Loaded.GlowPotionDuration;
            ctx.GlowStrength = AlchemyConfig.Loaded.GlowPotionStrength;
        }

        private static void ApplyWaterBreathePotion(EffectContext ctx)
        {
            ctx.ResetsEffects = AlchemyConfig.Loaded.WaterBreathePotionResetsEffects;
            ctx.WaterBreathe = true;
            ctx.Duration = AlchemyConfig.Loaded.WaterBreathePotionDuration;
        }

        private static void ApplyColdResistPotion(EffectContext ctx)
        {
            ctx.ResetsEffects = AlchemyConfig.Loaded.ColdResistPotionResetsEffects;
            ctx.ColdResist = true;
            ctx.Duration = AlchemyConfig.Loaded.ColdResistPotionDuration;
        }

        private static void ApplyNutritionPotion(EffectContext ctx)
        {
            ctx.ResetsEffects = AlchemyConfig.Loaded.NutritionPotionResetsEffects;
            ctx.RetainedNutrition = AlchemyConfig.Loaded.NutritionPotionRetainedNutrition;
        }

        private static void ApplyRecallPotion(EffectContext ctx)
        {
            ctx.ResetsEffects = AlchemyConfig.Loaded.RecallPotionResetsEffects;
            ctx.Respawn = true;
        }

        private static void ApplyTemporalPotion(EffectContext ctx)
        {
            ctx.ResetsEffects = AlchemyConfig.Loaded.TemporalPotionResetsEffects;
            ctx.TemporalStabilityGain = AlchemyConfig.Loaded.StabilityPotionTemporalStabilityGain;
        }

        private static void ApplyReshapePotion(EffectContext ctx)
        {
            ctx.ResetsEffects = AlchemyConfig.Loaded.ReshapePotionResetsEffects;
            ctx.Reshape = true;
        }

        private static void ApplyGrowPotion(EffectContext ctx)
        {
            ctx.ResetsEffects = AlchemyConfig.Loaded.GrowPotionResetsEffects;
            ctx.SizeChange = AlchemyConfig.Loaded.GrowPotionSizeChange;
        }

        private static void ApplyShrinkPotion(EffectContext ctx)
        {
            ctx.ResetsEffects = AlchemyConfig.Loaded.ShrinkPotionResetsEffects;
            ctx.SizeChange = AlchemyConfig.Loaded.ShrinkPotionSizeChange;
        }

        private static void ApplyFallPotion(EffectContext ctx)
        {
            ctx.ResetsEffects = AlchemyConfig.Loaded.FallPotionResetsEffects;
            ctx.FallDamageReduction = AlchemyConfig.Loaded.FallPotionDamageReduction;
            ctx.Duration = AlchemyConfig.Loaded.FallPotionDuration;
        }

        private static void ApplyClimbPotion(EffectContext ctx)
        {
            ctx.ResetsEffects = AlchemyConfig.Loaded.ClimbPotionResetsEffects;
            ctx.CanClimbAnywhere = true;
            ctx.Duration = AlchemyConfig.Loaded.ClimbPotionDuration;
        }

        private static void ApplyFlightPotion(EffectContext ctx)
        {
            ctx.ResetsEffects = AlchemyConfig.Loaded.FlightPotionResetsEffects;
            ctx.CanFly = true;
            ctx.Duration = AlchemyConfig.Loaded.FlightPotionDuration;
        }
    }
}
