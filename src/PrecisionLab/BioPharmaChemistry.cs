using System;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Bio-pharma lines: licensed/unlicensed phagimycin pills and the "Eternal" stem-cell essence.
    /// No blender is used; reactions run in containers, purification in the purifier, filling in
    /// the Chem Finisher.
    /// </summary>
    internal static class BioPharmaChemistry
    {
        internal static int React(GameItem container)
        {
            if (container == null || WaterHelper.IsEmpty(container))
            {
                return 0;
            }
            int steps = 0;
            try
            {
                steps += StepSymbioticCulture(container);
                steps += StepAntibioticPurify(container);
                steps += StepCellActivation(container);
                steps += StepEssencePurify(container);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] bio-pharma reaction tick failed: " + e.Message);
            }
            if (steps > 0)
            {
                try
                {
                    ItemFeature feature = WaterFeatureHelper.GetWaterFeature(container);
                    WaterFeatureHelper.OnLiquidChanged(container, feature.IsExposed());
                }
                catch (Exception)
                {
                    // Diagnostics containers have no water feature yet.
                }
            }
            return steps;
        }

        /// <summary>M1: phage culture + fungal extract + water at ambient -> symbiotic culture.</summary>
        internal static int StepSymbioticCulture(GameItem container)
        {
            double temp = Temp(container);
            if (temp < 26.0 || temp > 40.0)
            {
                return 0;
            }
            int phage = Amount(container, CustomLiquids.PhageCulture);
            int fungus = Amount(container, CustomLiquids.FungalExtract);
            int water = Amount(container, "water");
            int x = ReactionMath.Step(Math.Min(Math.Min(phage, fungus), water));
            if (x <= 0)
            {
                return 0;
            }
            Add(container, CustomLiquids.PhageCulture, -x);
            Add(container, CustomLiquids.FungalExtract, -x);
            Add(container, "water", -x);
            Add(container, CustomLiquids.SymbioticCulture, x);
            Add(container, "chemical_contaminant", Math.Max(1, x / 25));
            return 1;
        }

        /// <summary>M2: >=99% symbiotic culture -> pure antibiotic API (purifier required).</summary>
        internal static int StepAntibioticPurify(GameItem container)
        {
            double temp = Temp(container);
            if (temp < 26.0 || temp > 40.0)
            {
                return 0;
            }
            int symbiotic = Amount(container, CustomLiquids.SymbioticCulture);
            if (symbiotic <= 0)
            {
                return 0;
            }
            int total = WaterHelper.GetTotalVolume(container);
            int valuable = symbiotic + Amount(container, CustomLiquids.PureAntibioticApi);
            if (total <= 0 || valuable * 100.0 / total < 99.0)
            {
                return 0;
            }
            int x = ReactionMath.Step(symbiotic);
            if (x <= 0)
            {
                return 0;
            }
            Add(container, CustomLiquids.SymbioticCulture, -x);
            Add(container, CustomLiquids.PureAntibioticApi, x);
            return 1;
        }

        /// <summary>L1: stem cells x2 + water + alcohol at <=30 C -> active cell suspension.</summary>
        internal static int StepCellActivation(GameItem container)
        {
            double temp = Temp(container);
            if (temp < 26.0)
            {
                return 0;
            }
            int stem = Amount(container, CustomLiquids.StemCell);
            int water = Amount(container, "water");
            int alcohol = Amount(container, "alcohol");
            int x = ReactionMath.Step(Math.Min(stem / 2, Math.Min(water, alcohol)));
            if (x <= 0)
            {
                return 0;
            }
            Add(container, CustomLiquids.StemCell, -x * 2);
            Add(container, "water", -x);
            Add(container, "alcohol", -x);
            if (temp > 30.0)
            {
                Add(container, CustomLiquids.DeadCellWaste, x);
            }
            else
            {
                Add(container, CustomLiquids.ActiveCellSuspension, x);
            }
            return 1;
        }

        /// <summary>L2: active cells + lipobond at <=30 C -> essence concentrate.</summary>
        internal static int StepEssencePurify(GameItem container)
        {
            double temp = Temp(container);
            if (temp < 26.0)
            {
                return 0;
            }
            int active = Amount(container, CustomLiquids.ActiveCellSuspension);
            int lipobond = Amount(container, "lipobond_agent");
            int x = ReactionMath.Step(Math.Min(active, lipobond));
            if (x <= 0)
            {
                return 0;
            }
            Add(container, CustomLiquids.ActiveCellSuspension, -x);
            Add(container, "lipobond_agent", -x);
            if (temp > 30.0)
            {
                Add(container, CustomLiquids.DeadCellWaste, x);
            }
            else
            {
                Add(container, CustomLiquids.EssenceConcentrate, x);
            }
            return 1;
        }

        private static double Temp(GameItem container)
        {
            return container.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble();
        }

        private static int Amount(GameItem container, string liquidId)
        {
            return LiquidTools.GetLiquidAmount(container, liquidId);
        }

        private static void Add(GameItem container, string liquidId, int delta)
        {
            if (delta == 0)
            {
                return;
            }
            string part = Liquid.GetCurrentPartFromId(liquidId);
            if (part == null)
            {
                return;
            }
            int current = container.GetTagReadonly(part).GetInt();
            int next = Math.Max(0, current + delta);
            container.ModifyTag(part, delegate (TagState state)
            {
                state.Enable().SetInt(next);
            }, skipValidate: true);
        }
    }
}
