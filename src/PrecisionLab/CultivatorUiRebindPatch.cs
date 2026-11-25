using System;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>Rebind the cultivator strain button whenever its live content window is opened.</summary>
    [HarmonyPatch(typeof(OverlayHandler), "ShowInventory")]
    internal static class CultivatorShowInventoryRebindPatch
    {
        private static void Postfix(GameItem item)
        {
            Debug.Log("[PrecisionLab] ShowInventory path id=" + (item?.identifier ?? "null")); try { CultivatorStrainUI.RebindMachine(item); } catch (Exception) { }
        }
    }

    [HarmonyPatch(typeof(ItemMouseDoubleClickHandler), "OpenContentAction")]
    internal static class CultivatorDoubleClickRebindPatch
    {
        private static void Postfix(GameItem newItem)
        {
            Debug.Log("[PrecisionLab] OpenContentAction path id=" + (newItem?.identifier ?? "null")); try { CultivatorStrainUI.RebindMachine(newItem); } catch (Exception) { }
        }
    }
}
