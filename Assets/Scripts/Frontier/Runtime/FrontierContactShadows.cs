using UnityEngine;

namespace Starfall
{
    public sealed partial class PixelArt
    {
        private const string ContactShadowKey = "contact_shadow";

        public void PrewarmContactShadow()
        {
            if (cache.ContainsKey(ContactShadowKey)) return;
            const int w = 64, h = 16;
            Begin(w, h);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float nx = (x + .5f - w / 2f) / (w / 2f);
                float ny = (y + .5f - h / 2f) / (h / 2f);
                float radius = nx * nx + ny * ny;
                if (radius >= 1) continue;
                // Discrete translucent bands keep the existing pixel-art
                // sampling while the ground remains visible through the edge.
                float alpha = radius < .20f ? 1 : radius < .42f ? .8f : radius < .64f ? .52f : radius < .82f ? .26f : .08f;
                Box(x, y, 1, 1, new Color(1, 1, 1, alpha));
            }
            End(ContactShadowKey);
        }

        public Texture2D ContactShadow()
        {
            // No first-actor allocation, including after Dispose.
            Texture2D texture;
            return cache.TryGetValue(ContactShadowKey, out texture) ? texture : null;
        }
    }

    public sealed partial class FrontierRuntime
    {
        private void DrawContactShadow(Point foot, float width, float height, float topOffset, Color tint)
        {
            // HUD silhouettes must not repeat ground shading above a panel.
            if (drawingOutline) return;
            Texture2D texture = art.ContactShadow();
            if (texture != null) Image(texture, foot.x - width / 2, foot.y + topOffset, width, height, tint);
        }
    }
}
