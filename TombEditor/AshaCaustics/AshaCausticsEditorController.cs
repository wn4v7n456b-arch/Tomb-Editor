// Glue for the standalone CausticsPanel and SurfaceSelectionStore.
// Host editor must forward actual Panel3D picking results here.
// This file deliberately does not claim that viewport hooking is complete.
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Asha.Editor.Caustics
{
    public sealed class CausticsEditorController : IDisposable
    {
        public CausticsPanel Panel { get; }
        public SurfaceSelectionStore Selection { get; }
        public bool ShowPurpleSelection => Panel.IsSelecting;
        public bool ShowStaticTexturedPreview => !Panel.IsSelecting && Panel.Preview;
        public event EventHandler ViewportRefreshRequested;

        public CausticsEditorController(CausticsPanel panel, SurfaceSelectionStore selection)
        {
            Panel = panel ?? throw new ArgumentNullException(nameof(panel));
            Selection = selection ?? throw new ArgumentNullException(nameof(selection));
            Panel.SettingsChanged += OnPanelChanged;
            Panel.SelectionModeChanged += OnPanelChanged;
            Panel.PreviewChanged += OnPanelChanged;
            Panel.ClearSelectionRequested += OnClear;
            Panel.SaveRequested += OnSave;
            Panel.LoadRequested += OnLoad;
            Selection.Changed += OnPanelChanged;
        }
        private void OnPanelChanged(object sender, EventArgs e) =>
            ViewportRefreshRequested?.Invoke(this, EventArgs.Empty);
        private void OnClear(object sender, EventArgs e) => Selection.Clear();
        private void OnSave(object sender, EventArgs e)
        {
            using (var dialog = new SaveFileDialog { Filter = "Asha caustics selections (*.acsf)|*.acsf", DefaultExt = "acsf", AddExtension = true })
                if (dialog.ShowDialog(Panel) == DialogResult.OK)
                    using (var writer = new StreamWriter(dialog.FileName))
                        CausticsSelectionFile.Save(writer, Selection);
        }
        private void OnLoad(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog { Filter = "Asha caustics selections (*.acsf)|*.acsf", CheckFileExists = true })
                if (dialog.ShowDialog(Panel) == DialogResult.OK)
                    using (var reader = new StreamReader(dialog.FileName))
                        CausticsSelectionFile.Load(reader, Selection);
        }
        public void PaintPickedFace(string stableRoomId, int sectorX, int sectorZ, string face)
        {
            if (!Panel.IsSelecting) return;
            var c = Panel.Tint;
            Selection.Paint(new SurfaceKey(stableRoomId, sectorX, sectorZ, face),
                new SurfaceSettings {
                    Fade = Panel.EdgeFade,
                    Brightness = Panel.Brightness,
                    Red = c.R, Green = c.G, Blue = c.B,
                    Inherit = Panel.Inherit
                });
        }
        public void ErasePickedFace(string stableRoomId, int sectorX, int sectorZ, string face)
        {
            if (Panel.IsSelecting)
                Selection.Erase(new SurfaceKey(stableRoomId, sectorX, sectorZ, face));
        }
        public void Dispose()
        {
            Panel.SettingsChanged -= OnPanelChanged;
            Panel.SelectionModeChanged -= OnPanelChanged;
            Panel.PreviewChanged -= OnPanelChanged;
            Panel.ClearSelectionRequested -= OnClear;
            Panel.SaveRequested -= OnSave;
            Panel.LoadRequested -= OnLoad;
            Selection.Changed -= OnPanelChanged;
        }
    }
}
