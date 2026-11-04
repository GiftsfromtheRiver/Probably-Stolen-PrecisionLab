using System;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>Blue Blood (蓝血) content: purchasable bio-gel base and the Chem Finisher recipe.</summary>
    internal static class BlueBloodContent
    {
        internal const string VanillaBagId = "blue_blood_bag";
        internal const int BagVolume = 5000;

        internal static void EnsureRecipes()
        {
            try
            {
                if (!DirectoryMaster.Has<GameItem>(VanillaBagId))
                {
                    Debug.LogWarning("[PrecisionLab] blue blood: vanilla blue_blood_bag is missing");
                    return;
                }
                ChemFinisherRecipe recipe = ChemFinisherRecipe.GetRecipeById(VanillaBagId);
                if (recipe == null)
                {
                    recipe = new ChemFinisherRecipe { identifier = VanillaBagId };
                    ChemFinisherRecipe.chemFinisherRecipes.Add(recipe);
                }
                recipe.displayName = LocHelper.GetLocalizedItem("item_blue_blood_bag_name");
                if (string.IsNullOrEmpty(recipe.displayName))
                {
                    recipe.displayName = Loc.Get("item.blue_blood.recipe_name", "蓝血");
                }
                recipe.requiredVolume = BagVolume;
                recipe.isRequireDry = true;
                recipe.activeIngredientId = CustomLiquids.MedicalBlueGel;
                Debug.Log("[PrecisionLab] blue blood chem finisher recipe registered");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] registering blue blood recipe failed: " + e.Message);
            }
        }

        internal static GameItem CreateSupplyBox()
        {
            GameItem item = ItemDirectory.CreateEmptyItem();
            ModSprites.Apply(item, SpriteKeys.ReagentKitBlueBlood, "Items/items_material", "chemical2");
            ReagentKitVisuals.ForceKitShape(item);
            item.name = Loc.Get("item.blue_blood_box.name", "蓝血原料盒");
            item.shortDescription = Loc.Get(
                "item.blue_blood_box.desc",
                "激活拆开得到刚好一锅蓝血原料：生物胶基 20 毫升、水 10 毫升、微量酸 50 微升、还原剂 10 毫升、脂合剂 10 毫升。");
            item.flavorText = Loc.Get("item.blue_blood_box.flavor", "标签只写“生物凝胶原料”。");
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
                        AdrenalineContent.CreateBottle("med_bottle_blue", CustomLiquids.BioGelBase, 20000, "item.bio_gel_bottle.name", "Bio-Gel Matrix Bottle (20 mL)"),
                        AdrenalineContent.CreateBottle("med_bottle_blue", "water", 10000, "item.water_bottle.name", "Water (10 mL)"),
                        AdrenalineContent.CreateBottle("med_bottle_red", "acid", 50, "item.acid_bottle.name", "Trace Acid (50 uL)"),
                        AdrenalineContent.CreateBottle("med_bottle_orange", "reducing_agent", 10000, "item.reducing_bottle.name", "Reducing Agent Bottle (10 mL)"),
                        AdrenalineContent.CreateBottle("med_bottle_small_pink", "lipobond_agent", 10000, "item.lipobond_bottle.name", "Lipobond Agent Bottle (10 mL)")
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
                    Debug.Log("[PrecisionLab] blue blood kit unpacked (" + placed + "/" + bottles.Length + " bottles)");
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[PrecisionLab] unpacking the blue blood kit failed: " + e.Message);
                }
            };
            AudioHelper.InitPlasticContainer(item);
            ModHelper.SetSource(item, Loc.ModId, ModEntry.ModDisplayName);
            return item;
        }

        internal static GameItem CreateBioGelBottle()
        {
            GameItem bottle = AdrenalineContent.CreateBottle(
                "med_bottle_blue",
                CustomLiquids.BioGelBase,
                20000,
                "item.bio_gel_bottle.name",
                "Bio-Gel Matrix Bottle (20 mL)");
            if (bottle == null)
            {
                return null;
            }
            bottle.name = Loc.Get("item.bio_gel_bottle.name", "生物胶基瓶（20 毫升）");
            bottle.shortDescription = Loc.Get(
                "item.bio_gel_bottle.desc",
                "A full 20 mL vial of bio-gel matrix, the starting material for the Blue Blood chain. Purchased from the retired chemist; handle at room temperature.");
            bottle.flavorText = Loc.Get("item.bio_gel_bottle.flavor", "Not blood. Do not let the customers read the label.");
            bottle.unitValue = 120L;
            ModHelper.SetSource(bottle, Loc.ModId, ModEntry.ModDisplayName);
            return bottle;
        }
    }
}
