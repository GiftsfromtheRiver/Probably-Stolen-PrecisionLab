using System;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// After the player unlocks the Precision Lab purifier through the Wild Network, Dr. Jackson
    /// also stocks it on his counter: one machine on a regular visit, two during his
    /// "special stock" night. The Wild Network unlock still delivers the first machine for free.
    ///
    /// Implemented as two postfix patches because the vanilla stock lists are hard-coded in
    /// StoreClientList.PlaceInventorInventory / PlaceInventorSpecialStock.
    /// </summary>
    [HarmonyPatch(typeof(StoreClientList), "PlaceInventorInventory")]
    internal static class JacksonStockPatch
    {
        private static void Postfix()
        {
            JacksonStock.AddPurifierToJackson(1);
        }
    }

    [HarmonyPatch(typeof(StoreClientList), "PlaceInventorSpecialStock")]
    internal static class JacksonSpecialStockPatch
    {
        private static void Postfix()
        {
            JacksonStock.AddPurifierToJackson(2);
        }
    }

    internal static class JacksonStock
    {
        private const string PurifierUpgradeId = "PRECISION_LAB_PURIFIER";

        internal static void AddPurifierToJackson(int count)
        {
            try
            {
                if (count <= 0 || !IsPurifierUnlocked())
                {
                    return;
                }
                PlayerStore store = PlayerStore.Instance;
                if (store == null)
                {
                    return;
                }
                int added = 0;
                for (int i = 0; i < count; i++)
                {
                    GameItem machine = ItemSpawner.Spawn(ItemIds.Purifier);
                    if (machine == null)
                    {
                        break;
                    }
                    store.AddDirectSellingItemToTable(machine);
                    added++;
                }
                if (added > 0)
                {
                    Debug.Log("[PrecisionLab] Dr. Jackson stocks " + added + " purification machine(s)");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] adding the purifier to Dr. Jackson's stock failed: " + e);
            }
        }

        private static bool IsPurifierUnlocked()
        {
            PlayerStore store = PlayerStore.Instance;
            if (store == null || store.networkUpgrade == null)
            {
                return false;
            }
            NetworkUpgrade upgrade;
            return store.networkUpgrade.TryGetValue(PurifierUpgradeId, out upgrade)
                && upgrade != null
                && upgrade.IsUnlocked();
        }
    }
}
