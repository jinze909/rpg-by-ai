using System;
using System.Collections.Generic;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static SaveData riverLessonEarned;
        private static SaveData RiverSave(SaveData source)
        {
            SaveData copy = CloneSave(source);
            if (source.moments != null)
            {
                copy.moments = new List<JournalMoment>();
                foreach (JournalMoment moment in source.moments)
                    copy.moments.Add(moment == null ? null : new JournalMoment { zh = moment.zh, en = moment.en, day = moment.day });
            }
            return copy;
        }
        private static WorldObject RiverStand(GameCore g, string kind)
        {
            g.Close(); WorldObject source = Object(g, kind);
            foreach (Point offset in new[] { new Point(0, .7f), new Point(0, -.7f), new Point(-.7f, 0), new Point(.7f, 0) })
            {
                Point p = new Point(source.x, source.y) + offset;
                if (!g.CanStand(p)) continue;
                g.save.x = p.x; g.save.y = p.y;
                if (ReferenceEquals(g.Nearest(), source)) return source;
            }
            throw new Exception("No legal canonical river-lesson approach: " + kind);
        }
        private static void RiverVisit(GameCore g, string kind) { RiverStand(g, kind); g.Interact(); }
        private static int RiverChoice(GameCore g, string action)
        {
            Check(g.mode == "dialog" && g.conversation != null, "River-lesson choices originate in the actual conversation");
            int index = Array.FindIndex(g.conversation.choices, c => c.action == action);
            Check(index >= 0, "Missing river-lesson conversation action: " + action); return index;
        }
        private static void RiverChoose(GameCore g, string action) { g.Choose(RiverChoice(g, action)); }
        private static bool RiverRecorded(GameCore g, int site) { return g.RiverLessonRecorded(site); }
        private static void RiverLakeCatch(GameCore g)
        {
            RiverVisit(g, "fish"); RiverChoose(g, "fish");
            Check(g.mode == "fish" && !g.RiverFishingActive, "The old town lake still starts its original fishing mode");
            for (int pull = 0; pull < 3; pull++)
            {
                int frames = 0;
                do { g.Tick(.02f, new Point()); frames++; }
                while ((Math.Abs(g.fishCursor - .5f) > .08f || frames < 5) && frames < 1000);
                Check(frames < 1000, "The original lake marker reaches its existing green band through Tick"); g.FishHit();
            }
            Check(g.mode == "world", "The old lake finishes after its original three good pulls");
        }
        private static GameCore RiverGame(Campaign data, bool accepted = false)
        {
            if (riverLessonEarned == null)
            {
                var original = Fresh(data); original.SkipTutorial();
                for (int catchIndex = 0; catchIndex < 3; catchIndex++) RiverLakeCatch(original);
                RiverVisit(original, "fish"); RiverChoose(original, "ally2");
                Check(original.Has("ally_2") && original.save.inventory[3] == 0,
                    "The lesson fixture earns Old Tide's original three-fish commission through nine real pulls");
                original.Close(); original.save.flags.Add("core_1"); original.PrepareSave(); riverLessonEarned = RiverSave(original.save);
            }
            var g = new GameCore(data, RiverSave(riverLessonEarned)); g.Start(); g.Close(); g.enemies.Clear();
            Check(g.RiverLessonReady && !g.RiverLessonAccepted && !g.RiverLessonComplete, "An earned original commission and core unlock an unaccepted lesson");
            if (accepted)
            {
                RiverVisit(g, "fish"); RiverChoose(g, "river_offer"); RiverChoose(g, "river_accept");
                Check(g.RiverLessonAccepted && g.save.trackedQuest == "riverlesson", "Acceptance tracks the appended lesson");
            }
            return g;
        }
        private static void RiverEnter(GameCore g)
        { g.Enter(GameCore.ReachRoom); Check(g.save.room == 11, "The earned riverbank route remains available"); g.enemies.Clear(); g.projectiles.Clear(); g.hazards.Clear(); }
        private static void RiverBegin(GameCore g, int site)
        {
            RiverStand(g, "riverFishing" + site);
            Check(g.StartRiverFishing(site) && g.mode == "fish" && g.RiverFishingActive && g.RiverFishingSite == site && g.RiverPhase == RiverFishingPhase.Waiting,
                "The actual owned river marker begins a fresh waiting phase at site " + site);
            Check(g.fishHits == 0 && g.fishMisses == 0 && g.fishCursor == 0 && Math.Abs(g.RiverPhaseRemaining - 1) < .0001f,
                "A new cast has zero pulls and the full one-second wait");
        }
        private static void RiverAwaitReeling(GameCore g)
        {
            for (int frame = 0; frame < 400 && g.RiverFishingActive && g.RiverPhase != RiverFishingPhase.Reeling; frame++) g.Tick(.01f, new Point());
            Check(g.RiverFishingActive && g.RiverPhase == RiverFishingPhase.Reeling, "Actual waiting/rest frames open the next reeling window");
        }
        private static void RiverAdvanceMarker(GameCore g, float target)
        {
            RiverAwaitReeling(g);
            for (int frame = 0; frame < 400 && g.fishCursor < target; frame++)
            {
                float delta = Math.Min(.01f, (target - g.fishCursor) * 1.6f);
                if (delta < .000000001f) break;
                g.Tick(delta, new Point());
            }
            Check(g.RiverPhase == RiverFishingPhase.Reeling && Math.Abs(g.fishCursor - target) < .000002f,
                "Production Tick advances the river marker to the chosen pull position without writing its cursor");
        }
        private static void RiverGoodPull(GameCore g)
        {
            int good = g.fishHits, misses = g.fishMisses; RiverAdvanceMarker(g, g.RiverFishingTarget); g.Interact();
            Check(g.fishHits == good + 1 && g.fishMisses == misses, "An actual E in the river band records exactly one good pull");
            if (good < 2)
            {
                Check(g.RiverFishingActive && g.RiverPhase == RiverFishingPhase.Resting && Math.Abs(g.RiverPhaseRemaining - .55f) < .0001f,
                    "A pull opens the full short resting phase");
                g.FishHit(); g.Interact();
                Check(g.fishHits == good + 1 && g.fishMisses == misses, "Duplicate inputs in the same consumed window cannot add pulls");
            }
        }
        private static void RiverCatch(GameCore g, int site)
        {
            RiverBegin(g, site); g.FishHit(); g.Interact();
            Check(g.fishHits == 0 && g.fishMisses == 0, "Early waiting inputs are ignored without penalizing the player");
            for (int pull = 0; pull < 3; pull++) RiverGoodPull(g);
            Check(!g.RiverFishingActive && g.mode == "world" && RiverRecorded(g, site), "Three actual good pulls catch and record site " + site);
        }
        private static void RiverBothSites(GameCore g)
        { RiverEnter(g); RiverCatch(g, 0); RiverCatch(g, 1); }

        public static void RiverLessonOriginalCommissionAndLakeChoicesRemain(Campaign data)
        {
            var g = Fresh(data); g.SkipTutorial(); RiverVisit(g, "fish");
            Check(g.conversation.choices.Length == 3 && g.conversation.choices[0].action == "fish" && g.conversation.choices[1].action == "ally2" && g.conversation.choices[2].action == "close",
                "The unearned town fisher retains the exact original cast, three-fish commission and leave choice prefix");
            Check(!g.RiverLessonReady && !g.RiverLessonAccepted && g.GetQuest("riverlesson").locked, "The optional lesson cannot replace the first commission");
            g.Close(); g.AcceptBounty(2); int coins = g.save.coins, fish = g.save.inventory[3]; RiverLakeCatch(g);
            Check(g.save.inventory[3] == fish + 1 && g.save.coins == coins + 4 && g.save.bountyProgress == 1 && !RiverRecorded(g, 0) && !RiverRecorded(g, 1),
                "The old lake pays one fish, four coins and the existing fishing-bounty event without inventing river notes");
            RiverLakeCatch(g); RiverLakeCatch(g); RiverVisit(g, "fish"); RiverChoose(g, "ally2");
            Check(g.Has("ally_2") && g.save.inventory[3] == 0 && !g.RiverLessonReady, "Three actual original catches still pay the mature commission, and core one is still required");
            g.save.flags.Add("core_1"); RiverVisit(g, "fish");
            Check(g.conversation.choices.Length >= 3 && g.conversation.choices[0].action == "fish" && g.conversation.choices[1].action == "close",
                "The earned lesson is appended after the mature cast and leave actions");
            SaveData before = RiverSave(g.save); RiverChoose(g, "river_offer"); RiverChoose(g, "river_accept");
            Check(g.RiverLessonAccepted, "An earned local fisherman can accept the separate lesson through his real choices");
            Check(g.save.coins == before.coins && g.save.inventory[3] == before.inventory[3] && g.save.inventory[6] == before.inventory[6] && g.save.skillPoints == before.skillPoints && g.save.stage == before.stage,
                "Accepting the lesson does not repeat the original commission reward or consume fish");
        }

        public static void RiverLessonLockedForeignStaleAndUnsafeActionsAreRejected(Campaign data)
        {
            foreach (string flag in new[] { "", "core_1", "ally_2" })
            {
                var locked = Fresh(data); locked.SkipTutorial(); if (flag != "") locked.save.flags.Add(flag); RiverStand(locked, "fish");
                Check(!locked.AcceptRiverLesson() && !locked.FinishRiverLesson() && !locked.RiverLessonAccepted && !locked.RiverLessonComplete,
                    "Neither a missing original commission nor a missing starcore can be bypassed: " + flag);
            }
            var remote = RiverGame(data); remote.save.x = 18; remote.save.y = 14;
            Check(!remote.AcceptRiverLesson(), "A remote direct acceptance cannot act as the town fisher");
            RiverVisit(remote, "fish"); RiverChoose(remote, "river_offer"); int stale = RiverChoice(remote, "river_accept");
            remote.save.x = 18; remote.save.y = 14; remote.Choose(stale);
            Check(!remote.RiverLessonAccepted, "A stale real fisher menu rechecks locality when its accept choice is selected");
            foreach (string boundary in new[] { "menu", "practice", "health", "nan", "wall", "enemy", "projectile", "hazard", "canonical" })
            {
                var g = RiverGame(data); WorldObject source = RiverStand(g, "fish"); WorldObject[] authored = g.Map.objects;
                if (boundary == "menu") g.mode = "inventory";
                if (boundary == "practice") g.practicing = true;
                if (boundary == "health") g.save.health = 0;
                if (boundary == "nan") g.save.x = float.NaN;
                if (boundary == "wall") { g.save.x = 0; g.save.y = 0; }
                if (boundary == "enemy") g.enemies.Add(new Enemy(999, 0, g.save.x, g.save.y, 20));
                if (boundary == "projectile") g.projectiles.Add(new Projectile { position = g.Position, friendly = false });
                if (boundary == "hazard") g.hazards.Add(new Hazard { position = g.Position, timer = 1, radius = 1 });
                try
                {
                    if (boundary == "canonical") g.Map.objects = Array.ConvertAll(authored, o => ReferenceEquals(o, source) ? new WorldObject { id = "foreign-fisher", kind = "fish", x = o.x, y = o.y } : o);
                    Check(!g.AcceptRiverLesson() && !g.RiverLessonAccepted, "Acceptance rechecks actual canonical, living, quiet local world: " + boundary);
                }
                finally { g.Map.objects = authored; }
            }
            var river = RiverGame(data, true); RiverEnter(river); RiverStand(river, "riverFishing0");
            Check(!river.StartRiverFishing(-1) && !river.StartRiverFishing(2) && !river.StartRiverFishing(1), "Invalid sites and the other bank cannot reuse this marker's ownership");
            RiverVisit(river, "riverFishing0"); int staleCast = RiverChoice(river, "river_cast_0");
            river.save.x = river.Map.spawnX; river.save.y = river.Map.spawnY; river.Choose(staleCast);
            Check(!river.RiverFishingActive && !RiverRecorded(river, 0), "A stale real river marker choice cannot begin a cast after leaving that bank");
            foreach (string boundary in new[] { "remote", "enemy", "projectile", "hazard", "stock", "locked", "canonical" })
            {
                var g = RiverGame(data, true); RiverEnter(g); WorldObject source = RiverStand(g, "riverFishing0"); WorldObject[] authored = g.Map.objects;
                if (boundary == "remote") { g.save.x = g.Map.spawnX; g.save.y = g.Map.spawnY; }
                if (boundary == "enemy") g.enemies.Add(new Enemy(999, 0, g.save.x, g.save.y, 20));
                if (boundary == "projectile") g.projectiles.Add(new Projectile { position = g.Position, friendly = false });
                if (boundary == "hazard") g.hazards.Add(new Hazard { position = g.Position, timer = 1, radius = 1 });
                if (boundary == "stock") g.save.inventory[3] = 9999;
                if (boundary == "locked") g.save.flags.Remove("riverlesson_accept");
                try
                {
                    if (boundary == "canonical") g.Map.objects = Array.ConvertAll(authored, o => ReferenceEquals(o, source) ? new WorldObject { id = "foreign-site", kind = "riverFishing0", x = o.x, y = o.y } : o);
                    Check(!g.StartRiverFishing(0) && !g.RiverFishingActive && !RiverRecorded(g, 0), "Starting a cast rechecks actual owned safe site and stock: " + boundary);
                }
                finally { g.Map.objects = authored; }
            }
        }

        public static void RiverLessonBothBanksUseActualPhasesTimeoutsAndSinglePulls(Campaign data)
        {
            var g = RiverGame(data, true); g.AcceptBounty(2); RiverEnter(g); int fish = g.save.inventory[3], coins = g.save.coins;
            Check(GameCore.RiverFishingWaitSeconds == 1 && GameCore.RiverFishingReelSeconds == 1.6f && GameCore.RiverFishingRestSeconds == .55f && GameCore.RiverFishingHalfWidth == .14f,
                "The displayed lesson timing and inclusive green-band width match the production constants");
            float worldTime = g.save.totalTime, health = g.save.health, energy = g.save.energy;
            for (int site = 0; site < 2; site++)
            {
                RiverCatch(g, site);
                Check(g.save.inventory[3] == fish + site + 1 && g.save.coins == coins + (site + 1) * 4 && g.save.bountyProgress == site + 1 && !g.RiverLessonComplete,
                    "Each bank needs its own three phased pulls and pays one ordinary catch before the separate report");
            }
            Check(g.save.totalTime == worldTime && g.save.health == health && g.save.energy == energy,
                "Waiting, reeling and resting use their own clock while the existing fishing mode pauses world time and vitals");
            var timeout = RiverGame(data, true); RiverEnter(timeout); RiverBegin(timeout, 0); SaveData before = RiverSave(timeout.save);
            for (int miss = 1; miss <= 3; miss++)
            {
                RiverAwaitReeling(timeout); Advance(timeout, 1.7f);
                Check(timeout.fishMisses == miss && timeout.fishHits == 0, "A fully expired reeling window records exactly one miss: " + miss);
                if (miss < 3) { timeout.FishHit(); timeout.Interact(); Check(timeout.fishMisses == miss, "Inputs after timeout cannot duplicate its miss"); }
            }
            Check(!timeout.RiverFishingActive && timeout.mode == "world" && !RiverRecorded(timeout, 0) && TacticsState(timeout.save.inventory) == TacticsState(before.inventory) && timeout.save.coins == before.coins,
                "Three timeouts release the fish with no lost supplies, coins or fabricated note");
            foreach (float edge in new[] { -.14f, .14f })
            {
                var boundary = RiverGame(data, true); RiverEnter(boundary); RiverBegin(boundary, 0);
                RiverAdvanceMarker(boundary, boundary.RiverFishingTarget + edge); boundary.FishHit();
                Check(boundary.fishHits == 1 && boundary.fishMisses == 0, "The actual reeling band includes its boundary at offset " + edge);
            }
            var outside = RiverGame(data, true); RiverEnter(outside); RiverBegin(outside, 1);
            Check(Math.Abs(outside.RiverFishingTarget - .65f) < .0001f, "The second bank has its own .65 target rather than the first bank's .45");
            RiverAdvanceMarker(outside, outside.RiverFishingTarget - .15f); outside.FishHit(); outside.FishHit();
            Check(outside.fishHits == 0 && outside.fishMisses == 1 && outside.RiverPhase == RiverFishingPhase.Resting,
                "A pull outside the inclusive band misses once and cannot be repeated in rest");
            var clock = RiverGame(data, true); RiverEnter(clock); RiverBegin(clock, 0); float remaining = clock.RiverPhaseRemaining;
            foreach (float dt in new[] { 0f, -1f, float.NaN, float.PositiveInfinity }) clock.Tick(dt, new Point());
            Check(clock.RiverPhaseRemaining == remaining && !clock.RiverCanReel, "Invalid frame times cannot progress or open a pull window");
            clock.Tick(5, new Point()); Check(Math.Abs(clock.RiverPhaseRemaining - remaining + .05f) < .0001f,
                "Long fishing frames obey the existing production .05 second simulation clamp");
        }

        public static void RiverLessonReportIsLocalAtomicCappedAndPaidOnlyOnce(Campaign data)
        {
            var g = RiverGame(data, true); RiverEnter(g); RiverCatch(g, 0); g.Enter(0); g.enemies.Clear(); RiverStand(g, "fish");
            Check(!g.FinishRiverLesson() && !g.RiverLessonComplete, "A single genuine bank note cannot complete the two-bank report");
            RiverEnter(g); RiverCatch(g, 1); g.Enter(0); g.enemies.Clear(); RiverStand(g, "fish");
            SaveData reportable = RiverSave(g.save);
            RiverVisit(g, "fish"); RiverChoose(g, "river_offer"); int stale = RiverChoice(g, "river_finish"); g.save.x = 18; g.save.y = 14; g.Choose(stale);
            Check(!g.RiverLessonComplete, "An earned but stale real report choice cannot hand in notes remotely");
            foreach (string boundary in new[] { "menu", "practice", "health", "enemy", "projectile", "hazard", "canonical" })
            {
                var invalid = new GameCore(data, RiverSave(reportable)); invalid.Start(); invalid.Close(); invalid.enemies.Clear(); WorldObject source = RiverStand(invalid, "fish");
                WorldObject[] authored = invalid.Map.objects;
                if (boundary == "menu") invalid.mode = "inventory";
                if (boundary == "practice") invalid.practicing = true;
                if (boundary == "health") invalid.save.health = 0;
                if (boundary == "enemy") invalid.enemies.Add(new Enemy(999, 0, invalid.save.x, invalid.save.y, 20));
                if (boundary == "projectile") invalid.projectiles.Add(new Projectile { position = invalid.Position, friendly = false });
                if (boundary == "hazard") invalid.hazards.Add(new Hazard { position = invalid.Position, radius = 1, timer = 1 });
                string unpaid = TacticsState(invalid.save);
                try
                {
                    if (boundary == "canonical") invalid.Map.objects = Array.FindAll(authored, o => !ReferenceEquals(o, source));
                    Check(!invalid.FinishRiverLesson() && !invalid.RiverLessonComplete && TacticsState(invalid.save) == unpaid,
                        "The real report rechecks local living safe canonical world atomically: " + boundary);
                }
                finally { invalid.Map.objects = authored; }
            }
            RiverStand(g, "fish");
            g.save.inventory[6] = 9998; g.save.reputation[2] = 9; string full = TacticsState(g.save);
            Check(!g.FinishRiverLesson() && !g.RiverLessonComplete && TacticsState(g.save) == full,
                "A pantry that cannot hold both stews refuses the entire report without consuming its once-only flag or trust");
            g.save.inventory[6] = 9997; g.save.inventory[3] = 0; SaveData before = RiverSave(g.save);
            Check(g.FinishRiverLesson() && g.RiverLessonComplete && g.GetQuest("riverlesson").complete && g.save.inventory[6] == 9999 && g.save.reputation[2] == 10 && g.save.inventory[3] == 0,
                "The real town handoff awards exactly two bounded stews and one capped Wilds trust, without a fish payment");
            Check(g.save.coins == before.coins && g.save.xp == before.xp && g.save.skillPoints == before.skillPoints && g.save.shards == before.shards && g.save.stage == before.stage,
                "Reporting the optional lesson does not repeat any combat, survey or chapter reward");
            string paid = TacticsState(g.save); Check(!g.FinishRiverLesson() && !g.AcceptRiverLesson() && TacticsState(g.save) == paid,
                "Replayed completion or acceptance cannot pay or mutate the already completed lesson");
            var capped = RiverGame(data, true); RiverEnter(capped); capped.save.coins = 999997; capped.save.inventory[3] = 9998; RiverCatch(capped, 0);
            Check(capped.save.coins == 999999 && capped.save.inventory[3] == 9999, "An ordinary river catch respects both existing currency and fish-stock caps");
            RiverStand(capped, "riverFishing0"); Check(!capped.StartRiverFishing(0), "Full fish storage cannot start an unpayable cast");
        }

        public static void RiverLessonAppendsQuietGuidanceAndKeepsReachTopology(Campaign data)
        {
            string[] old = { "main", "shore", "ally1", "ally2", "ally3", "personal0", "personal1", "personal2", "bounty", "secrets", "watch", "signal", "archive", "reach", "homeward", "commission", "shelter", "fieldcraft", "supper", "returnletter" };
            Check(GameCore.QuestIds.Length == 21 && GameCore.QuestIds[20] == "riverlesson", "The optional lesson appends as the twenty-first journal identity");
            for (int id = 0; id < old.Length; id++) Check(GameCore.QuestIds[id] == old[id], "Every mature quest retains its saved index: " + old[id]);
            var g = RiverGame(data, true); RiverEnter(g);
            Check(g.Map.rows.Length == 48 && Array.TrueForAll(g.Map.rows, row => row.Length == 72), "Both fishing sites retain the original larger Reach dimensions");
            foreach (WorldObject source in g.Map.objects)
                if (source.kind == "riverFishing0" || source.kind == "riverFishing1")
                {
                    int site = source.kind == "riverFishing0" ? 0 : 1;
                    Check(source.x == (site == 0 ? 9 : 29) && source.y == (site == 0 ? 14 : 25), "River markers use their authored unchanged-bank coordinates");
                    Check(g.NavigationPath(new Point(source.x, source.y)).Count > 0, "Each fishing bank has a real full-footprint route from the existing entrance");
                }
            Check(!g.ReachBridgeOpen && !g.ReachRootsOpen && !g.ReachComplete && !g.ReachCacheRecovered && !g.ReachPostRecorded(0),
                "Accepting the lesson cannot open bridges, clear roots or earn original survey progress");
            RiverBegin(g, 0); string state = TacticsState(g); float clock = g.RiverPhaseRemaining;
            for (int read = 0; read < 30; read++)
            {
                QuestGuide guide = g.GetQuest("riverlesson"); bool active = g.RiverFishingActive; float remaining = g.RiverPhaseRemaining, target = g.RiverFishingTarget;
                Check(guide.id == "riverlesson" && !guide.complete && !guide.locked && active && remaining == clock && target == .45f && g.RiverFishingSite == 0,
                    "Journal and HUD reads report the actual optional state without advancing a fishing phase");
            }
            Check(TacticsState(g) == state, "Repeated river journal, target, phase and active queries are completely quiet"); g.Close();
            ReachOpenRoutes(g); ReachReadPosts(g); ReachStand(g, "reachCache"); Check(g.RecoverReachCache(), "The original rooted cache remains its own local transaction");
            ReachStand(g, "reachBoard"); int coins = g.save.coins, shards = g.save.shards, points = g.save.skillPoints;
            Check(g.ClaimReachSurvey() && g.save.coins == coins + 40 && g.save.shards == shards + 4 && g.save.skillPoints == points + 1 && !RiverRecorded(g, 0) && !RiverRecorded(g, 1),
                "The original three-lamp survey retains its exact separate payout and cannot fabricate river catches");
        }

        public static void RiverLessonLegacyCheckpointsAndLiveOwnershipNeverMintCatch(Campaign data)
        {
            var partial = RiverGame(data, true); RiverEnter(partial); RiverCatch(partial, 0); partial.PrepareSave(); SaveData checkpoint = RiverSave(partial.save);
            foreach (int version in new[] { 1, 2 })
            {
                var unaccepted = new GameCore(data, new SaveData { version = version, coins = 83, flags = new List<string> { "intro", "core_1", "ally_2" } });
                unaccepted.Start(); unaccepted.Close();
                Check(unaccepted.RiverLessonReady && !unaccepted.RiverLessonAccepted && !unaccepted.RiverLessonComplete && !RiverRecorded(unaccepted, 0) && !RiverRecorded(unaccepted, 1) && !unaccepted.RiverFishingActive && unaccepted.save.coins == 83,
                    "A supported old save with both prerequisites does not invent acceptance, bank notes, catch or rewards: v" + version);
                var old = RiverSave(checkpoint); old.version = version; var loaded = new GameCore(data, old); loaded.Start(); loaded.Close(); loaded.enemies.Clear();
                Check(loaded.RiverLessonAccepted && RiverRecorded(loaded, 0) && !RiverRecorded(loaded, 1) && !loaded.RiverLessonComplete && !loaded.RiverFishingActive && loaded.save.inventory[3] == checkpoint.inventory[3],
                    "Supported legacy version preserves the genuine partial note without a live cast or free fish: v" + version);
                RiverCatch(loaded, 1); loaded.Enter(0); loaded.enemies.Clear(); RiverStand(loaded, "fish"); Check(loaded.FinishRiverLesson(), "A partial legacy lesson can be completed deliberately");
                loaded.PrepareSave(); SaveData paid = RiverSave(loaded.save); var resumed = new GameCore(data, RiverSave(paid)); resumed.Start(); resumed.Close(); RiverStand(resumed, "fish");
                Check(resumed.RiverLessonComplete && !resumed.FinishRiverLesson() && resumed.save.inventory[6] == paid.inventory[6] && resumed.save.reputation[2] == paid.reputation[2],
                    "Completed legacy checkpoints retain the paid record without repeating rewards: v" + version);
            }
            foreach (string boundary in new[] { "close", "menu", "travel", "load", "new", "foreignsave", "removed", "replaced", "moved", "remote", "health", "practice", "enemy", "projectile", "hazard", "stock", "lostaccept" })
            {
                var g = RiverGame(data, true); RiverEnter(g); RiverBegin(g, 0); RiverGoodPull(g); RiverGoodPull(g); RiverAwaitReeling(g); RiverAdvanceMarker(g, .45f);
                int fish = g.save.inventory[3], coins = g.save.coins; WorldObject source = Object(g, "riverFishing0"); WorldObject[] authored = g.Map.objects; float oldX = source.x;
                try
                {
                    if (boundary == "close") g.Close();
                    if (boundary == "menu") g.mode = "inventory";
                    if (boundary == "travel") { g.Enter(0); g.Enter(11); g.enemies.Clear(); }
                    if (boundary == "load") { g.PrepareSave(); g.Load(RiverSave(g.save)); g.Start(); }
                    if (boundary == "new") { g.NewGame(2); g.Close(); fish = g.save.inventory[3]; coins = g.save.coins; }
                    if (boundary == "foreignsave") g.save = RiverSave(g.save);
                    if (boundary == "removed") g.Map.objects = Array.FindAll(authored, o => !ReferenceEquals(o, source));
                    if (boundary == "replaced") g.Map.objects = Array.ConvertAll(authored, o => ReferenceEquals(o, source) ? new WorldObject { id = o.id, kind = o.kind, x = o.x, y = o.y } : o);
                    if (boundary == "moved") source.x += .25f;
                    if (boundary == "remote") { g.save.x = g.Map.spawnX; g.save.y = g.Map.spawnY; }
                    if (boundary == "health") g.save.health = 0;
                    if (boundary == "practice") g.practicing = true;
                    if (boundary == "enemy") g.enemies.Add(new Enemy(999, 0, g.save.x, g.save.y, 20));
                    if (boundary == "projectile") g.projectiles.Add(new Projectile { position = g.Position, friendly = false });
                    if (boundary == "hazard") g.hazards.Add(new Hazard { position = g.Position, radius = 1, timer = 1 });
                    if (boundary == "stock") g.save.inventory[3] = 9999;
                    if (boundary == "lostaccept") g.save.flags.Remove("riverlesson_accept");
                    g.FishHit(); g.Tick(.02f, new Point());
                    Check(!g.RiverFishingActive && !RiverRecorded(g, 0) && g.save.inventory[3] == (boundary == "stock" ? 9999 : fish) && g.save.coins == coins,
                        "The captured source, world generation and SaveData owner must survive until a real catch: " + boundary);
                }
                finally { source.x = oldX; data.maps[11].objects = authored; }
            }
            foreach (RiverFishingPhase phase in new[] { RiverFishingPhase.Waiting, RiverFishingPhase.Reeling, RiverFishingPhase.Resting })
            {
                var g = RiverGame(data, true); RiverEnter(g); RiverBegin(g, 1);
                if (phase == RiverFishingPhase.Reeling) RiverAwaitReeling(g);
                if (phase == RiverFishingPhase.Resting) RiverGoodPull(g);
                int fish = g.save.inventory[3], good = g.fishHits, misses = g.fishMisses; float remaining = g.RiverPhaseRemaining, cursor = g.fishCursor;
                g.PrepareSave();
                Check(g.RiverFishingActive && g.RiverPhase == phase && g.RiverPhaseRemaining == remaining && g.fishHits == good && g.fishMisses == misses && g.fishCursor == cursor,
                    "The actual autosave snapshot does not interrupt the live unpersisted cast in phase " + phase);
                var loaded = new GameCore(data, RiverSave(g.save)); loaded.Start(); loaded.Close(); loaded.enemies.Clear();
                Check(!loaded.RiverFishingActive && !RiverRecorded(loaded, 1) && loaded.save.inventory[3] == fish, "Loading never resumes a phase or pays a catch from phase " + phase);
                while (g.RiverFishingActive) RiverGoodPull(g);
                Check(RiverRecorded(g, 1) && g.save.inventory[3] == fish + 1 && !RiverRecorded(loaded, 1) && loaded.save.inventory[3] == fish,
                    "Only the original live owner can finish its actual remaining pulls after autosave: " + phase);
            }
            foreach (FieldInfo field in typeof(SaveData).GetFields())
                Check(field.Name.IndexOf("riverlesson", StringComparison.OrdinalIgnoreCase) < 0 && field.Name.IndexOf("riverfish", StringComparison.OrdinalIgnoreCase) < 0,
                    "River lesson and live phases use additive flags and transient Core state without expanding the legacy SaveData schema");
        }
    }
}
