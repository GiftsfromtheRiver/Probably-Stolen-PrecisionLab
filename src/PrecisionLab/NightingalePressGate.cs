using System;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    [HarmonyPatch(typeof(MachineChemFinisher), "CanOutput")]
    internal static class NightingalePressGate
    {
        private static bool Prefix(GameItem machine, GameSlotInventory containerSlot, ref bool __result)
        {
            try
            {
                if (machine == null || containerSlot == null || containerSlot.childItem == null)
                {
                    return true;
                }
                if (!machine.IsTag("SELECTED_CHEM_ID_TAG")
                    || machine.GetTagReadonly("SELECTED_CHEM_ID_TAG").GetString() != NightingaleContent.FinalId)
                {
                    return true;
                }
                GameItem container = containerSlot.childItem;
                int purified = Amount(container, CustomLiquids.PurifiedOintmentBase);
                int alkali = Amount(container, "base");
                int total = WaterHelper.GetTotalVolume(container);
                if (purified >= 5000 && alkali >= 100 && total >= 5000)
                {
                    return true;
                }
                __result = false;
                try
                {
                    StoreUIManager.Instance.Notify(
                        Loc.Get("ui.nightingale.need", "南丁格尔软膏需要精制软膏基质 ≥5000 微升 + 微量碱，且总液量足够。"),
                        "default");
                }
                catch (Exception) { }
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] nightingale press gate failed: " + e.Message);
                return true;
            }
        }

        private static int Amount(GameItem c, string liquid)
        {
            return LiquidTools.GetLiquidAmount(c, liquid);
        }
    }
}
