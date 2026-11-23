using System;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Items and Chem Finisher recipes for the two perfume lines. Both final items are vanilla:
    /// neuroactive_perfume (5 uses, luxury) and pheromone_perfume (5 uses, luxury + contraband).
    /// </summary>
    internal static class PerfumeContent
    {
        internal const string NeuroPerfumeId = "neuroactive_perfume";
        internal const string PheromonePerfumeId = "pheromone_perfume";
        internal const int PressVolume = 5000;

        internal static void EnsureRecipes()
        {
            try
            {
                RegisterRecipe(NeuroPerfumeId, CustomLiquids.NeuroConcentrate,
                    "item.neuroactive_perfume.recipe_name", "神经活性香水「同步」");
                RegisterRecipe(PheromonePerfumeId, CustomLiquids.PheromoneConcentrate,
                    "item.pheromone_perfume.recipe_name", "费洛蒙香水「本源」");
                Debug.Log("[PrecisionLab] perfume chem finisher recipes registered");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] registering perfume recipes failed: " + e.Message);
            }
        }

        private static void RegisterRecipe(string identifier, string activeLiquid, string nameKey, string fallback)
        {
            if (!DirectoryMaster.Has<GameItem>(identifier))
            {
                Debug.LogWarning("[PrecisionLab] perfume: missing vanilla item " + identifier);
                return;
            }
            ChemFinisherRecipe recipe = ChemFinisherRecipe.GetRecipeById(identifier);
            if (recipe == null)
            {
                recipe = new ChemFinisherRecipe { identifier = identifier };
                ChemFinisherRecipe.chemFinisherRecipes.Add(recipe);
            }
            recipe.displayName = Loc.Get(nameKey, fallback);
            recipe.requiredVolume = PressVolume;
            // The filling explicitly includes alcohol, so the container is never "dry".
            recipe.isRequireDry = false;
            recipe.activeIngredientId = activeLiquid;
        }

        internal static GameItem CreateAromaticBottle()
        {
            GameItem bottle = AdrenalineContent.CreateBottle(
                "med_bottle_violet", CustomLiquids.AromaticExtract, 20000,
                "item.aromatic_bottle.name", "芳香萃取液瓶（20 毫升）");
            if (bottle == null) return null;
            bottle.name = Loc.Get("item.aromatic_bottle.name", "芳香萃取液瓶（20 毫升）");
            bottle.shortDescription = Loc.Get("item.aromatic_bottle.desc",
                "一整瓶 20 毫升芳香萃取液/植物精油，是神经活性香水的起始原料。");
            bottle.unitValue = 300L;
            ModHelper.SetSource(bottle, Loc.ModId, ModEntry.ModDisplayName);
            return bottle;
        }

        internal static GameItem CreateCrudePheromoneBottle()
        {
            GameItem bottle = AdrenalineContent.CreateBottle(
                "med_bottle_orange", CustomLiquids.CrudePheromone, 20000,
                "item.crude_pheromone_bottle.name", "粗制信息素瓶（20 毫升）");
            if (bottle == null) return null;
            bottle.name = Loc.Get("item.crude_pheromone_bottle.name", "粗制信息素瓶（20 毫升）");
            bottle.shortDescription = Loc.Get("item.crude_pheromone_bottle.desc",
                "一整瓶 20 毫升粗制信息素，黑市来源，极度怕热。");
            bottle.unitValue = 600L;
            ContrabandHelper.InitContrabandItem(bottle, 1);
            ModHelper.SetSource(bottle, Loc.ModId, ModEntry.ModDisplayName);
            return bottle;
        }

        internal static GameItem CreateSupplyBox()
        {
            GameItem item = ItemDirectory.CreateEmptyItem();
            ModSprites.Apply(item, SpriteKeys.ReagentKitPerfume, "Items/items_material", "chemical2");
            ReagentKitVisuals.ForceKitShape(item);
            item.name = Loc.Get("item.perfume_box.name", "香水原料盒");
            item.shortDescription = Loc.Get("item.perfume_box.desc",
                "激活拆开得到一批香水原料：芳香萃取液 20 毫升、粗制信息素 20 毫升、水 20 毫升、酸 5 毫升、酒精 20 毫升 ×2、N-羟甲基碱溶液 20 毫升、脂合剂 20 毫升。");
            item.flavorText = Loc.Get("item.perfume_box.flavor", "违禁品：含有信息素成分。一半是香气，一半是麻烦。");
            item.SetGameItemType("CHEMICAL_SUPPLIES");
            ContrabandHelper.InitContrabandItem(item, 2);
            item.unitValue = 2000L;
            item.canActivateSlotItemFunc = delegate (GameItem self, GameInventory inventory, SlotMarker slot)
            {
                return GeneralHelper.IsItemOwned(self);
            };
            item.mayActivateSlotItemFunc = delegate (GameItem self, GameInventory inventory, SlotMarker slot)
            {
                return GeneralHelper.IsItemOwned(self);
            };
            item.onActivateSlotItemFunc = delegate (GameItem self, GameInventory inventory, SlotMarker slot)
            {
                try
                {
                    GameItem[] bottles =
                    {
                        AdrenalineContent.CreateBottle("med_bottle_violet", CustomLiquids.AromaticExtract, 20000, "item.aromatic_bottle.name", "Aromatic Extract Bottle (20 mL)"),
                        AdrenalineContent.CreateBottle("med_bottle_orange", CustomLiquids.CrudePheromone, 20000, "item.crude_pheromone_bottle.name", "Crude Pheromone Bottle (20 mL)"),
                        AdrenalineContent.CreateBottle("med_bottle_blue", "water", 20000, "item.water_bottle.name", "Water (20 mL)"),
                        AdrenalineContent.CreateBottle("med_bottle_red", "acid", 5000, "item.acid_bottle.name", "Acid Reagent Bottle"),
                        AdrenalineContent.CreateBottle("med_bottle_red", "alcohol", 20000, "item.alcohol_bottle.name", "Alcohol Bottle (20 mL)"),
                        AdrenalineContent.CreateBottle("med_bottle_red", "alcohol", 20000, "item.alcohol_bottle.name", "Alcohol Bottle (20 mL)"),
                        AdrenalineContent.CreateBottle("med_bottle_violet", CustomLiquids.HydroxymethylBase, 20000, "item.hydroxymethyl_bottle.name", "N-Hydroxymethyl Base Solution"),
                        AdrenalineContent.CreateBottle("med_bottle_small_pink", "lipobond_agent", 20000, "item.lipobond_bottle.name", "Lipobond Agent Bottle")
                    };
                    int placed = 0;
                    for (int i = 0; i < bottles.Length; i++)
                    {
                        GameItem bottle = bottles[i];
                        if (bottle == null) continue;
                        GeneralHelper.SetItemOwned(bottle, true);
                        if (inventory != null) GraphUtils.EmporiumTryAdd(inventory, bottle);
                        else GraphUtils.EmporiumTryAdd(bottle);
                        if (bottle.parentInventory != null) placed++;
                    }
                    self.Destroy();
                    Debug.Log("[PrecisionLab] perfume kit unpacked (" + placed + "/" + bottles.Length + " bottles)");
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[PrecisionLab] unpacking perfume kit failed: " + e.Message);
                }
            };
            AudioHelper.InitPlasticContainer(item);
            ModHelper.SetSource(item, Loc.ModId, ModEntry.ModDisplayName);
            return item;
        }
    }
}
