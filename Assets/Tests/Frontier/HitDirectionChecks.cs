using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore HitDirectionGame(Campaign data, int profession = 0)
        {
            var game = Quiet(data, profession); game.SkipTutorial(); game.Enter(0);
            game.enemies.Clear(); game.projectiles.Clear(); game.effects.Clear(); game.hazards.Clear();
            game.save.x = 15; game.save.y = 14; game.save.companion = -1; game.invulnerable = 0;
            game.save.difficulty = 1; game.save.talents = new int[3]; game.save.relics = new[] { -1, -1 };
            game.save.mealKind = 0; game.save.mealUntil = 0; game.save.health = game.save.maxHealth;
            Check(game.CanStand(game.Position), "Direction contacts use actual clear town collision"); return game;
        }
        private static bool HitDirectionNear(float a, float b) { return Math.Abs(a - b) < .0003f; }
        private static List<Effect> HitDirectionLabels(GameCore game)
        { return game.effects.FindAll(effect => effect.kind == "damageTaken"); }
        private static Effect HitDirectionOne(GameCore game, float before, Point incoming)
        {
            List<Effect> labels = HitDirectionLabels(game); float loss = Math.Max(0, before) - Math.Max(0, game.save.health);
            Check(labels.Count == 1 && loss > 0, "Exactly one receipt requires an actual positive clamped HP loss");
            Effect label = labels[0]; string amount = loss < .01f ? "<0.01" : loss.ToString("0.##", CultureInfo.InvariantCulture);
            Check(label.text == "-" + amount && HitDirectionNear(label.life, .7f) && HitDirectionNear(label.maxLife, .7f),
                "Direction metadata keeps the exact mature damage text and .7-second lifetime");
            Check((label.position - game.Position).Length < .0003f, "The receipt keeps the real accepted contact position");
            Check((label.direction - incoming).Length < .0003f,
                "A real loss captures the original normalized incoming travel heading; expected " + incoming.x + "," + incoming.y + ", got " + label.direction.x + "," + label.direction.y);
            return label;
        }
        private static Projectile HitDirectionShot(GameCore game, Point velocity, float damage = 17)
        {
            var shot = new Projectile { position = game.Position - velocity.Normalized * .8f, velocity = velocity,
                damage = damage, source = "shot", visual = "enemyArrow", life = 2 };
            Check(game.CanStand(shot.position), "Hostile flight starts on real unobstructed authored terrain");
            game.projectiles.Add(shot); return shot;
        }
        private static void HitDirectionContact(GameCore game, Projectile shot)
        {
            for (int frame = 0; frame < 100 && game.projectiles.Contains(shot); frame++) game.Tick(.02f, new Point());
            Check(!game.projectiles.Contains(shot), "Public Tick retires the actual hostile projectile at contact");
        }
        private static void HitDirectionBoundaryHurt(GameCore game, float damage, Point? incoming, string source = "environment")
        {
            // Reflection isolates optional/malformed incoming and explicitly
            // supplied environmental direction without changing live hazards.
            typeof(GameCore).GetMethod("Hurt", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(game, new object[] { damage, source, incoming });
        }

        public static void HitDirectionRealHostileFlightCapturesEightHeadingsAndActualMitigatedLoss(Campaign data)
        {
            Point[] headings = { new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1),
                new Point(1, 1), new Point(1, -1), new Point(-1, 1), new Point(-1, -1) };
            for (int profession = 0; profession < 3; profession++) foreach (Point heading in headings)
            {
                var game = HitDirectionGame(data, profession); float hp = game.save.health; int seed = game.save.seed;
                Projectile shot = HitDirectionShot(game, heading * 8); game.Tick(.02f, new Point());
                Check(game.projectiles.Contains(shot) && game.save.health == hp && HitDirectionLabels(game).Count == 0,
                    "A genuine approaching missile cannot announce a hit before its actual collision");
                HitDirectionContact(game, shot); Effect receipt = HitDirectionOne(game, hp, heading.Normalized);
                Check(HitDirectionNear(hp - game.save.health, 17) && game.save.seed == seed,
                    "All classes retain original hostile damage without direction feedback consuming RNG");
                Point captured = receipt.direction; game.aim = heading * -1; game.Tick(.02f, new Point(), heading * -1);
                Check((receipt.direction - captured).Length == 0, "Later aim and recoil cannot redirect the accepted hit receipt");
            }
            foreach (int difficulty in new[] { 0, 1, 2 }) foreach (bool guarded in new[] { false, true })
            {
                var game = HitDirectionGame(data); game.save.difficulty = difficulty; game.guardTime = guarded ? 2 : 0;
                game.save.talents[1] = 4; game.save.relics[0] = 5; game.save.mealKind = 2; game.save.mealUntil = game.save.totalTime + 60;
                float hp = game.save.health; Projectile shot = HitDirectionShot(game, new Point(-9, 12), 19.75f);
                HitDirectionContact(game, shot); HitDirectionOne(game, hp, new Point(-.6f, .8f));
                float expected = 19.75f * (difficulty == 0 ? .45f : difficulty == 2 ? 1.4f : 1)
                    * (guarded ? .45f : 1) * (1 - 4 * .035f) * 1.15f * .9f;
                Check(HitDirectionNear(hp - game.save.health, expected), "The receipt observes unchanged difficulty, guard, talent, relic and broth reductions");
            }
            var lethal = HitDirectionGame(data); lethal.save.health = 3.375f; int deaths = lethal.save.deaths;
            Projectile fatal = HitDirectionShot(lethal, new Point(6, -8), 999); HitDirectionContact(lethal, fatal);
            Effect last = HitDirectionOne(lethal, 3.375f, new Point(.6f, -.8f));
            Check(lethal.Dying && lethal.save.health == 0 && lethal.save.deaths == deaths + 1 && last.text == "-3.38",
                "Lethal contact retains its direction and reports only remaining HP without changing death ownership");
        }

        public static void HitDirectionProtectedAndUnlocatedContactsNeverInventCueData(Campaign data)
        {
            foreach (string boundary in new[] { "invulnerable", "dodge", "practice", "sparring", "range", "zero" })
            {
                var game = HitDirectionGame(data);
                if (boundary == "invulnerable") game.invulnerable = 1;
                if (boundary == "dodge") game.Dash(new Point(0, 1));
                if (boundary == "practice") game.StartPractice();
                if (boundary == "sparring" || boundary == "range")
                {
                    game.save.practiceBits = 7; var mentor = Array.Find(game.Map.objects, item => item.kind == "mentor");
                    Check(mentor != null, "The original Ada training host remains available"); game.save.x = mentor.x; game.save.y = mentor.y - .7f;
                    Check(boundary == "sparring" ? game.StartSparring() : game.StartRange(0), "The real safe training boundary starts");
                }
                float hp = game.save.health;
                // Start at the actual player feet so an accepted dash cannot
                // simply outrun this explicitly protected contact fixture.
                var shot = HitDirectionShot(game, new Point(8, 0), boundary == "zero" ? 0 : 17); shot.position = game.Position;
                HitDirectionContact(game, shot);
                Check(game.save.health == hp && HitDirectionLabels(game).Count == 0,
                    "No accepted HP loss produces neither numeric receipt nor direction at " + boundary);
                if (boundary == "dodge") Check(game.effects.Exists(effect => effect.kind == "number" && effect.text == game.L("闪避反击", "COUNTER")),
                    "The genuine perfect dodge keeps its mature counter feedback");
            }
            Point?[] absent = { null, new Point(), new Point(.00001f, 0), new Point(.0001f, 0), new Point(float.NaN, 1), new Point(1, float.NaN),
                new Point(float.PositiveInfinity, 1), new Point(1, float.NegativeInfinity), new Point(float.MaxValue, float.MaxValue) };
            foreach (Point? incoming in absent)
            {
                var game = HitDirectionGame(data); game.aim = new Point(0, -1); float hp = game.save.health;
                HitDirectionBoundaryHurt(game, 8, incoming); HitDirectionOne(game, hp, new Point());
                Check(HitDirectionNear(hp - game.save.health, 8), "Missing or malformed direction cannot change an accepted health transaction");
            }
            var wind = HitDirectionGame(data); float windHp = wind.save.health;
            HitDirectionBoundaryHurt(wind, 8, new Point(0, -7)); HitDirectionOne(wind, windHp, new Point(0, -1));
            Check(HitDirectionNear(windHp - wind.save.health, 8), "An explicitly supplied environmental travel direction stays truthful without a source whitelist");
        }

        public static void HitDirectionReceiptsStayTransientAndPreservePauseEconomyAndActionCosts(Campaign data)
        {
            var game = HitDirectionGame(data); game.save.flags.Add("hit_direction_story_sentinel"); game.save.coins = 314; game.save.inventory[3] = 7;
            // First ordinary Tick records the existing town exploration cell.
            // Establish that baseline before observing a combat transaction.
            game.Tick(.02f, new Point());
            int seed = game.save.seed, revision = game.revision; float energy = game.save.energy, damage = game.Damage;
            float hp = game.save.health; Projectile shot = HitDirectionShot(game, new Point(-8, 0)); HitDirectionContact(game, shot);
            Effect receipt = HitDirectionOne(game, hp, new Point(-1, 0));
            Check(game.save.coins == 314 && game.save.inventory[3] == 7 && game.save.seed == seed && game.save.energy == energy
                && game.Damage == damage && game.revision == revision, "Accepted direction feedback adds no RNG, energy, damage, inventory, currency or progression transaction"
                    + "; coins=" + game.save.coins + ", inventory=" + game.save.inventory[3] + ", seed=" + game.save.seed + "/" + seed
                    + ", energy=" + game.save.energy + "/" + energy + ", damage=" + game.Damage + "/" + damage + ", revision=" + game.revision + "/" + revision);
            Point heading = receipt.direction; float life = receipt.life, clock = game.save.totalTime;
            game.mode = "pause"; Advance(game, .4f);
            Check(receipt.life == life && game.save.totalTime == clock && (receipt.direction - heading).Length == 0,
                "Menus freeze the original receipt clock and its immutable contact heading"); game.Close();
            game.PrepareSave(); SaveData snapshot = CloneSave(game.save);
            foreach (int version in new[] { 1, 2 })
            {
                SaveData old = CloneSave(snapshot); old.version = version; var loaded = new GameCore(data, old); loaded.Start();
                Check(loaded.save.health == game.save.health && loaded.save.coins == 314 && loaded.save.inventory[3] == 7
                    && loaded.Has("hit_direction_story_sentinel") && loaded.save.seed == seed,
                    "Legacy version " + version + " preserves the real health loss, story, economy and RNG");
                Check(HitDirectionLabels(loaded).Count == 0 && loaded.effects.Count == 0 && loaded.invulnerable == 1,
                    "Legacy reload cannot resurrect a directional receipt and retains original spawn immunity");
            }
            foreach (Type schema in new[] { typeof(SaveData), typeof(EnemyRecord) })
                foreach (FieldInfo field in schema.GetFields())
                    Check(field.FieldType != typeof(Effect) && field.FieldType != typeof(List<Effect>)
                        && field.Name != "hitDirection" && field.Name != "damageTaken" && field.Name != "incoming",
                        "Direction receipts do not extend either persistent schema");
            Advance(game, .75f);
            Check(!game.effects.Contains(receipt) && HitDirectionLabels(game).Count == 0 && game.save.health == hp - 17,
                "Normal simulation expires the one original receipt without a delayed or repeated health transaction");
            game.invulnerable = 0; float before = game.save.health; HitDirectionContact(game, HitDirectionShot(game, new Point(0, 8)));
            HitDirectionOne(game, before, new Point(0, 1)); game.Enter(1);
            Check(HitDirectionLabels(game).Count == 0 && game.save.coins == 314 && game.save.inventory[3] == 7,
                "Actual map travel discards old contact direction while retaining economy");
            var actionGame = HitDirectionGame(data); actionGame.Attack(new Point(1, 0)); ActorAction action = actionGame.playerAction;
            Check(action != null && HitDirectionNear(actionGame.save.energy, 100 - actionGame.Profile(0, 0).energy),
                "Direction observation leaves the real J profile payment unchanged");
            while (actionGame.ActionTransitionBlocked) actionGame.Tick(.02f, new Point());
            actionGame.save.energy = 100; actionGame.Skill(new Point(0, -1));
            Check(actionGame.playerAction.kind == "skill" && actionGame.save.energy == 60 && actionGame.skillCooldown == 3.5f
                && actionGame.playerAction.contact == .28f && actionGame.playerAction.duration == .62f,
                "The existing paid Q preparation, contact, duration and cooldown remain original");
        }
    }
}
