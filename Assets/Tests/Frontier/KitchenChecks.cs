using System;
using System.Collections.Generic;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static GameCore KitchenGame(Campaign data, int profession = 0)
        {
            var g = Fresh(data, profession); g.SkipTutorial(); g.enemies.Clear();
            WorldObject cook = Object(g, "cook"); g.save.x = cook.x; g.save.y = cook.y + 1;
            for (int i = 0; i < 5; i++) g.save.inventory[i] = 20;
            return g;
        }
        private static void KitchenChoice(GameCore g, string action)
        {
            Check(g.mode == "dialog" && g.conversation != null, "Kitchen choices require a real open conversation");
            for (int i = 0; i < g.conversation.choices.Length; i++)
                if (g.conversation.choices[i].action == action) { g.Choose(i); return; }
            throw new Exception("Missing kitchen action " + action);
        }
        private static void KitchenQuietFooting(GameCore g)
        { g.enemies.Clear(); g.projectiles.Clear(); g.hazards.Clear(); g.save.x = 10; g.save.y = 14; g.invulnerable = 0; g.save.reducedMotion = true; }

        public static void RoadKitchenAtomicRecipesAndReplacement(Campaign data)
        {
            var g = KitchenGame(data); int coins = g.save.coins, shards = g.save.shards, points = g.save.skillPoints;
            g.save.health = 50; g.save.energy = 25;
            int[] ingredients = (int[])g.save.inventory.Clone();
            Check(g.CookRoadMeal(1) && g.ActiveRoadMeal == 1 && g.RoadMealActive && Math.Abs(g.RoadMealRemaining - 180) < .001f,
                "The first recipe produces one timed preparation meal at the cook");
            Check(g.save.inventory[2] == ingredients[2] - 2 && g.save.inventory[3] == ingredients[3] - 1 &&
                g.save.inventory[0] == ingredients[0] && g.save.inventory[1] == ingredients[1] &&
                g.save.health == 50 && g.save.energy == 25 && g.save.inventory[6] == ingredients[6],
                "Herb-and-fish cooking spends only its ingredients and cannot fabricate healing, energy or existing stew");
            Advance(g, 2); float health = g.save.health, energy = g.save.energy;
            int wood = g.save.inventory[0], fish = g.save.inventory[3];
            Check(g.CookRoadMeal(2) && g.ActiveRoadMeal == 2 && g.save.inventory[0] == wood - 1 && g.save.inventory[3] == fish - 1 &&
                Math.Abs(g.RoadMealRemaining - 180) < .001f && g.save.health == health && g.save.energy == energy,
                "A different recipe replaces the active meal, pays its full cost and restarts one bounded duration");
            wood = g.save.inventory[0]; fish = g.save.inventory[3];
            Check(g.CookRoadMeal(2) && g.save.inventory[0] == wood - 1 && g.save.inventory[3] == fish - 1 && g.ActiveRoadMeal == 2,
                "Cooking the same recipe again still consumes ingredients instead of stacking or refreshing for free");
            g.save.inventory[1] = 0; ingredients = (int[])g.save.inventory.Clone(); float deadline = g.save.mealUntil;
            Check(!g.CookRoadMeal(3) && g.ActiveRoadMeal == 2 && g.save.mealUntil == deadline,
                "A missing crystal leaves the existing meal and its expiry intact");
            for (int i = 0; i < ingredients.Length; i++) Check(g.save.inventory[i] == ingredients[i], "Failed meal cooking is an atomic ingredient transaction");
            Check(!g.CookRoadMeal(0) && !g.CookRoadMeal(4) && g.save.mealUntil == deadline,
                "Out-of-range recipes cannot consume resources or invent another meal");
            g.save.inventory[1] = 1; int herbs = g.save.inventory[2]; health = g.save.health; energy = g.save.energy;
            Check(g.CookRoadMeal(3) && g.ActiveRoadMeal == 3 && g.save.inventory[2] == herbs - 2 && g.save.inventory[1] == 0 &&
                Math.Abs(g.RoadMealRemaining - 180) < .001f && g.save.health == health && g.save.energy == energy,
                "The travel recipe really spends two herbs and one crystal while replacing armor without instant recovery");
            Check(g.save.coins == coins && g.save.shards == shards && g.save.skillPoints == points,
                "Learning or replacing meals cannot pay coins, shards or talent points");
            for (int language = 0; language < 2; language++)
            {
                var recorded = KitchenGame(data); recorded.save.english = language == 1;
                Check(recorded.CookRoadMeal(1), "The recipe is learned through an actual kitchen transaction");
                recorded.PrepareSave(); var reloaded = new GameCore(data, CloneSave(recorded.save));
                JournalMoment moment = reloaded.save.moments[reloaded.save.moments.Count - 1];
                Check(moment.zh.Contains("药草鱼餐") && moment.en.Contains("Moonherb fish"),
                    "A saved recipe keeps both translations regardless of the language used when cooking");
            }
        }

        public static void RoadKitchenAccessAndMatureCooking(Campaign data)
        {
            var g = KitchenGame(data); WorldObject cook = Object(g, "cook"); g.Talk(cook);
            string[] original = { "cook", "buymeal", "buywood", "tea", "close" };
            Check(g.conversation.choices.Length == 7 && g.conversation.choices[5].action == "kitchen"
                && g.conversation.choices[6].action == "supply_prepare", "Road kitchen keeps its sixth position, followed by the separate supply preparation entry");
            for (int i = 0; i < original.Length; i++) Check(g.conversation.choices[i].action == original[i], "Existing cook choices retain their original order and action");
            KitchenChoice(g, "kitchen"); Check(g.mode == "dialog" && g.conversation.choices.Length == 4, "The optional kitchen opens its own three-recipe menu");
            KitchenChoice(g, "kitchen_cook_3"); Check(g.ActiveRoadMeal == 3, "Selecting a real recipe conversation cooks its meal");
            int stew = g.save.inventory[6]; Action(g, "cook", "cook"); Check(g.save.inventory[6] == stew + 1 && g.ActiveRoadMeal == 3,
                "The existing healing stew recipe still works without replacing the new preparation meal");
            g.Enter(9); cook = Object(g, "cook"); g.save.x = cook.x; g.save.y = cook.y + 1;
            Check(g.CookRoadMeal(1) && g.ActiveRoadMeal == 1, "The existing inn cook supports the same kitchen");
            g.save.x = 18; g.save.y = 15; int fish = g.save.inventory[3];
            Check(!g.CookRoadMeal(2) && g.save.inventory[3] == fish && g.ActiveRoadMeal == 1, "Cooking cannot be called remotely from a map without nearby cook or camp");
            g.Enter(0); cook = Object(g, "cook"); g.save.x = cook.x; g.save.y = cook.y + 1;
            foreach (string mode in new[] { "dialog", "inventory", "pause", "journal", "title" })
            { g.mode = mode; Check(!g.CookRoadMeal(2) && g.save.inventory[3] == fish, "Paused and title modes cannot cook through the public method"); }
            g.Close(); g.StartPractice(); g.save.x = cook.x; g.save.y = cook.y + 1;
            Check(!g.CookRoadMeal(2), "The practice yard cannot consume cooking ingredients"); g.EndPractice();
            var field = Unlocked(data); field.Enter(5); field.enemies.Clear(); WorldObject camp = Object(field, "campfire");
            field.save.x = camp.x; field.save.y = camp.y + 1; for (int i = 0; i < 5; i++) field.save.inventory[i] = 10;
            Check(!field.CookRoadMeal(1), "An unbuilt waystation cannot cook meals at field camps");
            field.save.projects[1] = 1; field.enemies.Add(new Enemy(945, 0, field.save.x + 2, field.save.y, 30) { timer = 9999 });
            Check(!field.CookRoadMeal(1), "A nearby live enemy closes field cooking without spending materials"); field.enemies.Clear();
            field.Talk(camp); Check(field.conversation.choices.Length == 7 && field.conversation.choices[4].action == "close" && field.conversation.choices[5].action == "kitchen"
                && field.conversation.choices[6].action == "supply_prepare", "Safe upgraded camps keep the original kitchen and append supply preparation after all six mature choices");
            KitchenChoice(field, "kitchen"); KitchenChoice(field, "kitchen_cook_1"); Check(field.ActiveRoadMeal == 1,
                "A built and safe camp cooks through its actual conversation");
        }

        public static void RoadMealsUseWorldClockAndStackWithExistingSupplies(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++)
            {
                var plain = KitchenGame(data, profession); var meal = KitchenGame(data, profession); Check(meal.CookRoadMeal(1), "Prepare the energy recipe");
                KitchenQuietFooting(plain); KitchenQuietFooting(meal); plain.save.energy = meal.save.energy = 10;
                plain.save.teaUntil = meal.save.teaUntil = 180; Advance(plain, 1); Advance(meal, 1);
                Check(Math.Abs(meal.save.energy - plain.save.energy - 3) < .002f,
                    "The actual simulation adds three energy per second to every class, alongside the existing tea recovery");
                float remaining = meal.RoadMealRemaining, time = meal.save.totalTime;
                foreach (string mode in new[] { "dialog", "inventory", "journal", "pause", "fish" })
                { meal.mode = mode; meal.Tick(.05f, new Point(1, 0)); Check(meal.save.totalTime == time && meal.RoadMealRemaining == remaining, "Paused modes and fishing do not spend the preparation meal's world time"); }
                meal.Close(); meal.hitStop = .1f; meal.Tick(.05f, new Point());
                Check(meal.save.totalTime == time && meal.RoadMealRemaining == remaining, "Existing hit-stop also freezes the meal's world clock"); meal.hitStop = 0;
                meal.save.inventory[6] = 1; meal.save.health = 30; meal.Eat();
                Check(meal.save.health == 85 && meal.save.inventory[6] == 0 && meal.save.foodUntil > meal.save.totalTime && meal.ActiveRoadMeal == 1,
                    "Healing stew keeps its original healing and attack buff without erasing the road meal");
                meal.save.tea = 1; meal.DrinkTea(); Check(meal.save.tea == 0 && meal.save.teaUntil > meal.save.totalTime && meal.ActiveRoadMeal == 1,
                    "Existing cooling tea remains independently consumable with a preparation meal");
                meal.save.totalTime = meal.save.mealUntil - .025f; meal.save.energy = 10; meal.save.teaUntil = 0; meal.Tick(.05f, new Point());
                Check(!meal.RoadMealActive && meal.ActiveRoadMeal == 0 && meal.RoadMealRemaining == 0 &&
                    Math.Abs(meal.save.energy - (10 + .05f * (profession == 2 ? 18 : 14))) < .001f,
                    "The frame crossing expiry has no extra energy recovery and reports no active meal");
            }
            var rested = KitchenGame(data); Check(rested.CookRoadMeal(1), "Prepare a meal before resting");
            Action(rested, "rest", "rest"); Check(Math.Abs(rested.RoadMealRemaining - 135) < .001f, "The existing forty-five-second rest spends exactly that much meal time");
            rested.save.stage = 3; rested.save.flags.Add("exp_intro"); rested.Enter(5); rested.enemies.Clear(); rested.save.projects[1] = 1;
            WorldObject camp = Object(rested, "campfire"); rested.save.x = camp.x; rested.save.y = camp.y + 1;
            Action(rested, "campfire", "fieldrest"); Check(Math.Abs(rested.RoadMealRemaining - 115) < .001f, "The existing twenty-second camp rest also uses the same world deadline");
        }

        private static float KitchenIncomingDamage(Campaign data, int mealKind, string source)
        {
            var g = KitchenGame(data); if (mealKind > 0) Check(g.CookRoadMeal(mealKind), "Prepare damage test recipe"); KitchenQuietFooting(g);
            float health = g.save.health;
            if (source == "slam" || source == "lunge")
            {
                var e = new Enemy(946, source == "slam" ? 2 : 0, g.save.x - 1, g.save.y, 100)
                    { state = 1, timer = .01f, windupDuration = .65f, target = g.Position, facing = new Point(1, 0) };
                e.attackOrigin = e.attackDestination = e.Position; g.enemies.Add(e);
            }
            else if (source == "area") g.hazards.Add(new Hazard { position = g.Position, radius = 1, timer = .01f, damage = 20, kind = "rift" });
            else g.projectiles.Add(new Projectile { position = g.Position, damage = 20, source = source });
            g.Tick(.02f, new Point()); float damage = health - g.save.health;
            Check(damage > 0, "The authored " + source + " contact must really hit the hero before comparing reduction");
            return damage;
        }
        public static void RoadMealArmorChangesRealCombatAndLeavesEnvironment(Campaign data)
        {
            foreach (string source in new[] { "shot", "slam", "lunge", "area" })
            {
                float plain = KitchenIncomingDamage(data, 0, source), armored = KitchenIncomingDamage(data, 2, source);
                Check(Math.Abs(armored - plain * .9f) < .001f, "The protective recipe reduces the real " + source + " contact by ten percent");
                Check(Math.Abs(KitchenIncomingDamage(data, 1, source) - plain) < .001f && Math.Abs(KitchenIncomingDamage(data, 3, source) - plain) < .001f,
                    "Other recipes cannot retain the replaced armor effect");
            }
            Check(Math.Abs(KitchenIncomingDamage(data, 2, "environment") - KitchenIncomingDamage(data, 0, "environment")) < .001f,
                "An environmental source cannot inherit combat-only meal armor");
            foreach (int kind in new[] { 0, 2 })
            {
                var climate = KitchenGame(data); if (kind > 0) Check(climate.CookRoadMeal(kind), "Prepare armor before desert exposure");
                climate.save.stage = 3; climate.save.flags.Add("exp_intro"); climate.save.flags.Add("harbor_report"); climate.Enter(6); climate.enemies.Clear();
                climate.save.x = 14; climate.save.y = 10; climate.save.heat = 100; climate.invulnerable = 0;
                float health = climate.save.health; Advance(climate, 1.1f); Check(Math.Abs(health - climate.save.health - 4) < .001f,
                    "Actual desert climate damage remains four health even while the protective meal is active");
            }
        }

        public static void RoadMealTravelMovementKeepsDashSkillAndRaceRules(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++)
            {
                var plain = KitchenGame(data, profession); var meal = KitchenGame(data, profession); Check(meal.CookRoadMeal(3), "Prepare travel recipe");
                KitchenQuietFooting(plain); KitchenQuietFooting(meal); float start = plain.save.x;
                for (int frame = 0; frame < 20; frame++) { plain.Tick(.02f, new Point(1, 0)); meal.Tick(.02f, new Point(1, 0)); }
                Check(plain.save.x > start && Math.Abs(meal.save.x - plain.save.x - .12f) < .002f,
                    "Actual free walking gains 0.3 units per second without changing any class's original base speed");
                KitchenQuietFooting(plain); KitchenQuietFooting(meal); plain.save.energy = meal.save.energy = 100;
                plain.Dash(new Point(1, 0)); meal.Dash(new Point(1, 0));
                Check(plain.save.energy == 80 && meal.save.energy == 80 && plain.dashTime == meal.dashTime && plain.dashCooldown == meal.dashCooldown,
                    "The travel meal cannot change the twenty-energy dash cost, authored duration or cooldown");
                for (int frame = 0; frame < 30 && plain.dashTime > 0; frame++) { plain.Tick(.02f, new Point()); meal.Tick(.02f, new Point()); }
                Check(plain.dashTime == 0 && meal.dashTime == 0, "Both authored dodge timers finish before testing the next technique");
                Check(Math.Abs(meal.save.x - plain.save.x) < .001f, "Actual dodge travel remains fifteen units per second for both heroes");
                plain.save.energy = meal.save.energy = 100; plain.skillCooldown = meal.skillCooldown = 0; plain.Skill(); meal.Skill();
                Check(plain.save.energy == 60 && meal.save.energy == 60 && plain.playerAction != null && meal.playerAction != null &&
                    plain.skillCooldown == meal.skillCooldown, "The preparation meal leaves the class technique's real forty-energy cost and action timing intact");
            }
            var racer = KitchenGame(data); Check(racer.CookRoadMeal(3), "Prepare travel recipe before the race");
            racer.save.stage = 3; racer.save.flags.Add("exp_intro"); racer.save.flags.Add("harbor_report"); racer.Enter(6); racer.enemies.Clear();
            racer.save.x = 14; racer.save.y = 18; float speed = racer.Speed; Action(racer, "skiff", "race");
            Check(racer.racing && Math.Abs(racer.Speed - (speed - .3f + .65f)) < .001f,
                "The existing skiff race grants only its own racing speed while temporarily suppressing travel meal speed");
            RunRace(racer); Check(racer.Has("race_finished") && !racer.racing && racer.RoadMealActive,
                "The real authored race remains completable and preserves the unexpired meal after finishing");
        }

        public static void RoadMealSaveMigrationSanitizationAndRecovery(Campaign data)
        {
            var g = KitchenGame(data); Check(g.CookRoadMeal(2), "Prepare the persistent recipe"); Advance(g, 2);
            float remaining = g.RoadMealRemaining; int fish = g.save.inventory[3]; g.PrepareSave();
            var loaded = new GameCore(data, CloneSave(g.save)); loaded.Start();
            Check(loaded.save.version == 2 && loaded.ActiveRoadMeal == 2 && Math.Abs(loaded.RoadMealRemaining - remaining) < .001f && loaded.save.inventory[3] == fish,
                "Reconstructing the actual saved fields retains one paid meal and its original expiry");
            loaded.Enter(1); loaded.enemies.Clear(); loaded.Enter(0);
            Check(loaded.ActiveRoadMeal == 2 && Math.Abs(loaded.RoadMealRemaining - remaining) < .001f, "Map travel keeps the deadline without refilling or discarding meal time");
            KitchenQuietFooting(loaded); loaded.save.health = 1; loaded.projectiles.Add(new Projectile { position = loaded.Position, damage = 999, source = "shot" }); loaded.Tick(.02f, new Point());
            Check(loaded.Dying && !loaded.CookRoadMeal(1), "A fallen hero cannot spend ingredients to replace a meal"); loaded.PrepareSave();
            var recovered = new GameCore(data, CloneSave(loaded.save)); recovered.Start();
            Check(recovered.save.room == 0 && recovered.ActiveRoadMeal == 2 && Math.Abs(recovered.RoadMealRemaining - (remaining - .02f)) < .002f && recovered.save.inventory[3] == fish,
                "Save recovery retains the paid meal's remaining world time without duplicate spending or an expiry refresh");
            foreach (int version in new[] { 1, 2 })
            {
                var old = new SaveData { version = version, coins = 91, room = 1, stage = 1, x = 5, y = 14,
                    flags = new List<string> { "intro", "scout_saved", "core_1", "watch_complete" } };
                var legacy = new GameCore(data, old); legacy.Start(); Check(legacy.save.version == 2 && legacy.save.coins == 91 && legacy.Has("watch_complete") && legacy.Has("core_1") && !legacy.RoadMealActive,
                    "Version-one and version-two saves without meal fields retain mature progress and acquire no free benefit");
            }
            foreach (SaveData bad in new[] {
                new SaveData { totalTime = 50, mealKind = -1, mealUntil = 100 },
                new SaveData { totalTime = 50, mealKind = 4, mealUntil = 100 },
                new SaveData { totalTime = 50, mealKind = 2, mealUntil = float.NaN },
                new SaveData { totalTime = 50, mealKind = 2, mealUntil = float.PositiveInfinity },
                new SaveData { totalTime = 50, mealKind = 2, mealUntil = 20 } })
            {
                var sanitized = new GameCore(data, bad); Check(!sanitized.RoadMealActive && sanitized.save.mealKind == 0 && sanitized.save.mealUntil == 0,
                    "Invalid recipe IDs, nonfinite deadlines and expired saves clear the meal without poisoning gameplay");
            }
            var bounded = new GameCore(data, new SaveData { totalTime = 50, mealKind = 1, mealUntil = 9000 });
            Check(bounded.RoadMealRemaining <= GameCore.RoadMealSeconds && bounded.save.mealUntil <= bounded.save.totalTime + GameCore.RoadMealSeconds,
                "A manipulated finite deadline cannot retain more than one authored meal duration");
            recovered.NewGame(1); recovered.Close(); Check(!recovered.RoadMealActive && recovered.save.mealKind == 0 && recovered.save.mealUntil == 0,
                "Starting a new protagonist journey clears the previous save's meal");
        }
    }
}
