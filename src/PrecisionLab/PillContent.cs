using System;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Contents for the two small pill lines: precursor kits and Chem Finisher recipes that press
    /// the final API solutions into the vanilla birth-control pill and the vanilla Zyanide
    /// (Sannai) poison pill. Both lines are part of the existing "More Medicines" unlock.
    /// </summary>
    internal static class PillContent
    {
        internal const string BirthControlPillId = "birth_control_pill";
        internal const string SannaiPillId = "zyanide_pill";

        internal const int PressVolume = 5000;

        internal static void EnsureRecipes()
        {
            try
            {
                // Vanilla already ships chem-finisher recipes for these two pills (using the vanilla
                // liquid ids). Override them in place so the mod's liquid chain feeds the press.
                if (DirectoryMaster.Has<GameItem>(BirthControlPillId))
                {
                    ChemFinisherRecipe recipe = ChemFinisherRecipe.GetRecipeById(BirthControlPillId);
                    if (recipe == null)
                    {
                        recipe = new ChemFinisherRecipe { identifier = BirthControlPillId };
                        ChemFinisherRecipe.chemFinisherRecipes.Add(recipe);
                    }
                    recipe.displayName = Loc.Get("item.birth_control_pill.recipe_name", "Birth Control Pill");
                    recipe.requiredVolume = PressVolume;
                    recipe.isRequireDry = true;
                    recipe.activeIngredientId = "contraceptive";
                }
                else
                {
                    Debug.LogWarning("[PrecisionLab] birth control pill item is missing");
                }
                if (DirectoryMaster.Has<GameItem>(SannaiPillId))
                {
                    ChemFinisherRecipe recipe2 = ChemFinisherRecipe.GetRecipeById(SannaiPillId);
                    if (recipe2 == null)
                    {
                        recipe2 = new ChemFinisherRecipe { identifier = SannaiPillId };
                        ChemFinisherRecipe.chemFinisherRecipes.Add(recipe2);
                    }
                    recipe2.displayName = Loc.Get("item.sannai_pill.recipe_name", "Zyanide Pill (Sannai)");
                    recipe2.requiredVolume = PressVolume;
                    recipe2.isRequireDry = true;
                    recipe2.activeIngredientId = "zyanide";
                }
                else
                {
                    Debug.LogWarning("[PrecisionLab] zyanide pill item is missing");
                }
                Debug.Log("[PrecisionLab] pill chem finisher recipes registered");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] registering pill recipes failed: " + e.Message);
            }
        }

        // ------------------------------------------------------------------
        // Precursor kits
        // ------------------------------------------------------------------

        internal static GameItem CreateBirthControlBox()
        {
            return CreateBox(
                "item.birth_control_box.name",
                "Birth Control Precursor Kit",
                "item.birth_control_box.desc",
                "Activate to unpack one contraceptive batch: steroid raw material, acid, water, alcohol, reducing agent and lipobond agent (20 mL each).",
                "item.birth_control_box.flavor",
                "A sealed precursor kit for the contraceptive line.",
                new[]
                {
                    Bottle("med_bottle_violet", CustomLiquids.SteroidRaw, 20000, "item.steroid_bottle.name", "Steroid Raw Material Bottle"),
                    Bottle("med_bottle_red", "acid", 20000, "item.acid_bottle.name", "Acid Reagent Bottle"),
                    Bottle("med_bottle_blue", "water", 20000, "item.water_bottle.name", "Water Bottle"),
                    Bottle("med_bottle_blue", "alcohol", 20000, "item.alcohol_bottle.name", "Alcohol Reagent Bottle"),
                    Bottle("med_bottle_orange", "reducing_agent", 20000, "item.reducing_bottle.name", "Reducing Agent Bottle"),
                    Bottle("med_bottle_small_pink", "lipobond_agent", 20000, "item.lipobond_bottle.name", "Lipobond Agent Bottle")
                });
        }

        internal static GameItem CreateSannaiBox()
        {
            return CreateBox(
                "item.sannai_box.name",
                "Sannai Precursor Kit",
                "item.sannai_box.desc",
                "Activate to unpack one Sannai batch: Sannai raw material (20 mL), alcohol (2 x 20 mL) and a small acid bottle (10 mL).",
                "item.sannai_box.flavor",
                "A sealed precursor kit for the Sannai line. Handle with gloves.",
                new[]
                {
                    Bottle("med_bottle_violet", CustomLiquids.SannaiRaw, 20000, "item.sannai_bottle.name", "Sannai Raw Material Bottle"),
                    Bottle("med_bottle_blue", "alcohol", 20000, "item.alcohol_bottle.name", "Alcohol Reagent Bottle"),
                    Bottle("med_bottle_blue", "alcohol", 20000, "item.alcohol_bottle.name", "Alcohol Reagent Bottle"),
                    Bottle("med_bottle_red", "acid", 10000, "item.acid_bottle.name", "Acid Reagent Bottle")
                });
        }

        private static GameItem Bottle(string containerId, string liquidId, int amount, string locKey, string fallback)
        {
            return AdrenalineContent.CreateBottle(containerId, liquidId, amount, locKey, fallback);
        }

        private static GameItem CreateBox(string nameKey, string nameFallback, string descKey, string descFallback, string flavorKey, string flavorFallback, GameItem[] bottles)
        {
            GameItem item = ItemDirectory.CreateEmptyItem();
            string spriteKey = nameKey.Contains("birth_control") ? SpriteKeys.ReagentKitBirthControl : SpriteKeys.ReagentKitSannai;
            ModSprites.Apply(item, spriteKey, "Items/items_material", "chemical2");
            ReagentKitVisuals.ForceKitShape(item);
            item.name = Loc.Get(nameKey, nameFallback);
            item.shortDescription = Loc.Get(descKey, descFallback);
            item.flavorText = Loc.Get(flavorKey, flavorFallback);
            item.SetGameItemType("CHEMICAL_SUPPLIES");
            ReagentKitVisuals.MakeNonContraband(item);
            item.unitValue = 120L;
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
                Unpack(self, inventory, bottles);
            };
            AudioHelper.InitPlasticContainer(item);
            ModHelper.SetSource(item, Loc.ModId, ModEntry.ModDisplayName);
            return item;
        }

        private static void Unpack(GameItem box, GameInventory inventory, GameItem[] bottles)
        {
            try
            {
                int placed = 0;
                for (int i = 0; i < bottles.Length; i++)
                {
                    GameItem bottle = bottles[i];
                    if (bottle == null)
                    {
                        continue;
                    }
                    GeneralHelper.SetItemOwned(bottle, true);
                    if (inventory != null)
                    {
                        GraphUtils.EmporiumTryAdd(inventory, bottle);
                    }
                    else
                    {
                        GraphUtils.EmporiumTryAdd(bottle);
                    }
                    if (bottle.parentInventory != null)
                    {
                        placed++;
                    }
                }
                box.Destroy();
                Debug.Log("[PrecisionLab] pill precursor kit unpacked (" + placed + "/" + bottles.Length + " bottles)");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] unpacking a pill precursor kit failed: " + e.Message);
            }
        }
    }
}
