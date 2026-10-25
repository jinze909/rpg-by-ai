using System;
using System.Collections.Generic;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore GatheringMarkGame(Campaign data, int room = 1, int profession = 0)
        {
            var game = Unlocked(data); game.SkipTutorial(); game.save.profession = profession;
            foreach (string flag in new[] { "core_1", "core_2", "core_3", "scout_saved", "gate_2", "gate_3" })
                if (!game.Has(flag)) game.save.flags.Add(flag);
            game.Enter(room); game.enemies.Clear(); game.save.explored[room] = 16777215;
            game.mode = "landmarks"; return game;
        }
        private static bool GatheringMarkResource(WorldObject obj)
        { return obj != null && (obj.kind == "wood" || obj.kind == "herb" || obj.kind == "ore"); }
        private static string GatheringMarkState(GameCore game)
        { return LandmarkDescribe(game) + "/mode=" + game.mode + "/generation=" + game.WorldGeneration; }
        private static string GatheringMarkFlag(GameCore game, WorldObject obj)
        { return "landmark_pin:" + game.save.room + ":" + obj.id; }
        private static void GatheringMarkAtNode(GameCore game, WorldObject node)
        {
            List<Point> route = game.InteractionPath(node);
            Check(route.Count > 0 || ReferenceEquals(game.Nearest(), node), "The actual resource has an honest reachable E approach");
            InteractionRouteOwnsEndpoint(game, node, route);
            if (route.Count > 0)
            { Point end = route[route.Count - 1]; game.save.x = end.x; game.save.y = end.y; }
            game.Close(); Check(ReferenceEquals(game.Nearest(), node), "The original E selector owns this exact gathering node");
        }
        private static bool GatheringMarkSame(Point? a, Point? b)
        { return a.HasValue == b.HasValue && (!a.HasValue || (a.Value - b.Value).Length < .0001f); }

        public static void GatheringLandmarksExposeOnlyExploredAvailableCanonicalNodes(Campaign data)
        {
            int count = 0;
            foreach (int room in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 9, GameCore.ArchiveRoom, GameCore.ReachRoom })
            {
                var game = GatheringMarkGame(data, room); game.save.explored[room] = 0;
                Check(game.DiscoveredGatheringNodes().Count == 0, "An unexplored sector cannot disclose resource locations");
                game.save.explored[room] = 16777215;
                List<WorldObject> found = game.DiscoveredGatheringNodes(); int cursor = 0;
                foreach (WorldObject obj in game.Map.objects) if (GatheringMarkResource(obj))
                {
                    Check(cursor < found.Count && ReferenceEquals(found[cursor++], obj),
                        "The resource list keeps canonical object identity and authored order in every static map");
                    Check(!game.DiscoveredLandmarks().Contains(obj), "The mature Places list still excludes all gathering nodes");
                }
                Check(cursor == found.Count, "No resident, service or undiscovered object is smuggled into Gathering"); count += cursor;
                if (found.Count > 0)
                {
                    WorldObject node = found[0]; game.save.flags.Add("node_" + node.id);
                    Check(!game.DiscoveredGatheringNodes().Contains(node), "An exhausted resource disappears from available gathering results");
                    found.Clear(); Check(game.DiscoveredGatheringNodes().Count == cursor - 1,
                        "Mutating a caller's list cannot remove any authoritative map object");
                }
            }
            Check(count >= 30, "Discovery exercises the authored campaign and Reach resources rather than a synthetic trio");
            var local = GatheringMarkGame(data); WorldObject herb = Object(local, "herb"); local.save.explored[1] = 0;
            int sector = (int)(herb.x * 6 / local.Map.rows[0].Length) + (int)(herb.y * 4 / local.Map.rows.Length) * 6;
            local.save.explored[1] = 1 << sector;
            Check(local.DiscoveredGatheringNodes().Contains(herb) && !local.DiscoveredGatheringNodes().Contains(Object(local, "ore")),
                "One discovered sector reveals its herb without exposing the distant ore sector");
            local.save.explored[1] = 16777215; WorldObject[] original = local.Map.objects;
            try
            {
                foreach (WorldObject invalid in new[] {
                    new WorldObject { id = "bad:node", kind = "wood", name = "Bad", x = 12, y = 18 },
                    new WorldObject { id = "bad_nan", kind = "herb", name = "Bad", x = float.NaN, y = 18 },
                    new WorldObject { id = "bad_name", kind = "ore", x = 12, y = 18 },
                    new WorldObject { id = "bad_outside", kind = "wood", name = "Bad", x = -5, y = 18 } })
                {
                    var objects = new List<WorldObject>(original); objects.Add(invalid); local.Map.objects = objects.ToArray();
                    Check(!local.DiscoveredGatheringNodes().Contains(invalid) && !local.PinLandmark(invalid),
                        "Malformed canonical node metadata cannot become a visible or persisted destination");
                }
                var duplicate = new WorldObject { id = herb.id, kind = herb.kind, name = herb.name, x = herb.x + 1, y = herb.y };
                var ambiguous = new List<WorldObject>(original); ambiguous.Add(duplicate); local.Map.objects = ambiguous.ToArray();
                Check(!local.DiscoveredGatheringNodes().Contains(herb) && !local.PinLandmark(herb) && !local.PinLandmark(duplicate),
                    "Ambiguous stable IDs cannot persist either resource under a misleading destination");
            }
            finally { local.Map.objects = original; }
        }

        public static void GatheringLandmarksPinActualNodesAndReachTheirOwnInteraction(Campaign data)
        {
            int reached = 0;
            foreach (int room in new[] { 1, 2, 3, 4, 5, 6, 7, GameCore.ReachRoom })
            {
                var game = GatheringMarkGame(data, room); string tracked = game.save.trackedQuest;
                foreach (WorldObject node in game.DiscoveredGatheringNodes())
                {
                    game.mode = "landmarks";
                    Check(game.PinLandmark(node) && ReferenceEquals(game.PinnedLandmark, node) && game.HasLandmarkPin
                        && game.Has(GatheringMarkFlag(game, node)) && LandmarkFlags(game) == 1 && game.save.trackedQuest == tracked,
                        "Only a deliberate resource choice replaces the personal route and leaves the real tracked quest intact");
                    Check(LandmarkTarget(game, new Point(node.x, node.y)), "A valid local resource pin owns the original navigation target");
                    List<Point> route = game.GuidancePath(game.NavigationTarget().Value);
                    if (route.Count > 0 || ReferenceEquals(game.Nearest(), node))
                    { InteractionRouteOwnsEndpoint(game, node, route); reached++; }
                    int revision = game.revision;
                    Check(!game.PinLandmark(node) && game.revision == revision, "Reconfirming the same node is a genuine no-op");
                }
            }
            Check(reached >= 20, "Actual E-owned endpoints are checked across ordinary regions and reachable Reach nodes");
            var local = GatheringMarkGame(data); WorldObject herb = Object(local, "herb");
            var copied = new WorldObject { id = herb.id, kind = herb.kind, name = herb.name, x = herb.x, y = herb.y };
            Check(!local.PinLandmark(copied) && !local.PinLandmark(data.maps[2].objects[0]) && !local.PinLandmark(null),
                "Copied, foreign and null references never authorize resource navigation");
            foreach (string mode in new[] { "world", "map", "inventory", "journal", "dialog", "title" })
            {
                local.mode = mode; string before = GatheringMarkState(local);
                Check(!local.PinLandmark(herb) && GatheringMarkState(local) == before,
                    "A stale pin callback outside the existing Landmarks owner state cannot change saved progress");
            }
            local.mode = "landmarks"; local.save.explored[1] = 0;
            Check(!local.PinLandmark(herb), "Knowing an object reference cannot bypass its sector discovery requirement");
            local.save.explored[1] = 16777215; local.save.flags.Add("node_" + herb.id);
            Check(!local.PinLandmark(herb), "An already gathered node cannot be repinned from a stale available row");
            var locked = GatheringMarkGame(data, 2); WorldObject inaccessible = Object(locked, "ore"); locked.save.stage = 0;
            string shut = GatheringMarkState(locked);
            Check(!locked.CanEnter(2) && locked.DiscoveredGatheringNodes().Count == 0 && !locked.PinLandmark(inaccessible)
                && GatheringMarkState(locked) == shut,
                "An inconsistent current-map reference cannot bypass the existing story route lock");
        }

        public static void GatheringLandmarksConsumeOriginalRewardsAndRetireOnlyMatchingPin(Campaign data)
        {
            foreach (string kind in new[] { "wood", "herb", "ore" })
            {
                var game = GatheringMarkGame(data); game.save.inventory[2] = 0;
                if (kind == "ore") { game.Close(); game.AcceptBounty(1); }
                else game.TrackQuest("ally1");
                game.mode = "landmarks"; WorldObject node = Object(game, kind); string tracked = game.save.trackedQuest;
                Check(game.PinLandmark(node), "A genuine discovered resource is explicitly pinned before gathering");
                string pin = GatheringMarkFlag(game, node); GatheringMarkAtNode(game, node);
                SaveData before = CloneSave(game.save); game.Interact();
                Check(game.Has("node_" + node.id) && !game.Has(pin) && !game.HasLandmarkPin && game.PinnedLandmark == null
                    && !game.DiscoveredGatheringNodes().Contains(node) && game.save.trackedQuest == tracked,
                    "The actual E gathering action consumes the node and retires only its fulfilled personal route");
                for (int item = 0; item < game.save.inventory.Length; item++)
                    Check(game.save.inventory[item] == before.inventory[item] + (item == (kind == "wood" ? 0 : kind == "herb" ? 2 : 1) || kind == "ore" && item == 4 ? 3 : 0),
                        "The old gathering recipe still grants exactly three of its mature materials and nothing else");
                Check(game.save.coins == before.coins && game.save.shards == before.shards && game.save.xp == before.xp
                    && game.save.skillPoints == before.skillPoints && game.save.health == before.health && game.save.energy == before.energy
                    && game.save.bountyProgress == before.bountyProgress + (kind == "ore" ? 1 : 0),
                    "A pin adds no reward, healing, price, XP or contract event beyond the original one ore event");
                var control = new GameCore(data, CloneSave(game.save)); control.Start();
                Check(GatheringMarkSame(game.NavigationTarget(), control.NavigationTarget()),
                    "Retirement returns navigation to the unchanged current quest rather than a hidden stale node");
                string paid = LandmarkDescribe(game.save); game.Interact();
                Check(LandmarkDescribe(game.save) == paid, "A second E at an exhausted node cannot repeat material or bounty rewards");
                game.Enter(0); Action(game, "rest", "rest"); game.Enter(1);
                Check(!game.Has("node_" + node.id) && game.DiscoveredGatheringNodes().Contains(node)
                    && !game.HasLandmarkPin && !game.Has(pin),
                    "Original inn rest renews resources without resurrecting a completed old resource pin");
            }
            var other = GatheringMarkGame(data); WorldObject wood = Object(other, "wood"), herbNode = Object(other, "herb");
            Check(other.PinLandmark(wood), "Choose a timber destination"); string woodPin = GatheringMarkFlag(other, wood);
            GatheringMarkAtNode(other, herbNode); other.Interact();
            Check(other.Has(woodPin) && other.HasLandmarkPin && ReferenceEquals(other.PinnedLandmark, wood),
                "Gathering another resource leaves the explicitly selected timber route intact");
            var town = LandmarkGame(data); WorldObject kitchen = Object(town, "cook"); Check(town.PinLandmark(kitchen), "Choose a mature kitchen place pin");
            string kitchenPin = GatheringMarkFlag(town, kitchen); town.Close(); town.Enter(1); town.enemies.Clear();
            GatheringMarkAtNode(town, Object(town, "ore")); town.Interact();
            Check(town.Has(kitchenPin) && town.HasLandmarkPin && town.PinnedLandmark == null,
                "Resource collection cannot clear an unrelated dormant place destination on another map");
            town.Enter(0); Check(ReferenceEquals(town.PinnedLandmark, kitchen), "The original place pin resolves normally on returning home");
        }

        public static void GatheringLandmarksQueriesAndLegacyLoadsRemainReadOnly(Campaign data)
        {
            var game = GatheringMarkGame(data); WorldObject wood = Object(game, "wood"); game.PinLandmark(wood); game.Close();
            game.save.x = 12; game.save.y = 14; var enemy = new Enemy(99792, 0, 13.1f, 14, 500) { stun = 99 }; game.enemies.Add(enemy);
            game.Attack(new Point(1, 0)); ActorAction action = game.playerAction; Check(action != null && !action.released, "Accept a real attack before reading gathering information");
            string before = GatheringMarkState(game); int seed = game.save.seed;
            for (int repeat = 0; repeat < 30; repeat++)
            {
                var a = game.DiscoveredGatheringNodes(); var b = game.DiscoveredGatheringNodes();
                Check(!ReferenceEquals(a, b) && a.Contains(wood) && ReferenceEquals(game.PinnedLandmark, wood),
                    "Each read returns an independent list of the same authoritative active map objects");
                game.HasLandmarkPin.ToString(); game.DiscoveredLandmarks(); a.Clear();
            }
            Check(GatheringMarkState(game) == before && game.save.seed == seed && ReferenceEquals(game.playerAction, action),
                "Repeated discovery and pin queries leave every public combat/save field, RNG and accepted action unchanged");
            for (int frame = 0; frame < 200 && enemy.hp == enemy.maxHp; frame++) game.Tick(.01f, new Point());
            Check(action.released && enemy.hp < enemy.maxHp, "The accepted attack still completes its unchanged real contact after queries");
            for (int version = 1; version <= 2; version++)
            {
                var original = GatheringMarkGame(data); WorldObject ore = Object(original, "ore"); Check(original.PinLandmark(ore), "Select an actual available ore destination");
                original.PrepareSave(); SaveData old = CloneSave(original.save); old.version = version;
                old.flags.AddRange(new[] { "gathering_legacy_sentinel", "landmark_pinball:preserve", "landmark_pin:bad", "landmark_pin:8:ore1" });
                var resumed = new GameCore(data, old); resumed.Start(); string snapshot = GatheringMarkState(resumed);
                Check(resumed.HasLandmarkPin && ReferenceEquals(resumed.PinnedLandmark, Object(resumed, "ore"))
                    && resumed.Has("gathering_legacy_sentinel") && resumed.Has("landmark_pinball:preserve") && LandmarkFlags(resumed) == 1,
                    "The existing v" + version + " flag namespace persists one canonical discovered resource pin and preserves unrelated records");
                resumed.DiscoveredGatheringNodes(); resumed.PinnedLandmark.ToString(); Check(GatheringMarkState(resumed) == snapshot,
                    "A reconstructed source list does not re-sanitize or modify an already migrated save during read");
                resumed.PrepareSave(); var again = new GameCore(data, CloneSave(resumed.save)); again.Start();
                Check(again.HasLandmarkPin && again.PinnedLandmark.id == ore.id && again.save.trackedQuest == resumed.save.trackedQuest,
                    "Normal PrepareSave and load retain the existing stable flag without a new save field");
                SaveData depleted = CloneSave(resumed.save); depleted.flags.Add("node_" + ore.id);
                var exhausted = new GameCore(data, depleted); exhausted.Start();
                Check(!exhausted.HasLandmarkPin && exhausted.PinnedLandmark == null && LandmarkFlags(exhausted) == 0
                    && exhausted.Has("node_" + ore.id) && exhausted.Has("gathering_legacy_sentinel"),
                    "A loaded exhausted-node pin is sanitized without refreshing its resource or touching unrelated flags");
            }
            foreach (FieldInfo field in typeof(SaveData).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Check(field.Name.IndexOf("gathering", StringComparison.OrdinalIgnoreCase) < 0,
                    "Gathering routes reuse existing exploration/node/landmark flags instead of extending the save schema");
            var training = RangeGame(data); Check(training.StartRange(0), "Start genuine safe training"); training.mode = "landmarks";
            string rehearsal = GatheringMarkState(training);
            Check(training.DiscoveredGatheringNodes().Count == 0 && !training.PinLandmark(Object(training, "cook"))
                && GatheringMarkState(training) == rehearsal, "Training suppresses gathering navigation without altering saved progress");
            var fallen = GatheringMarkGame(data); WorldObject destination = Object(fallen, "wood"); fallen.PinLandmark(destination);
            string savedPin = GatheringMarkFlag(fallen, destination); fallen.Close(); fallen.save.health = 1; fallen.invulnerable = 0;
            fallen.projectiles.Add(new Projectile { position = fallen.Position, damage = 999, source = "shot" }); fallen.Tick(.02f, new Point());
            Check(fallen.Dying, "A real lethal projectile establishes the existing defeat owner state");
            fallen.mode = "landmarks"; string defeat = GatheringMarkState(fallen);
            Check(fallen.DiscoveredGatheringNodes().Count == 0 && !fallen.PinLandmark(destination) && fallen.PinnedLandmark == null
                && fallen.Has(savedPin) && GatheringMarkState(fallen) == defeat,
                "Defeat suppresses resource discovery and activation without deleting an unfulfilled saved destination");
            var abyss = GatheringMarkGame(data); abyss.Close(); abyss.StartAbyss(0); abyss.mode = "landmarks";
            string run = GatheringMarkState(abyss);
            Check(abyss.DiscoveredGatheringNodes().Count == 0 && !abyss.PinLandmark(abyss.Map.objects[0]) && GatheringMarkState(abyss) == run,
                "Seeded abyss objectives remain authoritative and cannot be replaced by static gathering pins");
        }
    }
}
