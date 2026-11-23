using System;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Blue Blood (蓝血) biomedical gel chain. The vanilla item is a bag of injectable bio-gel
    /// ("not actually blood"), so the line is built around room-temperature polymerisation and a
    /// strict purity requirement:
    ///
    ///   B1 sol activation   bio-gel base x2 + water x1 + trace acid (26-40 C) -> acidic gel
    ///   B2 cross-linking    acidic gel + reducing agent + lipobond (26-40 C)
    ///                       -> crude blue gel + 5% chemical contaminant
    ///                       (>40 C -> denatured gel waste)
    ///   B3 purification    crude blue gel with >=99% purity -> medical-grade blue gel
    ///                       (below 99% nothing is synthesised; use the purification machine first)
    ///   B4 filling         chem finisher: dry medical-grade blue gel 5000 uL -> vanilla blue blood bag
    /// </summary>
    internal static class BlueBloodChemistry
    {
        internal const double AmbientTemp = 26.0;
        internal const double MaxSafeTemp = 40.0;
        internal const double MinimumPurity = 99.0;

        internal static int React(GameItem container)
        {
            if (container == null || WaterHelper.IsEmpty(container))
            {
                return 0;
            }
            int steps = 0;
            try
            {
                steps += StepSolubilise(container);
                steps += StepCrosslink(container);
                steps += StepMedicalGrade(container);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] blue blood reaction tick failed: " + e.Message);
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

        /// <summary>B1: bio-gel base x2 + water + trace acid (catalyst) -> acidic gel.</summary>
        internal static int StepSolubilise(GameItem container)
        {
            double temp = Temp(container);
            if (temp < AmbientTemp || temp > MaxSafeTemp)
            {
                return 0;
            }
            int baseGel = Amount(container, CustomLiquids.BioGelBase);
            int water = Amount(container, "water");
            int acid = Amount(container, "acid");
            if (baseGel <= 0 || water <= 0 || acid <= 0)
            {
                return 0;
            }
            int x = ReactionMath.Step(Math.Min(baseGel / 2, water));
            if (x <= 0)
            {
                return 0;
            }
            Add(container, CustomLiquids.BioGelBase, -x * 2);
            Add(container, "water", -x);
            // acid is a trace pH catalyst and is not consumed
            Add(container, CustomLiquids.AcidicGel, x);
            return 1;
        }

        /// <summary>B2: acidic gel + reducing agent + lipobond at 26-40 C -> crude blue gel.</summary>
        internal static int StepCrosslink(GameItem container)
        {
            double temp = Temp(container);
            if (temp < AmbientTemp)
            {
                return 0;
            }
            int acidic = Amount(container, CustomLiquids.AcidicGel);
            int reducing = Amount(container, "reducing_agent");
            int lipobond = Amount(container, "lipobond_agent");
            int x = ReactionMath.Step(Math.Min(Math.Min(acidic, reducing), lipobond));
            if (x <= 0)
            {
                return 0;
            }
            if (temp > MaxSafeTemp)
            {
                Add(container, CustomLiquids.AcidicGel, -x);
                Add(container, "reducing_agent", -x);
                Add(container, "lipobond_agent", -x);
                Add(container, CustomLiquids.DenaturedBlueGelWaste, x);
                return 1;
            }
            Add(container, CustomLiquids.AcidicGel, -x);
            Add(container, "reducing_agent", -x);
            Add(container, "lipobond_agent", -x);
            Add(container, CustomLiquids.CrudeBlueGel, x);
            // 5% chemical contaminant: the purification machine is required to reach 99% purity.
            Add(container, "chemical_contaminant", Math.Max(1, x / 20));
            return 1;
        }

        /// <summary>B3: only convert crude gel to medical grade when its purity is >=99%.</summary>
        internal static int StepMedicalGrade(GameItem container)
        {
            double temp = Temp(container);
            if (temp < AmbientTemp || temp > MaxSafeTemp)
            {
                return 0;
            }
            int crude = Amount(container, CustomLiquids.CrudeBlueGel);
            if (crude <= 0)
            {
                return 0;
            }
            int total = WaterHelper.GetTotalVolume(container);
            // Once conversion starts, earlier converted medical gel is still valuable; count both so
            // the batch can finish instead of stopping after the first tick.
            int valuable = crude + Amount(container, CustomLiquids.MedicalBlueGel);
            double purity = total > 0 ? valuable * 100.0 / total : 0.0;
            if (purity + 1e-6 < MinimumPurity)
            {
                return 0;
            }
            int x = ReactionMath.Step(crude);
            if (x <= 0)
            {
                return 0;
            }
            Add(container, CustomLiquids.CrudeBlueGel, -x);
            Add(container, CustomLiquids.MedicalBlueGel, x);
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
