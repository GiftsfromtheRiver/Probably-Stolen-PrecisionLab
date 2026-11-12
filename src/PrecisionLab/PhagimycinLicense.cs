using System;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Joe Wilde phone contact: buy the permanent phagimycin manufacturing licence (800 credits).
    /// The licence is a passive perk, not a selectable starting trait.
    /// </summary>
    internal static class PhagimycinLicense
    {
        internal const string PerkId = "precision_lab_phagimycin_license";
        internal const string LicenseAlias = "PRECISION_PHAGIMYCIN_LICENSE";
        internal const string DialogFuncId = "PrecisionWildePhagimycinLicense";
        internal const long PhoneNumber = 6688L;
        internal const int Price = 800;

        internal static bool HasLicense()
        {
            try
            {
                if (StartingPerk.IsPerkActive(PerkId))
                {
                    return true;
                }
                StorePhoneClient assistant = FindWildeAssistant();
                return assistant != null && assistant.alias != null && assistant.alias.Contains(LicenseAlias);
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static void Register()
        {
            try
            {
                // Remove the old standalone contact from earlier builds (it had no locID and
                // showed "Translation Error" in the quick-dial list).
                if (PlayerStore.Instance != null
                    && PlayerStore.Instance.PhoneClientDict != null
                    && PlayerStore.Instance.PhoneClientDict.ContainsKey(PhoneNumber))
                {
                    PlayerStore.Instance.PhoneClientDict.Remove(PhoneNumber);
                }
                CleanBrokenPhoneContacts();
                StorePhoneClient assistant = FindWildeAssistant();
                if (assistant == null)
                {
                    Debug.LogWarning("[PrecisionLab] Wilde assistant phone contact not found; licence option not installed");
                    return;
                }
                // Repair older saves where the perk was lost: the assistant alias and/or the
                // purchase night-log line are ES3-saved, so use them as a durable marker.
                if ((assistant.alias != null && assistant.alias.Contains(LicenseAlias))
                    || HasLicenseNightLog())
                {
                    GrantPerk();
                    EnsureAlias(assistant);
                }
                InstallOverlay(assistant);
                ApplyIcon();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] registering Wilde licence option failed: " + e.Message);
            }
        }

        private static void InstallOverlay(StorePhoneClient assistant)
        {
            try
            {
                if (assistant == null)
                {
                    return;
                }
                const string prefix = "PrecisionWildeOverlay_";
                string baseId = assistant.dialogFuncId ?? "";
                // A save may persist the wrapper id. Strip any wrappers to recover the vanilla
                // dialog function id, which still exists in PhoneClientList.DialogFuncs on load.
                while (baseId.StartsWith(prefix, StringComparison.Ordinal)
                    && baseId.Length > prefix.Length)
                {
                    baseId = baseId.Substring(prefix.Length);
                }
                if (string.IsNullOrEmpty(baseId))
                {
                    return;
                }
                Func<StorePhoneClient, Dialogue> original;
                if (!PhoneClientList.DialogFuncs.TryGetValue(baseId, out original) || original == null)
                {
                    Debug.LogWarning("[PrecisionLab] Wilde base dialog function missing: " + baseId);
                    return;
                }
                string wrapperId = prefix + baseId;
                if (!PhoneClientList.DialogFuncs.ContainsKey(wrapperId))
                {
                    PhoneClientList.DialogFuncs[wrapperId] = delegate (StorePhoneClient client)
                    {
                        Dialogue dialogue = original(client);
                        AddLicenseChoice(dialogue, client);
                        return dialogue;
                    };
                }
                assistant.dialogFuncId = wrapperId;
                Debug.Log("[PrecisionLab] licence overlay installed (" + wrapperId + ")");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] installing Wilde licence overlay failed: " + e.Message);
            }
        }

        private static void CleanBrokenPhoneContacts()
        {
            try
            {
                if (PlayerStore.Instance == null || PlayerStore.Instance.PhoneClientDict == null)
                {
                    return;
                }
                System.Collections.Generic.List<long> remove = null;
                foreach (System.Collections.Generic.KeyValuePair<long, StorePhoneClient> pair
                    in PlayerStore.Instance.PhoneClientDict)
                {
                    StorePhoneClient client = pair.Value;
                    if (client == null)
                    {
                        continue;
                    }
                    string locId = client.locID ?? "";
                    bool broken = locId.IndexOf("brewing_expansion", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!broken)
                    {
                        try
                        {
                            string name = client.GetLocalizedDisplayName() ?? "";
                            broken = name.IndexOf("Translation Error", StringComparison.OrdinalIgnoreCase) >= 0;
                        }
                        catch (Exception)
                        {
                            broken = true;
                        }
                    }
                    if (broken)
                    {
                        if (remove == null) remove = new System.Collections.Generic.List<long>();
                        remove.Add(pair.Key);
                    }
                }
                if (remove != null)
                {
                    for (int i = 0; i < remove.Count; i++)
                    {
                        PlayerStore.Instance.PhoneClientDict.Remove(remove[i]);
                    }
                    Debug.Log("[PrecisionLab] removed " + remove.Count + " broken phone contact(s)");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] cleaning broken phone contacts failed: " + e.Message);
            }
        }

        private static bool HasLicenseNightLog()
        {
            try
            {
                if (PlayerStore.Instance == null || PlayerStore.Instance.nightLogs == null)
                {
                    return false;
                }
                for (int i = 0; i < PlayerStore.Instance.nightLogs.Count; i++)
                {
                    string line = PlayerStore.Instance.nightLogs[i];
                    if (!string.IsNullOrEmpty(line)
                        && line.IndexOf("制药执照", StringComparison.Ordinal) >= 0)
                    {
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                // Fall through.
            }
            return false;
        }

        private static void EnsureAlias(StorePhoneClient client)
        {
            try
            {
                if (client.alias == null)
                {
                    client.alias = new System.Collections.Generic.List<string>();
                }
                if (!client.alias.Contains(LicenseAlias))
                {
                    client.alias.Add(LicenseAlias);
                }
            }
            catch (Exception)
            {
                // Non-fatal.
            }
        }

        private static void GrantPerk()
        {
            try
            {
                if (StartingPerk.IsPerkActive(PerkId))
                {
                    return;
                }
                PlayerStore store = PlayerStore.Instance;
                if (store == null || store.startingPerks == null)
                {
                    return;
                }
                store.startingPerks.Add(new StartingPerk
                {
                    id = PerkId,
                    type = StartingPerk.StartingPerkType.NEUTRAL,
                    cost = 0,
                    mindEditable = false,
                    rawName = Loc.Get("perk.phagimycin_license.name", "制药执照：噬菌霉素"),
                    rawDescription = Loc.Get("perk.phagimycin_license.desc",
                        "允许合法生产噬菌霉素片；永久被动生效。"),
                    source = Loc.ModId
                });
                ApplyIcon();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] granting phagimycin licence perk failed: " + e.Message);
            }
        }

        private static StorePhoneClient FindWildeAssistant()
        {
            try
            {
                if (PlayerStore.Instance == null || PlayerStore.Instance.PhoneClientDict == null)
                {
                    return null;
                }
                foreach (StorePhoneClient client in PlayerStore.Instance.PhoneClientDict.Values)
                {
                    if (client == null || string.IsNullOrEmpty(client.dialogFuncId))
                    {
                        continue;
                    }
                    string name = "";
                    try
                    {
                        name = client.GetLocalizedDisplayName() ?? "";
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    if (name.IndexOf("王尔德", StringComparison.Ordinal) >= 0
                        || name.IndexOf("Wilde", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return client;
                    }
                }
            }
            catch (Exception)
            {
                // Fall through.
            }
            return null;
        }

        private static void AddLicenseChoice(Dialogue root, StorePhoneClient client)
        {
            if (root == null || HasLicense())
            {
                return;
            }
            string name = Loc.Get("phone.wilde.name", "乔·王尔德的助手");
            // The phone UI renders a choice bubble from the NEXT dialogue's plainText, not from the
            // AddChoice name. Use the player's request as the next line so the bubble does not show
            // the purchase-success text before the player has paid.
            Dialogue playerBuy = new Dialogue();
            playerBuy.SetText("PLAYER", Loc.Get("phone.wilde.buy", "我想弄点合法的门路。"));
            bool canAfford = PlayerStore.Instance != null && PlayerStore.Instance.playerCash >= Price;
            Dialogue result = new Dialogue();
            if (canAfford)
            {
                result.SetText(name, Loc.Get("phone.wilde.bought", "手续办好了，执照生效。别让我后悔。"))
                    .SetEndAction(delegate { PhoneUIManager.Instance.OnPhoneDialogEnd(); });
            }
            else
            {
                result.SetText(name, Loc.Get("phone.wilde.not_enough", "先凑够 800 信用点再来，别浪费我时间。"))
                    .SetEndAction(delegate { PhoneUIManager.Instance.OnPhoneDialogEnd(); });
            }
            playerBuy.SetNextDialogue(result);
            root.AddChoice(DirectoryMaster.Item("$"),
                Loc.Get("phone.wilde.buy", "我想弄点合法的门路。"), "",
                playerBuy, delegate { BuyLicense(client); });
        }

        private static Dialogue BuildDialog(StorePhoneClient client)
        {
            string name = Loc.Get("phone.wilde.name", "乔·王尔德的助手");
            Dialogue root = new Dialogue();
            root.SetText(name, Loc.Get("phone.wilde.intro",
                "我是王尔德的助手。想要合法生产噬菌霉素？执照 800 信用点，一次买断。"));
            return root;
        }

        private static void BuyLicense(StorePhoneClient client)
        {
            try
            {
                PlayerStore store = PlayerStore.Instance;
                if (store == null || HasLicense())
                {
                    return;
                }
                if (store.playerCash < Price)
                {
                    StoreUIManager.Instance.Notify(
                        Loc.Get("phone.wilde.no_cash", "钱不够：执照需要 800 信用点。"), "default");
                    return;
                }
                store.playerCash -= Price;
                if (client == null)
                {
                    client = FindWildeAssistant();
                }
                EnsureAlias(client);
                GrantPerk();
                store.AddNightLog(Loc.Get("phone.wilde.log", "你从乔·王尔德处拿到了制药执照：噬菌霉素。"), "9cc3ff");
                Debug.Log("[PrecisionLab] phagimycin licence purchased");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] buying phagimycin licence failed: " + e.Message);
            }
        }

        internal static void ApplyIcon()
        {
            try
            {
                var icons = StartingPerkIconLoader.perkIcons;
                if (icons == null || icons.ContainsKey(PerkId))
                {
                    return;
                }
                Sprite sprite;
                if (icons.TryGetValue("saving", out sprite) && sprite != null)
                {
                    icons[PerkId] = sprite;
                }
            }
            catch (Exception)
            {
                // Cosmetic.
            }
        }
    }

    /// <summary>
    /// A licensed press of pure antibiotic API turns the unlicensed pill output into the true
    /// vanilla phagimycin_pill. Low purity or no licence stays unlicensed.
    /// </summary>
    [HarmonyPatch(typeof(ChemicalProductHelper), "GradeProduct")]
    /// <summary>
    /// Captures the licence/purity decision while CanOutput still has the input container. The
    /// output item (a vanilla unlicensed pill) is not a CHEM_ITEM, so its liquid tags cannot be read
    /// reliably; GradeProduct then consumes this flag.
    /// </summary>
    [HarmonyPatch(typeof(MachineChemFinisher), "CanOutput")]
    internal static class PhagimycinPillGate
    {
        internal static bool ConversionEligible;

        private static bool Prefix(GameItem machine, GameSlotInventory containerSlot, ref bool __result)
        {
            try
            {
                if (machine == null || containerSlot == null || containerSlot.childItem == null
                    || !machine.IsTag("SELECTED_CHEM_ID_TAG"))
                {
                    ConversionEligible = false;
                    return true;
                }
                if (machine.GetTagReadonly("SELECTED_CHEM_ID_TAG").GetString() != BioPharmaContent.UnlicensedPillId)
                {
                    ConversionEligible = false;
                    return true;
                }
                string part = Liquid.GetCurrentPartFromId(CustomLiquids.PureAntibioticApi);
                int active = part != null ? containerSlot.childItem.GetTagReadonly(part).GetInt() : 0;
                int total = WaterHelper.GetTotalVolume(containerSlot.childItem);
                ConversionEligible = PhagimycinLicense.HasLicense()
                    && total > 0
                    && active * 100.0 / total >= 99.0;
            }
            catch (Exception)
            {
                ConversionEligible = false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(ChemicalProductHelper), "GradeProduct")]
    internal static class PhagimycinPillConversionPatch
    {
        private static void Postfix(GameItem item)
        {
            try
            {
                if (item == null
                    || item.identifier != BioPharmaContent.UnlicensedPillId
                    || !PhagimycinPillGate.ConversionEligible
                    || !PhagimycinLicense.HasLicense())
                {
                    return;
                }
                PhagimycinPillGate.ConversionEligible = false;
                item.identifier = BioPharmaContent.LicensedPillId;
                item.name = LocHelper.GetLocalizedItem("item_phagimycin_pill_name");
                item.flavorText = LocHelper.GetLocalizedItem("item_phagimycin_pill_flavor");
                item.SetSpriteAndShape("Items/items_medical", "pill1");
                item.unitValue = 25L;
                item.SetGameItemType("MEDICAL");
                ContrabandHelper.RemoveContrabandStatus(item);
                try
                {
                    item.EnableManufacturer("carmicheal");
                }
                catch (Exception)
                {
                    // Optional.
                }
                Debug.Log("[PrecisionLab] licensed phagimycin pill produced");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] phagimycin licence conversion failed: " + e.Message);
            }
        }
    }
}

