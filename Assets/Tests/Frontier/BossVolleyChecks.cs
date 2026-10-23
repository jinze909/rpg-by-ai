using System;
using System.Reflection;
using System.Text;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static readonly int[,] VolleyAuthoredSpokes = {
            { 0, 10, 12 }, { 10, 12, 14 }, { 12, 14, 16 }, { 14, 16, 18 },
            { 16, 18, 20 }, { 16, 18, 20 }, { 16, 18, 20 }, { 16, 18, 20 }
        };
        private static void VolleyNear(Point actual, Point expected, string reason)
        { Check((actual - expected).Length < .0001f, reason); }
        private static string VolleyState(GameCore game)
        { var text = new StringBuilder(); SmithAppend(text, game); return text.ToString(); }
        private static void VolleyEmitted(GameCore game, Enemy boss, BossVolleyPlan plan, bool movedCharge)
        {
            Check(game.projectiles.Count == plan.Count, "The actual AI emits precisely the announced number of chapter projectiles");
            for (int index = 0; index < plan.Count; index++)
            {
                Projectile shot = game.projectiles[index];
                VolleyNear(shot.velocity, plan.Direction(index) * plan.Speed(index), "Every actual aimed/radial velocity matches its own planned lane in original emission order");
                VolleyNear(shot.position, movedCharge ? boss.Position : plan.origin, "Missiles start at the actual release foot; the forest's mature final charge may still move before release");
                Check(shot.damage == 14 && !shot.friendly && shot.source == "shot" && shot.visual == "enemyOrb" && shot.theme == game.save.room,
                    "Warnings do not reprice damage, replace orb art, change ownership or lose the authored region theme");
                Check(Math.Abs(shot.Radius - .28f) < .00001f && shot.height == GameCore.BossEmissionHeight(game.save.room),
                    "Actual canonical orb radius and emission height remain unchanged");
            }
        }

        public static void VolleyPlansMatchActualEightRegionThreePhaseReleases(Campaign data)
        {
            Check(typeof(BossVolleyPlan).IsValueType, "An announced volley is a value snapshot, not mutable actor authority");
            foreach (FieldInfo field in typeof(BossVolleyPlan).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Check(field.IsInitOnly, "Every public volley snapshot field is readonly: " + field.Name);
            for (int room = 1; room <= 8; room++) for (int phase = 1; phase <= 3; phase++)
            {
                Enemy boss; GameCore game = BossCommitmentGame(data, room, phase, out boss);
                BossVolleyPlan plan = GameCore.PendingBossVolley(boss, room);
                int aimed = room == 3 ? 3 : 0, spokes = VolleyAuthoredSpokes[room - 1, phase - 1];
                Check(plan.aimedCount == aimed && plan.radialCount == spokes && plan.Count == aimed + spokes && plan.phase == phase
                    && plan.releaseCycle == 3, "Every real chapter preparation retains its authored salvo count and next release cycle");
                VolleyNear(plan.origin, boss.Position, "The current warning follows its actual live emitter foot");
                double heading = Math.Atan2(boss.facing.y, boss.facing.x);
                for (int index = 0; index < plan.Count; index++)
                {
                    double angle = index < aimed ? heading + (index - 1) * .18 : (index - aimed) * Math.PI * 2 / spokes + .6;
                    VolleyNear(plan.Direction(index), new Point((float)Math.Cos(angle), (float)Math.Sin(angle)), "The authored fan spread and next-cycle rotation are visible before release");
                    Check(Math.Abs(plan.Speed(index) - (index < aimed ? 6 : 3.5f + phase * .4f)) < .00001f,
                        "The three-shot mine fan keeps six speed and regional spokes retain phase speed");
                }
                foreach (int invalid in new[] { -1, plan.Count, int.MinValue, int.MaxValue })
                    Check(plan.Direction(invalid).Length == 0 && plan.Speed(invalid) == 0, "Out-of-range lanes cannot invent a shot");
                ReleaseBossCommitment(game, boss); VolleyEmitted(game, boss, plan, room == 1);
                CheckBossReleasePattern(game, boss, phase);
            }
        }

        public static void VolleyPreparedThresholdsCyclesCancellationAndSpecialActorsStayHonest(Campaign data)
        {
            for (int room = 1; room <= 8; room++) for (int phase = 1; phase <= 2; phase++)
            {
                Enemy boss; GameCore game = BossCommitmentGame(data, room, phase, out boss);
                BossVolleyPlan before = GameCore.PendingBossVolley(boss, room); CrossBossThreshold(game, boss, false);
                BossVolleyPlan crossed = GameCore.PendingBossVolley(boss, room);
                Check(crossed.phase == before.phase && crossed.Count == before.Count && crossed.releaseCycle == before.releaseCycle,
                    "Real nonbreaking HP-threshold hits cannot silently expand an already announced volley");
                for (int index = 0; index < before.Count; index++)
                    VolleyNear(crossed.Direction(index), before.Direction(index), "Threshold hits preserve the already committed fan and radial rotation");
                // The real hit reaction may continue to displace the emitter before release,
                // so each rendered frame reads its live origin while the direction stays committed.
                ReleaseBossCommitment(game, boss); VolleyEmitted(game, boss, crossed, true);
                Check(GameCore.PendingBossVolley(boss, room).Count == 0, "A completed attack immediately retires its warning lanes");
                NextBossCommitment(game, boss); BossVolleyPlan next = GameCore.PendingBossVolley(boss, room);
                Check(next.phase == phase + 1 && next.releaseCycle == before.releaseCycle + 1
                    && next.radialCount == VolleyAuthoredSpokes[room - 1, phase], "Actual recovery prepares current HP phase and the next fresh radial rotation");
                if (before.radialCount > 0)
                    Check((next.Direction(next.aimedCount) - before.Direction(before.aimedCount)).Length > .1f,
                        "The next real attack advances the visible radial lane instead of reusing a stale snapshot");
                for (int index = 0; index < before.Count; index++)
                    Check(Math.Abs(before.Direction(index).Length - 1) < .00001f, "Previously returned value snapshots remain intact after phase and cycle changes");

                game = BossCommitmentGame(data, room, phase, out boss); boss.stagger = 94;
                float hp = boss.hp; game.Attack(new Point(1, 0));
                for (int frame = 0; frame < 80 && boss.hp == hp; frame++) game.Tick(.01f, new Point());
                Check(boss.hp < hp && boss.state == 0 && boss.stun > 0 && boss.cycle == 2 && game.projectiles.Count == 0
                    && GameCore.PendingBossVolley(boss, room).Count == 0, "A genuine stagger break cancels the warning without emitting or advancing its cycle");
            }
            Check(GameCore.PendingBossVolley(null, 3).Count == 0, "Null actors have no salvo authority");
            foreach (int special in new[] { GameCore.SignalWardenId, GameCore.CommissionGuardianId })
            {
                var actor = new Enemy(special, 3, 12, 14, 100, true) { state = 1, attackPhase = 3, cycle = 2 };
                Check(GameCore.PendingBossVolley(actor, 3).Count == 0, "Dedicated Warden and commissioning patterns never gain chapter volley warnings");
            }
            foreach (int state in new[] { 0, 2 })
                Check(GameCore.PendingBossVolley(new Enemy(123, 3, 12, 14, 100, true) { state = state }, 3).Count == 0,
                    "Idle, released and unsupported actor states have no pending chapter salvo");
            Check(GameCore.PendingBossVolley(new Enemy(124, 1, 12, 14, 100) { state = 1 }, 3).Count == 0,
                "An ordinary prepared archer keeps its existing distinct single-arrow warning");
        }

        public static void VolleyTerrainClipsToActualOrbClearanceWithoutMutatingSimulation(Campaign data)
        {
            MethodInfo blocked = typeof(GameCore).GetMethod("ShotBlocked", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(float), typeof(Point) }, null);
            Check(blocked != null, "The warning and actual projectile path share the canonical radius-aware collision query");
            MethodInfo fly = typeof(GameCore).GetMethod("UpdateProjectiles", BindingFlags.Instance | BindingFlags.NonPublic);
            int full = 0, clipped = 0, checkedLanes = 0, checkedFlights = 0;
            foreach (int room in new[] { 2, 3, 5, 6, 7, 8 })
            {
                Enemy boss; GameCore game = BossCommitmentGame(data, room, 3, out boss);
                game.save.x = 12; game.save.y = 14; game.projectiles.Clear();
                for (int y = 1; y < game.Map.rows.Length - 1; y += 2) for (int x = 1; x < game.Map.rows[0].Length - 1; x += 2)
                {
                    Point origin = new Point(x + .5f, y + .5f); if (!game.CanStand(origin)) continue;
                    boss.x = origin.x; boss.y = origin.y; boss.facing = new Point(1, 0);
                    BossVolleyPlan plan = GameCore.PendingBossVolley(boss, room);
                    string before = VolleyState(game); int seed = game.save.seed;
                    for (int index = 0; index < plan.Count; index++)
                    {
                        Point end = game.BossVolleyWarningEnd(boss, index), expected = origin; Point direction = plan.Direction(index);
                        for (int step = 1; step <= 24; step++)
                        {
                            Point next = origin + direction * (step * .1f);
                            if ((bool)blocked.Invoke(game, new object[] { .28f, next })) break;
                            expected = next;
                        }
                        VolleyNear(end, expected, "Each lane stops at the last genuinely clear canonical orb footprint on shipped terrain");
                        Check((end - origin).Length <= GameCore.BossVolleyWarningReach + .0001f, "The local warning never claims the full lifetime of a travelling orb");
                        if ((end - origin).Length >= 2.399f) full++; else clipped++;
                        checkedLanes++;
                    }
                    Check(VolleyState(game) == before && game.save.seed == seed,
                        "All warning rays leave every public simulation field, world lists, sounds, revision and complete save unchanged");
                    if (checkedFlights < 18 && (origin - game.Position).Length > 4)
                    {
                        int lane = checkedFlights % plan.Count; Point end = game.BossVolleyWarningEnd(boss, lane);
                        var shot = new Projectile { position = origin, velocity = plan.Direction(lane), visual = "enemyOrb", source = "shot", damage = 14, life = 20 };
                        game.projectiles.Add(shot); fly.Invoke(game, new object[] { 2.4f });
                        VolleyNear(shot.position, end, "Actual projectile stepping stops at the same last clear point as the displayed short ray");
                        game.projectiles.Clear(); game.effects.Clear(); checkedFlights++;
                    }
                }
            }
            Check(full > 50 && clipped > 50 && checkedLanes > 500 && checkedFlights == 18,
                "The check observes both open and obstructed real terrain plus eighteen physical orb flights, rather than one handpicked blank tile");
        }

        public static void VolleyQueriesKeepLegacySavesExpansionHazardsAndCapturedPlayerActions(Campaign data)
        {
            Check(typeof(EnemyRecord).GetFields().Length == 7, "Volley warnings leave the mature seven-field enemy snapshot unchanged");
            foreach (FieldInfo field in typeof(SaveData).GetFields())
                Check(field.Name.IndexOf("volley", StringComparison.OrdinalIgnoreCase) < 0,
                    "A transient warning never becomes a persistent save requirement");
            foreach (int version in new[] { 1, 2 }) foreach (int room in new[] { 3, 5, 8 })
            {
                Enemy boss; GameCore game = BossCommitmentGame(data, room, 2, out boss);
                game.save.mastery = new[] { 23, 64, 87 }; game.save.flags.Add("return_letter_complete");
                game.save.weaponStyles = new[] { 2, 1, 2 }; game.save.shards = 71;
                game.mode = "paused"; string paused = VolleyState(game), savedBeforePause = SmithSaveState(game.save); float timer = boss.timer, toastTimer = game.toastTime;
                for (int repeat = 0; repeat < 8; repeat++)
                {
                    BossVolleyPlan plan = GameCore.PendingBossVolley(boss, room);
                    for (int index = 0; index < plan.Count; index++) game.BossVolleyWarningEnd(boss, index);
                }
                Check(VolleyState(game) == paused, "Paused warning reads leave every public simulation and saved field exactly unchanged");
                game.Tick(.05f, new Point());
                Check(SmithSaveState(game.save) == savedBeforePause && boss.timer == timer && game.projectiles.Count == 0
                    && Math.Abs(game.toastTime - (toastTimer - .05f)) < .00001f,
                    "Paused world ticks preserve attacks and RNG while retaining the mature notification-only countdown");
                game.Close(); game.PrepareSave(); SaveData saved = CloneSave(game.save); saved.version = version;
                var loaded = new GameCore(data, saved); loaded.Start(); loaded.Close(); Enemy restored = loaded.enemies.Find(e => e.id == boss.id);
                Check(restored != null && restored.hp == boss.hp && restored.state == 0 && GameCore.PendingBossVolley(restored, room).Count == 0,
                    "Legacy load retains exact Boss health but requires a fresh full warning instead of restoring an unseen partial salvo");
                Check(loaded.save.mastery[2] == 87 && loaded.save.weaponStyles[2] == 2 && loaded.save.shards == 71 && loaded.Has("return_letter_complete"),
                    "Legacy warning compatibility retains independent mastery, equipment, economy and story progress");
                loaded.invulnerable = 1000; NextBossCommitment(loaded, restored);
                Check(GameCore.PendingBossVolley(restored, room).phase == 2, "The restored Boss prepares its retained phase with a genuine new salvo");
                ReleaseBossCommitment(loaded, restored); CheckBossReleasePattern(loaded, restored, 2);
            }
            for (int weapon = 0; weapon < 3; weapon++)
            {
                GameCore game = SmithReady(data, weapon); game.Close(); game.Enter(3); game.enemies.Clear();
                game.save.x = 12; game.save.y = 14; game.Equip(weapon); game.invulnerable = 1000;
                var boss = new Enemy(98660 + weapon, 3, weapon == 0 ? 13.1f : 17, 14, 500, true)
                    { state = 1, attackPhase = 2, cycle = 2, timer = 5, stun = 99 };
                game.enemies.Add(boss); Check(game.CanStand(boss.Position), "Captured action target uses shipped legal ground");
                game.Attack(new Point(1, 0)); ActorAction accepted = game.playerAction;
                Check(accepted != null && !accepted.released, "A real sword, bow or starfire action is accepted before rendering any volley");
                ImpactProfile profile = accepted.impact; float damage = accepted.damage, elapsed = accepted.elapsed; string before = VolleyState(game);
                for (int repeat = 0; repeat < 10; repeat++)
                {
                    BossVolleyPlan plan = GameCore.PendingBossVolley(boss, 3);
                    for (int index = 0; index < plan.Count; index++) game.BossVolleyWarningEnd(boss, index);
                }
                Check(VolleyState(game) == before && ReferenceEquals(game.playerAction, accepted) && ReferenceEquals(accepted.impact, profile)
                    && accepted.damage == damage && accepted.elapsed == elapsed, "Reading enemy lanes cannot heal, reroll, cancel, retarget or recapture an already accepted player attack");
                bool sawFlight = weapon == 0;
                for (int frame = 0; frame < 400 && boss.hp == boss.maxHp; frame++)
                {
                    foreach (Projectile shot in game.projectiles) if (shot.friendly)
                    {
                        sawFlight = true; string inFlight = VolleyState(game); Point position = shot.position, velocity = shot.velocity;
                        game.BossVolleyWarningEnd(boss, 0);
                        Check(VolleyState(game) == inFlight && (shot.position - position).Length == 0 && (shot.velocity - velocity).Length == 0,
                            "The same warning reads leave genuinely flying player projectiles and captured contact data intact");
                    }
                    game.Tick(.01f, new Point());
                }
                Check(accepted.released && boss.hp < boss.maxHp && sawFlight && ReferenceEquals(accepted.impact, profile) && accepted.damage == damage,
                    "The original captured melee/arrow/starfire still completes actual physical contact after warning inspection");
            }
        }
    }
}
