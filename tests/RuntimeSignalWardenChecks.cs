// Production Update, OnGUI and SaveStore integration using offline Unity declarations.
// This exercises real input paths and records layout; it is not native Unity playback.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeSignalWardenChecks
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
            GUI.Export(captureFolder, "qa-signal-" + capture); GUI.commands.AddRange(commands); GUI.textureCalls.AddRange(textures); GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static void Update(FrontierRuntime app) { Call(app, "Update"); Input.keys.Clear(); }
    private static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key); Update(app); }
    private static void Position(FrontierRuntime app, Point foot)
    { Check(app.SetVisualReviewPosition(foot), "The warden integration fixture has valid hero footing"); Set(app, "camera", app.Game.Position); }
    private static void Interact(FrontierRuntime app, WorldObject target)
    { Position(app, new Point(target.x, target.y - 1)); Key(app, KeyCode.E); }
    private static WorldObject Object(GameCore g, string kind)
    { WorldObject o = Array.Find(g.Map.objects, x => x.kind == kind); Check(o != null, "Missing actual world interaction " + kind); return o; }
    private static void Choose(FrontierRuntime app, string action, string capture = null)
    {
        Check(app.Game.mode == "dialog" && app.Game.conversation != null, "The numbered warden choice belongs to a real dialogue");
        int index = Array.FindIndex(app.Game.conversation.choices, c => c.action == action);
        Check(index >= 0 && index < 5, "The warden dialogue exposes its expected keyboard choice " + action);
        int page = 0;
        do
        {
            Draw(app, capture == null ? null : capture + "-page" + page++);
            if ((bool)Call(app, "DialogueChoicesVisible")) break;
            Key(app, KeyCode.Return);
        } while (page < 20);
        Check((bool)Call(app, "DialogueChoicesVisible"), "All warden narrative pages can be read before selecting a choice");
        Key(app, (KeyCode)((int)KeyCode.Alpha1 + index));
    }
    private static void Click(FrontierRuntime app, Rect button)
    { GUI.queuedClick = button; Draw(app); Check(!GUI.queuedClick.HasValue, "The production warden menu accepts its visible button"); }
    private static void Prepare(FrontierRuntime app, bool english = true, bool large = false)
    {
        Input.Clear(); GUI.queuedClick = null; Screen.width = 1280; Screen.height = 800; Time.unscaledDeltaTime = .02f;
        app.BeginVisualReview(0, 0); app.VisualReviewPaused = false;
        GameCore g = app.Game; g.enemies.Clear(); g.save.companion = -1; g.save.reducedMotion = true; g.invulnerable = 0;
        foreach (string flag in new[] { "scout_saved", "cache_1", "watch_accept", "watch_signal_0", "watch_signal_1", "watch_signal_2", "watch_complete" })
            if (!g.Has(flag)) g.save.flags.Add(flag);
        g.save.english = english; g.save.largeText = large; g.save.showGuidance = true;
        Set(app, "applicationPaused", false); Set(app, "pointerSeen", false); Set(app, "controllerUsed", false);
        Set(app, "combatHudTime", 0f); Set(app, "journalTab", 0); Call(app, "Resize");
    }
    private static void Travel(FrontierRuntime app, int room)
    {
        Key(app, KeyCode.M); Set(app, "selectedRegion", room); Click(app, new Rect(888, 616, 306, 47));
        Check(app.Game.mode == "world" && app.Game.save.room == room, "The ordinary map travel button reaches the Warden destination");
        Set(app, "camera", app.Game.Position);
    }
    private static void Start(FrontierRuntime app, string suffix = "")
    {
        Interact(app, Object(app.Game, "watchSignal2")); Choose(app, "signal_start", "rules" + suffix);
        Check(app.Game.SignalActive && app.Game.save.trackedQuest == "signal", "Actual E and the numbered start choice begin exactly one optional trial");
    }
    private static void Views(FrontierRuntime app, string suffix)
    {
        var g = app.Game; Update(app); QuestGuide guide = g.CurrentGuide; Point? target = g.NavigationTarget();
        Check(target.HasValue && Get<Point?>(app, "routeGoal").HasValue && (target.Value - Get<Point?>(app, "routeGoal").Value).Length < .001f,
            "Production Update follows the optional Warden's current guide target");
        Check((g.Position - target.Value).Length <= 1.8f || Get<List<Point>>(app, "route").Count > 0, "The optional Warden target has a usable navigation route");
        Draw(app, "hud" + suffix);
        if (g.SignalActive)
            Check(GUI.commands.Exists(c => c.Contains("\"text\":" + GUI.S(g.L(g.CurrentBoss.name, g.CurrentBoss.en) + "  /  " + g.enemies.Find(GameCore.IsSignalWarden).BossPhase))),
                "The actual combat HUD names the optional Warden rather than the chapter guardian");
        Key(app, KeyCode.Tab); Draw(app);
        int quest = Array.IndexOf(GameCore.QuestIds, "signal"); float spacing = Math.Min(46, 464f / 18);
        string[] original = { "main", "shore", "ally1", "ally2", "ally3", "personal0", "personal1", "personal2", "bounty", "secrets", "watch", "signal" };
        Check(quest == 11 && GameCore.QuestIds.Length >= original.Length, "The Warden remains after all eleven mature quest entries when later optional stories are appended");
        for (int i = 0; i < original.Length; i++) Check(GameCore.QuestIds[i] == original[i], "The existing journal prefix retains its identity and order: " + original[i]);
        const int pageSize = 19; int pages = (GameCore.QuestIds.Length + pageSize - 1) / pageSize, checkedRows = 0;
        while (Get<int>(app, "selectedQuest") / pageSize > 0) Click(app, new Rect(650, 699, 258, 26));
        for (int page = 0; page < pages; page++)
        {
            Draw(app); int count = Math.Min(pageSize, GameCore.QuestIds.Length - page * pageSize);
            // The tracked-task toolbar shares the list width. Count only
            // task-list controls; all exact row and story assertions remain.
            var questRows = GUI.buttonCalls.FindAll(b => b.rect.x == 279 && b.rect.width == 321 && b.rect.y >= 226 && b.rect.y < 710);
            Check(questRows.Count == count, "Each visible page draws exactly its actual quest count");
            for (int cell = 0; cell < count; cell++)
            {
                int i = page * pageSize + cell; float y = 226 + cell * spacing - (cell == 18 ? 3 : 0);
                Rect current = new Rect(279, y, 321, spacing - 7);
                Check(y + spacing - 7 <= (cell == 18 ? 706 : 690) && GUI.buttonCalls.Exists(b => b.rect.x == 279 && b.rect.width == 321 && Math.Abs(b.rect.y - y) < .01f && Math.Abs(b.rect.height - (spacing - 7)) < .01f && b.enabled),
                    "Every current journal row remains visible on its own page, including the preserved Warden row");
                Click(app, current); Draw(app); Check(Get<int>(app, "selectedQuest") == i, "Every page's actual row selects its correct quest: " + i); checkedRows++;
            }
            if (page + 1 < pages) { Click(app, new Rect(927, 699, 263, 26)); Check(Get<int>(app, "selectedQuest") == (page + 1) * pageSize, "Actual next-page control exposes later stories"); }
        }
        Check(checkedRows == GameCore.QuestIds.Length, "Visible-page checks and clicks cover every current quest without dropping appended stories");
        while (Get<int>(app, "selectedQuest") / pageSize > 0) Click(app, new Rect(650, 699, 258, 26));
        Rect row = new Rect(279, 226 + quest * spacing, 321, spacing - 7); Click(app, row); Draw(app, "journal" + suffix);
        Check(Get<int>(app, "selectedQuest") == quest && GUI.commands.Exists(c => c.Contains("\"text\":" + GUI.S(g.L("下一步：", "Next: ") + guide.next))),
            "Selecting the actual Warden row displays the current story checkpoint");
        Click(app, new Rect(927, 647, 263, 31)); Check(g.mode == "map" && Get<int>(app, "selectedRegion") == guide.room,
            "The journal destination button selects the Warden's real region");
        Draw(app, "map" + suffix);
        Check(GUI.textureCalls.Exists(c => Math.Abs(c.rect.x - (888 + guide.target.x * 8.4f - 3)) < .01f && Math.Abs(c.rect.y - (264 + guide.target.y * 8.4f - 3)) < .01f && c.rect.width == 7 && c.rect.height == 7),
            "The local map draws the gold objective at the actual Warden interaction or live actor");
        Key(app, KeyCode.Escape); Check(g.mode == "world", "Closing optional trial menus restores gameplay");
    }
    private static void Fight(FrontierRuntime app)
    {
        var g = app.Game; int frames = 0, techniques = 0, dodges = 0, meals = 0;
        int stew = g.save.inventory[6]; Key(app, KeyCode.F); meals++;
        Check(g.save.inventory[6] == stew - 1 && g.save.foodUntil > g.save.totalTime,
            "The actual F shortcut consumes the starter's earned scout stew and applies its existing combat food effect");
        while (g.SignalActive && frames++ < 16000)
        {
            Check(g.save.room == 1 && !g.Dying, "The starter Vanguard must survive the actual runtime Warden encounter");
            Enemy target = g.enemies.Find(GameCore.IsSignalWarden); float distance = (target.Position - g.Position).Length;
            Point movement = distance > 1.4f ? g.DirectionTo(g.Position, target.Position) : new Point();
            if (target.state == 1 && (g.Position - target.target).Length < 3.4f)
            {
                movement = (g.Position - target.target).Length > .05f ? (g.Position - target.target).Normalized : new Point(-1, 0);
                if (g.dashCooldown <= 0) { Input.keys.Add(KeyCode.Space); dodges++; }
            }
            Input.axes["Horizontal"] = movement.x; Input.axes["Vertical"] = -movement.y; Input.held.Add(KeyCode.J);
            Set(app, "camera", g.Position); Point screen = (Point)Call(app, "ScreenPoint", target.Position);
            Input.mousePosition = new Vector3(screen.x, Screen.height - screen.y, 0);
            if (distance < 3 && g.skillCooldown <= 0 && g.save.energy >= 40) { Input.keys.Add(KeyCode.Q); techniques++; }
            if (g.save.health < g.save.maxHealth * .6f && g.save.inventory[6] > 0) { Input.keys.Add(KeyCode.F); meals++; }
            Update(app);
        }
        Input.held.Clear(); Input.axes.Clear();
        Check(frames < 16000 && !g.Dying && g.SignalComplete && techniques > 0 && dodges > 0 && meals > 0,
            "Actual held J, Q, Space and earned stew through F complete the optional trial without patched damage, health, enemy attack clocks or invulnerability");
        Console.WriteLine("  Vanguard runtime trial: " + frames + " frames; Q inputs=" + techniques + ", Space=" + dodges + ", F=" + meals + "; health=" + g.save.health);
    }
    public static void SignalStoryInputsGuidesAndVictory(FrontierRuntime app)
    {
        for (int language = 0; language < 2; language++) for (int large = 0; large < 2; large++)
        {
            string suffix = (language == 1 ? "-en" : "-zh") + (large == 1 ? "-large" : "");
            Prepare(app, language == 1, large == 1); var g = app.Game; WorldObject scout = Object(g, "returnscout");
            Interact(app, scout);
            Check(g.conversation.choices[0].action == "scoutgift" && Array.FindIndex(g.conversation.choices, c => c.action == "watch_commission") >= 0,
                "The original first scout gift and completed patrol dialogue remain available beside the new trial");
            int stew = g.save.inventory[6]; Choose(app, "scoutgift");
            Check(g.Has("scout_gift") && g.save.inventory[6] == stew + 1, "Real numbered scout thanks still grants its one-time stew");
            Interact(app, scout); Choose(app, "watch_commission", "watch-report" + suffix);
            Check(Array.FindIndex(g.conversation.choices, c => c.action == "watch_claim") < 0, "The completed patrol never exposes a duplicate reward");
            Choose(app, "signal_menu"); Choose(app, "signal_track", "commission" + suffix);
            Check(g.save.trackedQuest == "signal" && !g.SignalActive, "The scout's real dialogue tracks the new trial without spawning a remote Boss");
            Views(app, "-available" + suffix); Travel(app, 1); g.enemies.Clear(); // Isolate the authored trial from ordinary forest foes.
            Views(app, "-lamp" + suffix); Start(app, suffix); Views(app, "-active" + suffix);
            int shards = g.save.shards, points = g.save.skillPoints; Fight(app);
            Check(g.save.shards == shards + 6 && g.save.skillPoints == points + 1 && !g.Has("boss_1") && g.Has("watch_complete"),
                "Actual runtime victory automatically pays the independent optional reward without chapter progress");
            Draw(app, "victory" + suffix); Interact(app, Object(g, "watchSignal2")); Choose(app, "close", "completed" + suffix);
            Check(!g.SignalActive && g.save.shards == shards + 6 && g.save.skillPoints == points + 1,
                "Revisiting the actual completed lamp cannot duplicate the reward or respawn the Warden");
        }
        Console.WriteLine("PASS Warden actual E/numbered dialogue, preserved scout gift/patrol report, starter J/Q/Space/F combat, twelve journal rows, guide HUD/journal/map and one-time reward in both languages/text sizes; NATIVE_UNVERIFIED");
    }
    public static void SignalSaveStorePauseDeathAndRetry(FrontierRuntime app)
    {
        Prepare(app); Travel(app, 1); var g = app.Game; g.enemies.Clear(); Start(app);
        Enemy warden = g.enemies.Find(GameCore.IsSignalWarden); warden.hp = 143; g.save.flags.Add("boss_1");
        for (int frame = 0; frame < 90 && warden.state != 1; frame++) Update(app);
        Check(warden.state == 1, "The actual runtime Warden reaches its own prepared warning");
        float timer = warden.timer; Point origin = warden.attackOrigin;
        Call(app, "OnApplicationPause", true); for (int frame = 0; frame < 20; frame++) Update(app);
        Check(warden.timer == timer && (warden.attackOrigin - origin).Length < .001f, "Application pause freezes a prepared optional Boss attack");
        Call(app, "OnApplicationPause", false); g.PrepareSave(); Check(SaveStore.Write(g.save, 2), "The optional Boss uses the production save envelope");
        string warning; SaveData saved = SaveStore.Read(2, out warning); Check(saved != null && warning == null, "The actual trial JSON validates its checksum and metadata");
        g.Load(saved); g.Start(); warden = g.enemies.Find(GameCore.IsSignalWarden);
        Check(warden != null && warden.hp == 143 && warden.state == 0 && warden.timer > 0 && warden.timer <= 1.2f && g.Has("boss_1"),
            "Production reload retains the optional Boss HP even after mature chapter victory, then prepares a new safe warning");
        Draw(app, "resume"); int shards = g.save.shards, points = g.save.skillPoints;
        g.save.health = 1; g.invulnerable = 0; g.projectiles.Add(new Projectile { position = g.Position, damage = 999, source = "shot" }); Update(app);
        Check(g.Dying && !g.SignalComplete && g.save.shards == shards && g.save.skillPoints == points, "Actual runtime lethal contact cannot pay optional completion");
        Draw(app, "fallen");
        for (int frame = 0; frame < 150 && g.save.room != 0; frame++) Update(app);
        Check(g.save.room == 0 && !g.SignalActive && g.Has("signal_started") && g.Has("watch_complete") && !g.SignalComplete,
            "Actual death recovery abandons the trial while preserving accepted story and all patrol checkpoints");
        Travel(app, 1); g.enemies.Clear(); Start(app, "-retry");
        Check(g.enemies.Find(GameCore.IsSignalWarden).hp == GameCore.SignalWardenHealth && g.save.shards == shards && g.save.skillPoints == points,
            "Real lamp interaction offers a full-health retry after death without fabricated rewards");
        Key(app, KeyCode.Escape); warden = g.enemies.Find(GameCore.IsSignalWarden); timer = warden.timer;
        for (int frame = 0; frame < 30; frame++) Update(app);
        Check(g.mode == "pause" && warden.timer == timer, "The actual pause menu also freezes an optional trial"); Key(app, KeyCode.Escape);
        Console.WriteLine("PASS Warden production save envelope/JSON/checksum resume, mature guardian isolation, application/menu pause, real death recovery and full-health E/numbered retry; NATIVE_UNVERIFIED");
    }
    private static bool Contains(Rect rect, Point point)
    { return point.x >= rect.x && point.y >= rect.y && point.x <= rect.x + rect.width && point.y <= rect.y + rect.height; }
    public static void SignalStagedWarningGeometryAndOverlays(FrontierRuntime app)
    {
        // These are deliberately staged render states, distinct from the real battle above.
        for (int language = 0; language < 2; language++) for (int large = 0; large < 2; large++)
        {
            Prepare(app, language == 1, large == 1); Travel(app, 1); app.Game.enemies.Clear(); Start(app);
            Enemy e = app.Game.enemies.Find(GameCore.IsSignalWarden); Point authored = e.Position;
            for (int pattern = 0; pattern < 3; pattern++)
            {
                e.x = authored.x; e.y = authored.y; e.state = 1; e.cycle = pattern; e.attackOrigin = authored;
                e.facing = new Point(1, 0); e.target = authored + e.facing * 2.5f; e.attackDestination = authored + e.facing * 3;
                e.windupDuration = e.timer = pattern == 0 ? .95f : pattern == 1 ? 1.15f : 1.1f;
                Set(app, "camera", app.Game.Position); Rect bounds = (Rect)Call(app, "EnemyWarningBounds", e);
                Rect direct = (Rect)Call(app, "SignalWarningBounds", e);
                Check(bounds.x == direct.x && bounds.y == direct.y && bounds.width == direct.width && bounds.height == direct.height,
                    "HUD visibility protection uses the optional warning's own exact union geometry");
                var points = new List<Point>();
                if (pattern == 0) { points.Add(e.attackOrigin); points.Add(e.attackDestination); }
                else if (pattern == 1) points.AddRange(GameCore.SignalMarks(e));
                else foreach (Point direction in GameCore.SignalFanDirections(e))
                { points.Add(e.attackOrigin + direction * GameCore.SignalFanStart); points.Add(e.attackOrigin + direction * (GameCore.SignalFanStart + GameCore.SignalFanLength)); }
                foreach (Point point in points) Check(Contains(bounds, (Point)Call(app, "ScreenPoint", point)), "Every frozen authored lane endpoint lies in the warning visibility bounds");
                string suffix = "-pattern" + pattern + (language == 1 ? "-en" : "-zh") + (large == 1 ? "-large" : "");
                Draw(app, "warning" + suffix);
                string hint = (string)Call(app, "SignalMoveHint", e);
                Check(GUI.commands.Exists(c => c.Contains("\"text\":" + GUI.S(hint))), "The actual HUD explains the currently staged Warden pattern");
                GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear();
                // Stage an overlay covering the warning to exercise the actual
                // same witness redraw used by world status panels and HUD regions.
                Get<List<Rect>>(app, "worldOverlayRegions").Add(bounds);
                Call(app, "DrawHudThreatWitness");
                Check(GUI.textureCalls.Count > 40, "Warning boundary redraw survives a HUD overlay over its authored lanes");
                if (captureFolder != null) GUI.Export(captureFolder, "qa-signal-overlay" + suffix);
                Check(e.timer == e.windupDuration && (e.attackOrigin - authored).Length < .001f && e.cycle == pattern,
                    "Drawing a warning or its overlay cannot advance or retarget the real attack");
            }
        }
        Console.WriteLine("PASS Warden staged three-pattern warning geometry, live Boss name/hints and HUD overlap boundary redraw in both languages/text sizes; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous = Application.persistentDataPath; string directory = Path.Combine(Path.GetTempPath(), "starfall-signal-check-" + Guid.NewGuid());
        Application.persistentDataPath = directory;
        captureFolder = args.Length > 0 ? args[0] : null; if (captureFolder != null) Directory.CreateDirectory(captureFolder);
        var app = new FrontierRuntime(); int failed = 0;
        try
        {
            Call(app, "Awake"); Check(app.Game != null, "Actual runtime Awake loads the shipped full campaign");
            foreach (Action<FrontierRuntime> test in new Action<FrontierRuntime>[] { SignalStoryInputsGuidesAndVictory, SignalSaveStorePauseDeathAndRetry, SignalStagedWarningGeometryAndOverlays })
            {
                try { test(app); }
                catch (Exception error) { failed++; Console.WriteLine("FAIL " + test.Method.Name + ": " + (error.InnerException ?? error).Message); }
            }
            app.EndVisualReview(); Call(app, "OnDestroy");
        }
        finally { Application.persistentDataPath = previous; Input.Clear(); GUI.queuedClick = null; if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        Console.WriteLine("Runtime Signal Warden: " + (3 - failed) + "/3 groups passed; NATIVE_UNVERIFIED"); return failed == 0 ? 0 : 1;
    }
}
