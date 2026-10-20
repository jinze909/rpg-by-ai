using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Starfall.Tests
{
    // Floor clearance is staged at the existing gate, but stacks, descents,
    // banking, healing and extraction use the actual production transactions.
    // These are deterministic Core checks, not native Unity playtest evidence.
    public static partial class CoreChecks
    {
        private static GameCore AbyssPlanRun(Campaign data, int profession = 0, int risk = 0, int covenant = 1)
        {
            var game = Fresh(data, profession); game.SkipTutorial(); game.save.stage = 3;
            game.save.frontierEnding = covenant; game.StartAbyss(risk);
            Check(game.InAbyss && game.save.abyssFloor == 1 && game.save.weapon == profession,
                "An earned expedition begins with the unchanged class starter on floor one");
            return game;
        }
        private static void AbyssPlanGate(GameCore game)
        {
            game.Close(); game.enemies.Clear();
            game.Talk(Object(game, "abyssGate"));
            Check(game.mode == "dialog" && game.Has("abyss_clear_" + game.save.abyssFloor),
                "The original gate records staged clearance before offering the real choices");
        }
        private static int AbyssPlanChoice(GameCore game, string action)
        {
            Check(game.conversation != null, "An actual gate dialogue owns the choice");
            for (int index = 0; index < game.conversation.choices.Length; index++)
                if (game.conversation.choices[index].action == action) return index;
            throw new Exception("Missing actual abyss choice " + action);
        }
        private static void AbyssPlanChoose(GameCore game, string action)
        { game.Choose(AbyssPlanChoice(game, action)); }
        private static GameCore AbyssPlanCapped(Campaign data, int boon, int profession = 0, int risk = 0)
        {
            var game = AbyssPlanRun(data, profession, risk);
            for (int floor = 1; floor <= 4; floor++)
            {
                AbyssPlanGate(game); AbyssPlanChoose(game, "boon" + boon);
                Check(game.save.abyssFloor == floor + 1 && game.save.boons[boon] == floor,
                    "Four stacks are earned by four actual descents, without injecting boon flags");
            }
            return game;
        }
        private static string AbyssPlanState(GameCore game)
        {
            var text = new StringBuilder();
            foreach (FieldInfo field in typeof(SaveData).GetFields(BindingFlags.Instance | BindingFlags.Public))
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
        private static void AbyssPlanNear(float actual, float expected, string message)
        { Check(Math.Abs(actual - expected) < .0002f, message + ": expected " + expected + ", observed " + actual); }

        public static void AbyssPlanningFourEarnedStacksRejectFifthBeforeBankHealOrDescent(Campaign data)
        {
            for (int boon = 0; boon < 3; boon++)
            {
                var game = AbyssPlanCapped(data, boon, boon, boon); game.save.runCoins = 17;
                game.save.runShards = 3; game.save.runXp = 7; game.save.health = 11; game.save.energy = 13;
                AbyssPlanGate(game); string before = AbyssPlanState(game); Conversation menu = game.conversation;
                Check(AbyssPlanChoice(game, "boon" + boon) == boon && AbyssPlanChoice(game, "abyssBank") == 3,
                    "The original three boon slots and fourth bank slot retain their actions");
                Check(!game.AbyssBoonChoiceEnabled("boon" + boon), "A real four-stack boon cannot advertise another gain");
                AbyssPlanChoose(game, "boon" + boon);
                Check(game.mode == "dialog" && game.conversation == menu && AbyssPlanState(game) == before,
                    "Choosing a capped row keeps the original decision open without banking, healing or descending");
                game.TakeBoon(boon);
                Check(AbyssPlanState(game) == before, "Direct callbacks also reject a fifth boon before any transaction");
                int alternative = (boon + 1) % 3, coins = game.save.coins, shards = game.save.shards, xp = game.save.xp;
                Check(game.AbyssBoonChoiceEnabled("boon" + alternative), "Another genuine blessing remains available");
                AbyssPlanChoose(game, "boon" + alternative);
                Check(game.save.abyssFloor == 6 && game.save.boons[boon] == 4 && game.save.boons[alternative] == 1 &&
                    game.save.coins == coins + 17 && game.save.shards == shards + 3 && game.save.xp == xp + 7 &&
                    game.save.runCoins == 0 && game.save.runShards == 0 && game.save.runXp == 0 && game.save.bestFloor == 5,
                    "The real alternative banks the exact pending floor once and adds only its own stack");
                AbyssPlanNear(game.save.health, 11 + game.save.maxHealth * .5f, "The accepted descent retains its original half-maximum heal");
                Check(game.save.energy == 100 && game.enemies.Exists(enemy => enemy.boss),
                    "An accepted descent restores energy and retains the final Root Reaper encounter");
            }
        }

        public static void AbyssPlanningAllStartersRisksAndBuildPathsPreserveSixFloors(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++) for (int risk = 0; risk < 3; risk++)
                for (int preferred = 0; preferred < 3; preferred++)
                {
                    var game = AbyssPlanRun(data, profession, risk);
                    for (int floor = 1; floor <= 6; floor++)
                    {
                        Check(game.save.abyssFloor == floor && game.save.profession == profession && game.save.weapon == profession &&
                            game.enemies.Count == 5 + floor + (floor == 6 ? 1 : 0),
                            "Every class, risk and preferred boon retains every original floor and enemy count");
                        for (int slot = 0; slot < 5 + floor; slot++)
                            Check(game.enemies[slot].kind == slot % 3 && !game.enemies[slot].boss &&
                                game.enemies[slot].maxHp == 38 + floor * 6 + risk * 10 && game.CanStand(game.enemies[slot].Position),
                                "The original three roles, risk health and legal footing are unchanged");
                        Check(game.enemies.Exists(enemy => enemy.boss) == (floor == 6) &&
                            game.RootEchoAvailable == (floor == 2 || floor == 4),
                            "The Reaper and optional second/fourth-floor roots retain their exact locations in progression");
                        game.save.runCoins = 5; game.save.runShards = 2; AbyssPlanGate(game);
                        if (floor < 6)
                        {
                            int id = floor < 5 ? preferred : (preferred + 1) % 3;
                            Check(game.AbyssBoonChoiceEnabled("boon" + id), "Unchosen optional echoes never block a valid descent");
                            AbyssPlanChoose(game, "boon" + id);
                        }
                        else
                        {
                            Check(game.conversation.choices[0].action == "abyssBank", "Floor six still starts with the original completion action");
                            AbyssPlanChoose(game, "abyssBank");
                        }
                    }
                    Check(game.save.room == 0 && game.save.abyssFloor == 0 && game.save.bestFloor == 6 &&
                        game.Has("abyss_complete") && game.Has("relic_5") && Array.TrueForAll(game.save.boons, value => value == 0),
                        "All twenty-seven real descent paths retain completion, permanent milestones and run cleanup");
                }
        }

        public static void AbyssPlanningBoonSummariesMatchLiveDamageAndEnergyWithoutMutation(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++) for (int stacks = 0; stacks <= 4; stacks++)
            {
                var game = AbyssPlanRun(data, profession); game.enemies.Clear();
                game.save.boons[0] = game.save.boons[1] = game.save.boons[2] = stacks;
                string before = AbyssPlanState(game); int revision = game.revision; string mode = game.mode;
                for (int language = 0; language < 2; language++) for (int id = 0; id < 3; id++)
                {
                    string summary = game.AbyssBoonSummary(id, language == 1);
                    Check(summary.Contains(stacks + "/4") && summary.Contains((stacks * (id == 0 ? 15 : id == 1 ? 4 : 12)).ToString(CultureInfo.InvariantCulture)),
                        "Both languages report the current stack count and actual additive benefit");
                }
                Check(game.AbyssBoonSummary(-1, true) == "" && game.AbyssBoonSummary(3, false) == "" &&
                    AbyssPlanState(game) == before && game.revision == revision && game.mode == mode,
                    "All descriptions, including invalid ids, are read-only and cannot alter the build or world");
                AbyssPlanNear(game.Damage, 19 * (1 + stacks * .15f), "Edge's advertised additive multiplier equals the actual starter damage");
                game.save.energy = 0; game.Tick(.01f, new Point());
                AbyssPlanNear(game.save.energy, .01f * ((profession == 2 ? 18 : 14) + stacks * 4),
                    "Spring's current total is exercised by the actual accepted world energy tick");
                float hp = game.save.health; game.invulnerable = 0;
                game.projectiles.Add(new Projectile { position = game.Position, damage = 20, source = "shot" });
                game.Tick(.01f, new Point());
                AbyssPlanNear(hp - game.save.health, 20 * (1 - stacks * .12f),
                    "Stone's advertised additive mitigation equals a real hostile projectile contact");
                Check(game.playerHitTime > 0, "The mitigation check reached the unchanged actual hurt path");
            }
        }

        public static void AbyssPlanningBankPreviewMatchesCovenantAndOnceOnlyMilestones(Campaign data)
        {
            foreach (int covenant in new[] { 1, 3 })
            {
                var game = AbyssPlanRun(data, 0, 0, covenant);
                for (int floor = 1; floor <= 6; floor++)
                {
                    game.save.runCoins = 11; game.save.runShards = 7; game.save.runXp = 3; AbyssPlanGate(game);
                    int coins = game.save.coins, shards = game.save.shards, points = game.save.skillPoints;
                    string before = AbyssPlanState(game); int revision = game.revision;
                    string text = game.AbyssBankingSummary(true), zh = game.AbyssBankingSummary(false);
                    int expectedShards = 7 + (covenant == 3 ? 2 : 0) + (floor == 6 ? 12 : 0);
                    Check(text.Contains("Bank now: 11 coins, " + expectedShards + " shards, 3 XP.") &&
                        zh.Contains("现在结算：硬币11、碎片" + expectedShards + "、经验3。") && AbyssPlanState(game) == before && revision == game.revision,
                        "The bilingual bank preview includes pending values and cannot bank or grant milestones");
                    if (covenant == 3) Check(text.Contains("2"), "The signed covenant's two extra banked shards are disclosed");
                    if (floor == 6) Check(text.Contains("12") && text.Contains("2"), "The first completion discloses its twelve shards and two points");
                    if (floor < 6) AbyssPlanChoose(game, "boon" + ((floor - 1) % 3)); else AbyssPlanChoose(game, "abyssBank");
                    Check(game.save.coins == coins + 11 && game.save.shards == shards + 7 + (covenant == 3 ? 2 : 0) + (floor == 6 ? 12 : 0) &&
                        game.save.skillPoints == points + (floor == 6 ? 2 : 0),
                        "Actual banking matches pending loot, the covenant and the once-only first-clear payment exactly");
                    Check(game.Has("relic_5") == (floor >= 3), "The existing third-floor relic milestone is neither delayed nor granted early");
                }
                int paidPoints = game.save.skillPoints; game.StartAbyss(0);
                for (int floor = 1; floor <= 6; floor++)
                {
                    game.save.runCoins = 0; game.save.runShards = 1; game.save.runXp = 0; AbyssPlanGate(game);
                    int shards = game.save.shards;
                    if (floor < 6) AbyssPlanChoose(game, "boon" + ((floor - 1) % 3)); else AbyssPlanChoose(game, "abyssBank");
                    Check(game.save.shards == shards + 1 + (covenant == 3 ? 2 : 0) && game.save.skillPoints == paidPoints,
                        "A later complete expedition cannot repeat the permanent twelve-shard or two-point reward");
                }
            }
        }

        public static void AbyssPlanningForecastDescribesActualNextFloorAndNeverRequiresEcho(Campaign data)
        {
            var game = AbyssPlanRun(data);
            for (int floor = 1; floor <= 6; floor++)
            {
                AbyssPlanGate(game); string before = AbyssPlanState(game);
                string forecast = game.AbyssNextFloorHint(true); game.AbyssNextFloorHint(false);
                Check(AbyssPlanState(game) == before, "Reading the next-floor forecast cannot consume or resolve a shrine");
                if (floor < 6)
                {
                    bool echo = floor + 1 == 2 || floor + 1 == 4;
                    Check(forecast.Contains("echo") == echo && forecast.Contains("Reaper") == (floor == 5),
                        "Optional root and final Reaper warnings describe the actual next encounter, not the current floor");
                    Check((forecast.IndexOf("Rift marks", StringComparison.OrdinalIgnoreCase) >= 0) == (floor + 1 >= 3), "Rift marks are disclosed from the unchanged third floor onward");
                    Check(!game.RootEchoResolved && game.AbyssBoonChoiceEnabled("boon" + ((floor - 1) % 3)),
                        "Skipping an unresolved optional root remains a valid informed choice");
                    AbyssPlanChoose(game, "boon" + ((floor - 1) % 3));
                }
                else Check(!game.AbyssBoonChoiceEnabled("boon0") && !game.AbyssBoonChoiceEnabled("abyssRetain"),
                    "The final forecast cannot offer an invented seventh floor");
            }
        }

        public static void AbyssPlanningStaleChoicesRejectEnemyClearanceDeathAndActiveEcho(Campaign data)
        {
            foreach (string boundary in new[] { "enemy", "clearance", "death", "outside", "final", "echo" })
            {
                var game = AbyssPlanRun(data); AbyssPlanGate(game); AbyssPlanChoose(game, "boon1"); AbyssPlanGate(game);
                Conversation actualGate = game.conversation;
                if (boundary == "enemy") game.enemies.Add(new Enemy(99241, 0, 20, 14, 80));
                if (boundary == "clearance") game.save.flags.Remove("abyss_clear_2");
                if (boundary == "death") game.deathTime = .2f;
                if (boundary == "outside") game.save.room = 0;
                if (boundary == "final") { game.save.abyssFloor = 6; game.save.flags.Add("abyss_clear_6"); }
                if (boundary == "echo")
                {
                    game.Close(); game.save.x = GameCore.RootEchoPoint.x; game.save.y = GameCore.RootEchoPoint.y + .5f;
                    Action(game, "abyssEcho", "echo_begin"); Check(game.RootEchoActive, "The guard case starts an actual optional warning");
                    game.conversation = actualGate; game.mode = "dialog";
                }
                game.save.runCoins = 19; game.save.runShards = 5; game.save.runXp = 3; game.save.health = 31;
                string before = AbyssPlanState(game);
                Check(!game.AbyssBoonChoiceEnabled("boon0") && !game.AbyssBoonChoiceEnabled("abyssRetain"),
                    "The displayed choice rechecks the actual " + boundary + " boundary");
                AbyssPlanChoose(game, "boon0"); game.TakeBoon(0);
                Check(AbyssPlanState(game) == before, "Stale UI and direct callbacks retain all state at the " + boundary + " boundary");
                if (boundary == "echo") Check(game.RootEchoActive && game.hazards.Exists(hazard => hazard.kind == "echo"),
                    "A blocked stale decision cannot cancel or consume an active real echo challenge");
            }
            var invalid = AbyssPlanRun(data); AbyssPlanGate(invalid); string state = AbyssPlanState(invalid);
            invalid.TakeBoon(-1); invalid.TakeBoon(3);
            Check(AbyssPlanState(invalid) == state && invalid.AbyssBoonChoiceEnabled("close") && invalid.AbyssBoonChoiceEnabled("boon01"),
                "Only exact owned action ids are filtered, without changing unrelated dialog semantics");
        }

        public static void AbyssPlanningLegacyAllCappedRetainsBuildWithoutBlockingExtraction(Campaign data)
        {
            foreach (int version in new[] { 1, 2 })
            {
                var earned = AbyssPlanCapped(data, 0); AbyssPlanGate(earned); earned.PrepareSave();
                SaveData legacy = CloneSave(earned.save); legacy.version = version;
                legacy.boons = new[] { 4, 4, 4 }; legacy.runCoins = 23; legacy.runShards = 6; legacy.runXp = 7;
                legacy.health = 20; legacy.energy = 17;
                var game = new GameCore(data, CloneSave(legacy)); game.Start(); game.Close(); AbyssPlanGate(game);
                Check(game.save.version == 2 && game.save.boons[0] == 4 && game.save.boons[1] == 4 && game.save.boons[2] == 4 &&
                    AbyssPlanChoice(game, "abyssBank") == 3 && AbyssPlanChoice(game, "abyssRetain") > 3,
                    "A raw v" + version + " all-capped run keeps all original action slots and appends a compatible escape");
                Check(game.AbyssBoonChoiceEnabled("abyssRetain") && !game.AbyssBoonChoiceEnabled("boon0") &&
                    !game.AbyssBoonChoiceEnabled("boon1") && !game.AbyssBoonChoiceEnabled("boon2"),
                    "Only the explicit no-new-boon descent is available when all imported stacks are full");
                int coins = game.save.coins, shards = game.save.shards, xp = game.save.xp;
                AbyssPlanChoose(game, "abyssRetain");
                Check(game.save.abyssFloor == 6 && Array.TrueForAll(game.save.boons, stack => stack == 4) &&
                    game.save.coins == coins + 23 && game.save.shards == shards + 6 && game.save.xp == xp + 7 &&
                    game.save.runCoins == 0 && game.save.runShards == 0 && game.save.runXp == 0 && game.save.energy == 100,
                    "Compatible retention descends, banks and restores energy without inventing a thirteenth stack");
                AbyssPlanNear(game.save.health, 20 + game.save.maxHealth * .5f, "Legacy retention heals by the unchanged descent amount");

                var extract = new GameCore(data, CloneSave(legacy)); extract.Start(); extract.Close(); AbyssPlanGate(extract);
                coins = extract.save.coins; shards = extract.save.shards;
                AbyssPlanChoose(extract, "abyssBank");
                Check(extract.save.room == 0 && extract.save.abyssFloor == 0 && extract.save.coins == coins + 23 && extract.save.shards == shards + 6,
                    "The original fourth-slot safe extraction remains available for an imported all-capped build");
            }
            var leave = AbyssPlanRun(data); leave.enemies.Clear(); leave.save.runCoins = 13; leave.save.runShards = 8;
            int wallet = leave.save.coins, banked = leave.save.shards;
            Action(leave, "abyssExit", "abyssLeave");
            Check(leave.save.room == 0 && leave.save.coins == wallet && leave.save.shards == banked && leave.save.runCoins == 0 && leave.save.runShards == 0,
                "The original west exit still discards this floor's unbanked loot without taking previous possessions");
        }

        public static void AbyssPlanningCandidateProfilesMatchActuallyEquippedMasteryAndEdge(Campaign data)
        {
            foreach (bool abyss in new[] { false, true }) for (int stacks = 0; stacks <= 4; stacks++)
            {
                var game = abyss ? AbyssPlanRun(data) : Fresh(data); game.enemies.Clear(); game.save.boons[0] = stacks;
                game.save.level = 4; game.save.upgrades = 2; game.save.talents[0] = 1;
                for (int equipped = 0; equipped < 3; equipped++) for (int candidate = 0; candidate < 3; candidate++)
                    for (int ownRank = 0; ownRank <= 5; ownRank++) for (int candidateRank = 0; candidateRank <= 5; candidateRank++)
                        for (int style = 0; style < 3; style++)
                        {
                            Array.Clear(game.save.mastery, 0, 3); game.save.weapon = equipped;
                            game.save.mastery[equipped] = ownRank * 20; game.save.mastery[candidate] = candidateRank * 20;
                            string before = AbyssPlanState(game); WeaponProfile preview = game.Profile(candidate, style);
                            Check(AbyssPlanState(game) == before, "Reading a candidate profile cannot silently equip it or adjust mastery");
                            game.save.weapon = candidate; WeaponProfile actual = game.Profile(candidate, style); game.save.weapon = equipped;
                            float expected = (19 + 8 + 6 + 3 + candidateRank * 2) * (abyss ? 1 + stacks * .15f : 1)
                                * (candidate == 1 ? .9f : candidate == 2 ? 1.25f : 1) * (style == 1 ? 1.4f : style == 2 ? .8f : 1);
                            AbyssPlanNear(preview.damage, actual.damage, "Every displayed candidate matches actually equipping that weapon and style");
                            AbyssPlanNear(actual.damage, expected, "Current and surface profiles retain the mature mastery, style and Edge formula");
                            Check(preview.interval == actual.interval && preview.range == actual.range && preview.energy == actual.energy &&
                                preview.benefit == actual.benefit && preview.cost == actual.cost,
                                "Preview accuracy leaves every original speed, reach, energy and style explanation unchanged");
                        }
            }
        }
    }
}
