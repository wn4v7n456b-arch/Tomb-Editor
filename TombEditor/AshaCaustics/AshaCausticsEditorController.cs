// Glue for the standalone CausticsPanel and SurfaceSelectionStore.
// Host editor must forward actual Panel3D picking results here.
// This file deliberately does not claim that viewport hooking is complete.
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using TombLib.LevelData;
using System.Windows.Forms;

namespace Asha.Editor.Caustics
{
    public sealed class CausticsEditorController : IDisposable
    {
        private Level _boundLevel;
        private bool _loadingLevel;
        public void BindLevel(Level level)
        {
            if (ReferenceEquals(_boundLevel, level)) return;
            _boundLevel = level;
            _loadingLevel = true;
            try
            {
                Selection.ReplaceAll(level.CausticsFaces.Select(face =>
                    new System.Collections.Generic.KeyValuePair<SurfaceKey, SurfaceSettings>(
                        new SurfaceKey(face.RoomIndex.ToString(), face.SectorX, face.SectorZ, face.Face),
                        new SurfaceSettings {
                            Fade = face.Fade, Brightness = face.Brightness,
                            Red = face.Red, Green = face.Green, Blue = face.Blue,
                            Inherit = face.Inherit
                        }), resetHistory: true);
            }
            finally { _loadingLevel = false; }
        }
        private void SyncToLevel(object sender, EventArgs e)
        {
            if (_loadingLevel || _boundLevel == null) return;
            _boundLevel.CausticsFaces.Clear();
            foreach (var entry in Selection.Selected)
            {
                if (!int.TryParse(entry.Key.RoomId, out int roomIndex) ||
                    roomIndex < 0 || roomIndex >= _boundLevel.Rooms.Length ||
                    _boundLevel.Rooms[roomIndex] == null) continue;
                var settings = entry.Value;
                _boundLevel.CausticsFaces.Add(new CausticsFaceRecord {
                    RoomIndex = roomIndex, SectorX = entry.Key.SectorX,
                    SectorZ = entry.Key.SectorZ, Face = entry.Key.Face,
                    Fade = (byte)settings.Fade, Brightness = (byte)settings.Brightness,
                    Red = settings.Red, Green = settings.Green, Blue = settings.Blue,
                    Inherit = settings.Inherit
                });
            }
            TombEditor.Editor.Instance.HasUnsavedChanges = true;
        }
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
            Selection.Changed += OnPanelChanged;
            Selection.Changed += UpdatePaintedCount;
            Selection.Changed += SyncToLevel;
            Panel.SetPaintedFaceCount(Selection.Selected.Count);
        }
        private void UpdatePaintedCount(object sender, EventArgs e) => Panel.SetPaintedFaceCount(Selection.Selected.Count);
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
            Selection.Changed -= OnPanelChanged;
            Selection.Changed -= UpdatePaintedCount;
            Selection.Changed -= SyncToLevel;
        }
    }
}
