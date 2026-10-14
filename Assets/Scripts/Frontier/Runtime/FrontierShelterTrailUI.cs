using System;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private bool DrawShelterObject(WorldObject o)
        {
            if (!o.kind.StartsWith("shelterMark", StringComparison.Ordinal)) return false;
            int index;
            if (!int.TryParse(o.kind.Substring(11), out index) || index < 0 || index > 2) return true;
            Point point = new Point(o.x, o.y), screen = ScreenPoint(point);
            bool read = Game.Has("shelter_mark_" + index);
            PropImage("post", 0, screen.x - 24, screen.y - 55, 48, 56, read ? Color.white : gold);
            Ring(point, .45f, read ? mint : gold, 24);
            Text(screen.x - 86, screen.y - 92, 172, 32, Game.L("路碑 ", "WAYMARK ") + (index + 1), 12, read ? mint : gold, TextAnchor.MiddleCenter);
            return true;
        }

        private Rect ShelterWindBounds()
        { return SignalLaneBounds(Game.ShelterWindFrom, Game.ShelterWindTo, GameCore.ShelterWindRadius); }

        private void DrawShelterWind(bool boundaryOnly = false)
        {
            if (!Game.ShelterWindWarning || Game.save.room != 7) return;
            Color warning = PixelArt.C("#f0b676"); warning.a = boundaryOnly ? .96f : .8f;
            DrawSignalLane(Game.ShelterWindFrom, Game.ShelterWindTo, GameCore.ShelterWindRadius, warning);
            if (!boundaryOnly)
                DrawRay(Game.ShelterWindFrom, Game.ShelterWindTo, new Color(warning.r, warning.g, warning.b, .32f));
            // Mint circles retain the original lit-brazier shelter radius.
            foreach (WorldObject o in Game.Map.objects)
                if (o.kind == "brazier" && Game.Has("warm_" + o.id))
                    Ring(new Point(o.x, o.y), 3.5f, new Color(mint.r, mint.g, mint.b, .72f), 48);
        }

        private void DrawShelterWorld()
        {
            if (Game.mode != "world" || Game.Dying || Game.save.room != 7 || !Game.ShelterAccepted) return;
            if (Game.ShelterReported)
            {
                Point[] route = GameCore.ShelterRoutePoints(Game.Has("sheltered_upper"));
                for (int i = 1; i < route.Length; i++)
                    DrawRay(route[i - 1], route[i], new Color(mint.r, mint.g, mint.b, .5f));
                if (Game.ShelterColdProtected)
                {
                    Box(348, 610, 584, 53, new Color(.035f, .07f, .10f, .92f));
                    Text(363, 617, 554, 38, Game.L("森的避风路 · 寒冷不会继续积累", "Sen's shelter trail · cold stops accumulating"), 14, mint, TextAnchor.MiddleCenter);
                }
                return;
            }
            if (Game.ShelterLeg < 0) return;
            DrawShelterWind();
            string status = Game.ShelterWindWarning
                ? Game.L("横风已锁定 ", "Crosswind locked ") + Math.Max(0, Game.ShelterWindTimer).ToString("0.0") + Game.L("秒 · 离开金色风路，火盆附近安全", "s · leave the gold lane; lit braziers shelter you")
                : Game.L("沿路读路碑，再到下一处交谈确认 · 没有时间限制", "Read the waymark, then confirm arrival · no time limit");
            Box(348, 610, 584, 65, new Color(.035f, .07f, .10f, .92f));
            Text(363, 618, 554, 49, status, 14, Game.ShelterWindWarning ? gold : paper, TextAnchor.MiddleCenter);
        }
    }
}
