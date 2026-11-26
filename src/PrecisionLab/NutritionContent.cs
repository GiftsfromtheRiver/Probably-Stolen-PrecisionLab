using System;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>Three-in-one nutrition IV: coffee lipids/protein + honey carbohydrate + vitamin
    /// extract + water, purified and filled into a milky-white blood bag. Counts as FOOD,
    /// PROCESSED_FOOD and MEDICAL.</summary>
    internal static class NutritionContent
    {
        internal const string Id = "nutrition_iv_bag";
        internal const string KitId = "nutrition_supply_box";

        internal static void EnsureRecipes()
        {
            try
            {
                if (ChemFinisherRecipe.GetRecipeById(Id) != null) return;
                ChemFinisherRecipe.chemFinisherRecipes.Add(new ChemFinisherRecipe
                {
                    identifier = Id,
                    displayName = Loc.Get("item.nutrition_iv_bag.name", "三合一营养注射液"),
                    requiredVolume = 5000,
                    isRequireDry = false,
                    activeIngredientId = CustomLiquids.NutritionEmulsion
                });
            }
            catch (Exception e) { Debug.LogWarning("[PrecisionLab] nutrition recipe failed: " + e.Message); }
        }

        internal static GameItem Create()
        {
            GameItem item = ItemDirectory.CreateEmptyItem();
            ModSprites.Apply(item, "nutrition_iv_bag", "Items/items_food", "blood_bag");
            item.SetShape(new GridShapeBuilder(2, 3).SetDataFill(1).Build());
            item.name = Loc.Get("item.nutrition_iv_bag.name", "三合一营养注射液");
            item.shortDescription = Loc.Get("item.nutrition_iv_bag.desc",
                "乳白色营养输液：葡萄糖/蜂蜜、咖啡脂蛋白、维生素与电解质。属于食物、合成食物和医疗品。");
            item.SetGameItemType("FOOD");
            item.SetGameItemType("PROCESSED_FOOD");
            item.SetGameItemType("MEDICAL");
            item.EnableTag("ITEM_MEDICAL_TAG");
            item.unitValue = 160L;
            FoodItemHelper.InitFoodItem(item, 1500, isCanned: false);
            AudioHelper.InitPlasticContainer(item);
            ModHelper.SetSource(item, Loc.ModId, ModEntry.ModDisplayName);
            return item;
        }
    }

    internal static class NutritionChemistry
    {
        internal static int React(GameItem c)
        {
            if (c == null || WaterHelper.IsEmpty(c)) return 0;
            double t = c.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble();
            if (t < 20.0 || t > 29.0) return 0;
            try
            {
                int steps = 0;
                steps += StepCoffeeSeparation(c);
                steps += StepNutrition(c);
                return steps > 0 ? 1 : 0;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] nutrition reaction failed: " + e.Message);
                return 0;
            }
        }

        private static int StepCoffeeSeparation(GameItem c)
        {
            int coffee = LiquidTools.GetLiquidAmount(c, "coffee_mush");
            int water = LiquidTools.GetLiquidAmount(c, "water");
            if (coffee <= 0 || water <= 0) return 0;
            double ratio = Math.Min(1.0, Math.Min((double)coffee / 4000, (double)water / 1000));
            double step = ReactionMath.Fraction(ratio);
            if (step <= 0) return 0;
            Add(c, "coffee_mush", -Math.Max(1, (int)(4000 * step)));
            Add(c, "water", -Math.Max(1, (int)(1000 * step)));
            Add(c, "fat", (int)(800 * step));
            Add(c, "protein", (int)(1200 * step));
            Add(c, "chemical_contaminant", Math.Max(1, (int)(200 * step)));
            return 1;
        }

        private static int StepNutrition(GameItem c)
        {
            try
            {
                int fat = LiquidTools.GetLiquidAmount(c, "fat");
                int protein = LiquidTools.GetLiquidAmount(c, "protein");
                int honey = LiquidTools.GetLiquidAmount(c, CustomLiquids.MedicalHoney);
                int aloe = LiquidTools.GetLiquidAmount(c, CustomLiquids.AloeExtract);
                int water = LiquidTools.GetLiquidAmount(c, "water");
                double ratio = 1.0;
                ratio = Math.Min(ratio, (double)fat / 800);
                ratio = Math.Min(ratio, (double)protein / 1200);
                ratio = Math.Min(ratio, (double)honey / 1000);
                ratio = Math.Min(ratio, (double)aloe / 1000);
                ratio = Math.Min(ratio, (double)water / 1000);
                if (ratio <= 0) return 0;
                double step = ReactionMath.Fraction(Math.Min(1.0, ratio));
                if (step <= 0) return 0;
                Add(c, "fat", -Math.Max(1, (int)(800 * step)));
                Add(c, "protein", -Math.Max(1, (int)(1200 * step)));
                Add(c, CustomLiquids.MedicalHoney, -Math.Max(1, (int)(1000 * step)));
                Add(c, CustomLiquids.AloeExtract, -Math.Max(1, (int)(1000 * step)));
                Add(c, "water", -Math.Max(1, (int)(1000 * step)));
                Add(c, CustomLiquids.NutritionEmulsion, (int)(5000 * step));
                Add(c, "chemical_contaminant", Math.Max(1, (int)(150 * step)));
                return 1;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] nutrition reaction failed: " + e.Message);
                return 0;
            }
        }

        private static void Add(GameItem c, string id, int delta)
        {
            string part = Liquid.GetCurrentPartFromId(id);
            if (part == null) return;
            int next = Math.Max(0, c.GetTagReadonly(part).GetInt() + delta);
            c.ModifyTag(part, delegate (TagState st) { st.Enable().SetInt(next); }, skipValidate: true);
        }
    }

    [HarmonyPatch(typeof(MachineChemFinisher), "CanOutput")]
    internal static class NutritionPressGate
    {
        private static void Postfix(GameItem machine, GameSlotInventory containerSlot, ref bool __result)
        {
            try
            {
                if (machine == null || containerSlot == null || containerSlot.childItem == null) return;
                if (!machine.IsTag("SELECTED_CHEM_ID_TAG")) return;
                if (machine.GetTagReadonly("SELECTED_CHEM_ID_TAG").GetString() != NutritionContent.Id) return;
                GameItem c = containerSlot.childItem;
                int total = WaterHelper.GetTotalVolume(c);
                int emulsion = LiquidTools.GetLiquidAmount(c, CustomLiquids.NutritionEmulsion);
                int contaminants = LiquidTools.GetLiquidAmount(c, "chemical_contaminant")
                    + LiquidTools.GetLiquidAmount(c, "physical_contaminant");
                __result = total >= 5000 && emulsion >= 4800 && contaminants == 0;
            }
            catch (Exception e) { Debug.LogWarning("[PrecisionLab] nutrition press gate failed: " + e.Message); }
        }
    }
}
