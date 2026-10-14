using System;

namespace Starfall
{
    public sealed partial class GameCore
    {
        // Tomorrow's trial owns an identity, not a chapter guardian reward.
        // Geometry uses existing transient Enemy fields; snapshots contain HP only.
        public const int CommissionGuardianId = 78000;
        public const float CommissionGuardianHealth = 260;
        public const float CommissionBeamLength = 8, CommissionBeamRadius = .55f;
        public static readonly Point CommissionGuardianPoint = new Point(29, 13);
        public static readonly Point CommissionReceiverPoint = new Point(29, 17);
        private static readonly float[] commissionWindups = { 1.15f, 1.3f, 1.8f };
        private static readonly float[] commissionRecoveries = { 1.2f, 1.3f, 1.4f };
        public int CommissionVents { get; private set; }
        public bool CommissionActive { get { return save.room == 6 && enemies.Exists(CommissionActorOwned); } }
        public static bool IsCommissionGuardian(Enemy enemy)
        { return enemy != null && enemy.id == CommissionGuardianId && enemy.boss && enemy.kind == 3; }
        public static int CommissionPattern(Enemy enemy)
        { return Math.Max(0, enemy.cycle) % 3; }
        public static Point CommissionBeamEnd(Enemy enemy) { return enemy.attackDestination; }
        public static float CommissionRadius(Enemy enemy)
        { return CommissionPattern(enemy) == 0 ? CommissionBeamRadius : CommissionPattern(enemy) == 1 ? 1.3f : 1.6f; }
        public static string CommissionPatternName(Enemy enemy, bool english = false)
        {
            string[] zh = { "光路试压", "落点检查", "蓄压检查" };
            string[] en = { "Pressure beam", "Marked release", "Charge check" };
            return english ? en[CommissionPattern(enemy)] : zh[CommissionPattern(enemy)];
        }
        public static string CommissionHint(Enemy enemy, bool english = false)
        {
            if (enemy.state != 1) return english ? "Pressure settles: counter now." : "压力回落，现在反击。";
            if (CommissionPattern(enemy) == 0) return english ? "Step out of the fixed beam lane." : "离开已锁定的光路。";
            if (CommissionPattern(enemy) == 1) return english ? "Leave the marked circle." : "离开已锁定的光圈。";
            return enemy.timer > .35f ? english ? "At the receiver: E vents pressure, then counter." : "靠近蓄光池按 E 排压，再反击。"
                : english ? "Valve closed: leave the marked circle." : "排压时机已关，离开光圈。";
        }
        private bool CommissionActorOwned(Enemy enemy)
        {
            return IsCommissionGuardian(enemy) && save.room == 6 && CommissionReady && CommissionAccepted
                && Has("commission_started") && !CommissionTested && !practicing;
        }
        private void ResetCommissionTransient() { CommissionVents = 0; }
        private Enemy CreateCommissionGuardian(float health)
        {
            Enemy enemy = CreatePlacedEnemy(CommissionGuardianId, 3, CommissionGuardianPoint, CommissionGuardianHealth, true);
            enemy.hp = Clamp(health, .1f, CommissionGuardianHealth);
            enemy.state = enemy.cycle = 0; enemy.timer = 1.1f;
            enemy.Face(Position - enemy.Position);
            return enemy;
        }
        public bool StartCommissionEncounter()
        {
            if (!CommissionWorldAction || !CommissionLocal("receiver") || !CommissionAccepted || CommissionTested
                || CommissionActive || enemies.Exists(e => e.boss) || !CommissionQuiet(Position)
                || !Fits(CommissionGuardianPoint)) return false;
            CommissionVents = 0;
            enemies.Add(CreateCommissionGuardian(CommissionGuardianHealth));
            Flag("commission_started"); TrackQuest("commission"); sound = "windup";
            Notify("明天开始试机。避开光路与落点；蓄压时可在蓄光池按 E 排压。",
                "Trial started. Evade warnings; E at the receiver vents charge.");
            return true;
        }
        public bool TryCommissionVent()
        {
            if (!CommissionWorldAction || !CommissionLocal("receiver")) return false;
            Enemy enemy = enemies.Find(CommissionActorOwned);
            if (enemy == null || enemy.state != 1 || CommissionPattern(enemy) != 2 || enemy.timer <= .35f) return false;
            enemy.state = 0; enemy.cycle++; enemy.timer = 2; enemy.stun = 1.1f;
            enemy.windupDuration = enemy.releaseTime = 0;
            CommissionVents++; Flag("commission_vent");
            effects.Add(new Effect("break", enemy.Position, .7f, L("排压", "VENT")));
            effects.Add(new Effect("spark", CommissionReceiverPoint, .5f));
            Notify("排压成功！守卫暂时停机，现在反击。", "Pressure vented! The guardian pauses. Counter now.");
            sound = "counter"; revision++; return true;
        }
        private void RestoreCommissionRecord(EnemyRecord record)
        {
            if (record == null || record.id != CommissionGuardianId || !record.boss || record.kind != 3
                || !Finite(record.hp) || record.hp <= 0 || !Finite(record.maxHp) || !Finite(record.x) || !Finite(record.y)
                || save.room != 6 || !CommissionReady || !CommissionAccepted || !Has("commission_started")
                || CommissionTested || practicing || (new Point(record.x, record.y) - CommissionGuardianPoint).Length > .01f
                || !Fits(CommissionGuardianPoint)) return;
            // Resume canonical HP with a fresh full warning, never an unseen attack.
            enemies.Add(CreateCommissionGuardian(record.hp));
        }
        private void OnCommissionDefeated(Enemy enemy)
        {
            foreach (Projectile shot in projectiles.ToArray())
                if (!shot.friendly && shot.ownerId == CommissionGuardianId) projectiles.Remove(shot);
            if (!CommissionActorOwned(enemy)) return;
            Flag("commission_tested");
            effects.Add(new Effect("defeat", enemy.Position, .5f));
            RecordMoment("明天的试机已完成。伊沃还在蓄光池西侧等你，一起决定第一班供水送往哪里。",
                "Tomorrow's trial is complete. Ivo waits west of the receiver: decide together where its first water goes.");
            Notify("试机完成。回到伊沃身边，商定第一班供水。", "Trial complete. Return to Ivo and agree on the first water shift.");
            sound = "quest";
        }
        private static float CommissionSegmentDistance(Point point, Point from, Point to)
        {
            Point line = to - from; float square = line.x * line.x + line.y * line.y;
            float t = square > .000001f ? Clamp(((point.x - from.x) * line.x + (point.y - from.y) * line.y) / square, 0, 1) : 0;
            return (point - (from + line * t)).Length;
        }
        private void TickCommissionEnemy(Enemy enemy, float dt)
        {
            if (!CommissionActorOwned(enemy)) return;
            enemy.timer -= dt;
            if (enemy.state == 1)
            {
                if (enemy.timer > 0) return;
                int pattern = CommissionPattern(enemy);
                effects.Add(new Effect("enemyRelease", enemy.Position, .3f) { direction = enemy.facing, variant = 16 });
                if (pattern == 0)
                {
                    if (CommissionSegmentDistance(Position, enemy.attackOrigin, CommissionBeamEnd(enemy)) < CommissionBeamRadius
                        && ClearAttackPath(enemy.attackOrigin, Position)) Hurt(20, "shot", enemy.facing);
                }
                else
                {
                    float radius = CommissionRadius(enemy);
                    effects.Add(new Effect("danger", enemy.target, .45f) { variant = 6, radius = radius });
                    if ((Position - enemy.target).Length < radius && ClearAttackPath(enemy.attackOrigin, Position))
                        Hurt(pattern == 1 ? 22 : 24, "slam", enemy.facing);
                }
                enemy.state = 0; enemy.timer = commissionRecoveries[pattern]; enemy.releaseTime = .3f; enemy.cycle++;
                sound = pattern == 0 ? "spell" : "slam"; return;
            }
            if (enemy.timer > 0 || (Position - enemy.Position).Length >= 18) return;
            enemy.state = 1; enemy.attackPhase = enemy.BossPhase;
            enemy.attackOrigin = enemy.Position; enemy.target = Position;
            enemy.Face(Position - enemy.Position);
            enemy.attackDestination = enemy.attackOrigin;
            // The warning ends at terrain that will stop its damage. Capture this
            // once, so turning or taking a hit never redraws the announced lane.
            int steps = (int)Math.Ceiling(CommissionBeamLength / .12f);
            for (int step = 1; step <= steps; step++)
            {
                Point end = enemy.attackOrigin + enemy.facing * (CommissionBeamLength * step / steps);
                if (!ClearAttackPath(enemy.attackOrigin, end)) break;
                enemy.attackDestination = end;
            }
            enemy.timer = enemy.windupDuration = commissionWindups[CommissionPattern(enemy)];
            sound = "windup";
            effects.Add(new Effect("enemyWindup", enemy.Position, .35f) { direction = enemy.facing, variant = 16 });
        }
    }
}
