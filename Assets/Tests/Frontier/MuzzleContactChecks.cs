using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        // Clone the authored map container so wall fixtures never change another group.
        private static GameCore MuzzleGame(Campaign data, int weapon = 1)
        {
            MapData authored = data.maps[0]; var maps = (MapData[])data.maps.Clone();
            string[] rows = new string[authored.rows.Length];
            for (int row = 0; row < rows.Length; row++) rows[row] = new string('.', authored.rows[0].Length);
            maps[0] = new MapData { id = authored.id, name = authored.name, en = authored.en,
                subtitle = authored.subtitle, suben = authored.suben, color = authored.color,
                spawnX = authored.spawnX, spawnY = authored.spawnY, rows = rows,
                objects = new WorldObject[0], decor = new Decoration[0] };
            var isolated = new Campaign { title = data.title, titleEn = data.titleEn, version = data.version,
                maps = maps, items = data.items, bosses = data.bosses, lore = data.lore };
            var game = Fresh(isolated, weapon); game.SkipTutorial(); game.Enter(0);
            game.enemies.Clear(); game.projectiles.Clear(); game.effects.Clear(); game.hazards.Clear();
            game.save.x = 14; game.save.y = 14.5f; game.save.companion = -1;
            game.save.seed = 1; game.save.reducedMotion = true; game.invulnerable = 0;
            Check(game.CanStand(game.Position), "The isolated authored-map clone has legal full-foot clearance");
            return game;
        }
        private static Enemy MuzzleTarget(GameCore game, int id, Point position)
        {
            var enemy = new Enemy(id, 0, position.x, position.y, 2000) { timer = 99, stun = 99 };
            game.enemies.Add(enemy); return enemy;
        }
        private static bool MuzzleNear(float a, float b) { return Math.Abs(a - b) < .0003f; }
        private static void MuzzleUpdate(GameCore game, float dt)
        {
            typeof(GameCore).GetMethod("UpdateProjectiles", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(game, new object[] { dt });
        }
        private static Projectile MuzzleFriendlyShot(GameCore game, int weapon, Point heading, bool explosive = false)
        {
            // Use the existing production constructor; no future launch-state API is needed.
            var impact = new ImpactProfile(weapon, 0, weapon, 0, 0, true);
            var shot = (Projectile)typeof(GameCore).GetMethod("FriendlyShot", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(game, new object[] { game.Position, weapon, heading, 12f, 20f, explosive, null, impact });
            game.projectiles.Add(shot); return shot;
        }
        private static ActorAction MuzzleAttackToRelease(GameCore game, Point heading, Enemy target)
        {
            float hp = target.hp, energy = game.save.energy; var profile = game.Profile(game.save.weapon, game.Style);
            game.Attack(heading); ActorAction action = game.playerAction;
            Check(action != null && !action.released && MuzzleNear(energy - game.save.energy, profile.energy)
                && MuzzleNear(game.attackCooldown, profile.interval), "Public Attack retains its exact paid preparation and cooldown");
            Check(MuzzleNear(action.contact, profile.interval * (action.weapon == 1 ? .55f : .45f)),
                "The muzzle correction does not move the authored release instant");
            while (action.elapsed + .01001f < action.contact)
            {
                game.Tick(.01f, new Point());
                Check(!action.released && target.hp == hp && game.projectiles.Count == 0,
                    "A point-blank target cannot take damage during draw or cast preparation");
            }
            for (int frame = 0; frame < 10 && !action.released; frame++) game.Tick(.01f, new Point());
            Check(action.released, "The actual action clock reaches its existing release"); return action;
        }
        private static void MuzzleWall(GameCore game, int x, int y)
        {
            char[] row = game.Map.rows[y].ToCharArray(); row[x] = '#'; game.Map.rows[y] = new string(row);
        }

        public static void MuzzleContactEightHeadingsBowAndStaffRespectRealRelease(Campaign data)
        {
            Point[] headings = { new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1),
                new Point(1, 1), new Point(1, -1), new Point(-1, 1), new Point(-1, -1) };
            foreach (int weapon in new[] { 1, 2 }) foreach (Point raw in headings)
            {
                var game = MuzzleGame(data, weapon); Point heading = raw.Normalized;
                Enemy target = MuzzleTarget(game, 99101, game.Position + heading * .12f);
                ActorAction action = MuzzleAttackToRelease(game, heading, target);
                Check(MuzzleNear(target.maxHp - target.hp, action.damage),
                    "The real released weapon must hit a living forward target between feet and muzzle exactly once; weapon "
                    + weapon + ", heading " + raw.x + "," + raw.y + ", loss " + (target.maxHp - target.hp));
                Check(game.projectiles.Count == 0 && game.effects.FindAll(effect => effect.kind == (weapon == 2 ? "magicImpact" : "projectileImpact")).Count == 1,
                    "A nonpiercing point-blank release retires once through the normal impact path");
                float hp = target.hp; game.Tick(.01f, new Point());
                Check(target.hp == hp, "The consumed point-blank release cannot deal another contact next frame");
            }
        }

        public static void MuzzleContactForwardSweepRejectsBehindAndOutsideCorridor(Campaign data)
        {
            foreach (int weapon in new[] { 1, 2 }) foreach (int mirror in new[] { -1, 1 })
            {
                var game = MuzzleGame(data, weapon); Point heading = new Point(mirror, 0);
                Enemy behind = MuzzleTarget(game, 99110, game.Position - heading * .12f);
                Enemy side = MuzzleTarget(game, 99111, game.Position + new Point(mirror * .4f, 1.2f));
                Projectile shot = MuzzleFriendlyShot(game, weapon, heading); MuzzleUpdate(game, .01f);
                Check(behind.hp == behind.maxHp && side.hp == side.maxHp && shot.hits.Count == 0
                    && game.projectiles.Contains(shot), "The added launch sweep cannot hit behind the prepared heading or outside its muzzle corridor");
            }
        }

        public static void MuzzleContactWallOrderKeepsEarlierActorAndBlocksLaterActor(Campaign data)
        {
            // The shipped forest has legal feet beside the west wall here.
            // WeaponShot already permits a staff to emerge away from that wall.
            var outgoing = Quiet(data, 2); outgoing.save.x = 7.25f; outgoing.save.y = 5.5f; outgoing.save.seed = 1;
            Enemy distant = MuzzleTarget(outgoing, 99119, new Point(10, 5.5f));
            Check(outgoing.CanStand(outgoing.Position) && outgoing.CanStand(distant.Position),
                "The outgoing staff regression uses actual legal shipped-forest foot positions");
            ActorAction cast = MuzzleAttackToRelease(outgoing, new Point(1, 0), distant);
            Check(distant.hp == distant.maxHp && outgoing.projectiles.Count == 1,
                "An outgoing staff remains in flight and cannot be killed by radius overlap behind its legal launch feet");
            MuzzleUpdate(outgoing, .2f);
            Check(MuzzleNear(distant.maxHp - distant.hp, cast.damage) && outgoing.projectiles.Count == 0,
                "The mature outgoing forest staff still reaches its later target through ordinary flight");
            foreach (int weapon in new[] { 1, 2 }) foreach (int mirror in new[] { -1, 1 })
            {
                var early = MuzzleGame(data, weapon); early.save.x = mirror > 0 ? 14.25f : 15.75f;
                MuzzleWall(early, mirror > 0 ? 15 : 14, 14); Point heading = new Point(mirror, 0);
                Enemy first = MuzzleTarget(early, 99120, early.Position + heading * .2f);
                Check(early.CanStand(early.Position) && early.CanStand(first.Position), "The actor-before-wall fixture has two legal foot positions");
                Projectile contact = MuzzleFriendlyShot(early, weapon, heading); MuzzleUpdate(early, .01f);
                Check(MuzzleNear(first.maxHp - first.hp, contact.damage) && !early.projectiles.Contains(contact),
                    "An earlier forward actor contact wins before the wall later along the actual muzzle corridor");
                var wallFirst = MuzzleGame(data, weapon); wallFirst.save.x = mirror > 0 ? 14.25f : 15.75f;
                MuzzleWall(wallFirst, mirror > 0 ? 15 : 14, 14);
                Enemy hidden = MuzzleTarget(wallFirst, 99121, wallFirst.Position + heading * 2.1f);
                Projectile blocked = MuzzleFriendlyShot(wallFirst, weapon, heading); MuzzleUpdate(wallFirst, .2f);
                Check(hidden.hp == hidden.maxHp && !wallFirst.projectiles.Contains(blocked)
                    && wallFirst.effects.FindAll(effect => effect.kind == "projectileImpact").Count == 1,
                    "A wall before an actor keeps radius-aware obstruction and the single mature impact");
            }
        }

        public static void MuzzleContactPiercingAndExplosiveKeepDamageAndSingleOwnership(Campaign data)
        {
            var piercing = MuzzleGame(data); piercing.save.weaponStyles[1] = 1;
            Enemy near = MuzzleTarget(piercing, 99130, piercing.Position + new Point(.12f, 0));
            Enemy far = MuzzleTarget(piercing, 99131, piercing.Position + new Point(2.4f, 0));
            ActorAction action = MuzzleAttackToRelease(piercing, new Point(1, 0), near);
            Check(piercing.projectiles.Count == 1 && piercing.projectiles[0].piercing,
                "The actual piercing bow profile keeps its released projectile");
            Projectile arrow = piercing.projectiles[0]; float nearHp = near.hp;
            Check(MuzzleNear(near.maxHp - near.hp, action.damage) && arrow.hits.Contains(near.id),
                "A piercing launch records its first near contact with the captured damage");
            MuzzleUpdate(piercing, .2f);
            Check(near.hp == nearHp && MuzzleNear(far.maxHp - far.hp, action.damage)
                && arrow.hits.Count == 2 && arrow.hits.Contains(far.id),
                "Launch plus ordinary flight hits each actor once while piercing onward");
            MuzzleUpdate(piercing, .1f);
            Check(near.hp == nearHp && MuzzleNear(far.maxHp - far.hp, action.damage), "Recorded piercing contacts never repeat on another update");

            var explosive = MuzzleGame(data, 2);
            Enemy direct = MuzzleTarget(explosive, 99132, explosive.Position + new Point(.12f, 0));
            Enemy splash = MuzzleTarget(explosive, 99133, explosive.Position + new Point(.3f, 1.1f));
            Projectile orb = MuzzleFriendlyShot(explosive, 2, new Point(1, 0), true); MuzzleUpdate(explosive, .01f);
            Check(MuzzleNear(direct.maxHp - direct.hp, 20) && MuzzleNear(splash.maxHp - splash.hp, 12)
                && orb.hits.Count == 1 && orb.hits.Contains(direct.id) && !explosive.projectiles.Contains(orb),
                "A near staff collision keeps one direct hit and the original 60-percent splash without direct-target duplication");
            Check(explosive.effects.FindAll(effect => effect.kind == "magicImpact" && MuzzleNear(effect.radius, 1.8f)).Count == 1,
                "The accepted explosive contact owns exactly one original 1.8-radius magic impact");
            float directHp = direct.hp, splashHp = splash.hp; MuzzleUpdate(explosive, .2f);
            Check(direct.hp == directHp && splash.hp == splashHp, "An already retired explosion cannot repeat direct or splash damage");
        }

        public static void MuzzleContactCompletedLaunchAndLegacyFlightsNeverSweepAgain(Campaign data)
        {
            foreach (int weapon in new[] { 1, 2 })
            {
                var game = MuzzleGame(data, weapon); Projectile shot = MuzzleFriendlyShot(game, weapon, new Point(1, 0));
                MuzzleUpdate(game, .01f); Check(game.projectiles.Contains(shot), "An unobstructed released shot first completes its normal launch");
                Enemy late = MuzzleTarget(game, 99140, game.Position + new Point(.12f, 0));
                game.save.x -= 3; MuzzleUpdate(game, .01f);
                Check(late.hp == late.maxHp && shot.hits.Count == 0, "A later target in the old launch corridor cannot be hit after the muzzle sweep was consumed");
            }
            var legacy = MuzzleGame(data); Enemy byFeet = MuzzleTarget(legacy, 99141, legacy.Position + new Point(.12f, 0));
            var oldShot = new Projectile { position = legacy.Position + new Point(2, 0), velocity = new Point(12, 0),
                friendly = true, visual = "arrow", damage = 20, life = 2 };
            legacy.projectiles.Add(oldShot); MuzzleUpdate(legacy, .01f);
            Check(byFeet.hp == byFeet.maxHp && oldShot.hits.Count == 0 && legacy.projectiles.Contains(oldShot),
                "Existing already-flying projectiles have no invented feet-to-muzzle sweep");
        }

        public static void MuzzleContactCapturedNearAimKeepsPreparedArmorApproach(Campaign data)
        {
            foreach (int weapon in new[] { 1, 2 }) foreach (int mirror in new[] { -1, 1 })
            {
                var game = MuzzleGame(data, weapon); Point heading = new Point(mirror, 0);
                Enemy armored = MuzzleTarget(game, 99145, game.Position + heading * .12f);
                armored.kind = 2; armored.stun = 0; armored.facing = heading * -1;
                game.SetAimTarget(armored.Position); game.Attack(heading); ActorAction action = game.playerAction;
                Check(action != null && action.target.HasValue && (action.target.Value - armored.Position).Length < .0001f,
                    "Public Attack captures the original near target before release");
                game.SetAim(heading * -1); game.SetAimTarget(game.Position - heading * 5);
                for (int frame = 0; frame < 100 && !action.released; frame++) game.Tick(.01f, new Point());
                Check(action.released && MuzzleNear(armored.maxHp - armored.hp, action.damage * .4f)
                    && (armored.recoil - heading).Length < .0003f,
                    "Launch contact keeps the captured forward armor approach and recoil even when muzzle-to-target retargeting points backward");
                Check(game.projectiles.Count == 0 && (action.direction - heading).Length < .0001f,
                    "Later aim changes cannot redirect the released point-blank action");
            }
        }

        public static void MuzzleContactBowSkillsKeepPaidVolleyAndPiercingRelease(Campaign data)
        {
            foreach (bool alternate in new[] { false, true })
            {
                var game = MuzzleGame(data); game.save.technique = alternate ? 1 : 0;
                Enemy target = MuzzleTarget(game, 99146, game.Position + new Point(.12f, 0));
                float energy = game.save.energy; game.Skill(new Point(1, 0)); ActorAction action = game.playerAction;
                Check(action != null && action.kind == "skill" && !action.released && MuzzleNear(energy - game.save.energy, 40)
                    && MuzzleNear(game.skillCooldown, 3.5f) && MuzzleNear(action.contact, .28f),
                    "Both public bow Q techniques retain the original 40 energy, cooldown and release instant");
                while (action.elapsed + .01001f < action.contact)
                {
                    game.Tick(.01f, new Point());
                    Check(!action.released && target.hp == target.maxHp && game.projectiles.Count == 0,
                        "Neither bow Q technique damages a point-blank actor before its captured release");
                }
                for (int frame = 0; frame < 10 && !action.released; frame++) game.Tick(.01f, new Point());
                float expected = action.damage * (alternate ? 3.4f : 1.25f * 5);
                Check(action.released && MuzzleNear(target.maxHp - target.hp, expected),
                    "The released primary five-arrow volley or alternate piercing arrow keeps its original exact point-blank damage");
                Check(alternate ? game.projectiles.Count == 1 && game.projectiles[0].piercing && game.projectiles[0].technique
                    && game.projectiles[0].hits.Count == 1 : game.projectiles.Count == 0,
                    "Primary Q consumes its five contacts while alternate Q retains one piercing technique projectile");
                float hp = target.hp; MuzzleUpdate(game, .01f);
                Check(target.hp == hp, "A bow Q launch never duplicates a recorded near contact next update");
            }
        }

        public static void MuzzleContactHostileArcherUsesSamePreparedReleasePath(Campaign data)
        {
            foreach (int mirror in new[] { -1, 1 })
            {
                var game = MuzzleGame(data); Point heading = new Point(mirror, 0), foot = game.Position - heading * .12f;
                var archer = new Enemy(99150, 1, foot.x, foot.y, 100) { state = 1, timer = .03f,
                    windupDuration = .65f, target = game.Position, facing = heading };
                game.enemies.Add(archer); float hp = game.save.health;
                game.Tick(.01f, new Point());
                Check(game.save.health == hp && game.projectiles.Count == 0 && archer.state == 1,
                    "A genuinely preparing hostile archer cannot hurt the player before its captured release");
                for (int frame = 0; frame < 10 && archer.state == 1; frame++) game.Tick(.01f, new Point());
                Check(archer.state == 0 && MuzzleNear(hp - game.save.health, 10) && game.projectiles.Count == 0,
                    "A hostile arrow between archer feet and muzzle uses the same one-time release collision and original damage");
                Check(game.effects.FindAll(effect => effect.kind == "projectileImpact" && effect.variant == 1).Count == 1,
                    "The shared hostile launch keeps its single hostile impact owner");
            }
        }
    }
}
