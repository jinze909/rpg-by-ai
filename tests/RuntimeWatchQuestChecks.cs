// Production Update, OnGUI and SaveStore integration using offline Unity declarations.
// This exercises real input paths and records layout; it is not native Unity playback.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeWatchQuestChecks
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
            GUI.Export(captureFolder, "qa-watch-" + capture); GUI.commands.AddRange(commands); GUI.textureCalls.AddRange(textures); GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static void Update(FrontierRuntime app) { Call(app, "Update"); Input.keys.Clear(); }
    private static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key); Update(app); }
    private static void Position(FrontierRuntime app, Point foot)
    { Check(app.SetVisualReviewPosition(foot), "The watch integration fixture has valid hero footing"); Set(app, "camera", app.Game.Position); }
    private static void Interact(FrontierRuntime app, WorldObject target)
    { Position(app, new Point(target.x, target.y - 1)); Key(app, KeyCode.E); }
    private static WorldObject Object(GameCore g, string kind)
    { WorldObject o = Array.Find(g.Map.objects, x => x.kind == kind); Check(o != null, "Missing actual world interaction " + kind); return o; }
    private static void Choose(FrontierRuntime app, string action, string capture = null)
    {
        Check(app.Game.mode == "dialog" && app.Game.conversation != null, "The numbered watch choice belongs to a real dialogue");
        int index = Array.FindIndex(app.Game.conversation.choices, c => c.action == action);
        Check(index >= 0 && index < 5, "The watch dialogue exposes its expected keyboard choice " + action);
        int page = 0;
        do
        {
            Draw(app, capture == null ? null : capture + "-page" + page++);
            if ((bool)Call(app, "DialogueChoicesVisible")) break;
            Key(app, KeyCode.Return);
        } while (page < 20);
        Check((bool)Call(app, "DialogueChoicesVisible"), "All watch narrative pages can be read before selecting a choice");
        Key(app, (KeyCode)((int)KeyCode.Alpha1 + index));
    }
    private static void Click(FrontierRuntime app, Rect button)
    { GUI.queuedClick = button; Draw(app); Check(!GUI.queuedClick.HasValue, "The production watch menu accepts its visible button"); }
    private static void Prepare(FrontierRuntime app, bool english = true, bool large = false)
    {
        Input.Clear(); GUI.queuedClick = null; Screen.width = 1280; Screen.height = 800; Time.unscaledDeltaTime = .02f;
        app.BeginVisualReview(0, 0); app.VisualReviewPaused = false;
        GameCore g = app.Game; g.enemies.Clear(); g.save.companion = -1; g.save.reducedMotion = true; g.invulnerable = 0;
        if (!g.Has("scout_saved")) g.save.flags.Add("scout_saved");
        g.save.english = english; g.save.largeText = large; g.save.showGuidance = true;
        Set(app, "applicationPaused", false); Set(app, "pointerSeen", false); Set(app, "controllerUsed", false);
        Set(app, "combatHudTime", 0f); Set(app, "journalTab", 0); Call(app, "Resize");
    }
    private static void Accept(FrontierRuntime app, string suffix)
    {
        WorldObject scout = Object(app.Game, "returnscout"); Interact(app, scout);
        Check(app.Game.conversation.choices[0].action == "scoutgift", "The new patrol entry preserves the original scout's first thank-you choice");
        Choose(app, "watch_commission"); Choose(app, "watch_accept", "commission" + suffix);
        Check(app.Game.WatchAccepted && app.Game.save.trackedQuest == "watch", "Actual E and numbered choices accept and track The Last Watch");
    }
    private static void Travel(FrontierRuntime app, int room)
    {
        Key(app, KeyCode.M); Set(app, "selectedRegion", room); Click(app, new Rect(888, 616, 306, 47));
        Check(app.Game.mode == "world" && app.Game.save.room == room, "The ordinary map travel button reaches the patrol's destination");
        Set(app, "camera", app.Game.Position);
    }
    private static void Views(FrontierRuntime app, string suffix)
    {
        var g = app.Game; Update(app); QuestGuide guide = g.CurrentGuide; Point? target = g.NavigationTarget();
        Check(target.HasValue && Get<Point?>(app, "routeGoal").HasValue && (target.Value - Get<Point?>(app, "routeGoal").Value).Length < .001f,
            "Production Update follows the optional patrol's active guide target");
        var route = Get<List<Point>>(app, "route");
        Check((g.Position - target.Value).Length <= 1.8f || route.Count > 0, "The next patrol interaction has a usable route");
        Draw(app, "hud" + suffix);
        // Combat HUD intentionally has less story text; the full guide remains in the journal.
        if (Get<float>(app, "combatHudTime") <= 0)
            Check(GUI.commands.Exists(c => c.Contains("\"text\":" + GUI.S(g.GuidanceHint()))), "The exploration HUD displays the patrol next step");
        Key(app, KeyCode.Tab); Draw(app);
        int quest = Array.IndexOf(GameCore.QuestIds, "watch"); float spacing = Math.Min(46, 464f / GameCore.QuestIds.Length);
        Rect row = new Rect(279, 226 + quest * spacing, 321, spacing - 7);
        Check(row.y + row.height <= 690 && GUI.buttonCalls.Exists(b => Math.Abs(b.rect.y - row.y) < .01f && Math.Abs(b.rect.height - row.height) < .01f),
            "All eleven quest rows remain visible and the patrol has a real selectable journal row");
        Click(app, row); Draw(app, "journal" + suffix);
        Check(Get<int>(app, "selectedQuest") == quest && GUI.commands.Exists(c => c.Contains("\"text\":" + GUI.S(g.L("下一步：", "Next: ") + guide.next))),
            "Selecting the actual patrol row displays the current story checkpoint");
        Click(app, new Rect(927, 647, 263, 31)); Check(g.mode == "map" && Get<int>(app, "selectedRegion") == guide.room,
            "The journal destination button selects the patrol destination's real region");
        Draw(app, "map" + suffix);
        Check(GUI.textureCalls.Exists(c => Math.Abs(c.rect.x - (888 + guide.target.x * 8.4f - 3)) < .01f && Math.Abs(c.rect.y - (264 + guide.target.y * 8.4f - 3)) < .01f && c.rect.width == 7 && c.rect.height == 7),
            "The local map draws the patrol's gold objective marker at the authored interaction");
        Key(app, KeyCode.Escape); Check(g.mode == "world", "Closing patrol menus restores gameplay");
    }
    private static void Fight(FrontierRuntime app)
    {
        var g = app.Game; int frames = 0;
        while (g.enemies.Count > 0 && frames++ < 12000)
        {
            Check(g.save.room == 1 && !g.Dying, "The starter Vanguard must survive the actual runtime patrol encounter");
            Enemy target = null; float distance = float.MaxValue;
            foreach (Enemy e in g.enemies) { float d = (e.Position - g.Position).Length; if (d < distance) { target = e; distance = d; } }
            Point movement = distance > 1.4f ? g.DirectionTo(g.Position, target.Position) : new Point();
            if (target.state == 1 && target.kind == 2 && (g.Position - target.target).Length < 2)
            { movement = (g.Position - target.target).Length > .05f ? (g.Position - target.target).Normalized : new Point(-1, 0); Input.keys.Add(KeyCode.Space); }
            Input.axes["Horizontal"] = movement.x; Input.axes["Vertical"] = -movement.y; Input.held.Add(KeyCode.J);
            Set(app, "camera", g.Position); Point screen = (Point)Call(app, "ScreenPoint", target.Position);
            Input.mousePosition = new Vector3(screen.x, Screen.height - screen.y, 0);
            if (distance < 3 && g.skillCooldown <= 0) Input.keys.Add(KeyCode.Q);
            if (g.save.health < g.save.maxHealth * .6f && g.save.inventory[6] > 0) Input.keys.Add(KeyCode.F);
            Update(app);
        }
        Input.held.Clear(); Input.axes.Clear(); Check(frames < 12000 && !g.Dying, "Public keyboard combat completes the patrol wave without patched damage, health or invulnerability");
    }
    public static void LastWatchInputsGuidesStoryAndRewards(FrontierRuntime app)
    {
        for (int language = 0; language < 2; language++) for (int large = 0; large < 2; large++)
        {
            string suffix = (language == 1 ? "-en" : "-zh") + (large == 1 ? "-large" : "");
            Prepare(app, language == 1, large == 1); Accept(app, suffix); var g = app.Game;
            Views(app, "-record" + suffix); Travel(app, 1); g.enemies.Clear();
            Position(app, new Point(7.5f, 19.5f)); Key(app, KeyCode.R);
            Check(g.Has("seal_1"), "The original R exploration power opens the existing southwest patrol record path");
            Interact(app, Object(g, "cache1")); Choose(app, "close", "record" + suffix);
            Check(g.Has("cache_1"), "Actual E reads the original cache and advances the patrol story without replacing its reward");
            for (int stage = 0; stage < 3; stage++)
            {
                Views(app, "-signal" + stage + suffix); WorldObject lantern = Object(g, "watchSignal" + stage);
                Interact(app, lantern); Choose(app, "watch_start_" + stage, "start" + stage + suffix);
                Check(g.WatchActive(stage) && g.enemies.FindAll(e => g.IsWatchEnemy(e)).Count == (stage == 0 ? 2 : 3),
                    "Actual E and the start option launch only the expected patrol wave");
                Draw(app, "pressure" + stage + suffix); Fight(app); Position(app, new Point(lantern.x, lantern.y - 1));
                for (int frame = 0; frame < 350 && g.WatchStage == stage; frame++) Update(app);
                Check(g.WatchProgress > .3f && g.WatchStage == stage, "A real runtime lantern shows intermediate channel progress");
                Draw(app, "holding" + stage + suffix);
                Check(GUI.commands.Exists(c => c.Contains("\"text\":" + GUI.S(g.WatchStatus))), "The active lantern renders its actual progress status");
                Key(app, KeyCode.Escape); float progress = g.WatchProgress; for (int frame = 0; frame < 20; frame++) Update(app);
                Check(g.mode == "pause" && g.WatchProgress == progress, "The actual Escape menu freezes lantern progress");
                Key(app, KeyCode.Escape);
                for (int frame = 0; frame < 700 && g.WatchStage == stage; frame++) Update(app);
                Check(g.WatchStage == stage + 1 && g.WatchLampLit(stage), "Runtime world time permanently restores exactly one patrol checkpoint");
                Draw(app, "lit" + stage + suffix);
            }
            Views(app, "-report" + suffix); Travel(app, 0); Interact(app, Object(g, "returnscout")); Choose(app, "watch_commission");
            int coins = g.save.coins, shards = g.save.shards, points = g.save.skillPoints;
            Choose(app, "watch_claim", "delivery" + suffix);
            Check(g.Has("watch_complete") && g.save.coins == coins + 40 && g.save.shards == shards + 4 && g.save.skillPoints == points + 1,
                "The real numbered delivery choice pays the patrol's original promised reward once");
            Interact(app, Object(g, "returnscout")); Choose(app, "watch_commission");
            Check(Array.FindIndex(g.conversation.choices, c => c.action == "watch_claim") < 0, "Revisiting the scout cannot repeat the completed reward");
            Choose(app, "close", "completed" + suffix);
        }
        Console.WriteLine("PASS Last Watch actual E/R/numbered dialogue, starter combat, eleven quest rows, guide HUD/journal/map, lantern pressure/hold, pause and final reward in both languages/text sizes; NATIVE_UNVERIFIED");
    }
    public static void LastWatchSaveStoreAndApplicationPause(FrontierRuntime app)
    {
        Prepare(app); Accept(app, "-save"); Travel(app, 1); var g = app.Game; g.enemies.Clear();
        Position(app, new Point(7.5f, 19.5f)); Key(app, KeyCode.R); Interact(app, Object(g, "cache1")); Choose(app, "close");
        WorldObject lantern = Object(g, "watchSignal0"); Interact(app, lantern); Choose(app, "watch_start_0");
        Enemy survivor = g.enemies.Find(e => e.id == 72001); survivor.hp -= 3; float hp = survivor.hp;
        foreach (Enemy e in g.enemies) { e.x = 23; e.y = 16; e.timer = 9999; }
        Position(app, new Point(lantern.x, lantern.y - 1)); for (int frame = 0; frame < 100; frame++) Update(app);
        float progress = g.WatchProgress; Check(progress > 0, "The saved patrol has an unfinished channel");
        Call(app, "OnApplicationPause", true); for (int frame = 0; frame < 20; frame++) Update(app);
        Check(g.WatchProgress == progress, "Application pause freezes the patrol's actual runtime channel"); Call(app, "OnApplicationPause", false);
        g.PrepareSave(); Check(SaveStore.Write(g.save, 1), "The patrol can be written through the production save envelope");
        string warning; SaveData saved = SaveStore.Read(1, out warning); Check(saved != null && warning == null, "The actual patrol save validates its checksum and metadata");
        g.Load(saved); g.Start(); Check(g.WatchAccepted && g.WatchStage == 0 && g.WatchActive(0) && g.WatchProgress == 0,
            "Reading a patrol save retains its accepted checkpoint and restarts its current hold");
        Check(Math.Abs(g.enemies.Find(e => e.id == 72001).hp - hp) < .001f && g.enemies.Find(e => e.id == 72001).timer <= 2,
            "A production save reload retains survivor health and promptly restores its combat behavior");
        Draw(app, "resume");
        Console.WriteLine("PASS Last Watch production save envelope/JSON/checksum reload retains checkpoint and survivor health, restores attack readiness and resets the hold; application pause freezes progress; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous = Application.persistentDataPath; string directory = Path.Combine(Path.GetTempPath(), "starfall-watch-check-" + Guid.NewGuid());
        Application.persistentDataPath = directory;
        captureFolder = args.Length > 0 ? args[0] : null; if (captureFolder != null) Directory.CreateDirectory(captureFolder);
        var app = new FrontierRuntime(); int failed = 0;
        try
        {
            Call(app, "Awake"); Check(app.Game != null, "Actual runtime Awake loads the shipped full campaign");
            foreach (Action<FrontierRuntime> test in new Action<FrontierRuntime>[] { LastWatchInputsGuidesStoryAndRewards, LastWatchSaveStoreAndApplicationPause })
            {
                try { test(app); }
                catch (Exception error) { failed++; Console.WriteLine("FAIL " + test.Method.Name + ": " + (error.InnerException ?? error).Message); }
            }
            app.EndVisualReview(); Call(app, "OnDestroy");
        }
        finally { Application.persistentDataPath = previous; Input.Clear(); GUI.queuedClick = null; if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        Console.WriteLine("Runtime watch quest: " + (2 - failed) + "/2 groups passed; NATIVE_UNVERIFIED"); return failed == 0 ? 0 : 1;
    }
}
