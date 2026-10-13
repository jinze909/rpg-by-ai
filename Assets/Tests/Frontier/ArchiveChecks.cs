using System;
using System.Collections.Generic;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static void ArchiveChoose(GameCore g, string action)
        {
            Check(g.mode == "dialog" && g.conversation != null, "Archive choices belong to an actual conversation");
            int index = Array.FindIndex(g.conversation.choices, c => c.action == action);
            Check(index >= 0, "Missing archive conversation choice " + action); g.Choose(index);
        }
        private static WorldObject ArchiveVisit(GameCore g, string kind)
        {
            WorldObject o = Object(g, kind); g.save.x = o.x; g.save.y = o.y - .7f;
            Check(g.CanStand(g.Position), "The archive object has a real standing approach: " + kind);
            g.Interact(); return o;
        }
        private static GameCore ArchiveGame(Campaign data, int profession = 0)
        {
            var g = Fresh(data, profession); g.SkipTutorial(); g.save.stage = 2;
            foreach (string flag in new[] { "core_1", "core_2", "scout_saved", "boss_1", "boss_2", "gate_2" }) g.save.flags.Add(flag);
            g.Enter(2); g.enemies.Clear(); ArchiveVisit(g, "portal10"); ArchiveChoose(g, "archive_enter");
            Check(g.save.room == GameCore.ArchiveRoom && g.ArchiveAccepted && g.save.trackedQuest == "archive",
                "The real ruins entrance accepts and tracks the optional archive investigation");
            Check(g.enemies.Count == 0, "The new dungeon begins with an exploration puzzle rather than unrelated chapter enemies");
            return g;
        }
        private static void ArchiveSetValve(GameCore g, int valve, int rotation)
        {
            WorldObject o = Object(g, "archiveValve" + valve); g.save.x = o.x; g.save.y = o.y - .7f;
            Check(g.CanStand(g.Position), "A valve's standing approach has full foot clearance");
            for (int turn = 0; turn < 4 && g.ArchiveValve(valve) != rotation; turn++)
                Check(g.RotateArchiveValve(valve), "A nearby unfinished valve rotates through its real action");
            Check(g.ArchiveValve(valve) == rotation, "The chosen valve reaches its requested orientation");
        }
        private static void ArchiveDrain(GameCore g)
        {
            ArchiveVisit(g, "archivePlan"); g.Close();
            ArchiveSetValve(g, 0, 2); ArchiveSetValve(g, 1, 0); ArchiveSetValve(g, 2, 3);
            g.Tick(.02f, new Point());
            Check(g.ArchiveDrained, "The source, three connected valves and outlet drain the sealed first chamber");
        }
        private static void ArchiveVent(GameCore g)
        {
            ArchiveDrain(g); WorldObject vent = Object(g, "archiveVent"); g.save.x = vent.x; g.save.y = vent.y - .7f;
            Check(g.CanStand(g.Position) && g.VentArchive() && g.ArchiveVented,
                "The far gallery control releases pressure and opens the record vault through its actual action");
        }
        private static void ArchiveReadAll(GameCore g)
        {
            for (int record = 0; record < 3; record++)
            {
                ArchiveVisit(g, "archiveRecord" + record); g.Close();
                Check(g.Has("archive_record_" + record) && g.Has("history_" + (17 + record)),
                    "Reading a real archive record preserves both the investigation and the existing history codex");
            }
        }
        private static bool ArchiveNearReachable(GameCore g, HashSet<int> reachable, Point target)
        {
            int width = g.Map.rows[0].Length * 4;
            foreach (int cell in reachable)
                if ((new Point((cell % width + .5f) / 4, (cell / width + .5f) / 4) - target).Length <= 1.5f) return true;
            return false;
        }
        private static void ArchiveRoute(GameCore g, Point target)
        {
            var path = g.NavigationPath(target);
            Check(path.Count > 0 && (path[path.Count - 1] - target).Length <= 1.75f,
                "The actual guidance route reaches an archive interaction approach");
            Point before = g.Position;
            foreach (Point p in path)
            {
                int steps = Math.Max(1, (int)Math.Ceiling((p - before).Length / .1f));
                for (int step = 1; step <= steps; step++)
                    Check(g.CanStand(before + (p - before) * (step / (float)steps)),
                        "Every guidance segment, not just its waypoints, respects the complete hero footprint");
                before = p;
            }
        }
        private static Hazard ArchiveAwaitPulse(GameCore g)
        {
            for (int frame = 0; frame < 250; frame++)
            {
                Hazard warning = g.hazards.Find(h => h.kind == "archiveTide");
                if (warning != null) return warning;
                g.Tick(.02f, new Point());
            }
            throw new Exception("The drained, unvented gallery must produce its readable pressure warning");
        }
        public static void ArchiveEntryRequiresEarnedPowerAndPreservesRuins(Campaign data)
        {
            var locked = Fresh(data); locked.SkipTutorial(); locked.save.stage = 1; locked.Enter(2); locked.enemies.Clear();
            int stage = locked.save.stage, coins = locked.save.coins; ArchiveVisit(locked, "portal10");
            Check(!locked.ArchiveReady && !locked.ArchiveAccepted && locked.GetQuest("archive").locked,
                "Visiting the entrance before returning the second core cannot accept the optional dungeon");
            Check(locked.conversation == null || Array.FindIndex(locked.conversation.choices, c => c.action == "archive_enter") < 0,
                "A locked archive does not offer a usable travel transaction");
            locked.Close(); locked.Enter(GameCore.ArchiveRoom);
            Check(locked.save.room == 2 && locked.save.stage == stage && locked.save.coins == coins,
                "Direct travel also respects the earned-core requirement and costs no mature progress");
            var g = ArchiveGame(data);
            Check(g.ArchiveReady && !g.ArchiveComplete && !g.ArchiveDrained && !g.ArchiveVented && g.save.stage == 2 &&
                g.Has("core_2") && g.Has("boss_2") && g.Has("gate_2") && !g.Has("core_3") && !g.Has("boss_10"),
                "Entering the new branch preserves the existing ruins resolution without chapter or Boss credit");
            Check(g.Map.id == 10 && g.CanStand(g.Position) && (g.Position - new Point(4, 18)).Length < .001f,
                "The appended map starts at its own valid entrance");
            ArchiveVisit(g, "portal2");
            Check(g.save.room == 2 && g.ArchiveAccepted && g.save.stage == 2,
                "The actual west exit returns to the same ruins while retaining the accepted investigation");
        }
        public static void ArchiveValvePuzzleIsLocalCostFreeAndPersistent(Campaign data)
        {
            var g = ArchiveGame(data); int coins = g.save.coins; int[] inventory = (int[])g.save.inventory.Clone();
            Check(!g.RotateArchiveValve(-1) && !g.RotateArchiveValve(3) && !g.RotateArchiveValve(0) && !g.VentArchive(),
                "Invalid indices and remote actions cannot rotate the network or release distant pressure");
            ArchiveSetValve(g, 0, 1); ArchiveSetValve(g, 1, 1); ArchiveSetValve(g, 2, 1);
            Check(!g.ArchiveDrained && !g.ArchiveVented, "A disconnected network never grants either chamber checkpoint");
            WorldObject valve = Object(g, "archiveValve0"); g.save.x = valve.x; g.save.y = valve.y - .7f;
            foreach (string mode in new[] { "pause", "inventory", "journal", "dialog" })
            {
                g.mode = mode; int before = g.ArchiveValve(0);
                Check(!g.RotateArchiveValve(0) && g.ArchiveValve(0) == before, "Menus cannot change a world valve");
            }
            g.Close(); g.PrepareSave(); var loaded = new GameCore(data, CloneSave(g.save)); loaded.Start();
            for (int i = 0; i < 3; i++) Check(loaded.ArchiveValve(i) == 1, "Every unfinished valve orientation survives a save");
            ArchiveDrain(loaded); Check(loaded.Has("archive_plan"), "The existing history interaction records the network plan");
            for (int i = 0; i < 3; i++)
            {
                WorldObject completed = Object(loaded, "archiveValve" + i); loaded.save.x = completed.x; loaded.save.y = completed.y - .7f;
                int before = loaded.ArchiveValve(i); Check(!loaded.RotateArchiveValve(i) && loaded.ArchiveValve(i) == before,
                    "A drained network stays solved instead of allowing a valve to trap the player behind its reopened gate");
            }
            Check(loaded.save.coins == coins, "All experimental rotations and the solved network are cost free");
            for (int i = 0; i < inventory.Length; i++) Check(loaded.save.inventory[i] == inventory[i], "Valve experiments do not spend or mint supplies");
            loaded.PrepareSave(); var solved = new GameCore(data, CloneSave(loaded.save)); solved.Start();
            Check(solved.ArchiveDrained && !solved.ArchiveVented && !solved.ArchiveComplete,
                "Loading the drained checkpoint cannot invent the later pressure, records or reward stages");
        }
        public static void ArchiveAllStagesHaveFootprintSafeRoutesAndAlcoves(Campaign data)
        {
            var g = ArchiveGame(data);
            for (int stage = 0; stage < 3; stage++)
            {
                g.save.x = g.Map.spawnX; g.save.y = g.Map.spawnY; HashSet<int> reached = ArenaReachable(g);
                foreach (WorldObject o in g.Map.objects)
                {
                    bool first = o.kind == "portal2" || o.kind == "archivePlan" || o.kind.StartsWith("archiveValve");
                    bool gallery = o.kind == "archiveVent";
                    bool vault = o.kind.StartsWith("archiveRecord") || o.kind == "archiveKeeper";
                    if (first || stage >= 1 && gallery || stage >= 2 && vault)
                    {
                        Point target = new Point(o.x, o.y);
                        Check(ArchiveNearReachable(g, reached, target), "Quarter-tile traversal reaches the correct stage object " + o.kind);
                        ArchiveRoute(g, target);
                    }
                    else if (gallery || vault)
                        Check(!ArchiveNearReachable(g, reached, new Point(o.x, o.y)), "Unfinished chamber geometry prevents skipping to " + o.kind);
                }
                if (stage >= 1)
                    foreach (Point safe in new[] { new Point(15.5f, 9.5f), new Point(21, 11.5f) })
                        Check(g.CanStand(safe) && ArchiveNearReachable(g, reached, safe), "The timed gallery has reachable safe ground beside both pulse lanes");
                if (stage == 0) ArchiveDrain(g);
                if (stage == 1)
                {
                    WorldObject vent = Object(g, "archiveVent"); g.save.x = vent.x; g.save.y = vent.y - .7f;
                    Check(g.VentArchive(), "The routed far pressure valve opens the final chamber");
                }
            }
        }
        public static void ArchiveChamberSealsRejectDodgeAttackAndPowerBypass(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++) for (int gate = 0; gate < 2; gate++)
            {
                var g = ArchiveGame(data, profession); if (gate == 1) ArchiveDrain(g);
                Point approach = gate == 0 ? new Point(8.6f, 13.5f) : new Point(22.6f, 6.5f);
                float edge = gate == 0 ? 9 : 23;
                Check(g.CanStand(approach) && g.Blocked(edge + .1f, approach.y), "Each closed archive seal has a safe approach and actual collision");
                foreach (int action in new[] { 0, 1, 2, 3 })
                {
                    g.save.x = approach.x; g.save.y = approach.y; g.dashCooldown = g.attackCooldown = g.skillCooldown = 0;
                    g.save.energy = 100; g.playerAction = null; g.invulnerable = 100;
                    if (action == 0) g.Dash(new Point(1, 0));
                    if (action == 1) g.Attack(new Point(1, 0));
                    if (action == 2) g.Skill(new Point(1, 0));
                    if (action == 3) g.UseExplorationAbility();
                    for (int frame = 0; frame < 50; frame++) g.Tick(.02f, new Point(1, 0));
                    Check(g.CanStand(g.Position) && g.Position.x <= edge - .219f && !g.ArchiveVented && (gate != 0 || !g.ArchiveDrained),
                        "Walking, swept dodge, weapon root motion, Q and R cannot bypass an unfinished dungeon chamber, class " + profession + " action " + action);
                }
                g.save.x = edge + .5f; g.save.y = approach.y; g.PrepareSave();
                var recovered = new GameCore(data, CloneSave(g.save)); recovered.Start();
                Check(recovered.CanStand(recovered.Position) && recovered.Position.x < 9 && !recovered.ArchiveVented && (gate != 0 || !recovered.ArchiveDrained),
                    "A saved position inside a closed seal recovers to the safe entrance without granting its puzzle checkpoint");
            }
        }
        public static void ArchiveGuardsAndStoryCompleteWithEveryStarterClass(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++)
            {
                var g = ArchiveGame(data, profession); ArchiveVent(g); int originalStage = g.save.stage;
                Check(!g.ArchiveGuardsCleared && !g.ArchiveComplete && g.enemies.Count == 0,
                    "The final chamber still requires recovering the authored records before its defence begins");
                for (int record = 0; record < 2; record++)
                { ArchiveVisit(g, "archiveRecord" + record); g.Close(); Check(g.enemies.Count == 0, "An incomplete record collection does not spawn duplicate guards"); }
                ArchiveVisit(g, "archiveRecord2"); g.Close();
                Check(g.Has("archive_wave") && g.enemies.Count == 3, "The last recovered record starts one mixed three-enemy defence");
                var ids = new HashSet<int>();
                foreach (Enemy e in g.enemies)
                    Check(ids.Add(e.id) && e.id >= 74000 && e.id <= 74002 && !e.boss && g.CanStand(e.Position),
                        "Every archive guard has its unique reserved ID, normal enemy role and valid footing");
                int coinsBeforeFight = g.save.coins, kills = g.save.kills[10], mastery = g.save.mastery[g.save.weapon];
                g.save.bounty = 0; g.save.bountyProgress = 0; Combat(g, false);
                Check(!g.Dying && g.ArchiveGuardsCleared && g.save.kills[10] == kills + 3 && g.save.coins == coinsBeforeFight + 18 &&
                    g.save.mastery[g.save.weapon] >= mastery + 6 && g.save.bountyProgress == 3 && !g.Has("boss_10"),
                    "Actual attacks, techniques and dodges defeat the archive guards with ordinary loot, mastery and kill-contract credit");
                int coins = g.save.coins, shards = g.save.shards, points = g.save.skillPoints;
                ArchiveVisit(g, "archiveKeeper"); ArchiveChoose(g, profession == 1 ? "archive_family" : "archive_public");
                Check(g.ArchiveComplete && g.GetQuest("archive").complete && g.save.coins == coins + 50 &&
                    g.save.shards == shards + 6 && g.save.skillPoints == points + 1,
                    "The actual record-keeper choice completes the story with fifty coins, six shards and one point once");
                Check(g.Has(profession == 1 ? "archive_family" : "archive_public") && !g.Has(profession == 1 ? "archive_public" : "archive_family") &&
                    g.save.stage == originalStage && g.Has("core_2") && g.Has("boss_2") && !g.Has("core_3") && !g.Has("boss_10"),
                    "The saved records choice is exclusive and leaves mature chapter progress intact");
                g.PrepareSave(); g = new GameCore(data, CloneSave(g.save)); g.Start(); ArchiveVisit(g, "archiveKeeper");
                Check(g.conversation == null || Array.FindIndex(g.conversation.choices, c => c.action == "archive_public" || c.action == "archive_family") < 0,
                    "A saved completed archive cannot offer a second story reward or overwrite the first choice");
                g.Close(); ArchiveReadAll(g);
                Check(g.enemies.Count == 0 && g.save.coins == coins + 50 && g.save.shards == shards + 6 && g.save.skillPoints == points + 1,
                    "Rereading completed records cannot respawn the defence or duplicate payment");
            }
        }
        public static void ArchiveGuardSnapshotsRetainHealthAndCannotFarm(Campaign data)
        {
            var g = ArchiveGame(data); ArchiveVent(g); ArchiveReadAll(g);
            Enemy first = g.enemies.Find(e => e.id == 74000), survivor = g.enemies.Find(e => e.id == 74001);
            Check(first != null && survivor != null, "The archive defence uses its stable authored identities");
            foreach (Enemy e in g.enemies) e.timer = 9999;
            g.save.x = first.x - 1; g.save.y = first.y; first.hp = 1;
            for (int frame = 0; frame < 100 && g.enemies.Contains(first); frame++) { g.Tick(.02f, new Point()); g.Attack(first.Position - g.Position); }
            Check(!g.enemies.Contains(first) && g.Has("archive_defeated_74000"), "A lethal weapon contact persists only that archive guard's defeat");
            survivor.hp -= 3; float hp = survivor.hp; int coins = g.save.coins, kills = g.save.kills[10]; g.PrepareSave();
            var loaded = new GameCore(data, CloneSave(g.save)); loaded.Start(); Enemy resumed = loaded.enemies.Find(e => e.id == 74001);
            Check(resumed != null && Math.Abs(resumed.hp - hp) < .001f && resumed.timer > 0 && resumed.timer <= 2 && !loaded.enemies.Exists(e => e.id == 74000),
                "A same-room save preserves surviving health, bounds the reserved-ID attack clock and respects actual defeat");
            loaded.Enter(2); loaded.Enter(10);
            Check(loaded.enemies.Count == 2 && !loaded.enemies.Exists(e => e.id == 74000) && !loaded.ArchiveGuardsCleared &&
                loaded.save.coins == coins && loaded.save.kills[10] == kills && !loaded.ArchiveComplete,
                "Travel reconstructs living defence members without farming loot or treating an empty old room as completion");
            var empty = CloneSave(loaded.save); empty.snapshotRoom = 10; empty.worldEnemies = new EnemyRecord[0];
            var rebuilt = new GameCore(data, empty); rebuilt.Start();
            Check(rebuilt.enemies.Count == 2 && !rebuilt.enemies.Exists(e => e.id == 74000) && !rebuilt.ArchiveGuardsCleared,
                "An old empty snapshot cannot silently erase the active archive defence");
        }
        public static void ArchivePressureWarningsMatchDamageAndSafeAlcoves(Campaign data)
        {
            var g = ArchiveGame(data); ArchiveDrain(g); g.save.reducedMotion = true;
            Advance(g, 5);
            Check(!g.hazards.Exists(h => h.kind == "archiveTide"), "The drained entrance chamber remains safe before the hero enters the gallery");
            g.save.x = 18.5f; g.save.y = 12.5f; g.invulnerable = 0;
            Hazard first = ArchiveAwaitPulse(g); float health = g.save.health;
            Check(Math.Abs(first.timer - 1.2f) < .001f && Math.Abs(first.radius - 1.35f) < .001f && first.damage == 14 &&
                (first.position - g.Position).Length < .001f && GameCore.HazardTheme(first) == 5,
                "The first pressure warning has its full preparation, water theme, actual damage radius and lower-lane contact");
            float timer = first.timer, worldTime = g.save.totalTime; g.mode = "pause"; Advance(g, 2);
            Check(first.timer == timer && g.save.totalTime == worldTime && g.save.health == health,
                "Pausing freezes the active warning and the dungeon's world clock without damaging the hero");
            g.Close(); Advance(g, 1.1f);
            Check(g.hazards.Contains(first) && g.save.health == health && first.timer > 0,
                "Standing inside the pressure circle remains safe throughout its visible preparation");
            Advance(g, .15f);
            Check(!g.hazards.Contains(first) && g.save.health < health && !g.Dying,
                "A missed warning resolves once through ordinary area damage rather than hidden pre-warning contact");
            float damaged = g.save.health; g.save.x = 21; g.save.y = 11.5f;
            Hazard second = ArchiveAwaitPulse(g);
            Check((second.position - new Point(18.5f, 8.5f)).Length < .001f && second.timer > 1.1f,
                "The next fully prepared warning alternates to the upper pressure lane");
            Advance(g, 1.3f);
            Check(g.save.health == damaged && !g.hazards.Contains(second), "The eastern alcove stays outside both actual pressure circles");
            g.save.x = 15.5f; g.save.y = 9.5f; Hazard third = ArchiveAwaitPulse(g);
            Check((third.position - new Point(18.5f, 12.5f)).Length < .001f, "The pressure cycle returns to the lower lane instead of spawning overlapping circles");
            Advance(g, 1.3f); Check(g.save.health == damaged, "The western alcove offers a second genuinely safe waiting position");
            g.save.x = 21; g.save.y = 6; Hazard active = ArchiveAwaitPulse(g);
            var unrelated = new Hazard { kind = "glass", position = new Point(30, 20), radius = .1f, damage = 1, timer = 100 };
            g.hazards.Add(unrelated);
            Check(g.VentArchive() && !g.hazards.Contains(active) && g.hazards.Contains(unrelated),
                "The far wheel permanently releases archive pressure while preserving unrelated hazards");
            Advance(g, 8); Check(!g.hazards.Exists(h => h.kind == "archiveTide") && g.save.health == damaged,
                "The released gallery remains quiet across later pressure cycles");
            var pending = ArchiveGame(data); ArchiveDrain(pending); pending.save.x = 18.5f; pending.save.y = 12.5f;
            Hazard beforeSave = ArchiveAwaitPulse(pending); Advance(pending, .8f); pending.PrepareSave();
            var resumed = new GameCore(data, CloneSave(pending.save)); resumed.Start();
            Check(!resumed.hazards.Exists(h => h.kind == "archiveTide") && !resumed.ArchiveVented,
                "An unfinished warning is transient and loading never fabricates the permanent pressure milestone");
            Hazard resumedWarning = ArchiveAwaitPulse(resumed);
            Check(resumedWarning.timer > 1.1f && (resumedWarning.position - beforeSave.position).Length < .001f,
                "Returning from a saved pressure warning restarts the first lane with full readable preparation");
        }
        public static void ArchiveGuidanceFollowsActualPuzzleAndDefenceProgress(Campaign data)
        {
            string[] original = { "main", "shore", "ally1", "ally2", "ally3", "personal0", "personal1", "personal2", "bounty", "secrets", "watch", "signal" };
            Check(GameCore.QuestIds.Length == 13 && GameCore.QuestIds[12] == "archive", "The archive quest appends a thirteenth entry rather than replacing an existing story");
            for (int i = 0; i < original.Length; i++) Check(GameCore.QuestIds[i] == original[i], "All original guide identifiers retain their exact order");
            var g = Fresh(data); g.SkipTutorial(); g.save.stage = 2; g.save.flags.Add("core_2"); g.TrackQuest("archive");
            QuestGuide q = g.CurrentGuide; Point? target = g.NavigationTarget();
            Check(q.id == "archive" && !q.locked && !q.complete && q.room == 2 && (q.target - new Point(10, 14)).Length < .001f &&
                target.HasValue && (target.Value - new Point(3, 14)).Length < .001f,
                "An unaccepted archive guides through the real Town-to-Ruins route rather than sending the hero to a distant-shores envoy");
            g.Enter(2); g.enemies.Clear(); target = g.NavigationTarget();
            Check(target.HasValue && (target.Value - new Point(10, 14)).Length < .001f, "The ruins guide reaches the actual appended archive entrance");
            ArchiveVisit(g, "portal10"); ArchiveChoose(g, "archive_enter"); q = g.CurrentGuide;
            Check(q.room == 10 && (q.target - new Point(4, 8)).Length < .001f && q.Progress == 0,
                "The first accepted objective introduces the real network plan rather than an unreachable later chamber");
            ArchiveVisit(g, "archivePlan"); g.Close(); q = g.CurrentGuide;
            Check((q.target - new Point(7, 6)).Length < .001f, "Reading the plan points to the first disconnected elbow valve");
            ArchiveSetValve(g, 0, 2); q = g.CurrentGuide;
            Check((q.target - new Point(10, 10)).Length < .001f, "The solved first valve and already-correct middle valve are skipped in favour of the unfinished final elbow");
            ArchiveSetValve(g, 2, 3); q = g.CurrentGuide;
            Check(g.ArchiveDrained && (q.target - new Point(21, 6)).Length < .001f && q.Progress > 0,
                "A solved real circuit advances the gold route to the far pressure wheel");
            g.save.x = 21; g.save.y = 5.3f; Check(g.VentArchive(), "The pressure stage completes through its local wheel");
            for (int record = 0; record < 3; record++)
            {
                WorldObject next = Object(g, "archiveRecord" + record); q = g.CurrentGuide;
                Check(q.room == 10 && (q.target - new Point(next.x, next.y)).Length < .001f,
                    "The gold route points to each still-unread record rather than a previously recovered page");
                ArchiveVisit(g, next.kind); g.Close();
            }
            q = g.CurrentGuide; Check(g.enemies.Exists(e => (e.Position - q.target).Length < .001f) && !q.complete,
                "Recovering all records guides to an actual living defender and cannot prematurely complete the story");
            Combat(g, false); q = g.CurrentGuide;
            Check(g.ArchiveGuardsCleared && (q.target - new Point(31, 19)).Length < .001f && !q.complete,
                "Clearing the real defence guides to the final archive desk and its unresolved choice");
            g.TrackQuest("main"); target = g.NavigationTarget();
            Check(target.HasValue && (target.Value - new Point(3, 18)).Length < .001f,
                "Tracking the mature story from inside the archive uses the actual exit back to Ruins");
            g.TrackQuest("archive"); ArchiveVisit(g, "archiveKeeper"); ArchiveChoose(g, "archive_public");
            Check(g.CurrentGuide.complete && g.CurrentGuide.Progress == 1 && !g.NavigationTarget().HasValue,
                "The saved final choice completes the guide and removes its route instead of retaining a misleading unfinished target");
        }
        public static void ArchiveLocalChoicesAndDeathRecoveryPreserveProgress(Campaign data)
        {
            var entry = Fresh(data); entry.SkipTutorial(); entry.save.stage = 2; entry.save.flags.Add("core_2"); entry.Enter(2); entry.enemies.Clear();
            ArchiveVisit(entry, "portal10"); entry.save.x = 18; entry.save.y = 18; ArchiveChoose(entry, "archive_enter");
            Check(entry.save.room == 2 && !entry.ArchiveAccepted, "A stale entrance conversation cannot travel after the hero moves away from its real host");
            var g = ArchiveGame(data); WorldObject valve = Object(g, "archiveValve0");
            g.save.x = valve.x; g.save.y = valve.y - .7f; g.Interact(); int rotation = g.ArchiveValve(0);
            g.save.x = 4; g.save.y = 18; ArchiveChoose(g, "archive_rotate_0");
            Check(g.ArchiveValve(0) == rotation && !g.ArchiveDrained, "A stale valve conversation cannot remotely change the waterway");
            g.save.x = 27; g.save.y = 4.3f; g.Talk(Object(g, "archiveRecord0"));
            Check(!g.Has("archive_record_0") && !g.Has("history_17") && !g.Has("archive_wave"),
                "A record reached through malformed position data still requires releasing the vault gate");
            g.Close(); ArchiveVent(g); int coins = g.save.coins, shards = g.save.shards, points = g.save.skillPoints;
            ArchiveVisit(g, "archiveKeeper");
            Check(Array.FindIndex(g.conversation.choices, c => c.action == "archive_public" || c.action == "archive_family") < 0,
                "The archive desk does not offer payment before records and actual defenders are complete");
            g.Close(); g.ChooseArchive("archive_public");
            Check(!g.ArchiveComplete && g.save.coins == coins && g.save.shards == shards && g.save.skillPoints == points,
                "A direct premature archive action also cannot fabricate story completion or rewards");
            ArchiveReadAll(g); Enemy defeated = g.enemies.Find(e => e.id == 74000);
            foreach (Enemy e in g.enemies) e.timer = 9999;
            g.save.x = defeated.x - 1; g.save.y = defeated.y; defeated.hp = 1;
            for (int frame = 0; frame < 100 && g.enemies.Contains(defeated); frame++) { g.Tick(.02f, new Point()); g.Attack(defeated.Position - g.Position); }
            Check(g.Has("archive_defeated_74000") && g.enemies.Count == 2, "The defence has one actual completed death before player recovery is tested");
            int kills = g.save.kills[10]; coins = g.save.coins; g.save.health = 1; g.invulnerable = 0; g.playerAction = null;
            g.projectiles.Add(new Projectile { position = g.Position, damage = 999, source = "shot" }); Advance(g, .15f);
            Check(g.Dying && !g.ArchiveComplete && !g.ArchiveGuardsCleared, "Lethal contact cannot quietly finish an incomplete record defence");
            g.PrepareSave(); var recovered = new GameCore(data, CloneSave(g.save)); recovered.Start();
            Check(recovered.save.room == 0 && recovered.save.coins == Math.Max(0, coins - 10) && recovered.save.kills[10] == kills &&
                recovered.ArchiveAccepted && recovered.ArchiveDrained && recovered.ArchiveVented && recovered.Has("archive_defeated_74000") &&
                !recovered.ArchiveComplete && recovered.save.shards == shards && recovered.save.skillPoints == points,
                "Saving during the fallen pose retains puzzle checkpoints and actual defender deaths without adding a reward or applying recovery twice");
            recovered.Enter(2); recovered.Enter(10);
            Check(recovered.enemies.Count == 2 && !recovered.enemies.Exists(e => e.id == 74000) && !recovered.ArchiveGuardsCleared,
                "Returning after death reconstructs only the unresolved defence and cannot farm the defeated member");
            Combat(recovered, false); ArchiveVisit(recovered, "archiveKeeper"); coins = recovered.save.coins;
            recovered.save.x = 27; recovered.save.y = 17; ArchiveChoose(recovered, "archive_family");
            Check(!recovered.ArchiveComplete && recovered.save.coins == coins && recovered.save.shards == shards && recovered.save.skillPoints == points,
                "A completed defence still requires local delivery at the desk; a stale final-choice dialogue cannot claim from a distance");
        }
        public static void ArchiveReservedActorSnapshotsRespectOwnership(Campaign data)
        {
            var g = ArchiveGame(data); ArchiveVent(g); ArchiveReadAll(g); g.PrepareSave();
            var malformed = CloneSave(g.save);
            malformed.worldEnemies = new[] {
                new EnemyRecord { id = 74000, kind = 3, boss = true, x = 27, y = 11, hp = 100, maxHp = 300 },
                new EnemyRecord { id = 74001, kind = 2, x = 31, y = 8, hp = 40, maxHp = 40 },
                new EnemyRecord { id = 74002, kind = 2, x = 30, y = 15, hp = float.NaN, maxHp = 70 },
                new EnemyRecord { id = 99, kind = 3, boss = true, x = 29, y = 10, hp = 300, maxHp = 300 },
                new EnemyRecord { id = 98, kind = 0, x = 28, y = 10, hp = 30, maxHp = 30 }
            };
            var restored = new GameCore(data, malformed); restored.Start();
            Check(restored.enemies.Count == 3 && !restored.Has("boss_10") && !restored.ArchiveGuardsCleared,
                "Malformed optional identities and unrelated chapter actors cannot enter the authored archive defence or create completion");
            foreach (Enemy e in restored.enemies)
                Check(restored.IsArchiveEnemy(e) && !e.boss && e.hp > 0 && e.timer > 0 && e.timer <= 2,
                    "Each rejected snapshot is replaced only by its genuine living guard with a bounded readable attack clock");
            var inactive = CloneSave(g.save); inactive.flags.Remove("archive_wave");
            var notStarted = new GameCore(data, inactive); notStarted.Start();
            Check(notStarted.enemies.Count == 0 && !notStarted.Has("archive_wave") && !notStarted.ArchiveGuardsCleared,
                "Saved actor records cannot start an unaccepted defence when its authored wave flag is absent");
            var complete = CloneSave(g.save); complete.flags.Add("archive_complete"); complete.flags.Add("archive_family");
            var finished = new GameCore(data, complete); finished.Start();
            Check(finished.enemies.Count == 0 && finished.ArchiveComplete && !finished.Has("archive_public"),
                "A completed local branch cannot restore reserved actors or replace the saved family-verification choice");
            var outside = CloneSave(g.save); outside.room = outside.snapshotRoom = 1; outside.x = 20; outside.y = 9;
            outside.flags.Remove("boss_1"); outside.worldEnemies = new[] {
                new EnemyRecord { id = 41, kind = 0, x = 20, y = 9, hp = 23, maxHp = 30 },
                new EnemyRecord { id = 74000, kind = 0, x = 21, y = 9, hp = 23, maxHp = 45 }
            };
            var forest = new GameCore(data, outside); forest.Start();
            Check(forest.enemies.Count == 1 && forest.enemies[0].id == 41 && forest.enemies[0].hp == 23,
                "Archive-reserved actors cannot appear in a mature map, while its original ordinary snapshot retains exact identity and health");
            forest.save.x = 28; forest.save.y = 10; AwaitArenaOpening(forest); Action(forest, "shrine", "boss");
            Enemy guardian = forest.enemies.Find(e => e.boss);
            Check(guardian != null && guardian.id == 42 && !forest.IsArchiveEnemyId(guardian.id) && !GameCore.IsSignalWarden(guardian),
                "Rejected optional IDs cannot inflate the original chapter guardian's ordinary actor allocator");
        }
        public static void ArchiveLegacySavesGrowMapCountersWithoutInventingStory(Campaign data)
        {
            foreach (int version in new[] { 1, 2 })
            {
                var saved = new SaveData { version = version, stage = 2, room = 9, x = 11, y = 16, coins = 113,
                    kills = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, explored = new[] { 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 },
                    flags = new List<string> { "intro", "core_1", "core_2", "boss_1", "boss_2", "gate_2", "watch_complete", "signal_complete", "history_16" } };
                var g = new GameCore(data, saved); g.Start();
                Check(g.save.room == 9 && g.save.coins == 113 && g.ArchiveReady && !g.ArchiveAccepted && !g.ArchiveDrained && !g.ArchiveVented && !g.ArchiveComplete,
                    "Legacy saves retain the inn and mature progress while leaving the optional archive unaccepted");
                Check(g.save.version == 2 && g.save.kills.Length == data.maps.Length && g.save.explored.Length == data.maps.Length &&
                    g.save.kills[10] == 0 && g.save.explored[10] == 0, "Map counters append a clean archive slot while the version-two save schema remains valid");
                for (int i = 0; i < 10; i++) Check(g.save.kills[i] == i + 1 && g.save.explored[i] == i + 11,
                    "All original map counters preserve their exact prefixes during migration");
                Check(g.Has("watch_complete") && g.Has("signal_complete") && g.Has("history_16") && g.enemies.Count == 0,
                    "Old patrol, optional Boss, codex and safe inn state survive the added dungeon");
            }
            var malformed = new SaveData { room = 10, x = float.NaN, y = float.PositiveInfinity, kills = null, explored = null,
                flags = new List<string> { "intro", "core_2", "archive_accept", "archive_valve_0_99", null } };
            var recovered = new GameCore(data, malformed); recovered.Start();
            Check(recovered.save.room == 10 && recovered.CanStand(recovered.Position) && recovered.ArchiveValve(0) >= 0 && recovered.ArchiveValve(0) <= 3 &&
                !recovered.ArchiveDrained && !recovered.ArchiveVented && !recovered.ArchiveComplete,
                "Malformed positions, missing map arrays and unrelated flags recover without fabricating dungeon milestones");
        }
    }
}
