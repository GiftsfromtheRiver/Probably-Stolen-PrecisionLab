using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Diagnostic that runs once per session, after a save has been loaded into gameplay,
    /// and writes the result to Player.log prefixed with "[PrecisionLab] SELFTEST".
    ///
    /// It exercises the REAL item factories and the REAL liquid maths. The test items are not
    /// attached to any inventory UI element, so UI/feature refresh is suppressed for them
    /// (ItemFactory.SuppressUiRefresh) and all assertions read the item tags directly.
    /// </summary>
    internal static class SelfTest
    {
        private const string Prefix = "[PrecisionLab] SELFTEST ";

        internal static void Run(ModLog log)
        {
            GameItem extractor = null;
            GameItem target = null;
            GameItem product = null;
            GameItem machine = null;
            GameItem cultivator = null;
            GameItem hydro = null;
            GameItem sugarCup = null;
            GameItem fruit = null;
            GameItem adrenalineCup = null;
            GameItem apiItem = null;
            GameItem penItem = null;
            GameItem boxItem = null;
            GameItem noteItem = null;
            GameItem recycleCup = null;
            GameItem naloxoneCup = null;
            GameItem naloxoneAlcoholCup = null;
            GameItem naloxoneApiItem = null;
            GameItem naloxoneBoxItem = null;
            GameItem pillCup = null;
            GameItem sannaiCup = null;
            GameItem birthControlBoxItem = null;
            GameItem sannaiBoxItem = null;
            GameItem synaprestCup = null;
            GameItem synaprestBoxItem = null;
            GameItem synaprestInjectorItem = null;
            GameItem immunivaxCup = null;
            GameItem immunivaxWasteCup = null;
            GameItem immuneSerumItem = null;
            GameItem blueGelCup = null;
            GameItem blueGelWasteCup = null;
            GameItem bioGelBottleItem = null;
            GameItem flashCup = null;
            GameItem smokeCup = null;
            GameItem chargeCup = null;
            GameItem chargeHotCup = null;
            GameItem mysteryCrateItem = null;
            GameItem neuroCup = null;
            GameItem pheromoneCup = null;
            GameItem pheromoneHotCup = null;
            GameItem perfumeKitItem = null;
            GameItem resolvingBottleItem = null;
            GameItem resolvingRefillItem = null;
            GameItem recipeBookItem = null;
            GameItem tourniquetItem = null;
            GameItem fabricSample = null;
            GameItem scrapForCondition = null;
            GameItem ingotForCondition = null;
            GameItem tqWorn = null;
            GameItem tqExcellent = null;
            GameItem tqAged = null;
            try
            {
                if (ItemMouseDragHandler.current == null)
                {
                    log.Log("SELFTEST skipped: gameplay UI not ready yet");
                    return;
                }

                ItemFactory.SuppressUiRefresh = true;
                extractor = ItemFactory.CreateExtractor();
                MachinerySlider extractorSlider = ExtractorWindow.LastCreatedSlider;
                target = ItemFactory.CreateExtractor();
                product = ItemFactory.CreateExtractor();
                machine = ItemFactory.CreatePurifier();
                // Created directly through the factory (not DirectoryMaster.Item), so set the
                // identifier the helper uses to recognise the machine (DirectoryMaster normally
                // assigns it after the factory returns).
                machine.identifier = ItemIds.Purifier;
                GeneralHelper.SetItemOwned(extractor, true);
                GeneralHelper.SetItemOwned(target, true);
                GeneralHelper.SetItemOwned(product, true);
                GeneralHelper.SetItemOwned(machine, true);

                // The purifier must ship with its instruction note inserted in the note slot.
                // NOTE: the note is added by PurifierManualHelper (post-DirectoryMaster); the
                // selftest creates the machine directly, so emulate that post-create step here.
                PurifierManualHelper.TryEnsure(machine);
                // Storage indices: power=0, module=1, container=2, waste=3, note=4.
                GameSlotInventory noteSlotInv = MachineHelper.GetSlot(machine, 4) as GameSlotInventory;
                GameItem manual = noteSlotInv != null ? noteSlotInv.childItem : null;
                bool manualOk = manual != null && manual.IsTag("MACHINE_MANUAL_TAG");

                log.Log(Prefix + "items extractor='"
                    + extractor.name + "' capacity=" + WaterHelper.GetTotalCapacity(extractor) + "µL dose="
                    + LiquidTools.GetDose(extractor) + "µL window=" + (extractor.contentWindow != null)
                    + " | purifier='"
                    + machine.name + "' power=" + MachineryHelper.GetMachinePowerUsage(machine)
                    + " qualityCap=" + LiquidTools.GetPurityCap(machine) + "bp"
                    + " manual=" + manualOk + " manualName='" + (manual != null ? manual.name : "-") + "'");

                // --- 0. Direction modes (auto / draw / inject) -----------------------
                bool modesEmpty = LiquidTools.GetMode(extractor) == LiquidTools.ModeAuto
                    && LiquidTools.ResolveDirection(extractor);
                LiquidTools.SetMode(extractor, LiquidTools.ModeDraw);
                bool modesDraw = LiquidTools.GetMode(extractor) == LiquidTools.ModeDraw && LiquidTools.ResolveDirection(extractor);
                LiquidTools.SetMode(extractor, LiquidTools.ModeInject);
                bool modesInject = LiquidTools.GetMode(extractor) == LiquidTools.ModeInject && !LiquidTools.ResolveDirection(extractor);
                LiquidTools.SetMode(extractor, LiquidTools.ModeAuto);
                log.Log(Prefix + "direction modes: autoEmptyDraws=" + modesEmpty + " forcedDraw=" + modesDraw
                    + " forcedInject=" + modesInject + " (auto restored)");

                // --- 1. Exact transfer ------------------------------------------------
                // Fill with raw tag writes: WaterHelper.AddLiquid would refresh sprites/UI and
                // needs the item to be attached to a live inventory element.
                LiquidTools.SetLiquidRaw(extractor, "water", 7000);
                LiquidTools.SetLiquidRaw(extractor, "microbe", 600);
                LiquidTools.SetLiquidRaw(extractor, "heavy_metal", 400);
                LiquidTools.SetLiquidRaw(extractor, "physical_contaminant", 300);
                int totalBefore = WaterHelper.GetTotalVolume(extractor);
                int purityBefore = Purity(extractor);
                // Auto mode with a filled pipette must resolve to "inject" (the user's bug report:
                // drawing more of the same liquid into a non-empty pipette needed the forced mode).
                bool autoFilledInjects = !LiquidTools.ResolveDirection(extractor);

                int moved = LiquidTools.MoveExact(extractor, target, 1234);
                int targetTotal = WaterHelper.GetTotalVolume(target);
                int sourceTotal = WaterHelper.GetTotalVolume(extractor);
                int targetPurity = Purity(target);
                log.Log(Prefix + "exact transfer: moved=" + moved + "µL targetTotal=" + targetTotal
                    + "µL (expect 1234) sourceTotal=" + sourceTotal + "µL (expect " + (totalBefore - moved) + ")"
                    + " targetPurity=" + targetPurity + "bp sourcePurity=" + Purity(extractor) + "bp");

                // The tool must refuse an un-deliverable request and move nothing.
                int overRequest = LiquidTools.MoveExact(extractor, target, 999999);
                int afterOverRequest = WaterHelper.GetTotalVolume(target);
                log.Log(Prefix + "exact-or-nothing: overRequest=" + overRequest + "µL targetVolume="
                    + afterOverRequest + "µL (expect unchanged " + targetTotal + ")");

                // --- 2. Purifier, clean-water accounting (water + contamination only) -
                int cleanTotalBefore = WaterHelper.GetTotalVolume(extractor);
                int cleanWaterBefore = LiquidTools.GetLiquidAmount(extractor, "water");
                int cleanPurityBefore = Purity(extractor);
                int cleanRemoved = LiquidTools.PurifyContaminants(extractor, machine, out LiquidTools.PurifyAccounting cleanMode);
                int cleanTotalAfter = WaterHelper.GetTotalVolume(extractor);
                int cleanWaterAfter = LiquidTools.GetLiquidAmount(extractor, "water");
                int cleanPurityAfter = Purity(extractor);
                int cleanCap = LiquidTools.GetPurityCap(machine);
                log.Log(Prefix + "purify/clean water: mode=" + cleanMode + " removed=" + cleanRemoved
                    + "µL total=" + cleanTotalBefore + "->" + cleanTotalAfter + "µL (expect unchanged)"
                    + " water=" + cleanWaterBefore + "->" + cleanWaterAfter + "µL"
                    + " purity=" + cleanPurityBefore + "->" + cleanPurityAfter + "bp cap=" + cleanCap + "bp");

                // --- 3. Purifier, concentrate accounting (valuable product present) ---
                LiquidTools.SetLiquidRaw(product, "water", 3000);
                LiquidTools.SetLiquidRaw(product, "dream_cap_extract", 4000);
                LiquidTools.SetLiquidRaw(product, "microbe", 300);
                LiquidTools.SetLiquidRaw(product, "debris", 200);
                int concContaminantBefore = LiquidTools.GetContaminantAmount(product);
                int concTotalBefore = WaterHelper.GetTotalVolume(product);
                int concProductBefore = LiquidTools.GetLiquidAmount(product, "dream_cap_extract");
                int concPurityBefore = Purity(product);
                int concRemoved = LiquidTools.PurifyContaminants(product, machine, out LiquidTools.PurifyAccounting concMode);
                int concTotalAfter = WaterHelper.GetTotalVolume(product);
                int concProductAfter = LiquidTools.GetLiquidAmount(product, "dream_cap_extract");
                int concPurityAfter = Purity(product);
                int concContaminantAfter = LiquidTools.GetContaminantAmount(product);
                log.Log(Prefix + "purify/concentrate: mode=" + concMode + " removed=" + concRemoved
                    + "µL total=" + concTotalBefore + "->" + concTotalAfter + "µL (expect -removed)"
                    + " product=" + concProductBefore + "->" + concProductAfter + "µL (expect unchanged)"
                    + " purity=" + concPurityBefore + "->" + concPurityAfter + "bp"
                    + " contaminants=" + concContaminantBefore + "->" + concContaminantAfter + "µL");

                // --- 4. Starch hydrolysis (starch + acid at 40-60 C -> sugar) ---------
                hydro = ItemFactory.CreateExtractor();
                GeneralHelper.SetItemOwned(hydro, true);
                LiquidTools.SetLiquidRaw(hydro, "water", 3000);
                LiquidTools.SetLiquidRaw(hydro, "starch", 1000);
                LiquidTools.SetLiquidRaw(hydro, "acid", 200);
                int hydroTotalBefore = WaterHelper.GetTotalVolume(hydro);
                SetTemperature(hydro, 50.0);
                int hydrolyzed = StarchHydrolysis.React(hydro, false);
                int starchAfter = LiquidTools.GetLiquidAmount(hydro, "starch");
                int sugarAfter = LiquidTools.GetLiquidAmount(hydro, "sugar");
                int acidAfter = LiquidTools.GetLiquidAmount(hydro, "acid");
                int hydroTotalAfter = WaterHelper.GetTotalVolume(hydro);
                SetTemperature(hydro, 30.0);
                int coldTick = StarchHydrolysis.React(hydro, false);
                log.Log(Prefix + "starch hydrolysis: converted=" + hydrolyzed + "µL starch=" + 1000 + "->" + starchAfter
                    + "µL sugar=" + sugarAfter + "µL acid=" + acidAfter + "µL (expect unchanged 200)"
                    + " total=" + hydroTotalBefore + "->" + hydroTotalAfter + "µL (expect unchanged)"
                    + " coldTick=" + coldTick + "µL (expect 0)");

                // --- 5. Microbial cultivator: sugar -> organic waste ------------------
                sugarCup = ItemFactory.CreateExtractor();
                GeneralHelper.SetItemOwned(sugarCup, true);
                LiquidTools.SetLiquidRaw(sugarCup, "water", 2000);
                LiquidTools.SetLiquidRaw(sugarCup, "sugar", 5000);
                int sugarTotalBefore = WaterHelper.GetTotalVolume(sugarCup);
                int digested = LiquidTools.DigestSugar(sugarCup, 0, 0, 100);
                int sugarLeft = LiquidTools.GetLiquidAmount(sugarCup, "sugar");
                int wasteAfter = LiquidTools.GetLiquidAmount(sugarCup, "organic_waste");
                int sugarTotalAfter = WaterHelper.GetTotalVolume(sugarCup);
                int dormantDigest = LiquidTools.DigestSugar(sugarCup, 0, 0, 0);
                int sugarAfterDormant = LiquidTools.GetLiquidAmount(sugarCup, "sugar");
                log.Log(Prefix + "cultivator digest: digested=" + digested + "µL (expect 2000)"
                    + " sugar=" + 5000 + "->" + sugarLeft + "µL"
                    + " organicWaste=" + wasteAfter + "µL (expect 400)"
                    + " total=" + sugarTotalBefore + "->" + sugarTotalAfter + "µL (expect -1600)"
                    + " dormantDigest=" + dormantDigest + "µL (expect 0) sugarWhileDormant=" + sugarAfterDormant + "µL");

                // --- 6. Cultivator machine factory + instruction manual ---------------
                cultivator = ItemFactory.CreateCultivator();
                cultivator.identifier = ItemIds.Cultivator;
                GeneralHelper.SetItemOwned(cultivator, true);
                PurifierManualHelper.TryEnsure(cultivator);
                // Storage indices with the nutrient slot: power=0, module=1, container=2,
                // nutrient=3, note=4 (the culture text panel is display-only).
                GameSlotInventory cultivatorNoteSlot = MachineHelper.GetSlot(cultivator, 4) as GameSlotInventory;
                GameItem cultivatorManual = cultivatorNoteSlot != null ? cultivatorNoteSlot.childItem : null;
                bool cultivatorManualOk = cultivatorManual != null && cultivatorManual.IsTag("MACHINE_MANUAL_TAG");
                int cultivatorActivity = ItemFactory.GetCultivatorActivity(cultivator);

                // The nutrient slot only accepts nutrifruit; pressing a huge fruit (+60) wakes a
                // dormant culture (activity 0 -> 60) and consumes it.
                fruit = DirectoryMaster.Item("nutrifruit");
                if (fruit != null)
                {
                    GeneralHelper.SetItemOwned(fruit, true);
                    fruit.unitValue = 32L; // huge nutrifruit
                }
                GameSlotInventory nutrientSlot = MachineHelper.GetSlot(cultivator, 3) as GameSlotInventory;
                if (nutrientSlot != null && fruit != null)
                {
                    GraphUtils.TryAcceptAll(nutrientSlot, fruit);
                }
                bool fruitAccepted = nutrientSlot != null && nutrientSlot.childItem == fruit;
                cultivator.ModifyTag(Tags.CultivatorActivity, delegate (TagState state)
                {
                    state.Enable().SetInt(0);
                });
                int pressed = ItemFactory.PressNutrientFruit(cultivator, nutrientSlot);
                int activityAfterFeed = ItemFactory.GetCultivatorActivity(cultivator);
                bool fruitConsumed = nutrientSlot != null && nutrientSlot.childItem == null;
                string cultureAt60 = ItemFactory.FormatCulturePanel(cultivator);
                cultivator.ModifyTag(Tags.CultivatorActivity, delegate (TagState state)
                {
                    state.Enable().SetInt(0);
                });
                string cultureDormant = ItemFactory.FormatCulturePanel(cultivator);
                cultivator.ModifyTag(Tags.CultivatorActivity, delegate (TagState state)
                {
                    state.Enable().SetInt(100);
                });
                bool cultureDisplayOk = cultivator.onOneSecondItemFunc != null
                    && cultureAt60 != null && cultureAt60.Contains("60%")
                    && cultureDormant == Loc.Get("tooltip.cultivator.dormant_panel", "Dormant · needs nutrifruit");
                log.Log(Prefix + "cultivator machine: window=" + (cultivator.contentWindow != null)
                    + " power=" + MachineryHelper.GetMachinePowerUsage(cultivator)
                    + " activity=" + cultivatorActivity + "% manual=" + cultivatorManualOk
                    + " manualName='" + (cultivatorManual != null ? cultivatorManual.name : "-") + "'"
                    + " nutrientSlot=" + (nutrientSlot != null) + " fruitAccepted=" + fruitAccepted
                    + " pressed=+" + pressed + "% activityAfterFeed=" + activityAfterFeed + "%"
                    + " fruitConsumed=" + fruitConsumed + " cultureDisplay=" + cultureDisplayOk);

                // --- 7. Adrenaline production chain (six reaction steps) ---------------
                adrenalineCup = ItemFactory.CreateExtractor();
                GeneralHelper.SetItemOwned(adrenalineCup, true);
                LiquidTools.SetLiquidRaw(adrenalineCup, CustomLiquids.Catechol, 3000);
                LiquidTools.SetLiquidRaw(adrenalineCup, CustomLiquids.HydroxymethylBase, 3000);
                LiquidTools.SetLiquidRaw(adrenalineCup, "lipobond_agent", 3000);
                SetTemperature(adrenalineCup, 40.0);
                PulseStep(adrenalineCup, AdrenalineChemistry.StepAmination);
                int amination = LiquidTools.GetLiquidAmount(adrenalineCup, CustomLiquids.AminationIntermediate);
                int catecholLeft = LiquidTools.GetLiquidAmount(adrenalineCup, CustomLiquids.Catechol);
                int lipobondLeft = LiquidTools.GetLiquidAmount(adrenalineCup, "lipobond_agent");

                LiquidTools.SetLiquidRaw(adrenalineCup, "reducing_agent", 3000);
                SetTemperature(adrenalineCup, 70.0);
                PulseStep(adrenalineCup, AdrenalineChemistry.StepReduction);
                int racemic = LiquidTools.GetLiquidAmount(adrenalineCup, CustomLiquids.RacemicAdrenaline);
                int reducingLeft = LiquidTools.GetLiquidAmount(adrenalineCup, "reducing_agent");

                LiquidTools.SetLiquidRaw(adrenalineCup, CustomLiquids.ResolvingAgent, 3000);
                LiquidTools.SetLiquidRaw(adrenalineCup, "acid", 1500);
                SetTemperature(adrenalineCup, 50.0);
                PulseStep(adrenalineCup, AdrenalineChemistry.StepResolution);
                int levoSalt = LiquidTools.GetLiquidAmount(adrenalineCup, CustomLiquids.LevoAdrenalineSalt);
                int dextro = LiquidTools.GetLiquidAmount(adrenalineCup, CustomLiquids.DextroWaste);
                int acidLeftAfterResolution = LiquidTools.GetLiquidAmount(adrenalineCup, "acid");

                SetTemperature(adrenalineCup, 40.0);
                LiquidTools.SetLiquidRaw(adrenalineCup, "base", levoSalt);
                PulseStep(adrenalineCup, AdrenalineChemistry.StepFreebase);
                int freebase = LiquidTools.GetLiquidAmount(adrenalineCup, CustomLiquids.AdrenalineFreebase);
                int baseLeftAfterFreebase = LiquidTools.GetLiquidAmount(adrenalineCup, "base");

                LiquidTools.SetLiquidRaw(adrenalineCup, "acid", freebase);
                PulseStep(adrenalineCup, AdrenalineChemistry.StepHcl);
                int hcl = LiquidTools.GetLiquidAmount(adrenalineCup, CustomLiquids.AdrenalineHcl);
                int acidLeftAfterSalt = LiquidTools.GetLiquidAmount(adrenalineCup, "acid");

                GameSlotInventory wasteSlot = MachineHelper.GetSlot(machine, 3) as GameSlotInventory;
                int wasteCollected = ItemFactory.CollectDextroWaste(adrenalineCup, wasteSlot);
                GameItem wasteItem = wasteSlot != null ? wasteSlot.childItem : null;
                int dextroAfterCollect = LiquidTools.GetLiquidAmount(adrenalineCup, CustomLiquids.DextroWaste);

                LiquidTools.SetLiquidRaw(adrenalineCup, "water", hcl);
                PulseStep(adrenalineCup, AdrenalineChemistry.StepSolution);
                int solution = LiquidTools.GetLiquidAmount(adrenalineCup, CustomLiquids.AdrenalineSolution);
                int hclLeft = LiquidTools.GetLiquidAmount(adrenalineCup, CustomLiquids.AdrenalineHcl);
                int waterLeft = LiquidTools.GetLiquidAmount(adrenalineCup, "water");

                // --- 8. Plan B: recycle dextro waste back into racemic adrenaline ---------
                recycleCup = ItemFactory.CreateExtractor();
                GeneralHelper.SetItemOwned(recycleCup, true);
                LiquidTools.SetLiquidRaw(recycleCup, CustomLiquids.DextroWaste, 1000);
                LiquidTools.SetLiquidRaw(recycleCup, "reducing_agent", 1000);
                SetTemperature(recycleCup, 70.0);
                PulseStep(recycleCup, AdrenalineChemistry.StepDextroRecycle);
                int recycledRacemic = LiquidTools.GetLiquidAmount(recycleCup, CustomLiquids.RacemicAdrenaline);
                int recycleDextroLeft = LiquidTools.GetLiquidAmount(recycleCup, CustomLiquids.DextroWaste);
                int recycleReducingLeft = LiquidTools.GetLiquidAmount(recycleCup, "reducing_agent");
                bool recycleOk = recycledRacemic == 1000
                    && recycleDextroLeft == 0
                    && recycleReducingLeft == 0;
                log.Log(Prefix + "adrenaline recycle: racemic=" + recycledRacemic + "µL (expect 1000)"
                    + " dextroLeft=" + recycleDextroLeft + "µL reducingLeft=" + recycleReducingLeft + "µL");
                AdrenalineContent.EnsureRecipes();
                bool recipesOk = ChemFinisherRecipe.GetRecipeById(ItemIds.Api) != null
                    && ChemFinisherRecipe.GetRecipeById(ItemIds.Pen) != null
                    && ChemFinisherRecipe.GetRecipeById(ItemIds.Api).requiredVolume == AdrenalineContent.ApiRequiredVolume
                    && ChemFinisherRecipe.GetRecipeById(ItemIds.Pen).requiredVolume == AdrenalineContent.PenRequiredVolume;
                // --- 8c. Naloxone chain (thebaine -> reduction -> hydrolysis -> crude -> HCl -> solution)
                naloxoneCup = ItemFactory.CreateExtractor();
                GeneralHelper.SetItemOwned(naloxoneCup, true);
                LiquidTools.SetLiquidRaw(naloxoneCup, CustomLiquids.Thebaine, 2000);
                LiquidTools.SetLiquidRaw(naloxoneCup, "acid", 2000);
                LiquidTools.SetLiquidRaw(naloxoneCup, "reducing_agent", 2000);
                LiquidTools.SetLiquidRaw(naloxoneCup, "lipobond_agent", 2000);
                SetTemperature(naloxoneCup, 50.0);
                PulseStep(naloxoneCup, NaloxoneChemistry.StepReduction);
                int nalReduction = LiquidTools.GetLiquidAmount(naloxoneCup, CustomLiquids.NaloxoneReductionIntermediate);
                int nalThebaineLeft = LiquidTools.GetLiquidAmount(naloxoneCup, CustomLiquids.Thebaine);
                int nalLipobondLeft = LiquidTools.GetLiquidAmount(naloxoneCup, "lipobond_agent");
                LiquidTools.SetLiquidRaw(naloxoneCup, "acid", 2000);
                LiquidTools.SetLiquidRaw(naloxoneCup, "water", 2000);
                SetTemperature(naloxoneCup, 90.0);
                PulseStep(naloxoneCup, NaloxoneChemistry.StepHydrolysis);
                int nalHydrolysis = LiquidTools.GetLiquidAmount(naloxoneCup, CustomLiquids.NaloxoneHydrolysisIntermediate);
                LiquidTools.SetLiquidRaw(naloxoneCup, CustomLiquids.HydroxymethylBase, 2000);
                LiquidTools.SetLiquidRaw(naloxoneCup, "lipobond_agent", 2000);
                SetTemperature(naloxoneCup, 65.0);
                PulseStep(naloxoneCup, NaloxoneChemistry.StepAllylation);
                int nalCrude = LiquidTools.GetLiquidAmount(naloxoneCup, CustomLiquids.NaloxoneCrude);
                LiquidTools.SetLiquidRaw(naloxoneCup, "acid", 2000);
                LiquidTools.SetLiquidRaw(naloxoneCup, "water", 2000);
                SetTemperature(naloxoneCup, 40.0);
                PulseStep(naloxoneCup, NaloxoneChemistry.StepSalt);
                int nalHcl = LiquidTools.GetLiquidAmount(naloxoneCup, CustomLiquids.NaloxoneHcl);
                LiquidTools.SetLiquidRaw(naloxoneCup, "water", 2000);
                PulseStep(naloxoneCup, NaloxoneChemistry.StepSolution);
                int nalSolution = LiquidTools.GetLiquidAmount(naloxoneCup, CustomLiquids.NaloxoneSolution);

                // Alcohol is a recoverable solvent: it must not be consumed in N1/N3.
                naloxoneAlcoholCup = ItemFactory.CreateExtractor();
                GeneralHelper.SetItemOwned(naloxoneAlcoholCup, true);
                LiquidTools.SetLiquidRaw(naloxoneAlcoholCup, CustomLiquids.NaloxoneHydrolysisIntermediate, 1000);
                LiquidTools.SetLiquidRaw(naloxoneAlcoholCup, CustomLiquids.HydroxymethylBase, 1000);
                LiquidTools.SetLiquidRaw(naloxoneAlcoholCup, "alcohol", 5000);
                SetTemperature(naloxoneAlcoholCup, 65.0);
                PulseStep(naloxoneAlcoholCup, NaloxoneChemistry.StepAllylation);
                int nalAlcoholCrude = LiquidTools.GetLiquidAmount(naloxoneAlcoholCup, CustomLiquids.NaloxoneCrude);
                int nalAlcoholLeft = LiquidTools.GetLiquidAmount(naloxoneAlcoholCup, "alcohol");

                NaloxoneContent.EnsureRecipes();
                bool naloxoneRecipesOk = ChemFinisherRecipe.GetRecipeById(ItemIds.NaloxoneApi) != null
                    && ChemFinisherRecipe.GetRecipeById(NaloxoneContent.VanillaInjectorId) != null
                    && ChemFinisherRecipe.GetRecipeById(ItemIds.NaloxoneApi).requiredVolume == NaloxoneContent.ApiRequiredVolume
                    && ChemFinisherRecipe.GetRecipeById(NaloxoneContent.VanillaInjectorId).requiredVolume == NaloxoneContent.InjectorRequiredVolume;
                naloxoneApiItem = NaloxoneContent.CreateApi();
                naloxoneBoxItem = NaloxoneContent.CreateSupplyBox();
                bool naloxoneOk = nalReduction == 2000
                    && nalThebaineLeft == 0
                    && nalLipobondLeft == 0
                    && nalHydrolysis == 2000
                    && nalCrude == 2000
                    && nalHcl == 2000
                    && nalSolution == 2000
                    && nalAlcoholCrude == 1000
                    && nalAlcoholLeft == 5000
                    && naloxoneRecipesOk
                    && naloxoneApiItem != null
                    && naloxoneApiItem.IsTag("CHEM_ITEM_TAG")
                    && naloxoneApiItem.unitValue == 50
                    && naloxoneBoxItem != null
                    && naloxoneBoxItem.onActivateSlotItemFunc != null;
                log.Log(Prefix + "naloxone chain: reduction=" + nalReduction + "µL hydrolysis=" + nalHydrolysis
                    + "µL crude=" + nalCrude + "µL hcl=" + nalHcl + "µL solution=" + nalSolution
                    + "µL alcoholCrude=" + nalAlcoholCrude + "µL alcoholLeft=" + nalAlcoholLeft
                    + "µL recipes=" + naloxoneRecipesOk + " api=" + (naloxoneApiItem != null)
                    + " box=" + (naloxoneBoxItem != null));
                // --- 8d. Contraceptive + Sannai pill chains ---------------------------------
                pillCup = ItemFactory.CreateExtractor();
                GeneralHelper.SetItemOwned(pillCup, true);
                LiquidTools.SetLiquidRaw(pillCup, CustomLiquids.SteroidRaw, 1000);
                LiquidTools.SetLiquidRaw(pillCup, "acid", 1000);
                LiquidTools.SetLiquidRaw(pillCup, "water", 1000);
                LiquidTools.SetLiquidRaw(pillCup, "alcohol", 1000);
                SetTemperature(pillCup, 70.0);
                PulseStep(pillCup, PillChemistry.StepHydrolysis);
                int birthHydro = LiquidTools.GetLiquidAmount(pillCup, CustomLiquids.SteroidHydrolysate);
                LiquidTools.SetLiquidRaw(pillCup, "reducing_agent", 1000);
                LiquidTools.SetLiquidRaw(pillCup, "lipobond_agent", 1000);
                SetTemperature(pillCup, 60.0);
                PulseStep(pillCup, PillChemistry.StepReduction);
                int birthIntermediate = LiquidTools.GetLiquidAmount(pillCup, CustomLiquids.SteroidIntermediate);
                SetTemperature(pillCup, 40.0);
                PulseStep(pillCup, PillChemistry.StepContraceptiveApi);
                int birthApi = LiquidTools.GetLiquidAmount(pillCup, "contraceptive");

                sannaiCup = ItemFactory.CreateExtractor();
                GeneralHelper.SetItemOwned(sannaiCup, true);
                LiquidTools.SetLiquidRaw(sannaiCup, CustomLiquids.SannaiRaw, 1000);
                LiquidTools.SetLiquidRaw(sannaiCup, "alcohol", 5000);
                SetTemperature(sannaiCup, 60.0);
                PulseStep(sannaiCup, PillChemistry.StepExtraction);
                int sannaiExtract = LiquidTools.GetLiquidAmount(sannaiCup, CustomLiquids.SannaiExtract);
                int sannaiAlcoholLeft = LiquidTools.GetLiquidAmount(sannaiCup, "alcohol");
                LiquidTools.SetLiquidRaw(sannaiCup, "acid", 1000);
                SetTemperature(sannaiCup, 40.0);
                PulseStep(sannaiCup, PillChemistry.StepSannaiApi);
                int sannaiApi = LiquidTools.GetLiquidAmount(sannaiCup, "zyanide");
                int sannaiAcidLeft = LiquidTools.GetLiquidAmount(sannaiCup, "acid");

                PillContent.EnsureRecipes();
                bool pillRecipesOk = ChemFinisherRecipe.GetRecipeById(PillContent.BirthControlPillId) != null
                    && ChemFinisherRecipe.GetRecipeById(PillContent.SannaiPillId) != null
                    && ChemFinisherRecipe.GetRecipeById(PillContent.BirthControlPillId).activeIngredientId == "contraceptive"
                    && ChemFinisherRecipe.GetRecipeById(PillContent.SannaiPillId).activeIngredientId == "zyanide"
                    && ChemFinisherRecipe.GetRecipeById(PillContent.BirthControlPillId).requiredVolume == PillContent.PressVolume
                    && ChemFinisherRecipe.GetRecipeById(PillContent.SannaiPillId).requiredVolume == PillContent.PressVolume;
                birthControlBoxItem = PillContent.CreateBirthControlBox();
                sannaiBoxItem = PillContent.CreateSannaiBox();
                bool pillOk = birthHydro == 1000
                    && birthIntermediate == 1000
                    && birthApi == 1000
                    && sannaiExtract == 1000
                    && sannaiAlcoholLeft == 5000
                    && sannaiApi == 1000
                    && sannaiAcidLeft == 1000
                    && pillRecipesOk
                    && birthControlBoxItem != null
                    && birthControlBoxItem.onActivateSlotItemFunc != null
                    && sannaiBoxItem != null
                    && sannaiBoxItem.onActivateSlotItemFunc != null;
                log.Log(Prefix + "pill chains: birthHydro=" + birthHydro + "µL birthIntermediate=" + birthIntermediate
                    + "µL birthApi=" + birthApi + "µL sannaiExtract=" + sannaiExtract
                    + "µL sannaiAlcoholLeft=" + sannaiAlcoholLeft + "µL sannaiApi=" + sannaiApi
                    + "µL sannaiAcidLeft=" + sannaiAcidLeft + "µL recipes=" + pillRecipesOk
                    + " boxes=" + (birthControlBoxItem != null && sannaiBoxItem != null));
                // --- 8e. Synaprest chain (ligand -> activated -> crude -> API) -----------------
                synaprestCup = ItemFactory.CreateExtractor();
                GeneralHelper.SetItemOwned(synaprestCup, true);
                LiquidTools.SetLiquidRaw(synaprestCup, CustomLiquids.NeuralLigand, 1000);
                LiquidTools.SetLiquidRaw(synaprestCup, "acid", 1000);
                LiquidTools.SetLiquidRaw(synaprestCup, "water", 1000);
                SetTemperature(synaprestCup, 70.0);
                PulseStep(synaprestCup, SynaprestChemistry.StepActivation);
                int synActivated = LiquidTools.GetLiquidAmount(synaprestCup, CustomLiquids.ActivatedLigand);
                LiquidTools.SetLiquidRaw(synaprestCup, CustomLiquids.SteroidRaw, 1000);
                LiquidTools.SetLiquidRaw(synaprestCup, "reducing_agent", 1000);
                SetTemperature(synaprestCup, 60.0);
                PulseStep(synaprestCup, SynaprestChemistry.StepSynthesis);
                int synCrude = LiquidTools.GetLiquidAmount(synaprestCup, CustomLiquids.SynaprestCrude);
                LiquidTools.SetLiquidRaw(synaprestCup, "alcohol", 5000);
                SetTemperature(synaprestCup, 40.0);
                PulseStep(synaprestCup, SynaprestChemistry.StepRefinement);
                int synApi = LiquidTools.GetLiquidAmount(synaprestCup, CustomLiquids.SynaprestApi);
                int synAlcoholLeft = LiquidTools.GetLiquidAmount(synaprestCup, "alcohol");
                SynaprestContent.EnsureRecipes();
                bool synRecipesOk = ChemFinisherRecipe.GetRecipeById(SynaprestContent.VanillaInjectorId) != null
                    && ChemFinisherRecipe.GetRecipeById(SynaprestContent.VanillaInjectorId).activeIngredientId == CustomLiquids.SynaprestApi
                    && ChemFinisherRecipe.GetRecipeById(SynaprestContent.VanillaInjectorId).requiredVolume == SynaprestContent.PressVolume;
                synaprestBoxItem = SynaprestContent.CreateSupplyBox();
                synaprestInjectorItem = DirectoryMaster.Item(SynaprestContent.VanillaInjectorId);
                SynaprestEffects.RegisterPerks();
                // These are gameplay status effects, not selectable starting traits: they must
                // NOT be in the starting-perk registry, but their local templates must exist.
                bool synPerksOk = StartingPerkList.GetModPerk(SynaprestEffects.AnhedoniaPerk) == null
                    && StartingPerkList.GetModPerk(SynaprestEffects.DependencyPerk) == null
                    && StartingPerkList.GetModPerk(SynaprestEffects.UseCountPerk) == null
                    && SynaprestEffects.PerkTemplatesReady();
                bool speedOk = ReactionMath.Step(20000) >= 4000;
                bool synaprestOk = synActivated == 1000
                    && synCrude == 1000
                    && synApi == 1000
                    && synAlcoholLeft == 5000
                    && synRecipesOk
                    && synPerksOk
                    && synaprestBoxItem != null
                    && synaprestBoxItem.unitValue == 500
                    && synaprestInjectorItem != null
                    && synaprestInjectorItem.identifier == SynaprestContent.VanillaInjectorId
                    && synaprestInjectorItem.unitValue == 350
                    && !synaprestInjectorItem.IsTag("MOD_ID_TAG")
                    && speedOk;
                log.Log(Prefix + "synaprest chain: activated=" + synActivated + "µL crude=" + synCrude
                    + "µL api=" + synApi + "µL alcoholLeft=" + synAlcoholLeft
                    + "µL recipes=" + synRecipesOk + " perks=" + synPerksOk
                    + " box=" + (synaprestBoxItem != null) + " injector=" + (synaprestInjectorItem != null)
                    + " speedOk=" + speedOk);
                // --- 8f. Tourniquet price/gate + Apothecary unlock/phone/client -------------------
                // IMPORTANT: the selftest runs on every boot against the real player store. Snapshot
                // everything it touches and restore it afterwards, otherwise a pending apothecary
                // appointment gets wiped (and a fake arrival night-log line is left behind).
                StorePhoneClient existingContact = null;
                bool contactExistedBefore = false;
                int savedStockType = 0;
                int savedCooldown = 0;
                int savedAmountSpent = 0;
                bool savedDialedBefore = false;
                StorePhoneClient.PhoneState savedPhoneState = StorePhoneClient.PhoneState.Regular;
                List<string> savedNightLogs = null;
                if (PlayerStore.Instance != null && PlayerStore.Instance.PhoneClientDict != null)
                {
                    contactExistedBefore = PlayerStore.Instance.PhoneClientDict.TryGetValue(Apothecary.PhoneNumber, out existingContact)
                        && existingContact != null;
                    if (contactExistedBefore)
                    {
                        savedStockType = existingContact.stockType;
                        savedPhoneState = existingContact.phoneState;
                        savedCooldown = existingContact.currentCooldown;
                        savedAmountSpent = existingContact.amountSpent;
                        savedDialedBefore = existingContact.dialedBefore;
                    }
                }
                if (PlayerStore.Instance != null && PlayerStore.Instance.nightLogs != null)
                {
                    savedNightLogs = new List<string>(PlayerStore.Instance.nightLogs);
                }
                Apothecary.EnsurePhoneContact();
                bool phoneContactOk = PlayerStore.Instance != null && PlayerStore.Instance.PhoneClientDict != null
                    && PlayerStore.Instance.PhoneClientDict.ContainsKey(Apothecary.PhoneNumber);
                bool apothecaryDefinitionsOk = NetworkUpgrade.GetDefinition(Apothecary.UpgradeId) != null
                    && StoreClientListDict.storeClientDict.ContainsKey(Apothecary.ClientId)
                    && PhoneClientList.DialogFuncs.ContainsKey(Apothecary.DialogFuncId);
                bool appointmentOk = false;
                try
                {
                    StoreClientManager manager = PlayerStore.Instance.storeClientManager;
                    if (phoneContactOk && manager != null)
                    {
                        StorePhoneClient contact = PlayerStore.Instance.PhoneClientDict[Apothecary.PhoneNumber];
                        contact.stockType = StoreStation.GetDayCounter();
                        contact.phoneState = StorePhoneClient.PhoneState.Regular;
                        int before = manager.clientStack.Count;
                        Apothecary.TryInjectAppointment(manager);
                        appointmentOk = manager.clientStack.Count == before + 1
                            && manager.clientStack.Count > 0
                            && manager.clientStack[0] != null
                            && manager.clientStack[0].identifier == Apothecary.ClientId;
                        manager.clientStack.RemoveAll(delegate (StoreClient c)
                        {
                            return c != null && c.identifier == Apothecary.ClientId;
                        });
                    }
                }
                catch (Exception e)
                {
                    log.Log(Prefix + "apothecary appointment test warning: " + e.Message);
                }
                finally
                {
                    // Restore the real phone contact / appointment and the real night log list.
                    if (contactExistedBefore && existingContact != null)
                    {
                        existingContact.stockType = savedStockType;
                        existingContact.phoneState = savedPhoneState;
                        existingContact.currentCooldown = savedCooldown;
                        existingContact.amountSpent = savedAmountSpent;
                        existingContact.dialedBefore = savedDialedBefore;
                    }
                    else if (PlayerStore.Instance != null && PlayerStore.Instance.PhoneClientDict != null)
                    {
                        PlayerStore.Instance.PhoneClientDict.Remove(Apothecary.PhoneNumber);
                    }
                    if (savedNightLogs != null && PlayerStore.Instance != null && PlayerStore.Instance.nightLogs != null)
                    {
                        PlayerStore.Instance.nightLogs.Clear();
                        PlayerStore.Instance.nightLogs.AddRange(savedNightLogs);
                    }
                }
                bool apothecaryOk = phoneContactOk
                    && apothecaryDefinitionsOk
                    && appointmentOk;
                log.Log(Prefix + "apothecary: phone=" + phoneContactOk + " defs=" + apothecaryDefinitionsOk
                    + " firstInQueue=" + appointmentOk);
                // --- 8g. Immunivax chain (serum + alcohol -> suspension -> crude -> API) ------------
                immunivaxCup = ItemFactory.CreateExtractor();
                GeneralHelper.SetItemOwned(immunivaxCup, true);
                LiquidTools.SetLiquidRaw(immunivaxCup, CustomLiquids.ImmuneSerum, 2000);
                LiquidTools.SetLiquidRaw(immunivaxCup, "alcohol", 2000);
                SetTemperature(immunivaxCup, 30.0);
                PulseStep(immunivaxCup, ImmunivaxChemistry.StepPrecipitation);
                int immSuspension = LiquidTools.GetLiquidAmount(immunivaxCup, CustomLiquids.ImmunoglobulinSuspension);
                int immSerumLeft = LiquidTools.GetLiquidAmount(immunivaxCup, CustomLiquids.ImmuneSerum);
                SetTemperature(immunivaxCup, 35.0);
                PulseStep(immunivaxCup, ImmunivaxChemistry.StepConcentration);
                int immCrude = LiquidTools.GetLiquidAmount(immunivaxCup, CustomLiquids.CrudeImmunoglobulin);
                int immRecoveredAlcohol = LiquidTools.GetLiquidAmount(immunivaxCup, "alcohol");
                LiquidTools.SetLiquidRaw(immunivaxCup, "water", 1000);
                PulseStep(immunivaxCup, ImmunivaxChemistry.StepDialysis);
                int immApi = LiquidTools.GetLiquidAmount(immunivaxCup, CustomLiquids.ImmunoglobulinApi);

                immunivaxWasteCup = ItemFactory.CreateExtractor();
                GeneralHelper.SetItemOwned(immunivaxWasteCup, true);
                LiquidTools.SetLiquidRaw(immunivaxWasteCup, CustomLiquids.ImmunoglobulinSuspension, 1000);
                SetTemperature(immunivaxWasteCup, 50.0);
                PulseStep(immunivaxWasteCup, ImmunivaxChemistry.StepConcentration);
                int immDenatured = LiquidTools.GetLiquidAmount(immunivaxWasteCup, CustomLiquids.DenaturedWaste);
                int immCrudeLeft = LiquidTools.GetLiquidAmount(immunivaxWasteCup, CustomLiquids.CrudeImmunoglobulin);

                ImmunivaxContent.EnsureRecipes();
                bool immRecipesOk = ChemFinisherRecipe.GetRecipeById(ImmunivaxContent.VanillaInjectorId) != null
                    && ChemFinisherRecipe.GetRecipeById(ImmunivaxContent.VanillaInjectorId).activeIngredientId == CustomLiquids.ImmunoglobulinApi
                    && ChemFinisherRecipe.GetRecipeById(ImmunivaxContent.VanillaInjectorId).requiredVolume == ImmunivaxContent.InjectorVolume;
                immuneSerumItem = ImmunivaxContent.CreateImmuneSerumBottle();
                bool immunivaxOk = immSuspension == 1000
                    && immSerumLeft == 0
                    && immCrude == 1000
                    && immRecoveredAlcohol == 1000
                    && immApi == 1000
                    && immDenatured == 1000
                    && immCrudeLeft == 0
                    && immRecipesOk
                    && immuneSerumItem != null
                    && immuneSerumItem.unitValue == 120
                    && LiquidTools.GetLiquidAmount(immuneSerumItem, CustomLiquids.ImmuneSerum) == 20000;
                log.Log(Prefix + "immunivax chain: suspension=" + immSuspension + "µL crude=" + immCrude
                    + "µL recoveredAlcohol=" + immRecoveredAlcohol + "µL api=" + immApi
                    + "µL denatured=" + immDenatured + "µL crudeLeft=" + immCrudeLeft
                    + "µL recipes=" + immRecipesOk + " serumBottle=" + (immuneSerumItem != null));
                // --- 8h. Blue Blood chain (bio-gel -> acidic gel -> crude -> medical) -------------
                blueGelCup = ItemFactory.CreateExtractor();
                GeneralHelper.SetItemOwned(blueGelCup, true);
                LiquidTools.SetLiquidRaw(blueGelCup, CustomLiquids.BioGelBase, 2000);
                LiquidTools.SetLiquidRaw(blueGelCup, "water", 1000);
                LiquidTools.SetLiquidRaw(blueGelCup, "acid", 5);
                SetTemperature(blueGelCup, 30.0);
                PulseStep(blueGelCup, BlueBloodChemistry.StepSolubilise);
                int acidicGel = LiquidTools.GetLiquidAmount(blueGelCup, CustomLiquids.AcidicGel);
                LiquidTools.SetLiquidRaw(blueGelCup, "reducing_agent", 1000);
                LiquidTools.SetLiquidRaw(blueGelCup, "lipobond_agent", 1000);
                PulseStep(blueGelCup, BlueBloodChemistry.StepCrosslink);
                int crudeBlueGel = LiquidTools.GetLiquidAmount(blueGelCup, CustomLiquids.CrudeBlueGel);
                int blueContaminant = LiquidTools.GetLiquidAmount(blueGelCup, "chemical_contaminant");
                // B3 must refuse impure crude gel (purity 1000/1050 = 95.2% < 99%).
                PulseStep(blueGelCup, BlueBloodChemistry.StepMedicalGrade);
                int medicalBeforePurify = LiquidTools.GetLiquidAmount(blueGelCup, CustomLiquids.MedicalBlueGel);
                // Simulate the purification machine having removed the contaminant.
                LiquidTools.SetLiquidRaw(blueGelCup, "chemical_contaminant", 0);
                PulseStep(blueGelCup, BlueBloodChemistry.StepMedicalGrade);
                int medicalBlueGel = LiquidTools.GetLiquidAmount(blueGelCup, CustomLiquids.MedicalBlueGel);

                blueGelWasteCup = ItemFactory.CreateExtractor();
                GeneralHelper.SetItemOwned(blueGelWasteCup, true);
                LiquidTools.SetLiquidRaw(blueGelWasteCup, CustomLiquids.AcidicGel, 1000);
                LiquidTools.SetLiquidRaw(blueGelWasteCup, "reducing_agent", 1000);
                LiquidTools.SetLiquidRaw(blueGelWasteCup, "lipobond_agent", 1000);
                SetTemperature(blueGelWasteCup, 50.0);
                PulseStep(blueGelWasteCup, BlueBloodChemistry.StepCrosslink);
                int blueDenatured = LiquidTools.GetLiquidAmount(blueGelWasteCup, CustomLiquids.DenaturedBlueGelWaste);
                int blueWasteCrude = LiquidTools.GetLiquidAmount(blueGelWasteCup, CustomLiquids.CrudeBlueGel);

                BlueBloodContent.EnsureRecipes();
                bool blueRecipesOk = ChemFinisherRecipe.GetRecipeById(BlueBloodContent.VanillaBagId) != null
                    && ChemFinisherRecipe.GetRecipeById(BlueBloodContent.VanillaBagId).activeIngredientId == CustomLiquids.MedicalBlueGel
                    && ChemFinisherRecipe.GetRecipeById(BlueBloodContent.VanillaBagId).requiredVolume == BlueBloodContent.BagVolume;
                bioGelBottleItem = BlueBloodContent.CreateBioGelBottle();
                bool blueBloodOk = acidicGel == 1000
                    && crudeBlueGel == 1000
                    && blueContaminant > 0
                    && medicalBeforePurify == 0
                    && medicalBlueGel == 1000
                    && blueDenatured == 1000
                    && blueWasteCrude == 0
                    && blueRecipesOk
                    && bioGelBottleItem != null
                    && bioGelBottleItem.unitValue == 120
                    && LiquidTools.GetLiquidAmount(bioGelBottleItem, CustomLiquids.BioGelBase) == 20000;
                log.Log(Prefix + "blue blood chain: acidic=" + acidicGel + "µL crude=" + crudeBlueGel
                    + "µL contaminant=" + blueContaminant + "µL medicalBeforePurify=" + medicalBeforePurify
                    + "µL medical=" + medicalBlueGel + "µL denatured=" + blueDenatured
                    + "µL wasteCrude=" + blueWasteCrude + "µL recipes=" + blueRecipesOk
                    + " bioGelBottle=" + (bioGelBottleItem != null));
                // --- 8i. Military ordnance: flash / smoke / plastic explosive fillings ------------
                flashCup = ItemFactory.CreateExtractor();
                GeneralHelper.SetItemOwned(flashCup, true);
                LiquidTools.SetLiquidRaw(flashCup, CustomLiquids.MetalPowder, 1000);
                LiquidTools.SetLiquidRaw(flashCup, CustomLiquids.Oxidizer, 1000);
                SetTemperature(flashCup, 30.0);
                PulseStep(flashCup, OrdnanceChemistry.StepFlashMix);
                int flashAmount = LiquidTools.GetLiquidAmount(flashCup, CustomLiquids.FlashPowder);

                smokeCup = ItemFactory.CreateExtractor();
                GeneralHelper.SetItemOwned(smokeCup, true);
                LiquidTools.SetLiquidRaw(smokeCup, "sugar", 1000);
                LiquidTools.SetLiquidRaw(smokeCup, CustomLiquids.Oxidizer, 1000);
                LiquidTools.SetLiquidRaw(smokeCup, "lipobond_agent", 1000);
                SetTemperature(smokeCup, 30.0);
                PulseStep(smokeCup, OrdnanceChemistry.StepSmokeMix);
                int smokeAmount = LiquidTools.GetLiquidAmount(smokeCup, CustomLiquids.SmokeComposition);

                chargeCup = ItemFactory.CreateExtractor();
                GeneralHelper.SetItemOwned(chargeCup, true);
                LiquidTools.SetLiquidRaw(chargeCup, CustomLiquids.NitratingAcid, 1000);
                LiquidTools.SetLiquidRaw(chargeCup, "alcohol", 1000);
                SetTemperature(chargeCup, 30.0);
                PulseStep(chargeCup, OrdnanceChemistry.StepNitration);
                LiquidTools.SetLiquidRaw(chargeCup, "stabilizing_agent", 1000);
                LiquidTools.SetLiquidRaw(chargeCup, "lipobond_agent", 1000);
                PulseStep(chargeCup, OrdnanceChemistry.StepPutty);
                int puttyAmount = LiquidTools.GetLiquidAmount(chargeCup, CustomLiquids.PlasticPutty);

                chargeHotCup = ItemFactory.CreateExtractor();
                GeneralHelper.SetItemOwned(chargeHotCup, true);
                LiquidTools.SetLiquidRaw(chargeHotCup, CustomLiquids.NitratingAcid, 1000);
                LiquidTools.SetLiquidRaw(chargeHotCup, "alcohol", 1000);
                SetTemperature(chargeHotCup, 50.0);
                PulseStep(chargeHotCup, OrdnanceChemistry.StepNitration);
                int burntAmount = LiquidTools.GetLiquidAmount(chargeHotCup, CustomLiquids.OrdnanceWaste);
                int hotNitro = LiquidTools.GetLiquidAmount(chargeHotCup, CustomLiquids.NitroBase);

                OrdnanceContent.EnsureRecipes();
                bool ordRecipesOk = ChemFinisherRecipe.GetRecipeById(OrdnanceContent.VanillaFlashbangId) != null
                    && ChemFinisherRecipe.GetRecipeById(OrdnanceContent.VanillaFlashbangId).activeIngredientId == CustomLiquids.FlashPowder
                    && ChemFinisherRecipe.GetRecipeById(OrdnanceContent.VanillaFlashbangId).requiredVolume == OrdnanceContent.PressVolume
                    && ChemFinisherRecipe.GetRecipeById(OrdnanceContent.VanillaSmokeId) != null
                    && ChemFinisherRecipe.GetRecipeById(OrdnanceContent.VanillaSmokeId).activeIngredientId == CustomLiquids.SmokeComposition
                    && ChemFinisherRecipe.GetRecipeById(OrdnanceContent.VanillaC4Id) != null
                    && ChemFinisherRecipe.GetRecipeById(OrdnanceContent.VanillaC4Id).activeIngredientId == CustomLiquids.PlasticPutty;
                bool ordnanceOk = flashAmount == 1000
                    && smokeAmount == 1000
                    && puttyAmount == 1000
                    && burntAmount == 1000
                    && hotNitro == 0
                    && ordRecipesOk;
                log.Log(Prefix + "ordnance chain: flash=" + flashAmount + "µL smoke=" + smokeAmount
                    + "µL putty=" + puttyAmount + "µL burnt=" + burntAmount + "µL hotNitro=" + hotNitro
                    + "µL directRecipes=" + ordRecipesOk);
                // --- 8j. Sealed random medicine crate -------------------------------------------
                mysteryCrateItem = MysteryMedBox.CreateStockItem();
                int mysteryCount = mysteryCrateItem.contentWindow != null
                    ? GraphUtils.FindChildrenType<GameItem>(mysteryCrateItem.contentWindow).Count
                    : 0;
                bool mysteryContentsOk = true;
                if (mysteryCrateItem.contentWindow != null)
                {
                    List<GameItem> mysteryContents = GraphUtils.FindChildrenType<GameItem>(mysteryCrateItem.contentWindow);
                    for (int mi = 0; mi < mysteryContents.Count; mi++)
                    {
                        GameItem content = mysteryContents[mi];
                        if (content == null || !content.IsTag(MysteryMedBox.ReagentBottleTag))
                        {
                            mysteryContentsOk = false;
                            break;
                        }
                    }
                }
                bool mysteryOk = mysteryCrateItem != null
                    && mysteryCrateItem.shape != null
                    && mysteryCrateItem.shape.width == 3
                    && mysteryCrateItem.shape.height == 4
                    && mysteryCrateItem.unitValue == 1500
                    && mysteryCrateItem.contentWindow != null
                    && mysteryCount >= MysteryMedBox.MinItems
                    && mysteryCount <= MysteryMedBox.MaxItems
                    && mysteryContentsOk
                    && !mysteryCrateItem.IsTag("CONTAINER_TOOLTIP_TAG");

                // --- 8k. Perfume lines (neuroactive + pheromone) ------------------------------
                neuroCup = ItemFactory.CreateExtractor();
                GeneralHelper.SetItemOwned(neuroCup, true);
                LiquidTools.SetLiquidRaw(neuroCup, CustomLiquids.AromaticExtract, 1000);
                LiquidTools.SetLiquidRaw(neuroCup, "water", 1000);
                LiquidTools.SetLiquidRaw(neuroCup, "acid", 5);
                SetTemperature(neuroCup, 40.0);
                PulseStep(neuroCup, PerfumeChemistry.StepNeuroActivation);
                int activated = LiquidTools.GetLiquidAmount(neuroCup, CustomLiquids.ActivatedFragrance);
                int neuroContaminant = LiquidTools.GetLiquidAmount(neuroCup, "chemical_contaminant");
                LiquidTools.SetLiquidRaw(neuroCup, "chemical_contaminant", 0);
                PulseStep(neuroCup, PerfumeChemistry.StepNeuropePurify);
                int neuroConcentrate = LiquidTools.GetLiquidAmount(neuroCup, CustomLiquids.NeuroConcentrate);

                pheromoneCup = ItemFactory.CreateExtractor();
                GeneralHelper.SetItemOwned(pheromoneCup, true);
                LiquidTools.SetLiquidRaw(pheromoneCup, CustomLiquids.CrudePheromone, 1000);
                LiquidTools.SetLiquidRaw(pheromoneCup, "alcohol", 1000);
                SetTemperature(pheromoneCup, 28.0);
                PulseStep(pheromoneCup, PerfumeChemistry.StepPheromoneColdPurify);
                int purifiedPheromone = LiquidTools.GetLiquidAmount(pheromoneCup, CustomLiquids.PurifiedPheromone);
                int pheromoneWaste = LiquidTools.GetLiquidAmount(pheromoneCup, CustomLiquids.PheromoneByproduct);
                LiquidTools.SetLiquidRaw(pheromoneCup, CustomLiquids.HydroxymethylBase, 1000);
                PulseStep(pheromoneCup, PerfumeChemistry.StepPheromoneModify);
                int pheromoneConcentrate = LiquidTools.GetLiquidAmount(pheromoneCup, CustomLiquids.PheromoneConcentrate);

                pheromoneHotCup = ItemFactory.CreateExtractor();
                GeneralHelper.SetItemOwned(pheromoneHotCup, true);
                LiquidTools.SetLiquidRaw(pheromoneHotCup, CustomLiquids.CrudePheromone, 1000);
                LiquidTools.SetLiquidRaw(pheromoneHotCup, "alcohol", 1000);
                SetTemperature(pheromoneHotCup, 35.0);
                PulseStep(pheromoneHotCup, PerfumeChemistry.StepPheromoneColdPurify);
                int hotPheromone = LiquidTools.GetLiquidAmount(pheromoneHotCup, CustomLiquids.PurifiedPheromone);
                int hotPheromoneWaste = LiquidTools.GetLiquidAmount(pheromoneHotCup, CustomLiquids.PheromoneWaste);

                PerfumeContent.EnsureRecipes();
                bool perfumeRecipesOk = ChemFinisherRecipe.GetRecipeById(PerfumeContent.NeuroPerfumeId) != null
                    && ChemFinisherRecipe.GetRecipeById(PerfumeContent.NeuroPerfumeId).activeIngredientId == CustomLiquids.NeuroConcentrate
                    && ChemFinisherRecipe.GetRecipeById(PerfumeContent.PheromonePerfumeId) != null
                    && ChemFinisherRecipe.GetRecipeById(PerfumeContent.PheromonePerfumeId).activeIngredientId == CustomLiquids.PheromoneConcentrate;
                perfumeKitItem = PerfumeContent.CreateSupplyBox();
                bool perfumeOk = activated == 1000
                    && neuroContaminant > 0
                    && neuroConcentrate == 1000
                    && purifiedPheromone == 1000
                    && pheromoneWaste > 0
                    && pheromoneConcentrate == 1000
                    && hotPheromone == 0
                    && hotPheromoneWaste == 1000
                    && perfumeRecipesOk
                    && perfumeKitItem != null
                    && perfumeKitItem.unitValue == 2000;
                log.Log(Prefix + "perfume chains: activated=" + activated + "µL contaminant=" + neuroContaminant
                    + "µL neuroConcentrate=" + neuroConcentrate + "µL purifiedPheromone=" + purifiedPheromone
                    + "µL pheromoneConcentrate=" + pheromoneConcentrate + "µL hotPheromone=" + hotPheromone
                    + "µL hotWaste=" + hotPheromoneWaste + "µL recipes=" + perfumeRecipesOk
                    + " kit=" + (perfumeKitItem != null));
                log.Log(Prefix + "mystery med crate: shape="
                    + (mysteryCrateItem != null && mysteryCrateItem.shape != null ? mysteryCrateItem.shape.width + "x" + mysteryCrateItem.shape.height : "-")
                    + " contents=" + mysteryCount + " reagents=" + mysteryContentsOk + " value="
                    + (mysteryCrateItem != null ? mysteryCrateItem.unitValue : -1));

                apiItem = AdrenalineContent.CreateApi();
                penItem = AdrenalineContent.CreatePen();
                boxItem = AdrenalineContent.CreateSupplyBox();
                resolvingBottleItem = AdrenalineContent.CreateResolvingAgentBottle();
                resolvingRefillItem = AdrenalineContent.CreateResolvingAgentRefillPack();
                recipeBookItem = RecipeBook.Spawn();
                noteItem = AdrenalineContent.CreateRecipeNote();
                tourniquetItem = ItemSpawner.Spawn(ItemIds.Tourniquet);
                fabricSample = ItemSpawner.Spawn(TourniquetCraft.FabricId);
                bool resolvingBottleOk = resolvingBottleItem != null
                    && LiquidTools.GetLiquidAmount(resolvingBottleItem, CustomLiquids.ResolvingAgent) == 20000
                    && WaterHelper.GetTotalCapacity(resolvingBottleItem) == 20000
                    && resolvingBottleItem.unitValue > 0;
                bool recipeBookOk = recipeBookItem != null
                    && recipeBookItem.IsTag("DOCUMENT")
                    && recipeBookItem.canActivateSlotItemFunc != null;
                bool resolvingRefillOk = resolvingRefillItem != null
                    && resolvingRefillItem.unitValue >= 60
                    && resolvingRefillItem.canActivateSlotItemFunc != null
                    && resolvingRefillItem.mayActivateSlotItemFunc != null;
                bool itemsOk = apiItem != null
                    && penItem != null
                    && penItem.IsTag("ITEM_MEDICAL_TAG")
                    && penItem.IsGameItemType("MEDICAL")
                    && penItem.IsGameItemType("LUXURY_ITEM")
                    && penItem.IsTag("LUXURY_ITEM")
                    && penItem.unitValue == 120
                    && boxItem != null
                    && boxItem.unitValue == 150
                    && penItem.effects.Count >= 2
                    && penItem.effects[0].abilityEffectType == CombatAbilityEffect.EffectType.buff
                    && penItem.effects[0].affectedStat == CombatEntity.EffectType.accuracyPercentage
                    && penItem.effects[1].affectedStat == CombatEntity.EffectType.criticalChanceDealing
                    && boxItem != null
                    && boxItem.canActivateSlotItemFunc != null
                    && resolvingBottleOk
                    && resolvingRefillOk
                    && recipeBookOk
                    && noteItem != null
                    && noteItem.IsTag("HANDNOTE_BODY");

                log.Log(Prefix + "adrenaline chain: amination=" + amination + "µL catecholLeft=" + catecholLeft
                    + "µL lipobondLeft=" + lipobondLeft + "µL | racemic=" + racemic
                    + "µL reducingLeft=" + reducingLeft + "µL | levoSalt=" + levoSalt + "µL dextro=" + dextro
                    + "µL acidLeft=" + acidLeftAfterResolution + "µL | freebase=" + freebase + "µL hcl=" + hcl
                    + "µL baseLeft2=" + baseLeftAfterFreebase + "µL acidLeft2=" + acidLeftAfterSalt
                    + "µL | wasteCollected=" + wasteCollected + "µL dextroLeft=" + dextroAfterCollect
                    + "µL wasteItem='" + (wasteItem != null ? wasteItem.name : "-")
                    + "' | solution=" + solution + "µL hclLeft=" + hclLeft + "µL waterLeft=" + waterLeft + "µL"
                    + " | recipes=" + recipesOk + " items=" + itemsOk
                    + " resolvingBottle=" + resolvingBottleOk
                    + " resolvingRefill=" + resolvingRefillOk
                    + " recipeBook=" + recipeBookOk);

                bool tourniquetOk = tourniquetItem != null
                    && tourniquetItem.IsGameItemType("MEDICAL")
                    && tourniquetItem.IsTag("ITEM_MEDICAL_TAG")
                    && tourniquetItem.unitValue == 150
                    && tourniquetItem.mayActivateSlotItemFunc != null
                    && tourniquetItem.canActivateSlotItemFunc != null
                    && tourniquetItem.shape != null
                    && tourniquetItem.shape.width == 2
                    && tourniquetItem.shape.height == 2
                    && fabricSample != null;
                if (fabricSample != null)
                {
                    TourniquetCraft.Attach(fabricSample);
                    tourniquetOk = tourniquetOk && fabricSample.mayThisTargetItemFunc != null;
                }
                scrapForCondition = null;
                ingotForCondition = null;
                tqWorn = null;
                tqExcellent = null;
                tqAged = null;
                bool conditionOk = false;
                try
                {
                    scrapForCondition = ItemSpawner.Spawn(TourniquetCraft.ScrapId);
                    ingotForCondition = ItemSpawner.Spawn(TourniquetCraft.IngotId);
                    tqWorn = ItemSpawner.Spawn(ItemIds.Tourniquet);
                    TourniquetCraft.ApplyMaterialCondition(tqWorn, scrapForCondition);
                    conditionOk = GetTourniquetConditionMod(tqWorn) == -50;
                    if (ingotForCondition != null)
                    {
                        IngotPurityHelper.SetPurity(ingotForCondition, "INGOT_PURITY_HIGH");
                        tqExcellent = ItemSpawner.Spawn(ItemIds.Tourniquet);
                        TourniquetCraft.ApplyMaterialCondition(tqExcellent, ingotForCondition);
                        conditionOk = conditionOk && GetTourniquetConditionMod(tqExcellent) == 25;
                        IngotPurityHelper.SetPurity(ingotForCondition, "INGOT_PURITY_LOW");
                        tqAged = ItemSpawner.Spawn(ItemIds.Tourniquet);
                        TourniquetCraft.ApplyMaterialCondition(tqAged, ingotForCondition);
                        conditionOk = conditionOk && GetTourniquetConditionMod(tqAged) == -25;
                    }
                }
                catch (Exception e)
                {
                    log.Log(Prefix + "tourniquet condition test failed: " + e.Message);
                }
                tourniquetOk = tourniquetOk && conditionOk;
                log.Log(Prefix + "makeshift tourniquet condition: worn=" + (tqWorn != null ? GetTourniquetConditionMod(tqWorn) : -999)
                    + " excellent=" + (tqExcellent != null ? GetTourniquetConditionMod(tqExcellent) : -999)
                    + " aged=" + (tqAged != null ? GetTourniquetConditionMod(tqAged) : -999));
                log.Log(Prefix + "makeshift tourniquet: item=" + (tourniquetItem != null)
                    + " medical=" + (tourniquetItem != null && tourniquetItem.IsGameItemType("MEDICAL"))
                    + " shape=" + (tourniquetItem != null && tourniquetItem.shape != null ? tourniquetItem.shape.width + "x" + tourniquetItem.shape.height : "-")
                    + " sprite=" + (tourniquetItem != null && tourniquetItem.useSpriteFromMod)
                    + " attach=" + (fabricSample != null && fabricSample.mayThisTargetItemFunc != null));

                bool exact = moved == 1234 && targetTotal == 1234 && sourceTotal == totalBefore - moved;
                bool refuseOverdose = overRequest == 0 && afterOverRequest == targetTotal;
                bool hasWindow = extractor.contentWindow != null;
                bool hydrolysis = hydrolyzed > 0
                    && starchAfter == 1000 - hydrolyzed
                    && sugarAfter == hydrolyzed
                    && acidAfter == 200
                    && hydroTotalAfter == hydroTotalBefore
                    && coldTick == 0;
                bool digestion = digested == 2000
                    && sugarLeft == 3000
                    && wasteAfter == 400
                    && sugarTotalAfter == sugarTotalBefore - 1600
                    && dormantDigest == 0
                    && sugarAfterDormant == 3000;
                bool cultivatorOk = cultivator.contentWindow != null
                    && cultivatorManualOk
                    && cultivatorActivity == 100
                    && fruitAccepted
                    && pressed == 60
                    && activityAfterFeed == 60
                    && fruitConsumed
                    && cultureDisplayOk
                    && LiquidTools.GetCultivatorThroughput(0, 100) == LiquidTools.CultivatorBaseThroughput;
                bool adrenalineOk = amination == 3000
                    && catecholLeft == 0
                    && lipobondLeft == 0
                    && racemic == 3000
                    && reducingLeft == 0
                    && levoSalt + dextro >= 2999
                    && levoSalt > 0
                    && dextro > 0
                    && Math.Abs(levoSalt - dextro) <= 1
                    && acidLeftAfterResolution == 1500
                    && freebase == levoSalt
                    && baseLeftAfterFreebase == 0
                    && hcl == freebase
                    && acidLeftAfterSalt == 0
                    && wasteCollected == dextro
                    && dextroAfterCollect == 0
                    && wasteItem != null
                    && wasteItem.identifier == ItemIds.DextroWaste
                    && solution == hcl
                    && hclLeft == 0
                    && waterLeft == 0;
                bool adrenalineItemsOk = recipesOk && itemsOk && tourniquetOk;

                // Verify the pipette dial: moving the in-window slider must write the per-item
                // dose tag through the real UI callback.
                bool sliderWorks = false;
                try
                {
                    if (extractorSlider != null)
                    {
                        extractorSlider.SetValue(4321f, true);
                        sliderWorks = LiquidTools.GetDose(extractor) == 4321;
                        extractorSlider.SetValue(1000f, true);
                    }
                }
                catch (Exception e)
                {
                    log.Warning("SELFTEST slider check failed: " + e.Message);
                }
                log.Log(Prefix + "metering dial: window=" + hasWindow + " slider=" + (extractorSlider != null)
                    + " sliderWritesDose=" + sliderWorks + " doseNow=" + LiquidTools.GetDose(extractor) + "µL");

                bool clean = cleanMode == LiquidTools.PurifyAccounting.CleanWater
                    && cleanRemoved > 0
                    && cleanTotalAfter == cleanTotalBefore
                    && cleanWaterAfter == cleanWaterBefore + cleanRemoved
                    && cleanPurityAfter > cleanPurityBefore
                    && cleanPurityAfter >= cleanCap - 1;
                bool conc = concMode == LiquidTools.PurifyAccounting.Concentrate
                    && concRemoved > 0
                    && concTotalAfter == concTotalBefore - concRemoved
                    && concProductAfter == concProductBefore
                    && concPurityAfter > concPurityBefore
                    && concContaminantAfter < concContaminantBefore;
                bool modes = modesEmpty && modesDraw && modesInject && autoFilledInjects;
                log.Log(Prefix + (exact && refuseOverdose && hasWindow && sliderWorks && modes && manualOk
                        && clean && conc && hydrolysis && digestion && cultivatorOk
                        && adrenalineOk && adrenalineItemsOk && recycleOk && naloxoneOk && pillOk
                        && synaprestOk && apothecaryOk && immunivaxOk && blueBloodOk && ordnanceOk
                        && mysteryOk && perfumeOk ? "OK" : "FAILED")
                    + " exact=" + exact + " refuseOverdose=" + refuseOverdose + " window=" + hasWindow
                    + " slider=" + sliderWorks + " modes=" + modes + " manual=" + manualOk
                    + " cleanWater=" + clean + " concentrate=" + conc
                    + " hydrolysis=" + hydrolysis + " digestion=" + digestion + " cultivator=" + cultivatorOk
                    + " adrenaline=" + adrenalineOk + " adrenalineItems=" + adrenalineItemsOk
                    + " tourniquet=" + tourniquetOk
                    + " recycle=" + recycleOk + " naloxone=" + naloxoneOk + " pills=" + pillOk + " synaprest=" + synaprestOk + " apothecary=" + apothecaryOk + " immunivax=" + immunivaxOk + " blueBlood=" + blueBloodOk + " ordnance=" + ordnanceOk
                    + " mystery=" + mysteryOk + " perfume=" + perfumeOk);
            }
            catch (Exception e)
            {
                log.Error("SELFTEST exception: " + e);
            }
            finally
            {
                ItemFactory.SuppressUiRefresh = false;
                DestroyQuietly(extractor);
                DestroyQuietly(target);
                DestroyQuietly(product);
                DestroyQuietly(machine);
                DestroyQuietly(cultivator);
                DestroyQuietly(hydro);
                DestroyQuietly(sugarCup);
                DestroyQuietly(fruit);
                DestroyQuietly(adrenalineCup);
                DestroyQuietly(apiItem);
                DestroyQuietly(penItem);
                DestroyQuietly(boxItem);
                DestroyQuietly(noteItem);
                DestroyQuietly(tourniquetItem);
                DestroyQuietly(resolvingBottleItem);
                DestroyQuietly(resolvingRefillItem);
                DestroyQuietly(recipeBookItem);
                DestroyQuietly(fabricSample);
                DestroyQuietly(scrapForCondition);
                DestroyQuietly(ingotForCondition);
                DestroyQuietly(tqWorn);
                DestroyQuietly(tqExcellent);
                DestroyQuietly(tqAged);
                DestroyQuietly(recycleCup);
                DestroyQuietly(naloxoneCup);
                DestroyQuietly(naloxoneAlcoholCup);
                DestroyQuietly(naloxoneApiItem);
                DestroyQuietly(naloxoneBoxItem);
                DestroyQuietly(pillCup);
                DestroyQuietly(sannaiCup);
                DestroyQuietly(birthControlBoxItem);
                DestroyQuietly(sannaiBoxItem);
                DestroyQuietly(synaprestCup);
                DestroyQuietly(synaprestBoxItem);
                DestroyQuietly(synaprestInjectorItem);
                DestroyQuietly(immunivaxCup);
                DestroyQuietly(immunivaxWasteCup);
                DestroyQuietly(immuneSerumItem);
                DestroyQuietly(blueGelCup);
                DestroyQuietly(blueGelWasteCup);
                DestroyQuietly(bioGelBottleItem);
                DestroyQuietly(flashCup);
                DestroyQuietly(smokeCup);
                DestroyQuietly(chargeCup);
                DestroyQuietly(chargeHotCup);
                DestroyQuietly(mysteryCrateItem);
                DestroyQuietly(neuroCup);
                DestroyQuietly(pheromoneCup);
                DestroyQuietly(pheromoneHotCup);
                DestroyQuietly(perfumeKitItem);
            }
        }

        private static void PulseStep(GameItem container, Func<GameItem, int> step)
        {
            for (int i = 0; i < 5000; i++)
            {
                if (step(container) <= 0)
                {
                    return;
                }
            }
        }

        private static void SetTemperature(GameItem container, double temperature)
        {
            container.ModifyTag("CONTAINER_TEMP_TAG", delegate (TagState state)
            {
                state.Enable().SetDouble(temperature);
            }, skipValidate: true);
        }

        private static int Purity(GameItem container)
        {
            int total = WaterHelper.GetTotalVolume(container);
            if (total <= 0)
            {
                return 0;
            }
            return (int)((long)LiquidTools.GetLiquidAmount(container, "water") * 10000L / total);
        }

        private static int GetTourniquetConditionMod(GameItem item)
        {
            if (item == null)
            {
                return -999;
            }
            ItemFeature feature = item.FindItemFeatureByCategory("CATEGORY_TOURNIQUET_CONDITION");
            return feature != null && feature.realCondition != null ? feature.realCondition.modValue : -999;
        }

        private static void DestroyQuietly(GameItem item)
        {
            if (item == null)
            {
                return;
            }
            try
            {
                item.Destroy();
            }
            catch (Exception)
            {
                // Ignore: diagnostics must never break the game.
            }
        }
    }
}