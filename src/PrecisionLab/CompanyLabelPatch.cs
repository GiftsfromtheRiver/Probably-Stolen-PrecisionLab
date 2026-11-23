using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>Draws the mod-only “星泉医疗 / Xingquan Medical” manufacturer label.</summary>
    [HarmonyPatch(typeof(GameItem), "DisplayManufacturer")]
    internal static class CompanyLabelPatch
    {
        private static bool Prefix(GameItem __instance, RichTextBuilder builder, string manufacturer)
        {
            if (manufacturer != "xingquan")
            {
                return true;
            }
            builder.Add(" ");
            builder.AddLine(Loc.Get("corpo_name_xingquan", "星泉医疗 / Xingquan Medical"),
                color: RenderHandler.ColorPalette.Turquoise, ignoreEmpty: true, bold: false, italic: false, underline: true);
            return false;
        }
    }
}