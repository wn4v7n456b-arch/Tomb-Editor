// Asha caustics editor-side surface selection adapter.
// Keeps authoring separate from the existing Lighting tool window.
// Integration point: TombEditor.Controls.Panel3D.PickingResultSector
// (Room, Pos, Face) as inspected in Tomb Editor source.
// No changes to approved shader, PLS, bloom or existing controller.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Asha.Editor.Caustics
{
    public struct SurfaceKey : IEquatable<SurfaceKey>
    {
        public readonly string RoomId;
        public readonly int SectorX;
        public readonly int SectorZ;
        public readonly string Face;
        public SurfaceKey(string roomId, int x, int z, string face)
        {
            RoomId = roomId ?? throw new ArgumentNullException(nameof(roomId));
            SectorX = x; SectorZ = z;
            Face = face ?? throw new ArgumentNullException(nameof(face));
        }
        public bool Equals(SurfaceKey other) =>
            RoomId == other.RoomId && SectorX == other.SectorX &&
            SectorZ == other.SectorZ && Face == other.Face;
        public override bool Equals(object obj) => obj is SurfaceKey other && Equals(other);
        public override int GetHashCode()
        {
            unchecked {
                int hash = 17;
                hash = hash * 31 + RoomId.GetHashCode();
                hash = hash * 31 + SectorX;
                hash = hash * 31 + SectorZ;
                hash = hash * 31 + Face.GetHashCode();
                return hash;
            }
        }
    }

    public sealed class SurfaceSettings
    {
        public int Fade { get; set; } = 1;
        public int Brightness { get; set; } = 3;
        public byte Red { get; set; } = 255;
        public byte Green { get; set; } = 255;
        public byte Blue { get; set; } = 255;
        public bool Inherit { get; set; }
        public SurfaceSettings Copy() => (SurfaceSettings)MemberwiseClone();
        public void Validate()
        {
            if (Fade < 1 || Fade > 5 || Brightness < 1 || Brightness > 5)
                throw new ArgumentOutOfRangeException("Caustics fade/brightness must be 1..5");
        }
    }

    public sealed class SurfaceSelectionStore
    {
        private readonly Dictionary<SurfaceKey, SurfaceSettings> selected =
            new Dictionary<SurfaceKey, SurfaceSettings>();
        public IReadOnlyDictionary<SurfaceKey, SurfaceSettings> Selected => selected;
        public event EventHandler Changed;
        public bool Paint(SurfaceKey key, SurfaceSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.Validate();
            selected[key] = settings.Copy();
            Changed?.Invoke(this, EventArgs.Empty);
            return true;
        }
        public bool Erase(SurfaceKey key)
        {
            if (!selected.Remove(key)) return false;
            Changed?.Invoke(this, EventArgs.Empty);
            return true;
        }
        public void ReplaceAll(IEnumerable<KeyValuePair<SurfaceKey, SurfaceSettings>> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            var next = new Dictionary<SurfaceKey, SurfaceSettings>();
            foreach (var entry in entries)
            {
                if (entry.Value == null) throw new ArgumentException("Missing settings");
                entry.Value.Validate();
                next.Add(entry.Key, entry.Value.Copy());
            }
            selected.Clear();
            foreach (var entry in next) selected.Add(entry.Key, entry.Value);
            Changed?.Invoke(this, EventArgs.Empty);
        }
        public void Clear()
        {
            if (selected.Count == 0) return;
            selected.Clear();
            Changed?.Invoke(this, EventArgs.Empty);
        }
        public bool IsSelected(SurfaceKey key) => selected.ContainsKey(key);

        // The editor's picker supplies Room, sector position and SectorFace.
        // Call Paint/Erase only while the dedicated caustics selection mode is active.
        // Do not use LightInstance or insert a type-5 light for every painted face.
        // Persist these keys via a stable room/face ID mapping at level export.
    }
}
