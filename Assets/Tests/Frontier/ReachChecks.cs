using System;
using System.Collections.Generic;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore ReachGame(Campaign data, int profession = 0)
        {
            var g = Fresh(data, profession); g.SkipTutorial(); g.save.flags.Add("core_1");
            g.Enter(GameCore.ReachRoom);
            Check(g.save.room == GameCore.ReachRoom && g.ReachVisited, "The earned forest route visits its own appended exploration region");
            return g;
        }
        private static WorldObject ReachStand(GameCore g, string kind)
        {
            g.Close(); WorldObject obj = Object(g, kind);
            Point[] offsets = { new Point(0,.7f),new Point(0,-.7f),new Point(-.7f,0),new Point(.7f,0) };
            bool found = false;
            foreach (Point offset in offsets)
            {
                Point p = new Point(obj.x,obj.y) + offset;
                if (!g.CanStand(p)) continue;
                g.save.x = p.x; g.save.y = p.y;
                if (g.Nearest() == obj) { found = true; break; }
            }
            Check(found, "The authored object has a full-footprint interaction approach: " + kind);
            return obj;
        }
        private static void ReachChoose(GameCore g, string action)
        {
            Check(g.mode == "dialog" && g.conversation != null, "Reach decisions use their actual local conversation");
            int index = Array.FindIndex(g.conversation.choices,c => c.action == action);
            Check(index >= 0, "Missing Reach conversation action " + action); g.Choose(index);
        }
        private static bool ReachNearSeen(GameCore g, HashSet<int> reached, WorldObject obj)
        {
            int width = g.Map.rows[0].Length * 4;
            foreach (int cell in reached)
                if ((new Point((cell % width + .5f)/4, (cell/width + .5f)/4) - new Point(obj.x,obj.y)).Length < 1.65f) return true;
            return false;
        }
        private static void ReachOpenRoutes(GameCore g)
        {
            ReachStand(g,"reachBridge"); Check(g.OpenReachBridge(), "The actual bank wheel opens the bridge");
            ReachStand(g,"reachRoot"); Check(g.ClearReachRoots(), "The nearby recovered core releases the hidden grove roots");
        }
        private static void ReachReadPosts(GameCore g)
        {
            for (int i = 0; i < 3; i++)
            { ReachStand(g,"reachPost" + i); Check(g.RecordReachPost(i), "The nearby cleared survey station records its real finding"); }
        }

        public static void ReachAppendedRegionRequiresEarnedPowerAndKeepsMatureRoutes(Campaign data)
        {
            Check(data.maps.Length >= 12 && GameCore.ReachRoom == 11 && data.maps[11].id == 11,
                "The larger route appends after the original eleven map identities");
            for (int room = 0; room < 11; room++)
                Check(data.maps[room].id == room && data.maps[room].rows.Length == 24 && data.maps[room].rows[0].Length == 36,
                    "All mature regions preserve their existing indices and coordinate dimensions");
            MapData map = data.maps[11];
            Check(map.rows.Length == 48 && Array.TrueForAll(map.rows,row => row.Length == 72),
                "The exploration route has a rectangular 72 by 48 walkable world without resizing old maps");
            var locked = Fresh(data); int coins = locked.save.coins;
            Check(!locked.ReachReady && !locked.CanEnter(11) && locked.GetQuest("reach").locked,
                "The optional route cannot bypass the first starcore milestone");
            locked.Enter(11); Check(locked.save.room == 0 && locked.save.coins == coins && !locked.ReachVisited,
                "A locked route cannot mutate story, room, funds or discovery");
            locked.save.flags.Add("core_1"); locked.Enter(11);
            Check(locked.ReachReady && locked.GateOpen && !locked.GateBarrierActive && locked.ArtRoom == 1,
                "The new region selects accepted forest art and never acquires a mature guardian arena");
            Check(locked.enemies.Count == 7 && locked.enemies.TrueForAll(e => e.id >= 76000 && e.id <= 76006 && !e.boss),
                "Only the seven authored normal patrols populate the new route");
            locked.Enter(0); locked.Enter(2); Check(locked.save.room == 0,
                "Holding core one alone does not silently deliver it or bypass the old ruins gate");
        }

        public static void ReachLargeMapFootprintsAndPermanentRoutesMatchPresentation(Campaign data)
        {
            var g = ReachGame(data); g.enemies.Clear(); HashSet<int> closed = ArenaReachable(g);
            WorldObject cache = Object(g,"reachCache");
            Check(!ReachNearSeen(g,closed,cache), "The hidden cache is genuinely unreachable behind the closed grove route");
            for (int y = 28; y <= 30; y++) for (int x = 30; x <= 33; x++)
                Check(g.PresentationTile(x,y) == '~' && !g.CanStand(new Point(x+.5f,y+.5f)),
                    "The collapsed crossing is water in both presentation and full-footprint collision");
            for (int y = 11; y <= 13; y++) for (int x = 45; x <= 47; x++)
                Check(g.PresentationTile(x,y) == 'v' && !g.CanStand(new Point(x+.5f,y+.5f)),
                    "The hidden grove's real root cells remain visibly and physically sealed");
            foreach (WorldObject obj in g.Map.objects)
                if (obj.kind != "reachCache") Check(ReachNearSeen(g,closed,obj), "Ungated exploration interaction is reachable: " + obj.id);
            ReachOpenRoutes(g);
            for (int y = 28; y <= 30; y++) for (int x = 30; x <= 33; x++)
                Check(g.PresentationTile(x,y) == '=' && g.CanStand(new Point(x+.5f,y+.5f)), "The repaired crossing presents and admits the unchanged hero footprint");
            for (int y = 11; y <= 13; y++) for (int x = 45; x <= 47; x++)
                Check(g.PresentationTile(x,y) == '=' && g.CanStand(new Point(x+.5f,y+.5f)), "Cleared roots present exactly the walkable route");
            g.save.x = g.Map.spawnX; g.save.y = g.Map.spawnY;
            HashSet<int> open = ArenaReachable(g);
            foreach (WorldObject obj in g.Map.objects) Check(ReachNearSeen(g,open,obj), "Every appended interaction, including the secret, has a real route: " + obj.id);
            foreach (WorldObject obj in g.Map.objects)
            {
                List<Point> path = g.NavigationPath(new Point(obj.x,obj.y));
                Check(path.Count > 0 && (path[path.Count-1]-new Point(obj.x,obj.y)).Length <= 1.75f,
                    "Dimension-based guidance reaches the expanded region's actual interaction approach: " + obj.id);
                Point before = g.Position;
                foreach (Point p in path)
                {
                    int steps = Math.Max(1,(int)Math.Ceiling((p-before).Length/.1f));
                    for (int step = 1; step <= steps; step++) Check(g.CanStand(before+(p-before)*(step/(float)steps)), "Large-map route segments cannot clip a wall or root edge");
                    before = p;
                }
            }
        }

        public static void ReachScaledExplorationKeepsLegacyBitsAndOwnedNavigation(Campaign data)
        {
            var g = ReachGame(data); g.enemies.Clear();
            for (int room = 0; room < 11; room++)
            {
                for (int bit = 0; bit < 24; bit++)
                {
                    g.save.explored[room] = 1 << bit;
                    for (int sector = 0; sector < 24; sector++)
                        Check(g.Explored(room,sector%6*6+3,sector/6*6+3) == (sector == bit),
                            "Every existing 36 by 24 exploration bit preserves its original six-cell meaning");
                }
            }
            for (int bit = 0; bit < 24; bit++)
            {
                g.save.explored[11] = 1 << bit;
                for (int sector = 0; sector < 24; sector++)
                    Check(g.Explored(11,sector%6*12+6,sector/6*12+6) == (sector == bit),
                        "Far eastern and southern parts of the larger map have independent saved exploration sectors");
            }
            g.save.explored[11] = 0; g.Tick(.02f,new Point());
            Check(g.Explored(11,g.save.x,g.save.y), "Actual world simulation records the dimension-scaled sector at the hero's real position");
            g.TrackQuest("reach"); QuestGuide q = g.CurrentGuide; Point? local = g.NavigationTarget();
            Check(q.id == "reach" && q.room == 11 && local.HasValue && g.NavigationPath(local.Value).Count > 0,
                "The appended quest owns a navigable real destination in the larger map");
            g.Enter(0); Point? fromTown = g.NavigationTarget(); WorldObject forestExit = Object(g,"portal1");
            Check(fromTown.HasValue && (fromTown.Value-new Point(forestExit.x,forestExit.y)).Length < .01f,
                "Town guides the new survey toward the existing forest exit rather than distant-shores travel");
            g.Enter(1); g.enemies.Clear(); Point? fromForest = g.NavigationTarget(); WorldObject reachExit = Object(g,"portal11");
            Check(fromForest.HasValue && (fromForest.Value-new Point(reachExit.x,reachExit.y)).Length < .01f,
                "The original forest points the survey through its appended local exit");
        }

        public static void ReachLocalActionsAndOnceOnlyRewardsRejectStaleDecisions(Campaign data)
        {
            var g = ReachGame(data); int coins = g.save.coins, shards = g.save.shards, points = g.save.skillPoints;
            Check(!g.RecordReachPost(-1) && !g.RecordReachPost(3) && !g.ClaimReachSurvey(), "Invalid station indices and unfinished claims cannot alter progress");
            ReachStand(g,"reachPost0");
            Check(!g.RecordReachPost(0) && !g.ReachPostRecorded(0), "A survey station does not record while its nearby live guards remain");
            var local = ReachGame(data); ReachStand(local,"reachPost0");
            local.enemies.RemoveAll(e => (e.Position-GameCore.ReachPosts[0]).Length < 5);
            Check(local.enemies.Count > 0 && local.RecordReachPost(0),
                "Surveying requires its nearby guards, not every distant patrol on the expanded map");
            ReachStand(local,"reachRoot"); Check(local.ClearReachRoots(), "The earned root path can open while unrelated patrols remain"); ReachStand(local,"reachCache");
            Check(!local.RecoverReachCache() && !local.ReachCacheRecovered,
                "An open route does not bypass the living guard beside the hidden cache");
            local.enemies.RemoveAll(e => (e.Position-new Point(52,7)).Length < 5);
            Check(local.enemies.Count > 0 && local.RecoverReachCache(),
                "The hidden cache can be claimed once its own guard clears, without requiring unrelated foes");
            g.enemies.Clear(); ReachStand(g,"reachBridge"); g.mode = "pause";
            Check(!g.OpenReachBridge() && !g.ReachBridgeOpen, "A paused-world callback cannot operate the river crossing");
            g.Close(); g.practicing = true; Check(!g.OpenReachBridge(), "Training isolation prevents survey actions"); g.practicing = false;
            g.deathTime = .3f; Check(!g.OpenReachBridge(), "A defeated hero cannot change exploration terrain"); g.deathTime = 0;
            g.Enter(0); Check(!g.RecordReachPost(0) && !g.OpenReachBridge() && !g.ClearReachRoots() && !g.RecoverReachCache() && !g.ClaimReachSurvey(),
                "Wrong-map direct calls cannot mutate distant survey content");
            Check(g.save.coins == coins && g.save.shards == shards && g.save.skillPoints == points, "Rejected actions preserve the economy atomically");
            g.Enter(11); g.enemies.Clear(); ReachStand(g,"reachBridge"); g.Interact();
            Check(g.mode == "dialog", "The actual bridge interaction opens its authored decision");
            g.save.x = g.Map.spawnX; g.save.y = g.Map.spawnY; ReachChoose(g,"reach_bridge");
            Check(!g.ReachBridgeOpen, "A bridge choice accepted after leaving its bank rechecks the current local body");
            ReachOpenRoutes(g); ReachReadPosts(g);
            ReachStand(g,"reachCache"); g.Interact();
            g.save.x = g.Map.spawnX; g.save.y = g.Map.spawnY; ReachChoose(g,"reach_cache");
            Check(!g.ReachCacheRecovered, "A captured cache choice cannot claim distant supplies after the hero moves away");
            ReachStand(g,"reachCache"); int stew = g.save.inventory[6]; coins = g.save.coins; shards = g.save.shards;
            Check(g.RecoverReachCache() && g.ReachCacheRecovered && g.save.coins == coins+20 && g.save.shards == shards+2 && g.save.inventory[6] == stew+1,
                "The nearby hidden cache grants its advertised existing materials exactly once");
            Check(!g.RecoverReachCache() && g.save.coins == coins+20 && g.save.shards == shards+2 && g.save.inventory[6] == stew+1,
                "Repeated cache callbacks cannot farm coins, shards or soup");
            ReachStand(g,"reachBoard"); g.Interact();
            g.save.x = Object(g,"reachCache").x; g.save.y = Object(g,"reachCache").y; ReachChoose(g,"reach_claim");
            Check(!g.ReachComplete, "An earned survey still requires the actual local board handoff after a stale choice");
            ReachStand(g,"reachBoard"); coins = g.save.coins; shards = g.save.shards; points = g.save.skillPoints;
            Check(g.ClaimReachSurvey() && g.ReachComplete && g.save.coins == coins+40 && g.save.shards == shards+4 && g.save.skillPoints == points+1,
                "Three recovered survey records complete one deliberate local handoff");
            Check(!g.ClaimReachSurvey() && g.save.coins == coins+40 && g.save.shards == shards+4 && g.save.skillPoints == points+1,
                "The completed survey is rereadable but never pays again");
            Check(g.GetQuest("reach").complete && !g.Has("core_2") && !g.Has("boss_1") && g.save.stage == 0,
                "Optional completion cannot deliver cores, defeat mature guardians or advance the main story");
        }

        public static void ReachCanonicalSnapshotsRejectForeignActorsAndPreserveDefeats(Campaign data)
        {
            var g = ReachGame(data); g.enemies[0].hp -= 7; Enemy survivor = g.enemies[0];
            g.PrepareSave(); var next = new GameCore(data,CloneSave(g.save)); next.Start();
            Check(next.enemies.Count == 7 && Math.Abs(next.enemies.Find(e => e.id == survivor.id).hp-survivor.hp) < .01f,
                "A same-room real save preserves living patrol health without resetting the encounter");
            var bad = CloneSave(g.save); bad.flags.Add("reach_defeated_76001");
            bad.worldEnemies = new[] {
                new EnemyRecord { id = 76000,kind = 0,x = survivor.x,y = survivor.y,hp = 19,maxHp = 5000 },
                new EnemyRecord { id = 76000,kind = 0,x = survivor.x,y = survivor.y,hp = 999,maxHp = 999 },
                new EnemyRecord { id = 76001,kind = 1,x = 15,y = 14,hp = 30,maxHp = 40 },
                new EnemyRecord { id = 76002,kind = 2,boss = true,x = 15,y = 14,hp = 90,maxHp = 90 },
                new EnemyRecord { id = 76003,kind = 1,x = 15,y = 14,hp = 31,maxHp = 40 },
                new EnemyRecord { id = 76004,kind = 1,x = float.NaN,y = 14,hp = 31,maxHp = 40 },
                new EnemyRecord { id = 999,kind = 3,boss = true,x = 15,y = 14,hp = 90,maxHp = 90 },
                new EnemyRecord { id = 74000,kind = 0,x = 15,y = 14,hp = 20,maxHp = 40 }
            };
            next = new GameCore(data,bad); next.Start();
            Check(next.enemies.Count == 6 && next.enemies.TrueForAll(e => e.id >= 76000 && e.id <= 76006 && !e.boss && next.CanStand(e.Position)) && next.enemies.Find(e => e.id == 76001) == null &&
                Math.Abs(next.enemies.Find(e => e.id == 76000).hp-19) < .01f && next.enemies.Find(e => e.id == 76000).maxHp == survivor.maxHp &&
                next.enemies.Find(e => e.id == 76002).hp == 85 && next.enemies.Find(e => e.id == 76003).kind == 0 && next.enemies.Find(e => e.id == 76003).hp == 55,
                "Malformed records cannot restore foreign bosses, resurrect permanent defeats or replace canonical patrol identities");
            next.Enter(1); next.enemies.Clear(); SaveData forest = CloneSave(next.save); forest.snapshotRoom = 1;
            forest.worldEnemies = new[] { new EnemyRecord { id = 76000,kind = 0,x = 15,y = 14,hp = 20,maxHp = 40 },
                new EnemyRecord { id = 17,kind = 0,x = 15,y = 14,hp = 20,maxHp = 40 } };
            next = new GameCore(data,forest); next.Start();
            Check(next.enemies.Count == 1 && next.enemies[0].id == 17, "Reserved Reach actors cannot leak into the mature forest or chapter allocator");
            next.save.flags.Add("scout_saved"); next.save.x = 28; next.save.y = 10; AwaitArenaOpening(next); Action(next,"shrine","boss");
            Enemy guardian = next.enemies.Find(e => e.boss);
            Check(guardian != null && guardian.id == 18, "Rejected exploration IDs cannot inflate the original guardian's ordinary actor allocator");
            var abyss = Unlocked(data); abyss.save.frontierEnding = 1; abyss.StartAbyss(0); SaveData expedition = CloneSave(abyss.save);
            expedition.abyssSnapshotFloor = expedition.abyssFloor;
            expedition.abyssEnemies = new[] { new EnemyRecord { id = 76000,kind = 0,x = 15,y = 14,hp = 20,maxHp = 40 },
                new EnemyRecord { id = 17,kind = 0,x = 15,y = 14,hp = 20,maxHp = 40 } };
            next = new GameCore(data,expedition); next.Start();
            Check(next.InAbyss && next.enemies.Count == 1 && next.enemies[0].id == 17,
                "Reserved exploration actors cannot become expedition enemies or inflate its allocator");
        }

        public static void ReachLegacySavesTravelAndDefeatPreserveEarnedSurvey(Campaign data)
        {
            var stranded = new SaveData { room = 11,x = 52,y = 7,
                flags = new List<string> { "intro","core_1" } };
            var repaired = new GameCore(data,stranded); repaired.Start();
            Check(repaired.save.room == 11 && repaired.CanStand(repaired.Position) && !repaired.ReachRootsOpen &&
                (repaired.Position-new Point(repaired.Map.spawnX,repaired.Map.spawnY)).Length < .01f,
                "A malformed save inside the unopened secret returns to the authored entry instead of stranding the hero or inventing an open gate");
            foreach (int version in new[] { 1,2 })
            {
                int[] oldKills = new int[11], oldExplored = new int[11];
                for (int i = 0; i < 11; i++) { oldKills[i] = i*3; oldExplored[i] = (i+1)*719; }
                var old = new SaveData { version = version,room = 0,stage = 2,coins = 83,presentationVersion = 1,kills = oldKills,explored = oldExplored,
                    flags = new List<string> { "intro","core_1","boss_1","scout_saved","legacy_reach_sentinel" } };
                var g = new GameCore(data,old); g.Start();
                Check(g.save.version == 2 && g.save.coins == 83 && g.Has("legacy_reach_sentinel") && g.save.kills.Length == data.maps.Length && g.save.explored.Length == data.maps.Length,
                    "Both supported old save versions preserve wealth, flags and existing counters while appending one map slot");
                for (int i = 0; i < 11; i++) Check(g.save.kills[i] == i*3 && g.save.explored[i] == (i+1)*719,
                    "Migration never rewrites mature-map kill or exploration history");
                Check(g.save.kills[11] == 0 && g.save.explored[11] == 0 && !g.ReachVisited && !g.ReachComplete,
                    "Old saves begin the optional route undiscovered with no invented rewards");
                g.Enter(11); g.enemies.Clear(); ReachOpenRoutes(g); ReachReadPosts(g); ReachStand(g,"reachCache"); Check(g.RecoverReachCache(), "Recovery setup earns the real hidden cache");
                g.save.flags.Add("reach_defeated_76000"); g.PrepareSave(); SaveData checkpoint = CloneSave(g.save);
                var loaded = new GameCore(data,checkpoint); loaded.Start();
                Check(loaded.ReachBridgeOpen && loaded.ReachRootsOpen && loaded.ReachCacheRecovered && loaded.ReachPostRecorded(2) && !loaded.ReachComplete,
                    "Loading preserves actual terrain, records and cache choices without inventing the final handoff");
                int coins = loaded.save.coins, kills = loaded.save.kills[11]; loaded.Enter(1); loaded.Enter(11);
                Check(loaded.enemies.Count == 6 && loaded.enemies.Find(e => e.id == 76000) == null && loaded.enemies.TrueForAll(e => e.hp == e.maxHp) &&
                    loaded.save.coins == coins && loaded.save.kills[11] == kills, "Travel rebuilds only unresolved patrols at full health without paying defeated foes again");
                loaded.enemies.Clear(); loaded.invulnerable = 0; loaded.save.health = 1;
                loaded.projectiles.Add(new Projectile { position = loaded.Position,source = "shot",damage = 999 }); loaded.Tick(.02f,new Point()); Advance(loaded,2);
                Check(loaded.save.room == 0 && !loaded.Dying && loaded.save.deaths == 1 && loaded.ReachBridgeOpen && loaded.ReachPostRecorded(2) && loaded.ReachCacheRecovered,
                    "A real lethal contact returns safely to town and retains earned exploration records and terrain");
                loaded.Enter(11); Check(loaded.enemies.Count == 6 && loaded.enemies.Find(e => e.id == 76000) == null,
                    "Death cannot respawn permanently defeated patrols or reset optional progress");
            }
        }

        private static bool ReachOpenShot(GameCore g, Point target)
        {
            Point span = target-g.Position; int samples = Math.Max(1,(int)Math.Ceiling(span.Length/.1f));
            for (int i = 0; i <= samples; i++)
            {
                Point p = g.Position+span*(i/(float)samples);
                if (g.Blocked(p.x,p.y)) return false;
            }
            return true;
        }
        private static int ReachCombatRoutes(GameCore g)
        {
            // The larger river has real detours. Swinging toward a foe across
            // water while walking away lets melee root motion oppose the walk.
            // Follow the actual owned route first, then fight within clear range.
            int frames = 0; Enemy target = null; List<Point> route = new List<Point>();
            float routeClock = 0; Point planned = new Point();
            while (g.enemies.Count > 0 && frames++ < 30000)
            {
                Check(g.save.room == 11 && !g.Dying, "The unchanged starter must survive the real exploration patrol");
                if (target == null || !g.enemies.Contains(target))
                {
                    target = null; float nearest = float.MaxValue;
                    foreach (Enemy enemy in g.enemies)
                    { float distance = (enemy.Position-g.Position).Length; if (distance < nearest) { nearest = distance; target = enemy; } }
                    routeClock = 0; route.Clear();
                }
                Point toward = target.Position-g.Position; float distanceTo = toward.Length;
                float fightingRange = g.save.weapon == 0 ? 1.65f : g.save.weapon == 1 ? 7 : 5.5f;
                bool contact = distanceTo <= fightingRange && ReachOpenShot(g,target.Position);
                Point movement = new Point();
                if (!contact)
                {
                    routeClock -= .02f;
                    if (routeClock <= 0 || (planned-target.Position).Length > 1.5f)
                    {
                        planned = target.Position; route = g.NavigationPath(planned); routeClock = .6f;
                        Check(route.Count > 0 || distanceTo < 1.75f, "Each live patrol has an actual full-footprint approach from the larger-map route");
                    }
                    while (route.Count > 0 && (route[0]-g.Position).Length < .16f) route.RemoveAt(0);
                    movement = route.Count > 0 ? (route[0]-g.Position).Normalized : g.DirectionTo(g.Position,target.Position);
                }
                if (target.state == 1 && target.kind == 2 && (g.Position-target.target).Length < 2)
                {
                    movement = (g.Position-target.target).Length > .05f ? (g.Position-target.target).Normalized : new Point(-1,0);
                    g.Dash(movement);
                }
                g.Tick(.02f,movement);
                if (contact && g.enemies.Contains(target))
                {
                    g.Attack(target.Position-g.Position);
                    if (distanceTo < 3) g.Skill(target.Position-g.Position);
                }
                if (g.save.health < g.save.maxHealth*.6f && g.save.inventory[6] > 0) g.Eat();
            }
            string remaining = "";
            foreach (Enemy enemy in g.enemies) remaining += " id="+enemy.id+" @"+enemy.x+","+enemy.y+" hp="+enemy.hp;
            Check(frames < 30000, "Actual exploration combat and routes must converge: player="+g.save.x+","+g.save.y+remaining);
            return frames;
        }

        public static void ReachEveryStarterClassClearsActualPatrolsWithoutRewardFarming(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++)
            {
                var g = ReachGame(data,profession); ReachOpenRoutes(g);
                int kills = g.save.kills[11], coins = g.save.coins, bounty = g.save.bountyProgress;
                int[] inventory = (int[])g.save.inventory.Clone(); int[] kinds = { 0,1,2,0,1,2,0 };
                for (int i = 0; i < 7; i++) Check(g.enemies.Exists(e => e.id == 76000+i && e.kind == kinds[i] && !e.boss && g.CanStand(e.Position)),
                    "Each starter profession receives the same correctly placed authored normal patrol");
                Console.WriteLine("  Reach starter battle: profession=" + profession + ", opponents=" + g.enemies.Count);
                int frames = ReachCombatRoutes(g); Advance(g,.2f);
                Console.WriteLine("  Reach starter victory: profession=" + profession + ", frames=" + frames + ", health=" + g.save.health + ", seconds=" + g.save.totalTime);
                Check(g.save.room == 11 && !g.Dying && g.enemies.Count == 0 && g.save.deaths == 0 && g.save.kills[11] == kills+7 && g.save.coins == coins+42,
                    "Actual starter attacks, techniques, dodges and existing supplies clear all seven normal foes with their unchanged ordinary rewards");
                for (int i = 0; i < 7; i++) Check(g.Has("reach_defeated_"+(76000+i)), "Every real defeat belongs to a permanent authored patrol identity");
                Check(g.save.inventory[1] == inventory[1]+2 && g.save.inventory[2] == inventory[2]+3 && g.save.inventory[4] == inventory[4]+2 && g.save.bountyProgress == bounty,
                    "Normal patrol kills retain existing material rewards and cannot create an unaccepted contract");
                Check(!g.Has("boss_11") && !g.Has("core_2") && g.save.stage == 0,
                    "Expanded-map combat cannot impersonate a chapter guardian or starcore delivery");
                g.Enter(1); g.Enter(11); Check(g.enemies.Count == 0 && g.save.kills[11] == kills+7 && g.save.coins == coins+42,
                    "Revisiting a cleared optional route preserves traversal without a repeatable normal-enemy economy farm");
            }
        }
    }
}
