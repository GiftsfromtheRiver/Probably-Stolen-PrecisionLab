using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>Provides the custom apothecary-workshop background as the afterhour stage
    /// background (no Jackson scene/character is used).</summary>
    [HarmonyPatch(typeof(MapUIManager), "ResolveBackground")]
    internal static class ApothecaryWorkshopBackgroundPatch
    {
        internal const string BackgroundKey = "apothecary_workshop_bg";

        private static bool Prefix(string key, ref Sprite __result)
        {
            if (key != BackgroundKey) return true;
            try
            {
                Sprite source = ModSprites.Get(BackgroundKey);
                if (source != null && source.texture != null)
                {
                    __result = Sprite.Create(
                        source.texture,
                        new Rect(0f, 0f, source.texture.width, source.texture.height),
                        new Vector2(0.5f, 0.5f),
                        100f);
                    return false;
                }
            }
            catch (System.Exception) { }
            return true;
        }
    }
}