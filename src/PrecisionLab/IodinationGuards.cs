using System;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>Vanilla lipid sedimentation/boiling can eat the lipobond agent while the
    /// iodination mix is present, before our M2 step has a chance to use it. Skip those
    /// vanilla handlers for containers holding iodination mix.</summary>
    [HarmonyPatch(typeof(ChemicalHelper), "HandleLipobondAgentSedimentation")]
    internal static class IodinationLipidGuard
    {
        private static bool Prefix(GameItem container, ref int __result)
        {
            try
            {
                if (container != null && LiquidTools.GetLiquidAmount(container, CustomLiquids.IodinationMix) > 0)
                {
                    __result = 0;
                    return false;
                }
            }
            catch (Exception) { }
            return true;
        }
    }

    [HarmonyPatch(typeof(ChemicalHelper), "HandleBoilingReaction")]
    internal static class IodinationBoilingGuard
    {
        private static bool Prefix(GameItem container, ref int __result)
        {
            try
            {
                if (container != null && LiquidTools.GetLiquidAmount(container, CustomLiquids.IodinationMix) > 0)
                {
                    __result = 0;
                    return false;
                }
            }
            catch (Exception) { }
            return true;
        }
    }
}