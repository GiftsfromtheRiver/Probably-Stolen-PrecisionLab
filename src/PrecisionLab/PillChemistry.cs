using System;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Two small over-the-counter production lines, merged into the "More Medicines" unlock:
    ///
    /// Contraceptive pills (B1-B4):
    ///   B1 hydrolysis    steroid raw + acid + water + alcohol, 60-80 C -> steroid hydrolysate
    ///   B2 reduction     hydrolysate + reducing agent + (lipobond OR alcohol), 50-70 C
    ///                    -> steroid intermediate
    ///   B3 purification  steroid intermediate -> contraceptive API solution (purifier optional)
    ///   B4 pressing      chem finisher: dry contraceptive API solution -> birth control pill
    ///
    /// Sannai/Zyanide pills (S1-S3):
    ///   S1 extraction    sannai raw + alcohol x2, 50-70 C -> sannai extract (alcohol is a
    ///                    recoverable extraction solvent; evaporate it in the evaporator to
    ///                    concentrate the extract)
    ///   S2 purification  extract + trace acid (catalyst), 26-60 C -> sannai API solution
    ///                    (purifier optional)
    ///   S3 pressing      chem finisher: dry sannai API solution -> zyanide (Sannai) pill
    /// </summary>
    internal static class PillChemistry
    {
        private const double AmbientTemp = 26.0;

        internal static int React(GameItem container)
        {
            if (container == null || WaterHelper.IsEmpty(container))
            {
                return 0;
            }
            int steps = 0;
            try
            {
                steps += StepHydrolysis(container);
                steps += StepReduction(container);
                steps += StepContraceptiveApi(container);
                steps += StepExtraction(container);
                steps += StepSannaiApi(container);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] pill reaction tick failed: " + e.Message);
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

        /// <summary>B1: steroid raw + acid + water + alcohol (60-80 C) -> steroid hydrolysate.</summary>
        internal static int StepHydrolysis(GameItem container)
        {
            double temp = Temp(container);
            if (temp < 60.0 || temp > 80.0)
            {
                return 0;
            }
            int raw = Amount(container, CustomLiquids.SteroidRaw);
            int acid = Amount(container, "acid");
            int water = Amount(container, "water");
            int alcohol = Amount(container, "alcohol");
            int x = Step(Math.Min(Math.Min(raw, acid), Math.Min(water, alcohol)));
            if (x <= 0)
            {
                return 0;
            }
            Add(container, CustomLiquids.SteroidRaw, -x);
            Add(container, "acid", -x);
            Add(container, "water", -x);
            Add(container, "alcohol", -x);
            Add(container, CustomLiquids.SteroidHydrolysate, x);
            return 1;
        }

        /// <summary>B2: hydrolysate + reducing agent + (lipobond OR alcohol), 50-70 C -> intermediate.</summary>
        internal static int StepReduction(GameItem container)
        {
            double temp = Temp(container);
            if (temp < 50.0 || temp > 70.0)
            {
                return 0;
            }
            int hydrolysate = Amount(container, CustomLiquids.SteroidHydrolysate);
            int reducing = Amount(container, "reducing_agent");
            int lipobond = Amount(container, "lipobond_agent");
            int alcohol = Amount(container, "alcohol");
            if (hydrolysate <= 0 || reducing <= 0 || (lipobond <= 0 && alcohol <= 0))
            {
                return 0;
            }
            int x = Step(Math.Min(hydrolysate, reducing));
            if (x <= 0)
            {
                return 0;
            }
            Add(container, CustomLiquids.SteroidHydrolysate, -x);
            Add(container, "reducing_agent", -x);
            if (lipobond > 0)
            {
                Add(container, "lipobond_agent", -x);
            }
            Add(container, CustomLiquids.SteroidIntermediate, x);
            return 1;
        }

        /// <summary>B3: steroid intermediate -> contraceptive API solution (26-60 C).</summary>
        internal static int StepContraceptiveApi(GameItem container)
        {
            double temp = Temp(container);
            if (temp < AmbientTemp || temp > 60.0)
            {
                return 0;
            }
            int intermediate = Amount(container, CustomLiquids.SteroidIntermediate);
            int x = Step(intermediate);
            if (x <= 0)
            {
                return 0;
            }
            Add(container, CustomLiquids.SteroidIntermediate, -x);
            Add(container, "contraceptive", x);
            return 1;
        }

        /// <summary>S1: sannai raw + alcohol x2 (50-70 C) -> sannai extract (alcohol is not consumed).</summary>
        internal static int StepExtraction(GameItem container)
        {
            double temp = Temp(container);
            if (temp < 50.0 || temp > 70.0)
            {
                return 0;
            }
            int raw = Amount(container, CustomLiquids.SannaiRaw);
            int alcohol = Amount(container, "alcohol");
            if (raw <= 0)
            {
                return 0;
            }
            int x = Step(raw);
            if (x <= 0 || alcohol < x * 2)
            {
                return 0;
            }
            Add(container, CustomLiquids.SannaiRaw, -x);
            Add(container, CustomLiquids.SannaiExtract, x);
            return 1;
        }

        /// <summary>S2: extract + trace acid catalyst (26-60 C) -> sannai API solution.</summary>
        internal static int StepSannaiApi(GameItem container)
        {
            double temp = Temp(container);
            if (temp < AmbientTemp || temp > 60.0)
            {
                return 0;
            }
            int extract = Amount(container, CustomLiquids.SannaiExtract);
            int acid = Amount(container, "acid");
            if (extract <= 0 || acid <= 0)
            {
                return 0;
            }
            int x = Step(extract);
            if (x <= 0)
            {
                return 0;
            }
            Add(container, CustomLiquids.SannaiExtract, -x);
            Add(container, "zyanide", x);
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

        private static int Step(int limiting)
        {
            return ReactionMath.Step(limiting);
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
