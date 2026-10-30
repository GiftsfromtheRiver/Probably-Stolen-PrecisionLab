using System;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// Pipette-style metering settings window (opened by double-clicking the extractor).
    ///
    /// Built with the game's CustomUI framework (META element prefabs, because the game only
    /// assigns slider/textfield prefabs for the options-menu style; the default window frame
    /// renders fine on the persistent overlay canvas).
    ///
    /// All controls apply IMMEDIATELY - there is no Apply button:
    ///   * exact numeric input (µL) commits when the text field loses focus / Enter is pressed;
    ///   * the slider commits while dragging and mirrors its value into the input field;
    ///   * preset buttons write both the input field and the slider;
    ///   * the direction dropdown (auto / draw / inject) is applied on selection.
    /// Closing the window can therefore never lose the chosen value.
    /// </summary>
    internal static class ExtractorSettingsUI
    {
        internal const string WindowId = "precision_lab:extractor_settings";

        private const string InputId = "dose_input";
        private const string SliderId = "dose_slider";
        private const string SliderLabelId = "slider_label";
        private const string ModeId = "dose_mode";

        /// <summary>Shared state for the element callbacks (they run after the window is built).</summary>
        private sealed class Controls
        {
            internal CustomUIWindow Window;
            internal int LastValue;

            internal CustomUIElement Input
            {
                get { return Window != null ? Window.Get(InputId) : null; }
            }

            internal CustomUIElement Slider
            {
                get { return Window != null ? Window.Get(SliderId) : null; }
            }

            /// <summary>Writes the dose into the item plus the two synced widgets (no recursion).</summary>
            internal void ShowDose(GameItem extractor, int value)
            {
                LastValue = value;
                if (extractor != null)
                {
                    LiquidTools.SetDose(extractor, value);
                }
                CustomUIElement input = Input;
                if (input != null)
                {
                    input.SetString(value.ToString());
                }
                CustomUIElement slider = Slider;
                if (slider != null)
                {
                    slider.SetFloat(value);
                }
            }
        }

        /// <summary>Opens the metering window for the given extractor. Returns false if it could not be opened.</summary>
        internal static bool Open(GameItem extractor)
        {
            if (extractor == null)
            {
                return false;
            }
            CustomUIManager manager = CustomUIManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("[PrecisionLab] CustomUIManager not available; falling back to the native metering window");
                return false;
            }
            try
            {
                bool inStore = StoreUIManager.Instance != null;
                // The overlay canvas is always rendered in the store scene ("meta"/"paper" belong
                // to canvases that are only active for the options menu / tablet). The main menu
                // keeps using the menu layer so the window participates in menu input blocking.
                string layerId = inStore ? CustomUIManager.LAYER_OVERLAY : CustomUIManager.LAYER_MENU;
                int dose = LiquidTools.GetDose(extractor);
                int mode = LiquidTools.GetMode(extractor);
                int volume = WaterHelper.GetTotalVolume(extractor);
                int capacity = WaterHelper.GetTotalCapacity(extractor);

                Controls controls = new Controls { LastValue = dose };
                CustomUIWindow window = null;

                CustomUIBuilder builder = manager.CreateWindow(
                    WindowId,
                    Loc.Get("ui.extractor.title", "精确取液器 · 量程设置 / Metering"),
                    layerId,
                    null,
                    CustomUIManager.Style.Meta);
                builder.SetSize(780f, 470f).SetDraggable(true).SetCloseOnEscape(!inStore);
                builder.BeginScrollFlexible();

                // --- direction / mode ------------------------------------------------
                builder.BeginRow(4f, TextAnchor.MiddleLeft);
                builder.AddLabel(Loc.Get("ui.extractor.mode", "Mode"), "mode_label");
                builder.AddFlexibleSpace();
                builder.AddDropdown(
                    new[]
                    {
                        Loc.Get("ui.extractor.mode_auto", "Auto (empty draws / filled injects)"),
                        Loc.Get("ui.extractor.mode_draw", "Always draw"),
                        Loc.Get("ui.extractor.mode_inject", "Always inject")
                    },
                    mode,
                    delegate (int index)
                    {
                        LiquidTools.SetMode(extractor, index);
                    },
                    ModeId);
                builder.Last.SetFixedWidth(340f);
                builder.End();

                // --- exact numeric input -------------------------------------------
                builder.BeginRow(4f, TextAnchor.MiddleLeft);
                builder.AddLabel(Loc.Get("ui.extractor.dose_input", "Metering (µL)"), "dose_label");
                builder.AddFlexibleSpace();
                builder.AddInput(
                    Loc.Get("ui.extractor.input_hint", "1 - 10000"),
                    dose.ToString(),
                    delegate (string text)
                    {
                        int parsed;
                        if (int.TryParse(text, out parsed))
                        {
                            parsed = Mathf.Clamp(parsed, 1, LiquidTools.MaxDoseUl);
                            controls.ShowDose(extractor, parsed);
                        }
                    },
                    InputId);
                builder.Last.SetFixedWidth(220f);
                builder.End();

                // --- coarse slider --------------------------------------------------
                builder.BeginRow(4f, TextAnchor.MiddleLeft);
                builder.AddLabel(Loc.Get("ui.extractor.slider", "Slider"), SliderLabelId);
                builder.AddFlexibleSpace();
                builder.AddSlider(
                    1f,
                    LiquidTools.MaxDoseUl,
                    dose,
                    delegate (float value)
                    {
                        int rounded = Mathf.RoundToInt(Mathf.Clamp(value, 1f, LiquidTools.MaxDoseUl));
                        controls.ShowDose(extractor, rounded);
                    },
                    true,
                    SliderId);
                builder.Last.SetFixedWidth(300f);
                builder.End();

                // --- presets ---------------------------------------------------------
                builder.BeginRow(4f, TextAnchor.MiddleCenter);
                builder.AddFlexibleSpace();
                AddPresetButton(builder, controls, extractor, "1 µL", 1);
                AddPresetButton(builder, controls, extractor, "10 µL", 10);
                AddPresetButton(builder, controls, extractor, "100 µL", 100);
                AddPresetButton(builder, controls, extractor, "1 mL", 1000);
                AddPresetButton(builder, controls, extractor, "5 mL", 5000);
                AddPresetButton(builder, controls, extractor, "10 mL", LiquidTools.MaxDoseUl);
                builder.AddFlexibleSpace();
                builder.End();

                builder.AddSeparator(null, 2);
                builder.BeginRow(4f, TextAnchor.MiddleLeft);
                builder.AddLabel(
                    Loc.Fmt("ui.extractor.contents_line", "Contents: {0} / {1}",
                        LiquidTools.FormatUl(volume) + " mL", LiquidTools.FormatUl(capacity) + " mL"),
                    "contents_label");
                builder.End();

                // --- footer: only Close (every control applies immediately) ----------
                builder.AddSeparator(null, 2);
                builder.BeginRow(12f, TextAnchor.MiddleCenter);
                builder.AddFlexibleSpace();
                builder.AddButton(Loc.Get("ui.extractor.close", "Close"), delegate
                {
                    CommitFinal(extractor, controls);
                    if (controls.Window != null)
                    {
                        controls.Window.Close();
                    }
                }, "close");
                builder.Last.SetFixedWidth(220f);
                builder.AddFlexibleSpace();
                builder.End();

                builder.End();
                window = builder.Show();
                if (window == null || !window.IsAlive)
                {
                    return false;
                }
                controls.Window = window;

                // Defensive: if the slider prefab is missing in this build/scene, hide the
                // (then useless) slider row instead of leaving an empty labelled line.
                CustomUIElement sliderElement = controls.Slider;
                bool sliderVisual = sliderElement != null && sliderElement.root != null;
                if (!sliderVisual)
                {
                    Debug.LogWarning("[PrecisionLab] slider element could not be created; hiding the slider row");
                    if (sliderElement != null)
                    {
                        sliderElement.SetVisible(false);
                    }
                    CustomUIElement sliderLabel = window.Get(SliderLabelId);
                    if (sliderLabel != null)
                    {
                        sliderLabel.SetVisible(false);
                    }
                }
                try
                {
                    window.Center();
                }
                catch (Exception centerError)
                {
                    Debug.LogWarning("[PrecisionLab] centering the metering window failed: " + centerError.Message);
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[PrecisionLab] metering settings window failed to open: " + e);
                return false;
            }
        }

        private static void AddPresetButton(CustomUIBuilder builder, Controls controls, GameItem extractor, string label, int value)
        {
            builder.AddButton(label, delegate
            {
                controls.ShowDose(extractor, value);
            });
            builder.Last.SetFixedWidth(84f);
        }

        /// <summary>Last chance to persist a value the player typed but did not commit (e.g. close via X).</summary>
        private static void CommitFinal(GameItem extractor, Controls controls)
        {
            try
            {
                if (extractor == null)
                {
                    return;
                }
                int value = controls.LastValue;
                CustomUIElement input = controls.Input;
                if (input != null && input.IsAlive)
                {
                    int parsed;
                    if (int.TryParse(input.GetString(), out parsed))
                    {
                        value = parsed;
                    }
                }
                value = Mathf.Clamp(value, 1, LiquidTools.MaxDoseUl);
                LiquidTools.SetDose(extractor, value);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PrecisionLab] final metering commit failed: " + e.Message);
            }
        }
    }
}
