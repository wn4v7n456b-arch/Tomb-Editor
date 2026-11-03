using System;
using TombLib.Rendering;

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
