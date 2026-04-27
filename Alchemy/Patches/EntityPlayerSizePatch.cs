using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace Alchemy.Patches
{
    // Keeps the player's collision box, eye height, and visual scale in sync with potionSizeDelta.
    // Runs on both server and client via WatchedAttributes sync.
    [HarmonyPatch(typeof(Entity), "Initialize")]
    public static class EntityPlayerSizePatch
    {
        public static void Postfix(Entity __instance)
        {
            if (__instance is not EntityPlayer player)
                return;

            ApplySize(player);
            player.WatchedAttributes.RegisterModifiedListener("potionSizeDelta", () => ApplySize(player));
        }

        internal static void ApplySize(EntityPlayer entity)
        {
            float baseHeight = entity.WatchedAttributes.GetFloat("potionBaseHeight", 0f);
            if (baseHeight < 0.1f)
                return;

            float delta = entity.WatchedAttributes.GetFloat("potionSizeDelta", 0f);
            float baseEyeHeight = entity.WatchedAttributes.GetFloat("potionBaseEyeHeight", baseHeight * 0.9054f);
            float newHeight = baseHeight + delta;
            float scale = newHeight / baseHeight;

            entity.CollisionBox.Y2 = newHeight;
            entity.SelectionBox.Y2 = newHeight;
            entity.Properties.EyeHeight = baseEyeHeight * scale;

            if (entity.Properties.Client != null)
            {
                float baseClientSize = entity.WatchedAttributes.GetFloat("potionBaseClientSize", 0f);
                entity.Properties.Client.Size = baseClientSize > 0.01f ? baseClientSize * scale : scale;
            }
        }
    }
}
