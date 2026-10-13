using System;
using System.Collections.Generic;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore SparringGame(Campaign data, int profession = 0)
        {
            var g = Fresh(data, profession); g.SkipTutorial(); g.save.flags.Add("core_1");
            WorldObject mentor = Object(g, "mentor");
            g.save.x = mentor.x; g.save.y = mentor.y + 1;
            return g;
        }
        private static void SparringChoose(GameCore g, string action)
        {
            Check(g.mode == "dialog" && g.conversation != null, "A sparring menu must be opened through its mentor");
            for (int i = 0; i < g.conversation.choices.Length; i++)
                if (g.conversation.choices[i].action == action) { g.Choose(i); return; }
            throw new Exception("Missing sparring choice " + action);
        }
        private static void SparringClearStage(GameCore g)
        {
            Check(g.SparringActive && !g.SparringAwaitingNext && !g.SparringFinished && g.enemies.Count > 0,
                "Each sparring lesson begins with its own live opponents");
            Combat(g, false); Advance(g, .1f);
            Check(g.SparringActive && !g.Dying && g.enemies.Count == 0 &&
                (g.SparringAwaitingNext || g.SparringFinished),
                "Starter attacks, techniques and movement clear the real authored AI and reach a stable lesson result");
        }
        private static void SparringClearAll(GameCore g)
        {
            Check(g.StartSparring(), "The nearby mentor opens a deliberate sparring session");
            for (int stage = 0; stage < 3; stage++)
            {
                Check(g.SparringStage == stage, "Manual progression preserves the authored lesson order");
                SparringClearStage(g);
                if (stage < 2) Check(g.ContinueSparring(), "A deliberate next-lesson action starts the following wave");
            }
            Check(g.SparringFinished && g.Has("sparring_complete"), "All three actual victories complete the optional mentor challenge");
        }
        private static void SparringContact(GameCore g, Enemy e)
        {
            g.playerAction = null; g.hitStop = 0; g.playerHitTime = 0;
            g.attackCooldown = 0; g.combo = 0; g.comboTimer = 0; g.save.seed = 1;
            g.save.x = e.x - 1; g.save.y = e.y; e.timer = 99;
            float hp = e.hp; g.Attack(new Point(1, 0));
            for (int frame = 0; frame < 80 && e.hp == hp; frame++) g.Tick(.01f, new Point());
            Check(e.hp < hp, "A prepared starter attack must reach an actual training opponent contact");
        }

        public static void SparringRealHitsCountersFlanksAndBreaksKeepCombatFeedback(Campaign data)
        {
            var g = SparringGame(data); Check(g.StartSparring(), "Begin the hit-feedback lesson");
            Enemy hunter = g.enemies[0]; float initialHp = hunter.hp; SparringContact(g, hunter);
            Check(hunter.hp < initialHp && hunter.flash > 0 && hunter.hitTime > 0 &&
                g.effects.Exists(x => x.kind == "impact") && g.effects.Exists(x => x.kind == "number"),
                "Unlike an invulnerable dummy, sparring keeps ordinary opponent damage, recoil and readable impact numbers");
            Advance(g, .1f); // A melee impact's existing hit stop freezes the next contact too.
            g.invulnerable = 0; int hits = g.SparringHitsTaken;
            g.projectiles.Add(new Projectile { position = g.Position, source = "shot", ownerId = hunter.id, damage = 999 });
            g.Tick(.02f, new Point());
            Check(!g.Dying && g.save.health > 0 && g.SparringHitsTaken == hits + 1 && g.playerHitTime > 0 &&
                g.effects.Exists(x => x.kind == "hurt"),
                "An actual hostile training contact counts a mistake and shows the real player hit reaction without causing death");
            Check(g.save.deaths == 0, "Rehearsal mistakes cannot create campaign defeat records"); g.EndPractice();

            var counter = SparringGame(data); Check(counter.StartSparring(), "Begin the perfect-dodge contact scenario");
            Enemy target = counter.enemies[0]; target.timer = 99; counter.Dash(new Point(1, 0));
            counter.projectiles.Add(new Projectile { position = counter.Position, source = "shot", ownerId = target.id, damage = 14 });
            counter.Tick(.01f, new Point());
            Check(counter.riposteTime > 0 && counter.SparringCounters == 0 && counter.SparringHitsTaken == 0,
                "Perfectly avoiding an actual contact arms the existing riposte without counting an unlanded counterattack");
            Advance(counter, .35f); SparringContact(counter, target);
            Check(counter.SparringCounters == 1 && counter.riposteTime <= 0,
                "The score counts the actual captured counterattack impact after the existing perfect-dodge mechanic"); counter.EndPractice();

            var ranged = SparringGame(data, 1); Check(ranged.StartSparring(), "Begin a real bow-counter flight");
            Enemy rangedTarget = ranged.enemies[0]; rangedTarget.timer = 99;
            ranged.Dash(new Point(1, 0));
            ranged.projectiles.Add(new Projectile { position = ranged.Position, source = "shot", ownerId = rangedTarget.id, damage = 14 });
            ranged.Tick(.01f, new Point()); Advance(ranged, .35f);
            Check(ranged.riposteTime > 0 && ranged.SparringCounters == 0,
                "A bow rehearsal arms its counter through an actual perfectly dodged hostile contact");
            ranged.save.x = rangedTarget.x - 3; ranged.save.y = rangedTarget.y;
            ranged.Attack(new Point(1, 0));
            Projectile counterArrow = null;
            for (int frame = 0; frame < 80 && counterArrow == null; frame++)
            {
                ranged.Tick(.01f, new Point());
                counterArrow = ranged.projectiles.Find(p => p.friendly && p.impact != null && p.impact.counter);
            }
            Check(counterArrow != null && ranged.SparringCounters == 0 && ranged.riposteTime <= 0,
                "Releasing the actual bow counter consumes riposte but cannot score an arrow that has not hit yet");
            float rangedHp = rangedTarget.hp; ranged.Equip(0);
            for (int frame = 0; frame < 80 && rangedTarget.hp == rangedHp; frame++) ranged.Tick(.01f, new Point());
            Check(rangedTarget.hp < rangedHp && ranged.SparringCounters == 1 && counterArrow.impact.weapon == 1 && counterArrow.impact.counter,
                "A counter arrow retains its captured tactic and bow profile after a real weapon switch until the actual flight contact");
            ranged.EndPractice();

            var front = SparringGame(data); Check(front.StartSparring(), "Begin the armor comparison");
            SparringClearStage(front); Check(front.ContinueSparring(), "Reach the authored heavy through a real hunter victory");
            Enemy heavy = front.enemies[0]; heavy.facing = new Point(-1, 0); float before = heavy.hp;
            int breaksBeforeHeavy = front.SparringBreaks;
            int flanks = front.SparringFlanks; SparringContact(front, heavy); float guarded = before - heavy.hp;
            Check(front.SparringFlanks == flanks && front.effects.Exists(x => x.kind == "number" && (x.text == "GUARDED" || x.text == "正面护甲")),
                "A frontal strike retains the ordinary armored feedback instead of awarding flank credit");
            heavy.facing = new Point(1, 0); heavy.hitTime = 0; heavy.stun = 0; heavy.state = 0;
            before = heavy.hp; SparringContact(front, heavy);
            Check(before - heavy.hp > guarded * 2 && front.SparringFlanks == flanks + 1,
                "A real rear-side hit bypasses the same frontal armor and records that demonstrated tactic");
            // Remaining health is deliberately generous for this isolated ordinary
            // stagger exercise; attack damage and the live break threshold stay real.
            heavy.hp = heavy.maxHp = 999;
            for (int swing = 0; swing < 10 && front.SparringBreaks == breaksBeforeHeavy; swing++)
            { heavy.hitTime = 0; heavy.stun = 0; heavy.state = 0; SparringContact(front, heavy); }
            Check(front.SparringBreaks > breaksBeforeHeavy && heavy.stun > 0 && heavy.staggerRecovery > 0 && front.effects.Exists(x => x.kind == "break"),
                "Ordinary stagger accumulation produces a real break, control window and tactic score");
            front.EndPractice();
        }

        public static void SparringEntryRequiresMentorAndPreservesBasicChoices(Campaign data)
        {
            var novice = Fresh(data); WorldObject noviceMentor = Object(novice, "mentor");
            novice.save.x = noviceMentor.x; novice.save.y = noviceMentor.y + 1;
            Check(!novice.StartSparring() && !novice.practicing,
                "New players first complete basic practice or earn their first core before the mixed sparring lessons");
            var g = SparringGame(data); WorldObject mentor = Object(g, "mentor");
            g.Talk(mentor);
            Check(g.conversation.choices.Length >= 3 && g.conversation.choices[0].action == "practice" &&
                g.conversation.choices[1].action == "close" && g.conversation.choices[2].action == "sparring_menu",
                "The original practice and later choices retain their order while the mentor gains an optional sparring briefing");
            SparringChoose(g, "sparring_menu");
            Check(g.mode == "dialog" && !g.SparringActive && !g.practicing,
                "Reading the briefing does not silently start a challenge");
            SparringChoose(g, "sparring_start");
            Check(g.mode == "world" && g.SparringActive && g.practicing && !g.StartSparring(),
                "Actual mentor choices start one session and cannot stack opponents");
            g.EndPractice();
            g.save.x = mentor.x + 3; g.save.y = mentor.y;
            Check(!g.StartSparring() && !g.practicing, "The optional challenge cannot start remotely");
            g.save.x = mentor.x; g.save.y = mentor.y + 1;
            foreach (string mode in new[] { "title", "dialog", "pause", "inventory", "journal", "settings" })
            { g.mode = mode; Check(!g.StartSparring(), "A menu or title cannot begin a sparring wave"); }
            g.Close(); g.Enter(1);
            Check(!g.StartSparring() && !g.practicing, "Other regions retain their real encounters instead of starting a town lesson");
            g.Enter(0); g.save.x = mentor.x; g.save.y = mentor.y + 1; g.StartPractice();
            Check(g.practicing && !g.SparringActive && !g.StartSparring(),
                "The original dummy practice retains its own identity and cannot be replaced mid-lesson");
            g.EndPractice();
        }

        private static void SparringSameArray(int[] actual, int[] baseline, string description)
        {
            Check(actual.Length == baseline.Length, description + " keeps its existing array dimensions");
            for (int i = 0; i < actual.Length; i++) Check(actual[i] == baseline[i], description + " cannot change at index " + i);
        }
        private static void SparringUnchangedEconomy(GameCore g, SaveData baseline, int rewardCoins, int rewardPoints)
        {
            Check(g.save.coins == baseline.coins + rewardCoins && g.save.skillPoints == baseline.skillPoints + rewardPoints &&
                g.save.xp == baseline.xp && g.save.level == baseline.level && g.save.shards == baseline.shards &&
                g.save.bounty == baseline.bounty && g.save.bountyProgress == baseline.bountyProgress && g.save.bountyCycle == baseline.bountyCycle &&
                g.save.runCoins == baseline.runCoins && g.save.runShards == baseline.runShards && g.save.runXp == baseline.runXp &&
                g.save.stage == baseline.stage && g.save.ending == baseline.ending && g.save.frontierEnding == baseline.frontierEnding,
                "Only the stated first-clear reward changes the permanent economy and story");
            SparringSameArray(g.save.inventory, baseline.inventory, "Supplies");
            SparringSameArray(g.save.kills, baseline.kills, "Regional kill counts");
            SparringSameArray(g.save.mastery, baseline.mastery, "Weapon mastery");
            SparringSameArray(g.save.explored, baseline.explored, "Map exploration");
            SparringSameArray(g.save.reputation, baseline.reputation, "Faction support");
            SparringSameArray(g.save.relationships, baseline.relationships, "Companion relationships");
            Check(!g.Has("boss_0") && !g.Has("sparring_defeated_75000"),
                "Practice defeats cannot create guardian progress or persistent loot-farming actor records");
        }

        public static void SparringSandboxFreezesWorldEconomyAndRestoresVitals(Campaign data)
        {
            var g = SparringGame(data); g.save.health = 43; g.save.energy = 37;
            g.save.totalTime = 90; g.save.plantedAt = 10; g.save.foodUntil = 120;
            g.save.tea = 3; g.save.teaUntil = 130; g.save.mealKind = 2; g.save.mealUntil = 210;
            g.save.tideHeldUntil = 170; g.save.heat = 34; g.save.cold = 17;
            g.save.bounty = 4; g.save.bountyProgress = 1; g.save.shards = 8;
            SaveData baseline = CloneSave(g.save); Point original = g.Position;
            Check(g.StartSparring(), "A prepared adventurer can rehearse without risking their road provisions");
            g.Eat(); g.DrinkTea(); Advance(g, 5);
            Check(g.save.totalTime == baseline.totalTime && g.save.plantedAt == baseline.plantedAt &&
                g.save.foodUntil == baseline.foodUntil && g.save.teaUntil == baseline.teaUntil &&
                g.save.mealUntil == baseline.mealUntil && g.save.tideHeldUntil == baseline.tideHeldUntil &&
                g.save.tea == baseline.tea && g.SparringTime > 4.5f,
                "Sparring measures its own live time while crops, meals, tea, tides and supplies keep their original world deadlines");
            SparringClearStage(g); Check(g.ContinueSparring(), "Advance into the heavy lesson");
            SparringClearStage(g); Check(g.ContinueSparring(), "Advance into the mixed lesson"); SparringClearStage(g);
            SparringUnchangedEconomy(g, baseline, 25, 1);
            g.EndPractice();
            Check(!g.SparringActive && !g.practicing && (g.Position - original).Length < .001f &&
                g.save.health == baseline.health && g.save.energy == baseline.energy &&
                g.save.heat == baseline.heat && g.save.cold == baseline.cold && g.save.totalTime == baseline.totalTime &&
                g.save.mealUntil == baseline.mealUntil && g.save.foodUntil == baseline.foodUntil,
                "Leaving the sandbox restores the entering body and paid preparation clocks without discarding earned progress");
            SparringUnchangedEconomy(g, baseline, 25, 1);
        }

        public static void SparringPauseManualAdvanceAndExitCancelPreparedContacts(Campaign data)
        {
            var g = SparringGame(data, 2); Check(g.StartSparring(), "Begin the actual caster rehearsal");
            Enemy e = g.enemies[0]; Point body = g.Position; float hp = e.hp, time = g.SparringTime;
            g.mode = "pause"; g.Tick(.05f, new Point(1, 0));
            Check(g.SparringTime == time && e.hp == hp && (g.Position - body).Length == 0 && !g.ContinueSparring(),
                "A paused rehearsal freezes simulation and rejects wave advancement");
            g.Close(); SparringClearStage(g);
            int stage = g.SparringStage; g.mode = "inventory";
            Check(!g.ContinueSparring() && g.SparringStage == stage && g.enemies.Count == 0,
                "A result waiting behind another menu cannot be advanced by a stale next action");
            int previousHits = g.SparringHitsTaken, previousCounters = g.SparringCounters,
                previousBreaks = g.SparringBreaks, previousFlanks = g.SparringFlanks;
            float previousTime = g.SparringTime;
            g.Close(); Check(g.ContinueSparring(), "A live result acceptance starts the next challenge");
            Check(g.SparringHitsTaken == previousHits && g.SparringCounters == previousCounters &&
                g.SparringBreaks == previousBreaks && g.SparringFlanks == previousFlanks && g.SparringTime == previousTime,
                "Manual wave progression retains the course's demonstrated tactics and elapsed live time");
            SparringReturnRecord returnBody = g.save.sparringReturn;
            g.hitStop = g.invulnerable = 0;
            g.projectiles.Add(new Projectile { position = g.Position, source = "shot", ownerId = g.enemies[0].id, damage = 14 });
            g.Tick(.02f, new Point());
            Check(g.SparringHitsTaken == previousHits + 1,
                "A real new-wave mistake contributes to the cumulative course feedback");
            Check(g.RestartSparring() && g.SparringStage == 0 && !g.SparringAwaitingNext && !g.SparringFinished &&
                g.SparringHitsTaken == 0 && g.SparringCounters == 0 && g.SparringBreaks == 0 && g.SparringFlanks == 0 &&
                g.SparringTime == 0 && g.projectiles.Count == 0 && g.playerAction == null &&
                System.Object.ReferenceEquals(returnBody, g.save.sparringReturn),
                "Restart begins a fresh feedback record and removes pending contacts while preserving the original safe return body");
            SparringClearStage(g); Check(g.ContinueSparring(), "A restarted course still reaches its heavy through a real first-wave victory");
            g.Skill(new Point(1, 0)); g.Attack(new Point(1, 0));
            g.projectiles.Add(new Projectile { position = g.Position + new Point(3, 0), velocity = new Point(-1, 0),
                ownerId = 75010, source = "shot", damage = 14, life = 4 });
            g.EndPractice();
            Check(!g.practicing && !g.SparringActive && g.enemies.Count == 0 && g.projectiles.Count == 0 &&
                g.playerAction == null && g.dashTime == 0 &&
                g.skillCooldown == 0 && g.attackCooldown == 0,
                "Exiting a live rehearsal cancels pending techniques, buffered input, practice missiles and frost before returning to town");
            int money = g.save.coins; Advance(g, 1);
            Check(!g.Has("sparring_complete") && g.save.coins == money && g.enemies.Count == 0 &&
                !g.effects.Exists(x => x.kind == "frost") && !g.projectiles.Exists(x => x.technique),
                "Abandoned delayed contacts cannot finish a lesson or pay a reward after returning");
            WorldObject mentor = Object(g, "mentor"); g.save.x = mentor.x; g.save.y = mentor.y + 1;
            Check(g.StartSparring() && g.SparringStage == 0 && !g.SparringAwaitingNext && !g.SparringFinished,
                "A fresh deliberate attempt starts with the hunter instead of resuming abandoned shots");
            g.Enter(1);
            Check(!g.practicing && !g.SparringActive && g.save.room == 1 && !g.Has("sparring_complete") &&
                g.enemies.TrueForAll(x => x.id < 75000 || x.id > 75021),
                "Actual travel tears down the practice yard and retains the destination's mature encounter");
        }

        public static void SparringSaveReturnsSafeBodyWithoutResumingPracticeActors(Campaign data)
        {
            var g = SparringGame(data, 1); g.save.health = 61; g.save.energy = 28;
            g.save.totalTime = 71; g.save.foodUntil = 100; g.save.mealKind = 1; g.save.mealUntil = 171;
            g.save.heat = 22; g.save.cold = 12; Point original = g.Position; SaveData baseline = CloneSave(g.save);
            Check(g.StartSparring(), "Start the training snapshot case");
            g.Tick(.05f, new Point(1, 0)); g.Attack(new Point(1, 0)); g.PrepareSave(); Point live = g.Position;
            Check(g.SparringActive && g.practicing && g.save.snapshotRoom == -1 && g.save.worldEnemies == null && g.save.sparringReturn != null,
                "Saving keeps the active live rehearsal but serializes its return body rather than a practice encounter");
            var loaded = new GameCore(data, CloneSave(g.save)); loaded.Start();
            Check(!loaded.practicing && !loaded.SparringActive && loaded.save.room == 0 && loaded.save.sparringReturn == null &&
                (loaded.Position - original).Length < .001f && loaded.save.health == baseline.health && loaded.save.energy == baseline.energy &&
                loaded.save.heat == baseline.heat && loaded.save.cold == baseline.cold && loaded.save.totalTime == baseline.totalTime &&
                loaded.save.foodUntil == baseline.foodUntil && loaded.save.mealUntil == baseline.mealUntil && loaded.enemies.Count == 0 &&
                !loaded.Has("sparring_complete"),
                "Reloading an interrupted sandbox returns to the original town position and vitals without recreating opponents, rewards or expiring provisions");
            Check(g.SparringActive && (g.Position - live).Length == 0 && g.save.sparringReturn != null,
                "Constructing the saved game does not mutate the live rehearsal or its pending return record");
            g.EndPractice();
            Check(g.save.sparringReturn == null, "Normal exit clears the transient recovery record");
            SparringClearAll(g); g.PrepareSave();
            var finished = new GameCore(data, CloneSave(g.save)); finished.Start();
            SparringUnchangedEconomy(finished, baseline, 25, 1);
            Check(finished.Has("sparring_complete") && !finished.SparringActive && finished.save.sparringReturn == null,
                "Saving the final score retains the earned reward and returns safely without reviving the trial");
        }

        public static void SparringLegacyProgressAndOriginalDummyPracticeRemainIndependent(Campaign data)
        {
            foreach (int version in new[] { 1, 2 })
            {
                var old = new SaveData { version = version, room = 1, stage = 2, x = 14, y = 15,
                    coins = 91, health = 63, maxHealth = 120, energy = 42, totalTime = 71,
                    flags = new List<string> { "intro", "scout_saved", "core_1", "watch_complete", "archive_complete" },
                    snapshotRoom = 1, worldEnemies = new[] { new EnemyRecord { id = 17, kind = 0,
                        x = 15, y = 17, hp = 23, maxHp = 52 } } };
                var loaded = new GameCore(data, old); loaded.Start();
                Check(!loaded.SparringActive && !loaded.Has("sparring_complete") && loaded.save.sparringReturn == null &&
                    loaded.save.room == 1 && loaded.save.stage == 2 && loaded.save.coins == 91 && loaded.save.health == 63 &&
                    loaded.save.energy == 42 && loaded.save.totalTime == 71 && loaded.Has("watch_complete") && loaded.Has("archive_complete") &&
                    loaded.enemies.Count == 1 && loaded.enemies[0].id == 17 && loaded.enemies[0].hp == 23,
                    "Saves without a training return record retain their mature story, economy, location, vitals and ordinary encounter health");
            }
            var basicGraduate = Fresh(data); basicGraduate.save.practiceBits = 7;
            WorldObject mentor = Object(basicGraduate, "mentor"); basicGraduate.save.x = mentor.x; basicGraduate.save.y = mentor.y + 1;
            Check(basicGraduate.StartSparring() && !basicGraduate.Has("core_1"),
                "Completing the original combat tutorial opens the optional challenge before finding a story core");
            basicGraduate.EndPractice();
            var g = Fresh(data); g.SkipTutorial(); int coins = g.save.coins, points = g.save.skillPoints;
            PracticeRun(g);
            Check(g.save.practiceBits == 7 && g.Has("practice_reward") && !g.Has("sparring_complete") &&
                g.save.coins == coins + 15 && g.save.skillPoints == points + 1,
                "The original actual dummy, dodge and Q sequence preserves its separate reward and mature tutorial behavior");
            mentor = Object(g, "mentor"); g.save.x = mentor.x; g.save.y = mentor.y + 1;
            SparringClearAll(g); g.EndPractice();
            Check(g.save.practiceBits == 7 && g.Has("practice_reward") && g.Has("sparring_complete") &&
                g.save.coins == coins + 40 && g.save.skillPoints == points + 2,
                "The mature three-step lesson and new mixed fights reward their separate one-time accomplishments");
            PracticeRun(g);
            Check(g.save.coins == coins + 40 && g.save.skillPoints == points + 2,
                "Returning to the original practice after sparring cannot repeat either permanent reward");
        }

        public static void SparringMalformedReturnRecordsAndReservedActorsStaySafe(Campaign data)
        {
            foreach (SparringReturnRecord record in new[] {
                new SparringReturnRecord { x = float.NaN, y = float.PositiveInfinity, health = float.NaN,
                    energy = float.PositiveInfinity, heat = float.NaN, cold = float.PositiveInfinity },
                new SparringReturnRecord { x = -300, y = 999, health = -5, energy = -99, heat = -200, cold = 999 } })
            {
                var saved = new SaveData { room = 1, stage = 2, x = 14, y = 13, coins = 91,
                    maxHealth = 120, flags = new List<string> { "intro", "core_1", "watch_complete", "archive_complete" },
                    sparringReturn = record, snapshotRoom = 0,
                    worldEnemies = new[] { new EnemyRecord { id = 75000, kind = 0, x = 16, y = 13, hp = 20, maxHp = 70 } } };
                var restored = new GameCore(data, saved); restored.Start();
                Check(restored.save.room == 0 && restored.save.sparringReturn == null && !restored.SparringActive &&
                    !restored.practicing && restored.enemies.Count == 0 && restored.save.coins == 91 && restored.save.stage == 2 &&
                    restored.Has("core_1") && restored.Has("watch_complete") && restored.Has("archive_complete"),
                    "Malformed sandbox snapshots abandon only the practice attempt and preserve mature progress without fabricating actors or rewards");
                Check(!float.IsNaN(restored.save.x) && !float.IsInfinity(restored.save.x) &&
                    !float.IsNaN(restored.save.y) && !float.IsInfinity(restored.save.y) &&
                    !restored.Blocked(restored.save.x - .22f, restored.save.y - .18f) &&
                    !restored.Blocked(restored.save.x + .22f, restored.save.y + .18f) &&
                    restored.save.health > 0 && restored.save.health <= restored.save.maxHealth &&
                    restored.save.energy >= 0 && restored.save.energy <= 100 &&
                    restored.save.heat >= 0 && restored.save.heat <= 100 && restored.save.cold >= 0 && restored.save.cold <= 100,
                    "Nonfinite or out-of-range return-body data resolves to finite collision-safe town control and bounded vitals");
            }
            var crossRoom = new SaveData { room = 8, stage = 3, frontierEnding = 1, x = 5, y = 14,
                coins = 91, shards = 12, xp = 7, level = 2, bestFloor = 4, abyssFloor = 3,
                runCoins = 30, runShards = 4, runXp = 14, abyssSnapshotFloor = 3, boons = new[] { 2, 1, 3 },
                abyssEnemies = new[] { new EnemyRecord { id = 17, kind = 0, x = 15, y = 17, hp = 23, maxHp = 52 } },
                flags = new List<string> { "intro", "core_1", "watch_complete", "archive_complete", "abyss_clear_2" },
                sparringReturn = new SparringReturnRecord { x = 11, y = 15, health = 63, energy = 28, heat = 22, cold = 12 } };
            var returning = new GameCore(data, crossRoom); returning.Start();
            Check(returning.save.room == 0 && returning.save.sparringReturn == null && !returning.SparringActive &&
                returning.save.abyssFloor == 0 && returning.save.runCoins == 0 && returning.save.runShards == 0 && returning.save.runXp == 0 &&
                returning.save.abyssSnapshotFloor == 0 && returning.save.abyssEnemies == null && returning.enemies.Count == 0 &&
                returning.save.boons[0] == 0 && returning.save.boons[1] == 0 && returning.save.boons[2] == 0,
                "A mismatched abyss save with a training return body cannot leave active floor loot, survivors or temporary boons in town");
            Check(returning.save.coins == 91 && returning.save.shards == 12 && returning.save.xp == 7 && returning.save.level == 2 &&
                returning.save.bestFloor == 4 && returning.save.stage == 3 && returning.save.frontierEnding == 1 &&
                returning.Has("core_1") && returning.Has("watch_complete") && returning.Has("archive_complete") && returning.Has("abyss_clear_2"),
                "Abandoning only the incompatible active expedition preserves banked economy, prior best floor and mature story flags");
            int[] ids = { 75000, 75010, 75020, 75021 };
            var records = new List<EnemyRecord>();
            foreach (int id in ids) records.Add(new EnemyRecord { id = id, kind = 0, boss = true, x = 15, y = 17, hp = 23, maxHp = 70 });
            records.Add(new EnemyRecord { id = 17, kind = 0, x = 15, y = 17, hp = 23, maxHp = 52 });
            var ordinary = new GameCore(data, new SaveData { room = 1, stage = 1, x = 14, y = 15,
                snapshotRoom = 1, flags = new List<string> { "intro", "core_1" }, worldEnemies = records.ToArray() }); ordinary.Start();
            Check(ordinary.enemies.Count == 1 && ordinary.enemies[0].id == 17 && ordinary.enemies[0].hp == 23,
                "Reserved training identities can never become campaign Bosses or ordinary restored actors while real world records remain intact");
            var abyss = new GameCore(data, new SaveData { room = 8, stage = 3, frontierEnding = 1, x = 4, y = 14,
                abyssFloor = 1, abyssSnapshotFloor = 1, abyssEnemies = records.ToArray(),
                flags = new List<string> { "intro", "core_1", "exp_intro", "harbor_report", "desert_report", "mountain_report" } }); abyss.Start();
            Check(abyss.InAbyss && abyss.enemies.Count == 1 && abyss.enemies[0].id == 17 && abyss.enemies[0].hp == 23,
                "An abyss save also rejects reserved sparring actors without discarding a valid floor survivor");
        }

        public static void SparringEveryStarterClassClearsMixedWavesAndRewardsOnce(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++)
            {
                var g = SparringGame(data, profession);
                int coins = g.save.coins, points = g.save.skillPoints;
                SaveData baseline = CloneSave(g.save);
                Check(g.StartSparring(), "Every starter profession can use the mentor's sparring yard");
                for (int stage = 0; stage < 3; stage++)
                {
                    Check(g.SparringStage == stage && g.enemies.Count == (stage == 2 ? 2 : 1),
                        "The authored lessons are one hunter, one heavy, then a mixed pair");
                    foreach (Enemy e in g.enemies)
                        Check(!e.boss && e.id >= 75000 + stage * 10 && e.id < 75000 + stage * 10 + 2 &&
                            e.timer > 0 && e.timer < 3 && !g.Blocked(e.x - .22f, e.y - .18f) && !g.Blocked(e.x + .22f, e.y + .18f),
                            "Reserved practice opponents have bounded live AI clocks and collision-safe footing");
                    Check(stage == 0 ? g.enemies[0].kind == 0 : stage == 1 ? g.enemies[0].kind == 2 :
                        g.enemies.Exists(e => e.kind == 0) && g.enemies.Exists(e => e.kind == 1),
                        "The opponent roles teach the normal hunter, armored heavy and archer matchups");
                    Check(!g.ContinueSparring(), "A live lesson cannot skip its opponents");
                    SparringClearStage(g);
                    SparringUnchangedEconomy(g, baseline, stage == 2 ? 25 : 0, stage == 2 ? 1 : 0);
                    if (stage < 2)
                    {
                        int clearedStage = g.SparringStage;
                        Advance(g, 2);
                        Check(g.SparringStage == clearedStage && g.SparringAwaitingNext && g.enemies.Count == 0,
                            "Elapsed time and lingering effects cannot automatically spawn the next wave");
                        Check(g.ContinueSparring(), "The player explicitly accepts each next lesson");
                    }
                }
                Check(g.SparringFinished && g.Has("sparring_complete") && g.save.coins == coins + 25 &&
                    g.save.skillPoints == points + 1 && !g.ContinueSparring(),
                    "Completing all three real fights grants the authored first-clear reward once");
                g.EndPractice();
                WorldObject mentor = Object(g, "mentor"); g.save.x = mentor.x; g.save.y = mentor.y + 1;
                SparringClearAll(g); g.EndPractice();
                Check(g.save.coins == coins + 25 && g.save.skillPoints == points + 1 && !g.Has("practice_reward") && g.save.practiceBits == 0,
                    "Replaying the full sparring chain cannot farm rewards or complete the original dummy lessons");
            }
        }
    }
}
