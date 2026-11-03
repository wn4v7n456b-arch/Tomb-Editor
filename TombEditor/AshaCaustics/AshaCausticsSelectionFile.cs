// Editor-side persistence of painted face selections.
// Distinct from the runtime UV polygon format: a face picker cannot infer
// stable runtime surface IDs or world-space UV outlines by itself.
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Asha.Editor.Caustics
{
    public static class CausticsSelectionFile
    {
        private const string Header = "ASHA_CAUSTICS_FACE_SELECTIONS 1";

        // One tab-separated row per selected face. Base64 protects room/face
        // identifiers against tabs, newlines and non-ASCII characters.
        public static void Save(TextWriter writer, SurfaceSelectionStore store)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (store == null) throw new ArgumentNullException(nameof(store));
            writer.WriteLine(Header);
            foreach (var entry in store.Selected.OrderBy(e => e.Key.RoomId, StringComparer.Ordinal)
                         .ThenBy(e => e.Key.SectorX).ThenBy(e => e.Key.SectorZ)
                         .ThenBy(e => e.Key.Face, StringComparer.Ordinal))
            {
                var key = entry.Key;
                var s = entry.Value;
                s.Validate();
                writer.WriteLine(string.Join("\t", new[] {
                    Encode(key.RoomId), key.SectorX.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    key.SectorZ.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Encode(key.Face), s.Fade.ToString(), s.Brightness.ToString(),
                    s.Red.ToString(), s.Green.ToString(), s.Blue.ToString(),
                    s.Inherit ? "1" : "0"
                }));
            }
        }

        // Parse fully before mutating the live selection. Invalid files leave
        // the existing editor selection unchanged.
        public static void Load(TextReader reader, SurfaceSelectionStore store)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            if (store == null) throw new ArgumentNullException(nameof(store));
            if (reader.ReadLine() != Header) throw new InvalidDataException("Unsupported caustics selection format");
            var rows = new System.Collections.Generic.Dictionary<SurfaceKey, SurfaceSettings>();
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (rows.Count >= 65536) throw new InvalidDataException("Too many selected caustics faces");
                var fields = line.Split('\t');
                if (fields.Length != 10) throw new InvalidDataException("Invalid caustics face record");
                int Number(int i) => int.Parse(fields[i], System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture);
                var key = new SurfaceKey(Decode(fields[0]), Number(1), Number(2), Decode(fields[3]));
                var settings = new SurfaceSettings {
                    Fade = Number(4), Brightness = Number(5),
                    Red = Byte(fields[6]), Green = Byte(fields[7]), Blue = Byte(fields[8]),
                    Inherit = fields[9] == "1"
                };
                if (fields[9] != "0" && fields[9] != "1")
                    throw new InvalidDataException("Invalid inherit value");
                settings.Validate();
                if (string.IsNullOrEmpty(key.RoomId) || string.IsNullOrEmpty(key.Face) ||
                    rows.ContainsKey(key))
                    throw new InvalidDataException("Empty or duplicate face identifier");
                rows.Add(key, settings);
            }
            store.ReplaceAll(rows);
        }

        private static byte Byte(string text) => byte.Parse(text,
            System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture);
        private static string Encode(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        private static string Decode(string text) => new UTF8Encoding(false, true).GetString(Convert.FromBase64String(text));
    }
}
