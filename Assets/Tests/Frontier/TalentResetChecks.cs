using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore ResetBuild(Campaign data, int profession = 0)
        {
            var game = Fresh(data, profession); game.SkipTutorial();
            // An earned equipment fixture establishes the existing maximum HP.
            // The six refundable ranks are then bought through real Learn calls.
            game.save.upgrades = 2; game.save.maxHealth = 140 + (profession == 0 ? 20 : 0);
            game.save.health = game.save.maxHealth; game.save.skillPoints = 14;
            foreach (int talent in new[] { 0, 0, 1, 1, 1, 2 }) game.Learn(talent);
            game.save.coins = 73; game.mode = "talents"; return game;
        }
        private static void ResetAppend(StringBuilder text, object value)
        {
            if (value == null) { text.Append("null;"); return; }
            if (value is string) { string s = (string)value; text.Append(s.Length).Append(':').Append(s).Append(';'); return; }
            if (value.GetType().IsPrimitive || value.GetType().IsEnum)
            { text.Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append(';'); return; }
            if (value is IEnumerable)
            { text.Append('['); foreach (object item in (IEnumerable)value) ResetAppend(text, item); text.Append(']'); return; }
            text.Append('{'); foreach (FieldInfo field in value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            { text.Append(field.Name).Append('='); ResetAppend(text, field.GetValue(value)); } text.Append('}');
        }
        private static string ResetSaveState(SaveData save, bool protectOnly = false)
        {
            var text = new StringBuilder();
            foreach (FieldInfo field in typeof(SaveData).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (protectOnly && Array.IndexOf(new[] { "coins", "skillPoints", "talents", "health", "maxHealth", "moments" }, field.Name) >= 0) continue;
                text.Append(field.Name).Append('='); ResetAppend(text, field.GetValue(save));
            }
            return text.ToString();
        }
        private static string ResetUnchanged(GameCore game)
        {
            return ResetSaveState(game.save) + "/" + game.revision + "/" + game.mode + "/" + game.WorldGeneration
                + "/" + game.effects.Count + "/" + game.sounds.Count + "/" + game.toast + "/" + game.sound;
        }
        private static void ResetReject(GameCore game, TalentResetQuote quote, string label)
        {
            string before = ResetUnchanged(game);
            Check(!game.CanResetTalents(quote) && !game.TryResetTalents(quote) && ResetUnchanged(game) == before,
                "A rejected reviewed reset is entirely atomic: " + label);
        }

        public static void TalentResetQuotesExposeExactThreeClassCostsRefundsAndHealthWithoutMutation(Campaign data)
        {
            Check(typeof(TalentResetQuote).IsSealed, "A reset review is sealed instead of exposing a mutable derived transaction");
            foreach (FieldInfo field in typeof(TalentResetQuote).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Check(field.IsInitOnly, "Every public quoted value is immutable: " + field.Name);
            for (int profession = 0; profession < 3; profession++) foreach (int healthCase in new[] { 0, 1, 2 })
            {
                var game = ResetBuild(data, profession); float baseMaximum = 140 + (profession == 0 ? 20 : 0);
                game.save.health = healthCase == 0 ? 37 : healthCase == 1 ? baseMaximum : baseMaximum + 17;
                string before = ResetUnchanged(game); TalentResetQuote quote = game.GetTalentResetQuote();
                Check(quote.available && game.CanResetTalents(quote) && quote.cost == 20 && quote.coinsBefore == 73
                    && quote.edge == 2 && quote.resolve == 3 && quote.agility == 1 && quote.refund == 6
                    && quote.pointsBefore == 8 && quote.pointsAfter == 14,
                    "The review exposes the original fee, all three paid ranks and exact refund for class " + profession);
                Check(quote.maxHealthBefore == baseMaximum + 36 && quote.maxHealthAfter == baseMaximum
                    && quote.healthBefore == game.save.health && quote.healthAfter == Math.Min(game.save.health, baseMaximum),
                    "The review shows actual HP clamping without pretending that a reset heals the hero");
                for (int query = 0; query < 8; query++)
                    Check(game.GetTalentResetQuote().available && game.CanResetTalents(quote), "Reading a valid review remains available");
                Check(ResetUnchanged(game) == before, "Repeated read-only review does not spend, heal, allocate, log, sound or advance RNG/time");
            }
        }

        public static void TalentResetTransactionsPayOnceKeepOriginalWrapperAndTrainingAccess(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++)
            {
                var game = ResetBuild(data, profession); game.save.health = game.save.maxHealth - 3;
                TalentResetQuote quote = game.GetTalentResetQuote(); string protectedSave = ResetSaveState(game.save, true);
                int moments = game.save.moments.Count;
                Check(game.TryResetTalents(quote) && game.save.coins == 53 && game.save.skillPoints == 14
                    && game.save.talents[0] == 0 && game.save.talents[1] == 0 && game.save.talents[2] == 0
                    && game.save.maxHealth == quote.maxHealthAfter && game.save.health == quote.healthAfter,
                    "One reviewed confirmation performs exactly the original 20-coin refund transaction");
                Check(ResetSaveState(game.save, true) == protectedSave && game.save.moments.Count == Math.Min(8, moments + 1)
                    && game.save.moments[game.save.moments.Count - 1].en == "Build reconsidered: refunded 6 talent points.",
                    "Only the original reset economics, HP and one existing journal moment change");
                ResetReject(game, quote, "the consumed confirmation");
                TalentResetQuote empty = game.GetTalentResetQuote(); Check(!empty.available, "A fresh review does not sell a second zero-rank reset");
                ResetReject(game, empty, "no remaining invested ranks");
                string paid = ResetUnchanged(game); game.ResetTalents(); Check(ResetUnchanged(game) == paid, "The mature void entry also refuses an empty second purchase");

                var legacy = ResetBuild(data, profession); legacy.save.health = 42; legacy.ResetTalents();
                Check(legacy.save.coins == 53 && legacy.save.skillPoints == 14 && legacy.save.health == 42
                    && legacy.save.maxHealth == 140 + (profession == 0 ? 20 : 0),
                    "Existing ResetTalents callers retain the original fee, refund and wounded HP outcome");
            }
            var capped = ResetBuild(data); capped.save.skillPoints = 93;
            Check(capped.GetTalentResetQuote().pointsAfter == 99 && capped.TryResetTalents(capped.GetTalentResetQuote()),
                "Exactly 99 available points remains a valid full refund rather than being discarded");
            capped.PrepareSave(); var resumed = new GameCore(data, CloneSave(capped.save));
            Check(resumed.save.skillPoints == 99 && resumed.save.coins == 53, "A cap-boundary refund survives the existing loader unchanged");

            var field = ResetBuild(data); field.Close(); field.Enter(1); field.mode = "talents";
            Check(field.TryResetTalents(field.GetTalentResetQuote()), "The original field-accessible reset does not acquire a new town or mentor restriction");
            foreach (bool range in new[] { false, true })
            {
                var game = ResetBuild(data); game.save.flags.Add("core_1"); game.Close();
                WorldObject mentor = Object(game, "mentor"); game.save.x = mentor.x; game.save.y = mentor.y + 1;
                Check(range ? game.StartRange(0) : game.StartSparring(), "An actual pre-existing safe training session starts");
                int actors = game.enemies.Count; SparringReturnRecord returnRecord = game.save.sparringReturn;
                Check(game.TryResetTalents(game.GetTalentResetQuote()) && game.practicing && game.enemies.Count == actors
                    && object.ReferenceEquals(game.save.sparringReturn, returnRecord) && (range ? game.RangeActive : game.SparringActive),
                    "Resetting preserves the actual " + (range ? "loadout range" : "sparring") + " and its return ownership");
                game.EndPractice();
            }
        }

        public static void TalentResetRejectsStaleOwnersDataWorldsAndInvalidRefundsAtomically(Campaign data)
        {
            Action<GameCore>[] edits = {
                game => game.save.coins++, game => game.save.skillPoints++,
                game => game.save.talents[0]++, game => game.save.talents[1]--, game => game.save.talents[2]++,
                game => game.save.health--, game => game.save.maxHealth++, game => game.save.profession = 1,
                game => game.save.upgrades++, game => game.save = CloneSave(game.save),
                game => game.Enter(0), game => game.Load(game.save), game => game.deathTime = .2f
            };
            for (int edit = 0; edit < edits.Length; edit++)
            {
                var game = ResetBuild(data); TalentResetQuote quote = game.GetTalentResetQuote(); edits[edit](game);
                ResetReject(game, quote, "changed quote input/world/save " + edit);
            }
            var owner = ResetBuild(data); var foreign = ResetBuild(data);
            ResetReject(foreign, owner.GetTalentResetQuote(), "another live GameCore with equal values");
            ResetReject(owner, null, "a missing quote");

            Action<GameCore>[] invalid = {
                game => game.save.talents = null, game => game.save.talents = new[] { 1, 2 },
                game => game.save.talents = new[] { 1, 2, 3, 4 }, game => game.save.talents[0] = -1,
                game => game.save.talents[1] = 6, game => game.save.talents[2] = int.MaxValue,
                game => game.save.talents = new int[3], game => game.save.skillPoints = 94,
                game => game.save.skillPoints = 100, game => game.save.skillPoints = -1,
                game => game.save.skillPoints = int.MaxValue, game => game.save.coins = 19,
                game => game.save.coins = -1, game => game.save.health = float.NaN,
                game => game.save.health = float.PositiveInfinity, game => game.save.health = 0,
                game => game.save.health = -1, game => game.save.maxHealth = float.NaN,
                game => game.save.maxHealth = float.PositiveInfinity, game => game.save.maxHealth = 0,
                game => game.deathTime = .2f
            };
            for (int edit = 0; edit < invalid.Length; edit++)
            {
                var game = ResetBuild(data); invalid[edit](game); string before = ResetUnchanged(game);
                TalentResetQuote quote = game.GetTalentResetQuote();
                Check(!quote.available && !string.IsNullOrEmpty(quote.reasonZh) && !string.IsNullOrEmpty(quote.reasonEn)
                    && ResetUnchanged(game) == before, "Malformed/unsupported refund explains its refusal without normalizing or spending: " + edit);
                ResetReject(game, quote, "invalid economic/HP/rank input " + edit);
                game.ResetTalents(); Check(ResetUnchanged(game) == before, "The legacy entry also rejects invalid state without charging or swallowing points: " + edit);
            }
            var shortCoins = ResetBuild(data); shortCoins.save.coins = 19; TalentResetQuote unavailable = shortCoins.GetTalentResetQuote();
            shortCoins.save.coins = 73; ResetReject(shortCoins, unavailable, "an originally disabled quote after a later deposit");
            Check(shortCoins.TryResetTalents(shortCoins.GetTalentResetQuote()), "A deliberate fresh affordable review remains usable after a refusal");
        }

        public static void TalentResetRetainsLegacyProgressAndCapturedCombatActions(Campaign data)
        {
            for (int version = 1; version <= 2; version++) for (int profession = 0; profession < 3; profession++)
            {
                var original = ResetBuild(data, profession);
                foreach (string flag in new[] { "core_1", "weapon_1", "weapon_2", "technique_" + profession,
                    "relic_1", "relic_4", "scout_saved", "return_letter_complete", "archive_complete" })
                    if (!original.Has(flag)) original.save.flags.Add(flag);
                original.save.technique = 1; original.save.weaponStyles = new[] { 2, 1, 2 };
                original.save.mastery = new[] { 23, 64, 87 }; original.save.relics = new[] { 1, 4 }; original.save.relicRank = 2;
                original.save.projects = new[] { 2, 1, 2 }; original.save.relationships = new[] { 3, 4, 5 };
                original.save.reputation = new[] { 2, 3, 4 }; original.save.shards = 43; original.save.tea = 7;
                original.mode = "plans"; Check(original.SaveBattlePlan(0), "A real earned preparation slot is present before migration/reset");
                original.PrepareSave(); SaveData old = CloneSave(original.save); old.version = version;
                var game = new GameCore(data, old); game.Start(); game.mode = "talents";
                // The mature v1 migration predates equipped relic slots and
                // intentionally starts them empty. Owned relic flags and saved
                // plans survive; the reset must preserve this migrated state.
                int[] equippedRelics = version == 1 ? new[] { -1, -1 } : new[] { 1, 4 };
                Check(game.save.relics[0] == equippedRelics[0] && game.save.relics[1] == equippedRelics[1]
                    && game.Has("relic_1") && game.Has("relic_4") && game.BattlePlanAt(0).relicFirst == 1
                    && game.BattlePlanAt(0).relicSecond == 4,
                    "Version " + version + " follows the existing equipped-relic migration while retaining ownership and saved plans");
                string protectedSave = ResetSaveState(game.save, true); TalentResetQuote quote = game.GetTalentResetQuote();
                Check(quote.available && game.TryResetTalents(quote) && ResetSaveState(game.save, true) == protectedSave,
                    "Version " + version + " retains every non-reset save field, earned technique/stance/mastery/relic/story and preparation");
                game.PrepareSave(); var loaded = new GameCore(data, CloneSave(game.save)); loaded.Start();
                Check(loaded.save.skillPoints == 14 && loaded.save.coins == 53 && loaded.save.talents[0] == 0
                    && loaded.save.talents[1] == 0 && loaded.save.talents[2] == 0 && loaded.save.maxHealth == quote.maxHealthAfter
                    && loaded.save.technique == 1 && loaded.save.weaponStyles[2] == 2 && loaded.save.mastery[2] == 87
                    && loaded.save.relics[0] == equippedRelics[0] && loaded.save.relics[1] == equippedRelics[1]
                    && loaded.Has("relic_1") && loaded.Has("relic_4") && loaded.save.relicRank == 2
                    && loaded.BattlePlanAt(0) != null && loaded.BattlePlanAt(0).relicFirst == 1 && loaded.BattlePlanAt(0).relicSecond == 4,
                    "The normal loader round-trip retains the fully refunded points and every independent learned/equipped system");
            }
            for (int weapon = 0; weapon < 3; weapon++)
            {
                var game = ResetBuild(data, weapon); game.save.flags.Add("weapon_1"); game.save.flags.Add("weapon_2");
                game.Close(); game.Enter(1); game.enemies.Clear(); game.save.x = 12; game.save.y = 14;
                game.Equip(weapon); game.save.health = 61; game.save.energy = 83; game.invulnerable = 0;
                var enemy = new Enemy(98990 + weapon, 0, weapon == 0 ? 13.1f : 17, 14, 500) { stun = 99 };
                game.enemies.Add(enemy); Check(game.CanStand(enemy.Position), "The real contact target stands on legal authored terrain");
                game.Attack(new Point(1, 0)); ActorAction action = game.playerAction;
                Check(action != null && !action.released && action.kind == "attack", "A real accepted basic preparation exists before reset");
                ImpactProfile impact = action.impact; float damage = action.damage, critical = impact.criticalChance;
                float cooldown = game.attackCooldown, skill = game.skillCooldown, dash = game.dashCooldown, energy = game.save.energy;
                int seed = game.save.seed; Point direction = action.direction;
                Check(game.TryResetTalents(game.GetTalentResetQuote()) && object.ReferenceEquals(game.playerAction, action)
                    && object.ReferenceEquals(action.impact, impact) && action.damage == damage && impact.criticalChance == critical
                    && !action.released && action.elapsed == 0 && (action.direction - direction).Length == 0
                    && game.attackCooldown == cooldown && game.skillCooldown == skill && game.dashCooldown == dash
                    && game.save.energy == energy && game.save.seed == seed && game.save.health == 61,
                    "A reviewed refund cannot cancel/recapture the paid preparation, alter its aim, advance RNG or change live cooldowns/energy");
                for (int frame = 0; frame < 400 && enemy.hp == enemy.maxHp; frame++) game.Tick(.01f, new Point());
                Check(action.released && enemy.hp < enemy.maxHp && object.ReferenceEquals(action.impact, impact)
                    && action.damage == damage && impact.criticalChance == critical,
                    "The originally captured sword/arrow/starfire completes its genuine unchanged contact after reset: " + weapon);
            }
        }
    }
}
