using System;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void CombatFocusCycleOrdersWrapsAndSkipsInvalidActors(Campaign data)
        {
            var g = Quiet(data);
            Enemy near = FocusEnemy(g, 98001, 16, 14), far = FocusEnemy(g, 98002, 18, 14), side = FocusEnemy(g, 98003, 16, 16);
            FocusAt(g, near);
            foreach (Enemy expected in new[] { far, side, near })
                Check(g.CycleCombatFocus() && ReferenceEquals(g.CombatFocusTarget, expected), "Forward selection sorts polar angle, then distance, and wraps");
            foreach (Enemy expected in new[] { side, far, near })
                Check(g.CycleCombatFocus(-1) && ReferenceEquals(g.CombatFocusTarget, expected), "Reverse selection is the inverse order including the wrap");
            string[] rows = g.Map.rows;
            try
            {
                g.Map.rows = (string[])rows.Clone(); char[] wall = g.Map.rows[14].ToCharArray(); wall[17] = '#'; g.Map.rows[14] = new string(wall);
                Check(g.CycleCombatFocus() && ReferenceEquals(g.CombatFocusTarget, side), "The real line-of-attack guard excludes an intervening wall");
            }
            finally { g.Map.rows = rows; }
            g.enemies.Clear(); g.ClearCombatFocus();
            Enemy high = FocusEnemy(g, 98020, 16, 14), low = FocusEnemy(g, 98010, 16, 14), duplicate = FocusEnemy(g, 98010, 16, 14);
            FocusAt(g, low);
            Check(g.CycleCombatFocus() && ReferenceEquals(g.CombatFocusTarget, duplicate) && g.CycleCombatFocus() && ReferenceEquals(g.CombatFocusTarget, high),
                "Equal angle and distance use ID, then stable list position for malformed duplicate IDs");
            Check(g.CycleCombatFocus() && ReferenceEquals(g.CombatFocusTarget, low), "A duplicate identity never substitutes for the selected reference");
            foreach (Enemy invalid in new[] { new Enemy(98030, 0, 17, 14, 0), new Enemy(98031, 0, 17, 14, float.NaN),
                new Enemy(98032, 0, 24.01f, 14, 99), new Enemy(98033, 0, float.NaN, 14, 99) }) g.enemies.Add(invalid);
            Check(g.CycleCombatFocus(-1) && ReferenceEquals(g.CombatFocusTarget, high), "Dead, nonfinite and out-of-range actors never enter the ring");
        }

        public static void CombatFocusCycleNeedsAnExistingLiveSession(Campaign data)
        {
            var g = Quiet(data); Enemy a = FocusEnemy(g, 98040, 16, 14), b = FocusEnemy(g, 98041, 16, 16);
            string state = TacticsState(g);
            Check(!g.CanCycleCombatFocus && !g.CycleCombatFocus() && TacticsState(g) == state, "Cycling from free aim cannot acquire or mutate a lock");
            FocusAt(g, a); g.enemies.Remove(b); state = TacticsState(g);
            Check(!g.CanCycleCombatFocus && !g.CycleCombatFocus() && TacticsState(g) == state, "One eligible actor is an exact no-op, not release");
            g.enemies.Add(b);
            foreach (int invalid in new[] { 0, 2, -2, int.MinValue, int.MaxValue })
            { state = TacticsState(g); Check(!g.CycleCombatFocus(invalid) && TacticsState(g) == state, "Invalid cycle directions mutate nothing"); }
            foreach (string boundary in new[] { "removed", "dead", "save", "world", "menu" })
            {
                g = Quiet(data); a = FocusEnemy(g, 98042, 16, 14); FocusEnemy(g, 98043, 16, 16); FocusAt(g, a);
                if (boundary == "removed") { g.enemies.Remove(a); FocusEnemy(g, a.id, a.x, a.y); }
                else if (boundary == "dead") a.hp = 0;
                else if (boundary == "save") g.save = CloneSave(g.save);
                else if (boundary == "world") g.Enter(g.save.room);
                else g.mode = "inventory";
                state = TacticsState(g);
                Check(g.CombatFocusTarget == null && !g.CanCycleCombatFocus && !g.CycleCombatFocus() && TacticsState(g) == state,
                    "Cycle cannot reacquire across invalidated reference/session boundary: " + boundary);
            }
        }

        public static void CombatFocusCyclePreservesAcceptedQueuedAndFlyingAim(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++)
            {
                var g = Quiet(data, profession); Enemy a = FocusEnemy(g, 98050, 17, 14), b = FocusEnemy(g, 98051, 16, 16); FocusAt(g, a);
                g.Attack(new Point(-1, 0)); ActorAction action = g.playerAction; Point facing = action.direction, target = action.target.Value;
                float timer = action.elapsed, energy = g.save.energy, cooldown = g.attackCooldown;
                Check(g.CycleCombatFocus() && ReferenceEquals(g.CombatFocusTarget, b) && !g.aimTarget.HasValue &&
                    ReferenceEquals(g.playerAction, action) && action.elapsed == timer && (action.direction - facing).Length == 0 &&
                    (action.target.Value - target).Length == 0 && g.save.energy == energy && g.attackCooldown == cooldown,
                    "A cycle severs only live aim, preserving the actual accepted action and its existing cost/timing for class " + profession);
                FocusRelease(g, action);
                if (profession > 0)
                {
                    Check(g.projectiles.Count == 1, "The real prepared ranged contact emits its original missile");
                    Projectile shot = g.projectiles[0]; Point velocity = shot.velocity;
                    Check(g.CycleCombatFocus(-1), "A real flying projectile permits explicit selection changes");
                    g.Tick(.02f, new Point()); Check(g.projectiles.Contains(shot) && (shot.velocity - velocity).Length == 0, "Released arrows and starfire remain nonhoming");
                }
                var queued = Quiet(data, profession); a = FocusEnemy(queued, 98052, 17, 14); b = FocusEnemy(queued, 98053, 16, 16); FocusAt(queued, a);
                queued.Attack(new Point(-1, 0)); ActorAction basic = queued.playerAction; Advance(queued, basic.contact - .04f);
                queued.Skill(new Point(-1, 0)); Point captured = a.Position;
                Check(ReferenceEquals(queued.playerAction, basic) && queued.skillCooldown <= 0 && queued.save.energy == 100, "Actual Q buffers without early cost");
                Check(queued.CycleCombatFocus() && ReferenceEquals(queued.CombatFocusTarget, b), "Selection changes while actual Q is queued");
                for (int frame = 0; frame < 100 && ReferenceEquals(queued.playerAction, basic); frame++) queued.Tick(.01f, new Point());
                ActorAction q = queued.playerAction;
                Check(q != null && q.kind == "skill" && basic.released && q.direction.x > .999f && q.target.HasValue &&
                    (q.target.Value - captured).Length < .0001f && queued.save.energy == 60 && queued.skillCooldown == 3.5f,
                    "Buffered Q captures its pressed target once and keeps original cost/cooldown through cycling, class " + profession);
            }
        }

        public static void CombatFocusCycleQueriesAndSelectionKeepProgressReadOnly(Campaign data)
        {
            var g = Quiet(data, 1); Enemy a = FocusEnemy(g, 98060, 17, 14); FocusEnemy(g, 98061, 16, 16); FocusAt(g, a);
            g.Attack(new Point(-1, 0)); g.Tick(.03f, new Point()); g.hitStop = .07f;
            string state = TacticsState(g);
            for (int i = 0; i < 100; i++) Check(g.CanCycleCombatFocus && ReferenceEquals(g.CombatFocusTarget, a), "HUD query observes the same live ring");
            Check(TacticsState(g) == state, "Repeated cycle availability reads cannot advance simulation, RNG, private state or save fields");
            g.PrepareSave(); string save = TacticsState(g.save); float time = g.save.totalTime, stop = g.hitStop; int effects = g.effects.Count, revision = g.revision;
            Check(g.CycleCombatFocus() && g.CycleCombatFocus(-1) && ReferenceEquals(g.CombatFocusTarget, a) && TacticsState(g.save) == save &&
                g.save.totalTime == time && g.hitStop == stop && g.effects.Count == effects && g.revision == revision,
                "Explicit selection changes no saved field, world time, hitstop, effects, progression or campaign revision");
        }
    }
}
