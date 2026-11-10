using System;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// The sealed reagent crate must stay locked while it is store stock. PlayerStore.BuyItem calls
    /// OnItemBought after the money is paid; unlock the content window there so double-clicking the
    /// purchased crate reveals its contents, while the unowned shop item cannot be opened.
    /// </summary>
    [HarmonyPatch(typeof(PlayerStore), "OnItemBought")]
    internal static class MysteryCrateUnlockPatch
    {
        private static void Postfix(GameItem gameItem, int cost)
        {
            try
            {
                if (gameItem != null
                    && gameItem.identifier == ItemIds.MysteryMedBox
                    && gameItem.contentWindow != null)
                {
                    gameItem.contentWindow.isLocked = false;
                    Debug.Log("[PrecisionLab] purchased reagent crate unlocked");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] unlocking purchased reagent crate failed: " + e.Message);
            }
        }
    }
}
