using HarmonyLib;
using Vintagestory.API.Common;

namespace Alchemy
{
    //This harmony patch allows the glow potion to work
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
