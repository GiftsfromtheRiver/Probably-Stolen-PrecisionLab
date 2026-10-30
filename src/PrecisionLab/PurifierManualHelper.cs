using System;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Ensures every spawned Precision Purifier carries its instruction note in the note panel.
    ///
    /// The note must be inserted AFTER DirectoryMaster/ItemDirectory has finished creating the
    /// item: DirectoryMaster.Item explicitly destroys any child items found on a freshly created
    /// item ("this should not happen ... Removing all child items as a fix"), so inserting from
    /// inside the item factory is defeated. We hook DirectoryMaster.Item with a Harmony postfix
    /// and also re-check machines that were loaded from a save or spawned by another path.
    /// </summary>
    internal static class PurifierManualHelper
    {
        /// <summary>Adds the instruction paper to the machine's note slot (storage index 3) if missing.</summary>
        internal static void TryEnsure(GameItem machine)
        {
            if (machine == null || machine.identifier != ItemIds.Purifier)
            {
                return;
            }
            try
            {
                // Storage indices (MachineryPanelRow.CollectStorage): power=0, module=1,
                // container=2, note=3 — the display-only purity panel adds no storage entry.
                GameSlotInventory noteSlot = MachineHelper.GetSlot(machine, 3) as GameSlotInventory;
                if (noteSlot == null || noteSlot.childItem != null)
                {
                    return;
                }
                GameItem manual = ItemFactory.CreatePurifierManual();
                if (manual == null)
                {
                    return;
                }
                GraphUtils.TryAcceptAll(noteSlot, manual);
                if (noteSlot.childItem == null)
                {
                    noteSlot.UncheckedAccept(manual);
                }
                if (noteSlot.childItem != null)
                {
                    Debug.Log("[PrecisionLab] instruction note added to the purifier's note slot");
                }
                else
                {
                    Debug.LogWarning("[PrecisionLab] purifier instruction note could not be inserted into the note slot");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] ensuring the purifier manual failed: " + e.Message);
            }
        }

        /// <summary>Scans the store for purifier machines without a manual (load / morning catch-up).</summary>
        internal static void EnsureAllInStore()
        {
            try
            {
                PlayerStore store = PlayerStore.Instance;
                if (store == null)
                {
                    return;
                }
                for (int i = 0; i < store.FindAllItem().Count; i++)
                {
                    TryEnsure(store.FindAllItem()[i]);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] purifier manual scan failed: " + e.Message);
            }
        }
    }

    /// <summary>Postfix on DirectoryMaster.Item: runs after child-item stripping and ownership setup.</summary>
    [HarmonyPatch(typeof(DirectoryMaster), "Item", typeof(string), typeof(bool))]
    internal static class PurifierSpawnPatch
    {
        private static void Postfix(string identifier, GameItem __result)
        {
            if (__result != null && identifier == ItemIds.Purifier)
            {
                PurifierManualHelper.TryEnsure(__result);
            }
        }
    }
}
