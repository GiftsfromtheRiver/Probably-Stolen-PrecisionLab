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
            GameSlotInventory noteSlot = new GameSlotInventory();

            MachineryText purityText = new MachineryText();
            MachineryPanel purityPanel = MachineHelper.Panel(
                Loc.Get("ui.purifier.cap_label", "Purity cap"), purityText, "purity");

            // Panel order: power, module, container, purity, note — one horizontal row like the
            // vanilla machines (user preference). Storage indices returned by GetSlot only count
            // panels that actually contain a storage element: power=0, module=1, container=2,
            // note=3; the purity panel is display-only and contributes no storage index.
            PixelWindow window = MachineHelper.CreateMachineryWindow(
                true,
                MachineHelper.PowerPanel(powerSlot),
                MachineHelper.Panel(Loc.Get("machine.purifier.module", "Modules"), moduleGrid, "module"),
                MachineHelper.Panel(Loc.Get("machine.purifier.container", "Liquid Container"), containerSlot, "container"),
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
                RunPurifierCycle(self, powerSlot, moduleGrid, containerSlot);
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

        private static void RunPurifierCycle(GameItem machine, GameSlotInventory powerSlot, GameGridInventory moduleGrid, GameSlotInventory containerSlot)
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
                int powerUsage = MachineryHelper.GetMachinePowerUsage(machine);
                if (powerSlot.childItem == null || !PowerHelper.CanDrawPowerSource(powerSlot.childItem, powerUsage))
                {
                    return;
                }
                int removed = LiquidTools.PurifyContaminants(container, machine, out LiquidTools.PurifyAccounting accounting);
                if (removed <= 0)
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
                Debug.Log("[PrecisionLab] purifier removed " + removed + " µL of contaminants (" + accounting + ")");
            }
            catch (Exception e)
            {
                Debug.LogError("[PrecisionLab] purifier cycle failed: " + e);
            }
        }
    }
}
