using System;
using System.Reflection;
using System.Text;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        // Reflection allows the same final checks to compile against an older
        // Core: missing quote behavior must be a semantic failure, not a build failure.
        private static object BountyRewardRead(GameCore game)
        {
            MethodInfo method = typeof(GameCore).GetMethod("GetBountyRewardQuote", Type.EmptyTypes);
            Check(method != null, "Contracts expose a fresh read-only reward quote");
            return method.Invoke(game, null);
        }
        private static T BountyRewardValue<T>(object quote, string name)
        {
            Check(quote != null, "An accepted contract has a reward quote");
            FieldInfo field = quote.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Check(field != null && field.IsInitOnly && field.FieldType == typeof(T), "Reward snapshot field is immutable and typed: " + name);
            return (T)field.GetValue(quote);
        }
        private static GameCore BountyRewardGame(Campaign data, int contract = 0, bool complete = true)
        {
            GameCore game = Unlocked(data); game.Close(); game.enemies.Clear(); game.projectiles.Clear(); game.hazards.Clear();
            game.save.companion = -1; game.save.bounty = -1; game.save.level = 20; game.save.xp = 0;
            game.save.skillPoints = 3; game.save.coins = 100; game.save.shards = 10; game.save.frontierEnding = 1;
            game.AcceptBounty(contract); game.save.bountyProgress = complete ? GameCore.BountyTargets[contract] : 0;
            Check(game.save.bounty == contract, "The actual acceptance action owns the quoted contract");
            return game;
        }
        private static string BountyRewardSave(GameCore game)
        { var text = new StringBuilder(); PlanStatsAppend(text, game.save); return text.ToString(); }
        private static void BountyRewardPaidMatches(GameCore game, object quote, string label)
        {
            int cycle = game.save.bountyCycle;
            Check(BountyRewardValue<bool>(quote,"complete") && BountyRewardValue<bool>(quote,"canClaim"), label + " is ready and fits");
            Check(game.ClaimBounty(), label + " pays through the actual original claim transaction");
            Check(game.save.coins == BountyRewardValue<long>(quote,"coinsAfter") && game.save.shards == BountyRewardValue<long>(quote,"shardsAfter")
                && game.save.xp == BountyRewardValue<long>(quote,"experienceAfter") && game.save.level == BountyRewardValue<int>(quote,"levelAfter")
                && game.save.skillPoints == BountyRewardValue<long>(quote,"skillPointsAfter"), label + " actual balances and level outcome match the preview");
            Check(game.save.bounty == -1 && game.save.bountyProgress == 0 && game.save.bountyCycle == cycle + 1,
                label + " consumes only the completed contract and advances its original rotation once");
            string paid = BountyRewardSave(game);
            Check(!game.ClaimBounty() && BountyRewardSave(game) == paid && BountyRewardRead(game) == null,
                label + " cannot pay twice or keep a stale active quote");
        }

        public static void BountyRewardQuotesAreImmutableExactAndFresh(Campaign data)
        {
            var empty = Unlocked(data); empty.save.bounty = -1; string original = PlanStatsState(empty);
            Check(BountyRewardRead(empty) == null && original == PlanStatsState(empty), "No contract returns no quote and mutates nothing");
            for (int contract = 0; contract < 6; contract++) for (int ending = 0; ending < 4; ending++)
            {
                GameCore game = BountyRewardGame(data,contract,false); game.save.frontierEnding = ending;
                game.save.energy = 100; game.Attack(new Point(1,0)); ActorAction paid = game.playerAction;
                Check(paid != null && !paid.released, "Quote purity includes a genuinely paid unreleased attack");
                foreach (string mode in new[] {"world","bounties","inventory","dialog","pause"})
                {
                    game.mode = mode; string before = PlanStatsState(game); object quote = null;
                    for (int repeat = 0; repeat < 5; repeat++) quote = BountyRewardRead(game);
                    Check(BountyRewardValue<int>(quote,"coinReward") == 35 && BountyRewardValue<int>(quote,"experienceReward") == 30
                        && BountyRewardValue<int>(quote,"shardReward") == (ending == 3 ? 4 : 3), "Every contract shows the exact original ending-dependent payout");
                    Check(BountyRewardValue<int>(quote,"coinsBefore") == 100 && BountyRewardValue<long>(quote,"coinsAfter") == 135
                        && BountyRewardValue<int>(quote,"shardsBefore") == 10 && BountyRewardValue<long>(quote,"shardsAfter") == 10 + (ending == 3 ? 4 : 3)
                        && BountyRewardValue<int>(quote,"experienceBefore") == 0 && BountyRewardValue<long>(quote,"experienceAfter") == 30,
                        "The existing balance is distinguished from the expected payout");
                    Check(!BountyRewardValue<bool>(quote,"complete") && !BountyRewardValue<bool>(quote,"canClaim"), "An unfinished contract cannot be redeemed by previewing it");
                    Check(PlanStatsState(game) == before && ReferenceEquals(paid,game.playerAction), "Repeated reward reads preserve save, simulation, accepted attack and owner in " + mode);
                }
                object captured = BountyRewardRead(game); game.save.coins = 250; game.save.frontierEnding = ending == 3 ? 1 : 3;
                object fresh = BountyRewardRead(game);
                Check(BountyRewardValue<int>(captured,"coinsBefore") == 100 && BountyRewardValue<int>(captured,"shardReward") == (ending == 3 ? 4 : 3)
                    && BountyRewardValue<int>(fresh,"coinsBefore") == 250 && BountyRewardValue<int>(fresh,"shardReward") == (ending == 3 ? 3 : 4),
                    "An immutable captured quote stays fixed while the next read reflects the live ending and balance");
            }
        }

        public static void BountyRewardPaymentsMatchCapsLevelUpsAndCurrentState(Campaign data)
        {
            foreach (int ending in new[] {1,3})
            {
                GameCore game = BountyRewardGame(data,4); game.save.frontierEnding = ending;
                game.save.coins = 999964; game.save.shards = ending == 3 ? 99995 : 99996;
                game.save.skillPoints = 99; game.save.health = 5;
                BountyRewardPaidMatches(game,BountyRewardRead(game),"Exact coin/shard caps with no level-up");
                Check(game.save.coins == 999999 && game.save.shards == 99999 && game.save.skillPoints == 99 && game.save.health == 5,
                    "Exact caps remain payable and an ordinary non-leveling reward does not heal or consume a point");
            }
            // Fixed authored outcomes exercise the production AddXp transaction,
            // including more than one carried threshold and the level-30 ceiling.
            int[,] cases = {{1,20,2,10,4},{1,100,3,10,5},{29,1150,30,20,4},{30,1200,30,1230,3}};
            for (int i = 0; i < cases.GetLength(0); i++)
            {
                GameCore game = BountyRewardGame(data,2); game.save.level = cases[i,0]; game.save.xp = cases[i,1]; game.save.health = 5;
                object quote = BountyRewardRead(game);
                Check(BountyRewardValue<int>(quote,"levelBefore") == cases[i,0] && BountyRewardValue<int>(quote,"levelAfter") == cases[i,2]
                    && BountyRewardValue<long>(quote,"experienceAfter") == cases[i,3] && BountyRewardValue<int>(quote,"skillPointsBefore") == 3
                    && BountyRewardValue<long>(quote,"skillPointsAfter") == cases[i,4], "Authored original XP/point projection case " + i);
                BountyRewardPaidMatches(game,quote,"Level projection " + i);
                Check(game.save.health == (cases[i,2] > cases[i,0] ? game.save.maxHealth : 5), "Actual AddXp retains its original level-up full heal only");
            }
            GameCore stale = BountyRewardGame(data); object old = BountyRewardRead(stale); stale.save.frontierEnding = 3; stale.save.coins = 400;
            Check(BountyRewardValue<int>(old,"shardReward") == 3, "A previous preview really predates the changed ending");
            BountyRewardPaidMatches(stale,BountyRewardRead(stale),"A fresh claim uses changed live ending and balance");
            Check(stale.save.coins == 435 && stale.save.shards == 14, "Payment cannot use a previously displayed three-shard quote");
            GameCore unfinished = BountyRewardGame(data,1,false); string before = BountyRewardSave(unfinished);
            Check(!unfinished.ClaimBounty() && BountyRewardSave(unfinished) == before, "Incomplete claims remain atomic and do not advance progress or rotation");
        }

        public static void BountyRewardCapacityBlocksRetainCompletedContractsAcrossSaves(Campaign data)
        {
            foreach (string blocker in new[] {"coins","shards","points","xp"})
            {
                GameCore game = BountyRewardGame(data,3); game.save.bountyCycle = 17; game.save.health = 7;
                if (blocker == "coins") game.save.coins = 999965;
                if (blocker == "shards") game.save.shards = 99997;
                if (blocker == "points") {game.save.level = 1; game.save.xp = 20; game.save.skillPoints = 99;}
                if (blocker == "xp") {game.save.level = 30; game.save.xp = int.MaxValue - 29;}
                string before = BountyRewardSave(game); int revision = game.revision;
                ActorAction action = game.playerAction;
                // This assertion also runs against the archived old production
                // before asking for any new quote API: RED witnesses lost rewards.
                Check(!game.ClaimBounty() && BountyRewardSave(game) == before && game.revision == revision,
                    "The actual claim must retain a completed contract when " + blocker + " rewards exceed persistent capacity");
                object quote = BountyRewardRead(game);
                Check(BountyRewardValue<bool>(quote,"complete") && !BountyRewardValue<bool>(quote,"canClaim")
                    && !string.IsNullOrEmpty(BountyRewardValue<string>(quote,"reasonZh")) && !string.IsNullOrEmpty(BountyRewardValue<string>(quote,"reasonEn")),
                    "A completed " + blocker + " capacity blocker explains why reward collection remains pending");
                Check(!game.ClaimBounty() && BountyRewardSave(game) == before && game.revision == revision && ReferenceEquals(action,game.playerAction),
                    "Rejected " + blocker + " collection preserves every save field, accepted action and revision");
                game.PrepareSave(); game = new GameCore(data,CloneSave(game.save)); game.Start(); game.Close();
                Check(game.save.bounty == 3 && game.save.bountyProgress == 1 && game.save.bountyCycle == 17
                    && !BountyRewardValue<bool>(BountyRewardRead(game),"canClaim"), "The completed " + blocker + " contract survives the actual save reconstruction without a hidden claim flag");
                // Free capacity through existing resource-consuming operations
                // where possible; XP overflow uses a restored sane XP value.
                if (blocker == "coins") Action(game,"garden","seeds");
                if (blocker == "shards") {game.save.projects[2] = 1; game.save.relicRank = 0; Check(game.RefineRelics(), "The existing workshop consumes shards to free reward capacity");}
                if (blocker == "points") game.Learn(0);
                if (blocker == "xp") game.save.xp = 0;
                BountyRewardPaidMatches(game,BountyRewardRead(game),"Previously retained " + blocker + " contract after capacity is freed");
                game.PrepareSave(); var loaded = new GameCore(data,CloneSave(game.save));
                Check(loaded.save.bounty == -1 && loaded.save.bountyCycle == 18 && loaded.save.coins == game.save.coins
                    && loaded.save.shards == game.save.shards && loaded.save.xp == game.save.xp && loaded.save.skillPoints == game.save.skillPoints,
                    "A successful formerly blocked payment survives save/load without clipped reward");
            }
            GameCore refreshed = BountyRewardGame(data); object formerlyReady = BountyRewardRead(refreshed);
            Check(BountyRewardValue<bool>(formerlyReady,"canClaim"), "A quote can be valid before another reward fills capacity");
            refreshed.save.coins = 999999; string full = BountyRewardSave(refreshed);
            Check(!refreshed.ClaimBounty() && BountyRewardSave(refreshed) == full && refreshed.save.bounty == 0,
                "The actual claim independently rechecks capacity rather than trusting earlier ready UI state");
        }

        public static void BountyRewardLegacyMigrationAndAbandonStayOriginal(Campaign data)
        {
            foreach (int version in new[] {1,2})
            {
                GameCore original = BountyRewardGame(data,5); original.save.coins = 999999; original.save.bountyCycle = 41;
                original.save.inventory[2] = 11; original.save.flags.Add("legacy_bounty_reward_keep"); original.PrepareSave();
                SaveData saved = CloneSave(original.save); saved.version = version;
                GameCore loaded = new GameCore(data,saved); loaded.Start(); loaded.Close(); string before = PlanStatsState(loaded);
                object quote = BountyRewardRead(loaded);
                Check(loaded.save.coins == 999999 && loaded.save.inventory[2] == 11 && loaded.Has("legacy_bounty_reward_keep")
                    && loaded.save.bountyCycle == 41 && loaded.save.version == 2 && PlanStatsState(loaded) == before,
                    "Reward queries preserve existing old-save resources, story markers and migration version");
                if (version == 1) Check(loaded.save.bounty == -1 && quote == null, "The original v1 migration still resets the unsupported expansion contract");
                else
                {
                    Check(loaded.save.bounty == 5 && loaded.save.bountyProgress == 2 && quote != null && !BountyRewardValue<bool>(quote,"canClaim"),
                        "An existing v2 completed contract keeps its progress and can wait for safe payment");
                    int coins = loaded.save.coins, shards = loaded.save.shards, cycle = loaded.save.bountyCycle;
                    loaded.AbandonBounty(); Check(loaded.save.bounty == -1 && loaded.save.bountyProgress == 0 && loaded.save.coins == coins
                        && loaded.save.shards == shards && loaded.save.bountyCycle == cycle && BountyRewardRead(loaded) == null,
                        "The mature explicit abandon action remains available without paying or rotating a completed blocked contract");
                }
            }
            foreach (FieldInfo field in typeof(SaveData).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Check(field.Name.IndexOf("quote",StringComparison.OrdinalIgnoreCase) < 0 && field.Name.IndexOf("bountyReward",StringComparison.OrdinalIgnoreCase) < 0,
                    "Derived reward snapshots introduce no new persisted cache or migration requirement");
        }
    }
}
