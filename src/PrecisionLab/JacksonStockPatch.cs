using System;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// After the player unlocks a Precision Lab machine through the Wild Network, Dr. Jackson
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
            JacksonStock.AddMachinesToJackson(1);
        }
    }

    [HarmonyPatch(typeof(StoreClientList), "PlaceInventorSpecialStock")]
    internal static class JacksonSpecialStockPatch
    {
        private static void Postfix()
        {
            JacksonStock.AddMachinesToJackson(2);
        }
    }

    internal static class JacksonStock
    {
        private static readonly string[,] Machines =
        {
            { ItemIds.Purifier, "PRECISION_LAB_PURIFIER", "purification machine" },
            { ItemIds.Cultivator, "PRECISION_LAB_CULTIVATOR", "microbial cultivator" }
        };

        internal static void AddMachinesToJackson(int count)
        {
            try
            {
                if (count <= 0)
                {
                    return;
                }
                PlayerStore store = PlayerStore.Instance;
                if (store == null)
                {
                    return;
                }
                for (int m = 0; m < Machines.GetLength(0); m++)
                {
                    string itemId = Machines[m, 0];
                    string upgradeId = Machines[m, 1];
                    string label = Machines[m, 2];
                    if (!IsUnlocked(upgradeId))
                    {
                        continue;
                    }
                    int added = 0;
                    for (int i = 0; i < count; i++)
                    {
                        GameItem machine = ItemSpawner.Spawn(itemId);
                        if (machine == null)
                        {
                            break;
                        }
                        store.AddDirectSellingItemToTable(machine);
                        added++;
                    }
                    if (added > 0)
                    {
                        Debug.Log("[PrecisionLab] Dr. Jackson stocks " + added + " " + label + "(s)");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] adding machines to Dr. Jackson's stock failed: " + e);
            }
        }

        private static bool IsUnlocked(string upgradeId)
        {
            PlayerStore store = PlayerStore.Instance;
            if (store == null || store.networkUpgrade == null)
            {
                return false;
            }
            NetworkUpgrade upgrade;
            return store.networkUpgrade.TryGetValue(upgradeId, out upgrade)
                && upgrade != null
                && upgrade.IsUnlocked();
        }
    }
}
