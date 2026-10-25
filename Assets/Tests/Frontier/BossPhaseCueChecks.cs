using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        // Invoke the real shared contact seam, never the phase receipt recorder.
        // A noncritical captured impact makes exact floating-point boundaries
        // deterministic; accepted Attack/Q and physical missiles are covered below.
        private static void PhaseCueContact(GameCore game, Enemy boss, float damage)
        {
            typeof(GameCore).GetMethod("Hit", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(game, new object[] { boss, damage, false, null,
                    new ImpactProfile(1, 0, game.save.weapon, 0, 0, true) });
        }
        private static void PhaseCueEmpty(BossPhaseCue cue, string reason)
        {
            Check(cue.phase == 0 && cue.preparedPhase == 0 && cue.fromPhase == 0 && cue.remaining == 0
                && !cue.Changed && !cue.Pending, reason);
        }
        private static void PhaseCueChanged(GameCore game, Enemy boss, int before, int after, int prepared)
        {
            BossPhaseCue cue = game.GetBossPhaseCue(boss);
            Check(cue.phase == after && cue.fromPhase == before && cue.preparedPhase == prepared && cue.Changed
                && cue.Pending == (prepared > 0 && prepared != after) && cue.remaining > 0
                && cue.remaining <= GameCore.BossPhaseCueSeconds,
                "A real nonlethal crossing reports its exact old/live/prepared phases and bounded lifetime");
        }

        public static void PhaseCueActualContactsAndExactBoundariesAcrossEightRooms(Campaign data)
        {
            Check(typeof(BossPhaseCue).IsValueType, "Phase cues are independent value snapshots");
            foreach (FieldInfo field in typeof(BossPhaseCue).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Check(field.IsInitOnly, "Every public phase-cue field is readonly: " + field.Name);
            Check(GameCore.BossPhaseTwoThreshold == .66f && GameCore.BossPhaseThreeThreshold == .33f,
                "Shared live and receipt boundaries retain the mature 66% and 33% thresholds");
            for (int room = 1; room <= 8; room++)
            {
                for (int phase = 1; phase <= 2; phase++)
                {
                    Enemy boss; GameCore game = BossCommitmentGame(data, room, phase, out boss);
                    BossPhaseCue initial = game.GetBossPhaseCue(boss);
                    Check(initial.phase == phase && initial.preparedPhase == phase && !initial.Changed && !initial.Pending,
                        "An actual first warning identifies its current phase without inventing a past crossing");
                    CrossBossThreshold(game, boss, false); PhaseCueChanged(game, boss, phase, phase + 1, phase);
                    Check(initial.phase == phase && initial.remaining == 0, "An earlier value snapshot cannot change after a real hit");

                    game = BossCommitmentGame(data, room, phase, out boss);
                    float boundary = boss.maxHp * (phase == 1 ? GameCore.BossPhaseTwoThreshold : GameCore.BossPhaseThreeThreshold);
                    boss.hp = boundary + 1; boss.staggerRecovery = 4;
                    Check(boss.BossPhase == phase && !game.GetBossPhaseCue(boss).Changed,
                        "Health immediately above an exact boundary remains in its previous phase");
                    PhaseCueContact(game, boss, 1);
                    Check(boss.hp == boundary && boss.BossPhase == phase + 1,
                        "A real shared hit at exact equality enters the next phase in every canonical room");
                    PhaseCueChanged(game, boss, phase, phase + 1, phase);
                    float remaining = game.GetBossPhaseCue(boss).remaining;
                    PhaseCueContact(game, boss, 1);
                    Check(boss.hp == boundary - 1 && game.GetBossPhaseCue(boss).fromPhase == phase
                        && game.GetBossPhaseCue(boss).remaining == remaining,
                        "A further hit below the same boundary neither invents another transition nor refreshes the receipt");
                }
                Enemy skipped; GameCore skip = BossCommitmentGame(data, room, 1, out skipped, 100);
                float hp = skipped.hp; skip.Skill(new Point(1, 0));
                Check(skip.playerAction != null && skip.playerAction.kind == "skill", "A double crossing begins with an accepted real Q");
                for (int frame = 0; frame < 80 && skipped.hp == hp; frame++) skip.Tick(.01f, new Point());
                Check(skipped.hp > 0 && skipped.BossPhase == 3 && skip.enemies.Contains(skipped),
                    "One actual Q crosses both thresholds while the chapter Boss remains alive");
                PhaseCueChanged(skip, skipped, 1, 3, 1);

                Enemy edited; GameCore direct = BossCommitmentGame(data, room, 1, out edited);
                edited.hp = edited.maxHp * .3f;
                BossPhaseCue live = direct.GetBossPhaseCue(edited);
                Check(live.phase == 3 && live.preparedPhase == 1 && live.Pending && !live.Changed && live.remaining == 0,
                    "A query can read live HP but cannot fabricate a hit receipt from an external HP edit");
            }
        }

        public static void PhaseCueKeepsCommittedWarningReleaseAndBreakOutputs(Campaign data)
        {
            for (int room = 1; room <= 8; room++) for (int phase = 1; phase <= 2; phase++)
            {
                Enemy boss; GameCore game = BossCommitmentGame(data, room, phase, out boss);
                float radius = GameCore.EnemyAreaRadius(boss), duration = boss.windupDuration, timer = boss.timer;
                Point target = boss.target, facing = boss.facing, origin = boss.attackOrigin, destination = boss.attackDestination;
                BossVolleyPlan announced = GameCore.PendingBossVolley(boss, room);
                CrossBossThreshold(game, boss, false); PhaseCueChanged(game, boss, phase, phase + 1, phase);
                BossVolleyPlan retained = GameCore.PendingBossVolley(boss, room);
                Check(boss.attackPhase == phase && boss.AttackPhase == phase && boss.windupDuration == duration
                    && boss.timer <= timer && boss.timer > 0 && GameCore.EnemyAreaRadius(boss) == radius
                    && (boss.target - target).Length == 0 && (boss.facing - facing).Length == 0
                    && (boss.attackOrigin - origin).Length == 0 && (boss.attackDestination - destination).Length == 0,
                    "Phase feedback preserves the actual captured attack phase, footprint, target, lane and full warning duration");
                Check(retained.phase == announced.phase && retained.Count == announced.Count
                    && retained.releaseCycle == announced.releaseCycle,
                    "Pending phase text cannot expand or advance the already announced volley");
                for (int index = 0; index < announced.Count; index++)
                    Check((retained.Direction(index) - announced.Direction(index)).Length == 0
                        && retained.Speed(index) == announced.Speed(index), "Every committed lane and projectile speed survives the crossing");
                game.save.x = target.x; game.save.y = target.y; game.invulnerable = 0; float health = game.save.health;
                ReleaseBossCommitment(game, boss);
                Check(Math.Abs(health - game.save.health - (22 + phase * 3)) < .001f,
                    "Actual slam contact retains the captured phase's damage after the live phase changes");
                VolleyEmitted(game, boss, retained, true); CheckBossReleasePattern(game, boss, phase);
                Check(game.GetBossPhaseCue(boss).preparedPhase == 0 && !game.GetBossPhaseCue(boss).Pending,
                    "Actual release immediately retires the old prepared phase from the cue");
                NextBossCommitment(game, boss);
                Check(game.GetBossPhaseCue(boss).phase == phase + 1 && game.GetBossPhaseCue(boss).preparedPhase == phase + 1
                    && !game.GetBossPhaseCue(boss).Pending && boss.attackPhase == phase + 1
                    && Math.Abs(boss.windupDuration - (1.1f - (phase + 1) * .08f)) < .001f,
                    "The next actual warning adopts the current phase and its complete authored timing");

                game = BossCommitmentGame(data, room, phase, out boss); boss.stagger = 94;
                float hp = boss.hp; game.Attack(new Point(1, 0));
                for (int frame = 0; frame < 80 && boss.hp == hp; frame++) game.Tick(.01f, new Point());
                Check(boss.hp > 0 && boss.hp < hp && boss.state == 0 && boss.stun > 0 && boss.cycle == 2
                    && boss.releaseTime <= 0 && game.projectiles.Count == 0 && GameCore.PendingBossVolley(boss, room).Count == 0,
                    "A genuine stagger break cancels the old warning without emitting its attacks or advancing the cycle");
                PhaseCueChanged(game, boss, phase, phase + 1, 0);
                NextBossCommitment(game, boss);
                Check(game.GetBossPhaseCue(boss).preparedPhase == phase + 1 && !game.GetBossPhaseCue(boss).Pending,
                    "The real post-break warning starts cleanly in the live phase");
            }
        }

        public static void PhaseCueQueriesLifetimeOwnershipAndLegacyLoads(Campaign data)
        {
            Enemy boss; GameCore game = BossCommitmentGame(data, 3, 1, out boss); CrossBossThreshold(game, boss, false);
            BossPhaseCue snapshot = game.GetBossPhaseCue(boss); ActorAction accepted = game.playerAction;
            string before = VolleyState(game); int generation = game.WorldGeneration;
            for (int repeat = 0; repeat < 20; repeat++) PhaseCueChanged(game, boss, 1, 2, 1);
            Check(VolleyState(game) == before && game.WorldGeneration == generation && ReferenceEquals(game.playerAction, accepted)
                && game.GetBossPhaseCue(boss).remaining == snapshot.remaining,
                "Repeated cue queries preserve complete public simulation/save state, RNG and the accepted player action");
            float timer = boss.timer, remaining = snapshot.remaining;
            game.mode = "paused"; string paused = VolleyState(game);
            for (int repeat = 0; repeat < 5; repeat++) PhaseCueEmpty(game.GetBossPhaseCue(boss), "A paused world presents no active combat cue");
            Check(VolleyState(game) == paused, "Paused reads have no simulation or persistence side effects");
            game.Tick(.05f, new Point()); game.Close();
            Check(game.GetBossPhaseCue(boss).remaining == remaining && boss.timer == timer,
                "A menu freezes the receipt and its prepared attack, preserving both on return");
            game.hitStop = .1f; game.Tick(.05f, new Point());
            Check(game.GetBossPhaseCue(boss).remaining == remaining && boss.timer == timer,
                "Hit stop freezes the phase receipt on the same simulation clock as the attack");
            game.hitStop = 0; boss.stun = 99; game.Tick(.05f, new Point());
            Check(Math.Abs(game.GetBossPhaseCue(boss).remaining - (remaining - .05f)) < .00001f,
                "An active simulation tick consumes precisely its own phase-cue lifetime");
            remaining = game.GetBossPhaseCue(boss).remaining; PhaseCueContact(game, boss, 1);
            Check(game.GetBossPhaseCue(boss).remaining == remaining, "A same-phase contact cannot extend an existing notice");
            boss.staggerRecovery = 4; PhaseCueContact(game, boss, boss.hp - boss.maxHp * GameCore.BossPhaseThreeThreshold);
            PhaseCueChanged(game, boss, 2, 3, 1);
            Check(game.GetBossPhaseCue(boss).remaining == GameCore.BossPhaseCueSeconds && snapshot.phase == 2 && snapshot.fromPhase == 1,
                "A subsequent real crossing replaces the receipt and refreshes its bounded lifetime without mutating old snapshots");
            Advance(game, GameCore.BossPhaseCueSeconds + .1f);
            Check(!game.GetBossPhaseCue(boss).Changed && game.GetBossPhaseCue(boss).fromPhase == 0
                && game.GetBossPhaseCue(boss).remaining == 0 && game.GetBossPhaseCue(boss).phase == 3,
                "Expiry removes transient crossing history while retaining the live phase");

            foreach (string boundary in new[] { "actor", "save", "generation", "room" })
            {
                game = BossCommitmentGame(data, 3, 1, out boss); CrossBossThreshold(game, boss, false);
                if (boundary == "actor")
                {
                    var impostor = new Enemy(boss.id, boss.kind, boss.x, boss.y, boss.maxHp, true) { hp = boss.hp, state = 1, attackPhase = 1 };
                    game.enemies.Add(impostor);
                    Check(!game.GetBossPhaseCue(impostor).Changed && game.GetBossPhaseCue(boss).Changed,
                        "The same numeric actor ID cannot acquire another object instance's receipt");
                    game.enemies.Remove(boss); PhaseCueEmpty(game.GetBossPhaseCue(boss), "A removed actor immediately loses presentation authority");
                    game.Tick(.01f, new Point()); game.enemies.Add(boss);
                }
                else if (boundary == "save") game.save = CloneSave(game.save);
                else if (boundary == "generation")
                {
                    generation = game.WorldGeneration; SaveData owner = game.save; int room = game.save.room;
                    typeof(GameCore).GetMethod("ResetTransient", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(game, null);
                    Check(game.WorldGeneration == generation + 1 && ReferenceEquals(game.save, owner) && game.save.room == room,
                        "A real same-save same-room reset creates a fresh world generation");
                }
                else game.save.room = 2;
                Check(!game.GetBossPhaseCue(boss).Changed && game.GetBossPhaseCue(boss).remaining == 0,
                    "Actor, selected-save, generation and room ownership each reject stale crossing receipts: " + boundary);
            }
            Check(typeof(EnemyRecord).GetFields().Length == 7, "Phase feedback retains the mature seven-field enemy save record");
            foreach (Type type in new[] { typeof(SaveData), typeof(EnemyRecord) }) foreach (FieldInfo field in type.GetFields())
                Check(field.FieldType != typeof(BossPhaseCue) && field.Name.IndexOf("bossPhase", StringComparison.OrdinalIgnoreCase) < 0,
                    "A transient phase receipt is absent from persisted save schemas");
            for (int room = 1; room <= 8; room++) foreach (int version in new[] { 1, 2 })
            {
                game = BossCommitmentGame(data, room, 1, out boss); CrossBossThreshold(game, boss, false);
                game.PrepareSave(); SaveData saved = CloneSave(game.save); saved.version = version;
                var loaded = new GameCore(data, saved); loaded.Start(); loaded.Close();
                Enemy restored = loaded.enemies.Find(e => e.id == boss.id); Check(restored != null, "Legacy load retains the actual living chapter Boss");
                BossPhaseCue cue = loaded.GetBossPhaseCue(restored);
                Check(restored.hp == boss.hp && restored.BossPhase == 2 && restored.state == 0 && cue.phase == 2
                    && cue.preparedPhase == 0 && !cue.Pending && !cue.Changed && cue.fromPhase == 0 && cue.remaining == 0,
                    "Version-one and version-two loads preserve HP while replaying neither a crossing receipt nor a partial warning");
                loaded.invulnerable = 1000; NextBossCommitment(loaded, restored); cue = loaded.GetBossPhaseCue(restored);
                Check(cue.phase == 2 && cue.preparedPhase == 2 && !cue.Pending && !cue.Changed,
                    "The first genuine loaded warning adopts retained HP without inventing a historical hit event");
            }
        }

        public static void PhaseCueInvalidSpecialTrainingDeathAndLethalGuards(Campaign data)
        {
            Enemy boss; GameCore game = BossCommitmentGame(data, 3, 1, out boss);
            PhaseCueEmpty(game.GetBossPhaseCue(null), "Null actors expose an empty cue");
            PhaseCueEmpty(game.GetBossPhaseCue(new Enemy(boss.id, 3, boss.x, boss.y, 1000, true)),
                "A foreign actor with the same ID is not a live encounter member");
            foreach (int id in new[] { GameCore.SignalWardenId, GameCore.CommissionGuardianId })
            {
                boss.id = id; PhaseCueContact(game, boss, 20);
                PhaseCueEmpty(game.GetBossPhaseCue(boss), "Dedicated Warden and commissioning patterns never receive chapter-phase cues");
                boss.hp = 670;
            }
            boss.id = 13; boss.boss = false; PhaseCueContact(game, boss, 20);
            PhaseCueEmpty(game.GetBossPhaseCue(boss), "Ordinary enemies retain their separate presentation"); boss.boss = true;
            foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                boss.hp = invalid; PhaseCueEmpty(game.GetBossPhaseCue(boss), "Dead and nonfinite HP cannot authorize a chapter cue");
                boss.hp = 670; boss.maxHp = invalid;
                PhaseCueEmpty(game.GetBossPhaseCue(boss), "Invalid maximum health cannot authorize a chapter cue"); boss.maxHp = 1000;
            }
            foreach (int room in new[] { 0, 9, 10, 11 })
            { game.save.room = room; PhaseCueEmpty(game.GetBossPhaseCue(boss), "Only the eight canonical chapter arenas expose phase feedback"); }
            game.save.room = 3;
            foreach (string mode in new[] { "title", "paused", "dialog", "inventory" })
            { game.mode = mode; PhaseCueEmpty(game.GetBossPhaseCue(boss), "Inactive modes cannot present a live phase cue"); }
            game.Close(); SaveData ownerSave = game.save; game.save = null;
            PhaseCueEmpty(game.GetBossPhaseCue(boss), "Missing save ownership safely rejects a query"); game.save = ownerSave;

            game = BossCommitmentGame(data, 3, 1, out boss); float hp = boss.hp;
            PhaseCueContact(game, boss, 0); PhaseCueContact(game, boss, -20);
            Check(boss.hp > hp && !game.GetBossPhaseCue(boss).Changed, "Zero damage and healing never record a downward HP phase crossing");
            foreach (string training in new[] { "basic", "sparring", "range" })
            {
                GameCore drill = SparringGame(data);
                if (training == "basic") drill.StartPractice();
                else Check(training == "sparring" ? drill.StartSparring() : drill.StartRange(0), "The actual safe training session begins");
                Check(drill.practicing && (training == "basic" || drill.SafeTrainingActive), "The requested real training context is active");
                // Stage otherwise eligible chapter metadata so the training guard,
                // rather than town/ordinary-actor rejection, must refuse this cue.
                drill.save.room = 3; boss = new Enemy(13, 3, 13.5f, 14, 1000, true) { hp = 670, state = 1, attackPhase = 1 };
                drill.enemies.Add(boss); PhaseCueContact(drill, boss, 20);
                PhaseCueEmpty(drill.GetBossPhaseCue(boss), "Basic, sparring and range contacts never create chapter crossing receipts");
                drill.practicing = false; if (!drill.enemies.Contains(boss)) drill.enemies.Add(boss);
                Check(!drill.GetBossPhaseCue(boss).Changed && drill.GetBossPhaseCue(boss).remaining == 0,
                    "Leaving a training guard cannot reveal a receipt secretly recorded by a drill contact");
            }
            game = BossCommitmentGame(data, 3, 1, out boss); CrossBossThreshold(game, boss, false);
            game.invulnerable = 0;
            typeof(GameCore).GetMethod("Hurt", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(game, new object[] { 10000f, "slam", null });
            Check(game.Dying && game.save.health == 0, "The actual shared player-damage path begins death");
            PhaseCueEmpty(game.GetBossPhaseCue(boss), "Player death immediately clears the active phase receipt");
            game.deathTime = 0; game.save.health = 1;
            Check(!game.GetBossPhaseCue(boss).Changed, "A cleared death receipt cannot reappear when the death guard is removed");

            game = BossCommitmentGame(data, 3, 1, out boss); boss.stun = 99;
            game.projectiles.Add(new Projectile { position = boss.Position - new Point(.2f, 0), velocity = new Point(1, 0),
                friendly = true, damage = 10000, life = 1, visual = "arrow", impact = new ImpactProfile(1, 0, 0, 0, 0, true) });
            game.Tick(.01f, new Point());
            Check(boss.hp <= 0 && !game.enemies.Contains(boss) && game.fallen.Exists(body => ReferenceEquals(body.actor, boss)),
                "A genuine physical missile defeats the chapter Boss through the shared hit/death path");
            PhaseCueEmpty(game.GetBossPhaseCue(boss), "A lethal threshold skip produces no living phase announcement");
            boss.hp = boss.maxHp * .3f; game.enemies.Add(boss);
            Check(!game.GetBossPhaseCue(boss).Changed && game.GetBossPhaseCue(boss).remaining == 0,
                "Restaging that actor proves a lethal hit recorded no hidden crossing receipt");
        }
    }
}
