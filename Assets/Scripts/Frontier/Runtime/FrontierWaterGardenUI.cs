using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private bool DrawWaterGardenObject(WorldObject obj)
        {
            if (obj.kind != "waterDraw" && obj.kind != "waterBed") return false;
            Point p = ScreenPoint(new Point(obj.x, obj.y));
            Ring(new Point(obj.x, obj.y), .42f, gold, 16);
            if (obj.kind == "waterDraw")
            {
                // A static, readable bucket and water surface in every FX mode.
                Box(p.x - 11, p.y - 25, 22, 24, ink);
                Box(p.x - 8, p.y - 22, 16, 18, muted);
                Box(p.x - 8, p.y - 22, 16, 5, mint);
                Box(p.x - 7, p.y - 32, 14, 3, gold);
                Box(p.x - 10, p.y - 31, 3, 10, gold); Box(p.x + 7, p.y - 31, 3, 10, gold);
            }
            else DrawWaterGardenBed(p, false);
            return true;
        }
        private void DrawWaterGardenBed(Point p, bool watered)
        {
            Box(p.x - 22, p.y - 10, 44, 15, ink);
            Box(p.x - 19, p.y - 8, 38, 10, PixelArt.C(watered ? "#325e59" : "#755d45"));
            for (int i = -1; i <= 1; i++)
            {
                Box(p.x + i * 12 - 1, p.y - 19, 3, 13, mint);
                Box(p.x + i * 12 - 5, p.y - 19, 5, 4, mint);
            }
            if (watered) Box(p.x - 16, p.y, 32, 2, mint);
        }
        private void DrawWaterGardenScenery(Decoration prop)
        {
            if (Game.save.room != 0 || prop.kind != "garden" || !Game.WaterGardenWatered) return;
            // Keep the original garden and its crop variant. The separate trial
            // bed remains a quiet world consequence after its E target is gone.
            WorldObject bed = System.Array.Find(Game.Map.objects, o => o.id == "watergarden_bed" && o.kind == "waterBed");
            if (bed != null) DrawWaterGardenBed(ScreenPoint(new Point(bed.x, bed.y)), true);
        }
    }
}
