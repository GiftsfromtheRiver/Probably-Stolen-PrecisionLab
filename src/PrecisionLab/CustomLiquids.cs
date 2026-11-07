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
        internal const string AdrenalineSolution = "adrenaline_solution";
        internal const string Thebaine = "thebaine";
        internal const string NaloxoneReductionIntermediate = "naloxone_reduction_intermediate";
        internal const string NaloxoneHydrolysisIntermediate = "naloxone_hydrolysis_intermediate";
        internal const string NaloxoneCrude = "naloxone_crude";
        internal const string NaloxoneHcl = "naloxone_hcl";
        internal const string NaloxoneSolution = "naloxone_solution";
        internal const string SteroidRaw = "steroid_raw";
        internal const string SteroidHydrolysate = "steroid_hydrolysate";
        internal const string SteroidIntermediate = "steroid_intermediate";
        internal const string ContraceptiveApi = "contraceptive_api";
        internal const string SannaiRaw = "sannai_raw";
        internal const string SannaiExtract = "sannai_extract";
        internal const string SannaiApi = "sannai_api";
        internal const string NeuralLigand = "neural_ligand";
        internal const string ActivatedLigand = "activated_ligand";
        internal const string SynaprestCrude = "synaprest_crude";
        internal const string SynaprestApi = "synaprest_api";
        internal const string ImmuneSerum = "immune_serum";
        internal const string ImmunoglobulinSuspension = "immunoglobulin_suspension";
        internal const string CrudeImmunoglobulin = "crude_immunoglobulin";
        internal const string ImmunoglobulinApi = "immunoglobulin_api";
        internal const string DenaturedWaste = "denatured_immunoglobulin_waste";
        internal const string BioGelBase = "bio_gel_base";
        internal const string AcidicGel = "acidic_gel";
        internal const string CrudeBlueGel = "crude_blue_gel";
        internal const string MedicalBlueGel = "medical_blue_gel";
        internal const string DenaturedBlueGelWaste = "denatured_blue_gel_waste";
        internal const string Oxidizer = "oxidizer";
        internal const string MetalPowder = "metal_powder";
        internal const string NitratingAcid = "nitrating_acid";
        internal const string FlashPowder = "flash_powder";
        internal const string SmokeComposition = "smoke_composition";
        internal const string NitroBase = "nitro_base";
        internal const string PlasticPutty = "plastic_explosive_putty";
        internal const string OrdnanceWaste = "ordnance_waste"; // 肾上腺素药液

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
                AddLiquid(Thebaine, "liquid.thebaine", "Thebaine", "e8d2a0", false);
                AddLiquid(NaloxoneReductionIntermediate, "liquid.naloxone_reduction_intermediate", "Naloxone Reduction Intermediate", "b8c8e8", false);
                AddLiquid(NaloxoneHydrolysisIntermediate, "liquid.naloxone_hydrolysis_intermediate", "Naloxone Hydrolysis Intermediate", "a8d8d0", false);
                AddLiquid(NaloxoneCrude, "liquid.naloxone_crude", "Naloxone Crude", "c8a8d8", false);
                AddLiquid(NaloxoneHcl, "liquid.naloxone_hcl", "Naloxone HCl", "dfeef5", false);
                AddLiquid(NaloxoneSolution, "liquid.naloxone_solution", "Naloxone Solution", "9be7f0", false);
                AddLiquid(SteroidRaw, "liquid.steroid_raw", "Steroid Raw Material", "e8d8c0", false);
                AddLiquid(SteroidHydrolysate, "liquid.steroid_hydrolysate", "Steroid Hydrolysate", "d8c8a8", false);
                AddLiquid(SteroidIntermediate, "liquid.steroid_intermediate", "Steroid Intermediate", "c8d8b8", false);
                AddLiquid(ContraceptiveApi, "liquid.contraceptive_api", "Contraceptive API Solution", "dfeef5", false);
                AddLiquid(SannaiRaw, "liquid.sannai_raw", "Sannai Raw Material", "e8d8c0", false);
                AddLiquid(SannaiExtract, "liquid.sannai_extract", "Sannai Extract", "d8b8d8", false);
                AddLiquid(SannaiApi, "liquid.sannai_api", "Sannai API Solution", "dfeef5", false);
                AddLiquid(NeuralLigand, "liquid.neural_ligand", "Neural Receptor Ligand", "e8d2a0", false);
                AddLiquid(ActivatedLigand, "liquid.activated_ligand", "Activated Ligand", "c8d8f8", false);
                AddLiquid(SynaprestCrude, "liquid.synaprest_crude", "Synaprest Crude", "b8a8c8", false);
                AddLiquid(SynaprestApi, "liquid.synaprest_api", "Synaprest API Solution", "dfeef5", false);
                AddLiquid(ImmuneSerum, "liquid.immune_serum", "Immune Serum", "f0d8a8", false);
                AddLiquid(ImmunoglobulinSuspension, "liquid.immunoglobulin_suspension", "Immunoglobulin Suspension", "e8e0c8", false);
                AddLiquid(CrudeImmunoglobulin, "liquid.crude_immunoglobulin", "Crude Immunoglobulin", "e8d8b0", false);
                AddLiquid(ImmunoglobulinApi, "liquid.immunoglobulin_api", "Immunivax API Solution", "e0f0f8", false);
                AddLiquid(DenaturedWaste, "liquid.denatured_waste", "Denatured Protein Waste", "8a7a6a", true);
                AddLiquid(BioGelBase, "liquid.bio_gel_base", "Bio-Gel Matrix", "c8e8f0", false);
                AddLiquid(AcidicGel, "liquid.acidic_gel", "Acidic Gel", "b0d8e8", false);
                AddLiquid(CrudeBlueGel, "liquid.crude_blue_gel", "Crude Blue Gel", "6ac8e8", false);
                AddLiquid(MedicalBlueGel, "liquid.medical_blue_gel", "Medical-Grade Blue Gel", "4ab8e0", false);
                AddLiquid(DenaturedBlueGelWaste, "liquid.denatured_blue_gel_waste", "Denatured Gel Waste", "6a6a7a", true);
                AddLiquid(Oxidizer, "liquid.oxidizer", "Oxidizer", "e8c060", false);
                AddLiquid(MetalPowder, "liquid.metal_powder", "Metal Powder", "b8b8c0", false);
                AddLiquid(NitratingAcid, "liquid.nitrating_acid", "Nitrating Acid", "d08060", false);
                AddLiquid(FlashPowder, "liquid.flash_powder", "Flash Composition", "f0e8a0", false);
                AddLiquid(SmokeComposition, "liquid.smoke_composition", "Smoke Composition", "c8c8c8", false);
                AddLiquid(NitroBase, "liquid.nitro_base", "Nitro Base", "e0b080", false);
                AddLiquid(PlasticPutty, "liquid.plastic_putty", "Plastic Explosive Putty", "d8c8b0", false);
                AddLiquid(OrdnanceWaste, "liquid.ordnance_waste", "Burnt Ordnance Waste", "5a5a4a", true);
                Debug.Log("[PrecisionLab] registered " + (Liquid.Liquids.Count) + " liquids (custom chain=" + 45 + ")");
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
                SetName(Thebaine, "liquid.thebaine", "Thebaine");
                SetName(NaloxoneReductionIntermediate, "liquid.naloxone_reduction_intermediate", "Naloxone Reduction Intermediate");
                SetName(NaloxoneHydrolysisIntermediate, "liquid.naloxone_hydrolysis_intermediate", "Naloxone Hydrolysis Intermediate");
                SetName(NaloxoneCrude, "liquid.naloxone_crude", "Naloxone Crude");
                SetName(NaloxoneHcl, "liquid.naloxone_hcl", "Naloxone HCl");
                SetName(NaloxoneSolution, "liquid.naloxone_solution", "Naloxone Solution");
                SetName(SteroidRaw, "liquid.steroid_raw", "Steroid Raw Material");
                SetName(SteroidHydrolysate, "liquid.steroid_hydrolysate", "Steroid Hydrolysate");
                SetName(SteroidIntermediate, "liquid.steroid_intermediate", "Steroid Intermediate");
                SetName(ContraceptiveApi, "liquid.contraceptive_api", "Contraceptive API Solution");
                SetName(SannaiRaw, "liquid.sannai_raw", "Sannai Raw Material");
                SetName(SannaiExtract, "liquid.sannai_extract", "Sannai Extract");
                SetName(SannaiApi, "liquid.sannai_api", "Sannai API Solution");
                SetName(NeuralLigand, "liquid.neural_ligand", "Neural Receptor Ligand");
                SetName(ActivatedLigand, "liquid.activated_ligand", "Activated Ligand");
                SetName(SynaprestCrude, "liquid.synaprest_crude", "Synaprest Crude");
                SetName(SynaprestApi, "liquid.synaprest_api", "Synaprest API Solution");
                SetName(ImmuneSerum, "liquid.immune_serum", "Immune Serum");
                SetName(ImmunoglobulinSuspension, "liquid.immunoglobulin_suspension", "Immunoglobulin Suspension");
                SetName(CrudeImmunoglobulin, "liquid.crude_immunoglobulin", "Crude Immunoglobulin");
                SetName(ImmunoglobulinApi, "liquid.immunoglobulin_api", "Immunivax API Solution");
                SetName(DenaturedWaste, "liquid.denatured_waste", "Denatured Protein Waste");
                SetName(BioGelBase, "liquid.bio_gel_base", "Bio-Gel Matrix");
                SetName(AcidicGel, "liquid.acidic_gel", "Acidic Gel");
                SetName(CrudeBlueGel, "liquid.crude_blue_gel", "Crude Blue Gel");
                SetName(MedicalBlueGel, "liquid.medical_blue_gel", "Medical-Grade Blue Gel");
                SetName(DenaturedBlueGelWaste, "liquid.denatured_blue_gel_waste", "Denatured Gel Waste");
                SetName(Oxidizer, "liquid.oxidizer", "Oxidizer");
                SetName(MetalPowder, "liquid.metal_powder", "Metal Powder");
                SetName(NitratingAcid, "liquid.nitrating_acid", "Nitrating Acid");
                SetName(FlashPowder, "liquid.flash_powder", "Flash Composition");
                SetName(SmokeComposition, "liquid.smoke_composition", "Smoke Composition");
                SetName(NitroBase, "liquid.nitro_base", "Nitro Base");
                SetName(PlasticPutty, "liquid.plastic_putty", "Plastic Explosive Putty");
                SetName(OrdnanceWaste, "liquid.ordnance_waste", "Burnt Ordnance Waste");
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
                || liquidId == AdrenalineFreebase || liquidId == AdrenalineHcl
                || liquidId.StartsWith("naloxone_", StringComparison.Ordinal)
                || liquidId.StartsWith("steroid_", StringComparison.Ordinal)
                || liquidId.StartsWith("sannai_", StringComparison.Ordinal)
                || liquidId == NeuralLigand || liquidId == ActivatedLigand
                || liquidId == SynaprestCrude || liquidId == SynaprestApi
                || liquidId == ImmuneSerum || liquidId == ImmunoglobulinSuspension
                || liquidId == CrudeImmunoglobulin || liquidId == ImmunoglobulinApi
                || liquidId == DenaturedWaste
                || liquidId == BioGelBase || liquidId == AcidicGel || liquidId == CrudeBlueGel
                || liquidId == MedicalBlueGel || liquidId == DenaturedBlueGelWaste
                || liquidId == Oxidizer || liquidId == MetalPowder || liquidId == NitratingAcid
                || liquidId == FlashPowder || liquidId == SmokeComposition || liquidId == NitroBase
                || liquidId == PlasticPutty || liquidId == OrdnanceWaste
                || liquidId == Thebaine || liquidId == ContraceptiveApi || liquidId == SannaiApi;
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
