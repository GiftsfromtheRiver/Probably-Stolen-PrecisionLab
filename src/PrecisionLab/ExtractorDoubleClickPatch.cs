using System;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Double-clicking the Precision Liquid Extractor opens the CustomUI metering window with an
    /// exact numeric input field (+ slider/presets) instead of the native pixel window.
    /// The native machinery-style window (ExtractorWindow) stays attached to the item as a
    /// fallback: if the CustomUI window cannot be opened, this prefix returns true and the game
    /// opens the native window itself.
    /// </summary>
    [HarmonyPatch(typeof(ItemMouseDoubleClickHandler), "DoubleClickAction")]
    internal static class ExtractorDoubleClickPatch
    {
        private static bool Prefix(GameItem newItem)
        {
            try
            {
                if (newItem == null || newItem.identifier != ItemIds.Extractor)
                {
                    return true;
                }
                if (ExtractorSettingsUI.Open(newItem))
                {
                    // Handled by the mod: skip the vanilla content-window double click.
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] extractor double-click handler failed, falling back to the native window: " + e);
            }
            return true;
        }
    }
}
