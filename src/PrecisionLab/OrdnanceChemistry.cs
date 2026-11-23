using System;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Military ordnance chain: flashbang, smoke grenade and a C4-like plastic explosive charge.
    ///
    ///   F1 flash mix      metal powder + oxidizer (26-40 C) -> flash composition
    ///   F2 press          dry flash composition 5000 uL -> vanilla flashbang grenade
    ///   S1 smoke mix      sugar + oxidizer + lipobond (26-60 C) -> smoke composition
    ///   S2 press          dry smoke composition 5000 uL -> vanilla smoke grenade
    ///   C1 nitration      nitrating acid + alcohol (26-40 C) -> nitro base
    ///   C2 putty          nitro base + stabilizing agent + lipobond (26-40 C) -> plastic explosive putty
    ///   C3 press          dry putty 5000 uL -> custom C4-like plastic explosive charge
    ///
    /// Overheating any mixing stage destroys the batch into burnt ordnance waste.
    /// </summary>
    internal static class OrdnanceChemistry
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
                steps += StepFlashMix(container);
                steps += StepSmokeMix(container);
                steps += StepNitration(container);
                steps += StepPutty(container);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] ordnance reaction tick failed: " + e.Message);
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

        internal static int StepFlashMix(GameItem container)
        {
            double temp = Temp(container);
            if (temp < 26.0)
            {
                return 0;
            }
            int metal = Amount(container, CustomLiquids.MetalPowder);
            int oxidizer = Amount(container, CustomLiquids.Oxidizer);
            int x = ReactionMath.Step(Math.Min(metal, oxidizer));
            if (x <= 0)
            {
                return 0;
            }
            Consume(container, CustomLiquids.MetalPowder, x);
            Consume(container, CustomLiquids.Oxidizer, x);
            if (temp > 40.0)
            {
                Add(container, CustomLiquids.OrdnanceWaste, x);
            }
            else
            {
                Add(container, CustomLiquids.FlashPowder, x);
            }
            return 1;
        }

        internal static int StepSmokeMix(GameItem container)
        {
            double temp = Temp(container);
            if (temp < 26.0)
            {
                return 0;
            }
            int sugar = Amount(container, "sugar");
            int oxidizer = Amount(container, CustomLiquids.Oxidizer);
            int lipobond = Amount(container, "lipobond_agent");
            int x = ReactionMath.Step(Math.Min(Math.Min(sugar, oxidizer), lipobond));
            if (x <= 0)
            {
                return 0;
            }
            Consume(container, "sugar", x);
            Consume(container, CustomLiquids.Oxidizer, x);
            Consume(container, "lipobond_agent", x);
            if (temp > 60.0)
            {
                Add(container, CustomLiquids.OrdnanceWaste, x);
            }
            else
            {
                Add(container, CustomLiquids.SmokeComposition, x);
            }
            return 1;
        }

        internal static int StepNitration(GameItem container)
        {
            double temp = Temp(container);
            if (temp < 26.0)
            {
                return 0;
            }
            int acid = Amount(container, CustomLiquids.NitratingAcid);
            int alcohol = Amount(container, "alcohol");
            int x = ReactionMath.Step(Math.Min(acid, alcohol));
            if (x <= 0)
            {
                return 0;
            }
            Consume(container, CustomLiquids.NitratingAcid, x);
            Consume(container, "alcohol", x);
            if (temp > 40.0)
            {
                Add(container, CustomLiquids.OrdnanceWaste, x);
            }
            else
            {
                Add(container, CustomLiquids.NitroBase, x);
            }
            return 1;
        }

        internal static int StepPutty(GameItem container)
        {
            double temp = Temp(container);
            if (temp < 26.0)
            {
                return 0;
            }
            int nitro = Amount(container, CustomLiquids.NitroBase);
            int stabilizer = Amount(container, "stabilizing_agent");
            int lipobond = Amount(container, "lipobond_agent");
            int x = ReactionMath.Step(Math.Min(Math.Min(nitro, stabilizer), lipobond));
            if (x <= 0)
            {
                return 0;
            }
            Consume(container, CustomLiquids.NitroBase, x);
            Consume(container, "stabilizing_agent", x);
            Consume(container, "lipobond_agent", x);
            if (temp > 40.0)
            {
                Add(container, CustomLiquids.OrdnanceWaste, x);
            }
            else
            {
                Add(container, CustomLiquids.PlasticPutty, x);
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

        private static void Consume(GameItem container, string liquidId, int amount)
        {
            Add(container, liquidId, -amount);
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
