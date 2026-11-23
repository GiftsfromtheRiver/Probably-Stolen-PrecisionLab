using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrecisionLab
{
    internal static class DrugKitContent
    {
        private sealed class Def
        {
            public string Id;
            public string Sprite;
            public string Name;
            public string Desc;
            public long Value;
            public string[] Solids;
            public string[] Bottles;
        }

        private static readonly Def[] Kits =
        {
            K("belladonna_supply_box", "drug_kit_belladonna", "颠茄药剂包", "颠茄草×2、酒精×2、酸、碱", 420,
                new[] { "belladonna_herb", "belladonna_herb" }, new[] { "alcohol", "alcohol", "acid", "base", "lipobond_agent", "water" }),
            K("antidepressant_supply_box", "drug_kit_antidepressant", "抗抑郁药剂包", "芳香族化合物、还原剂、酸、水、酒精、碱", 480,
                new[] { "aromatic_compound" }, new[] { "reducing_agent", "acid", "water", "alcohol", "base" }),
            K("amiodarone_supply_box", "drug_kit_amiodarone", "胺碘酮药剂包", "芳香族化合物、碘化钾晶体、酸、脂合剂、水、碱", 460,
                new[] { "aromatic_compound", "potassium_iodide_crystal" }, new[] { "acid", "lipobond_agent", "water", "base" }),
            K("vasopressin_supply_box", "drug_kit_vasopressin", "加压素药剂包", "动物腺体粉末×2、酸×2、水、碱", 440,
                new[] { "animal_gland_powder", "animal_gland_powder" }, new[] { "acid", "acid", "water", "base", "lipobond_agent", "alcohol" }),
            K("vasodilator_supply_box", "drug_kit_vasodilator", "舒张素药剂包", "动物腺体粉末×2、碱×2、水、脂合剂", 440,
                new[] { "animal_gland_powder", "animal_gland_powder" }, new[] { "base", "base", "water", "lipobond_agent", "acid", "alcohol" }),
            K("coagulant_supply_box", "drug_kit_coagulant", "促凝剂药剂包", "动物腺体粉末、水×2、酒精×2、脂合剂", 400,
                new[] { "animal_gland_powder" }, new[] { "water", "water", "alcohol", "alcohol", "lipobond_agent" }),
            K("anticoagulant_supply_box", "drug_kit_anticoagulant", "抗凝剂药剂包", "动物腺体粉末、碱×3、水、酸", 430,
                new[] { "animal_gland_powder" }, new[] { "base", "base", "base", "water", "acid", "potassium_iodide", "alcohol" }),
            K("smelling_salt_supply_box", "drug_kit_smelling_salt", "嗅盐药剂包", "氨水、酒精×2、水、水培芦苇过滤芯", 260,
                new[] { "hydroreed_filter" }, new[] { "ammonia_water", "alcohol", "alcohol", "water" })
        };

        private static Def K(string id, string sprite, string name, string desc, long value, string[] solids, string[] bottles)
        {
            return new Def { Id = id, Sprite = sprite, Name = name, Desc = desc, Value = value, Solids = solids, Bottles = bottles };
        }

        internal static bool EnsureRegistered(ModItemDirectory directory)
        {
            bool all = true;
            for (int i = 0; i < Kits.Length; i++)
            {
                Def d = Kits[i];
                all &= directory.Has(d.Id) || directory.Add(d.Id, delegate { return Create(d); });
            }
            return all;
        }

        private static GameItem Create(Def d)
        {
            GameItem item = ItemDirectory.CreateEmptyItem();
            ModSprites.Apply(item, d.Sprite, "Items/items_material", "chemical2");
            ReagentKitVisuals.ForceKitShape(item);
            item.name = Loc.Get("item." + d.Id + ".name", d.Name);
            item.shortDescription = Loc.Get("item." + d.Id + ".desc", d.Desc);
            item.SetGameItemType("CHEMICAL_SUPPLIES");
            item.unitValue = d.Value;
            item.canActivateSlotItemFunc = (GameItem self, GameInventory inv, SlotMarker s) => GeneralHelper.IsItemOwned(self);
            item.mayActivateSlotItemFunc = (GameItem self, GameInventory inv, SlotMarker s) => GeneralHelper.IsItemOwned(self);
            item.onActivateSlotItemFunc = delegate (GameItem self, GameInventory inv, SlotMarker s)
            {
                try
                {
                    for (int i = 0; i < d.Solids.Length; i++) Give(inv, DirectoryMaster.Item(d.Solids[i]));
                    for (int i = 0; i < d.Bottles.Length; i++)
                    {
                        GameItem bottle = AdrenalineContent.CreateBottle("med_bottle_red", d.Bottles[i], 20000,
                            "item." + d.Id + ".bottle", d.Bottles[i]);
                        if (bottle != null)
                        {
                            Liquid liquid = Liquid.GetLiquidFromId(d.Bottles[i]);
                            bottle.name = (liquid != null ? liquid.displayName : d.Bottles[i]) + " 瓶";
                            bottle.shortDescription = (liquid != null ? liquid.displayName : d.Bottles[i]) + "，药剂包原料。";
                        }
                        Give(inv, bottle);
                    }
                    self.Destroy();
                }
                catch (Exception e) { Debug.LogWarning("[PrecisionLab] unpacking drug kit failed: " + e.Message); }
            };
            ReagentKitVisuals.MakeNonContraband(item);
            AudioHelper.InitPlasticContainer(item);
            ModHelper.SetSource(item, Loc.ModId, ModEntry.ModDisplayName);
            return item;
        }

        private static void Give(GameInventory inv, GameItem item)
        {
            if (item == null) return;
            GeneralHelper.SetItemOwned(item, true);
            if (inv != null) GraphUtils.EmporiumTryAdd(inv, item);
            else GraphUtils.EmporiumTryAdd(item);
        }
    }
}
