using System;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        public static void ControllerAimUsesFiniteRadialDirections(Campaign data)
        {
            foreach (Point sample in new[] {
                new Point(), new Point(.25f, 0), new Point(0, -.25f), new Point(.15f, .15f),
                new Point(float.NaN, 1), new Point(1, float.NaN),
                new Point(float.PositiveInfinity, 0), new Point(0, float.NegativeInfinity)
            })
                Check(GameCore.ControllerStickDirection(sample).Length == 0,
                    "The radial deadzone includes its boundary and rejects every nonfinite axis");

            foreach (Point sample in new[] {
                new Point(.25001f, 0), new Point(.2f, .2f), new Point(-.4f, .3f),
                new Point(0, -.9f), new Point(1, 1), new Point(float.MaxValue, float.MaxValue),
                new Point(float.MaxValue, -1)
            })
            {
                Point direction = GameCore.ControllerStickDirection(sample);
                double length = Math.Sqrt((double)sample.x * sample.x + (double)sample.y * sample.y);
                Point expected = new Point((float)(sample.x / length), (float)(sample.y / length));
                Check(Math.Abs(direction.Length - 1) < .00001f && (direction - expected).Length < .00001f,
                    "Active stick samples preserve their angle at unit length, including large finite readings");
            }
        }

        public static void ControllerAimSettingsRequireSafeSettingsModes(Campaign data)
        {
            var g = Quiet(data); int revision = g.revision;
            foreach (string mode in new[] { "world", "title", "dialog", "inventory", "help", "fish" })
            {
                g.mode = mode;
                Check(!g.ConfigureControllerAim(1, true) && g.save.controllerAimProfile == 0 && !g.save.controllerAimInvertY && g.revision == revision,
                    "Controller preferences cannot mutate through an unrelated gameplay or menu mode: " + mode);
            }
            foreach (string settingsMode in new[] { "pause", "controls" })
            {
                g.mode = settingsMode;
                foreach (int invalid in new[] { -1, 3, int.MinValue, int.MaxValue })
                    Check(!g.ConfigureControllerAim(invalid, true) && g.revision == revision,
                        "Invalid profile requests are rejected atomically rather than silently changing inversion");
                int profile = settingsMode == "pause" ? 1 : 2;
                Check(g.ConfigureControllerAim(profile, true) && g.save.controllerAimProfile == profile && g.save.controllerAimInvertY && g.revision == revision + 1,
                    "A real settings mode applies the selected profile and inversion together");
                revision = g.revision;
                Check(g.ConfigureControllerAim(profile, true) && g.revision == revision,
                    "Selecting the existing settings does not invent a state change");
            }
            g.deathTime = .5f;
            Check(!g.ConfigureControllerAim(0, false) && g.save.controllerAimProfile == 2 && g.save.controllerAimInvertY && g.revision == revision,
                "Settings cannot alter a dying actor even when a settings screen is open");
        }

        public static void ControllerAimLegacyAndSavedPreferencesStayCompatible(Campaign data)
        {
            foreach (int version in new[] { 1, 2 })
            {
                // The appended zero-default fields are absent from older saves.
                var legacy = new SaveData { version = version, coins = 123, stage = 1 };
                legacy.flags.Add("core_1"); legacy.flags.Add("scout_saved"); legacy.flags.Add("controller_legacy_story");
                legacy.inventory[4] = 17;
                var loaded = new GameCore(data, legacy);
                Check(loaded.save.controllerAimProfile == 0 && !loaded.save.controllerAimInvertY && loaded.save.version == 2,
                    "Both legacy save versions default to standard non-inverted aiming and retain the existing version-two migration");
                Check(loaded.save.coins == 123 && loaded.save.inventory[4] == 17 && loaded.Has("controller_legacy_story") && loaded.Has("scout_saved"),
                    "Loading appended input preferences preserves the existing inventory and story");
                foreach (int profile in new[] { 0, 1, 2 }) foreach (bool invert in new[] { false, true })
                {
                    var saved = CloneSave(loaded.save); saved.controllerAimProfile = profile; saved.controllerAimInvertY = invert;
                    var resumed = new GameCore(data, saved);
                    Check(resumed.save.controllerAimProfile == profile && resumed.save.controllerAimInvertY == invert,
                        "Every supported preference pair survives normal save reconstruction");
                }
                foreach (int invalid in new[] { -10, 3, 99, int.MaxValue })
                {
                    var malformed = CloneSave(loaded.save); malformed.controllerAimProfile = invalid; malformed.controllerAimInvertY = true;
                    var repaired = new GameCore(data, malformed);
                    Check(repaired.save.controllerAimProfile == 0 && repaired.save.controllerAimInvertY && repaired.Has("controller_legacy_story"),
                        "A malformed profile recovers to standard aiming while retaining the independent inversion preference and story");
                }
            }
        }

        public static void ControllerAimPreferencesSurviveJourneyAndRecovery(Campaign data)
        {
            foreach (int profile in new[] { 0, 1, 2 }) foreach (bool invert in new[] { false, true })
            {
                var g = Quiet(data, 1); g.mode = "pause";
                Check(g.ConfigureControllerAim(profile, invert), "A supported preference can be configured before a new journey");
                int generation = g.WorldGeneration; g.NewGame(2); g.Close();
                Check(g.save.controllerAimProfile == profile && g.save.controllerAimInvertY == invert && g.save.profession == 2,
                    "Starting another profession retains the player's input preferences");
                Check(g.WorldGeneration == generation + 1, "A new journey publishes a transient world boundary without changing the save schema");
                generation = g.WorldGeneration; g.Enter(1); g.enemies.Clear();
                Check(g.save.controllerAimProfile == profile && g.save.controllerAimInvertY == invert,
                    "Ordinary travel does not reset controller preferences");
                Check(g.WorldGeneration == generation + 1, "Ordinary travel publishes exactly one new world generation");
                SaveData identity = g.save; generation = g.WorldGeneration; g.Enter(g.save.room); g.enemies.Clear();
                Check(ReferenceEquals(g.save, identity) && g.WorldGeneration == generation + 1,
                    "Same-room travel publishes a boundary even though map ID and save identity are unchanged");
                g.PrepareSave(); generation = g.WorldGeneration; g.Load(identity); g.Start();
                Check(ReferenceEquals(g.save, identity) && g.WorldGeneration == generation + 1 && g.save.controllerAimProfile == profile && g.save.controllerAimInvertY == invert,
                    "Loading the same save instance publishes a boundary while retaining the selected preference pair");
                g.save.x = 12; g.save.y = 14; g.save.reducedMotion = true; g.invulnerable = 0; g.save.health = 1;
                g.projectiles.Add(new Projectile { position = g.Position, damage = 999, source = "shot" });
                g.Tick(.01f, new Point());
                Check(g.Dying && g.Has("recovery_pending"), "The recovery preference test uses an actual lethal projectile contact");
                g.PrepareSave(); var interrupted = new GameCore(data, CloneSave(g.save));
                Check(interrupted.save.room == 0 && interrupted.save.controllerAimProfile == profile && interrupted.save.controllerAimInvertY == invert,
                    "Loading a journey interrupted during death preserves the input preference pair");
                Advance(g, 1.1f);
                Check(!g.Dying && g.save.room == 0 && g.save.controllerAimProfile == profile && g.save.controllerAimInvertY == invert,
                    "Normal death recovery likewise preserves input preferences");
            }
        }

        public static void ControllerAimRetreatFireAndDodgeUseSimulationDirections(Campaign data)
        {
            Point right = GameCore.ControllerStickDirection(new Point(.8f, 0));
            for (int profession = 0; profession < 3; profession++)
            {
                var g = Quiet(data, profession); Point before = g.Position;
                g.Tick(.02f, new Point(-1, 0), right);
                Check(g.Position.x < before.x && g.aim.x > .999f && g.locomotionFacing.x < -.999f,
                    "A retreating actor keeps independent right-stick combat aim for every profession");
                var target = new Enemy(1950 + profession, 0, g.Position.x + (profession == 0 ? 1.4f : 4), g.Position.y, 999) { stun = 99 };
                g.enemies.Add(target); g.Attack(g.aim); ActorAction action = g.playerAction;
                Check(action != null && action.direction.x > .999f,
                    "The actual basic attack captures stick aim instead of the retreat direction");
                for (int frame = 0; frame < 80 && !action.released; frame++) g.Tick(.01f, new Point(-1, 0), right);
                Check(action.released, "Each weapon releases its real attack contact");
                if (profession == 0) Check(target.hp < 999, "The real sword contact hits the enemy on the aimed side");
                else Check(g.projectiles.Count == 1 && g.projectiles[0].velocity.x > 0 && Math.Abs(g.projectiles[0].velocity.y) < .001f,
                    "The real ranged attack launches towards stick aim while locomotion faces the retreat");
                for (int frame = 0; frame < 100 && target.hp == 999; frame++) g.Tick(.01f, new Point(), right);
                Check(target.hp < 999, "Released combat aim resolves a real hit on the enemy ahead of every equipped weapon");

                var dodge = Quiet(data, profession); before = dodge.Position; dodge.Tick(.01f, new Point(-1, 0), right); before = dodge.Position;
                dodge.Dash(new Point(-1, 0));
                Check(dodge.dashTime == .18f && dodge.dashCooldown == .75f && dodge.invulnerable == .32f && dodge.save.energy == 80,
                    "Independent aiming retains the existing dodge clocks and cost");
                dodge.Tick(.05f, new Point(-1, 0), right);
                Check(Math.Abs(dodge.Position.x - before.x + .75f) < .0001f && dodge.aim.x > .999f && dodge.VisualFacing.x < -.999f,
                    "Moving dodge follows movement, with its captured body facing, while live aim remains independent");
            }
        }

        public static void ControllerAimBufferedSkillsKeepCapturedStickIntent(Campaign data)
        {
            Point right = GameCore.ControllerStickDirection(new Point(.9f, 0));
            Point up = GameCore.ControllerStickDirection(new Point(0, -.8f));
            for (int profession = 0; profession < 3; profession++)
            {
                var g = Quiet(data, profession); g.Tick(.01f, new Point(-1, 0), right); g.Attack(g.aim);
                ActorAction attack = g.playerAction; Advance(g, attack.contact - .04f);
                g.Tick(.01f, new Point(), up); g.Skill(g.aim);
                Check(g.playerAction == attack && g.save.energy == 100 && g.skillCooldown <= 0,
                    "Q queued from a new stick direction preserves the preceding attack and does not spend early");
                for (int frame = 0; frame < 50 && g.playerAction == attack; frame++) g.Tick(.01f, new Point(-1, 0), right);
                ActorAction skill = g.playerAction;
                Check(skill != null && skill.kind == "skill" && (skill.direction - up).Length < .0001f && !skill.target.HasValue,
                    "Buffered Q keeps its original stick direction after the player aims elsewhere");
                Check(g.save.energy == 60 && g.skillCooldown == 3.5f && attack.released,
                    "Buffered stick Q releases after the old contact and pays exactly the mature cost once");
                for (int frame = 0; frame < 60 && !skill.released; frame++) g.Tick(.01f, new Point(), right);
                Check(skill.released && g.aim.x > .999f && (skill.direction - up).Length < .0001f,
                    "Changing the live aim cannot redirect a prepared Q contact");
                if (profession == 1)
                {
                    var volley = g.projectiles.FindAll(p => p.technique);
                    Check(volley.Count == 5 && volley[2].velocity.y < 0 && Math.Abs(volley[2].velocity.x) < .001f,
                        "The real queued Ranger volley retains the captured upward direction");
                }
            }
        }
    }
}
