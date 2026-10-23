using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore SmithReady(Campaign data, int profession = 0)
        {
            var game = Fresh(data, profession); game.SkipTutorial();
            game.save.inventory = new[] { 40, 40, 7, 3, 40, 2, 4, 3 };
            game.save.coins = 83; game.save.health = 37; game.save.energy = 67;
            return game;
        }
        private static void SmithAppend(StringBuilder text, object value)
        {
            if (value == null) { text.Append("null;"); return; }
            if (value is string) { string s = (string)value; text.Append(s.Length).Append(':').Append(s).Append(';'); return; }
            if (value.GetType().IsPrimitive || value.GetType().IsEnum)
            { text.Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append(';'); return; }
            if (value is IEnumerable)
            { text.Append('['); foreach (object item in (IEnumerable)value) SmithAppend(text, item); text.Append(']'); return; }
            text.Append('{'); foreach (FieldInfo field in value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            { text.Append(field.Name).Append('='); SmithAppend(text, field.GetValue(value)); } text.Append('}');
        }
        private static string SmithSaveState(SaveData save)
        { var text = new StringBuilder(); SmithAppend(text, save); return text.ToString(); }
        private static string SmithState(GameCore game)
        { return SmithSaveState(game.save) + "/" + game.revision + "/" + game.mode + "/" + game.sound + "/" + game.sounds.Count + "/" + game.toast; }
        private static int SmithChoice(GameCore game, int recipe)
        {
            Check(game.mode == "dialog" && game.conversation != null, "The original blacksmith conversation is available");
            string action = new[] { "upgrade", "bow", "staff" }[recipe];
            Check(game.conversation.choices.Length >= 3 && game.conversation.choices[recipe].action == action,
                "Original blacksmith actions preserve their first three numbered positions");
            return recipe;
        }
        private static void SmithTalk(GameCore game)
        {
            game.Close(); WorldObject smith = Object(game, "forge");
            game.save.x = smith.x; game.save.y = smith.y + 1; game.Talk(smith);
        }
        private static void SmithCosts(ForgeQuote quote, int wood, int crystals, int parts, string label)
        { Check(quote.wood == wood && quote.crystals == crystals && quote.parts == parts, "Exact original material prices: " + label); }

        public static void SmithQuotesExposeRecipesBenefitsAndReadOnlySnapshots(Campaign data)
        {
            Check(typeof(ForgeQuote).IsSealed, "Blacksmith previews cannot acquire mutable derived authority");
            foreach (FieldInfo field in typeof(ForgeQuote).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Check(field.IsInitOnly, "Every public forge quote field is immutable: " + field.Name);
            for (int profession = 0; profession < 3; profession++) for (int tier = 0; tier <= 5; tier++)
            {
                var game = SmithReady(data, profession); game.save.upgrades = tier;
                game.save.talents = new[] { 1, 2, 1 };
                game.save.maxHealth = 124 + tier * 20 + (profession == 0 ? 20 : 0);
                for (int recipe = 0; recipe < 3; recipe++)
                {
                    string before = SmithState(game); ForgeQuote quote = game.GetForgeQuote(recipe);
                    bool owned = recipe > 0 && game.Has("weapon_" + recipe), capped = recipe == 0 && tier == 5;
                    Check(quote.recipe == recipe && quote.owned == owned && quote.capped == capped && quote.available == !capped,
                        "Every class and equipment tier reports actual recipe ownership and the five-tier cap");
                    Check(quote.levelBefore == tier && quote.levelAfter == (recipe == 0 && !capped ? tier + 1 : tier)
                        && quote.damageBonusBefore == tier * 4 && quote.damageBonusAfter == quote.levelAfter * 4,
                        "Equipment's own base damage bonus is distinguished from class/weapon/talent total damage");
                    Check(quote.healthBefore == 37 && quote.maxHealthBefore == game.save.maxHealth
                        && quote.maxHealthAfter == game.save.maxHealth + (recipe == 0 && !capped ? 20 : 0)
                        && quote.healthAfter == (recipe == 0 && !capped ? quote.maxHealthAfter : 37),
                        "Only an actual eligible upgrade previews its original full heal and twenty maximum HP");
                    if (!capped) SmithCosts(quote, owned ? 0 : recipe == 0 ? 3 : recipe == 1 ? 4 : 0,
                        owned ? 0 : recipe == 0 ? 2 : recipe == 2 ? 4 : 0,
                        owned ? 0 : recipe == 2 ? 3 : 2, "class " + profession + ", recipe " + recipe);
                    Check(SmithState(game) == before, "Reading a forge preview leaves every save field, revision, mode, sound and notification intact");
                    game.save.coins++; Check(quote.healthBefore == 37 && quote.levelBefore == tier && quote.available == !capped,
                        "An already returned quote remains a read-only snapshot rather than changing with the game"); game.save.coins--;
                }
                foreach (int recipe in new[] { -1, 3, int.MinValue, int.MaxValue })
                {
                    string before = SmithState(game); ForgeQuote quote = game.GetForgeQuote(recipe);
                    Check(!quote.available && !string.IsNullOrEmpty(quote.reasonZh) && !string.IsNullOrEmpty(quote.reasonEn)
                        && SmithState(game) == before, "Invalid recipes explain their refusal in both languages without changing the game");
                }
            }
            for (int recipe = 0; recipe < 3; recipe++)
            {
                int[] prices = recipe == 0 ? new[] { 3, 2, 2 } : recipe == 1 ? new[] { 4, 0, 2 } : new[] { 0, 4, 3 };
                int[] slots = { 0, 1, 4 }; string[] names = { "timber", "crystal", "part" };
                for (int resource = 0; resource < 3; resource++) if (prices[resource] > 0)
                {
                    var game = SmithReady(data); game.save.inventory[slots[resource]] = prices[resource] - 1;
                    string before = SmithState(game); ForgeQuote quote = game.GetForgeQuote(recipe);
                    MatchCollection numbers = Regex.Matches(quote.reasonEn ?? "", "[0-9]+");
                    Check(!quote.available && quote.reasonEn.ToLowerInvariant().Contains(names[resource])
                        && numbers.Count == 1 && numbers[0].Value == "1" && !string.IsNullOrEmpty(quote.reasonZh),
                        "One missing material is identified by its exact deficit, rather than the full price or an unrelated resource");
                    Check(SmithState(game) == before, "Explaining shortfalls does not spend or normalize inventory");
                }
            }
        }

        public static void SmithTransactionsKeepFiveUpgradeTiersAndFreeOwnedEquipment(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++)
            {
                var game = SmithReady(data, profession); int[] inventory = (int[])game.save.inventory.Clone();
                for (int tier = 0; tier < 5; tier++)
                {
                    game.save.health = 31; float maximum = game.save.maxHealth; int coins = game.save.coins;
                    SmithTalk(game); ForgeQuote quote = game.GetForgeQuote(0); game.Choose(SmithChoice(game, 0));
                    Check(game.mode == "world" && game.save.upgrades == tier + 1 && game.save.health == maximum + 20
                        && game.save.maxHealth == maximum + 20 && game.save.coins == coins && game.save.energy == 67,
                        "The numbered original upgrade keeps its exact tier, full-heal, twenty-HP and zero-coin economics");
                    Check(game.save.health == quote.healthAfter && game.save.maxHealth == quote.maxHealthAfter,
                        "The completed upgrade matches its previously displayed actual health outcome");
                    for (int item = 0; item < inventory.Length; item++)
                        Check(game.save.inventory[item] == inventory[item] - (tier + 1) * (item == 0 ? 3 : item == 1 || item == 4 ? 2 : 0),
                            "Five upgrades consume only their original timber, crystals and parts");
                }
                SmithTalk(game); string capped = SmithSaveState(game.save); int revision = game.revision; Conversation dialogue = game.conversation;
                game.Choose(SmithChoice(game, 0));
                Check(game.mode == "dialog" && ReferenceEquals(game.conversation, dialogue) && SmithSaveState(game.save) == capped
                    && game.revision == revision, "A capped sixth purchase is disabled and preserves the live conversation without taking materials");
            }
            var crafter = SmithReady(data);
            foreach (int recipe in new[] { 1, 2 })
            {
                int[] before = (int[])crafter.save.inventory.Clone(); int coins = crafter.save.coins;
                SmithTalk(crafter); ForgeQuote quote = crafter.GetForgeQuote(recipe); crafter.Choose(SmithChoice(crafter, recipe));
                Check(crafter.Has("weapon_" + recipe) && crafter.save.weapon == recipe && crafter.save.coins == coins,
                    "The original bow/staff numbered craft grants and equips precisely its weapon without a new fee");
                for (int item = 0; item < before.Length; item++)
                    Check(crafter.save.inventory[item] == before[item] - (item == 0 ? quote.wood : item == 1 ? quote.crystals : item == 4 ? quote.parts : 0),
                        "Crafting spends the exact originally quoted recipe and preserves every unrelated item");
                Array.Clear(crafter.save.inventory, 0, crafter.save.inventory.Length); crafter.save.coins = 0;
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    crafter.Equip(0); SmithTalk(crafter); SaveData expected = CloneSave(crafter.save); expected.weapon = recipe;
                    ForgeQuote owned = crafter.GetForgeQuote(recipe); SmithCosts(owned, 0, 0, 0, "owned weapon");
                    Check(owned.owned && owned.available, "Already-owned weapons remain freely equippable with no stock or coins");
                    crafter.Choose(SmithChoice(crafter, recipe));
                    Check(crafter.save.weapon == recipe && crafter.save.coins == 0 && Array.TrueForAll(crafter.save.inventory, value => value == 0),
                        "Repeated free equipment cannot charge a second recipe or manufacture materials");
                    Check(SmithSaveState(crafter.save) == SmithSaveState(expected),
                        "Free owned-weapon equipment changes only the equipped weapon, preserving health, energy and every permanent save field");
                }
                // Restore fixture stock so the next distinct weapon still uses a genuine paid recipe.
                crafter.save.inventory = new[] { 40, 40, 7, 3, 40, 2, 4, 3 }; crafter.save.coins = 83;
            }
        }

        public static void SmithChoicesRecheckLiveMaterialsAndKeepRejectedDialogue(Campaign data)
        {
            for (int recipe = 0; recipe < 3; recipe++)
            {
                var game = SmithReady(data); SmithTalk(game); int choice = SmithChoice(game, recipe);
                ForgeQuote displayed = game.GetForgeQuote(recipe); Check(displayed.available, "The original displayed order is affordable before stock changes");
                int slot = recipe == 2 ? 1 : 0; int cost = recipe == 0 ? 3 : 4; game.save.inventory[slot] = cost - 1;
                Check(!game.DialogueChoiceEnabled(game.conversation.choices[choice].action) && !game.GetForgeQuote(recipe).available
                    && displayed.available, "Live choice availability rechecks changed materials rather than trusting an old preview");
                string before = SmithSaveState(game.save); int revision = game.revision; Conversation dialogue = game.conversation;
                game.Choose(choice);
                Check(SmithSaveState(game.save) == before && game.revision == revision && game.mode == "dialog"
                    && ReferenceEquals(game.conversation, dialogue), "Rejected stale orders take no partial materials, grant nothing and retain the conversation");
                game.save.inventory[slot] = cost;
                Check(game.DialogueChoiceEnabled(game.conversation.choices[choice].action), "A later genuine deposit makes the same existing option usable again");
                game.Choose(choice); Check(game.mode == "world" && game.save.inventory[slot] == 0
                    && (recipe == 0 ? game.save.upgrades == 1 : game.Has("weapon_" + recipe)),
                    "A fresh affordable order charges exactly once through the same mature numbered action");
            }
            var local = SmithReady(data); WorldObject forge = Object(local, "forge");
            local.save.x = forge.x; local.save.y = forge.y + 1; string save = SmithSaveState(local.save);
            Check(local.ShowForgeQuotes() && local.mode == "dialog" && local.conversation != null && SmithSaveState(local.save) == save,
                "Nearby world interaction can open the read-only quotation dialogue without changing progression");
            Check(local.ShowForgeQuotes() && SmithSaveState(local.save) == save, "The nearby dialog can refresh its quotation without purchase");
            local.Close(); local.save.x = 18; local.save.y = 15; string distant = SmithState(local);
            Check(!local.ShowForgeQuotes() && SmithState(local) == distant, "Remote quotation requests cannot teleport to or impersonate the blacksmith");
            foreach (bool supperReady in new[] { false, true })
            {
                var hostGame = SmithReady(data);
                if (supperReady) foreach (string flag in new[] { "ally_1", "ally_2", "ally_3" }) hostGame.save.flags.Add(flag);
                SmithTalk(hostGame); Conversation original = hostGame.conversation;
                int review = Array.FindIndex(original.choices, value => value.action == "forge_quotes");
                Check(review == 5 && original.choices.Length == (supperReady ? 7 : 6)
                    && (!supperReady || original.choices[6].action == "watergarden_offer")
                    && Array.Exists(original.choices, value => value.action == "close")
                    && Array.Exists(original.choices, value => value.action == (supperReady ? "supper_prep_2" : "ally3")),
                    "Review retains its numbered slot after goodbye and commission/hearth; the earned water follow-up appends afterward");
                string before = SmithSaveState(hostGame.save); hostGame.Choose(review);
                Check(hostGame.mode == "dialog" && hostGame.conversation.choices.Length == 4
                    && hostGame.conversation.choices[3].action == "forge_back" && SmithSaveState(hostGame.save) == before,
                    "The actual numbered workshop-review action opens a non-paying quotation with a return action");
                for (int recipe = 0; recipe < 3; recipe++) SmithChoice(hostGame, recipe);
                hostGame.Choose(3);
                Check(hostGame.mode == "dialog" && hostGame.conversation.speaker == original.speaker
                    && hostGame.conversation.text == original.text && hostGame.conversation.choices.Length == original.choices.Length
                    && SmithSaveState(hostGame.save) == before, "Returning restores the same mature blacksmith story and available actions without side effects");
                for (int index = 0; index < original.choices.Length; index++)
                    Check(hostGame.conversation.choices[index].action == original.choices[index].action,
                        "Commission and hearth action order remains intact after reviewing prices");
            }
        }

        public static void SmithQuotesPreserveLegacyProgressAndCapturedCombat(Campaign data)
        {
            for (int version = 1; version <= 2; version++)
            {
                var original = SmithReady(data); original.save.upgrades = 2; original.save.maxHealth = 160;
                foreach (string flag in new[] { "core_1", "weapon_1", "weapon_2", "relic_1", "relic_4", "technique_0", "return_letter_complete", "archive_complete" })
                    if (!original.Has(flag)) original.save.flags.Add(flag);
                original.save.relics = new[] { 1, 4 }; original.save.relicRank = 2; original.save.technique = 1;
                original.save.weaponStyles = new[] { 2, 1, 2 }; original.save.mastery = new[] { 23, 64, 87 };
                original.mode = "plans"; Check(original.SaveBattlePlan(0), "A genuine pre-existing earned battle plan is retained");
                original.PrepareSave(); SaveData old = CloneSave(original.save); old.version = version;
                var game = new GameCore(data, old); game.Start(); string migrated = SmithState(game);
                for (int recipe = 0; recipe < 3; recipe++) game.GetForgeQuote(recipe);
                Check(SmithState(game) == migrated, "Reading quotes retains every field after the mature v" + version + " migration");
                int relicFirst = game.save.relics[0], relicSecond = game.save.relics[1];
                SmithTalk(game); game.Choose(SmithChoice(game, 0)); game.PrepareSave();
                var resumed = new GameCore(data, CloneSave(game.save)); resumed.Start();
                Check(resumed.save.upgrades == 3 && resumed.save.maxHealth == 180 && resumed.save.health == 180
                    && resumed.save.inventory[0] == 37 && resumed.save.inventory[1] == 38 && resumed.save.inventory[4] == 38
                    && resumed.save.coins == 83 && resumed.save.technique == 1 && resumed.save.weaponStyles[2] == 2
                    && resumed.save.mastery[2] == 87 && resumed.save.relicRank == 2 && resumed.save.relics[0] == relicFirst
                    && resumed.save.relics[1] == relicSecond && resumed.Has("relic_1") && resumed.Has("relic_4")
                    && resumed.Has("return_letter_complete") && resumed.Has("archive_complete")
                    && resumed.BattlePlanAt(0) != null && resumed.BattlePlanAt(0).relicFirst == 1 && resumed.BattlePlanAt(0).relicSecond == 4,
                    "The existing save round-trip preserves the paid upgrade, migrated equipment and independent story/technique/style/mastery/relic/plan progress");
            }
            for (int weapon = 0; weapon < 3; weapon++)
            {
                var game = SmithReady(data, weapon); game.Close(); game.Enter(1); game.enemies.Clear();
                game.save.x = 12; game.save.y = 14; game.Equip(weapon); game.invulnerable = 0;
                var enemy = new Enemy(98600 + weapon, 0, weapon == 0 ? 13.1f : 17, 14, 500) { stun = 99 };
                game.enemies.Add(enemy); Check(game.CanStand(enemy.Position), "The captured-attack target occupies genuine legal terrain");
                game.Attack(new Point(1, 0)); ActorAction action = game.playerAction;
                Check(action != null && !action.released, "A genuine sword/bow/starfire preparation exists before reading a quotation");
                string before = SmithState(game); float damage = action.damage, elapsed = action.elapsed;
                ImpactProfile impact = action.impact; int seed = game.save.seed;
                for (int repeat = 0; repeat < 8; repeat++) for (int recipe = 0; recipe < 3; recipe++) game.GetForgeQuote(recipe);
                Check(SmithState(game) == before && ReferenceEquals(game.playerAction, action) && ReferenceEquals(action.impact, impact)
                    && action.damage == damage && action.elapsed == elapsed && game.save.seed == seed,
                    "Repeated read-only preview cannot cancel, reprice, advance or recapture an accepted combat action");
                for (int frame = 0; frame < 400 && enemy.hp == enemy.maxHp; frame++) game.Tick(.01f, new Point());
                Check(action.released && enemy.hp < enemy.maxHp && ReferenceEquals(action.impact, impact) && action.damage == damage,
                    "The original captured sword/arrow/starfire still completes physical contact with unchanged damage");
            }
        }
    }
}
