using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// All liquid maths for the two items.
    ///
    /// Game facts (decompiled WaterHelper/Liquid/WaterFeatureHelper/MachinePurifier):
    ///  - Liquids are stored as integer microlitres ("parts"): 1000 = 1 mL, 1 = 1 µL.
    ///    A container's capacity is LIQUID_CONTAINER_CAPACITY in the same unit.
    ///  - Each Liquid has two tags on the item: PART_&lt;ID&gt; (base amount, used for stacking
    ///    and persistence) and CURRENT_PART_&lt;ID&gt; (live amount, used by gameplay).
    ///  - "Water purity" = water volume / total volume * 10000 (basis points, 0..10000).
    ///  - Liquid ids: water, heavy_metal, microbe, organic_waste, mineral,
    ///    chemical_contaminant, physical_contaminant, microplastic, debris, plant_matter and
    ///    product liquids (alcohol, caffeine, protein, fat, starch, sugar, dream_cap_extract,
    ///    nightmare_extract, synaprest, wine_extract, ...).
    /// </summary>
    internal static class LiquidTools
    {
        internal const int MicrolitersPerMl = 1000;
        internal const int MaxDoseUl = 10000;

        /// <summary>Pipette interaction mode: automatic (empty draws / filled injects), always draw, always inject.</summary>
        internal const int ModeAuto = 0;
        internal const int ModeDraw = 1;
        internal const int ModeInject = 2;

        internal static int GetMode(GameItem extractor)
        {
            if (extractor == null)
            {
                return ModeAuto;
            }
            int stored = extractor.GetTagReadonly(Tags.ExtractorMode).GetInt();
            return (stored >= ModeAuto && stored <= ModeInject) ? stored : ModeAuto;
        }

        internal static void SetMode(GameItem extractor, int mode)
        {
            if (extractor == null)
            {
                return;
            }
            int clamped = Mathf.Clamp(mode, ModeAuto, ModeInject);
            extractor.ModifyTag(Tags.ExtractorMode, delegate (TagState state)
            {
                state.Enable().SetInt(clamped);
            });
        }

        /// <summary>
        /// Resolves whether the next use of the extractor should draw from the target (true) or
        /// inject into it (false). Auto: empty extractor draws, non-empty injects. Draw/inject
        /// modes force the direction, which is what allows topping a partially filled pipette
        /// back up with more of the same liquid.
        /// </summary>
        internal static bool ResolveDirection(GameItem extractor)
        {
            int mode = GetMode(extractor);
            if (mode == ModeDraw)
            {
                return true;
            }
            if (mode == ModeInject)
            {
                return false;
            }
            return WaterHelper.GetTotalVolume(extractor) <= 0;
        }

        /// <summary>
        /// Exactly the liquids that count as contamination for the purifier, split by the
        /// physical / chemical / biological categories shown in the item tooltip.
        /// Valuable product liquids are deliberately NOT in these lists.
        /// </summary>
        internal static readonly string[] PhysicalContaminants =
        {
            "physical_contaminant", "microplastic", "debris", "mineral"
        };

        internal static readonly string[] ChemicalContaminants =
        {
            "chemical_contaminant", "heavy_metal"
        };

        internal static readonly string[] BiologicalContaminants =
        {
            "microbe", "organic_waste", "plant_matter"
        };

        internal static readonly string[] AllContaminants = BuildContaminantList();

        /// <summary>How a finished purify cycle counted the removed contamination.</summary>
        internal enum PurifyAccounting
        {
            /// <summary>Water-only container: removed contamination becomes clean water (total volume unchanged).</summary>
            CleanWater,

            /// <summary>Container with valuable liquids: removed contamination is discarded (volume drops).</summary>
            Concentrate
        }

        private static string[] BuildContaminantList()
        {
            List<string> list = new List<string>();
            list.AddRange(PhysicalContaminants);
            list.AddRange(ChemicalContaminants);
            list.AddRange(BiologicalContaminants);
            return list.ToArray();
        }

        private static Liquid GetLiquid(string liquidId)
        {
            if (liquidId == null || Liquid.LiquidDict == null)
            {
                return null;
            }
            Liquid liquid;
            return Liquid.LiquidDict.TryGetValue(liquidId, out liquid) ? liquid : null;
        }

        internal static int GetLiquidAmount(GameItem container, string liquidId)
        {
            if (container == null || !container.IsTag(Tags.LiquidContainer))
            {
                return 0;
            }
            Liquid liquid = GetLiquid(liquidId);
            return liquid == null ? 0 : container.GetTagReadonly(liquid.currentPart).GetInt();
        }

        internal static int GetContaminantAmount(GameItem container)
        {
            int sum = 0;
            for (int i = 0; i < AllContaminants.Length; i++)
            {
                sum += GetLiquidAmount(container, AllContaminants[i]);
            }
            return sum;
        }

        internal static bool IsContaminant(string liquidId)
        {
            for (int i = 0; i < AllContaminants.Length; i++)
            {
                if (AllContaminants[i] == liquidId)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// True when the container holds only water plus contamination, i.e. the purifier can
        /// run in "clean water" accounting mode (removed contamination becomes water).
        /// </summary>
        internal static bool IsWaterAndContaminantsOnly(GameItem container)
        {
            if (container == null)
            {
                return false;
            }
            for (int i = 0; i < Liquid.Liquids.Count; i++)
            {
                Liquid liquid = Liquid.Liquids[i];
                if (liquid.identifier == "water" || IsContaminant(liquid.identifier))
                {
                    continue;
                }
                if (container.GetTagReadonly(liquid.currentPart).GetInt() > 0)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Maximum deliverable volume of an exact transfer in the given direction, in µL.
        /// </summary>
        internal static int GetDeliverableUl(GameItem source, GameItem target, int requestedUl)
        {
            if (source == null || target == null || requestedUl <= 0)
            {
                return 0;
            }
            if (!source.IsTag(Tags.LiquidContainer) || !target.IsTag(Tags.LiquidContainer))
            {
                return 0;
            }
            if (source.IsTag(Tags.WineContainer) || target.IsTag(Tags.WineContainer))
            {
                return 0;
            }
            long available = Math.Min((long)WaterHelper.GetTotalVolume(source), (long)WaterHelper.GetFreeCapacity(target));
            return (int)Math.Min((long)requestedUl, available);
        }

        /// <summary>
        /// Directly sets one liquid species' volume (raw tag write, no UI refresh). Intended for
        /// diagnostics/tests where the item is not attached to a live inventory element yet.
        /// </summary>
        internal static void SetLiquidRaw(GameItem container, string liquidId, int amountUl)
        {
            if (container == null || !container.IsTag(Tags.LiquidContainer))
            {
                return;
            }
            Liquid liquid = GetLiquid(liquidId);
            if (liquid == null)
            {
                return;
            }
            int clamped = Math.Max(0, amountUl);
            container.ModifyTag(liquid.currentPart, delegate (TagState state)
            {
                state.Enable().SetInt(clamped);
            }, true);
        }

        /// <summary>
        /// Moves exactly <paramref name="amountUl"/> microlitres from source to target,
        /// preserving the source's liquid composition proportionally. Callers must have
        /// validated that the volume is deliverable (see <see cref="GetDeliverableUl"/>);
        /// this function is all-or-nothing and returns 0 when it cannot deliver exactly the
        /// requested amount.
        ///
        /// Mirrors WaterHelper.TransferLiquid but with an explicit amount and no filter
        /// handling, which is what makes the extractor precise to 1 µL.
        /// </summary>
        internal static int MoveExact(GameItem source, GameItem target, int amountUl)
        {
            if (source == null || target == null || source == target || amountUl <= 0)
            {
                return 0;
            }
            if (!source.IsTag(Tags.LiquidContainer) || !target.IsTag(Tags.LiquidContainer))
            {
                return 0;
            }
            if (source.IsTag(Tags.WineContainer) || target.IsTag(Tags.WineContainer))
            {
                return 0;
            }
            if (GetDeliverableUl(source, target, amountUl) < amountUl)
            {
                return 0;
            }

            bool targetWasEmpty = WaterHelper.IsEmpty(target);
            int[] parts = WaterHelper.GetLiquidParts(source);
            int[] toMove = WaterHelper.SplitProportionally(parts, amountUl);
            int checkSum = 0;
            for (int i = 0; i < toMove.Length; i++)
            {
                checkSum += toMove[i];
            }
            if (checkSum != amountUl)
            {
                // Should never happen for a well-formed container; abort instead of making a
                // silently inexact transfer.
                Debug.LogError("[PrecisionLab] exact transfer aborted: split sum " + checkSum + " != " + amountUl);
                return 0;
            }

            int moved = 0;
            for (int i = 0; i < Liquid.Liquids.Count; i++)
            {
                int give = toMove[i];
                if (give <= 0)
                {
                    continue;
                }
                Liquid liquid = Liquid.Liquids[i];
                int have = source.GetTagReadonly(liquid.currentPart).GetInt();
                if (have <= 0)
                {
                    continue;
                }
                int delta = Math.Min(give, have);
                int targetHas = target.GetTagReadonly(liquid.currentPart).GetInt();
                ChemicalHelper.HandleReceiveLiquidTemp(target, delta, source);
                int newSource = have - delta;
                int newTarget = targetHas + delta;
                source.ModifyTag(liquid.currentPart, delegate (TagState state)
                {
                    state.Enable().SetInt(newSource);
                }, true);
                target.ModifyTag(liquid.currentPart, delegate (TagState state)
                {
                    state.Enable().SetInt(newTarget);
                }, true);
                moved += delta;
            }

            if (moved != amountUl)
            {
                Debug.LogError("[PrecisionLab] exact transfer aborted: moved " + moved + " != " + amountUl);
                return 0;
            }

            // UI/feature refresh must never make the maths fail: unattached items (diagnostics,
            // autoload helpers) can legitimately hit null UI singletons here.
            if (!ItemFactory.SuppressUiRefresh)
            {
                try
                {
                    source.ValidateShapeState();
                    target.ValidateShapeState();
                    WaterFeatureHelper.OnLiquidContainerPourInto(target, source, targetWasEmpty);
                    WaterFeatureHelper.OnLiquidContainerPourOutOf(source);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[PrecisionLab] liquid UI refresh after exact transfer failed: " + e.Message);
                }
            }
            return moved;
        }

        /// <summary>
        /// Extractor metering, in µL. Stored per item instance so two extractors can be
        /// set to different volumes; the mod config value is the default for new items.
        /// </summary>
        internal static int GetDose(GameItem extractor)
        {
            int stored = extractor.GetTagReadonly(Tags.ExtractorDose).GetInt();
            if (stored >= 1 && stored <= MaxDoseUl)
            {
                return stored;
            }
            return ModEntry.ConfigDoseUl;
        }

        internal static void SetDose(GameItem extractor, int doseUl)
        {
            int clamped = Mathf.Clamp(doseUl, 1, MaxDoseUl);
            extractor.ModifyTag(Tags.ExtractorDose, delegate (TagState state)
            {
                state.Enable().SetInt(clamped);
            });
        }

        internal static readonly int[] DosePresets =
        {
            1, 5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000
        };

        internal static int NextDosePreset(int current)
        {
            for (int i = 0; i < DosePresets.Length; i++)
            {
                if (DosePresets[i] > current)
                {
                    return DosePresets[i];
                }
            }
            return DosePresets[0];
        }

        /// <summary>
        /// Purity cap of the purified output in basis points (0..10000). Uses the same
        /// quality-module curve as the vanilla water purifier (99.00 % unmodded,
        /// 100.00 % with very strong quality modules).
        /// </summary>
        internal static int GetPurityCap(GameItem machine)
        {
            int quality = machine == null ? 0 : MachineryHelper.GetCurrentQualityBonus(machine);
            return MachinePurifier.GetMaxPurityCap(quality);
        }

        /// <summary>
        /// Per-cycle removal budget in µL — identical maths to the vanilla water purifier:
        /// (min(total, 10 ML) * 3‰ + 25 000) * dirtBonus * 1.25, where dirtBonus comes from
        /// the current purity (+ performance modules). Dirty liquid is cleaned much faster.
        /// </summary>
        internal static long GetCycleBudgetUl(GameItem container, GameItem machine)
        {
            int total = WaterHelper.GetTotalVolume(container);
            int purity = WaterHelper.GetWaterPurity(container);
            int performance = machine == null ? 0 : MachineryHelper.GetCurrentPerformanceBonus(machine);
            double dirtBonus = MachinePurifier.GetDirtBonus(purity) + performance / 100.0;
            if (dirtBonus < 0.0)
            {
                dirtBonus = 0.0;
            }
            dirtBonus *= 1.25;
            long baseVolume = Math.Min((long)total, 10000000L);
            return (long)((baseVolume * 3L / 1000L + 25000L) * dirtBonus);
        }

        /// <summary>
        /// Computes how much contamination must be removed to reach the purity cap, taking the
        /// accounting mode into account. Returns the contamination volume to target, or the
        /// whole contaminant volume when the cap is unreachable (product liquids keep purity
        /// below the cap by definition).
        /// </summary>
        internal static long ComputeRemovalTargetUl(GameItem container, int cap, PurifyAccounting accounting, int contaminant)
        {
            int total = WaterHelper.GetTotalVolume(container);
            int water = GetLiquidAmount(container, "water");
            if (accounting == PurifyAccounting.CleanWater)
            {
                // Removed contamination becomes water: (water + removed) / total >= cap.
                long waterNeeded = DivCeil((long)total * cap, 10000L) - water;
                long reachable = (long)(water + contaminant) * 10000L / total;
                if (reachable < cap)
                {
                    return contaminant;
                }
                return Math.Max(0L, waterNeeded);
            }
            // Concentrate: removed contamination is discarded, so total shrinks and water stays.
            long remainingTotalAllowed = water > 0 ? (long)water * 10000L / cap : 0L;
            long totalAfterAll = total - contaminant;
            long reachablePurity = totalAfterAll > 0 ? (long)water * 10000L / totalAfterAll : 10000L;
            if (totalAfterAll > 0 && reachablePurity < cap)
            {
                return contaminant;
            }
            long needed = total - Math.Max(0L, remainingTotalAllowed);
            return Math.Max(0L, Math.Min(needed, contaminant));
        }

        private static long DivCeil(long value, long divisor)
        {
            if (divisor <= 0)
            {
                return value;
            }
            return (value + divisor - 1) / divisor;
        }

        /// <summary>
        /// Removes physical/chemical/biological contaminants from the container, up to the
        /// machine's purity cap and per-cycle budget.
        ///
        /// Accounting is chosen automatically:
        ///  - water + contamination only  -> "clean water" mode: removed contamination becomes
        ///    clean water, total volume stays the same (vanilla purifier parity);
        ///  - container with valuable liquids -> "concentrate" mode: removed contamination is
        ///    discarded and the total volume drops (never turns product into water).
        /// Valuable product liquids are never touched in either mode.
        ///
        /// Returns the removed volume in µL; 0 means nothing could be removed.
        /// </summary>
        internal static int PurifyContaminants(GameItem container, GameItem machine)
        {
            PurifyAccounting accounting;
            return PurifyContaminants(container, machine, out accounting);
        }

        internal static int PurifyContaminants(GameItem container, GameItem machine, out PurifyAccounting accounting)
        {
            accounting = PurifyAccounting.Concentrate;
            if (container == null || !container.IsTag(Tags.LiquidContainer))
            {
                return 0;
            }
            if (container.IsTag(Tags.WineContainer))
            {
                return 0;
            }
            int total = WaterHelper.GetTotalVolume(container);
            if (total <= 0)
            {
                return 0;
            }
            accounting = IsWaterAndContaminantsOnly(container) ? PurifyAccounting.CleanWater : PurifyAccounting.Concentrate;

            int purity = WaterHelper.GetWaterPurity(container);
            int cap = GetPurityCap(machine);
            if (purity >= cap)
            {
                return 0;
            }

            int contaminant = GetContaminantAmount(container);
            if (contaminant <= 0)
            {
                return 0;
            }

            long removableByCap = ComputeRemovalTargetUl(container, cap, accounting, contaminant);
            if (removableByCap <= 0)
            {
                return 0;
            }

            long budget = GetCycleBudgetUl(container, machine);
            if (budget <= 0)
            {
                return 0;
            }
            long remove = Math.Min(Math.Min(removableByCap, (long)contaminant), budget);
            if (remove <= 0)
            {
                return 0;
            }

            // Spread the removal proportionally across all contaminant species so that
            // physical, chemical and biological contamination all go down together.
            long[] share = new long[AllContaminants.Length];
            long[] remainder = new long[AllContaminants.Length];
            long assigned = 0L;
            int[] present = new int[AllContaminants.Length];
            int presentSum = 0;
            for (int i = 0; i < AllContaminants.Length; i++)
            {
                present[i] = GetLiquidAmount(container, AllContaminants[i]);
                presentSum += present[i];
            }
            if (presentSum <= 0)
            {
                return 0;
            }
            for (int i = 0; i < present.Length; i++)
            {
                if (present[i] <= 0)
                {
                    continue;
                }
                long product = (long)present[i] * remove;
                share[i] = product / presentSum;
                remainder[i] = product % presentSum;
                assigned += share[i];
            }
            long leftover = remove - assigned;
            // Hand out rounding leftovers to the largest remainders first (and never exceed
            // what is actually present).
            while (leftover > 0)
            {
                int best = -1;
                for (int i = 0; i < remainder.Length; i++)
                {
                    if (present[i] <= 0 || share[i] >= present[i] || remainder[i] <= 0)
                    {
                        continue;
                    }
                    if (best < 0 || remainder[i] > remainder[best])
                    {
                        best = i;
                    }
                }
                if (best < 0)
                {
                    break;
                }
                share[best]++;
                remainder[best] = 0;
                leftover--;
            }

            int removed = 0;
            for (int i = 0; i < AllContaminants.Length; i++)
            {
                if (share[i] <= 0)
                {
                    continue;
                }
                Liquid liquid = GetLiquid(AllContaminants[i]);
                if (liquid == null)
                {
                    continue;
                }
                int have = container.GetTagReadonly(liquid.currentPart).GetInt();
                int delta = (int)Math.Min(share[i], (long)have);
                if (delta <= 0)
                {
                    continue;
                }
                int newAmount = have - delta;
                container.ModifyTag(liquid.currentPart, delegate (TagState state)
                {
                    state.Enable().SetInt(newAmount);
                }, true);
                removed += delta;
            }

            if (removed <= 0)
            {
                return 0;
            }

            if (accounting == PurifyAccounting.CleanWater)
            {
                Liquid water = GetLiquid("water");
                if (water != null)
                {
                    int waterNow = container.GetTagReadonly(water.currentPart).GetInt();
                    int newWater = waterNow + removed;
                    container.ModifyTag(water.currentPart, delegate (TagState state)
                    {
                        state.Enable().SetInt(newWater);
                    }, true);
                }
            }

            // Refresh everything the game would refresh after a normal liquid change:
            // unitValue (GetWaterPrice), the water-purity feature/label, empty-state handling,
            // the container sprite/colour and the "last scan" tooltip line.
            if (!ItemFactory.SuppressUiRefresh)
            {
                try
                {
                    container.ValidateShapeState();
                    WaterFeatureHelper.OnLiquidContainerPourOutOf(container);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[PrecisionLab] liquid UI refresh after purification failed: " + e.Message);
                }
                try
                {
                    WaterHelper.SetLastScanResult(container);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[PrecisionLab] last-scan refresh after purification failed: " + e.Message);
                }
            }
            return removed;
        }

        /// <summary>Formats a µL value as mL with up to 3 decimals (for tooltips).</summary>
        internal static string FormatUl(int volumeUl)
        {
            return (volumeUl / 1000.0).ToString("0.###");
        }

        // ------------------------------------------------------------------
        // 微生物培养机 / Microbial cultivator (sugar -> organic waste)
        // ------------------------------------------------------------------

        /// <summary>Base sugar processed per night at 100% culture activity (µL).</summary>
        internal const int CultivatorBaseThroughput = 2000;

        /// <summary>How much sugar one nightly cycle can digest (µL), given modules + activity.</summary>
        internal static int GetCultivatorThroughput(int performanceBonus, int activity)
        {
            double activityFactor = 0.25 + 0.75 * (Mathf.Clamp(activity, 0, 100) / 100.0);
            double performanceFactor = 1.0 + Mathf.Clamp(performanceBonus, 0, 300) / 100.0;
            return Math.Max(1, (int)Math.Round(CultivatorBaseThroughput * performanceFactor * activityFactor));
        }

        /// <summary>
        /// Digests sugar in the container: the sugar is consumed and a fraction of it becomes
        /// organic waste (the rest of the volume disappears — the microbes burn it). Quality
        /// modules reduce the leftover waste fraction. Returns the amount of sugar processed (µL).
        /// </summary>
        internal static int DigestSugar(GameItem container, int performanceBonus, int qualityBonus, int activity)
        {
            if (activity <= 0)
            {
                // Dormant culture: nothing is digested until it is fed nutrifruit again.
                return 0;
            }
            int sugar = GetLiquidAmount(container, "sugar");
            if (sugar <= 0)
            {
                return 0;
            }
            int capacity = GetCultivatorThroughput(performanceBonus, activity);
            int processed = Math.Min(sugar, capacity);
            if (processed <= 0)
            {
                return 0;
            }
            // 20% waste byproduct at quality 0, down to ~5% at high quality.
            double wasteRatio = Math.Max(0.05, 0.20 - 0.0015 * Mathf.Clamp(qualityBonus, 0, 200));
            int waste = (int)Math.Round(processed * wasteRatio);
            int existingWaste = GetLiquidAmount(container, "organic_waste");
            container.ModifyTag(Liquid.GetCurrentPartFromId("sugar"), delegate (TagState state)
            {
                state.Enable().SetInt(sugar - processed);
            }, skipValidate: true);
            container.ModifyTag(Liquid.GetCurrentPartFromId("organic_waste"), delegate (TagState state)
            {
                state.Enable().SetInt(existingWaste + waste);
            }, skipValidate: true);
            if (!ItemFactory.SuppressUiRefresh)
            {
                try
                {
                    ItemFeature feature = WaterFeatureHelper.GetWaterFeature(container);
                    WaterFeatureHelper.OnLiquidChanged(container, feature.IsExposed());
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[PrecisionLab] cultivator liquid refresh failed: " + e.Message);
                }
                try
                {
                    WaterHelper.SetLastScanResult(container);
                }
                catch (Exception)
                {
                    // Item not attached to a live UI yet.
                }
            }
            return processed;
        }
    }
}
