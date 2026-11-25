using System;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Old saves contain cocktail bottles made before dynamic value/purity was introduced: the
    /// factory item has unitValue 0 and vanilla GradeProduct only measured one concentrate
    /// (typically ~33%). Re-normalise those bottles on load/wake.
    /// </summary>
    internal static class AntibioticMigration
    {
        internal static void EnsureAll()
        {
            try
            {
                PlayerStore store = PlayerStore.Instance;
                if (store == null) return;
                var items = store.FindAllItem();
                if (items == null) return;
                int fixedCount = 0;
                foreach (GameItem item in items)
                {
                    if (item == null || item.identifier != AntibioticContent.CocktailId) continue;
                    if (Normalize(item)) fixedCount++;
                }
                if (fixedCount > 0)
                    Debug.Log("[PrecisionLab] normalized " + fixedCount + " legacy cocktail antibiotic bottle(s)");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] cocktail migration failed: " + e.Message);
            }
        }

        private static bool Normalize(GameItem item)
        {
            try
            {
                int broad = LiquidTools.GetLiquidAmount(item, CustomLiquids.AntibioticConcentrateBroad);
                int pos = LiquidTools.GetLiquidAmount(item, CustomLiquids.AntibioticConcentrateGramPos);
                int neg = LiquidTools.GetLiquidAmount(item, CustomLiquids.AntibioticConcentrateGramNeg);
                bool oldB = item.IsTag("ANTIBIOTIC_CONTAINS_BROAD");
                bool oldP = item.IsTag("ANTIBIOTIC_CONTAINS_GRAM_POS");
                bool oldN = item.IsTag("ANTIBIOTIC_CONTAINS_GRAM_NEG");
                bool hasTags = oldB || oldP || oldN;

                if (broad <= 0 && pos <= 0 && neg <= 0 && !hasTags) return false;

                AntibioticOutputPatch.ClearContainsTags(item);
                if (broad >= 500 || (broad <= 0 && pos <= 0 && neg <= 0 && oldB)) item.EnableTag("ANTIBIOTIC_CONTAINS_BROAD");
                if (pos >= 500 || (broad <= 0 && pos <= 0 && neg <= 0 && oldP)) item.EnableTag("ANTIBIOTIC_CONTAINS_GRAM_POS");
                if (neg >= 500 || (broad <= 0 && pos <= 0 && neg <= 0 && oldN)) item.EnableTag("ANTIBIOTIC_CONTAINS_GRAM_NEG");

                int ingredientValue = (item.IsTag("ANTIBIOTIC_CONTAINS_BROAD") ? AntibioticContent.BroadValue : 0)
                    + (item.IsTag("ANTIBIOTIC_CONTAINS_GRAM_POS") ? AntibioticContent.NarrowValue : 0)
                    + (item.IsTag("ANTIBIOTIC_CONTAINS_GRAM_NEG") ? AntibioticContent.NarrowValue : 0);
                if (ingredientValue <= 0) return false;

                item.unitValue = (long)Math.Round(ingredientValue * 1.1);
                item.ModifyTag("ANTIBIOTIC_INGREDIENT_VALUE", delegate (TagState state)
                {
                    state.Enable().SetInt(ingredientValue);
                });

                double purity = 100.0;
                int total = ChemicalHelper.GetTotalVolume(item);
                int effective = broad + pos + neg;
                if (total > 0 && effective > 0)
                    purity = Math.Min(100.0, effective * 100.0 / total);
                AntibioticOutputPatch.ApplyCocktailPurity(item, purity);
                AntibioticOutputPatch.EnsureCocktailComposition(item);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] legacy cocktail normalize failed: " + e.Message);
                return false;
            }
        }
    }
}
