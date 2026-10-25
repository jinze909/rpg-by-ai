using System;
using System.Collections.Generic;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static readonly int[] MaterialSourceItems = { 0, 1, 2, 3, 4, 7 };
        private static GameCore MaterialSourceGame(Campaign data, int room = 0)
        {
            var g = Unlocked(data); g.SkipTutorial();
            foreach (string flag in new[] { "core_1", "core_2", "core_3", "scout_saved", "gate_2", "gate_3" })
                if (!g.Has(flag)) g.save.flags.Add(flag);
            g.Enter(room); g.enemies.Clear(); g.projectiles.Clear(); g.hazards.Clear();
            for (int map = 0; map < g.save.explored.Length; map++) g.save.explored[map] = 16777215;
            g.mode = "materials"; return g;
        }
        private static MaterialSource MaterialSourceFind(GameCore g, int item, int room, string kind)
        {
            foreach (MaterialSource source in g.MaterialSources(item))
                if (source.room == room && source.target.kind == kind) return source;
            throw new Exception("Missing genuine material source: " + item + "/" + room + "/" + kind);
        }
        private static int MaterialSourceCount(GameCore g, int item, int room, string kind)
        {
            int count = 0; foreach (MaterialSource source in g.MaterialSources(item))
                if (source.room == room && source.target.kind == kind) count++;
            return count;
        }
        private static void MaterialSourceApproach(GameCore g, WorldObject target)
        {
            g.Close(); List<Point> route = g.InteractionPath(target);
            Check(route.Count > 0 || ReferenceEquals(g.Nearest(), target), "Source route reaches its actual original interaction");
            InteractionRouteOwnsEndpoint(g, target, route);
            if (route.Count > 0) { Point end = route[route.Count - 1]; g.save.x = end.x; g.save.y = end.y; }
            Check(ReferenceEquals(g.Nearest(), target), "The source retains ownership of the original E selector");
        }
        private static void MaterialSourceChoose(GameCore g, string action)
        {
            Check(g.mode == "dialog" && g.conversation != null, "A real source conversation is open");
            int choice = Array.FindIndex(g.conversation.choices, c => c.action == action);
            Check(choice >= 0, "Original source offers action " + action); g.Choose(choice);
        }
        private static void MaterialSourceReads(GameCore g)
        {
            for (int repeat = 0; repeat < 8; repeat++) foreach (int item in MaterialSourceItems)
            {
                List<MaterialSource> a = g.MaterialSources(item), b = g.MaterialSources(item);
                Check(!ReferenceEquals(a, b), "Callers receive independent source lists");
                foreach (MaterialSource source in a) { g.CanPinMaterialSource(source); source.method.ToString(); source.detail.ToString(); }
                a.Clear(); Check(g.MaterialSources(item).Count == b.Count, "Mutating a returned list cannot alter available sources");
            }
        }

        public static void MaterialSourcesExposeOnlyEarnedCanonicalMethodsAndLiveQuotes(Campaign data)
        {
            var g = MaterialSourceGame(data); int total = 0;
            foreach (int item in MaterialSourceItems)
            {
                var seen = new HashSet<string>();
                foreach (MaterialSource source in g.MaterialSources(item))
                {
                    Check(source.item == item && source.room != 8 && g.CanEnter(source.room) && source.target != null
                        && Array.IndexOf(data.maps[source.room].objects, source.target) >= 0 && g.IsObjectPresent(source.target)
                        && !string.IsNullOrEmpty(source.method) && !string.IsNullOrEmpty(source.detail),
                        "Every advertised method belongs to a real earned named source");
                    Check(seen.Add(source.room + ":" + source.target.id), "An item's source list has no duplicate object destination"); total++;
                }
            }
            Check(total >= 35, "The catalogue covers authored regions, resources and existing services, rather than a synthetic sample");
            Check(g.MaterialSources(-1).Count == 0 && g.MaterialSources(5).Count == 0 && g.MaterialSources(6).Count == 0
                && g.MaterialSources(8).Count == 0 && g.MaterialSources(99).Count == 0, "Only supported raw materials have source recipes");
            MaterialSource timber = MaterialSourceFind(g, 0, 0, "cook");
            Check(timber.detail.Contains("4") && timber.detail.Contains("10"), "Ari sells the original four timber for ten coins");
            g.save.reputation[1] = 3;
            Check(MaterialSourceFind(g, 0, 0, "cook").detail.Contains("8") && timber.detail.Contains("10"),
                "The live price refreshes to eight without mutating an older source snapshot");
            MaterialSource seed = MaterialSourceFind(g, 7, 0, "garden");
            Check(seed.detail.Contains("3") && seed.detail.Contains("5"), "An's existing seed purchase reports three seeds for five coins");
            g.save.projects[0] = 0; g.save.frontierEnding = 0;
            MaterialSource garden = MaterialSourceFind(g, 2, 0, "garden");
            Check(garden.detail.Contains("5") && garden.detail.Contains("60"), "The baseline herb garden reports five herbs and the existing sixty-second growth");
            g.save.projects[0] = 2; g.save.frontierEnding = 1;
            Check(MaterialSourceFind(g, 2, 0, "garden").detail.Contains("11") && garden.detail.Contains("5"),
                "The actual canal and ending bonuses refresh to eleven herbs, keeping earlier details immutable");
            foreach (int item in new[] { 1, 4 })
                Check(MaterialSourceFind(g, item, 1, "ore").detail.Contains("3"), "Ore sources expose the real three-crystal and three-part co-yield");
            Check(MaterialSourceCount(g, 3, GameCore.ReachRoom, "riverFishing0") == 0, "An unaccepted river lesson cannot leak its fishing banks");
            g.save.flags.Add("ally_2"); g.save.flags.Add("riverlesson_accept");
            Check(MaterialSourceCount(g, 3, GameCore.ReachRoom, "riverFishing0") == 1
                && MaterialSourceCount(g, 3, GameCore.ReachRoom, "riverFishing1") == 1, "Both real banks appear only after their original accepted lesson");
            WorldObject ore = Object(MaterialSourceRoom(g, 1), "ore"); g.save.flags.Add("node_" + ore.id);
            MaterialSource exhausted = MaterialSourceFind(g, 1, 1, "ore");
            Check(exhausted.depleted && !g.CanPinMaterialSource(exhausted) && exhausted.detail.Length > 0,
                "A gathered resource remains explanatory but cannot promise an active gathering route");
            for (int room = 0; room < g.save.explored.Length; room++) g.save.explored[room] = 0;
            foreach (int item in MaterialSourceItems) Check(g.MaterialSources(item).Count == 0, "Undiscovered sources never reveal new map sectors");
            g.save.explored[0] = 16777215; WorldObject cook = Object(MaterialSourceRoom(g, 0), "cook"); WorldObject[] authored = g.Map.objects;
            try
            {
                foreach (WorldObject bad in new[] {
                    new WorldObject { id = "bad:id", kind = "cook", name = "Bad", x = 8, y = 17 },
                    new WorldObject { id = "bad_nan", kind = "garden", name = "Bad", x = float.NaN, y = 17 },
                    new WorldObject { id = "bad_name", kind = "fish", x = 8, y = 17 },
                    new WorldObject { id = "bad_outside", kind = "wood", name = "Bad", x = -1, y = 17 } })
                {
                    var copy = new List<WorldObject>(authored); copy.Add(bad); g.Map.objects = copy.ToArray();
                    foreach (int item in MaterialSourceItems) foreach (MaterialSource source in g.MaterialSources(item))
                        Check(!ReferenceEquals(source.target, bad), "Invalid canonical metadata cannot disclose an actionable source");
                }
                var duplicate = new WorldObject { id = cook.id, kind = cook.kind, name = cook.name, x = cook.x + 1, y = cook.y };
                var both = new List<WorldObject>(authored); both.Add(duplicate); g.Map.objects = both.ToArray();
                Check(MaterialSourceCount(g, 0, 0, "cook") == 0, "Ambiguous stable source IDs are never persisted or advertised");
            }
            finally { g.Map.objects = authored; }
        }
        private static GameCore MaterialSourceRoom(GameCore g, int room)
        { if (g.save.room != room) { g.Close(); g.Enter(room); g.enemies.Clear(); g.projectiles.Clear(); g.hazards.Clear(); } g.mode = "materials"; return g; }

        public static void MaterialSourcesPinsUseRealInteractionsAndPreserveOriginalYields(Campaign data)
        {
            foreach (string kind in new[] { "wood", "herb", "ore" })
            {
                var g = MaterialSourceGame(data, 1); int item = kind == "wood" ? 0 : kind == "herb" ? 2 : 1;
                if (kind == "ore") { g.Close(); g.AcceptBounty(1); g.mode = "materials"; }
                MaterialSource source = MaterialSourceFind(g, item, 1, kind); string tracked = g.save.trackedQuest;
                Check(g.CanPinMaterialSource(source) && g.PinMaterialSource(source) && ReferenceEquals(g.PinnedLandmark, source.target)
                    && g.save.trackedQuest == tracked, "A deliberate material choice uses the original stable personal pin without replacing a quest");
                string flag = "landmark_pin:1:" + source.target.id; MaterialSourceApproach(g, source.target); SaveData before = CloneSave(g.save); g.Interact();
                for (int index = 0; index < g.save.inventory.Length; index++)
                    Check(g.save.inventory[index] == before.inventory[index] + (index == item || kind == "ore" && index == 4 ? 3 : 0),
                        "E gathering grants the original exact mature recipe, including the ore co-yield");
                Check(g.save.bountyProgress == before.bountyProgress + (kind == "ore" ? 1 : 0) && g.save.coins == before.coins
                    && g.save.shards == before.shards && g.save.xp == before.xp && g.save.health == before.health && g.save.energy == before.energy,
                    "Gathering adds only the existing one ore bounty event and no extra currency, XP or healing");
                Check(g.Has("node_" + source.target.id) && !g.Has(flag) && !g.HasLandmarkPin && g.save.trackedQuest == tracked,
                    "Collecting the selected resource retires only the fulfilled material pin");
                g.mode = "materials"; Check(MaterialSourceFind(g, item, 1, kind).depleted, "The source now explains its existing depletion");
                g.Close(); g.Enter(0); Action(g, "rest", "rest"); g.Close(); g.Enter(1); g.enemies.Clear(); g.mode = "materials";
                Check(!MaterialSourceFind(g, item, 1, kind).depleted && !g.HasLandmarkPin, "Original inn refresh renews the node without resurrecting a completed route");
            }
            foreach (int reputation in new[] { 0, 3 })
            {
                var g = MaterialSourceGame(data); g.save.coins = 100; g.save.reputation[1] = reputation;
                MaterialSource source = MaterialSourceFind(g, 0, 0, "cook"); Check(g.PinMaterialSource(source), "The existing cook is an explicit material destination");
                MaterialSourceApproach(g, source.target); g.Interact(); int coins = g.save.coins, wood = g.save.inventory[0]; MaterialSourceChoose(g, "buywood");
                Check(g.save.inventory[0] == wood + 4 && g.save.coins == coins - (reputation == 3 ? 8 : 10),
                    "The original dialogue purchases four timber for the actual normal or reputation price");
            }
            var farm = MaterialSourceGame(data); farm.save.coins = 100; farm.save.projects[0] = 2; farm.save.frontierEnding = 1;
            MaterialSource seeds = MaterialSourceFind(farm, 7, 0, "garden"); Check(farm.PinMaterialSource(seeds), "Choose the genuine garden service");
            MaterialSourceApproach(farm, seeds.target); farm.Interact(); int stock = farm.save.inventory[7]; MaterialSourceChoose(farm, "seeds");
            Check(farm.save.inventory[7] == stock + 3 && farm.save.coins == 95, "The actual seed purchase remains three seeds for five coins");
            farm.Close(); MaterialSourceApproach(farm, seeds.target); farm.Interact(); stock = farm.save.inventory[7]; MaterialSourceChoose(farm, "plant");
            Check(farm.save.inventory[7] == stock - 1 && farm.save.plantedAt >= 0, "Original planting still consumes one seed");
            farm.Close(); Action(farm, "rest", "rest"); farm.Close(); Action(farm, "rest", "rest"); farm.Close(); MaterialSourceApproach(farm, seeds.target); farm.Interact();
            int herbs = farm.save.inventory[2], coinsBefore = farm.save.coins; stock = farm.save.inventory[7]; MaterialSourceChoose(farm, "harvest");
            Check(farm.save.inventory[2] == herbs + 11 && farm.save.inventory[7] == stock + 2 && farm.save.coins == coinsBefore + 8,
                "The actual grown plot pays eleven earned herbs, two seeds and eight coins under the unchanged mature bonuses");
            var lake = MaterialSourceGame(data); MaterialSource fisher = MaterialSourceFind(lake, 3, 0, "fish"); Check(lake.PinMaterialSource(fisher), "Choose Old Tide through a real material source");
            int fish = lake.save.inventory[3], fishCoins = lake.save.coins; RiverLakeCatch(lake);
            Check(lake.save.inventory[3] == fish + 1 && lake.save.coins == fishCoins + 4, "Three real original pulls still pay one fish and four coins");
            var river = RiverGame(data, true); RiverEnter(river); river.save.explored[GameCore.ReachRoom] = 16777215;
            foreach (int site in new[] { 0, 1 })
            {
                river.mode = "materials"; MaterialSource bank = MaterialSourceFind(river, 3, GameCore.ReachRoom, "riverFishing" + site);
                Check(river.PinMaterialSource(bank), "An earned charted river bank can own its actual local route");
                int caught = river.save.inventory[3], riverCoins = river.save.coins; RiverCatch(river, site);
                Check(river.save.inventory[3] == caught + 1 && river.save.coins == riverCoins + 4,
                    "Each real river bank retains its original three-pull single fish and four-coin reward");
            }
            var other = MaterialSourceGame(data, 1); MaterialSource timberNode = MaterialSourceFind(other, 0, 1, "wood"); Check(other.PinMaterialSource(timberNode), "Choose a different material pin");
            MaterialSourceApproach(other, Object(other, "herb")); other.Interact();
            Check(ReferenceEquals(other.PinnedLandmark, timberNode.target), "Gathering an unrelated node never clears the selected timber destination");
        }

        public static void MaterialSourcesRejectRemoteStaleDefeatedAndForeignPinIntents(Campaign data)
        {
            foreach (string mode in new[] { "world", "inventory", "landmarks", "map", "journal", "dialog", "title" })
            {
                var g = MaterialSourceGame(data); MaterialSource source = MaterialSourceFind(g, 0, 0, "cook"); g.mode = mode; string before = PlanStatsState(g);
                Check(!g.CanPinMaterialSource(source) && !g.PinMaterialSource(source) && PlanStatsState(g) == before,
                    "A materials callback cannot mutate another owner mode: " + mode);
            }
            var local = MaterialSourceGame(data); MaterialSource remote = MaterialSourceFind(local, 0, 1, "wood"); string snapshot = PlanStatsState(local);
            Check(!local.CanPinMaterialSource(remote) && !local.PinMaterialSource(remote) && PlanStatsState(local) == snapshot, "A listed remote source never redirects the wrong map");
            MaterialSource own = MaterialSourceFind(local, 0, 0, "cook"); var foreign = MaterialSourceGame(data); MaterialSource alien = MaterialSourceFind(foreign, 0, 0, "cook");
            Check(!local.PinMaterialSource(alien) && !local.PinMaterialSource(null), "Matching map objects cannot authorize a source from another save owner");
            ConstructorInfo ctor = typeof(MaterialSource).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)[0];
            var copiedTarget = new WorldObject { id = own.target.id, kind = own.target.kind, name = own.target.name, en = own.target.en, x = own.target.x, y = own.target.y };
            var forgery = (MaterialSource)ctor.Invoke(new object[] { own.item, own.room, copiedTarget, false, own.method, own.detail, local.save, local.WorldGeneration });
            snapshot = PlanStatsState(local);
            Check(!local.CanPinMaterialSource(forgery) && !local.PinMaterialSource(forgery) && PlanStatsState(local) == snapshot,
                "Even a matching owner and copied coordinates cannot authorize a noncanonical world object");
            Check(local.PinMaterialSource(own), "A current valid choice may set one personal route"); snapshot = PlanStatsState(local);
            Check(!local.CanPinMaterialSource(own) && !local.PinMaterialSource(own) && PlanStatsState(local) == snapshot, "Choosing the current destination is a genuine no-op");
            var loaded = MaterialSourceGame(data); MaterialSource beforeLoad = MaterialSourceFind(loaded, 0, 0, "cook"); loaded.Load(CloneSave(loaded.save)); loaded.mode = "materials";
            snapshot = PlanStatsState(loaded); Check(!loaded.PinMaterialSource(beforeLoad) && PlanStatsState(loaded) == snapshot, "Load invalidates the old saved owner and generation");
            var moved = MaterialSourceGame(data); MaterialSource beforeTravel = MaterialSourceFind(moved, 0, 0, "cook"); moved.Enter(1); moved.Enter(0); moved.enemies.Clear(); moved.mode = "materials";
            snapshot = PlanStatsState(moved); Check(!moved.PinMaterialSource(beforeTravel) && PlanStatsState(moved) == snapshot, "Returning to the same map does not revive an obsolete world-generation callback");
            var exhausted = MaterialSourceGame(data, 1); MaterialSource oldOre = MaterialSourceFind(exhausted, 1, 1, "ore"); exhausted.save.flags.Add("node_" + oldOre.target.id);
            snapshot = PlanStatsState(exhausted); Check(!exhausted.PinMaterialSource(oldOre) && PlanStatsState(exhausted) == snapshot, "A row rechecks actual depletion at confirmation");
            var hidden = MaterialSourceGame(data); MaterialSource hiddenCook = MaterialSourceFind(hidden, 0, 0, "cook"); hidden.save.explored[0] = 0;
            snapshot = PlanStatsState(hidden); Check(!hidden.PinMaterialSource(hiddenCook) && PlanStatsState(hidden) == snapshot, "Stale discovery cannot become a saved hidden-map route");
            var locked = MaterialSourceGame(data, 2); MaterialSource lockedOre = MaterialSourceFind(locked, 1, 2, "ore"); locked.save.stage = 0;
            snapshot = PlanStatsState(locked); Check(MaterialSourceCount(locked, 1, 2, "ore") == 0 && !locked.PinMaterialSource(lockedOre) && PlanStatsState(locked) == snapshot, "A current-map reference cannot bypass original story access");
            var abyss = MaterialSourceGame(data); MaterialSource surface = MaterialSourceFind(abyss, 0, 0, "cook"); abyss.Close(); abyss.StartAbyss(0); abyss.mode = "materials";
            snapshot = PlanStatsState(abyss);
            Check(abyss.InAbyss && abyss.MaterialSources(0).Count == 0 && !abyss.PinMaterialSource(surface) && PlanStatsState(abyss) == snapshot,
                "A live seeded Abyss run retains navigation ownership without surface catalogue leakage");
            var fallen = MaterialSourceGame(data); MaterialSource dyingCook = MaterialSourceFind(fallen, 0, 0, "cook"); fallen.Close(); fallen.save.health = 1; fallen.invulnerable = 0;
            fallen.projectiles.Add(new Projectile { position = fallen.Position, damage = 999, source = "shot" }); fallen.Tick(.02f, new Point()); Check(fallen.Dying, "A real lethal contact owns the defeat state");
            fallen.mode = "materials"; snapshot = PlanStatsState(fallen);
            Check(fallen.MaterialSources(0).Count == 0 && !fallen.PinMaterialSource(dyingCook) && PlanStatsState(fallen) == snapshot, "Defeat suppresses source navigation without inventing a refund or route");
            foreach (bool safeTraining in new[] { false, true })
            {
                var training = safeTraining ? RangeGame(data) : MaterialSourceGame(data); if (safeTraining) Check(training.StartRange(0), "Start actual safe range"); else training.practicing = true;
                training.mode = "materials"; snapshot = PlanStatsState(training);
                Check(training.MaterialSources(0).Count == 0 && !training.PinMaterialSource(own) && PlanStatsState(training) == snapshot,
                    "Practice and safe range preserve their navigation ownership");
            }
        }

        public static void MaterialSourcesQueriesPreserveCombatQueuesAndLegacySaves(Campaign data)
        {
            Check(typeof(MaterialSource).IsSealed, "Material source snapshots are sealed");
            foreach (FieldInfo field in typeof(MaterialSource).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Check(field.IsInitOnly, "Every public source value is readonly: " + field.Name);
            var g = MaterialSourceGame(data, 1); g.mode = "world"; g.save.weapon = 1; g.save.x = 12; g.save.y = 14; g.Attack(new Point(1, 0));
            ActorAction prepared = g.playerAction; Check(prepared != null && !prepared.released, "Prepare an actual paid arrow before catalogue reads");
            string before = PlanStatsState(g); MaterialSourceReads(g);
            Check(PlanStatsState(g) == before && ReferenceEquals(g.playerAction, prepared), "Queries preserve every public combat/save/RNG field, queued sound and accepted attack identity");
            for (int frame = 0; frame < 80 && !prepared.released; frame++) g.Tick(.01f, new Point());
            Check(prepared.released && g.projectiles.Count > 0, "The same preparation still releases its actual projectile"); Projectile shot = g.projectiles[0];
            before = PlanStatsState(g); MaterialSourceReads(g); Check(PlanStatsState(g) == before && ReferenceEquals(shot, g.projectiles[0]), "Read-only sources cannot move, consume or recapture an airborne arrow");
            foreach (string kind in new[] { "attack", "skill", "dodge", "companion" })
            {
                var queued = MaterialSourceGame(data, 1); queued.Close(); string field;
                if (kind == "attack") { queued.attackCooldown = .08f; queued.PressAttack(new Point(0, 1)); field = "bufferedAttack"; }
                else if (kind == "skill") { queued.skillCooldown = .08f; queued.Skill(new Point(0, 1)); field = "bufferedSkill"; }
                else if (kind == "dodge") { queued.dashCooldown = .08f; queued.Dash(new Point(0, 1)); field = "bufferedDodge"; }
                else { queued.save.flags.Add("companion_1"); queued.SelectCompanion(1); queued.companionCooldown = .08f; queued.CommandCompanion(); field = "bufferedCompanionCommand"; }
                object intent = PlanStatsIntent(queued, field); Check(intent != null, "A real unpaid queued intent exists: " + kind); before = PlanStatsState(queued); MaterialSourceReads(queued);
                Check(PlanStatsState(queued) == before && ReferenceEquals(PlanStatsIntent(queued, field), intent), "Source reads preserve the original intent lifetime and captured aim: " + kind);
                Advance(queued, .12f); Check(kind == "dodge" ? queued.dashTime > 0 : kind == "companion" ? queued.companionAction != null : queued.playerAction != null,
                    "The untouched input still dispatches after catalogue reads: " + kind);
            }
            for (int version = 1; version <= 2; version++)
            {
                var original = MaterialSourceGame(data); MaterialSource cook = MaterialSourceFind(original, 0, 0, "cook"); original.PinMaterialSource(cook); original.PrepareSave();
                SaveData old = CloneSave(original.save); old.version = version; old.flags.Add("material_source_unknown_preserve");
                var resumed = new GameCore(data, old); resumed.Start(); resumed.mode = "materials"; before = PlanStatsState(resumed);
                Check(resumed.HasLandmarkPin && ReferenceEquals(resumed.PinnedLandmark, Object(resumed, "cook")), "Existing landmark storage resolves canonical sources after v" + version + " load");
                MaterialSourceReads(resumed); Check(PlanStatsState(resumed) == before && resumed.Has("material_source_unknown_preserve"), "Legacy reads preserve unrelated progress and flags");
                resumed.PrepareSave(); var again = new GameCore(data, CloneSave(resumed.save)); again.Start();
                Check(again.HasLandmarkPin && again.save.trackedQuest == resumed.save.trackedQuest && again.save.coins == resumed.save.coins,
                    "Normal prepare/load retains the existing waypoint without a new save field or payment");
            }
            foreach (FieldInfo field in typeof(SaveData).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Check(field.Name.IndexOf("materialsource", StringComparison.OrdinalIgnoreCase) < 0, "The material catalogue does not extend the save schema");
        }
    }
}
