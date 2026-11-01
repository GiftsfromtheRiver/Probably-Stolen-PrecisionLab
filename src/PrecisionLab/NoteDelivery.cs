using System;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Places delivered documents (recipe notes) where the player will actually see them.
    /// Front counter first (visible as soon as the shop opens), then the other store
    /// inventories. If everything is full the note is not silently lost: callers retry on the
    /// next day/load through EnsureAll().
    /// </summary>
    internal static class NoteDelivery
    {
        internal enum Placement
        {
            None,
            FrontCounter,
            StoreInventory
        }

        internal static Placement TryPlaceNote(GameItem note)
        {
            if (note == null)
            {
                return Placement.None;
            }
            try
            {
                GeneralHelper.SetItemOwned(note, true);
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
                        if (candidate == null)
                        {
                            continue;
                        }
                        if (GraphUtils.TryAcceptAll(candidate, note) == 0)
                        {
                            return i == 0 ? Placement.FrontCounter : Placement.StoreInventory;
                        }
                    }
                }
                GraphUtils.EmporiumTryAdd(note);
                return note.parentInventory != null ? Placement.StoreInventory : Placement.None;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] placing a note failed: " + e.Message);
                return Placement.None;
            }
        }

        /// <summary>Retries the recipe-note deliveries for the "More Medicines" unlock.</summary>
        internal static void EnsureAll()
        {
            try
            {
                if (!ModEntry.IsUpgradeUnlocked("PRECISION_LAB_MEDS"))
                {
                    return;
                }
                RecipeBook.EnsureDelivered();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] recipe note delivery retry failed: " + e.Message);
            }
        }
    }
}