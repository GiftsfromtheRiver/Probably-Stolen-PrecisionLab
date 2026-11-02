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
                StripLegacyBookingLogs();
                MigrateStaleCooldown();
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

        /// <summary>
        /// Earlier builds (and their boot-time selftest) could clear a pending appointment day while
        /// leaving the phone contact on cooldown, which left the player stuck showing “2 days later”
        /// with no visit scheduled. Detect that stale state (cooldown, no pending appointment,
        /// no arrival marker) and clear the cooldown so the player can book again.
        /// </summary>
        internal static void MigrateStaleCooldown()
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
                    return;
                }
                if (client.phoneState == StorePhoneClient.PhoneState.Cooldown
                    && client.currentCooldown > 0
                    && client.stockType <= 0
                    && client.amountSpent == 0)
                {
                    client.phoneState = StorePhoneClient.PhoneState.Regular;
                    client.currentCooldown = 0;
                    Debug.Log("[PrecisionLab] cleared stale apothecary cooldown (appointment was lost by an old build)");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] apothecary stale-cooldown migration failed: " + e.Message);
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

            // The phone UI shows the *next dialogue's line* as the clickable choice bubble, so the
            // choice dialogues must be the PLAYER's lines ("我要预约。" / "打错了。"), with the
            // apothecary's response chained after them.
            Dialogue bookReply = new Dialogue();
            bookReply.SetText(name, Loc.Get("phone.apothecary.confirmed",
                "好，明天开门时我会第一个到店。准备好信用点。"))
                .SetEndAction(delegate { PhoneUIManager.Instance.OnPhoneDialogEnd(); });

            Dialogue playerBook = new Dialogue();
            playerBook.SetText("PLAYER", Loc.Get("phone.apothecary.book_line", "我要预约。"))
                .SetNextDialogue(bookReply);

            Dialogue cancelReply = new Dialogue();
            cancelReply.SetText(name, Loc.Get("phone.apothecary.cancel", "行，想好了再打给我。"))
                .SetEndAction(delegate { PhoneUIManager.Instance.OnPhoneDialogEnd(); });

            Dialogue playerCancel = new Dialogue();
            playerCancel.SetText("PLAYER", Loc.Get("phone.apothecary.cancel_line", "打错了。"))
                .SetNextDialogue(cancelReply);

            Dialogue dialogue = new Dialogue();
            dialogue.SetText(name, Loc.Get("phone.apothecary.regular",
                "我是药剂师。需要我上门吗？我卖的东西比退休化学家更全，但只接受预约。"));
            dialogue.AddChoice(DirectoryMaster.Item("$"),
                Loc.Get("phone.apothecary.book_line", "我要预约。"), "",
                playerBook, delegate { BookAppointment(client); });
            dialogue.AddChoice(DirectoryMaster.Item("$"),
                Loc.Get("phone.apothecary.cancel_line", "打错了。"), "",
                playerCancel, null);
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
                // No night-log line on booking: the arrival line on the appointment day already
                // announces the visit, and a booking line printed in the same report was redundant.
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
                "货都在台面上：原料盒、散装试剂瓶，还有几支成色不一的成品药。挑好了叫我。"))
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
                // 1) Precursor kits: only a random few each visit, so the counter is not crowded out.
                AddRandomPrecursorKits();
                // 2) Loose 20 mL reagent bottles (random selection each visit).
                AddLooseReagentBottles();
                // 3) A small number of finished medicines with random quality.
                AddRandomQualityMedicines();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] apothecary stock failed: " + e.Message);
            }
        }

        private static readonly string[] PrecursorKitPool =
        {
            ItemIds.SupplyBox,
            ItemIds.ResolvingRefill,
            ItemIds.NaloxoneBox,
            ItemIds.BirthControlBox,
            ItemIds.SannaiBox,
            ItemIds.SynaprestBox
        };

        private static void AddRandomPrecursorKits()
        {
            int[] order = ShuffledIndices(PrecursorKitPool.Length);
            int count = UnityEngine.Random.Range(2, 4);
            for (int i = 0; i < count && i < order.Length; i++)
            {
                PlayerStore.Instance.AddDirectSellingItemToTable(PrecursorKitPool[order[i]]);
            }
        }

        private static readonly string[] LooseBottleLiquids =
        {
            CustomLiquids.Thebaine,
            CustomLiquids.NeuralLigand,
            CustomLiquids.SteroidRaw,
            CustomLiquids.SannaiRaw,
            CustomLiquids.Catechol,
            CustomLiquids.HydroxymethylBase,
            CustomLiquids.ResolvingAgent,
            "acid",
            "reducing_agent",
            "lipobond_agent",
            "alcohol",
            "base"
        };

        private static readonly int[] LooseBottlePrices =
        {
            150, 150, 60, 60, 40, 40, 40, 20, 20, 30, 20, 20
        };

        private static readonly string[] LooseBottleContainers =
        {
            "med_bottle_violet",
            "med_bottle_orange",
            "med_bottle_red",
            "med_bottle_small_pink",
            "med_bottle_blue"
        };

        private static readonly string[] FinishedMedicineIds =
        {
            ItemIds.Pen,
            ItemIds.SynaprestInjector,
            "pure_white_injector",
            "birth_control_pill",
            "zyanide_pill"
        };

        private static void AddLooseReagentBottles()
        {
            int[] order = ShuffledIndices(LooseBottleLiquids.Length);
            int count = UnityEngine.Random.Range(3, 6);
            for (int i = 0; i < count && i < order.Length; i++)
            {
                int index = order[i];
                string liquidId = LooseBottleLiquids[index];
                Liquid liquid = Liquid.GetLiquidFromId(liquidId);
                string liquidName = liquid != null && !string.IsNullOrEmpty(liquid.displayName)
                    ? liquid.displayName
                    : liquidId;
                GameItem bottle = AdrenalineContent.CreateBottle(
                    LooseBottleContainers[index % LooseBottleContainers.Length],
                    liquidId,
                    20000,
                    "item.apothecary.loose_bottle.name",
                    "Reagent Bottle");
                if (bottle == null)
                {
                    continue;
                }
                bottle.name = Loc.Fmt("item.apothecary.loose_bottle.name", "{0} · 20 毫升", liquidName);
                bottle.unitValue = LooseBottlePrices[index];
                PlayerStore.Instance.AddDirectSellingItemToTable(bottle);
            }
        }

        private static void AddRandomQualityMedicines()
        {
            int[] order = ShuffledIndices(FinishedMedicineIds.Length);
            int count = UnityEngine.Random.Range(1, 4);
            for (int i = 0; i < count && i < order.Length; i++)
            {
                string id = FinishedMedicineIds[order[i]];
                if (!DirectoryMaster.Has<GameItem>(id))
                {
                    continue;
                }
                GameItem item = DirectoryMaster.Item(id);
                if (item == null)
                {
                    continue;
                }
                ChemicalProductHelper.SetPurityFeature(item, RandomQualityCondition());
                PlayerStore.Instance.AddDirectSellingItemToTable(item);
            }
        }

        private static ItemCondition RandomQualityCondition()
        {
            // Apothecary stock is intentionally skewed towards usable grades:
            // lab pure is rare, street standard is common, junk/starch are rare.
            // 2% lab_pure / 25% pharma / 35% near_pharma / 30% street / 5% cut / 2% junk / 1% starch
            int roll = UnityEngine.Random.Range(0, 100);
            if (roll < 2)
            {
                return ItemConditionList.CreateLabPure();
            }
            if (roll < 27)
            {
                return ItemConditionList.CreatePharmaGrade();
            }
            if (roll < 62)
            {
                return ItemConditionList.CreateNearPharma();
            }
            if (roll < 92)
            {
                return ItemConditionList.CreateStreetStandard();
            }
            if (roll < 97)
            {
                return ItemConditionList.CreateCutProduct();
            }
            if (roll < 99)
            {
                return ItemConditionList.CreateJunkGrade();
            }
            return ItemConditionList.CreateStarchGrade();
        }

        private static int[] ShuffledIndices(int length)
        {
            int[] indices = new int[length];
            for (int i = 0; i < length; i++)
            {
                indices[i] = i;
            }
            for (int i = length - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                int tmp = indices[i];
                indices[i] = indices[j];
                indices[j] = tmp;
            }
            return indices;
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
                // Marker used by MigrateStaleCooldown: cooldown after a real arrival is legitimate,
                // while a cooldown with no pending day and no marker is a lost booking from old builds.
                client.amountSpent = 1;
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

        /// <summary>Removes the old “药剂师预约成功…” booking line left in saves by earlier versions.</summary>
        internal static void StripLegacyBookingLogs()
        {
            try
            {
                if (!PlayerStore.HasInstance || PlayerStore.Instance == null || PlayerStore.Instance.nightLogs == null)
                {
                    return;
                }
                int removed = PlayerStore.Instance.nightLogs.RemoveAll(delegate (string line)
                {
                    return !string.IsNullOrEmpty(line)
                        && (line.Contains("药剂师预约成功") || line.Contains("Apothecary booked:"));
                });
                if (removed > 0)
                {
                    Debug.Log("[PrecisionLab] removed " + removed + " legacy apothecary booking night-log line(s)");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] stripping legacy apothecary booking logs failed: " + e.Message);
            }
        }

        /// <summary>Called when the phone cooldown expires; replaces the generic contact-ready line.</summary>
        internal static void OnContactReady()
        {
            StripLegacyBookingLogs();
            AddNightLog(Loc.Get("mech.apothecary.ready", "药剂师已准备好更多药品。"), "67e8f9");
        }

        internal static void AddNightLog(string text, string color)
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

    /// <summary>Replaces the generic “contact ready” night log for the apothecary with a mod-specific line.</summary>
    [HarmonyPatch(typeof(NightLog), "ContactReady")]
    internal static class ApothecaryContactReadyPatch
    {
        private static bool Prefix(string contactName)
        {
            if (contactName == Apothecary.DisplayName)
            {
                Apothecary.OnContactReady();
                return false;
            }
            return true;
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
