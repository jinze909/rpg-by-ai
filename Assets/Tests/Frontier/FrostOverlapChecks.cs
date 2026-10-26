using System;
using System.Collections.Generic;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static readonly BindingFlags frostOverlapFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static object FrostOverlapField(object owner, string name)
        { return owner.GetType().GetField(name, frostOverlapFlags).GetValue(owner); }
        private static void FrostOverlapCall(GameCore game, string method, params object[] values)
        { typeof(GameCore).GetMethod(method, frostOverlapFlags).Invoke(game, values); }
        private static object FrostOverlapHead(GameCore game) { return FrostOverlapField(game, "frost"); }
        private static object FrostOverlapTail(GameCore game)
        {
            object field = FrostOverlapHead(game);
            // The same harness deliberately compiles against the old single-field
            // implementation. Missing chaining is diagnosed by HP/pulse semantics,
            // never by requiring a new production member or a compilation error.
            while (field != null)
            {
                FieldInfo next = field.GetType().GetField("next", frostOverlapFlags);
                object value = next == null ? null : next.GetValue(field);
                if (value == null) return field;
                field = value;
            }
            return null;
        }
        private static int FrostOverlapPulses(object field) { return (int)FrostOverlapField(field, "pulses"); }
        private static bool FrostOverlapNear(float a, float b) { return Math.Abs(a - b) < .008f; }
        private static GameCore FrostOverlapGame(Campaign data)
        {
            GameCore game = Quiet(data, 2); game.save.flags.Add("core_1"); game.save.flags.Add("technique_2");
            game.save.technique = 1; game.save.flags.Add("companion_2"); game.SelectCompanion(2);
            typeof(GameCore).GetField("companionAttack", frostOverlapFlags).SetValue(game, 1000f);
            game.save.tutorialBits = 7; game.save.seed = 1; return game;
        }
        private static Enemy FrostOverlapEnemy(GameCore game, int id, Point position, float health = 9999)
        {
            Check(game.CanStand(position), "Frost fixtures use legal authored ground");
            var enemy = new Enemy(id, 0, position.x, position.y, health) { stun = 1000, staggerRecovery = 1000, timer = 1000 };
            game.enemies.Add(enemy); return enemy;
        }
        private static void FrostOverlapAdvance(GameCore game, float seconds, float step = .01f)
        {
            while (seconds > .00001f)
            {
                float dt = Math.Min(seconds, step); game.save.seed = 1; game.Tick(dt, new Point()); seconds -= dt;
            }
        }
        private static object FrostOverlapRelease(GameCore game, bool player, out ActorAction action)
        {
            if (player)
            {
                float energy = game.save.energy; game.Skill(new Point(1, 0)); action = game.playerAction;
                Check(action != null && action.kind == "skill" && !action.released && FrostOverlapNear(energy - game.save.energy, 40)
                    && game.skillCooldown == 3.5f && action.contact == .28f && action.duration == .62f,
                    "A real paid alternate Q retains its original cost, cooldown, preparation and recovery");
            }
            else
            {
                float energy = game.save.energy; game.CommandCompanion(); action = game.companionAction;
                Check(action != null && action.kind == "command" && action.weapon == 2 && !action.released
                    && game.save.energy == energy && game.companionCooldown == game.CompanionCommandCooldownTotal
                    && action.contact == GameCore.CompanionCommandContact && action.duration == GameCore.CompanionCommandDuration,
                    "A real Sen Z retains its original energy-free preparation, cooldown and recovery");
            }
            for (int frame = 0; frame < 120 && !action.released; frame++) FrostOverlapAdvance(game, .01f);
            Check(action.released && FrostOverlapHead(game) != null, "The real action reaches its authored release and begins a field");
            object field = FrostOverlapTail(game);
            Check(ReferenceEquals(FrostOverlapField(field, "impact"), action.impact)
                && FrostOverlapNear((float)FrostOverlapField(field, "damage"), action.damage * .4f),
                "Every released cast owns its immutable prepared profile and original per-pulse multiplier");
            return field;
        }
        private static object FrostOverlapInject(GameCore game, Point center, float damage, ImpactProfile impact)
        { FrostOverlapCall(game, "BeginFrost", center, damage, impact); return FrostOverlapTail(game); }
        private static void FrostOverlapTick(GameCore game, float dt)
        { game.save.seed = 1; FrostOverlapCall(game, "TickFrost", dt); }
        private static void FrostOverlapPair(GameCore game, out object first, out object second)
        {
            first = FrostOverlapInject(game, game.Position, 10, new ImpactProfile(2, 0, 2, 0, 0, true));
            second = FrostOverlapInject(game, game.Position, 20, new ImpactProfile(2, 0, 0, 0, 0, false));
        }

        public static void FrostOverlapRealQAndZRetainBothCompletePaidFields(Campaign data)
        {
            foreach (bool qFirst in new[] { true, false }) foreach (float step in new[] { 1f / 60, .02f, .05f })
            {
                GameCore game = FrostOverlapGame(data); Enemy enemy = FrostOverlapEnemy(game, 42100, game.Position + new Point(1, 0));
                ActorAction firstAction, secondAction;
                object first = FrostOverlapRelease(game, qFirst, out firstAction); FrostOverlapAdvance(game, .9f, step);
                Check(FrostOverlapPulses(first) == 2, "The older real field has emitted two contacts before the other action is requested");
                object second = FrostOverlapRelease(game, !qFirst, out secondAction);
                Check(!ReferenceEquals(first, second), "Q and Sen Z each create their own actual release identity");
                FrostOverlapAdvance(game, 4.3f, step);
                Check(FrostOverlapPulses(first) == 5 && FrostOverlapPulses(second) == 5 && FrostOverlapHead(game) == null,
                    "Both accepted casts complete exactly five pulses and retire independently: " + (qFirst ? "Q then Z" : "Z then Q") + ", step=" + step);
                Check(FrostOverlapNear(enemy.maxHp - enemy.hp, (firstAction.damage + secondAction.damage) * .4f * 5),
                    "The original Q and Z pulse damage is conserved across all ten real contacts without cancellation or bonus damage");
                float hp = enemy.hp; FrostOverlapAdvance(game, 1, step);
                Check(enemy.hp == hp && !game.effects.Exists(e => e.kind == "frost"), "An expired pair produces no eleventh pulse or lingering effect");
            }
            // Isolate a delayed simulation frame: each field still owns only its
            // original five scheduled contacts, with no frame-rate bonus pulse.
            GameCore delayed = FrostOverlapGame(data); Enemy target = FrostOverlapEnemy(delayed, 42101, delayed.Position + new Point(1, 0));
            object a, b; FrostOverlapPair(delayed, out a, out b); FrostOverlapTick(delayed, 8);
            Check(FrostOverlapPulses(a) == 5 && FrostOverlapPulses(b) == 5 && target.hp == target.maxHp - 60 && FrostOverlapHead(delayed) == null,
                "A long final frame catches up both five-pulse schedules exactly once");
        }

        public static void FrostOverlapKeepsFixedCentersWallsAndMaximumControl(Campaign data)
        {
            GameCore game = FrostOverlapGame(data); Point firstCenter = game.Position, secondCenter = new Point(18, 14);
            Enemy left = FrostOverlapEnemy(game, 42110, new Point(13, 14));
            Enemy right = FrostOverlapEnemy(game, 42111, new Point(19, 14));
            Enemy middle = FrostOverlapEnemy(game, 42112, new Point(15, 14));
            ActorAction q, z; object first = FrostOverlapRelease(game, true, out q); FrostOverlapAdvance(game, .7f);
            game.save.x = secondCenter.x; game.save.y = secondCenter.y; object second = FrostOverlapRelease(game, false, out z);
            game.save.x = 25; game.save.y = 16; FrostOverlapAdvance(game, 4.3f);
            Check(((Point)FrostOverlapField(first, "center") - firstCenter).Length == 0
                && ((Point)FrostOverlapField(second, "center") - secondCenter).Length == 0,
                "Each actual Q/Z field stays at its distinct accepted release center after the hero moves away");
            Check(FrostOverlapNear(left.maxHp - left.hp, q.damage * .4f * 5)
                && FrostOverlapNear(right.maxHp - right.hp, z.damage * .4f * 5)
                && FrostOverlapNear(middle.maxHp - middle.hp, (q.damage + z.damage) * .4f * 5),
                "Separated and intersecting targets receive only the contacts from the fields whose original four-tile radii contain them");

            GameCore wall = FrostOverlapGame(data); Point a = new Point(), b = new Point(); bool found = false;
            for (int y = 2; y < wall.Map.rows.Length - 2 && !found; y++) for (int x = 2; x < wall.Map.rows[y].Length - 3 && !found; x++)
            {
                a = new Point(x + .5f, y + .5f); b = new Point(x + 2.5f, y + .5f);
                found = wall.CanStand(a) && wall.CanStand(b)
                    && !(bool)typeof(GameCore).GetMethod("ClearAttackPath", frostOverlapFlags).Invoke(wall, new object[] { a, b });
            }
            Check(found, "Authored ground contains a real nearby occluding wall");
            Enemy blocked = FrostOverlapEnemy(wall, 42113, b), visible = FrostOverlapEnemy(wall, 42114, a); visible.stun = 8;
            wall.save.x = a.x; wall.save.y = a.y; object one, two; FrostOverlapPair(wall, out one, out two);
            for (int i = 0; i < 5; i++) FrostOverlapTick(wall, i == 0 ? 0 : .8f);
            Check(blocked.hp == blocked.maxHp && visible.hp == visible.maxHp - 60 && visible.stun == 8,
                "Ten genuine overlapping contacts preserve wall occlusion and maximum remaining control, never add durations or shorten an earned eight-second window");
            Check(FrostOverlapPulses(one) == 5 && FrostOverlapPulses(two) == 5, "Occlusion never changes either field's own five-pulse schedule");
        }

        public static void FrostOverlapRetainsCastOrderOwnershipRewardsAndLessons(Campaign data)
        {
            foreach (bool playerFirst in new[] { true, false })
            {
                GameCore game = FrostOverlapGame(data); Enemy target = FrostOverlapEnemy(game, 42120, game.Position + new Point(1, 0));
                ActorAction firstAction, secondAction; object first = FrostOverlapRelease(game, playerFirst, out firstAction);
                FrostOverlapAdvance(game, .7f); object second = FrostOverlapRelease(game, !playerFirst, out secondAction);
                var firstImpact = (ImpactProfile)FrostOverlapField(first, "impact"); var secondImpact = (ImpactProfile)FrostOverlapField(second, "impact");
                Check(firstImpact.playerOwned == playerFirst && secondImpact.playerOwned != playerFirst
                    && ReferenceEquals(firstImpact, firstAction.impact) && ReferenceEquals(secondImpact, secondAction.impact),
                    "Each real field retains the exact immutable player versus companion identity captured by its own action");
                game.save.tutorialBits = 7; game.Equip(0); game.save.upgrades = 5; game.save.talents[0] = 20;
                game.save.weaponStyles[0] = 2; game.save.mastery[0] = 80; game.save.flags.Add("relic_5"); game.save.relics[0] = 5;
                float hp = target.hp; FrostOverlapAdvance(game, .85f);
                Check(FrostOverlapNear(hp - target.hp, (firstAction.damage + secondAction.damage) * .4f),
                    "Later equipment, talent, relic and mastery edits cannot change either prepared field's pulse damage or captured critical chance");
                Check((game.save.tutorialBits & (1 << 5)) != 0, "The retained player's actual later pulse still teaches the player's Q lesson");
            }
            // Explicit attribution edge: both casts become due at the same
            // instant. Their immutable mastery owners differ, so a one-HP foe
            // proves which real Hit resolves first without relying on list order.
            foreach (bool playerFirst in new[] { true, false })
            {
                GameCore game = FrostOverlapGame(data); Enemy victim = FrostOverlapEnemy(game, 42121, game.Position + new Point(1, 0), 1);
                game.save.tutorialBits = 7; int coins = game.save.coins, xp = game.save.xp, herb = game.save.inventory[2], kills = game.save.kills[game.save.room];
                var player = new ImpactProfile(2, 0, 2, 0, 0, true); var ally = new ImpactProfile(2, 0, 0, 0, 0, false);
                object oldest = FrostOverlapInject(game, game.Position, 20, playerFirst ? player : ally);
                object newest = FrostOverlapInject(game, game.Position, 30, playerFirst ? ally : player);
                FrostOverlapTick(game, 0);
                Check(!game.enemies.Contains(victim) && victim.hp == -7, "The oldest due cast consumes the lethal contact only once");
                Check(game.save.mastery[playerFirst ? 2 : 0] == 2 && game.save.mastery[playerFirst ? 0 : 2] == 0,
                    "Simultaneously due pulses credit the oldest accepted field's original mastery owner");
                Check(game.save.coins == coins + 6 && game.save.xp == xp + 12 && game.save.inventory[2] == herb + 1
                    && game.save.kills[game.save.room] == kills + 1,
                    "Overlapping lethal contacts award mature coins, XP, material and kill credit exactly once");
                Check(((game.save.tutorialBits & (1 << 5)) != 0) == playerFirst && ((game.save.tutorialBits & (1 << 3)) != 0) == playerFirst,
                    "A companion-first kill cannot impersonate a later player's attack or Q lesson");
                Check(FrostOverlapPulses(oldest) == 1 && FrostOverlapPulses(newest) == 1,
                    "The younger field still emits its due pulse after a prior kill without hitting the removed actor twice");
            }
        }

        public static void FrostOverlapBoundariesRetireAllFieldsAndContactEndCannotReviveThem(Campaign data)
        {
            foreach (string boundary in new[] { "travel", "new", "load", "practice-start", "practice-end", "range-reset", "range-layout", "range-clear", "range-end", "sparring-reset", "sparring-clear", "sparring-end", "reprise-start", "reprise-stop" })
            {
                GameCore game;
                if (boundary.StartsWith("range", StringComparison.Ordinal)) { game = RangeGame(data, 2); Check(game.StartRange(1), "Range boundary fixture starts through its real public API"); }
                else if (boundary.StartsWith("sparring", StringComparison.Ordinal)) { game = RangeGame(data, 2); Check(game.StartSparring(), "Sparring boundary fixture starts through its real public API"); }
                else if (boundary.StartsWith("reprise", StringComparison.Ordinal)) { game = RepriseGame(data, 2); RepriseLamp(game); if (boundary == "reprise-stop") RepriseStart(game); }
                else { game = FrostOverlapGame(data); if (boundary.StartsWith("practice", StringComparison.Ordinal)) { game.Enter(0); if (boundary == "practice-end") game.StartPractice(); } }
                object a, b; FrostOverlapPair(game, out a, out b);
                if (boundary == "travel") game.Enter(0);
                else if (boundary == "new") { game.NewGame(1); game.Close(); }
                else if (boundary == "load") { game.PrepareSave(); SaveData snapshot = CloneSave(game.save); snapshot.version = 1; game.Load(snapshot); game.Start(); }
                else if (boundary == "practice-start") game.StartPractice();
                else if (boundary == "practice-end" || boundary == "range-end" || boundary == "sparring-end") game.EndPractice();
                else if (boundary == "range-reset") Check(game.ResetRange(), "Live range reset succeeds");
                else if (boundary == "range-layout") Check(game.ChangeRangeLayout(2), "Live range layout change succeeds");
                else if (boundary == "range-clear") { game.enemies.Clear(); game.Tick(.01f, new Point()); Check(game.RangeFinished, "Actual range completion fires its retirement"); }
                else if (boundary == "sparring-reset") Check(game.RestartSparring(), "Live sparring restart succeeds");
                else if (boundary == "sparring-clear") { game.enemies.Clear(); game.Tick(.01f, new Point()); Check(game.SparringAwaitingNext, "Actual sparring completion fires its retirement"); }
                else if (boundary == "reprise-start") RepriseStart(game);
                else if (boundary == "reprise-stop") Check(game.StopSignalReprise(), "A real voluntary reprise stop succeeds");
                Check(FrostOverlapHead(game) == null, "Every original clearing boundary retires the whole pair: " + boundary);
                int ap = FrostOverlapPulses(a), bp = FrostOverlapPulses(b); FrostOverlapTick(game, 8);
                Check(FrostOverlapPulses(a) == ap && FrostOverlapPulses(b) == bp && FrostOverlapHead(game) == null,
                    "A stale chain cannot revive after clearing or contact a replacement scene: " + boundary);
            }
            foreach (int version in new[] { 1, 2 })
            {
                // Saved enemy health is bounded to 5000 by the existing load
                // sanitizer; use a legal snapshot instead of the 9999-HP tank.
                GameCore saved = FrostOverlapGame(data); Enemy old = FrostOverlapEnemy(saved, 42130, saved.Position + new Point(1, 0), 1000);
                object older, younger; FrostOverlapPair(saved, out older, out younger); FrostOverlapTick(saved, 0);
                saved.save.coins = 91; saved.save.health = 62; saved.PrepareSave(); SaveData snapshot = CloneSave(saved.save); snapshot.version = version;
                saved.Load(snapshot); saved.Start(); Enemy restored = saved.enemies.Find(e => e.id == old.id);
                Check(restored != null && restored.hp == old.hp && saved.save.coins == 91 && saved.save.health == 62 && FrostOverlapHead(saved) == null,
                    "Legacy version " + version + " preserves actual damaged HP and economy without serializing either field: restored=" + (restored == null ? "null" : restored.hp.ToString()) + ", old=" + old.hp + ", coins=" + saved.save.coins + ", hp=" + saved.save.health + ", field=" + (FrostOverlapHead(saved) != null));
                // A restored companion starts its ordinary one-second support
                // timer afresh. Isolate retired field damage from a new, valid
                // automatic attack, just as in FrostOverlapGame's setup.
                typeof(GameCore).GetField("companionAttack", frostOverlapFlags).SetValue(saved, 1000f);
                float hp = restored.hp; FrostOverlapAdvance(saved, 4.3f);
                Check(restored.hp == hp && FrostOverlapHead(saved) == null && FrostOverlapPulses(older) == 1 && FrostOverlapPulses(younger) == 1,
                    "Neither saved cast can resume its remaining four pulses after legacy reload: hp=" + restored.hp + "/" + hp
                    + ", fields=" + (FrostOverlapHead(saved) != null) + ", pulses=" + FrostOverlapPulses(older) + "/" + FrostOverlapPulses(younger)
                    + ", companion=" + saved.save.companion + ", auto=" + FrostOverlapField(saved, "companionAttack"));
            }
            GameCore reprise = RepriseGame(data, 2); RepriseLamp(reprise); Enemy warden = RepriseStart(reprise); warden.hp = 1;
            object elder = FrostOverlapInject(reprise, warden.Position, 10, new ImpactProfile(2, 0, 2, 0, 0, true));
            object junior = FrostOverlapInject(reprise, warden.Position, 20, new ImpactProfile(2, 0, 0, 0, 0, false));
            FrostOverlapTick(reprise, 8);
            Check(reprise.LastSignalReprise != null && reprise.LastSignalReprise.won && reprise.LastSignalReprise.contacts == 1
                && reprise.LastSignalReprise.damage == 1 && FrostOverlapHead(reprise) == null
                && FrostOverlapPulses(elder) == 1 && FrostOverlapPulses(junior) == 0,
                "A real Warden reprise victory clears inside the oldest Hit and immediately retires every remaining overlapping pulse");
            // EndPractice runs synchronously inside Hit/PracticeSkill. Exercise
            // that reentrancy using the real final lesson actor, not a mock list.
            GameCore practice = FrostOverlapGame(data); practice.Enter(0); practice.StartPractice();
            typeof(GameCore).GetField("practiceStage", frostOverlapFlags).SetValue(practice, 2);
            FrostOverlapCall(practice, "PracticeEnemy");
            object first, second; FrostOverlapPair(practice, out first, out second); int coinsBefore = practice.save.coins;
            FrostOverlapTick(practice, 8);
            Check(!practice.practicing && practice.Has("practice_reward") && practice.save.practiceBits == 7
                && practice.save.coins == coinsBefore + 15 && FrostOverlapHead(practice) == null,
                "The actual final player lesson contact awards once and clears all overlapping fields synchronously");
            Check(FrostOverlapPulses(first) == 1 && FrostOverlapPulses(second) == 0,
                "ClearFrost inside the oldest contact stops all remaining catch-up pulses and the younger cast immediately: oldest=" + FrostOverlapPulses(first) + ", youngest=" + FrostOverlapPulses(second));
            FrostOverlapTick(practice, 8);
            Check(FrostOverlapPulses(first) == 1 && FrostOverlapPulses(second) == 0 && FrostOverlapHead(practice) == null,
                "Returning from a contact-driven lesson end never relinks the retired pair");
            object fresh = FrostOverlapInject(practice, practice.Position, 10, new ImpactProfile(2, 0, 2, 0, 0, true));
            FrostOverlapTick(practice, 0);
            Check(FrostOverlapPulses(fresh) == 1 && FrostOverlapPulses(first) == 1 && FrostOverlapPulses(second) == 0,
                "A later deliberate fresh cast owns a new lifecycle and cannot inherit any retired contacts");
            Check(typeof(SaveData).GetField("frost") == null && typeof(EnemyRecord).GetField("frost") == null,
                "Independent active fields remain transient, without a save-schema addition");
        }
    }
}
