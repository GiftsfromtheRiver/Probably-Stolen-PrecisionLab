using System;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Items and chemistry content for the adrenaline injection pen (the "更多药品" unlock).
    ///
    /// * adrenaline supply box  — sold by the retired chemist after the unlock; activating it
    ///   unpacks six reagent bottles (catechol, N-hydroxymethyl base solution, lipobond agent,
    ///   reducing agent, resolving agent, acid).
    /// * adrenaline HCl API      — pressed by the Chem Finisher from a dry container whose active
    ///   ingredient is <see cref="CustomLiquids.AdrenalineHcl"/>.
    /// * adrenaline injection pen — pressed by the Chem Finisher from a dry container whose active
    ///   ingredient is <see cref="CustomLiquids.AdrenalineSolution"/>; usable as a combat stim.
    /// * dextro waste            — collected by the purifier's new waste output slot.
    /// * recipe note             — a vanilla handnote document explaining the six steps.
    /// </summary>
    internal static class AdrenalineContent
    {
        internal const int ApiRequiredVolume = 750;
        internal const int PenRequiredVolume = 750;

        private static bool _recipesAdded;

        // ------------------------------------------------------------------
        // Factories (registered in the mod item directory)
        // ------------------------------------------------------------------

        internal static GameItem CreateSupplyBox()
        {
            GameItem item = ItemDirectory.CreateEmptyItem();
            item.name = Loc.Get("item.adrenaline_box.name", "Adrenaline Precursor Kit");
            item.flavorText = Loc.Get(
                "item.adrenaline_box.flavor",
                "A pharmacist's sealed reagent kit. The label is deliberately vague.");
            item.shortDescription = Loc.Get(
                "item.adrenaline_box.desc",
                "Activate to unpack: catechol, N-hydroxymethyl base solution, lipobond agent, reducing agent, chiral resolving agent, base and acid. All intermediates are produced by the recipes themselves — only the starting reagents are supplied.");
            ModSprites.Apply(item, SpriteKeys.SupplyBox, "Items/items_material", "chemical2");
            item.SetGameItemType("CHEMICAL_SUPPLIES");
            item.unitValue = 1500L;
            item.canActivateSlotItemFunc = (GameItem s, GameInventory _, SlotMarker _) => GeneralHelper.IsItemOwned(s);
            item.mayActivateSlotItemFunc = (GameItem s, GameInventory _, SlotMarker _) => GeneralHelper.IsItemOwned(s);
            item.onActivateSlotItemFunc = delegate (GameItem curItem, GameInventory curInv, SlotMarker marker)
            {
                curItem.Destroy();
                UnpackReagents(curInv);
            };
            ModHelper.SetSource(item, Loc.ModId, ModEntry.ModDisplayName);
            return item;
        }

        private static void UnpackReagents(GameInventory inventory)
        {
            TryAddBottle(inventory, "med_bottle_red", CustomLiquids.Catechol, 3000, "item.catechol_bottle.name", "Catechol Reagent Bottle");
            TryAddBottle(inventory, "med_bottle_violet", CustomLiquids.HydroxymethylBase, 3000, "item.hydroxymethyl_bottle.name", "N-Hydroxymethyl Base Solution");
            TryAddBottle(inventory, "med_bottle_small_pink", "lipobond_agent", 3000, "item.lipobond_bottle.name", "Lipobond Agent Bottle");
            TryAddBottle(inventory, "med_bottle_orange", "reducing_agent", 3000, "item.reducing_bottle.name", "Reducing Agent Bottle");
            TryAddBottle(inventory, "med_bottle_blue", CustomLiquids.ResolvingAgent, 3000, "item.resolving_bottle.name", "Chiral Resolving Agent Bottle");
            TryAddBottle(inventory, "med_bottle_blue", "base", 1500, "item.base_bottle.name", "Base Reagent Bottle");
            TryAddBottle(inventory, "med_bottle_red", "acid", 1500, "item.acid_bottle.name", "Acid Reagent Bottle");
            Debug.Log("[PrecisionLab] adrenaline precursor kit unpacked");
        }

        private static void TryAddBottle(GameInventory inventory, string containerId, string liquidId, int amount, string locKey, string fallback)
        {
            try
            {
                if (!DirectoryMaster.Has<GameItem>(containerId))
                {
                    Debug.LogWarning("[PrecisionLab] reagent bottle template missing: " + containerId);
                    return;
                }
                GameItem bottle = DirectoryMaster.Item(containerId);
                bottle.name = Loc.Get(locKey, fallback);
                Liquid liquid = Liquid.GetLiquidFromId(liquidId);
                bottle.shortDescription = Loc.Fmt(
                    "tooltip.reagent_bottle.desc",
                    "Contains {0} mL of {1}.",
                    LiquidTools.FormatUl(amount),
                    liquid != null ? liquid.displayName : liquidId);
                WaterHelper.AddLiquid(bottle, liquidId, amount);
                GeneralHelper.SetItemOwned(bottle, true);
                GraphUtils.EmporiumTryAdd(inventory, bottle);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] unpacking reagent '" + liquidId + "' failed: " + e.Message);
            }
        }

        internal static GameItem CreateApi()
        {
            GameItem item = ItemDirectory.CreateEmptyItem();
            item.name = Loc.Get("item.adrenaline_api.name", "Adrenaline HCl API");
            item.flavorText = Loc.Get(
                "item.adrenaline_api.flavor",
                "Pharmaceutical-grade adrenaline hydrochloride. Handle with gloves and a steady hand.");
            item.shortDescription = Loc.Get(
                "item.adrenaline_api.desc",
                "Active pharmaceutical ingredient pressed by the Chem Finisher from a dry, filtered adrenaline HCl solution. Quality (lab pure / pharma grade / street standard / ...) is graded from the active fraction. Dissolve it in water to prepare the injectable solution.");
            ModSprites.Apply(item, SpriteKeys.Api, "Items/items_material", "chemical2");
            item.SetGameItemType("CHEMICAL_SUPPLIES");
            item.unitValue = 500L;
            ChemicalHelper.InitChemItem(item, isBlendable: true, isSoluble: true);
            ChemicalProductHelper.InitChemicalProduct(item, CustomLiquids.AdrenalineHcl);
            ChemicalFeedbackHelper.InitChemicalIntentItem(item, ChemicalFeedbackHelper.ChemicalIntent.MEDICAL);
            AudioHelper.InitPlasticContainer(item);
            ModHelper.SetSource(item, Loc.ModId, ModEntry.ModDisplayName);
            return item;
        }

        internal static GameItem CreatePen()
        {
            GameItem item = ItemDirectory.CreateEmptyItem();
            item.name = Loc.Get("item.adrenaline_pen.name", "Adrenaline Injection Pen");
            item.flavorText = Loc.Get(
                "item.adrenaline_pen.flavor",
                "A single-use auto-injector. The label says EPINEPHRINE in blue letters, then a smaller warning nobody reads.");
            item.shortDescription = Loc.Get(
                "item.adrenaline_pen.desc",
                "A combat stim auto-injector: sharp, short burst of accuracy and critical chance. Medical item. Quality is graded from the active fraction of the solution it was pressed from.");
            ModSprites.Apply(item, SpriteKeys.Pen, "Items/items_medical", "injector_blue");
            item.SetGameItemType("MEDICAL");
            item.EnableTag("injector");
            item.EnableTag("ITEM_MEDICAL_TAG");
            item.EnableProficiency("MEDICAL_PROFICIENCY");
            item.bonusAccuracy = 1000;
            item.unitValue = 1200L;
            // Built directly instead of via DirectoryMaster: the effect directories may not be
            // ready when the mod creates the very first pen (e.g. from the self-test), and a
            // failed lookup silently becomes a dummy effect. These match the vanilla combat
            // stimulant definitions (accuracy +20 / crit +33 for 3 turns).
            item.effects.Add(new CombatAbilityEffect(
                "combat_stim_acc_buff", 20, 3,
                CombatAbilityEffect.EffectType.buff,
                CombatAbilityEffect.TargetType.singleFriendly,
                CombatEntity.EffectType.accuracyPercentage,
                "Accuracy+"));
            item.effects.Add(new CombatAbilityEffect(
                "combat_stim_cc_buff", 33, 3,
                CombatAbilityEffect.EffectType.buff,
                CombatAbilityEffect.TargetType.singleFriendly,
                CombatEntity.EffectType.criticalChanceDealing,
                "Critical Chance++"));
            AudioHelper.InitInjectorAudio(item);
            ChemicalHelper.InitChemItem(item, isBlendable: true, isSoluble: true);
            ChemicalProductHelper.InitChemicalProduct(item, CustomLiquids.AdrenalineSolution);
            ChemicalFeedbackHelper.InitChemicalIntentItem(item, ChemicalFeedbackHelper.ChemicalIntent.MEDICAL);
            item.EnableManufacturer("carmicheal");
            ModHelper.SetSource(item, Loc.ModId, ModEntry.ModDisplayName);
            return item;
        }

        internal static GameItem CreateWasteItem()
        {
            GameItem item = ItemDirectory.CreateEmptyItem();
            item.name = Loc.Get("item.dextro_waste.name", "Dextro Waste");
            item.flavorText = Loc.Get(
                "item.dextro_waste.flavor",
                "The useless half of the resolution. Sticky, bitter and best not handled bare-handed.");
            item.shortDescription = Loc.Get(
                "item.dextro_waste.desc",
                "Waste collected by the purification machine's waste output slot when a liquid contains dextro waste from the chiral resolution step. Can be sold, discarded or dissolved back into a container.");
            ModSprites.Apply(item, SpriteKeys.DextroWaste, "Items/items_material", "chemical2");
            item.SetGameItemType("CHEMICAL_SUPPLIES");
            item.unitValue = 0L;
            ChemicalHelper.InitChemItem(item, isBlendable: true, isSoluble: true);
            ChemicalProductHelper.InitChemicalProduct(item, CustomLiquids.DextroWaste);
            AudioHelper.InitPlasticContainer(item);
            ModHelper.SetSource(item, Loc.ModId, ModEntry.ModDisplayName);
            return item;
        }

        // ------------------------------------------------------------------
        // Chem Finisher recipes (only registered once "更多药品" is unlocked)
        // ------------------------------------------------------------------

        internal static void EnsureRecipes()
        {
            if (_recipesAdded)
            {
                return;
            }
            try
            {
                if (ChemFinisherRecipe.GetRecipeById(ItemIds.Api) == null)
                {
                    ChemFinisherRecipe api = new ChemFinisherRecipe();
                    api.identifier = ItemIds.Api;
                    api.displayName = Loc.Get("item.adrenaline_api.name", "Adrenaline HCl API");
                    api.requiredVolume = ApiRequiredVolume;
                    api.isRequireDry = true;
                    api.activeIngredientId = CustomLiquids.AdrenalineHcl;
                    ChemFinisherRecipe.chemFinisherRecipes.Add(api);
                }
                if (ChemFinisherRecipe.GetRecipeById(ItemIds.Pen) == null)
                {
                    ChemFinisherRecipe pen = new ChemFinisherRecipe();
                    pen.identifier = ItemIds.Pen;
                    pen.displayName = Loc.Get("item.adrenaline_pen.name", "Adrenaline Injection Pen");
                    pen.requiredVolume = PenRequiredVolume;
                    pen.isRequireDry = true;
                    pen.activeIngredientId = CustomLiquids.AdrenalineSolution;
                    ChemFinisherRecipe.chemFinisherRecipes.Add(pen);
                }
                _recipesAdded = true;
                Debug.Log("[PrecisionLab] chem finisher recipes registered: " + ItemIds.Api + ", " + ItemIds.Pen);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] registering chem finisher recipes failed: " + e.Message);
            }
        }

        internal static bool AreRecipesAdded()
        {
            return ChemFinisherRecipe.GetRecipeById(ItemIds.Api) != null
                && ChemFinisherRecipe.GetRecipeById(ItemIds.Pen) != null;
        }

        /// <summary>
        /// Rewrites the body of any already-existing adrenaline recipe note (handnote items keep
        /// the body text in a tag from the moment they were created, so notes delivered by older
        /// mod versions would otherwise keep the stale wording).
        /// </summary>
        internal static void RefreshRecipeNotesInStore()
        {
            try
            {
                PlayerStore store = PlayerStore.Instance;
                if (store == null)
                {
                    return;
                }
                string title = Loc.Get("item.adrenaline_recipe.title", "Adrenaline Injection Pen");
                string body = Loc.Get("item.adrenaline_recipe.body", "");
                if (string.IsNullOrEmpty(body))
                {
                    return;
                }
                int refreshed = 0;
                for (int i = 0; i < store.FindAllItem().Count; i++)
                {
                    GameItem item = store.FindAllItem()[i];
                    if (item == null || !item.IsTag("HANDNOTE_TITLE"))
                    {
                        continue;
                    }
                    if (item.GetTagReadonly("HANDNOTE_TITLE").GetString() != title)
                    {
                        continue;
                    }
                    item.ModifyTag("HANDNOTE_BODY", delegate (TagState state)
                    {
                        state.Enable().SetString(body);
                    });
                    item.ModifyTag("HANDNOTE_SIGN", delegate (TagState state)
                    {
                        state.Enable().SetString("");
                    });
                    refreshed++;
                }
                if (refreshed > 0)
                {
                    Debug.Log("[PrecisionLab] refreshed " + refreshed + " adrenaline recipe note(s)");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] refreshing recipe notes failed: " + e.Message);
            }
        }
        // ------------------------------------------------------------------
        // Recipe note document (vanilla handnote page)
        // ------------------------------------------------------------------

        internal static GameItem CreateRecipeNote()
        {
            try
            {
                GameItem note = PreBuiltItemHelper.CreateHandnote(
                    Loc.Get("item.adrenaline_recipe.body",
                        "ADRENALINE INJECTION PEN - six steps.\n\nSTEP 1 Amination (26-60 C): catechol + N-hydroxymethyl base solution + lipobond agent gives amination intermediate\nSTEP 2 Reduction (60-80 C): amination intermediate + reducing agent gives racemic adrenaline\nSTEP 3 Resolution (40-60 C acid catalyst): racemic adrenaline + resolving agent + acid gives levo-adrenaline salt + dextro waste. Remove the waste with the purifier waste slot.\nSTEP 4 Salt formation (26-60 C): levo salt + base gives free base; free base + acid gives adrenaline HCl. Evaporate water then press the dry liquid in the Chem Finisher to get the API\nSTEP 5 Solution: dissolve the API in an equal amount of water gives adrenaline solution (evaporate excess water)\nSTEP 6 Filling: press the dry adrenaline solution in the Chem Finisher gives the injection pen\n\nPer precursor kit: catechol 3000 / base sol 3000 / lipobond 3000 / reducing 3000 / resolving 3000 / base 1500 / acid 1500 uL. Stir and heat each step."),
                    Loc.Get("item.adrenaline_recipe.title", "Adrenaline Injection Pen"),
                    "",
                    1);
                if (note != null)
                {
                    note.name = Loc.Get("item.adrenaline_recipe.name", "Adrenaline Injection Pen Recipe");
                    ModHelper.SetSource(note, Loc.ModId, ModEntry.ModDisplayName);
                }
                return note;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] creating the recipe note failed: " + e.Message);
                return null;
            }
        }
    }
}
