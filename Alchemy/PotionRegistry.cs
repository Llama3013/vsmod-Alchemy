using System.Collections.Generic;
using Alchemy.ModConfig;

namespace Alchemy
{
    public delegate void PotionApply(PotionContext ctx);

    public static class PotionRegistry
    {
        private static Dictionary<string, PotionApply> apply;
        public static Dictionary<string, PotionApply> Apply
        {
            get
            {
                if (apply == null)
                {
                    Init();
                }
                return apply;
            }
        }

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

        private static void Init()
        {
            apply = new()
            {
                ["archerpotionid"] = ctx =>
                {
                    ctx.AddEffect("rangedWeaponsAcc", AlchemyConfig.Loaded.ArcherPotionAcc);
                    ctx.AddEffect("rangedWeaponsDamage", AlchemyConfig.Loaded.ArcherPotionDamage);
                    ctx.AddEffect("rangedWeaponsSpeed", AlchemyConfig.Loaded.ArcherPotionSpeed);
                    ctx.Duration = AlchemyConfig.Loaded.ArcherPotionDuration;
                },
                ["healingeffectpotionid"] = ctx =>
                {
                    ctx.AddEffect(
                        "healingeffectivness",
                        AlchemyConfig.Loaded.HealingEffectPotionValue
                    );
                    ctx.Duration = AlchemyConfig.Loaded.HealingEffectPotionDuration;
                },
                ["hungerenhancepotionid"] = ctx =>
                {
                    ctx.AddEffect("hungerrate", AlchemyConfig.Loaded.HungerEnhancePotionValue);
                    ctx.Duration = AlchemyConfig.Loaded.HungerEnhancePotionDuration;
                },
                ["hungersupresspotionid"] = ctx =>
                {
                    ctx.AddEffect("hungerrate", AlchemyConfig.Loaded.HungerSupressPotionValue);
                    ctx.Duration = AlchemyConfig.Loaded.HungerSupressPotionDuration;
                },
                ["hunterpotionid"] = ctx =>
                {
                    ctx.AddEffect(
                        "animalLootDropRate",
                        AlchemyConfig.Loaded.HunterPotionAnimalDrop
                    );
                    ctx.AddEffect(
                        "animalSeekingRange",
                        AlchemyConfig.Loaded.HunterPotionAnimalSeek
                    );
                    ctx.AddEffect("forageDropRate", AlchemyConfig.Loaded.HunterPotionForageDrop);
                    ctx.AddEffect("wildCropDropRate", AlchemyConfig.Loaded.HunterPotionWildDrop);
                    ctx.Duration = AlchemyConfig.Loaded.HunterPotionDuration;
                },
                ["looterpotionid"] = ctx =>
                {
                    ctx.AddEffect("forageDropRate", AlchemyConfig.Loaded.LooterPotionForageDrop);
                    ctx.AddEffect("rustyGearDropRate", AlchemyConfig.Loaded.LooterPotionGearDrop);
                    ctx.AddEffect(
                        "vesselContentsDropRate",
                        AlchemyConfig.Loaded.LooterPotionVesselContentDrop
                    );
                    ctx.AddEffect("wildCropDropRate", AlchemyConfig.Loaded.LooterPotionWildDrop);
                    ctx.Duration = AlchemyConfig.Loaded.LooterPotionDuration;
                },
                ["meleepotionid"] = ctx =>
                {
                    ctx.AddEffect("meleeWeaponsDamage", AlchemyConfig.Loaded.MeleePotionDamage);
                    ctx.Duration = AlchemyConfig.Loaded.MeleePotionDuration;
                },
                ["miningpotionid"] = ctx =>
                {
                    ctx.AddEffect("miningSpeedMul", AlchemyConfig.Loaded.MiningPotionSpeed);
                    ctx.AddEffect("oreDropRate", AlchemyConfig.Loaded.MiningPotionOreDrop);
                    ctx.Duration = AlchemyConfig.Loaded.MiningPotionDuration;
                },
                ["poisontickpotionid"] = ctx =>
                {
                    ctx.SetHealth(AlchemyConfig.Loaded.PoisonPotionHealth);
                    ctx.TickSec = AlchemyConfig.Loaded.PoisonPotionTickSec;
                    ctx.IgnoreArmour = AlchemyConfig.Loaded.PoisonPotionIgnoreArmour;
                    ctx.Duration = AlchemyConfig.Loaded.PoisonPotionDuration;
                },
                ["predatorpotionid"] = ctx =>
                {
                    ctx.AddEffect(
                        "animalSeekingRange",
                        AlchemyConfig.Loaded.PredatorPotionAnimalSeek
                    );
                    ctx.Duration = AlchemyConfig.Loaded.HunterPotionDuration;
                },
                ["regentickpotionid"] = ctx =>
                {
                    ctx.SetHealth(AlchemyConfig.Loaded.RegenPotionHealth);
                    ctx.TickSec = AlchemyConfig.Loaded.RegenPotionTickSec;
                    ctx.IgnoreArmour = AlchemyConfig.Loaded.RegenPotionIgnoreArmour;
                    ctx.Duration = AlchemyConfig.Loaded.RegenPotionDuration;
                },
                ["scentmaskpotionid"] = ctx =>
                {
                    ctx.AddEffect(
                        "animalSeekingRange",
                        AlchemyConfig.Loaded.ScentMaskPotionAnimalSeek
                    );
                    ctx.Duration = AlchemyConfig.Loaded.ScentMaskPotionDuration;
                },
                ["speedpotionid"] = ctx =>
                {
                    ctx.AddEffect("walkspeed", AlchemyConfig.Loaded.SpeedPotionValue);
                    ctx.Duration = AlchemyConfig.Loaded.SpeedPotionDuration;
                },
                ["vitalitypotionid"] = ctx =>
                {
                    ctx.AddEffect(
                        "maxhealthExtraPoints",
                        AlchemyConfig.Loaded.VitalityPotionMaxHealth
                    );
                    ctx.Duration = AlchemyConfig.Loaded.VitalityPotionDuration;
                },
                ["glowpotionid"] = ctx =>
                {
                    ctx.Duration = AlchemyConfig.Loaded.GlowPotionDuration;
                },
                ["waterbreathepotionid"] = ctx =>
                {
                    ctx.Duration = AlchemyConfig.Loaded.WaterBreathePotionDuration;
                }
            };
        }
    }
}
