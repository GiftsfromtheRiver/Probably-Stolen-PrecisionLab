using System;
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
            GameItem tourniquetItem = null;
            GameItem fabricSample = null;
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
                log.Log(Prefix + "cultivator machine: window=" + (cultivator.contentWindow != null)
                    + " power=" + MachineryHelper.GetMachinePowerUsage(cultivator)
                    + " activity=" + cultivatorActivity + "% manual=" + cultivatorManualOk
                    + " manualName='" + (cultivatorManual != null ? cultivatorManual.name : "-") + "'"
                    + " nutrientSlot=" + (nutrientSlot != null) + " fruitAccepted=" + fruitAccepted
                    + " pressed=+" + pressed + "% activityAfterFeed=" + activityAfterFeed + "%"
                    + " fruitConsumed=" + fruitConsumed);

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

                apiItem = AdrenalineContent.CreateApi();
                penItem = AdrenalineContent.CreatePen();
                boxItem = AdrenalineContent.CreateSupplyBox();
                noteItem = AdrenalineContent.CreateRecipeNote();
                tourniquetItem = ItemSpawner.Spawn(ItemIds.Tourniquet);
                fabricSample = ItemSpawner.Spawn(TourniquetCraft.FabricId);
                bool itemsOk = apiItem != null
                    && penItem != null
                    && penItem.IsTag("ITEM_MEDICAL_TAG")
                    && penItem.IsGameItemType("MEDICAL")
                    && penItem.IsGameItemType("LUXURY_ITEM")
                    && penItem.IsTag("LUXURY_ITEM")
                    && penItem.effects.Count >= 2
                    && penItem.effects[0].abilityEffectType == CombatAbilityEffect.EffectType.buff
                    && penItem.effects[0].affectedStat == CombatEntity.EffectType.accuracyPercentage
                    && penItem.effects[1].affectedStat == CombatEntity.EffectType.criticalChanceDealing
                    && boxItem != null
                    && boxItem.canActivateSlotItemFunc != null
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
                    + " | recipes=" + recipesOk + " items=" + itemsOk);

                bool tourniquetOk = tourniquetItem != null
                    && tourniquetItem.IsGameItemType("MEDICAL")
                    && tourniquetItem.IsTag("ITEM_MEDICAL_TAG")
                    && tourniquetItem.unitValue > 0
                    && tourniquetItem.mayActivateSlotItemFunc != null
                    && tourniquetItem.canActivateSlotItemFunc != null
                    && tourniquetItem.shape != null
                    && tourniquetItem.shape.width == 3
                    && tourniquetItem.shape.height == 2
                    && fabricSample != null;
                if (fabricSample != null)
                {
                    TourniquetCraft.Attach(fabricSample);
                    tourniquetOk = tourniquetOk && fabricSample.mayThisTargetItemFunc != null;
                }
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
                        && adrenalineOk && adrenalineItemsOk && recycleOk ? "OK" : "FAILED")
                    + " exact=" + exact + " refuseOverdose=" + refuseOverdose + " window=" + hasWindow
                    + " slider=" + sliderWorks + " modes=" + modes + " manual=" + manualOk
                    + " cleanWater=" + clean + " concentrate=" + conc
                    + " hydrolysis=" + hydrolysis + " digestion=" + digestion + " cultivator=" + cultivatorOk
                    + " adrenaline=" + adrenalineOk + " adrenalineItems=" + adrenalineItemsOk
                    + " tourniquet=" + tourniquetOk
                    + " recycle=" + recycleOk);
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
                DestroyQuietly(fabricSample);
                DestroyQuietly(recycleCup);
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
