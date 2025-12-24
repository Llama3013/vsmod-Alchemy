using System.Collections.Generic;
using Alchemy.ModConfig;
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
                ["nutritionpotionid"] = ApplyNutritionPotion,
                ["recallpotionid"] = ApplyRecallPotion,
                ["temporalpotionid"] = ApplyTemporalPotion,
                ["reshapepotionid"] = ApplyReshapePotion
            };
        }

        private static void ApplyArcherPotion(PotionContext ctx)
        {
            ctx.AddEffect("rangedWeaponsAcc", AlchemyConfig.Loaded.ArcherPotionAcc);
            ctx.AddEffect("rangedWeaponsDamage", AlchemyConfig.Loaded.ArcherPotionDamage);
            ctx.AddEffect("rangedWeaponsSpeed", AlchemyConfig.Loaded.ArcherPotionSpeed);
            ctx.Duration = AlchemyConfig.Loaded.ArcherPotionDuration;
        }

        private static void ApplyHealingEffectPotion(PotionContext ctx)
        {
            ctx.AddEffect("healingeffectivness", AlchemyConfig.Loaded.HealingEffectPotionValue);
            ctx.Duration = AlchemyConfig.Loaded.HealingEffectPotionDuration;
        }

        private static void ApplyHungerEnhancePotion(PotionContext ctx)
        {
            ctx.AddEffect("hungerrate", AlchemyConfig.Loaded.HungerEnhancePotionValue);
            ctx.Duration = AlchemyConfig.Loaded.HungerEnhancePotionDuration;
        }

        private static void ApplyHungerSupressPotion(PotionContext ctx)
        {
            ctx.AddEffect("hungerrate", AlchemyConfig.Loaded.HungerSupressPotionValue);
            ctx.Duration = AlchemyConfig.Loaded.HungerSupressPotionDuration;
        }

        private static void ApplyHunterPotion(PotionContext ctx)
        {
            ctx.AddEffect("animalLootDropRate", AlchemyConfig.Loaded.HunterPotionAnimalDrop);
            ctx.AddEffect("animalSeekingRange", AlchemyConfig.Loaded.HunterPotionAnimalSeek);
            ctx.AddEffect("forageDropRate", AlchemyConfig.Loaded.HunterPotionForageDrop);
            ctx.AddEffect("wildCropDropRate", AlchemyConfig.Loaded.HunterPotionWildDrop);
            ctx.Duration = AlchemyConfig.Loaded.HunterPotionDuration;
        }

        private static void ApplyLooterPotion(PotionContext ctx)
        {
            ctx.AddEffect("forageDropRate", AlchemyConfig.Loaded.LooterPotionForageDrop);
            ctx.AddEffect("rustyGearDropRate", AlchemyConfig.Loaded.LooterPotionGearDrop);
            ctx.AddEffect(
                "vesselContentsDropRate",
                AlchemyConfig.Loaded.LooterPotionVesselContentDrop
            );
            ctx.AddEffect("wildCropDropRate", AlchemyConfig.Loaded.LooterPotionWildDrop);
            ctx.Duration = AlchemyConfig.Loaded.LooterPotionDuration;
        }

        private static void ApplyMeleePotion(PotionContext ctx)
        {
            ctx.AddEffect("meleeWeaponsDamage", AlchemyConfig.Loaded.MeleePotionDamage);
            ctx.Duration = AlchemyConfig.Loaded.MeleePotionDuration;
        }

        private static void ApplyMiningPotion(PotionContext ctx)
        {
            ctx.AddEffect("miningSpeedMul", AlchemyConfig.Loaded.MiningPotionSpeed);
            ctx.AddEffect("oreDropRate", AlchemyConfig.Loaded.MiningPotionOreDrop);
            ctx.Duration = AlchemyConfig.Loaded.MiningPotionDuration;
        }

        private static void ApplyPoisonPotion(PotionContext ctx)
        {
            ctx.SetHealth(AlchemyConfig.Loaded.PoisonPotionHealth);
            ctx.TickSec = AlchemyConfig.Loaded.PoisonPotionTickSec;
            ctx.IgnoreArmour = AlchemyConfig.Loaded.PoisonPotionIgnoreArmour;
            ctx.Duration = AlchemyConfig.Loaded.PoisonPotionDuration;
        }

        private static void ApplyPredatorPotion(PotionContext ctx)
        {
            ctx.AddEffect("animalSeekingRange", AlchemyConfig.Loaded.PredatorPotionAnimalSeek);
            ctx.Duration = AlchemyConfig.Loaded.HunterPotionDuration;
        }

        private static void ApplyRegenPotion(PotionContext ctx)
        {
            ctx.SetHealth(AlchemyConfig.Loaded.RegenPotionHealth);
            ctx.TickSec = AlchemyConfig.Loaded.RegenPotionTickSec;
            ctx.IgnoreArmour = AlchemyConfig.Loaded.RegenPotionIgnoreArmour;
            ctx.Duration = AlchemyConfig.Loaded.RegenPotionDuration;
        }

        private static void ApplyScentMaskPotion(PotionContext ctx)
        {
            ctx.AddEffect("animalSeekingRange", AlchemyConfig.Loaded.ScentMaskPotionAnimalSeek);
            ctx.Duration = AlchemyConfig.Loaded.ScentMaskPotionDuration;
        }

        private static void ApplySpeedPotion(PotionContext ctx)
        {
            ctx.AddEffect("walkspeed", AlchemyConfig.Loaded.SpeedPotionValue);
            ctx.Duration = AlchemyConfig.Loaded.SpeedPotionDuration;
        }

        private static void ApplyVitalityPotion(PotionContext ctx)
        {
            ctx.AddEffect("maxhealthExtraPoints", AlchemyConfig.Loaded.VitalityPotionMaxHealth);
            ctx.Duration = AlchemyConfig.Loaded.VitalityPotionDuration;
        }

        private static void ApplyGlowPotion(PotionContext ctx)
        {
            ctx.Duration = AlchemyConfig.Loaded.GlowPotionDuration;
            ctx.GlowStrength = AlchemyConfig.Loaded.GlowPotionStrength;
        }

        private static void ApplyWaterBreathePotion(PotionContext ctx)
        {
            ctx.Duration = AlchemyConfig.Loaded.WaterBreathePotionDuration;
        }

        private static void ApplyNutritionPotion(PotionContext ctx)
        {
            ctx.RetainedNutrition = AlchemyConfig.Loaded.NutritionPotionRetainedNutrition;
        }

        private static void ApplyRecallPotion(PotionContext ctx)
        {
            ctx.Respawn = true;
        }

        private static void ApplyTemporalPotion(PotionContext ctx)
        {
            ctx.TemporalStabilityGain = AlchemyConfig.Loaded.StabilityPotionTemporalStabilityGain;
        }

        private static void ApplyReshapePotion(PotionContext ctx)
        {
            ctx.Reshape = true;
        }
    }
}
