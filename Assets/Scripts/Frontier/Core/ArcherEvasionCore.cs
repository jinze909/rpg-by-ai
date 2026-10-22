using System;

namespace Starfall
{
    public sealed partial class GameCore
    {
        private Point ArcherRetreat(Point origin, Point towardPlayer, float dt)
        {
            Point direction = towardPlayer.Normalized;
            Point away = direction * -1;
            float budget = dt * 1.6f;
            Point requested = direction * -dt * 1.6f;
            Point best = SweepActor(origin, requested);
            // Keep unobstructed retreat exactly as before. A partly clipped
            // step also needs a way around the wall, not a near-zero slide.
            if ((best - origin - requested).Length <= .00001f) return best;

            Point movement = best - origin;
            bool clippedX = Math.Abs(movement.x - requested.x) > .00001f;
            bool clippedY = Math.Abs(movement.y - requested.y) > .00001f;
            // Follow an axis-aligned wall instead of circling the player and
            // repeatedly drifting back into the same wall at small timesteps.
            Point tangent = clippedX && !clippedY ? new Point(0, 1)
                : clippedY && !clippedX ? new Point(1, 0) : new Point(-away.y, away.x);

            Point separation = best - Position;
            float bestDistance = separation.x * separation.x + separation.y * separation.y;
            separation = origin - Position;
            float originalDistance = separation.x * separation.x + separation.y * separation.y;
            if (bestDistance < originalDistance) bestDistance = originalDistance;
            for (int side = 0; side < 2; side++)
            {
                // Both alternatives start at the original feet and share one
                // frame's speed budget. Axis sliding must not close distance.
                Point candidate = SweepActor(origin, tangent * (side == 0 ? budget : -budget));
                separation = candidate - Position;
                float distance = separation.x * separation.x + separation.y * separation.y;
                if (distance > bestDistance) { best = candidate; bestDistance = distance; }
            }
            return best;
        }
    }
}
