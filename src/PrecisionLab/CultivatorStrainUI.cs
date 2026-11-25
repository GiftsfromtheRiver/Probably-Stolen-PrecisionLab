using System;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>Selector for the cultivator's antibiotic flora.</summary>
    internal static class CultivatorStrainUI
    {
        private const string WindowId = "precision_lab:cultivator_strain";

        /// <summary>
        /// Save-loaded machine windows keep the factory-created button callback, so the captured
        /// GameItem can be a stale template. Resolve the live item from the clicked button's
        /// parent window instead.
        /// </summary>
        internal static GameItem ResolveMachineFromButton(MachineryButton button, GameItem fallback)
        {
            try
            {
                if (button == null || button.handler == null) return fallback;
                PixelWindow window = button.handler.FindParentWindow();
                if (window == null) return fallback;
                if (window.parentItems != null)
                {
                    GameItem first = null;
                    foreach (GameItem item in window.parentItems)
                    {
                        if (item == null || item.contentWindow != window
                            || item.identifier != "precision_microbe_cultivator") continue;
                        if (item.parentInventory != null) return item;
                        if (first == null) first = item;
                    }
                    if (first != null) return first;
                }
                // Fallback: find the live machine by content window identity in the store inventory.
                foreach (GameItem item in EmporiumEntry.Instance.vendingMachineElement.childItems)
                {
                    if (item != null && item.identifier == "precision_microbe_cultivator" && item.contentWindow == window)
                        return item;
                }
            }
            catch (Exception) { }
            return fallback;
        }

        /// <summary>
        /// Rebind every loaded cultivator's strain button to its live GameItem. Save decoding keeps
        /// the factory-created callback closure (pointing at a template item), so this must run
        /// after load and whenever the store wakes up.
        /// </summary>
        internal static void RebindAll()
        {
            try
            {
                if (!PlayerStore.HasInstance || PlayerStore.Instance == null) return;
                var items = PlayerStore.Instance.FindAllItem();
                if (items == null) return;
                int bound = 0;
                foreach (GameItem machine in items)
                {
                    if (machine == null || machine.parentInventory == null) continue;
                    bound += RebindMachine(machine);
                }
                Debug.Log("[PrecisionLab] rebound " + bound + " cultivator strain button(s)");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] cultivator strain rebind failed: " + e.Message);
            }
        }

        /// <summary>Rebind one cultivator's live strain button; returns 1 when a button was found.</summary>
        internal static int RebindMachine(GameItem machine)
        {
            try
            {
                if (machine == null || machine.identifier != "precision_microbe_cultivator") return 0;
                Debug.Log("[PrecisionLab] RebindMachine id=" + machine.identifier + " window=" + (machine.contentWindow != null) + " child=" + (machine.contentWindow?.childElement?.GetType().Name ?? "null"));
                if (!(machine.contentWindow?.childElement is MachineryPanelRow row)) return 0;
                foreach (MachineryPanel panel in row.Panels)
                {
                    if (panel == null || panel.role != "strain") continue;
                    // Replace the whole button: save-decoded windows can keep a callback to a
                    // factory template even after SetCallback, so a fresh button is safer.
                    GameItem live = machine;
                    MachineryButton fresh = new MachineryButton(
                        Loc.Get("ui.cultivator.strain_button", "选择菌群"));
                    fresh.SetCallback(delegate { CultivatorStrainUI.Open(live, null, fresh); });
                    panel.SetContent(fresh);
                    Debug.Log("[PrecisionLab] RebindMachine replaced strain button for " + machine.GetUniqueID());
                    return 1;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] cultivator strain button replace failed: " + e.Message);
            }
            return 0;
        }

        internal static void Open(GameItem machine, MachineryText cultureText, MachineryButton strainButton = null)
        {
            CustomUIManager manager = CustomUIManager.Instance;
            if (manager == null || machine == null) return;
            try
            {
                manager.CloseWindow(WindowId);
                CustomUIBuilder builder = manager.CreateWindow(WindowId,
                    Loc.Get("ui.cultivator.strain_window", "选择培养菌群"),
                    StoreUIManager.Instance != null ? CustomUIManager.LAYER_OVERLAY : CustomUIManager.LAYER_MENU,
                    null, CustomUIManager.Style.Meta);
                builder.SetSize(600f, 520f).SetDraggable(true).SetCloseOnEscape();
                builder.BeginScrollFlexible();
                builder.AddHeader(Loc.Get("ui.cultivator.strain_header", "选择要培养的菌群"));
                builder.AddLabel(Loc.Get("ui.cultivator.strain_desc",
                    "不同菌群消耗同样的糖，但产出的抗生素培养液不同。广谱更便宜，窄谱更贵；混合两种以上可压制鸡尾酒抗生素。"));
                builder.AddSeparator(null, 2);

                AddChoice(builder, manager, machine, cultureText, strainButton,
                    AntibioticContent.StrainBroad,
                    Loc.Get("ui.cultivator.strain.broad", "广谱放线菌（便宜）"),
                    Loc.Get("ui.cultivator.strain.broad_desc", "生产广谱抗生素培养液。"));
                AddChoice(builder, manager, machine, cultureText, strainButton,
                    AntibioticContent.StrainGramPos,
                    Loc.Get("ui.cultivator.strain.gram_pos", "革兰阳性菌（窄谱，较贵）"),
                    Loc.Get("ui.cultivator.strain.gram_pos_desc", "生产革兰阳性窄谱抗生素培养液。"));
                AddChoice(builder, manager, machine, cultureText, strainButton,
                    AntibioticContent.StrainGramNeg,
                    Loc.Get("ui.cultivator.strain.gram_neg", "革兰阴性菌（窄谱，较贵）"),
                    Loc.Get("ui.cultivator.strain.gram_neg_desc", "生产革兰阴性窄谱抗生素培养液。"));
                AddChoice(builder, manager, machine, cultureText, strainButton,
                    AntibioticContent.StrainNone,
                    Loc.Get("ui.cultivator.strain.none", "停止培养 / 原版糖消化"),
                    Loc.Get("ui.cultivator.strain.none_desc", "不产抗生素，只保留原来的糖→有机废物功能。"));
                builder.End();
                builder.AddSeparator(null, 2);
                builder.AddButton(Loc.Get("ui.cultivator.strain.close", "关闭"),
                    delegate { manager.CloseWindow(WindowId); });
                CustomUIWindow window = builder.Show();
                if (window != null) window.Center();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] cultivator strain selector failed: " + e.Message);
            }
        }

        private static void AddChoice(CustomUIBuilder builder, CustomUIManager manager, GameItem machine,
            MachineryText cultureText, MachineryButton strainButton, string strain, string title, string description)
        {
            builder.AddLabel(title + " — " + description);
            builder.AddButton(Loc.Get("ui.cultivator.strain.pick", "选择：") + title, delegate
            {
                Debug.Log("[PrecisionLab] strain choice clicked=" + strain + " machine=" + (machine?.GetUniqueID().ToString() ?? "null"));
                SetStrain(machine, strain);
                Debug.Log("[PrecisionLab] strain choice tag after=" + GetStrain(machine));
                ItemFactory.RefreshCulturePanel(machine, cultureText);
                try
                {
                    machine.Validate();
                    if (machine.contentWindow != null) machine.contentWindow.Validate();
                }
                catch (Exception) { }
                manager.CloseWindow(WindowId);
            });
            builder.AddSeparator(null, 1);
        }

        internal static void SetStrain(GameItem machine, string strain)
        {
            if (machine == null) return;
            // Validation copies state -> modifiedState; without it IsTag/GetTagReadonly still
            // return the old value on save-loaded machines.
            machine.ModifyTag(AntibioticContent.StrainTag, delegate (TagState state)
            {
                state.Enable().SetString(string.IsNullOrEmpty(strain) ? AntibioticContent.StrainNone : strain);
            });
        }

        internal static string GetStrain(GameItem machine)
        {
            try
            {
                return machine != null && machine.IsTag(AntibioticContent.StrainTag)
                    ? machine.GetTagReadonly(AntibioticContent.StrainTag).GetString()
                    : AntibioticContent.StrainNone;
            }
            catch (Exception)
            {
                return AntibioticContent.StrainNone;
            }
        }

        internal static string DisplayFor(string strain)
        {
            if (strain == AntibioticContent.StrainBroad) return Loc.Get("ui.cultivator.strain.broad", "广谱放线菌");
            if (strain == AntibioticContent.StrainGramPos) return Loc.Get("ui.cultivator.strain.gram_pos", "革兰阳性菌");
            if (strain == AntibioticContent.StrainGramNeg) return Loc.Get("ui.cultivator.strain.gram_neg", "革兰阴性菌");
            return Loc.Get("ui.cultivator.strain.none", "原版糖消化");
        }
    }

    /// <summary>
    /// Save-loaded machine windows keep the factory button callback (stale GameItem). Rebind the
    /// clicked strain button to the live machine resolved from its own window before OnSelect runs.
    /// </summary>
    [HarmonyPatch(typeof(MachineryButton), "OnSelect")]
    internal static class StrainButtonRebindPatch
    {
        private static void Prefix(MachineryButton __instance)
        {
            try
            {
                if (__instance == null || __instance.handler == null) return;
                MachineryPanel panel = __instance.handler.FindParentElement<MachineryPanel>();
                if (panel == null || panel.role != "strain") return;
                PixelWindow window = __instance.handler.FindParentWindow();
                if (window == null || window.parentItems == null) return;
                GameItem live = null;
                foreach (GameItem item in window.parentItems)
                {
                    if (item == null || item.identifier != "precision_microbe_cultivator" || item.contentWindow != window) continue;
                    if (live == null || item.parentInventory != null) live = item;
                    if (item.parentInventory != null) break;
                }
                if (live == null) return;
                // Suppress the stale factory callback and open the selector for this live machine.
                __instance.SetCallback(delegate { });
                Debug.Log("[PrecisionLab] OnSelect direct live machine " + live.GetUniqueID());
                CultivatorStrainUI.Open(live, null, __instance);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] strain OnSelect direct failed: " + e.Message);
            }
        }
    }
}
