using System;
using System.Collections.Generic;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore CompanionFollowGame(Campaign data, int companion = 1, int room = 0, Point? player = null)
        {
            var game = Fresh(data); game.SkipTutorial(); game.save.stage = 3;
            if (room != 0) game.Enter(room);
            game.enemies.Clear(); game.save.flags.Add("companion_" + companion); game.save.relationships[companion] = 3;
            Point foot = player ?? new Point(8, 6.5f); game.save.x = foot.x; game.save.y = foot.y;
            Check(game.CanStand(foot), "Follower routes begin with the complete player footprint on authored ground");
            game.SelectCompanion(companion); game.invulnerable = 1000; game.save.health = game.save.maxHealth;
            Check(game.save.companion == companion && game.CanStand(game.companionPosition), "The real companion selection places valid initial feet");
            return game;
        }
        private static Point CompanionFollowAnchor(GameCore game)
        {
            return (Point)typeof(GameCore).GetMethod("CompanionFollowTarget", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(game, new object[0]);
        }
        private static void CompanionFollowStep(GameCore game, float dt, Point movement)
        {
            Point foot = game.companionPosition; float distance = game.companionMoveTime;
            ActorAction action = game.companionAction;
            game.Tick(dt, movement); Point travelled = game.companionPosition - foot;
            Check(game.CanStand(game.Position) && game.CanStand(game.companionPosition), "Every real route frame keeps both complete feet out of authored walls, houses and water");
            Check(travelled.Length <= (game.Speed + 1) * Math.Min(dt, .05f) + .00003f, "Routing retains the original walking budget without an instant catch-up jump");
            if (action != null)
                Check(travelled.Length == 0 && game.companionMoveTime == distance && !game.companionMoving, "Prepared and recovering actions keep the companion's feet planted");
            else if (travelled.Length > .001f && game.companionAction == null)
                Check(game.companionMoving && Math.Abs(game.companionMoveTime - distance - travelled.Length) < .00003f &&
                    (game.companionAim - travelled.Normalized).Length < .0001f, "Only actual swept travel advances the original gait distance and facing");
            else if (travelled.Length <= .001f)
                Check(!game.companionMoving && game.companionMoveTime == distance, "Stopping or blocked movement cannot create walking animation distance");
        }
        private static void CompanionFollowWalk(GameCore game, Point waypoint, float dt, bool dash = false)
        {
            for (int frame = 0; frame < 1500 && (waypoint - game.Position).Length > .005f; frame++)
            {
                Point delta = waypoint - game.Position;
                if (dash && game.dashCooldown <= 0 && delta.Length > 2.8f) game.Dash(delta.Normalized);
                CompanionFollowStep(game, Math.Min(dt, delta.Length / game.Speed), delta.Normalized);
            }
            Check((waypoint - game.Position).Length <= .005f, "Public walking and dodge inputs reach the authored route waypoint");
        }
        private static void CompanionFollowHouseRoute(GameCore game, float dt, bool dash)
        {
            CompanionFollowWalk(game, new Point(9.5f, 6.5f), dt);
            CompanionFollowWalk(game, new Point(9.5f, 3.63f), dt, dash);
            CompanionFollowWalk(game, new Point(8, 3.63f), dt);
            Point anchor = CompanionFollowAnchor(game);
            Check((anchor - new Point(6.35f, 4.18f)).Length < .0001f && game.CanStand(anchor) && !game.CanStand(new Point(6.5f, 4.5f)),
                "The unchanged first legal follow offset has valid feet above Emberfall's house while its cell center is blocked");
        }
        private static void CompanionFollowSettle(GameCore game, float dt)
        {
            for (int frame = 0; frame < (int)Math.Ceiling(20 / dt); frame++) CompanionFollowStep(game, dt, new Point());
            Check((game.companionPosition - CompanionFollowAnchor(game)).Length <= .3f && !game.companionMoving,
                "The physical follower reaches the unchanged legal anchor and settles rather than endlessly grinding into the house");
        }

        public static void CompanionFollowWalksAroundAuthoredHouseAfterNaturalDodgeAtTwentyAndSixtyFrames(Campaign data)
        {
            foreach (float dt in new[] { .05f, 1 / 60f }) for (int companion = 0; companion < 3; companion++)
            {
                var game = CompanionFollowGame(data, companion); Point selected = game.companionPosition;
                Check((selected - new Point(6.35f, 7.05f)).Length < .0001f, "Actual selection begins behind the authored house without arranging the companion afterward");
                int coins = game.save.coins, xp = game.save.xp, shards = game.save.shards;
                CompanionFollowHouseRoute(game, dt, true); float walked = game.companionMoveTime;
                CompanionFollowSettle(game, dt);
                Check(game.companionMoveTime > walked && game.projectiles.Count == 0 && game.companionAction == null &&
                    game.save.health == game.save.maxHealth && game.save.coins == coins && game.save.xp == xp && game.save.shards == shards,
                    "All three companions finish the needed detour through real walking without attacks, healing, rewards or other progression");
            }
        }

        public static void CompanionFollowKeepsOrdinaryHouseAndAuthoredWaterRoutes(Campaign data)
        {
            foreach (float dt in new[] { .05f, 1 / 60f })
            {
                var house = CompanionFollowGame(data); CompanionFollowHouseRoute(house, dt, false); CompanionFollowSettle(house, dt);
                var water = CompanionFollowGame(data, 1, 2, new Point(3.5f, 10.5f)); Point destination = new Point(8.5f, 4.5f);
                List<Point> route = water.NavigationPath(destination);
                Check(route.Count > 5 && (water.DirectionTo(water.Position, destination) - (destination - water.Position).Normalized).Length > .1f,
                    "The second region uses the shipped water detour instead of an invented wall fixture");
                foreach (Point waypoint in route) CompanionFollowWalk(water, waypoint, dt);
                CompanionFollowWalk(water, destination, dt); Point anchor = CompanionFollowAnchor(water);
                Check(water.CanStand(new Point((float)Math.Floor(anchor.x) + .5f, (float)Math.Floor(anchor.y) + .5f)),
                    "This ordinary follow destination retains its original legal navigation cell center");
                CompanionFollowSettle(water, dt);
                Check(water.companionMoveTime > 10 && water.enemies.Count == 0 && water.projectiles.Count == 0,
                    "The original regional follower still crosses the long legal route and arrives without combat side effects");
            }
        }

        public static void CompanionFollowKeepsPreparedAutomaticAndCommandFeetAndAim(Campaign data)
        {
            var automatic = CompanionFollowGame(data); Enemy enemy = FocusEnemy(automatic, 98301, 4, 7);
            ActorAction attack = CompanionFocusWait(automatic); Point foot = automatic.companionPosition, target = attack.target.Value, facing = attack.direction;
            Check(attack.kind == "attack" && attack.contact == .16f && attack.duration == .42f && attack.damage == 15, "An actual automatic preparation keeps its original timing and bond damage");
            enemy.y = 8; automatic.SetAimTarget(new Point(14, 7));
            for (int frame = 0; frame < 100 && !attack.released; frame++)
            {
                CompanionFollowStep(automatic, .01f, new Point(1, 0));
                Check(ReferenceEquals(automatic.companionAction, attack) && CompanionFocusPoint(attack.target, target) && (attack.direction - facing).Length == 0,
                    "Changed live aim, a moving target and a moving player cannot retarget the accepted automatic shot");
            }
            Check(attack.released && automatic.projectiles.Count == 1 && (automatic.companionPosition - foot).Length == 0,
                "The real automatic contact releases exactly once from its planted origin");
            Point origin = GameCore.WeaponOrigin(foot, 1, facing);
            Check((automatic.projectiles[0].velocity.Normalized - (target - origin).Normalized).Length < .0001f, "The real arrow retains the original captured foot target and muzzle aim");

            for (int companion = 0; companion < 3; companion++)
            {
                var game = CompanionFollowGame(data, companion); CompanionFollowHouseRoute(game, .01f, true);
                Point captured = new Point(12, 3.5f); game.SetAimTarget(captured); game.CommandCompanion(); ActorAction command = game.companionAction;
                Check(command != null && command.kind == "command" && command.contact == .2f && command.duration == .52f && game.companionCooldown == 18,
                    "An explicit command still starts with its original cost-free preparation and cooldown during a needed detour");
                foot = game.companionPosition; facing = command.direction; game.SetAimTarget(new Point(3, 8));
                for (int frame = 0; frame < 100 && ReferenceEquals(game.companionAction, command); frame++)
                {
                    CompanionFollowStep(game, .01f, new Point(-1, 0));
                    Check(CompanionFocusPoint(command.target, captured) && (command.direction - facing).Length == 0 && (game.companionPosition - foot).Length == 0,
                        "Routing never interrupts or redirects an accepted command while its player's follow anchor moves");
                }
                Check(command.released && game.LastCompanionReceipt != null && game.LastCompanionReceipt.id == companion,
                    "Every original companion command still reaches its actual single release receipt");
            }
        }

        public static void CompanionFollowPreservesPauseLegacySavesAndWorldResetOwnership(Campaign data)
        {
            var game = CompanionFollowGame(data); CompanionFollowHouseRoute(game, .01f, true);
            Point foot = game.companionPosition; float moved = game.companionMoveTime, clock = game.save.totalTime;
            game.mode = "pause";
            for (int frame = 0; frame < 100; frame++) game.Tick(.02f, new Point());
            Check((game.companionPosition - foot).Length == 0 && game.companionMoveTime == moved && game.save.totalTime == clock,
                "A paused detour advances neither companion feet, gait nor world time");
            game.Close(); CompanionFollowSettle(game, .02f); game.PrepareSave(); string state = TacticsState(game.save);
            for (int read = 0; read < 100; read++) CompanionFollowAnchor(game);
            Check(TacticsState(game.save) == state, "Repeated anchor diagnostics remain read-only for the complete persisted save");
            foreach (int version in new[] { 1, 2 })
            {
                SaveData copy = CloneSave(game.save); copy.version = version;
                var loaded = new GameCore(data, copy); loaded.Start();
                Check(loaded.save.version == 2 && loaded.save.relationships[1] == 3 && loaded.save.companion == (version == 1 ? -1 : 1) &&
                    loaded.companionMoveTime == 0 && loaded.companionAction == null && loaded.projectiles.Count == 0 && loaded.CanStand(loaded.companionPosition),
                    "Both save versions keep original companion progression and migration while resetting only transient movement and actions");
                Check((loaded.companionPosition - CompanionFollowAnchor(loaded)).Length < .0001f,
                    "Loading retains the existing legal initial companion placement rather than inheriting a previous route cursor");
                loaded.Tick(.02f, new Point()); Check(!loaded.companionMoving, "A newly loaded companion does not invent a follow step while already at its anchor");
            }
            game.Enter(game.save.room);
            Check(game.companionMoveTime == 0 && game.companionAction == null && game.CanStand(game.companionPosition) &&
                (game.companionPosition - CompanionFollowAnchor(game)).Length < .0001f, "Same-room travel retains the existing fresh world placement and clears old movement");
            foreach (FieldInfo field in typeof(SaveData).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Check(field.Name.IndexOf("companionFollow", StringComparison.OrdinalIgnoreCase) < 0 && field.Name.IndexOf("followPath", StringComparison.OrdinalIgnoreCase) < 0,
                    "Physical follow routing introduces no saved path or companion navigation fields");
        }
    }
}
