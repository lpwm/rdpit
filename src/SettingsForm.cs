using System;
using System.Drawing;
using System.Windows.Forms;

namespace RdpIt
{
    /// <summary>
    /// Settings panel to customize the default resolution used for new sessions.
    /// </summary>
    public sealed class SettingsForm : Form
    {
        private readonly RadioButton _fit = new RadioButton();
        private readonly RadioButton _fixed = new RadioButton();
        private readonly RadioButton _full = new RadioButton();

        private readonly ComboBox _preset = new ComboBox();
        private readonly NumericUpDown _width = new NumericUpDown();
        private readonly NumericUpDown _height = new NumericUpDown();
        private readonly CheckBox _autoAdjust = new CheckBox();
        private readonly ComboBox _experience = new ComboBox();

        // Common desktop resolutions offered as presets.
        private static readonly Size[] Presets =
        {
            new Size(1920, 1080),
            new Size(1600, 900),
            new Size(1536, 864),
            new Size(1366, 768),
            new Size(1280, 720),
            new Size(1024, 768),
            new Size(800, 600),
        };

        // Experience levels in display order, paired with their enum value.
        private static readonly Tuple<ExperienceLevel, string>[] ExperienceItems =
        {
            Tuple.Create(ExperienceLevel.Modem,              "Modem (56 kbps) - minimal visuals"),
            Tuple.Create(ExperienceLevel.Broadband,          "Low-speed broadband (256 kbps - 2 Mbps)"),
            Tuple.Create(ExperienceLevel.HighSpeedBroadband, "High-speed broadband (2 - 10 Mbps)"),
            Tuple.Create(ExperienceLevel.Lan,                "LAN (10 Mbps+) - full visuals"),
            Tuple.Create(ExperienceLevel.AutoDetect,         "Detect connection quality automatically"),
        };

        public AppSettings Result { get; private set; }

        private readonly AppSettings _current;

        public SettingsForm(AppSettings current)
        {
            _current = current ?? new AppSettings();
            BuildUi();
            LoadFrom(_current);
            UpdateEnabledState();
        }

        private void BuildUi()
        {
            Text = "Settings — Default Resolution";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(420, 400);
            Font = new Font("Segoe UI", 9f);
            AppIcon.Apply(this);

            var group = new GroupBox
            {
                Text = "Default resolution for new sessions",
                Location = new Point(12, 12),
                Size = new Size(396, 210),
            };

            _fit.Text = "Fit to window  (start at window size, follow on resize)";
            _fit.Location = new Point(14, 26);
            _fit.AutoSize = true;
            _fit.CheckedChanged += (s, e) => UpdateEnabledState();

            _fixed.Text = "Fixed resolution:";
            _fixed.Location = new Point(14, 58);
            _fixed.AutoSize = true;
            _fixed.CheckedChanged += (s, e) => UpdateEnabledState();

            _preset.DropDownStyle = ComboBoxStyle.DropDownList;
            _preset.Location = new Point(150, 84);
            _preset.Width = 150;
            _preset.Items.Add("Custom...");
            foreach (var p in Presets)
                _preset.Items.Add(p.Width + " x " + p.Height);
            _preset.SelectedIndexChanged += OnPresetChanged;

            var lblW = new Label { Text = "Width:", Location = new Point(40, 118), AutoSize = true };
            _width.Location = new Point(90, 115);
            _width.Width = 80;
            _width.Minimum = 640; _width.Maximum = 7680; _width.Increment = 10;

            var lblH = new Label { Text = "Height:", Location = new Point(190, 118), AutoSize = true };
            _height.Location = new Point(245, 115);
            _height.Width = 80;
            _height.Minimum = 480; _height.Maximum = 4320; _height.Increment = 10;

            _full.Text = "Full screen  (primary monitor resolution)";
            _full.Location = new Point(14, 158);
            _full.AutoSize = true;
            _full.CheckedChanged += (s, e) => UpdateEnabledState();

            group.Controls.Add(_fit);
            group.Controls.Add(_fixed);
            group.Controls.Add(_preset);
            group.Controls.Add(lblW);
            group.Controls.Add(_width);
            group.Controls.Add(lblH);
            group.Controls.Add(_height);
            group.Controls.Add(_full);

            _autoAdjust.Text = "Auto-adjust remote resolution when the window is resized";
            _autoAdjust.Location = new Point(14, 232);
            _autoAdjust.AutoSize = true;

            // --- Connection experience / performance level ---
            var expGroup = new GroupBox
            {
                Text = "Connection experience",
                Location = new Point(12, 262),
                Size = new Size(396, 76),
            };
            var expLabel = new Label
            {
                Text = "Performance level:",
                Location = new Point(14, 30),
                AutoSize = true,
            };
            _experience.DropDownStyle = ComboBoxStyle.DropDownList;
            _experience.Location = new Point(130, 27);
            _experience.Width = 250;
            foreach (var item in ExperienceItems)
                _experience.Items.Add(item.Item2);
            expGroup.Controls.Add(expLabel);
            expGroup.Controls.Add(_experience);

            var ok = new Button { Text = "Save", DialogResult = DialogResult.OK, Width = 80, Location = new Point(236, 352) };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80, Location = new Point(324, 352) };
            ok.Click += OnSave;

            Controls.Add(group);
            Controls.Add(_autoAdjust);
            Controls.Add(expGroup);
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        private void LoadFrom(AppSettings s)
        {
            _width.Value = Clamp(s.DefaultWidth, _width.Minimum, _width.Maximum);
            _height.Value = Clamp(s.DefaultHeight, _height.Minimum, _height.Maximum);
            _autoAdjust.Checked = s.AutoAdjustOnResize;

            switch (s.ResolutionMode)
            {
                case ResolutionMode.Fixed: _fixed.Checked = true; break;
                case ResolutionMode.FullScreen: _full.Checked = true; break;
                default: _fit.Checked = true; break;
            }
            SyncPresetToValues();

            _experience.SelectedIndex = ExperienceIndexOf(s.ExperienceLevel);
        }

        private static int ExperienceIndexOf(ExperienceLevel level)
        {
            for (int i = 0; i < ExperienceItems.Length; i++)
                if (ExperienceItems[i].Item1 == level) return i;
            return 3; // default to LAN
        }

        private static decimal Clamp(int v, decimal min, decimal max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        private void SyncPresetToValues()
        {
            // Select the matching preset, or "Custom..." if none match.
            for (int i = 0; i < Presets.Length; i++)
            {
                if (Presets[i].Width == (int)_width.Value && Presets[i].Height == (int)_height.Value)
                {
                    _preset.SelectedIndex = i + 1; // +1 because index 0 is "Custom..."
                    return;
                }
            }
            _preset.SelectedIndex = 0;
        }

        private void OnPresetChanged(object sender, EventArgs e)
        {
            if (_preset.SelectedIndex <= 0) return; // "Custom..." -> leave values as-is
            var p = Presets[_preset.SelectedIndex - 1];
            _width.Value = p.Width;
            _height.Value = p.Height;
        }

        private void UpdateEnabledState()
        {
            bool fixedMode = _fixed.Checked;
            _preset.Enabled = fixedMode;
            _width.Enabled = fixedMode;
            _height.Enabled = fixedMode;
        }

        private void OnSave(object sender, EventArgs e)
        {
            var mode = _fixed.Checked ? ResolutionMode.Fixed
                     : _full.Checked ? ResolutionMode.FullScreen
                     : ResolutionMode.FitToWindow;

            Result = new AppSettings
            {
                ResolutionMode = mode,
                DefaultWidth = (int)_width.Value,
                DefaultHeight = (int)_height.Value,
                AutoAdjustOnResize = _autoAdjust.Checked,
                ExperienceLevel = _experience.SelectedIndex >= 0
                    ? ExperienceItems[_experience.SelectedIndex].Item1
                    : ExperienceLevel.Lan
            };
            Result.Normalize();
        }
    }
}
