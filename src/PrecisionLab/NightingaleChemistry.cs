using System;
using UnityEngine;

namespace PrecisionLab
{
    internal static class NightingaleChemistry
    {
        private static readonly System.Collections.Generic.Dictionary<int, int> _batchRatios
            = new System.Collections.Generic.Dictionary<int, int>();

        internal static void Register()
        {
            // Reactions run in the generic container tick; no extra registration needed.
        }

        internal static int React(GameItem container)
        {
            if (container == null || WaterHelper.IsEmpty(container))
            {
                return 0;
            }
            int steps = 0;
            try
            {
                steps += StepExtraction(container);
                steps += StepFusion(container);
                steps += StepPurify(container);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] nightingale reaction failed: " + e.Message);
            }
            return steps;
        }

        private static int StepExtraction(GameItem c)
        {
            double temp = Temp(c);
            if (temp < 26.0) return 0;
            int pulp = Amount(c, CustomLiquids.AloePulp);
            int alcohol = Amount(c, "alcohol");
            if (pulp <= 0 || alcohol <= 0) return 0;
            if (temp > 30.0)
            {
                int hot = Math.Max(100, (int)Math.Round(pulp * 0.1));
                hot = Math.Min(hot, pulp);
                Add(c, CustomLiquids.AloePulp, -hot);
                Add(c, CustomLiquids.OintmentWaste, hot);
                return 1;
            }
            // Same 10%-per-tick extraction speed as Dream Cap. Alcohol is a solvent and is NOT
            // consumed: 3x alcohol relative to the paste gives a 50/50 split into extract and
            // plant matter; less alcohol shifts the split toward plant matter. The batch ratio is
            // captured once when the extraction starts, otherwise the ratio would rise as pulp is
            // consumed and later ticks would use the 50% split.
            // Custom tags on an arbitrary container are not reliably persisted by the tag master,
            // so keep the initial batch ratio in a per-container session dictionary instead.
            int key = c.uniqueId;
            int ratioBp;
            if (!_batchRatios.TryGetValue(key, out ratioBp))
            {
                ratioBp = (int)Math.Round(alcohol * 1000.0 / Math.Max(1, pulp));
                _batchRatios[key] = ratioBp;
            }
            int process = Math.Max(100, (int)Math.Round(pulp * 0.1));
            process = Math.Min(process, pulp);
            double ratio = ratioBp / 1000.0;
            double extractFraction = 0.5 * Math.Min(1.0, ratio / 3.0);
            int extract;
            if (ratio >= 2.999)
            {
                // 3x alcohol: consume the whole paste batch and split exactly 50/50.
                process = pulp;
                extract = process / 2;
            }
            else
            {
                extract = (int)Math.Round(process * extractFraction);
            }
            int plantMatter = process - extract;
            Add(c, CustomLiquids.AloePulp, -process);
            if (extract > 0) Add(c, CustomLiquids.AloeExtract, extract);
            if (plantMatter > 0) Add(c, "plant_matter", plantMatter);
            if (pulp - process <= 0)
            {
                _batchRatios.Remove(key);
            }
            return 1;
        }

        private static int StepFusion(GameItem c)
        {
            double temp = Temp(c);
            if (temp < 26.0) return 0;
            int extract = Amount(c, CustomLiquids.AloeExtract);
            int honey = Amount(c, CustomLiquids.MedicalHoney);
            int stem = Amount(c, CustomLiquids.StemCell);
            if (extract <= 0 || honey <= 0 || stem <= 0) return 0;
            int x = ReactionMath.Step(Math.Min(extract, honey));
            if (temp > 30.0)
            {
                if (x <= 0) return 0;
                Add(c, CustomLiquids.AloeExtract, -x);
                Add(c, CustomLiquids.MedicalHoney, -x);
                Add(c, CustomLiquids.StemCell, -Math.Min(stem, Math.Max(1, x / 50)));
                Add(c, CustomLiquids.OintmentWaste, x);
                return 1;
            }
            if (x <= 0) return 0;
            Add(c, CustomLiquids.AloeExtract, -x);
            Add(c, CustomLiquids.MedicalHoney, -x);
            Add(c, CustomLiquids.StemCell, -Math.Min(stem, Math.Max(1, x / 50)));
            Add(c, CustomLiquids.OintmentBase, x);
            Add(c, "chemical_contaminant", Math.Max(1, x / 50));
            return 1;
        }

        private static int StepPurify(GameItem c)
        {
            int baseAmount = Amount(c, CustomLiquids.OintmentBase);
            if (baseAmount <= 0) return 0;
            // The purifier removes reaction contaminants before the purity check.
            int contaminant = Amount(c, "chemical_contaminant");
            int plantMatter = Amount(c, "plant_matter");
            if (contaminant > 0) Add(c, "chemical_contaminant", -contaminant);
            if (plantMatter > 0) Add(c, "plant_matter", -plantMatter);
            int total = WaterHelper.GetTotalVolume(c);
            int valuable = baseAmount + Amount(c, CustomLiquids.PurifiedOintmentBase);
            if (total <= 0 || valuable * 100.0 / total < 99.0) return 0;
            int x = ReactionMath.Step(baseAmount);
            if (x <= 0) return 0;
            Add(c, CustomLiquids.OintmentBase, -x);
            Add(c, CustomLiquids.PurifiedOintmentBase, x);
            return 1;
        }

        private static double Temp(GameItem c)
        {
            return c.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble();
        }

        private static int Amount(GameItem c, string liquid)
        {
            return LiquidTools.GetLiquidAmount(c, liquid);
        }

        private static void Add(GameItem c, string liquid, int delta)
        {
            if (delta == 0) return;
            string part = Liquid.GetCurrentPartFromId(liquid);
            if (part == null) return;
            int next = Math.Max(0, c.GetTagReadonly(part).GetInt() + delta);
            c.ModifyTag(part, delegate (TagState state) { state.Enable().SetInt(next); }, skipValidate: true);
        }
    }
}
