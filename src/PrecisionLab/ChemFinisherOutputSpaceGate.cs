using System;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Prevents the Chem Finisher from consuming the input when its output grid cannot accept the
    /// finished item. Vanilla consumes the liquid during the output callback and only then tries to
    /// place the item, so a full output grid silently ate the input.
    ///
    /// The gate runs in MachineChemFinisher.CanOutput, which the UI calls before enabling the
    /// output button, so both the button and the actual click are blocked before any liquid moves.
    /// </summary>
    [HarmonyPatch(typeof(MachineChemFinisher), "CanOutput")]
    internal static class ChemFinisherOutputSpaceGate
    {
        private static float _lastNotifyTime = -10f;

        private static void Postfix(GameItem machine, GameSlotInventory containerSlot, ref bool __result)
        {
            try
            {
                if (!__result || machine == null || containerSlot == null || containerSlot.childItem == null)
                {
                    return;
                }
                if (!machine.IsTag("SELECTED_CHEM_ID_TAG"))
                {
                    return;
                }
                string recipeId = machine.GetTagReadonly("SELECTED_CHEM_ID_TAG").GetString();
                ChemFinisherRecipe recipe = ChemFinisherRecipe.GetRecipeById(recipeId);
                if (recipe == null)
                {
                    return;
                }
                GameGridInventory outputGrid = FindOutputGrid(machine);
                if (outputGrid == null)
                {
                    return;
                }
                GameItem output = DirectoryMaster.Item(recipeId);
                if (output == null)
                {
                    return;
                }
                bool canAccept = GraphUtils.CanAccept(outputGrid, output);
                output.Destroy();
                if (canAccept)
                {
                    return;
                }
                __result = false;
                NotifyOnce();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] output-space gate failed: " + e.Message);
            }
        }

        private static GameGridInventory FindOutputGrid(GameItem machine)
        {
            if (machine == null || machine.contentWindow == null)
            {
                return null;
            }
            try
            {
                foreach (GameGridInventory grid in GraphUtils.FindChildrenType<GameGridInventory>(machine.contentWindow))
                {
                    if (grid != null
                        && grid.inventoryShape != null
                        && grid.inventoryShape.width == 6
                        && grid.inventoryShape.height == 4)
                    {
                        return grid;
                    }
                }
            }
            catch (Exception)
            {
                // Fall through: unknown window layout means no extra restriction.
            }
            return null;
        }

        private static void NotifyOnce()
        {
            try
            {
                float now = Time.realtimeSinceStartup;
                if (now - _lastNotifyTime < 4f)
                {
                    return;
                }
                _lastNotifyTime = now;
                StoreUIManager.Instance.Notify(
                    Loc.Get("mech.chem.output_space",
                        "输出空间不足：请先清理成型机的输出格，输入原料不会被消耗。"),
                    "default");
            }
            catch (Exception)
            {
                // Cosmetic only.
            }
        }
    }
}
