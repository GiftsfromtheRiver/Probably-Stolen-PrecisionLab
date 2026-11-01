using System;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// The retired chemist is a vanilla supplier client (StoreClientListSpec.CreateRetiredChemist)
    /// who puts a hard-coded stock list on the counter in his main dialogue's end action.
    ///
    /// Once the player has unlocked the Precision Liquid Extractor upgrade, the retired chemist
    /// also offers a replacement extractor — but only if the player currently owns none (lost it),
    /// so a player can never own more than one through him. The Wild Network upgrade itself still
    /// delivers the first extractor.
    /// </summary>
    [HarmonyPatch(typeof(StoreClientListSpec), "CreateRetiredChemist")]
    internal static class RetiredChemistExtractorPatch
    {
        private static void Postfix(StoreClient __result)
        {
            try
            {
                if (__result == null || __result.mainDialogue == null)
                {
                    return;
                }
                Dialogue dialogue = __result.mainDialogue;
                Action original = dialogue.endAction;
                dialogue.endAction = delegate
                {
                    if (original != null)
                    {
                        original();
                    }
                    RetiredChemistStock.TryAddExtractor();
                    RetiredChemistStock.TryAddAdrenalineKit();
                };
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] retired chemist stock hook failed: " + e.Message);
            }
        }
    }

    internal static class RetiredChemistStock
    {
        private const string ExtractorUpgradeId = "PRECISION_LAB_EXTRACTOR";

        internal static void TryAddExtractor()
        {
            try
            {
                PlayerStore store = PlayerStore.Instance;
                if (store == null || !IsExtractorUnlocked(store))
                {
                    return;
                }
                if (store.IsPlayerOwnThisItem(ItemIds.Extractor))
                {
                    // At most one extractor: only sell a replacement after the old one is lost.
                    return;
                }
                store.AddDirectSellingItemToTable(ItemIds.Extractor);
                Debug.Log("[PrecisionLab] retired chemist offers a replacement extractor");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] retired chemist extractor stock failed: " + e);
            }
        }

        private static bool IsExtractorUnlocked(PlayerStore store)
        {
            if (store.networkUpgrade == null)
            {
                return false;
            }
            NetworkUpgrade upgrade;
            return store.networkUpgrade.TryGetValue(ExtractorUpgradeId, out upgrade)
                && upgrade != null
                && upgrade.IsUnlocked();
        }

        /// <summary>After the "更多药品" unlock the retired chemist sells adrenaline precursor kits.</summary>
        internal static void TryAddAdrenalineKit()
        {
            try
            {
                PlayerStore store = PlayerStore.Instance;
                if (store == null || store.networkUpgrade == null)
                {
                    return;
                }
                NetworkUpgrade upgrade;
                if (!store.networkUpgrade.TryGetValue("PRECISION_LAB_MEDS", out upgrade)
                    || upgrade == null
                    || !upgrade.IsUnlocked())
                {
                    return;
                }
                store.AddDirectSellingItemToTable(ItemIds.SupplyBox);
                store.AddDirectSellingItemToTable(ItemIds.ResolvingRefill);
                Debug.Log("[PrecisionLab] retired chemist offers an adrenaline precursor kit and a resolving agent refill");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] retired chemist adrenaline kit stock failed: " + e);
            }
        }

        /// <summary>True when the player currently owns no extractor (used by diagnostics).</summary>
        internal static bool PlayerOwnsExtractor(PlayerStore store)
        {
            return store != null && store.IsPlayerOwnThisItem(ItemIds.Extractor);
        }
    }
}
