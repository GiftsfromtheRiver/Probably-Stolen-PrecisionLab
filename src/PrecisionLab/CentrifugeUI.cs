using System;
using UnityEngine;

namespace PrecisionLab
{
    internal static class CentrifugeUI
    {
        private const string WindowId = "precision_lab:centrifuge_select";

        internal static void Open(GameItem machine, GameSlotInventory inputSlot)
        {
            CustomUIManager manager = CustomUIManager.Instance;
            if (manager == null) return;
            try
            {
                manager.CloseWindow(WindowId);
                CustomUIBuilder builder = manager.CreateWindow(WindowId, "离心机 · 选择分离液体",
                    StoreUIManager.Instance != null ? CustomUIManager.LAYER_OVERLAY : CustomUIManager.LAYER_MENU,
                    null, CustomUIManager.Style.Meta);
                builder.SetSize(620f, 560f).SetDraggable(true).SetCloseOnEscape();
                builder.BeginScrollFlexible();
                builder.AddHeader("选择要输出的液体");
                builder.AddLabel("输入容器：混合液体；输出容器：接收单一液体。4个化学模组时污染0%。");
                builder.AddSeparator(null, 2);

                GameItem input = inputSlot != null ? inputSlot.childItem : null;
                int shown = 0;
                if (input != null)
                {
                    foreach (Liquid liquid in Liquid.Liquids)
                    {
                        if (liquid == null || string.IsNullOrEmpty(liquid.identifier)) continue;
                        int amount = LiquidTools.GetLiquidAmount(input, liquid.identifier);
                        if (amount <= 0) continue;
                        shown++;
                        string id = liquid.identifier;
                        builder.AddButton(liquid.displayName + "  " + LiquidTools.FormatUl(amount),
                            delegate
                            {
                                Centrifuge.SetSelected(machine, id);
                                manager.CloseWindow(WindowId);
                            });
                    }
                }
                if (shown == 0)
                {
                    builder.AddLabel("输入容器为空或没有可分离液体。");
                }
                builder.End();
                builder.AddSeparator(null, 2);
                builder.AddButton("清除选择", delegate
                {
                    Centrifuge.SetSelected(machine, "");
                    manager.CloseWindow(WindowId);
                });
                builder.AddButton("关闭", delegate { manager.CloseWindow(WindowId); });
                CustomUIWindow window = builder.Show();
                if (window != null) window.Center();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] centrifuge selector failed: " + e.Message);
            }
        }
    }
}
