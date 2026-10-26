using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore DodgeDistanceGame(Campaign data, int profession = 0)
        {
            var game = Fresh(data, profession); game.SkipTutorial(); game.save.reducedMotion = true;
            game.save.companion = -1; game.enemies.Clear(); game.invulnerable = 0;
            game.save.x = 12; game.save.y = 14;
            Check(game.CanStand(game.Position), "Dodge distance fixture uses existing legal town ground");
            return game;
        }
        private static float DodgeDistanceFinish(GameCore game, float[] schedule, Point? movement = null)
        {
            float elapsed = 0; int frame = 0;
            while (game.dashTime > 0 && frame < 256)
            {
                float dt = schedule[frame % schedule.Length];
                game.Tick(dt, movement ?? new Point(), new Point(-1, 0));
                elapsed += Math.Min(.05f, dt); frame++;
                Check(game.CanStand(game.Position), "Every dodge sample retains its complete collision footprint");
            }
            Check(frame < 256 && game.dashTime == 0, "A single accepted dodge naturally finishes without another input");
            return elapsed;
        }
        private static void DodgeDistanceEndpoint(GameCore game, Point start, Point heading, string context)
        {
            Point expected = start + heading.Normalized * 2.7f;
            Check((game.Position - expected).Length < .0002f,
                "The original 0.18 seconds at speed 15 owns exactly 2.7 tiles: " + context
                + "; actual=" + (game.Position - start).Length);
        }

        public static void DodgeDistanceUsesAuthoredTravelAcrossRatesAndCapturedHeadings(Campaign data)
        {
            float[][] schedules = {
                new[] { 1f / 240 }, new[] { 1f / 120 }, new[] { 1f / 60 },
                new[] { 1f / 30 }, new[] { .02f }, new[] { .05f },
                new[] { .007f, .05f, .011f, 1f / 30, .019f },
                new[] { .05f, .001f, .049f, .005f, .037f }
            };
            Point[] headings = { new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1),
                new Point(1, 1), new Point(1, -1), new Point(-1, 1), new Point(-1, -1) };
            for (int profession = 0; profession < 3; profession++)
                for (int schedule = 0; schedule < schedules.Length; schedule++) foreach (Point heading in headings)
            {
                var game = DodgeDistanceGame(data, profession); Point start = game.Position;
                Check(game.CanStand(start + heading.Normalized * 2.7f), "Each exact-distance endpoint is unobstructed authored ground");
                game.Dash(heading * 7); game.SetAim(new Point(-heading.y, heading.x));
                DodgeDistanceFinish(game, schedules[schedule], heading * -1);
                DodgeDistanceEndpoint(game, start, heading, "class " + profession + ", schedule " + schedule + ", heading " + heading.x + "," + heading.y);
                Check(game.playerAction == null && game.dashCooldown > 0,
                    "Later movement and live aiming cannot steer or repeat a paid dodge");
            }
            var stationary = DodgeDistanceGame(data); Point origin = stationary.Position;
            stationary.SetAim(new Point(0, -1)); stationary.Dash(new Point()); stationary.SetAim(new Point(1, 0));
            DodgeDistanceFinish(stationary, new[] { .02f }); DodgeDistanceEndpoint(stationary, origin, new Point(0, -1), "stationary captured aim");
        }

        public static void DodgeDistanceKeepsSweptWallsSlidesAndStoryBarriers(Campaign data)
        {
            foreach (float dt in new[] { 1f / 60, 1f / 30, .02f, .05f })
            {
                var wall = Quiet(data); wall.invulnerable = 0; wall.SkipTutorial();
                Point foot = WallStand(wall); wall.save.x = foot.x; wall.save.y = foot.y;
                wall.Dash(new Point(-1, 0)); DodgeDistanceFinish(wall, new[] { dt });
                Check(wall.save.x >= (float)Math.Floor(foot.x) + .219f && (wall.Position - foot).Length < .2f,
                    "Exact-budget fast movement stops at the existing west wall instead of tunnelling");

                // A shipped town house supplies a long straight west face. Its
                // authored wall blocks x while ordinary axis sliding allows y.
                var slide = DodgeDistanceGame(data); Point start = new Point(9.03f, 5.5f);
                Check(slide.CanStand(start) && !slide.CanStand(start + new Point(-.08f, 0)),
                    "House slide starts against the existing collision face");
                slide.save.x = start.x; slide.save.y = start.y; slide.Dash(new Point(-1, 1));
                DodgeDistanceFinish(slide, new[] { dt });
                Check(slide.CanStand(slide.Position) && slide.save.y > start.y + 1 && slide.save.x > start.x - 1.95f,
                    "Collision retains lateral sliding instead of stopping a diagonal dodge as a whole");
            }
            foreach (int room in new[] { 1, 2, 3, 5, 6, 7 }) foreach (float dt in new[] { 1f / 30, .05f })
            {
                var gate = Fresh(data); gate.SkipTutorial(); gate.save.stage = 3; gate.save.frontierEnding = 1;
                foreach (string flag in new[] { "exp_intro", "harbor_report", "desert_report" }) gate.save.flags.Add(flag);
                gate.Enter(room); gate.enemies.Clear(); gate.save.reducedMotion = true;
                Check(gate.save.room == room && gate.GateBarrierActive && !gate.GateOpen, "The original story objective really owns this closed arena");
                bool exercised = false;
                for (float y = 3; y < 13 && !exercised; y += .5f)
                {
                    Point approach = new Point(25.6f, y); if (!gate.CanStand(approach)) continue;
                    gate.save.x = approach.x; gate.save.y = approach.y; gate.Dash(new Point(1, 0));
                    DodgeDistanceFinish(gate, new[] { dt });
                    Check(!gate.InProtectedArena(gate.Position) && gate.save.x < 26 && !gate.GateOpen,
                        "The corrected last sample grants no bypass or objective progress in room " + room);
                    exercised = true;
                }
                Check(exercised, "Every protected regional arena has a real tested approach");
            }
        }

        public static void DodgeDistanceRetainsFeesProtectionAndSimulationClocks(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++) foreach (bool compass in new[] { false, true })
            {
                var game = DodgeDistanceGame(data, profession); game.save.relics[0] = 0; game.save.relics[1] = compass ? 2 : -1;
                game.save.health = 50; game.save.energy = 90; game.save.talents[2] = 3;
                game.attackCooldown = 1; game.skillCooldown = 1; game.guardTime = 1;
                var target = new Enemy(99141, 0, 16, 17, 1000) { stun = 100, timer = 999 };
                game.enemies.Add(target);
                var shot = new Projectile { position = new Point(19, 14), velocity = new Point(1, 0), friendly = true, life = 10 };
                game.projectiles.Add(shot); var effect = new Effect("number", game.Position, 1, "clock"); game.effects.Add(effect);
                float time = game.save.totalTime; Point start = game.Position;
                game.Dash(new Point(1, 0));
                float paid = 90 - (compass ? 25 : 20);
                Check(game.save.energy == paid && game.save.health == 53 && game.dashTime == .18f
                    && game.invulnerable == .32f && Math.Abs(game.dashCooldown - .57f) < .0001f,
                    "Acceptance retains the original fee, healing relic, protection, duration and talent cooldown");
                game.mode = "inventory"; game.Tick(.05f, new Point(1, 0));
                Check(game.Position.x == start.x && game.save.totalTime == time && game.save.energy == paid && game.dashTime == .18f
                    && game.invulnerable == .32f && shot.age == 0 && effect.life == 1, "Menus freeze all clocks together without spending the travel budget");
                game.Close(); game.hitStop = .1f; game.Tick(.05f, new Point()); game.Tick(.05f, new Point());
                Check(game.Position.x == start.x && game.dashTime == .18f && shot.age == 0 && game.save.energy == paid,
                    "Hit stop freezes the same original combat and world clocks");
                float simulated = DodgeDistanceFinish(game, new[] { .05f });
                DodgeDistanceEndpoint(game, start, new Point(1, 0), "unchanged fee/protection clock");
                Check(Math.Abs(simulated - .2f) < .00001f && Math.Abs(game.save.totalTime - time - simulated) < .00001f
                    && Math.Abs(game.invulnerable - .12f) < .00001f && Math.Abs(game.dashCooldown - .37f) < .00001f
                    && Math.Abs(game.attackCooldown - .8f) < .00001f && Math.Abs(game.skillCooldown - .8f) < .00001f
                    && Math.Abs(game.guardTime - .8f) < .00001f && Math.Abs(target.stun - 99.8f) < .0001f
                    && Math.Abs(shot.age - simulated) < .00001f && Math.Abs(effect.life - .8f) < .00001f,
                    "Only terminal displacement is clipped; enemies, projectiles, buffs, protection and cooldowns retain the full world timestep");
                Check(Math.Abs(game.save.energy - paid - simulated * (profession == 2 ? 18 : 14)) < .0001f && game.save.health == 53,
                    "One dodge pays and heals once; ordinary class regeneration continues on the original clock");
            }
            var protectedGame = DodgeDistanceGame(data); protectedGame.Dash(new Point(1, 0));
            DodgeDistanceFinish(protectedGame, new[] { .05f }); float health = protectedGame.save.health;
            DodgeHit(protectedGame, 10); Check(protectedGame.save.health == health, "Completing movement early in a frame does not shorten the existing invulnerability tail");
            for (int frame = 0; frame < 13; frame++) protectedGame.Tick(.01f, new Point());
            DodgeHit(protectedGame, 10); Check(protectedGame.save.health < health, "The same mature protection naturally expires rather than granting permanent safety");
        }

        public static void DodgeDistanceFinalSamplePreservesPresentationQueuesAndLifecycle(Campaign data)
        {
            var terminal = DodgeDistanceGame(data); Point start = terminal.Position;
            terminal.Dash(new Point(1, 0)); for (int frame = 0; frame < 3; frame++) terminal.Tick(.05f, new Point());
            terminal.Tick(.05f, new Point(0, -1), new Point(-1, 0));
            DodgeDistanceEndpoint(terminal, start, new Point(1, 0), "terminal long frame");
            Check(terminal.dashTime == 0 && Math.Abs(terminal.movementDirection.Length - .45f) < .0001f
                && terminal.PlayerPose == 8 && terminal.VisualFacing.x > .999f && terminal.footContacts == 0 && terminal.walkDistance == 0,
                "The last real dash sample retains its direction/pose, gets no ordinary walking remainder, and creates no walking footsteps");
            terminal.Tick(.01f, new Point());
            Check(!terminal.ActuallyMoving && terminal.PlayerPose != 8, "The dash presentation retires on the next stationary sample");

            foreach (bool skill in new[] { false, true })
            {
                var queued = DodgeDistanceGame(data, 1); queued.save.weaponStyles[1] = 1; Point origin = queued.Position;
                queued.Dash(new Point(1, 0)); for (int frame = 0; frame < 3; frame++) queued.Tick(.05f, new Point());
                if (skill) queued.Skill(new Point(0, -1)); else queued.PressAttack(new Point(0, -1));
                Check(queued.playerAction == null, "Late buffered input cannot pay or start before the accepted dodge ends");
                queued.Tick(.05f, new Point()); DodgeDistanceEndpoint(queued, origin, new Point(1, 0), "buffered follow-up");
                ActorAction action = queued.playerAction;
                Check(action != null && action.kind == (skill ? "skill" : "attack") && action.elapsed == 0 && !action.released
                    && action.direction.y < -.999f && queued.PlayerPose != 8,
                    "The existing end-of-tick queue starts one fresh captured-direction preparation after the exact final travel sample");
            }
            var exact = DodgeDistanceGame(data); exact.Dash(new Point(1, 0));
            for (int frame = 0; frame < 9; frame++) exact.Tick(.02f, new Point());
            Check(exact.dashTime == 0 && !exact.ActionTransitionBlocked, "Float subtraction residue cannot impose a tenth full dodge frame or an invisible action lock");

            foreach (int version in new[] { 1, 2 })
            {
                var saved = DodgeDistanceGame(data); saved.save.version = version; saved.save.coins = 73; saved.save.inventory[0] = 21;
                saved.save.flags.Add("dodge_distance_saved_story"); saved.Dash(new Point(1, 0)); saved.Tick(.05f, new Point());
                saved.PrepareSave(); Point stored = saved.Position; int coins = saved.save.coins, wood = saved.save.inventory[0];
                var restored = new GameCore(data, saved.save); restored.Close(); restored.Tick(.05f, new Point());
                Check((restored.Position - stored).Length < .0001f && restored.dashTime == 0 && restored.playerAction == null
                    && restored.save.coins == coins && restored.save.inventory[0] == wood && restored.Has("dodge_distance_saved_story"),
                    "Both existing save versions restore actual feet and mature progress without replaying transient dodge travel");
            }
            var travel = DodgeDistanceGame(data); travel.Dash(new Point(1, 0)); travel.Tick(.05f, new Point()); travel.Enter(1);
            Point arrival = travel.Position; travel.enemies.Clear(); travel.Tick(.05f, new Point());
            Check((travel.Position - arrival).Length < .0001f && travel.dashTime == 0, "Map entry severs the old dodge displacement instead of applying a remainder in the new world");
        }
    }
}
