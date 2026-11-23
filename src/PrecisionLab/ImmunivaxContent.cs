using System;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Items/recipes for the Immunivax (免疫宁) line: the purchasable immune serum bottle and the
    /// Chem Finisher recipe that presses the API solution into the vanilla Immunivax injector.
    /// </summary>
    internal static class ImmunivaxContent
    {
        internal const string VanillaInjectorId = "large_purple_injector";
        internal const int InjectorVolume = 5000;

        internal static void EnsureRecipes()
        {
            try
            {
                if (!DirectoryMaster.Has<GameItem>(VanillaInjectorId))
                {
                    Debug.LogWarning("[PrecisionLab] immunivax: vanilla injector is missing");
                    return;
                }
                ChemFinisherRecipe recipe = ChemFinisherRecipe.GetRecipeById(VanillaInjectorId);
                if (recipe == null)
                {
                    recipe = new ChemFinisherRecipe { identifier = VanillaInjectorId };
                    ChemFinisherRecipe.chemFinisherRecipes.Add(recipe);
                }
                recipe.displayName = Loc.Get("item.immunivax_injector.recipe_name", "免疫宁注射器");
                recipe.requiredVolume = InjectorVolume;
                recipe.isRequireDry = true;
                recipe.activeIngredientId = CustomLiquids.ImmunoglobulinApi;
                Debug.Log("[PrecisionLab] immunivax chem finisher recipe registered");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] registering immunivax recipe failed: " + e.Message);
            }
        }

        internal static GameItem CreateSupplyBox()
        {
            GameItem item = ItemDirectory.CreateEmptyItem();
            ModSprites.Apply(item, SpriteKeys.ReagentKitImmunivax, "Items/items_material", "chemical2");
            ReagentKitVisuals.ForceKitShape(item);
            item.name = Loc.Get("item.immunivax_box.name", "免疫宁原料盒");
            item.shortDescription = Loc.Get(
                "item.immunivax_box.desc",
                "激活拆开得到一批免疫宁原料：免疫血清 20 毫升 ×2、酒精 20 毫升 ×2、水 10 毫升。");
            item.flavorText = Loc.Get("item.immunivax_box.flavor", "冷藏保存，别问里面是什么。");
            item.SetGameItemType("CHEMICAL_SUPPLIES");
            ReagentKitVisuals.MakeNonContraband(item);
            item.unitValue = 300L;
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
                        AdrenalineContent.CreateBottle("med_bottle_violet", CustomLiquids.ImmuneSerum, 20000, "item.immune_serum_bottle.name", "Immune Serum Bottle (20 mL)"),
                        AdrenalineContent.CreateBottle("med_bottle_violet", CustomLiquids.ImmuneSerum, 20000, "item.immune_serum_bottle.name", "Immune Serum Bottle (20 mL)"),
                        AdrenalineContent.CreateBottle("med_bottle_red", "alcohol", 20000, "item.alcohol_bottle.name", "Alcohol Bottle (20 mL)"),
                        AdrenalineContent.CreateBottle("med_bottle_red", "alcohol", 20000, "item.alcohol_bottle.name", "Alcohol Bottle (20 mL)"),
                        AdrenalineContent.CreateBottle("med_bottle_blue", "water", 10000, "item.water_bottle.name", "Water (10 mL)")
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
                    Debug.Log("[PrecisionLab] immunivax kit unpacked (" + placed + "/" + bottles.Length + " bottles)");
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[PrecisionLab] unpacking immunivax kit failed: " + e.Message);
                }
            };
            AudioHelper.InitPlasticContainer(item);
            ModHelper.SetSource(item, Loc.ModId, ModEntry.ModDisplayName);
            return item;
        }

        internal static GameItem CreateImmuneSerumBottle()
        {
            GameItem bottle = AdrenalineContent.CreateBottle(
                "med_bottle_violet",
                CustomLiquids.ImmuneSerum,
                20000,
                "item.immune_serum_bottle.name",
                "Immune Serum Bottle (20 mL)");
            if (bottle == null)
            {
                return null;
            }
            bottle.name = Loc.Get("item.immune_serum_bottle.name", "免疫血清瓶（20 毫升）");
            bottle.shortDescription = Loc.Get(
                "item.immune_serum_bottle.desc",
                "A full 20 mL vial of immune serum, the starting material for the Immunivax chain. It is purchased directly from the retired chemist.");
            bottle.flavorText = Loc.Get(
                "item.immune_serum_bottle.flavor",
                "Kept cold. Do not shake.");
            bottle.unitValue = 120L;
            ModHelper.SetSource(bottle, Loc.ModId, ModEntry.ModDisplayName);
            return bottle;
        }
    }
}
