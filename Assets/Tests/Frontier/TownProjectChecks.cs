using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore TownReady(Campaign data, int profession = 0)
        {
            var game = Fresh(data, profession); game.SkipTutorial();
            game.save.inventory = new[] { 80, 80, 13, 7, 80, 3, 6, 5 };
            game.save.coins = 450; game.save.health = 37; game.save.energy = 63;
            game.save.reputation = new[] { 2, 3, 4 }; game.mode = "camp"; return game;
        }
        private static void TownAppend(StringBuilder text, object value)
        {
            if (value == null) { text.Append("null;"); return; }
            if (value is string) { string s = (string)value; text.Append(s.Length).Append(':').Append(s).Append(';'); return; }
            if (value.GetType().IsPrimitive || value.GetType().IsEnum)
            { text.Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append(';'); return; }
            if (value is IEnumerable)
            { text.Append('['); foreach (object item in (IEnumerable)value) TownAppend(text, item); text.Append(']'); return; }
            text.Append('{'); foreach (FieldInfo field in value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            { text.Append(field.Name).Append('='); TownAppend(text, field.GetValue(value)); } text.Append('}');
        }
        private static string TownSave(SaveData save)
        { var text = new StringBuilder(); TownAppend(text, save); return text.ToString(); }
        private static string TownState(GameCore game)
        {
            var text = new StringBuilder(TownSave(game.save)); TownAppend(text, game.playerAction);
            return text + "/" + game.revision + "/" + game.mode + "/" + game.WorldGeneration + "/" + game.toast
                + "/" + game.toastTime + "/" + game.sound + "/" + game.sounds.Count + "/" + game.effects.Count
                + "/" + game.attackCooldown + "/" + game.skillCooldown + "/" + game.dashCooldown;
        }
        private static void TownReject(GameCore game, TownProjectQuote quote, string label)
        {
            string before = TownState(game);
            Check(!game.CanBuildProject(quote) && !game.TryBuildProject(quote) && TownState(game) == before,
                "Rejected review changes no save, action, feedback or payment: " + label);
        }
        private static void TownPay(GameCore game, int project)
        {
            game.mode = "camp"; TownProjectQuote quote = game.GetTownProjectQuote(project);
            Check(quote.available && game.CanBuildProject(quote), "A deliberately reviewed project is affordable and valid");
            SaveData expected = CloneSave(game.save);
            expected.inventory[0] -= quote.wood; expected.inventory[1] -= quote.crystals; expected.inventory[4] -= quote.parts;
            expected.coins -= quote.coins; expected.projects[project]++; expected.reputation[2] = Math.Min(10, expected.reputation[2] + 1);
            if (project == 0 && expected.projects[0] == 2 && !expected.flags.Contains("relic_1")) expected.flags.Add("relic_1");
            Check(game.TryBuildProject(quote) && TownSave(game.save) == TownSave(expected),
                "Confirmed construction performs only original materials, coins, project, trust and the earned Amber Seed award");
            TownReject(game, quote, "the already consumed tier review");
        }

        public static void TownProjectQuotesExposeExactCostsBenefitsAndReadOnlySnapshots(Campaign data)
        {
            Check(typeof(TownProjectQuote).IsSealed, "Construction reviews cannot derive mutable payment authority");
            foreach (FieldInfo field in typeof(TownProjectQuote).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Check(field.IsInitOnly, "Every public quoted field is immutable: " + field.Name);
            for (int profession = 0; profession < 3; profession++) for (int project = 0; project < 3; project++)
            for (int tier = 0; tier <= 2; tier++) foreach (int ending in new[] { 0, 1, 3 })
            {
                var game = TownReady(data, profession); game.save.projects[project] = tier; game.save.frontierEnding = ending;
                game.save.reputation[2] = tier == 1 ? 10 : 4; string before = TownState(game);
                TownProjectQuote quote = game.GetTownProjectQuote(project); bool capped = tier == 2;
                Check(quote.project == project && quote.tier == tier && quote.nextTier == Math.Min(2, tier + 1)
                    && quote.capped == capped && quote.available == !capped,
                    "Every facility, class and council outcome exposes its actual two-tier progression");
                Check(quote.wood == (capped ? 0 : 4 + tier * 2) && quote.crystals == (capped ? 0 : 2 + tier)
                    && quote.parts == (capped ? 0 : 3 + tier) && quote.coins == (capped ? 0 : 30 + tier * 20),
                    "The original first-tier and second-tier costs are quoted exactly; maximum-tier payment is zero");
                Check(quote.woodBefore == 80 && quote.crystalsBefore == 80 && quote.partsBefore == 80 && quote.coinsBefore == 450
                    && quote.trustBefore == game.save.reputation[2] && quote.trustAfter == Math.Min(10, quote.trustBefore + (capped ? 0 : 1))
                    && quote.tiersBefore == game.ProjectTiers && quote.tiersAfter == game.ProjectTiers + (capped ? 0 : 1),
                    "The review describes exact inventory balances, capped Wild Assembly trust and shared-rule project total");
                int harvest = 5 + game.save.projects[0] * 2 + (ending == 1 ? 2 : 0);
                Check(quote.harvestBefore == harvest && quote.harvestAfter == harvest + (!capped && project == 0 ? 2 : 0),
                    "Canal yield previews include the existing water covenant without adding it to unrelated projects");
                for (int read = 0; read < 5; read++)
                {
                    game.GetTownProjectQuote(project); game.CanBuildProject(quote);
                    Check(!string.IsNullOrEmpty(game.TownProjectName(project)) && !string.IsNullOrEmpty(game.TownProjectBenefit(project, tier)),
                        "Current facility names and benefits are available in the selected language");
                }
                Check(TownState(game) == before, "Repeated construction previews never heal, spend, log, advance clocks/RNG or rewrite a prepared attack");
                game.save.coins++; Check(quote.coinsBefore == 450, "An existing review remains an independent snapshot");
            }
            int[] slots = { 0, 1, 4, -1 }; string[] names = { "timber", "crystals", "parts", "coins" };
            for (int tier = 0; tier < 2; tier++) for (int resource = 0; resource < 4; resource++)
            {
                var game = TownReady(data); game.save.projects[1] = tier;
                int[] need = { 4 + tier * 2, 2 + tier, 3 + tier, 30 + tier * 20 };
                if (slots[resource] < 0) game.save.coins = need[resource] - 1; else game.save.inventory[slots[resource]] = need[resource] - 1;
                string before = TownState(game); TownProjectQuote quote = game.GetTownProjectQuote(1);
                Check(!quote.available && quote.reasonEn.Contains("1 " + names[resource]) && !string.IsNullOrEmpty(quote.reasonZh)
                    && TownState(game) == before, "A one-unit shortfall is explained in both languages without changing stock: " + names[resource]);
            }
            var remote = TownReady(data); remote.Enter(1); string remoteBefore = TownState(remote);
            Check(!remote.GetTownProjectQuote(0).available && TownState(remote) == remoteBefore,
                "A remote read explains that construction belongs in Emberfall without travelling or paying");
        }

        public static void TownProjectTransactionsKeepSixTiersAndActualFacilityBenefits(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++)
            {
                var game = TownReady(data, profession); game.save.reputation[2] = 9;
                for (int project = 0; project < 3; project++) for (int tier = 0; tier < 2; tier++) TownPay(game, project);
                Check(game.ProjectTiers == 6 && game.save.coins == 210 && game.save.inventory[0] == 50
                    && game.save.inventory[1] == 65 && game.save.inventory[4] == 59 && game.save.reputation[2] == 10 && game.Has("relic_1"),
                    "All six original construction tiers keep cumulative prices, trust cap and unique relic award");
                for (int project = 0; project < 3; project++) TownReject(game, game.GetTownProjectQuote(project), "a fully upgraded facility");
            }
            for (int tier = 1; tier <= 2; tier++) foreach (int ending in new[] { 0, 1 })
            {
                var garden = TownReady(data); garden.save.frontierEnding = ending;
                for (int built = 0; built < tier; built++) TownPay(garden, 0);
                garden.Close(); Action(garden, "garden", "plant"); Action(garden, "rest", "rest"); Action(garden, "rest", "rest");
                int herbs = garden.save.inventory[2]; Action(garden, "garden", "harvest");
                Check(garden.save.inventory[2] == herbs + 5 + tier * 2 + (ending == 1 ? 2 : 0)
                    && (tier != 2 || garden.Has("relic_1")), "Real planting, inn time and harvest apply the quoted canal/covenant yield and tier-II relic");
            }
            for (int tier = 1; tier <= 2; tier++)
            {
                var camp = TownReady(data); for (int built = 0; built < tier; built++) TownPay(camp, 1);
                camp.save.stage = 3; camp.save.flags.Add("exp_intro"); camp.Close(); camp.Enter(5); camp.enemies.Clear();
                camp.save.totalTime = 100; camp.save.health = 17; camp.save.energy = 11; int tea = camp.save.tea;
                Action(camp, "campfire", "fieldrest");
                Check(camp.save.health == camp.save.maxHealth && camp.save.energy == 100 && camp.save.totalTime == 120
                    && camp.save.tea == tea + (tier == 2 ? 1 : 0), "The actual camp rest is enabled at tier I and gives a real daily tea at tier II");
                Action(camp, "campfire", "fieldrest");
                Check(camp.save.tea == tea + (tier == 2 ? 1 : 0), "Repeated same-day camp rest cannot duplicate the waystation tea");
                var forge = TownReady(data); for (int built = 0; built < tier; built++) TownPay(forge, 2);
                forge.save.shards = 40; int coins = forge.save.coins; var quote = forge.GetRelicForgeQuote();
                Check(quote.available && quote.coins == (tier == 2 ? 0 : 20) && forge.RefineRelics()
                    && forge.save.relicRank == 1 && forge.save.coins == coins - (tier == 2 ? 0 : 20),
                    "The actual relic forge opens at tier I and tier II removes exactly its original coin fee");
            }
            var legacy = TownReady(data); legacy.Close(); Check(legacy.BuildProject(0) && legacy.save.projects[0] == 1,
                "The mature direct BuildProject API remains usable in the world without a UI confirmation object");
        }

        public static void TownProjectStaleInvalidAndForeignReviewsRejectWithoutPayment(Campaign data)
        {
            Action<GameCore>[] edits = {
                game => game.save.inventory[0]++, game => game.save.inventory[1]--, game => game.save.inventory[4]++,
                game => game.save.coins++, game => game.save.projects[0]++, game => game.save.projects[1]++,
                game => game.save.projects[2]++, game => game.save.reputation[2]++, game => game.save.frontierEnding = 1,
                game => game.mode = "inventory", game => game.save = CloneSave(game.save),
                game => { game.Enter(0); game.mode = "camp"; }, game => { game.Load(CloneSave(game.save)); game.mode = "camp"; },
                game => game.deathTime = .2f, game => game.save.health = 0, game => game.practicing = true
            };
            for (int edit = 0; edit < edits.Length; edit++)
            {
                var game = TownReady(data); TownProjectQuote quote = game.GetTownProjectQuote(0); edits[edit](game);
                TownReject(game, quote, "changed relevant input or ownership " + edit);
            }
            var owner = TownReady(data); var foreign = TownReady(data); TownReject(foreign, owner.GetTownProjectQuote(0), "another game with equal balances");
            TownReject(owner, null, "a missing review");
            Action<GameCore>[] invalid = {
                game => game.save.inventory = null, game => game.save.inventory = new[] { 80, 80 },
                game => game.save.inventory[0] = -1, game => game.save.coins = -1,
                game => game.save.projects = null, game => game.save.projects = new[] { 0, 0 },
                game => game.save.projects[0] = -1, game => game.save.projects[2] = 3,
                game => game.save.reputation = null, game => game.save.reputation[2] = 11,
                game => game.save.health = float.NaN, game => game.save.health = float.PositiveInfinity,
                game => game.save.health = 0, game => game.deathTime = .2f, game => game.practicing = true
            };
            for (int edit = 0; edit < invalid.Length; edit++)
            {
                var game = TownReady(data); invalid[edit](game); string before = TownState(game); TownProjectQuote quote = game.GetTownProjectQuote(0);
                Check(!quote.available && !string.IsNullOrEmpty(quote.reasonZh) && !string.IsNullOrEmpty(quote.reasonEn)
                    && TownState(game) == before, "Malformed or unsafe construction state is described without normalization or payment: " + edit);
                TownReject(game, quote, "invalid source state " + edit);
                string save = TownSave(game.save); Check(!game.BuildProject(0) && TownSave(game.save) == save, "The old entry also rejects invalid state without granting or charging");
            }
            foreach (int project in new[] { -1, 3, int.MinValue, int.MaxValue })
            {
                var game = TownReady(data); string before = TownState(game); var quote = game.GetTownProjectQuote(project);
                Check(!quote.available && quote.tier == -1 && quote.nextTier == -1 && TownState(game) == before, "Invalid project IDs do not index arrays or mutate the game");
                TownReject(game, quote, "invalid project identity");
            }
            var unrelated = TownReady(data); var valid = unrelated.GetTownProjectQuote(0);
            unrelated.save.inventory[2]++; unrelated.save.health = 19;
            Check(unrelated.CanBuildProject(valid) && unrelated.TryBuildProject(valid) && unrelated.save.health == 19,
                "Unrelated herbs and wounded HP do not invalidate a construction quote or turn payment into healing");
        }

        public static void TownProjectLegacyProgressAndCapturedActionsRemainCompatible(Campaign data)
        {
            for (int version = 1; version <= 2; version++) for (int profession = 0; profession < 3; profession++)
            {
                var original = TownReady(data, profession); original.save.projects = new[] { 1, 1, 1 };
                original.save.flags.AddRange(new[] { "core_1", "weapon_1", "weapon_2", "relic_1", "scout_saved", "watergarden_complete", "return_letter_complete" });
                original.save.mastery = new[] { 23, 64, 87 }; original.save.weaponStyles = new[] { 2, 1, 2 };
                original.save.bestRace = 155; original.save.bestFloor = 4; original.save.bounty = 1; original.save.bountyProgress = 2;
                original.PrepareSave(); SaveData old = CloneSave(original.save); old.version = version;
                var game = new GameCore(data, old); game.Start(); game.mode = "camp"; string before = TownState(game);
                // The established v1 expansion migration has no active contract;
                // v2 keeps its real accepted contract. Construction preserves
                // whichever state the existing loader produced.
                int migratedBounty = game.save.bounty, migratedProgress = game.save.bountyProgress;
                Check(migratedBounty == (version == 1 ? -1 : 1) && migratedProgress == (version == 1 ? 0 : 2),
                    "The pre-existing v1/v2 contract migration remains authoritative before any project transaction");
                for (int project = 0; project < 3; project++) game.GetTownProjectQuote(project);
                Check(TownState(game) == before, "Reading projects preserves every field after the existing v" + version + " save migration");
                TownPay(game, 1); game.PrepareSave(); var loaded = new GameCore(data, CloneSave(game.save)); loaded.Start();
                Check(loaded.save.projects[0] == 1 && loaded.save.projects[1] == 2 && loaded.save.projects[2] == 1
                    && loaded.save.coins == 400 && loaded.save.inventory[0] == 74 && loaded.save.inventory[1] == 77
                    && loaded.save.inventory[4] == 76 && loaded.save.bestRace == 155 && loaded.save.bestFloor == 4
                    && loaded.save.bounty == migratedBounty && loaded.save.bountyProgress == migratedProgress && loaded.save.mastery[2] == 87
                    && loaded.save.weaponStyles[2] == 2 && loaded.Has("watergarden_complete") && loaded.Has("return_letter_complete")
                    && loaded.save.profession == profession, "Normal save/resume preserves payment, town progress, story, identity, combat growth and independent records");
            }
            for (int weapon = 0; weapon < 3; weapon++)
            {
                var game = TownReady(data, weapon); game.save.flags.Add("weapon_1"); game.save.flags.Add("weapon_2");
                game.Close(); game.enemies.Clear(); game.save.x = 12; game.save.y = 14; game.Equip(weapon);
                var enemy = new Enemy(98970 + weapon, 0, weapon == 0 ? 13.1f : 17, 14, 500) { stun = 99 };
                game.enemies.Add(enemy); Check(game.CanStand(enemy.Position), "The accepted action's target occupies real legal town terrain");
                game.Attack(new Point(1, 0)); ActorAction action = game.playerAction;
                Check(action != null && !action.released, "A genuine accepted sword, arrow or starfire preparation exists before construction review");
                game.mode = "camp"; ImpactProfile impact = action.impact; float damage = action.damage, elapsed = action.elapsed;
                int seed = game.save.seed; float health = game.save.health, energy = game.save.energy;
                float attack = game.attackCooldown, skill = game.skillCooldown, dash = game.dashCooldown;
                for (int read = 0; read < 5; read++) game.GetTownProjectQuote(0);
                TownPay(game, 0);
                Check(ReferenceEquals(game.playerAction, action) && ReferenceEquals(action.impact, impact) && action.damage == damage
                    && action.elapsed == elapsed && game.save.seed == seed && game.save.health == health && game.save.energy == energy
                    && game.attackCooldown == attack && game.skillCooldown == skill && game.dashCooldown == dash,
                    "Review and payment never cancel, advance, heal or recapture an already accepted attack");
                game.Close(); for (int frame = 0; frame < 400 && enemy.hp == enemy.maxHp; frame++) game.Tick(.01f, new Point());
                Check(action.released && enemy.hp < enemy.maxHp && ReferenceEquals(action.impact, impact) && action.damage == damage,
                    "The unchanged sword/arrow/starfire preparation completes its genuine physical contact after construction");
            }
        }
    }
}
