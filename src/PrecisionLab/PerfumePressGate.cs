using System;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Ingredient gate for the perfume filling recipes. The Chem Finisher only looks at the total
    /// volume, so without this a bottle of concentrate alone would produce perfume without the
    /// alcohol carrier (and pheromone perfume without its fixative). The gate runs in CanOutput,
    /// before any liquid is consumed.
    /// </summary>
    [HarmonyPatch(typeof(MachineChemFinisher), "CanOutput")]
    internal static class PerfumePressGate
    {
        private static string _lastRecipe = "";
        private static float _lastNotify = -10f;

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
                GameItem container = containerSlot.childItem;
                bool valid;
                string message;
                if (recipeId == PerfumeContent.NeuroPerfumeId)
                {
                    valid = Has(container, CustomLiquids.NeuroConcentrate, 5000)
                        && Has(container, "alcohol", 5000);
                    message = "缺少成分：神经活性香水需要神经活性浓缩液 + 酒精各至少 5000 微升。";
                }
                else if (recipeId == PerfumeContent.PheromonePerfumeId)
                {
                    valid = Has(container, CustomLiquids.PheromoneConcentrate, 5000)
                        && Has(container, "lipobond_agent", 5000)
                        && Has(container, "alcohol", 5000);
                    message = "缺少成分：费洛蒙香水需要费洛蒙浓缩液 + 脂合剂 + 酒精各至少 5000 微升。";
                }
                else
                {
                    return true;
                }
                if (valid)
                {
                    return true;
                }
                __result = false;
                Notify(recipeId, message);
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] perfume press gate failed: " + e.Message);
                return true;
            }
        }

        private static bool Has(GameItem container, string liquidId, int amount)
        {
            string part = Liquid.GetCurrentPartFromId(liquidId);
            return part != null && container.GetTagReadonly(part).GetInt() >= amount;
        }

        private static void Notify(string recipeId, string message)
        {
            try
            {
                float now = Time.realtimeSinceStartup;
                if (_lastRecipe == recipeId && now - _lastNotify < 4f)
                {
                    return;
                }
                _lastRecipe = recipeId;
                _lastNotify = now;
                StoreUIManager.Instance.Notify(message, "default");
            }
            catch (Exception)
            {
                // Cosmetic only.
            }
        }
    }
}
