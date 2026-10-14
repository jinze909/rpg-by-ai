using System;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        // The optional trial reuses the accepted forest guardian art, even when
        // its fallen actor is inspected after a room change.
        private static int BossArtRoom(Enemy enemy, int room)
        { return GameCore.IsSignalWarden(enemy) ? 1 : GameCore.IsCommissionGuardian(enemy) ? 6 : room; }

        private Rect SignalLaneBounds(Point from, Point to, float radius)
        {
            Point a = ScreenPoint(from), b = ScreenPoint(to); float edge = radius * Tile + 3;
            return new Rect(Math.Min(a.x,b.x)-edge,Math.Min(a.y,b.y)-edge,Math.Abs(a.x-b.x)+edge*2,Math.Abs(a.y-b.y)+edge*2);
        }
        private static Rect UnionSignalBounds(Rect a, Rect b)
        {
            float x = Math.Min(a.x,b.x), y = Math.Min(a.y,b.y);
            return new Rect(x,y,Math.Max(a.x+a.width,b.x+b.width)-x,Math.Max(a.y+a.height,b.y+b.height)-y);
        }
        private Rect SignalWarningBounds(Enemy enemy)
        {
            int pattern = GameCore.SignalPattern(enemy);
            if (pattern == 0) return SignalLaneBounds(enemy.attackOrigin,enemy.attackDestination,GameCore.SignalRushRadius);
            if (pattern == 1)
            {
                Point[] marks = GameCore.SignalMarks(enemy);
                return UnionSignalBounds(SignalLaneBounds(marks[0],marks[0],GameCore.SignalMarkRadius),SignalLaneBounds(marks[1],marks[1],GameCore.SignalMarkRadius));
            }
            Point[] directions = GameCore.SignalFanDirections(enemy); Rect bounds = new Rect();
            for (int i = 0; i < directions.Length; i++)
            {
                Point from = enemy.attackOrigin + directions[i] * GameCore.SignalFanStart;
                Point to = from + directions[i] * GameCore.SignalFanLength;
                Rect lane = SignalLaneBounds(from,to,GameCore.SignalFanRadius);
                bounds = i == 0 ? lane : UnionSignalBounds(bounds,lane);
            }
            return bounds;
        }
        private void DrawSignalLane(Point from, Point to, float radius, Color color)
        {
            Point direction = (to-from).Normalized;
            if (direction.Length < .01f) { Ring(from,radius,color,32); return; }
            Point side = new Point(-direction.y,direction.x) * radius;
            DrawRay(from+side,to+side,color); DrawRay(from-side,to-side,color);
            float angle = (float)Math.Atan2(direction.y,direction.x);
            // Semicircular end caps describe the exact swept contact radius.
            for (int i = 0; i <= 24; i++)
            {
                float a = angle - (float)Math.PI/2 + (float)Math.PI*i/24;
                Point end = ScreenPoint(to + new Point((float)Math.Cos(a),(float)Math.Sin(a))*radius);
                Point start = ScreenPoint(from - new Point((float)Math.Cos(a),(float)Math.Sin(a))*radius);
                Box(end.x-1.5f,end.y-1.5f,3,3,color); Box(start.x-1.5f,start.y-1.5f,3,3,color);
            }
        }
        private void DrawSignalWarning(Enemy enemy, bool boundaryOnly = false)
        {
            if (!GameCore.IsSignalWarden(enemy) || enemy.state != 1) return;
            float progress = Mathf.Clamp01(1-enemy.timer/Math.Max(.01f,enemy.windupDuration));
            Color warning = new Color(.875f,.54f,.408f,boundaryOnly?.78f:.7f+progress*.2f);
            int pattern = GameCore.SignalPattern(enemy);
            if (pattern == 0)
            {
                DrawSignalLane(enemy.attackOrigin,enemy.attackDestination,GameCore.SignalRushRadius,warning);
                if (!boundaryOnly) DrawRay(enemy.attackOrigin,enemy.attackDestination,new Color(warning.r,warning.g,warning.b,.2f+progress*.3f));
            }
            else if (pattern == 1)
            {
                foreach (Point mark in GameCore.SignalMarks(enemy))
                {
                    Ring(mark,GameCore.SignalMarkRadius,warning,40);
                    if (!boundaryOnly) Ring(mark,GameCore.SignalMarkRadius*(.2f+progress*.8f),new Color(warning.r,warning.g,warning.b,.3f+progress*.3f),32);
                }
            }
            else
            {
                foreach (Point direction in GameCore.SignalFanDirections(enemy))
                {
                    Point from = enemy.attackOrigin + direction*GameCore.SignalFanStart;
                    DrawSignalLane(from,from+direction*GameCore.SignalFanLength,GameCore.SignalFanRadius,warning);
                }
                // The split fan deliberately leaves the captured center line
                // open; do not draw a misleading filled cone over that gap.
                if (!boundaryOnly) DrawRay(enemy.attackOrigin+enemy.facing*GameCore.SignalFanStart,enemy.attackOrigin+enemy.facing*(GameCore.SignalFanStart+GameCore.SignalFanLength),new Color(mint.r,mint.g,mint.b,.5f));
            }
        }
        private string SignalMoveHint(Enemy enemy)
        {
            if (enemy.state != 1) return Game.L("守望者正在恢复 · 现在可以反击", "Warden recovering · counterattack now");
            int pattern = GameCore.SignalPattern(enemy);
            return pattern == 0 ? Game.L("旧路冲刺 · 向两侧闪避", "Road rush · dodge sideways")
                : pattern == 1 ? Game.L("双灯落印 · 离开两个光圈", "Twin marks · leave both circles")
                : Game.L("分光散射 · 中间留有安全路线", "Split fan · the center lane is clear");
        }
        private void DrawSignalLamp(WorldObject obj, Point screen)
        {
            if (obj.kind != "watchSignal2" || !Game.SignalReady || Game.save.room != 1 || Game.SignalActive) return;
            Ring(Game.SignalPoint,.65f,Game.SignalComplete?mint:gold,24);
            Text(screen.x-105,screen.y-105,210,26,Game.SignalComplete?Game.L("守望者已认可你", "WARDEN'S TRUST"):Game.L("守望者挑战", "WARDEN TRIAL"),12,Game.SignalComplete?mint:gold,TextAnchor.MiddleCenter);
        }
    }
}
