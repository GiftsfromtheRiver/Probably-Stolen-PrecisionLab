using System;
using UnityEngine;

namespace PrecisionLab
{
    internal static class ItemIds
    {
        internal const string Extractor = "precision_liquid_extractor";
        internal const string Purifier = "precision_purifier";
        internal const string Cultivator = "precision_microbe_cultivator";
        internal const string SupplyBox = "adrenaline_supply_box";
        internal const string Api = "adrenaline_api";
        internal const string Pen = "adrenaline_pen";
        internal const string DextroWaste = "dextro_waste_item";
        internal const string Tourniquet = "makeshift_tourniquet";
        internal const string ResolvingBottle = "resolving_agent_bottle";
        internal const string ResolvingRefill = "resolving_agent_refill";
        internal const string NaloxoneBox = "naloxone_supply_box";
        internal const string NaloxoneApi = "naloxone_hcl_api";
        internal const string BirthControlBox = "birth_control_supply_box";
        internal const string SannaiBox = "sannai_supply_box";
        internal const string SynaprestBox = "synaprest_supply_box";
        internal const string SynaprestInjector = "synaprest_injector";
    }

    internal static class SpriteKeys
    {
        internal const string Extractor = "precision_extractor";
        internal const string Purifier = "precision_purifier";
        internal const string PurifierActive = "precision_purifier_active";
        internal const string Cultivator = "precision_cultivator";
        internal const string CultivatorActive = "precision_cultivator_active";
        internal const string SupplyBox = "adrenaline_supply_box";
        internal const string Api = "adrenaline_api";
        internal const string Pen = "adrenaline_pen";
        internal const string DextroWaste = "dextro_waste";
        internal const string Tourniquet = "makeshift_tourniquet";
        internal const string ResolvingBottle = "resolving_agent_bottle";
        internal const string ResolvingRefill = "resolving_agent_refill";
        internal const string NaloxoneBox = "naloxone_supply_box";
        internal const string NaloxoneApi = "naloxone_hcl_api";
        internal const string BirthControlBox = "birth_control_supply_box";
        internal const string SannaiBox = "sannai_supply_box";
        internal const string SynaprestBox = "synaprest_supply_box";
        internal const string SynaprestInjector = "synaprest_injector";
    }

    internal static class Tags
    {
        internal const string ExtractorDose = "PRECISION_EXTRACTOR_DOSE_UL";
        internal const string ExtractorMode = "PRECISION_EXTRACTOR_MODE";
        internal const string ExtractorLastResult = "PRECISION_EXTRACTOR_LAST_RESULT";
        internal const string PurifierRemoved = "PRECISION_PURIFIER_REMOVED_TAG";
        internal const string CultivatorActivity = "PRECISION_CULTIVATOR_ACTIVITY_INT";
        internal const string CultivatorProcessed = "PRECISION_CULTIVATOR_PROCESSED_INT";
        internal const string LiquidContainer = "LIQUID_CONTAINER_TAG";
        internal const string LiquidCapacity = "LIQUID_CONTAINER_CAPACITY";
        internal const string LiquidBasePrice = "LIQUID_CONTAINER_BASE_PRICE_TAG";
        internal const string Graduated = "GRADUATED_CONTAINER_TAG";
        internal const string WineContainer = "WINE_CONTAINER_TAG";
    }

    /// <summary>
    /// Mod sprite application. Mods can ship 16x16 PNGs in Textures/Items/; ModLoader
    /// turns them into LoadedMod.Sprites and GameItem.SetSpriteAndShapeFromMod builds the
    /// grid shape from them. If the sprite is not available yet (or was removed) we fall
    /// back to a vanilla atlas sprite so items are never drawn with a broken shape.
    /// </summary>
    internal static class ModSprites
    {
        internal static bool Has(string spriteKey)
        {
            try
            {
                LoadedMod mod = ModLoader.Mods.Find(m => m != null && m.Manifest != null && m.Manifest.ID == Loc.ModId);
                return mod != null && mod.Sprites != null && mod.Sprites.ContainsKey(spriteKey);
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static void Apply(GameItem item, string spriteKey, string fallbackAtlas, string fallbackSprite)
        {
            if (item == null)
            {
                return;
            }
            if (Has(spriteKey))
            {
                item.SetSpriteAndShapeFromMod(Loc.ModId, spriteKey);
                return;
            }
            item.SetSpriteAndShape(fallbackAtlas, fallbackSprite);
        }
    }
}
