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
        internal const string IngotId = "metal_ingot";
        internal const string ConditionCategory = "CATEGORY_TOURNIQUET_CONDITION";
        internal const int IngotFairPurityLevel = 7; // INGOT_PURITY_FAIR and above counts as "medium or better"
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
                    && (target.identifier == ScrapId || target.identifier == IngotId)
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
                // Try the inventories the player is looking at first, then let the game find any
                // other free store inventory (fragmented/full counters otherwise made the craft
                // silently do nothing when the 2x2 result did not fit).
                bool placed = false;
                GameInventory[] inventories = { target.parentInventory, source.parentInventory };
                for (int i = 0; i < inventories.Length; i++)
                {
                    if (inventories[i] == null)
                    {
                        continue;
                    }
                    // GraphUtils.TryAcceptAll returns the amount that was NOT accepted (0 = placed).
                    if (GraphUtils.TryAcceptAll(inventories[i], result) == 0)
                    {
                        placed = true;
                        break;
                    }
                }
                if (!placed)
                {
                    GraphUtils.EmporiumTryAdd(inventories[0], result);
                    placed = result.parentInventory != null;
                }
                if (!placed)
                {
                    result.Destroy();
                    NotifyNoSpace();
                    return;
                }
                ApplyMaterialCondition(result, target);
                ConsumeOne(source);
                ConsumeOne(target);
                Debug.Log("[PrecisionLab] crafted a makeshift tourniquet from fabric + " + target.identifier);
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

        private static void NotifyNoSpace()
        {
            try
            {
                if (PlayerStore.HasInstance && PlayerStore.Instance != null)
                {
                    PlayerStore.Instance.AddNightLog(
                        Loc.Get("mech.tourniquet.no_space", "Not enough room to place the makeshift tourniquet; the fabric and metal were not consumed."),
                        "9c3738");
                }
            }
            catch (Exception)
            {
                // Night log is cosmetic; never let it break crafting.
            }
            Debug.LogWarning("[PrecisionLab] no room to place the crafted makeshift tourniquet");
        }
        /// <summary>
        /// Labels the crafted tourniquet by the buckle material:
        ///   scrap metal                       -> 破旧 / worn (value -50%)
        ///   metal ingot, fair purity or better-> 极佳 / excellent (value +25%)
        ///   metal ingot, below fair purity    -> 陈旧 / aged (value -25%)
        /// The label is an ItemFeature condition, which the game's own value pipeline applies
        /// as a percentage modifier and the negotiation UI shows as the item condition.
        /// </summary>
        internal static void ApplyMaterialCondition(GameItem item, GameItem material)
        {
            if (item == null)
            {
                return;
            }
            try
            {
                string displayKey;
                string fallback;
                string conditionId;
                int modValue;
                if (material != null && material.identifier == IngotId)
                {
                    int level = GetIngotPurityLevel(material);
                    if (level >= IngotFairPurityLevel)
                    {
                        displayKey = "condition.tourniquet.excellent";
                        fallback = "Excellent";
                        conditionId = "tourniquet_excellent";
                        modValue = 25;
                    }
                    else
                    {
                        displayKey = "condition.tourniquet.aged";
                        fallback = "Aged";
                        conditionId = "tourniquet_aged";
                        modValue = -25;
                    }
                }
                else
                {
                    displayKey = "condition.tourniquet.worn";
                    fallback = "Worn";
                    conditionId = "tourniquet_worn";
                    modValue = -50;
                }
                ItemCondition condition = new ItemCondition
                {
                    category = ConditionCategory,
                    display = Loc.Get(displayKey, fallback),
                    modValue = modValue,
                    identifier = conditionId
                };
                ItemFeature feature = new ItemFeature(ConditionCategory);
                feature.identifier = "tourniquet_condition";
                feature.useCondition = true;
                feature.fakeCondition = condition;
                feature.realCondition = condition;
                feature.isFeatureMatch = true;
                item.AddItemFeature(feature);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] labeling the tourniquet condition failed: " + e.Message);
            }
        }

        private static int GetIngotPurityLevel(GameItem ingot)
        {
            try
            {
                string purity = IngotPurityHelper.GetPurity(ingot);
                if (!string.IsNullOrEmpty(purity) && IngotPurityHelper.ingotPurities.ContainsKey(purity))
                {
                    return IngotPurityHelper.ingotPurities[purity].PurityLevel;
                }
            }
            catch (Exception)
            {
                // Fall through to the safe default.
            }
            return 0;
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
                    NoteDelivery.Placement placement = NoteDelivery.TryPlaceNote(note);
                    if (placement == NoteDelivery.Placement.None)
                    {
                        Debug.LogWarning("[PrecisionLab] no room for the makeshift tourniquet recipe note; will retry on the next day/load");
                        return;
                    }
                    try
                    {
                        PlayerStore.Instance.AddNightLog(
                            Loc.Get("mech.tourniquet.note_delivered", "A makeshift tourniquet recipe note was added to your store inventory."),
                            "5c6f46");
                    }
                    catch (Exception)
                    {
                        // Cosmetic only.
                    }
                    Debug.Log("[PrecisionLab] makeshift tourniquet recipe note delivered (placement=" + placement + ")");
                }
            }
        }
    }
}
