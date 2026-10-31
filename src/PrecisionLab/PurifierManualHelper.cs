using System;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Ensures every spawned Precision machine (purifier / microbial cultivator) carries its
    /// instruction note in the note panel.
    ///
    /// The note must be inserted AFTER DirectoryMaster/ItemDirectory has finished creating the
    /// item: DirectoryMaster.Item explicitly destroys any child items found on a freshly created
    /// item ("this should not happen ... Removing all child items as a fix"), so inserting from
    /// inside the item factory is defeated. We hook DirectoryMaster.Item with a Harmony postfix
    /// and also re-check machines that were loaded from a save or spawned by another path.
    /// </summary>
    internal static class PurifierManualHelper
    {
        private static bool IsGuidedMachine(GameItem machine)
        {
            return machine != null
                && (machine.identifier == ItemIds.Purifier || machine.identifier == ItemIds.Cultivator);
        }

        /// <summary>Adds the instruction paper to the machine's note slot (storage index 3) if missing.</summary>
        internal static void TryEnsure(GameItem machine)
        {
            if (!IsGuidedMachine(machine))
            {
                return;
            }
            try
            {
                // Storage indices (MachineryPanelRow.CollectStorage):
                //   purifier : power=0, module=1, container=2, waste=3, note=4
                //   cultivator: power=0, module=1, container=2, nutrient=3, note=4
                int noteIndex = 4;
                GameSlotInventory noteSlot = MachineHelper.GetSlot(machine, noteIndex) as GameSlotInventory;
                if (noteSlot == null || noteSlot.childItem != null)
                {
                    return;
                }
                GameItem manual = machine.identifier == ItemIds.Purifier
                    ? ItemFactory.CreatePurifierManual()
                    : ItemFactory.CreateCultivatorManual();
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
                    Debug.Log("[PrecisionLab] instruction note added to '" + machine.identifier + "' note slot");
                }
                else
                {
                    Debug.LogWarning("[PrecisionLab] instruction note could not be inserted into '" + machine.identifier + "'");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] ensuring the instruction manual failed: " + e.Message);
            }
        }

        /// <summary>Scans the store for mod machines without a manual (load / morning catch-up).</summary>
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
                Debug.LogWarning("[PrecisionLab] machine manual scan failed: " + e.Message);
            }
        }
    }

    /// <summary>Postfix on DirectoryMaster.Item: runs after child-item stripping and ownership setup.</summary>
    [HarmonyPatch(typeof(DirectoryMaster), "Item", typeof(string), typeof(bool))]
    internal static class PurifierSpawnPatch
    {
        private static void Postfix(string identifier, GameItem __result)
        {
            if (__result != null && (identifier == ItemIds.Purifier || identifier == ItemIds.Cultivator))
            {
                PurifierManualHelper.TryEnsure(__result);
            }
        }
    }
}
