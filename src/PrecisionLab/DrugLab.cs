using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>Data-driven five-stage drug lines: antidepressant, amiodarone, vasopressin,
    /// vasodilator, coagulant and anticoagulant. Belladonna is implemented separately.</summary>
    internal static class DrugLabContent
    {
        internal const string AntidepressantId = "antidepressant_pill";
        internal const string AmiodaroneId = "amiodarone_pill";
        internal const string VasopressinId = "vasopressin_injector";
        internal const string VasodilatorId = "vasodilator_injector";
        internal const string CoagulantId = "coagulant_dressing";
        internal const string AnticoagulantId = "anticoagulant_injector";
        internal const string GlandPowderId = "animal_gland_powder";
        internal const string AromaticId = "aromatic_compound";
        internal const string IodideId = "potassium_iodide_crystal";

        private static readonly string[] Finals =
        {
            AntidepressantId, AmiodaroneId, VasopressinId, VasodilatorId, CoagulantId, AnticoagulantId
        };

        internal static bool IsFinal(string id)
        {
            for (int i = 0; i < Finals.Length; i++) if (Finals[i] == id) return true;
            return false;
        }

        internal static void EnsureRecipes()
        {
            try
            {
                AddRecipe(AntidepressantId, "item.antidepressant.name", "抗抑郁药片", CustomLiquids.AntidepressantApi);
                AddRecipe(AmiodaroneId, "item.amiodarone.name", "胺碘酮片", CustomLiquids.AmiodaroneApi);
                AddRecipe(VasopressinId, "item.vasopressin.name", "血管加压素注射器", CustomLiquids.VasopressinSolution);
                AddRecipe(VasodilatorId, "item.vasodilator.name", "血管舒张素注射器", CustomLiquids.VasodilatorSolution);
                AddRecipe(CoagulantId, "item.coagulant.name", "促凝剂瓶", CustomLiquids.CoagulantGel);
                AddRecipe(AnticoagulantId, "item.anticoagulant.name", "抗凝剂瓶", CustomLiquids.AnticoagulantSolution);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] drug recipes failed: " + e.Message);
            }
        }

        private static void AddRecipe(string id, string locKey, string fallback, string active)
        {
            if (ChemFinisherRecipe.GetRecipeById(id) != null) return;
            ChemFinisherRecipe.chemFinisherRecipes.Add(new ChemFinisherRecipe
            {
                identifier = id,
                displayName = Loc.Get(locKey, fallback),
                requiredVolume = 5000,
                isRequireDry = false,
                activeIngredientId = active
            });
        }

        internal static GameItem CreateGlandPowder()
        {
            GameItem item = RawItem(GlandPowderId, "动物腺体粉末",
                "干燥动物腺体粉末。可溶解/搅拌成腺体提取液，用于加压素、舒张素、促凝剂和抗凝剂。",
                "Items/items_material", "animal_gland_powder", CustomLiquids.GlandExtract, 35);
            return item;
        }

        internal static GameItem CreateAromatic()
        {
            return RawItem(AromaticId, "芳香族化合物",
                "芳香族化合物原料。用于抗抑郁药和胺碘酮的前体合成。",
                "Items/items_material", "aromatic_compound_bottle", CustomLiquids.Aromatic, 45);
        }

        internal static GameItem CreateIodide()
        {
            return RawItem(IodideId, "碘化钾晶体",
                "碘化钾晶体。用于胺碘酮的碘化和抗凝剂灌装。",
                "Items/items_material", "potassium_iodide_bottle", CustomLiquids.Iodide, 40);
        }

        private static GameItem RawItem(string id, string name, string desc, string atlas, string sprite, string liquid, long value)
        {
            GameItem item = ItemDirectory.CreateEmptyItem();
            ModSprites.Apply(item, sprite, "Items/items_material", "chemical2");
            item.SetShape(new GridShapeBuilder(1, (sprite == "aromatic_compound_bottle" || sprite == "potassium_iodide_bottle") ? 2 : 1).SetDataFill(1).Build());
            item.name = Loc.Get("item." + id + ".name", name);
            item.shortDescription = Loc.Get("item." + id + ".desc", desc);
            item.SetGameItemType("CHEMICAL_SUPPLIES");
            item.unitValue = value;
            ChemicalHelper.InitChemItem(item, isBlendable: true, isSoluble: true);
            ChemicalHelper.RemoveAllLiquid(item);
            string rawPart = Liquid.GetCurrentPartFromId(liquid);
            item.ModifyTag(rawPart, delegate (TagState st) { st.Enable().SetInt(20000); }, skipValidate: true);
            item.ModifyTag("ACTIVATE_INGREDIENT_TAG", delegate (TagState st) { st.Enable().SetString(liquid); });
            ModHelper.SetSource(item, Loc.ModId, ModEntry.ModDisplayName);
            return item;
        }

        internal static GameItem CreateAntidepressant()
        {
            return Final(AntidepressantId, "抗抑郁药片",
                "高级违禁品。星泉医疗的抗抑郁药片；按原版风格仅作交易/医疗收藏品。长期使用可能产生依赖。",
                "Items/items_material", "drug_bottle_antidepressant", 600, 3, CustomLiquids.AntidepressantApi);
        }

        internal static GameItem CreateAmiodarone()
        {
            return Final(AmiodaroneId, "胺碘酮片",
                "星泉医疗的胺碘酮片，用于循环系统急救的医疗品；仅作交易/医疗收藏品。",
                "Items/items_material", "drug_bottle_amiodarone", 400, 0, CustomLiquids.AmiodaroneApi);
        }

        internal static GameItem CreateVasopressin()
        {
            return Final(VasopressinId, "血管加压素注射器",
                "星泉医疗的血管加压素注射器，用于升压与止血；仅作交易/医疗收藏品。",
                "Items/items_material", "injector_pen_vasopressin", 450, 0, CustomLiquids.VasopressinSolution);
        }

        internal static GameItem CreateVasodilator()
        {
            return Final(VasodilatorId, "血管舒张素注射器",
                "星泉医疗的血管舒张素注射器，用于扩血管与降压；仅作交易/医疗收藏品。",
                "Items/items_material", "injector_pen_vasodilator", 450, 0, CustomLiquids.VasodilatorSolution);
        }

        internal static GameItem CreateCoagulant()
        {
            return Final(CoagulantId, "促凝剂瓶",
                "星泉医疗的促凝剂瓶/安瓿，用于快速凝血；仅作交易/医疗收藏品。",
                "Items/items_material", "ampoule_coagulant", 350, 0, CustomLiquids.CoagulantGel);
        }

        internal static GameItem CreateAnticoagulant()
        {
            return Final(AnticoagulantId, "抗凝剂瓶",
                "星泉医疗的抗凝剂瓶/安瓿，用于抗凝血；价格低于蓝血，仅作交易/医疗收藏品。",
                "Items/items_material", "ampoule_anticoagulant", 200, 0, CustomLiquids.AnticoagulantSolution);
        }

        private static GameItem Final(string id, string name, string desc, string atlas, string sprite,
            long value, int contraband, string active)
        {
            GameItem item = ItemDirectory.CreateEmptyItem();
            ModSprites.Apply(item, sprite, "Items/items_material", "chemical2");
            bool horizontalPen = sprite.StartsWith("injector_pen_", StringComparison.Ordinal);
            item.SetShape(new GridShapeBuilder(horizontalPen ? 3 : 1, horizontalPen ? 1 : 2).SetDataFill(1).Build());
            item.name = Loc.Get("item." + id + ".name", name);
            item.shortDescription = Loc.Get("item." + id + ".desc", desc);
            item.SetGameItemType("MEDICAL");
            if (id == AntidepressantId)
            {
                // Real-world antidepressants are controlled anesthetics/narcotics class in-game.
                item.SetGameItemType("NARCOTIC");
            }
            item.EnableTag("ITEM_MEDICAL_TAG");
            item.EnableProficiency("MEDICAL_PROFICIENCY");
            item.unitValue = value;
            // Anything more expensive than Synaprest (350) is a luxury medicine.
            if (value > 350L) item.SetGameItemType("LUXURY_ITEM");
            item.EnableManufacturer("xingquan");
            ChemicalHelper.InitChemItem(item, isBlendable: true, isSoluble: true);
            ChemicalProductHelper.InitChemicalProduct(item, active);
            AdrenalineContent.SeedChemicalComposition(item, active, 5000);
            if (contraband > 0) ContrabandHelper.InitContrabandItem(item, contraband);
            AudioHelper.InitPlasticContainer(item);
            ModHelper.SetSource(item, Loc.ModId, ModEntry.ModDisplayName);
            return item;
        }
    }

    internal static class DrugLabChemistry
    {
        private sealed class Step
        {
            public string[] Inputs;
            public int[] Amounts;
            public string[] Outputs;
            public int[] OutAmounts;
            public double Min;
            public double Max;
        }

        private static readonly List<Step> Steps = Build();

        private static Step S(double min, double max, string[] inputs, int[] amounts, string[] outputs, int[] outAmounts)
        {
            return new Step { Min = min, Max = max, Inputs = inputs, Amounts = amounts, Outputs = outputs, OutAmounts = outAmounts };
        }

        private static List<Step> Build()
        {
            var l = new List<Step>();
            // A: antidepressant
            l.Add(S(40, 60, new[] { CustomLiquids.Aromatic, "reducing_agent" }, new[] { 5000, 5000 },
                new[] { CustomLiquids.AntidepressantIntermediate }, new[] { 5000 }));
            l.Add(S(20, 29, new[] { CustomLiquids.AntidepressantIntermediate, "acid", "water" }, new[] { 5000, 1000, 1000 },
                new[] { CustomLiquids.AntidepressantSalt }, new[] { 5000 }));
            l.Add(S(60, 80, new[] { CustomLiquids.AntidepressantSalt }, new[] { 5000 },
                new[] { CustomLiquids.AntidepressantConcentrate }, new[] { 5000 }));
            l.Add(S(20, 29, new[] { CustomLiquids.AntidepressantConcentrate, "alcohol" }, new[] { 5000, 1000 },
                new[] { CustomLiquids.AntidepressantApi }, new[] { 5000 }));
            // M: amiodarone
            l.Add(S(60, 80, new[] { CustomLiquids.Aromatic, CustomLiquids.Iodide, "acid" }, new[] { 5000, 5000, 1000 },
                new[] { CustomLiquids.IodinationMix }, new[] { 5000 }));
            l.Add(S(20, 29, new[] { CustomLiquids.IodinationMix, "lipobond_agent" }, new[] { 5000, 1000 },
                new[] { CustomLiquids.OrganicIodide }, new[] { 5000 }));
            l.Add(S(20, 29, new[] { CustomLiquids.OrganicIodide, "water" }, new[] { 5000, 2000 },
                new[] { CustomLiquids.CrudeAmiodarone }, new[] { 5000 }));
            l.Add(S(20, 29, new[] { CustomLiquids.CrudeAmiodarone }, new[] { 5000 },
                new[] { CustomLiquids.AmiodaroneApi }, new[] { 5000 }));
            // V: vasopressin (no waste in V1)
            l.Add(S(20, 29, new[] { CustomLiquids.GlandExtract, "acid", "water" }, new[] { 10000, 4000, 1000 },
                new[] { CustomLiquids.AcidicPituitary, CustomLiquids.PituitaryWaste }, new[] { 4500, 500 }));
            l.Add(S(20, 29, new[] { CustomLiquids.AcidicPituitary, "water", "base" }, new[] { 4500, 500, 100 },
                new[] { CustomLiquids.VasopressinCrude }, new[] { 5000 }));
            l.Add(S(20, 29, new[] { CustomLiquids.VasopressinCrude, "alcohol" }, new[] { 5000, 1000 },
                new[] { CustomLiquids.VasopressinPure }, new[] { 5000 }));
            l.Add(S(20, 29, new[] { CustomLiquids.VasopressinPure, "water" }, new[] { 5000, 1000 },
                new[] { CustomLiquids.VasopressinSolution }, new[] { 5000 }));
            // D: vasodilator
            l.Add(S(40, 50, new[] { CustomLiquids.GlandExtract, "base", "water" }, new[] { 10000, 4000, 1000 },
                new[] { CustomLiquids.AlkalineGland }, new[] { 5000 }));
            l.Add(S(20, 29, new[] { CustomLiquids.AlkalineGland, "lipobond_agent" }, new[] { 5000, 1000 },
                new[] { CustomLiquids.VasodilatorComplex }, new[] { 5000 }));
            l.Add(S(20, 29, new[] { CustomLiquids.VasodilatorComplex, "alcohol" }, new[] { 5000, 1000 },
                new[] { CustomLiquids.VasodilatorPure }, new[] { 5000 }));
            l.Add(S(20, 29, new[] { CustomLiquids.VasodilatorPure, "acid", "water" }, new[] { 5000, 1000, 1000 },
                new[] { CustomLiquids.VasodilatorSolution }, new[] { 5000 }));
            // C: coagulant
            l.Add(S(36.5, 37.5, new[] { CustomLiquids.GlandExtract, "water" }, new[] { 5000, 4000 },
                new[] { CustomLiquids.ThrombinActive }, new[] { 5000 }));
            l.Add(S(20, 29, new[] { CustomLiquids.ThrombinActive, "alcohol" }, new[] { 5000, 4000 },
                new[] { CustomLiquids.ThrombinPrecipitate }, new[] { 5000 }));
            l.Add(S(20, 29, new[] { CustomLiquids.ThrombinPrecipitate, "water" }, new[] { 5000, 1000 },
                new[] { CustomLiquids.ThrombinPure }, new[] { 5000 }));
            l.Add(S(20, 29, new[] { CustomLiquids.ThrombinPure, "lipobond_agent" }, new[] { 5000, 1000 },
                new[] { CustomLiquids.CoagulantGel }, new[] { 5000 }));
            // H: anticoagulant
            l.Add(S(79.5, 80.5, new[] { CustomLiquids.GlandExtract, "base", "water" }, new[] { 5000, 9000, 1000 },
                new[] { CustomLiquids.HeparinCrude }, new[] { 5000 }));
            l.Add(S(20, 29, new[] { CustomLiquids.HeparinCrude, "acid" }, new[] { 5000, 1000 },
                new[] { CustomLiquids.AnticoagulantCrude }, new[] { 5000 }));
            l.Add(S(20, 29, new[] { CustomLiquids.AnticoagulantCrude, "alcohol" }, new[] { 5000, 1000 },
                new[] { CustomLiquids.AnticoagulantActive }, new[] { 5000 }));
            l.Add(S(20, 29, new[] { CustomLiquids.AnticoagulantActive, "water" }, new[] { 5000, 1000 },
                new[] { CustomLiquids.AnticoagulantSolution }, new[] { 5000 }));
            return l;
        }

        internal static int React(GameItem c)
        {
            if (c == null || WaterHelper.IsEmpty(c)) return 0;
            try
            {
                double t = c.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble();
                for (int i = 0; i < Steps.Count; i++)
                {
                    Step s = Steps[i];
                    if (t < s.Min || t > s.Max) continue;
                    double ratio = 1.0;
                    for (int k = 0; k < s.Inputs.Length; k++)
                    {
                        int have = LiquidTools.GetLiquidAmount(c, s.Inputs[k]);
                        if (have <= 0 || s.Amounts[k] <= 0) { ratio = 0; break; }
                        ratio = Math.Min(ratio, (double)have / s.Amounts[k]);
                    }
                    if (ratio <= 0) continue;
                    if (ratio > 1.0) ratio = 1.0;
                    double stepRatio = ReactionMath.Fraction(ratio);
                    if (stepRatio <= 0.0) return 0;
                    for (int k = 0; k < s.Inputs.Length; k++)
                        Add(c, s.Inputs[k], -Math.Max(1, (int)Math.Floor(s.Amounts[k] * stepRatio)));
                    for (int k = 0; k < s.Outputs.Length; k++)
                    {
                        int outAmt = (int)Math.Floor(s.OutAmounts[k] * stepRatio);
                        if (outAmt > 0) Add(c, s.Outputs[k], outAmt);
                    }
                    return 1;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] drug reaction failed: " + e.Message);
            }
            return 0;
        }

        private static void Add(GameItem c, string id, int delta)
        {
            string part = Liquid.GetCurrentPartFromId(id);
            if (part == null) return;
            int next = Math.Max(0, c.GetTagReadonly(part).GetInt() + delta);
            c.ModifyTag(part, delegate (TagState s) { s.Enable().SetInt(next); }, skipValidate: true);
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(MachineChemFinisher), "CanOutput")]
    internal static class DrugLabPressGate
    {
        private static bool Prefix(GameItem machine, GameSlotInventory containerSlot, ref bool __result)
        {
            try
            {
                if (machine == null || containerSlot == null || containerSlot.childItem == null) return true;
                if (!machine.IsTag("SELECTED_CHEM_ID_TAG")) return true;
                string id = machine.GetTagReadonly("SELECTED_CHEM_ID_TAG").GetString();
                if (!DrugLabContent.IsFinal(id)) return true;
                GameItem c = containerSlot.childItem;
                string active = id == DrugLabContent.AntidepressantId ? CustomLiquids.AntidepressantApi
                    : id == DrugLabContent.AmiodaroneId ? CustomLiquids.AmiodaroneApi
                    : id == DrugLabContent.VasopressinId ? CustomLiquids.VasopressinSolution
                    : id == DrugLabContent.VasodilatorId ? CustomLiquids.VasodilatorSolution
                    : id == DrugLabContent.CoagulantId ? CustomLiquids.CoagulantGel
                    : CustomLiquids.AnticoagulantSolution;
                int activeAmount = LiquidTools.GetLiquidAmount(c, active);
                int totalVolume = WaterHelper.GetTotalVolume(c);
                bool ok = totalVolume >= 5000 && activeAmount >= 2000 && activeAmount * 100 >= totalVolume * 40;
                if (ok) return true;
                __result = false;
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] drug press gate failed: " + e.Message);
                return true;
            }
        }
    }
}
