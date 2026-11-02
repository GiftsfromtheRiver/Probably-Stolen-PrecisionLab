using System;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// The vanilla birth-control and Sannai pills are normally created from their own vanilla
    /// Chem Finisher recipes. Our production chains override those two recipes so the pills can be
    /// pressed from our custom API liquids, which made normally-spawned/crafted pills carry a
    /// contradictory composition (custom API liquid at 100%, purity 0%, starch grade).
    ///
    /// This postfix runs after the game's final GradeProduct call: if a pill still contains one of
    /// our custom API liquids, rebuild its content as the vanilla active liquid at vanilla volume
    /// (birth: contraceptive 100 µL, Sannai: zyanide 600 µL) and re-grade it. The result has a real
    /// purity percentage matching its condition, exactly like a vanilla pill. Apothecary stock
    /// pills already use real vanilla composition, so they are left untouched (they don't contain
    /// the custom API liquid).
    /// </summary>
    internal static class PillCompositionFix
    {
        internal static bool IsPill(GameItem item)
        {
            return item != null && (item.identifier == "birth_control_pill" || item.identifier == "zyanide_pill");
        }

        internal static bool HasCustomApi(GameItem item)
        {
            if (item == null)
            {
                return false;
            }
            try
            {
                // Pills are chem items, not liquid containers: read the chemical-content tags
                // directly instead of LiquidTools.GetLiquidAmount (which requires a liquid container).
                string apiPart = Liquid.GetCurrentPartFromId(CustomLiquids.ContraceptiveApi);
                if (apiPart != null && item.GetTagReadonly(apiPart).GetInt() > 0)
                {
                    return true;
                }
                string sannaiPart = Liquid.GetCurrentPartFromId(CustomLiquids.SannaiApi);
                return sannaiPart != null && item.GetTagReadonly(sannaiPart).GetInt() > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static void NormalizeVanilla(GameItem item)
        {
            if (!IsPill(item))
            {
                return;
            }
            string active;
            int volume;
            if (item.identifier == "birth_control_pill")
            {
                active = "contraceptive";
                volume = 100;
            }
            else
            {
                active = "zyanide";
                volume = 600;
            }
            ChemicalHelper.RemoveAllLiquid(item);
            item.ModifyTag("ACTIVATE_INGREDIENT_TAG", delegate (TagState state)
            {
                state.Enable().SetString(active);
            });
            ChemFinisherRecipe.SetCompositionByPurity(item, 60, 80, volume, active);
            ChemicalProductHelper.GradeProduct(item);
        }
    }

    [HarmonyPatch(typeof(ChemicalProductHelper), "GradeProduct")]
    internal static class PillCompositionGradePatch
    {
        private static bool _fixing;

        private static void Postfix(GameItem item)
        {
            if (_fixing || item == null)
            {
                return;
            }
            if (!PillCompositionFix.IsPill(item) || !PillCompositionFix.HasCustomApi(item))
            {
                return;
            }
            _fixing = true;
            try
            {
                PillCompositionFix.NormalizeVanilla(item);
                Debug.Log("[PrecisionLab] normalized " + item.identifier + " composition to vanilla internal values");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] normalizing pill composition failed: " + e.Message);
            }
            finally
            {
                _fixing = false;
            }
        }
    }
}
