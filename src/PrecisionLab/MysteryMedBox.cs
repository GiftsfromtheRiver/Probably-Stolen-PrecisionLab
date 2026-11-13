using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Sealed random reagent crate. Contents are rolled after DirectoryMaster creates the crate
    /// (DirectoryMaster strips child items from factory output). Before purchase the stock crate is
    /// unowned, so its content window cannot be opened and nothing leaks. After purchase,
    /// double-click opens the crate and reveals 5-10 random raw reagent bottles - not finished
    /// medicines, so it complements the medical supply crate instead of duplicating it.
    /// </summary>
    internal static class MysteryMedBox
    {
        internal const int MinItems = 5;
        internal const int MaxItems = 10;
        internal const string ReagentBottleTag = "PRECISION_REAGENT_BOTTLE_TAG";
        internal const int BottleVolume = 20000;

        private static readonly (string liquid, string sprite, string nameKey, string nameFallback, long value, int weight)[] Pool =
        {
            (CustomLiquids.Catechol, "med_bottle_red", "item.catechol_bottle.name", "儿茶酚瓶（20 毫升）", 40L, 10),
            (CustomLiquids.HydroxymethylBase, "med_bottle_violet", "item.hydroxymethyl_bottle.name", "N-羟甲基碱溶液瓶（20 毫升）", 40L, 9),
            ("lipobond_agent", "med_bottle_small_pink", "item.lipobond_bottle.name", "脂合剂瓶（20 毫升）", 40L, 9),
            ("reducing_agent", "med_bottle_orange", "item.reducing_bottle.name", "还原剂瓶（20 毫升）", 40L, 9),
            (CustomLiquids.ResolvingAgent, "med_bottle_blue", "item.resolving_bottle.name", "拆分剂瓶（20 毫升）", 60L, 7),
            ("base", "med_bottle_blue", "item.base_bottle.name", "碱液瓶（20 毫升）", 20L, 8),
            ("acid", "med_bottle_red", "item.acid_bottle.name", "酸液瓶（20 毫升）", 20L, 8),
            ("alcohol", "med_bottle_red", "item.alcohol_bottle.name", "酒精瓶（20 毫升）", 20L, 8),
            (CustomLiquids.Thebaine, "med_bottle_violet", "item.thebaine_bottle.name", "蒂巴因瓶（20 毫升）", 150L, 4),
            (CustomLiquids.NeuralLigand, "med_bottle_orange", "item.neural_ligand_bottle.name", "神经受体配体瓶（20 毫升）", 150L, 4),
            (CustomLiquids.SteroidRaw, "med_bottle_blue", "item.steroid_bottle.name", "甾体原料瓶（20 毫升）", 60L, 6),
            (CustomLiquids.SannaiRaw, "med_bottle_red", "item.sannai_bottle.name", "山奈原料瓶（20 毫升）", 60L, 6),
            (CustomLiquids.ImmuneSerum, "med_bottle_violet", "item.immune_serum_bottle.name", "免疫血清瓶（20 毫升）", 120L, 4),
            (CustomLiquids.BioGelBase, "med_bottle_blue", "item.bio_gel_bottle.name", "生物胶基瓶（20 毫升）", 120L, 4),
            (CustomLiquids.Oxidizer, "med_bottle_blue", "item.oxidizer_bottle.name", "氧化剂瓶（20 毫升）", 45L, 6),
            (CustomLiquids.MetalPowder, "med_bottle_orange", "item.metal_powder_bottle.name", "金属粉末瓶（20 毫升）", 60L, 5),
            (CustomLiquids.NitratingAcid, "med_bottle_red", "item.nitrating_acid_bottle.name", "硝化酸瓶（20 毫升）", 90L, 4),
            ("sugar", "med_bottle_blue", "item.sugar_bottle.name", "糖浆瓶（20 毫升）", 20L, 8),
            ("stabilizing_agent", "med_bottle_violet", "item.stabilizing_bottle.name", "稳定剂瓶（20 毫升）", 50L, 6)
        };

        internal static GameItem CreateCrate()
        {
            (PixelWindow window, GameInventory inventory) = DirectoryUtils.CreateInventoryWindow(10, 8);
            GameItem crate = ItemDirectory.CreateEmptyItem()
                .SetContentWindow(window);
            ModSprites.Apply(crate, SpriteKeys.MysteryMedCrate, "Items/items_container", "med_box");
            crate.name = Loc.Get("item.mystery_med_box.name", "密封药剂原料箱");
            crate.shortDescription = Loc.Get(
                "item.mystery_med_box.desc",
                "A sealed reagent crate. Contents are locked before purchase; after buying it, double-click to open and get 5-10 random raw reagent bottles for the production lines.");
            crate.flavorText = Loc.Get(
                "item.mystery_med_box.flavor",
                "Manifest says: assorted laboratory reagents. The seal says: no refunds.");
            crate.SetGameItemType("HOUSEHOLD_GOOD");
            crate.SetShape(new GridShapeBuilder(4, 3).SetDataFill(1).Build());
            // Store stock must not be openable before purchase; PlayerStore.OnItemBought unlocks it.
            if (crate.contentWindow != null)
            {
                crate.contentWindow.isLocked = true;
            }
            crate.unitValue = 1500L;
            ModHelper.SetSource(crate, Loc.ModId, ModEntry.ModDisplayName);
            return crate;
        }

        /// <summary>Stock crate; DirectoryMaster strips factory children, so fill after creation.</summary>
        internal static GameItem CreateStockItem()
        {
            GameItem crate = DirectoryMaster.Item(ItemIds.MysteryMedBox);
            FillContents(crate);
            return crate;
        }

        internal static int FillContents(GameItem crate)
        {
            if (crate == null || crate.identifier != ItemIds.MysteryMedBox || crate.contentWindow == null)
            {
                return 0;
            }
            GameInventory inventory = crate.contentWindow.child as GameInventory;
            if (inventory == null)
            {
                return 0;
            }
            if (inventory.childItems.Count >= MinItems)
            {
                return inventory.childItems.Count;
            }
            FillRandomReagents(inventory);
            FillFirstAidParts(inventory);
            return inventory.childItems.Count;
        }

        private static void FillFirstAidParts(GameInventory inventory)
        {
            try
            {
                for (int i = 0; i < 2; i++)
                {
                    GameItem shell = DirectoryMaster.Item(ItemIds.FirstAidShell);
                    GameItem seal = DirectoryMaster.Item(ItemIds.FirstAidSeal);
                    if (shell != null)
                    {
                        GeneralHelper.SetItemOwned(shell, true);
                        if (GraphUtils.TryAcceptAll(inventory, shell) != 0) shell.Destroy();
                    }
                    if (seal != null)
                    {
                        GeneralHelper.SetItemOwned(seal, true);
                        if (GraphUtils.TryAcceptAll(inventory, seal) != 0) seal.Destroy();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] adding first aid parts to crate failed: " + e.Message);
            }
        }

        private static void FillRandomReagents(GameInventory inventory)
        {
            int target = UnityEngine.Random.Range(MinItems, MaxItems + 1);
            HashSet<string> used = new HashSet<string>();
            int guard = 0;
            while (inventory.childItems.Count < target && guard < 200)
            {
                guard++;
                int index = PickWeightedIndex();
                var entry = Pool[index];
                if (used.Contains(entry.liquid))
                {
                    continue;
                }
                GameItem bottle = AdrenalineContent.CreateBottle(
                    entry.sprite,
                    entry.liquid,
                    BottleVolume,
                    entry.nameKey,
                    entry.nameFallback);
                if (bottle == null)
                {
                    continue;
                }
                bottle.name = Loc.Get(entry.nameKey, entry.nameFallback);
                bottle.unitValue = entry.value;
                bottle.EnableTag(ReagentBottleTag);
                GeneralHelper.SetItemOwned(bottle, true);
                if (GraphUtils.TryAcceptAll(inventory, bottle) != 0)
                {
                    bottle.Destroy();
                }
                else
                {
                    used.Add(entry.liquid);
                }
            }
        }

        private static int PickWeightedIndex()
        {
            int total = 0;
            for (int i = 0; i < Pool.Length; i++)
            {
                total += Pool[i].weight;
            }
            int roll = UnityEngine.Random.Range(0, total);
            for (int i = 0; i < Pool.Length; i++)
            {
                roll -= Pool[i].weight;
                if (roll < 0)
                {
                    return i;
                }
            }
            return 0;
        }
    }
}
