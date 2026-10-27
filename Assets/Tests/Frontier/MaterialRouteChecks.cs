using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        // Reflection lets the frozen pre-feature production compile and fail
        // assertions on missing behavior, rather than fail C# compilation.
        private static MethodInfo MaterialRouteMethod(string name)
        {
            MethodInfo method = typeof(GameCore).GetMethod(name, new[] { typeof(MaterialSource) });
            Check(method != null, "Explicit material map route API exists: " + name);
            return method;
        }
        private static void MaterialRouteApis()
        {
            Check(MaterialRouteMethod("GetMaterialRouteSource").ReturnType == typeof(MaterialSource), "Preview returns a fresh material source");
            Check(MaterialRouteMethod("CanRouteMaterialSource").ReturnType == typeof(bool) &&
                MaterialRouteMethod("RouteToMaterialSource").ReturnType == typeof(bool), "Route queries and confirmation return acceptance");
        }
        private static MaterialSource MaterialRoutePreview(GameCore g, MaterialSource source)
        { return (MaterialSource)MaterialRouteMethod("GetMaterialRouteSource").Invoke(g, new object[] { source }); }
        private static bool MaterialRouteCan(GameCore g, MaterialSource source)
        { return (bool)MaterialRouteMethod("CanRouteMaterialSource").Invoke(g, new object[] { source }); }
        private static bool MaterialRouteConfirm(GameCore g, MaterialSource source)
        { return (bool)MaterialRouteMethod("RouteToMaterialSource").Invoke(g, new object[] { source }); }
        private static void MaterialRouteRejected(GameCore g, MaterialSource source, string reason, bool preview = false)
        {
            string before = PlanStatsState(g);
            Check((MaterialRoutePreview(g, source) != null) == preview && !MaterialRouteCan(g, source) && !MaterialRouteConfirm(g, source), reason);
            Check(PlanStatsState(g) == before, "Rejected confirmation and both queries are pure: " + reason);
        }
        private static void MaterialRouteRead(GameCore g, MaterialSource source)
        { for (int repeat = 0; repeat < 8; repeat++) { MaterialRoutePreview(g, source); MaterialRouteCan(g, source); } }

        public static void MaterialRoutePreviewsRemainPureAndRefreshCanonicalDescriptions(Campaign data)
        {
            MaterialRouteApis(); var g = MaterialSourceGame(data);
            foreach (int item in MaterialSourceItems) foreach (MaterialSource source in g.MaterialSources(item))
            {
                foreach (string mode in new[] { "materials", "map" })
                {
                    g.mode = mode; string before = PlanStatsState(g);
                    MaterialSource current = MaterialRoutePreview(g, source);
                    Check(current != null && !ReferenceEquals(current, source) && current.item == item && current.room == source.room &&
                        ReferenceEquals(current.target, source.target) && current.depleted == source.depleted,
                        "Each earned local or remote preview resolves a fresh canonical catalogue description");
                    Check(MaterialRouteCan(g, source) == (mode == "map" && !source.depleted), "Only explicit map confirmation can own an available source route");
                    MaterialRouteRead(g, source); Check(PlanStatsState(g) == before, "Repeated preview and availability queries change no simulation, save or navigation state");
                }
            }
            g.mode = "materials"; MaterialSource cook = MaterialSourceFind(g, 0, 0, "cook");
            Check(cook.detail.Contains("10"), "The genuine initial cook quote is ten coins");
            g.save.reputation[1] = 3;
            Check(MaterialRoutePreview(g, cook).detail.Contains("8"), "Preview refreshes an earned live shop quote without trusting old description text");
            MaterialRouteRejected(g, cook, "The catalogue's view action cannot itself confirm travel or pinning", true);
            MaterialSource node = MaterialSourceFind(g, 0, 1, "wood"); g.save.flags.Add("node_" + node.target.id); g.mode = "map";
            MaterialSource depleted = MaterialRoutePreview(g, node);
            Check(depleted != null && depleted.depleted && depleted.detail.Contains(g.save.english ? "rest" : "休息"), "A spent row remains previewable and reports its current original rest requirement");
            MaterialRouteRejected(g, node, "Depletion after preview disables confirmation without hiding the source", true);
        }

        public static void MaterialRouteConfirmationUsesOriginalTravelAndInteractionPayments(Campaign data)
        {
            MaterialRouteApis();
            var local = MaterialSourceGame(data, 1); MaterialSource herb = MaterialSourceFind(local, 2, 1, "herb");
            MaterialSource wood = MaterialSourceFind(local, 0, 1, "wood"); Check(local.PinMaterialSource(herb), "An older personal destination exists");
            local.enemies.Add(new Enemy(9001, 0, local.save.x + 1, local.save.y, 40));
            string tracked = local.save.trackedQuest; int generation = local.WorldGeneration, coins = local.save.coins, timber = local.save.inventory[0];
            float health = local.save.health, energy = local.save.energy; Point feet = local.Position;
            local.mode = "map";
            Check(!local.SafeTravel && MaterialRouteCan(local, wood) && MaterialRouteConfirm(local, wood), "A local source can be explicitly selected without invoking fast travel's combat gate");
            Check(local.mode == "world" && local.WorldGeneration == generation && (local.Position - feet).Length == 0 &&
                ReferenceEquals(local.PinnedLandmark, wood.target) && local.save.trackedQuest == tracked &&
                local.save.flags.FindAll(f => f != null && f.StartsWith("landmark_pin:", StringComparison.Ordinal)).Count == 1 &&
                local.save.health == health && local.save.energy == energy && local.save.coins == coins && local.save.inventory[0] == timber,
                "Local confirmation replaces only the personal pin, closes the map and grants no travel, supply or healing");
            local.mode = "map"; int revision = local.revision; string sound = local.sound;
            Check(MaterialRouteConfirm(local, wood) && local.revision == revision && local.sound == sound, "Confirming the exact current pin succeeds without duplicate revision or sound");
            local.enemies.Clear(); MaterialSourceApproach(local, wood.target); local.Interact();
            Check(local.save.inventory[0] == timber + 3 && local.save.coins == coins && local.Has("node_" + wood.target.id) &&
                !local.HasLandmarkPin && local.save.trackedQuest == tracked, "Only original E gathering grants three timber and retires the fulfilled personal route");

            var remote = MaterialSourceGame(data); MaterialSource ore = MaterialSourceFind(remote, 1, 1, "ore");
            remote.save.health = 63; remote.save.energy = 47; remote.save.coins = 79; remote.save.trackedQuest = "secrets";
            SaveData before = CloneSave(remote.save); generation = remote.WorldGeneration; remote.mode = "map";
            Check(MaterialRouteCan(remote, ore) && MaterialRouteConfirm(remote, ore), "The explicit safe remote confirmation travels and creates its arrival waypoint");
            Check(remote.save.room == 1 && remote.mode == "world" && remote.WorldGeneration > generation &&
                (remote.Position - new Point(remote.Map.spawnX, remote.Map.spawnY)).Length == 0 && ReferenceEquals(remote.PinnedLandmark, ore.target) &&
                remote.save.health == before.health && remote.save.energy == before.energy && remote.save.coins == before.coins &&
                remote.save.trackedQuest == before.trackedQuest && remote.save.inventory[1] == before.inventory[1] && remote.save.inventory[4] == before.inventory[4],
                "Original Enter owns arrival/reset while routing preserves health, energy, payments, quest tracking and supplies");
            MaterialRouteRejected(remote, ore, "The old remote preview loses authority after its own travel changes world generation");
            remote.enemies.Clear(); MaterialSourceApproach(remote, ore.target); remote.Interact();
            Check(remote.save.inventory[1] == before.inventory[1] + 3 && remote.save.inventory[4] == before.inventory[4] + 3 &&
                remote.save.coins == before.coins && !remote.HasLandmarkPin, "The arrived original ore interaction alone grants both three-item co-yields and completes its pin");
            foreach (int reputation in new[] { 0, 3 })
            {
                var shopper = MaterialSourceGame(data, 1); shopper.save.coins = 100; shopper.save.reputation[1] = reputation;
                MaterialSource cook = MaterialSourceFind(shopper, 0, 0, "cook"); timber = shopper.save.inventory[0]; shopper.mode = "map";
                Check(MaterialRouteConfirm(shopper, cook) && shopper.save.room == 0 && shopper.save.coins == 100 && shopper.save.inventory[0] == timber,
                    "Remote merchant routing enters the real town without automatically making a purchase");
                MaterialSourceApproach(shopper, cook.target); shopper.Interact(); MaterialSourceChoose(shopper, "buywood");
                Check(shopper.save.coins == 100 - (reputation == 3 ? 8 : 10) && shopper.save.inventory[0] == timber + 4 &&
                    ReferenceEquals(shopper.PinnedLandmark, cook.target), "The actual shop retains its normal/reputation price, four-timber payment and reusable merchant pin");
            }
        }

        public static void MaterialRouteRejectsStaleForeignUnsafeAndUnavailableChoices(Campaign data)
        {
            MaterialRouteApis();
            foreach (string mode in new[] { "world", "inventory", "landmarks", "journal", "dialog", "title", "fish", "camp" })
            { var g = MaterialSourceGame(data); MaterialSource source = MaterialSourceFind(g, 0, 0, "cook"); g.mode = mode; MaterialRouteRejected(g, source, "Unowned mode " + mode); }
            var own = MaterialSourceGame(data); MaterialSource cook = MaterialSourceFind(own, 0, 0, "cook"); own.mode = "map";
            MaterialRouteRejected(own, null, "Null destination");
            var foreign = MaterialSourceGame(data); MaterialRouteRejected(own, MaterialSourceFind(foreign, 0, 0, "cook"), "A different save's matching map target");
            ConstructorInfo constructor = typeof(MaterialSource).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)[0];
            var copied = new WorldObject { id = cook.target.id, kind = cook.target.kind, name = cook.target.name, en = cook.target.en, x = cook.target.x, y = cook.target.y };
            var forgery = (MaterialSource)constructor.Invoke(new object[] { cook.item, cook.room, copied, false, cook.method, cook.detail, own.save, own.WorldGeneration });
            MaterialRouteRejected(own, forgery, "A copied noncanonical target with matching owner/generation");
            var wrongItem = (MaterialSource)constructor.Invoke(new object[] { 1, cook.room, cook.target, false, cook.method, cook.detail, own.save, own.WorldGeneration });
            MaterialRouteRejected(own, wrongItem, "A canonical cook falsely advertised as a crystal source");
            foreach (string boundary in new[] { "load", "same-owner-load", "travel", "new-game", "uncharted", "locked", "owner", "practice", "range", "abyss", "death", "zero-health", "nan-health", "infinite-health", "nan-feet", "blocked-feet" })
            {
                var g = MaterialSourceGame(data); MaterialSource source = MaterialSourceFind(g, 1, 2, "ore");
                if (boundary == "load") g.Load(CloneSave(g.save));
                else if (boundary == "same-owner-load") g.Load(g.save);
                else if (boundary == "travel") { g.Enter(1); g.Enter(0); }
                else if (boundary == "new-game") { g.NewGame(0); g.Close(); }
                else if (boundary == "uncharted") g.save.explored[2] = 0;
                else if (boundary == "locked") g.save.stage = 0;
                else if (boundary == "owner") g.save = CloneSave(g.save);
                else if (boundary == "practice") { g.Close(); g.StartPractice(); Check(g.practicing, "Actual basic practice starts"); }
                else if (boundary == "range")
                { g.Close(); WorldObject mentor = Object(g, "mentor"); g.save.x = mentor.x; g.save.y = mentor.y + 1; Check(g.StartRange(0), "Actual safe range starts"); }
                else if (boundary == "abyss") { g.Close(); g.StartAbyss(0); Check(g.InAbyss, "Actual seeded Abyss owns navigation"); }
                else if (boundary == "death")
                { g.Close(); g.save.health = 1; g.invulnerable = 0; g.projectiles.Add(new Projectile { position = g.Position, damage = 999, source = "shot" }); g.Tick(.02f, new Point()); Check(g.Dying, "Actual lethal contact begins defeat"); }
                else if (boundary == "zero-health") g.save.health = 0;
                else if (boundary == "nan-health") g.save.health = float.NaN;
                else if (boundary == "infinite-health") g.save.health = float.PositiveInfinity;
                else if (boundary == "nan-feet") g.save.x = float.NaN;
                else if (boundary == "blocked-feet") { g.save.x = 0; g.save.y = 0; }
                g.mode = "map"; MaterialRouteRejected(g, source, "Stale/unavailable boundary " + boundary);
            }
            var unsafeGame = MaterialSourceGame(data); MaterialSource remote = MaterialSourceFind(unsafeGame, 0, 1, "wood");
            unsafeGame.enemies.Add(new Enemy(9002, 0, unsafeGame.save.x + 1, unsafeGame.save.y, 40)); unsafeGame.mode = "map";
            Check(!unsafeGame.SafeTravel, "The original nearby-foe travel gate is active");
            MaterialRouteRejected(unsafeGame, remote, "An earned preview cannot bypass original unsafe remote travel", true);
        }

        public static void MaterialRoutePreservesPreparedActionsSnapshotsAndLegacyPins(Campaign data)
        {
            MaterialRouteApis();
            var g = MaterialSourceGame(data, 1); MaterialSource source = MaterialSourceFind(g, 0, 1, "wood");
            g.Close(); g.save.weapon = 1; g.save.x = 12; g.save.y = 14; g.Attack(new Point(1, 0)); ActorAction action = g.playerAction;
            Check(action != null && !action.released, "An actual paid arrow preparation exists before map preview");
            g.mode = "map"; string state = PlanStatsState(g); MaterialRouteRead(g, source);
            Check(PlanStatsState(g) == state && ReferenceEquals(action, g.playerAction), "Map previews leave every public combat/save field and owned preparation untouched");
            float elapsed = action.elapsed; g.Tick(.05f, new Point(1, 0));
            Check(action.elapsed == elapsed && ReferenceEquals(g.playerAction, action) && !action.released, "Paused map time cannot dispatch or recapture the prepared shot");
            g.Close(); for (int frame = 0; frame < 80 && !action.released; frame++) g.Tick(.01f, new Point());
            Check(action.released && g.projectiles.Count > 0, "Closing the preview resumes the exact original projectile preparation");
            Projectile shot = g.projectiles[0]; state = PlanStatsState(g); MaterialRouteRead(g, source);
            Check(PlanStatsState(g) == state && ReferenceEquals(shot, g.projectiles[0]), "World-mode preview calls cannot advance or remove an airborne arrow");
            foreach (string kind in new[] { "attack", "skill", "dodge", "companion" })
            {
                var queued = MaterialSourceGame(data, 1); MaterialSource choice = MaterialSourceFind(queued, 0, 1, "wood"); queued.Close(); string field;
                if (kind == "attack") { queued.attackCooldown = .08f; queued.PressAttack(new Point(0, 1)); field = "bufferedAttack"; }
                else if (kind == "skill") { queued.skillCooldown = .08f; queued.Skill(new Point(0, 1)); field = "bufferedSkill"; }
                else if (kind == "dodge") { queued.dashCooldown = .08f; queued.Dash(new Point(0, 1)); field = "bufferedDodge"; }
                else { queued.save.flags.Add("companion_1"); queued.SelectCompanion(1); queued.companionCooldown = .08f; queued.CommandCompanion(); field = "bufferedCompanionCommand"; }
                object intent = PlanStatsIntent(queued, field); Check(intent != null, "Real queued input exists: " + kind);
                MaterialRouteRejected(queued, choice, "World callback cannot consume unpaid " + kind + " input");
                Check(ReferenceEquals(intent, PlanStatsIntent(queued, field)), "Rejected routing preserves the original intent object");
                Advance(queued, .12f); Check(kind == "dodge" ? queued.dashTime > 0 : kind == "companion" ? queued.companionAction != null : queued.playerAction != null,
                    "The original buffered action still dispatches after rejected routing: " + kind);
            }
            foreach (int version in new[] { 1, 2 })
            {
                var original = MaterialSourceGame(data); MaterialSource destination = MaterialSourceFind(original, 0, 1, "wood"); original.mode = "map";
                Check(MaterialRouteConfirm(original, destination), "A remote material destination is explicitly confirmed");
                Check(original.enemies.Count > 0, "Original travel restores an actual regional encounter");
                Enemy enemy = original.enemies[0]; enemy.hp = Math.Min(enemy.hp, 17); original.save.coins = 81; original.save.flags.Add("material_route_mature_unknown");
                string tracked = original.save.trackedQuest; original.PrepareSave(); SaveData saved = CloneSave(original.save); saved.version = version;
                Check(saved.worldEnemies != null && saved.worldEnemies.Length == original.enemies.Count, "Normal PrepareSave owns the unchanged world snapshot");
                var resumed = new GameCore(data, saved); resumed.Start();
                Enemy restored = resumed.enemies.Find(e => e.id == enemy.id);
                Check(resumed.save.version == 2 && restored != null && restored.hp == enemy.hp && (restored.Position - enemy.Position).Length == 0 &&
                    ReferenceEquals(resumed.PinnedLandmark, destination.target) && resumed.save.coins == 81 && resumed.save.trackedQuest == tracked &&
                    resumed.Has("material_route_mature_unknown") && resumed.save.flags.FindAll(f => f != null && f.StartsWith("landmark_pin:", StringComparison.Ordinal)).Count == 1,
                    "Version " + version + " retains original HP/feet, one canonical personal pin and unrelated campaign value");
                resumed.mode = "map"; MaterialRouteRejected(resumed, destination, "Original save's preview cannot authorize a loaded session");
                MaterialSource fresh = MaterialSourceFind(resumed, 0, 1, "wood"); int revision = resumed.revision;
                Check(MaterialRouteConfirm(resumed, fresh) && resumed.revision == revision && ReferenceEquals(resumed.PinnedLandmark, fresh.target),
                    "Fresh loaded preview can confirm an existing exact pin without duplicating persistence");
            }
            foreach (FieldInfo field in typeof(SaveData).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Check(field.Name.IndexOf("materialroute", StringComparison.OrdinalIgnoreCase) < 0, "Material routes add no save field");
        }
    }
}
