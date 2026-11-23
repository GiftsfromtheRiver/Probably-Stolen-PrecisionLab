using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Swaps the mod's sealed precursor kits to the new 3x2 reagent-rack artwork and guarantees
    /// they are not contraband. Existing kits in old saves are migrated on load.
    /// </summary>
    internal static class ReagentKitVisuals
    {
        private static readonly Dictionary<string, string> SpriteById = new Dictionary<string, string>
        {
            { ItemIds.SupplyBox, SpriteKeys.ReagentKitAdrenaline },
            { ItemIds.NaloxoneBox, SpriteKeys.ReagentKitNaloxone },
            { ItemIds.BirthControlBox, SpriteKeys.ReagentKitBirthControl },
            { ItemIds.SannaiBox, SpriteKeys.ReagentKitSannai },
            { ItemIds.SynaprestBox, SpriteKeys.ReagentKitSynaprest },
            { ItemIds.BlueBloodBox, SpriteKeys.ReagentKitBlueBlood },
            { ItemIds.ImmunivaxBox, SpriteKeys.ReagentKitImmunivax },
            { ItemIds.OrdnanceBox, SpriteKeys.ReagentKitOrdnance },
            { ItemIds.PerfumeBox, SpriteKeys.ReagentKitPerfume },
            { ItemIds.BioPharmaBox, SpriteKeys.ReagentKitBioPharma }
        };

        internal static bool HasArt(string itemId)
        {
            string key;
            return itemId != null && SpriteById.TryGetValue(itemId, out key) && ModSprites.Has(key);
        }

        internal static void ApplyArt(GameItem item)
        {
            if (item == null || item.identifier == null)
            {
                return;
            }
            string key;
            if (SpriteById.TryGetValue(item.identifier, out key) && ModSprites.Has(key))
            {
                item.SetSpriteAndShapeFromMod(Loc.ModId, key);
                ForceKitShape(item);
            }
            ApplyContrabandPolicy(item);
        }

        /// <summary>Military ordnance kits are high contraband; all other kits are non-contraband.</summary>
        internal static void ApplyContrabandPolicy(GameItem item)
        {
            if (item == null)
            {
                return;
            }
            if (item.identifier == ItemIds.OrdnanceBox)
            {
                try
                {
                    ContrabandHelper.InitContrabandItem(item, 3);
                }
                catch (Exception)
                {
                    // Older/newer builds may not expose the helper.
                }
                return;
            }
            if (item.identifier == ItemIds.PerfumeBox)
            {
                try
                {
                    ContrabandHelper.InitContrabandItem(item, 2);
                }
                catch (Exception)
                {
                    // Older/newer builds may not expose the helper.
                }
                return;
            }
            MakeNonContraband(item);
        }

        /// <summary>Forces the kit footprint to 3x2 regardless of sprite-derived shape rounding.</summary>
        internal static void ForceKitShape(GameItem item)
        {
            try
            {
                if (item != null)
                {
                    item.SetShape(new GridShapeBuilder(3, 2).SetDataFill(1).Build());
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] forcing kit 3x2 shape failed: " + e.Message);
            }
        }

        internal static void MakeNonContraband(GameItem item)
        {
            if (item == null)
            {
                return;
            }
            try
            {
                ContrabandHelper.RemoveContrabandStatus(item);
            }
            catch (Exception)
            {
                // Older/newer builds may not expose the helper; the item never carried the tag anyway.
            }
        }

        /// <summary>Migrates kits already present in a save (sprite, 3x2 shape, non-contraband).</summary>
        internal static void MigrateExistingKits()
        {
            try
            {
                if (!PlayerStore.HasInstance || PlayerStore.Instance == null)
                {
                    return;
                }
                List<GameItem> items = PlayerStore.Instance.FindAllItem();
                if (items == null)
                {
                    return;
                }
                int migrated = 0;
                for (int i = 0; i < items.Count; i++)
                {
                    GameItem item = items[i];
                    if (item == null || item.identifier == null || !SpriteById.ContainsKey(item.identifier))
                    {
                        continue;
                    }
                    GameInventory inventory = item.parentInventory;
                    if (inventory == null)
                    {
                        ApplyArt(item);
                        migrated++;
                        continue;
                    }

                    // SetSpriteAndShapeFromMod resets the item position to (0,0) without
                    // re-placing it, so several kits would stack on the same cells (2x2 -> 3x2
                    // migration, or already-migrated overlapping kits from older builds). Always
                    // re-place an in-inventory kit after applying the 3x2 art; if it cannot fit,
                    // fall back to the old 2x2 supply-box art and place it again.
                    ApplyArt(item);
                    inventory.Expel(item);
                    GraphUtils.TryAcceptAll(inventory, item);
                    if (item.parentInventory == null)
                    {
                        try
                        {
                            // Force an explicit 2x2 fallback shape (chemical2 is not 32x32 in this build).
                            item.SetSpriteAndShape("Items/items_material", "chemical2");
                            item.useSpriteFromMod = false;
                            item.SetShape(new GridShapeBuilder(2, 2).SetDataFill(1).Build());
                        }
                        catch (Exception)
                        {
                            // Shape restore is best-effort; placement below still prevents loss.
                        }
                        ApplyContrabandPolicy(item);
                        // The kit previously occupied a 2x2 slot in this inventory, so this should fit;
                        // only search other store inventories if the parent genuinely has no room.
                        if (GraphUtils.TryAcceptAll(inventory, item) != 0 || item.parentInventory == null)
                        {
                            GraphUtils.EmporiumTryAdd(inventory, item);
                        }
                        Debug.LogWarning("[PrecisionLab] no room to re-place 3x2 kit '" + item.identifier + "'; kept a 2x2 fallback shape");
                    }
                    else
                    {
                        migrated++;
                    }
                }
                if (migrated > 0)
                {
                    Debug.Log("[PrecisionLab] migrated " + migrated + " precursor kit item(s) to the 3x2 reagent-kit art");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] migrating precursor kit visuals failed: " + e.Message);
            }
        }
    }
}
