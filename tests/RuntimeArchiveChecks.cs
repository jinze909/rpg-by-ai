// Production Update, OnGUI and SaveStore paths compiled with offline Unity declarations.
// Real input/persistence evidence and staged GUI evidence remain NATIVE_UNVERIFIED.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeArchiveChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string captureFolder;
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static object Call(FrontierRuntime app, string method, params object[] args)
    { return typeof(FrontierRuntime).GetMethod(method, Private).Invoke(app, args); }
    private static void Set(FrontierRuntime app, string field, object value)
    { typeof(FrontierRuntime).GetField(field, Private).SetValue(app, value); }
    private static T Get<T>(FrontierRuntime app, string field)
    { return (T)typeof(FrontierRuntime).GetField(field, Private).GetValue(app); }
    private static void Draw(FrontierRuntime app, string capture = null)
    {
        GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app, "OnGUI");
        if (captureFolder != null && capture != null)
        {
            var commands = GUI.commands.ToArray(); var textures = GUI.textureCalls.ToArray(); var buttons = GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder, "qa-archive-" + capture);
            GUI.commands.AddRange(commands); GUI.textureCalls.AddRange(textures); GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static bool HasText(string text) { return GUI.commands.Exists(c => c.Contains("\"text\":" + GUI.S(text))); }
    private static double LinearChannel(float value)
    { return value <= .04045f ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4); }
    private static double Luminance(Color color)
    { return .2126 * LinearChannel(color.r) + .7152 * LinearChannel(color.g) + .0722 * LinearChannel(color.b); }
    private static void Update(FrontierRuntime app) { Call(app, "Update"); Input.keys.Clear(); }
    private static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key); Update(app); }
    private static void Click(FrontierRuntime app, Rect rect)
    { GUI.queuedClick = rect; Draw(app); Check(!GUI.queuedClick.HasValue, "The visible production button accepts its click"); }
    private static WorldObject Object(GameCore g, string kind)
    { WorldObject o = Array.Find(g.Map.objects, x => x.kind == kind); Check(o != null, "Missing shipped archive interaction " + kind); return o; }
    private static void Position(FrontierRuntime app, Point position)
    { Check(app.SetVisualReviewPosition(position), "The archive fixture uses actual walkable hero footing"); Set(app, "camera", app.Game.Position); }
    private static void Interact(FrontierRuntime app, string kind)
    { WorldObject o = Object(app.Game, kind); Position(app, new Point(o.x, o.y - .7f)); Key(app, KeyCode.E); }
    private static void Choose(FrontierRuntime app, string action, string capture = null)
    {
        Check(app.Game.mode == "dialog" && app.Game.conversation != null, "A numbered archive choice belongs to a real interaction");
        int index = Array.FindIndex(app.Game.conversation.choices, c => c.action == action);
        Check(index >= 0 && index < 9, "The actual dialogue exposes a keyboard choice: " + action);
        int page = 0;
        do
        {
            Draw(app, capture == null ? null : capture + "-page" + page++);
            if ((bool)Call(app, "DialogueChoicesVisible")) break;
            Key(app, KeyCode.Return);
        } while (page < 20);
        Check((bool)Call(app, "DialogueChoicesVisible"), "Every archive narrative page finishes before selecting an action");
        Key(app, (KeyCode)((int)KeyCode.Alpha1 + index));
    }
    private static void Prepare(FrontierRuntime app, bool english = true, bool large = false)
    {
        Input.Clear(); GUI.queuedClick = null; Screen.width = 1280; Screen.height = 800; Time.unscaledDeltaTime = .02f;
        app.BeginVisualReview(0, 2); app.VisualReviewPaused = false; GameCore g = app.Game;
        g.enemies.Clear(); g.save.companion = -1; g.save.reducedMotion = true; g.save.english = english; g.save.largeText = large;
        g.save.showGuidance = true; g.invulnerable = 0; g.save.flags.Add("archive_runtime_mature_story");
        Set(app, "applicationPaused", false); Set(app, "pointerSeen", false); Set(app, "controllerUsed", false);
        Set(app, "combatHudTime", 0f); Set(app, "journalTab", 0); Set(app, "selectedRegion", -1); Call(app, "Resize");
    }
    private static void Enter(FrontierRuntime app, string suffix = "")
    {
        Interact(app, "portal10"); Choose(app, "archive_enter", "entry" + suffix);
        Check(app.Game.save.room == GameCore.ArchiveRoom && app.Game.ArchiveAccepted && app.Game.save.trackedQuest == "archive",
            "Actual E and the numbered entrance accept and track the optional dungeon");
        Check(app.Game.enemies.Count == 0, "The first chamber starts as the authored safe puzzle"); Set(app, "camera", app.Game.Position);
    }
    private static void Drain(FrontierRuntime app, string suffix = "")
    {
        Interact(app, "archivePlan"); Choose(app, "close", "plan" + suffix);
        Check(app.Game.Has("archive_plan"), "The actual plan interaction registers its investigation checkpoint");
        int[] solution = { 2, 0, 3 };
        for (int valve = 0; valve < solution.Length; valve++)
            for (int turn = 0; turn < 4 && app.Game.ArchiveValve(valve) != solution[valve]; turn++)
            {
                Interact(app, "archiveValve" + valve); Choose(app, "archive_rotate_" + valve, "valve" + valve + "-turn" + turn + suffix);
                Check(app.Game.mode == "world", "A numbered rotation resumes the actual world puzzle");
            }
        Check(app.Game.ArchiveDrained && app.Game.ArchiveFlowSegments == 4 && app.Game.PresentationTile(12, 13) == '=',
            "Visible port rotations complete the water route and permanently render its open bridge");
    }
    private static void Vent(FrontierRuntime app, string suffix = "")
    {
        Interact(app, "archiveVent"); Choose(app, "archive_vent", "vent" + suffix);
        Check(app.Game.ArchiveVented && app.Game.PresentationTile(23, 6) == '=' && !app.Game.hazards.Exists(h => h.kind == "archiveTide"),
            "The real pressure wheel opens the vault and retires only archive warnings");
    }
    private static void Records(FrontierRuntime app, string suffix = "")
    {
        for (int record = 0; record < 3; record++)
        {
            Interact(app, "archiveRecord" + record); Choose(app, "close", "record" + record + suffix);
            Check(app.Game.Has("archive_record_" + record) && app.Game.Has("history_" + (17 + record)),
                "Reading the real record appends its local checkpoint and matching history entry");
            Check(app.Game.enemies.Count == (record == 2 ? 3 : 0), "The third actual record starts exactly one authored defence");
        }
    }
    private static void Aim(FrontierRuntime app, Point point)
    {
        Set(app, "camera", app.Game.Position); Point screen = (Point)Call(app, "ScreenPoint", point);
        float scale = Get<float>(app, "scale"), x = Get<float>(app, "offsetX"), y = Get<float>(app, "offsetY");
        Input.mousePosition = new Vector3(x + screen.x * scale, Screen.height - y - screen.y * scale, 0);
    }
    private static void Fight(FrontierRuntime app, int stopAt = 0)
    {
        var g = app.Game; int frames = 0, skills = 0, dodges = 0;
        while (g.enemies.Count > stopAt && frames++ < 16000)
        {
            Check(!g.Dying, "The unmodified starter must survive actual archive combat");
            Enemy target = g.enemies[0]; float distance = (target.Position - g.Position).Length;
            Point movement = distance > 1.4f ? g.DirectionTo(g.Position, target.Position) : new Point();
            if (target.state == 1 && distance < 3)
            { movement = distance > .05f ? (g.Position - target.Position).Normalized : new Point(-1, 0); if (g.dashCooldown <= 0) { Input.keys.Add(KeyCode.Space); dodges++; } }
            Input.axes["Horizontal"] = movement.x; Input.axes["Vertical"] = -movement.y; Input.held.Add(KeyCode.J); Aim(app, target.Position);
            if (distance < 3 && g.skillCooldown <= 0 && g.save.energy >= 40) { Input.keys.Add(KeyCode.Q); skills++; }
            if (g.save.health < g.save.maxHealth * .65f && g.save.inventory[6] > 0) Input.keys.Add(KeyCode.F);
            Update(app);
        }
        Input.axes.Clear(); Input.held.Clear();
        Check(frames < 16000 && !g.Dying && g.enemies.Count <= stopAt, "Actual J/Q/Space inputs finish the requested archive defence without patched health, damage or attack clocks");
        if (stopAt == 0) Check(g.ArchiveGuardsCleared && skills > 0 && dodges > 0, "The real complete fight exercises both techniques and dodge inputs");
        Console.WriteLine("  Archive defence: " + frames + " frames; Q=" + skills + ", Space=" + dodges + "; health=" + g.save.health + "; survivors=" + g.enemies.Count);
    }
    private static void Views(FrontierRuntime app, string suffix)
    {
        var g = app.Game; Update(app); QuestGuide guide = g.GetQuest("archive"); Draw(app, "world" + suffix);
        Check(g.save.trackedQuest == "archive" && g.NavigationTarget().HasValue, "The accepted optional dungeon has an actual navigation target");
        Point? goal = Get<Point?>(app, "routeGoal");
        Check(goal.HasValue && (goal.Value - g.NavigationTarget().Value).Length < .001f, "Production Update follows the current archive checkpoint");
        Key(app, KeyCode.Tab); Draw(app); int quest = Array.IndexOf(GameCore.QuestIds, "archive");
        Check(quest == 12 && GameCore.QuestIds.Length == 13, "The archive appends after all twelve existing journal entries");
        float spacing = Math.Min(46, 464f / GameCore.QuestIds.Length); Click(app, new Rect(279, 226 + quest * spacing, 321, spacing - 7));
        Draw(app, "journal" + suffix);
        Check(Get<int>(app, "selectedQuest") == quest && HasText(g.L("下一步：", "Next: ") + guide.next), "The real new journal row displays the current archive objective");
        Click(app, new Rect(927, 647, 263, 31)); Draw(app, "map" + suffix);
        Check(g.mode == "map" && Get<int>(app, "selectedRegion") == guide.room && Get<int>(app, "mapPage") == guide.room / 9,
            "The actual journal destination opens its selected region on the correct map page");
        Key(app, KeyCode.Escape); Check(g.mode == "world", "Closing the new quest views restores gameplay");
    }
    public static void ArchiveActualInputsQuestAndDefence(FrontierRuntime app)
    {
        for (int language = 0; language < 2; language++) for (int large = 0; large < 2; large++)
        {
            string suffix = (language == 1 ? "-en" : "-zh") + (large == 1 ? "-large" : "");
            Prepare(app, language == 1, large == 1); var g = app.Game; Enter(app, suffix); Views(app, "-entry" + suffix);
            int coins = g.save.coins; Drain(app, suffix); Check(g.save.coins == coins, "Trial rotations never charge the player"); Views(app, "-network" + suffix);
            Vent(app, suffix); Views(app, "-vented" + suffix); Records(app, suffix); Views(app, "-defence" + suffix); Fight(app);
            int shards = g.save.shards, points = g.save.skillPoints; coins = g.save.coins;
            Interact(app, "archiveKeeper"); Choose(app, language == 1 ? "archive_family" : "archive_public", "choice" + suffix);
            Check(g.ArchiveComplete && g.save.coins == coins + 50 && g.save.shards == shards + 6 && g.save.skillPoints == points + 1 &&
                g.Has("archive_runtime_mature_story") && g.Has("core_2") && !g.Has("boss_10"), "The actual closing choice pays once and preserves mature story/chapter identity");
            Check(g.Has(language == 1 ? "archive_family" : "archive_public") && !g.Has(language == 1 ? "archive_public" : "archive_family"), "Only the selected equally rewarded story branch is persisted");
            Interact(app, "archiveKeeper"); Choose(app, "close", "completed" + suffix);
            Check(g.save.coins == coins + 50 && g.save.shards == shards + 6 && g.save.skillPoints == points + 1 && g.enemies.Count == 0,
                "Completed desk interaction cannot duplicate the defence or closing reward"); Draw(app, "completed-world" + suffix);
        }
        Console.WriteLine("PASS archive actual E/numbered input, every valve port/plan/vent/record, all13 journal rows, objective maps, starter J/Q/Space combat and both one-time story choices in both languages/text sizes; NATIVE_UNVERIFIED");
    }
    public static void ArchiveSaveStorePressurePauseDefenceAndLegacy(FrontierRuntime app)
    {
        Prepare(app); Enter(app); Drain(app); var g = app.Game; Position(app, new Point(18.5f, 14.5f));
        for (int frame = 0; frame < 100 && !g.hazards.Exists(h => h.kind == "archiveTide"); frame++) Update(app);
        Hazard warning = g.hazards.Find(h => h.kind == "archiveTide"); Check(warning != null && warning.timer > 1.1f, "Real world Update gives the first pulse its full readable warning");
        float timer = warning.timer, time = g.save.totalTime; Call(app, "OnApplicationPause", true);
        for (int frame = 0; frame < 30; frame++) Update(app);
        Check(warning.timer == timer && g.save.totalTime == time, "Application pause freezes the archive warning and actual world time"); Call(app, "OnApplicationPause", false);
        Key(app, KeyCode.Escape); timer = warning.timer; for (int frame = 0; frame < 30; frame++) Update(app);
        Check(g.mode == "pause" && warning.timer == timer, "The real menu pause also freezes dungeon pressure"); Key(app, KeyCode.Escape);
        g.PrepareSave(); Check(SaveStore.Write(g.save, 0), "The production envelope stores the unfinished gallery"); string issue;
        SaveData saved = SaveStore.Read(0, out issue); Check(saved != null && issue == null, "The archive save validates its real JSON checksum");
        g.Load(saved); g.Start(); Check(g.ArchiveDrained && !g.ArchiveVented && g.hazards.Count == 0 && g.Has("archive_runtime_mature_story"),
            "Loading retains puzzle/story progress and resets transient pressure safely");
        Vent(app); Records(app); Fight(app, 2); int kills = g.save.kills[10], coins = g.save.coins; int defeated = 0;
        foreach (int id in new[] { 74000, 74001, 74002 }) if (g.Has("archive_defeated_" + id)) defeated++;
        Check(defeated == 1 && !g.ArchiveGuardsCleared, "Only the actually defeated defence member is permanently cleared");
        var health = new Dictionary<int, float>(); foreach (Enemy enemy in g.enemies) health.Add(enemy.id, enemy.hp);
        g.PrepareSave(); Check(SaveStore.Write(g.save, 1), "The production envelope stores partial defence"); saved = SaveStore.Read(1, out issue);
        Check(saved != null && issue == null, "Partial defender snapshots survive real JSON and checksum validation"); g.Load(saved); g.Start();
        Check(g.enemies.Count == 2 && !g.ArchiveComplete && !g.ArchiveGuardsCleared, "Production load rebuilds exactly the two living defenders");
        foreach (Enemy enemy in g.enemies) Check(health.ContainsKey(enemy.id) && Math.Abs(health[enemy.id] - enemy.hp) < .001f && enemy.timer > 0 && enemy.timer <= 2,
            "The actual roundtrip preserves each living HP and bounds its resumed warning clock");
        g.Enter(2); g.enemies.Clear(); g.Enter(10); Check(g.enemies.Count == 2 && g.save.kills[10] == kills && g.save.coins == coins,
            "Travel cannot regenerate a defeated guard or duplicate its ordinary loot");
        var legacy = new SaveData { version = 1, stage = 2, room = 9, x = 11, y = 16, coins = 137,
            kills = new[] { 1,2,3,4,5,6,7,8,9,10 }, explored = new[] { 11,12,13,14,15,16,17,18,19,20 },
            flags = new List<string> { "intro", "core_2", "watch_complete", "signal_complete", "history_16" } };
        File.WriteAllText(SaveStore.PathFor(2), JsonUtility.ToJson(legacy)); saved = SaveStore.Read(2, out issue);
        Check(saved != null && issue == null, "The actual legacy decoder accepts a ten-region version-one save"); g.Load(saved); g.Start();
        Check(g.save.room == 9 && g.save.coins == 137 && !g.ArchiveAccepted && !g.ArchiveComplete && g.save.kills.Length == 11 && g.save.explored.Length == 11,
            "Legacy save migration retains the old inn and resources, appends one empty counter and invents no archive progress");
        for (int i = 0; i < 10; i++) Check(g.save.kills[i] == i + 1 && g.save.explored[i] == i + 11, "Every mature map counter retains its exact prefix");
        Check(g.Has("watch_complete") && g.Has("signal_complete") && g.Has("history_16"), "The real migration retains earlier optional stories and lore"); Draw(app, "legacy-inn");
        Console.WriteLine("PASS archive production SaveStore JSON/checksum, both pause paths, transient warning reset, actual partial-defence deaths/livingHP, travel anti-farming and old ten-map v1 save migration; NATIVE_UNVERIFIED");
    }
    public static void ArchiveActualMapPaginationAndStagedWarningPolicies(FrontierRuntime app)
    {
        for (int language = 0; language < 2; language++) for (int large = 0; large < 2; large++) for (int policy = 0; policy < 2; policy++)
        {
            Prepare(app, language == 1, large == 1); var g = app.Game;
            Screen.width = policy == 0 ? 960 : 1920; Screen.height = policy == 0 ? 600 : 1080;
            g.save.effectsQuality = policy == 0 ? 0 : 2; g.save.reducedMotion = policy == 0; Call(app, "Resize");
            string suffix = (language == 1 ? "-en" : "-zh") + (large == 1 ? "-large" : "") + (policy == 0 ? "-960-minimal" : "-1920-rich");
            Key(app, KeyCode.M); Draw(app, "map-first-page" + suffix);
            Check(Get<int>(app, "mapPage") == 0 && GUI.buttonCalls.Exists(b => b.rect.x == 289 && b.rect.y == 224) && GUI.buttonCalls.Exists(b => b.rect.x == 667 && b.rect.y == 532),
                "All mature first-page cards retain their original clickable geometry");
            Click(app, new Rect(685, 633, 149, 24)); Draw(app, "map-second-page" + suffix);
            Check(Get<int>(app, "mapPage") == 1 && HasText(g.L(g.data.maps[9].name, g.data.maps[9].en)) && HasText(g.L(g.data.maps[10].name, g.data.maps[10].en)),
                "The actual next-page button reveals both inn and archive rather than hiding mature regions");
            Click(app, new Rect(478, 224, 156, 80)); Draw(app, "map-archive-selected" + suffix);
            Check(Get<int>(app, "selectedRegion") == 10 && HasText(g.GetQuest("archive").next), "The visible second-page archive card selects its actual destination and objective");
            Click(app, new Rect(888, 616, 306, 47)); Check(g.save.room == 10 && g.ArchiveAccepted && g.save.trackedQuest == "archive", "Actual selected-card travel enters and accepts the optional dungeon");
            Set(app, "camera", g.Position); Draw(app, "world-closed-bridge" + suffix); Drain(app, suffix); Position(app, new Point(7, 8)); Draw(app, "world-network" + suffix);
            Check(HasText("I · S + W") && HasText("II · N + E") && HasText("III · W + N"), "Actual valve labels expose all solved port directions without relying on motion or color");
            Position(app, new Point(18.5f, 14.5f));
            for (int frame = 0; frame < 100 && !g.hazards.Exists(h => h.kind == "archiveTide"); frame++) Update(app);
            Hazard pulse = g.hazards.Find(h => h.kind == "archiveTide"); Check(pulse != null, "The warning-policy fixture is produced by the real gallery scheduler");
            float timer = pulse.timer; Draw(app, "world-pulse" + suffix);
            GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app, "DrawHazardWarnings");
            Point p = (Point)Call(app, "ScreenPoint", pulse.position); float radius = pulse.radius * 36;
            List<GUI.TextureCall> cores = GUI.textureCalls.FindAll(c => c.rect.width == 4 && c.rect.height == 4);
            List<GUI.TextureCall> backing = GUI.textureCalls.FindAll(c => c.rect.width == 8 && c.rect.height == 8);
            List<GUI.TextureCall> caption = GUI.textureCalls.FindAll(c => c.rect.width == 54 && c.rect.height == 24);
            Check(GUI.textureCalls.Count == 65 && cores.Count == 32 && backing.Count == 32 && caption.Count == 1 && pulse.timer == timer,
                "All32 collision-boundary core/backing pairs and the countdown plate remain drawn under both warning policies without advancing damage time");
            for (int mark = 0; mark < 32; mark++)
            {
                Point core = new Point(cores[mark].rect.x + 2, cores[mark].rect.y + 2);
                Point border = new Point(backing[mark].rect.x + 4, backing[mark].rect.y + 4);
                Check(Math.Abs((core - p).Length - radius) < .01f && (core - border).Length < .01f,
                    "Every bright core and larger dark backing share the actual collision boundary rather than enlarging its hit radius");
                Check(cores[mark].color.a > .99f && backing[mark].color.a >= .9f && Luminance(cores[mark].color) > .8 &&
                    (Luminance(cores[mark].color) + .05) / (Luminance(backing[mark].color) + .05) > 12,
                    "The permanent bright/dark boundary has strong measured luminance contrast on textured stone independent of effect quality or motion");
            }
            Rect countdown = caption[0].rect;
            Check(Math.Abs(countdown.x + countdown.width / 2 - p.x) < .01f && countdown.y + countdown.height <= p.y - radius - 3 &&
                caption[0].color.a >= .9f && Luminance(caption[0].color) < .01 && HasText(pulse.timer.ToString("0.0")),
                "A dark countdown plate sits wholly above the dangerous circle and retains the actual unadvanced timer");
            Get<List<Rect>>(app, "worldOverlayRegions").Add(new Rect(p.x - radius, p.y - radius, radius * 2, radius * 2));
            GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app, "DrawHudThreatWitness");
            List<GUI.TextureCall> witnesses = GUI.textureCalls.FindAll(c => c.rect.width == 4 && c.rect.height == 4);
            Check(witnesses.Count == 48 && pulse.timer == timer, "Existing HUD-overlay warning protection redraws all48 archive boundary marks without advancing it");
            foreach (GUI.TextureCall witness in witnesses)
                Check(Math.Abs((new Point(witness.rect.x + 2, witness.rect.y + 2) - p).Length - radius) < .01f &&
                    Luminance(witness.color) > .8 && witness.color.a >= .79f,
                    "HUD witnesses retain the bright archive color at the actual collision radius");
            if (captureFolder != null) GUI.Export(captureFolder, "qa-archive-warning-witness" + suffix);
            Vent(app, suffix); Position(app, new Point(29, 10)); Draw(app, "world-vault" + suffix); Views(app, "-vault" + suffix);
        }
        Console.WriteLine("PASS archive actual first/second map-page clicks and travel, preserved mature card geometry, visible valve port directions and real pressure warning/overlay draw across two languages/text sizes/resolutions/effect-motion policies; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous = Application.persistentDataPath; string directory = Path.Combine(Path.GetTempPath(), "starfall-archive-check-" + Guid.NewGuid());
        Application.persistentDataPath = directory; captureFolder = args.Length > 0 ? args[0] : null;
        if (captureFolder != null) Directory.CreateDirectory(captureFolder); var app = new FrontierRuntime(); int failed = 0;
        try
        {
            Call(app, "Awake"); Check(app.Game != null, "Actual runtime Awake loads the shipped full campaign");
            foreach (Action<FrontierRuntime> test in new Action<FrontierRuntime>[] { ArchiveActualInputsQuestAndDefence, ArchiveSaveStorePressurePauseDefenceAndLegacy, ArchiveActualMapPaginationAndStagedWarningPolicies })
            {
                try { test(app); }
                catch (Exception error) { failed++; Console.WriteLine("FAIL " + test.Method.Name + ": " + (error.InnerException ?? error).Message); }
            }
            app.EndVisualReview(); Call(app, "OnDestroy");
        }
        finally { Application.persistentDataPath = previous; Input.Clear(); GUI.queuedClick = null; if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        Console.WriteLine("Runtime Tide Archive: " + (3 - failed) + "/3 groups passed; NATIVE_UNVERIFIED"); return failed == 0 ? 0 : 1;
    }
}
