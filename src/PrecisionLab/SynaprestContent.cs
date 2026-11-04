using System;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Items for the Synaprest line: the precursor kit sold by the retired chemist and the
    /// crafted black injector ("赛纳普斯注射器"). The injector removes all addictions but applies
    /// the anhedonia/dependency side effects from <see cref="SynaprestEffects"/>.
    /// </summary>
    internal static class SynaprestContent
    {
        internal const int PressVolume = 5000;
        internal const string VanillaInjectorId = "black_injector";

        internal static void EnsureRecipes()
        {
            try
            {
                // Press the VANILLA Synaprest injector, not a mod clone: no mod source line in the
                // tooltip and exactly the original item. Side effects are attached to the vanilla
                // item in AttachToVanilla below.
                ChemFinisherRecipe old = ChemFinisherRecipe.GetRecipeById(ItemIds.SynaprestInjector);
                if (old != null)
                {
                    ChemFinisherRecipe.chemFinisherRecipes.Remove(old);
                }
                ChemFinisherRecipe recipe = ChemFinisherRecipe.GetRecipeById(VanillaInjectorId);
                if (recipe == null)
                {
                    recipe = new ChemFinisherRecipe { identifier = VanillaInjectorId };
                    ChemFinisherRecipe.chemFinisherRecipes.Add(recipe);
                }
                recipe.displayName = LocHelper.GetLocalizedItem("item_black_injector_name");
                if (string.IsNullOrEmpty(recipe.displayName))
                {
                    recipe.displayName = Loc.Get("item.synaprest_injector.name", "赛纳普斯注射器");
                }
                recipe.requiredVolume = PressVolume;
                recipe.isRequireDry = true;
                recipe.activeIngredientId = CustomLiquids.SynaprestApi;
                Debug.Log("[PrecisionLab] synaprest recipe registered (vanilla black_injector output)");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] registering synaprest recipe failed: " + e.Message);
            }
        }

        /// <summary>Attaches the mod's addiction-clear + side effects to the vanilla injector.</summary>
        internal static void AttachToVanilla(GameItem item)
        {
            if (item == null || item.identifier != VanillaInjectorId)
            {
                return;
            }
            item.canActivateSlotItemFunc = delegate (GameItem self, GameInventory inventory, SlotMarker slot)
            {
                return GeneralHelper.IsItemOwned(self) && HealthData.HasAnyAddiction();
            };
            item.mayActivateSlotItemFunc = delegate (GameItem self, GameInventory inventory, SlotMarker slot)
            {
                return GeneralHelper.IsItemOwned(self) && HealthData.HasAnyAddiction();
            };
            item.onActivateSlotItemFunc = delegate (GameItem self, GameInventory inventory, SlotMarker slot)
            {
                try
                {
                    SynaprestEffects.ApplyOnUse(self);
                }
                finally
                {
                    self.Destroy();
                }
            };
        }

        internal static GameItem CreateSupplyBox()
        {
            GameItem item = ItemDirectory.CreateEmptyItem();
            ModSprites.Apply(item, SpriteKeys.ReagentKitSynaprest, "Items/items_material", "chemical2");
            ReagentKitVisuals.ForceKitShape(item);
            item.name = Loc.Get("item.synaprest_box.name", "赛纳普斯原料盒");
            item.shortDescription = Loc.Get(
                "item.synaprest_box.desc",
                "激活拆开得到一批赛纳普斯原料：神经受体配体、甾体原料、酸、水、还原剂、酒精各 20 毫升。");
            item.flavorText = Loc.Get(
                "item.synaprest_box.flavor",
                "密封的高价值原料盒。标签上写着：抑制奖励，也抑制你自己。");
            item.SetGameItemType("CHEMICAL_SUPPLIES");
            ReagentKitVisuals.MakeNonContraband(item);
            item.unitValue = 500L;
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

        private static void Unpack(GameItem box, GameInventory inventory)
        {
            try
            {
                GameItem[] bottles =
                {
                    AdrenalineContent.CreateBottle("med_bottle_violet", CustomLiquids.NeuralLigand, 20000, "item.neural_ligand_bottle.name", "Neural Receptor Ligand Bottle"),
                    AdrenalineContent.CreateBottle("med_bottle_violet", CustomLiquids.SteroidRaw, 20000, "item.steroid_bottle.name", "Steroid Raw Material Bottle"),
                    AdrenalineContent.CreateBottle("med_bottle_red", "acid", 20000, "item.acid_bottle.name", "Acid Reagent Bottle"),
                    AdrenalineContent.CreateBottle("med_bottle_blue", "water", 20000, "item.water_bottle.name", "Water Bottle"),
                    AdrenalineContent.CreateBottle("med_bottle_orange", "reducing_agent", 20000, "item.reducing_bottle.name", "Reducing Agent Bottle"),
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
                Debug.Log("[PrecisionLab] synaprest kit unpacked (" + placed + "/" + bottles.Length + " bottles)");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] unpacking the synaprest kit failed: " + e.Message);
            }
        }

        internal static GameItem CreateInjector()
        {
            GameItem item = ItemDirectory.CreateEmptyItem();
            item.SetSpriteAndShape("Items/items_medical", "injector_black");
            item.name = Loc.Get("item.synaprest_injector.name", "赛纳普斯注射器");
            // Keep the in-game description identical to the vanilla Synaprest injector.
            string vanillaDesc = LocHelper.GetLocalizedItem("item_black_injector_desc");
            item.shortDescription = string.IsNullOrEmpty(vanillaDesc)
                ? Loc.Get("item.synaprest_injector.desc", "A next-generation addiction treatment.")
                : vanillaDesc;
            string vanillaFlavor = LocHelper.GetLocalizedItem("item_black_injector_flavor");
            item.flavorText = string.IsNullOrEmpty(vanillaFlavor)
                ? Loc.Get("item.synaprest_injector.flavor", "A next-generation therapy for drug addiction.")
                : vanillaFlavor;
            item.unitValue = 350L;
            item.SetGameItemType("MEDICAL");
            item.SetGameItemType("LUXURY_ITEM");
            item.EnableManufacturer("pastel");
            AudioHelper.InitInjectorAudio(item);
            item.canActivateSlotItemFunc = delegate (GameItem self, GameInventory inventory, SlotMarker slot)
            {
                return GeneralHelper.IsItemOwned(self) && HealthData.HasAnyAddiction();
            };
            item.mayActivateSlotItemFunc = delegate (GameItem self, GameInventory inventory, SlotMarker slot)
            {
                return GeneralHelper.IsItemOwned(self) && HealthData.HasAnyAddiction();
            };
            item.onActivateSlotItemFunc = delegate (GameItem self, GameInventory inventory, SlotMarker slot)
            {
                try
                {
                    SynaprestEffects.ApplyOnUse(self);
                }
                finally
                {
                    self.Destroy();
                }
            };
            ModHelper.SetSource(item, Loc.ModId, ModEntry.ModDisplayName);
            return item;
        }
    }

    /// <summary>Attaches the mod's effects to vanilla black injectors created at runtime.</summary>
    [HarmonyLib.HarmonyPatch(typeof(DirectoryMaster), "Item", typeof(string), typeof(bool))]
    internal static class SynaprestVanillaAttachPatch
    {
        private static void Postfix(string identifier, GameItem __result)
        {
            if (__result != null && identifier == SynaprestContent.VanillaInjectorId)
            {
                SynaprestContent.AttachToVanilla(__result);
            }
        }
    }
}
