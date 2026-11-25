using System;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Legacy cultivators from old saves have a nutrient-fruit slot. The new cultivator has no such
    /// slot, so on load/wake any fruit still sitting there is returned to the store (front counter
    /// first). If there is nowhere to put it, the fruit stays in the legacy slot and migration retries.
    /// </summary>
    internal static class CultivatorNutrientMigration
    {
        private static readonly System.Collections.Generic.List<GameItem> Pending = new System.Collections.Generic.List<GameItem>();

        internal static void EnsureAll()
        {
            try
            {
                PlayerStore store = PlayerStore.Instance;
                if (store == null) return;
                for (int i = Pending.Count - 1; i >= 0; i--)
                {
                    if (Pending[i] == null) { Pending.RemoveAt(i); continue; }
                    if (TryReturnToStore(Pending[i])) Pending.RemoveAt(i);
                }
                var items = store.FindAllItem();
                if (items == null) return;
                for (int i = 0; i < items.Count; i++)
                {
                    GameItem machine = items[i];
                    if (machine == null || machine.identifier != ItemIds.Cultivator || machine.parentInventory == null) continue;
                    if (!(machine.contentWindow?.childElement is MachineryPanelRow row)) continue;
                    MachineryPanel nutrientPanel = null;
                    foreach (MachineryPanel panel in row.Panels)
                    {
                        if (panel != null && panel.role == "nutrient") { nutrientPanel = panel; break; }
                    }
                    if (nutrientPanel == null) continue;
                    GameSlotInventory slot = MachineHelper.GetSlot(machine, 3) as GameSlotInventory;
                    if (slot == null || slot.childItem == null)
                    {
                        // Empty legacy slot: no migration needed, but the obsolete panel still hides.
                        nutrientPanel.SetHidden(true);
                        continue;
                    }
                    GameItem fruit = slot.childItem;
                    string name = fruit.name;
                    slot.Expel(fruit);
                    if (!TryReturnToStore(fruit))
                    {
                        if (!slot.UncheckedAccept(fruit))
                        {
                            Pending.Add(fruit);
                            Debug.LogError("[PrecisionLab] CRITICAL: could not restore or return legacy nutrient '" + name + "'; item is detached and queued for retry");
                        }
                        else
                        {
                            Debug.LogWarning("[PrecisionLab] cultivator nutrient return failed; kept " + name + " in legacy slot");
                        }
                        continue;
                    }
                    nutrientPanel.SetHidden(true);
                    Debug.Log("[PrecisionLab] returned legacy cultivator nutrient to store and hid old nutrient panel: " + name);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] cultivator nutrient migration failed: " + e.Message);
            }
        }

        private static bool TryReturnToStore(GameItem item)
        {
            if (item == null) return false;
            try
            {
                GeneralHelper.SetItemOwned(item, true);
                EmporiumEntry entry = EmporiumEntry.Instance;
                if (entry != null)
                {
                    GraphNodeStorage[] candidates =
                    {
                        entry.frontInvinvElement,
                        entry.backInvinvElement,
                        entry.invElement,
                        entry.showcaseElement,
                        entry.afterhourInventory
                    };
                    for (int i = 0; i < candidates.Length; i++)
                    {
                        GraphNodeStorage candidate = candidates[i];
                        if (candidate == null) continue;
                        if (GraphUtils.TryAcceptAll(candidate, item) == 0) return true;
                    }
                }
                GraphUtils.EmporiumTryAdd(item);
                return item.parentInventory != null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] returning legacy nutrient failed: " + e.Message);
                return false;
            }
        }
    }
}
