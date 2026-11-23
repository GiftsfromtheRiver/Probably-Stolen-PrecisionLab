using System;
using TMPro;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Replaces the vanilla heating plate factory with the same machine plus ±1°C
    /// buttons alongside the original ±5°C buttons, so coarse speed stays intact.
    /// </summary>
    [HarmonyPatch(typeof(MachineHeatingPlate), "HeatingPlate")]
    internal static class PreciseHeatingPlate
    {
        private static bool Prefix(ref GameItem __result)
        {
            try
            {
                __result = Build();
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] precise heating plate factory failed: " + e.Message);
                return true;
            }
        }

        private static GameItem Build()
        {
            var tuple = MachineHeatingPlate.CreateMachineInventoryWindow(8, 8);
            MachineryPanelRow rows = tuple.Item1;
            PixelWindow window = tuple.Item2;
            GameGridInventory moduleGrid = tuple.Item3;
            GameSlotInventory containerSlot = tuple.Item4;
            MachineryText tempLabel = tuple.Item5;
            MachineryRadioList modes = tuple.Item6;

            MachineryButton minus5 = new MachineryButton(LocHelper.GetLocalizedMechanic("mech_heating_plate_btn_minus"));
            MachineryButton plus5 = new MachineryButton(LocHelper.GetLocalizedMechanic("mech_heating_plate_btn_plus"));
            MachineryButton minus1 = new MachineryButton("-1");
            MachineryButton plus1 = new MachineryButton("+1");
            MachineryText tempControl = new MachineryText(MetaHelper.FormatTemperature(99), 9, HorizontalAlignmentOptions.Center, uppercase: false).SetFillWidth(fill: false);
            minus5.SetFillWidth(fill: false);
            plus5.SetFillWidth(fill: false);
            minus1.SetFillWidth(fill: false);
            plus1.SetFillWidth(fill: false);

            rows.Add(MachineHelper.Panel(LocHelper.GetLocalizedMechanic("mech_heating_plate_label_target_temp"),
                MachineryStack.Horizontal(minus5, tempControl, plus5), "target"));
            rows.Add(MachineHelper.Panel("精确控温",
                MachineryStack.Horizontal(minus1, plus1), "fine"));

            SetupWaterSlot(containerSlot);
            GameItem machine = ItemDirectory.CreateEmptyItem().SetContentWindow(window).SetSpriteAndShape("Items/items_tool2", "heating_plate");
            containerSlot.onSlotAddItemEndFunc = delegate
            {
                machine.SetSpriteAndShape("Items/items_tool2", "heating_plate_b");
            };
            containerSlot.onSlotRemoveItemEndFunc = delegate
            {
                machine.SetSpriteAndShape("Items/items_tool2", "heating_plate");
            };
            MachineBlender.SetupChemModuleGrid(moduleGrid, machine);
            machine.name = LocHelper.GetLocalizedItem("item_heating_plate_name");
            machine.shortDescription = LocHelper.GetLocalizedItem("item_heating_plate_desc");
            machine.flavorText = "";
            machine.unitValue = 250L;
            machine.EnableTag("CONTAINER_TAG");
            machine.SetGameItemType("MACHINE");
            AudioHelper.InitMetalMachine(machine);
            machine.ModifyTag("CURRENT_TARGET_TEMP", delegate (TagState state) { state.Enable().SetInt(99); });

            modes.group.onChanged = delegate (int index)
            {
                machine.ModifyTag("HEATING_PLATE_STATE", delegate (TagState state) { state.Enable().SetInt(index); });
            };
            int state = machine.GetTagReadonly("HEATING_PLATE_STATE").GetInt();
            modes.Select((state == 1 || state == 2) ? state : 0);

            machine.onOneSecondItemFunc = delegate
            {
                int stateNow = machine.GetTagReadonly("HEATING_PLATE_STATE").GetInt();
                if (stateNow >= 0 && stateNow <= 2 && modes.selected != stateNow) modes.Select(stateNow);
                int modules = MachineBlender.GetUpgradeCount(machine);
                if (modules < 2)
                {
                    minus5.Hide(); plus5.Hide(); minus1.Hide(); plus1.Hide();
                }
                else
                {
                    minus5.Show(); plus5.Show(); minus1.Show(); plus1.Show();
                }
                int fixedTarget = modules switch { 0 => 99, 1 => 160, _ => -1 };
                if (fixedTarget >= 0 && machine.GetTagReadonly("CURRENT_TARGET_TEMP").GetInt() != fixedTarget)
                {
                    machine.ModifyTag("CURRENT_TARGET_TEMP", delegate (TagState s) { s.Enable().SetInt(fixedTarget); }, skipValidate: true);
                }
                tempControl.SetText(MetaHelper.FormatTemperature(machine.GetTagReadonly("CURRENT_TARGET_TEMP").GetInt()));
                if (containerSlot.childItem != null && window.IsVisible())
                    tempLabel.SetText(MetaHelper.FormatTemperature((int)containerSlot.childItem.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble()));
                else
                    tempLabel.SetText(LocHelper.GetLocalizedMechanic("mech_heating_plate_temp_na"));
                if (stateNow != 0 && containerSlot.childItem != null && !WaterHelper.IsEmpty(containerSlot.childItem))
                    MachineHeatingPlate.HandleHeating(containerSlot, stateNow, machine);
            };

            minus5.SetCallback(delegate { Adjust(machine, -5, tempControl); });
            plus5.SetCallback(delegate { Adjust(machine, 5, tempControl); });
            minus1.SetCallback(delegate { Adjust(machine, -1, tempControl); });
            plus1.SetCallback(delegate { Adjust(machine, 1, tempControl); });
            return machine;
        }

        private static void Adjust(GameItem machine, int delta, MachineryText tempControl)
        {
            int max = MachineBlender.GetUpgradeCount(machine) == 0 ? 99 : 160;
            int target = Math.Max(26, Math.Min(max, machine.GetTagReadonly("CURRENT_TARGET_TEMP").GetInt() + delta));
            machine.ModifyTag("CURRENT_TARGET_TEMP", delegate (TagState s) { s.Enable().SetInt(target); });
            tempControl.SetText(MetaHelper.FormatTemperature(target));
        }

        private static void SetupWaterSlot(GameSlotInventory waterSlot)
        {
            waterSlot.mayInventoryAddItemFunc = (GameItem item, GameInventory _) =>
                GeneralHelper.IsItemOwned(item) && item.IsTag("LIQUID_CONTAINER_TAG");
            waterSlot.SetBackgroundFadeSprite("Items/water_container4", "xl");
        }
    }
}
