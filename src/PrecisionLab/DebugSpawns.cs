using System;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Debug/cheat-menu spawn helpers. The vanilla item spawner only lists directory IDs; these
    /// buttons expose the mod's machines, kits and sealed reagent crate together
    /// so testing does not require visiting the retired chemist/apothecary first.
    /// </summary>
    internal static class DebugSpawns
    {
        private static bool _registered;

        internal static void Register()
        {
            if (_registered)
            {
                return;
            }
            _registered = true;
            try
            {
                ModHelper.AddDebugFunction(Loc.ModId, "Modding", "PrecisionLab: 测试大礼包（机器/原料盒/打印芯片/药剂箱）", SpawnTestingBundle);
                ModHelper.AddDebugFunction(Loc.ModId, "Modding", "PrecisionLab: 全部原料盒 ×9", SpawnAllKits);
                ModHelper.AddDebugFunction(Loc.ModId, "Modding", "PrecisionLab: 密封药剂原料箱", SpawnReagentCrate);
                ModHelper.AddDebugFunction(Loc.ModId, "Modding", "PrecisionLab: 急救盒（真/假/外壳/密封贴）", SpawnFirstAidTest);
                Debug.Log("[PrecisionLab] debug spawn buttons registered");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] registering debug spawn buttons failed: " + e.Message);
            }
        }

        private static void SpawnTestingBundle()
        {
            SpawnItem(ItemIds.Extractor);
            SpawnItem(ItemIds.Purifier);
            SpawnItem(ItemIds.Cultivator);
            SpawnItem("printer");
            SpawnAllKits();
            SpawnReagentCrate();
            for (int i = 0; i < 6; i++)
            {
                SpawnItem("printer_plastic");
            }
        }

        private static void SpawnAllKits()
        {
            SpawnItem(ItemIds.SupplyBox);
            SpawnItem(ItemIds.ResolvingRefill);
            SpawnItem(ItemIds.NaloxoneBox);
            SpawnItem(ItemIds.BirthControlBox);
            SpawnItem(ItemIds.SannaiBox);
            SpawnItem(ItemIds.SynaprestBox);
            SpawnItem(ItemIds.BlueBloodBox);
            SpawnItem(ItemIds.ImmunivaxBox);
            SpawnItem(ItemIds.OrdnanceBox);
            SpawnItem(ItemIds.PerfumeBox);
            SpawnItem(ItemIds.BioPharmaBox);
            SpawnItem(ItemIds.FirstAidShell);
            SpawnItem(ItemIds.FirstAidSeal);
        }

        private static void SpawnFirstAidTest()
        {
            SpawnItem(ItemIds.FirstAidShell);
            SpawnItem(ItemIds.FirstAidSeal);
            try
            {
                GameItem genuine = FirstAidBox.CreateStockBox();
                GameItem fake = FirstAidBox.CreateStockBox();
                if (genuine != null) { GeneralHelper.SetItemOwned(genuine, true); GraphUtils.EmporiumTryAdd(genuine); }
                if (fake != null) { GeneralHelper.SetItemOwned(fake, true); GraphUtils.EmporiumTryAdd(fake); }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] debug first aid spawn failed: " + e.Message);
            }
        }

        private static void SpawnReagentCrate()
        {
            try
            {
                GameItem crate = MysteryMedBox.CreateStockItem();
                if (crate != null && crate.contentWindow != null)
                {
                    crate.contentWindow.isLocked = false;
                }
                GraphUtils.EmporiumTryAdd(crate);
                Debug.Log("[PrecisionLab] debug spawned sealed reagent crate");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] debug crate spawn failed: " + e.Message);
            }
        }

        private static void SpawnItem(string id)
        {
            try
            {
                if (!DirectoryMaster.Has<GameItem>(id))
                {
                    Debug.LogWarning("[PrecisionLab] debug spawn missing item " + id);
                    return;
                }
                GraphUtils.EmporiumTryAdd(DirectoryMaster.Item(id));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] debug spawn " + id + " failed: " + e.Message);
            }
        }
    }
}
