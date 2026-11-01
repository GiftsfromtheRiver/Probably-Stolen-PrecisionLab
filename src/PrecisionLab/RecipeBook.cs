using System;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// The recipes are shipped as a vanilla paper-book item (Books/*.json + PaperBookRegistry)
    /// instead of loose handnote papers: one book item, one page per recipe. Adding a future
    /// medicine only means appending another page to the JSON, not delivering another note item.
    /// </summary>
    internal static class RecipeBook
    {
        internal const string BookId = "precision_lab_recipes";
        internal const string ItemId = "precision_lab_recipe_book";

        internal static GameItem Spawn()
        {
            try
            {
                if (ModHelper.GetPaperBookIds(Loc.ModId) == null || ModHelper.GetPaperBookIds(Loc.ModId).Count == 0)
                {
                    Debug.LogWarning("[PrecisionLab] recipe book asset is not loaded yet");
                    return null;
                }
                return ModHelper.SpawnPaperItem(Loc.ModId, BookId);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] spawning the recipe book failed: " + e.Message);
                return null;
            }
        }

        /// <summary>Delivers the recipe book once (front counter first), if the player has the meds unlock.</summary>
        internal static void EnsureDelivered()
        {
            try
            {
                if (!ModEntry.IsUpgradeUnlocked("PRECISION_LAB_MEDS")
                    || !PlayerStore.HasInstance
                    || PlayerStore.Instance == null)
                {
                    return;
                }
                var items = PlayerStore.Instance.FindAllItem();
                if (items != null)
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        GameItem item = items[i];
                        if (item != null && item.identifier == ItemId)
                        {
                            return;
                        }
                    }
                }
                GameItem book = Spawn();
                if (book == null)
                {
                    Debug.LogWarning("[PrecisionLab] recipe book could not be spawned; will retry");
                    return;
                }
                GeneralHelper.SetItemOwned(book, true);
                NoteDelivery.Placement placement = NoteDelivery.TryPlaceNote(book);
                if (placement == NoteDelivery.Placement.None)
                {
                    Debug.LogWarning("[PrecisionLab] no room for the recipe book; will retry");
                    return;
                }
                try
                {
                    PlayerStore.Instance.AddNightLog(
                        Loc.Get("mech.recipe_book.delivered", "A Precision Lab recipe book was added to your store (front counter)."),
                        "5c6f46");
                }
                catch (Exception)
                {
                    // Cosmetic only.
                }
                Debug.Log("[PrecisionLab] recipe book delivered (placement=" + placement + ")");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] delivering the recipe book failed: " + e.Message);
            }
        }
    }
}
