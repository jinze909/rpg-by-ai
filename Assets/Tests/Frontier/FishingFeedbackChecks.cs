using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore FishingFeedbackLake(Campaign data)
        {
            var game = Fresh(data); game.SkipTutorial();
            FishingFeedbackLakeCast(game); return game;
        }
        private static void FishingFeedbackLakeCast(GameCore game)
        {
            RiverVisit(game, "fish"); RiverChoose(game, "fish");
            Check(game.mode == "fish" && !game.RiverFishingActive && game.RecentFishingPull == null
                && game.FishingPullFeedbackRemaining == 0 && game.fishHits == 0 && game.fishMisses == 0,
                "A real lake cast starts its existing mode and counters with no old pull receipt");
        }
        private static void FishingFeedbackLakeGood(GameCore game)
        {
            int frames = 0;
            do { game.Tick(.01f, new Point()); frames++; }
            while ((Math.Abs(game.fishCursor - .5f) > .08f || frames < 5) && frames < 1000);
            Check(frames < 1000, "The production lake sine clock reaches its original success band");
            game.FishHit();
        }
        private static FishingPullFeedback FishingFeedbackReceipt(GameCore game, FishingPullOutcome outcome,
            float cursor, float lower, float upper, int good, int misses, bool river)
        {
            FishingPullFeedback receipt = game.RecentFishingPull;
            Check(receipt != null && receipt.outcome == outcome && receipt.cursor == cursor
                && Math.Abs(receipt.lower - lower) < .000002f && Math.Abs(receipt.upper - upper) < .000002f
                && receipt.goodPulls == good && receipt.misses == misses && receipt.river == river,
                "The accepted " + outcome + " pull snapshots its actual cursor, original band, updated counts and water kind");
            Check(game.FishingPullFeedbackRemaining > 0 && game.FishingPullFeedbackRemaining <= GameCore.FishingPullFeedbackDuration,
                "An accepted pull has a positive finite feedback lifetime");
            return receipt;
        }
        private static void FishingFeedbackAbsent(GameCore game, string reason)
        { Check(game.RecentFishingPull == null && game.FishingPullFeedbackRemaining == 0, reason); }

        public static void FishingFeedbackLakePullsUseActualClockAndPreserveInclusiveBand(Campaign data)
        {
            var game = FishingFeedbackLake(data);
            float cursor = game.fishCursor; game.FishHit();
            FishingPullFeedback left = FishingFeedbackReceipt(game, FishingPullOutcome.Left, cursor, .32f, .68f, 0, 1, false);
            Check(game.FishingPullFeedbackRemaining == 1.2f && game.sound == "hurt", "A lake miss opens the complete receipt lifetime and keeps its existing sound");
            game.Tick(.05f, new Point()); cursor = game.fishCursor;
            Check(cursor > .32f && cursor < .68f, "A real first lake frame moves from the left into the green band");
            game.Interact();
            FishingPullFeedback good = FishingFeedbackReceipt(game, FishingPullOutcome.Good, cursor, .32f, .68f, 1, 1, false);
            Check(!ReferenceEquals(left, good) && game.FishingPullFeedbackRemaining == 1.2f && game.sound == "reward",
                "The next accepted lake pull replaces the old receipt and restarts only its short feedback clock");
            for (int frame = 0; frame < 100 && game.fishCursor <= .7f; frame++) game.Tick(.01f, new Point());
            Check(game.fishCursor > .7f, "The production lake marker reaches the right side after its speed changes with a good pull");
            cursor = game.fishCursor; game.FishHit();
            FishingFeedbackReceipt(game, FishingPullOutcome.Right, cursor, .32f, .68f, 1, 2, false);
            Check(left.outcome == FishingPullOutcome.Left && left.cursor == 0 && left.goodPulls == 0 && left.misses == 1
                && good.outcome == FishingPullOutcome.Good && good.cursor != game.fishCursor && good.goodPulls == 1 && good.misses == 1,
                "Previously returned receipts remain immutable as later pulls and the live cursor advance");

            foreach (float side in new[] { -1f, 1f }) foreach (float outside in new[] { 0f, .00001f })
            {
                var boundary = FishingFeedbackLake(data); boundary.Tick(.02f, new Point());
                // Probe the exact original float comparator after a real cast/frame;
                // normal lake pulls above and every river probe use the real clock.
                boundary.fishCursor = .5f + side * (.18f + outside); cursor = boundary.fishCursor;
                boundary.FishHit();
                FishingPullOutcome expected = outside == 0 ? FishingPullOutcome.Good : side < 0 ? FishingPullOutcome.Left : FishingPullOutcome.Right;
                FishingFeedbackReceipt(boundary, expected, cursor, .32f, .68f, outside == 0 ? 1 : 0, outside == 0 ? 0 : 1, false);
                Check(boundary.fishHits == (outside == 0 ? 1 : 0) && boundary.fishMisses == (outside == 0 ? 0 : 1),
                    "The original lake includes both exact .18 edges and excludes values just beyond them");
            }
        }

        public static void FishingFeedbackRiverPullsDistinguishBothSidesAndExpiredWindows(Campaign data)
        {
            for (int site = 0; site < 2; site++)
            {
                var game = RiverGame(data, true); RiverEnter(game); RiverBegin(game, site);
                game.FishHit(); game.Interact(); FishingFeedbackAbsent(game, "Waiting inputs cannot invent a river pull receipt");
                float target = game.RiverFishingTarget, lower = target - .14f, upper = target + .14f;
                RiverAdvanceMarker(game, target - .15f); float cursor = game.fishCursor; game.Interact();
                FishingPullFeedback left = FishingFeedbackReceipt(game, FishingPullOutcome.Left, cursor, lower, upper, 0, 1, true);
                game.Tick(.04f, new Point()); float remaining = game.FishingPullFeedbackRemaining;
                game.FishHit(); game.Interact();
                Check(ReferenceEquals(left, game.RecentFishingPull) && game.FishingPullFeedbackRemaining == remaining && game.fishMisses == 1,
                    "Ignored resting inputs neither replace the accepted river miss nor refresh its feedback clock");
                Advance(game, .52f); remaining = game.FishingPullFeedbackRemaining;
                Check(game.RiverPhase == RiverFishingPhase.Waiting && remaining > 0,
                    "The next real waiting phase begins while the previous pull receipt is still briefly visible");
                game.FishHit(); game.Interact();
                Check(ReferenceEquals(left, game.RecentFishingPull) && game.FishingPullFeedbackRemaining == remaining && game.fishMisses == 1,
                    "Ignored waiting inputs preserve the prior accepted receipt and its remaining lifetime");
                RiverAdvanceMarker(game, target + .15f); cursor = game.fishCursor; game.FishHit();
                FishingFeedbackReceipt(game, FishingPullOutcome.Right, cursor, lower, upper, 0, 2, true);
                Check(left.outcome == FishingPullOutcome.Left && left.cursor < lower && left.misses == 1,
                    "A captured left-side river receipt does not change when the next window misses on the right");

                var timeout = RiverGame(data, true); RiverEnter(timeout); RiverBegin(timeout, site); RiverAwaitReeling(timeout);
                for (int frame = 0; frame < 200 && timeout.fishMisses == 0; frame++) timeout.Tick(.01f, new Point());
                Check(timeout.fishMisses == 1 && timeout.RiverPhase == RiverFishingPhase.Resting,
                    "An actual unpulled production reeling window expires exactly once into rest");
                FishingPullFeedback expired = FishingFeedbackReceipt(timeout, FishingPullOutcome.Timeout, timeout.fishCursor, lower, upper, 0, 1, true);
                Check(expired.cursor == 1 && timeout.FishingPullFeedbackRemaining == GameCore.FishingPullFeedbackDuration,
                    "Timeout is distinct from pulling right and snapshots the expired end of the river cursor");
                timeout.FishHit(); timeout.Interact();
                Check(ReferenceEquals(expired, timeout.RecentFishingPull) && timeout.fishMisses == 1,
                    "Inputs after automatic expiry cannot turn one timeout into another pull");

                foreach (float edge in new[] { -.14f, .14f })
                {
                    var boundary = RiverGame(data, true); RiverEnter(boundary); RiverBegin(boundary, site);
                    RiverAdvanceMarker(boundary, boundary.RiverFishingTarget + edge); cursor = boundary.fishCursor; boundary.FishHit();
                    FishingFeedbackReceipt(boundary, FishingPullOutcome.Good, cursor, lower, upper, 1, 0, true);
                    Check(boundary.fishHits == 1 && boundary.fishMisses == 0,
                        "Both bank-specific inclusive band edges still succeed through the actual river clock");
                }
            }
        }

        public static void FishingFeedbackReadsExpiryAndOwnershipStayTransient(Campaign data)
        {
            Check(GameCore.FishingPullFeedbackDuration == 1.2f && typeof(FishingPullFeedback).IsSealed,
                "The feedback API exposes the short original-design lifetime and a sealed snapshot");
            foreach (FieldInfo field in typeof(FishingPullFeedback).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Check(field.IsInitOnly, "Every feedback snapshot field is immutable: " + field.Name);
            foreach (FieldInfo field in typeof(SaveData).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Check(field.FieldType != typeof(FishingPullFeedback) && field.Name.IndexOf("fishingpull", StringComparison.OrdinalIgnoreCase) < 0,
                    "Pull feedback introduces no durable SaveData field");
            var game = FishingFeedbackLake(data); game.FishHit(); FishingPullFeedback receipt = game.RecentFishingPull;
            string before = TacticsState(game); float worldTime = game.save.totalTime, health = game.save.health, energy = game.save.energy;
            for (int read = 0; read < 30; read++)
                Check(ReferenceEquals(receipt, game.RecentFishingPull) && game.FishingPullFeedbackRemaining == 1.2f,
                    "Reading the live receipt does not consume it, advance its clock or create another receipt");
            Check(TacticsState(game) == before, "Repeated feedback reads preserve all public/private simulation fields, save data, RNG, queues and story");
            foreach (float dt in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity }) game.Tick(dt, new Point());
            Check(TacticsState(game) == before, "Invalid fishing frame times cannot advance or expire feedback");
            game.Tick(5, new Point());
            Check(Math.Abs(game.FishingPullFeedbackRemaining - 1.15f) < .000002f && game.fishCursor != receipt.cursor,
                "The feedback clock obeys the existing .05-second frame clamp while the actual marker continues moving");
            Advance(game, 1.1f); Check(game.RecentFishingPull != null, "The receipt remains visible until its final valid fishing frames");
            Advance(game, .06f); FishingFeedbackAbsent(game, "Only valid fishing simulation frames expire a live pull receipt");
            Check(game.mode == "fish" && game.fishHits == 0 && game.fishMisses == 1 && game.save.totalTime == worldTime
                && game.save.health == health && game.save.energy == energy,
                "Receipt expiry leaves the cast, accepted counts, paused world time and vitals unchanged");

            foreach (string boundary in new[] { "close", "mode", "start", "newcast", "load", "travel", "same-room", "newgame", "owner", "room" })
            {
                var scoped = FishingFeedbackLake(data); scoped.FishHit();
                if (boundary == "close") scoped.Close();
                if (boundary == "mode") scoped.mode = "inventory";
                if (boundary == "start") scoped.Start();
                if (boundary == "newcast") FishingFeedbackLakeCast(scoped);
                if (boundary == "load") scoped.Load(RiverSave(scoped.save));
                if (boundary == "travel") scoped.Enter(1);
                if (boundary == "same-room") scoped.Enter(0);
                if (boundary == "newgame") scoped.NewGame(0);
                if (boundary == "owner") scoped.save = RiverSave(scoped.save);
                if (boundary == "room") scoped.save.room = 1;
                string invalid = TacticsState(scoped);
                FishingFeedbackAbsent(scoped, "No live pull receipt crosses its cast/mode/save/room/generation boundary: " + boundary);
                Check(TacticsState(scoped) == invalid, "Reading absent or stale feedback cannot mutate its inactive owner: " + boundary);
            }
            var river = RiverGame(data, true); RiverEnter(river); RiverBegin(river, 0); RiverAdvanceMarker(river, river.RiverFishingTarget); river.FishHit();
            river.save.x = river.Map.spawnX; river.save.y = river.Map.spawnY;
            FishingFeedbackAbsent(river, "A river receipt loses ownership when the actual local river session becomes invalid");
            river.Tick(.02f, new Point()); Check(river.mode == "world", "The next real frame retires the invalid river cast");
            FishingFeedbackAbsent(river, "Invalid river cleanup cannot leave a lake receipt behind");
            for (int version = 1; version <= 2; version++)
            {
                var source = FishingFeedbackLake(data); source.FishHit(); source.PrepareSave(); SaveData saved = RiverSave(source.save); saved.version = version;
                var loaded = new GameCore(data, saved); loaded.Start(); loaded.Close();
                FishingFeedbackAbsent(loaded, "Legacy/current save reconstruction never persists the transient pull receipt");
                Check(loaded.save.inventory[3] == 0 && loaded.save.coins == source.save.coins,
                    "Saving an unfinished pull cannot mint a catch or its four coins on load");
            }
        }

        public static void FishingFeedbackRetainsCatchRewardsBountiesAndStory(Campaign data)
        {
            var lake = FishingFeedbackLake(data); lake.AcceptBounty(2); lake.Notify("原有剧情", "Original story");
            string story = lake.toast, unpaid = TacticsState(lake.save); int revision = lake.revision, coins = lake.save.coins;
            for (int pull = 0; pull < 2; pull++)
            {
                FishingFeedbackLakeGood(lake);
                Check(lake.RecentFishingPull != null && lake.RecentFishingPull.goodPulls == pull + 1 && lake.toast == story
                    && TacticsState(lake.save) == unpaid && lake.revision == revision,
                    "Nonterminal lake feedback keeps story, inventory, bounty, RNG, coins and revision unchanged");
            }
            FishingFeedbackLakeGood(lake);
            Check(lake.mode == "world" && lake.fishHits == 3 && lake.save.inventory[3] == 1 && lake.save.coins == coins + 4
                && lake.save.bountyProgress == 1 && lake.revision == revision + 2 && lake.toast == lake.L("钓到银鳍鱼 ×1！", "Caught a Silverfin!"),
                "The third lake pull retains the original fish, four coins, single bounty event, revisions and final catch notification");
            FishingFeedbackAbsent(lake, "The completed lake catch clears its short receipt when its fishing mode closes");
            RiverLakeCatch(lake); RiverLakeCatch(lake); int paidCoins = lake.save.coins, stew = lake.save.inventory[6], points = lake.save.skillPoints;
            RiverVisit(lake, "fish"); RiverChoose(lake, "ally2");
            Check(lake.Has("ally_2") && lake.save.inventory[3] == 0 && lake.save.coins == paidCoins + 40
                && lake.save.inventory[6] == stew + 1 && lake.save.skillPoints == points + 1,
                "Three genuine catches still feed the original fisher commission with its unchanged payment and rewards");
            var missed = FishingFeedbackLake(data); unpaid = TacticsState(missed.save); missed.FishHit(); missed.FishHit(); missed.FishHit();
            Check(missed.mode == "world" && missed.fishMisses == 3 && TacticsState(missed.save) == unpaid
                && missed.toast == missed.L("鱼游走了。没有损失，再试一次吧。", "The fish slipped away. Nothing lost; try again."),
                "Three real lake misses retain the existing no-loss outcome and final notification");
            FishingFeedbackAbsent(missed, "The failed lake attempt cannot leave its third-miss receipt visible");

            var river = RiverGame(data, true); river.AcceptBounty(2); RiverEnter(river); coins = river.save.coins;
            int fish = river.save.inventory[3]; float time = river.save.totalTime, health = river.save.health, energy = river.save.energy;
            for (int site = 0; site < 2; site++)
            {
                RiverBegin(river, site); river.Notify("原有河岸剧情", "Original river story"); story = river.toast;
                for (int pull = 0; pull < 3; pull++)
                {
                    RiverAdvanceMarker(river, river.RiverFishingTarget); river.FishHit();
                    if (pull < 2) Check(river.toast == story && river.RecentFishingPull.goodPulls == pull + 1,
                        "Accepted river feedback does not replace narrative notifications before a catch settles");
                }
                Check(river.mode == "world" && river.RiverLessonRecorded(site) && river.save.inventory[3] == fish + site + 1
                    && river.save.coins == coins + (site + 1) * 4 && river.save.bountyProgress == site + 1 && !river.RiverLessonComplete,
                    "Each bank still requires three actual pulls and pays one original fish, four coins, one bounty event and only its own note");
                FishingFeedbackAbsent(river, "A terminal river success clears its feedback with the completed cast");
            }
            Check(river.save.totalTime == time && river.save.health == health && river.save.energy == energy,
                "River feedback retains the existing paused world and vital clocks across both real catches");
            river.Enter(0); river.enemies.Clear(); RiverVisit(river, "fish"); RiverChoose(river, "river_offer");
            int heldFish = river.save.inventory[3], heldCoins = river.save.coins, heldStew = river.save.inventory[6], trust = river.save.reputation[2];
            RiverChoose(river, "river_finish");
            Check(river.RiverLessonComplete && river.save.inventory[3] == heldFish && river.save.coins == heldCoins
                && river.save.inventory[6] == heldStew + 2 && river.save.reputation[2] == trust + 1,
                "The separate genuine two-bank report retains its original two stews, Assembly trust and caught fish");
        }
    }
}
