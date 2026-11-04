using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Immunivax injectors pressed below 99% API purity are allowed through the Chem Finisher, but
    /// their authenticity is damaged: the lower the purity, the more random authenticity problems
    /// (indicator / serial / label / colour / cracked body). At 99%+ the injector is fully genuine.
    ///
    /// This replaces the hard 99% press gate for Immunivax only: Naloxgone and Synaprest still use
    /// the hard gate, while Immunivax turns low purity into visible defects as requested.
    /// </summary>
    internal static class ImmunivaxQualityDefects
    {
        internal static double GetPurity(GameItem item)
        {
            try
            {
                string part = Liquid.GetCurrentPartFromId(CustomLiquids.ImmunoglobulinApi);
                if (part == null)
                {
                    return 0.0;
                }
                int active = item.GetTagReadonly(part).GetInt();
                // Injectors are ordinary (non-CHEM_ITEM) items; use the liquid/water total.
                int total = WaterHelper.GetTotalVolume(item);
                return total > 0 ? active * 100.0 / total : 0.0;
            }
            catch (Exception)
            {
                return 0.0;
            }
        }

        internal static int DefectCount(double purity)
        {
            if (purity >= 99.0) return 0;
            if (purity >= 90.0) return 1;
            if (purity >= 75.0) return 2;
            if (purity >= 50.0) return 3;
            return 4;
        }

        internal static void Apply(GameItem item)
        {
            if (item == null || item.identifier != "large_purple_injector")
            {
                return;
            }
            double purity = GetPurity(item);
            int count = DefectCount(purity);
            bool counterfeit = false;
            bool expired = false;

            try
            {
                if (count <= 0)
                {
                    if (item.FindItemFeatureByCategory("CATEGORY_GENUINE_INJECTOR") == null)
                    {
                        item.AddItemFeature(ItemFeatureList.InjectorFeature(
                            ItemFeatureList.InjectorConditionType.Genuine,
                            ItemFeatureList.InjectorConditionType.Genuine));
                    }
                    return;
                }

                List<string> pool = new List<string> { "indicator", "serial", "label", "color", "body" };
                for (int i = pool.Count - 1; i > 0; i--)
                {
                    int j = UnityEngine.Random.Range(0, i + 1);
                    string t = pool[i];
                    pool[i] = pool[j];
                    pool[j] = t;
                }

                // A body defect requires visible cracks (INJECTOR_BODY 1-3) while a colour defect
                // uses body=-1 to show the wrong colour. Never roll both; replace one with the
                // remaining unused category so the defect count stays exact.
                List<string> chosen = new List<string>();
                for (int i = 0; i < count && i < pool.Count; i++)
                {
                    chosen.Add(pool[i]);
                }
                if (chosen.Contains("color") && chosen.Contains("body"))
                {
                    for (int i = count; i < pool.Count; i++)
                    {
                        if (pool[i] != "color" && pool[i] != "body")
                        {
                            chosen[chosen.IndexOf("body")] = pool[i];
                            break;
                        }
                    }
                }

                for (int i = 0; i < chosen.Count; i++)
                {
                    switch (chosen[i])
                    {
                        case "indicator":
                        {
                            int indicator = UnityEngine.Random.Range(0, 2) == 0 ? 1 : 3;
                            item.ModifyTag("INJECTOR_INDICATOR", delegate (TagState state)
                            {
                                state.Enable().SetInt(indicator);
                            });
                            item.EnableTag("ISSUE_INDICATOR");
                            if (indicator == 1) expired = true;
                            break;
                        }
                        case "serial":
                        {
                            bool recalled = UnityEngine.Random.Range(0, 2) == 0;
                            string serial = recalled
                                ? InsInjectorHelper.GenerateRecalledSerial()
                                : InsInjectorHelper.GenerateInCorrectSerial();
                            item.ModifyTag("INJECTOR_SERIAL", delegate (TagState state)
                            {
                                state.Enable().SetString(serial);
                            });
                            item.EnableTag("ISSUE_SERIAL");
                            if (!recalled) counterfeit = true;
                            break;
                        }
                        case "label":
                        {
                            item.ModifyTag("INJECTOR_COMPANY", delegate (TagState state)
                            {
                                state.Enable().SetString(InsInjectorHelper.GenerateIncorrectCompany());
                            });
                            item.ModifyTag("INJECTOR_MODEL", delegate (TagState state)
                            {
                                state.Enable().SetString(InsInjectorHelper.GenerateIncorrectModel());
                            });
                            item.EnableTag("ISSUE_LABEL");
                            counterfeit = true;
                            break;
                        }
                        case "color":
                        {
                            int color = UnityEngine.Random.Range(0, 2);
                            item.ModifyTag("INJECTOR_COLOR", delegate (TagState state)
                            {
                                state.Enable().SetInt(color);
                            });
                            item.ModifyTag("INJECTOR_BODY", delegate (TagState state)
                            {
                                state.Enable().SetInt(-1);
                            });
                            item.EnableTag("ISSUE_COLOR");
                            counterfeit = true;
                            break;
                        }
                        default: // body
                        {
                            int body = UnityEngine.Random.Range(1, 4);
                            item.ModifyTag("INJECTOR_BODY", delegate (TagState state)
                            {
                                state.Enable().SetInt(body);
                            });
                            item.EnableTag("ISSUE_BODY");
                            break;
                        }
                    }
                }

                // Refresh the authenticity condition so inspection/confront logic sees the defect.
                item.RemoveItemFeatureByID("CATEGORY_GENUINE_INJECTOR");
                ItemFeatureList.InjectorConditionType type = counterfeit
                    ? ItemFeatureList.InjectorConditionType.Counterfeit
                    : (expired ? ItemFeatureList.InjectorConditionType.Expired : ItemFeatureList.InjectorConditionType.Unusable);
                item.AddItemFeature(ItemFeatureList.InjectorFeature(type, type));
                Debug.Log("[PrecisionLab] pressed Immunivax purity " + purity.ToString("F1")
                    + "%: added " + count + " authenticity defect(s)");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] applying Immunivax authenticity defects failed: " + e.Message);
            }
        }
    }

    [HarmonyPatch(typeof(ChemicalProductHelper), "GradeProduct")]
    internal static class ImmunivaxAuthenticityPatch
    {
        private static void Postfix(GameItem item)
        {
            ImmunivaxQualityDefects.Apply(item);
        }
    }
}
