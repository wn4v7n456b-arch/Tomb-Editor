using System;
using TombLib.Rendering;
using TombLib.Utils;
using TombLib.LevelData;
using TombLib.LevelData.SectorEnums;
using TombLib.LevelData.SectorGeometry;
using TombLib.Graphics;
using TombLib.LevelData.SectorStructs;
using SharpDX.Toolkit.Graphics;
using System.Collections.Generic;
using System.Numerics;

namespace TombEditor.Controls.Panel3D
{
    public partial class Panel3D
    {
        private Asha.Editor.Caustics.CausticsEditorController _ashaCausticsController;

        public void AttachAshaCaustics(Asha.Editor.Caustics.CausticsEditorController controller)
        {
            if (_ashaCausticsController != null)
                _ashaCausticsController.ViewportRefreshRequested -= RefreshAshaCaustics;
            _ashaCausticsController = controller;
            if (controller != null)
                controller.ViewportRefreshRequested += RefreshAshaCaustics;
            Invalidate();
        }

        private void RefreshAshaCaustics(object sender, EventArgs e) => Invalidate();


        private void DrawAshaCausticsOverlay(Effect effect)
        {
            if (_ashaCausticsController == null || _editor.Level == null ||
                _ashaCausticsController.Selection.Selected.Count == 0)
                return;

            bool geometry = _editor.Mode == EditorMode.Geometry;
            bool preview = _editor.Mode == EditorMode.FaceEdit ||
                           _editor.Mode == EditorMode.Lighting;
            if (!geometry && !preview)
                return;

            var selected = new List<SolidVertex>();
            var highlights = new List<SolidVertex>();
            foreach (var entry in _ashaCausticsController.Selection.Selected)
            {
                if (!int.TryParse(entry.Key.RoomId, out int roomIndex) ||
                    roomIndex < 0 || roomIndex >= _editor.Level.Rooms.Length)
                    continue;
                Room room = _editor.Level.Rooms[roomIndex];
                if (room?.RoomGeometry == null ||
                    !Enum.TryParse(entry.Key.Face, out SectorFace face))
                    continue;

                var identity = new SectorFaceIdentity(entry.Key.SectorX, entry.Key.SectorZ, face);
                if (!room.RoomGeometry.VertexRangeLookup.TryGetValue(identity, out var range))
                    continue;

                Vector3 offset = new Vector3(room.WorldPos.X, room.WorldPos.Y, room.WorldPos.Z);
                if (geometry)
                {
                    if (!_ashaCausticsController.ShowPurpleSelection)
                        continue;
                    for (int i = range.Start; i < range.Start + range.Count; i++)
                        selected.Add(new SolidVertex(room.RoomGeometry.VertexPositions[i] + offset));
                }
                else
                {
                    // Static preview: narrow bright ribbons tessellated on the
                    // existing surface, never the purple selection material.
                    // This preview is intentionally static; gameplay animation
                    // belongs in the game renderer.
                    const int divisions = 18;
                    for (int i = range.Start; i + 2 < range.Start + range.Count; i += 3)
                    {
                        Vector3 a = room.RoomGeometry.VertexPositions[i] + offset;
                        Vector3 b = room.RoomGeometry.VertexPositions[i + 1] + offset;
                        Vector3 d = room.RoomGeometry.VertexPositions[i + 2] + offset;
                        for (int u = 0; u < divisions; u++)
                            for (int v = 0; v < divisions - u; v++)
                            {
                                Vector3 p = a + (b - a) * (u / (float)divisions) +
                                            (d - a) * (v / (float)divisions);
                                Vector3 q = a + (b - a) * ((u + 1) / (float)divisions) +
                                            (d - a) * (v / (float)divisions);
                                Vector3 s = a + (b - a) * (u / (float)divisions) +
                                            (d - a) * ((v + 1) / (float)divisions);
                                // World-space waves continue across neighbouring
                                // faces instead of restarting at each triangle.
                                Vector3 mid = (p + q + s) / 3.0f;
                                float wave = (float)(Math.Sin(mid.X * 0.018 + mid.Z * 0.012 +
                                    mid.Y * 0.006) * Math.Sin(mid.X * 0.011 -
                                    mid.Z * 0.021 + mid.Y * 0.008));
                                if (wave > 0.34f)
                                {
                                    highlights.Add(new SolidVertex(p));
                                    highlights.Add(new SolidVertex(q));
                                    highlights.Add(new SolidVertex(s));
                                }
                            }
                    }
                }
            }

            var vertices = geometry ? selected : highlights;
            if (vertices.Count == 0)
                return;
            using (var buffer = SharpDX.Toolkit.Graphics.Buffer.Vertex.New(
                _legacyDevice, vertices.ToArray(), SharpDX.Direct3D11.ResourceUsage.Dynamic))
            {
                _legacyDevice.SetBlendState(geometry ? _legacyDevice.BlendStates.Opaque :
                    _legacyDevice.BlendStates.AlphaBlend);
                _legacyDevice.SetDepthStencilState(_legacyDevice.DepthStencilStates.DepthRead);
                _legacyDevice.SetRasterizerState(_rasterizerStateDepthBias);
                _legacyDevice.SetVertexBuffer(buffer);
                _legacyDevice.SetVertexInputLayout(VertexInputLayout.FromBuffer(0, buffer));
                effect.Parameters["ModelViewProjection"].SetValue(_viewProjection.ToSharpDX());
                effect.Parameters["Color"].SetValue(geometry
                    ? new Vector4(0.72f, 0.0f, 1.0f, 1.0f)
                    : new Vector4(0.82f, 0.94f, 1.0f, 0.32f));
                effect.CurrentTechnique.Passes[0].Apply();
                _legacyDevice.Draw(PrimitiveType.TriangleList, buffer.ElementCount);
                _legacyDevice.SetBlendState(_legacyDevice.BlendStates.Opaque);
                _legacyDevice.SetDepthStencilState(_legacyDevice.DepthStencilStates.Default);
                _legacyDevice.SetRasterizerState(_legacyDevice.RasterizerStates.CullBack);
            }
        }

        private bool HandleAshaCausticsClick(int x, int y, bool erase)
        {
            if (_ashaCausticsController == null || !_ashaCausticsController.Panel.IsSelecting)
                return false;
            var picked = DoPicking(GetRay(x, y), true) as PickingResultSector;
            if (picked != null && picked.Room != null && _editor.Level != null)
            {
                int roomIndex = Array.IndexOf(_editor.Level.Rooms, picked.Room);
                if (roomIndex >= 0)
                {
                    string roomId = roomIndex.ToString();
                    string faceId = picked.Face.ToString();
                    if (erase)
                        _ashaCausticsController.ErasePickedFace(roomId, picked.Pos.X, picked.Pos.Y, faceId);
                    else
                        _ashaCausticsController.PaintPickedFace(roomId, picked.Pos.X, picked.Pos.Y, faceId);
                }
            }
            return true;
        }
    }
}
