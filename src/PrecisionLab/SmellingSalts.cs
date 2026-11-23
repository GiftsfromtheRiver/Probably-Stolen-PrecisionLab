using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Vanilla Smelling Salts production:
    ///   S1: evaporator, room temperature: ammonia water 1 + alcohol 2 + water 1
    ///       -> smelling salts volatile liquid; if heated, ammonia escapes and only wastewater remains.
    ///   S2: chem finisher: volatile liquid + hydroreed fiber slurry -> vanilla smelling_salt.
    /// The hydroreed filter is blended into a fiber slurry first, so the finisher never has to
    /// accept a solid carrier.
    /// </summary>
    internal static class SmellingSaltsContent
    {
        internal const string AmmoniaWaterId = "ammonia_water";
        internal const string SmellingSaltId = "smelling_salt";
        internal const string RecipePageName = "嗅盐";

        internal static GameItem CreateAmmoniaBottle()
        {
            GameItem bottle = AdrenalineContent.CreateBottle(
                "med_bottle_blue", CustomLiquids.AmmoniaWater, 20000,
                "item.ammonia_water.name", "氨水瓶（20 毫升）");
            if (bottle == null) return null;
            bottle.unitValue = 18L;
            bottle.flavorText = Loc.Get("item.ammonia_water.flavor",
                "刺鼻的氨水。常温下与酒精、水混合可制得嗅盐挥发液；一旦加热，氨气就会跑光。");
            ModHelper.SetSource(bottle, Loc.ModId, ModEntry.ModDisplayName);
            return bottle;
        }

        internal static void EnsureRecipes()
        {
            try
            {
                if (ChemFinisherRecipe.GetRecipeById(SmellingSaltId) == null)
                {
                    ChemFinisherRecipe.chemFinisherRecipes.Add(new ChemFinisherRecipe
                    {
                        identifier = SmellingSaltId,
                        displayName = Loc.Get("item.smelling_salt.name", "嗅盐"),
                        requiredVolume = 1000,
                        isRequireDry = false,
                        activeIngredientId = CustomLiquids.SmellingSaltVolatile
                    });
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] smelling salts recipe failed: " + e.Message);
            }
        }
    }

    /// <summary>Adds the hydroreed-filter -> reed fiber slurry conversion used by the blender.</summary>
    [HarmonyPatch(typeof(AmenitiesItemDirectory), "HydroreedFilter")]
    internal static class HydroreedFilterFiberPatch
    {
        private static void Postfix(ref GameItem __result)
        {
            try
            {
                if (__result == null) return;
                if (!__result.IsTag("BLENDABLE_TAG"))
                {
                    ChemicalHelper.InitChemItem(__result, isBlendable: true, isSoluble: true);
                }
                ChemicalHelper.AddChemContentUL(__result, CustomLiquids.ReedFiber, 4000);
                __result.EnableTag("BLENDABLE_TAG");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] hydroreed fiber patch failed: " + e.Message);
            }
        }
    }

    /// <summary>Press gate for the two-liquid smelling salts recipe.</summary>
    [HarmonyPatch(typeof(MachineChemFinisher), "CanOutput")]
    internal static class SmellingSaltPressGate
    {
        private static void Postfix(GameItem machine, GameSlotInventory containerSlot, ref bool __result)
        {
            try
            {
                if (machine == null || containerSlot == null || containerSlot.childItem == null) return;
                if (!machine.IsTag("SELECTED_CHEM_ID_TAG")) return;
                if (machine.GetTagReadonly("SELECTED_CHEM_ID_TAG").GetString() != SmellingSaltsContent.SmellingSaltId) return;
                GameItem c = containerSlot.childItem;
                int volatileLiquid = LiquidTools.GetLiquidAmount(c, CustomLiquids.SmellingSaltVolatile);
                int fiber = LiquidTools.GetLiquidAmount(c, CustomLiquids.ReedFiber);
                int total = WaterHelper.GetTotalVolume(c);
                __result = total >= 1000 && volatileLiquid * 100 >= total * 40 && fiber * 100 >= total * 40;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] smelling salts press gate failed: " + e.Message);
            }
        }
    }

    /// <summary>Vanilla smelling_salt must not retain the intermediate liquid composition.</summary>
    [HarmonyPatch(typeof(ChemicalProductHelper), "GradeProduct")]
    internal static class SmellingSaltCleanupPatch
    {
        private static void Postfix(GameItem item)
        {
            try
            {
                if (item != null && item.identifier == SmellingSaltsContent.SmellingSaltId)
                {
                    ChemicalHelper.RemoveAllLiquid(item);
                }
            }
            catch (Exception) { }
        }
    }


    /// <summary>
    /// The kit costs 260 credits and its one hydroreed filter yields enough (2600 uL at 0 modules)
    /// fiber for 6 salts (400 uL each). 260 / 6 = 43.33 material cost per salt; at 80% of the
    /// finished sale price that gives a sale price of 54 credits (6 x 54 = 324; 260/324 = 80.2%).
    /// </summary>
    [HarmonyPatch(typeof(MedsItemDirectory), "SmellingSalt")]
    internal static class SmellingSaltPricePatch
    {
        private static void Postfix(ref GameItem __result)
        {
            try { if (__result != null) __result.unitValue = 54L; }
            catch (Exception) { }
        }
    }


    /// <summary>
    /// Direct container reaction (no evaporator): at room temperature ammonia water + alcohol +
    /// water react in the container itself. The old evaporator S1 was too slow and left too much
    /// waste; this direct reaction consumes the same inputs and leaves 1000 uL volatile liquid.
    /// Heating to 30 C or above destroys the ammonia and leaves wastewater instead.
    /// </summary>
    internal static class SmellingSaltsReaction
    {
        internal static int React(GameItem container)
        {
            try
            {
                if (container == null || WaterHelper.IsEmpty(container)) return 0;
                int ammonia = LiquidTools.GetLiquidAmount(container, CustomLiquids.AmmoniaWater);
                int alcohol = LiquidTools.GetLiquidAmount(container, "alcohol");
                int water = LiquidTools.GetLiquidAmount(container, "water");
                if (ammonia < 1000 || alcohol < 2000 || water < 1000) return 0;
                double temp = container.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble();
                Add(container, CustomLiquids.AmmoniaWater, -1000);
                Add(container, "alcohol", -2000);
                Add(container, "water", -1000);
                if (temp < 30.0)
                {
                    Add(container, CustomLiquids.SmellingSaltVolatile, 1000);
                }
                else
                {
                    Add(container, CustomLiquids.SmellingSaltWaste, 4000);
                }
                WaterFeatureHelper.OnLiquidContainerPourOutOf(container);
                SpriteHelper.UpdateWaterContainerSprite(container);
                return 1;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] smelling salts direct reaction failed: " + e.Message);
                return 0;
            }
        }

        private static void Add(GameItem c, string id, int delta)
        {
            string part = Liquid.GetCurrentPartFromId(id);
            if (part == null) return;
            int next = Math.Max(0, c.GetTagReadonly(part).GetInt() + delta);
            c.ModifyTag(part, delegate (TagState st) { st.Enable().SetInt(next); }, skipValidate: true);
        }
    }
}
