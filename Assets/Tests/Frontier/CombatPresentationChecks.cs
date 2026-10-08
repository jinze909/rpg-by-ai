using System;
namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void EffectOriginsAndSources(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++)
            {
                var g = Fresh(data, profession); g.Enter(1); g.enemies.Clear(); g.save.reducedMotion = true;
                g.save.x = 12; g.save.y = 14; g.Skill();
                Check(g.effects.Count == 0 && g.projectiles.Count == 0, "Preparing Q cannot create an early effect or projectile");
                Advance(g, .3f);
                if (profession == 1)
                    Check(g.projectiles.Count == 5 && g.effects.Exists(e => e.kind == "release" && (e.position-GameCore.WeaponOrigin(g.Position,1,g.aim)).Length < .01f) && !g.effects.Exists(e => e.kind == "burst"), "Archer volley releases from its bow, with no unrelated ground explosion");
                else
                {
                    Effect burst = g.effects.Find(e => e.kind == "burst");
                    Check(burst != null && burst.variant == profession && (burst.position-g.Position).Length < .01f && Math.Abs(burst.radius-(profession == 0 ? 3.1f : 4.5f)) < .001f, "AoE carries its actual class, origin and unchanged damage radius to the renderer");
                }
            }
            var guard = Fresh(data); guard.Enter(1); guard.enemies.Clear(); guard.save.technique = 1; guard.Skill(); Advance(guard,.3f);
            Check(guard.effects.Exists(e => e.kind == "burst" && e.variant == 0 && e.radius == 3.8f), "Alternate guard skill retains its own AoE size");
            var marksman = Fresh(data,1); marksman.Enter(1); marksman.enemies.Clear(); marksman.save.technique = 1; marksman.Skill(); Advance(marksman,.3f);
            Check(marksman.projectiles.Count == 1 && marksman.effects.Exists(e => e.kind == "release") && !marksman.effects.Exists(e => e.kind == "burst"), "Alternate shot has a projectile release rather than a class-independent explosion");

            var killer = Fresh(data,2); killer.Enter(1); killer.enemies.Clear(); killer.save.reducedMotion = true;
            killer.save.x = 12; killer.save.y = 14; var foe = new Enemy(931,0,14,14,1) {timer=9999}; killer.enemies.Add(foe);
            killer.projectiles.Add(new Projectile {position=foe.Position,friendly=true,visual="arrow",damage=50}); killer.Tick(.01f,new Point());
            Check(killer.effects.Exists(e => e.kind == "defeat" && (e.position-foe.Position).Length < .01f) && !killer.effects.Exists(e => e.kind == "burst"), "Enemy death remains at the enemy and cannot impersonate the player's vocation skill");
            Check(killer.fallen.Count == 1 && killer.effects.FindAll(e => e.kind == "loot").Count == 1, "Death art still awards a corpse and loot once");

            for (int companion = 0; companion < 3; companion++)
            {
                var g = Fresh(data,2); g.Enter(1); g.enemies.Clear(); g.save.reducedMotion = true; g.save.x = 12; g.save.y = 14;
                g.save.flags.Add("companion_" + companion); g.SelectCompanion(companion); g.save.health = 20; g.CommandCompanion(); Advance(g,.22f);
                Check(!g.effects.Exists(e => e.kind == "burst"), "Companion command cannot borrow the mage player's attack explosion");
                if (companion == 0) Check(g.save.health == 38 && g.effects.Exists(e => e.kind == "heal" && (e.position-g.Position).Length < .01f), "Mira heals with a healing effect at the recipient");
                if (companion == 1) Check(g.projectiles.Count == 3 && g.effects.Exists(e => e.kind == "release" && (e.position-GameCore.WeaponOrigin(g.companionPosition,1,g.companionAim)).Length < .01f), "Ivo's volley releases from Ivo's bow");
                if (companion == 2) Check(g.effects.Exists(e => e.kind == "frost" && (e.position-g.Position).Length < .01f), "Sen's field retains its gameplay center");
            }
            var magic = Fresh(data,2); magic.Enter(1); magic.enemies.Clear(); magic.save.reducedMotion = true; magic.save.x = 12; magic.save.y = 14;
            Point collision = new Point(14,14); magic.enemies.Add(new Enemy(932,0,14,14,999) {stun=20,timer=9999});
            magic.projectiles.Add(new Projectile {position=collision,friendly=true,explosive=true,visual="starfire",damage=20}); magic.Tick(.01f,new Point());
            Check(magic.projectiles.Count == 0 && magic.effects.FindAll(e => e.kind == "magicImpact").Count == 1 && !magic.effects.Exists(e => e.kind == "burst"), "Starfire collision emits exactly one impact, without a duplicate class explosion");
            Effect impact = magic.effects.Find(e => e.kind == "magicImpact"); Check((impact.position-collision).Length < .01f && impact.radius == 1.8f, "Explosive impact carries the actual collision and splash footprint");
            var dodge = Fresh(data); dodge.Dash(new Point(0,-1)); Check(dodge.effects.Exists(e => e.kind == "dash" && e.direction.y == -1 && e.direction.x == 0), "Dodge trail remembers the actual dodge direction");
        }
        public static void InnRoomAndCollisionPersistence(Campaign data)
        {
            var g = Fresh(data); int stage = g.save.stage;
            Check(data.maps.Length == 10 && data.maps[9].id == 9, "Inn is appended without renumbering existing regions");
            WorldObject door = Object(g, "portal9"); g.save.x = door.x; g.save.y = door.y; g.Interact();
            Check(g.save.room == 9 && g.enemies.Count == 0 && !g.Blocked(g.save.x, g.save.y), "The actual town door enters a safe, navigable interior");
            Check(g.Blocked(15.5f, 8.5f) && !g.Blocked(15.5f, 10.5f), "Inn furniture blocks its footprint while the aisle remains open");
            g.PrepareSave(); var loaded = new GameCore(data, CloneSave(g.save));
            Check(loaded.save.room == 9 && loaded.save.stage == stage, "Interior saves restore without advancing or resetting quests");
            g.save.health = 10; Action(g, "rest", "rest");
            Check(g.save.health == g.save.maxHealth, "Interior rest uses the existing recovery transaction");
            g.Close(); g.Enter(9); Action(g, "history16", "close"); Check(g.Has("history_16"), "Inn ledger is recorded in the existing codex");
            g.Close(); WorldObject exit = Object(g, "portal0"); g.save.x = exit.x; g.save.y = exit.y; g.Interact();
            Check(g.save.room == 0 && g.save.stage == stage, "The inn exit returns to the same town journey");
        }
        public static void ContactFramesAndCancellation(Campaign data)
        {
            for (int weapon = 0; weapon < 3; weapon++)
            {
                var g = Fresh(data, weapon); g.Enter(1); g.enemies.Clear(); g.save.reducedMotion = true;
                g.save.x = 12; g.save.y = 14; var foe = new Enemy(41, 0, weapon == 0 ? 13 : 17, 14, 999) { timer = 9999 }; g.enemies.Add(foe);
                g.Attack(new Point(1, 0)); var action = g.playerAction;
                Check(action != null && g.PlayerPose == 4 && foe.hp == 999 && g.projectiles.Count == 0, "Input starts wind-up without early damage or missiles");
                Advance(g, action.contact - .01f); Check(!action.released && foe.hp == 999 && g.projectiles.Count == 0, "No contact before the actual release boundary");
                g.mode = "pause"; Advance(g, 1); Check(!action.released, "Pausing freezes the action and its damage"); g.Close();
                g.Tick(.02f, new Point()); Check(action.released && g.PlayerPose == 5, "Contact and release pose share the same clock");
                if (weapon == 0) { float hp = foe.hp; Check(hp < 999, "Sword contacts at the strike frame"); Advance(g, .1f); Check(foe.hp == hp, "A swing cannot repeatedly damage the same enemy"); }
                else Check(g.projectiles.Count > 0, "Ranged weapons spawn only at their release frame");
                g.Enter(0); g.Close(); g.attackCooldown = 0; g.Attack(new Point(1, 0)); g.save.flags.Add("weapon_1"); g.Equip(1); Advance(g, .8f);
                Check(g.playerAction == null && g.projectiles.Count == 0, "Equipment change cancels the stored attack");
                g.Attack(new Point(1, 0)); g.Enter(1); Advance(g, .8f); Check(g.projectiles.Count == 0, "Travel does not release an old attack in the new region");
            }
            var caster = Fresh(data, 2); caster.Enter(1); caster.enemies.Clear(); caster.save.reducedMotion = true;
            caster.save.x = 12; caster.save.y = 14; var target = new Enemy(50, 0, 13, 14, 999) { timer = 9999 }; caster.enemies.Add(target);
            caster.Skill(); Check(target.hp == 999 && caster.PlayerPose == 6, "Q first channels visibly"); Advance(caster, .3f);
            Check(target.hp < 999 && caster.PlayerPose == 7, "Q damage follows its release pose");
            var struck = Fresh(data); struck.Enter(1); struck.enemies.Clear(); struck.save.x = 12; struck.save.y = 14; struck.save.reducedMotion = true;
            var foeHit = new Enemy(51, 0, 13, 14, 999) { timer = 9999 }; struck.enemies.Add(foeHit);
            struck.Attack(new Point(1, 0)); struck.Tick(.04f, new Point());
            struck.invulnerable = 0; struck.projectiles.Add(new Projectile { position = struck.Position, damage = 5, source = "shot" }); struck.Tick(.01f, new Point());
            Advance(struck, .1f); Check(foeHit.hp == 999 && struck.PlayerPose == 10 && !struck.playerAction.released, "Recoil cannot conceal a pending damage event");
            Advance(struck, .25f); Check(foeHit.hp < 999 && struck.playerAction.released && struck.PlayerPose == 5, "After recoil the visible swing resumes before contact");
        }
        public static void ProjectilesAndEnemyContact(Campaign data)
        {
            var g = Fresh(data, 1); g.Enter(1); g.enemies.Clear(); g.save.reducedMotion = true;
            g.save.x = 12; g.save.y = 14; var archer = new Enemy(60, 1, 17, 14, 999) { timer = 9999 }; g.enemies.Add(archer);
            g.Attack(new Point(1, 0)); Advance(g, .26f); Check(g.projectiles.Count == 1 && archer.hp == 999, "An arrow has a visible flight before a distant hit");
            float first = g.projectiles[0].position.x; Advance(g, .12f); Check(g.projectiles[0].position.x > first && archer.hp == 999, "The arrow travels instead of applying instant ranged damage");
            for (int i = 0; i < 50 && archer.hp == 999; i++) g.Tick(.02f, new Point()); Check(archer.hp < 999 && g.effects.Exists(e => e.kind == "projectileImpact"), "Arrow collision gives damage and a disappearance effect");
            g.projectiles.Clear(); g.invulnerable = 0; var heavy = new Enemy(61, 2, 13, 14, 999) { state = 1, timer = .4f, windupDuration = .4f, target = g.Position }; g.enemies.Clear(); g.enemies.Add(heavy);
            float health = g.save.health; Advance(g, .38f); Check(g.save.health == health && heavy.state == 1, "Readable enemy preparation cannot damage early");
            g.Tick(.03f, new Point()); Check(g.save.health < health && heavy.releaseTime > 0 && g.PlayerPose == 10, "Enemy contact triggers the strike and player recoil together");
        }
        public static void DeathPresentationAndResume(Campaign data)
        {
            var g = Fresh(data); g.Enter(1); g.enemies.Clear(); g.save.reducedMotion = true; g.save.health = 1; g.invulnerable = 0;
            g.projectiles.Add(new Projectile { position = g.Position, damage = 999, source = "shot" }); g.Tick(.02f, new Point());
            Check(g.Dying && g.save.room == 1 && g.PlayerPose == 11, "The player falls before being returned home");
            g.PrepareSave(); var resumed = new GameCore(data, CloneSave(g.save)); Check(resumed.save.room == 0 && resumed.save.health == resumed.save.maxHealth && resumed.save.deaths == 1, "Saving during death safely resumes at the inn without charging death twice");
            Advance(g, 1); Check(!g.Dying && g.save.room == 0 && g.save.deaths == 1, "Death completion restores control exactly once");
            g.Enter(1); g.enemies.Clear(); g.save.x = 12; g.save.y = 14; var foe = new Enemy(71, 0, 13, 14, 1) { timer = 9999 }; g.enemies.Add(foe);
            int kills = g.save.kills[1]; g.Attack(new Point(1, 0)); Advance(g, .2f);
            Check(g.enemies.Count == 0 && g.fallen.Count == 1 && g.save.kills[1] == kills + 1, "Death removes combat participation but preserves a visible corpse");
            g.PrepareSave(); var loaded = new GameCore(data, CloneSave(g.save)); Check(loaded.enemies.Count == 0, "A visual corpse cannot respawn as a living loot source");
            Advance(g, 1); Check(g.fallen.Count == 0 && g.save.kills[1] == kills + 1, "The corpse fades without awarding twice");
        }
        public static void PhysicalAttacksAndDodgeRespectWalls(Campaign data)
        {
            var g = Fresh(data); g.Enter(1); g.enemies.Clear(); g.save.reducedMotion = true;
            g.save.x = 9.5f; g.save.y = 8.8f;
            var foe = new Enemy(90, 0, 9.5f, 10.2f, 999) { timer = 9999 }; g.enemies.Add(foe);
            Check(!g.Blocked(g.save.x, g.save.y) && !g.Blocked(foe.x, foe.y) && g.Blocked(9.5f, 9.5f), "A real forest wall separates two otherwise valid positions");
            g.Attack(new Point(0, 1)); Advance(g, .25f);
            Check(foe.hp == 999, "A visible sword strike cannot damage through an intervening wall");
            foe.state = 1; foe.timer = .04f; foe.target = g.Position; foe.facing = new Point(0, -1);
            float health = g.save.health; Advance(g, .06f);
            Check(g.save.health == health && foe.releaseTime > 0, "A creature can finish its lunge without biting through stone");

            g.Enter(0); g.enemies.Clear(); g.save.x = 7; g.save.y = 6.3f;
            g.Dash(new Point(0, -1));
            for (int i = 0; i < 5; i++)
            {
                g.Tick(.05f, new Point());
                Check(g.save.y >= 5.97f && !g.Blocked(g.save.x, g.save.y), "A fast dodge stops outside the unchanged house footprint");
            }
            g.Enter(1); g.enemies.Clear(); g.save.x = 12; g.save.y = 14;
            var open = new Enemy(91, 0, 12, 15.3f, 999) { timer = 9999 }; g.enemies.Add(open);
            g.Attack(new Point(0, 1)); Advance(g, .25f);
            Check(open.hp < 999, "The obstruction fix preserves normal open-lane melee contacts");
        }
        public static void CompanionPreparationAndSafeFollowing(Campaign data)
        {
            for (int id = 0; id < 3; id++)
            {
                var g = Fresh(data); g.save.flags.Add("companion_" + id); g.SelectCompanion(id);
                Check((g.companionPosition - g.Position).Length > 1.4f && g.CanStand(g.companionPosition), "A companion starts beside the hero on a valid footprint");
                g.save.health = 20; g.aim = new Point(0, 1); g.CommandCompanion();
                var action = g.companionAction; Check(action != null && !action.released && g.projectiles.Count == 0 && g.save.health == 20, "Companion command prepares before applying its effect");
                g.mode = "pause"; Advance(g, 1); Check(action.elapsed == 0, "Companion casting pauses with the same world clock"); g.Close();
                Advance(g, .22f); Check(action.released, "Companion pose and command release use the same boundary");
                if (id == 0) Check(g.save.health == 38, "Mira retains the original 18 HP command strength");
                if (id == 1) Check(g.projectiles.Count == 3 && g.projectiles.TrueForAll(p => p.velocity.y > 0 && p.piercing), "Ivo's actual volley flies in the locked south direction");
                if (id == 2) Check(g.effects.Exists(e => e.kind == "frost"), "Sen's command starts its frost field on release");
                g.Enter(1); Check(g.companionAction == null && g.CanStand(g.companionPosition), "Travel cancels a previous companion action and restores safe following");
            }
        }
    }
}
