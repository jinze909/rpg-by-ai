using System;
using System.Collections.Generic;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore RangeGame(Campaign data, int profession = 0)
        {
            GameCore game = Fresh(data, profession); game.save.flags.Add("core_1");
            WorldObject mentor = Object(game, "mentor"); game.save.x = mentor.x; game.save.y = mentor.y + 1;
            return game;
        }
        private static void RangeChoose(GameCore game, string action)
        {
            Check(game.mode == "dialog" && game.conversation != null, "A range choice requires its actual local mentor menu");
            for (int i = 0; i < game.conversation.choices.Length; i++)
                if (game.conversation.choices[i].action == action) { game.Choose(i); return; }
            throw new Exception("Missing range choice " + action);
        }
        private static void RangeRealAttack(GameCore game, Enemy enemy, bool rear = false)
        {
            game.hitStop = 0; game.attackCooldown = 0; game.playerAction = null; game.save.seed = 1;
            game.save.x = enemy.x + (rear ? 1 : -1) * (game.save.weapon == 0 ? 1 : 3); game.save.y = enemy.y;
            float health = enemy.hp; game.Attack(new Point(rear ? -1 : 1, 0));
            for (int frame = 0; frame < 150 && enemy.hp == health; frame++) game.Tick(.01f, new Point());
            Check(enemy.hp < health, "A real accepted player attack must reach the chosen range body");
        }
        private static void RangeInjectedContact(GameCore game, Enemy enemy, float damage, bool playerOwned, bool technique = false)
        {
            typeof(GameCore).GetMethod("Hit", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game,
                new object[] { enemy, damage, technique, (Point?)new Point(1, 0), new ImpactProfile(0, 0, 0, 0, 0, playerOwned) });
        }

        public static void RangeMenusPreserveMatureAdaChoicesAndRequireEarnedLocalControl(Campaign data)
        {
            GameCore game = RangeGame(data); game.Talk(Object(game, "mentor"));
            string[] prefix = { "practice", "close", "sparring_menu", "fieldcraft_offer", "range_menu" };
            Check(game.conversation.choices.Length == 5, "The range appends exactly one optional Ada choice");
            for (int i = 0; i < prefix.Length; i++) Check(game.conversation.choices[i].action == prefix[i], "All four mature mentor actions keep their original indices");
            RangeChoose(game, "range_menu");
            Check(game.conversation.choices.Length == 4, "Choose one of three target arrangements or leave");
            RangeChoose(game, "range_start_1");
            Check(game.RangeActive && game.SafeTrainingActive && !game.SparringActive && game.RangeLayout == 1,
                "The actual fifth mentor choice opens an independent range without entering the rewarded sparring course");
            Check(!game.StartRange(0) && !game.StartSparring(), "An active rehearsal cannot stack training sessions"); game.EndPractice();
            game.RestartTutorial(); Check(game.StartRange(0), "A deliberately restarted tutorial does not remove an earned range");
            WorldObject nearby = Object(game, "mentor"); game.save.x = nearby.x; game.save.y = nearby.y + 1; game.Interact();
            Check(game.RangeActive && game.mode == "world" && game.save.tutorialBits == 0,
                "A rejected resident interaction inside the range cannot silently advance a restarted world tutorial"); game.EndPractice();
            GameCore novice = Fresh(data); WorldObject mentor = Object(novice, "mentor"); novice.save.x = mentor.x; novice.save.y = mentor.y + 1;
            Check(!novice.RangeReady && !novice.StartRange(0), "The new range never skips the mature basic lesson or first-core gate");
            novice.save.practiceBits = 7; Check(novice.RangeReady && novice.StartRange(2), "The original basic graduation also opens the new range"); novice.EndPractice();
            foreach (string mode in new[] { "pause", "inventory", "plans", "dialog", "title" })
            { game.mode = mode; Check(!game.StartRange(0), "Stale start callbacks reject " + mode); }
            game.Close(); game.save.x = 29; game.save.y = 19;
            Check(!game.StartRange(0), "The real local mentor is required, not merely town membership");
            game.save.x = mentor.x; game.save.y = mentor.y + 1;
            Check(!game.StartRange(-1) && !game.StartRange(3), "Malformed arrangement indices cannot mutate the current world");
            game.Enter(1); Check(!game.StartRange(0), "The range cannot replace a mature encounter in a foreign region");
        }

        public static void RangeThreeClassesAndLayoutsUseRealContactsAndClearWithoutRewards(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++) for (int layout = 0; layout < 3; layout++)
            {
                GameCore game = RangeGame(data, profession); int money = game.save.coins, xp = game.save.xp, points = game.save.skillPoints;
                int[] inventory = (int[])game.save.inventory.Clone(), mastery = (int[])game.save.mastery.Clone(), kills = (int[])game.save.kills.Clone();
                int practice = game.save.practiceBits, tutorial = game.save.tutorialBits, flags = game.save.flags.Count;
                Check(game.StartRange(layout), "Each starter can deliberately enter each authored arrangement");
                int expected = layout == 2 ? 1 : 3; Check(game.enemies.Count == expected, "Each arrangement keeps its actual target count");
                var feet = new Dictionary<int, Point>();
                foreach (Enemy enemy in game.enemies)
                {
                    Check(game.IsRangeEnemy(enemy) && !enemy.boss && enemy.maxHp == 300 && enemy.hp == 300 &&
                        game.CanStand(enemy.Position) && enemy.facing.x == -1 && enemy.facing.y == 0,
                        "Every target has a finite valid 300-HP canonical body with an unchanged westward armor heading");
                    feet.Add(enemy.id, enemy.Position);
                }
                Advance(game, .7f);
                Check(!game.RangeStarted && game.RangeTime == 0 && game.enemies.TrueForAll(e => (e.Position - feet[e.id]).Length < .001f),
                    "Idle range targets never move, attack or start the clock");
                Combat(game, false); Advance(game, .05f);
                Check(game.RangeFinished && game.RangeStarted && game.RangeTime > 0 && game.RangeContacts > 0 && Math.Abs(game.RangeDamage - expected * 300) < .02f,
                    "Real starter weapons and techniques clear all full-health targets and record exactly their health, excluding overkill");
                Check(game.save.coins == money && game.save.xp == xp && game.save.skillPoints == points && game.save.flags.Count == flags &&
                    game.save.practiceBits == practice && game.save.tutorialBits == tutorial && !game.Has("sparring_complete") && !game.Has("practice_reward"),
                    "Range victories cannot mint rewards, journal events, tutorial completion or either mature practice reward");
                for (int i = 0; i < inventory.Length; i++) Check(game.save.inventory[i] == inventory[i], "Range contacts never consume or grant campaign items");
                for (int i = 0; i < mastery.Length; i++) Check(game.save.mastery[i] == mastery[i], "Range clears cannot farm mastery");
                for (int i = 0; i < kills.Length; i++) Check(game.save.kills[i] == kills[i], "Range targets never enter campaign kill counts");
                float time = game.RangeTime, energy = game.save.energy; game.Attack(new Point(1, 0)); game.Skill(); game.Dash(new Point(1, 0)); Advance(game, .2f);
                Check(game.RangeTime == time && game.playerAction == null && game.projectiles.Count == 0 && game.dashTime == 0 && game.save.energy >= energy,
                    "A finished result freezes its clock and rejects paid combat starts until a deliberate reset");
                game.EndPractice();
            }
        }

        public static void RangeArmorFlanksBreaksAndStunKeepActualSimulation(Campaign data)
        {
            GameCore front = RangeGame(data); Check(front.StartRange(2), "Begin frontal comparison"); Enemy armored = front.enemies[0];
            float before = armored.hp; RangeRealAttack(front, armored); float guarded = before - armored.hp;
            Check(front.RangeContacts == 1 && Math.Abs(front.RangeDamage - guarded) < .001f && front.RangeFlanks == 0 &&
                front.effects.Exists(e => e.kind == "number" && e.text == front.L("正面护甲", "GUARDED")),
                "Front armor records the actual reduced contact, retaining the mature guarded feedback");
            GameCore rear = RangeGame(data); Check(rear.StartRange(2), "Begin rear comparison"); Enemy flanked = rear.enemies[0];
            before = flanked.hp; RangeRealAttack(rear, flanked, true); float unguarded = before - flanked.hp;
            Check(Math.Abs(unguarded / guarded - 2.5f) < .001f && rear.RangeFlanks == 1 && rear.RangeContacts == 1,
                "Approaching the same stationary armor from behind preserves the real .4 mitigation and genuine flank count");
            GameCore breaker = RangeGame(data, 2); Check(breaker.StartRange(2), "Begin technique and break comparison"); Enemy heavy = breaker.enemies[0];
            breaker.save.x = heavy.x - 2; breaker.save.y = heavy.y; breaker.Skill(new Point(1, 0));
            for (int frame = 0; frame < 100 && breaker.RangeContacts == 0; frame++) breaker.Tick(.01f, new Point());
            Check(breaker.RangeContacts == 1 && breaker.RangeBreaks == 0 && heavy.stagger > 0 && heavy.stun > 0,
                "The first actual starter nova builds its mature stagger amount without inventing a break");
            Advance(breaker, 3.8f); breaker.Skill(new Point(1, 0));
            for (int frame = 0; frame < 100 && breaker.RangeContacts < 2; frame++) breaker.Tick(.01f, new Point());
            Check(breaker.RangeBreaks == 1 && heavy.stun > 0 && heavy.staggerRecovery > 0 && heavy.hitTime > 0,
                "A real damaging class technique retains ordinary guard bypass, break, hit and stun windows");
            Point feet = heavy.Position; Point facing = heavy.facing; Advance(breaker, 2.5f);
            Check(heavy.stun <= 0 && heavy.staggerRecovery <= 0 && heavy.hitTime <= 0 && heavy.flash <= 0 &&
                (heavy.Position - feet).Length < .001f && (heavy.facing - facing).Length < .001f && GameCore.EnemyHasFrontalArmor(heavy),
                "Control and recovery clocks expire normally while fixed target feet and headings never drift through recoil or pursuit");
        }

        public static void RangeDamageCountsOnlyAcceptedPlayerOwnedPositiveHealthLoss(Campaign data)
        {
            GameCore game = RangeGame(data); Check(game.StartRange(0), "Begin contact accounting"); Enemy target = game.enemies[0];
            RangeInjectedContact(game, target, 7, false);
            Check(game.RangeContacts == 0 && game.RangeDamage == 0 && !game.RangeStarted,
                "A foreign contact cannot claim player damage, a contact, or an accepted action start");
            target.hp = 2; RangeInjectedContact(game, target, 999, true);
            Check(game.RangeContacts == 1 && Math.Abs(game.RangeDamage - 2) < .001f,
                "A lethal player-owned contact caps its measured damage to the positive remaining two health");
            RangeInjectedContact(game, target, 999, true);
            Check(game.RangeContacts == 1 && Math.Abs(game.RangeDamage - 2) < .001f,
                "A removed target cannot double-count a stale hit or overkill");
            int coins = game.save.coins, xp = game.save.xp, mastery = game.save.mastery[0], kills = game.save.kills[0];
            Enemy foreign = new Enemy(54321, 0, 16, 14, 1); game.enemies.Add(foreign); RangeInjectedContact(game, foreign, 999, true);
            Check(game.RangeContacts == 1 && game.save.coins == coins && game.save.xp == xp && game.save.mastery[0] == mastery && game.save.kills[0] == kills,
                "Even a noncanonical foreign actor defeated inside the safe range cannot pay world rewards or claim range damage");
            Enemy live = game.enemies[0]; RangeInjectedContact(game, live, 0, true);
            Check(game.RangeContacts == 1 && Math.Abs(game.RangeDamage - 2) < .001f,
                "Zero health loss never increments an impact count");
        }

        public static void RangeAcceptedActionTimingMenusAndSafeVitalsStayIsolated(Campaign data)
        {
            GameCore game = RangeGame(data, 1); game.save.health = 43; game.save.energy = 27; game.save.heat = 31; game.save.cold = 17;
            game.save.foodUntil = 71; game.save.teaUntil = 81; game.save.mealKind = 1; game.save.mealUntil = 91;
            game.save.flags.Add("companion_0"); game.save.companion = 0; game.save.bounty = 0; game.save.bountyProgress = 2;
            Point origin = game.Position; float worldTime = game.save.totalTime; int bits = game.save.tutorialBits;
            Check(game.StartRange(0), "Begin timing and resource isolation"); SparringReturnRecord body = game.save.sparringReturn;
            Advance(game, 1); Check(game.RangeTime == 0 && game.save.totalTime == worldTime, "Idle target viewing advances neither attempt nor world time");
            game.attackCooldown = 1; game.Attack(new Point(1, 0)); Check(!game.RangeStarted, "A rejected cooldown input cannot start the timer");
            game.attackCooldown = 0; game.Attack(new Point(-1, 0)); Check(game.RangeStarted && game.RangeTime == 0 && game.playerAction != null,
                "A real accepted whiff starts the timer in its preparation, before any hit");
            game.Tick(.05f, new Point()); Check(game.RangeTime > 0 && game.RangeContacts == 0, "Preparation time counts without inventing a target contact");
            foreach (string mode in new[] { "pause", "inventory", "plans", "journal" })
            {
                game.mode = mode; float time = game.RangeTime; Point p = game.Position; int layout = game.RangeLayout;
                game.Tick(.05f, new Point(1, 0));
                Check(game.RangeTime == time && (game.Position - p).Length == 0 && !game.ResetRange() && !game.ChangeRangeLayout(2) && game.RangeLayout == layout,
                    "Every paused menu freezes the range and rejects stale live reset controls: " + mode);
            }
            game.Close(); game.hitStop = game.invulnerable = 0;
            game.projectiles.Add(new Projectile { position = game.Position, source = "shot", damage = 999, life = 1 }); game.Tick(.02f, new Point());
            Check(game.save.health == 43 && !game.Dying && game.SparringHitsTaken == 0 && game.playerHitTime > 0,
                "Even a foreign hostile projectile keeps real hurt feedback without health loss or sparring statistics");
            int stew = game.save.inventory[6], tea = game.save.tea; game.Eat(); game.DrinkTea(); game.CommandCompanion();
            Check(game.save.inventory[6] == stew && game.save.tea == tea && game.companionAction == null && game.save.bountyProgress == 2 && game.save.tutorialBits == bits,
                "Supply, companion, tutorial and bounty isolation follows actual practicing ownership");
            Check(game.save.totalTime == worldTime && game.save.foodUntil == 71 && game.save.teaUntil == 81 && game.save.mealUntil == 91,
                "Practice does not spend live world food, tea or meal duration");
            game.ReturnToTown();
            Check(!game.RangeActive && !game.practicing && game.save.sparringReturn == null && (game.Position - origin).Length < .001f &&
                game.save.health == 43 && game.save.energy == 27 && game.save.heat == 31 && game.save.cold == 17,
                "The actual town-return path exits to the exact original body, not a new town spawn");
            Check(body != null, "A shared legacy-compatible recovery body is recorded before the temporary session");
        }

        public static void RangeManualResetsSeverPreparedMissilesAndPersistentFrost(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++)
            {
                GameCore game = RangeGame(data, profession); Check(game.StartRange(0), "Begin delayed-contact reset");
                SparringReturnRecord body = game.save.sparringReturn;
                game.save.flags.Add("technique_" + profession); game.save.technique = 1;
                game.Skill(new Point(1, 0)); Advance(game, .35f);
                if (profession == 2) Check(game.effects.Exists(e => e.kind == "frost"), "The original caster field really exists before reset");
                game.Attack(new Point(1, 0)); game.Skill(new Point(1, 0));
                Check(game.ChangeRangeLayout(1) && game.RangeLayout == 1 && !game.RangeStarted && !game.RangeFinished && game.RangeTime == 0 && game.RangeDamage == 0 &&
                    game.RangeContacts == 0 && game.playerAction == null && game.projectiles.Count == 0 && game.hazards.Count == 0 &&
                    System.Object.ReferenceEquals(body, game.save.sparringReturn),
                    "Changing targets retires every old contact and clock before creating fresh actors, retaining the first recovery body");
                Advance(game, 4.5f);
                Check(game.enemies.TrueForAll(e => e.hp == 300) && !game.effects.Exists(e => e.kind == "frost") && game.RangeContacts == 0,
                    "Old arrows, buffered techniques and all remaining frost pulses cannot hit the reused IDs in a later arrangement");
                RangeRealAttack(game, game.enemies[0]); Enemy sameBody = game.enemies[0]; int contacts = game.RangeContacts;
                float damage = game.RangeDamage, time = game.RangeTime, energy = game.save.energy;
                Check(!game.ChangeRangeLayout(1) && System.Object.ReferenceEquals(sameBody, game.enemies[0]) &&
                    game.RangeContacts == contacts && game.RangeDamage == damage && game.RangeTime == time && game.save.energy == energy,
                    "Choosing the already selected layout is an honest no-op rather than a hidden reset or resource refill");
                Check(game.ResetRange() && game.RangeLayout == 1 && game.RangeContacts == 0 && game.RangeDamage == 0 && game.save.energy == 100,
                    "The manual reset retains its chosen arrangement and explicitly starts a fresh full-energy comparison");
                game.mode = "plans"; Check(game.SaveBattlePlan(0), "The actual range permits deliberate owned battle-plan preparation through Inventory");
                game.Close(); game.EndPractice(); Advance(game, 4.5f);
                Check(game.enemies.Count == 0 && game.projectiles.Count == 0 && !game.effects.Exists(e => e.kind == "frost") && game.BattlePlanAt(0) != null,
                    "Leaving removes every combat context while keeping deliberate equipment-plan work");
            }
        }

        public static void RangeSaveRecoveryAndReservedActorsKeepCampaignAndAbyssBoundaries(Campaign data)
        {
            for (int version = 1; version <= 2; version++)
            {
                GameCore game = RangeGame(data, 1); game.save.health = 51; game.save.energy = 39; Point origin = game.Position;
                Check(game.StartRange(0), "Begin a save checkpoint rehearsal"); RangeRealAttack(game, game.enemies[0]); game.PrepareSave();
                Check(game.RangeActive && game.save.snapshotRoom == -1 && game.save.worldEnemies == null && game.save.sparringReturn != null,
                    "Preparing a save captures a safe return body and never serializes temporary range actors");
                game.save.version = version; var loaded = new GameCore(data, game.save); loaded.Start();
                Check(!loaded.practicing && !loaded.RangeActive && !loaded.SparringActive && loaded.save.sparringReturn == null && loaded.enemies.Count == 0 &&
                    (loaded.Position - origin).Length < .001f && loaded.save.health == 51 && loaded.save.energy == 39 && loaded.Has("core_1"),
                    "Version " + version + " recovers the original town body without resuming a range or granting completion");
            }
            var records = new List<EnemyRecord>();
            foreach (int id in new[] { 79000, 79001, 79002 }) records.Add(new EnemyRecord { id = id, kind = 2, boss = true, x = 15, y = 17, hp = 23, maxHp = 300 });
            records.Add(new EnemyRecord { id = 17, kind = 0, x = 15, y = 17, hp = 23, maxHp = 52 });
            var ordinary = new GameCore(data, new SaveData { room = 1, stage = 1, x = 14, y = 15, snapshotRoom = 1,
                flags = new List<string> { "intro", "core_1" }, worldEnemies = records.ToArray() }); ordinary.Start();
            Check(ordinary.enemies.Count == 1 && ordinary.enemies[0].id == 17 && ordinary.enemies[0].hp == 23,
                "Reserved range identities never restore as ordinary actors or forged campaign Bosses");
            var abyss = new GameCore(data, new SaveData { room = 8, stage = 3, frontierEnding = 1, x = 4, y = 14,
                abyssFloor = 1, abyssSnapshotFloor = 1, abyssEnemies = records.ToArray(),
                flags = new List<string> { "intro", "core_1", "exp_intro", "harbor_report", "desert_report", "mountain_report" } }); abyss.Start();
            Check(abyss.enemies.Count == 1 && abyss.enemies[0].id == 17 && !abyss.RangeActive,
                "The independent abyss restore path also rejects every reserved range actor");
            Check(!ordinary.IsRangeEnemyId(76000) && !ordinary.IsRangeEnemyId(75000) && !ordinary.IsRangeEnemyId(78000),
                "The range's reserved namespace cannot consume mature riverbank, sparring or commissioning identities");
            GameCore departure = RangeGame(data); departure.save.frontierEnding = 1; Check(departure.StartRange(0), "Begin direct travel guards");
            departure.StartAbyss(0); Check(departure.RangeActive && departure.save.room == 0, "A direct expedition callback cannot skip the range return transaction");
            departure.Enter(1); Check(!departure.practicing && !departure.RangeActive && departure.save.sparringReturn == null && departure.save.room == 1 &&
                departure.enemies.TrueForAll(e => !departure.IsRangeEnemyId(e.id)), "Actual region travel tears down only the rehearsal before spawning the mature destination");
        }
    }
}
