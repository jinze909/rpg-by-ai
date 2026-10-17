using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static readonly MethodInfo heavyGuardHitMethod = typeof(GameCore).GetMethod("Hit", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly ImpactProfile heavyGuardImpact = new ImpactProfile(1, 0, 1, 0, 0, false);
        private static Enemy HeavyGuardTarget(GameCore game, int kind = 2, bool boss = false)
        {
            var enemy = new Enemy(98101, kind, 13.3f, 14, 1000, boss) { timer = 1000, staggerRecovery = 1000 };
            enemy.Face(new Point(-1, 0)); game.enemies.Clear(); game.enemies.Add(enemy); return enemy;
        }
        private static float HeavyGuardContact(GameCore game, Enemy enemy, Point approach, bool technique = false, float damage = 10)
        {
            float health = enemy.hp;
            heavyGuardHitMethod.Invoke(game, new object[] { enemy, damage, technique, (Point?)(approach * -1), heavyGuardImpact });
            return health - enemy.hp;
        }
        private static void HeavyGuardDamage(float actual, float expected, string message)
        { Check(Math.Abs(actual - expected) < .0001f, message + ": expected " + expected + ", observed " + actual); }

        public static void HeavyArmorEightHeadingsMatchExactContactSector(Campaign data)
        {
            Check(GameCore.EnemyArmorCosine == .45f, "The mature frontal threshold remains exactly .45");
            for (int heading = 0; heading < 8; heading++)
            {
                double angle = heading * Math.PI / 4;
                Point facing = new Point((float)Math.Cos(angle), (float)Math.Sin(angle));
                Point tangent = new Point(-facing.y, facing.x);
                foreach (float cosine in new[] { 1f, .4501f, .45f, .4499f, 0f, -1f })
                {
                    var game = Quiet(data); Enemy enemy = HeavyGuardTarget(game); enemy.Face(facing);
                    Point approach = facing * cosine + tangent * (float)Math.Sqrt(Math.Max(0, 1 - cosine * cosine));
                    float dot = approach.x * enemy.facing.x + approach.y * enemy.facing.y;
                    bool blocked = dot > .45f;
                    Check(GameCore.EnemyArmorBlocks(enemy, approach) == blocked,
                        "Helper follows the original unrounded dot for heading " + heading + " and cosine " + cosine);
                    HeavyGuardDamage(HeavyGuardContact(game, enemy, approach), blocked ? 4 : 10,
                        "Real Hit preserves frontal/edge/flank damage in heading " + heading);
                    Check(game.effects.Exists(effect => effect.kind == "number" && effect.text == game.L("正面护甲", "GUARDED")) == blocked,
                        "Real guarded feedback follows the same contact sector");
                }
            }
            // This cardinal basis represents the exact .45 threshold without
            // introducing rotation-rounding above or below the strict edge.
            var exact = Quiet(data); Enemy edge = HeavyGuardTarget(exact); edge.Face(new Point(1, 0));
            Point boundary = new Point(.45f, (float)Math.Sqrt(1 - .45f * .45f));
            Check(!GameCore.EnemyArmorBlocks(edge, boundary), "The exact .45 edge is open, not inclusive");
            HeavyGuardDamage(HeavyGuardContact(exact, edge, boundary), 10, "The real exact-edge contact stays unmitigated");
        }

        public static void HeavyArmorRolesStatesAndTechniquesKeepMatureDamage(Campaign data)
        {
            Check(!GameCore.EnemyHasFrontalArmor(null), "No actor has no frontal armor");
            for (int kind = 0; kind <= 3; kind++) foreach (bool boss in new[] { false, true })
                foreach (int state in new[] { 0, 1, 2 }) foreach (float stun in new[] { -.01f, 0f, .001f })
                {
                    var game = Quiet(data); Enemy enemy = HeavyGuardTarget(game, kind, boss); enemy.state = state; enemy.stun = stun;
                    bool eligible = !boss && kind == 2 && state == 0 && stun <= 0;
                    Check(GameCore.EnemyHasFrontalArmor(enemy) == eligible, "The public eligibility matches the original role/state/stun rule");
                    HeavyGuardDamage(HeavyGuardContact(game, enemy, enemy.facing), eligible ? 4 : 10,
                        "Real role/state/stun contact retains mature damage");
                }
            foreach (float health in new[] { .1f, 329f, 659f, 1000f })
            {
                var game = Quiet(data); Enemy enemy = HeavyGuardTarget(game); enemy.hp = health;
                Check(GameCore.EnemyHasFrontalArmor(enemy), "Living heavy armor does not gain an HP phase threshold");
            }
            foreach (bool reaction in new[] { false, true }) foreach (bool release in new[] { false, true })
            {
                var game = Quiet(data); Enemy enemy = HeavyGuardTarget(game); enemy.hitTime = reaction ? .18f : 0; enemy.releaseTime = release ? .24f : 0;
                Check(GameCore.EnemyHasFrontalArmor(enemy), "Hurt and post-release animation clocks do not disable existing armor");
                HeavyGuardDamage(HeavyGuardContact(game, enemy, enemy.facing), 4, "Hurt/release retains mature frontal reduction");
                HeavyGuardDamage(HeavyGuardContact(game, enemy, enemy.facing, true), 10, "Techniques retain their mature armor bypass");
            }
        }

        public static void HeavyArmorRealWindupReleaseAndBreakRestoreEligibility(Campaign data)
        {
            var game = Quiet(data); Enemy enemy = HeavyGuardTarget(game); enemy.timer = 0;
            Check(GameCore.EnemyHasFrontalArmor(enemy), "A resting heavy starts armored");
            game.Tick(.01f, new Point());
            Check(enemy.state == 1 && enemy.windupDuration == .65f && !GameCore.EnemyHasFrontalArmor(enemy),
                "Actual AI preparation opens armor for the unchanged .65-second warning");
            HeavyGuardDamage(HeavyGuardContact(game, enemy, enemy.facing), 10, "A real contact during preparation bypasses armor");
            for (int frame = 0; frame < 200 && enemy.state == 1; frame++) game.Tick(.01f, new Point());
            Check(enemy.state == 0 && enemy.releaseTime > 0 && GameCore.EnemyHasFrontalArmor(enemy),
                "Actual slam completion restores armor during its original post-release animation");
            HeavyGuardDamage(HeavyGuardContact(game, enemy, enemy.facing), 4, "A real release-recovery contact is still guarded");
            enemy.staggerRecovery = 0; enemy.stagger = 29.5f;
            HeavyGuardDamage(HeavyGuardContact(game, enemy, enemy.facing, false, 4), 1.6f, "The breaking frontal contact retains armor before stagger resolves");
            Check(enemy.state == 0 && enemy.stun == 1.7f && !GameCore.EnemyHasFrontalArmor(enemy)
                && game.effects.Exists(effect => effect.kind == "break"), "Real stagger break removes armor throughout its unchanged control window");
            HeavyGuardDamage(HeavyGuardContact(game, enemy, enemy.facing), 10, "A real contact during control deals full damage");
            for (int frame = 0; frame < 300 && enemy.stun > 0; frame++) game.Tick(.01f, new Point());
            Check(enemy.stun <= 0 && enemy.state == 0 && GameCore.EnemyHasFrontalArmor(enemy),
                "The actual stun clock restores armor before the next windup, without a new presentation timer");
            HeavyGuardDamage(HeavyGuardContact(game, enemy, enemy.facing), 4, "Recovered armor again owns real contact damage");
        }

        public static void HeavyArmorSafeDummySparringAndDefeatStayDistinct(Campaign data)
        {
            var dummy = Quiet(data); dummy.practicing = true; Enemy rig = HeavyGuardTarget(dummy); float hp = rig.hp;
            HeavyGuardDamage(HeavyGuardContact(dummy, rig, rig.facing), 0, "Safe dummy practice still exits before any armor or damage resolution");
            Check(rig.hp == hp && !dummy.effects.Exists(effect => effect.kind == "number"), "The safe rig does not emit false guard/damage feedback");
            var sparring = Fresh(data); sparring.save.flags.Add("core_1");
            WorldObject mentor = Object(sparring, "mentor"); sparring.save.x = mentor.x; sparring.save.y = mentor.y;
            Check(sparring.StartSparring(), "Real mentor access starts the original safe-health tactical course");
            sparring.enemies.Clear(); sparring.Tick(.01f, new Point());
            Check(sparring.ContinueSparring(), "The first cleared drill advances through its actual public transition");
            Enemy heavy = sparring.enemies.Find(actor => actor.kind == 2); Check(heavy != null, "The second actual drill owns its canonical heavy");
            heavy.staggerRecovery = 1000;
            HeavyGuardDamage(HeavyGuardContact(sparring, heavy, heavy.facing), 4, "Real tactical sparring retains front armor contact rules");
            float coins = sparring.save.coins, xp = sparring.save.xp; int kills = sparring.save.kills[sparring.save.room];
            HeavyGuardContact(sparring, heavy, heavy.facing * -1, true, 2000);
            Check(!sparring.enemies.Contains(heavy) && sparring.fallen.Exists(body => body.actor == heavy), "Actual lethal contact retires the heavy to its existing fallen presentation");
            Check(sparring.save.coins == coins && sparring.save.xp == xp && sparring.save.kills[sparring.save.room] == kills,
                "Defeating the drill heavy still grants no ordinary loot, experience or campaign kill credit");
        }
    }
}
