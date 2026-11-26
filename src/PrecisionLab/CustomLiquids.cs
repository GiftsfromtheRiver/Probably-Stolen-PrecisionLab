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
        internal const string OrdnanceWaste = "ordnance_waste";
        internal const string AromaticExtract = "aromatic_extract";
        internal const string ActivatedFragrance = "activated_fragrance";
        internal const string NeuroConcentrate = "neuroactive_concentrate";
        internal const string CrudePheromone = "crude_pheromone";
        internal const string PurifiedPheromone = "purified_pheromone";
        internal const string PheromoneConcentrate = "pheromone_concentrate";
        internal const string PheromoneByproduct = "pheromone_waste";
        internal const string PheromoneWaste = "denatured_pheromone_waste";
        internal const string PhageCulture = "phage_culture";
        internal const string FungalExtract = "fungal_extract";
        internal const string SymbioticCulture = "symbiotic_culture";
        internal const string PureAntibioticApi = "pure_antibiotic_api";
        internal const string StemCell = "stem_cell";
        internal const string ActiveCellSuspension = "active_cell_suspension";
        internal const string EssenceConcentrate = "essence_concentrate";
        internal const string DeadCellWaste = "dead_cell_waste";
        internal const string AloePulp = "modified_aloe_pulp";
        internal const string AloeExtract = "modified_aloe_extract";
        internal const string MedicalHoney = "medical_honey";
        internal const string OintmentBase = "ointment_base";
        internal const string PurifiedOintmentBase = "purified_ointment_base";
        internal const string OintmentWaste = "ointment_waste";
        internal const string BelladonnaExtract = "belladonna_extract";
        internal const string BelladonnaCrude = "belladonna_crude";
        internal const string BelladonnaAcidic = "belladonna_acidic";
        internal const string BelladonnaAlkaloid = "belladonna_alkaloid";
        internal const string BelladonnaPure = "belladonna_high_purity";
        internal const string BelladonnaWaste = "belladonna_waste";
        internal const string BelladonnaPaste = "belladonna_paste";
        internal const string PituitaryWaste = "pituitary_waste";        internal const string Aromatic = "aromatic";
        internal const string AntidepressantIntermediate = "antidepressant_intermediate";
        internal const string AntidepressantSalt = "antidepressant_salt";
        internal const string AntidepressantConcentrate = "antidepressant_concentrate";
        internal const string AntidepressantApi = "antidepressant_api";
        internal const string Iodide = "potassium_iodide";
        internal const string IodinationMix = "iodination_mix";
        internal const string OrganicIodide = "organic_iodide";
        internal const string CrudeAmiodarone = "crude_amiodarone";
        internal const string AmiodaroneApi = "amiodarone_api";
        internal const string GlandExtract = "gland_extract";
        internal const string AcidicPituitary = "acidic_pituitary";
        internal const string VasopressinCrude = "vasopressin_crude";
        internal const string VasopressinPure = "vasopressin_pure";
        internal const string VasopressinSolution = "vasopressin_solution";
        internal const string AlkalineGland = "alkaline_gland";
        internal const string VasodilatorComplex = "vasodilator_complex";
        internal const string VasodilatorPure = "vasodilator_pure";
        internal const string VasodilatorSolution = "vasodilator_solution";
        internal const string ThrombinActive = "thrombin_active";
        internal const string ThrombinPrecipitate = "thrombin_precipitate";
        internal const string ThrombinPure = "thrombin_pure";
        internal const string CoagulantGel = "coagulant_gel";
        internal const string HeparinCrude = "heparin_crude";
        internal const string AnticoagulantCrude = "anticoagulant_crude";
        internal const string AnticoagulantActive = "anticoagulant_active";
        internal const string AnticoagulantSolution = "anticoagulant_solution"; // 肾上腺素药液
        internal const string AntibioticCultureBroad = "antibiotic_culture_broad";
        internal const string AntibioticCultureGramPos = "antibiotic_culture_gram_pos";
        internal const string AntibioticCultureGramNeg = "antibiotic_culture_gram_neg";
        internal const string AntibioticConcentrateBroad = "antibiotic_concentrate_broad";
        internal const string AntibioticConcentrateGramPos = "antibiotic_concentrate_gram_pos";
        internal const string AntibioticConcentrateGramNeg = "antibiotic_concentrate_gram_neg";
        internal const string AmmoniaWater = "ammonia_water";
        internal const string SmellingSaltVolatile = "smelling_salt_volatile";
        internal const string ReedFiber = "reed_fiber";
        internal const string SmellingSaltWaste = "smelling_salt_waste";
        internal const string RadioactiveWaste = "radioactive_isotope_waste";
        internal const string IronCyanideComplex = "iron_cyanide_complex";
        internal const string IronCyanideComplexLow = "iron_cyanide_complex_low";
        internal const string SodCrude = "sod_crude";
        internal const string SodExtract = "sod_extract";
        internal const string TargetedMicrocapsule = "targeted_repair_microcapsule";
        internal const string AntiRadiationCrude = "anti_radiation_crude";
        internal const string AntiRadiationApi = "anti_radiation_api";
        internal const string NutritionEmulsion = "nutrition_emulsion";

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
                AddLiquid(AromaticExtract, "liquid.aromatic_extract", "Aromatic Extract", "f0c8d8", false);
                AddLiquid(ActivatedFragrance, "liquid.activated_fragrance", "Activated Fragrance", "f8d8e8", false);
                AddLiquid(NeuroConcentrate, "liquid.neuro_concentrate", "Neuroactive Concentrate", "f0e0f8", false);
                AddLiquid(CrudePheromone, "liquid.crude_pheromone", "Crude Pheromone", "d8c0a8", false);
                AddLiquid(PurifiedPheromone, "liquid.purified_pheromone", "Purified Pheromone", "e8d0b8", false);
                AddLiquid(PheromoneConcentrate, "liquid.pheromone_concentrate", "Pheromone Concentrate", "f0d8c0", false);
                AddLiquid(PheromoneByproduct, "liquid.pheromone_byproduct", "Pheromone Byproduct", "8a7a6a", true);
                AddLiquid(PheromoneWaste, "liquid.pheromone_waste", "Denatured Pheromone Waste", "6a5a5a", true);
                AddLiquid(PhageCulture, "liquid.phage_culture", "Bacteriophage Culture", "c8e8d8", false);
                AddLiquid(FungalExtract, "liquid.fungal_extract", "Fungal Extract", "d8c8a8", false);
                AddLiquid(SymbioticCulture, "liquid.symbiotic_culture", "Symbiotic Culture", "d8e8c0", false);
                AddLiquid(PureAntibioticApi, "liquid.pure_antibiotic_api", "Pure Antibiotic Powder Suspension", "f0f0e0", false);
                AddLiquid(StemCell, "liquid.stem_cell", "Stem Cell Stock", "f0d8e0", false);
                AddLiquid(ActiveCellSuspension, "liquid.active_cell_suspension", "Active Cell Suspension", "f8e0e8", false);
                AddLiquid(EssenceConcentrate, "liquid.essence_concentrate", "Eternal Essence Concentrate", "f8e8f0", false);
                AddLiquid(DeadCellWaste, "liquid.dead_cell_waste", "Dead Cell Waste", "7a6a6a", true);
                AddLiquid(AloePulp, "liquid.aloe_pulp", "Modified Aloe Pulp", "a8d878", false);
                AddLiquid(AloeExtract, "liquid.aloe_extract", "High-purity Aloe Extract", "b8e888", false);
                AddLiquid(MedicalHoney, "liquid.medical_honey", "Medical-grade Honey", "e8c858", false);
                AddLiquid(OintmentBase, "liquid.ointment_base", "Active Ointment Base", "e8d8b8", false);
                AddLiquid(PurifiedOintmentBase, "liquid.purified_ointment_base", "Purified Ointment Base", "f0e8d8", false);
                AddLiquid(OintmentWaste, "liquid.ointment_waste", "Denatured Ointment Waste", "8a7a6a", true);
                AddLiquid(BelladonnaExtract, "liquid.belladonna_extract", "Belladonna Herb Extract", "8a6aa8", false);
                AddLiquid(BelladonnaCrude, "liquid.belladonna_crude", "Crude Belladonna Extract", "6a4a8a", false);
                AddLiquid(BelladonnaAcidic, "liquid.belladonna_acidic", "Acidic Belladonna Solution", "9a6ac8", false);
                AddLiquid(BelladonnaAlkaloid, "liquid.belladonna_alkaloid", "Belladonna Alkaloid Precipitate", "c8a8e8", false);
                AddLiquid(BelladonnaPure, "liquid.belladonna_pure", "High-purity Belladonna Alkaloid", "e0d0f8", false);
                AddLiquid(BelladonnaWaste, "liquid.belladonna_waste", "Belladonna Waste", "5a5a5a", true);
                AddLiquid(BelladonnaPaste, "liquid.belladonna_paste", "Belladonna Paste", "6a4a8a", false);
                AddLiquid(PituitaryWaste, "liquid.pituitary_waste", "Denatured Pituitary Waste", "6a6a5a", false);
                AddLiquid(Aromatic, "liquid.aromatic", "Aromatic Compounds", "d8a848", false);
                AddLiquid(AntidepressantIntermediate, "liquid.antidepressant_intermediate", "Antidepressant Intermediate", "88b8d8", false);
                AddLiquid(AntidepressantSalt, "liquid.antidepressant_salt", "Antidepressant Salt Solution", "98c8e8", false);
                AddLiquid(AntidepressantConcentrate, "liquid.antidepressant_concentrate", "Antidepressant Concentrate", "b8d8f8", false);
                AddLiquid(AntidepressantApi, "liquid.antidepressant_api", "Antidepressant API", "e8e8f8", false);
                AddLiquid(Iodide, "liquid.potassium_iodide", "Potassium Iodide Solution", "c8c8e8", false);
                AddLiquid(IodinationMix, "liquid.iodination_mix", "Iodination Reaction Liquid", "a888c8", false);
                AddLiquid(OrganicIodide, "liquid.organic_iodide", "Organic Iodide Phase", "b898d8", false);
                AddLiquid(CrudeAmiodarone, "liquid.crude_amiodarone", "Crude Amiodarone", "c8a8e8", false);
                AddLiquid(AmiodaroneApi, "liquid.amiodarone_api", "Amiodarone API", "e8d8f8", false);
                AddLiquid(GlandExtract, "liquid.gland_extract", "Animal Gland Extract", "c88888", false);
                AddLiquid(AcidicPituitary, "liquid.acidic_pituitary", "Acidic Pituitary Extract", "d8a878", false);
                AddLiquid(VasopressinCrude, "liquid.vasopressin_crude", "Crude Vasopressin", "d8b898", false);
                AddLiquid(VasopressinPure, "liquid.vasopressin_pure", "High-purity Vasopressin Powder", "f0d8c8", false);
                AddLiquid(VasopressinSolution, "liquid.vasopressin_solution", "Vasopressin Solution", "f8e8d8", false);
                AddLiquid(AlkalineGland, "liquid.alkaline_gland", "Alkaline Gland Extract", "88c8a8", false);
                AddLiquid(VasodilatorComplex, "liquid.vasodilator_complex", "Vasodilator Complex", "a8d8b8", false);
                AddLiquid(VasodilatorPure, "liquid.vasodilator_pure", "High-purity Vasodilator", "c8e8d0", false);
                AddLiquid(VasodilatorSolution, "liquid.vasodilator_solution", "Vasodilator Solution", "e0f8e8", false);
                AddLiquid(ThrombinActive, "liquid.thrombin_active", "Active Thrombin Solution", "c8a878", false);
                AddLiquid(ThrombinPrecipitate, "liquid.thrombin_precipitate", "Thrombin Precipitate", "d8c898", false);
                AddLiquid(ThrombinPure, "liquid.thrombin_pure", "High-purity Thrombin Solution", "e8d8b8", false);
                AddLiquid(CoagulantGel, "liquid.coagulant_gel", "Coagulant Gel", "f0e8c8", false);
                AddLiquid(HeparinCrude, "liquid.heparin_crude", "Crude Heparin Extract", "88a8c8", false);
                AddLiquid(AnticoagulantCrude, "liquid.anticoagulant_crude", "Crude Anticoagulant", "98b8d8", false);
                AddLiquid(AnticoagulantActive, "liquid.anticoagulant_active", "Active Anticoagulant", "a8c8e8", false);
                AddLiquid(AnticoagulantSolution, "liquid.anticoagulant_solution", "Anticoagulant Solution", "c8e0f8", false);
                AddLiquid(AmmoniaWater, "liquid.ammonia_water", "Ammonia Water", "a8d8e8", false);
                AddLiquid(SmellingSaltVolatile, "liquid.smelling_salt_volatile", "Smelling Salts Volatile Liquid", "d8f0e8", false);
                AddLiquid(ReedFiber, "liquid.reed_fiber", "Hydroreed Fiber Slurry", "b8c898", false);
                AddLiquid(SmellingSaltWaste, "liquid.smelling_salt_waste", "Ammonia Wastewater", "7a8a8a", true);
                                AddLiquid(AntibioticCultureBroad, "liquid.antibiotic_culture_broad", "Broad-spectrum Antibiotic Culture", "b8d8e8", false);
                AddLiquid(AntibioticCultureGramPos, "liquid.antibiotic_culture_gram_pos", "Gram-positive Antibiotic Culture", "e8b8d8", false);
                AddLiquid(AntibioticCultureGramNeg, "liquid.antibiotic_culture_gram_neg", "Gram-negative Antibiotic Culture", "b8e8c8", false);
                AddLiquid(AntibioticConcentrateBroad, "liquid.antibiotic_concentrate_broad", "Concentrated Broad-spectrum Antibiotic", "6aa8d8", false);
                AddLiquid(AntibioticConcentrateGramPos, "liquid.antibiotic_concentrate_gram_pos", "Concentrated Gram-positive Antibiotic", "c878b8", false);
                AddLiquid(AntibioticConcentrateGramNeg, "liquid.antibiotic_concentrate_gram_neg", "Concentrated Gram-negative Antibiotic", "68c888", false);
                AddLiquid(RadioactiveWaste, "liquid.radioactive_waste", "Radioactive Isotope Waste", "8ad868", false);
                AddLiquid(IronCyanideComplex, "liquid.iron_cyanide_complex", "Iron Cyanide Complex Solution", "5070c8", false);
                AddLiquid(IronCyanideComplexLow, "liquid.iron_cyanide_complex_low", "Low-purity Iron Cyanide Complex", "706090", false);
                AddLiquid(SodCrude, "liquid.sod_crude", "Crude SOD Extract", "c8e8a0", false);
                AddLiquid(SodExtract, "liquid.sod_extract", "High-purity SOD Extract", "a8e878", false);
                AddLiquid(TargetedMicrocapsule, "liquid.targeted_microcapsule", "Targeted Repair Microcapsule", "b8e8e8", false);
                AddLiquid(AntiRadiationCrude, "liquid.anti_radiation_crude", "Crude Anti-radiation Drug", "d8c870", false);
                AddLiquid(AntiRadiationApi, "liquid.anti_radiation_api", "Anti-radiation API", "e8e8b8", false);
                AddLiquid(NutritionEmulsion, "liquid.nutrition_emulsion", "Three-in-One Nutrition Emulsion", "f0ead8", false);
Debug.Log("[PrecisionLab] registered " + (Liquid.Liquids.Count) + " liquids (custom chain=" + 61 + ")");
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
                SetName(AromaticExtract, "liquid.aromatic_extract", "Aromatic Extract");
                SetName(ActivatedFragrance, "liquid.activated_fragrance", "Activated Fragrance");
                SetName(NeuroConcentrate, "liquid.neuro_concentrate", "Neuroactive Concentrate");
                SetName(CrudePheromone, "liquid.crude_pheromone", "Crude Pheromone");
                SetName(PurifiedPheromone, "liquid.purified_pheromone", "Purified Pheromone");
                SetName(PheromoneConcentrate, "liquid.pheromone_concentrate", "Pheromone Concentrate");
                SetName(PheromoneByproduct, "liquid.pheromone_byproduct", "Pheromone Byproduct");
                SetName(PheromoneWaste, "liquid.pheromone_waste", "Denatured Pheromone Waste");
                SetName(PhageCulture, "liquid.phage_culture", "Bacteriophage Culture");
                SetName(FungalExtract, "liquid.fungal_extract", "Fungal Extract");
                SetName(SymbioticCulture, "liquid.symbiotic_culture", "Symbiotic Culture");
                SetName(PureAntibioticApi, "liquid.pure_antibiotic_api", "Pure Antibiotic Powder Suspension");
                SetName(StemCell, "liquid.stem_cell", "Stem Cell Stock");
                SetName(ActiveCellSuspension, "liquid.active_cell_suspension", "Active Cell Suspension");
                SetName(EssenceConcentrate, "liquid.essence_concentrate", "Eternal Essence Concentrate");
                SetName(DeadCellWaste, "liquid.dead_cell_waste", "Dead Cell Waste");
                SetName(AloePulp, "liquid.aloe_pulp", "Modified Aloe Pulp");
                SetName(AloeExtract, "liquid.aloe_extract", "High-purity Aloe Extract");
                SetName(MedicalHoney, "liquid.medical_honey", "Medical-grade Honey");
                SetName(OintmentBase, "liquid.ointment_base", "Active Ointment Base");
                SetName(PurifiedOintmentBase, "liquid.purified_ointment_base", "Purified Ointment Base");
                SetName(OintmentWaste, "liquid.ointment_waste", "Denatured Ointment Waste");
                SetName(BelladonnaExtract, "liquid.belladonna_extract", "Belladonna Herb Extract");
                SetName(BelladonnaCrude, "liquid.belladonna_crude", "Crude Belladonna Extract");
                SetName(BelladonnaAcidic, "liquid.belladonna_acidic", "Acidic Belladonna Solution");
                SetName(BelladonnaAlkaloid, "liquid.belladonna_alkaloid", "Belladonna Alkaloid Precipitate");
                SetName(BelladonnaPure, "liquid.belladonna_pure", "High-purity Belladonna Alkaloid");
                SetName(BelladonnaWaste, "liquid.belladonna_waste", "Belladonna Waste");
                SetName(BelladonnaPaste, "liquid.belladonna_paste", "Belladonna Paste");
                SetName(PituitaryWaste, "liquid.pituitary_waste", "Denatured Pituitary Waste");
                SetName(Aromatic, "liquid.aromatic", "Aromatic Compounds");
                SetName(AntidepressantIntermediate, "liquid.antidepressant_intermediate", "Antidepressant Intermediate");
                SetName(AntidepressantSalt, "liquid.antidepressant_salt", "Antidepressant Salt Solution");
                SetName(AntidepressantConcentrate, "liquid.antidepressant_concentrate", "Antidepressant Concentrate");
                SetName(AntidepressantApi, "liquid.antidepressant_api", "Antidepressant API");
                SetName(Iodide, "liquid.potassium_iodide", "Potassium Iodide Solution");
                SetName(IodinationMix, "liquid.iodination_mix", "Iodination Reaction Liquid");
                SetName(OrganicIodide, "liquid.organic_iodide", "Organic Iodide Phase");
                SetName(CrudeAmiodarone, "liquid.crude_amiodarone", "Crude Amiodarone");
                SetName(AmiodaroneApi, "liquid.amiodarone_api", "Amiodarone API");
                SetName(GlandExtract, "liquid.gland_extract", "Animal Gland Extract");
                SetName(AcidicPituitary, "liquid.acidic_pituitary", "Acidic Pituitary Extract");
                SetName(VasopressinCrude, "liquid.vasopressin_crude", "Crude Vasopressin");
                SetName(VasopressinPure, "liquid.vasopressin_pure", "High-purity Vasopressin Powder");
                SetName(VasopressinSolution, "liquid.vasopressin_solution", "Vasopressin Solution");
                SetName(AlkalineGland, "liquid.alkaline_gland", "Alkaline Gland Extract");
                SetName(VasodilatorComplex, "liquid.vasodilator_complex", "Vasodilator Complex");
                SetName(VasodilatorPure, "liquid.vasodilator_pure", "High-purity Vasodilator");
                SetName(VasodilatorSolution, "liquid.vasodilator_solution", "Vasodilator Solution");
                SetName(ThrombinActive, "liquid.thrombin_active", "Active Thrombin Solution");
                SetName(ThrombinPrecipitate, "liquid.thrombin_precipitate", "Thrombin Precipitate");
                SetName(ThrombinPure, "liquid.thrombin_pure", "High-purity Thrombin Solution");
                SetName(CoagulantGel, "liquid.coagulant_gel", "Coagulant Gel");
                SetName(HeparinCrude, "liquid.heparin_crude", "Crude Heparin Extract");
                SetName(AnticoagulantCrude, "liquid.anticoagulant_crude", "Crude Anticoagulant");
                SetName(AnticoagulantActive, "liquid.anticoagulant_active", "Active Anticoagulant");
                SetName(AnticoagulantSolution, "liquid.anticoagulant_solution", "Anticoagulant Solution");
                SetName(AntibioticCultureBroad, "liquid.antibiotic_culture_broad", "Broad-spectrum Antibiotic Culture");
                SetName(AntibioticCultureGramPos, "liquid.antibiotic_culture_gram_pos", "Gram-positive Antibiotic Culture");
                SetName(AntibioticCultureGramNeg, "liquid.antibiotic_culture_gram_neg", "Gram-negative Antibiotic Culture");
                SetName(AntibioticConcentrateBroad, "liquid.antibiotic_concentrate_broad", "Concentrated Broad-spectrum Antibiotic");
                SetName(AntibioticConcentrateGramPos, "liquid.antibiotic_concentrate_gram_pos", "Concentrated Gram-positive Antibiotic");
                SetName(AntibioticConcentrateGramNeg, "liquid.antibiotic_concentrate_gram_neg", "Concentrated Gram-negative Antibiotic");
                SetName(AmmoniaWater, "liquid.ammonia_water", "Ammonia Water");
                SetName(SmellingSaltVolatile, "liquid.smelling_salt_volatile", "Smelling Salts Volatile Liquid");
                SetName(ReedFiber, "liquid.reed_fiber", "Hydroreed Fiber Slurry");
                SetName(SmellingSaltWaste, "liquid.smelling_salt_waste", "Ammonia Wastewater");
                SetName(RadioactiveWaste, "liquid.radioactive_waste", "Radioactive Isotope Waste");
                SetName(IronCyanideComplex, "liquid.iron_cyanide_complex", "Iron Cyanide Complex Solution");
                SetName(IronCyanideComplexLow, "liquid.iron_cyanide_complex_low", "Low-purity Iron Cyanide Complex");
                SetName(SodCrude, "liquid.sod_crude", "Crude SOD Extract");
                SetName(SodExtract, "liquid.sod_extract", "High-purity SOD Extract");
                SetName(TargetedMicrocapsule, "liquid.targeted_microcapsule", "Targeted Repair Microcapsule");
                SetName(AntiRadiationCrude, "liquid.anti_radiation_crude", "Crude Anti-radiation Drug");
                SetName(AntiRadiationApi, "liquid.anti_radiation_api", "Anti-radiation API");
                SetName(NutritionEmulsion, "liquid.nutrition_emulsion", "Three-in-One Nutrition Emulsion");
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
                || liquidId == AromaticExtract || liquidId == ActivatedFragrance || liquidId == NeuroConcentrate
                || liquidId == CrudePheromone || liquidId == PurifiedPheromone || liquidId == PheromoneConcentrate
                || liquidId == PheromoneByproduct || liquidId == PheromoneWaste
                || liquidId == PhageCulture || liquidId == FungalExtract || liquidId == SymbioticCulture
                || liquidId == PureAntibioticApi || liquidId == StemCell || liquidId == ActiveCellSuspension
                || liquidId == EssenceConcentrate || liquidId == DeadCellWaste
                || liquidId == AloePulp || liquidId == AloeExtract || liquidId == MedicalHoney
                || liquidId == OintmentBase || liquidId == PurifiedOintmentBase || liquidId == OintmentWaste
                || liquidId == BelladonnaExtract || liquidId == BelladonnaCrude || liquidId == BelladonnaAcidic
                || liquidId == BelladonnaAlkaloid || liquidId == BelladonnaPure || liquidId == BelladonnaWaste || liquidId == BelladonnaPaste || liquidId == PituitaryWaste
                || liquidId == Aromatic
                || liquidId == AntidepressantIntermediate
                || liquidId == AntidepressantSalt
                || liquidId == AntidepressantConcentrate
                || liquidId == AntidepressantApi
                || liquidId == Iodide
                || liquidId == IodinationMix
                || liquidId == OrganicIodide
                || liquidId == CrudeAmiodarone
                || liquidId == AmiodaroneApi
                || liquidId == GlandExtract
                || liquidId == AcidicPituitary
                || liquidId == VasopressinCrude
                || liquidId == VasopressinPure
                || liquidId == VasopressinSolution
                || liquidId == AlkalineGland
                || liquidId == VasodilatorComplex
                || liquidId == VasodilatorPure
                || liquidId == VasodilatorSolution
                || liquidId == ThrombinActive
                || liquidId == ThrombinPrecipitate
                || liquidId == ThrombinPure
                || liquidId == CoagulantGel
                || liquidId == HeparinCrude
                || liquidId == AnticoagulantCrude
                || liquidId == AnticoagulantActive
                || liquidId == AnticoagulantSolution
                || liquidId == Thebaine || liquidId == ContraceptiveApi || liquidId == SannaiApi
                || liquidId == RadioactiveWaste || liquidId == IronCyanideComplex
                || liquidId == IronCyanideComplexLow || liquidId == SodCrude || liquidId == SodExtract
                || liquidId == TargetedMicrocapsule || liquidId == AntiRadiationCrude || liquidId == AntiRadiationApi || liquidId == NutritionEmulsion;
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
