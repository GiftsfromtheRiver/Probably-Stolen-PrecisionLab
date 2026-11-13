using System;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// When the player sells a labelled first aid box, the customer may question the label.
    /// Chance depends on how far the displayed tier is above/below the real tier:
    ///   displayed higher by 1 = 15%, by 2 = 30%
    ///   same = 10%
    ///   displayed lower by 1 = 5%, by 2 = 1%
    /// If questioned: real &gt;= displayed → +10% price and faction reputation;
    /// otherwise → -15% price and faction reputation loss.
    /// </summary>
    [HarmonyPatch(typeof(PlayerStore), "SellItem")]
    internal static class FirstAidQuestionPatch
    {
        private const string AppliedTag = "FIRST_AID_QUESTION_APPLIED_INT";
        private const string PriceFeatureId = "first_aid_question_price";

        private static void Prefix(GameItem item)
        {
            try
            {
                if (!FirstAidBox.IsBox(item) || item.IsTag(AppliedTag))
                {
                    return;
                }
                FirstAidTier.EnsureFeature(item);
                int real = FirstAidTier.GetRealTier(item);
                int displayed = FirstAidTier.GetDisplayTier(item);
                int diff = displayed - real;
                int chance = QuestionChance(diff);
                if (chance <= 0 || UnityEngine.Random.Range(0, 100) >= chance)
                {
                    return;
                }
                item.EnableTag(AppliedTag);
                StoreClient client = PlayerStore.Instance != null && PlayerStore.Instance.currentClientInstance != null
                    ? PlayerStore.Instance.currentClientInstance.GetClientBlueprint()
                    : null;
                string faction = client != null ? client.clientFaction : null;
                bool satisfied = real >= displayed;
                int percent = satisfied ? 10 : -15;
                ApplyPriceModifier(item, percent);
                if (!string.IsNullOrEmpty(faction))
                {
                    StoreReputation.ModReputation(faction, satisfied ? 5 : -5, true);
                }
                string message = Loc.Get("ui.first_aid.question", "这个拎起来怎么感觉重量不对啊……")
                    + " " + string.Format(
                        Loc.Get("ui.first_aid.question_report", "顾客拆开检查：标签「{0}」，实际「{1}」。"),
                        FirstAidTier.TierName(displayed), FirstAidTier.TierName(real))
                    + " " + (satisfied
                        ? Loc.Get("ui.first_aid.question_ok", "符合标签，额外加价 10%。")
                        : Loc.Get("ui.first_aid.question_bad", "不符合标签，交易价格 -15%，阵营好感下降。"));
                try
                {
                    StoreUIManager.Instance.Notify(message, "default");
                }
                catch (Exception) { }
                Debug.Log("[PrecisionLab] first aid questioned real=" + real + " displayed=" + displayed
                    + " satisfied=" + satisfied + " chance=" + chance);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] first aid question check failed: " + e.Message);
            }
        }

        private static int QuestionChance(int diff)
        {
            switch (diff)
            {
                case 2: return 30;
                case 1: return 15;
                case 0: return 10;
                case -1: return 5;
                case -2: return 1;
                default: return 0;
            }
        }

        private static void ApplyPriceModifier(GameItem item, int percent)
        {
            try
            {
                ItemFeature feature = item.FindItemFeatureByID(PriceFeatureId);
                if (feature == null)
                {
                    feature = new ItemFeature("CATEGORY_FIRST_AID_QUESTION");
                    feature.identifier = PriceFeatureId;
                    item.AddItemFeature(feature);
                }
                feature.useCondition = false;
                feature.SetValueStage(ItemFeature.ValueStage.Final);
                feature.SetValueModifier(percent);
                feature.SetPublicDisplay("急救盒交易调整 " + (percent > 0 ? "+" : "") + percent + "%");
                feature.SetActualDisplay("急救盒交易调整 " + (percent > 0 ? "+" : "") + percent + "%");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] applying first aid price modifier failed: " + e.Message);
            }
        }
    }
}
