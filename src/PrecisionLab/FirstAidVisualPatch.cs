using System;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Some first aid shell/seal instances are created during mod-init prewarm, before the mod sprite
    /// dictionary is ready, so they keep the fallback atlas sprite permanently. Refresh the sprite
    /// whenever the item element is laid out; this repairs old saves and store stock without touching
    /// content/ownership.
    /// </summary>
    [HarmonyPatch(typeof(GameItemElement), "UpdateRectTransform")]
    internal static class FirstAidVisualPatch
    {
        private static void Prefix(GameItemElement __instance)
        {
            try
            {
                GameItem item = __instance;
                if (item == null)
                {
                    return;
                }
                string id = item.identifier;
                string key;
                string atlas;
                string fallback;
                if (id == ItemIds.FirstAidBox)
                {
                    key = SpriteKeys.FirstAidBoxSprite;
                    atlas = "Items/items_container";
                    fallback = "med_box";
                }
                else if (id == ItemIds.FirstAidShell)
                {
                    key = SpriteKeys.FirstAidShellSprite;
                    atlas = "Items/items_container";
                    fallback = "med_box";
                }
                else if (id == ItemIds.FirstAidSeal)
                {
                    key = SpriteKeys.FirstAidSealSprite;
                    atlas = "Items/items_tool";
                    fallback = "label";
                }
                else
                {
                    return;
                }
                if (item.useSpriteFromMod && item.spritePath == key)
                {
                    return;
                }
                ModSprites.Apply(item, key, atlas, fallback);
                Debug.Log("[PrecisionLab] refreshed first aid UI sprite " + id
                    + " -> " + key + " mod=" + item.useSpriteFromMod);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] first aid UI sprite refresh failed: " + e.Message);
            }
        }
    }
}
