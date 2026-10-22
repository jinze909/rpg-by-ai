using System;
using System.Collections.Generic;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        // Live actor formations and owned equipment are explicit fixtures. Every
        // positive example earns riposte through an actual hostile contact during
        // Dash, then damages HP through the accepted basic action's release.
        private static GameCore CounterArena(Campaign data, int weapon = 0, int style = 0)
        {
            var g = Fresh(data, weapon); g.SkipTutorial(); g.save.flags.Add("core_1");
            g.save.flags.Add("weapon_1"); g.save.flags.Add("weapon_2");
            g.Enter(1); g.enemies.Clear(); g.save.x = 12; g.save.y = 14;
            g.save.reducedMotion = true; g.Equip(weapon); g.SetStyle(style);
            Check(g.CanStand(g.Position) && g.save.weapon == weapon && g.Style == style, "The explicit combat formation uses legal ground and existing loadout controls");
            return g;
        }
        private static Enemy CounterFoe(GameCore g, int id, float x, float y, float hp = 500, int kind = 0)
        {
            var enemy = new Enemy(id, kind, x, y, hp) { timer = 99 };
            g.enemies.Add(enemy); Check(g.CanStand(enemy.Position), "The isolated target stands on legal authored terrain"); return enemy;
        }
        private static void CounterEarn(GameCore g)
        {
            float health = g.save.health; g.Dash(new Point(-1, 0));
            g.projectiles.Add(new Projectile { position = g.Position, source = "shot", damage = 14, ownerId = 999801 });
            g.Tick(.01f, new Point());
            Check(g.riposteTime > 0 && g.save.health == health && g.dashTime > 0,
                "An actual hostile projectile contact during Dash earns riposte without damage");
            Advance(g, .2f); Check(g.dashTime <= 0 && g.riposteTime > 0, "The genuine counter window survives the completed dash");
        }
        private static bool CounterNumeric(Effect effect)
        {
            int amount; return effect.kind == "number" && int.TryParse(effect.text.StartsWith("! ", StringComparison.Ordinal) ? effect.text.Substring(2) : effect.text, out amount);
        }
        private static List<Effect> CounterNumbers(GameCore g)
        { return g.effects.FindAll(CounterNumeric); }
        private static void CounterUntilContact(GameCore g, Enemy target, float before)
        {
            for (int frame = 0; frame < 250 && target.hp == before; frame++) g.Tick(.01f, new Point());
            Check(target.hp < before, "A real prepared basic action reaches the live target HP");
        }
        private static ActorAction CounterAttack(GameCore g, Point direction)
        {
            g.Attack(direction); ActorAction action = g.playerAction;
            Check(action != null && action.kind == "attack" && !action.released && action.impact.counter && g.riposteTime <= 0,
                "The accepted J captures and consumes the earned counter before its contact frame");
            return action;
        }
        private static void CounterRawContact(GameCore g, Enemy enemy, float damage, ImpactProfile profile, bool technique = false)
        {
            // Explicit malformed/source-ownership boundary only. Positive player
            // coverage above and below always uses public actions and real flight.
            typeof(GameCore).GetMethod("Hit", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(g, new object[] { enemy, damage, technique, null, profile });
        }

        public static void CounterHitEarnedSwordAppearsOnlyAtRealContactAndExpires(Campaign data)
        {
            var g = CounterArena(data); Enemy enemy = CounterFoe(g, 98901, 13.1f, 14, 500); enemy.timer = .05f;
            for (int frame = 0; frame < 500 && (enemy.state != 1 || enemy.timer > .025f); frame++) g.Tick(.01f, new Point());
            Check(enemy.state == 1 && enemy.timer <= .025f && g.riposteTime <= 0, "The actual hunter AI reaches the last frames of its original wind-up");
            float health = g.save.health; g.Dash(new Point(-1, 0));
            for (int frame = 0; frame < 30 && g.dashTime > 0; frame++) g.Tick(.01f, new Point());
            Check(g.riposteTime > 0 && g.save.health == health, "A public Dash evades the hunter's real melee release and earns riposte");
            Check(g.effects.Exists(e => e.kind == "number" && e.text == g.L("闪避反击", "COUNTER") && e.variant == 0),
                "The original dodge-ready notification remains distinct from an unlanded hit");
            for (int frame = 0; frame < 100 && (g.Position - enemy.Position).Length > 1.35f; frame++) g.Tick(.01f, g.DirectionTo(g.Position, enemy.Position));
            Check(g.riposteTime > 0, "Ordinary walking returns to sword range inside the genuine counter window");
            float hp = enemy.hp; ActorAction action = CounterAttack(g, enemy.Position - g.Position);
            Check(CounterNumbers(g).Count == 0 && enemy.hp == hp, "Acceptance and the consumed window create no premature damage number");
            float clock = g.save.totalTime; g.mode = "pause"; Advance(g, 1);
            Check(g.save.totalTime == clock && !action.released && enemy.hp == hp && CounterNumbers(g).Count == 0,
                "A paused preparation neither releases damage nor invents a landed counter");
            g.Close(); CounterUntilContact(g, enemy, hp); List<Effect> numbers = CounterNumbers(g);
            Check(numbers.Count == 1 && numbers[0].variant == 1 && numbers[0].maxLife == .7f && numbers[0].life > 0,
                "Exactly the original landed damage number carries the counter presentation variant and original lifetime");
            Check(numbers[0].text == (numbers[0].text.StartsWith("! ", StringComparison.Ordinal) ? "! " : "") + ((int)(hp - enemy.hp)),
                "The visible integer and existing critical prefix still describe the original sword HP contact");
            Effect landed = numbers[0]; Advance(g, .8f);
            Check(!g.effects.Contains(landed), "The marked number uses ordinary effect expiry instead of leaving a persistent badge");
        }

        public static void CounterHitPreparedBowAndStaffRetainCapturedCounterAcrossFlightAndGear(Campaign data)
        {
            foreach (int weapon in new[] { 1, 2 })
            {
                var g = CounterArena(data, weapon, weapon == 1 ? 1 : 0); CounterEarn(g);
                // The completed dash ends at a real legal foot position. The
                // stationary formation isolates projectile ownership and blast.
                Point p = g.Position;
                Enemy a = CounterFoe(g, 98910 + weapon * 10, p.x + 5, p.y);
                Enemy b = CounterFoe(g, 98911 + weapon * 10, p.x + (weapon == 1 ? 7 : 5.7f), p.y + (weapon == 1 ? 0 : .5f));
                a.stun = b.stun = 10;
                ActorAction action = CounterAttack(g, new Point(1, 0));
                for (int frame = 0; frame < 100 && !action.released; frame++) g.Tick(.01f, new Point());
                Projectile flight = g.projectiles.Find(pj => pj.friendly && object.ReferenceEquals(pj.impact, action.impact));
                Check(action.released && flight != null && a.hp == a.maxHp && b.hp == b.maxHp && CounterNumbers(g).Count == 0,
                    "The real ranged counter releases into flight before either HP contact");
                Check(action.impact.weapon == weapon && action.impact.counter && g.riposteTime <= 0 && (weapon != 1 || flight.piercing),
                    "The arrow or starfire retains the accepted immutable counter identity after consumption");
                g.Equip(0); g.SetStyle(2); CounterUntilContact(g, b, b.maxHp);
                List<Effect> numbers = CounterNumbers(g);
                Check(a.hp < a.maxHp && b.hp < b.maxHp && numbers.Count == 2 && numbers.TrueForAll(e => e.variant == 1),
                    "Every real piercing-arrow or direct-and-splash contact carries the captured counter despite current sword gear");
                Check(g.save.weapon == 0 && g.Style == 2 && action.impact.weapon == weapon && action.impact.counter,
                    "Current equipment cannot overwrite the original launched action identity");
            }
        }

        public static void CounterHitMissesSkillsCompanionsDummyAndZeroLossStayUnmarked(Campaign data)
        {
            var plain = CounterArena(data); Enemy target = CounterFoe(plain, 98940, 13.1f, 14); plain.Attack(new Point(1, 0)); CounterUntilContact(plain, target, target.maxHp);
            Check(CounterNumbers(plain).Count == 1 && CounterNumbers(plain)[0].variant == 0, "An ordinary J damages normally without a counter label");
            var miss = CounterArena(data); CounterEarn(miss); CounterAttack(miss, new Point(-1, 0)); Advance(miss, .5f);
            Check(CounterNumbers(miss).Count == 0, "A genuinely armed accepted whiff creates no landed-counter feedback");
            var skill = CounterArena(data); CounterEarn(skill); Enemy skillTarget = CounterFoe(skill, 98941, skill.save.x + 1.1f, skill.save.y);
            skill.Skill(new Point(1, 0)); Check(skill.playerAction != null && skill.playerAction.kind == "skill", "The real Q is accepted during an earned riposte window"); CounterUntilContact(skill, skillTarget, skillTarget.maxHp);
            Check(CounterNumbers(skill).Count > 0 && CounterNumbers(skill).TrueForAll(e => e.variant == 0), "An actual class skill cannot borrow an armed basic-counter label");
            var ally = CounterArena(data); ally.save.flags.Add("companion_2"); ally.SelectCompanion(2);
            Enemy allyTarget = CounterFoe(ally, 98942, 14.5f, 14); ally.CommandCompanion(); CounterUntilContact(ally, allyTarget, allyTarget.maxHp);
            Check(CounterNumbers(ally).Count > 0 && CounterNumbers(ally).TrueForAll(e => e.variant == 0), "A real companion command retains its own unmarked HP contacts");
            var dummy = Fresh(data); dummy.SkipTutorial(); dummy.StartPractice(); Check(dummy.practicing && !dummy.SafeTrainingActive, "The original no-HP teaching dummy is active");
            CounterEarn(dummy); Enemy practiceTarget = dummy.enemies[0]; dummy.save.x = practiceTarget.x - 1; dummy.save.y = practiceTarget.y;
            float dummyHp = practiceTarget.hp; CounterAttack(dummy, new Point(1, 0)); Advance(dummy, .5f);
            Check(practiceTarget.hp == dummyHp && CounterNumbers(dummy).Count == 0, "Classic teaching records its original practice action without pretending HP was lost");
            foreach (bool range in new[] { false, true })
            {
                var training = Fresh(data); training.SkipTutorial(); training.save.flags.Add("core_1");
                WorldObject mentor = Object(training, "mentor"); training.save.x = mentor.x; training.save.y = mentor.y + 1;
                Check(range ? training.StartRange(0) : training.StartSparring(), "The original HP-bearing training session starts through its public entry");
                Enemy foe = training.enemies[0]; foe.timer = 99; CounterEarn(training);
                training.save.x = foe.x - 1; training.save.y = foe.y; float hp = foe.hp;
                CounterAttack(training, new Point(1, 0)); CounterUntilContact(training, foe, hp);
                Check(CounterNumbers(training).Count == 1 && CounterNumbers(training)[0].variant == 1,
                    "Real HP contacts retain earned counter feedback in " + (range ? "the loadout range" : "tactical sparring"));
                training.EndPractice();
            }
            foreach (string boundary in new[] { "zero", "companion-owned", "technique", "nonfinite-hp" })
            {
                var g = CounterArena(data); Enemy e = CounterFoe(g, 98943, 13.1f, 14);
                if (boundary == "nonfinite-hp") e.hp = float.PositiveInfinity;
                var profile = new ImpactProfile(0, 0, 0, 0, 0, boundary != "companion-owned", true);
                CounterRawContact(g, e, boundary == "zero" ? 0 : 5, profile, boundary == "technique");
                Check(CounterNumbers(g).Count == 1 && CounterNumbers(g)[0].variant == 0,
                    "Explicit invalid attribution/HP fixture cannot mint a badge: " + boundary);
            }
        }

        public static void CounterHitPreservesArmorCriticalLethalRewardsAndLegacySaveState(Campaign data)
        {
            foreach (bool critical in new[] { false, true }) foreach (bool lethal in new[] { false, true })
            {
                var games = new[] { CounterArena(data), CounterArena(data) }; var foes = new Enemy[2];
                int initialCoins = games[0].save.coins, initialXp = games[0].save.xp;
                for (int i = 0; i < 2; i++)
                {
                    GameCore g = games[i]; Enemy e = foes[i] = CounterFoe(g, 98950, 13.1f, 14, lethal ? 1 : 500, 2); e.facing = new Point(-1, 0); g.save.seed = critical ? 0 : 1;
                    CounterRawContact(g, e, 19, new ImpactProfile(0, 0, 0, 0, .06f, true, i == 1));
                }
                GameCore plain = games[0], marked = games[1]; List<Effect> left = CounterNumbers(plain), right = CounterNumbers(marked);
                Check(left.Count == 1 && right.Count == 1 && left[0].variant == 0 && right[0].variant == 1 && left[0].text == right[0].text,
                    "Only the presentation variant differs for identical frontal armor/critical/lethal contact inputs");
                Check(right[0].text.StartsWith("! ", StringComparison.Ordinal) == critical && marked.effects.Exists(e => e.kind == "number" && (e.text == "GUARDED" || e.text == "正面护甲") && e.variant == 0),
                    "The original critical prefix and independent frontal-armor notification remain exact");
                Check(foes[0].hp == foes[1].hp && foes[0].stagger == foes[1].stagger && plain.save.seed == marked.save.seed && plain.save.coins == marked.save.coins && plain.save.xp == marked.save.xp && plain.save.kills[1] == marked.save.kills[1] && plain.save.mastery[0] == marked.save.mastery[0],
                    "Counter presentation cannot alter damage, stagger, RNG consumption, kill rewards or mastery");
                SparringSameArray(plain.save.inventory, marked.save.inventory, "Counter-display paired contact inventory");
                int coins = marked.save.coins, xp = marked.save.xp, kills = marked.save.kills[1];
                if (lethal)
                {
                    Check(!marked.enemies.Contains(foes[1]) && kills == 1 && marked.save.coins == initialCoins + 6 && marked.save.xp == initialXp + 12, "A real lethal contact pays the original single ordinary heavy reward");
                    CounterRawContact(marked, foes[1], 19, new ImpactProfile(0, 0, 0, 0, .06f, true, true));
                    Check(marked.save.coins == coins && marked.save.xp == xp && marked.save.kills[1] == kills && CounterNumbers(marked).Count == 1, "A removed target cannot pay or emit the same landed counter twice");
                }
                marked.PrepareSave(); SaveData before = CloneSave(marked.save);
                foreach (int version in new[] { 1, 2 })
                {
                    SaveData saved = CloneSave(before); saved.version = version; var restored = new GameCore(data, saved); restored.Start();
                    Check(CounterNumbers(restored).Count == 0 && restored.riposteTime <= 0 && restored.save.coins == coins && restored.save.xp == xp && restored.save.kills[1] == kills,
                        "Legacy save version " + version + " preserves the same economy and never persists a floating badge or consumed counter");
                    Enemy surviving = restored.enemies.Find(e => e.id == 98950);
                    Check(lethal ? surviving == null : surviving != null && surviving.hp == foes[1].hp,
                        "Ordinary enemy snapshot health/death remains exact across save reload");
                }
            }
        }
    }
}
