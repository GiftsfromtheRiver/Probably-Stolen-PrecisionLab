using System;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Naloxone content: precursor kit (the only purchased precursor is thebaine), crude HCl API
    /// item and the Chem Finisher recipes that turn naloxone solution into the vanilla Naloxgone
    /// injector ("纳洛通"). The whole line is part of the existing "More Medicines" unlock.
    /// </summary>
    internal static class NaloxoneContent
    {
        internal const string VanillaInjectorId = "pure_white_injector";

        internal const int ApiRequiredVolume = 5000;
        internal const int InjectorRequiredVolume = 5000;

        // ------------------------------------------------------------------
        // Chem Finisher recipes
        // ------------------------------------------------------------------
        internal static void EnsureRecipes()
        {
            try
            {
                if (!DirectoryMaster.Has<GameItem>(VanillaInjectorId))
                {
                    Debug.LogWarning("[PrecisionLab] naloxone: vanilla injector '" + VanillaInjectorId + "' is missing");
                    return;
                }
                if (ChemFinisherRecipe.GetRecipeById(ItemIds.NaloxoneApi) == null)
                {
                    ChemFinisherRecipe.chemFinisherRecipes.Add(new ChemFinisherRecipe
                    {
                        identifier = ItemIds.NaloxoneApi,
                        displayName = Loc.Get("item.naloxone_api.name", "Naloxone HCl API"),
                        requiredVolume = ApiRequiredVolume,
                        isRequireDry = true,
                        activeIngredientId = CustomLiquids.NaloxoneHcl
                    });
                }
                if (ChemFinisherRecipe.GetRecipeById(VanillaInjectorId) == null)
                {
                    ChemFinisherRecipe.chemFinisherRecipes.Add(new ChemFinisherRecipe
                    {
                        identifier = VanillaInjectorId,
                        displayName = Loc.Get("item.naloxone_injector.name", "Naloxgone Injector"),
                        requiredVolume = InjectorRequiredVolume,
                        isRequireDry = true,
                        activeIngredientId = CustomLiquids.NaloxoneSolution
                    });
                }
                Debug.Log("[PrecisionLab] naloxone chem finisher recipes registered");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] registering naloxone recipes failed: " + e.Message);
            }
        }

        // ------------------------------------------------------------------
        // Items
        // ------------------------------------------------------------------

        /// <summary>Sealed precursor kit: one full 20 mL bottle for every reagent of one batch.</summary>
        internal static GameItem CreateSupplyBox()
        {
            GameItem item = ItemDirectory.CreateEmptyItem();
            ModSprites.Apply(item, SpriteKeys.SupplyBox, "Items/items_material", "chemical2");
            item.name = Loc.Get("item.naloxone_box.name", "Naloxone Precursor Kit");
            item.flavorText = Loc.Get(
                "item.naloxone_box.flavor",
                "A sealed precursor kit from the retired chemist. The label only lists half of the reagents.");
            item.shortDescription = Loc.Get(
                "item.naloxone_box.desc",
                "Activate to unpack one naloxone batch: thebaine, acid, reducing agent, lipobond agent, N-hydroxymethyl base solution and alcohol (20 mL each). Thebaine is the only ingredient you cannot get anywhere else.");
            item.SetGameItemType("CHEMICAL_SUPPLIES");
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
                Unpack(self, inventory);
            };
            AudioHelper.InitPlasticContainer(item);
            ModHelper.SetSource(item, Loc.ModId, ModEntry.ModDisplayName);
            return item;
        }

        internal static void Unpack(GameItem box, GameInventory inventory)
        {
            try
            {
                GameItem[] bottles =
                {
                    AdrenalineContent.CreateBottle("med_bottle_violet", CustomLiquids.Thebaine, 20000, "item.thebaine_bottle.name", "Thebaine Reagent Bottle"),
                    // N1, N2 and N4 each consume one 20 mL acid bottle per 20 mL batch, so the kit
                    // ships three acid bottles (60 mL) - otherwise a kit alone cannot finish a batch.
                    AdrenalineContent.CreateBottle("med_bottle_red", "acid", 20000, "item.acid_bottle.name", "Acid Reagent Bottle"),
                    AdrenalineContent.CreateBottle("med_bottle_red", "acid", 20000, "item.acid_bottle.name", "Acid Reagent Bottle"),
                    AdrenalineContent.CreateBottle("med_bottle_red", "acid", 20000, "item.acid_bottle.name", "Acid Reagent Bottle"),
                    AdrenalineContent.CreateBottle("med_bottle_orange", "reducing_agent", 20000, "item.reducing_bottle.name", "Reducing Agent Bottle"),
                    AdrenalineContent.CreateBottle("med_bottle_small_pink", "lipobond_agent", 20000, "item.lipobond_bottle.name", "Lipobond Agent Bottle"),
                    AdrenalineContent.CreateBottle("med_bottle_violet", CustomLiquids.HydroxymethylBase, 20000, "item.hydroxymethyl_bottle.name", "N-Hydroxymethyl Base Solution"),
                    AdrenalineContent.CreateBottle("med_bottle_blue", "alcohol", 20000, "item.alcohol_bottle.name", "Alcohol Reagent Bottle")
                };
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
                Debug.Log("[PrecisionLab] naloxone precursor kit unpacked (" + placed + "/" + bottles.Length + " bottles)");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] unpacking the naloxone kit failed: " + e.Message);
            }
        }

        /// <summary>Pressable naloxone HCl API item (the chem finisher's first naloxone recipe).</summary>
        internal static GameItem CreateApi()
        {
            GameItem item = ItemDirectory.CreateEmptyItem();
            ModSprites.Apply(item, SpriteKeys.Api, "Items/items_material", "chemical2");
            item.name = Loc.Get("item.naloxone_api.name", "Naloxone HCl API");
            item.flavorText = Loc.Get(
                "item.naloxone_api.flavor",
                "The opioid antidote, pressed into a dry pellet. Do not confuse it with the party drugs.");
            item.shortDescription = Loc.Get(
                "item.naloxone_api.desc",
                "Active pharmaceutical ingredient pressed by the Chem Finisher from 5000 uL of dry naloxone HCl liquid. Dissolve one API in an equal amount of water to prepare the solution for one Naloxgone injector.");
            item.SetGameItemType("CHEMICAL_SUPPLIES");
            item.unitValue = 50L;
            ChemicalHelper.InitChemItem(item, isBlendable: true, isSoluble: true);
            ChemicalProductHelper.InitChemicalProduct(item, CustomLiquids.NaloxoneHcl);
            ChemicalFeedbackHelper.InitChemicalIntentItem(item, ChemicalFeedbackHelper.ChemicalIntent.MEDICAL);
            AudioHelper.InitPlasticContainer(item);
            ModHelper.SetSource(item, Loc.ModId, ModEntry.ModDisplayName);
            return item;
        }
    }
}
