using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>Transit-map thumbnail for the apothecary workshop: use the workshop scene
    /// background (with character) instead of a bare portrait.</summary>
    [HarmonyPatch(typeof(TransitUI), "ResolveImage")]
    internal static class ApothecaryWorkshopImagePatch
    {
        internal const string CardKey = "precision_apothecary_workshop_card";

        private static bool Prefix(string key, ref Sprite __result)
        {
            if (key != CardKey) return true;
            try
            {
                Sprite card = ModSprites.Get("apothecary_workshop_card");
                if (card != null)
                {
                    __result = card;
                    return false;
                }
            }
            catch (System.Exception) { }
            return true;
        }
    }
}