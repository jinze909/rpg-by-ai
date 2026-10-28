using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static float TideForecastRead(GameCore game)
        {
            PropertyInfo property = typeof(GameCore).GetProperty("TideChangeRemaining", BindingFlags.Public | BindingFlags.Instance);
            Check(property != null && property.PropertyType == typeof(float) && property.CanRead && !property.CanWrite,
                "Harbor forecasts expose one read-only float derived from the existing world clock");
            return (float)property.GetValue(game, null);
        }
        private static GameCore TideForecastGame(Campaign data)
        {
            GameCore game = Unlocked(data); game.Enter(5); game.enemies.Clear(); game.projectiles.Clear(); game.hazards.Clear();
            game.save.companion = -1; game.invulnerable = 1000; game.save.totalTime = game.save.tideHeldUntil = 0;
            return game;
        }
        private static void TideForecastNear(float actual, float expected, string reason)
        {
            Check(!float.IsNaN(actual) && !float.IsInfinity(actual) && Math.Abs(actual - expected) < .001f,
                reason + ": expected=" + expected + ", actual=" + actual);
        }

        public static void TideForecastMatchesNaturalAndHeldBoundaries(Campaign data)
        {
            GameCore game = TideForecastGame(data);
            // Expected values are authored examples, including gate deadlines
            // that fall before, inside and exactly at the natural high phase.
            float[,] cases = {
                {0,0,22,1}, {21.75f,0,.25f,1}, {22,0,14,0}, {35.75f,0,.25f,0}, {36,0,22,1}, {58,0,14,0},
                {20,30,10,1}, {20,40,38,1}, {24,90,70,1}, {24,94,70,1}, {24,108,106,1},
                {35.75f,36,22.25f,1}, {22,22,14,0}, {21.75f,22,.25f,1}, {21.75f,21.75f,.25f,1},
                {35.75f,35.75f,.25f,0}, {10000000,0,8,0}, {10000000,10000090,102,1}
            };
            for (int i = 0; i < cases.GetLength(0); i++)
            {
                game.save.totalTime = cases[i,0]; game.save.tideHeldUntil = cases[i,1];
                Check(game.LowTide == (cases[i,3] == 1), "Authored current tide remains the original actual LowTide rule in case " + i);
                TideForecastNear(TideForecastRead(game), cases[i,2], "First effective tide transition in case " + i);
            }
            foreach (int room in new[] {0,1,2,3,4,6,7,9})
            {
                game.save.room = room; game.save.totalTime = 20; game.save.tideHeldUntil = 110;
                Check(TideForecastRead(game) == 0, "Harbor-only facts do not advertise a tide countdown in room " + room);
            }
            game.save.room = 5;
            foreach (float bad in new[] {-1f,float.NaN,float.PositiveInfinity,float.NegativeInfinity})
            {
                game.save.totalTime = bad; game.save.tideHeldUntil = 100; string before = PlanStatsState(game);
                Check(TideForecastRead(game) == 0 && PlanStatsState(game) == before,
                    "Invalid world clocks return no countdown without repairing or replacing saved state");
                game.save.totalTime = 20; game.save.tideHeldUntil = bad; before = PlanStatsState(game);
                Check(TideForecastRead(game) == 0 && PlanStatsState(game) == before,
                    "Invalid gate deadlines return no countdown without mutating the original deadline");
            }
        }

        public static void TideForecastPredictsFirstActualLowTideChange(Campaign data)
        {
            GameCore game = TideForecastGame(data); int witnessed = 0;
            foreach (float time in new[] {0f,.25f,21.75f,22f,22.25f,35.75f,36f,57.75f,58f,71.75f,120.25f,239.5f})
                foreach (float heldFor in new[] {0f,.25f,10f,22f,36f,60f,90f})
                {
                    game.save.totalTime = time; game.save.tideHeldUntil = heldFor == 0 ? 0 : time + heldFor;
                    bool initial = game.LowTide; float remaining = TideForecastRead(game);
                    Check(remaining > 0 && remaining <= 112, "Every valid phase has a bounded positive next actual transition");
                    // Observe the production predicate directly at each quarter
                    // second, rather than reproducing its formula in a test model.
                    for (float elapsed = .25f; elapsed < remaining; elapsed += .25f)
                    {
                        game.save.totalTime = time + elapsed;
                        Check(game.LowTide == initial, "No earlier actual tide change was skipped at " + time + "/" + heldFor + "/" + elapsed);
                    }
                    game.save.totalTime = time + remaining - .015625f;
                    Check(game.LowTide == initial, "Actual tide still has its old state immediately before the forecast boundary");
                    TideForecastNear(TideForecastRead(game), .015625f, "Actual countdown approaches that same boundary");
                    game.save.totalTime = time + remaining;
                    Check(game.LowTide != initial, "Actual LowTide changes at the reported effective boundary");
                    game.save.totalTime += .015625f;
                    Check(game.LowTide != initial && TideForecastRead(game) > 0,
                        "After the real boundary the new state exposes its following change without retaining zero");
                    witnessed++;
                }
            Check(witnessed == 84, "All eighty-four deterministic natural and held tide witnesses ran");
        }

        public static void TideForecastPreservesTransactionsPauseLegacyAndReadOnlyState(Campaign data)
        {
            GameCore game = TideForecastGame(data); game.save.totalTime = 24; game.save.inventory[1] = 7;
            int coins = game.save.coins; Action(game,"sluice","holdtide");
            Check(game.save.inventory[1] == 5 && game.save.coins == coins && game.save.tideHeldUntil == 114 && game.LowTide,
                "The actual sluice choice still spends exactly two crystals for its original ninety-second hold");
            TideForecastNear(TideForecastRead(game), 106, "A real ninety-second gate runs into natural low water until the actual rise at130");
            game.Close(); game.save.energy = 100; game.Attack(new Point(1,0)); ActorAction action = game.playerAction;
            Check(action != null && !action.released, "The purity fixture owns an actual accepted attack");
            SaveData owner = game.save; int[] inventory = owner.inventory;
            foreach (string mode in new[] {"world","inventory","journal","dialog","pause"})
            {
                game.mode = mode; string before = PlanStatsState(game);
                for (int repeat = 0; repeat < 8; repeat++)
                { TideForecastNear(TideForecastRead(game),106,"Repeated forecast reads stay on the same clock"); game.Conditions(); }
                Check(PlanStatsState(game) == before && ReferenceEquals(owner,game.save) && ReferenceEquals(inventory,game.save.inventory)
                    && ReferenceEquals(action,game.playerAction), "Forecast and Conditions reads retain complete save, paid action and owner in " + mode);
            }
            game.mode = "inventory"; float elapsedAction = action.elapsed; game.Tick(.05f,new Point(1,0));
            Check(game.save.totalTime == 24 && action.elapsed == elapsedAction && TideForecastRead(game) == 106,
                "Actual paused Tick cannot consume the gate or the already accepted action while the forecast is visible");
            game.Close(); game.hitStop = .1f; game.Tick(.05f,new Point());
            Check(game.save.totalTime == 24 && TideForecastRead(game) == 106, "Existing hit-stop pauses the actual tide countdown");

            GameCore exploration = TideForecastGame(data); exploration.save.totalTime = 24; exploration.save.flags.Remove("seal_5");
            if (!exploration.Has("core_2")) exploration.save.flags.Add("core_2");
            WorldObject seal = Object(exploration,"seal2"); exploration.save.x = seal.x; exploration.save.y = seal.y;
            int[] supplies = (int[])exploration.save.inventory.Clone(); exploration.UseExplorationAbility();
            Check(exploration.Has("seal_5") && exploration.save.tideHeldUntil == 84 && exploration.LowTide,
                "The actual second-core exploration ability retains its original sixty-second harbor hold");
            for (int i = 0; i < supplies.Length; i++) Check(exploration.save.inventory[i] == supplies[i], "Exploration power still spends no item " + i);
            TideForecastNear(TideForecastRead(exploration),70,"The actual exploration hold ends in natural low water, ten seconds before the next rise");

            foreach (int version in new[] {1,2})
            {
                SaveData old = CloneSave(exploration.save); old.version = version; old.coins = 91;
                old.flags.Add("legacy_tide_forecast_keep"); var loaded = new GameCore(data,old); loaded.Start(); loaded.Close();
                string before = PlanStatsState(loaded); TideForecastNear(TideForecastRead(loaded),70,"Legacy clocks derive the same effective harbor forecast"); loaded.Conditions();
                Check(PlanStatsState(loaded) == before && loaded.save.coins == 91 && loaded.Has("legacy_tide_forecast_keep") && loaded.Has("seal_5")
                    && loaded.save.totalTime == 24 && loaded.save.tideHeldUntil == 84,
                    "Existing v" + version + " clocks, completed seal and mature progress remain untouched by forecast reads");
                loaded.PrepareSave(); var restored = new GameCore(data,CloneSave(loaded.save));
                Check(TideForecastRead(restored) == 70 && restored.save.coins == 91 && restored.Has("legacy_tide_forecast_keep"),
                    "Existing save preparation/reconstruction retains the original deadlines without a forecast cache");
            }
            Check(typeof(SaveData).GetField("TideChangeRemaining") == null,
                "A derived countdown introduces no new persisted field or migration requirement");
        }
    }
}
