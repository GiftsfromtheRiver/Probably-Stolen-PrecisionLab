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
