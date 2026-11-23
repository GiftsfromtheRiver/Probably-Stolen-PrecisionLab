using System;
using TMPro;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Centrifuge: insert a mixed-liquid container, choose one liquid component, and drain it
    /// into an output container. Contamination starts at 20% with no chemistry modules and
    /// drops to 0% with four modules (20/15/10/5/0).
    /// </summary>
    internal static class Centrifuge
    {
        internal const string Id = "precision_centrifuge";
        internal const string SelectedTag = "CENTRIFUGE_SELECTED_LIQUID";

        internal static GameItem Create()
        {
            GameGridInventory moduleGrid = new GameGridInventory(4, 4);
            GameSlotInventory inputSlot = new GameSlotInventory();
            GameSlotInventory outputSlot = new GameSlotInventory();
            SetupLiquidSlot(inputSlot);
            SetupLiquidSlot(outputSlot);

            MachineryButton choose = new MachineryButton("选择液体");
            MachineryButton output = new MachineryButton("分离输出");
            MachineryText status = new MachineryText("未选择液体", 7, HorizontalAlignmentOptions.Center, uppercase: false);
            MachineryStack control = MachineryStack.Vertical(choose, status, output);

            PixelWindow window = MachineHelper.CreateMachineryWindow(true,
                MachineHelper.Panel("化学模组", moduleGrid, "module"),
                MachineHelper.Panel("输入容器", inputSlot, "input"),
                MachineHelper.Panel("输出容器", outputSlot, "output"),
                MachineHelper.Panel("控制面板", control, "control"));

            GameItem machine = ItemDirectory.CreateEmptyItem().SetContentWindow(window);
            ModSprites.Apply(machine, "centrifuge", "Items/items_tool2", "chem_finisher");
            machine.SetShape(new GridShapeBuilder(3, 3).SetDataFill(1).Build());
            machine.name = Loc.Get("item.precision_centrifuge.name", "离心机");
            machine.shortDescription = Loc.Get("item.precision_centrifuge.desc",
                "分离混合液体：把混合液体容器放入输入槽，选择要分离的液体，再放入空容器输出。基础污染20%，4个化学模组降为0%。");
            machine.flavorText = Loc.Get("item.precision_centrifuge.flavor", "转得够快，杂质就追不上你。");
            machine.unitValue = 800L;
            machine.SetGameItemType("MACHINE");
            machine.EnableTag("CONTAINER_TAG");
            machine.EnableTag("STANDARD_MACHINE_TAG");

            MachineBlender.SetupChemModuleGrid(moduleGrid, machine);
            AudioHelper.InitMetalMachine(machine);

            choose.SetCallback(delegate { CentrifugeUI.Open(machine, inputSlot); });
            output.SetCallback(delegate { TrySeparate(machine, inputSlot, outputSlot); });

            ModHelper.SetSource(machine, Loc.ModId, ModEntry.ModDisplayName);

            machine.onOneSecondItemFunc = delegate
            {
                if (!machine.IsTag(SelectedTag))
                {
                    status.SetText("未选择液体");
                    return;
                }
                string id = machine.GetTagReadonly(SelectedTag).GetString();
                if (string.IsNullOrEmpty(id))
                {
                    status.SetText("未选择液体");
                    return;
                }
                Liquid liquid = Liquid.GetLiquidFromId(id);
                status.SetText((liquid != null ? liquid.displayName : id) + " · 污染 " + GetLoss(machine) + "%");
            };
            return machine;
        }

        internal static void SetSelected(GameItem machine, string liquidId)
        {
            machine.ModifyTag(SelectedTag, delegate (TagState state)
            {
                state.Enable().SetString(liquidId ?? "");
            });
        }

        internal static int GetLoss(GameItem machine)
        {
            return MachineBlender.GetUpgradeCount(machine) switch
            {
                0 => 20,
                1 => 15,
                2 => 10,
                3 => 5,
                _ => 0
            };
        }

        private static void SetupLiquidSlot(GameSlotInventory slot)
        {
            slot.mayInventoryAddItemFunc = (GameItem item, GameInventory _) =>
                GeneralHelper.IsItemOwned(item) && item.IsTag("LIQUID_CONTAINER_TAG");
            slot.SetBackgroundFadeSprite("Items/water_container4", "xl");
        }

        private static void TrySeparate(GameItem machine, GameSlotInventory inputSlot, GameSlotInventory outputSlot)
        {
            try
            {
                if (!machine.IsTag(SelectedTag))
                {
                    StoreUIManager.Instance.Notify("请先用“选择液体”选择要分离的成分。", "default");
                    return;
                }
                string id = machine.GetTagReadonly(SelectedTag).GetString();
                if (string.IsNullOrEmpty(id))
                {
                    StoreUIManager.Instance.Notify("请先选择液体。", "default");
                    return;
                }
                GameItem input = inputSlot.childItem;
                GameItem output = outputSlot.childItem;
                if (input == null || output == null)
                {
                    StoreUIManager.Instance.Notify("需要同时放入输入容器和输出容器。", "default");
                    return;
                }
                int amount = LiquidTools.GetLiquidAmount(input, id);
                if (amount <= 0)
                {
                    StoreUIManager.Instance.Notify("输入容器中没有所选液体。", "default");
                    return;
                }
                int free = WaterHelper.GetTotalCapacity(output) - WaterHelper.GetTotalVolume(output);
                if (free < amount)
                {
                    StoreUIManager.Instance.Notify("输出容器容量不足。", "default");
                    return;
                }
                string part = Liquid.GetCurrentPartFromId(id);
                if (part == null) return;
                int next = Math.Max(0, input.GetTagReadonly(part).GetInt() - amount);
                input.ModifyTag(part, delegate (TagState state) { state.Enable().SetInt(next); }, skipValidate: true);
                WaterFeatureHelper.OnLiquidContainerPourOutOf(input);
                WaterHelper.AddLiquid(output, id, amount);
                int loss = GetLoss(machine);
                if (loss > 0)
                {
                    int total = WaterHelper.GetTotalVolume(output);
                    int contaminated = Mathf.RoundToInt(total * (loss / 100f));
                    if (contaminated > 0)
                    {
                        WaterHelper.Remove(output, contaminated);
                        WaterHelper.AddLiquid(output, "physical_contaminant", contaminated);
                    }
                }
                StoreUIManager.Instance.Notify("已分离出 " + amount + "µL（污染 " + GetLoss(machine) + "%）。", "default");
                Debug.Log("[PrecisionLab] centrifuge separated " + id + " x" + amount + "uL loss=" + GetLoss(machine));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] centrifuge separation failed: " + e.Message);
            }
        }
    }
}
