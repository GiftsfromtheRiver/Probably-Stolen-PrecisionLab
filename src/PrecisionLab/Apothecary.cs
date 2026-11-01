using System;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// The Apothecary ("药剂师"): a new network unlock that adds a phone contact using the retired
    /// chemist's sprite. Calling the contact books an appointment with a 3-day cooldown; the
    /// apothecary then arrives on the next day as the FIRST client in the customer queue and sells
    /// every medicine / precursor kit added by this mod.
    /// </summary>
    internal static class Apothecary
    {
        internal const string UpgradeId = "PRECISION_APOTHECARY";
        internal const string ClientId = "precision_apothecary";
        internal const string DialogFuncId = "PrecisionApothecary";
        internal const long PhoneNumber = 7742L;
        internal const int AppointmentCooldownDays = 3;
        internal const int UnlockCost = 1800;

        internal static string DisplayName
        {
            get { return Loc.Get("client.apothecary.name", "药剂师"); }
        }

        // ------------------------------------------------------------------
        // Registration (called from ModEntry.Init / game load)
        // ------------------------------------------------------------------

        internal static void Register()
        {
            try
            {
                if (!StoreClientListDict.storeClientDict.ContainsKey(ClientId))
                {
                    StoreClientListDict.storeClientDict[ClientId] = CreateClient;
                }
                if (!PhoneClientList.DialogFuncs.ContainsKey(DialogFuncId))
                {
                    PhoneClientList.DialogFuncs[DialogFuncId] = BuildCallDialog;
                }
                NetworkUpgrade.RegisterModUpgrade(new NetworkUpgrade(UpgradeId)
                {
                    subtitle = "Referral",
                    cost = UnlockCost,
                    state = 1,
                    source = Loc.ModId,
                    recommended = true,
                    titleProvider = delegate { return Loc.Get("network.PRECISION_APOTHECARY.title", "药剂师 / Apothecary"); },
                    descriptionProvider = delegate
                    {
                        return Loc.Get(
                            "network.PRECISION_APOTHECARY.desc",
                            "解锁药剂师电话。打电话预约后，药剂师会在第二天第一个到店，出售本 mod 新增的药剂与原料盒；预约冷却 3 天。");
                    }
                }, OnUnlocked);
                Debug.Log("[PrecisionLab] apothecary unlock + phone contact registered");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] registering the apothecary failed: " + e.Message);
            }
        }

        /// <summary>On game load: if the upgrade is already unlocked, make sure the phone contact exists.</summary>
        internal static void EnsureUnlockedState()
        {
            try
            {
                if (ModEntry.IsUpgradeUnlocked(UpgradeId))
                {
                    EnsurePhoneContact();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] apothecary load hook failed: " + e.Message);
            }
        }

        private static void OnUnlocked()
        {
            EnsurePhoneContact();
            AddNightLog(Loc.Get("mech.apothecary.unlocked",
                "新联系人「药剂师」已加入电话簿：打电话预约，第二天第一个到店（冷却 3 天）。"), "67e8f9");
        }

        internal static void EnsurePhoneContact()
        {
            try
            {
                if (!PlayerStore.HasInstance || PlayerStore.Instance == null || PlayerStore.Instance.PhoneClientDict == null)
                {
                    return;
                }
                StorePhoneClient client;
                if (!PlayerStore.Instance.PhoneClientDict.TryGetValue(PhoneNumber, out client) || client == null)
                {
                    client = new StorePhoneClient
                    {
                        displayName = DisplayName,
                        locID = "precision_apothecary",
                        dialogFuncId = DialogFuncId,
                        phoneClientType = StorePhoneClient.PhoneClientType.Supplier,
                        cooldownDuration = AppointmentCooldownDays,
                        phoneState = StorePhoneClient.PhoneState.Regular,
                        dialedBefore = true
                    };
                    PlayerStore.Instance.PhoneClientDict[PhoneNumber] = client;
                    Debug.Log("[PrecisionLab] apothecary phone contact added (" + PhoneNumber + ")");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] ensuring apothecary phone contact failed: " + e.Message);
            }
        }

        // ------------------------------------------------------------------
        // Phone dialog / appointment
        // ------------------------------------------------------------------

        private static Dialogue BuildCallDialog(StorePhoneClient client)
        {
            string name = DisplayName;
            if (client == null || client.phoneState == StorePhoneClient.PhoneState.Cooldown)
            {
                Dialogue cooldown = new Dialogue();
                int days = client != null ? Math.Max(0, client.currentCooldown) : AppointmentCooldownDays;
                cooldown.SetText(name, string.Format(
                    Loc.Get("phone.apothecary.cooldown", "最近不方便出诊，{0} 天后才能再预约。"), days))
                    .SetEndAction(delegate { PhoneUIManager.Instance.OnPhoneDialogEnd(); });
                return cooldown;
            }

            Dialogue confirm = new Dialogue();
            confirm.SetText(name, Loc.Get("phone.apothecary.confirmed",
                "好，明天开门时我会第一个到店。准备好信用点。"))
                .SetEndAction(delegate { PhoneUIManager.Instance.OnPhoneDialogEnd(); });

            Dialogue cancel = new Dialogue();
            cancel.SetText(name, Loc.Get("phone.apothecary.cancel", "行，想好了再打给我。"))
                .SetEndAction(delegate { PhoneUIManager.Instance.OnPhoneDialogEnd(); });

            Dialogue dialogue = new Dialogue();
            dialogue.SetText(name, Loc.Get("phone.apothecary.regular",
                "我是药剂师。需要我上门吗？我卖的东西比退休化学家更全，但只接受预约。"));
            dialogue.AddChoice(DirectoryMaster.Item("$"),
                Loc.Get("phone.apothecary.book", "预约上门（冷却 3 天）"), "",
                confirm, delegate { BookAppointment(client); });
            dialogue.AddChoice(DirectoryMaster.Item("$"),
                Loc.Get("phone.apothecary.cancel_choice", "改天再说"), "",
                cancel, null);
            return dialogue;
        }

        private static void BookAppointment(StorePhoneClient client)
        {
            try
            {
                if (client == null)
                {
                    return;
                }
                client.dialedBefore = true;
                client.UseService();
                client.stockType = StoreStation.GetDayCounter() + 1;
                AddNightLog(Loc.Get("mech.apothecary.booked",
                    "药剂师预约成功：明天开门时第一个到店。"), "67e8f9");
                Debug.Log("[PrecisionLab] apothecary appointment booked for day " + client.stockType);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] booking the apothecary failed: " + e.Message);
            }
        }

        // ------------------------------------------------------------------
        // Store client
        // ------------------------------------------------------------------

        private static StoreClient CreateClient()
        {
            StoreClient storeClient = new StoreClient();
            storeClient.identifier = ClientId;
            storeClient.displayName = DisplayName;
            storeClient.nonShootable = true;
            storeClient.spriteName = SpriteDict.GetRetiredChemistSpriteName();
            storeClient.clientFaction = "FACTION_LOWER_LEVEL";
            StoreClientFactionSetup.InitLowerLevelCitizen(storeClient);
            storeClient.clientIntent = StoreClient.ClientIntent.SELL;
            storeClient.clientItemFeatureBuying.Add("premiumBuy15");
            storeClient.mainDialogue.SetText(DisplayName, Loc.Get("dialog.apothecary.generic_1",
                "货都在台面上。我只卖本店实验线的新药和原料盒，挑好了叫我。"))
                .SetEndAction(delegate
                {
                    AddStock();
                });
            storeClient.AddBasicDialog();
            storeClient.CompleteClientCreation();
            return storeClient;
        }

        private static void AddStock()
        {
            try
            {
                // Precursor kits and refills.
                PlayerStore.Instance.AddDirectSellingItemToTable(ItemIds.SupplyBox);
                PlayerStore.Instance.AddDirectSellingItemToTable(ItemIds.ResolvingRefill);
                PlayerStore.Instance.AddDirectSellingItemToTable(ItemIds.NaloxoneBox);
                PlayerStore.Instance.AddDirectSellingItemToTable(ItemIds.BirthControlBox);
                PlayerStore.Instance.AddDirectSellingItemToTable(ItemIds.SannaiBox);
                PlayerStore.Instance.AddDirectSellingItemToTable(ItemIds.SynaprestBox);
                PlayerStore.Instance.AddDirectSellingItemToTable(ItemIds.Tourniquet);
                // Finished medicines from the mod's production lines.
                PlayerStore.Instance.AddDirectSellingItemToTable(ItemIds.Pen);
                PlayerStore.Instance.AddDirectSellingItemToTable(ItemIds.SynaprestInjector);
                PlayerStore.Instance.AddDirectSellingItemToTable("pure_white_injector");
                PlayerStore.Instance.AddDirectSellingItemToTable("birth_control_pill");
                PlayerStore.Instance.AddDirectSellingItemToTable("zyanide_pill");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] apothecary stock failed: " + e.Message);
            }
        }

        /// <summary>Inserts the booked apothecary at the front of the queue when the appointment day starts.</summary>
        internal static void TryInjectAppointment(StoreClientManager manager)
        {
            try
            {
                if (manager == null || !PlayerStore.HasInstance || PlayerStore.Instance.PhoneClientDict == null)
                {
                    return;
                }
                StorePhoneClient client;
                if (!PlayerStore.Instance.PhoneClientDict.TryGetValue(PhoneNumber, out client) || client == null)
                {
                    return;
                }
                int appointmentDay = client.stockType;
                if (appointmentDay <= 0 || StoreStation.GetDayCounter() < appointmentDay)
                {
                    return;
                }
                client.stockType = 0;
                StoreClient apothecary = CreateClient();
                manager.clientStack.RemoveAll(delegate (StoreClient c)
                {
                    return c != null && ClientId.Equals(c.identifier);
                });
                manager.clientStack.Insert(0, apothecary);
                manager.TrackClient(apothecary);
                AddNightLog(Loc.Get("mech.apothecary.arrived",
                    "药剂师今天第一个到店，正在柜台等你。"), "67e8f9");
                Debug.Log("[PrecisionLab] apothecary inserted at the front of the client queue");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] injecting the apothecary appointment failed: " + e.Message);
            }
        }

        private static void AddNightLog(string text, string color)
        {
            try
            {
                PlayerStore.Instance.AddNightLog(text, color);
            }
            catch (Exception)
            {
                // Cosmetic only.
            }
        }
    }

    /// <summary>Copies the localised display name onto the phone contact.</summary>
    [HarmonyPatch(typeof(StorePhoneClient), "GetLocalizedDisplayName")]
    internal static class ApothecaryPhoneNamePatch
    {
        private static void Postfix(StorePhoneClient __instance, ref string __result)
        {
            if (__instance != null && __instance.locID == "precision_apothecary")
            {
                __result = Apothecary.DisplayName;
            }
        }
    }

    /// <summary>Runs after the normal morning client generation to put the apothecary first.</summary>
    [HarmonyPatch(typeof(StoreClientManager), "GenerateClient")]
    internal static class ApothecaryArrivalPatch
    {
        private static void Postfix(StoreClientManager __instance)
        {
            Apothecary.TryInjectAppointment(__instance);
        }
    }
}
