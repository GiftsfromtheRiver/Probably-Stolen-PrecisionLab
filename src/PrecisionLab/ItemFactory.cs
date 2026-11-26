using System;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Factories for the two mod items. They are registered on ModItemDirectory and are
    /// called every time the game creates a new instance (DirectoryMaster.Item / ItemSpawner).
    /// The structure is copied from vanilla machine/item factories (MachinePurifier,
    /// FoodItemDirectory's droppers) so the items behave exactly like native content.
    /// </summary>
    internal static class ItemFactory
    {
        internal const int ExtractorCapacityUl = 10000;   // 10 mL
        internal const int ExtractorBasePrice = 1200;
        internal const int PurifierBasePrice = 2400;
        internal const int PurifierPowerPerCycle = 6;
        internal const int CultivatorBasePrice = 1800;
        internal const int CultivatorPowerPerCycle = 8;

        /// <summary>
        /// Set by the diagnostic self-test: items created while this is true are not attached to
        /// any inventory UI element, so the water-feature initialisation (which touches the
        /// drag handler / sprite refresh) would NRE. Skipping it only affects diagnostics.
        /// </summary>
        internal static bool SuppressUiRefresh;

        // ------------------------------------------------------------------
        // 精确取液器 / Precision Liquid Extractor
        // ------------------------------------------------------------------
        internal static GameItem CreateExtractor()
        {
            GameItem item = ItemDirectory.CreateEmptyItem();
            item.name = Loc.Get("item.extractor.name", "Precision Liquid Extractor");
            item.flavorText = Loc.Get(
                "item.extractor.flavor",
                "A lab-grade pipette rebuilt from three broken ones. It can draw or inject with 1 µL accuracy.");
            item.shortDescription = Loc.Get(
                "item.extractor.desc",
                "Pipette-style metering tool with a 1 µL dial. Double-click it to open the metering window and type the exact volume (or use the slider and presets, 1-10000 µL). Empty: draws exactly N µL from the targeted container. Filled: injects exactly N µL into it. If the requested volume is not available the attempt is refused and nothing moves.");
            item.SetGameItemType("TOOL");
            ModSprites.Apply(item, SpriteKeys.Extractor, "Items/water_container3", "1ml");
            InitLiquidContainer(item, ExtractorCapacityUl, ExtractorBasePrice);
            item.EnableTag(Tags.Graduated);
            item.unitValue = ExtractorBasePrice;
            LiquidTools.SetDose(item, ModEntry.ConfigDoseUl);

            item.maySelectSlotItemFunc = (self, inventory, slot) => WaterHelper.CanUseWaterContainer(self);
            item.canSelectSlotItemFunc = (self, inventory, slot) => WaterHelper.CanUseWaterContainer(self);

            item.mayThisTargetItemFunc = (self, target) => CanUseOn(self, target);
            item.canThisTargetItemFunc = (self, target) => CanUseOn(self, target);
            item.onThisTargetItemFunc = delegate (GameItem self, GameItem target)
            {
                OnExtractOrInject(self, target);
            };

            // The extractor only initiates transfers; vanilla containers must not be able to
            // pour themselves into it (that would bypass the metering and the item description).
            item.mayItemTargetThisFunc = (self, initiator) => false;
            item.canItemTargetThisFunc = (self, initiator) => false;

            // The metering is configured through the content window (double-click). The item is
            // deliberately not "activatable": the native double-click handler would otherwise
            // also run the activation callback and change the dose while opening the window.
            item.mayActivateSlotItemFunc = (self, inventory, slot) => false;
            item.canActivateSlotItemFunc = (self, inventory, slot) => false;

            // Double-clicking the item opens this window (native ItemMouseDoubleClickHandler
            // behaviour for items with a content window): the pipette-style metering dial.
            try
            {
                item.SetContentWindow(ExtractorWindow.Create(item));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] extractor settings window could not be created: " + e);
            }

            ModHelper.SetSource(item, Loc.ModId, ModEntry.ModDisplayName);
            return item;
        }

        private static bool CanUseOn(GameItem self, GameItem target)
        {
            if (self == null || target == null || self == target)
            {
                return false;
            }
            if (!GeneralHelper.IsItemOwned(self) || !GeneralHelper.IsItemEffectivelyOwned(target))
            {
                return false;
            }
            if (target.IsTag(Tags.WineContainer))
            {
                // Wine has its own fermentation/quality simulation; mixing with a metering
                // tool would corrupt it, so it is deliberately not a valid target.
                return false;
            }
            return target.IsTag(Tags.LiquidContainer);
        }

        private static void OnExtractOrInject(GameItem extractor, GameItem target)
        {
            try
            {
                int dose = LiquidTools.GetDose(extractor);
                bool drawing = LiquidTools.ResolveDirection(extractor);
                GameItem source = drawing ? target : extractor;
                GameItem destination = drawing ? extractor : target;
                int deliverable = LiquidTools.GetDeliverableUl(source, destination, dose);
                if (deliverable < dose)
                {
                    SetLastResult(extractor, Loc.Fmt(
                        "extractor.last.fail",
                        "Last attempt: FAILED — only {0} µL transferable of the requested {1} µL.",
                        deliverable, dose));
                    return;
                }
                int moved = LiquidTools.MoveExact(source, destination, dose);
                if (moved == dose)
                {
                    SetLastResult(extractor, drawing
                        ? Loc.Fmt("extractor.last.draw", "Last operation: drew exactly {0} µL.", moved)
                        : Loc.Fmt("extractor.last.inject", "Last operation: injected exactly {0} µL.", moved));
                    if (drawing)
                    {
                        AudioHelper.DropperDraw();
                    }
                    else
                    {
                        AudioHelper.DropperDrop();
                    }
                }
                else
                {
                    SetLastResult(extractor, Loc.Get("extractor.last.error", "Last attempt: FAILED (internal error, nothing transferred)."));
                }
                WaterHelper.UpdateWaterBottleSound(extractor);
                WaterHelper.UpdateWaterBottleSound(target);
            }
            catch (Exception e)
            {
                Debug.LogError("[PrecisionLab] extractor interaction failed: " + e);
            }
        }

        private static void SetLastResult(GameItem extractor, string text)
        {
            extractor.ModifyTag(Tags.ExtractorLastResult, delegate (TagState state)
            {
                state.Enable().SetString(text ?? "");
            });
        }

        /// water-feature system all treat modded containers as native ones.
        /// </summary>
        internal static void InitLiquidContainer(GameItem item, int capacityUl, int basePrice)
        {
            item.EnableTag(Tags.LiquidContainer);
            item.ModifyTag(Tags.LiquidBasePrice, state =>
            {
                state.Enable().SetInt(basePrice);
            });
            int capacity = Math.Max(1000, capacityUl);
            item.ModifyTag(Tags.LiquidCapacity, state =>
            {
                state.Enable().SetInt(capacity);
            });
            for (int i = 0; i < Liquid.Liquids.Count; i++)
            {
                string part = Liquid.Liquids[i].currentPart;
                item.ModifyTag(part, state =>
                {
                    state.Enable().SetInt(0);
                });
            }
            ChemicalFeedbackHelper.InitChemicalIntentItem(item, ChemicalFeedbackHelper.ChemicalIntent.HYDRATION);
            ChemicalHelper.InitContainerTemp(item);
            ChemicalHelper.InitContainerTempFunction(item);
            if (!SuppressUiRefresh)
            {
                try
                {
                    // WaterFeatureHelper.InitWaterFeature dereferences the item's UI element /
                    // drag handler. If an item is created before it can be attached (menu-time
                    // creation, diagnostics), skip the feature init; the first liquid change
                    // creates it lazily through WaterFeatureHelper.GetWaterFeature.
                    WaterFeatureHelper.InitWaterFeature(item);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[PrecisionLab] water feature init deferred: " + e.Message);
                    WaterHelper.UpdateContainerGoodType(item);
                }
            }
            WaterHelper.UpdateContainerGoodType(item);
            WaterHelper.UpdateWaterBottleSound(item);
        }

        // ------------------------------------------------------------------
        // 提纯机器 / Purification Machine
        // ------------------------------------------------------------------
        internal static GameItem CreatePurifier()
        {
            GameSlotInventory powerSlot = new GameSlotInventory();
            GameGridInventory moduleGrid = new GameGridInventory(MachineHelper.DEFAULT_MODULE_BAY_CONFIG, MachineHelper.DEFAULT_MODULE_BAY_WIDTH);
            GameSlotInventory containerSlot = new GameSlotInventory();
            GameSlotInventory wasteSlot = new GameSlotInventory();
            GameSlotInventory noteSlot = new GameSlotInventory();

            MachineryText purityText = new MachineryText();
            MachineryPanel purityPanel = MachineHelper.Panel(
                Loc.Get("ui.purifier.cap_label", "Purity cap"), purityText, "purity");

            // Panel order (single row): power, module, container, waste output, purity, note.
            // Storage indices: power=0, module=1, container=2, waste=3, note=4 — the purity panel
            // is display-only and contributes no storage index.
            PixelWindow window = MachineHelper.CreateMachineryWindow(
                true,
                MachineHelper.PowerPanel(powerSlot),
                MachineHelper.Panel(Loc.Get("machine.purifier.module", "Modules"), moduleGrid, "module"),
                MachineHelper.Panel(Loc.Get("machine.purifier.container", "Liquid Container"), containerSlot, "container"),
                MachineHelper.Panel(Loc.Get("machine.purifier.waste", "Waste Output"), wasteSlot, "waste"),
                purityPanel,
                MachineHelper.NotePanel(noteSlot));

            GameItem machine = ItemDirectory.CreateEmptyItem().SetContentWindow(window);
            machine.name = Loc.Get("item.purifier.name", "Purification Machine");
            machine.flavorText = Loc.Get(
                "item.purifier.flavor",
                "Industrial filter stack bolted to a stolen centrifuge. It does not care where the fluid came from — it only cares what is floating in it.");
            machine.shortDescription = Loc.Get(
                "item.purifier.desc",
                "Removes physical, chemical (including heavy metals) and biological contaminants from any liquid container during the night cycle (or an extra cycle from a turbo booster). Valuable ingredients are never removed. Quality modules raise the purity cap; performance modules raise the amount processed per cycle; efficiency modules reduce power draw.");
            machine.unitValue = PurifierBasePrice;
            ModSprites.Apply(machine, SpriteKeys.Purifier, "Items/items_tool2", "purifier");
            machine.SetGameItemType("MACHINE");
            machine.EnableTag("CONTAINER_TAG");
            machine.EnableTag("manufacture");
            machine.EnableTag(MachineHelper.STANDARD_MACHINE_TAG);
            machine.EnableTag("TURBO_BOOST_ELIGIBLE_TAG");
            MachineryHelper.InitMachinery(machine, PurifierPowerPerCycle);
            machine.ModifyTag("MACHINE_QUALITY_RATIO_INT", delegate (TagState state)
            {
                state.Enable().SetInt(2);
            });

            MachineHelper.SetupBatterySlot(powerSlot, machine);
            MachineHelper.SetupModuleBay(moduleGrid, machine, delegate
            {
                UpdatePurityPanel(purityText, machine);
            });
            MachineHelper.SetupManualSlot(noteSlot);

            // Waste output slot: the purifier collects dextro waste from the chiral resolution
            // step here as an item instead of destroying it.
            wasteSlot.mayInventoryAddItemFunc = (candidate, inventory) =>
                GeneralHelper.IsItemOwned(candidate) && candidate.identifier == ItemIds.DextroWaste;
            // Deliberately no background fade sprite: the vanilla chemical2 icon looked like a
            // paper note inside the empty waste slot and confused players. The panel label already
            // says "Waste Output"; an empty slot renders as a plain dark slot now.
            wasteSlot.SetTooltipName(Loc.Get("machine.purifier.waste", "Waste Output"));

            containerSlot.mayInventoryAddItemFunc = (candidate, inventory) =>
                GeneralHelper.IsItemOwned(candidate)
                && candidate.IsTag(Tags.LiquidContainer)
                && !candidate.IsTag(Tags.WineContainer);
            containerSlot.SetBackgroundFadeSprite("Items/water_container4", "xl");
            containerSlot.onSlotAddItemFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(
                containerSlot.onSlotAddItemFunc,
                (Action<GameItem, GameInventory, SlotMarker>)delegate (GameItem addedItem, GameInventory inventory, SlotMarker slot)
                {
                    ModSprites.Apply(machine, SpriteKeys.PurifierActive, "Items/items_tool2", "purifier_w");
                });
            containerSlot.onSlotRemoveItemFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(
                containerSlot.onSlotRemoveItemFunc,
                (Action<GameItem, GameInventory, SlotMarker>)delegate (GameItem removedItem, GameInventory inventory, SlotMarker slot)
                {
                    ModSprites.Apply(machine, SpriteKeys.Purifier, "Items/items_tool2", "purifier");
                });

            machine.onCycleEndSlotItemFunc = delegate (GameItem self, GameInventory parentInventory, SlotMarker slot)
            {
                RunPurifierCycle(self, powerSlot, moduleGrid, containerSlot, wasteSlot);
            };

            // NOTE: the instruction note is NOT inserted here. DirectoryMaster.Item strips all
            // child items from a freshly created item, so the note is added by
            // PurifierManualHelper (Harmony postfix on DirectoryMaster.Item) / on save load.
            UpdatePurityPanel(purityText, machine);
            ModHelper.SetSource(machine, Loc.ModId, ModEntry.ModDisplayName);
            return machine;
        }

        /// <summary>The instruction paper that ships in the purifier's note slot.</summary>
        internal static GameItem CreatePurifierManual()
        {
            GameItem manual = DirectoryMaster.Item("postit");
            if (manual == null)
            {
                return null;
            }
            manual.name = Loc.Get("item.purifier.manual.name", "Purification Machine Notes");
            manual.flavorText = Loc.Get(
                "item.purifier.manual.flavor",
                "Someone scribbled the important parts on a sticky note and taped it to the filter stack.");
            manual.shortDescription = Loc.Get(
                "item.purifier.manual.desc",
                "[Module effects]:\n- Efficiency: power draw\n- Performance: amount processed per night\n- Quality: purity cap (purity +x%, up to 100%)\n\nProvide power and a liquid container and the machine runs at night. Removes physical, chemical (including heavy metals) and biological contamination; valuable ingredients are kept.");
            manual.SetGameItemType("DOCUMENT");
            manual.EnableTag("IMPORTANT_TAG");
            manual.EnableTag("MACHINE_MANUAL_TAG");
            manual.unitValue = 0L;
            ModHelper.SetSource(manual, Loc.ModId, ModEntry.ModDisplayName);
            return manual;
        }

        /// <summary>Updates the machine window's purity-cap panel (cap + quality-module bonus).</summary>
        private static void UpdatePurityPanel(MachineryText text, GameItem machine)
        {
            if (text == null || machine == null)
            {
                return;
            }
            try
            {
                int cap = LiquidTools.GetPurityCap(machine);
                int baseCap = MachinePurifier.GetMaxPurityCap(0);
                double delta = (cap - baseCap) / 100.0;
                // Keep this short: the panel row is a single line like the vanilla machines and
                // the panel header already says "Purity cap". The label is shown by the panel.
                text.SetText(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.00}% (+{1:0.00}%)", cap / 100.0, delta));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] purity panel update failed: " + e.Message);
            }
        }

        private static void RunPurifierCycle(GameItem machine, GameSlotInventory powerSlot, GameGridInventory moduleGrid, GameSlotInventory containerSlot, GameSlotInventory wasteSlot)
        {
            try
            {
                machine.ModifyTag(Tags.PurifierRemoved, delegate (TagState state)
                {
                    state.Enable().SetInt(0);
                });
                GameItem container = containerSlot != null ? containerSlot.childItem : null;
                if (container == null || WaterHelper.IsEmpty(container))
                {
                    return;
                }
                // The purifier's waste output slot collects dextro waste from the chiral
                // resolution step; this separation does not need a charged battery.
                int wasteCollected = CollectDextroWaste(container, wasteSlot);
                if (wasteCollected > 0)
                {
                    Debug.Log("[PrecisionLab] purifier collected " + wasteCollected + " µL of dextro waste");
                }
                int powerUsage = MachineryHelper.GetMachinePowerUsage(machine);
                if (powerSlot.childItem == null || !PowerHelper.CanDrawPowerSource(powerSlot.childItem, powerUsage))
                {
                    return;
                }
                int removed = LiquidTools.PurifyContaminants(container, machine, out LiquidTools.PurifyAccounting accounting);
                // Crude conversion capacity: base 2 mL per night, +1 mL for every +50% performance.
                int performance = MachineryHelper.GetCurrentPerformanceBonus(machine);
                int crudeCapacity = 2000 + Math.Max(0, performance / 50) * 1000;
                int concentrated = AntibioticPurify.Process(container, crudeCapacity);
                int antiRad = AntiRadiationPurify.Process(container, crudeCapacity);
                if (removed <= 0 && concentrated <= 0 && antiRad <= 0)
                {
                    return;
                }
                PowerHelper.DrawPowerSource(powerSlot.childItem, powerUsage);
                int finalRemoved = removed;
                machine.ModifyTag(Tags.PurifierRemoved, delegate (TagState state)
                {
                    state.Enable().SetInt(finalRemoved);
                });
                MachineHelper.OnMachineActioned(machine, moduleGrid);
                Debug.Log("[PrecisionLab] purifier removed " + removed + " µL of contaminants; antibiotic concentrated=" + concentrated + " anti-rad=" + antiRad);
            }
            catch (Exception e)
            {
                Debug.LogError("[PrecisionLab] purifier cycle failed: " + e);
            }
        }

        /// <summary>
        /// Moves up to all dextro waste from the container into the purifier's waste output slot
        /// as a waste item. Returns the collected amount (0 when there is no room / nothing).
        /// </summary>
        internal static int CollectDextroWaste(GameItem container, GameSlotInventory wasteSlot)
        {
            if (container == null || wasteSlot == null)
            {
                return 0;
            }
            try
            {
                int waste = LiquidTools.GetLiquidAmount(container, CustomLiquids.DextroWaste);
                if (waste <= 0)
                {
                    return 0;
                }
                if (wasteSlot.childItem != null)
                {
                    // The output holds one waste bottle, but it accumulates across cycles: more
                    // batches add their dextro waste into the same bottle instead of the
                    // collection silently doing nothing until the player empties the slot.
                    GameItem existing = wasteSlot.childItem;
                    if (existing == null || !existing.identifier.Equals(ItemIds.DextroWaste))
                    {
                        return 0;
                    }
                    ChemicalHelper.AddChemContentUL(existing, CustomLiquids.DextroWaste, waste);
                    ChemicalProductHelper.GradeProduct(existing);
                    try
                    {
                        ItemFeature existingFeature = WaterFeatureHelper.GetWaterFeature(existing);
                        WaterFeatureHelper.OnLiquidChanged(existing, existingFeature.IsExposed());
                    }
                    catch (Exception)
                    {
                        // Not attached to a live UI yet.
                    }
                }
                else
                {
                    GameItem item = ItemSpawner.Spawn(ItemIds.DextroWaste);
                    if (item == null)
                    {
                        return 0;
                    }
                    ChemicalHelper.SetChemContentUL(item, CustomLiquids.DextroWaste, waste);
                    GraphUtils.TryAcceptAll(wasteSlot, item);
                    if (wasteSlot.childItem == null)
                    {
                        item.Destroy();
                        return 0;
                    }
                }
                string part = Liquid.GetCurrentPartFromId(CustomLiquids.DextroWaste);
                if (part != null)
                {
                    container.ModifyTag(part, delegate (TagState state)
                    {
                        state.Enable().SetInt(0);
                    }, skipValidate: true);
                }
                if (!SuppressUiRefresh)
                {
                    try
                    {
                        ItemFeature feature = WaterFeatureHelper.GetWaterFeature(container);
                        WaterFeatureHelper.OnLiquidChanged(container, feature.IsExposed());
                    }
                    catch (Exception)
                    {
                        // Not attached to a live UI yet.
                    }
                }
                return waste;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] collecting dextro waste failed: " + e.Message);
                return 0;
            }
        }

        // ------------------------------------------------------------------
        // 微生物培养机 / Microbial Cultivator
        // ------------------------------------------------------------------
        internal static GameItem CreateCultivator()
        {
            GameSlotInventory powerSlot = new GameSlotInventory();
            GameGridInventory moduleGrid = new GameGridInventory(MachineHelper.DEFAULT_MODULE_BAY_CONFIG, MachineHelper.DEFAULT_MODULE_BAY_WIDTH);
            GameSlotInventory containerSlot = new GameSlotInventory();
            GameSlotInventory noteSlot = new GameSlotInventory();

            MachineryText cultureText = new MachineryText();
            MachineryPanel culturePanel = MachineHelper.Panel(
                Loc.Get("ui.cultivator.culture_label", "Culture"), cultureText, "culture");
            MachineryButton strainButton = new MachineryButton(
                Loc.Get("ui.cultivator.strain_button", "选择菌群"));
            MachineryPanel strainPanel = MachineHelper.Panel(
                Loc.Get("machine.cultivator.strain", "菌群"), strainButton, "strain");

            // Panel order: power, module, container, culture, note — one row.
            // Storage indices (display-only culture text adds none): power=0, module=1,
            // container=2, note=3.
            PixelWindow window = MachineHelper.CreateMachineryWindow(
                true,
                MachineHelper.PowerPanel(powerSlot),
                MachineHelper.Panel(Loc.Get("machine.cultivator.module", "Modules"), moduleGrid, "module"),
                MachineHelper.Panel(Loc.Get("machine.cultivator.container", "Liquid Container"), containerSlot, "container"),
                culturePanel,
                strainPanel,
                MachineHelper.NotePanel(noteSlot));

            GameItem machine = ItemDirectory.CreateEmptyItem().SetContentWindow(window);
            machine.name = Loc.Get("item.cultivator.name", "Microbial Cultivator");
            machine.flavorText = Loc.Get(
                "item.cultivator.flavor",
                "A sealed fermenter humming with something that eats sugar. Do not open the lid during a night shift.");
            machine.shortDescription = Loc.Get(
                "item.cultivator.desc",
                "Cultures sugar-eating microbes in a sealed vat. Put a sugary liquid container (for example blended nutrifruit juice) in the liquid slot: each night the culture digests sugar and produces crude antibiotic culture plus contaminants. No nutrifruit slot is needed. When the liquid runs out of sugar the culture goes dormant until sugar is added again. Performance modules increase the culture yield per processed sugar, quality modules reduce contaminants, efficiency modules reduce power draw.");
            machine.unitValue = CultivatorBasePrice;
            ModSprites.Apply(machine, SpriteKeys.Cultivator, "Items/items_tool2", "purifier");
            machine.SetGameItemType("MACHINE");
            machine.EnableTag("CONTAINER_TAG");
            machine.EnableTag("manufacture");
            machine.EnableTag(MachineHelper.STANDARD_MACHINE_TAG);
            machine.EnableTag("TURBO_BOOST_ELIGIBLE_TAG");
            MachineryHelper.InitMachinery(machine, CultivatorPowerPerCycle);
            machine.ModifyTag(Tags.CultivatorActivity, delegate (TagState state)
            {
                state.Enable().SetInt(100);
            });
            CultivatorStrainUI.SetStrain(machine, AntibioticContent.StrainNone);
            strainButton.SetCallback(delegate
            {
                GameItem liveMachine = CultivatorStrainUI.ResolveMachineFromButton(strainButton, machine);
                CultivatorStrainUI.Open(liveMachine, cultureText, strainButton);
            });

            MachineHelper.SetupBatterySlot(powerSlot, machine);
            MachineHelper.SetupModuleBay(moduleGrid, machine, delegate
            {
                UpdateCulturePanel(cultureText, machine);
            });
            MachineHelper.SetupManualSlot(noteSlot);

            containerSlot.mayInventoryAddItemFunc = (candidate, inventory) =>
                GeneralHelper.IsItemOwned(candidate)
                && candidate.IsTag(Tags.LiquidContainer)
                && !candidate.IsTag(Tags.WineContainer);
            containerSlot.SetBackgroundFadeSprite("Items/water_container4", "xl");
            containerSlot.onSlotAddItemFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(
                containerSlot.onSlotAddItemFunc,
                (Action<GameItem, GameInventory, SlotMarker>)delegate (GameItem addedItem, GameInventory inventory, SlotMarker slot)
                {
                    ModSprites.Apply(machine, SpriteKeys.CultivatorActive, "Items/items_tool2", "purifier_w");
                });
            containerSlot.onSlotRemoveItemFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(
                containerSlot.onSlotRemoveItemFunc,
                (Action<GameItem, GameInventory, SlotMarker>)delegate (GameItem removedItem, GameInventory inventory, SlotMarker slot)
                {
                    ModSprites.Apply(machine, SpriteKeys.Cultivator, "Items/items_tool2", "purifier");
                });

            machine.onCycleEndSlotItemFunc = delegate (GameItem self, GameInventory parentInventory, SlotMarker slot)
            {
                RunCultivatorCycle(self, powerSlot, moduleGrid, containerSlot, cultureText);
            };

            // Keep the culture readout live: activity changes on the nightly cycle (and can go
            // dormant) while the player never touches the machine. A once-per-second refresh with
            // a cheap string comparison keeps the panel in sync even without slot interactions.
            machine.onOneSecondItemFunc = (Action<GameItem, GameInventory, SlotMarker>)Delegate.Combine(
                machine.onOneSecondItemFunc,
                (Action<GameItem, GameInventory, SlotMarker>)delegate (GameItem self, GameInventory inventory, SlotMarker slotMarker)
                {
                    RefreshCulturePanel(self, cultureText);
                });

            UpdateCulturePanel(cultureText, machine);
            ModHelper.SetSource(machine, Loc.ModId, ModEntry.ModDisplayName);
            return machine;
        }

        /// <summary>Instruction paper that ships in the cultivator's note slot.</summary>
        internal static GameItem CreateCultivatorManual()
        {
            GameItem manual = DirectoryMaster.Item("postit");
            if (manual == null)
            {
                return null;
            }
            manual.name = Loc.Get("item.cultivator.manual.name", "Microbial Cultivator Notes");
            manual.flavorText = Loc.Get(
                "item.cultivator.manual.flavor",
                "A lab note covered in coffee rings and a warning about not licking the culture.");
            manual.shortDescription = Loc.Get(
                "item.cultivator.manual.desc",
                "[Module effects]:\n- Efficiency: power draw\n- Performance: higher culture yield per processed sugar\n- Quality: fewer contaminants\n\nPut a **sugary liquid** container in the liquid slot, for example blended nutrifruit juice. There is no nutrifruit slot and no fruit feeding: the culture wakes from dormancy as soon as sugar is present. An empty bottle or pure water has no sugar and will NOT produce antibiotic culture. Turbo booster only triggers one extra nightly cycle; it does not replace the sugar.");
            manual.SetGameItemType("DOCUMENT");
            manual.EnableTag("IMPORTANT_TAG");
            manual.EnableTag("MACHINE_MANUAL_TAG");
            manual.unitValue = 0L;
            ModHelper.SetSource(manual, Loc.ModId, ModEntry.ModDisplayName);
            return manual;
        }

        internal static int GetCultivatorActivity(GameItem machine)
        {
            if (machine == null || !machine.IsTag(Tags.CultivatorActivity))
            {
                return 100;
            }
            return Mathf.Clamp(machine.GetTagReadonly(Tags.CultivatorActivity).GetInt(), 0, 100);
        }

        /// <summary>Formats the culture panel text from the live machine state.</summary>
        internal static string FormatCulturePanel(GameItem machine)
        {
            int activity = GetCultivatorActivity(machine);
            if (activity <= 0)
            {
                return Loc.Get("tooltip.cultivator.dormant_panel", "Dormant · needs sugar");
            }
            int performance = MachineryHelper.GetCurrentPerformanceBonus(machine);
            int throughput = LiquidTools.GetCultivatorThroughput(performance, activity);
            int cultureYield = LiquidTools.GetCultivatorCultureYield(performance, activity);
            string strain = CultivatorStrainUI.GetStrain(machine);
            bool selected = !string.IsNullOrEmpty(strain) && strain != AntibioticContent.StrainNone;
            if (selected)
            {
                try
                {
                    GameSlotInventory liquidSlot = MachineHelper.GetSlot(machine, 2) as GameSlotInventory;
                    GameItem liquid = liquidSlot != null ? liquidSlot.childItem : null;
                    int sugar = liquid != null ? LiquidTools.GetLiquidAmount(liquid, "sugar") : 0;
                    if (sugar <= 0)
                    {
                        return Loc.Fmt("tooltip.cultivator.no_sugar_panel",
                            "{0}% · 缺糖：需含糖液体 · 不产培养液", activity);
                    }
                    if (!AntibioticCultivation.EnvironmentOk(machine, liquid, out string catalyst))
                    {
                        return Loc.Fmt("tooltip.cultivator.no_catalyst_panel",
                            "{0}% · 缺少培养环境：{1} 需≥总量10%", activity, catalyst == "acid" ? "酸" : "碱");
                    }
                    string catalystLabel = catalyst == null ? "" : Loc.Get(catalyst == "acid" ? "tooltip.cultivator.catalyst_acid" : "tooltip.cultivator.catalyst_base", catalyst == "acid" ? "酸" : "碱");
                    string cost = catalyst == null ? "" : Loc.Fmt("tooltip.cultivator.catalyst_cost",
                        " · {0}耗约{1}mL/夜", catalystLabel, LiquidTools.FormatUl((int)Math.Round(cultureYield * 0.10)));
                    return Loc.Fmt("panel.cultivator.yield",
                        "{0}% · 培养液{1}mL/夜{2} · {3}", activity, LiquidTools.FormatUl(cultureYield), cost, CultivatorStrainUI.DisplayFor(strain));
                }
                catch (Exception) { }
            }
            return Loc.Fmt("panel.cultivator.none",
                "{0}% · 糖{1}mL/夜 · 原版糖消化", activity, LiquidTools.FormatUl(throughput));
        }

        /// <summary>Updates the culture panel: activity (or dormant state) + sugar throughput per night.</summary>
        internal static void UpdateCulturePanel(MachineryText text, GameItem machine)
        {
            if (text == null || machine == null)
            {
                return;
            }
            try
            {
                string value = FormatCulturePanel(machine);
                if (text.GetText() != value)
                {
                    text.SetText(value);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] culture panel update failed: " + e.Message);
            }
        }

        /// <summary>Finds the live culture MachineryText inside the placed machine's panel row and updates it.</summary>
        internal static void RefreshCulturePanel(GameItem machine, MachineryText fallback = null)
        {
            try
            {
                if (machine == null) return;
                PixelWindow window = machine.contentWindow;
                if (window != null && window.childElement is MachineryPanelRow row)
                {
                    foreach (MachineryPanel panel in row.Panels)
                    {
                        if (panel == null || panel.role != "culture") continue;
                        if (panel.content is MachineryText live)
                        {
                            UpdateCulturePanel(live, machine);
                            return;
                        }
                    }
                }
                if (fallback != null) UpdateCulturePanel(fallback, machine);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] live culture panel refresh failed: " + e.Message);
            }
        }

        /// <summary>Nutrifruit sizes give different amounts of culture feed (activity points).</summary>
        internal static int GetNutrifruitNutrition(GameItem fruit)
        {
            if (fruit == null || fruit.identifier != "nutrifruit")
            {
                return 0;
            }
            long value = fruit.unitValue;
            if (value <= 9L)
            {
                return 15;   // small
            }
            if (value <= 14L)
            {
                return 25;   // normal
            }
            if (value <= 25L)
            {
                return 40;   // large
            }
            return 60;       // huge
        }

        /// <summary>
        private static void RunCultivatorCycle(GameItem machine, GameSlotInventory powerSlot, GameGridInventory moduleGrid, GameSlotInventory containerSlot, MachineryText cultureText)
        {
            try
            {
                machine.ModifyTag(Tags.CultivatorProcessed, delegate (TagState state)
                {
                    state.Enable().SetInt(0);
                });
                GameItem container = containerSlot != null ? containerSlot.childItem : null;
                int sugar = container != null && !WaterHelper.IsEmpty(container)
                    ? LiquidTools.GetLiquidAmount(container, "sugar")
                    : 0;
                int processed = 0;
                int activity = GetCultivatorActivity(machine);
                if (sugar > 0 && container != null && !AntibioticCultivation.EnvironmentOk(machine, container, out _))
                {
                    UpdateCulturePanel(cultureText, machine);
                    return;
                }
                if (sugar > 0 && activity <= 0)
                {
                    activity = 25;
                    machine.ModifyTag(Tags.CultivatorActivity, delegate (TagState state)
                    {
                        state.Enable().SetInt(activity);
                    });
                }
                if (sugar > 0 && activity > 0)
                {
                    int powerUsage = MachineryHelper.GetMachinePowerUsage(machine);
                    if (powerSlot.childItem != null && PowerHelper.CanDrawPowerSource(powerSlot.childItem, powerUsage))
                    {
                        int performance = MachineryHelper.GetCurrentPerformanceBonus(machine);
                        int quality = MachineryHelper.GetCurrentQualityBonus(machine);                        processed = LiquidTools.DigestSugar(container, performance, quality, activity);
                        if (processed > 0)
                        {
                            PowerHelper.DrawPowerSource(powerSlot.childItem, powerUsage);
                            machine.ModifyTag(Tags.CultivatorActivity, delegate (TagState state)
                            {
                                state.Enable().SetInt(Math.Min(100, activity + 5));
                            });
                            machine.ModifyTag(Tags.CultivatorProcessed, delegate (TagState state)
                            {
                                state.Enable().SetInt(processed);
                            });
                            MachineHelper.OnMachineActioned(machine, moduleGrid);
                            AntibioticCultivation.Produce(machine, container, processed);
                            Debug.Log("[PrecisionLab] cultivator digested " + processed + " µL of sugar");
                        }
                    }
                }
                if (sugar <= 0)
                {
                    // No sugar left: the colony goes dormant quickly (nutrifruit alone keeps it
                    // alive but with nothing to digest it still shuts down until more sugar comes).
                    int now = GetCultivatorActivity(machine);
                    int next = Math.Max(0, now - 25);
                    machine.ModifyTag(Tags.CultivatorActivity, delegate (TagState state)
                    {
                        state.Enable().SetInt(next);
                    });
                    if (next <= 0 && now > 0)
                    {
                        Debug.Log("[PrecisionLab] culture went dormant (no sugar)");
                    }
                }
                else if (processed <= 0)
                {
                    // Sugar available but nothing was processed (no power / dormant): slow decay.
                    int now = GetCultivatorActivity(machine);
                    machine.ModifyTag(Tags.CultivatorActivity, delegate (TagState state)
                    {
                        state.Enable().SetInt(Math.Max(0, now - 10));
                    });
                }
                UpdateCulturePanel(cultureText, machine);
            }
            catch (Exception e)
            {
                Debug.LogError("[PrecisionLab] cultivator cycle failed: " + e);
            }
        }
    }
}
