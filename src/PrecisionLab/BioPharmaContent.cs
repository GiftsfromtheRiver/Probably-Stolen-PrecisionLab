using System;
using UnityEngine;

namespace PrecisionLab
{
    internal static class BioPharmaContent
    {
        internal const string UnlicensedPillId = "unlicensed_phagimycin_pill";
        internal const string LicensedPillId = "phagimycin_pill";
        internal const string EssenceId = "skincare_cream";
        internal const int PhagimycinPressVolume = 1000;
        internal const int EssencePressVolume = 5000;

        internal static void EnsureRecipes()
        {
            try
            {
                RegisterRecipe(UnlicensedPillId, CustomLiquids.PureAntibioticApi,
                    PhagimycinPressVolume, "item.phagimycin.recipe_name", "噬菌霉素片");
                RegisterRecipe(EssenceId, CustomLiquids.EssenceConcentrate,
                    EssencePressVolume, "item.eternal.recipe_name", "永恒焕活精华液");
                Debug.Log("[PrecisionLab] bio-pharma recipes registered");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] registering bio-pharma recipes failed: " + e.Message);
            }
        }

        private static void RegisterRecipe(string id, string active, int volume, string nameKey, string fallback)
        {
            if (!DirectoryMaster.Has<GameItem>(id))
            {
                Debug.LogWarning("[PrecisionLab] bio-pharma missing vanilla item " + id);
                return;
            }
            ChemFinisherRecipe recipe = ChemFinisherRecipe.GetRecipeById(id);
            if (recipe == null)
            {
                recipe = new ChemFinisherRecipe { identifier = id };
                ChemFinisherRecipe.chemFinisherRecipes.Add(recipe);
            }
            recipe.displayName = Loc.Get(nameKey, fallback);
            recipe.requiredVolume = volume;
            // Both intermediates are liquid suspensions, so the container is always "wet".
            recipe.isRequireDry = false;
            recipe.activeIngredientId = active;
        }

        internal static GameItem CreateSupplyBox()
        {
            GameItem item = ItemDirectory.CreateEmptyItem();
            ModSprites.Apply(item, SpriteKeys.ReagentKitBioPharma, "Items/items_material", "chemical2");
            ReagentKitVisuals.ForceKitShape(item);
            item.name = Loc.Get("item.biopharma_box.name", "生物制药原料盒");
            item.shortDescription = Loc.Get("item.biopharma_box.desc",
                "激活拆开得到一批生物制药原料：噬菌体培养液 20 毫升、真菌提取物 20 毫升、水 20 毫升 ×2、干细胞原液 20 毫升 ×2、酒精 20 毫升、脂合剂 20 毫升。");
            item.flavorText = Loc.Get("item.biopharma_box.flavor", "冷藏运输。里面有活的，也有不能算活的。");
            item.SetGameItemType("CHEMICAL_SUPPLIES");
            ReagentKitVisuals.MakeNonContraband(item);
            item.unitValue = 1500L;
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
                        AdrenalineContent.CreateBottle("med_bottle_blue", CustomLiquids.PhageCulture, 20000, "item.phage_culture_bottle.name", "Bacteriophage Culture Bottle (20 mL)"),
                        AdrenalineContent.CreateBottle("med_bottle_orange", CustomLiquids.FungalExtract, 20000, "item.fungal_extract_bottle.name", "Fungal Extract Bottle (20 mL)"),
                        AdrenalineContent.CreateBottle("med_bottle_blue", "water", 20000, "item.water_bottle.name", "Water (20 mL)"),
                        AdrenalineContent.CreateBottle("med_bottle_blue", "water", 20000, "item.water_bottle.name", "Water (20 mL)"),
                        AdrenalineContent.CreateBottle("med_bottle_violet", CustomLiquids.StemCell, 20000, "item.stem_cell_bottle.name", "Stem Cell Stock Bottle (20 mL)"),
                        AdrenalineContent.CreateBottle("med_bottle_violet", CustomLiquids.StemCell, 20000, "item.stem_cell_bottle.name", "Stem Cell Stock Bottle (20 mL)"),
                        AdrenalineContent.CreateBottle("med_bottle_red", "alcohol", 20000, "item.alcohol_bottle.name", "Alcohol Bottle (20 mL)"),
                        AdrenalineContent.CreateBottle("med_bottle_small_pink", "lipobond_agent", 20000, "item.lipobond_bottle.name", "Lipobond Agent Bottle (20 mL)")
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
                    Debug.Log("[PrecisionLab] bio-pharma kit unpacked (" + placed + "/" + bottles.Length + " bottles)");
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[PrecisionLab] unpacking bio-pharma kit failed: " + e.Message);
                }
            };
            AudioHelper.InitPlasticContainer(item);
            ModHelper.SetSource(item, Loc.ModId, ModEntry.ModDisplayName);
            return item;
        }

        internal static GameItem CreatePhageCultureBottle()
        {
            return CreateBottle("med_bottle_blue", CustomLiquids.PhageCulture, 20000,
                "item.phage_culture_bottle.name", "噬菌体培养液瓶（20 毫升）",
                "item.phage_culture_bottle.desc", "一整瓶 20 毫升噬菌体培养液，噬菌霉素的起始原料。", 250L);
        }

        internal static GameItem CreateFungalExtractBottle()
        {
            return CreateBottle("med_bottle_orange", CustomLiquids.FungalExtract, 20000,
                "item.fungal_extract_bottle.name", "真菌提取物瓶（20 毫升）",
                "item.fungal_extract_bottle.desc", "一整瓶 20 毫升真菌提取物，与噬菌体共生培养。", 200L);
        }

        internal static GameItem CreateStemCellBottle()
        {
            return CreateBottle("med_bottle_violet", CustomLiquids.StemCell, 20000,
                "item.stem_cell_bottle.name", "干细胞原液瓶（20 毫升）",
                "item.stem_cell_bottle.desc", "一整瓶 20 毫升干细胞原液，必须低温保存。", 350L);
        }

        private static GameItem CreateBottle(string sprite, string liquid, int amount, string nameKey,
            string nameFallback, string descKey, string descFallback, long value)
        {
            GameItem bottle = AdrenalineContent.CreateBottle(sprite, liquid, amount, nameKey, nameFallback);
            if (bottle == null) return null;
            bottle.name = Loc.Get(nameKey, nameFallback);
            bottle.shortDescription = Loc.Get(descKey, descFallback);
            bottle.unitValue = value;
            ModHelper.SetSource(bottle, Loc.ModId, ModEntry.ModDisplayName);
            return bottle;
        }
    }
}
