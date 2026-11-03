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
                    // Static world-space caustic contours: clipped smooth ribbons,
                    // rather than discrete bright triangles. A shared world-space
                    // field prevents the pattern restarting at triangle boundaries.
                    const int divisions = 42;
                    const float threshold = 0.54f;
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
                                AddAshaCausticContour(highlights, p, q, s, threshold);
                                if (u + v + 1 < divisions)
                                {
                                    Vector3 t = q + s - p;
                                    AddAshaCausticContour(highlights, q, t, s, threshold);
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

        private static float AshaCausticField(Vector3 p)
        {
            // Two refracted wave families, sampled at a fixed time.
            // Shared world coordinates keep adjacent faces continuous.
            double x = p.X * 0.0095, z = p.Z * 0.0095, y = p.Y * 0.0095;
            double u = x + 0.28 * Math.Sin(z * 1.7 + y * 0.3);
            double v = z + 0.26 * Math.Sin(x * 1.5 - y * 0.4);
            return (float)(0.5 + 0.27 * Math.Cos(3.0 * u + 1.5 * Math.Sin(v * 1.8)) +
                           0.23 * Math.Cos(3.2 * v + 1.4 * Math.Sin(u * 1.7)));
        }

        private static void AddAshaCausticContour(List<SolidVertex> output,
            Vector3 a, Vector3 b, Vector3 c, float threshold)
        {
            // Clip a triangle against the bright side of the caustic contour.
            // Interpolated intersections eliminate the checkerboard triangles.
            Vector3[] points = { a, b, c };
            float[] values = { AshaCausticField(a), AshaCausticField(b), AshaCausticField(c) };
            var clipped = new List<Vector3>(4);
            for (int i = 0; i < 3; ++i)
            {
                int j = (i + 1) % 3;
                bool inside = values[i] >= threshold;
                bool nextInside = values[j] >= threshold;
                if (inside) clipped.Add(points[i]);
                if (inside != nextInside)
                {
                    float t = (threshold - values[i]) / (values[j] - values[i]);
                    clipped.Add(Vector3.Lerp(points[i], points[j], t));
                }
            }
            for (int i = 1; i + 1 < clipped.Count; i++)
            {
                output.Add(new SolidVertex(clipped[0]));
                output.Add(new SolidVertex(clipped[i]));
                output.Add(new SolidVertex(clipped[i + 1]));
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
