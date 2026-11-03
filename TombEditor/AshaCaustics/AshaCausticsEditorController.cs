// Glue for the standalone CausticsPanel and SurfaceSelectionStore.
// Host editor must forward actual Panel3D picking results here.
// This file deliberately does not claim that viewport hooking is complete.
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
using TombLib.LevelData;
using TombEditor.Controls;

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
                var validFaces = (level?.CausticsFaces ?? Enumerable.Empty<CausticsFaceRecord>())
                    .Where(face => face != null && face.RoomIndex >= 0 &&
                        face.RoomIndex < level.Rooms.Length && level.Rooms[face.RoomIndex] != null &&
                        face.SectorX >= 0 && face.SectorX < level.Rooms[face.RoomIndex].NumXSectors &&
                        face.SectorZ >= 0 && face.SectorZ < level.Rooms[face.RoomIndex].NumZSectors &&
                        !string.IsNullOrWhiteSpace(face.Face) &&
                        face.Fade >= 1 && face.Fade <= 5 &&
                        face.Brightness >= 1 && face.Brightness <= 5);
                // Duplicate face records can arise from hand-edited or imported projects.
                // Keep the last entry instead of failing to open the painting panel.
                Selection.ReplaceAll(validFaces
                    .GroupBy(face => new SurfaceKey(face.RoomIndex.ToString(),
                        face.SectorX, face.SectorZ, face.Face))
                    .Select(group => {
                        var face = group.Last();
                        return new System.Collections.Generic.KeyValuePair<SurfaceKey, SurfaceSettings>(
                            group.Key, new SurfaceSettings {
                                Fade = face.Fade, Brightness = face.Brightness,
                                Red = face.Red, Green = face.Green, Blue = face.Blue,
                                Inherit = face.Inherit
                            });
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
                    _boundLevel.Rooms[roomIndex] == null ||
                    entry.Key.SectorX < 0 || entry.Key.SectorX >= _boundLevel.Rooms[roomIndex].NumXSectors ||
                    entry.Key.SectorZ < 0 || entry.Key.SectorZ >= _boundLevel.Rooms[roomIndex].NumZSectors) continue;
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
            // Sector paint is not a room-geometry edit, so the 2D grid will
            // not receive its normal geometry-changed invalidation event.
            // Refresh the visible floor grid immediately after paint/erase.
            foreach (Form form in Application.OpenForms)
                RefreshCausticsGrids(form);
        }
        private static void RefreshCausticsGrids(Control parent)
        {
            if (parent is Panel2DGrid)
                parent.Invalidate();
            foreach (Control child in parent.Controls)
                RefreshCausticsGrids(child);
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
        private void OnClear(object sender, EventArgs e)
        {
            if (Selection.Selected.Count == 0) return;
            if (MessageBox.Show(Panel,
                    "Remove all painted Caustics faces from this level? You can undo this action.",
                    "Clear Caustics painting", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) == DialogResult.Yes)
                Selection.Clear();
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
