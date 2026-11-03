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
            if (picked != null && picked.Room != null)
            {
                if (erase)
                    _ashaCausticsController.ErasePickedFace(picked.Room.Name, picked.Pos.X, picked.Pos.Y, picked.Face.ToString());
                else
                    _ashaCausticsController.PaintPickedFace(picked.Room.Name, picked.Pos.X, picked.Pos.Y, picked.Face.ToString());
            }
            return true;
        }
    }
}
