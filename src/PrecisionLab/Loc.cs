using System;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Localization helper. Values come from the mod's Localization/*.csv files
    /// (loaded by ModLoader.LoadLocalization into LoadedMod.Localization) and are read
    /// through ModHelper.GetLocalized, which tries the current locale code and then "en".
    /// Before the assets are loaded (or if a key is missing) the English fallback baked
    /// into the code is returned, so the UI never shows a raw key.
    /// </summary>
    internal static class Loc
    {
        internal const string ModId = "precision_lab";

        internal static string Get(string key, string fallback)
        {
            try
            {
                string value = ModHelper.GetLocalized(ModId, key);
                if (string.IsNullOrEmpty(value) || value == key)
                {
                    return fallback;
                }
                return value;
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        internal static string Fmt(string key, string fallback, params object[] args)
        {
            string format = Get(key, fallback);
            try
            {
                return string.Format(format, args);
            }
            catch (FormatException)
            {
                return format;
            }
        }
    }
}
