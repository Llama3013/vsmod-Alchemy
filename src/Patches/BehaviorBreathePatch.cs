using HarmonyLib;
using Vintagestory.GameContent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Alchemy
{
    //This harmony patch allows the water breathing potion to work
    [HarmonyPatch(typeof (EntityBehaviorBreathe), "Oxygen", MethodType.Getter)]
    public class BehaviorBreathePatch
    {
        public static void Postfix(EntityBehaviorBreathe __instance, ref float __result)
        {
            if (__instance.entity.WatchedAttributes.GetLong("waterbreathepotionid") == 0)
            {
                return;
            }
            ITreeAttribute oxygenTree = __instance.entity.WatchedAttributes.GetTreeAttribute("oxygen");
            float currOxygen = oxygenTree.GetFloat("maxoxygen");
            __result = currOxygen;
        }
    }
}
