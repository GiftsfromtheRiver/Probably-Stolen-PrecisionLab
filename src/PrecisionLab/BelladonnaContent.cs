using System;
using UnityEngine;

namespace PrecisionLab
{
    internal static class BelladonnaContent
    {
        internal const string SeedId = "belladonna_seed";
        internal const string PlantId = "belladonna_seed";
        internal const string HerbId = "belladonna_herb";
        internal const string InjectorId = "belladonna_injector";
        private static bool _plantRegistered;

        internal static void EnsurePlantRegistered()
        {
            if (_plantRegistered) return;
            _plantRegistered = true;
            try
            {
                PlantRegistry.Add(new PlantDefinition(PlantId, Loc.Get("plant.belladonna.name", "Belladonna"),
                        HerbId, 32, 6)
                    .Nutriment(350, 150, 75)
                    .Contaminant(1.26)
                    .Water(176)
                    .ReturnsSeeds()
                    .Innate("bountiful", 2)
                    .Innate("thirsty", 2)
                    .Innate("frail")
                    .Innate("slow_grower")
                    .OnHarvest(HarvestBelladonna));
                RegisterPasteTrait();
                Debug.Log("[PrecisionLab] belladonna plant registered");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] registering belladonna plant failed: " + e.Message);
            }
        }


        private static void RegisterPasteTrait()
        {
            try
            {
                if (PlantTraitRegistry.Get("production_belladonna_paste") != null) return;
                PlantTrait trait = new PlantTrait("production_belladonna_paste",
                        Loc.Get("plant_trait.belladonna_paste.name", "颠茄糊生成"), 3,
                        Loc.Get("plant_trait.belladonna_paste.desc", "收获物内部生成颠茄糊。"))
                    .Named(
                        delegate { return Loc.Get("plant_trait.belladonna_paste.name", "颠茄糊生成"); },
                        delegate { return Loc.Get("plant_trait.belladonna_paste.desc", "收获物内部生成颠茄糊。"); })
                    .Infuses(CustomLiquids.BelladonnaPaste)
                    .Unlimited()
                    .AppliesToSeedProduce()
                    .Describe(delegate (int rank) { return Loc.Fmt("plant_trait.belladonna_paste.effect", "每株收获物内部生成颠茄糊，50级约2毫升，上限3毫升。"); })
                    .OnHarvestItem(delegate (PlantTraitContext ctx)
                    {
                        try
                        {
                            if (ctx.item == null) return;
                            if (!ctx.item.IsTag("CHEM_ITEM_TAG")) ChemicalHelper.InitChemItem(ctx.item, isBlendable: true);
                            int amount = (int)Math.Round(2000.0 * PlantGeneticsHelper.RankScale(Math.Max(1, ctx.rank)) / PlantGeneticsHelper.RankScale(50));
                            amount = Math.Max(1, Math.Min(amount, 3000));
                            ChemicalHelper.AddChemContentUL(ctx.item, CustomLiquids.BelladonnaPaste, amount);
                        }
                        catch (Exception e) { Debug.LogWarning("[PrecisionLab] belladonna paste gene failed: " + e.Message); }
                    });
                PlantTraitRegistry.Add(trait);
                Debug.Log("[PrecisionLab] belladonna paste gene registered");
            }
            catch (Exception e) { Debug.LogWarning("[PrecisionLab] registering belladonna paste gene failed: " + e.Message); }
        }
        internal static void EnsureRecipes()
        {
            try
            {
                if (ChemFinisherRecipe.GetRecipeById(InjectorId) == null)
                {
                    ChemFinisherRecipe.chemFinisherRecipes.Add(new ChemFinisherRecipe
                    {
                        identifier = InjectorId,
                        displayName = Loc.Get("item.belladonna_injector.name", "颠茄注射器"),
                        requiredVolume = 5000,
                        isRequireDry = false,
                        activeIngredientId = CustomLiquids.BelladonnaPure
                    });
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] belladonna recipe failed: " + e.Message);
            }
        }

        private static void HarvestBelladonna(PlantHarvestContext ctx)
        {
            try
            {
                int leaves = Math.Max(1, ctx.harvestCount);
                for (int i = 0; i < leaves; i++) ctx.OutputById(HerbId, 1);
                int seedCopies = Math.Max(1, 1 + (PlantGeneticsHelper.GetTraitRank(ctx.seed, "fertile") > 0 ? 1 : 0)
                    - (PlantGeneticsHelper.GetTraitRank(ctx.seed, "infertile") > 0 ? 1 : 0));
                for (int i = 0; i < seedCopies; i++)
                {
                    GameItem seed = DirectoryMaster.Item(SeedId);
                    if (seed == null) continue;
                    PlantGeneticsHelper.InheritTraits(ctx.seed, seed);
                    if (ctx.output == null || GraphUtils.TryAcceptAll(ctx.output, seed) > 0)
                    {
                        GraphUtils.EmporiumTryAdd(seed);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] belladonna harvest failed: " + e.Message);
            }
        }

        internal static GameItem CreateHerb()
        {
            GameItem herb = ItemDirectory.CreateEmptyItem();
            ModSprites.Apply(herb, SpriteKeys.BelladonnaHerb, "Items/items_material", "chemical2");
            herb.SetShape(new GridShapeBuilder(1, 1).SetDataFill(1).Build());
            herb.name = Loc.Get("item.belladonna_herb.name", "颠茄草");
            herb.shortDescription = Loc.Get("item.belladonna_herb.desc",
                "颠茄的干燥草叶。可搅拌/溶解出颠茄糊，是颠茄注射器的原料。");
            herb.SetGameItemType("CHEMICAL_SUPPLIES");
            herb.unitValue = 25L;
            ChemicalHelper.InitChemItem(herb, isBlendable: true, isSoluble: true);
            ChemicalHelper.RemoveAllLiquid(herb);
            string herbPart = Liquid.GetCurrentPartFromId(CustomLiquids.BelladonnaPaste);
            herb.ModifyTag(herbPart, delegate (TagState st) { st.Enable().SetInt(5000); }, skipValidate: true);
            herb.ModifyTag("ACTIVATE_INGREDIENT_TAG", delegate (TagState st) { st.Enable().SetString(CustomLiquids.BelladonnaPaste); });
            ModHelper.SetSource(herb, Loc.ModId, ModEntry.ModDisplayName);
            return herb;
        }

        internal static GameItem CreateSeed()
        {
            EnsurePlantRegistered();
            GameItem seed = ItemDirectory.CreateEmptyItem();
            ModSprites.Apply(seed, SpriteKeys.BelladonnaSeed, "Items/items_material", "chemical2");
            seed.SetShape(new GridShapeBuilder(1, 1).SetDataFill(1).Build());
            seed.name = Loc.Get("item.belladonna_seed.name", "颠茄种子");
            seed.shortDescription = Loc.Get("item.belladonna_seed.desc",
                "可放入原版水培机。生长特性接近韧棉，收获颠茄草；子代继承基因。");
            seed.unitValue = 60L;
            HydroponicHelper.InitSeedItem(seed, PlantId);
            seed.EnableTag("SEED_ITEM_TAG");
            seed.EnableTag("SEED");
            seed.SetType("SEED_ITEM_TAG");
            seed.SetGameItemType("SEED");
            seed.SetGameItemType("MODIFIED_SEED");
            ModHelper.SetSource(seed, Loc.ModId, ModEntry.ModDisplayName);
            return seed;
        }

        internal static GameItem CreateInjector()
        {
            GameItem item = ItemDirectory.CreateEmptyItem();
            ModSprites.Apply(item, "injector_pen_belladonna", "Items/items_material", "chemical2");
            item.SetShape(new GridShapeBuilder(3, 1).SetDataFill(1).Build());
            item.name = Loc.Get("item.belladonna_injector.name", "颠茄注射器");
            item.shortDescription = Loc.Get("item.belladonna_injector.desc",
                "中级违禁医疗品。颠茄生物碱注射剂，兼具毒性与医疗用途；仅作为交易/医疗收藏品。");
            item.flavorText = Loc.Get("item.belladonna_injector.flavor",
                "星泉医疗的黑色安瓿。剂量是药还是毒，只取决于推入多少。");
            item.SetGameItemType("MEDICAL");
            item.SetGameItemType("POISON");
            item.EnableTag("ITEM_MEDICAL_TAG");
            item.EnableProficiency("MEDICAL_PROFICIENCY");
            item.unitValue = 523L;
            item.SetGameItemType("LUXURY_ITEM");
            item.EnableManufacturer("xingquan");
            ChemicalHelper.InitChemItem(item, isBlendable: true, isSoluble: true);
            ChemicalProductHelper.InitChemicalProduct(item, CustomLiquids.BelladonnaPure);
            AdrenalineContent.SeedChemicalComposition(item, CustomLiquids.BelladonnaPure, 5000);
            ContrabandHelper.InitContrabandItem(item, 2);
            AudioHelper.InitPlasticContainer(item);
            ModHelper.SetSource(item, Loc.ModId, ModEntry.ModDisplayName);
            return item;
        }
    }
}
