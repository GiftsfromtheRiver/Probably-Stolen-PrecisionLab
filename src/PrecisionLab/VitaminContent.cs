using System;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>Simple multivitamin health supplement: aloe extract + medical honey + water
    /// pressed into a bottle of mixed vitamin tablets. Cheap OTC product (value 50).</summary>
    internal static class VitaminContent
    {
        internal const string Id = "multivitamin_bottle"; // kept ID for save compatibility

        internal static bool IsVitamin(string id) => id == Id;

        internal static void EnsureRecipes()
        {
            try
            {
                if (ChemFinisherRecipe.GetRecipeById(Id) != null) return;
                ChemFinisherRecipe.chemFinisherRecipes.Add(new ChemFinisherRecipe
                {
                    identifier = Id,
                    displayName = Loc.Get("item.multivitamin_bottle.name", "复合维生素药片瓶"),
                    requiredVolume = 3000,
                    isRequireDry = false,
                    activeIngredientId = CustomLiquids.AloeExtract
                });
            }
            catch (Exception e) { Debug.LogWarning("[PrecisionLab] vitamin recipe failed: " + e.Message); }
        }

        internal static int Amount(GameItem item, string liquidId)
        {
            string part = Liquid.GetCurrentPartFromId(liquidId);
            int direct = part == null ? 0 : item.GetTagReadonly(part).GetInt();
            if (direct > 0) return direct;
            return LiquidTools.GetLiquidAmount(item, liquidId);
        }

        internal static double ComputePurity(GameItem item)
        {
            if (item == null) return 0.0;
            int active = Amount(item, CustomLiquids.AloeExtract);
            int impurities = Amount(item, "chemical_contaminant")
                + Amount(item, "physical_contaminant")
                + Amount(item, "starch")
                + Amount(item, "plant_matter");
            int denom = active + impurities;
            if (denom <= 0) return 0.0;
            return Math.Min(100.0, active * 100.0 / denom);
        }

        internal static void ApplyPurity(GameItem item)
        {
            double purity = ComputePurity(item);
            item.ModifyTag("CHEMICAL_PRODUCT_PURITY_TAG", delegate (TagState state)
            {
                state.Enable().SetDouble(purity);
            });
            if (purity >= 99.0) ChemicalProductHelper.SetPurityFeature(item, ItemConditionList.CreateLabPure());
            else if (purity >= 90.0) ChemicalProductHelper.SetPurityFeature(item, ItemConditionList.CreatePharmaGrade());
            else if (purity >= 80.0) ChemicalProductHelper.SetPurityFeature(item, ItemConditionList.CreateNearPharma());
            else if (purity >= 60.0) ChemicalProductHelper.SetPurityFeature(item, ItemConditionList.CreateStreetStandard());
            else if (purity >= 45.0) ChemicalProductHelper.SetPurityFeature(item, ItemConditionList.CreateCutProduct());
            else if (purity >= 30.0) ChemicalProductHelper.SetPurityFeature(item, ItemConditionList.CreateJunkGrade());
            else ChemicalProductHelper.SetPurityFeature(item, ItemConditionList.CreateStarchGrade());
        }

        internal static void NormalizeAll()
        {
            try
            {
                PlayerStore store = PlayerStore.Instance;
                if (store == null) return;
                var items = store.FindAllItem();
                if (items == null) return;
                foreach (GameItem item in items)
                {
                    if (item == null || item.identifier != Id) continue;
                    item.name = Loc.Get("item.multivitamin_bottle.name", "复合维生素药片瓶");
                    ApplyPurity(item);
                }
            }
            catch (Exception e) { Debug.LogWarning("[PrecisionLab] vitamin migration failed: " + e.Message); }
        }

        internal static GameItem Create()
        {
            GameItem item = ItemDirectory.CreateEmptyItem();
            ModSprites.Apply(item, "multivitamin_bottle", "Items/items_material", "chemical2");
            item.SetShape(new GridShapeBuilder(1, 2).SetDataFill(1).Build());
            item.name = Loc.Get("item.multivitamin_bottle.name", "复合维生素药片瓶");
            item.shortDescription = Loc.Get("item.multivitamin_bottle.desc",
                "非处方复合维生素药片瓶。作为日常保健品出售；不含直接治疗剂量。");
            item.SetGameItemType("MEDICAL");
            item.SetGameItemType("CHEMICAL_SUPPLIES");
            item.EnableTag("ITEM_MEDICAL_TAG");
            item.unitValue = 50L;
            ChemicalHelper.InitChemItem(item, isBlendable: true, isSoluble: true);
            ChemicalProductHelper.InitChemicalProduct(item, CustomLiquids.AloeExtract);
            AdrenalineContent.SeedChemicalComposition(item, CustomLiquids.AloeExtract, 3000);
            ApplyPurity(item);
            ModHelper.SetSource(item, Loc.ModId, ModEntry.ModDisplayName);
            return item;
        }
    }

    [HarmonyPatch(typeof(MachineChemFinisher), "CanOutput")]
    internal static class VitaminPressGate
    {
        private static void Postfix(GameItem machine, GameSlotInventory containerSlot, ref bool __result)
        {
            try
            {
                if (machine == null || containerSlot == null || containerSlot.childItem == null) return;
                if (!machine.IsTag("SELECTED_CHEM_ID_TAG")) return;
                if (machine.GetTagReadonly("SELECTED_CHEM_ID_TAG").GetString() != VitaminContent.Id) return;
                GameItem c = containerSlot.childItem;
                int total = WaterHelper.GetTotalVolume(c);
                int aloe = VitaminContent.Amount(c, CustomLiquids.AloeExtract);
                int honey = VitaminContent.Amount(c, CustomLiquids.MedicalHoney);
                int water = VitaminContent.Amount(c, "water");
                int contaminants = VitaminContent.Amount(c, "chemical_contaminant")
                    + VitaminContent.Amount(c, "physical_contaminant")
                    + VitaminContent.Amount(c, "starch");
                __result = total >= 3000 && aloe >= 2000 && honey >= 500 && water >= 300
                    && contaminants == 0;
            }
            catch (Exception e) { Debug.LogWarning("[PrecisionLab] vitamin press gate failed: " + e.Message); }
        }
    }


    [HarmonyPatch(typeof(ChemicalProductHelper), "GradeProduct")]
    internal static class VitaminOutputPatch
    {
        private static void Postfix(GameItem item)
        {
            try
            {
                if (item == null || item.identifier != VitaminContent.Id) return;
                VitaminContent.ApplyPurity(item);
            }
            catch (Exception) { }
        }
    }
}
