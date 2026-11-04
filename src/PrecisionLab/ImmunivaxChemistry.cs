using System;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Immunivax (免疫宁) production chain. Alcohol precipitates the serum proteins at room
    /// temperature, then the alcohol is removed in a narrow 26-40 °C window; overheating denatures
    /// the protein and produces unusable waste. The remaining steps are a buffer/dialysis
    /// conversion and a sterile press into the vanilla Immunivax injector.
    ///
    ///   I1 precipitation  immune serum x2 + alcohol x2 (26-40 C) -> immunoglobulin suspension
    ///   I2 concentration  suspension (26-40 C) -> crude immunoglobulin + recovered alcohol
    ///                     suspension (>40 C)   -> denatured protein waste
    ///   I3 dialysis       crude immunoglobulin + water (26-40 C) -> Immunivax API solution
    ///   I4 filling        chem finisher: dry API solution (5000 uL) -> vanilla Immunivax injector
    /// </summary>
    internal static class ImmunivaxChemistry
    {
        internal const double AmbientTemp = 26.0;
        internal const double MaxSafeTemp = 40.0;

        internal static int React(GameItem container)
        {
            if (container == null || WaterHelper.IsEmpty(container))
            {
                return 0;
            }
            int steps = 0;
            try
            {
                steps += StepPrecipitation(container);
                steps += StepConcentration(container);
                steps += StepDialysis(container);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] immunivax reaction tick failed: " + e.Message);
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

        /// <summary>I1: immune serum x2 + alcohol x2 at ambient -> immunoglobulin suspension.</summary>
        internal static int StepPrecipitation(GameItem container)
        {
            double temp = Temp(container);
            if (temp < AmbientTemp || temp > MaxSafeTemp)
            {
                return 0;
            }
            int serum = Amount(container, CustomLiquids.ImmuneSerum);
            int alcohol = Amount(container, "alcohol");
            if (serum <= 0 || alcohol <= 0)
            {
                return 0;
            }
            int x = ReactionMath.Step(Math.Min(serum, alcohol) / 2);
            if (x <= 0)
            {
                return 0;
            }
            Add(container, CustomLiquids.ImmuneSerum, -x * 2);
            Add(container, "alcohol", -x * 2);
            Add(container, CustomLiquids.ImmunoglobulinSuspension, x);
            return 1;
        }

        /// <summary>
        /// I2: warm the suspension. 26-40 C evaporates the alcohol and leaves the protein;
        /// above 40 C the protein denatures into waste.
        /// </summary>
        internal static int StepConcentration(GameItem container)
        {
            int suspension = Amount(container, CustomLiquids.ImmunoglobulinSuspension);
            if (suspension <= 0)
            {
                return 0;
            }
            double temp = Temp(container);
            if (temp < AmbientTemp)
            {
                return 0;
            }
            int x = ReactionMath.Step(suspension);
            if (x <= 0)
            {
                return 0;
            }
            if (temp <= MaxSafeTemp)
            {
                Add(container, CustomLiquids.ImmunoglobulinSuspension, -x);
                Add(container, CustomLiquids.CrudeImmunoglobulin, x);
                Add(container, "alcohol", x); // recovered alcohol
                return 1;
            }
            Add(container, CustomLiquids.ImmunoglobulinSuspension, -x);
            Add(container, CustomLiquids.DenaturedWaste, x);
            return 1;
        }

        /// <summary>I3: crude immunoglobulin + water (buffer) at ambient -> Immunivax API solution.</summary>
        internal static int StepDialysis(GameItem container)
        {
            double temp = Temp(container);
            if (temp < AmbientTemp || temp > MaxSafeTemp)
            {
                return 0;
            }
            int crude = Amount(container, CustomLiquids.CrudeImmunoglobulin);
            int water = Amount(container, "water");
            int x = ReactionMath.Step(Math.Min(crude, water));
            if (x <= 0)
            {
                return 0;
            }
            Add(container, CustomLiquids.CrudeImmunoglobulin, -x);
            Add(container, "water", -x);
            Add(container, CustomLiquids.ImmunoglobulinApi, x);
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
