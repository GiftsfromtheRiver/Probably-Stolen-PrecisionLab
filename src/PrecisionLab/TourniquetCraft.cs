using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Makeshift tourniquet: a medical item crafted by dragging a piece of tough cotton fabric
    /// (kotton_fabric) onto a piece of scrap metal (scrap_metal). The recipe is part of the
    /// "More Medicines" network upgrade: before the unlock the combine target functions return
    /// false, so the drag gets no highlight and nothing is crafted.
    /// </summary>
    internal static class TourniquetCraft
    {
        internal const string FabricId = "kotton_fabric";
        internal const string ScrapId = "scrap_metal";
        internal const string MedsUpgradeId = "PRECISION_LAB_MEDS";

        private static readonly HashSet<GameItem> attachedItems = new HashSet<GameItem>();

        internal static bool IsUnlocked()
        {
            return ModEntry.IsUpgradeUnlocked(MedsUpgradeId);
        }

        // ------------------------------------------------------------------
        // Item factory (registered on the mod item directory by ModEntry)
        // ------------------------------------------------------------------
        internal static GameItem CreateTourniquet()
        {
            GameItem item = ItemDirectory.CreateEmptyItem();
            ApplySpriteAndShape(item);
            item.name = Loc.Get("item.makeshift_tourniquet.name", "Makeshift Tourniquet");
            item.shortDescription = Loc.Get(
                "item.makeshift_tourniquet.desc",
                "A strip of tough fabric with a scrap-metal buckle. Medical item: use it while wounded to stabilise the wound and lower the wound level by one (like a hemostatic bandage).");
            item.unitValue = 24L;
            item.SetGameItemType("MEDICAL");
            item.EnableTag("ITEM_MEDICAL_TAG");
            item.EnableProficiency("MEDICAL_PROFICIENCY");
            item.mayActivateSlotItemFunc = delegate (GameItem self, GameInventory inventory, SlotMarker slot)
            {
                return GeneralHelper.IsItemOwned(self)
                    && PlayerStore.Instance != null
                    && !PlayerStore.Instance.healthData.isWoundStable;
            };
            item.canActivateSlotItemFunc = delegate (GameItem self, GameInventory inventory, SlotMarker slot)
            {
                return GeneralHelper.IsItemOwned(self)
                    && PlayerStore.Instance != null
                    && !PlayerStore.Instance.healthData.isWoundStable;
            };
            item.onActivateSlotItemFunc = delegate (GameItem self, GameInventory inventory, SlotMarker slot)
            {
                try
                {
                    HealthData health = PlayerStore.Instance.healthData;
                    health.isWoundStable = true;
                    if (health.woundState > 0)
                    {
                        health.woundState--;
                    }
                    health.OnHealingItemUsed();
                    self.Destroy();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[PrecisionLab] using the makeshift tourniquet failed: " + e.Message);
                }
            };
            AudioHelper.InitTextile(item);
            ModHelper.SetSource(item, Loc.ModId, ModEntry.ModDisplayName);
            return item;
        }

        /// <summary>Applies the 32x32 mod art and forces a rectangular 2x2 footprint.</summary>
        internal static void ApplySpriteAndShape(GameItem item)
        {
            if (item == null)
            {
                return;
            }
            try
            {
                if (ModSprites.Has(SpriteKeys.Tourniquet))
                {
                    item.SetSpriteAndShapeFromMod(Loc.ModId, SpriteKeys.Tourniquet);
                    // The alpha cut-out has transparent corners, so derive-from-alpha would give a
                    // ragged shape; the item is meant to occupy a clean 3x2 rectangle.
                    item.SetShape(new GridShapeBuilder(2, 2).SetDataFill(2, 2, 1).Build());
                }
                else
                {
                    item.SetSpriteAndShape("Items/items_medical", "bandage_small");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] applying the tourniquet sprite/shape failed: " + e.Message);
            }
        }
        // ------------------------------------------------------------------
        // Combine interaction: drag fabric onto scrap metal
        // ------------------------------------------------------------------
        internal static void Attach(GameItem item)
        {
            if (item == null || item.identifier != FabricId)
            {
                return;
            }
            if (!attachedItems.Add(item))
            {
                return;
            }
            try
            {
                item.mayThisTargetItemFunc = (Func<GameItem, GameItem, bool>)Delegate.Combine(
                    item.mayThisTargetItemFunc,
                    new Func<GameItem, GameItem, bool>(CanCraft));
                item.canThisTargetItemFunc = (Func<GameItem, GameItem, bool>)Delegate.Combine(
                    item.canThisTargetItemFunc,
                    new Func<GameItem, GameItem, bool>(CanCraft));
                item.onThisTargetItemFunc = (Action<GameItem, GameItem>)Delegate.Combine(
                    item.onThisTargetItemFunc,
                    new Action<GameItem, GameItem>(TryCraft));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] attaching the tourniquet recipe failed: " + e.Message);
            }
        }

        internal static bool CanCraft(GameItem source, GameItem target)
        {
            try
            {
                return source != null
                    && target != null
                    && source.identifier == FabricId
                    && target.identifier == ScrapId
                    && IsUnlocked()
                    && GeneralHelper.IsItemOwned(source)
                    && GeneralHelper.IsItemOwned(target)
                    && (target.parentInventory != null || source.parentInventory != null);
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static void TryCraft(GameItem source, GameItem target)
        {
            if (!CanCraft(source, target))
            {
                return;
            }
            GameItem result = null;
            try
            {
                result = ItemSpawner.Spawn(ItemIds.Tourniquet);
                if (result == null)
                {
                    return;
                }
                GeneralHelper.SetItemOwned(result, true);
                GameInventory destination = target.parentInventory ?? source.parentInventory;
                if (destination == null)
                {
                    result.Destroy();
                    return;
                }
                // GraphUtils.TryAcceptAll returns the amount that was NOT accepted (0 = fully placed).
                int remaining = GraphUtils.TryAcceptAll(destination, result);
                if (remaining != 0)
                {
                    result.Destroy();
                    return;
                }
                ConsumeOne(source);
                ConsumeOne(target);
                Debug.Log("[PrecisionLab] crafted a makeshift tourniquet from fabric + scrap metal");
            }
            catch (Exception e)
            {
                if (result != null)
                {
                    try
                    {
                        result.Destroy();
                    }
                    catch (Exception)
                    {
                        // Ignore cleanup failures.
                    }
                }
                Debug.LogWarning("[PrecisionLab] crafting the makeshift tourniquet failed: " + e.Message);
            }
        }

        private static void ConsumeOne(GameItem item)
        {
            if (item == null)
            {
                return;
            }
            if (item.unitCount > 1)
            {
                item.SetUnitCount(item.unitCount - 1);
                item.Validate();
            }
            else
            {
                item.Destroy();
            }
        }

        // ------------------------------------------------------------------
        // Recipe note: delivered together with the adrenaline note on unlock
        // ------------------------------------------------------------------
        internal static GameItem CreateRecipeNote()
        {
            try
            {
                GameItem note = PreBuiltItemHelper.CreateHandnote(
                    Loc.Get("item.tourniquet_recipe.body",
                        "MAKESHIFT TOURNIQUET - emergency medical recipe.\n\nDrag a piece of tough cotton fabric (kotton_fabric) onto a piece of scrap metal (scrap_metal): this consumes 1 fabric + 1 scrap metal and crafts 1 makeshift tourniquet.\n\nThe tourniquet is a medical item: use it while wounded to stabilise the wound and lower the wound level by one (like a hemostatic bandage).\n\nThis recipe only works after unlocking More Medicines: Adrenaline."),
                    Loc.Get("item.tourniquet_recipe.title", "Makeshift Tourniquet"),
                    "",
                    1);
                if (note != null)
                {
                    note.name = Loc.Get("item.tourniquet_recipe.name", "Makeshift Tourniquet Recipe");
                    ModHelper.SetSource(note, Loc.ModId, ModEntry.ModDisplayName);
                }
                return note;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] creating the tourniquet recipe note failed: " + e.Message);
                return null;
            }
        }

        /// <summary>Called on game load: decorate all existing fabric/scrap items and make sure the note exists.</summary>
        internal static void OnGameLoaded()
        {
            try
            {
                if (!IsUnlocked() || !PlayerStore.HasInstance || PlayerStore.Instance == null)
                {
                    return;
                }
                AttachToStoreItems();
                EnsureRecipeNote();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] tourniquet load hook failed: " + e.Message);
            }
        }

        /// <summary>Called right after the "More Medicines" upgrade is purchased.</summary>
        internal static void OnUnlocked()
        {
            try
            {
                AttachToStoreItems();
                EnsureRecipeNote();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] tourniquet unlock hook failed: " + e.Message);
            }
        }

        internal static void AttachToStoreItems()
        {
            if (!PlayerStore.HasInstance || PlayerStore.Instance == null)
            {
                return;
            }
            var items = PlayerStore.Instance.FindAllItem();
            if (items == null)
            {
                return;
            }
            for (int i = 0; i < items.Count; i++)
            {
                GameItem item = items[i];
                Attach(item);
                if (item != null && item.identifier == ItemIds.Tourniquet
                    && (item.shape == null || item.shape.width != 2 || item.shape.height != 2))
                {
                    try
                    {
                        ApplySpriteAndShape(item);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[PrecisionLab] migrating a tourniquet item shape failed: " + e.Message);
                    }
                }
            }
        }

        internal static void EnsureRecipeNote()
        {
            string title = Loc.Get("item.tourniquet_recipe.title", "Makeshift Tourniquet");
            string body = Loc.Get("item.tourniquet_recipe.body", "");
            if (string.IsNullOrEmpty(body))
            {
                return;
            }
            bool found = false;
            var items = PlayerStore.Instance.FindAllItem();
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    GameItem item = items[i];
                    if (item == null || !item.IsTag("HANDNOTE_TITLE"))
                    {
                        continue;
                    }
                    if (item.GetTagReadonly("HANDNOTE_TITLE").GetString() != title)
                    {
                        continue;
                    }
                    found = true;
                    item.ModifyTag("HANDNOTE_BODY", delegate (TagState state)
                    {
                        state.Enable().SetString(body);
                    });
                    item.ModifyTag("HANDNOTE_SIGN", delegate (TagState state)
                    {
                        state.Enable().SetString("");
                    });
                    break;
                }
            }
            if (!found)
            {
                GameItem note = CreateRecipeNote();
                if (note != null)
                {
                    GeneralHelper.SetItemOwned(note, true);
                    GraphUtils.EmporiumTryAdd(note);
                    Debug.Log("[PrecisionLab] makeshift tourniquet recipe note delivered");
                }
            }
        }
    }
}
