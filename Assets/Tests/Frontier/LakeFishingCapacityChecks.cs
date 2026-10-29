using System;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore LakeCapacityGame(Campaign data, int fish = 0, int coins = 25)
        {
            var game = Fresh(data); game.SkipTutorial(); game.enemies.Clear();
            game.save.inventory[3] = fish; game.save.coins = coins; return game;
        }
        private static void LakeCapacityOriginalChoices(GameCore game)
        {
            Choice[] choices = game.conversation.choices;
            Check(choices.Length == 3 && choices[0].action == "fish" && choices[0].label == "开始钓鱼" && choices[0].en == "Cast the line"
                && choices[1].action == "ally2" && choices[1].label == "支线：交付银鳍鱼 ×3" && choices[1].en == "Commission: deliver 3 Silverfin"
                && choices[2].action == "close" && choices[2].label == "再见" && choices[2].en == "Goodbye",
                "Capacity guidance retains the original numbered cast, three-fish commission and leave actions and labels");
            Check(game.conversation.text.StartsWith("我想给避难的孩子们煮一锅鱼汤，需要三条银鳍鱼。", StringComparison.Ordinal)
                && game.conversation.textEn.StartsWith("Three Silverfin would make soup for the refugee children.", StringComparison.Ordinal)
                && game.conversation.text.Contains("没有鱼饵消耗，慢慢试。")
                && game.conversation.textEn.Contains("No bait cost; take your time."),
                "Capacity guidance appends to the mature fisher narrative and original bait-free three-pull instructions");
        }
        private static void LakeCapacitySaveReload(Campaign data, GameCore source, int fish, int coins)
        {
            source.PrepareSave(); SaveData checkpoint = RiverSave(source.save);
            for (int version = 1; version <= 2; version++)
            {
                SaveData legacy = RiverSave(checkpoint); legacy.version = version;
                var loaded = new GameCore(data, legacy); loaded.Start(); loaded.Close();
                int expectedBounty = version == 1 ? -1 : checkpoint.bounty;
                int expectedProgress = version == 1 ? 0 : checkpoint.bountyProgress;
                Check(loaded.save.inventory[3] == fish && loaded.save.coins == coins
                    && loaded.save.bountyProgress == expectedProgress && loaded.save.bounty == expectedBounty
                    && loaded.save.trackedQuest == checkpoint.trackedQuest && loaded.Has("ally_2") == source.Has("ally_2")
                    && loaded.mode == "world" && !loaded.RiverFishingActive && loaded.RecentFishingRetry == null,
                    "Supported save v" + version + " reload retains the exact bounded catch and commission, with its original legacy bounty migration and no live cast"
                    + "; fish=" + loaded.save.inventory[3] + "/" + fish + "; coins=" + loaded.save.coins + "/" + coins
                    + "; bounty=" + loaded.save.bounty + "/" + checkpoint.bounty + "; progress=" + loaded.save.bountyProgress + "/" + checkpoint.bountyProgress
                    + "; tracked=" + loaded.save.trackedQuest + "/" + checkpoint.trackedQuest + "; ally=" + loaded.Has("ally_2") + "/" + source.Has("ally_2")
                    + "; mode=" + loaded.mode + "; river=" + loaded.RiverFishingActive + "; retry=" + (loaded.RecentFishingRetry != null));
            }
        }

        public static void LakeCapacityOriginalFullBasketRetainsDialogueAndCommissionRecovery(Campaign data)
        {
            foreach (int stock in new[] { 9999, -1, 10000, int.MaxValue })
            {
                var full = LakeCapacityGame(data, stock); RiverVisit(full, "fish"); LakeCapacityOriginalChoices(full);
                Conversation conversation = full.conversation; string before = TacticsState(full.save); int revision = full.revision;
                Check(!full.DialogueChoiceEnabled("fish"), "The original cast is unavailable for full or invalid stock: " + stock);
                RiverChoose(full, "fish");
                Check(full.mode == "dialog" && ReferenceEquals(conversation, full.conversation) && TacticsState(full.save) == before
                    && full.revision == revision && full.sound == "deny" && full.RecentFishingRetry == null,
                    "A full original fisher choice remains in place without discarding fish, charging, paying or creating a retry");
            }
            var game = LakeCapacityGame(data, 9999); RiverVisit(game, "fish");
            Check(!game.DialogueChoiceEnabled("fish") && game.DialogueChoiceEnabled("ally2"),
                "A full basket still offers the existing three-fish handoff as a usable way to make space");
            int coins = game.save.coins, stew = game.save.inventory[6], points = game.save.skillPoints;
            RiverChoose(game, "ally2");
            Check(game.Has("ally_2") && game.save.inventory[3] == 9996 && game.save.coins == coins + 40
                && game.save.inventory[6] == stew + 1 && game.save.skillPoints == points + 1,
                "The mature original commission still consumes three fish and pays its unchanged ally, forty coins, stew and skill point");
            RiverVisit(game, "fish");
            Check(game.DialogueChoiceEnabled("fish") && game.conversation.choices[0].action == "fish"
                && Array.FindIndex(game.conversation.choices, c => c.action == "ally2") < 0,
                "Making space through the real handoff re-enables the original cast without repeating the paid commission");
            RiverLakeCatch(game);
            Check(game.save.inventory[3] == 9997 && game.save.coins == coins + 44,
                "A basket recovered by the original commission can earn a genuine three-pull catch");
            LakeCapacitySaveReload(data, game, 9997, coins + 44);
        }

        public static void LakeCapacityNearLimitCatchAndLegacyReloadKeepExactRewards(Campaign data)
        {
            foreach (int coins in new[] { 999994, 999995, 999996, 999997, 999998, 999999 })
            {
                var game = LakeCapacityGame(data, 9998, coins); game.AcceptBounty(2); game.save.trackedQuest = "ally2";
                RiverVisit(game, "fish"); Check(game.DialogueChoiceEnabled("fish"), "The last fish slot accepts a bait-free cast even when coins are full");
                RiverChoose(game, "fish"); string before = TacticsState(game.save); int revision = game.revision;
                for (int pull = 0; pull < 2; pull++)
                {
                    FishingFeedbackLakeGood(game);
                    Check(TacticsState(game.save) == before && game.revision == revision,
                        "The first two real lake pulls never prepay fish, coins, bounty or story progress");
                }
                FishingFeedbackLakeGood(game); int expectedCoins = Math.Min(999999, coins + 4);
                Check(game.mode == "world" && game.save.inventory[3] == 9999 && game.save.coins == expectedCoins
                    && game.save.bountyProgress == 1 && !game.RiverLessonRecorded(0) && !game.RiverLessonRecorded(1)
                    && game.save.trackedQuest == "ally2" && !game.Has("ally_2"),
                    "The last real lake catch retains its one fish and available zero-to-four coin gain without exceeding persisted limits: " + coins);
                Check(game.RecentFishingRetry != null && !game.CanRetryFishing,
                    "A legitimately settled last-slot catch reports success but cannot advertise an unpayable retry");
                string settled = TacticsState(game.save); game.FishHit(); game.FishHit();
                Check(TacticsState(game.save) == settled, "Extra terminal pull calls cannot pay an already settled last-slot catch again");
                LakeCapacitySaveReload(data, game, 9999, expectedCoins);
            }
            // Older reward paths can leave an already-over-limit live balance.
            // This cast must not remove that pre-existing money; the unchanged
            // save migration remains responsible for normalizing it on load.
            foreach (int coins in new[] { 1000004, int.MaxValue })
            {
                var existing = LakeCapacityGame(data, 0, coins); existing.AcceptBounty(2);
                RiverLakeCatch(existing);
                Check(existing.save.inventory[3] == 1 && existing.save.coins == coins && existing.save.bountyProgress == 1,
                    "A pre-existing oversized live balance gains zero coins without losing its original money, fish or bounty catch: " + coins);
            }
        }

        public static void LakeCapacityStaleChoiceAndChangedCastStockNeverMintUnstoredCatch(Campaign data)
        {
            var stale = LakeCapacityGame(data, 9998); RiverVisit(stale, "fish");
            int choice = RiverChoice(stale, "fish"); Check(stale.DialogueChoiceEnabled("fish"), "The displayed pre-full cast begins as enabled");
            stale.save.inventory[3] = 9999; Conversation conversation = stale.conversation;
            string before = TacticsState(stale.save); int revision = stale.revision; stale.Choose(choice);
            Check(stale.mode == "dialog" && ReferenceEquals(conversation, stale.conversation) && TacticsState(stale.save) == before
                && stale.revision == revision && stale.sound == "deny", "A stale enabled original choice rechecks capacity before closing or starting a cast");
            foreach (int stock in new[] { 9999, -1, 10000, int.MaxValue })
            {
                var changed = LakeCapacityGame(data, 9998, 999998); changed.AcceptBounty(2); RiverVisit(changed, "fish"); RiverChoose(changed, "fish");
                FishingFeedbackLakeGood(changed); FishingFeedbackLakeGood(changed);
                changed.save.inventory[3] = stock; before = TacticsState(changed.save); revision = changed.revision;
                FishingFeedbackLakeGood(changed);
                Check(changed.mode == "world" && changed.fishHits == 3 && TacticsState(changed.save) == before
                    && changed.revision == revision && changed.save.bountyProgress == 0 && !changed.Has("ally_2")
                    && !changed.RiverLessonRecorded(0) && !changed.RiverLessonRecorded(1) && changed.RecentFishingRetry == null
                    && !changed.CanRetryFishing && changed.toast.Length > 0,
                    "A changed full or invalid basket at settlement closes with no unstoreable fish, coins, bounty, story or retry: " + stock);
            }
        }

        public static void LakeCapacityNormalMissCancelAndReadonlyGuidanceKeepOriginalRules(Campaign data)
        {
            foreach (bool english in new[] { false, true }) foreach (int fish in new[] { 0, 3, 9998, 9999 }) foreach (int coins in new[] { 25, 999997, 999999 })
            {
                var game = LakeCapacityGame(data, fish, coins); game.save.english = english; RiverVisit(game, "fish"); LakeCapacityOriginalChoices(game);
                string state = TacticsState(game); Conversation conversation = game.conversation;
                for (int read = 0; read < 12; read++)
                {
                    Check(game.DialogueChoiceEnabled("fish") == (fish < 9999), "Readonly availability reflects actual basket space in both languages");
                    Check(game.DialogueChoiceLabel(conversation.choices[0]) == game.L("开始钓鱼", "Cast the line")
                        && conversation.text.Contains(fish + "/9999") && conversation.textEn.Contains(fish + "/9999")
                        && conversation.text.Contains("+" + Math.Min(4, 999999 - coins)) && conversation.textEn.Contains("+" + Math.Min(4, 999999 - coins)),
                        "The unchanged cast label and appended bilingual preview report exact held fish and actual bounded coin gain");
                }
                Check(TacticsState(game) == state, "Repeated dialogue labels, status text and readiness reads are strictly simulation/save readonly");
            }
            var normal = LakeCapacityGame(data); normal.AcceptBounty(2); int initialCoins = normal.save.coins;
            RiverLakeCatch(normal);
            Check(normal.save.inventory[3] == 1 && normal.save.coins == initialCoins + 4 && normal.save.bountyProgress == 1
                && normal.toast == normal.L("钓到银鳍鱼 ×1！", "Caught a Silverfin!"), "Ordinary three-pull success keeps its exact fish, four coins, bounty and final story notification");
            var missed = LakeCapacityGame(data); RiverVisit(missed, "fish"); RiverChoose(missed, "fish"); string before = TacticsState(missed.save);
            missed.FishHit(); missed.FishHit(); missed.FishHit();
            Check(missed.mode == "world" && missed.fishMisses == 3 && TacticsState(missed.save) == before
                && missed.toast == missed.L("鱼游走了。没有损失，再试一次吧。", "The fish slipped away. Nothing lost; try again."),
                "Three actual missed lake pulls retain the no-loss bait-free original result");
            var cancelled = LakeCapacityGame(data); RiverVisit(cancelled, "fish"); RiverChoose(cancelled, "fish"); FishingFeedbackLakeGood(cancelled);
            before = TacticsState(cancelled.save); cancelled.Close(); cancelled.FishHit();
            Check(cancelled.mode == "world" && TacticsState(cancelled.save) == before && cancelled.RecentFishingRetry == null,
                "Cancelling an unfinished lake cast still pays nothing, costs no bait and never offers a completed-attempt retry");
            LakeCapacitySaveReload(data, cancelled, 0, 25);
        }
    }
}
