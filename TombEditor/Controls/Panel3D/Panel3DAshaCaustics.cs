using System;
using TombLib.Rendering;
using TombLib.Utils;
using TombLib.LevelData;
using TombLib.LevelData.SectorEnums;
using TombLib.Graphics.Primitives;
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
            if (_ashaCausticsController == null || !_ashaCausticsController.ShowPurpleSelection ||
                _editor.Level == null || _ashaCausticsController.Selection.Selected.Count == 0)
                return;

            var vertices = new List<SolidVertex>();
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

                for (int i = range.Start; i < range.Start + range.Count; i++)
                {
                    Vector3 position = room.RoomGeometry.VertexPositions[i] + room.WorldPos;
                    vertices.Add(new SolidVertex(position));
                }
            }
            if (vertices.Count == 0)
                return;

            using (var buffer = SharpDX.Toolkit.Graphics.Buffer.Vertex.New(
                _legacyDevice, vertices.ToArray(), SharpDX.Direct3D11.ResourceUsage.Dynamic))
            {
                _legacyDevice.SetBlendState(_legacyDevice.BlendStates.AlphaBlend);
                _legacyDevice.SetDepthStencilState(_legacyDevice.DepthStencilStates.DepthRead);
                _legacyDevice.SetRasterizerState(_legacyDevice.RasterizerStates.CullNone);
                _legacyDevice.SetVertexBuffer(buffer);
                _legacyDevice.SetVertexInputLayout(VertexInputLayout.FromBuffer(0, buffer));
                effect.Parameters["ModelViewProjection"].SetValue(_viewProjection.ToSharpDX());
                effect.Parameters["Color"].SetValue(new Vector4(0.65f, 0.15f, 0.9f, 0.55f));
                effect.CurrentTechnique.Passes[0].Apply();
                _legacyDevice.Draw(PrimitiveType.TriangleList, buffer.ElementCount);
                _legacyDevice.SetBlendState(_legacyDevice.BlendStates.Opaque);
                _legacyDevice.SetDepthStencilState(_legacyDevice.DepthStencilStates.Default);
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
