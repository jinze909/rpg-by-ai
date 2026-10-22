using System;
using System.Reflection;

namespace Starfall.Tests
{
    // Exercise the shared production defeat path through accepted actions and
    // missile collisions. These checks never invoke the receipt recorder.
    public static partial class CoreChecks
    {
        private static long LootEarnedXp(GameCore game)
        { return game.save.xp + 20L * game.save.level * (game.save.level - 1); }

        private static Enemy LootEnemy(GameCore game, int kind = 0, bool boss = false, float hp = 1)
        {
            var enemy = new Enemy(9880 + game.save.kills[game.save.room], kind,
                game.Position.x + 1.3f, game.Position.y, hp, boss) { stun = 99 };
            Check(game.CanStand(enemy.Position), "A loot fixture uses legal current-room footing");
            game.enemies.Add(enemy); return enemy;
        }

        private static void LootMissileDefeat(GameCore game, Enemy enemy)
        {
            enemy.stun = 99;
            game.projectiles.Add(new Projectile {
                position = enemy.Position - new Point(.2f, 0), velocity = new Point(1, 0),
                friendly = true, damage = 10000, life = 1, visual = "arrow",
                impact = new ImpactProfile(1, 0, game.save.weapon, 0, 0, true)
            });
            game.Tick(.01f, new Point());
            Check(!game.enemies.Contains(enemy), "A real friendly missile collision must defeat the live actor");
        }

        private static void LootAwaitDefeat(GameCore game, Enemy enemy)
        {
            for (int frame = 0; frame < 400 && game.enemies.Contains(enemy); frame++)
            { enemy.stun = 99; game.Tick(.01f, new Point()); }
            Check(!game.enemies.Contains(enemy), "The accepted production action must actually defeat its target");
        }

        private static void LootExpect(CombatLootReceipt receipt, long coins, long xp, long shards, bool unbanked, int item = -1, long count = 0)
        {
            Check(receipt != null && receipt.coins == coins && receipt.xp == xp && receipt.shards == shards && receipt.unbanked == unbanked,
                "The receipt matches actual awarded coins, earned XP, shards and bank ownership");
            for (int index = 0; index < 8; index++)
                Check(receipt.ItemCount(index) == (index == item ? count : 0), "Only the actual inventory award appears at item " + index);
            Check(receipt.ItemCount(-1) == 0 && receipt.ItemCount(8) == 0, "Absent item indices safely report zero");
        }

        private static void LootAbsent(GameCore game, string context)
        { Check(game.RecentCombatLoot == null && game.CombatLootTimeRemaining == 0, "No combat receipt or timer survives " + context); }

        public static void CombatLootActualAwardsMatchKindsBossAndLevelRollover(Campaign data)
        {
            Check(GameCore.CombatLootReceiptDuration == 3f, "Combat loot is visible for exactly three simulation seconds");
            for (int kind = 0; kind < 4; kind++)
            {
                GameCore game = Quiet(data); game.save.xp = 39;
                int[] inventory = (int[])game.save.inventory.Clone(); int coins = game.save.coins, shards = game.save.shards;
                long xp = LootEarnedXp(game); int points = game.save.skillPoints, kills = game.save.kills[1];
                bool boss = kind == 3; int item = boss ? 5 : kind == 2 ? 4 : kind == 1 ? 1 : 2;
                Enemy enemy = LootEnemy(game, kind, boss);
                LootAbsent(game, "a living low-health enemy before contact");
                game.Attack(new Point(1, 0));
                Check(game.playerAction != null && !game.playerAction.released && game.save.coins == coins,
                    "Preparing a real sword attack awards no early loot");
                LootAwaitDefeat(game, enemy);
                int earnedCoins = boss ? 70 : 6, earnedXp = boss ? 80 : 12;
                Check(game.save.coins - coins == earnedCoins && LootEarnedXp(game) - xp == earnedXp && game.save.shards == shards,
                    "The actual normal or Boss payout remains exact for kind " + kind);
                Check(game.save.level == 2 && game.save.xp == (boss ? 79 : 11) && game.save.skillPoints == points + 1 && game.save.kills[1] == kills + 1,
                    "The real kill performs its mature level rollover and kill progression once");
                for (int index = 0; index < inventory.Length; index++)
                    Check(game.save.inventory[index] - inventory[index] == (index == item ? 1 : 0), "The real inventory delta matches kind " + kind);
                LootExpect(game.RecentCombatLoot, earnedCoins, earnedXp, 0, false, item, 1);
                Check(Math.Abs(game.CombatLootTimeRemaining - 3) < .0001f, "A completed payout starts the full receipt duration");
            }
        }

        public static void CombatLootSharedDefeatsIncludeSkillsProjectilesCompanionsAndFrost(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++) foreach (bool skill in new[] { false, true })
            {
                GameCore game = Quiet(data, profession); int coins = game.save.coins; long xp = LootEarnedXp(game);
                int herbs = game.save.inventory[2]; Enemy enemy = LootEnemy(game);
                if (skill) game.Skill(new Point(1, 0)); else game.Attack(new Point(1, 0));
                Check(game.playerAction != null, "Every class accepts its real basic or Q before a receipt is expected");
                LootAwaitDefeat(game, enemy);
                Check(game.save.coins == coins + 6 && LootEarnedXp(game) == xp + 12 && game.save.inventory[2] == herbs + 1,
                    "Every actual class basic or Q shares the unchanged normal payout");
                LootExpect(game.RecentCombatLoot, 6, 12, 0, false, 2, 1);
            }
            foreach (int companion in new[] { 1, 2 })
            {
                GameCore game = Quiet(data); game.save.flags.Add("companion_" + companion); game.SelectCompanion(companion);
                Enemy enemy = LootEnemy(game); int coins = game.save.coins, herbs = game.save.inventory[2]; long xp = LootEarnedXp(game);
                if (companion == 2) game.CommandCompanion();
                LootAwaitDefeat(game, enemy);
                Check(game.save.coins == coins + 6 && LootEarnedXp(game) == xp + 12 && game.save.inventory[2] == herbs + 1,
                    "A companion-owned actual missile or command defeat keeps the normal payout");
                LootExpect(game.RecentCombatLoot, 6, 12, 0, false, 2, 1);
            }
            GameCore frost = Quiet(data, 2); frost.save.flags.Add("technique_2"); frost.save.technique = 1;
            Enemy frozen = LootEnemy(frost, 0, false, 999); frost.Skill(new Point(1, 0));
            for (int frame = 0; frame < 100 && frozen.hp == 999; frame++) frost.Tick(.01f, new Point());
            Check(frozen.hp < 999 && frost.playerAction != null && frost.playerAction.released, "The actual persistent frost field emits its first pulse");
            LootAbsent(frost, "a nonlethal frost pulse");
            frozen.hp = 1; frost.Equip(0); LootAwaitDefeat(frost, frozen);
            Check(frost.save.mastery[2] == 2 && frost.save.mastery[0] == 0,
                "The later real frost pulse retains its caster's ownership after equipment changes");
            LootExpect(frost.RecentCombatLoot, 6, 12, 0, false, 2, 1);
        }

        public static void CombatLootAggregationSnapshotsAndSimulationClockStayReadOnly(Campaign data)
        {
            GameCore game = Quiet(data); LootMissileDefeat(game, LootEnemy(game));
            CombatLootReceipt first = game.RecentCombatLoot; float time = game.CombatLootTimeRemaining;
            string state = AbyssPlanState(game); int revision = game.revision, effects = game.effects.Count, projectiles = game.projectiles.Count;
            for (int query = 0; query < 100; query++)
            {
                LootExpect(game.RecentCombatLoot, 6, 12, 0, false, 2, 1);
                Check(game.CombatLootTimeRemaining == time, "Reading the receipt never advances its clock");
            }
            Check(AbyssPlanState(game) == state && game.revision == revision && game.effects.Count == effects && game.projectiles.Count == projectiles,
                "Receipt queries leave every public save field and simulation collections unchanged");
            game.mode = "pause"; Advance(game, 3.2f);
            Check(game.CombatLootTimeRemaining == time, "Menu pause freezes the combat loot simulation clock");
            game.Close(); game.hitStop = .2f;
            for (int frame = 0; frame < 5; frame++) game.Tick(.02f, new Point());
            Check(game.CombatLootTimeRemaining == time, "Real hit stop freezes the combat loot simulation clock");
            game.hitStop = 0; Advance(game, .5f);
            Check(Math.Abs(game.CombatLootTimeRemaining - (time - .5f)) < .0002f, "Only active simulation ages a receipt");
            LootMissileDefeat(game, LootEnemy(game, 1)); CombatLootReceipt second = game.RecentCombatLoot;
            Check(!ReferenceEquals(first, second) && second.coins == 12 && second.xp == 24 && second.ItemCount(1) == 1 && second.ItemCount(2) == 1,
                "A consecutive kill creates a new aggregated immutable snapshot with both actual items");
            LootExpect(first, 6, 12, 0, false, 2, 1);
            Check(Math.Abs(game.CombatLootTimeRemaining - 3) < .0001f, "A grouped kill refreshes the full three-second clock");
            LootMissileDefeat(game, LootEnemy(game, 2)); LootMissileDefeat(game, LootEnemy(game, 3, true));
            CombatLootReceipt all = game.RecentCombatLoot;
            Check(all.coins == 88 && all.xp == 116 && all.shards == 0 && !all.unbanked && all.ItemCount(1) == 1
                && all.ItemCount(2) == 1 && all.ItemCount(4) == 1 && all.ItemCount(5) == 1,
                "Mixed ordinary and Boss deaths aggregate actual currencies and each actual inventory kind once");
            Check(second.coins == 12 && second.xp == 24 && second.ItemCount(4) == 0 && second.ItemCount(5) == 0,
                "Later mixed drops cannot mutate any previously returned item snapshot");
            Advance(game, 3.02f); LootAbsent(game, "three active simulation seconds");
            LootMissileDefeat(game, LootEnemy(game, 1)); LootExpect(game.RecentCombatLoot, 6, 12, 0, false, 1, 1);
            Check(all.coins == 88 && all.ItemCount(5) == 1, "Expiration and a new group cannot mutate the expired snapshot");
        }

        public static void CombatLootAbyssMatchesUnbankedPayoutBankingLossAndEcho(Campaign data)
        {
            for (int risk = 0; risk < 3; risk++)
            {
                GameCore game = AbyssPlanRun(data, 0, risk, 3); game.enemies.Clear(); game.save.reducedMotion = true;
                int coins = game.save.coins, shards = game.save.shards, currentXp = game.save.xp;
                int[] inventory = (int[])game.save.inventory.Clone(); long earnedXp = LootEarnedXp(game);
                LootMissileDefeat(game, LootEnemy(game, 2)); CombatLootReceipt first = game.RecentCombatLoot;
                LootExpect(first, 8 + risk * 3, 14, 1 + risk, true);
                LootMissileDefeat(game, LootEnemy(game, 3, true)); CombatLootReceipt both = game.RecentCombatLoot;
                int runCoins = 78 + risk * 3, runXp = 114, runShards = 6 + risk * 2;
                Check(game.save.runCoins == runCoins && game.save.runXp == runXp && game.save.runShards == runShards
                    && game.save.coins == coins && game.save.shards == shards && game.save.xp == currentXp && LootEarnedXp(game) == earnedXp,
                    "Actual Abyss kills pay only the exact floor balances and leave current XP unbanked");
                for (int index = 0; index < inventory.Length; index++)
                    Check(game.save.inventory[index] == inventory[index] && both.ItemCount(index) == 0, "Abyss combat awards no ordinary inventory item");
                LootExpect(both, runCoins, runXp, runShards, true); LootExpect(first, 8 + risk * 3, 14, 1 + risk, true);
                game.Talk(Object(game, "abyssGate")); AbyssPlanChoose(game, "abyssBank");
                Check(!game.InAbyss && game.save.coins == coins + runCoins && game.save.shards == shards + runShards + 2
                    && LootEarnedXp(game) == earnedXp + runXp && game.save.runCoins == 0 && game.save.runXp == 0 && game.save.runShards == 0,
                    "The real root gate banks exact combat totals plus the separate covenant bonus");
                LootAbsent(game, "actual floor banking and extraction"); LootExpect(both, runCoins, runXp, runShards, true);
            }
            GameCore finalFloor = AbyssPlanRun(data, 0, 2, 3);
            for (int floor = 1; floor < 6; floor++)
            { AbyssPlanGate(finalFloor); AbyssPlanChoose(finalFloor, "boon" + ((floor - 1) % 3)); }
            Enemy reaper = finalFloor.enemies.Find(enemy => enemy.boss);
            Check(finalFloor.save.abyssFloor == 6 && reaper != null && !finalFloor.Has("abyss_complete"),
                "Five actual gate descents reach the authored final-floor Root Reaper before its first clear");
            finalFloor.enemies.RemoveAll(enemy => !ReferenceEquals(enemy, reaper)); finalFloor.save.reducedMotion = true;
            int finalCoins = finalFloor.save.coins, finalShards = finalFloor.save.shards, finalPoints = finalFloor.save.skillPoints, finalLevel = finalFloor.save.level;
            long finalXp = LootEarnedXp(finalFloor); LootMissileDefeat(finalFloor, reaper);
            CombatLootReceipt finalReceipt = finalFloor.RecentCombatLoot; LootExpect(finalReceipt, 70, 100, 7, true);
            Check(finalFloor.Has("boss_8") && finalFloor.save.runCoins == 70 && finalFloor.save.runXp == 100 && finalFloor.save.runShards == 7,
                "Defeating the actual floor-six Reaper records only its real Boss floor payout");
            finalFloor.Talk(Object(finalFloor, "abyssGate")); AbyssPlanChoose(finalFloor, "abyssBank");
            Check(finalFloor.Has("abyss_complete") && finalFloor.save.bestFloor == 6 && finalFloor.save.coins == finalCoins + 70
                && finalFloor.save.shards == finalShards + 7 + 2 + 12 && LootEarnedXp(finalFloor) == finalXp + 100
                && finalFloor.save.skillPoints == finalPoints + 2 + finalFloor.save.level - finalLevel,
                "Actual final-floor banking preserves separate covenant, twelve-shard first-clear and two-point completion rewards");
            LootAbsent(finalFloor, "the authored floor-six bank and first-clear rewards"); LootExpect(finalReceipt, 70, 100, 7, true);

            GameCore lost = AbyssPlanRun(data); lost.enemies.Clear(); lost.save.reducedMotion = true;
            int retainedCoins = lost.save.coins, retainedShards = lost.save.shards; long retainedXp = LootEarnedXp(lost);
            LootMissileDefeat(lost, LootEnemy(lost)); CombatLootReceipt unbanked = lost.RecentCombatLoot;
            LethalShot(lost); LootAbsent(lost, "actual lethal Abyss contact"); Advance(lost, 1.1f);
            Check(!lost.InAbyss && lost.save.coins == Math.Max(0, retainedCoins - 10) && lost.save.shards == retainedShards
                && LootEarnedXp(lost) == retainedXp && lost.save.runCoins == 0 && lost.save.runXp == 0 && lost.save.runShards == 0,
                "Recovery loses actual unbanked loot and applies only the mature ten-coin death penalty");
            LootExpect(unbanked, 8, 14, 1, true); LootAbsent(lost, "completed Abyss recovery");

            GameCore echo = EchoFloor(data); echo.enemies.Clear(); EchoNear(echo); echo.invulnerable = 100;
            int echoCoins = echo.save.runCoins, echoShards = echo.save.runShards; EchoChoice(echo, "echo_begin");
            for (int frame = 0; frame < 500 && echo.RootEchoActive; frame++) echo.Tick(.02f, new Point());
            Check(echo.RootEchoResolved && echo.save.runCoins == echoCoins + 20 && echo.save.runShards == echoShards + echo.RootEchoShards,
                "The real three-pulse Root Echo still applies its separate unbanked event reward");
            LootAbsent(echo, "a Root Echo event reward without a combat defeat");
        }

        public static void CombatLootSpecialActorsLifecycleAndLegacySavesKeepOwnership(Campaign data)
        {
            GameCore range = RangeGame(data); Check(range.StartRange(0), "Begin the actual mentor range");
            int rangeCoins = range.save.coins, rangeXp = range.save.xp; int[] rangeItems = (int[])range.save.inventory.Clone();
            LootMissileDefeat(range, range.enemies[0]);
            Check(range.save.coins == rangeCoins && range.save.xp == rangeXp, "A real range defeat awards no generic currency or XP");
            for (int index = 0; index < 8; index++) Check(range.save.inventory[index] == rangeItems[index], "Range bodies grant no items");
            LootAbsent(range, "an actual range target defeat");
            GameCore sparring = SparringGame(data); Check(sparring.StartSparring(), "Begin the actual sparring lesson");
            int sparCoins = sparring.save.coins, sparXp = sparring.save.xp; LootMissileDefeat(sparring, sparring.enemies[0]);
            Check(sparring.save.coins == sparCoins && sparring.save.xp == sparXp, "An actual sparring defeat awards no generic currency or XP");
            LootAbsent(sparring, "an actual sparring target defeat");
            GameCore commission = CommissionGame(data); Enemy machine = CommissionStart(commission);
            int machineCoins = commission.save.coins, machineXp = commission.save.xp; LootMissileDefeat(commission, machine);
            Check(commission.CommissionTested && commission.save.coins == machineCoins && commission.save.xp == machineXp,
                "The actual commissioning guardian keeps its story checkpoint without generic combat loot");
            LootAbsent(commission, "the commissioning guardian story checkpoint");
            GameCore signal = SignalReadyGame(data); Enemy warden = SignalStart(signal);
            int signalCoins = signal.save.coins, signalXp = signal.save.xp, signalShards = signal.save.shards, signalPoints = signal.save.skillPoints;
            LootMissileDefeat(signal, warden);
            Check(signal.SignalComplete && signal.save.shards == signalShards + 6 && signal.save.skillPoints == signalPoints + 1
                && signal.save.coins == signalCoins && signal.save.xp == signalXp,
                "The actual Warden preserves its special six-shard and talent reward without fake generic loot");
            LootAbsent(signal, "the Warden's special story reward");

            Action<GameCore>[] boundaries = {
                g => g.Enter(0), g => g.Enter(1), g => { g.PrepareSave(); g.Load(CloneSave(g.save)); },
                g => g.save = CloneSave(g.save), g => g.save.room = 0, g => g.NewGame(0),
                g => g.practicing = true, g => LethalShot(g),
                g => typeof(GameCore).GetMethod("ResetCombatPresentation", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(g, null)
            };
            for (int boundary = 0; boundary < boundaries.Length; boundary++)
            {
                GameCore game = Quiet(data); LootMissileDefeat(game, LootEnemy(game)); CombatLootReceipt previous = game.RecentCombatLoot;
                boundaries[boundary](game); LootAbsent(game, "lifecycle boundary " + boundary);
                LootExpect(previous, 6, 12, 0, false, 2, 1);
            }
            GameCore practice = Fresh(data); practice.Close(); practice.enemies.Clear(); practice.save.x = 12; practice.save.y = 14;
            LootMissileDefeat(practice, LootEnemy(practice)); practice.StartPractice();
            Check(practice.practicing, "The real practice transaction starts after a town kill"); LootAbsent(practice, "StartPractice");
            practice.EndPractice(); int practiceCoins = practice.save.coins, practiceXp = practice.save.xp, practicePoints = practice.save.skillPoints;
            int[] practiceItems = (int[])practice.save.inventory.Clone(); PracticeRun(practice);
            Check(practice.save.coins == practiceCoins + 15 && practice.save.xp == practiceXp && practice.save.skillPoints == practicePoints + 1,
                "Actual basic, timed dodge and Q training preserve only the separate once-only fifteen-coin lesson reward");
            for (int index = 0; index < 8; index++) Check(practice.save.inventory[index] == practiceItems[index], "The safe lesson grants no generic inventory loot");
            LootAbsent(practice, "the completed practice lesson and its special reward");
            GameCore mismatch = AbyssPlanRun(data); mismatch.enemies.Clear(); mismatch.save.reducedMotion = true;
            LootMissileDefeat(mismatch, LootEnemy(mismatch)); mismatch.save.abyssFloor = 0;
            LootAbsent(mismatch, "a changed banked versus unbanked ownership type");

            foreach (int version in new[] { 1, 2 })
            {
                GameCore game = Quiet(data); LootMissileDefeat(game, LootEnemy(game)); game.PrepareSave();
                SaveData saved = CloneSave(game.save); saved.version = version;
                int coins = saved.coins, xp = saved.xp, herbs = saved.inventory[2], kills = saved.kills[1];
                GameCore loaded = new GameCore(data, saved); loaded.Start();
                Check(loaded.save.version == 2 && loaded.save.coins == coins && loaded.save.xp == xp
                    && loaded.save.inventory[2] == herbs && loaded.save.kills[1] == kills,
                    "Legacy and current saves preserve actual combat rewards through reconstruction");
                LootAbsent(loaded, "version " + version + " save reconstruction");
            }
            foreach (FieldInfo field in typeof(SaveData).GetFields(BindingFlags.Instance | BindingFlags.Public))
                Check(field.FieldType != typeof(CombatLootReceipt) && field.Name.IndexOf("combatLoot", StringComparison.OrdinalIgnoreCase) < 0,
                    "The receipt adds no persisted combat-loot field to the mature save schema");
        }
    }
}
