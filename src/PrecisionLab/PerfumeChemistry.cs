using System;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Two perfume lines, both avoiding the blender: reactions happen in liquid containers on the
    /// evaporator/hotplate, purification in the purifier, filling in the Chem Finisher.
    ///
    ///   P1 neuro activation   aromatic extract + water + trace acid (30-50 C)
    ///                         -> activated fragrance + ~4% chemical contaminant
    ///   P2 neuro purification activated fragrance must reach >=99% purity -> neuroactive concentrate
    ///   P3 neuro filling      concentrate + alcohol pressed into vanilla neuroactive_perfume
    ///
    ///   Q1 pheromone cold     crude pheromone + alcohol (<=30 C) -> purified pheromone + waste
    ///   Q2 pheromone modify   purified pheromone + N-hydroxymethyl base (<=30 C) -> pheromone concentrate
    ///   Q3 pheromone filling  concentrate + lipobond + alcohol -> vanilla pheromone_perfume
    ///
    /// Pheromone overheating denatures the protein and produces only waste.
    /// </summary>
    internal static class PerfumeChemistry
    {
        internal const double NeuroMinTemp = 30.0;
        internal const double NeuroMaxTemp = 50.0;
        internal const double PheromoneMaxTemp = 30.0;
        internal const double NeuroMinPurity = 99.0;

        internal static int React(GameItem container)
        {
            if (container == null || WaterHelper.IsEmpty(container))
            {
                return 0;
            }
            int steps = 0;
            try
            {
                steps += StepNeuroActivation(container);
                steps += StepNeuropePurify(container);
                steps += StepPheromoneColdPurify(container);
                steps += StepPheromoneModify(container);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] perfume reaction tick failed: " + e.Message);
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

        internal static int StepNeuroActivation(GameItem container)
        {
            double temp = Temp(container);
            if (temp < NeuroMinTemp || temp > NeuroMaxTemp)
            {
                return 0;
            }
            int aromatic = Amount(container, CustomLiquids.AromaticExtract);
            int water = Amount(container, "water");
            int acid = Amount(container, "acid");
            if (aromatic <= 0 || water <= 0 || acid <= 0)
            {
                return 0;
            }
            int x = ReactionMath.Step(Math.Min(aromatic, water));
            if (x <= 0)
            {
                return 0;
            }
            Add(container, CustomLiquids.AromaticExtract, -x);
            Add(container, "water", -x);
            Add(container, CustomLiquids.ActivatedFragrance, x);
            // The purifier is required to remove this irritant fraction.
            Add(container, "chemical_contaminant", Math.Max(1, x / 25));
            return 1;
        }

        internal static int StepNeuropePurify(GameItem container)
        {
            double temp = Temp(container);
            if (temp < 26.0 || temp > NeuroMaxTemp)
            {
                return 0;
            }
            int activated = Amount(container, CustomLiquids.ActivatedFragrance);
            if (activated <= 0)
            {
                return 0;
            }
            int total = WaterHelper.GetTotalVolume(container);
            int valuable = activated + Amount(container, CustomLiquids.NeuroConcentrate);
            if (total <= 0 || valuable * 100.0 / total < NeuroMinPurity)
            {
                return 0;
            }
            int x = ReactionMath.Step(activated);
            if (x <= 0)
            {
                return 0;
            }
            Add(container, CustomLiquids.ActivatedFragrance, -x);
            Add(container, CustomLiquids.NeuroConcentrate, x);
            return 1;
        }

        internal static int StepPheromoneColdPurify(GameItem container)
        {
            double temp = Temp(container);
            if (temp < 26.0)
            {
                return 0;
            }
            int crude = Amount(container, CustomLiquids.CrudePheromone);
            int alcohol = Amount(container, "alcohol");
            int x = ReactionMath.Step(Math.Min(crude, alcohol));
            if (x <= 0)
            {
                return 0;
            }
            Add(container, CustomLiquids.CrudePheromone, -x);
            Add(container, "alcohol", -x);
            if (temp > PheromoneMaxTemp)
            {
                Add(container, CustomLiquids.PheromoneWaste, x);
            }
            else
            {
                Add(container, CustomLiquids.PurifiedPheromone, x);
                // Safe cold processing leaves a normal byproduct, not denatured protein.
                Add(container, CustomLiquids.PheromoneByproduct, Math.Max(1, x / 5));
            }
            return 1;
        }

        internal static int StepPheromoneModify(GameItem container)
        {
            double temp = Temp(container);
            if (temp < 26.0)
            {
                return 0;
            }
            int purified = Amount(container, CustomLiquids.PurifiedPheromone);
            int baseSolution = Amount(container, CustomLiquids.HydroxymethylBase);
            int x = ReactionMath.Step(Math.Min(purified, baseSolution));
            if (x <= 0)
            {
                return 0;
            }
            Add(container, CustomLiquids.PurifiedPheromone, -x);
            Add(container, CustomLiquids.HydroxymethylBase, -x);
            if (temp > PheromoneMaxTemp)
            {
                Add(container, CustomLiquids.PheromoneWaste, x);
            }
            else
            {
                Add(container, CustomLiquids.PheromoneConcentrate, x);
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
