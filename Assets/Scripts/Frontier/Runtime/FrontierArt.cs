using System;
using UnityEngine;
namespace Starfall
{
    public sealed partial class PixelArt
    {
        public Texture2D Tile(int room, char tile, int variant, Color ground)
        {
            string key = "terrain_" + room + tile + variant; Texture2D cached; if (cache.TryGetValue(key, out cached)) return cached;
            Begin(32, 32); var random = new System.Random(room * 137 + variant * 53 + tile);
            Color shade = Color.Lerp(ground, C("#142936"), .24f), light = Color.Lerp(ground, C("#d6d5a5"), .2f);
            if (tile == 'v' || tile == 'w' || tile == 'b')
            {
                Box(0, 0, 32, 32, tile == 'w' ? C("#31566d") : C("#344955"));
                for (int i = 0; i < 4; i++) { int x = 3 + i * 8; Box(x, 3, 3, 26, tile == 'v' ? C("#70a080") : tile == 'b' ? C("#bca77a") : C("#87bdc2")); Box(x - 2, 10 + i * 3, 7, 2, C("#e2cc9c")); }
            }
            else if (tile == '~')
            {
                Color water = C(room == 4 || room == 8 ? "#252f4c" : "#31566d"); Box(0, 0, 32, 32, water);
                for (int n = 0; n < 7; n++) { int x = random.Next(26), y = random.Next(31); Box(x, y, random.Next(4, 11), 1, Color.Lerp(water, C("#8ba9af"), .15f + n % 3 * .12f)); }
            }
            else if (tile == '#')
            {
                Box(0, 0, 32, 32, C("#344955")); Box(2, 2, 28, 26, C(room == 7 ? "#869eab" : "#64747d")); Box(3, 2, 26, 3, C(room == 7 ? "#c5d2d2" : "#8b9798")); Box(3, 27, 28, 5, C("#40525e"));
                Box(17, 9, 2, 13, C("#4b606b")); Box(7, 17, 10, 1, C("#89958e")); Box(19, 10, 5, 1, C("#a2afa2"));
            }
            else if (tile == '=')
            {
                Color path = C(room == 2 ? "#7b8c86" : room == 3 ? "#aa8c6a" : room == 6 ? "#c3ad80" : "#aaa68b"); Box(0, 0, 32, 32, path);
                for (int row = 0; row < 2; row++) { Box(0, row * 16, 32, 2, Color.Lerp(path, C("#4d5c53"), .25f)); int x = (row * 12 + variant * 3 + 5) % 32; Box(x, row * 16 + 2, 1, 14, Color.Lerp(path, C("#53574e"), .22f)); Box(x + 2, row * 16 + 3, 9, 1, Color.Lerp(path, Color.white, .15f)); }
            }
            else if (tile == '%' || room == 6)
            {
                Color sand = tile == '%' ? C("#9aab97") : Color.Lerp(ground, C("#d0bc89"), .15f + variant * .05f); Box(0, 0, 32, 32, sand);
                for (int n = 0; n < 5; n++) { int x = random.Next(15), y = n * 6 + variant % 3; Box(x, y, 17, 1, Color.Lerp(sand, n % 2 == 0 ? C("#e0d0a0") : C("#7e795e"), .2f)); }
            }
            else if (room == 7)
            {
                Color snow = Color.Lerp(C("#b8cad0"), ground, variant * .12f); Box(0, 0, 32, 32, snow);
                for (int n = 0; n < 8; n++) { int x = random.Next(28), y = random.Next(29); Box(x, y, 5, 2, n % 3 == 0 ? C("#809cab") : C("#dce1d8")); }
            }
            else
            {
                Box(0, 0, 32, 32, Color.Lerp(ground, shade, variant * .04f));
                for (int n = 0; n < 13; n++) { int x = random.Next(29), y = random.Next(29); Box(x, y, 3, 1, shade); Box(x + 1, y - 2, 1, 3, light); if (n % 3 == 0) Box(x + 2, y - 1, 1, 2, light); }
                if (room < 2 && variant == 2) { Box(6, 18, 2, 2, C("#dec792")); Box(18, 6, 2, 2, C("#bc9fba")); }
                if (room == 8) { Box(4, 10, 8, 1, C("#568183")); Box(12, 9, 1, 7, C("#4b747b")); }
            }
            return End(key);
        }
        private void PolishProp(string kind, int variant)
        {
            // Directional palette shading and tiny texture clusters retain the original silhouettes.
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int n = (height - 1 - y) * width + x; Color c = pixels[n]; if (c.a < .8f) continue;
                float direction = (x / (float)width + y / (float)height) * .055f;
                pixels[n] = Color.Lerp(c, C("#17273d"), direction);
            }
            if (kind == "tree" || kind == "palm" || kind == "snowpine")
            {
                var random = new System.Random(variant * 23 + kind.Length * 37);
                for (int n = 0; n < 48; n++) { int x = random.Next(18, 78), y = random.Next(18, 65), index = (height - 1 - y) * width + x; if (pixels[index].a < .9f) continue; Color c = Color.Lerp(pixels[index], kind == "snowpine" ? C("#e4e4d6") : C("#b1be82"), .24f); Box(x, y, 3, 1, c); }
            }
            if (kind == "house") { for (int y = 18; y < 45; y += 7) for (int x = 20 + y % 2 * 3; x < 76; x += 10) { int i = (height - 1 - y) * width + x; if (pixels[i].a > .9f) Box(x, y, 1, 4, Color.Lerp(pixels[i], C("#342e40"), .25f)); } }
        }
        public Texture2D Glow()
        {
            Texture2D t; if (cache.TryGetValue("soft_glow", out t)) return t; Begin(64, 64);
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) { float r = (float)Math.Sqrt((x - 31.5f) * (x - 31.5f) + (y - 31.5f) * (y - 31.5f)) / 32; float a = Math.Max(0, 1 - r); Box(x, y, 1, 1, new Color(1, 1, 1, a * a * .55f)); }
            t = End("soft_glow"); t.filterMode = FilterMode.Bilinear; return t;
        }
        public Texture2D Icon(string id)
        {
            string key = "icon_" + id; Texture2D t; if (cache.TryGetValue(key, out t)) return t; Begin(48, 48);
            Color ivory = C("#eadbc1"), gold = C("#d5b57c"), teal = C("#83bca5"), dark = C("#263f52"), wood = C("#967650");
            Disc(24, 40, 16, 3, new Color(.015f, .035f, .08f, .25f));
            if (id == "sword") { for (int n = 0; n < 25; n++) { Box(12 + n, 32 - n, 4, 4, ivory); Box(14 + n, 33 - n, 2, 3, C("#88a7ae")); } Box(9, 30, 15, 3, gold); Box(9, 32, 5, 9, wood); }
            else if (id == "bow") { for (int n = 0; n < 31; n++) { int x = 14 + (int)(Math.Sin(n / 30f * Math.PI) * 16); Box(x, 7 + n, 4, 2, gold); } Box(15, 7, 1, 31, ivory); Box(7, 23, 34, 2, ivory); Box(36, 20, 5, 7, teal); }
            else if (id == "staff") { Box(21, 17, 5, 25, wood); Disc(24, 13, 10, 10, gold); Disc(24, 12, 7, 7, teal); Box(21, 6, 3, 9, ivory); }
            else if (id == "wood") { Box(8, 24, 31, 13, wood); Disc(37, 30, 7, 7, gold); Disc(37, 30, 4, 4, wood); Box(8, 26, 24, 2, gold); Box(10, 18, 27, 8, C("#78624c")); }
            else if (id == "crystal" || id == "relic") { for (int n = 0; n < 30; n++) { int w = n < 12 ? n / 2 : Math.Max(1, (30 - n) / 2); Box(25 - w, 6 + n, w * 2, 1, id == "relic" ? gold : teal); Box(25 - w, 6 + n, Math.Max(1, w), 1, ivory); } }
            else if (id == "herb" || id == "seed") { Box(23, 15, 3, 25, wood); Disc(16, 24, 9, 5, teal); Disc(31, 18, 9, 5, teal); if (id == "herb") { Disc(24, 10, 6, 6, ivory); Disc(24, 10, 2, 2, gold); } else { Disc(22, 37, 12, 5, gold); } }
            else if (id == "fish") { Disc(23, 25, 15, 8, C("#83acbd")); Box(36, 19, 7, 13, teal); Box(9, 21, 3, 3, dark); Box(18, 19, 14, 2, ivory); }
            else if (id == "gear") { Disc(24, 24, 14, 14, gold); for (int n = 0; n < 8; n++) { double a = n * Math.PI / 4; Box(21 + (int)(Math.Cos(a) * 15), 21 + (int)(Math.Sin(a) * 15), 7, 7, gold); } Disc(24, 24, 7, 7, dark); Disc(24, 24, 4, 4, ivory); }
            else if (id == "tea" || id == "stew") { Disc(24, 30, 14, 10, ivory); Box(10, 22, 29, 9, ivory); Disc(24, 22, 14, 5, id == "tea" ? teal : wood); Box(36, 23, 7, 9, gold); Box(19, 8, 2, 8, gold); Box(26, 6, 2, 9, ivory); }
            else if (id == "shield") { Box(10, 8, 28, 21, dark); for (int n = 0; n < 12; n++) Box(10 + n, 29 + n, 28 - n * 2, 1, dark); Box(12, 10, 24, 4, gold); Box(23, 12, 3, 24, teal); }
            else if (id == "companion") { Disc(18, 15, 7, 7, ivory); Disc(32, 18, 6, 6, gold); Box(8, 26, 20, 13, teal); Box(27, 29, 14, 10, wood); }
            else if (id == "camp") { for (int n = 0; n < 28; n++) Box(24 - n / 2, 11 + n, n, 1, gold); Box(22, 25, 8, 15, dark); }
            else { Box(9, 10, 30, 30, wood); Box(12, 10, 26, 27, ivory); Box(15, 15, 19, 2, gold); Box(15, 22, 16, 1, teal); Box(15, 29, 20, 1, teal); Box(10, 10, 3, 30, teal); }
            return End(key);
        }
    }
}
