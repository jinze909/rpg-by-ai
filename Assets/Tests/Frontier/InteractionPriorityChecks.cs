using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        // Retain a semantic baseline: older production still compiles and runs
        // its normal tick, where a ready paid request incorrectly fires first.
        private static void InteractionPriorityTick(GameCore game, float dt, Point movement)
        {
            MethodInfo method = typeof(GameCore).GetMethod("TickForInteraction", new[] { typeof(float), typeof(Point), typeof(Point?) });
            if (method == null) game.Tick(dt, movement, new Point(1, 0));
            else method.Invoke(game, new object[] { dt, movement, (Point?)new Point(1, 0) });
        }
        private static bool InteractionPriorityCan(GameCore game)
        {
            PropertyInfo property = typeof(GameCore).GetProperty("CanPrioritizeInteraction");
            return property != null && (bool)property.GetValue(game, null);
        }
        private static GameCore InteractionPriorityGame(Campaign data, int profession = 1)
        {
            GameCore game = Fresh(data, profession); game.SkipTutorial(); game.save.companion = -1;
            game.enemies.Clear(); game.projectiles.Clear(); game.effects.Clear(); game.hazards.Clear(); game.sounds.Clear();
            game.save.x = 7; game.save.y = 9; game.save.energy = 60; game.save.health = game.save.maxHealth;
            game.save.weaponStyles[profession] = 1; game.invulnerable = 0; game.save.reducedMotion = true;
            Check(game.CanStand(game.Position) && game.Nearest() != null && game.Nearest().id == "smith",
                "The real blacksmith has a legal walkable interaction approach");
            return game;
        }
        private static void InteractionPriorityQueue(GameCore game, string request, float delay)
        {
            if (request == "attack") { game.attackCooldown = delay; game.PressAttack(new Point(1, 0)); }
            else if (request == "skill") { game.skillCooldown = delay; game.Skill(new Point(1, 0)); }
            else if (request == "dodge") { game.dashCooldown = delay; game.Dash(new Point(1, 0)); }
            else
            {
                game.save.flags.Add("companion_1"); game.SelectCompanion(1);
                game.companionCooldown = delay; game.CommandCompanion();
                typeof(GameCore).GetField("companionAttack", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game, 1000f);
            }
            Check(game.playerAction == null && game.companionAction == null && game.dashTime <= 0,
                "Only an unpaid near-ready intent exists before the E frame: " + request);
        }
        private static bool InteractionPriorityNear(float left, float right) { return Math.Abs(left - right) < .001f; }

        public static void InteractionPriorityRetiresUnstartedPaidRequestsAtTheirReadiness(Campaign data)
        {
            foreach (float step in new[] { .01f, .02f, .05f })
            foreach (float delay in new[] { 0f, .10f })
            foreach (int profession in new[] { 0, 1, 2 })
            foreach (string request in new[] { "attack", "skill", "dodge", "companion" })
            {
                GameCore game = InteractionPriorityGame(data, profession);
                // The zero case lands exactly on this frame's readiness boundary;
                // .10 remains pending and must not survive a rejected conversation.
                InteractionPriorityQueue(game, request, delay == 0 ? step : delay);
                float before = game.save.energy, clock = game.save.totalTime;
                InteractionPriorityTick(game, step, new Point());
                float expected = before + step * (profession == 2 ? 18 : 14);
                Check(InteractionPriorityNear(game.save.energy, expected),
                    "A real E frame at the smith cannot first spend unpaid " + request + " energy; class=" + profession + " dt=" + step + " delay=" + delay);
                Check(game.playerAction == null && game.companionAction == null && game.dashTime <= 0 && game.LastCompanionReceipt == null,
                    "E retires only the pending action, without early cast, dodge, command or support receipt: " + request);
                Check(InteractionPriorityNear(game.save.totalTime, clock + step) && game.effects.Count == 0 && game.projectiles.Count == 0,
                    "Claiming a real interaction does not pause the world or fabricate a combat effect");
                game.Interact(); Check(game.mode == "dialog" && game.conversation != null, "The original smith conversation still opens");
                game.Close(); Advance(game, .4f);
                Check(game.playerAction == null && game.companionAction == null && game.dashTime <= 0 && game.LastCompanionReceipt == null,
                    "A canceled E-frame request cannot revive after the same conversation closes");
            }
        }

        public static void InteractionPriorityPreservesPaidContactsAndWorldSimulation(Campaign data)
        {
            foreach (int profession in new[] { 0, 1, 2 }) foreach (bool skill in new[] { false, true })
            {
                GameCore ordinary = InteractionPriorityGame(data, profession), interacting = InteractionPriorityGame(data, profession);
                foreach (GameCore game in new[] { ordinary, interacting })
                {
                    if (skill) game.Skill(new Point(1, 0)); else game.Attack(new Point(1, 0));
                    Check(game.playerAction != null, "The mature entry point really accepts a paid preparation");
                    game.playerAction.elapsed = game.playerAction.contact - .01f;
                    game.effects.Add(new Effect("audit-clock", new Point(12, 14), 1));
                    game.projectiles.Add(new Projectile { position = new Point(12, 14), velocity = new Point(1, 0), friendly = true, visual = "arrow", damage = 1, life = 1 });
                }
                ActorAction owned = interacting.playerAction; float cost = interacting.save.energy, clock = interacting.save.totalTime;
                ordinary.Tick(.02f, new Point(), new Point(1, 0)); InteractionPriorityTick(interacting, .02f, new Point());
                Check(ReferenceEquals(interacting.playerAction, owned) && owned.released && owned.elapsed >= owned.contact,
                    "A previously paid " + (skill ? "skill" : "attack") + " keeps and reaches its original contact for class " + profession);
                Check(InteractionPriorityNear(interacting.save.energy, ordinary.save.energy) && interacting.save.energy >= cost
                    && InteractionPriorityNear(interacting.save.totalTime, clock + .02f)
                    && interacting.projectiles.Count == ordinary.projectiles.Count && interacting.effects.Count == ordinary.effects.Count,
                    "Actual contact, missile creation, effect aging and energy regeneration match the normal world tick");
                Check(InteractionPriorityNear(interacting.projectiles[0].position.x, ordinary.projectiles[0].position.x)
                    && InteractionPriorityNear(interacting.projectiles[0].life, ordinary.projectiles[0].life),
                    "An E request cannot freeze or restart an already flying projectile");
                float elapsed = owned.elapsed; interacting.Interact(); interacting.Tick(.05f, new Point());
                Check(ReferenceEquals(interacting.playerAction, owned) && owned.elapsed == elapsed,
                    "The actual opened conversation pauses an owned paid preparation without canceling it");
                interacting.Close(); interacting.Tick(.02f, new Point());
                Check(owned.elapsed > elapsed, "Closing the original dialogue resumes the paid action on its old clock");
            }
            for (int id = 0; id < 3; id++)
            {
                GameCore game = InteractionPriorityGame(data); game.save.flags.Add("companion_" + id); game.SelectCompanion(id); game.CommandCompanion();
                ActorAction command = game.companionAction; Check(command != null, "A ready companion command is already accepted");
                command.elapsed = command.contact - .01f; InteractionPriorityTick(game, .02f, new Point());
                Check(ReferenceEquals(game.companionAction, command) && command.released && game.LastCompanionReceipt != null
                    && game.LastCompanionReceipt.id == id && game.companionCooldown > 17,
                    "Interaction priority never cancels or duplicates a paid companion contact: " + id);
            }
            GameCore hazard = InteractionPriorityGame(data); hazard.invulnerable = 0;
            hazard.projectiles.Add(new Projectile { position = hazard.Position, damage = 9, source = "shot", visual = "enemyArrow", life = 1 });
            float health = hazard.save.health; InteractionPriorityTick(hazard, .02f, new Point());
            Check(hazard.save.health < health, "Requesting E is not an extra invulnerability or enemy-projectile pause");
        }

        public static void InteractionPriorityUsesPostMovementTargetAndDoesNotStealRejectedInputs(Campaign data)
        {
            foreach (string request in new[] { "attack", "skill", "dodge", "companion" })
            {
                GameCore noTarget = InteractionPriorityGame(data);
                bool foundEmpty = false;
                for (int y = 3; y < noTarget.Map.rows.Length - 3 && !foundEmpty; y++)
                    for (int x = 3; x < noTarget.Map.rows[y].Length - 3 && !foundEmpty; x++)
                    {
                        noTarget.save.x = x; noTarget.save.y = y;
                        foundEmpty = noTarget.CanStand(noTarget.Position) && noTarget.Nearest() == null;
                    }
                Check(foundEmpty, "The authored town contains a legal empty-road control point");
                Check(noTarget.Nearest() == null && !InteractionPriorityCan(noTarget), "An empty road has no claimable interaction");
                InteractionPriorityQueue(noTarget, request, .02f); InteractionPriorityTick(noTarget, .02f, new Point());
                Check(request == "dodge" ? noTarget.dashTime > 0 : request == "companion" ? noTarget.companionAction != null : noTarget.playerAction != null,
                    "An E miss on the road cannot suppress a valid ready " + request);
            }
            foreach (bool approach in new[] { true, false })
            {
                GameCore moving = InteractionPriorityGame(data); moving.save.y = approach ? 9.80f : 9.70f;
                Point direction = new Point(0, approach ? -1 : 1);
                Check(InteractionPriorityCan(moving) != approach, "The initial range belongs to the opposite side of this movement boundary");
                InteractionPriorityQueue(moving, "attack", .05f); InteractionPriorityTick(moving, .05f, direction);
                Check((moving.Nearest() != null) == approach && InteractionPriorityCan(moving) == approach,
                    "The actual legal movement crosses the blacksmith's authored 1.75-cell range");
                Check((moving.playerAction == null) == approach,
                    "E priority uses the post-movement target instead of stale proximity at frame start");
                if (approach) { moving.Interact(); Check(moving.mode == "dialog", "Walking into range can open the real smith conversation this frame"); }
            }
            GameCore once = InteractionPriorityGame(data); InteractionPriorityTick(once, .02f, new Point());
            InteractionPriorityQueue(once, "attack", .02f); once.Tick(.02f, new Point());
            Check(once.playerAction != null, "The interaction request is scoped to one tick; a later ordinary tick still starts its real queued attack");
        }

        public static void InteractionPriorityRetainsTrainingHitStopAndLegacyOwnership(Campaign data)
        {
            GameCore basic = InteractionPriorityGame(data); basic.StartPractice();
            Check(basic.practicing && !basic.SparringActive && !InteractionPriorityCan(basic), "Basic practice keeps its established combat owner");
            basic.save.weaponStyles[basic.save.weapon] = 1; basic.attackCooldown = .02f; basic.PressAttack(new Point(1, 0));
            InteractionPriorityTick(basic, .02f, new Point());
            Check(basic.playerAction != null && basic.practicing, "E during basic practice does not eat a valid training attack");
            GameCore sparring = SparringGame(data); Check(sparring.StartSparring(), "Real mentor starts its authored drills");
            Check(!InteractionPriorityCan(sparring), "An active sparring wave does not become an NPC interaction");
            sparring.enemies.Clear(); sparring.Tick(.02f, new Point());
            Check(sparring.SparringAwaitingNext && InteractionPriorityCan(sparring), "The real cleared-wave handoff is a deliberate E target");
            InteractionPriorityTick(sparring, .02f, new Point()); sparring.Interact();
            Check(sparring.SparringStage == 1 && !sparring.SparringAwaitingNext && sparring.enemies.Count > 0,
                "E continues the existing next drill without removing its opponents, clocks or rewards");
            foreach (float stop in new[] { 0f, .1f })
            {
                GameCore frozen = InteractionPriorityGame(data); InteractionPriorityQueue(frozen, "attack", .02f); frozen.hitStop = stop;
                float energy = frozen.save.energy, clock = frozen.save.totalTime;
                InteractionPriorityTick(frozen, stop > 0 ? .02f : 0, new Point());
                Check(frozen.playerAction == null && frozen.save.energy == energy && frozen.save.totalTime == clock,
                    "Zero-time or hit-stop interaction preserves the old simulation freeze");
                frozen.Interact(); frozen.Close(); Advance(frozen, .4f);
                Check(frozen.playerAction == null, "Opening the actual frozen conversation still retires unstarted combat intent");
            }
            foreach (float stop in new[] { 0f, .1f })
            {
                GameCore pickup = InteractionPriorityGame(data); pickup.Enter(1); pickup.enemies.Clear(); pickup.projectiles.Clear(); pickup.effects.Clear();
                pickup.save.energy = 60; pickup.invulnerable = 0; WorldObject node = Object(pickup, "wood"); bool found = false;
                for (int y = -2; y <= 2 && !found; y++) for (int x = -2; x <= 2 && !found; x++)
                {
                    pickup.save.x = node.x + x * .5f; pickup.save.y = node.y + y * .5f;
                    found = pickup.CanStand(pickup.Position) && ReferenceEquals(pickup.Nearest(), node);
                }
                Check(found && !pickup.Has("node_" + node.id), "A canonical unharvested field node has its own legal E approach");
                InteractionPriorityQueue(pickup, "attack", .02f); pickup.hitStop = stop;
                int stock = pickup.save.inventory[0]; InteractionPriorityTick(pickup, stop > 0 ? .02f : 0, new Point()); pickup.Interact();
                Check(pickup.mode == "world" && pickup.save.inventory[0] == stock + 3 && pickup.Has("node_" + node.id),
                    "E retains the original no-menu gather reward during zero-delta or contact hit-stop");
                Advance(pickup, .4f);
                Check(pickup.playerAction == null, "Even a world-mode pickup cannot leave an unpaid basic waiting behind frozen time");
            }
            for (int version = 1; version <= 2; version++)
            {
                GameCore source = InteractionPriorityGame(data); source.save.inventory[0] = 37; source.save.coins = 113;
                source.save.flags.Add("interaction-priority-save-witness"); source.PrepareSave(); SaveData saved = CloneSave(source.save); saved.version = version;
                GameCore loaded = new GameCore(data, saved); loaded.Start(); loaded.Close();
                Check(loaded.save.inventory[0] == 37 && loaded.save.coins == 113 && loaded.Has("interaction-priority-save-witness"),
                    "Both legacy save versions retain the original inventory, coins and story flags");
                loaded.save.x = 7; loaded.save.y = 9; string before = PlanStatsState(loaded);
                for (int repeat = 0; repeat < 12; repeat++) Check(InteractionPriorityCan(loaded), "A restored canonical nearby NPC remains eligible");
                Check(PlanStatsState(loaded) == before, "Eligibility inspection has no save, action, timer or economy side effects");
                InteractionPriorityQueue(loaded, "attack", .02f); loaded.PrepareSave(); loaded.Load(CloneSave(loaded.save)); loaded.Close();
                loaded.enemies.Clear(); Advance(loaded, .4f);
                Check(loaded.playerAction == null, "A selected-save owner cannot inherit an abandoned unstarted request");
            }
            PropertyInfo property = typeof(GameCore).GetProperty("CanPrioritizeInteraction");
            Check(property != null && property.PropertyType == typeof(bool) && property.GetSetMethod(true) == null,
                "The live interaction eligibility is a read-only observation, not a stored save field");
        }
    }
}
