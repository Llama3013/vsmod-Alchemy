using System;
using HarmonyLib;
using Vintagestory.API.Common;

namespace Alchemy
{

    [HarmonyPatch(typeof(EntityPlayer), "LightHsv", MethodType.Getter)]
    public class EntityPlayerPatch
    {
        public static void Postfix(EntityPlayer __instance, ref byte[] __result)
        {
            if (__instance.WatchedAttributes.GetLong("glowpotionid") == 0)
            {
                return;
            }
            byte[] glow = new byte[] { (byte)0, (byte)0, (byte)31 };
            __result = glow;
        }
    }
}