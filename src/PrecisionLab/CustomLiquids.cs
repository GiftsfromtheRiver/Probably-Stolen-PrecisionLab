using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Custom chemical liquids used by the adrenaline production chain.
    ///
    /// The game exposes Liquid.Liquids / Liquid.LiquidDict as public static containers, so a mod
    /// can append new liquids at boot. They must be registered BEFORE the first liquid container is
    /// created (container factories initialise one tag per entry of Liquid.Liquids); we do it from
    /// IMod.Init, which ModLoader runs before the gameplay scene is built.
    ///
    /// The intermediates are liquids (not items) so they fit the vanilla chemistry flow:
    /// pour / blend reagents into a container -> heat it on the heating plate -> evaporate solvents
    /// in the evaporator -> press the result into a solid product with the Chem Finisher.
    /// </summary>
    internal static class CustomLiquids
    {
        internal const string Catechol = "catechol";                    // 邻苯二酚
        internal const string HydroxymethylBase = "hydroxymethyl_base"; // N-羟甲基碱溶液
        internal const string ResolvingAgent = "resolving_agent";       // 拆分剂
        internal const string AminationIntermediate = "amination_intermediate"; // 胺化中间体
        internal const string RacemicAdrenaline = "racemic_adrenaline"; // 消旋肾上腺素
        internal const string LevoAdrenalineSalt = "levo_adrenaline_salt"; // 左旋肾上腺素盐
        internal const string DextroWaste = "dextro_waste";             // 右旋废料
        internal const string AdrenalineFreebase = "adrenaline_freebase"; // 游离肾上腺素碱
        internal const string AdrenalineHcl = "adrenaline_hcl";         // 盐酸肾上腺素（原料药成分）
        internal const string AdrenalineSolution = "adrenaline_solution"; // 肾上腺素药液

        private static bool _registered;

        internal static void EnsureRegistered()
        {
            if (_registered)
            {
                return;
            }
            _registered = true;
            try
            {
                AddLiquid(Catechol, "liquid.catechol", "Catechol", "c9d6e8", false);
                AddLiquid(HydroxymethylBase, "liquid.hydroxymethyl_base", "N-Hydroxymethyl Base Solution", "e8e0b0", false);
                AddLiquid(ResolvingAgent, "liquid.resolving_agent", "Chiral Resolving Agent", "bfe3d0", false);
                AddLiquid(AminationIntermediate, "liquid.amination_intermediate", "Amination Intermediate", "a9c7f2", false);
                AddLiquid(RacemicAdrenaline, "liquid.racemic_adrenaline", "Racemic Adrenaline", "e6a9c8", false);
                AddLiquid(LevoAdrenalineSalt, "liquid.levo_adrenaline_salt", "Levo-Adrenaline Salt", "d38fd0", false);
                AddLiquid(DextroWaste, "liquid.dextro_waste", "Dextro Waste", "8b4a4a", false);
                AddLiquid(AdrenalineFreebase, "liquid.adrenaline_freebase", "Adrenaline Free Base", "e8b46a", false);
                AddLiquid(AdrenalineHcl, "liquid.adrenaline_hcl", "Adrenaline HCl", "dfeef5", false);
                AddLiquid(AdrenalineSolution, "liquid.adrenaline_solution", "Adrenaline Solution", "9be7f0", false);
                Debug.Log("[PrecisionLab] registered " + (Liquid.Liquids.Count) + " liquids (custom chain=" + 10 + ")");
            }
            catch (Exception e)
            {
                Debug.LogError("[PrecisionLab] registering custom liquids failed: " + e);
            }
        }

        /// <summary>Refreshes the display names from the mod localization (assets are ready by then).</summary>
        internal static void RefreshLocalizedNames()
        {
            try
            {
                SetName(Catechol, "liquid.catechol", "Catechol");
                SetName(HydroxymethylBase, "liquid.hydroxymethyl_base", "N-Hydroxymethyl Base Solution");
                SetName(ResolvingAgent, "liquid.resolving_agent", "Chiral Resolving Agent");
                SetName(AminationIntermediate, "liquid.amination_intermediate", "Amination Intermediate");
                SetName(RacemicAdrenaline, "liquid.racemic_adrenaline", "Racemic Adrenaline");
                SetName(LevoAdrenalineSalt, "liquid.levo_adrenaline_salt", "Levo-Adrenaline Salt");
                SetName(DextroWaste, "liquid.dextro_waste", "Dextro Waste");
                SetName(AdrenalineFreebase, "liquid.adrenaline_freebase", "Adrenaline Free Base");
                SetName(AdrenalineHcl, "liquid.adrenaline_hcl", "Adrenaline HCl");
                SetName(AdrenalineSolution, "liquid.adrenaline_solution", "Adrenaline Solution");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] refreshing custom liquid names failed: " + e.Message);
            }
        }

        internal static bool IsCustom(string liquidId)
        {
            return !string.IsNullOrEmpty(liquidId) && liquidId.StartsWith("adrenaline_", StringComparison.Ordinal)
                || liquidId == Catechol || liquidId == HydroxymethylBase || liquidId == ResolvingAgent
                || liquidId == AminationIntermediate || liquidId == RacemicAdrenaline
                || liquidId == LevoAdrenalineSalt || liquidId == DextroWaste
                || liquidId == AdrenalineFreebase || liquidId == AdrenalineHcl;
        }

        private static void SetName(string id, string locKey, string fallback)
        {
            Liquid liquid;
            if (Liquid.LiquidDict != null && Liquid.LiquidDict.TryGetValue(id, out liquid) && liquid != null)
            {
                liquid.displayName = Loc.Get(locKey, fallback);
            }
        }

        private static void AddLiquid(string id, string locKey, string fallbackName, string color, bool evaporative)
        {
            if (Liquid.LiquidDict == null || Liquid.Liquids == null)
            {
                return;
            }
            if (Liquid.LiquidDict.ContainsKey(id))
            {
                return;
            }
            Liquid liquid = new Liquid();
            liquid.displayName = Loc.Get(locKey, fallbackName);
            liquid.identifier = id;
            liquid.part = "PART_" + id.ToUpperInvariant();
            liquid.currentPart = "CURRENT_PART_" + id.ToUpperInvariant();
            liquid.baseMaxPurityFromMachine = 10;
            liquid.basePurifierRemoveAmount = 4000;
            // Process chemicals must not be eaten by filters / the purifier: only the dedicated
            // dextro-waste collector in the purifier handles them.
            liquid.canBeRemovedByPurifier = false;
            liquid.particleSize = 10;
            liquid.color = color;
            liquid.colorMultiplier = 1;
            liquid.isEvaporative = evaporative;
            liquid.boilingPoint = evaporative ? 100.0 : 0.0;
            Liquid.Liquids.Add(liquid);
            Liquid.LiquidDict[id] = liquid;
        }
    }
}
