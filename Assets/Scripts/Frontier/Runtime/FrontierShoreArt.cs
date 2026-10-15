using System;
using UnityEngine;

namespace Starfall
{
    public sealed partial class PixelArt
    {
        private const string ReachShoreReadyKey = "reach_shore_240_1";
        private readonly Texture2D[] reachShoreMasks = new Texture2D[256 * 2];

        // Bits describe neighbouring land, clockwise from north. A diagonal is
        // useful only when both adjoining cardinal neighbours are water.
        public static int NormalizeReachShoreMask(int mask)
        {
            mask &= 255;
            if ((mask & 3) != 0) mask &= ~16;
            if ((mask & 6) != 0) mask &= ~32;
            if ((mask & 12) != 0) mask &= ~64;
            if ((mask & 9) != 0) mask &= ~128;
            return mask;
        }

        public void PrewarmReachShores()
        {
            if (cache.ContainsKey(ReachShoreReadyKey)) return;
            // There are 47 canonical topologies, including transparent open
            // water. The other 46 need two 48px masks: 828 KiB of RGBA32.
            for (int mask = 1; mask < 256; mask++)
            {
                if (NormalizeReachShoreMask(mask) != mask) continue;
                for (int variant = 0; variant < 2; variant++)
                {
                    string key = "reach_shore_" + mask + "_" + variant;
                    Texture2D texture;
                    if (!cache.TryGetValue(key, out texture)) texture = BuildReachShore(mask, variant, key);
                    reachShoreMasks[mask * 2 + variant] = texture;
                }
            }
        }

        public Texture2D ReachShore(int mask, int variant)
        {
            // Never create textures in OnGUI. The fixed key also makes a
            // disposed PixelArt stop returning its destroyed array entries.
            if (!cache.ContainsKey(ReachShoreReadyKey)) return null;
            return reachShoreMasks[NormalizeReachShoreMask(mask) * 2 + (variant & 1)];
        }

        private Texture2D BuildReachShore(int mask, int variant, string key)
        {
            const int side = 48;
            Begin(side, side);
            for (int y = 0; y < side; y++) for (int x = 0; x < side; x++)
            {
                // Both sides of a shared tile edge sample the same endpoint.
                // The 16px detail rhythm divides 48, so adjoining masks agree
                // even when one describes a straight edge and one a corner.
                int sx = x == side - 1 ? side : x, sy = y == side - 1 ? side : y;
                float distance = ReachShoreDistance(mask, sx, sy);
                if (distance >= 16) continue;
                float depth = distance;
                if (distance > 2.5f)
                {
                    int phase = (sx + sy) % 16;
                    depth += phase < 4 ? -.65f : phase < 10 ? .35f : .8f;
                    // Alternate only the submerged interior. The two-pixel
                    // border, dry boundary and shared endpoints stay identical.
                    if (x > 1 && x < side - 2 && y > 1 && y < side - 2)
                        depth += ((x / 6 + y / 7 + variant * 2) % 5 - 2) * .45f;
                }

                Color color;
                if (distance < .8f) color = new Color(.10f, .24f, .29f, .43f);
                else if (distance < 2.2f) color = new Color(.14f, .32f, .36f, .30f);
                else if (depth < 5) color = new Color(.36f, .60f, .55f, .27f);
                else if (depth < 8) color = new Color(.32f, .58f, .55f, .20f);
                else if (depth < 11) color = new Color(.29f, .54f, .53f, .12f);
                else if (depth < 14) color = new Color(.27f, .51f, .52f, .06f);
                else color = new Color(.26f, .49f, .51f, .025f);

                int foam = (sx + sy) % 16;
                if (depth >= 3.8f && depth < 4.8f && (foam >= 3 && foam <= 5 || foam == 12))
                    color = new Color(.65f, .80f, .73f, .38f);
                Box(x, y, 1, 1, color);
            }
            return End(key);
        }

        private static float ReachShoreDistance(int mask, float x, float y)
        {
            float distance = 100;
            if ((mask & 1) != 0) ReachShoreJoin(ref distance, y);
            if ((mask & 2) != 0) ReachShoreJoin(ref distance, 48 - x);
            if ((mask & 4) != 0) ReachShoreJoin(ref distance, 48 - y);
            if ((mask & 8) != 0) ReachShoreJoin(ref distance, x);
            if ((mask & 16) != 0) ReachShoreJoin(ref distance, (float)Math.Sqrt((48 - x) * (48 - x) + y * y));
            if ((mask & 32) != 0) ReachShoreJoin(ref distance, (float)Math.Sqrt((48 - x) * (48 - x) + (48 - y) * (48 - y)));
            if ((mask & 64) != 0) ReachShoreJoin(ref distance, (float)Math.Sqrt(x * x + (48 - y) * (48 - y)));
            if ((mask & 128) != 0) ReachShoreJoin(ref distance, (float)Math.Sqrt(x * x + y * y));
            return distance;
        }

        private static void ReachShoreJoin(ref float distance, float next)
        {
            // A small rounded union softens the inside turn between two banks;
            // diagonal-only banks use the circular distance to the land corner.
            // Blend only within the shallow band's support. Otherwise two
            // equally distant banks would pull transparent deep water back
            // into the mask (for example 17px + 17px used to become 15px).
            float nearest = Math.Min(distance, next);
            if (nearest >= 16) { distance = nearest; return; }
            float join = Math.Max(0, 1 - Math.Abs(distance - next) / 8);
            distance = nearest - join * join * 2;
        }
    }
}
