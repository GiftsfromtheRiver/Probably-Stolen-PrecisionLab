using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>Call-based apothecary pre-order page: pick up to 3 drugs/kits, pay a
    /// lock fee, and the next apothecary visit guarantees those items.</summary>
    internal static class ApothecaryOrderUI
    {
        internal const string WindowId = "precision_lab:apothecary_order";
        internal const int MaxPicks = 3;
        internal const int LockFee = 150;
        internal static StorePhoneClient PendingClient;

        private sealed class Entry
        {
            public string Id;
            public string Name;
        }

        private static readonly Entry[] Catalogue =
        {
            E("adrenaline_supply_box", "肾上腺素药剂包"),
            E("resolving_agent_refill", "拆分剂补充装（2×20mL）"),
            E("belladonna_supply_box", "颠茄药剂包"),
            E("antidepressant_supply_box", "抗抑郁药剂包"),
            E("amiodarone_supply_box", "胺碘酮药剂包"),
            E("vasopressin_supply_box", "加压素药剂包"),
            E("vasodilator_supply_box", "舒张素药剂包"),
            E("coagulant_supply_box", "促凝剂药剂包"),
            E("anticoagulant_supply_box", "抗凝剂药剂包"),
            E("smelling_salt_supply_box", "嗅盐药剂包"),
            E("anti_radiation_supply_box", "抗辐射药剂包"),
            E("multivitamin_bottle", "复合维生素药片瓶"),
            E("nutrition_supply_box", "三合一营养药剂包"),
            E("nutrition_iv_bag", "三合一营养注射液"),

            E("naloxone_supply_box", "纳洛酮药剂包"),
            E("birth_control_supply_box", "避孕药药剂包"),
            E("sannai_supply_box", "山奈德药剂包"),
            E("synaprest_supply_box", "赛纳普斯药剂包"),
            E("immunivax_supply_box", "免疫宁药剂包"),
            E("blue_blood_supply_box", "蓝血药剂包"),
            E("biopharma_supply_box", "生物制药药剂包"),
            E("nightingale_supply_box", "南丁格尔药剂包"),
            E("star_quiler_first_aid_box", "完整急救盒"),
            E("adrenaline_pen", "肾上腺素注射笔"),
            E("pure_white_injector", "纳洛通注射器"),
            E("large_purple_injector", "免疫宁注射器"),
            E("black_injector", "赛纳普斯注射器"),
            E("birth_control_pill", "避孕药"),
            E("zyanide_pill", "山奈德片"),
            E("belladonna_injector", "颠茄注射器"),
            E("antidepressant_pill", "抗抑郁药片"),
            E("amiodarone_pill", "胺碘酮片"),
            E("vasopressin_injector", "血管加压素注射器"),
            E("vasodilator_injector", "血管舒张素注射器"),
            E("coagulant_dressing", "促凝剂瓶"),
            E("anticoagulant_injector", "抗凝剂瓶")
        };

        private static Entry E(string id, string fallback)
        {
            return new Entry { Id = id, Name = Loc.Get("item." + id + ".order_name", fallback) };
        }

        internal static void Open(StorePhoneClient client)
        {
            PendingClient = client;
            CustomUIManager manager = CustomUIManager.Instance;
            if (manager == null)
            {
                StoreUIManager.Instance.Notify(Loc.Get("ui.apothecary_order.no_ui", "订购页面不可用。"), "default");
                return;
            }
            try
            {
                manager.CloseWindow(WindowId);
                var picked = new HashSet<string>();
                CustomUIWindow window = null;
                CustomUIBuilder b = manager.CreateWindow(WindowId,
                    Loc.Get("ui.apothecary_order.title", "药剂师 · 定向采购"),
                    StoreUIManager.Instance != null ? CustomUIManager.LAYER_OVERLAY : CustomUIManager.LAYER_MENU,
                    null, CustomUIManager.Style.Meta);
                b.SetSize(760f, 600f).SetDraggable(true).SetCloseOnEscape();
                b.BeginScrollFlexible();
                b.AddHeader(Loc.Get("ui.apothecary_order.header", "定向采购 / Standing Order"));
                b.AddLabel(Loc.Fmt("ui.apothecary_order.hint",
                    "最多锁定 3 件。锁定费 {0} 信用点；第二天药剂师到店时必定带上所选物品，其余正常补位。", LockFee));
                b.AddSeparator(null, 2);

                foreach (Entry entry in Catalogue)
                {
                    if (!DirectoryMaster.Has<GameItem>(entry.Id)) continue;
                    string id = entry.Id;
                    b.BeginRow(4f, TextAnchor.MiddleLeft);
                    b.AddLabel(entry.Name);
                    b.AddFlexibleSpace();
                    b.AddToggle("", false, delegate (bool on)
                    {
                        if (on)
                        {
                            if (picked.Count >= MaxPicks)
                            {
                                if (window != null)
                                {
                                    CustomUIElement row = window.Get("row_" + id);
                                    if (row != null) row.SetBool(false, false);
                                }
                                StoreUIManager.Instance.Notify(Loc.Get("ui.apothecary_order.max", "最多只能锁定 3 件。"), "default");
                                return;
                            }
                            picked.Add(id);
                        }
                        else
                        {
                            picked.Remove(id);
                        }
                        if (window != null)
                        {
                            CustomUIElement count = window.Get("picked_count");
                            if (count != null) count.SetText(picked.Count + " / " + MaxPicks);
                        }
                    }, "row_" + id);
                    b.End();
                }
                b.End();

                b.AddSeparator(null, 2);
                b.BeginRow(8f, TextAnchor.MiddleLeft);
                b.AddLabel(Loc.Get("ui.apothecary_order.picked", "已选") + " 0 / " + MaxPicks, "picked_count");
                b.AddFlexibleSpace();
                b.AddLabel(Loc.Fmt("ui.apothecary_order.fee", "锁定费：{0}", LockFee));
                b.End();

                b.AddSeparator(null, 2);
                b.BeginRow(12f, TextAnchor.MiddleCenter);
                b.AddFlexibleSpace();
                b.AddButton(Loc.Get("ui.apothecary_order.confirm", "确认锁定并预约"), delegate
                {
                    Confirm(picked);
                }, "confirm");
                b.Last.SetFixedWidth(220f);
                b.AddSpace(12f);
                b.AddButton(Loc.Get("ui.apothecary_order.book_only", "仅预约"), delegate
                {
                    Apothecary.BookAppointment(PendingClient);
                    Close();
                }, "book_only");
                b.Last.SetFixedWidth(140f);
                b.AddSpace(12f);
                b.AddButton(Loc.Get("ui.apothecary_order.close", "关闭"), Close, "close");
                b.Last.SetFixedWidth(120f);
                b.AddFlexibleSpace();
                b.End();
                b.AddLabel(Loc.Get("ui.apothecary_order.note", "锁定后第二天到店取货，费用不退；物品按正常价格购买。"));
                window = b.Show();
                if (window != null) window.Center();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] apothecary order UI failed: " + e.Message);
                StoreUIManager.Instance.Notify(Loc.Get("ui.apothecary_order.error", "订购页面打开失败。"), "default");
            }
        }

        private static void Confirm(HashSet<string> picked)
        {
            try
            {
                if (picked == null || picked.Count == 0)
                {
                    StoreUIManager.Instance.Notify(Loc.Get("ui.apothecary_order.none", "请至少选择 1 件。"), "default");
                    return;
                }
                PlayerStore store = PlayerStore.Instance;
                if (store == null || store.playerCash < LockFee)
                {
                    StoreUIManager.Instance.Notify(Loc.Get("ui.apothecary_order.no_cash", "信用点不足，无法支付锁定费。"), "default");
                    return;
                }
                List<string> ids = new List<string>(picked);
                if (!Apothecary.PlaceOrder(PendingClient, ids))
                {
                    StoreUIManager.Instance.Notify(Loc.Get("ui.apothecary_order.fail", "预约失败，请稍后再试。"), "default");
                    return;
                }
                store.playerCash -= LockFee;
                StoreUIManager.Instance.Notify(Loc.Get("ui.apothecary_order.done", "已锁定，明天药剂师会带来所选物品。"), "default");
                Close();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] apothecary order confirm failed: " + e.Message);
            }
        }

        private static void Close()
        {
            CustomUIManager.Instance?.CloseWindow(WindowId);
        }
    }
}
