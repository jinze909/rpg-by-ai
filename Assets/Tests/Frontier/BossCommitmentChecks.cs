using System;
using System.Reflection;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore BossCommitmentGame(Campaign data, int room, int phase, out Enemy boss, float maximum = 1000)
        {
            var g = Unlocked(data);
            for (int region = 1; region <= 7; region++)
            {
                g.save.flags.Add("gate_" + region); g.save.flags.Add("seal_" + region);
            }
            g.save.flags.Add("scout_saved");
            if (room == 8)
            {
                g.StartAbyss(0); g.save.abyssFloor = 6;
                g.Load(CloneSave(g.save)); g.Start(); g.Close();
            }
            else g.Enter(room);
            Check(g.save.room == room, "Boss commitment fixture enters the actual region " + room);
            g.enemies.Clear(); g.projectiles.Clear(); g.effects.Clear(); g.hazards.Clear();
            g.save.reducedMotion = true; g.save.difficulty = 1; g.invulnerable = 1000;
            g.save.x = 12; g.save.y = 14;
            boss = new Enemy(10 + room, 3, 13.5f, 14, maximum, true)
                { hp = maximum * (phase == 1 ? .67f : phase == 2 ? .34f : .3f), timer = 0, cycle = 2 };
            Check(g.CanStand(g.Position) && g.CanStand(boss.Position), "Prepared attacks use real region ground");
            g.enemies.Add(boss); g.Tick(.01f, new Point());
            Check(boss.state == 1 && boss.BossPhase == phase && boss.windupDuration > 0,
                "Actual AI starts the expected phase's full warning in region " + room);
            return g;
        }
        private static void CrossBossThreshold(GameCore g, Enemy boss, bool technique)
        {
            int phase = boss.BossPhase; float health = boss.hp;
            if (technique) g.Skill(new Point(1, 0)); else g.Attack(new Point(1, 0));
            Check(g.playerAction != null && g.playerAction.kind == (technique ? "skill" : "attack"),
                "Threshold reproduction uses an accepted real player action");
            for (int frame = 0; frame < 80 && boss.hp == health; frame++) g.Tick(.01f, new Point());
            Check(boss.hp < health && boss.BossPhase == phase + 1 && boss.state == 1,
                "A real non-breaking hit crosses the health threshold while the attack remains prepared");
        }
        private static void ReleaseBossCommitment(GameCore g, Enemy boss)
        {
            int cycle = boss.cycle;
            for (int frame = 0; frame < 240 && boss.cycle == cycle; frame++)
            {
                if (boss.timer <= .02001f && boss.hitTime <= 0 && boss.stun <= 0 && boss.releaseTime <= 0)
                {
                    // Observe the actual AI's emission boundary before public
                    // Tick flies newborn missiles into the player or terrain.
                    typeof(GameCore).GetMethod("UpdateEnemies", BindingFlags.NonPublic | BindingFlags.Instance)
                        .Invoke(g, new object[] { .025f });
                }
                else g.Tick(.01f, new Point());
            }
            Check(boss.cycle == cycle + 1 && boss.state == 0 && boss.releaseTime > 0,
                "The original prepared attack actually releases after hit reactions resume");
        }
        private static void NextBossCommitment(GameCore g, Enemy boss)
        {
            // Remove only already released missiles/hazards and extra foes from
            // this fixture; the Boss retains its real HP, recovery and AI clock.
            g.projectiles.Clear(); g.hazards.Clear(); g.enemies.RemoveAll(e => e != boss);
            for (int frame = 0; frame < 600 && boss.state != 1; frame++) g.Tick(.01f, new Point());
            Check(boss.state == 1 && boss.windupDuration > 0, "Actual recovery ends in a new prepared attack");
        }
        private static void CheckBossReleasePattern(GameCore g, Enemy boss, int phase)
        {
            int room = g.save.room;
            int spokes = room >= 2 || phase >= 2 ? 4 + Math.Min(5, room) * 2 + phase * 2 : 0;
            int aimed = room == 3 ? 3 : 0;
            Check(g.projectiles.Count == spokes + aimed,
                "Prepared phase owns radial projectile count/eligibility in region " + room + " phase " + phase);
            for (int shot = 0; shot < g.projectiles.Count; shot++)
            {
                float speed = shot < aimed ? 6 : 3.5f + phase * .4f;
                Check(Math.Abs(g.projectiles[shot].velocity.Length - speed) < .001f,
                    "Prepared phase owns projectile speed while the mine's aimed salvo keeps speed 6");
                Check(g.projectiles[shot].damage == 14, "Mature Boss projectile damage remains unchanged");
            }
            bool reinforcement = phase == 3 || room == 4 && phase >= 2;
            Check(g.enemies.Count == (reinforcement ? 2 : 1),
                "Prepared phase owns cycle-three reinforcement eligibility in region " + room);
            if (room == 5 || room == 6)
            {
                Check(g.hazards.Count == 3, "Mature tide/glass follow-up still creates its three marks");
                foreach (Hazard hazard in g.hazards)
                    Check(hazard.damage == (room == 5 ? 17 : 18), "Fixed follow-up damage is preserved");
            }
            else if (room == 7 || room == 8)
                Check(g.hazards.Count == 1 && g.hazards[0].damage == 20 + phase * 2,
                    "Prepared phase owns cold/abyss rift damage");
            else Check(g.hazards.Count == 0, "Earlier chapter attacks cannot gain expansion hazards");
        }
        public static void BossWarningsKeepPreparedPhaseAcrossRealHits(Campaign data)
        {
            for (int room = 1; room <= 8; room++) for (int phase = 1; phase <= 2; phase++)
                foreach (bool technique in new[] { false, true })
                {
                    Enemy boss; var g = BossCommitmentGame(data, room, phase, out boss);
                    float radius = GameCore.EnemyAreaRadius(boss), duration = boss.windupDuration;
                    Point target = boss.target, facing = boss.facing, origin = boss.attackOrigin, destination = boss.attackDestination;
                    CrossBossThreshold(g, boss, technique);
                    Check(Math.Abs(GameCore.EnemyAreaRadius(boss) - radius) < .001f,
                        "Health threshold cannot enlarge the visible warning already committed in region " + room + " phase " + phase);
                    Check(boss.windupDuration == duration && (boss.target - target).Length < .001f &&
                        (boss.facing - facing).Length < .001f && (boss.attackOrigin - origin).Length < .001f &&
                        (boss.attackDestination - destination).Length < .001f,
                        "Real Attack/Q reactions preserve the original timing, target and prepared dash lane");
                }
            for (int room = 1; room <= 8; room++)
            {
                // One normal Vanguard Q skips both thresholds on a living
                // low-HP Boss. Even a critical hit remains nonlethal here.
                Enemy boss; var g = BossCommitmentGame(data, room, 1, out boss, 100);
                float hp = boss.hp, radius = GameCore.EnemyAreaRadius(boss), duration = boss.windupDuration;
                g.Skill(new Point(1, 0));
                Check(g.playerAction != null && g.playerAction.kind == "skill", "Double threshold uses an accepted real Q");
                for (int frame = 0; frame < 80 && boss.hp == hp; frame++) g.Tick(.01f, new Point());
                Check(boss.hp > 0 && boss.hp < hp && boss.BossPhase == 3 && boss.state == 1 && g.enemies.Contains(boss),
                    "One real hit crosses both thresholds without killing, breaking or rewriting Boss HP");
                Check(Math.Abs(GameCore.EnemyAreaRadius(boss) - radius) < .001f && boss.windupDuration == duration,
                    "Skipping directly from phase one to three preserves the entire original warning");
                g.save.x = boss.target.x + radius + .08f; g.save.y = boss.target.y;
                g.invulnerable = 0; float health = g.save.health;
                ReleaseBossCommitment(g, boss);
                Check(g.save.health == health && g.effects.Exists(e => e.kind == "danger" && Math.Abs(e.radius - radius) < .001f),
                    "Double-threshold release keeps its original safe edge and contact footprint");
                CheckBossReleasePattern(g, boss, 1);
            }
        }
        public static void BossReleaseKeepsPreparedPhaseAcrossThresholds(Campaign data)
        {
            for (int room = 1; room <= 8; room++) for (int phase = 1; phase <= 2; phase++)
                foreach (bool avoid in new[] { true, false })
            {
                Enemy boss; var g = BossCommitmentGame(data, room, phase, out boss);
                float radius = GameCore.EnemyAreaRadius(boss); CrossBossThreshold(g, boss, false);
                g.save.x = boss.target.x + (avoid ? radius + .08f : 0); g.save.y = boss.target.y;
                Check(g.CanStand(g.Position), "Warning-edge avoidance stands on the actual region ground");
                g.invulnerable = 0; float health = g.save.health;
                ReleaseBossCommitment(g, boss);
                if (avoid) Check(g.save.health == health, "A player outside the announced footprint is not hit by a late phase expansion");
                else Check(Math.Abs((health - g.save.health) - (22 + phase * 3)) < .001f,
                    "An inner-footprint hit retains its prepared phase's slam damage despite the HP threshold change");
                Check(g.effects.Exists(e => e.kind == "danger" && Math.Abs(e.radius - radius) < .001f),
                    "Release impact footprint matches its original warning");
                CheckBossReleasePattern(g, boss, phase);
                Check(Math.Abs(boss.timer - (2.2f - boss.BossPhase * .3f)) < .001f,
                    "Recovery still adopts the mature live phase instead of extending old attack timing");
            }
        }
        public static void BossUnchangedPhasesKeepMatureAttackOutputs(Campaign data)
        {
            for (int room = 1; room <= 8; room++) for (int phase = 1; phase <= 3; phase++)
            {
                Enemy boss; var g = BossCommitmentGame(data, room, phase, out boss);
                float radius = 2.2f + phase * .2f;
                Check(Math.Abs(GameCore.EnemyAreaRadius(boss) - radius) < .001f &&
                    Math.Abs(boss.windupDuration - (1.1f - phase * .08f)) < .001f,
                    "Every unchanged phase retains its established radius and preparation duration");
                g.invulnerable = 0; float health = g.save.health;
                ReleaseBossCommitment(g, boss);
                Check(Math.Abs((health - g.save.health) - (22 + phase * 3)) < .001f,
                    "Every unchanged phase retains its established slam damage");
                CheckBossReleasePattern(g, boss, phase);
            }
            // Legacy staged enemies have no captured phase. Invalid transient
            // captures must retain that live-phase fallback, not undefined size.
            FieldInfo captured = typeof(Enemy).GetField("attackPhase");
            foreach (int invalid in new[] { -1, 0, 4, int.MaxValue })
            {
                var staged = new Enemy(31, 3, 13, 14, 100, true) { hp = 30, state = 1 };
                if (captured != null) captured.SetValue(staged, invalid);
                Check(Math.Abs(GameCore.EnemyAreaRadius(staged) - 2.8f) < .001f,
                    "A missing or invalid prepared capture falls back to the valid live Boss phase");
                if (captured != null) captured.SetValue(staged, 3);
                staged.state = 0; staged.hp = 100;
                Check(Math.Abs(GameCore.EnemyAreaRadius(staged) - 2.4f) < .001f,
                    "A completed attack's stale capture cannot own the idle Boss footprint");
                staged.boss = false; staged.state = 1;
                Check(GameCore.EnemyAreaRadius(staged) == GameCore.EnemyMeleeRadius,
                    "Captured Boss phases cannot alter mature ordinary heavy/melee reach");
            }
        }
        public static void BossNextAttackAndBreakAdoptCurrentPhase(Campaign data)
        {
            for (int room = 1; room <= 8; room++) for (int phase = 1; phase <= 2; phase++)
                foreach (bool interrupt in new[] { false, true })
                {
                    Enemy boss; var g = BossCommitmentGame(data, room, phase, out boss);
                    if (interrupt) boss.stagger = 94;
                    if (!interrupt) CrossBossThreshold(g, boss, false);
                    else
                    {
                        float hp = boss.hp; g.Attack(new Point(1, 0));
                        for (int frame = 0; frame < 80 && boss.hp == hp; frame++) g.Tick(.01f, new Point());
                        Check(boss.hp < hp && boss.BossPhase == phase + 1 && boss.state == 0 && boss.stun > 0 &&
                            boss.cycle == 2 && boss.releaseTime <= 0, "A real stagger break cancels the old attack without releasing it");
                    }
                    if (!interrupt) ReleaseBossCommitment(g, boss);
                    NextBossCommitment(g, boss);
                    Check(Math.Abs(GameCore.EnemyAreaRadius(boss) - (2.2f + (phase + 1) * .2f)) < .001f &&
                        Math.Abs(boss.windupDuration - (1.1f - (phase + 1) * .08f)) < .001f,
                        "Next normal or post-break warning adopts the current phase, never a stale old commitment");
                }
        }
        public static void BossPreparedAttacksPauseAndLoadAsFreshWarnings(Campaign data)
        {
            for (int room = 1; room <= 8; room++) foreach (int version in new[] { 1, 2 })
            {
                Enemy boss; var g = BossCommitmentGame(data, room, 1, out boss);
                CrossBossThreshold(g, boss, false); float timer = boss.timer, radius = GameCore.EnemyAreaRadius(boss);
                g.mode = "paused"; g.Tick(.05f, new Point());
                Check(boss.timer == timer && GameCore.EnemyAreaRadius(boss) == radius,
                    "Pausing freezes the actual prepared warning without adopting new HP phase");
                g.Close(); g.PrepareSave(); SaveData save = CloneSave(g.save); save.version = version;
                var resumed = new GameCore(data, save); resumed.Start(); resumed.Close();
                Enemy restored = resumed.enemies.Find(e => e.id == boss.id);
                Check(restored != null && restored.boss && restored.hp == boss.hp && restored.state == 0 &&
                    restored.windupDuration == 0 && restored.releaseTime == 0,
                    "Legacy-compatible load preserves Boss HP but restarts a full warning rather than resuming a partial attack");
                resumed.invulnerable = 1000; NextBossCommitment(resumed, restored);
                Check(Math.Abs(GameCore.EnemyAreaRadius(restored) - 2.6f) < .001f &&
                    Math.Abs(restored.windupDuration - .94f) < .001f,
                    "The restored warning prepares from retained phase-two HP with its complete authored timing");
            }
        }
    }
}
