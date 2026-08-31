using HarmonyLib;
using Vintagestory.API.Common;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace EffectLib
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    // Makes EffectContext.GlowStrength (EffectAttr.GlowStrength) actually make the player glow.
    [HarmonyPatch(typeof(EntityPlayer), "LightHsv", MethodType.Getter)]
    internal static class GlowPatch
    {
        public static void Postfix(EntityPlayer __instance, ref byte[] __result)
        {
            int glowStrength = __instance.WatchedAttributes.GetInt(EffectAttr.GlowStrength);
            if (glowStrength <= 0)
                return;

            __result = [0, 0, (byte)glowStrength];
        }
    }
}
