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

        private static StartingPerk AnhedoniaTemplate()
        {
            return new StartingPerk
            {
                id = AnhedoniaPerk,
                type = StartingPerk.StartingPerkType.NEGATIVE,
                cost = AnhedoniaDays,
                mindEditable = false,
                rawName = Loc.Get("perk.synaprest_anhedonia.name", "快感缺失 / Anhedonia"),
                rawDescription = Loc.Get(
                    "perk.synaprest_anhedonia.desc",
                    "赛纳普斯抑制了大脑奖励机制：好运 -15，持续 3 天。"),
                source = Loc.ModId
            };
        }

        private static StartingPerk DependencyTemplate()
        {
            return new StartingPerk
            {
                id = DependencyPerk,
                type = StartingPerk.StartingPerkType.NEGATIVE,
                cost = 0,
                mindEditable = false,
                rawName = Loc.Get("perk.synaprest_dependency.name", "赛纳普斯依赖 / Synaprest Dependency"),
                rawDescription = Loc.Get(
                    "perk.synaprest_dependency.desc",
                    "连续使用赛纳普斯超过 3 次形成的轻度依赖；每天醒来有概率重新出现药物成瘾。"),
                source = Loc.ModId
            };
        }

        private static StartingPerk UseCountTemplate()
        {
            return new StartingPerk
            {
                id = UseCountPerk,
                type = StartingPerk.StartingPerkType.NEUTRAL,
                cost = 0,
                mindEditable = false,
                rawName = Loc.Get("perk.synaprest_use_count.name", "赛纳普斯使用次数 / Synaprest Uses"),
                rawDescription = Loc.Get(
                    "perk.synaprest_use_count.desc",
                    "累计使用赛纳普斯的次数；达到 3 次会形成依赖。"),
                source = Loc.ModId
            };
        }

        private static StartingPerk GetTemplate(string perkId)
        {
            if (perkId == AnhedoniaPerk) return AnhedoniaTemplate();
            if (perkId == DependencyPerk) return DependencyTemplate();
            if (perkId == UseCountPerk) return UseCountTemplate();
            return null;
        }

        internal static bool PerkTemplatesReady()
        {
            return GetTemplate(AnhedoniaPerk) != null
                && GetTemplate(DependencyPerk) != null
                && GetTemplate(UseCountPerk) != null;
        }

        internal static void RegisterPerks()
        {
            if (_perksRegistered)
            {
                return;
            }
            _perksRegistered = true;
            // Deliberately do NOT call StartingPerkList.RegisterModPerk: these are gameplay
            // status effects, not selectable starting traits. They are only added to the player's
            // startingPerks list when the Synaprest injector actually applies them.
            ApplyPerkIcons();
        }

        /// <summary>
        /// The three mod perks are not in the vanilla icon loader, so the perk screen falls back to
        /// the giant default silhouette placeholder. Map them onto existing vanilla perk icons.
        /// </summary>
        internal static void ApplyPerkIcons()
        {
            try
            {
                var icons = StartingPerkIconLoader.perkIcons;
                if (icons == null || icons.Count == 0)
                {
                    return;
                }
                Sprite negative = GetIcon(icons, "narcotic_addiction")
                    ?? GetIcon(icons, "alcohol_addiction")
                    ?? GetIcon(icons, "nicotine_addiction")
                    ?? GetIcon(icons, "default");
                Sprite dependency = GetIcon(icons, "alcohol_addiction")
                    ?? GetIcon(icons, "nicotine_addiction")
                    ?? negative;
                Sprite neutral = GetIcon(icons, "experienced_chemist")
                    ?? GetIcon(icons, "saving")
                    ?? GetIcon(icons, "default")
                    ?? dependency;
                if (negative != null) icons[AnhedoniaPerk] = negative;
                if (dependency != null) icons[DependencyPerk] = dependency;
                if (neutral != null) icons[UseCountPerk] = neutral;
                Debug.Log("[PrecisionLab] synaprest perk icons mapped (anhedonia=" + (negative != null)
                    + " dependency=" + (dependency != null) + " useCount=" + (neutral != null) + ")");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] mapping synaprest perk icons failed: " + e.Message);
            }
        }

        private static Sprite GetIcon(Dictionary<string, Sprite> icons, string key)
        {
            Sprite sprite;
            return icons.TryGetValue(key, out sprite) ? sprite : null;
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
                // Match the vanilla Synaprest injector: remove ONE random addiction, not all.
                int before = CountAddictions();
                int cured = 0;
                if (before > 0 && health != null)
                {
                    health.RemoveOneRandomAddiction();
                    cured = Math.Max(0, before - CountAddictions());
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
                    "赛纳普斯注射器：随机清除了 {0} 项成瘾（原版效果），但奖励机制被抑制（快感缺失 3 天，好运 -15）。");
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
                        AddNightLog(Loc.Get("mech.synaprest.anhedonia_over", "赛纳普斯的影响消退了：快感缺失结束。"), "9cc3ff");
                    }
                    else
                    {
                        SetPerkCost(AnhedoniaPerk, daysLeft);
                    }
                }
                if (IsDependencyActive() && RNG.Roll(10) && !StartingPerk.IsPerkActive("narcotic_addiction"))
                {
                    PlayerStore.Instance.startingPerks.Add(StartingPerkList.NarcoticAddiction());
                    AddNightLog(Loc.Get("mech.synaprest.craving", "赛纳普斯依赖发作：你又对药物产生了渴望（获得药物成瘾）。"), "b45cff");
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

        private static int CountAddictions()
        {
            int count = 0;
            if (StartingPerk.IsPerkActive("nicotine_addiction")) count++;
            if (StartingPerk.IsPerkActive("alcohol_addiction")) count++;
            if (StartingPerk.IsPerkActive("narcotic_addiction")) count++;
            if (StartingPerk.IsPerkActive("gambling_addiction")) count++;
            return count;
        }

        private static void AddOrRefreshPerk(string perkId, int cost)
        {
            StartingPerk perk = FindActivePerk(perkId);
            if (perk == null)
            {
                StartingPerk template = GetTemplate(perkId);
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

    [HarmonyPatch(typeof(StartingPerkIconLoader), "Start")]
    internal static class SynaprestPerkIconPatch
    {
        private static void Postfix()
        {
            SynaprestEffects.ApplyPerkIcons();
        }
    }
}