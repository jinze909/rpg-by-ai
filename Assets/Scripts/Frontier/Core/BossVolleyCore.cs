using System;

namespace Starfall
{
    // A value snapshot of this prepared attack, not persistent encounter state.
    public struct BossVolleyPlan
    {
        public readonly int aimedCount, radialCount, phase, releaseCycle;
        public readonly Point origin;
        private readonly double aimedHeading;
        public int Count { get { return aimedCount + radialCount; } }
        internal BossVolleyPlan(Enemy enemy, int room)
        {
            phase = enemy.AttackPhase; releaseCycle = unchecked(enemy.cycle + 1); origin = enemy.Position;
            aimedCount = room == 3 ? 3 : 0;
            radialCount = room >= 2 || phase >= 2 ? 4 + Math.Min(5, room) * 2 + phase * 2 : 0;
            aimedHeading = Math.Atan2(enemy.facing.y, enemy.facing.x);
        }
        public Point Direction(int index)
        {
            if (index < 0 || index >= Count) return new Point();
            double angle = index < aimedCount ? aimedHeading + (index - 1) * .18
                : (index - aimedCount) * Math.PI * 2 / radialCount + releaseCycle * .2;
            return new Point((float)Math.Cos(angle), (float)Math.Sin(angle));
        }
        public float Speed(int index)
        { return index < 0 || index >= Count ? 0 : index < aimedCount ? 6 : 3.5f + phase * .4f; }
    }

    public sealed partial class GameCore
    {
        public const float BossVolleyWarningReach = 2.4f;
        // Read the canonical orb footprint once, without allocating a probe per ray/frame.
        private static readonly float bossVolleyRadius = new Projectile { visual = "enemyOrb" }.Radius;
        public static BossVolleyPlan PendingBossVolley(Enemy enemy, int room)
        {
            if (enemy == null || !enemy.boss || enemy.state != 1 || IsSignalWarden(enemy) || IsCommissionGuardian(enemy))
                return new BossVolleyPlan();
            return new BossVolleyPlan(enemy, room);
        }
        public Point BossVolleyWarningEnd(Enemy enemy, int index)
        {
            BossVolleyPlan plan = PendingBossVolley(enemy, save.room);
            if (index < 0 || index >= plan.Count || !Finite(plan.origin.x) || !Finite(plan.origin.y)) return plan.origin;
            Point direction = plan.Direction(index).Normalized, end = plan.origin;
            if (ShotBlocked(bossVolleyRadius, end)) return end;
            // Match projectile flight's maximum .1-world-unit obstruction step.
            for (int step = 1; step <= 24; step++)
            {
                Point next = plan.origin + direction * (step * .1f);
                if (ShotBlocked(bossVolleyRadius, next)) break;
                end = next;
            }
            return end;
        }
    }
}
