using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore ProjectileOrderGame(Campaign data, int weapon = 1)
        {
            var game = Quiet(data, weapon); game.SkipTutorial();
            game.save.x = 12; game.save.y = 14; game.save.companion = -1;
            game.save.seed = 1; game.projectiles.Clear(); game.effects.Clear(); game.hazards.Clear();
            Check(game.CanStand(game.Position), "Projectile ordering starts on legal feet in the shipped forest");
            return game;
        }
        private static Enemy ProjectileOrderTarget(GameCore game, int id, Point position, bool boss = false)
        {
            var enemy = new Enemy(id, 0, position.x, position.y, 2000, boss) { timer = 99, stun = 99 };
            game.enemies.Add(enemy); return enemy;
        }
        private static bool ProjectileOrderNear(float actual, float expected)
        { return Math.Abs(actual - expected) < .0005f; }
        private static void ProjectileOrderUpdate(GameCore game, float dt)
        {
            typeof(GameCore).GetMethod("UpdateProjectiles", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(game, new object[] { dt });
        }
        private static Projectile ProjectileOrderShot(GameCore game, int weapon, Point direction, bool explosive = false)
        {
            var impact = new ImpactProfile(weapon, 0, weapon, 0, 0, true);
            var shot = (Projectile)typeof(GameCore).GetMethod("FriendlyShot", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(game, new object[] { game.Position, weapon, direction, 8f, 20f, explosive, null, impact });
            game.projectiles.Add(shot); return shot;
        }
        private static Projectile ProjectileOrderFlight(GameCore game, int weapon, Point sample, Point heading, bool explosive = false)
        {
            Projectile shot = ProjectileOrderShot(game, weapon, heading, explosive);
            shot.launchPending = false; shot.position = sample - heading * .08f; shot.velocity = heading * 8;
            return shot;
        }
        private static bool ProjectileOrderClearPath(GameCore game, Point from, Point to)
        {
            return (bool)typeof(GameCore).GetMethod("ClearAttackPath", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(game, new object[] { from, to });
        }
        private static void ProjectileOrderContact(GameCore game, Projectile shot)
        {
            MethodInfo contact = typeof(GameCore).GetMethod("ProjectileContact", BindingFlags.Instance | BindingFlags.NonPublic);
            // Both production revisions use the same sampled contact path. The
            // baseline has no explicit travel argument; no new API is needed
            // to demonstrate its list-order-dependent result.
            object[] args = contact.GetParameters().Length == 3
                ? new object[] { shot, false, 0f }
                : new object[] { shot, false, shot.velocity.Normalized, 0f };
            contact.Invoke(game, args);
        }
        private static void ProjectileOrderExactContact(Campaign data, Point first, Point second, bool firstBoss,
            bool secondBoss, int firstId, int secondId, bool expectedFirst, string reason)
        {
            foreach (bool reverse in new[] { false, true })
            {
                var game = ProjectileOrderGame(data); Point sample = new Point(14.5f, 14);
                Enemy a = ProjectileOrderTarget(game, firstId, sample + first, firstBoss);
                Enemy b = ProjectileOrderTarget(game, secondId, sample + second, secondBoss);
                Check(game.CanStand(a.Position) && game.CanStand(b.Position), "Circle-entry fixtures retain legal shipped-map feet");
                if (reverse) game.enemies.Reverse();
                Projectile shot = ProjectileOrderShot(game, 1, new Point(1, 0));
                shot.launchPending = false; shot.position = sample; ProjectileOrderContact(game, shot);
                Enemy selected = expectedFirst ? a : b, untouched = expectedFirst ? b : a;
                Check(ProjectileOrderNear(selected.maxHp - selected.hp, 20) && untouched.hp == untouched.maxHp
                    && shot.hits.Count == 1 && shot.hits[0] == selected.id, reason);
            }
        }

        public static void ProjectileOrderOrdinaryBowAndStaffChooseFrontAcrossLaunchAndFlight(Campaign data)
        {
            // Exact investigation fixture: retain the naturally spawned forest
            // actors and only arrange two legal, overlapping combat positions.
            foreach (bool offset in new[] { false, true }) foreach (bool reverse in new[] { false, true })
            {
                var authored = Fresh(data, 1); authored.SkipTutorial(); authored.Enter(1);
                authored.save.x = 12; authored.save.y = 14;
                Enemy rear = authored.enemies.Find(enemy => enemy.id == 1);
                Enemy front = authored.enemies.Find(enemy => enemy.id == 3);
                Check(rear != null && front != null, "The original forest still has both authored encounter identities");
                rear.x = offset ? 15.02f : 15.12f; rear.y = offset ? 14.35f : 14;
                front.x = 15.08f; front.y = 14;
                Check(authored.CanStand(authored.Position) && authored.CanStand(rear.Position) && authored.CanStand(front.Position),
                    "The original regression positions retain the shipped map's full-foot clearance");
                float frontBefore = front.hp, rearBefore = rear.hp;
                if (reverse) authored.enemies.Reverse();
                Projectile arrow = ProjectileOrderShot(authored, 1, new Point(1, 0)); arrow.velocity = new Point(12, 0);
                for (int frame = 0; frame < 30 && authored.projectiles.Contains(arrow); frame++) ProjectileOrderUpdate(authored, .02f);
                Check(ProjectileOrderNear(frontBefore - front.hp, 20) && rear.hp == rearBefore
                    && arrow.hits.Count == 1 && arrow.hits[0] == front.id,
                    "A forest arrow contacts the earlier front boundary in aligned and off-axis layouts regardless of storage order");
            }
            Point[] headings = { new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1),
                new Point(1, 1).Normalized, new Point(1, -1).Normalized,
                new Point(-1, 1).Normalized, new Point(-1, -1).Normalized };
            foreach (int weapon in new[] { 1, 2 }) foreach (Point heading in headings)
                foreach (bool launching in new[] { true, false }) foreach (bool reverse in new[] { false, true })
                {
                    var game = ProjectileOrderGame(data, weapon);
                    Point sample = launching ? game.Position : game.Position + heading * 2.5f;
                    Enemy rear = ProjectileOrderTarget(game, 99202, sample + heading * (launching ? .4f : .62f));
                    Enemy front = ProjectileOrderTarget(game, 99201, sample + heading * (launching ? .12f : .58f));
                    Check(game.CanStand(front.Position) && game.CanStand(rear.Position), "Both overlapping targets have legal shipped-forest feet");
                    if (reverse) game.enemies.Reverse();
                    Projectile shot = launching ? ProjectileOrderShot(game, weapon, heading, weapon == 2)
                        : ProjectileOrderFlight(game, weapon, sample, heading, weapon == 2);
                    ProjectileOrderUpdate(game, .01f);
                    Check(ProjectileOrderNear(front.maxHp - front.hp, 20)
                        && ProjectileOrderNear(rear.maxHp - rear.hp, weapon == 2 ? 12 : 0)
                        && shot.hits.Count == 1 && shot.hits[0] == front.id && !game.projectiles.Contains(shot),
                        "Ordinary bow/staff selects the front direct contact independently of enemy-list order; weapon "
                        + weapon + ", launch " + launching + ", heading " + heading.x + "," + heading.y
                        + ", front loss " + (front.maxHp - front.hp) + ", rear loss " + (rear.maxHp - rear.hp));
                    Check(game.effects.FindAll(effect => effect.kind == (weapon == 2 ? "magicImpact" : "projectileImpact")).Count == 1,
                        "The selected ordinary contact owns one impact and retires once");
                }
        }

        public static void ProjectileOrderCircleEntryBossRadiusAndStableIdTies(Campaign data)
        {
            // All candidates already overlap this exact existing sample. These
            // fixtures distinguish circle entry from center projection or range.
            ProjectileOrderExactContact(data, new Point(.58f, 0), new Point(.52f, .35f), false, false, 99210, 99211, true,
                "The earlier circle boundary wins over a center with smaller forward projection");
            ProjectileOrderExactContact(data, new Point(.55f, 0), new Point(.1f, .59f), false, false, 99212, 99213, false,
                "Signed circle entry wins even when the other enemy center is closer in Euclidean distance");
            ProjectileOrderExactContact(data, new Point(.63f, 0), new Point(1, 0), false, true, 99214, 99215, false,
                "The boss's existing larger contact radius can enter before a nearer ordinary center");
            ProjectileOrderExactContact(data, new Point(.4f, 0), new Point(.4f, 0), false, false, 99219, 99218, false,
                "An exactly equal entry uses the lower stable enemy id in either list order");
        }

        private static GameCore ProjectileOrderWallGame(Campaign data)
        {
            // Explicit corner/wall geometry uses an isolated map clone; primary
            // ordering regressions above always retain the authored forest.
            var maps = (MapData[])data.maps.Clone(); MapData authored = maps[1];
            var rows = new string[authored.rows.Length];
            for (int row = 0; row < rows.Length; row++) rows[row] = new string('.', authored.rows[0].Length);
            maps[1] = new MapData { id = authored.id, name = authored.name, en = authored.en,
                spawnX = authored.spawnX, spawnY = authored.spawnY, rows = rows,
                objects = new WorldObject[0], decor = new Decoration[0] };
            var isolated = new Campaign { title = data.title, titleEn = data.titleEn, version = data.version,
                maps = maps, items = data.items, bosses = data.bosses, lore = data.lore };
            var game = ProjectileOrderGame(isolated); char[] wall = game.Map.rows[14].ToCharArray();
            wall[15] = '#'; game.Map.rows[14] = new string(wall); return game;
        }
        public static void ProjectileOrderPiercingExplosiveAndWallsKeepOwnership(Campaign data)
        {
            foreach (bool reverse in new[] { false, true })
            {
                var piercing = ProjectileOrderGame(data); Point sample = new Point(14.5f, 14);
                Enemy rear = ProjectileOrderTarget(piercing, 99221, sample + new Point(.62f, 0));
                Enemy front = ProjectileOrderTarget(piercing, 99220, sample + new Point(.58f, 0));
                if (reverse) piercing.enemies.Reverse();
                Projectile arrow = ProjectileOrderFlight(piercing, 1, sample, new Point(1, 0)); arrow.piercing = true;
                ProjectileOrderUpdate(piercing, .01f);
                Check(ProjectileOrderNear(front.maxHp - front.hp, 20) && ProjectileOrderNear(rear.maxHp - rear.hp, 20)
                    && arrow.hits.Count == 2 && arrow.hits[0] == front.id && arrow.hits[1] == rear.id
                    && piercing.projectiles.Contains(arrow), "Piercing resolves each overlapping target once in entry order and continues");
                ProjectileOrderUpdate(piercing, .01f); ProjectileOrderUpdate(piercing, .01f);
                Check(ProjectileOrderNear(front.maxHp - front.hp, 20) && ProjectileOrderNear(rear.maxHp - rear.hp, 20)
                    && arrow.hits.Count == 2, "Piercing hit identities prevent later sample duplication");

                var explosive = ProjectileOrderGame(data, 2);
                Enemy splash = ProjectileOrderTarget(explosive, 99223, sample + new Point(.62f, 0));
                Enemy direct = ProjectileOrderTarget(explosive, 99222, sample + new Point(.58f, 0));
                if (reverse) explosive.enemies.Reverse();
                Projectile orb = ProjectileOrderFlight(explosive, 2, sample, new Point(1, 0), true);
                ProjectileOrderUpdate(explosive, .01f);
                Check(ProjectileOrderNear(direct.maxHp - direct.hp, 20) && ProjectileOrderNear(splash.maxHp - splash.hp, 12)
                    && orb.hits.Count == 1 && orb.hits[0] == direct.id && !explosive.projectiles.Contains(orb),
                    "Explosive ordering keeps exactly one full direct target and the existing 60-percent splash");
                Check(explosive.effects.FindAll(effect => effect.kind == "magicImpact" && ProjectileOrderNear(effect.radius, 1.8f)).Count == 1,
                    "An explosive contact retains the one original 1.8-radius impact owner");
            }
            var corner = ProjectileOrderWallGame(data); Point at = new Point(14.85f, 14.8f);
            Enemy hidden = ProjectileOrderTarget(corner, 99224, new Point(15.3f, 15.3f), true);
            Enemy visible = ProjectileOrderTarget(corner, 99225, new Point(14.85f, 15.3f));
            Check(corner.CanStand(hidden.Position) && corner.CanStand(visible.Position), "Both wall-corner actors have full-foot clearance");
            Projectile probe = ProjectileOrderShot(corner, 1, new Point(1, 0)); probe.launchPending = false; probe.position = at;
            Check(!ProjectileOrderClearPath(corner, at, hidden.Position) && ProjectileOrderClearPath(corner, at, visible.Position),
                "The explicit wall rejects the earlier boss boundary while preserving visible contact LOS");
            ProjectileOrderContact(corner, probe);
            Check(hidden.hp == hidden.maxHp && ProjectileOrderNear(visible.maxHp - visible.hp, 20),
                "Sorting cannot admit an otherwise earlier target through the original LOS filter");
            var blocked = ProjectileOrderWallGame(data); blocked.save.x = 14.25f; blocked.save.y = 14.5f;
            Enemy beyond = ProjectileOrderTarget(blocked, 99226, new Point(16.3f, 14.5f));
            Projectile stopped = ProjectileOrderShot(blocked, 1, new Point(1, 0)); ProjectileOrderUpdate(blocked, .2f);
            Check(blocked.CanStand(blocked.Position) && blocked.CanStand(beyond.Position) && beyond.hp == beyond.maxHp
                && stopped.hits.Count == 0 && !blocked.projectiles.Contains(stopped)
                && blocked.effects.FindAll(effect => effect.kind == "projectileImpact").Count == 1,
                "The original launch wall still consumes a shot once before a legal actor beyond it");
        }

        public static void ProjectileOrderCapturedActionsLifecycleAndLegacySavesStayUnchanged(Campaign data)
        {
            foreach (int weapon in new[] { 1, 2 })
            {
                var game = ProjectileOrderGame(data, weapon); Point heading = new Point(1, 0), target = new Point(19, 14);
                game.save.coins = 137; game.save.inventory[3] = 7; game.save.flags.Add("projectile_order_story_sentinel");
                game.SetAimTarget(target); float energy = game.save.energy; WeaponProfile profile = game.Profile(weapon, game.Style);
                game.Attack(heading); ActorAction action = game.playerAction;
                Check(action != null && !action.released && ProjectileOrderNear(energy - game.save.energy, profile.energy)
                    && ProjectileOrderNear(game.attackCooldown, profile.interval)
                    && ProjectileOrderNear(action.contact, profile.interval * (weapon == 1 ? .55f : .45f)),
                    "Projectile ordering preserves the ordinary public attack payment, cooldown and release time");
                game.SetAim(new Point(-1, 0)); game.SetAimTarget(new Point(4, 14));
                while (action.elapsed + .01001f < action.contact)
                { game.Tick(.01f, new Point()); Check(!action.released && game.projectiles.Count == 0, "Preparation still cannot emit early damage"); }
                for (int frame = 0; frame < 10 && !action.released; frame++) game.Tick(.01f, new Point());
                Check(action.released && game.projectiles.Count == 1 && action.target.HasValue
                    && (action.target.Value - target).Length < .0001f && (action.direction - heading).Length < .0001f,
                    "An accepted release retains its captured target and direction after live aim changes");
                Projectile shot = game.projectiles[0];
                Check(ReferenceEquals(shot.impact, action.impact) && ProjectileOrderNear(shot.damage, action.damage)
                    && (shot.launchDirection - heading).Length < .0001f && shot.velocity.x > 0 && !shot.launchPending,
                    "The released flight retains captured impact identity and consumes its launch exactly once");
                Point position = shot.position; float age = shot.age, clock = game.save.totalTime;
                game.mode = "pause"; game.Tick(.1f, new Point()); game.Close();
                Check((shot.position - position).Length == 0 && shot.age == age && game.save.totalTime == clock,
                    "Paused menus freeze the existing released projectile clock");
                Enemy late = ProjectileOrderTarget(game, 99230, game.Position + new Point(.12f, 0));
                ProjectileOrderUpdate(game, .01f);
                Check(late.hp == late.maxHp && shot.hits.Count == 0, "A consumed launch never rechecks the old feet corridor");
                game.PrepareSave(); SaveData snapshot = CloneSave(game.save);
                foreach (int version in new[] { 1, 2 })
                {
                    SaveData old = CloneSave(snapshot); old.version = version; var loaded = new GameCore(data, old); loaded.Start(); loaded.Close();
                    Enemy restored = loaded.enemies.Find(enemy => enemy.id == late.id);
                    Check(loaded.save.coins == 137 && loaded.save.inventory[3] == 7 && loaded.save.seed == snapshot.seed
                        && loaded.save.health == snapshot.health && loaded.save.energy == snapshot.energy
                        && loaded.Has("projectile_order_story_sentinel") && restored != null && restored.hp == late.hp,
                        "Legacy save version " + version + " retains actual world health, payment, economy, story and RNG");
                    Check(loaded.projectiles.Count == 0 && loaded.effects.Count == 0 && loaded.playerAction == null,
                        "Loading never persists or resurrects a pending projectile contact or action");
                }
                shot.life = .001f; ProjectileOrderUpdate(game, .01f);
                Check(!game.projectiles.Contains(shot)
                    && game.effects.FindAll(effect => effect.kind == "projectileFade").Count == 1,
                    "An unconsumed flight still expires once through the existing fade lifecycle");
                Projectile traveling = ProjectileOrderShot(game, weapon, heading); game.Enter(0);
                Check(game.projectiles.Count == 0 && game.effects.Count == 0 && !game.projectiles.Contains(traveling)
                    && game.save.coins == 137 && game.save.inventory[3] == 7,
                    "Actual map travel discards old flights while retaining accepted economy");
            }
        }
    }
}
