using System;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Adrenaline production chain (six reaction steps), resolved once per second by the same
    /// temperature tick that drives the vanilla chemistry (ChemicalHelper.HandleTemperatureReaction).
    ///
    /// All intermediates are liquids so they can live in normal containers and flow through the
    /// vanilla machines: reagents are poured / blended in, the container is heated on the heating
    /// plate, solvents are removed in the evaporator, and the final solid products are pressed by
    /// the Chem Finisher (recipes are appended in <see cref="AdrenalineContent"/>).
    ///
    ///   1. amination    catechol + N-hydroxymethyl base + lipobond agent, 26-60 °C
    ///                   -> amination intermediate
    ///   2. reduction    amination intermediate + reducing agent, 60-80 °C
    ///                   -> racemic adrenaline
    ///   3. resolution   racemic adrenaline + resolving agent + acid, 40-60 °C
    ///                   -> levo-adrenaline salt + dextro waste (acid is a catalyst)
    ///   4. free base    levo-adrenaline salt + base, 26-60 °C
    ///                   -> adrenaline free base
    ///      HCl salt    adrenaline free base + acid, 26-60 °C
    ///                   -> adrenaline HCl (the chem finisher presses it into the API item)
    ///   5. solution     adrenaline HCl + water -> adrenaline solution
    ///   6. filling      chem finisher: adrenaline solution -> adrenaline injection pen
    /// </summary>
    internal static class AdrenalineChemistry
    {
        private const double AmbientTemp = 26.0;

        /// <summary>Runs every applicable step; returns the number of steps that converted anything.</summary>
        internal static int React(GameItem container)
        {
            if (container == null || WaterHelper.IsEmpty(container))
            {
                return 0;
            }
            int steps = 0;
            try
            {
                steps += StepAmination(container);
                steps += StepReduction(container);
                steps += StepResolution(container);
                steps += StepFreebase(container);
                steps += StepHcl(container);
                steps += StepSolution(container);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] adrenaline reaction tick failed: " + e.Message);
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

        // ------------------------------------------------------------------
        // Step implementations (return > 0 when they converted something).
        // ------------------------------------------------------------------

        internal static int StepAmination(GameItem container)
        {
            double temp = Temp(container);
            if (temp < AmbientTemp || temp > 60.0)
            {
                return 0;
            }
            int catechol = Amount(container, CustomLiquids.Catechol);
            int baseSolution = Amount(container, CustomLiquids.HydroxymethylBase);
            int lipobond = Amount(container, "lipobond_agent");
            int limiting = Math.Min(Math.Min(catechol, baseSolution), lipobond);
            int x = Step(limiting);
            if (x <= 0)
            {
                return 0;
            }
            Add(container, CustomLiquids.Catechol, -x);
            Add(container, CustomLiquids.HydroxymethylBase, -x);
            Add(container, "lipobond_agent", -x);
            Add(container, CustomLiquids.AminationIntermediate, x);
            return 1;
        }

        internal static int StepReduction(GameItem container)
        {
            double temp = Temp(container);
            if (temp < 60.0 || temp > 80.0)
            {
                return 0;
            }
            int intermediate = Amount(container, CustomLiquids.AminationIntermediate);
            int reducing = Amount(container, "reducing_agent");
            int x = Step(Math.Min(intermediate, reducing));
            if (x <= 0)
            {
                return 0;
            }
            Add(container, CustomLiquids.AminationIntermediate, -x);
            Add(container, "reducing_agent", -x);
            Add(container, CustomLiquids.RacemicAdrenaline, x);
            return 1;
        }

        internal static int StepResolution(GameItem container)
        {
            double temp = Temp(container);
            if (temp < 40.0 || temp > 60.0)
            {
                return 0;
            }
            int racemic = Amount(container, CustomLiquids.RacemicAdrenaline);
            int resolving = Amount(container, CustomLiquids.ResolvingAgent);
            int acid = Amount(container, "acid");
            if (acid <= 0)
            {
                return 0;
            }
            int limiting = Math.Min(racemic, resolving);
            int x = Step(limiting);
            if (x <= 0)
            {
                return 0;
            }
            // Keep the two enantiomers split 50/50 without a separate carry tag: base the
            // rounding on the *cumulative* amount already produced, so thousands of small
            // odd-litre conversions cannot drift into a 100+ µL imbalance.
            int alreadyLevo = Amount(container, CustomLiquids.LevoAdrenalineSalt);
            int alreadyDextro = Amount(container, CustomLiquids.DextroWaste);
            int producedSoFar = alreadyLevo + alreadyDextro;
            int targetLevo = (producedSoFar + x) / 2;
            int addLevo = Math.Max(0, targetLevo - alreadyLevo);
            addLevo = Math.Min(addLevo, x);
            int addDextro = x - addLevo;
            Add(container, CustomLiquids.RacemicAdrenaline, -x);
            Add(container, CustomLiquids.ResolvingAgent, -x);
            Add(container, CustomLiquids.LevoAdrenalineSalt, addLevo);
            Add(container, CustomLiquids.DextroWaste, addDextro);
            return 1;
        }

        internal static int StepFreebase(GameItem container)
        {
            double temp = Temp(container);
            if (temp < AmbientTemp || temp > 60.0)
            {
                return 0;
            }
            int salt = Amount(container, CustomLiquids.LevoAdrenalineSalt);
            int baseAmount = Amount(container, "base");
            int x = Step(Math.Min(salt, baseAmount));
            if (x <= 0)
            {
                return 0;
            }
            Add(container, CustomLiquids.LevoAdrenalineSalt, -x);
            Add(container, "base", -x);
            Add(container, CustomLiquids.AdrenalineFreebase, x);
            return 1;
        }

        internal static int StepHcl(GameItem container)
        {
            double temp = Temp(container);
            if (temp < AmbientTemp || temp > 60.0)
            {
                return 0;
            }
            int freebase = Amount(container, CustomLiquids.AdrenalineFreebase);
            int acid = Amount(container, "acid");
            int x = Step(Math.Min(freebase, acid));
            if (x <= 0)
            {
                return 0;
            }
            Add(container, CustomLiquids.AdrenalineFreebase, -x);
            Add(container, "acid", -x);
            Add(container, CustomLiquids.AdrenalineHcl, x);
            return 1;
        }

        internal static int StepSolution(GameItem container)
        {
            int hcl = Amount(container, CustomLiquids.AdrenalineHcl);
            int water = Amount(container, "water");
            int x = Math.Min(Math.Min(hcl, water), 5000);
            if (x <= 0)
            {
                return 0;
            }
            Add(container, CustomLiquids.AdrenalineHcl, -x);
            Add(container, "water", -x);
            Add(container, CustomLiquids.AdrenalineSolution, x);
            return 1;
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static double Temp(GameItem container)
        {
            return container.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble();
        }

        private static int Amount(GameItem container, string liquidId)
        {
            return LiquidTools.GetLiquidAmount(container, liquidId);
        }

        /// <summary>2% of the limiting reagent per one-second tick, clamped to a sane range.</summary>
        private static int Step(int limiting)
        {
            if (limiting <= 0)
            {
                return 0;
            }
            // Convert everything in one final chunk once the remainder is small: this keeps the
            // integer round-off from leaving 1 µL tails of expensive reagents behind.
            if (limiting <= 50)
            {
                return limiting;
            }
            int amount = Math.Max(1, limiting / 50);
            return Math.Min(Math.Min(amount, 5000), limiting);
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
