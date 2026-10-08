using UnityEngine;
namespace Starfall
{
    public static class GuiGeometry
    {
        // Rotate in the game's logical canvas, then apply the viewport scale
        // and letterbox offset. A screen-space pivot moves effects at any
        // resolution other than the reference canvas.
        public static Matrix4x4 RotateInCanvas(Matrix4x4 canvas, Rect rect, float angle)
        {
            Vector3 pivot = new Vector3(rect.x + rect.width / 2, rect.y + rect.height / 2, 0);
            return canvas * Matrix4x4.TRS(pivot, Quaternion.Euler(0, 0, angle), new Vector3(1, 1, 1))
                * Matrix4x4.TRS(new Vector3(-pivot.x, -pivot.y, 0), Quaternion.identity, new Vector3(1, 1, 1));
        }
    }
}
