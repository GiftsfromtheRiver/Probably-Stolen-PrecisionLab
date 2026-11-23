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

        internal static bool NeedsNormalize(GameItem item)
        {
            if (!IsPill(item))
            {
                return false;
            }
            try
            {
                return ChemicalHelper.GetTotalVolume(item) > 0;
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

            // Preserve the purity the pill was actually created at. A pure press (100%) must result
            // in a single-component, lab-pure pill; an impure press keeps only the relevant active
            // amount and puts the remainder into one contaminant entry. Both the vanilla active
            // liquid (new chains) and the older custom API liquids are counted, so old saves also
            // normalize correctly.
            double purity = 0.0;
            try
            {
                int total = ChemicalHelper.GetTotalVolume(item);
                int activeTotal = 0;
                string[] activeIds = item.identifier == "birth_control_pill"
                    ? new[] { "contraceptive", CustomLiquids.ContraceptiveApi }
                    : new[] { "zyanide", CustomLiquids.SannaiApi };
                for (int i = 0; i < activeIds.Length; i++)
                {
                    string part = Liquid.GetCurrentPartFromId(activeIds[i]);
                    if (part != null)
                    {
                        activeTotal += item.GetTagReadonly(part).GetInt();
                    }
                }
                if (total > 0) purity = Math.Min(100.0, activeTotal * 100.0 / total);
            }
            catch (Exception)
            {
                purity = 0.0;
            }

            ChemicalHelper.RemoveAllLiquid(item);
            item.ModifyTag("ACTIVATE_INGREDIENT_TAG", delegate (TagState state)
            {
                state.Enable().SetString(active);
            });
            int activeAmount = (int)Math.Round(volume * purity / 100.0);
            activeAmount = Math.Max(0, Math.Min(volume, activeAmount));
            if (activeAmount > 0)
            {
                ChemicalHelper.SetChemContentUL(item, active, activeAmount);
            }
            int remainder = volume - activeAmount;
            if (remainder > 0)
            {
                ChemicalHelper.SetChemContentUL(item, "physical_contaminant", remainder);
            }
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
            if (!PillCompositionFix.NeedsNormalize(item))
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
