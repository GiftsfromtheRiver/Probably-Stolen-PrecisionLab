using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Weekly medical-item draw for vending machines.
    ///
    /// User rules:
    /// - one shared weekly draw per store (not per machine);
    /// - only medical items already inside vending machines are considered;
    /// - price buckets: &lt;= 1000 credits 80%, &gt; 1000 credits 20%, when both exist;
    /// - within the selected bucket one item is chosen uniformly;
    /// - if the cheapest medical item in stock is above blue blood (250 credits), the normal
    ///   weekly draw has a 50% no-sale chance; during a medical shortage the no-sale chance is 25%;
    /// - during a medical shortage there is also one extra nightly draw (also 25% no-sale when the
    ///   cheapest medical item is above blue blood).
    /// </summary>
    [HarmonyPatch(typeof(VendingMachineHelper), "HandleNight")]
    internal static class VendingMedicalWeeklyPatch
    {
        private const string ModId = Loc.ModId;
        private const string LastWeekKey = "vending_medical_last_day";
        private const string LastShortageKey = "vending_medical_shortage_day";
        private const int BlueBloodPrice = 250;
        private const int BucketPivot = 1000;
        private static int _processedDay = -1;

        private static void Postfix(GameItem vendingMachine, GameInventory vendingInventory)
        {
            try
            {
                if (vendingMachine == null) return;
                int day = StoreStation.GetDayCounter();
                if (_processedDay == day) return;

                // First eligible vending machine this night performs the shared draw for all machines.
                if (!HasAnyPossibleMedical()) return;
                _processedDay = day;

                bool shortage = IsMedicalShortage();
                int lastWeek = ModHelper.GetModDataInt(ModId, LastWeekKey, -1);
                if (lastWeek < 0)
                {
                    // Start the seven-day clock without a free sale on the first night.
                    ModHelper.SetModDataInt(ModId, LastWeekKey, day);
                }
                else if (day - lastWeek >= 7)
                {
                    TryDraw(shortage ? 0.25 : 0.50);
                    ModHelper.SetModDataInt(ModId, LastWeekKey, day);
                }

                if (shortage)
                {
                    int lastShortage = ModHelper.GetModDataInt(ModId, LastShortageKey, -1);
                    if (lastShortage != day)
                    {
                        TryDraw(0.25);
                        ModHelper.SetModDataInt(ModId, LastShortageKey, day);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] vending medical draw failed: " + e.Message);
            }
        }

        private static bool IsMedicalShortage()
        {
            try
            {
                return StoreEventManager.GetShortageType("MEDICAL") > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private sealed class Candidate
        {
            public GameItem Machine;
            public GameItem Item;
            public int Price;
        }

        private static List<Candidate> Gather()
        {
            List<Candidate> list = new List<Candidate>();
            try
            {
                EmporiumEntry store = EmporiumEntry.Instance;
                if (store == null || store.vendingMachineElement == null) return list;
                foreach (GameItem machine in store.vendingMachineElement.childItems)
                {
                    if (machine == null || machine.identifier != "vending_machine") continue;
                    GameGridInventory inv = VendingMachineHelper.GetVendingInventory(machine);
                    if (inv == null) continue;
                    foreach (GameItem item in inv.childItems)
                    {
                        if (item == null) continue;
                        if (!(item.IsTag("ITEM_MEDICAL_TAG") || item.IsGameItemType("MEDICAL"))) continue;
                        int price = VendingMachineHelper.GetVendingPrice(item);
                        if (price <= 0) continue;
                        list.Add(new Candidate { Machine = machine, Item = item, Price = price });
                    }
                }
            }
            catch (Exception)
            {
                // Empty list means no draw this night.
            }
            return list;
        }

        private static bool HasAnyPossibleMedical()
        {
            return Gather().Count > 0;
        }

        /// <summary>Returns true when something was actually sold.</summary>
        private static bool TryDraw(double noSaleChanceWhenAboveBlueBlood)
        {
            List<Candidate> candidates = Gather();
            if (candidates.Count == 0) return false;

            int minPrice = candidates.Min(c => c.Price);
            if (minPrice > BlueBloodPrice && UnityEngine.Random.value < noSaleChanceWhenAboveBlueBlood)
            {
                return false; // this week's/night's draw produced no sale
            }

            List<Candidate> cheap = candidates.Where(c => c.Price <= BucketPivot).ToList();
            List<Candidate> expensive = candidates.Where(c => c.Price > BucketPivot).ToList();
            List<Candidate> chosenBucket;
            if (cheap.Count > 0 && expensive.Count > 0)
            {
                chosenBucket = UnityEngine.Random.value < 0.80f ? cheap : expensive;
            }
            else if (cheap.Count > 0)
            {
                chosenBucket = cheap;
            }
            else
            {
                chosenBucket = expensive;
            }

            Candidate chosen = chosenBucket[UnityEngine.Random.Range(0, chosenBucket.Count)];
            try
            {
                int value = (int)chosen.Item.GetCurrentValue(
                    useRetailMarkup: true, withChild: true, includeEvents: false,
                    forceMarkup: true, 0L);
                chosen.Item.AddItemFeature(ItemFeatureList.VendingMachineBonus());
                SoldItem.MarkStolenOrContrabandSale(chosen.Item);
                PlayerStore.Instance.PerformOnSoldCheck(chosen.Item, null);
                chosen.Item.Destroy();
                int cash = chosen.Machine.GetTagReadonly("VENDING_CASH").GetInt();
                chosen.Machine.ModifyTag("VENDING_CASH", delegate (TagState state)
                {
                    state.SetInt(cash + value);
                });
                RunStats.OnVendingSale(value);
                VendingMachineHelper.UpdateSprite(chosen.Machine, "vending");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] vending medical sale failed: " + e.Message);
                return false;
            }
        }
    }
    [HarmonyPatch(typeof(VendingMachineHelper), "CreateTooltip")]
    internal static class VendingMedicalTooltipPatch
    {
        private static void Postfix(RichTextBuilder builder, GameItem item)
        {
            try
            {
                if (builder == null || item == null || item.identifier != "vending_machine_core") return;
                builder.AddLine(Loc.Get("mech.vending.medical_rule",
                    "Weekly medical draw: 80% for items <=1000 credits, 20% for >1000; if the cheapest medical item is >250, 50% chance of no sale."), true, RenderHandler.ColorPalette.LightTurquoise);
                builder.AddLine(Loc.Get("mech.vending.medical_shortage",
                    "Medical shortage: one extra nightly draw; if the cheapest medical item is >250, 75% chance of a sale."), true, RenderHandler.ColorPalette.LightTurquoise);
            }
            catch (Exception) { }
        }
    }
}
