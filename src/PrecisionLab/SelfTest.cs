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
                // Storage indices: power=0, module=1, container=2, note=3 (purity panel is display-only).
                GameSlotInventory noteSlotInv = MachineHelper.GetSlot(machine, 3) as GameSlotInventory;
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

                bool exact = moved == 1234 && targetTotal == 1234 && sourceTotal == totalBefore - moved;
                bool refuseOverdose = overRequest == 0 && afterOverRequest == targetTotal;
                bool hasWindow = extractor.contentWindow != null;

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
                log.Log(Prefix + (exact && refuseOverdose && hasWindow && sliderWorks && modes && manualOk && clean && conc ? "OK" : "FAILED")
                    + " exact=" + exact + " refuseOverdose=" + refuseOverdose + " window=" + hasWindow
                    + " slider=" + sliderWorks + " modes=" + modes + " manual=" + manualOk
                    + " cleanWater=" + clean + " concentrate=" + conc);
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
            }
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
