using System;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>Adds a supplier stock item without losing it when the 18x4 front counter is full:
    /// overflow goes to the back-counter storage so every kit can still be obtained.</summary>
    internal static class StockOverflow
    {
        internal static void Add(string id)
        {
            try
            {
                if (!DirectoryMaster.Has<GameItem>(id)) return;
                GameItem item = DirectoryMaster.Item(id);
                if (item == null) return;
                GeneralHelper.SetItemOwned(item, false);
                EmporiumEntry store = EmporiumEntry.Instance;
                if (store == null || store.frontInvinvElement == null || store.backInvinvElement == null)
                {
                    PlayerStore.Instance.AddDirectSellingItemToTable(item);
                    return;
                }
                if (GraphUtils.TryAcceptAll(store.frontInvinvElement, item) == 0) return;
                if (GraphUtils.TryAcceptAll(store.backInvinvElement, item) == 0) return;
                Debug.LogWarning("[PrecisionLab] stock item could not be placed: " + id);
                GraphUtils.EmporiumTryAdd(item);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] stock overflow failed for " + id + ": " + e.Message);
            }
        }
    }
}