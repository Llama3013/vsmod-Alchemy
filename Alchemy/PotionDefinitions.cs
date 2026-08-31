using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Common;

namespace Alchemy
{
    public sealed record PotionDefinition(string Id, string Name, bool Utility);

    public static class PotionDefinitions
    {
        public static readonly PotionDefinition[] All =
        [
            new("archerpotionid", "Archer", false),
            new("healingeffectpotionid", "HealingEffect", false),
            new("hungerenhancepotionid", "HungerEnhance", false),
            new("hungersupresspotionid", "HungerSupress", false),
            new("hunterpotionid", "Hunter", false),
            new("looterpotionid", "Looter", false),
            new("meleepotionid", "Melee", false),
            new("miningpotionid", "Mining", false),
            new("poisontickpotionid", "Poison", false),
            new("predatorpotionid", "Predator", false),
            new("regentickpotionid", "Regen", false),
            new("scentmaskpotionid", "ScentMask", false),
            new("speedpotionid", "Speed", false),
            new("vitalitypotionid", "Vitality", false),
            new("recallpotionid", "Recall", true),
            new("glowpotionid", "Glow", true),
            new("waterbreathepotionid", "WaterBreathe", true),
            new("coldresistpotionid", "ColdResist", true),
            new("nutritionpotionid", "Nutrition", true),
            new("temporalpotionid", "Temporal", true),
            new("reshapepotionid", "Reshape", true),
            new("growpotionid", "Grow", true),
            new("shrinkpotionid", "Shrink", true),
            new("fallpotionid", "Fall", true),
            new("climbpotionid", "Climb", true),
            new("flightpotionid", "Flight", true),
        ];

        private static readonly Dictionary<string, PotionDefinition> byId = All.ToDictionary(
            def => def.Id,
            StringComparer.OrdinalIgnoreCase
        );

        private static readonly Dictionary<string, PropertyInfo> props = BuildPropertyCache();

        public static PotionDefinition Get(string potionId) =>
            potionId != null && byId.TryGetValue(potionId, out PotionDefinition def) ? def : null;

        public static bool IsEnabled(AlchemyConfig cfg, PotionDefinition def) =>
            Read(cfg, "Allow" + def.Name + "Potion", true);

        // Each delivery method is configured per potion and independently of the others:
        // Allow{Drinking,Throwing,Coating}<Name>. Drinking defaults on, the rest default off.
        public static bool AllowsDrinking(string potionId)
        {
            PotionDefinition def = Get(potionId);
            return def == null || Read(AlchemyConfig.Loaded, "AllowDrinking" + def.Name, true);
        }

        public static bool AllowsThrowing(string potionId)
        {
            PotionDefinition def = Get(potionId);
            return def != null && Read(AlchemyConfig.Loaded, "AllowThrowing" + def.Name, false);
        }

        public static bool AllowsCoating(string potionId)
        {
            PotionDefinition def = Get(potionId);
            return def != null && Read(AlchemyConfig.Loaded, "AllowCoating" + def.Name, false);
        }

        public static string GroupOf(string potionId)
        {
            PotionDefinition def = Get(potionId);
            return def == null
                ? "none"
                : Read(AlchemyConfig.Loaded, def.Name + "PotionGroup", "none");
        }

        public static (float damage, float intox, float psych, float satLoss) DrinkingSideEffects(
            string potionId
        )
        {
            PotionDefinition def = Get(potionId);
            if (def == null)
                return (0f, 0f, 0f, 0f);

            AlchemyConfig cfg = AlchemyConfig.Loaded;
            string prefix = def.Name + "PotionDrinking";
            return (
                Read(cfg, prefix + "Damage", 0f),
                Read(cfg, prefix + "Intoxication", 0f),
                Read(cfg, prefix + "Psychedelic", 0f),
                Read(cfg, prefix + "SaturationLoss", 0f)
            );
        }

        public static void Validate(ILogger logger)
        {
            if (logger == null)
                return;

            foreach (KeyValuePair<string, PropertyInfo> entry in props.Where(p => p.Value == null))
            {
                logger.Error(
                    "[Alchemy] Config property {0} does not exist. The potion setting it backs will fall back to its default.",
                    entry.Key
                );
            }
        }

        private static Dictionary<string, PropertyInfo> BuildPropertyCache()
        {
            Dictionary<string, PropertyInfo> cache = [];

            foreach (PotionDefinition def in All)
            {
                foreach (string name in PropertyNamesFor(def))
                {
                    cache[name] = typeof(AlchemyConfig).GetProperty(
                        name,
                        BindingFlags.Public | BindingFlags.Instance
                    );
                }
            }

            return cache;
        }

        private static IEnumerable<string> PropertyNamesFor(PotionDefinition def)
        {
            yield return "Allow" + def.Name + "Potion";
            yield return "AllowDrinking" + def.Name;
            yield return "AllowThrowing" + def.Name;
            yield return "AllowCoating" + def.Name;
            yield return def.Name + "PotionGroup";
            yield return def.Name + "PotionDrinkingDamage";
            yield return def.Name + "PotionDrinkingIntoxication";
            yield return def.Name + "PotionDrinkingPsychedelic";
            yield return def.Name + "PotionDrinkingSaturationLoss";
        }

        private static T Read<T>(AlchemyConfig cfg, string propertyName, T fallback)
        {
            return props.TryGetValue(propertyName, out PropertyInfo prop)
                && prop?.GetValue(cfg) is T value
                ? value
                : fallback;
        }
    }
}
