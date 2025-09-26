using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace WootMouseRemap
{
    public sealed class MouseSettingsBinder : IDisposable
    {
        private readonly Control _root;
        private readonly MouseSettingsBus _bus;
        private readonly Dictionary<string, Control> _map = new();
        private bool _updatingUi;

        public MouseSettingsBinder(Control root, MouseSettingsBus bus)
        {
            _root = root;
            _bus = bus;
            IndexControls(root);

            HookTrackBar("trkSensX");
            HookTrackBar("trkSensY");
            HookTrackBar("trkDeadzone");
            HookTrackBar("trkGamma");
            HookTrackBar("trkSmoothing");
            HookTrackBar("trkAccelGain");
            HookTrackBar("trkAccelCap");
            HookCheckBox("chkInvertX");
            HookCheckBox("chkInvertY");
            if (TryGet<Button>("btnMouseDefaults", out var btn))
                btn.Click += (_, __) => SetUiFrom(MouseSettings.Defaults);

            SetUiFrom(_bus.Current);
            PushFromUi();
        }

        public void Dispose() { }

        private void IndexControls(Control c)
        {
            _map[c.Name] = c;
            foreach (Control child in c.Controls) IndexControls(child);
        }

        private bool TryGet<T>(string name, out T ctrl) where T : Control
        {
            if (_map.TryGetValue(name, out var c) && c is T t) { ctrl = t; return true; }
            ctrl = null!;
            return false;
        }

        private void HookTrackBar(string name)
        {
            if (!TryGet<TrackBar>(name, out var tb)) return;
            tb.ValueChanged += (_, __) => PushFromUi();
            tb.Scroll += (_, __) => PushFromUi();
        }

        private void HookCheckBox(string name)
        {
            if (!TryGet<CheckBox>(name, out var cb)) return;
            cb.CheckedChanged += (_, __) => PushFromUi();
        }

        private int Val(string name, int fallback) => TryGet<TrackBar>(name, out var tb) ? tb.Value : fallback;
        private bool On(string name, bool fallback) => TryGet<CheckBox>(name, out var cb) ? cb.Checked : fallback;

        private void SetUiFrom(MouseSettings s)
        {
            _updatingUi = true;
            try
            {
                SetTB("trkSensX",   (int)Math.Round(s.SensitivityX * 100));
                SetTB("trkSensY",   (int)Math.Round(s.SensitivityY * 100));
                SetTB("trkDeadzone",(int)Math.Round(s.Deadzone     * 100));
                SetTB("trkGamma",   (int)Math.Round(s.Gamma        * 100));
                SetTB("trkSmoothing",(int)Math.Round(s.Smoothing   * 100));
                SetTB("trkAccelGain",(int)Math.Round(s.AccelGain   * 100));
                SetTB("trkAccelCap", (int)Math.Round(s.AccelCap    * 100));
                SetCB("chkInvertX", s.InvertX);
                SetCB("chkInvertY", s.InvertY);
                UpdateValueLabels();
            }
            finally { _updatingUi = false; }
        }

        private void SetTB(string name, int value)
        {
            if (!TryGet<TrackBar>(name, out var tb)) return;
            value = Math.Max(tb.Minimum, Math.Min(tb.Maximum, value));
            if (tb.Value != value) tb.Value = value;
        }
        private void SetCB(string name, bool v)
        {
            if (!TryGet<CheckBox>(name, out var cb)) return;
            if (cb.Checked != v) cb.Checked = v;
        }

        
        private void UpdateValueLabels()
        {
            float sx = Val("trkSensX", 100) / 100f;
            float sy = Val("trkSensY", 100) / 100f;
            float dz = Val("trkDeadzone", 5) / 100f;
            float gm = System.Math.Max(10, Val("trkGamma", 140)) / 100f;
            float sm = Val("trkSmoothing", 25) / 100f;
            float ag = Val("trkAccelGain", 0) / 100f;
            float ac = Val("trkAccelCap", 150) / 100f;

            SetLabel("lblSensXVal", sx.ToString("0.00"));
            SetLabel("lblSensYVal", sy.ToString("0.00"));
            SetLabel("lblDeadzoneVal", dz.ToString("0.00"));
            SetLabel("lblGammaVal", gm.ToString("0.00"));
            SetLabel("lblSmoothingVal", sm.ToString("0.00"));
            SetLabel("lblAccelGainVal", ag.ToString("0.00"));
            SetLabel("lblAccelCapVal", ac.ToString("0.00"));
        }
        private void SetLabel(string name, string text)
        {
            if (!TryGet<Label>(name, out var lbl)) return;
            if (!string.Equals(lbl.Text, text, StringComparison.Ordinal)) lbl.Text = text;
        }

        private void PushFromUi()
        {
            if (_updatingUi) return;

            var s = new MouseSettings(
                sensitivityX: Val("trkSensX", 100) / 100f,
                sensitivityY: Val("trkSensY", 100) / 100f,
                deadzone:     Val("trkDeadzone", 5) / 100f,
                gamma:        Math.Max(10, Val("trkGamma", 140)) / 100f,
                smoothing:    Val("trkSmoothing", 25) / 100f,
                accelGain:    Val("trkAccelGain", 0) / 100f,
                accelCap:     Val("trkAccelCap", 150) / 100f,
                invertX:      On("chkInvertX", false),
                invertY:      On("chkInvertY", false)
            );

            UpdateValueLabels();
            _bus.Queue(s);
        }
    }
}