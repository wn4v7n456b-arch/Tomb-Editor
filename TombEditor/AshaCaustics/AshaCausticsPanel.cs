// Asha-specific Tomb Editor dock panel prototype.
// Source reference: TombEditor.ToolWindows.Lighting / TexturePanel.
// This is intentionally standalone until the editor solution is vendored or forked.
// No lighting objects are created or modified here.
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Asha.Editor.Caustics
{
    public sealed class CausticsPanel : UserControl
    {
        public event EventHandler SettingsChanged;
        public event EventHandler SelectionModeChanged;
        public event EventHandler ClearSelectionRequested;
        public event EventHandler PreviewChanged;

        private readonly Label paintedCount = new Label { Text = "Painted faces: 0 (project saving not yet tested)", AutoSize = true };
        private readonly CheckBox selection = new CheckBox { Text = "Select caustics surfaces", AutoSize = true };
        private readonly CheckBox inherit = new CheckBox { Text = "Inherit (not implemented)", AutoSize = true, Enabled = false };
        private readonly CheckBox preview = new CheckBox { Text = "Textured preview (not implemented)", AutoSize = true, Checked = false, Enabled = false };
        private readonly NumericUpDown fade = new NumericUpDown { Minimum = 1, Maximum = 5, Value = 3, Width = 64 };
        private readonly NumericUpDown brightness = new NumericUpDown { Minimum = 1, Maximum = 5, Value = 3, Width = 64 };
        private readonly Button tint = new Button { Text = "Caustics colour", AutoSize = true, BackColor = Color.White };
        private readonly Button clear = new Button { Text = "Clear all painted faces", AutoSize = true };

        public bool IsSelecting => selection.Checked;
        public void StopSelecting() => selection.Checked = false;
        public void SetPaintedFaceCount(int count) => paintedCount.Text = $"Painted faces: {count} (project saving not yet tested)";
        public bool Inherit => inherit.Checked;
        public bool Preview => preview.Checked;
        public int EdgeFade => (int)fade.Value;
        public int Brightness => (int)brightness.Value;
        public Color Tint => tint.BackColor;

        public CausticsPanel()
        {
            Dock = DockStyle.Fill;
            var layout = new TableLayoutPanel {
                Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(8),
                ColumnCount = 2, RowCount = 10
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            layout.Controls.Add(selection, 0, 0);
            layout.SetColumnSpan(selection, 2);
            layout.Controls.Add(inherit, 0, 1);
            layout.SetColumnSpan(inherit, 2);
            layout.Controls.Add(new Label { Text = "Edge fade (1 sharp - 5 soft)", AutoSize = true }, 0, 2);
            layout.Controls.Add(fade, 1, 2);
            layout.Controls.Add(new Label { Text = "Brightness (1 dull - 5 bright)", AutoSize = true }, 0, 3);
            layout.Controls.Add(brightness, 1, 3);
            layout.Controls.Add(tint, 0, 4);
            layout.Controls.Add(preview, 0, 5);
            layout.SetColumnSpan(preview, 2);
            layout.Controls.Add(clear, 0, 6);


            layout.Controls.Add(paintedCount, 0, 9);
            layout.SetColumnSpan(paintedCount, 2);
            Controls.Add(layout);
            selection.CheckedChanged += (s, e) => SelectionModeChanged?.Invoke(this, EventArgs.Empty);
            inherit.CheckedChanged += (s, e) => SettingsChanged?.Invoke(this, EventArgs.Empty);
            fade.ValueChanged += (s, e) => SettingsChanged?.Invoke(this, EventArgs.Empty);
            brightness.ValueChanged += (s, e) => SettingsChanged?.Invoke(this, EventArgs.Empty);
            preview.CheckedChanged += (s, e) => PreviewChanged?.Invoke(this, EventArgs.Empty);
            clear.Click += (s, e) => ClearSelectionRequested?.Invoke(this, EventArgs.Empty);
            tint.Click += (s, e) => {
                using (var dialog = new ColorDialog { Color = tint.BackColor, FullOpen = true })
                    if (dialog.ShowDialog(this) == DialogResult.OK) {
                        tint.BackColor = dialog.Color;
                        SettingsChanged?.Invoke(this, EventArgs.Empty);
                    }
            };
        }
    }
}
