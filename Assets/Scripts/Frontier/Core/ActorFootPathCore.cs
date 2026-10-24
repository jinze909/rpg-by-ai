using System;

namespace Starfall
{
    public sealed partial class GameCore
    {
        private bool ClearActorFootPath(Point from, Point to)
        {
            Point delta = to - from;
            int steps = Math.Max(1, (int)Math.Ceiling(delta.Length / .08f));
            for (int step = 1; step <= steps; step++)
                if (!Fits(from + delta * (step / (float)steps))) return false;
            return true;
        }
        private Point DirectionToActorFoot(Point from, Point target)
        {
            // A legal foot beside a house may share a cell with an illegal
            // center. Keep the actual player foot or follow anchor as the goal.
            int width = Map.rows[0].Length, height = Map.rows.Length;
            Point direct = (target - from).Normalized;
            if (!CanStand(from) || !CanStand(target)) return new Point();
            int sx = (int)Math.Floor(from.x), sy = (int)Math.Floor(from.y);
            int tx = (int)Math.Floor(target.x), ty = (int)Math.Floor(target.y);
            if (Fits(new Point(tx + .5f, ty + .5f))) return DirectionTo(from, target);
            if (ClearActorFootPath(from, target)) return direct;

            // Search existing legal walking cells for a short, swept final
            // approach to the exact foot. Ordinary legal-center queries still
            // use DirectionTo; no new map, speed or teleport is added.
            PreparePathSearch(width * height, -1);
            int[] parents = pathParents; int head = 0, tail = 0, start = sy * width + sx, end = -1;
            pathQueue[tail++] = start; parents[start] = start;
            while (head < tail)
            {
                int cell = pathQueue[head++], x = cell % width, y = cell / width;
                Point center = new Point(x + .5f, y + .5f);
                if (Math.Abs(center.x - target.x) <= 1.5f && Math.Abs(center.y - target.y) <= 1.5f
                    && Fits(center) && (cell != start || ClearActorFootPath(from, center)) && ClearActorFootPath(center, target))
                { end = cell; break; }
                for (int side = 0; side < 4; side++)
                {
                    int xx = x + pursuitSteps[side, 0], yy = y + pursuitSteps[side, 1];
                    if (xx < 0 || yy < 0 || xx >= width || yy >= height) continue;
                    int next = yy * width + xx; Point nextCenter = new Point(xx + .5f, yy + .5f);
                    if (parents[next] >= 0 || !Fits(nextCenter) || cell == start && !ClearActorFootPath(from, nextCenter)) continue;
                    parents[next] = cell; pathQueue[tail++] = next;
                }
            }
            if (end < 0) return direct;
            int waypoint = end;
            if (end != start) while (parents[waypoint] != start) waypoint = parents[waypoint];
            return (new Point(waypoint % width + .5f, waypoint / width + .5f) - from).Normalized;
        }
    }
}
