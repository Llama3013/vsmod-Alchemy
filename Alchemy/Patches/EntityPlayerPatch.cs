using HarmonyLib;
using Vintagestory.API.Common;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    //This harmony patch allows the glow potion to work
    [HarmonyPatch(typeof(EntityPlayer), "LightHsv", MethodType.Getter)]
    public static class EntityPlayerPatch
    {
        public static void Postfix(EntityPlayer __instance, ref byte[] __result)
        {
            if (__instance.WatchedAttributes.GetLong("glowpotionid") == 0)
            {
                return;
            }
            int glowStrength = __instance.WatchedAttributes.GetInt(
                "glowStrength",
                AlchemyConfig.Loaded.GlowPotionStrength
            );
            __result = [0, 0, (byte)glowStrength];
        }
    }
}
