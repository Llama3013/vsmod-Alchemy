using System.Collections.Generic;
using Vintagestory.API.Common;

namespace Alchemy
{
    public delegate void PotionApply(PotionContext ctx);

    public static class PotionRegistry
    {
        private static Dictionary<string, PotionApply> apply;
        public static Dictionary<string, PotionApply> Apply => apply;

        public static PotionContext BuildPotionDef(string potionId, float strengthMul)
        {
            if (string.IsNullOrWhiteSpace(potionId))
                return null;
            if (!Apply.TryGetValue(potionId, out PotionApply applyDelegate))
                return null;

            PotionContext def = new() { StrengthMul = strengthMul };

            applyDelegate(def);
            return def;
        }

        public static void Init()
        {
            apply = BuildRegistry();
        }

        private static Dictionary<string, PotionApply> BuildRegistry()
        {
            return new()
            {
                ["archerpotionid"] = ApplyArcherPotion,
                ["healingeffectpotionid"] = ApplyHealingEffectPotion,
                ["hungerenhancepotionid"] = ApplyHungerEnhancePotion,
                ["hungersupresspotionid"] = ApplyHungerSupressPotion,
                ["hunterpotionid"] = ApplyHunterPotion,
                ["looterpotionid"] = ApplyLooterPotion,
                ["meleepotionid"] = ApplyMeleePotion,
                ["miningpotionid"] = ApplyMiningPotion,
                ["poisontickpotionid"] = ApplyPoisonPotion,
                ["predatorpotionid"] = ApplyPredatorPotion,
                ["regentickpotionid"] = ApplyRegenPotion,
                ["scentmaskpotionid"] = ApplyScentMaskPotion,
                ["speedpotionid"] = ApplySpeedPotion,
                ["vitalitypotionid"] = ApplyVitalityPotion,
                ["glowpotionid"] = ApplyGlowPotion,
                ["waterbreathepotionid"] = ApplyWaterBreathePotion,
                ["coldresistpotionid"] = ApplyColdResistPotion,
                ["nutritionpotionid"] = ApplyNutritionPotion,
                ["recallpotionid"] = ApplyRecallPotion,
                ["temporalpotionid"] = ApplyTemporalPotion,
                ["reshapepotionid"] = ApplyReshapePotion,
                ["growpotionid"] = ApplyGrowPotion,
                ["shrinkpotionid"] = ApplyShrinkPotion,
                ["fallpotionid"] = ApplyFallPotion,
                ["climbpotionid"] = ApplyClimbPotion,
                ["flightpotionid"] = ApplyFlightPotion,
            };
        }

        private static void ApplyArcherPotion(PotionContext ctx)
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;
            ctx.ResetsEffects = cfg.ArcherPotionResetsEffects;
            ctx.AddEffect("rangedWeaponsAcc", cfg.ArcherPotionAcc);
            ctx.AddEffect("rangedWeaponsDamage", cfg.ArcherPotionDamage);
            ctx.AddEffect("rangedWeaponsSpeed", cfg.ArcherPotionSpeed);
            ctx.Duration = cfg.ArcherPotionDuration;
            ctx.AddEffect("walkspeed", cfg.ArcherPotionWalkSpeed);
            ctx.AddEffect("meleeWeaponsDamage", cfg.ArcherPotionMeleeDamage);
            ctx.AddEffect("healingeffectivness", cfg.ArcherPotionHealingEffectiveness);
            ctx.AddEffect("hungerrate", cfg.ArcherPotionHungerRate);
            ctx.AddEffect("animalLootDropRate", cfg.ArcherPotionAnimalLootDrop);
            ctx.AddEffect("animalSeekingRange", cfg.ArcherPotionAnimalSeekingRange);
            ctx.AddEffect("forageDropRate", cfg.ArcherPotionForageDrop);
            ctx.AddEffect("wildCropDropRate", cfg.ArcherPotionWildCropDrop);
            ctx.AddEffect("rustyGearDropRate", cfg.ArcherPotionGearDrop);
            ctx.AddEffect("vesselContentsDropRate", cfg.ArcherPotionVesselContentsDrop);
            ctx.AddEffect("miningSpeedMul", cfg.ArcherPotionMiningSpeed);
            ctx.AddEffect("oreDropRate", cfg.ArcherPotionOreDrop);
            ctx.AddEffect("maxhealthExtraPoints", cfg.ArcherPotionMaxHealth);
            if (cfg.ArcherPotionHealth != 0f)
            {
                ctx.Health += cfg.ArcherPotionHealth;
                if (ctx.TickSec == 0 && cfg.ArcherPotionHealthTickSec > 0)
                    ctx.TickSec = cfg.ArcherPotionHealthTickSec;
            }
        }

        private static void ApplyHealingEffectPotion(PotionContext ctx)
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;
            ctx.ResetsEffects = cfg.HealingEffectPotionResetsEffects;
            ctx.AddEffect("healingeffectivness", cfg.HealingEffectPotionValue);
            ctx.Duration = cfg.HealingEffectPotionDuration;
            ctx.AddEffect("walkspeed", cfg.HealingEffectPotionWalkSpeed);
            ctx.AddEffect("meleeWeaponsDamage", cfg.HealingEffectPotionMeleeDamage);
            ctx.AddEffect("rangedWeaponsAcc", cfg.HealingEffectPotionRangedAccuracy);
            ctx.AddEffect("rangedWeaponsDamage", cfg.HealingEffectPotionRangedDamage);
            ctx.AddEffect("rangedWeaponsSpeed", cfg.HealingEffectPotionRangedSpeed);
            ctx.AddEffect("hungerrate", cfg.HealingEffectPotionHungerRate);
            ctx.AddEffect("animalLootDropRate", cfg.HealingEffectPotionAnimalLootDrop);
            ctx.AddEffect("animalSeekingRange", cfg.HealingEffectPotionAnimalSeekingRange);
            ctx.AddEffect("forageDropRate", cfg.HealingEffectPotionForageDrop);
            ctx.AddEffect("wildCropDropRate", cfg.HealingEffectPotionWildCropDrop);
            ctx.AddEffect("rustyGearDropRate", cfg.HealingEffectPotionGearDrop);
            ctx.AddEffect("vesselContentsDropRate", cfg.HealingEffectPotionVesselContentsDrop);
            ctx.AddEffect("miningSpeedMul", cfg.HealingEffectPotionMiningSpeed);
            ctx.AddEffect("oreDropRate", cfg.HealingEffectPotionOreDrop);
            ctx.AddEffect("maxhealthExtraPoints", cfg.HealingEffectPotionMaxHealth);
            if (cfg.HealingEffectPotionHealth != 0f)
            {
                ctx.Health += cfg.HealingEffectPotionHealth;
                if (ctx.TickSec == 0 && cfg.HealingEffectPotionHealthTickSec > 0)
                    ctx.TickSec = cfg.HealingEffectPotionHealthTickSec;
            }
        }

        private static void ApplyHungerEnhancePotion(PotionContext ctx)
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;
            ctx.ResetsEffects = cfg.HungerEnhancePotionResetsEffects;
            ctx.AddEffect("hungerrate", cfg.HungerEnhancePotionValue);
            ctx.Duration = cfg.HungerEnhancePotionDuration;
            ctx.AddEffect("walkspeed", cfg.HungerEnhancePotionWalkSpeed);
            ctx.AddEffect("meleeWeaponsDamage", cfg.HungerEnhancePotionMeleeDamage);
            ctx.AddEffect("rangedWeaponsAcc", cfg.HungerEnhancePotionRangedAccuracy);
            ctx.AddEffect("rangedWeaponsDamage", cfg.HungerEnhancePotionRangedDamage);
            ctx.AddEffect("rangedWeaponsSpeed", cfg.HungerEnhancePotionRangedSpeed);
            ctx.AddEffect("healingeffectivness", cfg.HungerEnhancePotionHealingEffectiveness);
            ctx.AddEffect("animalLootDropRate", cfg.HungerEnhancePotionAnimalLootDrop);
            ctx.AddEffect("animalSeekingRange", cfg.HungerEnhancePotionAnimalSeekingRange);
            ctx.AddEffect("forageDropRate", cfg.HungerEnhancePotionForageDrop);
            ctx.AddEffect("wildCropDropRate", cfg.HungerEnhancePotionWildCropDrop);
            ctx.AddEffect("rustyGearDropRate", cfg.HungerEnhancePotionGearDrop);
            ctx.AddEffect("vesselContentsDropRate", cfg.HungerEnhancePotionVesselContentsDrop);
            ctx.AddEffect("miningSpeedMul", cfg.HungerEnhancePotionMiningSpeed);
            ctx.AddEffect("oreDropRate", cfg.HungerEnhancePotionOreDrop);
            ctx.AddEffect("maxhealthExtraPoints", cfg.HungerEnhancePotionMaxHealth);
            if (cfg.HungerEnhancePotionHealth != 0f)
            {
                ctx.Health += cfg.HungerEnhancePotionHealth;
                if (ctx.TickSec == 0 && cfg.HungerEnhancePotionHealthTickSec > 0)
                    ctx.TickSec = cfg.HungerEnhancePotionHealthTickSec;
            }
        }

        private static void ApplyHungerSupressPotion(PotionContext ctx)
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;
            ctx.ResetsEffects = cfg.HungerSupressPotionResetsEffects;
            ctx.AddEffect("hungerrate", cfg.HungerSupressPotionValue);
            ctx.Duration = cfg.HungerSupressPotionDuration;
            ctx.AddEffect("walkspeed", cfg.HungerSupressPotionWalkSpeed);
            ctx.AddEffect("meleeWeaponsDamage", cfg.HungerSupressPotionMeleeDamage);
            ctx.AddEffect("rangedWeaponsAcc", cfg.HungerSupressPotionRangedAccuracy);
            ctx.AddEffect("rangedWeaponsDamage", cfg.HungerSupressPotionRangedDamage);
            ctx.AddEffect("rangedWeaponsSpeed", cfg.HungerSupressPotionRangedSpeed);
            ctx.AddEffect("healingeffectivness", cfg.HungerSupressPotionHealingEffectiveness);
            ctx.AddEffect("animalLootDropRate", cfg.HungerSupressPotionAnimalLootDrop);
            ctx.AddEffect("animalSeekingRange", cfg.HungerSupressPotionAnimalSeekingRange);
            ctx.AddEffect("forageDropRate", cfg.HungerSupressPotionForageDrop);
            ctx.AddEffect("wildCropDropRate", cfg.HungerSupressPotionWildCropDrop);
            ctx.AddEffect("rustyGearDropRate", cfg.HungerSupressPotionGearDrop);
            ctx.AddEffect("vesselContentsDropRate", cfg.HungerSupressPotionVesselContentsDrop);
            ctx.AddEffect("miningSpeedMul", cfg.HungerSupressPotionMiningSpeed);
            ctx.AddEffect("oreDropRate", cfg.HungerSupressPotionOreDrop);
            ctx.AddEffect("maxhealthExtraPoints", cfg.HungerSupressPotionMaxHealth);
            if (cfg.HungerSupressPotionHealth != 0f)
            {
                ctx.Health += cfg.HungerSupressPotionHealth;
                if (ctx.TickSec == 0 && cfg.HungerSupressPotionHealthTickSec > 0)
                    ctx.TickSec = cfg.HungerSupressPotionHealthTickSec;
            }
        }

        private static void ApplyHunterPotion(PotionContext ctx)
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;
            ctx.ResetsEffects = cfg.HunterPotionResetsEffects;
            ctx.AddEffect("animalLootDropRate", cfg.HunterPotionAnimalDrop);
            ctx.AddEffect("animalSeekingRange", cfg.HunterPotionAnimalSeek);
            ctx.AddEffect("forageDropRate", cfg.HunterPotionForageDrop);
            ctx.AddEffect("wildCropDropRate", cfg.HunterPotionWildDrop);
            ctx.Duration = cfg.HunterPotionDuration;
            ctx.AddEffect("walkspeed", cfg.HunterPotionWalkSpeed);
            ctx.AddEffect("meleeWeaponsDamage", cfg.HunterPotionMeleeDamage);
            ctx.AddEffect("rangedWeaponsAcc", cfg.HunterPotionRangedAccuracy);
            ctx.AddEffect("rangedWeaponsDamage", cfg.HunterPotionRangedDamage);
            ctx.AddEffect("rangedWeaponsSpeed", cfg.HunterPotionRangedSpeed);
            ctx.AddEffect("healingeffectivness", cfg.HunterPotionHealingEffectiveness);
            ctx.AddEffect("hungerrate", cfg.HunterPotionHungerRate);
            ctx.AddEffect("rustyGearDropRate", cfg.HunterPotionGearDrop);
            ctx.AddEffect("vesselContentsDropRate", cfg.HunterPotionVesselContentsDrop);
            ctx.AddEffect("miningSpeedMul", cfg.HunterPotionMiningSpeed);
            ctx.AddEffect("oreDropRate", cfg.HunterPotionOreDrop);
            ctx.AddEffect("maxhealthExtraPoints", cfg.HunterPotionMaxHealth);
            if (cfg.HunterPotionHealth != 0f)
            {
                ctx.Health += cfg.HunterPotionHealth;
                if (ctx.TickSec == 0 && cfg.HunterPotionHealthTickSec > 0)
                    ctx.TickSec = cfg.HunterPotionHealthTickSec;
            }
        }

        private static void ApplyLooterPotion(PotionContext ctx)
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;
            ctx.ResetsEffects = cfg.LooterPotionResetsEffects;
            ctx.AddEffect("forageDropRate", cfg.LooterPotionForageDrop);
            ctx.AddEffect("rustyGearDropRate", cfg.LooterPotionGearDrop);
            ctx.AddEffect("vesselContentsDropRate", cfg.LooterPotionVesselContentDrop);
            ctx.AddEffect("wildCropDropRate", cfg.LooterPotionWildDrop);
            ctx.Duration = cfg.LooterPotionDuration;
            ctx.AddEffect("walkspeed", cfg.LooterPotionWalkSpeed);
            ctx.AddEffect("meleeWeaponsDamage", cfg.LooterPotionMeleeDamage);
            ctx.AddEffect("rangedWeaponsAcc", cfg.LooterPotionRangedAccuracy);
            ctx.AddEffect("rangedWeaponsDamage", cfg.LooterPotionRangedDamage);
            ctx.AddEffect("rangedWeaponsSpeed", cfg.LooterPotionRangedSpeed);
            ctx.AddEffect("healingeffectivness", cfg.LooterPotionHealingEffectiveness);
            ctx.AddEffect("hungerrate", cfg.LooterPotionHungerRate);
            ctx.AddEffect("animalLootDropRate", cfg.LooterPotionAnimalLootDrop);
            ctx.AddEffect("animalSeekingRange", cfg.LooterPotionAnimalSeekingRange);
            ctx.AddEffect("miningSpeedMul", cfg.LooterPotionMiningSpeed);
            ctx.AddEffect("oreDropRate", cfg.LooterPotionOreDrop);
            ctx.AddEffect("maxhealthExtraPoints", cfg.LooterPotionMaxHealth);
            if (cfg.LooterPotionHealth != 0f)
            {
                ctx.Health += cfg.LooterPotionHealth;
                if (ctx.TickSec == 0 && cfg.LooterPotionHealthTickSec > 0)
                    ctx.TickSec = cfg.LooterPotionHealthTickSec;
            }
        }

        private static void ApplyMeleePotion(PotionContext ctx)
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;
            ctx.ResetsEffects = cfg.MeleePotionResetsEffects;
            ctx.AddEffect("meleeWeaponsDamage", cfg.MeleePotionDamage);
            ctx.Duration = cfg.MeleePotionDuration;
            ctx.AddEffect("walkspeed", cfg.MeleePotionWalkSpeed);
            ctx.AddEffect("rangedWeaponsAcc", cfg.MeleePotionRangedAccuracy);
            ctx.AddEffect("rangedWeaponsDamage", cfg.MeleePotionRangedDamage);
            ctx.AddEffect("rangedWeaponsSpeed", cfg.MeleePotionRangedSpeed);
            ctx.AddEffect("healingeffectivness", cfg.MeleePotionHealingEffectiveness);
            ctx.AddEffect("hungerrate", cfg.MeleePotionHungerRate);
            ctx.AddEffect("animalLootDropRate", cfg.MeleePotionAnimalLootDrop);
            ctx.AddEffect("animalSeekingRange", cfg.MeleePotionAnimalSeekingRange);
            ctx.AddEffect("forageDropRate", cfg.MeleePotionForageDrop);
            ctx.AddEffect("wildCropDropRate", cfg.MeleePotionWildCropDrop);
            ctx.AddEffect("rustyGearDropRate", cfg.MeleePotionGearDrop);
            ctx.AddEffect("vesselContentsDropRate", cfg.MeleePotionVesselContentsDrop);
            ctx.AddEffect("miningSpeedMul", cfg.MeleePotionMiningSpeed);
            ctx.AddEffect("oreDropRate", cfg.MeleePotionOreDrop);
            ctx.AddEffect("maxhealthExtraPoints", cfg.MeleePotionMaxHealth);
            if (cfg.MeleePotionHealth != 0f)
            {
                ctx.Health += cfg.MeleePotionHealth;
                if (ctx.TickSec == 0 && cfg.MeleePotionHealthTickSec > 0)
                    ctx.TickSec = cfg.MeleePotionHealthTickSec;
            }
        }

        private static void ApplyMiningPotion(PotionContext ctx)
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;
            ctx.ResetsEffects = cfg.MiningPotionResetsEffects;
            ctx.AddEffect("miningSpeedMul", cfg.MiningPotionSpeed);
            ctx.AddEffect("oreDropRate", cfg.MiningPotionOreDrop);
            ctx.Duration = cfg.MiningPotionDuration;
            ctx.AddEffect("walkspeed", cfg.MiningPotionWalkSpeed);
            ctx.AddEffect("meleeWeaponsDamage", cfg.MiningPotionMeleeDamage);
            ctx.AddEffect("rangedWeaponsAcc", cfg.MiningPotionRangedAccuracy);
            ctx.AddEffect("rangedWeaponsDamage", cfg.MiningPotionRangedDamage);
            ctx.AddEffect("rangedWeaponsSpeed", cfg.MiningPotionRangedSpeed);
            ctx.AddEffect("healingeffectivness", cfg.MiningPotionHealingEffectiveness);
            ctx.AddEffect("hungerrate", cfg.MiningPotionHungerRate);
            ctx.AddEffect("animalLootDropRate", cfg.MiningPotionAnimalLootDrop);
            ctx.AddEffect("animalSeekingRange", cfg.MiningPotionAnimalSeekingRange);
            ctx.AddEffect("forageDropRate", cfg.MiningPotionForageDrop);
            ctx.AddEffect("wildCropDropRate", cfg.MiningPotionWildCropDrop);
            ctx.AddEffect("rustyGearDropRate", cfg.MiningPotionGearDrop);
            ctx.AddEffect("vesselContentsDropRate", cfg.MiningPotionVesselContentsDrop);
            ctx.AddEffect("maxhealthExtraPoints", cfg.MiningPotionMaxHealth);
            if (cfg.MiningPotionHealthExtra != 0f)
            {
                ctx.Health += cfg.MiningPotionHealthExtra;
                if (ctx.TickSec == 0 && cfg.MiningPotionHealthExtraTickSec > 0)
                    ctx.TickSec = cfg.MiningPotionHealthExtraTickSec;
            }
        }

        private static void ApplyPoisonPotion(PotionContext ctx)
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;
            ctx.ResetsEffects = cfg.PoisonPotionResetsEffects;
            ctx.SetHealth(cfg.PoisonPotionHealth);
            ctx.TickSec = cfg.PoisonPotionTickSec;
            ctx.IgnoreArmour = cfg.PoisonPotionIgnoreArmour;
            ctx.Duration = cfg.PoisonPotionDuration;
            ctx.AddEffect("walkspeed", cfg.PoisonPotionWalkSpeed);
            ctx.AddEffect("meleeWeaponsDamage", cfg.PoisonPotionMeleeDamage);
            ctx.AddEffect("rangedWeaponsAcc", cfg.PoisonPotionRangedAccuracy);
            ctx.AddEffect("rangedWeaponsDamage", cfg.PoisonPotionRangedDamage);
            ctx.AddEffect("rangedWeaponsSpeed", cfg.PoisonPotionRangedSpeed);
            ctx.AddEffect("healingeffectivness", cfg.PoisonPotionHealingEffectiveness);
            ctx.AddEffect("hungerrate", cfg.PoisonPotionHungerRate);
            ctx.AddEffect("animalLootDropRate", cfg.PoisonPotionAnimalLootDrop);
            ctx.AddEffect("animalSeekingRange", cfg.PoisonPotionAnimalSeekingRange);
            ctx.AddEffect("forageDropRate", cfg.PoisonPotionForageDrop);
            ctx.AddEffect("wildCropDropRate", cfg.PoisonPotionWildCropDrop);
            ctx.AddEffect("rustyGearDropRate", cfg.PoisonPotionGearDrop);
            ctx.AddEffect("vesselContentsDropRate", cfg.PoisonPotionVesselContentsDrop);
            ctx.AddEffect("miningSpeedMul", cfg.PoisonPotionMiningSpeed);
            ctx.AddEffect("oreDropRate", cfg.PoisonPotionOreDrop);
            ctx.AddEffect("maxhealthExtraPoints", cfg.PoisonPotionMaxHealth);
        }

        private static void ApplyPredatorPotion(PotionContext ctx)
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;
            ctx.ResetsEffects = cfg.PredatorPotionResetsEffects;
            ctx.AddEffect("animalSeekingRange", cfg.PredatorPotionAnimalSeek);
            ctx.Duration = cfg.PredatorPotionDuration;
            ctx.AddEffect("walkspeed", cfg.PredatorPotionWalkSpeed);
            ctx.AddEffect("meleeWeaponsDamage", cfg.PredatorPotionMeleeDamage);
            ctx.AddEffect("rangedWeaponsAcc", cfg.PredatorPotionRangedAccuracy);
            ctx.AddEffect("rangedWeaponsDamage", cfg.PredatorPotionRangedDamage);
            ctx.AddEffect("rangedWeaponsSpeed", cfg.PredatorPotionRangedSpeed);
            ctx.AddEffect("healingeffectivness", cfg.PredatorPotionHealingEffectiveness);
            ctx.AddEffect("hungerrate", cfg.PredatorPotionHungerRate);
            ctx.AddEffect("animalLootDropRate", cfg.PredatorPotionAnimalLootDrop);
            ctx.AddEffect("forageDropRate", cfg.PredatorPotionForageDrop);
            ctx.AddEffect("wildCropDropRate", cfg.PredatorPotionWildCropDrop);
            ctx.AddEffect("rustyGearDropRate", cfg.PredatorPotionGearDrop);
            ctx.AddEffect("vesselContentsDropRate", cfg.PredatorPotionVesselContentsDrop);
            ctx.AddEffect("miningSpeedMul", cfg.PredatorPotionMiningSpeed);
            ctx.AddEffect("oreDropRate", cfg.PredatorPotionOreDrop);
            ctx.AddEffect("maxhealthExtraPoints", cfg.PredatorPotionMaxHealth);
            if (cfg.PredatorPotionHealth != 0f)
            {
                ctx.Health += cfg.PredatorPotionHealth;
                if (ctx.TickSec == 0 && cfg.PredatorPotionHealthTickSec > 0)
                    ctx.TickSec = cfg.PredatorPotionHealthTickSec;
            }
        }

        private static void ApplyRegenPotion(PotionContext ctx)
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;
            ctx.ResetsEffects = cfg.RegenPotionResetsEffects;
            ctx.SetHealth(cfg.RegenPotionHealth);
            ctx.TickSec = cfg.RegenPotionTickSec;
            ctx.IgnoreArmour = cfg.RegenPotionIgnoreArmour;
            ctx.Duration = cfg.RegenPotionDuration;
            ctx.AddEffect("walkspeed", cfg.RegenPotionWalkSpeed);
            ctx.AddEffect("meleeWeaponsDamage", cfg.RegenPotionMeleeDamage);
            ctx.AddEffect("rangedWeaponsAcc", cfg.RegenPotionRangedAccuracy);
            ctx.AddEffect("rangedWeaponsDamage", cfg.RegenPotionRangedDamage);
            ctx.AddEffect("rangedWeaponsSpeed", cfg.RegenPotionRangedSpeed);
            ctx.AddEffect("healingeffectivness", cfg.RegenPotionHealingEffectiveness);
            ctx.AddEffect("hungerrate", cfg.RegenPotionHungerRate);
            ctx.AddEffect("animalLootDropRate", cfg.RegenPotionAnimalLootDrop);
            ctx.AddEffect("animalSeekingRange", cfg.RegenPotionAnimalSeekingRange);
            ctx.AddEffect("forageDropRate", cfg.RegenPotionForageDrop);
            ctx.AddEffect("wildCropDropRate", cfg.RegenPotionWildCropDrop);
            ctx.AddEffect("rustyGearDropRate", cfg.RegenPotionGearDrop);
            ctx.AddEffect("vesselContentsDropRate", cfg.RegenPotionVesselContentsDrop);
            ctx.AddEffect("miningSpeedMul", cfg.RegenPotionMiningSpeed);
            ctx.AddEffect("oreDropRate", cfg.RegenPotionOreDrop);
            ctx.AddEffect("maxhealthExtraPoints", cfg.RegenPotionMaxHealth);
        }

        private static void ApplyScentMaskPotion(PotionContext ctx)
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;
            ctx.ResetsEffects = cfg.ScentMaskPotionResetsEffects;
            ctx.AddEffect("animalSeekingRange", cfg.ScentMaskPotionAnimalSeek);
            ctx.Duration = cfg.ScentMaskPotionDuration;
            ctx.AddEffect("walkspeed", cfg.ScentMaskPotionWalkSpeed);
            ctx.AddEffect("meleeWeaponsDamage", cfg.ScentMaskPotionMeleeDamage);
            ctx.AddEffect("rangedWeaponsAcc", cfg.ScentMaskPotionRangedAccuracy);
            ctx.AddEffect("rangedWeaponsDamage", cfg.ScentMaskPotionRangedDamage);
            ctx.AddEffect("rangedWeaponsSpeed", cfg.ScentMaskPotionRangedSpeed);
            ctx.AddEffect("healingeffectivness", cfg.ScentMaskPotionHealingEffectiveness);
            ctx.AddEffect("hungerrate", cfg.ScentMaskPotionHungerRate);
            ctx.AddEffect("animalLootDropRate", cfg.ScentMaskPotionAnimalLootDrop);
            ctx.AddEffect("forageDropRate", cfg.ScentMaskPotionForageDrop);
            ctx.AddEffect("wildCropDropRate", cfg.ScentMaskPotionWildCropDrop);
            ctx.AddEffect("rustyGearDropRate", cfg.ScentMaskPotionGearDrop);
            ctx.AddEffect("vesselContentsDropRate", cfg.ScentMaskPotionVesselContentsDrop);
            ctx.AddEffect("miningSpeedMul", cfg.ScentMaskPotionMiningSpeed);
            ctx.AddEffect("oreDropRate", cfg.ScentMaskPotionOreDrop);
            ctx.AddEffect("maxhealthExtraPoints", cfg.ScentMaskPotionMaxHealth);
            if (cfg.ScentMaskPotionHealth != 0f)
            {
                ctx.Health += cfg.ScentMaskPotionHealth;
                if (ctx.TickSec == 0 && cfg.ScentMaskPotionHealthTickSec > 0)
                    ctx.TickSec = cfg.ScentMaskPotionHealthTickSec;
            }
        }

        private static void ApplySpeedPotion(PotionContext ctx)
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;
            ctx.ResetsEffects = cfg.SpeedPotionResetsEffects;
            ctx.AddEffect("walkspeed", cfg.SpeedPotionValue);
            ctx.Duration = cfg.SpeedPotionDuration;
            ctx.AddEffect("meleeWeaponsDamage", cfg.SpeedPotionMeleeDamage);
            ctx.AddEffect("rangedWeaponsAcc", cfg.SpeedPotionRangedAccuracy);
            ctx.AddEffect("rangedWeaponsDamage", cfg.SpeedPotionRangedDamage);
            ctx.AddEffect("rangedWeaponsSpeed", cfg.SpeedPotionRangedSpeed);
            ctx.AddEffect("healingeffectivness", cfg.SpeedPotionHealingEffectiveness);
            ctx.AddEffect("hungerrate", cfg.SpeedPotionHungerRate);
            ctx.AddEffect("animalLootDropRate", cfg.SpeedPotionAnimalLootDrop);
            ctx.AddEffect("animalSeekingRange", cfg.SpeedPotionAnimalSeekingRange);
            ctx.AddEffect("forageDropRate", cfg.SpeedPotionForageDrop);
            ctx.AddEffect("wildCropDropRate", cfg.SpeedPotionWildCropDrop);
            ctx.AddEffect("rustyGearDropRate", cfg.SpeedPotionGearDrop);
            ctx.AddEffect("vesselContentsDropRate", cfg.SpeedPotionVesselContentsDrop);
            ctx.AddEffect("miningSpeedMul", cfg.SpeedPotionMiningSpeed);
            ctx.AddEffect("oreDropRate", cfg.SpeedPotionOreDrop);
            ctx.AddEffect("maxhealthExtraPoints", cfg.SpeedPotionMaxHealth);
            if (cfg.SpeedPotionHealth != 0f)
            {
                ctx.Health += cfg.SpeedPotionHealth;
                if (ctx.TickSec == 0 && cfg.SpeedPotionHealthTickSec > 0)
                    ctx.TickSec = cfg.SpeedPotionHealthTickSec;
            }
        }

        private static void ApplyVitalityPotion(PotionContext ctx)
        {
            AlchemyConfig cfg = AlchemyConfig.Loaded;
            ctx.ResetsEffects = cfg.VitalityPotionResetsEffects;
            ctx.AddEffect("maxhealthExtraPoints", cfg.VitalityPotionMaxHealth);
            ctx.Duration = cfg.VitalityPotionDuration;
            ctx.AddEffect("walkspeed", cfg.VitalityPotionWalkSpeed);
            ctx.AddEffect("meleeWeaponsDamage", cfg.VitalityPotionMeleeDamage);
            ctx.AddEffect("rangedWeaponsAcc", cfg.VitalityPotionRangedAccuracy);
            ctx.AddEffect("rangedWeaponsDamage", cfg.VitalityPotionRangedDamage);
            ctx.AddEffect("rangedWeaponsSpeed", cfg.VitalityPotionRangedSpeed);
            ctx.AddEffect("healingeffectivness", cfg.VitalityPotionHealingEffectiveness);
            ctx.AddEffect("hungerrate", cfg.VitalityPotionHungerRate);
            ctx.AddEffect("animalLootDropRate", cfg.VitalityPotionAnimalLootDrop);
            ctx.AddEffect("animalSeekingRange", cfg.VitalityPotionAnimalSeekingRange);
            ctx.AddEffect("forageDropRate", cfg.VitalityPotionForageDrop);
            ctx.AddEffect("wildCropDropRate", cfg.VitalityPotionWildCropDrop);
            ctx.AddEffect("rustyGearDropRate", cfg.VitalityPotionGearDrop);
            ctx.AddEffect("vesselContentsDropRate", cfg.VitalityPotionVesselContentsDrop);
            ctx.AddEffect("miningSpeedMul", cfg.VitalityPotionMiningSpeed);
            ctx.AddEffect("oreDropRate", cfg.VitalityPotionOreDrop);
            if (cfg.VitalityPotionHealth != 0f)
            {
                ctx.Health += cfg.VitalityPotionHealth;
                if (ctx.TickSec == 0 && cfg.VitalityPotionHealthTickSec > 0)
                    ctx.TickSec = cfg.VitalityPotionHealthTickSec;
            }
        }

        private static void ApplyGlowPotion(PotionContext ctx)
        {
            ctx.ResetsEffects = AlchemyConfig.Loaded.GlowPotionResetsEffects;
            ctx.Duration = AlchemyConfig.Loaded.GlowPotionDuration;
            ctx.GlowStrength = AlchemyConfig.Loaded.GlowPotionStrength;
        }

        private static void ApplyWaterBreathePotion(PotionContext ctx)
        {
            ctx.ResetsEffects = AlchemyConfig.Loaded.WaterBreathePotionResetsEffects;
            ctx.WaterBreathe = true;
            ctx.Duration = AlchemyConfig.Loaded.WaterBreathePotionDuration;
        }

        private static void ApplyColdResistPotion(PotionContext ctx)
        {
            ctx.ResetsEffects = AlchemyConfig.Loaded.ColdResistPotionResetsEffects;
            ctx.ColdResist = true;
            ctx.Duration = AlchemyConfig.Loaded.ColdResistPotionDuration;
        }

        private static void ApplyNutritionPotion(PotionContext ctx)
        {
            ctx.ResetsEffects = AlchemyConfig.Loaded.NutritionPotionResetsEffects;
            ctx.RetainedNutrition = AlchemyConfig.Loaded.NutritionPotionRetainedNutrition;
        }

        private static void ApplyRecallPotion(PotionContext ctx)
        {
            ctx.ResetsEffects = AlchemyConfig.Loaded.RecallPotionResetsEffects;
            ctx.Respawn = true;
        }

        private static void ApplyTemporalPotion(PotionContext ctx)
        {
            ctx.ResetsEffects = AlchemyConfig.Loaded.TemporalPotionResetsEffects;
            ctx.TemporalStabilityGain = AlchemyConfig.Loaded.StabilityPotionTemporalStabilityGain;
        }

        private static void ApplyReshapePotion(PotionContext ctx)
        {
            ctx.ResetsEffects = AlchemyConfig.Loaded.ReshapePotionResetsEffects;
            ctx.Reshape = true;
        }

        private static void ApplyGrowPotion(PotionContext ctx)
        {
            ctx.ResetsEffects = AlchemyConfig.Loaded.GrowPotionResetsEffects;
            ctx.SizeChange = AlchemyConfig.Loaded.GrowPotionSizeChange;
        }

        private static void ApplyShrinkPotion(PotionContext ctx)
        {
            ctx.ResetsEffects = AlchemyConfig.Loaded.ShrinkPotionResetsEffects;
            ctx.SizeChange = AlchemyConfig.Loaded.ShrinkPotionSizeChange;
        }

        private static void ApplyFallPotion(PotionContext ctx)
        {
            ctx.ResetsEffects = AlchemyConfig.Loaded.FallPotionResetsEffects;
            ctx.FallDamageReduction = AlchemyConfig.Loaded.FallPotionDamageReduction;
            ctx.Duration = AlchemyConfig.Loaded.FallPotionDuration;
        }

        private static void ApplyClimbPotion(PotionContext ctx)
        {
            ctx.ResetsEffects = AlchemyConfig.Loaded.ClimbPotionResetsEffects;
            ctx.CanClimbAnywhere = true;
            ctx.Duration = AlchemyConfig.Loaded.ClimbPotionDuration;
        }

        private static void ApplyFlightPotion(PotionContext ctx)
        {
            ctx.ResetsEffects = AlchemyConfig.Loaded.FlightPotionResetsEffects;
            ctx.CanFly = true;
            ctx.Duration = AlchemyConfig.Loaded.FlightPotionDuration;
        }
    }
}
