using System;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Older debug/seller tests could leave unowned first aid boxes/shells/seals in the store
    /// inventories. Unowned store items cannot be moved or removed by the player, and no customer
    /// was offering them, so they became stuck. On load, claim only our unowned first aid items so
    /// the player can move/sell/dismantle them normally.
    /// </summary>
    internal static class FirstAidCleanup
    {
        internal static void ClaimStaleUnownedItems()
        {
            try
            {
                if (EmporiumEntry.Instance == null)
                {
                    return;
                }
                int claimed = 0;
                claimed += Claim(EmporiumEntry.Instance.frontInvinvElement);
                claimed += Claim(EmporiumEntry.Instance.backInvinvElement);
                claimed += Claim(EmporiumEntry.Instance.invElement);
                if (claimed > 0)
                {
                    Debug.Log("[PrecisionLab] claimed " + claimed + " stale unowned first aid item(s)");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] first aid stale-item cleanup failed: " + e.Message);
            }
        }

        private static int Claim(GameInventory inventory)
        {
            int count = 0;
            if (inventory == null)
            {
                return 0;
            }
            Debug.Log("[PrecisionLab] cleanup inventory " + inventory.GetType().Name
                + " childCount=" + inventory.childItems.Count);
            for (int i = 0; i < inventory.childItems.Count; i++)
            {
                GameItem child = inventory.childItems[i];
                Debug.Log("[PrecisionLab] cleanup inventory child[" + i + "] id="
                    + (child != null ? child.identifier : "null")
                    + " owned=" + (child != null ? GeneralHelper.IsItemOwned(child).ToString() : "-"));
            }
            ClaimInventory(inventory, ref count);
            // Second pass: after parent boxes are claimed, claim children that were inside them.
            ClaimInventory(inventory, ref count);
            Debug.Log("[PrecisionLab] first aid cleanup scanned " + inventory.GetType().Name
                + " childCount=" + inventory.childItems.Count + " claimed=" + count);
            return count;
        }


        private static void RefreshVisual(GameItem item, string id)
        {
            try
            {
                if (item == null)
                {
                    return;
                }
                if (id == ItemIds.FirstAidBox)
                {
                    ModSprites.Apply(item, SpriteKeys.FirstAidBoxSprite, "Items/items_container", "med_box");
                    item.SetShape(new GridShapeBuilder(3, 3).SetDataFill(1).Build());
                }
                else if (id == ItemIds.FirstAidShell)
                {
                    ModSprites.Apply(item, SpriteKeys.FirstAidShellSprite, "Items/items_container", "med_box");
                    item.SetShape(new GridShapeBuilder(3, 3).SetDataFill(1).Build());
                }
                else if (id == ItemIds.FirstAidSeal)
                {
                    ModSprites.Apply(item, SpriteKeys.FirstAidSealSprite, "Items/items_tool", "label");
                    item.SetShape(new GridShapeBuilder(1, 1).SetDataFill(1).Build());
                }
                else
                {
                    return;
                }
                Debug.Log("[PrecisionLab] refreshed first aid visual " + id
                    + " spritePath=" + item.spritePath + " mod=" + item.useSpriteFromMod);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] refreshing first aid visual failed: " + e.Message);
            }
        }

        private static void ClaimInventory(GameInventory inventory, ref int count)
        {
            if (inventory == null)
            {
                return;
            }
            for (int i = 0; i < inventory.childItems.Count; i++)
            {
                GameItem item = inventory.childItems[i];
                if (item == null)
                {
                    continue;
                }
                string itemId = item.identifier ?? "";
                Debug.Log("[PrecisionLab] cleanup scan item id=" + itemId
                    + " owned=" + GeneralHelper.IsItemOwned(item)
                    + " inv=" + inventory.GetType().Name);
                RefreshVisual(item, itemId);
                if (itemId == ItemIds.FirstAidBox) FirstAidTier.EnsureFeature(item);
                if (!GeneralHelper.IsItemOwned(item)
                    && (itemId == ItemIds.FirstAidBox
                        || itemId == ItemIds.FirstAidShell
                        || itemId == ItemIds.FirstAidSeal
                        || itemId.IndexOf("first_aid", StringComparison.Ordinal) >= 0))
                {
                    GeneralHelper.SetItemOwned(item, true);
                    count++;
                    Debug.Log("[PrecisionLab] claimed stale first aid item " + item.identifier
                        + " from " + inventory.GetType().Name);
                }
                if (item.contentWindow != null)
                {
                    GameInventory nested = item.contentWindow.child as GameInventory;
                    ClaimInventory(nested, ref count);
                }
            }
        }
    }
}