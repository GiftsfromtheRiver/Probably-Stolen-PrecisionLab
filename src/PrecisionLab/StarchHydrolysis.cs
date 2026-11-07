using System;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Vanilla chemistry has no starch reaction: starch is a chemical liquid that can only be
    /// filtered out by the Chem Finisher. This adds the real-world reaction
    ///
    ///     starch --(acid catalyst, 40-60 °C)--> sugar
    ///
    /// The reaction runs from ChemicalHelper.HandleTemperatureReaction, which every liquid
    /// container ticks once per second (containerTempTick -> HandleAmbientTempDrop +
    /// HandleTemperatureReaction). The acid is a catalyst and is not consumed, matching the
    /// user's requested rule: warm the container to 40-60 °C (heating plate) and the starch
    /// slowly turns into sugar while the acid stays.
    /// </summary>
    [HarmonyPatch(typeof(ChemicalHelper), "HandleTemperatureReaction")]
    internal static class StarchHydrolysisPatch
    {
        private static void Postfix(GameItem container)
        {
            StarchHydrolysis.React(container);
            // The adrenaline and naloxone production chains use the same per-container chemistry tick.
            AdrenalineChemistry.React(container);
            NaloxoneChemistry.React(container);
            PillChemistry.React(container);
            SynaprestChemistry.React(container);
            ImmunivaxChemistry.React(container);
            BlueBloodChemistry.React(container);
            OrdnanceChemistry.React(container);
        }
    }

    internal static class StarchHydrolysis
    {
        internal const double MinTemp = 40.0;
        internal const double MaxTemp = 60.0;

        /// <summary>Fraction of the remaining starch converted per one-second tick.</summary>
        private const double TickConversion = 0.02;

        /// <summary>Upper bound per tick so huge tanks do not convert instantly.</summary>
        private const int MaxPerTick = 5000;

        /// <summary>
        /// Runs one hydrolysis step on a container. Returns the amount of starch converted (µL).
        /// Safe to call from the selftest with an explicitly set CONTAINER_TEMP_TAG.
        /// </summary>
        internal static int React(GameItem container, bool refreshFeature = true)
        {
            try
            {
                if (container == null || WaterHelper.IsEmpty(container))
                {
                    return 0;
                }
                double temperature = container.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble();
                if (temperature < MinTemp || temperature > MaxTemp)
                {
                    return 0;
                }
                int starch = LiquidTools.GetLiquidAmount(container, "starch");
                if (starch <= 0)
                {
                    return 0;
                }
                int acid = LiquidTools.GetLiquidAmount(container, "acid");
                if (acid <= 0)
                {
                    return 0;
                }
                int converted = ReactionMath.Step(starch);
                converted = Math.Min(converted, starch);
                if (converted <= 0)
                {
                    return 0;
                }
                int sugar = LiquidTools.GetLiquidAmount(container, "sugar");
                container.ModifyTag(Liquid.GetCurrentPartFromId("starch"), delegate (TagState state)
                {
                    state.Enable().SetInt(starch - converted);
                }, skipValidate: true);
                container.ModifyTag(Liquid.GetCurrentPartFromId("sugar"), delegate (TagState state)
                {
                    state.Enable().SetInt(sugar + converted);
                }, skipValidate: true);
                if (refreshFeature)
                {
                    try
                    {
                        ItemFeature feature = WaterFeatureHelper.GetWaterFeature(container);
                        WaterFeatureHelper.OnLiquidChanged(container, feature.IsExposed());
                    }
                    catch (Exception)
                    {
                        // Unattached diagnostics containers have no water feature yet.
                    }
                }
                return converted;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] starch hydrolysis tick failed: " + e.Message);
                return 0;
            }
        }

        /// <summary>True when a container currently satisfies the hydrolysis conditions.</summary>
        internal static bool IsReactive(GameItem container)
        {
            try
            {
                if (container == null || WaterHelper.IsEmpty(container))
                {
                    return false;
                }
                double temperature = container.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble();
                return temperature >= MinTemp
                    && temperature <= MaxTemp
                    && LiquidTools.GetLiquidAmount(container, "starch") > 0
                    && LiquidTools.GetLiquidAmount(container, "acid") > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
