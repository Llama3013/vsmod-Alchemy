using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Alchemy
{
    internal static class UtilityEffects
    {
        public static void ApplyNutritionPotion(EntityAgent byEntity, float retainedNutrition)
        {
            ITreeAttribute hungerTree = byEntity.WatchedAttributes.GetTreeAttribute("hunger");
            if (hungerTree != null)
            {
                float maxSaturation = hungerTree.GetFloat("maxsaturation");
                float totalSatiety =
                    (
                        hungerTree.GetFloat("fruitLevel")
                        + hungerTree.GetFloat("vegetableLevel")
                        + hungerTree.GetFloat("grainLevel")
                        + hungerTree.GetFloat("proteinLevel")
                        + hungerTree.GetFloat("dairyLevel")
                    ) * retainedNutrition;

                hungerTree.SetFloat(
                    "fruitLevel",
                    Math.Min(Math.Max(totalSatiety / 5, 0), maxSaturation)
                );
                hungerTree.SetFloat(
                    "vegetableLevel",
                    Math.Min(Math.Max(totalSatiety / 5, 0), maxSaturation)
                );
                hungerTree.SetFloat(
                    "grainLevel",
                    Math.Min(Math.Max(totalSatiety / 5, 0), maxSaturation)
                );
                hungerTree.SetFloat(
                    "proteinLevel",
                    Math.Min(Math.Max(totalSatiety / 5, 0), maxSaturation)
                );
                hungerTree.SetFloat(
                    "dairyLevel",
                    Math.Min(Math.Max(totalSatiety / 5, 0), maxSaturation)
                );
                byEntity.WatchedAttributes.MarkPathDirty("hunger");
            }
        }

        public static void ApplyRecallPotion(
            IServerPlayer serverPlayer,
            EntityAgent byEntity,
            ICoreAPI api
        )
        {
            if (api.Side.IsServer())
            {
                FuzzyEntityPos spawn = serverPlayer.GetSpawnPosition(false);
                byEntity.TeleportTo(spawn);
            }
        }

        public static void ApplyTemporalPotion(EntityAgent byEntity, float stabilityGain)
        {
            byEntity.GetBehavior<EntityBehaviorTemporalStabilityAffected>().OwnStability +=
                stabilityGain;
        }

        public static void ApplyReshapePotion(IServerPlayer serverPlayer)
        {
            serverPlayer.Entity.WatchedAttributes.SetBool("allowcharselonce", true);
        }
    }
}
