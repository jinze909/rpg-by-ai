using System;

namespace Starfall
{
    // The road's echo is an optional encounter, never a chapter guardian.
    // Its reserved identity fits existing world snapshots without a save migration.
    public sealed partial class GameCore
    {
        public const int SignalWardenId = 73000;
        public const float SignalWardenHealth = 300;
        public const float SignalRushRadius = .65f, SignalMarkRadius = .9f;
        public const float SignalFanStart = 1.7f, SignalFanLength = 4.5f, SignalFanRadius = .38f;
        private static readonly float[] signalWindups = { .95f, 1.15f, 1.1f };
        private static readonly float[] signalRecoveries = { .9f, 1.1f, 1.2f };
        public bool SignalReady { get { return Has("watch_complete"); } }
        public bool SignalComplete { get { return Has("signal_complete"); } }
        public bool SignalActive { get { return enemies.Exists(IsSignalWarden); } }
        public Point SignalPoint { get { return WatchPoint(2); } }
        public static bool IsSignalWarden(Enemy enemy)
        { return enemy != null && enemy.id == SignalWardenId && enemy.boss && enemy.kind == 3; }
        public static int SignalPattern(Enemy enemy) { return Math.Max(0, enemy.cycle) % 3; }
        public static Point[] SignalMarks(Enemy enemy)
        {
            Point side = new Point(-enemy.facing.y, enemy.facing.x) * 2;
            return new[] { enemy.target - side, enemy.target + side };
        }
        public static Point[] SignalFanDirections(Enemy enemy)
        {
            double heading = Math.Atan2(enemy.facing.y, enemy.facing.x);
            float[] angles = { -.78f, -.42f, .42f, .78f };
            Point[] directions = new Point[angles.Length];
            for (int i = 0; i < angles.Length; i++) directions[i] = new Point((float)Math.Cos(heading + angles[i]), (float)Math.Sin(heading + angles[i]));
            return directions;
        }
        public bool StartSignalEncounter()
        {
            if (mode != "world" || Dying || practicing || save.room != 1 || !SignalReady || SignalComplete || enemies.Exists(e => e.boss) || (Position - SignalPoint).Length >= 1.75f) return false;
            Enemy echo = CreatePlacedEnemy(SignalWardenId, 3, SignalPoint + new Point(-3, 0), SignalWardenHealth, true);
            echo.timer = 1.2f; echo.Face(Position - echo.Position); enemies.Add(echo);
            Flag("signal_started"); TrackQuest("signal");
            Notify("旧路回响现身。看清预告，避开后反击。", "The road's echo awakens. Read each warning, evade, then counter."); sound = "windup";
            return true;
        }
        private void RestoreSignalEncounter()
        {
            enemies.RemoveAll(e => e.id == SignalWardenId && (!IsSignalWarden(e) || save.room != 1 || !SignalReady || SignalComplete || practicing));
            foreach (Enemy e in enemies) if (IsSignalWarden(e))
            {
                // Attack geometry is transient: reload keeps HP, then announces a
                // fresh attack instead of resuming an unseen partial warning.
                e.state = e.cycle = 0; e.timer = 1.2f; e.windupDuration = e.releaseTime = 0;
                e.Face(Position - e.Position);
            }
        }
        private static float SignalSegmentDistance(Point point, Point from, Point to)
        {
            Point line = to - from; float square = line.x * line.x + line.y * line.y;
            float t = square > .000001f ? Clamp(((point.x - from.x) * line.x + (point.y - from.y) * line.y) / square, 0, 1) : 0;
            return (point - (from + line * t)).Length;
        }
        private void TickSignalEnemy(Enemy enemy, float dt)
        {
            if (enemy.state == 1)
            {
                enemy.timer -= dt;
                int pattern = SignalPattern(enemy);
                if (pattern == 0 && enemy.timer < .2f)
                {
                    Point before = enemy.Position;
                    Point next = enemy.attackOrigin + (enemy.attackDestination - enemy.attackOrigin) * Clamp(1 - enemy.timer / .2f, 0, 1);
                    Point swept = SweepActor(before, next - before); enemy.x = swept.x; enemy.y = swept.y;
                    enemy.moving = (swept - before).Length > .001f;
                    if (SignalSegmentDistance(Position, before, swept) < SignalRushRadius && ClearAttackPath(swept, Position)) Hurt(20, "lunge", enemy.facing);
                }
                if (Dying || enemy.timer > 0) return;
                effects.Add(new Effect("enemyRelease", enemy.Position, .3f) { direction = enemy.facing, variant = 11 });
                if (pattern == 1)
                    foreach (Point mark in SignalMarks(enemy))
                    {
                        effects.Add(new Effect("danger", mark, .45f) { variant = 1, radius = SignalMarkRadius });
                        if ((Position - mark).Length < SignalMarkRadius && ClearAttackPath(enemy.attackOrigin, Position)) Hurt(18, "area", enemy.facing);
                    }
                if (pattern == 2)
                    foreach (Point direction in SignalFanDirections(enemy))
                    {
                        var shot = new Projectile { position = enemy.attackOrigin, velocity = direction * 3,
                            damage = 14, life = SignalFanLength / 3, source = "shot", visual = "enemyOrb", theme = 1, ownerId = SignalWardenId,
                            height = BossEmissionHeight(1), launchHeight = BossEmissionHeight(1), flightHeight = BossEmissionHeight(1) };
                        // Sweep the offset launch as well as the flight. A root
                        // between the echo and its muzzle must stop this lane.
                        int steps = (int)Math.Ceiling(SignalFanStart / .04f);
                        for (int step = 1; step <= steps; step++)
                        {
                            Point next = enemy.attackOrigin + direction * (SignalFanStart * step / steps);
                            if (ShotBlocked(shot, next)) { shot.blockedAtSpawn = true; break; }
                            shot.position = next;
                        }
                        projectiles.Add(shot);
                    }
                enemy.state = 0; enemy.releaseTime = .3f; enemy.timer = signalRecoveries[pattern]; enemy.cycle++;
                sound = pattern == 2 ? "spell" : "slam";
                return;
            }
            // A stationary cooldown is a real counter window, rather than a
            // moving attack. Only approach after it has expired.
            enemy.timer -= dt;
            if (enemy.timer > 0) return;
            Point delta = Position - enemy.Position; float distance = delta.Length;
            bool visible = ClearAttackPath(enemy.Position, Position);
            if (distance > 5 || !visible)
            {
                Point before = enemy.Position;
                Point next = SweepActor(before, DirectionTo(before, Position) * dt * 1.4f);
                enemy.x = next.x; enemy.y = next.y; Point travelled = next - before;
                enemy.moving = travelled.Length > .001f;
                if (enemy.moving) { enemy.moveTime += travelled.Length; enemy.Face(travelled); }
                return;
            }
            enemy.state = 1; enemy.target = Position; enemy.Face(delta);
            enemy.attackOrigin = enemy.attackDestination = enemy.Position;
            int move = SignalPattern(enemy); enemy.timer = enemy.windupDuration = signalWindups[move];
            if (move == 0)
            {
                float reach = Math.Min(5, distance + .5f);
                enemy.attackDestination = SweepActor(enemy.Position, enemy.facing * reach);
            }
            sound = "windup";
        }
        private void OnSignalDefeated(Enemy enemy)
        {
            foreach (Projectile p in projectiles.ToArray()) if (p.ownerId == SignalWardenId && !p.friendly)
            { projectiles.Remove(p); effects.Add(new Effect("projectileFade", p.position, .25f) { direction = p.velocity.Normalized, variant = 10, height = p.height }); }
            if (save.room != 1 || !SignalReady || SignalComplete) return;
            Flag("signal_complete"); save.shards += 6; save.skillPoints++;
            effects.Add(new Effect("discover", enemy.Position, .7f));
            RecordMoment("旧路回响已平静。灯照旅人，也护着森林的根。", "The road's echo rests. Its lights protect travelers and the forest's roots.");
            Notify("旧路守望完成！碎片6、技能点1。回镇听侦察员的消息。", "Signal Warden complete! 6 shards, 1 talent point. Hear the scout's news in town."); sound = "reward";
        }
    }
}
