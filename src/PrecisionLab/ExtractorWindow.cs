using System;
using UnityEngine;

namespace PrecisionLab
{
    /// <summary>
    /// The 量程设置 window of the Precision Liquid Extractor, opened by double-clicking the
    /// item in-game (the game's native ItemMouseDoubleClickHandler opens any item's
    /// contentWindow). It is a machine-style PixelWindow built from the game's own machinery
    /// widgets: one slider for the metering range (1..10000 µL, 1 µL steps), fine-tune
    /// buttons, preset buttons and a contents readout. Every change writes the per-item dose
    /// tag immediately (also persistable through saves) and the tooltip shows the value.
    /// </summary>
    internal static class ExtractorWindow
    {
        /// <summary>Last slider created (diagnostics hook only; used by SelfTest to verify wiring).</summary>
        internal static MachinerySlider LastCreatedSlider { get; private set; }

        internal static PixelWindow Create(GameItem extractor)
        {
            int dose = LiquidTools.GetDose(extractor);

            MachineryText valueText = new MachineryText();
            SetValueText(valueText, dose);

            MachinerySlider slider = new MachinerySlider(dose, 200).SetRange(1f, LiquidTools.MaxDoseUl, 1f);
            slider.onChanged = delegate (float raw)
            {
                if (extractor == null)
                {
                    return;
                }
                int value = Mathf.RoundToInt(Mathf.Clamp(raw, 1f, LiquidTools.MaxDoseUl));
                LiquidTools.SetDose(extractor, value);
                SetValueText(valueText, value);
            };
            LastCreatedSlider = slider;

            MachineryStack meterRow = MachineryStack.Horizontal(slider, valueText);
            MachineryPanel meterPanel = new MachineryPanel(
                Loc.Get("ui.extractor.range", "Metering range (µL)"),
                meterRow,
                "meter");

            MachineryStack fineRow = MachineryStack.Horizontal(
                MakeNudgeButton("-10", slider, -10),
                MakeNudgeButton("-1", slider, -1),
                MakeNudgeButton("+1", slider, 1),
                MakeNudgeButton("+10", slider, 10));
            MachineryPanel finePanel = new MachineryPanel(
                Loc.Get("ui.extractor.fine", "Fine tune (µL)"),
                fineRow,
                "fine");

            MachineryStack presetRow = MachineryStack.Horizontal(
                MakePresetButton("1", slider, 1),
                MakePresetButton("10", slider, 10),
                MakePresetButton("100", slider, 100),
                MakePresetButton("1 mL", slider, 1000),
                MakePresetButton("5 mL", slider, 5000),
                MakePresetButton("10 mL", slider, LiquidTools.MaxDoseUl));
            MachineryPanel presetPanel = new MachineryPanel(
                Loc.Get("ui.extractor.presets", "Presets"),
                presetRow,
                "presets");

            int volume = WaterHelper.GetTotalVolume(extractor);
            int capacity = WaterHelper.GetTotalCapacity(extractor);
            MachineryText contentText = new MachineryText();
            contentText.SetStatus(
                Loc.Get("ui.extractor.contents", "Contents"),
                LiquidTools.FormatUl(volume) + " / " + LiquidTools.FormatUl(capacity) + " mL");
            MachineryPanel contentPanel = new MachineryPanel(
                Loc.Get("ui.extractor.status", "Status"),
                contentText,
                "status");

            PixelWindow window = MachineHelper.CreateMachineryWindow(true, meterPanel, finePanel, presetPanel, contentPanel);
            // The default machinery row puts every panel in ONE horizontal line, which is wider
            // than the screen for four panels. Restack it as a 2x2 grid (fallback window only).
            MachineryPanelRow row = window.childElement as MachineryPanelRow;
            if (row != null)
            {
                row.SetColumns(2);
            }
            return window;
        }

        private static MachineryButton MakeNudgeButton(string text, MachinerySlider slider, int delta)
        {
            return new MachineryButton(text, delegate
            {
                slider.SetValue(slider.value + delta, true);
            }, 36, -1);
        }

        private static MachineryButton MakePresetButton(string text, MachinerySlider slider, int value)
        {
            return new MachineryButton(text, delegate
            {
                slider.SetValue(value, true);
            }, 44, -1);
        }

        private static void SetValueText(MachineryText text, int dose)
        {
            try
            {
                text.SetStatus(Loc.Get("ui.extractor.value", "Value"), dose + " µL");
            }
            catch (Exception)
            {
                // UI-only feedback; never break metering.
            }
        }
    }
}
