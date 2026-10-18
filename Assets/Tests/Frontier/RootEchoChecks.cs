using System;
using System.Collections.Generic;

namespace Starfall.Tests
{
    // These checks stage earned floor clearance, then use the shipped Talk,
    // Choose, movement, dodge, hazard, save and banking paths. They do not
    // advance an echo-only timer or certify native Unity playback.
    public static partial class CoreChecks
    {
        private static GameCore EchoFloor(Campaign data, int floor = 2, int profession = 0, int risk = 0, bool cleared = true)
        {
            var game = Fresh(data, profession); game.SkipTutorial(); game.save.frontierEnding = 1;
            game.save.stage = 3; game.save.abyssFloor = floor; game.save.abyssRisk = risk;
            game.save.abyssSeed = 1729; game.save.room = 8; game.save.x = 7; game.save.y = 9.5f;
            if (cleared) game.save.flags.Add("abyss_clear_" + floor);
            game.Load(CloneSave(game.save)); game.Start(); game.Close(); return game;
        }
        private static void EchoNear(GameCore game)
        {
            game.Close(); Point point = GameCore.RootEchoPoint + new Point(0, .5f);
            Check(game.CanStand(point), "The optional shrine has a legal nearby interaction foot");
            game.save.x = point.x; game.save.y = point.y;
        }
        private static void EchoChoice(GameCore game, string action, bool open = true)
        {
            if (open) game.Talk(Object(game, "abyssEcho"));
            Check(game.mode == "dialog" && game.conversation != null, "The canonical root shrine opens actual dialogue");
            for (int index = 0; index < game.conversation.choices.Length; index++)
                if (game.conversation.choices[index].action == action) { game.Choose(index); return; }
            throw new Exception("Missing root shrine action " + action);
        }
        private static List<Hazard> EchoHazards(GameCore game)
        { return game.hazards.FindAll(hazard => hazard.kind == "echo"); }
        private static void EchoWaitStep(GameCore game, int step)
        {
            int frames = 0;
            while (game.RootEchoActive && game.RootEchoStep < step && frames++ < 1500) game.Tick(.01f, new Point());
            Check(frames < 1500 && game.RootEchoStep == step && !game.Dying, "The real world clock reaches echo step " + step);
        }
        private static int EchoWalkSafe(GameCore game)
        {
            int frames = 0;
            while (game.Position.y < 11.9f && frames++ < 100)
            {
                Point before = game.Position; game.Tick(.02f, new Point(0, 1));
                Check(game.CanStand(game.Position) && (game.Position - before).Length < .2f,
                    "The starter leaves the lock-on circle through continuous legal movement");
            }
            Check(frames < 100 && (game.Position - GameCore.RootEchoPoint).Length < GameCore.RootEchoLeash,
                "The safe gap is reachable before the first warning resolves, without leaving the encounter");
            return frames;
        }

        public static void RootEchoPreservesSixSeededFloorsAndCanonicalEncounters(Campaign data)
        {
            foreach (int seed in new[] { 1, 1729, 77101 }) for (int floor = 1; floor <= 6; floor++)
            {
                var game = EchoFloor(data, floor, 0, 1, false); game.save.abyssSeed = seed;
                game.save.abyssEnemies = null; game.save.abyssSnapshotFloor = 0;
                game.Load(CloneSave(game.save)); game.Start();
                var random = new Random(seed + floor * 919); char[][] expected = new char[24][];
                for (int y = 0; y < 24; y++) expected[y] = new string('#', 36).ToCharArray();
                int[,] rooms = { { 3, 4, 8, 16 }, { 13, 3, 9, 18 }, { 25, 4, 8, 16 } };
                for (int i = 0; i < 3; i++) for (int y = rooms[i, 1]; y < rooms[i, 1] + rooms[i, 3]; y++)
                    for (int x = rooms[i, 0]; x < rooms[i, 0] + rooms[i, 2]; x++) expected[y][x] = '.';
                for (int y = 13; y <= 15; y++) for (int x = 2; x < 34; x++) expected[y][x] = '=';
                for (int i = 0; i < 9; i++) { int x = random.Next(13, 22), y = random.Next(4, 20); if (y < 12 || y > 16) expected[y][x] = '#'; }
                for (int y = 0; y < 24; y++) Check(game.Map.rows[y] == new string(expected[y]), "The entire mature seeded floor geometry remains exact");
                Check(game.Map.objects[0].kind == "abyssExit" && game.Map.objects[0].x == 3 && game.Map.objects[0].y == 14 &&
                    game.Map.objects[1].kind == "abyssGate" && game.Map.objects[1].x == 31 && game.Map.objects[1].y == 14,
                    "The original exit and root gate retain their ordering and feet");
                bool supported = floor == 2 || floor == 4;
                Check(game.RootEchoAvailable == supported && game.Map.objects.Length == (supported ? 3 : 2),
                    "Only the second and fourth floors append one optional shrine");
                if (supported) { WorldObject shrine = Object(game, "abyssEcho"); Check(shrine.x == 7 && shrine.y == 9, "The echo uses the documented first-room shrine"); }
                Check(game.enemies.Count == 5 + floor + (floor == 6 ? 1 : 0), "Every original floor retains its full enemy count");
                for (int slot = 0; slot < 5 + floor; slot++)
                {
                    Enemy enemy = game.enemies[slot];
                    Check(enemy.kind == slot % 3 && !enemy.boss && enemy.maxHp == 38 + floor * 6 + 10 && game.CanStand(enemy.Position),
                        "Original kind, risk-scaled health and legal enemy footing are preserved");
                }
                Check(game.enemies.Exists(enemy => enemy.boss) == (floor == 6), "Only the original final floor has the Root Reaper");
                Check(!game.RootEchoActive && !game.RootEchoResolved && game.save.runCoins == 0 && game.save.runShards == 0,
                    "Entering a floor never autostarts or pays an optional event");
            }
        }

        public static void RootEchoRequiresEarnedClearanceAndRechecksPhysicalChoices(Campaign data)
        {
            var uncleared = EchoFloor(data, 2, 0, 0, false); EchoNear(uncleared);
            uncleared.Talk(Object(uncleared, "abyssEcho"));
            Check(!uncleared.RootEchoActive && !uncleared.RootEchoResolved, "The visible shrine cannot bypass the mature floor encounter");
            if (uncleared.conversation != null) foreach (Choice choice in uncleared.conversation.choices)
                Check(choice.action != "echo_begin" && choice.action != "echo_rest", "Uncleared dialogue does not offer completion choices");
            foreach (string boundary in new[] { "distance", "enemy", "clearance", "practice", "death" })
            {
                var game = EchoFloor(data); EchoNear(game); game.Talk(Object(game, "abyssEcho"));
                if (boundary == "distance") { game.save.x = 7; game.save.y = 14; }
                if (boundary == "enemy") game.enemies.Add(new Enemy(98301, 0, 18, 14, 40));
                if (boundary == "clearance") game.save.flags.Remove("abyss_clear_2");
                if (boundary == "practice") game.practicing = true;
                if (boundary == "death") game.deathTime = .3f;
                int coins = game.save.runCoins, shards = game.save.runShards;
                EchoChoice(game, "echo_begin", false);
                Check(!game.RootEchoActive && !game.RootEchoResolved && EchoHazards(game).Count == 0 &&
                    game.save.runCoins == coins && game.save.runShards == shards,
                    "A stale genuine dialogue rechecks the actual " + boundary + " boundary before activation");
            }
        }

        public static void RootEchoThreeWarningsKeepTheirCapturedReadableGeometry(Campaign data)
        {
            var game = EchoFloor(data, 2, 0, 2); EchoNear(game); Point captured = game.Position;
            EchoChoice(game, "echo_begin"); List<Hazard> first = EchoHazards(game);
            Check(game.RootEchoActive && game.RootEchoStep == 0 && first.Count == 1 && first[0].radius == 1.3f &&
                first[0].timer == 1.25f && first[0].damage == 20 && (first[0].position - captured).Length < .0001f,
                "First contact begins as a full 1.25-second captured lock-on warning at the actual player");
            EchoWalkSafe(game);
            Check((first[0].position - captured).Length < .0001f && first[0].timer > 0,
                "Real movement cannot retarget or shorten the already prepared circle");
            EchoWaitStep(game, 1); List<Hazard> pair = EchoHazards(game);
            Check(pair.Count == 2 && pair.TrueForAll(h => h.radius == 1.15f && h.timer > 1.2f && h.damage == 20),
                "The second pattern starts two full warnings only after first contact and recovery");
            foreach (Point offset in new[] { new Point(-1.9f, 0), new Point(1.9f, 0) })
                Check(pair.Exists(h => (h.position - (GameCore.RootEchoPoint + offset)).Length < .0001f), "The two circles leave the advertised central gap");
            EchoWaitStep(game, 2); List<Hazard> corners = EchoHazards(game);
            Check(corners.Count == 4 && corners.TrueForAll(h => h.radius == 1.05f && h.timer > 1.2f && h.damage == 20),
                "The final pattern has four fully prepared independent corner circles");
            foreach (float x in new[] { -1.8f, 1.8f }) foreach (float y in new[] { -1.8f, 1.8f })
                Check(corners.Exists(h => (h.position - (GameCore.RootEchoPoint + new Point(x, y))).Length < .0001f), "Every advertised corner preserves its fixed shrine-relative center");
            Check(!game.RootEchoResolved && game.save.runCoins == 0 && game.save.runShards == 0,
                "Seeing all three warnings is not completion before the last actual contact resolves");
            EchoWaitStep(game, 3);
            Check(!game.RootEchoActive && game.RootEchoResolved && EchoHazards(game).Count == 0,
                "All real contacts and recovery complete once and retire their warnings");
        }

        public static void RootEchoThreeClassesAndRisksEarnUnbankedRewardsThroughRealMovement(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++) for (int risk = 0; risk < 3; risk++)
            {
                var game = EchoFloor(data, 2, profession, risk); EchoNear(game);
                int coins = game.save.coins, shards = game.save.shards, xp = game.save.xp, kills = game.save.kills[8];
                float hp = game.save.health; int weapon = game.save.weapon;
                EchoChoice(game, "echo_begin"); EchoWalkSafe(game); EchoWaitStep(game, 3);
                Check(!game.Dying && game.save.health == hp && game.save.profession == profession && game.save.weapon == weapon,
                    "Each unchanged starter traverses the advertised gap without damage, invulnerability grants or identity swaps");
                Check(game.RootEchoResolved && game.Has("abyss_echo_2") && game.save.runCoins == 20 && game.save.runShards == 3 + risk && game.RootEchoShards == 3 + risk,
                    "Each risk pays the documented optional reward into the current unbanked floor exactly once");
                Check(game.save.coins == coins && game.save.shards == shards && game.save.xp == xp && game.save.kills[8] == kills && game.save.runXp == 0,
                    "Finishing a hazard event cannot silently bank, give combat XP or counterfeit a kill");
                game.Talk(Object(game, "abyssEcho"));
                if (game.conversation != null) foreach (Choice choice in game.conversation.choices)
                    Check(choice.action != "echo_begin" && choice.action != "echo_rest", "Resolved shrine dialogue cannot offer a second reward or rest");
                game.Close(); Action(game, "abyssGate", "boon0");
                Check(game.save.abyssFloor == 3 && game.save.boons[0] == 1 && game.save.coins == coins + 20 && game.save.shards == shards + 3 + risk && game.save.runCoins == 0 && game.save.runShards == 0,
                    "The original chosen blessing banks the actual bonus once and descends normally");
            }
        }

        public static void RootEchoRestReloadAndFreshRunNeverDuplicateResolution(Campaign data)
        {
            foreach (int floor in new[] { 2, 4 })
            {
                var game = EchoFloor(data, floor); EchoNear(game); game.save.health = 20;
                int coins = game.save.coins, shards = game.save.shards; float expected = 20 + game.save.maxHealth * .25f;
                EchoChoice(game, "echo_rest");
                Check(game.RootEchoResolved && !game.RootEchoActive && game.save.health == expected && game.save.runCoins == 0 && game.save.runShards == 0,
                    "The alternative consumes the shrine for precisely one quarter maximum health, with no challenge loot");
                game.PrepareSave(); var loaded = new GameCore(data, CloneSave(game.save)); loaded.Start(); EchoNear(loaded);
                Check(loaded.RootEchoResolved && !loaded.RootEchoActive && loaded.RootEchoTimer == 0 && loaded.save.health == expected &&
                    loaded.save.coins == coins && loaded.save.shards == shards, "Real reconstruction preserves the rest choice and health without inventing active timers or payouts");
                loaded.Talk(Object(loaded, "abyssEcho"));
                if (loaded.conversation != null) foreach (Choice choice in loaded.conversation.choices)
                    Check(choice.action != "echo_begin" && choice.action != "echo_rest", "Saved resolution cannot switch or repeat the consumed choice");
            }
            var active = EchoFloor(data); EchoNear(active); EchoChoice(active, "echo_begin"); active.Tick(.4f, new Point());
            active.PrepareSave(); var resumed = new GameCore(data, CloneSave(active.save)); resumed.Start(); EchoNear(resumed);
            Check(!resumed.RootEchoActive && !resumed.RootEchoResolved && resumed.RootEchoStep == 0 && resumed.RootEchoTimer == 0 && EchoHazards(resumed).Count == 0,
                "An interrupted attempt reconstructs safely as unstarted, without an invisible attack or unearned reward");
            EchoChoice(resumed, "echo_rest"); resumed.Close(); Action(resumed, "abyssGate", "abyssBank");
            Check(resumed.save.room == 0, "The original bank-return path ends the expedition");
            resumed.StartAbyss(1);
            Check(!resumed.save.flags.Exists(flag => flag.StartsWith("abyss_echo_", StringComparison.Ordinal)) && !resumed.RootEchoActive,
                "Starting a fresh run clears only run-scoped echo resolutions, allowing future expeditions to choose again");
            foreach (int version in new[] { 1, 2 })
            {
                var legacy = new SaveData { version = version, stage = 1, room = 1, x = 5, y = 14, coins = 91,
                    flags = new List<string> { "intro", "scout_saved", "core_1" } };
                var old = new GameCore(data, legacy);
                Check(old.save.coins == 91 && old.Has("core_1") && !old.RootEchoAvailable && !old.RootEchoActive &&
                    !old.save.flags.Exists(flag => flag.StartsWith("abyss_echo_", StringComparison.Ordinal)), "Legacy v" + version + " saves acquire no echo completion, reward or pending hazard");
            }
        }

        public static void RootEchoCancellationPauseAndBankGuardsRemainTransactional(Campaign data)
        {
            var paused = EchoFloor(data); EchoNear(paused); EchoChoice(paused, "echo_begin");
            float timer = paused.RootEchoTimer, contact = EchoHazards(paused)[0].timer;
            paused.mode = "pause"; Advance(paused, 2);
            Check(paused.RootEchoActive && paused.RootEchoTimer == timer && EchoHazards(paused)[0].timer == contact && !paused.RootEchoResolved,
                "Real paused world updates freeze both the echo and the actual contact hazard");
            paused.Close(); EchoChoice(paused, "echo_cancel");
            Check(!paused.RootEchoActive && !paused.RootEchoResolved && EchoHazards(paused).Count == 0 && paused.save.runCoins == 0 && paused.save.runShards == 0,
                "Manual cancellation returns the shrine to a retryable unpaid state");
            EchoNear(paused); EchoChoice(paused, "echo_begin");
            Hazard foreign = new Hazard { kind = "rift", position = new Point(29, 18), radius = 1, timer = 20, damage = 7 }; paused.hazards.Add(foreign);
            paused.save.x = 7; paused.save.y = 14;
            paused.Tick(.01f, new Point());
            Check(!paused.RootEchoActive && !paused.RootEchoResolved && EchoHazards(paused).Count == 0 && paused.hazards.Contains(foreign),
                "Leaving the documented radius cancels only the echo's own hazards, not a mature foreign danger");
            var bank = EchoFloor(data); EchoNear(bank); EchoChoice(bank, "echo_begin");
            bank.save.runCoins = 7; bank.save.runShards = 2; int coins = bank.save.coins, shards = bank.save.shards;
            bank.TakeBoon(1);
            Check(bank.RootEchoActive && bank.save.abyssFloor == 2 && bank.save.boons[1] == 0 && bank.save.coins == coins && bank.save.shards == shards && bank.save.runCoins == 7 && bank.save.runShards == 2,
                "A public descend callback cannot bank or abandon an active optional encounter");
            bank.Talk(Object(bank, "abyssGate"));
            Check(bank.RootEchoActive && bank.save.abyssFloor == 2 && bank.save.runCoins == 7,
                "The original gate dialogue also respects the active encounter transaction");
            var dying = EchoFloor(data); EchoNear(dying); EchoChoice(dying, "echo_begin"); dying.save.health = 1; dying.invulnerable = 0;
            dying.projectiles.Add(new Projectile { position = dying.Position, damage = 999, source = "shot" }); dying.Tick(.01f, new Point());
            Check(dying.Dying && !dying.RootEchoResolved && dying.save.runCoins == 0 && dying.save.runShards == 0,
                "Actual lethal combat cannot turn an interrupted hazard into successful completion");
            Advance(dying, 1.2f);
            Check(dying.save.room == 0 && !dying.RootEchoActive && EchoHazards(dying).Count == 0 && !dying.Has("abyss_echo_2"),
                "The mature death recovery transaction safely clears the attempt and returns home");
        }

        public static void RootEchoActualWarningContactAndDodgeKeepCombatRules(Campaign data)
        {
            foreach (int risk in new[] { 0, 1, 2 })
            {
                var exposed = EchoFloor(data, 2, 0, risk); EchoNear(exposed); EchoChoice(exposed, "echo_begin");
                float hp = exposed.save.health; exposed.invulnerable = 0;
                int frames = 0; while (EchoHazards(exposed).Count > 0 && frames++ < 200) exposed.Tick(.01f, new Point());
                Check(frames < 200 && Math.Abs(hp - exposed.save.health - (12 + risk * 4)) < .001f && exposed.playerHitTime > 0,
                    "A real standard-difficulty contact applies the exact advertised risk-scaled damage and existing hit reaction");
            }
            var dodge = EchoFloor(data); EchoNear(dodge); EchoChoice(dodge, "echo_begin"); float health = dodge.save.health;
            // Dash moves 15 tiles/s. Start inside the final .04-second window
            // so the body is still inside the captured 1.3-tile circle at
            // contact; an earlier dodge merely leaves it, with no counter hit.
            while (EchoHazards(dodge)[0].timer > .04f) dodge.Tick(.01f, new Point());
            dodge.Dash(new Point(0, 1));
            int remaining = 0; while (EchoHazards(dodge).Count > 0 && remaining++ < 100) dodge.Tick(.01f, new Point());
            Check(dodge.save.health == health && dodge.riposteTime > 0 && dodge.effects.Exists(effect => effect.kind == "number" && effect.text == dodge.L("闪避反击", "COUNTER")),
                "A correctly timed real Space-dodge uses the existing invulnerability and counter opportunity at echo contact");
        }
    }
}
