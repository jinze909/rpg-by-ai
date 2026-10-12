// Production Update, OnGUI and SaveStore integration with offline Unity declarations.
// These are real input and persistence paths, not native Unity playback.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Starfall;
using UnityEngine;

public static class RuntimeKitchenChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string captureFolder;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static object Call(FrontierRuntime app, string method, params object[] args)
    { return typeof(FrontierRuntime).GetMethod(method, Private).Invoke(app, args); }
    private static void Set(FrontierRuntime app, string name, object value)
    { typeof(FrontierRuntime).GetField(name, Private).SetValue(app, value); }
    private static T Get<T>(FrontierRuntime app, string name)
    { return (T)typeof(FrontierRuntime).GetField(name, Private).GetValue(app); }
    private static void Draw(FrontierRuntime app, string capture = null)
    {
        GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app, "OnGUI");
        if (captureFolder != null && capture != null)
        {
            var commands = GUI.commands.ToArray(); var textures = GUI.textureCalls.ToArray(); var buttons = GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder, "qa-kitchen-" + capture); GUI.commands.AddRange(commands); GUI.textureCalls.AddRange(textures); GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static bool HasText(string value)
    { return GUI.commands.Exists(c => c.Contains("\"text\":" + GUI.S(value))); }
    private static Rect TextRect(string value)
    {
        string command = GUI.commands.Find(c => c.Contains("\"text\":" + GUI.S(value)));
        Check(command != null, "The actual GUI renders expected text: " + value);
        string[] coordinates = Regex.Match(command, "\"r\":\\[([^\\]]+)\\]").Groups[1].Value.Split(',');
        return new Rect(float.Parse(coordinates[0], CultureInfo.InvariantCulture), float.Parse(coordinates[1], CultureInfo.InvariantCulture),
            float.Parse(coordinates[2], CultureInfo.InvariantCulture), float.Parse(coordinates[3], CultureInfo.InvariantCulture));
    }
    private static bool Contains(Rect outer, Rect inner)
    { return outer.x <= inner.x && outer.y <= inner.y && outer.x + outer.width >= inner.x + inner.width && outer.y + outer.height >= inner.y + inner.height; }
    private static bool Overlap(Rect a, Rect b)
    { return a.x < b.x + b.width && a.x + a.width > b.x && a.y < b.y + b.height && a.y + a.height > b.y; }
    private static void Update(FrontierRuntime app) { Call(app, "Update"); Input.keys.Clear(); }
    private static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key); Update(app); }
    private static WorldObject Object(GameCore g, string kind)
    { WorldObject o = Array.Find(g.Map.objects, x => x.kind == kind); Check(o != null, "Missing actual kitchen interaction " + kind); return o; }
    private static void Position(FrontierRuntime app, Point foot)
    { Check(app.SetVisualReviewPosition(foot), "The kitchen fixture uses valid hero footing"); Set(app, "camera", app.Game.Position); }
    private static void Interact(FrontierRuntime app, string kind)
    { WorldObject o = Object(app.Game, kind); Position(app, new Point(o.x, o.y - 1)); Key(app, KeyCode.E); }
    private static void Choose(FrontierRuntime app, string action, string capture = null)
    {
        Check(app.Game.mode == "dialog" && app.Game.conversation != null, "The numbered kitchen choice belongs to an actual dialogue");
        int index = Array.FindIndex(app.Game.conversation.choices, c => c.action == action);
        Check(index >= 0 && index < 9, "A numbered kitchen choice is exposed to keyboard input: " + action);
        int page = 0;
        do
        {
            Draw(app, capture == null ? null : capture + "-page" + page++);
            if ((bool)Call(app, "DialogueChoicesVisible")) break;
            Key(app, KeyCode.Return);
        } while (page < 20);
        Check((bool)Call(app, "DialogueChoicesVisible"), "Kitchen narrative pages finish before any recipe selection");
        if (action.StartsWith("kitchen_cook_"))
            Check(HasText(app.Game.L(app.Game.conversation.text, app.Game.conversation.textEn)),
                "Recipe costs, effects, current meal and stock remain visible together when choosing a meal");
        Key(app, (KeyCode)((int)KeyCode.Alpha1 + index));
    }
    private static void Prepare(FrontierRuntime app, int room = 0, bool english = true, bool large = false)
    {
        Input.Clear(); GUI.queuedClick = null; Screen.width = 1280; Screen.height = 800; Time.unscaledDeltaTime = .02f;
        app.BeginVisualReview(0, room); app.VisualReviewPaused = false;
        GameCore g = app.Game; g.enemies.Clear(); g.save.companion = -1; g.save.reducedMotion = true; g.invulnerable = 0;
        g.save.english = english; g.save.largeText = large; g.save.showGuidance = true;
        g.save.inventory[0] = g.save.inventory[1] = g.save.inventory[2] = g.save.inventory[3] = 20;
        g.save.totalTime = 100; g.save.health = 58; g.save.energy = 42;
        Set(app, "applicationPaused", false); Set(app, "pointerSeen", false); Set(app, "controllerUsed", false);
        Set(app, "combatHudTime", 0f); Call(app, "Resize");
    }
    private static void Menu(FrontierRuntime app, string kind = "cook", string capture = null)
    { Interact(app, kind); Choose(app, "kitchen", capture); Check(app.Game.mode == "dialog", "The sixth original interaction choice opens the road kitchen"); }
    private static void Cook(FrontierRuntime app, int kind, string interaction = "cook", string capture = null)
    { Menu(app, interaction); Choose(app, "kitchen_cook_" + kind, capture); Check(app.Game.mode == "world" && app.Game.ActiveRoadMeal == kind, "The real numbered recipe prepares the selected exclusive road meal"); }
    private static void StatusViews(FrontierRuntime app, string suffix)
    {
        var g = app.Game; Set(app, "combatHudTime", 0f); Draw(app, "exploration" + suffix);
        Check(HasText(g.RoadMealStatus), "The actual exploration HUD displays the current road meal and its remaining time");
        Rect meal = (Rect)Call(app, "RoadMealHudBounds", false);
        Check((bool)Call(app, "PointerOverInterface", new Point(meal.x + meal.width / 2, meal.y + meal.height / 2)),
            "The exploration status uses the established interface pointer protection");
        // A nearby ordinary enemy selects the production combat HUD, without a
        // fabricated HUD mode or altered player statistics.
        Enemy enemy = new Enemy(77, 0, g.Position.x + 4, g.Position.y, 35); enemy.timer = 10; g.enemies.Add(enemy); Update(app);
        Check(Get<float>(app, "combatHudTime") > 0, "A real nearby enemy selects the combat HUD"); Draw(app, "combat" + suffix);
        Check(HasText(g.RoadMealStatus), "The compact combat HUD retains the meal status");
        meal = (Rect)Call(app, "RoadMealHudBounds", true);
        Check(((List<Rect>)Call(app, "HudRegions")).Exists(r => r.x == meal.x && r.y == meal.y && r.width == meal.width && r.height == meal.height),
            "Warning visibility protection knows the full compact meal panel");
        Check(!(bool)Call(app, "PointerOverInterface", new Point(meal.x + meal.width / 2, meal.y + meal.height / 2))
            && (bool)Call(app, "PointerOverInterface", new Point(640, 750)) && (bool)Call(app, "PointerOverInterface", new Point(1195, 762)),
            "The compact read-only meal passes world aim through while the original action bar and pause control remain protected");
        g.enemies.Remove(enemy); Key(app, KeyCode.I); Check(g.mode == "inventory", "The actual I shortcut opens the inventory"); Draw(app, "inventory" + suffix);
        Check(HasText(g.RoadMealStatus), "The actual inventory retains the prepared meal status");
        Check(g.toastTime > 0 && HasText(g.toast), "The actual preparation notification stays visible beside the inventory status");
        Rect status = TextRect(g.RoadMealStatus), benefit = TextRect((string)Call(app, "RoadMealBenefit")), toast = TextRect(g.toast);
        GUI.TextureCall strip = GUI.textureCalls.Find(c => c.rect.height <= 40 && c.rect.width > 400 && Contains(c.rect, status) && Contains(c.rect, benefit));
        GUI.TextureCall notification = GUI.textureCalls.Find(c => c.rect.height > 40 && c.rect.height < 90 && c.rect.width > 500 && Contains(c.rect, toast));
        Check(strip != null && notification != null && !Overlap(strip.rect, notification.rect) && !Overlap(status, toast) && !Overlap(benefit, toast),
            "The actual drawn meal strip, name and benefit remain unobscured while the preparation toast is still active");
        Key(app, KeyCode.Escape);
        Check(g.mode == "world", "Leaving the inventory restores the journey");
    }
    public static void KitchenRecipesKeyboardAndPreservedConsumables(FrontierRuntime app)
    {
        for (int language = 0; language < 2; language++) for (int large = 0; large < 2; large++)
        {
            string suffix = (language == 1 ? "-en" : "-zh") + (large == 1 ? "-large" : "");
            Prepare(app, 0, language == 1, large == 1); var g = app.Game; Interact(app, "cook");
            string[] original = { "cook", "buymeal", "buywood", "tea", "close" };
            Check(g.conversation.choices.Length == 6, "Town cooking exposes the new sixth choice alongside all five existing choices");
            for (int i = 0; i < original.Length; i++) Check(g.conversation.choices[i].action == original[i], "The original cooking choice and number are preserved: " + original[i]);
            Choose(app, "kitchen", "town-menu" + suffix); Check(g.mode == "dialog", "Actual Alpha6 enters the new recipe menu");
            int herbs = g.save.inventory[2], fish = g.save.inventory[3]; float health = g.save.health, energy = g.save.energy;
            Choose(app, "kitchen_cook_1", "recipes" + suffix);
            Check(g.mode == "world" && g.RoadMealActive && g.ActiveRoadMeal == 1 && g.RoadMealRemaining > 179.9f,
                "The actual recipe choice applies one 180-second preparation");
            Check(g.save.inventory[2] == herbs - 2 && g.save.inventory[3] == fish - 1 && g.save.health == health && g.save.energy == energy,
                "Preparing a recovery meal charges exactly two herbs and one fish without instant healing or refilling energy");
            StatusViews(app, "-recovery" + suffix);
            int stew = g.save.inventory[6]; Interact(app, "cook"); Choose(app, "cook");
            Check(g.save.inventory[6] == stew + 1 && g.ActiveRoadMeal == 1, "The original Alpha1 stew crafting works beside the road meal");
            Key(app, KeyCode.F); Check(g.save.inventory[6] == stew && g.save.foodUntil > g.save.totalTime && g.ActiveRoadMeal == 1,
                "The original F stew shortcut retains its own damage effect beside a prepared meal");
            Interact(app, "cook"); int tea = g.save.tea; Choose(app, "tea");
            Check(g.save.tea == tea + 1, "The original Alpha4 tea recipe remains usable"); Key(app, KeyCode.T);
            Check(g.save.tea == tea && g.save.teaUntil > g.save.totalTime && g.ActiveRoadMeal == 1,
                "The original T tea protection remains independent of road meals");
            Cook(app, 2, "cook", "guard-recipe" + suffix); StatusViews(app, "-guard" + suffix);
            Cook(app, 3, "cook", "travel-recipe" + suffix); StatusViews(app, "-travel" + suffix);
        }
        Console.WriteLine("PASS kitchen actual E/Alpha6/paginated recipe inputs, all three exclusive meals, original numbered stew/tea choices and F/T consumables, exploration/combat/inventory status in both languages/text sizes; NATIVE_UNVERIFIED");
    }
    public static void KitchenCampSafetyCostsAndReplacement(FrontierRuntime app)
    {
        Prepare(app, 5); var g = app.Game; Interact(app, "campfire");
        string[] original = { "fieldrest", "tea", "drinktea", "companions", "close" };
        for (int i = 0; i < original.Length; i++) Check(g.conversation.choices[i].action == original[i], "The original camp choice and number survive: " + original[i]);
        int herbs = g.save.inventory[2], fish = g.save.inventory[3]; Choose(app, "kitchen", "camp-locked");
        Check(g.mode == "world" && !g.RoadMealActive && g.save.inventory[2] == herbs && g.save.inventory[3] == fish,
            "The real camp kitchen requires a constructed waystation and charges no ingredients while locked");
        g.save.projects[1] = 1; Menu(app, "campfire", "camp-built");
        g.save.inventory[0] = 0; herbs = g.save.inventory[2]; fish = g.save.inventory[3]; Choose(app, "kitchen_cook_2", "missing-materials");
        Check(!g.RoadMealActive && g.save.inventory[2] == herbs && g.save.inventory[3] == fish,
            "A real missing-wood recipe selection cannot partially spend another ingredient");
        g.save.inventory[0] = 20; Cook(app, 1, "campfire"); Menu(app, "campfire");
        int wood = g.save.inventory[0]; fish = g.save.inventory[3]; float until = g.save.mealUntil;
        Enemy enemy = new Enemy(78, 0, g.Position.x + 1, g.Position.y, 35); g.enemies.Add(enemy); Choose(app, "kitchen_cook_2", "unsafe");
        Check(g.ActiveRoadMeal == 1 && g.save.mealUntil == until && g.save.inventory[0] == wood && g.save.inventory[3] == fish,
            "An enemy arriving while reading the real menu blocks cooking without replacing or charging the existing meal");
        g.enemies.Remove(enemy); Cook(app, 2, "campfire", "replace");
        Check(g.ActiveRoadMeal == 2 && g.save.inventory[0] == wood - 1 && g.save.inventory[3] == fish - 1 && g.RoadMealRemaining > 179.9f,
            "A safe actual camp recipe replaces the prior meal and charges only its own ingredients");
        Interact(app, "campfire"); float before = g.RoadMealRemaining; Choose(app, "fieldrest");
        Check(g.ActiveRoadMeal == 2 && Math.Abs(g.RoadMealRemaining - (before - 20)) < .001f && g.save.health == g.save.maxHealth,
            "The preserved Alpha1 camp rest advances twenty seconds of meal time and retains its normal healing");
        Prepare(app, 9); Cook(app, 3); Check(app.Game.ActiveRoadMeal == 3, "The existing Hearth Inn cook also exposes the actual road kitchen");
        Draw(app, "inn");
        Console.WriteLine("PASS kitchen actual camp waystation lock, preserved camp numbering/rest, ingredient failure without partial charge, arrival safety check, exclusive replacement and Hearth Inn E/Alpha6 access; NATIVE_UNVERIFIED");
    }
    public static void KitchenSaveStorePauseRestExpiryAndLegacy(FrontierRuntime app)
    {
        Prepare(app); var g = app.Game; Cook(app, 3); g.save.flags.Add("kitchen_runtime_story_preserved");
        float time = g.save.totalTime, until = g.save.mealUntil;
        Call(app, "OnApplicationPause", true); for (int frame = 0; frame < 60; frame++) Update(app);
        Check(g.save.totalTime == time && g.save.mealUntil == until && g.ActiveRoadMeal == 3, "Application pause freezes actual meal time");
        Call(app, "OnApplicationPause", false); Key(app, KeyCode.Escape); time = g.save.totalTime;
        for (int frame = 0; frame < 60; frame++) Update(app);
        Check(g.mode == "pause" && g.save.totalTime == time, "The real Escape pause menu freezes meal time"); Key(app, KeyCode.Escape);
        g.PrepareSave(); Check(SaveStore.Write(g.save, 0), "The production save envelope writes a prepared road meal");
        string warning; SaveData saved = SaveStore.Read(0, out warning);
        Check(saved != null && warning == null && saved.mealKind == 3 && saved.mealUntil == until,
            "The production JSON/checksum retains the selected meal and absolute expiry");
        g.Load(saved); g.Start(); Check(g.ActiveRoadMeal == 3 && g.Has("kitchen_runtime_story_preserved"), "Production load restores a meal without losing mature story flags");
        Draw(app, "save-resume"); Interact(app, "rest"); float remaining = g.RoadMealRemaining; Choose(app, "rest");
        Check(g.ActiveRoadMeal == 3 && Math.Abs(g.RoadMealRemaining - (remaining - 45)) < .001f, "The original inn rest advances exactly forty-five seconds of meal time");
        g.save.mealUntil = g.save.totalTime + .03f; Update(app); Update(app);
        Check(!g.RoadMealActive && g.ActiveRoadMeal == 0 && g.RoadMealRemaining == 0, "Actual world Update expires a road meal without requiring another menu");
        Draw(app, "expired");
        string legacy = JsonUtility.ToJson(g.save);
        legacy = Regex.Replace(legacy, ",\"mealKind\":[^,}]+", ""); legacy = Regex.Replace(legacy, ",\"mealUntil\":[^,}]+", "");
        Directory.CreateDirectory(Application.persistentDataPath); File.WriteAllText(SaveStore.PathFor(1), legacy);
        saved = SaveStore.Read(1, out warning);
        Check(saved != null && warning == null && saved.mealKind == 0 && saved.mealUntil == 0, "The actual legacy save decoder accepts absent new meal fields with neutral defaults");
        g.Load(saved); g.Start(); Check(!g.RoadMealActive && g.Has("kitchen_runtime_story_preserved"), "An old save retains story and receives no invented preparation");
        Draw(app, "legacy");
        Console.WriteLine("PASS kitchen production SaveStore JSON/envelope/checksum persistence, application/menu pause, original inn rest, real world expiry and omitted-field legacy save decoding; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous = Application.persistentDataPath; string directory = Path.Combine(Path.GetTempPath(), "starfall-kitchen-check-" + Guid.NewGuid());
        Application.persistentDataPath = directory; captureFolder = args.Length > 0 ? args[0] : null;
        if (captureFolder != null) Directory.CreateDirectory(captureFolder);
        var app = new FrontierRuntime(); int failed = 0;
        try
        {
            Call(app, "Awake"); Check(app.Game != null, "Actual runtime Awake loads the shipped full campaign");
            foreach (Action<FrontierRuntime> test in new Action<FrontierRuntime>[] { KitchenRecipesKeyboardAndPreservedConsumables, KitchenCampSafetyCostsAndReplacement, KitchenSaveStorePauseRestExpiryAndLegacy })
            {
                try { test(app); }
                catch (Exception error) { failed++; Console.WriteLine("FAIL " + test.Method.Name + ": " + (error.InnerException ?? error).Message); }
            }
            app.EndVisualReview(); Call(app, "OnDestroy");
        }
        finally { Application.persistentDataPath = previous; Input.Clear(); GUI.queuedClick = null; if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        Console.WriteLine("Runtime road kitchen: " + (3 - failed) + "/3 groups passed; NATIVE_UNVERIFIED"); return failed == 0 ? 0 : 1;
    }
}
