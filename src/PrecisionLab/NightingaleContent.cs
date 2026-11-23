using System;
using UnityEngine;

namespace PrecisionLab
{
    internal static class NightingaleContent
    {
        internal const string LeafId = "modified_aloe_leaf";
        internal const string HoneyId = "medical_honey_bottle";
        internal const string SeedId = "nightingale_aloe_seed";
        internal const string PlantId = "nightingale_aloe_seed";
        internal const string FinalId = "salve";

        private static bool _plantRegistered;

        internal static void EnsurePlantRegistered()
        {
            if (_plantRegistered)
            {
                return;
            }
            _plantRegistered = true;
            try
            {
                // Same growth profile as vanilla Kotton (韧棉) plus edible.
                PlantRegistry.Add(new PlantDefinition(
                        PlantId,
                        Loc.Get("plant.nightingale_aloe.name", "Nightingale Aloe"),
                        LeafId,
                        32,
                        6)
                    .Nutriment(350, 150, 75)
                    .Contaminant(1.26)
                    .Water(176)
                    .ReturnsSeeds()
                    .Innate("bountiful", 3)
                    .Innate("thirsty", 2)
                    .Innate("frail", 2)
                    .Innate("slow_grower")
                    .Innate("edible")
                    .OnHarvest(HarvestNightingale));
                RegisterAloePasteTrait();
                Debug.Log("[PrecisionLab] nightingale aloe plant registered");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] registering nightingale aloe plant failed: " + e.Message);
            }
        }

        internal static void EnsureRecipes()
        {
            try
            {
                NightingaleChemistry.Register();
                if (DirectoryMaster.Has<GameItem>(FinalId) && ChemFinisherRecipe.GetRecipeById(FinalId) == null)
                {
                    ChemFinisherRecipe.chemFinisherRecipes.Add(new ChemFinisherRecipe
                    {
                        identifier = FinalId,
                        displayName = Loc.Get("item.nightingale_salve.name", "南丁格尔软膏"),
                        requiredVolume = 5000,
                        isRequireDry = false,
                        activeIngredientId = CustomLiquids.PurifiedOintmentBase
                    });
                }
                Debug.Log("[PrecisionLab] nightingale recipes registered");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] registering nightingale recipes failed: " + e.Message);
            }
        }


        private static void HarvestNightingale(PlantHarvestContext ctx)
        {
            try
            {
                // Base 6 leaves + 1 seed, but let the plant genetics work: bountiful/harvest
                // modifiers change ctx.harvestCount, and fertile/infertile change SEED_YIELD.
                int leaves = Math.Max(1, ctx.harvestCount);
                // Potent/weak change the active component amount in each leaf (vanilla traits use
                // +25%/-25% per rank); apply it explicitly because this is a custom harvest hook.
                int potent = PlantGeneticsHelper.GetTraitRank(ctx.seed, "potent");
                int weak = PlantGeneticsHelper.GetTraitRank(ctx.seed, "weak");
                double potency = Math.Max(0.1, 1.0 + 0.25 * potent - 0.25 * weak);
                string pulpPart = Liquid.GetCurrentPartFromId(CustomLiquids.AloePulp);
                for (int i = 0; i < leaves; i++)
                {
                    GameItem leaf = DirectoryMaster.Item(LeafId);
                    if (leaf == null) continue;
                    // Set a clean 1000-unit base instead of inheriting the factory's random
                    // 60-80% purity composition, so potency scaling is deterministic.
                    ChemicalHelper.RemoveAllLiquid(leaf);
                    int scaledPulp = Math.Max(1, (int)Math.Round(5000.0 * potency));
                    leaf.ModifyTag(pulpPart, delegate (TagState state) { state.Enable().SetInt(scaledPulp); }, skipValidate: true);
                    leaf.ModifyTag("ACTIVATE_INGREDIENT_TAG", delegate (TagState state)
                    {
                        state.Enable().SetString(CustomLiquids.AloePulp);
                    });
                    try { ChemicalProductHelper.GradeProduct(leaf); } catch (Exception) { }
                    ctx.Output(leaf);
                }
                // Fertile/infertile control seed copies; bountiful affects leaves only (not seeds).
                int fertileRank = PlantGeneticsHelper.GetTraitRank(ctx.seed, "fertile");
                int infertileRank = PlantGeneticsHelper.GetTraitRank(ctx.seed, "infertile");
                int seedCopies = Math.Max(1, 1 + (fertileRank > 0 ? 1 : 0) - (infertileRank > 0 ? 1 : 0));
                for (int i = 0; i < seedCopies; i++)
                {
                    GameItem seed = DirectoryMaster.Item(SeedId);
                    if (seed == null) continue;
                    PlantGeneticsHelper.InheritTraits(ctx.seed, seed);
                    // Bypass PlantHarvest.Output: it auto-expands own-seed items through
                    // OutputOwnSeed, which would multiply an already-counted seed number again.
                    if (ctx.output == null || GraphUtils.TryAcceptAll(ctx.output, seed) > 0)
                    {
                        GraphUtils.EmporiumTryAdd(seed);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] nightingale aloe harvest failed: " + e.Message);
            }
        }

        private static void RegisterAloePasteTrait()
        {
            try
            {
                if (PlantTraitRegistry.Get("production_modified_aloe_pulp") != null)
                {
                    return;
                }
                PlantTrait trait = new PlantTrait(
                        "production_modified_aloe_pulp",
                        Loc.Get("plant_trait.aloe_paste.name", "Aloe Paste Production"),
                        3,
                        Loc.Get("plant_trait.aloe_paste.desc", "Harvested produce contains pure modified aloe paste."))
                    .Named(
                        delegate { return Loc.Get("plant_trait.aloe_paste.name", "Aloe Paste Production"); },
                        delegate { return Loc.Get("plant_trait.aloe_paste.desc", "Harvested produce contains pure modified aloe paste."); })
                    .Infuses(CustomLiquids.AloePulp)
                    .Unlimited()
                    .AppliesToSeedProduce()
                    .Describe(delegate (int rank)
                    {
                        return Loc.Fmt("plant_trait.aloe_paste.effect", "每株收获物内部加入纯净芦荟糊（50 级约 2 毫升，单株最多 3 毫升）");
                    })
                    .OnHarvestItem(delegate (PlantTraitContext ctx)
                    {
                        try
                        {
                            if (ctx.item == null) return;
                            if (!ctx.item.IsTag("CHEM_ITEM_TAG"))
                            {
                                ChemicalHelper.InitChemItem(ctx.item, isBlendable: true);
                            }
                            int rank = Math.Max(1, ctx.rank);
                            // Rank 50 = 2 mL per plant, hard cap 3 mL.
                            int amount = (int)Math.Round(2000.0 * PlantGeneticsHelper.RankScale(rank)
                                / PlantGeneticsHelper.RankScale(50));
                            amount = Math.Max(1, Math.Min(amount, 3000));
                            ChemicalHelper.AddChemContentUL(ctx.item, CustomLiquids.AloePulp, amount);
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning("[PrecisionLab] aloe paste gene harvest failed: " + e.Message);
                        }
                    });
                PlantTraitRegistry.Add(trait);
                Debug.Log("[PrecisionLab] aloe paste production gene registered");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] registering aloe paste gene failed: " + e.Message);
            }
        }
        internal static GameItem CreateSupplyBox()
        {
            GameItem item = ItemDirectory.CreateEmptyItem();
            ModSprites.Apply(item, SpriteKeys.ReagentKitNightingale, "Items/items_material", "chemical2");
            ReagentKitVisuals.ForceKitShape(item);
            item.name = Loc.Get("item.nightingale_box.name", "Nightingale Supply Kit");
            item.shortDescription = Loc.Get("item.nightingale_box.desc",
                "Unpacks modified aloe leaves x2, medical honey, aloe seed, alcohol and stem cell stock.");
            item.flavorText = Loc.Get("item.nightingale_box.flavor", "From leaf to salve: all it needs is patience and a blender.");
            item.SetGameItemType("CHEMICAL_SUPPLIES");
            item.unitValue = 900L;
            item.canActivateSlotItemFunc = (GameItem self, GameInventory inventory, SlotMarker slot) => GeneralHelper.IsItemOwned(self);
            item.mayActivateSlotItemFunc = (GameItem self, GameInventory inventory, SlotMarker slot) => GeneralHelper.IsItemOwned(self);
            item.onActivateSlotItemFunc = delegate (GameItem self, GameInventory inventory, SlotMarker slot)
            {
                try
                {
                    GameItem[] items =
                    {
                        DirectoryMaster.Item(LeafId),
                        DirectoryMaster.Item(LeafId),
                        CreateHoneyBottle(),
                        DirectoryMaster.Item(SeedId),
                        AdrenalineContent.CreateBottle("med_bottle_red", "alcohol", 20000, "item.alcohol_bottle.name", "Alcohol Bottle"),
                        AdrenalineContent.CreateBottle("med_bottle_red", "alcohol", 20000, "item.alcohol_bottle.name", "Alcohol Bottle"),
                        AdrenalineContent.CreateBottle("med_bottle_blue", "base", 20000, "item.base_bottle.name", "Base Reagent Bottle"),
                        BioPharmaContent.CreateStemCellBottle()
                    };
                    for (int i = 0; i < items.Length; i++)
                    {
                        GameItem bottle = items[i];
                        if (bottle == null) continue;
                        GeneralHelper.SetItemOwned(bottle, true);
                        if (inventory != null) GraphUtils.EmporiumTryAdd(inventory, bottle);
                        else GraphUtils.EmporiumTryAdd(bottle);
                    }
                    self.Destroy();
                    Debug.Log("[PrecisionLab] nightingale supply box unpacked");
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[PrecisionLab] unpacking nightingale supply box failed: " + e.Message);
                }
            };
            AudioHelper.InitPlasticContainer(item);
            ModHelper.SetSource(item, Loc.ModId, ModEntry.ModDisplayName);
            return item;
        }

        internal static GameItem CreateLeaf()
        {
            GameItem leaf = ItemDirectory.CreateEmptyItem();
            ModSprites.Apply(leaf, SpriteKeys.ModifiedAloeLeafSprite, "Items/items_material", "chemical2");
            leaf.SetShape(new GridShapeBuilder(1, 1).SetDataFill(1).Build());
            leaf.name = Loc.Get("item.modified_aloe_leaf.name", "改性芦荟叶");
            leaf.shortDescription = Loc.Get("item.modified_aloe_leaf.desc",
                "基因改良芦荟的厚实叶片。可直接搅拌/溶解出芦荟浆，是南丁格尔软膏的原料。");
            leaf.SetGameItemType("HOUSEHOLD_GOOD");
            leaf.SetGameItemType("MEDICAL");
            leaf.unitValue = 40L;
            ChemicalHelper.InitChemItem(leaf, isBlendable: true, isSoluble: true);
            ChemicalHelper.RemoveAllLiquid(leaf);
            string leafPart = Liquid.GetCurrentPartFromId(CustomLiquids.AloePulp);
            leaf.ModifyTag(leafPart, delegate (TagState st) { st.Enable().SetInt(5000); }, skipValidate: true);
            leaf.ModifyTag("ACTIVATE_INGREDIENT_TAG", delegate (TagState st) { st.Enable().SetString(CustomLiquids.AloePulp); });
            ChemicalFeedbackHelper.InitChemicalIntentItem(leaf, ChemicalFeedbackHelper.ChemicalIntent.MEDICAL);
            ModHelper.SetSource(leaf, Loc.ModId, ModEntry.ModDisplayName);
            return leaf;
        }

        internal static GameItem CreateHoneyBottle()
        {
            GameItem bottle = AdrenalineContent.CreateBottle("med_bottle_orange", CustomLiquids.MedicalHoney, 20000,
                "item.medical_honey_bottle.name", "医用蜂蜜瓶（20 毫升）");
            if (bottle == null) return null;
            bottle.name = Loc.Get("item.medical_honey_bottle.name", "医用蜂蜜瓶（20 毫升）");
            bottle.shortDescription = Loc.Get("item.medical_honey_bottle.desc",
                "一整瓶 20 毫升医用级蜂蜜。与芦荟提取液、干细胞原液常温乳化。");
            bottle.unitValue = 260L;
            ModHelper.SetSource(bottle, Loc.ModId, ModEntry.ModDisplayName);
            return bottle;
        }

        internal static GameItem CreateSeed()
        {
            EnsurePlantRegistered();
            GameItem seed = ItemDirectory.CreateEmptyItem();
            ModSprites.Apply(seed, SpriteKeys.NightingaleAloeSeedSprite, "Items/items_material", "chemical2");
            seed.SetShape(new GridShapeBuilder(1, 1).SetDataFill(1).Build());
            seed.name = Loc.Get("item.nightingale_aloe_seed.name", "芦荟种子");
            seed.shortDescription = Loc.Get("item.nightingale_aloe_seed.desc",
                "基因改良芦荟种子，可放入原版水培机。生长特性类似韧棉，可食用，收获改性芦荟叶。");
            seed.unitValue = 60L;
            HydroponicHelper.InitSeedItem(seed, PlantId);
            // Be explicit: the hydroponic slot/machine checks SEED, and the tooltip labels are
            // driven by the game item types SEED / MODIFIED_SEED.
            seed.EnableTag("SEED_ITEM_TAG");
            seed.EnableTag("SEED");
            seed.SetType("SEED_ITEM_TAG");
            seed.SetGameItemType("SEED");
            seed.SetGameItemType("MODIFIED_SEED");
            ModHelper.SetSource(seed, Loc.ModId, ModEntry.ModDisplayName);
            return seed;
        }
    }
}
