using System;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Naloxone production chain (merged into the "More Medicines" unlock).
    ///
    /// Thebaine is the only purchased precursor; every intermediate is a liquid that is generated
    /// by the reactions themselves, so the player only has to move liquid and set temperatures:
    ///
    ///   N1 reduction       thebaine + acid + reducing agent + (lipobond agent OR alcohol), 40-60 C
    ///                      -> reduction intermediate
    ///   N2 hydrolysis      reduction intermediate + acid + water, 80-100 C
    ///                      -> hydrolysis intermediate
    ///   N3 N-allylation    hydrolysis intermediate + N-hydroxymethyl base + (lipobond OR alcohol),
    ///                      50-80 C -> naloxone crude
    ///   N4 salt formation  naloxone crude + acid + water, 26-60 C -> naloxone HCl
    ///   N5 solution        naloxone HCl + water (1:1), 26-60 C -> naloxone solution
    ///
    /// Lipobond agent is consumed when it is used as the solvent; alcohol is a recoverable
    /// solvent (it stays in the container and can be evaporated back out in the vanilla
    /// evaporator), matching the requested rules (no alcohol in N5).
    /// </summary>
    internal static class NaloxoneChemistry
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
                steps += StepReduction(container);
                steps += StepHydrolysis(container);
                steps += StepAllylation(container);
                steps += StepSalt(container);
                steps += StepSolution(container);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] naloxone reaction tick failed: " + e.Message);
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
        // Steps
        // ------------------------------------------------------------------

        /// <summary>N1: thebaine + acid + reducing agent + solvent -> reduction intermediate.</summary>
        internal static int StepReduction(GameItem container)
        {
            double temp = Temp(container);
            if (temp < 40.0 || temp > 60.0)
            {
                return 0;
            }
            int thebaine = Amount(container, CustomLiquids.Thebaine);
            int acid = Amount(container, "acid");
            int reducing = Amount(container, "reducing_agent");
            int lipobond = Amount(container, "lipobond_agent");
            int alcohol = Amount(container, "alcohol");
            if (thebaine <= 0 || acid <= 0 || reducing <= 0 || (lipobond <= 0 && alcohol <= 0))
            {
                return 0;
            }
            int x = Step(Math.Min(Math.Min(thebaine, acid), reducing));
            if (x <= 0)
            {
                return 0;
            }
            Add(container, CustomLiquids.Thebaine, -x);
            Add(container, "acid", -x);
            Add(container, "reducing_agent", -x);
            if (lipobond > 0)
            {
                Add(container, "lipobond_agent", -x);
            }
            Add(container, CustomLiquids.NaloxoneReductionIntermediate, x);
            return 1;
        }

        /// <summary>N2: reduction intermediate + acid + water (80-100 C) -> hydrolysis intermediate.</summary>
        internal static int StepHydrolysis(GameItem container)
        {
            double temp = Temp(container);
            if (temp < 80.0 || temp > 100.0)
            {
                return 0;
            }
            int intermediate = Amount(container, CustomLiquids.NaloxoneReductionIntermediate);
            int acid = Amount(container, "acid");
            int water = Amount(container, "water");
            int x = Step(Math.Min(Math.Min(intermediate, acid), water));
            if (x <= 0)
            {
                return 0;
            }
            Add(container, CustomLiquids.NaloxoneReductionIntermediate, -x);
            Add(container, "acid", -x);
            Add(container, "water", -x);
            Add(container, CustomLiquids.NaloxoneHydrolysisIntermediate, x);
            return 1;
        }

        /// <summary>N3: hydrolysis intermediate + N-hydroxymethyl base + solvent (50-80 C) -> crude.</summary>
        internal static int StepAllylation(GameItem container)
        {
            double temp = Temp(container);
            if (temp < 50.0 || temp > 80.0)
            {
                return 0;
            }
            int intermediate = Amount(container, CustomLiquids.NaloxoneHydrolysisIntermediate);
            int baseSolution = Amount(container, CustomLiquids.HydroxymethylBase);
            int lipobond = Amount(container, "lipobond_agent");
            int alcohol = Amount(container, "alcohol");
            if (intermediate <= 0 || baseSolution <= 0 || (lipobond <= 0 && alcohol <= 0))
            {
                return 0;
            }
            int x = Step(Math.Min(intermediate, baseSolution));
            if (x <= 0)
            {
                return 0;
            }
            Add(container, CustomLiquids.NaloxoneHydrolysisIntermediate, -x);
            Add(container, CustomLiquids.HydroxymethylBase, -x);
            if (lipobond > 0)
            {
                Add(container, "lipobond_agent", -x);
            }
            Add(container, CustomLiquids.NaloxoneCrude, x);
            return 1;
        }

        /// <summary>N4: crude + acid + water (26-60 C) -> naloxone HCl.</summary>
        internal static int StepSalt(GameItem container)
        {
            double temp = Temp(container);
            if (temp < AmbientTemp || temp > 60.0)
            {
                return 0;
            }
            int crude = Amount(container, CustomLiquids.NaloxoneCrude);
            int acid = Amount(container, "acid");
            int water = Amount(container, "water");
            int x = Step(Math.Min(Math.Min(crude, acid), water));
            if (x <= 0)
            {
                return 0;
            }
            Add(container, CustomLiquids.NaloxoneCrude, -x);
            Add(container, "acid", -x);
            Add(container, "water", -x);
            Add(container, CustomLiquids.NaloxoneHcl, x);
            return 1;
        }

        /// <summary>N5: naloxone HCl + water (1:1, 26-60 C) -> injectable solution (no alcohol).</summary>
        internal static int StepSolution(GameItem container)
        {
            double temp = Temp(container);
            if (temp < AmbientTemp || temp > 60.0)
            {
                return 0;
            }
            int hcl = Amount(container, CustomLiquids.NaloxoneHcl);
            int water = Amount(container, "water");
            int x = Step(Math.Min(hcl, water));
            if (x <= 0)
            {
                return 0;
            }
            Add(container, CustomLiquids.NaloxoneHcl, -x);
            Add(container, "water", -x);
            Add(container, CustomLiquids.NaloxoneSolution, x);
            return 1;
        }

        // ------------------------------------------------------------------
        // Helpers (same rounding behaviour as the adrenaline chain)
        // ------------------------------------------------------------------

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
