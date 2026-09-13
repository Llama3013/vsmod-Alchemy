using System;
using HarmonyLib;
using Vintagestory.API.Common;

#pragma warning disable IDE0130
namespace EffectLib
#pragma warning restore IDE0130
{
    [HarmonyPatch(typeof(EntityPlayer), "LightHsv", MethodType.Getter)]
    internal static class GlowPatch
    {
        // Light "value" is a 0-31 index (5-bit), not a 0-255 byte - anything higher overflows the
        // engine's light-level lookup and crashes the renderer.
        public const int MaxGlowStrength = 31;

        public static void Postfix(EntityPlayer __instance, ref byte[] __result)
        {
            int glowStrength = __instance.WatchedAttributes.GetInt(EffectAttr.GlowStrength);
            if (glowStrength <= 0)
                return;

            __result = [0, 0, (byte)Math.Clamp(glowStrength, 0, MaxGlowStrength)];
        }
    }
}
