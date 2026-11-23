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
                    int concentrate = Amount(container, CustomLiquids.NeuroConcentrate);
                    int alcohol = Amount(container, "alcohol");
                    int sum = concentrate + alcohol;
                    valid = sum >= 5000
                        && concentrate * 100 >= sum * 40
                        && alcohol * 100 >= sum * 40;
                    message = "比例不对：神经活性香水需要浓缩液与酒精大致各半（各 ≥40%），总量 ≥5000 微升。";
                }
                else if (recipeId == PerfumeContent.PheromonePerfumeId)
                {
                    int concentrate = Amount(container, CustomLiquids.PheromoneConcentrate);
                    int lipobond = Amount(container, "lipobond_agent");
                    int alcohol = Amount(container, "alcohol");
                    int sum = concentrate + lipobond + alcohol;
                    valid = sum >= 5000
                        && concentrate * 100 >= sum * 25
                        && lipobond * 100 >= sum * 25
                        && alcohol * 100 >= sum * 25;
                    message = "比例不对：费洛蒙香水需要原液/脂合剂/酒精大致三等分（各 ≥25%），总量 ≥5000 微升。";
                }
                else if (recipeId == BioPharmaContent.EssenceId)
                {
                    int essence = Amount(container, CustomLiquids.EssenceConcentrate);
                    int total = WaterHelper.GetTotalVolume(container);
                    valid = total >= BioPharmaContent.EssencePressVolume
                        && essence * 100 >= total * 99;
                    message = "纯度不足：「永恒」焕活精华液要求焕活精华原液纯度 ≥99%，且总量 ≥5000 微升。";
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

        private static bool TotalAtLeast(GameItem container, int amount)
        {
            return WaterHelper.GetTotalVolume(container) >= amount;
        }

        private static int Amount(GameItem container, string liquidId)
        {
            string part = Liquid.GetCurrentPartFromId(liquidId);
            return part != null ? container.GetTagReadonly(part).GetInt() : 0;
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
