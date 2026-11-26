using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Cultivator-produced antibiotic cultures and their four bottled products:
    /// broad-spectrum (cheap), gram-positive narrow, gram-negative narrow, and a single
    /// cocktail bottle whose tooltip lists whichever cultures were blended into it.
    /// </summary>
    internal static class AntibioticContent
    {
        internal const string BroadId = "antibiotic_broad";
        internal const string GramPosId = "antibiotic_gram_pos";
        internal const string GramNegId = "antibiotic_gram_neg";
        internal const string CocktailId = "antibiotic_cocktail";
        internal const int BroadValue = 25;
        internal const int NarrowValue = 50;

        internal const string StrainTag = "CULTIVATOR_STRAIN_TAG";
        internal const string StrainBroad = "broad";
        internal const string StrainGramPos = "gram_pos";
        internal const string StrainGramNeg = "gram_neg";
        internal const string StrainNone = "none";

        internal static bool IsAntibiotic(string id)
        {
            return id == BroadId || id == GramPosId || id == GramNegId || id == CocktailId;
        }

        internal static string CultureForStrain(string strain)
        {
            if (strain == StrainBroad) return CustomLiquids.AntibioticCultureBroad;
            if (strain == StrainGramPos) return CustomLiquids.AntibioticCultureGramPos;
            if (strain == StrainGramNeg) return CustomLiquids.AntibioticCultureGramNeg;
            return null;
        }

        internal static string ActiveForFinal(string id)
        {
            if (id == BroadId) return CustomLiquids.AntibioticConcentrateBroad;
            if (id == GramPosId) return CustomLiquids.AntibioticConcentrateGramPos;
            if (id == GramNegId) return CustomLiquids.AntibioticConcentrateGramNeg;
            return CustomLiquids.AntibioticConcentrateBroad; // cocktail gate checks all concentrates
        }

        internal static void EnsureRecipes()
        {
            try
            {
                AddRecipe(BroadId, "item.antibiotic_broad.name", "广谱抗生素", CustomLiquids.AntibioticConcentrateBroad);
                AddRecipe(GramPosId, "item.antibiotic_gram_pos.name", "窄谱抗生素（革兰阳性）", CustomLiquids.AntibioticConcentrateGramPos);
                AddRecipe(GramNegId, "item.antibiotic_gram_neg.name", "窄谱抗生素（革兰阴性）", CustomLiquids.AntibioticConcentrateGramNeg);
                AddRecipe(CocktailId, "item.antibiotic_cocktail.name", "鸡尾酒抗生素", CustomLiquids.AntibioticConcentrateBroad);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] antibiotic recipes failed: " + e.Message);
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

        private static GameItem CreateBottle(string id, string sprite, string locKey, string fallback,
            long value, string active, bool luxury, string flavor)
        {
            GameItem item = ItemDirectory.CreateEmptyItem();
            ModSprites.Apply(item, sprite, "Items/items_material", "chemical2");
            item.SetShape(new GridShapeBuilder(1, 2).SetDataFill(1).Build());
            item.name = Loc.Get(locKey + ".name", fallback);
            item.shortDescription = Loc.Get(locKey + ".desc",
                "广谱抗生素。价格便宜，适合批量销售。");
            item.flavorText = flavor;
            item.SetGameItemType("MEDICAL");
            item.SetGameItemType("CHEMICAL_SUPPLIES");
            if (luxury) item.SetGameItemType("LUXURY_ITEM");
            item.unitValue = value;
            item.EnableTag("ITEM_MEDICAL_TAG");
            ChemicalHelper.InitChemItem(item, isBlendable: true, isSoluble: true);
            ChemicalProductHelper.InitChemicalProduct(item, active);
            AdrenalineContent.SeedChemicalComposition(item, active, 5000);
            ModHelper.SetSource(item, Loc.ModId, ModEntry.ModDisplayName);
            return item;
        }

        internal static GameItem CreateBroad()
        {
            return CreateBottle(BroadId, "antibiotic_broad", "item.antibiotic_broad", "广谱抗生素",
                BroadValue, CustomLiquids.AntibioticConcentrateBroad, false,
                "广谱抗生素：便宜、覆盖面广，适合普通诊所。");
        }

        internal static GameItem CreateGramPos()
        {
            return CreateBottle(GramPosId, "antibiotic_gram_pos", "item.antibiotic_gram_pos", "窄谱抗生素（革兰阳性）",
                NarrowValue, CustomLiquids.AntibioticConcentrateGramPos, true,
                "窄谱抗生素：针对革兰阳性菌，产量低，价格高。");
        }

        internal static GameItem CreateGramNeg()
        {
            return CreateBottle(GramNegId, "antibiotic_gram_neg", "item.antibiotic_gram_neg", "窄谱抗生素（革兰阴性）",
                NarrowValue, CustomLiquids.AntibioticConcentrateGramNeg, true,
                "窄谱抗生素：针对革兰阴性菌，产量低，价格高。");
        }

        internal static GameItem CreateCocktail()
        {
            GameItem item = CreateBottle(CocktailId, "antibiotic_cocktail", "item.antibiotic_cocktail", "鸡尾酒抗生素",
                0, CustomLiquids.AntibioticConcentrateBroad, true,
                "鸡尾酒抗生素：把多种抗生素混合在一瓶里；标签会列出所含成分。");
            // Debug/directory template: same street-standard quality as other random stock.
            AntibioticOutputPatch.SeedSpawnedCocktail(item);
            return item;
        }

        internal static string CultureDisplay(string cultureId)
        {
            if (cultureId == CustomLiquids.AntibioticCultureBroad) return Loc.Get("liquid.antibiotic_culture_broad", "广谱抗生素培养液");
            if (cultureId == CustomLiquids.AntibioticCultureGramPos) return Loc.Get("liquid.antibiotic_culture_gram_pos", "革兰阳性抗生素培养液");
            if (cultureId == CustomLiquids.AntibioticCultureGramNeg) return Loc.Get("liquid.antibiotic_culture_gram_neg", "革兰阴性抗生素培养液");
            return cultureId;
        }

        internal static void AppendTooltip(RichTextBuilder builder, GameItem item)
        {
            try
            {
                string id = item.identifier;
                if (id == BroadId)
                {
                    builder.AddLine(Loc.Get("tooltip.antibiotic.broad", "广谱抗生素：覆盖范围广，价格较低；需要广谱菌群培养液5mL。"), true, RenderHandler.ColorPalette.LightBlue);
                }
                else if (id == GramPosId)
                {
                    builder.AddLine(Loc.Get("tooltip.antibiotic.gram_pos", "窄谱抗生素（革兰阳性）：针对性强，价格较高；需要革兰阳性菌群培养液5mL。"), true, RenderHandler.ColorPalette.LightPurple);
                }
                else if (id == GramNegId)
                {
                    builder.AddLine(Loc.Get("tooltip.antibiotic.gram_neg", "窄谱抗生素（革兰阴性）：针对性强，价格较高；需要革兰阴性菌群培养液5mL。"), true, RenderHandler.ColorPalette.LightGreen);
                }
                else if (id == CocktailId)
                {
                    builder.AddLine(Loc.Get("tooltip.antibiotic.cocktail", "鸡尾酒抗生素：至少含两种抗生素；标签列出所有成分。售价=成分价值总和×1.1（广谱50，窄谱100）。"), true, RenderHandler.ColorPalette.LightOrange);
                    int ingredientValue = item.IsTag("ANTIBIOTIC_INGREDIENT_VALUE")
                        ? item.GetTagReadonly("ANTIBIOTIC_INGREDIENT_VALUE").GetInt()
                        : (int)item.unitValue;
                    builder.AddLine(Loc.Fmt("tooltip.antibiotic.cocktail_price", "成分价值总和：{0} 信用点 × 1.1 = {1}", ingredientValue, item.unitValue), false, RenderHandler.ColorPalette.LightCyan);
                    builder.AddLine(Loc.Get("tooltip.antibiotic.cocktail_purity", "化学纯度：所有有效成分合计（不是单取一种成分）。"), false, RenderHandler.ColorPalette.LightCyan);
                    string[] keys =
                    {
                        "ANTIBIOTIC_CONTAINS_BROAD", "ANTIBIOTIC_CONTAINS_GRAM_POS", "ANTIBIOTIC_CONTAINS_GRAM_NEG"
                    };
                    string[] labels =
                    {
                        Loc.Get("antibiotic.broad.label", "广谱抗生素"),
                        Loc.Get("antibiotic.gram_pos.label", "革兰阳性窄谱"),
                        Loc.Get("antibiotic.gram_neg.label", "革兰阴性窄谱")
                    };
                    for (int i = 0; i < keys.Length; i++)
                        if (item.IsTag(keys[i]))
                            builder.AddLine("  · " + labels[i], false, RenderHandler.ColorPalette.LightCyan);
                }
            }
            catch (Exception) { }
        }
    }

    /// <summary>Adds the selected flora to the cultivator's output container each night.</summary>
    internal static class AntibioticCultivation
    {
        internal static string RequiredCatalyst(string strain)
        {
            if (strain == AntibioticContent.StrainGramPos) return "acid";
            if (strain == AntibioticContent.StrainGramNeg) return "base";
            return null;
        }

        internal static bool EnvironmentOk(GameItem machine, GameItem container, out string catalyst)
        {
            catalyst = null;
            try
            {
                if (machine == null || container == null) return true;
                string strain = machine.GetTagReadonly(AntibioticContent.StrainTag).GetString();
                catalyst = RequiredCatalyst(strain);
                if (catalyst == null) return true;
                int total = WaterHelper.GetTotalVolume(container);
                int amount = LiquidTools.GetLiquidAmount(container, catalyst);
                return total > 0 && amount * 100 >= total * 10;
            }
            catch (Exception)
            {
                return true;
            }
        }

        internal static int ConsumeCatalyst(GameItem container, string catalyst, int cultureAmount)
        {
            try
            {
                if (container == null || string.IsNullOrEmpty(catalyst) || cultureAmount <= 0) return 0;
                int amount = LiquidTools.GetLiquidAmount(container, catalyst);
                // Catalyst consumption follows the actual culture output: about 9-11% of output.
                double fraction = 0.09 + UnityEngine.Random.value * 0.02;
                int consume = Math.Min(amount, Math.Max(1, (int)Math.Round(cultureAmount * fraction)));
                if (consume <= 0) return 0;
                string part = Liquid.GetCurrentPartFromId(catalyst);
                if (part == null) return 0;
                container.ModifyTag(part, delegate (TagState state)
                {
                    state.Enable().SetInt(Math.Max(0, amount - consume));
                });
                WaterFeatureHelper.OnLiquidChanged(container, true);
                return consume;
            }
            catch (Exception) { return 0; }
        }

        internal static int Produce(GameItem machine, GameItem container, int processed, int performanceOverride = -1)
        {
            try
            {
                if (machine == null || container == null || processed <= 0) return 0;
                string strain = machine.GetTagReadonly(AntibioticContent.StrainTag).GetString();
                string culture = AntibioticContent.CultureForStrain(strain);
                if (string.IsNullOrEmpty(culture)) return 0;
                int free = WaterHelper.GetFreeCapacity(container);
                if (free <= 0) return 0;
                // Performance modules multiply culture yield per processed sugar.
                int performance = performanceOverride >= 0 ? performanceOverride : MachineryHelper.GetCurrentPerformanceBonus(machine);
                int desiredCulture = (int)Math.Round(processed * (1.0 + Mathf.Clamp(performance, 0, 300) / 100.0));
                int quality = MachineryHelper.GetCurrentQualityBonus(machine);
                double chemicalRatio = Math.Max(0.04, 0.16 - 0.0004 * Mathf.Clamp(quality, 0, 200));
                double physicalRatio = Math.Max(0.02, 0.08 - 0.0002 * Mathf.Clamp(quality, 0, 200));
                int totalNeeded = (int)Math.Round(desiredCulture * (1.0 + chemicalRatio + physicalRatio));
                if (totalNeeded > free) totalNeeded = free;
                int amount = (int)Math.Round(totalNeeded / (1.0 + chemicalRatio + physicalRatio));
                int chemical = (int)Math.Round(amount * chemicalRatio);
                int physical = (int)Math.Round(amount * physicalRatio);
                if (amount <= 0) return 0;
                WaterHelper.AddLiquid(container, culture, amount);
                if (chemical > 0) WaterHelper.AddLiquid(container, "chemical_contaminant", chemical);
                if (physical > 0) WaterHelper.AddLiquid(container, "physical_contaminant", physical);
                WaterFeatureHelper.OnLiquidChanged(container, true);
                ConsumeCatalyst(container, RequiredCatalyst(strain), amount);
                return amount;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] antibiotic culture production failed: " + e.Message);
                return 0;
            }
        }

    }

    internal static class AntibioticPurify
    {
        /// <summary>Returns the amount of crude culture converted this cycle.</summary>
        internal static int Process(GameItem c, int capacity)
        {
            if (c == null) return 0;
            // Convert even if trace contamination remains: the press gate still rejects dirty
            // concentrate, so the player can clean it in a later purifier pass. Requiring exactly
            // 0 contaminants made low-quality purifiers appear to do nothing at all.
            int budget = Math.Max(1, capacity);
            int converted = 0;
            converted += Convert(c, CustomLiquids.AntibioticCultureBroad, CustomLiquids.AntibioticConcentrateBroad, budget);
            budget -= converted;
            if (budget > 0) converted += Convert(c, CustomLiquids.AntibioticCultureGramPos, CustomLiquids.AntibioticConcentrateGramPos, budget);
            budget = Math.Max(0, capacity - converted);
            if (budget > 0) converted += Convert(c, CustomLiquids.AntibioticCultureGramNeg, CustomLiquids.AntibioticConcentrateGramNeg, budget);
            return converted;
        }

        private static void SetAmount(GameItem c, string id, int amount)
        {
            string part = Liquid.GetCurrentPartFromId(id);
            if (part == null) return;
            c.ModifyTag(part, delegate (TagState st) { st.Enable().SetInt(Math.Max(0, amount)); }, skipValidate: true);
        }

        private static void AddAmount(GameItem c, string id, int amount)
        {
            SetAmount(c, id, LiquidTools.GetLiquidAmount(c, id) + amount);
        }

        private static int Convert(GameItem c, string crudeId, string concentrateId, int budget)
        {
            int crude = LiquidTools.GetLiquidAmount(c, crudeId);
            if (crude <= 0 || budget <= 0) return 0;
            int converted = Math.Min(crude, Math.Max(1, budget));
            int concentrate = (int)Math.Round(converted * 0.8);
            int waste = converted - concentrate;
            SetAmount(c, crudeId, crude - converted);
            AddAmount(c, concentrateId, concentrate);
            if (waste > 0) AddAmount(c, "organic_waste", waste);
            return converted;
        }
    }

    [HarmonyPatch(typeof(MachineChemFinisher), "CanOutput")]
    internal static class AntibioticPressGate
    {
        internal static string PendingCocktailTags = "";
        internal static double PendingCocktailPurity = 0.0;

        private static void Postfix(GameItem machine, GameSlotInventory containerSlot, ref bool __result)
        {
            try
            {
                if (machine == null || containerSlot == null || containerSlot.childItem == null) return;
                if (!machine.IsTag("SELECTED_CHEM_ID_TAG")) return;
                string id = machine.GetTagReadonly("SELECTED_CHEM_ID_TAG").GetString();
                if (!AntibioticContent.IsAntibiotic(id)) return;
                GameItem c = containerSlot.childItem;
                int total = WaterHelper.GetTotalVolume(c);
                int contaminants = LiquidTools.GetLiquidAmount(c, "chemical_contaminant")
                    + LiquidTools.GetLiquidAmount(c, "physical_contaminant");
                if (id == AntibioticContent.CocktailId)
                {
                    int broad = LiquidTools.GetLiquidAmount(c, CustomLiquids.AntibioticConcentrateBroad);
                    int pos = LiquidTools.GetLiquidAmount(c, CustomLiquids.AntibioticConcentrateGramPos);
                    int neg = LiquidTools.GetLiquidAmount(c, CustomLiquids.AntibioticConcentrateGramNeg);
                    int count = (broad >= 1000 ? 1 : 0) + (pos >= 1000 ? 1 : 0) + (neg >= 1000 ? 1 : 0);
                    __result = total >= 5000 && count >= 2 && (broad + pos + neg) * 100 >= total * 90;
                    PendingCocktailTags = (broad >= 1000 ? "B" : "") + (pos >= 1000 ? "P" : "") + (neg >= 1000 ? "N" : "");
                    PendingCocktailPurity = total > 0 ? Math.Min(100.0, (broad + pos + neg) * 100.0 / total) : 0.0;
                }
                else
                {
                    string concentrate = AntibioticContent.ActiveForFinal(id);
                    int amount = LiquidTools.GetLiquidAmount(c, concentrate);
                    __result = total >= 5000 && amount >= 4000
                        && amount * 100 >= total * 90
                        && contaminants == 0;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] antibiotic press gate failed: " + e.Message);
            }
        }
    }

    [HarmonyPatch(typeof(ChemicalProductHelper), "GradeProduct")]
    internal static class AntibioticOutputPatch
    {
        private static void Postfix(GameItem item)
        {
            try
            {
                if (item == null || !AntibioticContent.IsAntibiotic(item.identifier)) return;
                if (item.identifier == AntibioticContent.CocktailId)
                {
                    ClearContainsTags(item);
                    string pending = AntibioticPressGate.PendingCocktailTags;
                    int ingredientValue = 0;
                    if (!string.IsNullOrEmpty(pending))
                    {
                        if (pending.Contains("B")) item.EnableTag("ANTIBIOTIC_CONTAINS_BROAD");
                        if (pending.Contains("P")) item.EnableTag("ANTIBIOTIC_CONTAINS_GRAM_POS");
                        if (pending.Contains("N")) item.EnableTag("ANTIBIOTIC_CONTAINS_GRAM_NEG");
                        ingredientValue = (pending.Contains("B") ? AntibioticContent.BroadValue : 0)
                            + (pending.Contains("P") ? AntibioticContent.NarrowValue : 0)
                            + (pending.Contains("N") ? AntibioticContent.NarrowValue : 0);
                        AntibioticPressGate.PendingCocktailTags = "";
                    }
                    else
                    {
                        if (LiquidTools.GetLiquidAmount(item, CustomLiquids.AntibioticConcentrateBroad) >= 500)
                            item.EnableTag("ANTIBIOTIC_CONTAINS_BROAD");
                        if (LiquidTools.GetLiquidAmount(item, CustomLiquids.AntibioticConcentrateGramPos) >= 500)
                            item.EnableTag("ANTIBIOTIC_CONTAINS_GRAM_POS");
                        if (LiquidTools.GetLiquidAmount(item, CustomLiquids.AntibioticConcentrateGramNeg) >= 500)
                            item.EnableTag("ANTIBIOTIC_CONTAINS_GRAM_NEG");
                        ingredientValue = (item.IsTag("ANTIBIOTIC_CONTAINS_BROAD") ? AntibioticContent.BroadValue : 0)
                            + (item.IsTag("ANTIBIOTIC_CONTAINS_GRAM_POS") ? AntibioticContent.NarrowValue : 0)
                            + (item.IsTag("ANTIBIOTIC_CONTAINS_GRAM_NEG") ? AntibioticContent.NarrowValue : 0);
                    }
                    item.unitValue = (long)Math.Round(ingredientValue * 1.1);
                    item.ModifyTag("ANTIBIOTIC_INGREDIENT_VALUE", delegate (TagState state)
                    {
                        state.Enable().SetInt(ingredientValue);
                    });
                    double pendingPurity = AntibioticPressGate.PendingCocktailPurity;
                    AntibioticPressGate.PendingCocktailPurity = 0.0;
                    if (pendingPurity > 0.0) ApplyCocktailPurity(item, pendingPurity);
                    else ApplyCocktailPurity(item);
                    EnsureCocktailComposition(item);
                }
                else
                {
                    EnsureSingleComposition(item);
                }
                // Keep the transferred composition so spectrometers can read the antibiotics.
            }
            catch (Exception) { }
        }

        internal static void ClearContainsTags(GameItem item)
        {
            try
            {
                item.DisableTag("ANTIBIOTIC_CONTAINS_BROAD");
                item.DisableTag("ANTIBIOTIC_CONTAINS_GRAM_POS");
                item.DisableTag("ANTIBIOTIC_CONTAINS_GRAM_NEG");
            }
            catch (Exception) { }
        }

        internal static void EnsureSingleComposition(GameItem item)
        {
            try
            {
                if (item == null) return;
                string active = AntibioticContent.ActiveForFinal(item.identifier);
                if (string.IsNullOrEmpty(active)) return;
                int existing = ChemAmount(item, active);
                if (existing > 0) return;
                CustomLiquids.EnsureRegistered();
                AddChemPart(item, active, 5000);
                item.ModifyTag("ACTIVATE_INGREDIENT_TAG", delegate (TagState state)
                {
                    state.Enable().SetString(active);
                });
            }
            catch (Exception) { }
        }

        internal static void SeedSpawnedCocktail(GameItem item)
        {
            try
            {
                if (item == null || item.identifier != AntibioticContent.CocktailId) return;
                ClearContainsTags(item);
                ChemicalHelper.RemoveAllLiquid(item);
                // Spawned/stock cocktail: ~65% effective ingredients, rest impurities.
                AddChemPart(item, CustomLiquids.AntibioticConcentrateBroad, 1084);
                AddChemPart(item, CustomLiquids.AntibioticConcentrateGramPos, 1083);
                AddChemPart(item, CustomLiquids.AntibioticConcentrateGramNeg, 1083);
                AddChemPart(item, "chemical_contaminant", 1000);
                AddChemPart(item, "physical_contaminant", 750);
                item.EnableTag("ANTIBIOTIC_CONTAINS_BROAD");
                item.EnableTag("ANTIBIOTIC_CONTAINS_GRAM_POS");
                item.EnableTag("ANTIBIOTIC_CONTAINS_GRAM_NEG");
                int ingredientValue = AntibioticContent.BroadValue + AntibioticContent.NarrowValue + AntibioticContent.NarrowValue;
                item.unitValue = (long)Math.Round(ingredientValue * 1.1);
                item.ModifyTag("ANTIBIOTIC_INGREDIENT_VALUE", delegate (TagState state)
                {
                    state.Enable().SetInt(ingredientValue);
                });
                item.ModifyTag("ACTIVATE_INGREDIENT_TAG", delegate (TagState state)
                {
                    state.Enable().SetString(CustomLiquids.AntibioticConcentrateBroad);
                });
                // 3250 effective / 5000 total = 65%, street standard.
                ApplyCocktailPurity(item, 65.0);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] spawning cocktail stock failed: " + e.Message);
            }
        }

        internal static void EnsureCocktailComposition(GameItem item)
        {
            try
            {
                if (item == null || item.identifier != AntibioticContent.CocktailId) return;
                CustomLiquids.EnsureRegistered();
                // Final antibiotic bottles are CHEM_ITEMs, not liquid containers:
                // LiquidTools.GetLiquidAmount would always return 0 here.
                int existing = ChemAmount(item, CustomLiquids.AntibioticConcentrateBroad)
                    + ChemAmount(item, CustomLiquids.AntibioticConcentrateGramPos)
                    + ChemAmount(item, CustomLiquids.AntibioticConcentrateGramNeg);
                if (existing > 0) return;
                int b = item.IsTag("ANTIBIOTIC_CONTAINS_BROAD") ? 1667 : 0;
                int p = item.IsTag("ANTIBIOTIC_CONTAINS_GRAM_POS") ? 1667 : 0;
                int n = item.IsTag("ANTIBIOTIC_CONTAINS_GRAM_NEG") ? 1666 : 0;
                if (b + p + n <= 0) return;
                AddChemPart(item, CustomLiquids.AntibioticConcentrateBroad, b);
                AddChemPart(item, CustomLiquids.AntibioticConcentrateGramPos, p);
                AddChemPart(item, CustomLiquids.AntibioticConcentrateGramNeg, n);
                item.ModifyTag("ACTIVATE_INGREDIENT_TAG", delegate (TagState state)
                {
                    state.Enable().SetString(CustomLiquids.AntibioticConcentrateBroad);
                });
            }
            catch (Exception) { }
        }

        private static int ChemAmount(GameItem item, string liquidId)
        {
            string part = Liquid.GetCurrentPartFromId(liquidId);
            return part == null ? 0 : item.GetTagReadonly(part).GetInt();
        }

        private static void AddChemPart(GameItem item, string liquidId, int amount)
        {
            if (amount <= 0) return;
            string part = Liquid.GetCurrentPartFromId(liquidId);
            if (part == null)
            {
                Debug.LogWarning("[PrecisionLab] unknown antibiotic composition liquid: " + liquidId);
                return;
            }
            item.EnableTag(part);
            item.ModifyTag(part, delegate (TagState state)
            {
                state.Enable().SetInt(state.GetInt() + amount);
            });
        }

        internal static void ApplyCocktailPurity(GameItem item, double knownPurity = -1.0)
        {
            try
            {
                double purity = knownPurity;
                if (purity < 0.0)
                {
                    int total = ChemicalHelper.GetTotalVolume(item);
                    if (total <= 0) return;
                    int effective = LiquidTools.GetLiquidAmount(item, CustomLiquids.AntibioticConcentrateBroad)
                        + LiquidTools.GetLiquidAmount(item, CustomLiquids.AntibioticConcentrateGramPos)
                        + LiquidTools.GetLiquidAmount(item, CustomLiquids.AntibioticConcentrateGramNeg);
                    purity = Math.Min(100.0, effective * 100.0 / total);
                }
                item.ModifyTag("CHEMICAL_PRODUCT_PURITY_TAG", delegate (TagState state)
                {
                    state.Enable().SetDouble(purity);
                });
                if (purity >= 99.0)
                    ChemicalProductHelper.SetPurityFeature(item, ItemConditionList.CreateLabPure());
                else if (purity >= 90.0)
                    ChemicalProductHelper.SetPurityFeature(item, ItemConditionList.CreatePharmaGrade());
                else if (purity >= 80.0)
                    ChemicalProductHelper.SetPurityFeature(item, ItemConditionList.CreateNearPharma());
                else if (purity >= 60.0)
                    ChemicalProductHelper.SetPurityFeature(item, ItemConditionList.CreateStreetStandard());
                else if (purity >= 45.0)
                    ChemicalProductHelper.SetPurityFeature(item, ItemConditionList.CreateCutProduct());
                else if (purity >= 30.0)
                    ChemicalProductHelper.SetPurityFeature(item, ItemConditionList.CreateJunkGrade());
                else
                    ChemicalProductHelper.SetPurityFeature(item, ItemConditionList.CreateStarchGrade());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] cocktail purity calculation failed: " + e.Message);
            }
        }
    }
}
