using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Night-time “raw materials workshop” for the apothecary. Reuses Dr. Jackson's
    /// after-hour store stage/panel so no new map prefab is required. Sells the mod's
    /// supply kits, machines and seeds; the daytime phone appointment still delivers the
    /// reserved kits to the apothecary's regular store visit.
    /// </summary>
    internal sealed class ApothecaryWorkshopLocation : AfterhourLocation
    {
        internal const string LocationId = "precision_apothecary_workshop";
        private static bool _registered;

        internal static void EnsureRegistered()
        {
            if (_registered) return;
            _registered = true;
            try
            {
                AfterhourLocation.Register(new ApothecaryWorkshopLocation());
                Debug.Log("[PrecisionLab] apothecary workshop afterhour location registered");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] registering apothecary workshop failed: " + e.Message);
            }
        }

        public override string Id => LocationId;
        public override string Source => Loc.ModId;
        // Reuse Jackson's map stage/panel/photo and ambience.
        public override string ImageKey => ApothecaryWorkshopImagePatch.CardKey;
        public override string BackgroundKey => ApothecaryWorkshopBackgroundPatch.BackgroundKey;
        public override string AmbienceId => "dr_jackson";
        public override bool ShowsStoreCounters => true;
        public override GameObject StageObject => null;

        public override GameObject PanelObject =>
            MapUIManager.Instance != null ? MapUIManager.Instance.UIUpgradeMerchantPanel : null;


        private static IEnumerator ForceJacksonAnchors()
        {
            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
                try
                {
                    EmporiumEntry.Instance.frontInvWindow.rectTransform.anchoredPosition = new Vector2(706.5f, -50f);
                    EmporiumEntry.Instance.backInvWindow.rectTransform.anchoredPosition = new Vector2(317f, -275f);
                }
                catch (Exception) { yield break; }
            }
        }

        public override string GetTitle()
        {
            return Loc.Get("location.precision_apothecary_workshop", "药剂师原料工坊 / Apothecary Workshop");
        }

        public override bool IsDiscovered()
        {
            try
            {
                if (!PlayerStore.HasInstance || PlayerStore.Instance == null) return false;
                if (ModEntry.IsUpgradeUnlocked(Apothecary.UpgradeId)) return true;
                // Also show the workshop once the apothecary phone contact exists (older saves
                // may have the contact without the upgrade dictionary entry).
                return PlayerStore.Instance.PhoneClientDict != null
                    && PlayerStore.Instance.PhoneClientDict.ContainsKey(Apothecary.PhoneNumber);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public override string GetLeaveWarning()
        {
            // Use the vanilla bottom-right leave button without an extra confirmation popup;
            // the Leave() handler still returns owned/left items to the back inventory.
            return "";
        }

        public override Vector2? GetFrontCounterPosition(int w, int h)
        {
            return new Vector2(w / 100f * 78.5f, -(h / 100f * 10f));
        }

        public override Vector2? GetBackCounterPosition(int w, int h)
        {
            return new Vector2(w / 2f - w / 100f * 18f, -(h / 100f * 55f));
        }

        public override void Arrive()
        {
            try
            {
                MapUIManager ui = MapUIManager.Instance;
                if (ui == null) return;
                PlayerStore.Instance.playerLocation = PlayerStore.PlayerLocation.INVENTOR;
                EmporiumEntry.Instance.frontInvWindow.Show();
                EmporiumEntry.Instance.backInvWindow.Show();
                ui.TransferBackInvToCounter();
                EmporiumEntry.Instance.ExpandFrontCounterGrid();
                PlaceWorkshopStock();
                ui.ActivateClientOutsideStore();
                StoreUIManager.Instance.SetClientPositionToDrJackson();
                StoreUIManager.Instance.storeCreditDisplay.creditDisplayText.gameObject.SetActive(true);
                ui.SetCreditDisplayPositionToDrJackson();
                // Seedream clipboard is slightly different from Jackson's: center the amount
                // over the paper's red button instead of leaving it on the paper edge.
                StoreUIManager.Instance.storeCreditDisplay.creditDisplayText.rectTransform.anchoredPosition = new Vector2(-482f, 262f);
                // Align the sale windows with Dr. Jackson's exact layout after the arrival
                // layout pass, so stock and window anchors match the vanilla location.
                try { ui.StartCoroutine(ForceJacksonAnchors()); } catch (Exception) { }
                PlayerStore.Instance.daySinceLastBrowseUpgrade = 0;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] apothecary workshop arrive failed: " + e.Message);
            }
        }

        public override void Leave()
        {
            try
            {
                MapUIManager ui = MapUIManager.Instance;
                if (ui == null) return;
                NegociationUIManager.Instance.CloseUI();
                List<GameItem> owned = new List<GameItem>();
                EmporiumEntry store = EmporiumEntry.Instance;
                GameGridInventory[] counters = { store.frontInvinvElement, store.backInvinvElement };
                for (int i = 0; i < counters.Length; i++)
                {
                    foreach (GameItem item in counters[i].childItems)
                    {
                        if (GeneralHelper.IsItemOwned(item))
                        {
                            item.CloseContentWindow(includeChildren: true);
                            counters[i].Expel(item);
                            owned.Add(item);
                        }
                    }
                }
                store.ResetFrontCounterGrid();
                store.ClearFrontInv();
                store.ClearBackInv();
                ui.TransferCounterToBackInv();
                for (int i = 0; i < owned.Count; i++)
                {
                    GraphUtils.EmporiumTryAdd(store.backInvWindow, owned[i]);
                }
                foreach (GameItem item in store.afterhourInventory.childItems)
                {
                    GraphUtils.EmporiumTryAdd(store.backInvWindow, item);
                }
                ui.ResetCreditDisplayPosition();
                ui.DeactivateClientOutsideStore();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] apothecary workshop leave failed: " + e.Message);
            }
        }


        private static readonly string[] KitPool =
        {
            "adrenaline_supply_box", "naloxone_supply_box", "birth_control_supply_box",
            "sannai_supply_box", "synaprest_supply_box", "immunivax_supply_box",
            "blue_blood_supply_box", "biopharma_supply_box", "nightingale_supply_box",
            "belladonna_supply_box", "antidepressant_supply_box", "amiodarone_supply_box",
            "vasopressin_supply_box", "vasodilator_supply_box", "coagulant_supply_box",
            "anticoagulant_supply_box", "smelling_salt_supply_box"
        };

        private static readonly string[] MachinePool =
        {
            "precision_centrifuge", "precision_purifier", "precision_microbe_cultivator", "precision_liquid_extractor", "hydroponic"
        };

        private static readonly string[] ModulePool = { "chem_module" };

        private static readonly string[] SeedPool =
        {
            "nightingale_aloe_seed", "belladonna_seed", "nutrifruit_seed", "kotton_seed", "fillerweed_seed"
        };

        private static readonly string[] RawPool =
        {
            "modified_aloe_leaf", "belladonna_herb", "medical_honey_bottle",
            "animal_gland_powder", "aromatic_compound", "potassium_iodide_crystal", "rubbing_alcohol"
        };

        private static void PlaceWorkshopStock()
        {
            try
            {
                // Make sure the expanded sale counter exists even when stock is placed directly
                // (e.g. by a debug/QA path) rather than through Arrive().
                try { EmporiumEntry.Instance.ExpandFrontCounterGrid(); } catch (Exception) { }
                Debug.Log("[PrecisionLab] workshop stock placement start");
                int weekday = StoreStation.GetDayCounter() % 7;
                bool friday = weekday == 5; // 0=Sunday, 1=Monday, 5=Friday
                Place("ammonia_water");
                for (int i = 0; i < KitPool.Length; i++) Place(KitPool[i]);
                if (friday)
                {
                    for (int i = 0; i < MachinePool.Length; i++) Place(MachinePool[i]);
                    for (int i = 0; i < SeedPool.Length; i++) Place(SeedPool[i]);
                    for (int i = 0; i < RawPool.Length; i++) Place(RawPool[i]);
                    for (int i = 0; i < ModulePool.Length; i++) Place(ModulePool[i]);
                }
                else
                {
                    Pick(MachinePool, 3, 3);
                    Pick(SeedPool, 2, 3);
                    Pick(RawPool, 1, 2);
                    Pick(ModulePool, 1, 2);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] apothecary workshop stock failed: " + e.Message);
            }
        }

        private static bool Place(string id)
        {
            if (string.IsNullOrEmpty(id) || !DirectoryMaster.Has<GameItem>(id)) return false;
            GameItem item = DirectoryMaster.Item(id);
            if (item == null) return false;
            int weekday = StoreStation.GetDayCounter() % 7;
            if (weekday == 1) item.unitValue = (long)Math.Round(item.unitValue * 0.9);
            else if (weekday == 5) item.unitValue = (long)Math.Round(item.unitValue * 1.5);
            GeneralHelper.SetItemOwned(item, false);
            EmporiumEntry store = EmporiumEntry.Instance;
            if (store == null) return false;
            // Use the vanilla direct-selling path so the item is registered as a purchasable
            // counter offer (selection/price tags work); plain TryAcceptAll only moves the grid item.
            if (store.frontInvinvElement.CanTakeAnything())
            {
                if (PlayerStore.Instance != null && PlayerStore.Instance.TryAddDirectSellingItemToTable(item, false))
                {
                    return true;
                }
            }
            if (GraphUtils.TryAcceptAll(store.frontInvinvElement, item) == 0) return true;
            if (GraphUtils.TryAcceptAll(store.backInvinvElement, item) == 0)
            {
                Debug.LogWarning("[PrecisionLab] workshop stock overflowed to back inventory: " + id);
                return true;
            }
            Debug.LogWarning("[PrecisionLab] workshop stock could not be placed: " + id);
            return false;
        }

        private static void Pick(string[] pool, int min, int max)
        {
            if (pool == null || pool.Length == 0) return;
            int count = UnityEngine.Random.Range(min, max + 1);
            List<string> copy = new List<string>(pool);
            for (int i = 0; i < copy.Count; i++)
            {
                int j = UnityEngine.Random.Range(i, copy.Count);
                string t = copy[i]; copy[i] = copy[j]; copy[j] = t;
            }
            int placed = 0;
            for (int i = 0; i < copy.Count && placed < count; i++)
            {
                if (Place(copy[i])) placed++;
            }
        }
    }
}
