using System;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>Grades drug-quality by how close the filled 5 mL pack is to the perfect
    /// active/additive ratio, instead of the raw active fraction. Perfect ±0.1 mL is lab pure;
    /// larger deviations step down; the worst case is when one component sits at its minimum.
    /// </summary>
    [HarmonyPatch(typeof(ChemicalProductHelper), "GradeProduct")]
    internal static class DrugQualityGradePatch
    {
        private static int _busy;

        private static void Postfix(GameItem item)
        {
            if (item == null || _busy > 0) return;
            string active = ActiveFor(item.identifier);
            if (active == null) return;
            try
            {
                _busy++;
                string activePart = Liquid.GetCurrentPartFromId(active);
                int activeAmt = activePart != null ? item.GetTagReadonly(activePart).GetInt() : 0;
                int total = ChemicalHelper.GetTotalVolume(item);
                if (total <= 0) total = 5000;
                int impurity = 0;
                string[] contaminantIds = { "chemical_contaminant", "physical_contaminant", "microbe", "plant_matter", "debris" };
                for (int i = 0; i < contaminantIds.Length; i++)
                {
                    string p = Liquid.GetCurrentPartFromId(contaminantIds[i]);
                    if (p != null) impurity += item.GetTagReadonly(p).GetInt();
                }
                int solvent = Math.Max(0, total - activeAmt - impurity);
                // Single active component (like adrenaline): keep vanilla grade.
                if (solvent < 500 && impurity == 0) return;
                int minPart = Math.Min(activeAmt, solvent);
                ItemCondition cond;
                if (impurity > 0)
                {
                    cond = minPart >= 2000 ? ItemConditionList.CreatePharmaGrade() : ItemConditionList.CreateJunkGrade();
                }
                else if (minPart >= 2400) cond = ItemConditionList.CreateLabPure();
                else if (minPart >= 2200) cond = ItemConditionList.CreatePharmaGrade();
                else if (minPart > 2000) cond = ItemConditionList.CreateNearPharma();
                else cond = ItemConditionList.CreateStarchGrade();
                ChemicalProductHelper.SetPurityFeature(item, cond);
                double purity = cond.identifier == "lab_pure" ? 100.0 : Math.Max(0.0, 100.0 * minPart / 2500.0);
                item.ModifyTag("CHEMICAL_PRODUCT_PURITY_TAG", delegate (TagState st) { st.Enable().SetDouble(purity); });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] drug quality grade failed: " + e.Message);
            }
            finally { _busy--; }
        }

        private static string ActiveFor(string id)
        {
            if (id == BelladonnaContent.InjectorId) return CustomLiquids.BelladonnaPure;
            if (id == DrugLabContent.AntidepressantId) return CustomLiquids.AntidepressantApi;
            if (id == DrugLabContent.AmiodaroneId) return CustomLiquids.AmiodaroneApi;
            if (id == DrugLabContent.VasopressinId) return CustomLiquids.VasopressinSolution;
            if (id == DrugLabContent.VasodilatorId) return CustomLiquids.VasodilatorSolution;
            if (id == DrugLabContent.CoagulantId) return CustomLiquids.CoagulantGel;
            if (id == DrugLabContent.AnticoagulantId) return CustomLiquids.AnticoagulantSolution;
            return null;
        }

        private static string AdditiveFor(string id)
        {
            if (id == BelladonnaContent.InjectorId) return "lipobond_agent";
            if (id == DrugLabContent.AntidepressantId || id == DrugLabContent.AmiodaroneId) return "base";
            if (id == DrugLabContent.VasopressinId) return "lipobond_agent";
            if (id == DrugLabContent.AnticoagulantId) return CustomLiquids.Iodide;
            return null;
        }
    }
}