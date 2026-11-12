using DarkUI.Docking;
using System;
using System.Numerics;
using System.Windows.Forms;
using TombLib.LevelData;
using TombLib.Utils;

namespace TombEditor.ToolWindows
{
    public partial class Lighting : DarkToolWindow
    {
        private readonly Editor _editor;
        private readonly System.Windows.Forms.FlowLayoutPanel causticsPanel = new System.Windows.Forms.FlowLayoutPanel();
        private readonly NumericUpDown causticsSpeed = new NumericUpDown();
        private readonly NumericUpDown causticsPattern = new NumericUpDown();
        private readonly NumericUpDown causticsDistortion = new NumericUpDown();
        private readonly NumericUpDown causticsSoftness = new NumericUpDown();
        private bool updatingCausticsControls;
        private readonly CheckBox causticsAmbient = new CheckBox { Text = "Inherit room ambient colour (default)", AutoSize = true };

        private void AddCausticsSetting(string label, NumericUpDown input, decimal min, decimal max,
            decimal increment, Func<LightInstance, float> getter, Action<LightInstance, float> setter)
        {
            var title = new Label { Text = label, AutoSize = false, Width = 94, Height = 22,
                TextAlign = System.Drawing.ContentAlignment.MiddleRight };
            input.Width = 65;
            input.DecimalPlaces = 2;
            input.Minimum = min;
            input.Maximum = max;
            input.Increment = increment;
            causticsPanel.Controls.Add(title);
            causticsPanel.Controls.Add(input);
            input.ValueChanged += (sender, args) =>
            {
                if (updatingCausticsControls || !causticsPanel.Visible) return;
                EditorActions.UpdateLight<float>(
                    (light, value) => light.Type != LightType.CausticsEffect ||
                        Math.Abs(getter(light) - value) < 0.005f,
                    (light, value) => setter(light, value),
                    light => (float)input.Value, updateLighting: false);
            };
        }

        private void InitializeCausticsControls()
        {
            causticsPanel.FlowDirection = FlowDirection.LeftToRight;
            causticsPanel.WrapContents = true;
            causticsPanel.AutoScroll = true;
            causticsPanel.Dock = DockStyle.Bottom;
            causticsPanel.Height = 110;
            causticsPanel.Controls.Add(causticsAmbient);
            causticsAmbient.CheckedChanged += (sender, args) =>
            {
                if (updatingCausticsControls || !causticsPanel.Visible) return;
                EditorActions.UpdateLight<bool>(
                    (light, value) => light.Type != LightType.CausticsEffect || light.CausticsInheritRoomAmbient == value,
                    (light, value) => light.CausticsInheritRoomAmbient = value,
                    light => causticsAmbient.Checked, updateLighting: false);
                panelLightColor.Enabled = !causticsAmbient.Checked;
            };
            causticsPanel.Visible = false;
            AddCausticsSetting("Scale", causticsSpeed, 0.1m, 20, 0.1m,
                l => l.CausticsScale, (l, v) => l.CausticsScale = v);
            AddCausticsSetting("Width", causticsPattern, 0.1m, 20, 0.1m,
                l => l.CausticsWidth, (l, v) => l.CausticsWidth = v);
            AddCausticsSetting("Height", causticsDistortion, 0.1m, 20, 0.1m,
                l => l.CausticsHeight, (l, v) => l.CausticsHeight = v);

            Controls.Add(causticsPanel);
            MinimumSize = new System.Drawing.Size(MinimumSize.Width, 285);
            Height = Math.Max(Height, 285);
        }


        // UI ordering is independent of serialized LightType enum values.
        private static readonly LightType[] DisplayLightTypes = {
            LightType.Point, LightType.Shadow, LightType.Spot,
            LightType.Effect, LightType.CausticsEffect, LightType.Sun, LightType.FogBulb
        };
        private static int LightTypeIndex(LightType type) => Array.IndexOf(DisplayLightTypes, type);

        public Lighting()
        {
            InitializeComponent();
            InitializeCausticsControls();
            CommandHandler.AssignCommandsToControls(Editor.Instance, this, toolTip);

            foreach (LightType l in DisplayLightTypes)
                cmbLightTypes.Items.Add(l.ToString().SplitCamelcase());

            cmbLightQuality.SelectedIndex = cmbLightTypes.SelectedIndex = 0; // Reset index to default

            _editor = Editor.Instance;
            _editor.EditorEventRaised += EditorEventRaised;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _editor.EditorEventRaised -= EditorEventRaised;
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        private void EditorEventRaised(IEditorEvent obj)
        {

            // Update light UI
            if (obj is Editor.InitEvent ||
                obj is Editor.GameVersionChangedEvent ||
                obj is Editor.LevelChangedEvent ||
                obj is Editor.ObjectChangedEvent ||
                obj is Editor.SelectedObjectChangedEvent)
            {
                var isTEN = _editor.Level.Settings.GameVersion == TRVersion.Game.TombEngine;
                var light = _editor.SelectedObject as LightInstance;

                // Get light type
                bool HasInRange = false;
                bool HasOutRange = false;
                bool HasInOutAngle = false;
                bool HasDirection = false;
                bool CanCastShadows = false;
                bool CanCastDynamicShadows = false;
                bool CanIlluminateGeometry = false;

                cmbLightQuality.Enabled = false;

                if (light != null)
                    switch (light.Type)
                    {
                        case LightType.Point:
                            HasInRange = true;
                            HasOutRange = true;
                            CanIlluminateGeometry = true;
                            CanCastShadows = true;
                            CanCastDynamicShadows = isTEN;
                            break;

                        case LightType.Shadow:
                            HasInRange = true;
                            HasOutRange = true;
                            CanCastShadows = true;
                            CanIlluminateGeometry = true;
                            break;

                        case LightType.Effect:
                            HasInRange = true;
                            HasOutRange = true;
                            break;

                        case LightType.CausticsEffect:
                            HasInRange = true;
                            HasOutRange = true;
                            // Caustics bulbs are directional: expose their existing
                            // yaw and pitch controls, just like spotlights.
                            HasDirection = true;
                            break;

                        case LightType.FogBulb:
                            HasOutRange = true;
                            break;

                        case LightType.Spot:
                            HasInRange = true;
                            HasOutRange = true;
                            HasInOutAngle = true;
                            HasDirection = true;
                            CanIlluminateGeometry = true;
                            CanCastShadows = true;
                            CanCastDynamicShadows = isTEN;
                            break;

                        case LightType.Sun:
                            HasDirection = true;
                            CanCastShadows = true;
                            CanIlluminateGeometry = true;
                            break;
                    }

                // Explain the existing caustics controls without changing the
                // layout or affecting any other light type.
                bool isCaustics = light?.Type == LightType.CausticsEffect;
                toolTip.SetToolTip(numOuterRange, isCaustics
                    ? "Caustics spread radius: smaller values keep the moving reflections local."
                    : "Outer radius or distance");
                toolTip.SetToolTip(numIntensity, isCaustics
                    ? "Caustics brightness: lower values produce subtler water reflections."
                    : "Light intensity");
                toolTip.SetToolTip(numInnerRange, isCaustics
                    ? "Full-strength caustics to this inner sphere; fade to the outer sphere."
                    : "Inner radius or distance");

                updatingCausticsControls = true;
                causticsPanel.Visible = isCaustics;
                if (isCaustics)
                {
                    causticsAmbient.Checked = light.CausticsInheritRoomAmbient;
                    causticsSpeed.Value = Math.Max(causticsSpeed.Minimum, Math.Min(causticsSpeed.Maximum, (decimal)light.CausticsScale));
                    causticsPattern.Value = Math.Max(causticsPattern.Minimum, Math.Min(causticsPattern.Maximum, (decimal)light.CausticsWidth));
                    causticsDistortion.Value = Math.Max(causticsDistortion.Minimum, Math.Min(causticsDistortion.Maximum, (decimal)light.CausticsHeight));
                }
                updatingCausticsControls = false;

                // Update enable state
                // We set it before the values to make sure that irrelevant values are
                // not recognized as being changed to 0 in the "ValueChanged" functions.
                panelLightColor.Enabled = light != null && (!isCaustics || !light.CausticsInheritRoomAmbient);
                cbLightEnabled.Enabled = light != null;
                cbLightIsObstructedByRoomGeometry.Enabled = CanCastShadows;
                cbLightCastsShadow.Enabled = CanCastDynamicShadows;
                cbLightIsDynamicallyUsed.Enabled = CanIlluminateGeometry;
                cbLightIsStaticallyUsed.Enabled = CanIlluminateGeometry;
                cbLightIsUsedForImportedGeometry.Enabled = CanIlluminateGeometry;
                numIntensity.Enabled = light != null;
                numInnerRange.Enabled = HasInRange;
                numOuterRange.Enabled = HasOutRange;
                numInnerAngle.Enabled = HasInOutAngle;
                numOuterAngle.Enabled = HasInOutAngle;
                numDirectionY.Enabled = HasDirection;
                numDirectionX.Enabled = HasDirection;

                // Update value state
                panelLightColor.BackColor = light != null ? new Vector4(light.Color * 0.5f, 1.0f).ToWinFormsColor() : BackColor;
                numIntensity.Value = (decimal)(light?.Intensity ?? 0);
                numInnerRange.Value = HasInRange ? (decimal)light.InnerRange : 0;
                numOuterRange.Value = HasOutRange ? (decimal)light.OuterRange : 0;
                numInnerAngle.Value = HasInOutAngle ? (decimal)light.InnerAngle : 0;
                numOuterAngle.Value = HasInOutAngle ? (decimal)light.OuterAngle : 0;
                numDirectionY.Value = HasDirection ? (decimal)light.RotationY : 0;
                numDirectionX.Value = HasDirection ? (decimal)light.RotationX : 0;

                cbLightEnabled.Checked = light?.Enabled ?? false;
                cbLightIsObstructedByRoomGeometry.Checked = light?.IsObstructedByRoomGeometry ?? false;
                cbLightIsDynamicallyUsed.Checked = light?.IsDynamicallyUsed ?? false;
                cbLightIsStaticallyUsed.Checked = light?.IsStaticallyUsed ?? false;
                cbLightIsUsedForImportedGeometry.Checked = light?.IsUsedForImportedGeometry ?? false;
                cbLightCastsShadow.Checked = light?.CastDynamicShadows ?? false;
                cmbLightQuality.Enabled = light != null;
                cmbLightQuality.SelectedIndex = (int)(light?.Quality ?? 0);
                cmbLightTypes.SelectedIndex = light == null ? cmbLightTypes.SelectedIndex : LightTypeIndex(light.Type);
            }

            // Update tooltip texts
            if (obj is Editor.ConfigurationChangedEvent)
            {
                if (((Editor.ConfigurationChangedEvent)obj).UpdateKeyboardShortcuts)
                    CommandHandler.AssignCommandsToControls(_editor, this, toolTip, true);
            }
        }

        private void cbLightEnabled_CheckedChanged(object sender, EventArgs e)
        {
            EditorActions.UpdateLight<bool>((light, value) => light.Enabled == value, (light, value) => light.Enabled = value,
                light => cbLightEnabled.Checked);
        }

        private void cbLightIsObstructedByRoomGeometry_CheckedChanged(object sender, EventArgs e)
        {
            EditorActions.UpdateLight<bool>((light, value) => light.IsObstructedByRoomGeometry == value, (light, value) => light.IsObstructedByRoomGeometry = value,
                light => cbLightIsObstructedByRoomGeometry.Checked);
        }

        private void cbLightIsStaticallyUsed_CheckedChanged(object sender, EventArgs e)
        {
            EditorActions.UpdateLight<bool>((light, value) => light.IsStaticallyUsed == value, (light, value) => light.IsStaticallyUsed = value,
                light => cbLightIsStaticallyUsed.Checked);
        }

        private void cbLightIsDynamicallyUsed_CheckedChanged(object sender, EventArgs e)
        {
            EditorActions.UpdateLight<bool>((light, value) => light.IsDynamicallyUsed == value, (light, value) => light.IsDynamicallyUsed = value,
                light => cbLightIsDynamicallyUsed.Checked, updateLighting: false);
        }

        private void cbLightIsUsedForImportedGeometry_CheckedChanged(object sender, EventArgs e)
        {
            EditorActions.UpdateLight<bool>((light, value) => light.IsUsedForImportedGeometry == value, (light, value) => light.IsUsedForImportedGeometry = value,
                light => cbLightIsUsedForImportedGeometry.Checked, updateLighting: false);
        }

        private void cbLightCastsShadow_CheckedChanged(object sender, EventArgs e)
        {
            EditorActions.UpdateLight<bool>((light, value) => light.CastDynamicShadows == value, (light, value) => light.CastDynamicShadows = value,
                light => cbLightCastsShadow.Checked, updateLighting: false);
        }

        private static bool Compare(float firstValue, float secondValue, NumericUpDown control)
        {
            // Check that this setting even matters for the light...
            if (!control.Enabled)
                return true;

            // Check if the value differs enough to warrant changing it
            // We don't want to polute the editor with useless event's because NumericUpDown
            // decided to round.
            for (int i = 0; i < control.DecimalPlaces; ++i)
            {
                firstValue *= 10.0f;
                secondValue *= 10.0f;
            }

            double firstValueDbl = Math.Round(firstValue);
            double secondValueDbl = Math.Round(secondValue);
            return firstValueDbl == secondValueDbl;
        }

        private void numIntensity_ValueChanged(object sender, EventArgs e)
        {
            EditorActions.UpdateLight<float>((light, value) => Compare(light.Intensity, value, numIntensity),
                (light, value) => light.Intensity = value, light => (float)numIntensity.Value);
        }

        private void numInnerRange_ValueChanged(object sender, EventArgs e)
        {
            EditorActions.UpdateLight<float>((light, value) => Compare(light.InnerRange, value, numInnerRange),
                (light, value) => light.InnerRange = value, light => (float)numInnerRange.Value);
        }

        private void numOuterRange_ValueChanged(object sender, EventArgs e)
        {
            EditorActions.UpdateLight<float>((light, value) => Compare(light.OuterRange, value, numOuterRange),
                (light, value) => light.OuterRange = value, light => (float)numOuterRange.Value);
        }

        private void numInnerAngle_ValueChanged(object sender, EventArgs e)
        {
            EditorActions.UpdateLight<float>((light, value) => Compare(light.InnerAngle, value, numInnerAngle),
                 (light, value) => light.InnerAngle = value, light => (float)numInnerAngle.Value);
        }

        private void numOuterAngle_ValueChanged(object sender, EventArgs e)
        {
            EditorActions.UpdateLight<float>((light, value) => Compare(light.OuterAngle, value, numOuterAngle),
                 (light, value) => light.OuterAngle = value, light => (float)numOuterAngle.Value);
        }

        private void numDirectionY_ValueChanged(object sender, EventArgs e)
        {
            EditorActions.UpdateLight<float>((light, value) => Compare(light.RotationY, value, numDirectionY),
                 (light, value) => light.RotationY = value, light => (float)numDirectionY.Value);
        }

        private void numDirectionX_ValueChanged(object sender, EventArgs e)
        {
            EditorActions.UpdateLight<float>((light, value) => Compare(light.RotationX, value, numDirectionX),
                 (light, value) => light.RotationX = value, light => (float)numDirectionX.Value);
        }

        private void panelLightColor_Click(object sender, EventArgs e)
        {
            EditorActions.EditLightColor(this);
        }

        private void cmbLightQualityChanged(object sender, EventArgs e)
        {
            EditorActions.UpdateLightQuality((LightQuality)cmbLightQuality.SelectedIndex);
        }

        private void butAddLight_Click(object sender, EventArgs e)
        {
            EditorActions.PlaceLight(DisplayLightTypes[cmbLightTypes.SelectedIndex]);
        }

        private void cmbLightTypes_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbLightTypes.SelectedIndex >= 0 && cmbLightTypes.SelectedIndex < DisplayLightTypes.Length)
                EditorActions.UpdateLightType(DisplayLightTypes[cmbLightTypes.SelectedIndex]);
        }
    }
}
