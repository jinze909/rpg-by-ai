using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore ControlArena(Campaign data, int profession = 0, bool alternate = false, bool mature = false, bool sen = false)
        {
            var game = Quiet(data, profession); game.SkipTutorial(); game.save.flags.Add("core_1");
            game.save.flags.Add("weapon_1"); game.save.flags.Add("weapon_2");
            if (alternate) { game.save.flags.Add("technique_" + profession); game.LearnTechnique(); }
            if (mature) { game.save.level = 30; game.save.upgrades = 5; }
            if (sen) { game.save.flags.Add("companion_2"); game.SelectCompanion(2); }
            game.save.seed = 1;
            return game;
        }
        private static Enemy ControlTarget(GameCore game, int id, bool boss = false, float distance = 1.1f)
        {
            var enemy = new Enemy(id, 0, game.save.x + distance, game.save.y, 2000, boss) { timer = 99 };
            game.enemies.Add(enemy); Check(game.CanStand(enemy.Position), "Control target uses actual authored walkable terrain"); return enemy;
        }
        private static bool ControlNear(float a, float b) { return Math.Abs(a - b) < .0003f; }
        private static object ControlField(GameCore game)
        { return typeof(GameCore).GetField("frost", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game); }
        private static int ControlPulses(GameCore game)
        {
            object field = ControlField(game); return field == null ? 0 : (int)field.GetType().GetField("pulses").GetValue(field);
        }
        private static void ControlAdvance(GameCore game, float seconds, int pace)
        {
            int frame = 0;
            while (seconds > .00001f)
            {
                float dt = Math.Min(seconds, pace == 0 ? .05f : pace == 1 ? 1 / 60f : new[] { .013f, .05f, .027f }[frame++ % 3]);
                game.Tick(dt, new Point()); seconds -= dt;
            }
        }
        private static ActorAction ControlSkillContact(GameCore game, Enemy target, int pace = 1)
        {
            float hp = target.hp; game.Skill(new Point(1, 0)); ActorAction action = game.playerAction;
            Check(action != null && action.kind == "skill" && !action.released, "A public Q starts the original paid preparation");
            for (int frame = 0; frame < 120 && target.hp == hp; frame++) game.Tick(pace == 0 ? .05f : 1 / 60f, new Point());
            Check(action.released && target.hp < hp, "The public Q actually contacts living target HP"); return action;
        }
        private static void ControlUntilPulse(GameCore game, int pulse, int pace)
        {
            for (int frame = 0; frame < 500 && ControlPulses(game) < pulse; frame++)
                game.Tick(pace == 0 ? .05f : pace == 1 ? 1 / 60f : new[] { .013f, .05f, .027f }[frame % 3], new Point());
            Check(ControlPulses(game) == pulse, "The existing field reaches authored pulse " + pulse);
        }

        public static void ControlOverlapFrostRetainsGenuinelyEarnedBreakAcrossPulses(Campaign data)
        {
            foreach (int pace in new[] { 0, 1, 2 })
            {
                var game = ControlArena(data, 2, true, true); Enemy enemy = ControlTarget(game, 99001);
                ControlSkillContact(game, enemy, pace); Check(ControlPulses(game) == 1 && ControlNear(enemy.stun, .45f), "An isolated first frost pulse retains its authored .45-second freeze");
                Check(enemy.stagger > 0 && enemy.stagger < 30 && !game.effects.Exists(e => e.kind == "break"), "The first real HP contact accumulates stagger without an invented break");
                ControlUntilPulse(game, 2, pace); float brokeAt = game.save.totalTime;
                Check(ControlNear(enemy.stun, 1.7f) && enemy.stagger == 0 && ControlNear(enemy.staggerRecovery, 2) && game.effects.Exists(e => e.kind == "break"),
                    "The second genuine frost HP contact earns the original normal break and recovery");
                ControlUntilPulse(game, 3, pace);
                float expected = 1.7f - (game.save.totalTime - brokeAt);
                Check(expected > .8f && ControlNear(enemy.stun, expected), "A later .45-second frost pulse must retain the remaining earned break; expected " + expected + ", got " + enemy.stun);
            }
        }

        public static void ControlOverlapRealSenPrimaryAndAlternateKeepLongerWindows(Campaign data)
        {
            var sen = ControlArena(data, sen: true); Enemy inner = ControlTarget(sen, 99010); Enemy ring = ControlTarget(sen, 99011, distance: 4.5f);
            sen.CommandCompanion(); ControlAdvance(sen, .2f, 1);
            Check(sen.LastCompanionReceipt != null && sen.LastCompanionReceipt.controlCount == 2 && inner.hp < inner.maxHp && ring.hp == ring.maxHp,
                "Sen's real command controls both original radii and damages only the inner frost field");
            Check(ControlNear(inner.stun, 1.4f) && ControlNear(ring.stun, 1.4f), "Sen's same-frame first frost pulse must retain the authored 1.4-second command control; inner got " + inner.stun);

            var bossGame = ControlArena(data, mature: true); bossGame.SetStyle(1); Enemy boss = ControlTarget(bossGame, 99012, true);
            bossGame.Attack(new Point(1, 0)); ActorAction attack = bossGame.playerAction;
            for (int frame = 0; frame < 120 && !attack.released; frame++) bossGame.Tick(.01f, new Point());
            Check(boss.hp < boss.maxHp && boss.staggerRecovery > 3.9f && boss.stun > 1.08f, "A real mature heavy sword contact earns boss break without seeded stagger");
            while (bossGame.ActionTransitionBlocked) bossGame.Tick(.01f, new Point());
            float before = boss.stun, clock = bossGame.save.totalTime; ControlSkillContact(bossGame, boss);
            float remaining = before - (bossGame.save.totalTime - clock);
            Check(remaining > .5f && ControlNear(boss.stun, remaining), "Primary Q must retain the still-longer genuinely earned boss break");

            var alternate = ControlArena(data, alternate: true, mature: true); Enemy altBoss = ControlTarget(alternate, 99013, true);
            ControlSkillContact(alternate, altBoss);
            Check(altBoss.stagger == 0 && altBoss.staggerRecovery > 3.9f && alternate.effects.Exists(e => e.kind == "break") && altBoss.stun > 1.08f,
                "Alternate Vanguard's same real contact must keep the newly earned 1.1-second boss break above its authored 1.0-second control");

            var layered = ControlArena(data, alternate: true, sen: true); Enemy normal = ControlTarget(layered, 99014);
            ControlSkillContact(layered, normal); float longWindow = normal.stun, at = layered.save.totalTime;
            layered.CommandCompanion(); ControlAdvance(layered, .2f, 1);
            Check(ControlNear(normal.stun, longWindow - (layered.save.totalTime - at)) && normal.stun > 1.9f,
                "Sen command and its real first frost contact retain an already earned 2.2-second alternate Vanguard window");
        }

        public static void ControlOverlapNeverAddsTimeAndHonorsPauseExpiryWallsAndFivePulses(Campaign data)
        {
            foreach (int pace in new[] { 0, 1, 2 })
            {
                var game = ControlArena(data, alternate: true); Enemy enemy = ControlTarget(game, 99020);
                ControlSkillContact(game, enemy, pace); float remaining = enemy.stun;
                Check(remaining > 2.14f && remaining <= 2.2f && enemy.staggerRecovery <= 0, "An isolated alternate Q keeps the original normal 2.2-second duration without addition");
                float timer = enemy.timer, clock = game.save.totalTime; game.mode = "pause"; ControlAdvance(game, 1, pace);
                Check(enemy.stun == remaining && enemy.timer == timer && game.save.totalTime == clock, "Pause freezes control and AI clocks"); game.Close();
                ControlAdvance(game, remaining - .08f, pace); Check(enemy.stun > 0 && enemy.timer == timer, "The AI remains asleep for the full longer window");
                ControlAdvance(game, .18f, pace); Check(enemy.stun <= 0 && enemy.timer < timer, "AI resumes its original timer when the longer window expires");

                var fieldGame = ControlArena(data, 2, true); Enemy fieldEnemy = ControlTarget(fieldGame, 99021); fieldEnemy.stun = 8; fieldEnemy.staggerRecovery = 99;
                // Explicit edge fixture isolates non-additive composition from new breaks.
                ActorAction action = ControlSkillContact(fieldGame, fieldEnemy, pace); object field = ControlField(fieldGame); float began = fieldGame.save.totalTime;
                float firstRemaining = fieldEnemy.stun; Check(ControlNear(firstRemaining, 8 - began), "The first short pulse neither replaces nor adds to existing remaining time");
                ControlUntilPulse(fieldGame, 5, pace);
                Check(ControlNear(fieldEnemy.stun, firstRemaining - (fieldGame.save.totalTime - began)), "Repeated pulses never stack control seconds additively");
                float hp = fieldEnemy.hp;
                Check((int)field.GetType().GetField("pulses").GetValue(field) == 5 && action.impact.playerOwned, "The actual original field owns exactly five pulses");
                ControlAdvance(fieldGame, 1.5f, pace);
                Check(ControlField(fieldGame) == null && fieldEnemy.hp == hp, "Field expiry creates no sixth contact or extra control pulse");
            }
            var wall = ControlArena(data, 2, true); Point a = new Point(), b = new Point(); bool found = false;
            for (int y = 2; y < wall.Map.rows.Length - 2 && !found; y++) for (int x = 2; x < wall.Map.rows[y].Length - 3 && !found; x++)
            {
                a = new Point(x + .5f, y + .5f); b = new Point(x + 2.5f, y + .5f);
                found = wall.CanStand(a) && wall.CanStand(b) && !(bool)typeof(GameCore).GetMethod("ClearAttackPath", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(wall, new object[] { a, b });
            }
            Check(found, "The shipped map supplies two legal nearby feet separated by a real wall"); wall.save.x = a.x; wall.save.y = a.y;
            var occluded = new Enemy(99022, 0, b.x, b.y, 2000) { stun = 8, timer = 99 }; wall.enemies.Add(occluded);
            wall.Skill(new Point(1, 0)); ControlAdvance(wall, 3.6f, 1);
            Check(occluded.hp == occluded.maxHp && ControlNear(occluded.stun, 4.4f), "Actual wall collision blocks all five nearby field contacts and leaves only the original countdown");
        }

        public static void ControlOverlapPreservesSaveTransienceDamageCostsAndImpactOwnership(Campaign data)
        {
            var game = ControlArena(data, 2, true); Enemy enemy = ControlTarget(game, 99030);
            float energy = game.save.energy; game.Skill(new Point(1, 0)); ActorAction action = game.playerAction;
            Check(ControlNear(game.save.energy, energy - 40) && game.skillCooldown == 3.5f && action.contact == .28f && action.duration == .62f,
                "Control composition preserves accepted skill cost, cooldown, preparation and action duration");
            while (!action.released) game.Tick(.01f, new Point());
            Check(ControlNear(enemy.maxHp - enemy.hp, action.damage * .4f), "A real noncritical pulse retains the original damage multiplier");
            object field = ControlField(game); var captured = (ImpactProfile)field.GetType().GetField("impact").GetValue(field);
            game.Equip(0); game.save.upgrades = 5;
            Check(object.ReferenceEquals(captured, action.impact) && captured.weapon == 2 && captured.masteryWeapon == 2 && captured.playerOwned,
                "The existing field keeps the accepted player's weapon and ownership across live equipment changes");
            float hp = enemy.hp; ControlUntilPulse(game, 2, 1);
            Check(ControlNear(hp - enemy.hp, action.damage * .4f), "A later pulse uses captured damage rather than current improved sword stats");
            game.save.coins = 91; game.PrepareSave(); SaveData snapshot = CloneSave(game.save);
            foreach (int version in new[] { 1, 2 })
            {
                SaveData old = CloneSave(snapshot); old.version = version; var loaded = new GameCore(data, old); loaded.Start();
                Enemy restored = loaded.enemies.Find(e => e.id == enemy.id);
                Check(restored != null && restored.hp == enemy.hp && loaded.save.coins == 91 && loaded.save.upgrades == 5,
                    "Legacy version " + version + " retains real damaged HP, economy and progression");
                Check(restored.stun == 0 && restored.stagger == 0 && restored.staggerRecovery == 0 && restored.state == 0 && ControlField(loaded) == null && loaded.playerAction == null && loaded.skillCooldown == 0,
                    "Legacy reload restores no control window, stagger, active field, action or cooldown");
                float restoredHp = restored.hp; loaded.save.x = restored.x + 8; ControlAdvance(loaded, 4.2f, 1);
                Check(restored.hp == restoredHp && ControlField(loaded) == null, "A saved field cannot resume damaging actors after reload");
            }
            foreach (string transient in new[] { "stun", "stagger", "staggerRecovery", "frost", "controlRemaining" })
                Check(typeof(SaveData).GetField(transient) == null && typeof(EnemyRecord).GetField(transient) == null,
                    "Existing save schemas do not acquire transient combat field " + transient);

            // Existing Hit must also keep a longer unrelated control window when
            // this isolated stagger-boundary fixture earns a real new break.
            var edge = ControlArena(data); Enemy edgeEnemy = ControlTarget(edge, 99031); edgeEnemy.stun = 4; edgeEnemy.stagger = 29;
            edge.Attack(new Point(1, 0)); ActorAction edgeAction = edge.playerAction; float edgeClock = edge.save.totalTime;
            while (!edgeAction.released) edge.Tick(.01f, new Point());
            Check(edgeEnemy.hp < edgeEnemy.maxHp && edgeEnemy.stagger == 0 && edgeEnemy.staggerRecovery > 1.9f && ControlNear(edgeEnemy.stun, 4 - (edge.save.totalTime - edgeClock)),
                "A real HP contact at the isolated stagger boundary never shortens preexisting longer control to break's 1.7 seconds");
        }
    }
}
