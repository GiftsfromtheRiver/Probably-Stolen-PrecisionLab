using System;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>Anti-radiation drug line:
    /// R1 radioactive waste + potassium iodide + acid at 80 C -> iron-cyanide complex
    ///    (below 80 C: low-purity complex, final drug is toxic)
    /// R2 gland extract + alcohol at room temperature -> crude SOD; purifier -> high-purity SOD
    /// R3 SOD + lipobond + N-hydroxymethyl base -> targeted microcapsule
    /// R4 complex + microcapsule + base -> crude anti-radiation drug; purifier -> API
    /// R5 API + water + trace reducing agent, pressed -> Purified anti-radiation tablet.
    /// </summary>
    internal static class AntiRadiationContent
    {
        internal const string FinalId = "anti_radiation_pill";
        internal const string RawId = "radioactive_isotope_waste";
        internal const string LowQualityTag = "ANTIRAD_LOW_QUALITY_TAG";
        internal const string ToxicTag = "ANTIRAD_TOXIC_TAG";

        internal static bool IsFinal(string id) => id == FinalId;

        internal static void EnsureRecipes()
        {
            try
            {
                if (ChemFinisherRecipe.GetRecipeById(FinalId) != null) return;
                ChemFinisherRecipe.chemFinisherRecipes.Add(new ChemFinisherRecipe
                {
                    identifier = FinalId,
                    displayName = Loc.Get("item.anti_radiation_pill.name", "「净化」抗辐射药片"),
                    requiredVolume = 5000,
                    isRequireDry = false,
                    activeIngredientId = CustomLiquids.AntiRadiationApi
                });
            }
            catch (Exception e) { Debug.LogWarning("[PrecisionLab] anti-radiation recipes failed: " + e.Message); }
        }

        internal static GameItem CreateRaw()
        {
            return RawItem(RawId, "放射性同位素废料", "放射性同位素废料。可用于提取螯合剂或作为反向催化剂；处理时请加热到80°C。",
                "radioactive_isotope_waste", CustomLiquids.RadioactiveWaste, 320L);
        }

        private static GameItem RawItem(string id, string name, string desc, string sprite, string liquid, long value)
        {
            GameItem item = ItemDirectory.CreateEmptyItem();
            ModSprites.Apply(item, sprite, "Items/items_material", "chemical2");
            item.SetShape(new GridShapeBuilder(1, 2).SetDataFill(1).Build());
            item.name = Loc.Get("item." + id + ".name", name);
            item.shortDescription = Loc.Get("item." + id + ".desc", desc);
            item.SetGameItemType("CHEMICAL_SUPPLIES");
            item.unitValue = value;
            ChemicalHelper.InitChemItem(item, isBlendable: true, isSoluble: true);
            ChemicalHelper.RemoveAllLiquid(item);
            string part = Liquid.GetCurrentPartFromId(liquid);
            if (part != null)
            {
                item.ModifyTag(part, delegate (TagState st) { st.Enable().SetInt(20000); }, skipValidate: true);
                item.ModifyTag("ACTIVATE_INGREDIENT_TAG", delegate (TagState st) { st.Enable().SetString(liquid); });
            }
            ModHelper.SetSource(item, Loc.ModId, ModEntry.ModDisplayName);
            return item;
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
                    if (item == null || item.identifier != FinalId) continue;
                    bool toxic = item.IsTag(ToxicTag);
                    item.ModifyTag("CHEMICAL_PRODUCT_PURITY_TAG", delegate (TagState state)
                    {
                        state.Enable().SetDouble(toxic ? 60.0 : 100.0);
                    });
                    ChemicalProductHelper.SetPurityFeature(item,
                        toxic ? ItemConditionList.CreateStreetStandard() : ItemConditionList.CreateLabPure());
                    if (toxic) item.unitValue = 450L;
                }
            }
            catch (Exception e) { Debug.LogWarning("[PrecisionLab] anti-radiation migration failed: " + e.Message); }
        }

        internal static GameItem CreateFinal()
        {
            GameItem item = ItemDirectory.CreateEmptyItem();
            ModSprites.Apply(item, "anti_radiation_pill", "Items/items_material", "chemical2");
            item.SetShape(new GridShapeBuilder(1, 2).SetDataFill(1).Build());
            item.name = Loc.Get("item.anti_radiation_pill.name", "「净化」抗辐射药片");
            item.shortDescription = Loc.Get("item.anti_radiation_pill.desc",
                "医用抗辐射药片。用于清除体内放射性铯/铊污染；配方错误或低温反应可能残留毒性。");
            item.SetGameItemType("MEDICAL");
            item.SetGameItemType("CHEMICAL_SUPPLIES");
            item.EnableTag("ITEM_MEDICAL_TAG");
            item.unitValue = 650L;
            ChemicalHelper.InitChemItem(item, isBlendable: true, isSoluble: true);
            ChemicalProductHelper.InitChemicalProduct(item, CustomLiquids.AntiRadiationApi);
            AdrenalineContent.SeedChemicalComposition(item, CustomLiquids.AntiRadiationApi, 5000);
            ModHelper.SetSource(item, Loc.ModId, ModEntry.ModDisplayName);
            return item;
        }
    }

    internal static class AntiRadiationChemistry
    {
        internal static int React(GameItem c)
        {
            if (c == null || WaterHelper.IsEmpty(c)) return 0;
            try
            {
                int steps = 0;
                steps += R1(c);
                steps += R2(c);
                steps += R3(c);
                steps += R4(c);
                if (steps > 0)
                {
                    try
                    {
                        ItemFeature f = WaterFeatureHelper.GetWaterFeature(c);
                        WaterFeatureHelper.OnLiquidChanged(c, f.IsExposed());
                    }
                    catch (Exception) { }
                }
                return steps;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] anti-radiation reaction failed: " + e.Message);
                return 0;
            }
        }

        private static int R1(GameItem c)
        {
            double t = Temp(c);
            if (t < 20.0 || t > 90.0) return 0;
            int waste = Amt(c, CustomLiquids.RadioactiveWaste);
            int iodide = Amt(c, CustomLiquids.Iodide);
            int acid = Amt(c, "acid");
            double ratio = Ratio(waste, 2000, iodide, 2000, acid, 1000);
            if (ratio <= 0) return 0;
            double step = ReactionMath.Fraction(ratio);
            if (step <= 0) return 0;
            Consume(c, CustomLiquids.RadioactiveWaste, 2000, step);
            Consume(c, CustomLiquids.Iodide, 2000, step);
            Consume(c, "acid", 1000, step);
            if (t >= 80.0)
            {
                Add(c, CustomLiquids.IronCyanideComplex, Scale(2000, step));
            }
            else
            {
                Add(c, CustomLiquids.IronCyanideComplexLow, Scale(2000, step));
                c.EnableTag(AntiRadiationContent.LowQualityTag);
            }
            return 1;
        }

        private static int R2(GameItem c)
        {
            double t = Temp(c);
            if (t < 20.0 || t > 29.0) return 0;
            int gland = Amt(c, CustomLiquids.GlandExtract);
            int alcohol = Amt(c, "alcohol");
            double ratio = Ratio(gland, 4000, alcohol, 8000);
            if (ratio <= 0) return 0;
            double step = ReactionMath.Fraction(ratio);
            if (step <= 0) return 0;
            Consume(c, CustomLiquids.GlandExtract, 4000, step);
            Consume(c, "alcohol", 8000, step);
            Add(c, CustomLiquids.SodCrude, Scale(4000, step));
            Add(c, "chemical_contaminant", Math.Max(1, Scale(200, step)));
            return 1;
        }

        private static int R3(GameItem c)
        {
            double t = Temp(c);
            if (t < 20.0 || t > 29.0) return 0;
            int sod = Amt(c, CustomLiquids.SodExtract);
            int lipo = Amt(c, "lipobond_agent");
            int hydrox = Amt(c, CustomLiquids.HydroxymethylBase);
            double ratio = Ratio(sod, 3000, lipo, 1000, hydrox, 1000);
            if (ratio <= 0) return 0;
            double step = ReactionMath.Fraction(ratio);
            if (step <= 0) return 0;
            Consume(c, CustomLiquids.SodExtract, 3000, step);
            Consume(c, "lipobond_agent", 1000, step);
            Consume(c, CustomLiquids.HydroxymethylBase, 1000, step);
            Add(c, CustomLiquids.TargetedMicrocapsule, Scale(3000, step));
            return 1;
        }

        private static int R4(GameItem c)
        {
            double t = Temp(c);
            if (t < 20.0 || t > 29.0) return 0;
            int pure = Amt(c, CustomLiquids.IronCyanideComplex);
            int low = Amt(c, CustomLiquids.IronCyanideComplexLow);
            int complex = pure + low;
            int micro = Amt(c, CustomLiquids.TargetedMicrocapsule);
            int baseAmt = Amt(c, "base");
            double ratio = Ratio(complex, 2000, micro, 2000, baseAmt, 500);
            if (ratio <= 0) return 0;
            double step = ReactionMath.Fraction(ratio);
            if (step <= 0) return 0;
            int take = Scale(2000, step);
            int takePure = Math.Min(pure, take);
            int takeLow = Math.Min(low, take - takePure);
            if (takePure > 0) Add(c, CustomLiquids.IronCyanideComplex, -takePure);
            if (takeLow > 0) Add(c, CustomLiquids.IronCyanideComplexLow, -takeLow);
            Consume(c, CustomLiquids.TargetedMicrocapsule, 2000, step);
            Consume(c, "base", 500, step);
            Add(c, CustomLiquids.AntiRadiationCrude, Scale(4000, step));
            if (takeLow > 0) c.EnableTag(AntiRadiationContent.LowQualityTag);
            return 1;
        }

        private static double Temp(GameItem c) => c.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble();
        private static int Amt(GameItem c, string id) => LiquidTools.GetLiquidAmount(c, id);

        private static double Ratio(params int[] pairs)
        {
            double r = 1.0;
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                int have = pairs[i], need = pairs[i + 1];
                if (have <= 0 || need <= 0) return 0;
                r = Math.Min(r, (double)have / need);
            }
            return Math.Min(1.0, r);
        }

        private static int Scale(int amount, double step) => (int)Math.Floor(amount * step);
        private static void Consume(GameItem c, string id, int amount, double step) => Add(c, id, -Math.Max(1, Scale(amount, step)));

        private static void Add(GameItem c, string id, int delta)
        {
            string part = Liquid.GetCurrentPartFromId(id);
            if (part == null) return;
            int next = Math.Max(0, c.GetTagReadonly(part).GetInt() + delta);
            c.ModifyTag(part, delegate (TagState st) { st.Enable().SetInt(next); }, skipValidate: true);
        }
    }

    /// <summary>Purifier-only conversions: crude SOD -> SOD extract; crude anti-rad drug -> API.</summary>
    internal static class AntiRadiationPurify
    {
        internal static int Process(GameItem c, int capacity)
        {
            if (c == null) return 0;
            // Convert crude even if a little contamination remains; the later press gate still
            // requires a clean container, so a second purifier pass may be needed.
            int budget = Math.Max(1, capacity);
            int converted = 0;
            converted += Convert(c, CustomLiquids.SodCrude, CustomLiquids.SodExtract, budget);
            budget = Math.Max(0, capacity - converted);
            if (budget > 0) converted += Convert(c, CustomLiquids.AntiRadiationCrude, CustomLiquids.AntiRadiationApi, budget);
            return converted;
        }

        private static int Convert(GameItem c, string crude, string product, int budget)
        {
            int have = LiquidTools.GetLiquidAmount(c, crude);
            if (have <= 0 || budget <= 0) return 0;
            int x = Math.Min(have, Math.Max(1, budget));
            Add(c, crude, -x);
            Add(c, product, x);
            return x;
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
    internal static class AntiRadiationPressGate
    {
        internal static bool PendingToxic;

        private static void Postfix(GameItem machine, GameSlotInventory containerSlot, ref bool __result)
        {
            try
            {
                if (machine == null || containerSlot == null || containerSlot.childItem == null) return;
                if (!machine.IsTag("SELECTED_CHEM_ID_TAG")) return;
                if (machine.GetTagReadonly("SELECTED_CHEM_ID_TAG").GetString() != AntiRadiationContent.FinalId) return;
                GameItem c = containerSlot.childItem;
                int total = WaterHelper.GetTotalVolume(c);
                int api = LiquidTools.GetLiquidAmount(c, CustomLiquids.AntiRadiationApi);
                int water = LiquidTools.GetLiquidAmount(c, "water");
                int reducing = LiquidTools.GetLiquidAmount(c, "reducing_agent");
                int contaminants = LiquidTools.GetLiquidAmount(c, "chemical_contaminant")
                    + LiquidTools.GetLiquidAmount(c, "physical_contaminant");
                __result = total >= 5000 && api >= 3500 && water >= 500 && reducing >= 100 && contaminants == 0;
                PendingToxic = c.IsTag(AntiRadiationContent.LowQualityTag);
            }
            catch (Exception e) { Debug.LogWarning("[PrecisionLab] anti-radiation press gate failed: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(ChemicalProductHelper), "GradeProduct")]
    internal static class AntiRadiationOutputPatch
    {
        private static void Postfix(GameItem item)
        {
            try
            {
                if (item == null || item.identifier != AntiRadiationContent.FinalId) return;
                if (AntiRadiationPressGate.PendingToxic)
                {
                    item.EnableTag(AntiRadiationContent.ToxicTag);
                    item.unitValue = 450L;
                    item.ModifyTag("CHEMICAL_PRODUCT_PURITY_TAG", delegate (TagState state)
                    {
                        state.Enable().SetDouble(60.0);
                    });
                    ChemicalProductHelper.SetPurityFeature(item, ItemConditionList.CreateStreetStandard());
                }
                else
                {
                    // Water/reducing agent are inert excipients, not impurities: a correctly
                    // pressed tablet is lab-grade even though the API fraction is below 99%.
                    item.ModifyTag("CHEMICAL_PRODUCT_PURITY_TAG", delegate (TagState state)
                    {
                        state.Enable().SetDouble(100.0);
                    });
                    ChemicalProductHelper.SetPurityFeature(item, ItemConditionList.CreateLabPure());
                }
                AntiRadiationPressGate.PendingToxic = false;
                // Keep the API composition so a spectrometer can read the tablet.
            }
            catch (Exception) { }
        }
    }
}
