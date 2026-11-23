using System;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Synaprest injector production line (the vanilla next-generation addiction treatment).
    ///
    ///   C1 structural modification  neural receptor ligand + acid + water, 60-80 C
    ///                               -> activated ligand
    ///   C2 targeted synthesis       activated ligand + steroid raw material (brain targeting
    ///                               carrier, reused from the contraceptive line) + reducing agent,
    ///                               50-70 C -> synaprest crude
    ///   C3 high-purity refinement   crude + alcohol wash, 26-60 C -> synaprest API solution
    ///                               (alcohol is a recoverable wash solvent; purifier optional)
    ///   C4 press                    chem finisher: dry synaprest API solution (5000 uL)
    ///                               -> Synaprest injector (vanilla-style black injector)
    /// </summary>
    internal static class SynaprestChemistry
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
                steps += StepActivation(container);
                steps += StepSynthesis(container);
                steps += StepRefinement(container);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] synaprest reaction tick failed: " + e.Message);
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

        /// <summary>C1: neural ligand + acid + water (60-80 C) -> activated ligand.</summary>
        internal static int StepActivation(GameItem container)
        {
            double temp = Temp(container);
            if (temp < 60.0 || temp > 80.0)
            {
                return 0;
            }
            int ligand = Amount(container, CustomLiquids.NeuralLigand);
            int acid = Amount(container, "acid");
            int water = Amount(container, "water");
            int x = ReactionMath.Step(Math.Min(Math.Min(ligand, acid), water));
            if (x <= 0)
            {
                return 0;
            }
            Add(container, CustomLiquids.NeuralLigand, -x);
            Add(container, "acid", -x);
            Add(container, "water", -x);
            Add(container, CustomLiquids.ActivatedLigand, x);
            return 1;
        }

        /// <summary>C2: activated ligand + steroid raw + reducing agent (50-70 C) -> crude.</summary>
        internal static int StepSynthesis(GameItem container)
        {
            double temp = Temp(container);
            if (temp < 50.0 || temp > 70.0)
            {
                return 0;
            }
            int activated = Amount(container, CustomLiquids.ActivatedLigand);
            int steroid = Amount(container, CustomLiquids.SteroidRaw);
            int reducing = Amount(container, "reducing_agent");
            int x = ReactionMath.Step(Math.Min(Math.Min(activated, steroid), reducing));
            if (x <= 0)
            {
                return 0;
            }
            Add(container, CustomLiquids.ActivatedLigand, -x);
            Add(container, CustomLiquids.SteroidRaw, -x);
            Add(container, "reducing_agent", -x);
            Add(container, CustomLiquids.SynaprestCrude, x);
            return 1;
        }

        /// <summary>C3: crude + alcohol wash (26-60 C) -> synaprest API solution.</summary>
        internal static int StepRefinement(GameItem container)
        {
            double temp = Temp(container);
            if (temp < AmbientTemp || temp > 60.0)
            {
                return 0;
            }
            int crude = Amount(container, CustomLiquids.SynaprestCrude);
            int alcohol = Amount(container, "alcohol");
            if (crude <= 0 || alcohol <= 0)
            {
                return 0;
            }
            int x = ReactionMath.Step(crude);
            if (x <= 0)
            {
                return 0;
            }
            Add(container, CustomLiquids.SynaprestCrude, -x);
            Add(container, CustomLiquids.SynaprestApi, x);
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
