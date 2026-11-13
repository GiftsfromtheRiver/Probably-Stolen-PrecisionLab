using System;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Some container inventories attach hidden state-validation delegates that reject items with a
    /// content window. The first aid box/shell are normal medical containers when owned, so let the
    /// state check pass; ownership and grid-shape checks still run before this point.
    /// </summary>
    [HarmonyPatch(typeof(GameItem), "CheckIsStateValid")]
    internal static class FirstAidStateValidPatch
    {
        private static bool Prefix(GameItem __instance, ref bool __result)
        {
            if (__instance == null)
            {
                return true;
            }
            if (!FirstAidBox.IsBox(__instance) && !FirstAidBox.IsShell(__instance))
            {
                return true;
            }
            __result = true;
            return false;
        }
    }
}
