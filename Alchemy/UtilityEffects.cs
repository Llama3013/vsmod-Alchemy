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
        public static void ApplyNutritionPotion(EntityAgent byEntity)
        {
            ITreeAttribute hungerTree = byEntity.WatchedAttributes.GetTreeAttribute("hunger");
            if (hungerTree != null)
            {
                float totalSatiety =
                    (
                        hungerTree.GetFloat("fruitLevel")
                        + hungerTree.GetFloat("vegetableLevel")
                        + hungerTree.GetFloat("grainLevel")
                        + hungerTree.GetFloat("proteinLevel")
                        + hungerTree.GetFloat("dairyLevel")
                    ) * 0.9f;

                hungerTree.SetFloat("fruitLevel", Math.Max(totalSatiety / 5, 0));
                hungerTree.SetFloat("vegetableLevel", Math.Max(totalSatiety / 5, 0));
                hungerTree.SetFloat("grainLevel", Math.Max(totalSatiety / 5, 0));
                hungerTree.SetFloat("proteinLevel", Math.Max(totalSatiety / 5, 0));
                hungerTree.SetFloat("dairyLevel", Math.Max(totalSatiety / 5, 0));
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

        public static void ApplyTemporalPotion(EntityAgent byEntity)
        {
            byEntity.GetBehavior<EntityBehaviorTemporalStabilityAffected>().OwnStability += 0.2;
        }

        public static void ApplyReshapePotion(IServerPlayer serverPlayer)
        {
            serverPlayer.Entity.WatchedAttributes.SetBool("allowcharselonce", true);
        }
    }
}
