using System;
using System.Collections.Generic;
using System.Text;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore HuntGame(Campaign data, int contract, int room = 1)
        {
            var game = Unlocked(data); game.SkipTutorial(); game.Enter(room); game.Close();
            game.enemies.Clear(); game.projectiles.Clear(); game.hazards.Clear(); game.effects.Clear();
            game.save.companion = -1; game.invulnerable = 10000; game.AcceptBounty(contract);
            Check(game.save.bounty == contract && game.save.trackedQuest == "bounty", "The actual Contracts action accepts and tracks the requested hunt");
            return game;
        }
        private static bool HuntSame(Point a, Point b) { return (a - b).Length < .001f; }
        private static Point HuntPoint(GameCore game, float minimum, float maximum)
        {
            Point origin = game.Position; Point best = new Point(); float nearest = float.MaxValue;
            for (int y = 1; y < game.Map.rows.Length - 1; y++) for (int x = 1; x < game.Map.rows[y].Length - 1; x++)
            {
                Point point = new Point(x + .5f, y + .5f); float distance = (point - origin).Length;
                if (distance >= minimum && distance <= maximum && distance < nearest && game.CanStand(point) && game.NavigationPath(point).Count > 0)
                { nearest = distance; best = point; }
            }
            Check(nearest < float.MaxValue, "The authored map supplies a real full-footprint reachable hunt fixture"); return best;
        }
        private static Enemy HuntActor(GameCore game, int id, int kind, Point point, bool boss = false)
        { var enemy = new Enemy(id, kind, point.x, point.y, 1, boss); enemy.timer = 9999; game.enemies.Add(enemy); return enemy; }
        private static void HuntTarget(GameCore game, Enemy enemy, string reason)
        {
            QuestGuide guide = game.GetQuest("bounty"); Point? nav = game.NavigationTarget();
            Check(guide.room == game.save.room && HuntSame(guide.target, enemy.Position), reason);
            Check(nav.HasValue && HuntSame(nav.Value, enemy.Position), "The active contract owns the same live navigation coordinate: " + reason);
            Check((game.Position - enemy.Position).Length < 1.8f || game.NavigationPath(guide.target).Count > 0
                || (new Point((int)game.Position.x + .5f, (int)game.Position.y + .5f) - enemy.Position).Length <= 1.2f,
                "The selected live actor is approachable with the existing NavigationPath: " + reason);
            List<Point> guidance = game.GuidancePath(nav.Value), path = game.NavigationPath(nav.Value);
            Check(guidance.Count == path.Count, "Combat guidance uses the existing actor path instead of an altar interaction path");
            for (int i = 0; i < path.Count; i++) Check(HuntSame(path[i], guidance[i]), "Every combat guidance waypoint preserves the existing navigation geometry");
        }
        private static void HuntObject(GameCore game, string kind, string reason)
        {
            WorldObject actual = Object(game, kind); QuestGuide guide = game.GetQuest("bounty");
            Check(guide.room == game.save.room && HuntSame(guide.target, new Point(actual.x, actual.y)), reason + " -> actual " + kind);
        }
        private static void HuntKill(GameCore game, Enemy enemy)
        {
            game.Close(); game.save.energy = 100; game.attackCooldown = 0; game.hitStop = 0;
            bool stood = false;
            foreach (Point offset in new[] { new Point(-1, 0), new Point(1, 0), new Point(0, -1), new Point(0, 1) })
            {
                Point foot = enemy.Position + offset;
                if (!game.CanStand(foot)) continue;
                game.save.x = foot.x; game.save.y = foot.y; stood = true; break;
            }
            Check(stood, "A real normal attack starts from a legal nearby foot position");
            game.Attack(enemy.Position - game.Position); Check(game.playerAction != null, "A hunt kill starts through the actual prepared weapon attack");
            for (int frame = 0; frame < 90 && game.enemies.Contains(enemy); frame++) game.Tick(.02f, new Point());
            Check(!game.enemies.Contains(enemy) && game.fallen.Exists(f => ReferenceEquals(f.actor, enemy)), "The real Hit path consumes the live actor and records its death");
            Advance(game, .7f);
        }

        public static void ContractHuntLiveTargetsFollowNearestReachableCountingActors(Campaign data)
        {
            foreach (int contract in new[] { 0, 3, 4 })
            {
                var game = HuntGame(data, contract); Point near = HuntPoint(game, 2.2f, 3.5f), far = HuntPoint(game, 4.2f, 6);
                int kind = contract == 4 ? 2 : contract == 3 ? 3 : 0; bool boss = contract == 3;
                Enemy farther = HuntActor(game, 62002, kind, far, boss), nearest = HuntActor(game, 62001, kind, near, boss);
                Point ignored = game.Position;
                HuntActor(game, 62010, kind, ignored, boss).hp = 0;
                HuntActor(game, 62011, kind, ignored, boss).hp = float.NaN;
                HuntActor(game, 62012, kind, ignored, boss).hp = float.PositiveInfinity;
                HuntActor(game, 62013, kind, new Point(float.NaN, ignored.y), boss);
                HuntActor(game, 62014, kind, new Point(float.PositiveInfinity, ignored.y), boss);
                HuntActor(game, 62015, kind, new Point(0, 0), boss);
                foreach (int id in new[] { 75000, 75010, 75020, 75021, 79000, 79001, 79002 }) HuntActor(game, id, kind, ignored, boss);
                HuntActor(game, GameCore.SignalWardenId, 3, ignored, true);
                HuntActor(game, GameCore.CommissionGuardianId, 3, ignored, true);
                if (contract == 3) HuntActor(game, 62016, 2, ignored);
                if (contract == 4) HuntActor(game, 62017, 0, ignored);
                // The authored forest cache enclosure has legal interior feet
                // behind the still-locked root exploration barrier.
                Point sealedPoint = new Point(4.5f, 19.5f);
                Check(game.CanStand(sealedPoint) && game.NavigationPath(sealedPoint).Count == 0,
                    "The authored sealed cache supplies a genuinely unreachable live actor");
                Enemy sealedActor = HuntActor(game, 62018, kind, sealedPoint, boss);
                HuntTarget(game, nearest, "An unfinished contract selects the nearest reachable counting live actor for type " + contract);
                nearest.x = far.x; nearest.y = far.y; farther.x = near.x; farther.y = near.y;
                HuntTarget(game, farther, "Moving actors are re-evaluated immediately without caching an old target");
                farther.hp = 0;
                HuntTarget(game, nearest, "A dead current target switches to the next live eligible actor");
                game.enemies.Remove(nearest); game.enemies.Remove(sealedActor); game.enemies.RemoveAll(e => e.hp <= 0);
                Check(!HuntSame(game.GetQuest("bounty").target, sealedPoint), "A sealed actor never becomes a fabricated reachable objective");
                game.enemies.Clear(); game.save.x = 7.5f; game.save.y = 19.5f;
                Enemy blockedNearest = HuntActor(game, 62020, kind, sealedPoint, boss);
                Enemy accessible = HuntActor(game, 62021, kind, HuntPoint(game, 4.2f, 6), boss);
                Check((blockedNearest.Position - game.Position).Length < (accessible.Position - game.Position).Length
                    && game.NavigationPath(blockedNearest.Position).Count == 0, "The nearest live actor really lies behind the existing locked root");
                HuntTarget(game, accessible, "Reachability filters the physically closest actor before choosing the nearest usable combat target");
            }
            foreach (int id in new[] { 72000, 74000, 76000, 77000 })
            {
                var game = HuntGame(data, 0); Enemy actor = HuntActor(game, id, 0, HuntPoint(game, 2.2f, 3.5f));
                HuntTarget(game, actor, "Watch, Archive, Reach and Homeward normal actors retain ordinary bounty eligibility: " + id);
            }
            var nearCell = HuntGame(data, 4); nearCell.save.x = 19.01f; nearCell.save.y = 13.01f;
            Enemy cellActor = HuntActor(nearCell, 62030, 2, new Point(20.3f, 14.3f));
            Check(nearCell.CanStand(nearCell.Position) && nearCell.CanStand(cellActor.Position) && nearCell.NavigationPath(cellActor.Position).Count == 0
                && (cellActor.Position - nearCell.Position).Length > 1.8f,
                "The real navigation start-cell shortcut can return no waypoints just outside direct attack range");
            HuntTarget(nearCell, cellActor, "A legally approachable start-cell neighbor remains a local hunt instead of incorrectly routing back to town");
            var moving = HuntGame(data, 0); Enemy walker = HuntActor(moving, 62031, 0, HuntPoint(moving, 2.2f, 3.5f)); Point oldFoot = walker.Position;
            moving.Tick(.05f, new Point());
            Check(!HuntSame(walker.Position, oldFoot), "The live target really moves through the existing enemy simulation");
            HuntTarget(moving, walker, "A fresh query and guidance path follow the simulated moving actor's current feet");
        }

        public static void ContractHuntRealKillsRewardsAndNonCombatContractsStayOriginal(Campaign data)
        {
            foreach (int contract in new[] { 0, 3, 4 }) foreach (int ending in new[] { 1, 3 })
            {
                var game = HuntGame(data, contract); game.save.frontierEnding = ending; game.save.level = 20; game.save.xp = 0;
                int wanted = GameCore.BountyTargets[contract];
                for (int count = 0; count < wanted; count++)
                {
                    Enemy actor = HuntActor(game, 63000 + count, contract == 4 ? 2 : contract == 3 ? 3 : 0,
                        HuntPoint(game, 2.2f, 3.5f), contract == 3);
                    HuntTarget(game, actor, "The hunt points to the same actor that the actual attack will defeat");
                    HuntKill(game, actor);
                    Check(game.save.bountyProgress == count + 1, "Real Hit -> OnExpansionKill -> BountyEvent advances exactly one accepted hunt step");
                }
                QuestGuide ready = game.GetQuest("bounty");
                WorldObject ledger = Array.Find(data.maps[0].objects, o => o.kind == "contract");
                Check(ready.room == 0 && HuntSame(ready.target, new Point(ledger.x, ledger.y)), "A completed hunt returns to the actual town Contracts object");
                int coins = game.save.coins, shards = game.save.shards, xp = game.save.xp, cycle = game.save.bountyCycle;
                Check(game.ClaimBounty() && game.save.coins == coins + 35 && game.save.xp == xp + 30 && game.save.shards == shards + (ending == 3 ? 4 : 3)
                    && game.save.bounty == -1 && game.save.bountyProgress == 0 && game.save.bountyCycle == cycle + 1,
                    "Original reward stays 35 coins, 30 XP, 3 shards and the existing ending-three extra shard");
                coins = game.save.coins; shards = game.save.shards; xp = game.save.xp;
                Check(!game.ClaimBounty() && game.save.coins == coins && game.save.shards == shards && game.save.xp == xp,
                    "The actual claim remains exactly once after the hunt is consumed");
            }
            foreach (int id in new[] { 72000, 74000, 76000, 77000 })
            {
                var game = HuntGame(data, 0); var actor = HuntActor(game, id, 0, HuntPoint(game, 2.2f, 3.5f)); HuntKill(game, actor);
                Check(game.save.bountyProgress == 1, "Normal task actor deaths still reach the mature kill counter: " + id);
            }
            foreach (int kind in new[] { 0, 1, 2, 3 })
            {
                var any = HuntGame(data, 0); Enemy actor = HuntActor(any, 63010 + kind, kind, HuntPoint(any, 2.2f, 3.5f), kind == 3);
                HuntTarget(any, actor, "The general kill contract accepts every real ordinary enemy kind including guardians"); HuntKill(any, actor);
                Check(any.save.bountyProgress == 1, "Any true kill contributes exactly once to contract zero: kind " + kind);
            }
            var ore = HuntGame(data, 1); HuntObject(ore, "ore", "The mineral contract retains its actual remaining resource node");
            WorldObject node = Object(ore, "ore"); ore.Talk(node); ore.Talk(node);
            Check(ore.save.bountyProgress == 1, "A real mineral gather still advances once, without query-driven renewal");
            var fish = HuntGame(data, 2, 0); HuntObject(fish, "fish", "The fish contract retains its original town fishing bank");
            Action(fish, "fish", "fish");
            for (int hit = 0; hit < 3; hit++)
            { int frames = 0; do { fish.Tick(.02f, new Point()); } while ((Math.Abs(fish.fishCursor - .5f) > .08f || frames++ < 5) && frames < 1000); fish.FishHit(); }
            Check(fish.save.bountyProgress == 1 && fish.save.inventory[3] == 1, "The actual original three-pull fishing action still counts its landed fish");
            var abyss = HuntGame(data, 5, 0); abyss.StartAbyss(0); Combat(abyss, false);
            Check(abyss.save.bountyProgress == 0, "Clearing enemies does not prematurely complete a bank-floor contract");
            Action(abyss, "abyssGate", "boon0");
            Check(abyss.save.bountyProgress == 1 && abyss.save.abyssFloor == 2, "The original real east-gate banking action still advances the expedition contract");
        }

        public static void ContractHuntFallbacksGuardianPrerequisitesAndNavigationOwnership(Campaign data)
        {
            foreach (int contract in new[] { 0, 4 }) for (int room = 1; room <= 7; room++)
            {
                var game = HuntGame(data, contract, room); QuestGuide guide = game.GetQuest("bounty");
                int expected = room == 1 ? 2 : 1;
                Check(guide.room == expected && game.CanEnter(guide.room) && HuntSame(guide.target, new Point(data.maps[expected].spawnX, data.maps[expected].spawnY)),
                    "An empty region routes to another enterable real region spawn, not a fictitious local target");
            }
            foreach (int contract in new[] { 0, 4 })
            {
                var game = Fresh(data); game.SkipTutorial(); game.AcceptBounty(contract); game.Enter(1); game.enemies.Clear();
                QuestGuide guide = game.GetQuest("bounty"); WorldObject townForest = Array.Find(data.maps[0].objects, o => o.kind == "portal1");
                Check(guide.room == 0 && HuntSame(guide.target, new Point(townForest.x, townForest.y)), "With only Thornwood unlocked, the real town portal is the revisit objective");
                game.Enter(0); game.Enter(1); Check(game.enemies.Count > 0 && game.GetQuest("bounty").room == 1, "Actual re-entry repopulates the mature regional placements for the hunt");
            }
            for (int room = 1; room <= 7; room++)
            {
                var game = HuntGame(data, 3, room); game.save.flags.Remove("gate_" + room); game.save.flags.Remove("scout_saved");
                game.save.flags.Remove("lore_2"); game.save.inventory[4] = 0; game.save.harborPledge = 0;
                game.save.mirrors = new int[3]; game.save.bells = new int[3];
                if (room == 1)
                {
                    game.save.kills[1] = 4; HuntObject(game, "scout", "An unspawned guardian follows its actual scout rescue prerequisite");
                    Action(game, "scout", "scout");
                }
                if (room == 2)
                {
                    HuntObject(game, "lore", "The ruins first route to the real record"); game.Talk(Object(game, "lore")); game.Close();
                    foreach (int rune in new[] { 0, 2, 1 }) { HuntObject(game, "rune" + rune, "The next tablet uses the actual in-progress rune order"); game.Talk(Object(game, "rune" + rune)); }
                }
                if (room == 3)
                {
                    HuntObject(game, "ore", "The broken elevator routes to an actual remaining part source"); game.Talk(Object(game, "ore"));
                    HuntObject(game, "engine", "Enough gathered parts route back to the actual elevator"); Action(game, "engine", "engine");
                }
                if (room == 5)
                {
                    game.save.flags.Remove("harbor_report");
                    HuntObject(game, "captain", "The harbor starts at Mira's actual route decision"); game.save.inventory[0] = game.save.inventory[2] = game.save.inventory[4] = 10;
                    Action(game, "captain", "pledge3");
                    for (int i = 0; i < 2; i++) { HuntObject(game, "castaway" + i, "The coordinated route finds each missing real rescue"); Action(game, "castaway" + i, "rescue" + i); }
                    HuntObject(game, "relay0", "The coordinated route switches to its missing real lamp relay"); Action(game, "relay0", "relay0");
                }
                if (room == 6)
                {
                    int[] wanted = { 1, 2, 1 };
                    for (int i = 0; i < 3; i++) { HuntObject(game, "mirror" + i, "The next wrong mirror is an actual authored object"); while (game.save.mirrors[i] != wanted[i]) game.Talk(Object(game, "mirror" + i)); }
                }
                if (room == 7)
                {
                    int[] wanted = { 2, 0, 1 };
                    HuntObject(game, "bell0", "The unsolved bell array routes to the first wrong real bell");
                    for (int i = 0; i < 3; i++) while (game.save.bells[i] != wanted[i]) game.Talk(Object(game, "bell" + i));
                }
                game.Close(); Check(game.GateOpen, "The original actual prerequisite interactions still open guardian gate " + room); AwaitArenaOpening(game);
                string altar = room < 5 ? "shrine" : "expAltar"; HuntObject(game, altar, "An open gate routes to the actual challenge altar");
                game.save.x = 28; game.save.y = 10; Action(game, altar, room < 5 ? "boss" : "expboss");
                Enemy guardian = game.enemies.Find(e => e.boss); Check(guardian != null, "The real altar challenge spawns the original guardian");
                HuntTarget(game, guardian, "A spawned guardian immediately replaces its prerequisite and altar");
                game.enemies.Clear(); game.save.flags.Add("boss_" + room);
                Check(game.GetQuest("bounty").room != room, "A resolved guardian altar is never selected again");
            }
            foreach (int pledge in new[] { 1, 2 })
            {
                var harbor = HuntGame(data, 3, 5); harbor.save.flags.Remove("gate_5"); harbor.save.harborPledge = pledge;
                HuntObject(harbor, pledge == 1 ? "relay0" : "castaway0", "Both established harbor priorities keep their real outstanding first objective");
            }
            foreach (int ending in new[] { 0, 1 })
            {
                var game = HuntGame(data, 3, 0); game.save.frontierEnding = ending;
                for (int room = 1; room <= 7; room++) game.save.flags.Add("boss_" + room);
                QuestGuide guide = game.GetQuest("bounty");
                if (ending > 0) HuntObject(game, "abyssEntry", "After all real guardians, the next challenge uses the actual town fissure entrance");
                else
                {
                    QuestGuide story = game.GetQuest("shore"); Check(!story.locked && guide.room == story.room && HuntSame(guide.target, story.target), "Without expedition unlock, guardian guidance follows the existing shore story route");
                    game.save.stage = 0; story = game.GetQuest("main"); guide = game.GetQuest("bounty");
                    Check(game.GetQuest("shore").locked && guide.room == story.room && HuntSame(guide.target, story.target), "When the shore story is locked, the hunt follows the mature main-story route instead");
                }
            }
            var priorities = HuntGame(data, 3, 0); priorities.save.explored[0] = 16777215;
            priorities.mode = "landmarks";
            WorldObject cook = Object(priorities, "cook"); Check(priorities.PinLandmark(cook), "The real landmark action pins an explored town destination");
            priorities.save.tutorialBits = 7;
            Check(HuntSame(priorities.NavigationTarget().Value, new Point(cook.x, cook.y)), "An explicit pin retains priority over hunt and tutorial");
            priorities.ClearLandmarkPin(); Check(HuntSame(priorities.NavigationTarget().Value, new Point(11, 14)), "The original mentor tutorial retains navigation priority over contracts");
            priorities.SkipTutorial(); priorities.Close(); priorities.StartPractice();
            Check(priorities.practicing && HuntSame(priorities.NavigationTarget().Value, priorities.enemies[0].Position)
                && priorities.GetQuest("bounty").room == 0 && HuntSame(priorities.GetQuest("bounty").target, priorities.Position), "Training owns its original dummy route while the contract honestly defers");
            priorities.Attack(priorities.enemies[0].Position - priorities.Position); Advance(priorities, .5f);
            Check(priorities.save.bountyProgress == 0, "A real training attack cannot advance an accepted guardian hunt");
            priorities.EndPractice(); priorities.StartAbyss(0);
            QuestGuide abyssGuide = priorities.GetQuest("bounty");
            Check(priorities.InAbyss && abyssGuide.room == 8 && HuntSame(abyssGuide.target, new Point(31, 14))
                && HuntSame(priorities.NavigationTarget().Value, new Point(31, 14)) && (abyssGuide.next.Contains("6")), "Abyss hunt text explains floor six while preserving the existing room-eight east banking gate");
            priorities.AbandonBounty(); priorities.invulnerable = 10000;
            for (int floor = 1; floor <= 6; floor++)
            {
                Combat(priorities, false);
                if (floor < 6) Action(priorities, "abyssGate", "boon" + ((floor - 1) % 3));
            }
            Check(priorities.save.abyssFloor == 6 && priorities.Has("boss_8") && !priorities.enemies.Exists(e => e.boss),
                "The late-acceptance fixture really defeated floor six before taking a guardian contract");
            priorities.AcceptBounty(3); priorities.save.english = true; string lateBefore = HuntState(priorities);
            QuestGuide late = priorities.GetQuest("bounty");
            Check(late.room == 8 && HuntSame(late.target, new Point(31, 14)) && HuntSame(priorities.NavigationTarget().Value, new Point(31, 14))
                && late.next.Contains("bank") && late.next.Contains("anew") && !late.next.Contains("Defeat"),
                "A contract accepted after the floor-six kill points to banking and a new run, never the dead guardian");
            Check(lateBefore == HuntState(priorities) && !priorities.enemies.Exists(e => e.boss), "Late-acceptance guide reads preserve the real cleared floor and never respawn its guardian");
        }

        private static string HuntState(GameCore game)
        {
            var text = new StringBuilder(ResetSaveState(game.save));
            text.Append('/').Append(game.revision).Append('/').Append(game.WorldGeneration).Append('/').Append(game.mode)
                .Append('/').Append(game.sound).Append('/').Append(game.toast).Append('/').Append(game.attackCooldown).Append('/').Append(game.skillCooldown)
                .Append('/').Append(game.dashCooldown).Append('/').Append(game.hitStop);
            ResetAppend(text, game.enemies); ResetAppend(text, game.projectiles); ResetAppend(text, game.hazards); ResetAppend(text, game.effects);
            ResetAppend(text, game.fallen); ResetAppend(text, game.sounds); ResetAppend(text, game.playerAction); ResetAppend(text, game.companionAction);
            return text.ToString();
        }
        public static void ContractHuntQueriesAndLegacySnapshotsRemainReadOnly(Campaign data)
        {
            foreach (int version in new[] { 1, 2 }) foreach (int contract in new[] { 0, 3, 4 }) foreach (bool empty in new[] { false, true })
            {
                var game = HuntGame(data, contract); game.save.coins = 87; game.save.shards = 19; game.save.xp = 9; game.save.health = 73;
                if (!empty) { Enemy actor = HuntActor(game, 64001, contract == 4 ? 2 : contract == 3 ? 3 : 0, HuntPoint(game, 2.2f, 3.5f), contract == 3); actor.maxHp = 60; actor.hp = 37; }
                game.PrepareSave(); SaveData checkpoint = CloneSave(game.save); checkpoint.version = version;
                var restored = new GameCore(data, CloneSave(checkpoint)); restored.Start(); restored.Close(); restored.SkipTutorial();
                Check(restored.save.version == 2 && restored.save.bounty == (version == 1 ? -1 : contract) && restored.save.bountyProgress == 0
                    && restored.save.coins == 87 && restored.save.shards == 19 && restored.save.health == 73,
                    "Version " + version + " preserves mature migration (v1 has no accepted contract), economy and health without a new save format");
                if (version == 1) restored.AcceptBounty(contract);
                Check(restored.enemies.Count == (empty ? 0 : 1) && (empty || restored.enemies[0].hp == 37), "Exact empty/live world snapshots retain their original actor health");
                restored.Attack(new Point(1, 0)); Check(restored.playerAction != null, "The read-only fixture includes a genuinely prepared unreleased player attack");
                ActorAction prepared = restored.playerAction; string before = HuntState(restored);
                for (int read = 0; read < 24; read++)
                {
                    QuestGuide guide = restored.GetQuest("bounty"); restored.GuidanceHint(); Point? target = restored.NavigationTarget();
                    if (target.HasValue) restored.GuidancePath(target.Value);
                    Check(guide != null, "Each repeated query returns a normal guide object");
                }
                Check(HuntState(restored) == before && ReferenceEquals(prepared, restored.playerAction) && !prepared.released,
                    "Repeated contract/route reads cannot change world actors, HP, seed, economy, snapshot, clocks, prepared attacks, resources or effects");
                restored.PrepareSave(); var again = new GameCore(data, CloneSave(restored.save)); again.Start(); again.Close();
                Check(again.enemies.Count == (empty ? 0 : 1) && (empty || again.enemies[0].hp == 37), "Querying and saving never regenerates an empty encounter or refreshes surviving HP");
            }
        }
    }
}
