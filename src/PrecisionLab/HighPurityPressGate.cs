using System;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// End-game purity gate for the two most valuable presses: the Naloxgone injector and the
    /// Synaprest injector. Their Chem Finisher recipes only accept containers whose main active
    /// ingredient is at least 99% of the total content, so contaminated/impure batches cannot be
    /// pressed into finished products. The check runs through MachineChemFinisher.CanOutput, which
    /// gates both the "output" button and the machine status line.
    /// </summary>
    internal static class HighPurityPressGate
    {
        internal const double MinimumPurity = 99.0;

        internal static bool IsRestrictedRecipe(string identifier)
        {
            return identifier == "pure_white_injector"
                || identifier == "black_injector"
                || identifier == "blue_blood_bag" // Blue Blood: below 99% it is not synthesised
                || identifier == OrdnanceContent.VanillaFlashbangId
                || identifier == OrdnanceContent.VanillaSmokeId
                || identifier == OrdnanceContent.VanillaC4Id; // wrong ordnance composition must not output
        }

        internal static double GetActivePurity(GameItem container, string activeIngredientId)
        {
            if (container == null || string.IsNullOrEmpty(activeIngredientId))
            {
                return 0.0;
            }
            try
            {
                string part = Liquid.GetCurrentPartFromId(activeIngredientId);
                if (part == null)
                {
                    return 0.0;
                }
                int active = container.GetTagReadonly(part).GetInt();
                // Chem Finisher inputs are LIQUID_CONTAINER items, not CHEM_ITEMs; use the water/
                // liquid total (same API vanilla ChemFinisherRecipe.CanOutput uses).
                int total = WaterHelper.GetTotalVolume(container);
                return total > 0 ? active * 100.0 / total : 0.0;
            }
            catch (Exception)
            {
                return 0.0;
            }
        }
    }

    [HarmonyPatch(typeof(MachineChemFinisher), "CanOutput")]
    internal static class HighPurityPressGatePatch
    {
        private static float _lastNotifyTime = -10f;
        private static string _lastNotifyRecipe = "";

        private static bool Prefix(GameItem machine, GameSlotInventory containerSlot, ref bool __result)
        {
            try
            {
                if (machine == null || containerSlot == null || containerSlot.childItem == null)
                {
                    return true;
                }
                if (!machine.IsTag("SELECTED_CHEM_ID_TAG"))
                {
                    return true;
                }
                string recipeId = machine.GetTagReadonly("SELECTED_CHEM_ID_TAG").GetString();
                if (!HighPurityPressGate.IsRestrictedRecipe(recipeId))
                {
                    return true;
                }
                ChemFinisherRecipe recipe = ChemFinisherRecipe.GetRecipeById(recipeId);
                if (recipe == null)
                {
                    return true;
                }
                double purity = HighPurityPressGate.GetActivePurity(containerSlot.childItem, recipe.activeIngredientId);
                if (purity + 1e-6 >= HighPurityPressGate.MinimumPurity)
                {
                    return true;
                }
                __result = false;
                NotifyOnce(recipeId, purity);
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] purity gate check failed: " + e.Message);
                return true;
            }
        }

        private static void NotifyOnce(string recipeId, double purity)
        {
            try
            {
                ChemFinisherUIManager ui = ChemFinisherUIManager.Instance;
                if (ui == null || ui.UIPanel == null || !ui.UIPanel.activeSelf)
                {
                    return;
                }
                float now = Time.realtimeSinceStartup;
                if (_lastNotifyRecipe == recipeId && now - _lastNotifyTime < 4f)
                {
                    return;
                }
                _lastNotifyTime = now;
                _lastNotifyRecipe = recipeId;
                string message = Loc.Fmt(
                    "mech.chem.purity_too_low",
                    "纯度过低：主成分 {0:F1}%，需要 ≥99% 才能压制（先把杂质去掉）。",
                    purity);
                StoreUIManager.Instance.Notify(message, "default");
            }
            catch (Exception)
            {
                // Notifications are cosmetic; never break pressing.
            }
        }
    }
}
