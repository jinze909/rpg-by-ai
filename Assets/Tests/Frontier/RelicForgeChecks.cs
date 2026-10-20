using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore ForgeReady(Campaign data, int workshop = 1, int rank = 0)
        {
            var game = Fresh(data); game.save.projects[2] = workshop; game.save.relicRank = rank;
            game.save.shards = 100; game.save.inventory[1] = 20; game.save.coins = 100;
            return game;
        }

        private static string ForgeState(GameCore game)
        {
            var text = new StringBuilder();
            foreach (FieldInfo field in typeof(SaveData).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                text.Append(field.Name).Append('='); object value = field.GetValue(game.save);
                var sequence = value as IEnumerable;
                if (sequence != null && !(value is string))
                    foreach (object item in sequence) text.Append(Convert.ToString(item, CultureInfo.InvariantCulture)).Append(',');
                else text.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                text.Append('|');
            }
            return text.ToString();
        }

        public static void ForgeQuotesExposeExactCostsAndImmutableSnapshots(Campaign data)
        {
            Check(typeof(RelicForgeQuote).IsSealed, "Quotes cannot be extended with mutable transaction state");
            foreach (FieldInfo field in typeof(RelicForgeQuote).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Check(field.IsInitOnly, "Every exposed quote field is readonly: " + field.Name);
            for (int workshop = 1; workshop <= 2; workshop++) for (int rank = 0; rank <= 3; rank++)
            {
                var game = ForgeReady(data, workshop, rank); string before = ForgeState(game); int revision = game.revision;
                RelicForgeQuote quote = game.GetRelicForgeQuote();
                Check(quote.currentRank == rank && quote.nextRank == Math.Min(3, rank + 1) &&
                    quote.currentBonus == rank * 2 && quote.nextBonus == Math.Min(3, rank + 1) * 2,
                    "Quote previews the existing rank and conditional equipped-relic damage bonus");
                Check(quote.shards == (rank == 3 ? 0 : (rank + 1) * 8) && quote.crystals == (rank == 3 ? 0 : 2) &&
                    quote.coins == (rank == 3 || workshop == 2 ? 0 : 20) && quote.available == (rank < 3),
                    "Both workshop tiers expose the original exact costs, with no fourth-rank offer");
                Check(ForgeState(game) == before && game.revision == revision, "Reading prices cannot spend, revise or alter saves");
                game.save.relicRank = rank == 3 ? 0 : rank + 1; game.save.shards = 0;
                Check(quote.currentRank == rank && quote.available == (rank < 3), "An existing quote is an immutable snapshot, not live authority");
            }
        }

        public static void ForgeShortfallsReportEveryMissingResourceWithoutMutation(Campaign data)
        {
            for (int rank = 0; rank < 3; rank++) for (int mask = 1; mask < 8; mask++)
            {
                var game = ForgeReady(data, 1, rank); int price = (rank + 1) * 8;
                game.save.shards = (mask & 1) != 0 ? price - 3 : price;
                game.save.inventory[1] = (mask & 2) != 0 ? 1 : 2;
                game.save.coins = (mask & 4) != 0 ? 13 : 20;
                string before = ForgeState(game); int revision = game.revision;
                RelicForgeQuote quote = game.GetRelicForgeQuote();
                Check(!quote.available && !String.IsNullOrEmpty(quote.reasonZh) && !String.IsNullOrEmpty(quote.reasonEn),
                    "Every unaffordable combination has both language explanations");
                string reason = quote.reasonEn.ToLowerInvariant();
                if ((mask & 1) != 0) Check(reason.Contains("shard") && reason.Contains("3"), "Explain the exact missing three shards");
                if ((mask & 2) != 0) Check(reason.Contains("crystal") && reason.Contains("1"), "Explain the exact missing one crystal");
                if ((mask & 4) != 0) Check(reason.Contains("coin") && reason.Contains("7"), "Explain the exact missing seven coins");
                Check(!game.RefineRelics() && ForgeState(game) == before && game.revision == revision,
                    "A rejected refinement preserves every save field and revision even when several resources are short");
            }
            var upgraded = ForgeReady(data, 2); upgraded.save.coins = 0;
            Check(upgraded.GetRelicForgeQuote().available && upgraded.RefineRelics() && upgraded.save.coins == 0,
                "The upgraded workshop waives the twenty-coin charge even at zero coins");
        }

        public static void ForgeTransactionsRecheckStaleQuotesBeforeAnySpend(Campaign data)
        {
            for (int stale = 0; stale < 6; stale++)
            {
                var game = ForgeReady(data); RelicForgeQuote quote = game.GetRelicForgeQuote();
                Check(quote.available, "The original button was legitimately available");
                if (stale == 0) game.save.shards = 7;
                if (stale == 1) game.save.inventory[1] = 1;
                if (stale == 2) game.save.coins = 19;
                if (stale == 3) game.save.room = 1;
                if (stale == 4) game.save.projects[2] = 0;
                if (stale == 5) game.save.relicRank = 3;
                string before = ForgeState(game); int revision = game.revision;
                Check(!game.GetRelicForgeQuote().available && !game.RefineRelics() && ForgeState(game) == before && game.revision == revision,
                    "Every stale gate is checked again before Spend or the rank increment: " + stale);
                Check(quote.available && quote.currentRank == 0, "Rejecting the current state cannot rewrite an earlier snapshot");
            }
        }

        public static void ForgeSuccessfulUpgradesKeepExactTransactionsAndExistingEntryModes(Campaign data)
        {
            foreach (string mode in new[] { "world", "loadout", "inventory" }) for (int workshop = 1; workshop <= 2; workshop++)
            {
                var game = ForgeReady(data, workshop); game.mode = mode;
                for (int rank = 0; rank < 3; rank++)
                {
                    int shards = game.save.shards, crystals = game.save.inventory[1], coins = game.save.coins, revision = game.revision;
                    int[] relics = (int[])game.save.relics.Clone(); int wood = game.save.inventory[0], herbs = game.save.inventory[2];
                    Check(game.RefineRelics() && game.save.relicRank == rank + 1 && game.save.shards == shards - (rank + 1) * 8 &&
                        game.save.inventory[1] == crystals - 2 && game.save.coins == coins - (workshop == 2 ? 0 : 20),
                        "Each accepted upgrade applies the original exact price once in an existing mode");
                    Check(game.revision == revision + 2 && game.sound == "reward" && game.mode == mode &&
                        game.save.inventory[0] == wood && game.save.inventory[2] == herbs &&
                        game.save.relics[0] == relics[0] && game.save.relics[1] == relics[1],
                        "Spend and refinement retain their two revisions without changing unrelated supplies or equipment");
                }
                string before = ForgeState(game); int lastRevision = game.revision;
                Check(!game.RefineRelics() && ForgeState(game) == before && game.revision == lastRevision,
                    "Rapid repeated confirmation at rank three cannot charge or refine a fourth time");
            }
        }

        public static void ForgeLegacySavesPreserveRanksAndDamageRequiresAnEquippedRelic(Campaign data)
        {
            for (int version = 1; version <= 2; version++) for (int rank = 0; rank <= 3; rank++)
            {
                var original = ForgeReady(data, 2, rank); original.save.version = version;
                original.save.flags.Add("relic_0"); original.save.flags.Add("relic_2"); original.save.flags.Add("return_letter_complete");
                original.save.stage = 2; original.save.kills[1] = 7;
                var game = new GameCore(data, CloneSave(original.save)); game.Close();
                Check(game.save.version == 2 && game.save.relicRank == rank && game.save.projects[2] == 2 &&
                    game.Has("return_letter_complete") && game.save.stage == 2 && game.save.kills[1] == 7 &&
                    game.save.shards == original.save.shards && game.save.coins == original.save.coins,
                    "Existing v1/v2 saves keep rank, workshop, story, kills and funds");
                float unarmedBonus = game.Damage; RelicForgeQuote quote = game.GetRelicForgeQuote();
                game.EquipRelic(0, 0); Check(Math.Abs(game.Damage - unarmedBonus - quote.currentBonus) < .001f,
                    "Actual damage receives the quoted rank bonus only after wearing a genuine unlocked relic");
                game.EquipRelic(1, 2); Check(Math.Abs(game.Damage - unarmedBonus - quote.currentBonus) < .001f,
                    "Two relic slots do not double the global refining bonus");
                game.EquipRelic(0, -1); game.EquipRelic(1, -1);
                Check(Math.Abs(game.Damage - unarmedBonus) < .001f && game.GetRelicForgeQuote().currentBonus == rank * 2,
                    "Removing both relics removes actual bonus while the conditional forging preview stays honest");
            }
        }
    }
}
