using System;
using UnityEngine;

namespace PrecisionLab
{
    internal static class BelladonnaChemistry
    {
        private const int Unit = 1000;

        internal static int React(GameItem c)
        {
            if (c == null || WaterHelper.IsEmpty(c)) return 0;
            try
            {
                int changed = 0;
                changed += ExtractStep(c);
                changed += RatioStep(c, CustomLiquids.BelladonnaExtract, 5000, "acid", 1000, CustomLiquids.BelladonnaAcidic, 5000);
                changed += RatioStep(c, CustomLiquids.BelladonnaAcidic, 5000, "base", 1000, CustomLiquids.BelladonnaAlkaloid, 5000);
                changed += RatioStep(c, CustomLiquids.BelladonnaAlkaloid, 5000, "water", 1000, CustomLiquids.BelladonnaPure, 5000);
                return changed;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] belladonna reaction failed: " + e.Message);
                return 0;
            }
        }

        private static int ExtractStep(GameItem c)
        {
            double t = Temp(c);
            if (t < 20.0 || t > 29.0) return 0;
            int paste = Amount(c, CustomLiquids.BelladonnaPaste);
            int alcohol = LiquidTools.GetLiquidAmount(c, "alcohol");
            if (paste <= 0 || alcohol <= 0) return 0;
            double ratio = Math.Min((double)paste / 10000.0, (double)alcohol / 10000.0);
            if (ratio <= 0) return 0;
            if (ratio > 1.0) ratio = 1.0;
            Add(c, CustomLiquids.BelladonnaPaste, -Math.Max(1, (int)Math.Floor(10000 * ratio)));
            Add(c, "alcohol", -Math.Max(1, (int)Math.Floor(10000 * ratio)));
            Add(c, CustomLiquids.BelladonnaExtract, Math.Max(1, (int)Math.Floor(5000 * ratio)));
            Add(c, "plant_matter", Math.Max(1, (int)Math.Floor(5000 * ratio)));
            return 1;
        }

        private static int RatioStep(GameItem c, string input, int inputAmt, string reagent, int reagentAmt, string output, int outputAmt)
        {
            double t = Temp(c);
            if (t < 20.0 || t > 29.0) return 0;
            int a = Amount(c, input);
            int b = LiquidTools.GetLiquidAmount(c, reagent);
            if (a <= 0 || b <= 0) return 0;
            double ratio = Math.Min((double)a / inputAmt, (double)b / reagentAmt);
            if (ratio <= 0) return 0;
            if (ratio > 1.0) ratio = 1.0;
            Add(c, input, -Math.Max(1, (int)Math.Floor(inputAmt * ratio)));
            Add(c, reagent, -Math.Max(1, (int)Math.Floor(reagentAmt * ratio)));
            int outAmt = (int)Math.Floor(outputAmt * ratio);
            if (outAmt > 0) Add(c, output, outAmt);
            return 1;
        }

        private static double Temp(GameItem c) => c.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble();
        private static int Amount(GameItem c, string id) => LiquidTools.GetLiquidAmount(c, id);
        private static void Add(GameItem c, string id, int delta)
        {
            string part = Liquid.GetCurrentPartFromId(id);
            if (part == null) return;
            int next = Math.Max(0, c.GetTagReadonly(part).GetInt() + delta);
            c.ModifyTag(part, delegate (TagState s) { s.Enable().SetInt(next); }, skipValidate: true);
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(MachineChemFinisher), "CanOutput")]
    internal static class BelladonnaPressGate
    {
        private static bool Prefix(GameItem machine, GameSlotInventory containerSlot, ref bool __result)
        {
            try
            {
                if (machine == null || containerSlot == null || containerSlot.childItem == null) return true;
                if (!machine.IsTag("SELECTED_CHEM_ID_TAG")
                    || machine.GetTagReadonly("SELECTED_CHEM_ID_TAG").GetString() != BelladonnaContent.InjectorId)
                    return true;
                GameItem c = containerSlot.childItem;
                int active = LiquidTools.GetLiquidAmount(c, CustomLiquids.BelladonnaPure);
                int total = WaterHelper.GetTotalVolume(c);
                if (total >= 5000 && active >= 2000 && active * 100 >= total * 40)
                    return true;
                __result = false;
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] belladonna press gate failed: " + e.Message);
                return true;
            }
        }
    }
}
