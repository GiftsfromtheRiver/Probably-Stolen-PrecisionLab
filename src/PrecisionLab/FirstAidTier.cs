using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Editable three-tier label for the first aid box (incomplete / complete / advanced).
    /// The real condition is recalculated from the contents; the fake condition is what the
    /// labeler writes and what the customer reads when deciding whether to question the box.
    ///
    /// A counterfeit / expired / unusable Immunivax injector forces the real tier to "incomplete"
    /// even when every other slot is filled (including the blue-blood upgrade).
    /// </summary>
    internal static class FirstAidTier
    {
        internal const string Category = "CATEGORY_FIRST_AID_TIER";
        internal const string FeatureId = "first_aid_tier";
        internal const string Incomplete = "first_aid_incomplete";
        internal const string Complete = "first_aid_complete";
        internal const string Advanced = "first_aid_advanced";

        internal static readonly string[] Ids = { Incomplete, Complete, Advanced };

        private static List<ItemCondition> _conditions;

        internal static void EnsureRegistered()
        {
            try
            {
                if (_conditions == null)
                {
                    _conditions = new List<ItemCondition>
                    {
                        Condition(Incomplete, "未配齐"),
                        Condition(Complete, "完整"),
                        Condition(Advanced, "高级")
                    };
                }
                if (ItemConditionList.conditionLookup != null
                    && !ItemConditionList.conditionLookup.ContainsKey(Category))
                {
                    ItemConditionList.conditionLookup[Category] = _conditions;
                }
                if (ItemFeatureList.CategoryNames != null
                    && !ItemFeatureList.CategoryNames.ContainsKey(Category))
                {
                    ItemFeatureList.CategoryNames[Category] = Loc.Get("ui.first_aid.tier_category", "急救盒品级");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] registering first aid tier condition failed: " + e.Message);
            }
        }

        private static ItemCondition Condition(string id, string display)
        {
            return new ItemCondition
            {
                category = Category,
                identifier = id,
                display = Loc.Get("ui.first_aid.tier_" + id, display),
                modValue = 0
            };
        }

        internal static ItemFeature Find(GameItem item)
        {
            try
            {
                return item != null ? item.FindItemFeatureByID(FeatureId) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static void EnsureFeature(GameItem item)
        {
            if (item == null)
            {
                return;
            }
            EnsureRegistered();
            try
            {
                ItemFeature feature = Find(item);
                int actual = ComputeActualTier(item);
                if (feature == null)
                {
                    feature = new ItemFeature(Category)
                    {
                        identifier = FeatureId,
                        useCondition = true,
                        fakeCondition = _conditions[actual],
                        realCondition = _conditions[actual],
                        isFeatureMatch = true
                    };
                    item.AddItemFeature(feature);
                    return;
                }
                feature.useCondition = true;
                feature.realCondition = _conditions[actual];
                if (feature.fakeCondition == null)
                {
                    feature.fakeCondition = _conditions[actual];
                }
                feature.isFeatureMatch = TierIndex(feature.fakeCondition) == actual;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] attaching first aid tier feature failed: " + e.Message);
            }
        }

        internal static int ComputeActualTier(GameItem box)
        {
            try
            {
                GameInventory inventory = box != null && box.contentWindow != null
                    ? box.contentWindow.child as GameInventory
                    : null;
                return ComputeTier(inventory != null ? new List<GameItem>(inventory.childItems) : new List<GameItem>());
            }
            catch (Exception)
            {
                return 0;
            }
        }

        internal static int ComputeTier(List<GameItem> items)
        {
            if (items == null)
            {
                return 0;
            }
            bool advanced = false;
            bool hasImmunivax = false;
            bool immunivaxBad = false;
            bool hasPen = false;
            bool hasTourniquet = false;
            bool hasAlcohol = false;
            bool hasBlood = false;
            bool hasNaloxone = false;
            for (int i = 0; i < items.Count; i++)
            {
                GameItem item = items[i];
                if (item == null) continue;
                string id = item.identifier;
                if (id == "blue_blood_bag") advanced = true;
                if (id == "blood_bag" || id == "blue_blood_bag") hasBlood = true;
                if (id == ItemIds.Pen) hasPen = true;
                if (id == "makeshift_tourniquet") hasTourniquet = true;
                if (id == "rubbing_alcohol") hasAlcohol = true;
                if (id == "pure_white_injector") hasNaloxone = true;
                if (id == "large_purple_injector")
                {
                    hasImmunivax = true;
                    if (!IsGenuineImmunivax(item))
                    {
                        immunivaxBad = true;
                    }
                }
            }
            // A counterfeit / expired / unusable Immunivax makes the whole kit "incomplete".
            if (!hasImmunivax || immunivaxBad)
            {
                return 0;
            }
            if (!hasPen || !hasTourniquet || !hasAlcohol || !hasBlood || !hasNaloxone)
            {
                return 0;
            }
            return advanced ? 2 : 1;
        }

        internal static bool IsGenuineImmunivax(GameItem item)
        {
            try
            {
                ItemFeature feature = item.FindItemFeatureByCategory("CATEGORY_GENUINE_INJECTOR");
                if (feature == null || feature.realCondition == null)
                {
                    // Stock / vendor injectors have no authenticity feature; treat as genuine unless
                    // the injector was explicitly marked with a problem.
                    return !item.IsTag("ISSUE_SERIAL")
                        && !item.IsTag("ISSUE_LABEL")
                        && !item.IsTag("ISSUE_COLOR")
                        && !item.IsTag("ISSUE_BODY")
                        && !item.IsTag("ISSUE_INDICATOR");
                }
                return feature.realCondition.identifier == "genuineInjector";
            }
            catch (Exception)
            {
                return true;
            }
        }

        internal static int GetRealTier(GameItem item)
        {
            ItemFeature feature = Find(item);
            if (feature != null && feature.realCondition != null)
            {
                return TierIndex(feature.realCondition);
            }
            return ComputeActualTier(item);
        }

        internal static int GetDisplayTier(GameItem item)
        {
            ItemFeature feature = Find(item);
            if (feature != null && feature.fakeCondition != null)
            {
                return TierIndex(feature.fakeCondition);
            }
            return GetRealTier(item);
        }

        internal static int TierIndex(ItemCondition condition)
        {
            if (condition == null)
            {
                return 0;
            }
            for (int i = 0; i < Ids.Length; i++)
            {
                if (Ids[i] == condition.identifier)
                {
                    return i;
                }
            }
            return 0;
        }

        internal static string TierName(int tier)
        {
            switch (tier)
            {
                case 2: return Loc.Get("ui.first_aid.tier_name_advanced", "高级");
                case 1: return Loc.Get("ui.first_aid.tier_name_complete", "完整");
                default: return Loc.Get("ui.first_aid.tier_name_incomplete", "未配齐");
            }
        }
    }
}
