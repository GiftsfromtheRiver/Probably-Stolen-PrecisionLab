using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Gameplay effects of the crafted Synaprest injector.
    ///
    /// The item removes ALL addictions at once (the vanilla injector only removes one random
    /// addiction per use), but suppresses the brain's reward system: the player gets the
    /// "anhedonia" perk for three days (Luck -15), and using it three times transfers the
    /// addiction into a "synaprest dependency" perk. While dependent there is a small nightly
    /// chance to develop a narcotic addiction.
    ///
    /// The perks are ordinary mod StartingPerks (StartingPerkList.RegisterModPerk), so they are
    /// serialised with the save and show up in the perks calendar. The anhedonia perk stores the
    /// remaining days in its cost field; the dependency/use-count perk stores the number of uses.
    /// </summary>
    internal static class SynaprestEffects
    {
        internal const string AnhedoniaPerk = "precision_lab_synaprest_anhedonia";
        internal const string DependencyPerk = "precision_lab_synaprest_dependency";
        internal const string UseCountPerk = "precision_lab_synaprest_use_count";

        internal const int AnhedoniaDays = 3;
        internal const int LuckPenalty = 15;
        internal const int UsesBeforeDependency = 3;

        private static bool _perksRegistered;

        internal static void RegisterPerks()
        {
            if (_perksRegistered)
            {
                return;
            }
            _perksRegistered = true;
            try
            {
                StartingPerkList.RegisterModPerk(new StartingPerk
                {
                    id = AnhedoniaPerk,
                    type = StartingPerk.StartingPerkType.NEGATIVE,
                    cost = AnhedoniaDays,
                    mindEditable = false,
                    rawName = Loc.Get("perk.synaprest_anhedonia.name", "快感缺失 / Anhedonia"),
                    rawDescription = Loc.Get(
                        "perk.synaprest_anhedonia.desc",
                        "赛普那斯抑制了大脑奖励机制：好运 -15，持续 3 天。"),
                    source = Loc.ModId
                });
                StartingPerkList.RegisterModPerk(new StartingPerk
                {
                    id = DependencyPerk,
                    type = StartingPerk.StartingPerkType.NEGATIVE,
                    cost = 0,
                    mindEditable = false,
                    rawName = Loc.Get("perk.synaprest_dependency.name", "赛普那斯依赖 / Synaprest Dependency"),
                    rawDescription = Loc.Get(
                        "perk.synaprest_dependency.desc",
                        "连续使用赛普那斯超过 3 次形成的轻度依赖；每天醒来有概率重新出现药物成瘾。"),
                    source = Loc.ModId
                });
                StartingPerkList.RegisterModPerk(new StartingPerk
                {
                    id = UseCountPerk,
                    type = StartingPerk.StartingPerkType.NEUTRAL,
                    cost = 0,
                    mindEditable = false,
                    rawName = Loc.Get("perk.synaprest_use_count.name", "赛普那斯使用次数 / Synaprest Uses"),
                    rawDescription = Loc.Get(
                        "perk.synaprest_use_count.desc",
                        "累计使用赛普那斯的次数；达到 3 次会形成依赖。"),
                    source = Loc.ModId
                });
                Debug.Log("[PrecisionLab] synaprest perks registered");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] registering synaprest perks failed: " + e.Message);
            }
        }

        internal static bool IsAnhedoniaActive()
        {
            return StartingPerk.IsPerkActive(AnhedoniaPerk);
        }

        internal static bool IsDependencyActive()
        {
            return StartingPerk.IsPerkActive(DependencyPerk);
        }

        /// <summary>Removes every addiction, then applies the anhedonia/dependency side effects.</summary>
        internal static void ApplyOnUse(GameItem item)
        {
            try
            {
                HealthData health = PlayerStore.Instance != null ? PlayerStore.Instance.healthData : null;
                int cured = 0;
                string[] addictions = { "nicotine_addiction", "alcohol_addiction", "narcotic_addiction", "gambling_addiction" };
                for (int i = 0; i < addictions.Length; i++)
                {
                    if (StartingPerk.IsPerkActive(addictions[i]))
                    {
                        StartingPerk.RemovePerk(addictions[i]);
                        cured++;
                    }
                }
                if (health != null)
                {
                    health.RefreshAddictionPenaltyFeatures();
                }
                AddOrRefreshPerk(AnhedoniaPerk, AnhedoniaDays);
                int uses = GetPerkCost(UseCountPerk) + 1;
                AddOrRefreshPerk(UseCountPerk, uses);
                if (uses >= UsesBeforeDependency && !IsDependencyActive())
                {
                    AddOrRefreshPerk(DependencyPerk, 0);
                }
                string msg = Loc.Get(
                    "mech.synaprest.use",
                    "赛普那斯注射器：清除了 {0} 项成瘾，但奖励机制被抑制（快感缺失 3 天，好运 -15）。");
                string text = string.Format(msg, cured);
                AddNightLog(text, "b45cff");
                Debug.Log("[PrecisionLab] synaprest used: cured=" + cured + " uses=" + uses);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] synaprest side effects failed: " + e.Message);
            }
        }

        /// <summary>Daily tick: expire anhedonia, and occasionally trigger the dependency craving.</summary>
        internal static void OnWakingUpLate()
        {
            try
            {
                if (IsAnhedoniaActive())
                {
                    int daysLeft = GetPerkCost(AnhedoniaPerk) - 1;
                    if (daysLeft <= 0)
                    {
                        StartingPerk.RemovePerk(AnhedoniaPerk);
                        AddNightLog(Loc.Get("mech.synaprest.anhedonia_over", "赛普那斯的影响消退了：快感缺失结束。"), "9cc3ff");
                    }
                    else
                    {
                        SetPerkCost(AnhedoniaPerk, daysLeft);
                    }
                }
                if (IsDependencyActive() && RNG.Roll(10) && !StartingPerk.IsPerkActive("narcotic_addiction"))
                {
                    PlayerStore.Instance.startingPerks.Add(StartingPerkList.NarcoticAddiction());
                    AddNightLog(Loc.Get("mech.synaprest.craving", "赛普那斯依赖发作：你又对药物产生了渴望（获得药物成瘾）。"), "b45cff");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] synaprest daily tick failed: " + e.Message);
            }
        }

        // ------------------------------------------------------------------
        // Perk helpers
        // ------------------------------------------------------------------

        private static void AddOrRefreshPerk(string perkId, int cost)
        {
            StartingPerk perk = FindActivePerk(perkId);
            if (perk == null)
            {
                StartingPerk template = StartingPerkList.GetModPerk(perkId);
                if (template == null)
                {
                    return;
                }
                perk = new StartingPerk
                {
                    id = template.id,
                    type = template.type,
                    cost = cost,
                    mindEditable = template.mindEditable,
                    rawName = template.rawName,
                    rawDescription = template.rawDescription,
                    source = template.source
                };
                PlayerStore.Instance.startingPerks.Add(perk);
            }
            else
            {
                perk.cost = cost;
            }
        }

        private static StartingPerk FindActivePerk(string perkId)
        {
            List<StartingPerk> perks = PlayerStore.Instance != null ? PlayerStore.Instance.startingPerks : null;
            if (perks == null)
            {
                return null;
            }
            for (int i = 0; i < perks.Count; i++)
            {
                if (perks[i] != null && perks[i].id == perkId)
                {
                    return perks[i];
                }
            }
            return null;
        }

        private static int GetPerkCost(string perkId)
        {
            StartingPerk perk = FindActivePerk(perkId);
            return perk != null ? perk.cost : 0;
        }

        private static void SetPerkCost(string perkId, int cost)
        {
            StartingPerk perk = FindActivePerk(perkId);
            if (perk != null)
            {
                perk.cost = cost;
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

    [HarmonyPatch(typeof(HealthData), "GetLuck")]
    internal static class SynaprestLuckPatch
    {
        private static void Postfix(ref int __result)
        {
            if (SynaprestEffects.IsAnhedoniaActive())
            {
                __result = Math.Max(0, __result - SynaprestEffects.LuckPenalty);
            }
        }
    }
}
