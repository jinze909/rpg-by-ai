using System;
using UnityEngine;
namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private static readonly int[] dryDetails = { 2, 4, 5, 8 }, snowDetails = { 2, 4, 9 }, ruinDetails = { 2, 3, 4, 8 }, greenDetails = { 0, 1, 3, 4, 10 };
        private int SurfaceId(char tile)
        {
            int room = Game.ArtRoom;
            if (tile == '~') return 8;
            if (tile == '%') return room == 5 && !Game.LowTide ? 8 : 13;
            if (tile == '#') return room == 3 || room == 6 ? 10 : room == 7 ? 14 : 9;
            if (room == 9) return 11;
            if (tile == '=') return room == 5 ? 11 : room == 6 ? 2 : room == 3 ? 6 : room == 4 ? 12 : room == 8 ? 7 : room == 2 ? 15 : 3;
            return room == 1 ? 1 : room == 2 ? 15 : room == 3 ? 6 : room == 4 ? 12 : room == 5 ? 13 : room == 6 ? 4 : room == 7 ? 5 : room == 8 ? 7 : 0;
        }
        private static int TileSeed(int x, int y, int room)
        { unchecked { int n = x * 73856093 ^ y * 19349663 ^ (room + 1) * 83492791; return n & 0x7fffffff; } }
        private void DrawRegionalAir()
        {
            if (Game.save.reducedMotion || Game.save.effectsQuality <= 0) return;
            int room = Game.save.room;
            if (room != 3 && room != 6 && room != 7)
            {
                // Keep the existing woodland motes and all other regions exact.
                for (int i = 0; i < 18; i++)
                {
                    float x = (i * 137 + Mathf.Sin(clock * .35f + i) * 45 + Width) % Width;
                    float y = (i * 83 - clock * 6 + Height * 100) % Height;
                    Box(x, y, 2, 2, new Color(.96f, .87f, .6f, .45f));
                }
                return;
            }
            // Analytic, sparse layers: no particles to spawn, accumulate or save.
            // Use the same presentation clock as the old motes/snow, including
            // menu backgrounds. Application/review pause still stops that clock.
            bool snow = room == 7, sand = room == 6;
            int count = snow ? 28 : sand ? 18 : 12;
            if (Game.save.effectsQuality == 1) count /= 2;
            Point origin = WorldOrigin;
            for (int i = 0; i < count; i++)
            {
                int seed = TileSeed(i, room, room);
                bool near = (i & 1) == 0;
                float layer = near ? .35f : .15f, phase = seed % 997 * .01f;
                float drift = snow ? (near ? 9 : 5) : sand ? (near ? 26 : 18) : 4;
                float fall = snow ? (near ? 27 : 16) : sand ? -2 : -5;
                float sway = (float)Math.Sin(clock * (snow ? .45f : .27f) + phase) * (snow ? 7 : 3);
                float x = AirWrap(seed % 1301 + clock * drift + sway + origin.x * layer, Width + 48) - 24;
                float y = AirWrap(seed / 1301 % 821 + clock * fall + origin.y * layer, Height + 48) - 24;
                float edge = Mathf.Clamp01(Math.Min(Math.Min(x + 24, Width + 24 - x), Math.Min(y + 24, Height + 24 - y)) / 24);
                float alpha = (snow ? (near ? .4f : .23f) : sand ? (near ? .23f : .13f) : (near ? .26f : .15f)) * edge;
                x = (float)Math.Round(x); y = (float)Math.Round(y);
                Color tint = snow ? new Color(.82f, .91f, .96f, alpha) : sand ? new Color(.71f, .58f, .38f, alpha) : new Color(.66f, .62f, .57f, alpha);
                Box(x, y, snow ? (near ? 3 : 2) : sand ? (near ? 5 : 3) : 2, snow ? 2 : 1, tint);
                // Tiny broken clusters, not opaque streaks across warning lines.
                if (snow && near) Box(x + 1, y - 1, 1, 1, new Color(tint.r, tint.g, tint.b, alpha * .65f));
                else if (!snow && i % 3 == 0) Box(x - 3, y + 1, sand ? 2 : 1, 1, new Color(tint.r, tint.g, tint.b, alpha * .45f));
            }
        }
        private static float AirWrap(float value, float span)
        { return value - (float)Math.Floor(value / span) * span; }
        private void DrawSurface(int x, int y, char tile, Point p)
        {
            if (terrainSheet == null) { Image(art.Tile(Game.ArtRoom, tile, (x * 7 + y * 11) % 4, PixelArt.C(Game.Map.color)), p.x, p.y, Tile + .5f, Tile + .5f); return; }
            int seed = TileSeed(x, y, Game.save.room), id = SurfaceId(tile);
            float value = .97f + seed % 5 * .014f;
            int row = id == 0 ? 0 : id == 1 ? 1 : id == 3 ? 2 : id == 8 ? 3 : -1;
            if (row >= 0 && terrainVariants != null) Atlas(terrainVariants, 4, 4, row * 4 + seed % 4, new Rect(p.x, p.y, Tile, Tile), new Color(value, value, value), (seed & 4) == 0);
            else Atlas(terrainSheet, 8, 2, id, new Rect(p.x, p.y, Tile, Tile), new Color(value, value, value), id != 11 && id != 9 && (seed & 1) == 1);
            if (tile == '=' && Game.save.room < 2)
            {
                // Dark, narrow soil edges make the road shape read above busy grass.
                if (x > 0 && Game.PresentationTile(x - 1, y) == '.') Box(p.x, p.y, 2, Tile, new Color(.17f, .22f, .15f, .28f));
                if (x + 1 < Game.Map.rows[y].Length && Game.PresentationTile(x + 1, y) == '.') Box(p.x + Tile - 2, p.y, 2, Tile, new Color(.17f, .22f, .15f, .28f));
                if (y > 0 && Game.PresentationTile(x, y - 1) == '.') Box(p.x, p.y, Tile, 2, new Color(.17f, .22f, .15f, .28f));
                if (y + 1 < Game.Map.rows.Length && Game.PresentationTile(x, y + 1) == '.') Box(p.x, p.y + Tile - 2, Tile, 2, new Color(.17f, .22f, .15f, .28f));
            }
        }
        private void DrawGroundDetail(int x, int y, char tile, Point p)
        {
            if (detailSheet == null || Game.save.room == 9 || tile != '.' && tile != '%') return;
            int seed = TileSeed(x, y, Game.save.room);
            if (seed % 17 != 0) return;
            Point world = new Point(x + .35f + seed % 5 * .07f, y + .65f);
            // Persistent world decoration: approaching actors cannot erase tiles.
            foreach (WorldObject item in Game.Map.objects) if ((world - new Point(item.x, item.y)).Length < 1.4f) return;
            int room = Game.ArtRoom;
            int[] options = room == 6 || room == 3 || room == 8 ? dryDetails : room == 7 ? snowDetails : room == 2 || room == 4 ? ruinDetails : greenDetails;
            int id = options[(seed / 17) % options.Length];
            if (id == 9) id = 4; // Large fallen trunks belong to fixed solid edge scenery.
            float size = id == 10 ? 33 : id == 8 ? 38 : 23;
            Actor(detailSheet, 4, 4, id, ScreenPoint(world), size, new Color(1, 1, 1, .8f), (seed & 1) == 1);
        }
        private void DrawRootedPlant(Decoration prop, Rect rect, Color tint)
        {
            if (worldProps == null) { PropImage(prop.kind, prop.variant, rect.x, rect.y, rect.width, rect.height, tint); return; }
            float side=Math.Min(rect.width,rect.height); rect=new Rect(rect.x+(rect.width-side)/2,rect.y+rect.height-side,side,side);
            int id = prop.kind == "palm" ? 5 : prop.kind == "snowpine" ? 6 : 0;
            int seed = TileSeed((int)(prop.x * 7), (int)(prop.y * 7), Game.save.room);
            float phase = seed % 997 * .01f, speed = .8f + seed % 7 * .07f, amplitude = .65f + seed % 5 * .12f;
            Color previous = GUI.color;
            try
            {
                GUI.color = tint;
                const int strips = 32;
                for (int i = 0; i < strips; i++)
                {
                    float y = i / (float)strips, next = (i + 1) / (float)strips;
                    float weight = y < .65f ? (1 - y / .65f) * (1 - y / .65f) : 0;
                    float wind = Game.save.reducedMotion || Game.save.effectsQuality == 0 ? 0 : (float)Math.Sin(clock * speed + phase + y * 2) * amplitude * weight;
                    GUI.DrawTextureWithTexCoords(new Rect(rect.x + wind, rect.y + rect.height * y, rect.width, rect.height / strips), worldProps,
                        new Rect(id % 4 / 4f, 1 - (id / 4 + next) / 2f, .25f, (next - y) / 2));
                }
            }
            finally { GUI.color = previous; }
        }
        private void DrawArenaBoundary()
        {
            if (!Game.GateBarrierActive) return;
            float opacity = !Game.GateOpen ? 1 : Math.Max(.12f, Game.gateOpeningTime / .55f);
            for (int y = 2; y < 14; y++) DrawBarrierCell(26, y, opacity);
            for (int x = 27; x < Game.Map.rows[0].Length - 2; x++) DrawBarrierCell(x, 13, opacity);
        }
        private void DrawBarrierCell(int x, int y, float opacity)
        {
            Point p = ScreenPoint(new Point(x, y));
            Color stone = Game.save.room == 1 ? new Color(.17f, .28f, .2f, opacity) : new Color(.22f, .3f, .34f, opacity);
            Box(p.x, p.y, Tile, Tile, stone); Frame(p.x + 1, p.y + 1, Tile - 2, Tile - 2, new Color(.6f, .65f, .43f, opacity * .65f));
            // The visible boundary has the same full tile extent as collision.
            Atlas(fieldProps, 4, 4, Game.save.room == 1 ? 11 : 4, new Rect(p.x - 2, p.y - 9, Tile + 4, Tile + 12), new Color(1, 1, 1, opacity));
            if ((x + y) % 3 == 0) Diamond(p.x + Tile / 2, p.y + Tile / 2, 3, new Color(gold.r, gold.g, gold.b, opacity));
        }
        private bool DrawFieldProp(string kind, int variant, float x, float y, float w, float h, Color? tint)
        {
            if (fieldProps == null) return false;
            int id = kind == "well" ? 0 : kind == "boat" ? 1 : kind == "column" ? 2 : kind == "obelisk" ? 3 : kind == "crystal" || kind == "ore" ? 4 : kind == "post" ? 5 : kind == "herb" ? 6 : kind == "wood" ? 7 : kind == "bell" ? 8 : kind == "tent" ? 9 : kind == "skiff" ? 10 : kind == "root" ? 11 : kind == "altar" || kind == "engine" ? 12 : kind == "fire" ? 13 : kind == "mirror" ? 14 : kind == "cache" ? 15 : -1;
            Point foot = new Point(x + w / 2, y + h - 8);
            if (id >= 0) { Actor(fieldProps, 4, 4, id, foot, Math.Max(w, h), tint ?? Color.white); return true; }
            id = kind == "crate" ? 6 : kind == "barrel" ? 7 : kind == "fence" ? 11 : kind == "bridge" ? 12 : kind == "bed" ? 13 : kind == "bookshelf" ? 14 : kind == "table" || kind == "counter" ? 15 : -1;
            if (id >= 0 && detailSheet != null)
            {
                Actor(detailSheet, 4, 4, id, foot, Math.Max(w, h), tint ?? Color.white);
                if (kind == "counter") { Actor(detailSheet, 4, 4, 15, foot + new Point(-45, 0), Math.Max(w, h), tint ?? Color.white); Actor(detailSheet, 4, 4, 15, foot + new Point(45, 0), Math.Max(w, h), tint ?? Color.white); }
                return true;
            }
            if (kind == "garden")
            {
                Atlas(terrainSheet, 8, 2, 2, new Rect(x + 6, y + h - 40, w - 12, 32), Color.white);
                for (int row = 0; row < 3; row++) { Box(x + 8, y + h - 36 + row * 10, w - 16, 1, new Color(.16f, .12f, .09f, .35f)); if (variant > 0) for (int col = 0; col < 3; col++) Actor(fieldProps, 4, 4, 6, new Point(x + 17 + col * (w - 24) / 3, y + h - 30 + row * 10), variant == 2 ? 27 : 18, Color.white); }
                return true;
            }
            return false;
        }
    }
}
