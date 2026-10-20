// Production input/GUI/SaveStore checks using offline Unity declarations.
// Fixture staging is explicit; no native Editor, GPU or play-feel claim is made.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeRootEchoChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static string captures, topologyPath;
    static int captureCount, movementFrames, dashInputs;
    static void Check(bool value, string text) { if (!value) throw new Exception(text); }
    static object Call(FrontierRuntime app, string name, params object[] args) { return typeof(FrontierRuntime).GetMethod(name, Private).Invoke(app, args); }
    static void Set(FrontierRuntime app, string name, object value) { typeof(FrontierRuntime).GetField(name, Private).SetValue(app, value); }
    static void Update(FrontierRuntime app) { Call(app, "Update"); Input.keys.Clear(); }
    static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key); Update(app); }
    static void Draw(FrontierRuntime app, string name = null)
    {
        GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app, "OnGUI");
        if (name != null && captures != null) { GUI.Export(captures, "qa-root-echo-" + name); captureCount++; }
    }
    static bool HasText(string text) { return GUI.commands.Exists(c => c.Contains("\"text\":" + GUI.S(text))); }
    static SaveData Copy(SaveData save) { return JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save)); }
    static List<Hazard> Echo(GameCore game) { return game.hazards.FindAll(h => h.kind == "echo"); }
    static WorldObject Object(GameCore game, string kind) { var o = Array.Find(game.Map.objects, x => x.kind == kind); Check(o != null, "Canonical object exists: " + kind); return o; }
    static void Position(FrontierRuntime app, Point point)
    {
        Check(app.Game.CanStand(point), "Fixture staging uses exact legal feet");
        Check(app.SetVisualReviewPosition(point) && (app.Game.Position - point).Length < .001f, "Fixture staging must not silently repair feet");
        Set(app, "camera", app.Game.Position);
    }
    static void Move(Point direction)
    {
        Input.axes["Horizontal"] = direction.x; Input.axes["Vertical"] = -direction.y; Input.held.Clear();
        if (direction.x > .01f) Input.held.Add(KeyCode.D); if (direction.x < -.01f) Input.held.Add(KeyCode.A);
        if (direction.y > .01f) Input.held.Add(KeyCode.S); if (direction.y < -.01f) Input.held.Add(KeyCode.W);
    }
    static void Stop() { Input.axes.Clear(); Input.held.Clear(); }
    static void Prepare(FrontierRuntime app, int floor = 2, int profession = 0, int risk = 0)
    {
        Input.Clear(); GUI.queuedClick = null; Screen.width = 960; Screen.height = 640; Time.unscaledDeltaTime = .02f;
        app.BeginVisualReview(profession, 0); app.VisualReviewPaused = false; var game = app.Game;
        // Stage only an already-earned floor and its clearance. The encounter's
        // entry, choices, movement, contacts, timers, payouts and saves stay real.
        var save = Copy(game.save); save.room = 8; save.stage = 3; save.frontierEnding = 1;
        save.abyssFloor = floor; save.abyssRisk = risk; save.abyssSeed = 1729; save.x = 7; save.y = 9.5f;
        save.abyssEnemies = null; save.abyssSnapshotFloor = 0; save.flags.Add("abyss_clear_" + floor);
        save.companion = -1; save.english = true; save.largeText = false; save.reducedMotion = true; save.effectsQuality = 0;
        game.Load(save); game.Start(); game.Close(); Check(game.enemies.Count == 0 && game.RootEchoAvailable, "Earned cleared floor has the optional shrine");
        Set(app, "applicationPaused", false); Set(app, "pointerSeen", false); Set(app, "controllerUsed", false);
        Set(app, "resumeTime", 0f); Set(app, "arrivalTime", 0f); Set(app, "combatHudTime", 0f); Set(app, "dialoguePage", 0); Set(app, "lastConversation", null);
        Position(app, new Point(7, 9.5f)); Call(app, "Resize");
        // Settle the real floor handover before reviewing the optional event.
        // UpdatePresentation notices the loaded room/floor on its first update
        // and opens an arrival banner; that earlier transition has elapsed in
        // this explicitly cleared-floor fixture, before the shrine is used.
        Update(app); Set(app, "arrivalTime", 0f); Set(app, "resumeTime", 0f); game.toastTime = 0;
    }
    static void Interact(FrontierRuntime app, string kind, bool stage = false)
    {
        var o = Object(app.Game, kind); if (stage) Position(app, new Point(o.x, o.y + .5f));
        Check(app.Game.Nearest() == o, "Production E targets " + kind); Key(app, KeyCode.E);
    }
    static void Choose(FrontierRuntime app, string action, string capture = null)
    {
        var game = app.Game; Check(game.mode == "dialog" && game.conversation != null, "Actual choice belongs to dialogue: " + action);
        int choice = Array.FindIndex(game.conversation.choices, c => c.action == action); Check(choice >= 0 && choice < 9, "Authored numeric choice exists: " + action);
        var pages = DialogueLayout.Pages(game.L(game.conversation.text, game.conversation.textEn), game.save.largeText); int page = 0;
        while (true)
        {
            Draw(app); Check(HasText(pages[page]), "Actual dialogue GUI shows each complete narrative page");
            if (capture != null && captures != null) { GUI.Export(captures, "qa-root-echo-" + capture + "-page" + page); captureCount++; }
            if ((bool)Call(app, "DialogueChoicesVisible")) break;
            Key(app, KeyCode.Return); page++; Check(page < pages.Count, "Dialogue pagination terminates");
        }
        Check(page == pages.Count - 1, "Numbers appear only after all narrative pages"); Key(app, (KeyCode)((int)KeyCode.Alpha1 + choice));
    }
    static void Begin(FrontierRuntime app, string capture = null) { Interact(app, "abyssEcho"); Choose(app, "echo_begin", capture); Check(app.Game.RootEchoActive, "Actual E and numeric selection start the event"); }
    static void WaitStep(FrontierRuntime app, int step)
    {
        int frame = 0; while (app.Game.RootEchoActive && app.Game.RootEchoStep < step && frame++ < 1000) Update(app);
        Check(frame < 1000 && app.Game.RootEchoStep == step && !app.Game.Dying, "Real runtime updates reach step " + step);
    }
    static void EscapeFirst(FrontierRuntime app, bool dash)
    {
        var game = app.Game; Point locked = Echo(game)[0].position; int frames = 0; Move(new Point(0, 1));
        if (dash) { float energy = game.save.energy; Key(app, KeyCode.Space); dashInputs++; Check(game.dashTime > 0 && game.save.energy < energy, "Actual Space spends ordinary energy and starts dash"); }
        while (game.Position.y < 11.9f && frames++ < 100)
        { Point before = game.Position; Update(app); Check(game.CanStand(game.Position) && (game.Position - before).Length < .35f, "Real WASD preserves continuous legal feet"); }
        Stop(); movementFrames += frames;
        Check(frames < 100 && (game.Position - GameCore.RootEchoPoint).Length < GameCore.RootEchoLeash, "First circle is escapable before warning within leash");
        Check(Echo(game).Count == 1 && (Echo(game)[0].position - locked).Length < .001f && Echo(game)[0].timer > 0, "Movement and dash cannot retarget the prepared lock");
    }
    public static void ActualInputThreeClassesWarningsAndContact(FrontierRuntime app)
    {
        for (int profession = 0; profession < 3; profession++) for (int risk = 0; risk < 3; risk++)
        {
            Prepare(app, 2, profession, risk); var game = app.Game; float hp = game.save.health; Begin(app);
            Check(game.RootEchoStep == 0 && Echo(game).Count == 1 && Echo(game)[0].radius == 1.3f && Echo(game)[0].timer == 1.25f && Echo(game)[0].damage == 12 + risk * 4, "Actual activation starts one full risk-scaled lock warning");
            EscapeFirst(app, profession == 1 && risk == 1); WaitStep(app, 1);
            Check(Echo(game).Count == 2 && Echo(game).TrueForAll(h => h.radius == 1.15f && h.timer > 1.2f), "Second pair receives fresh warnings after first contact and recovery");
            foreach (float x in new[] { -1.9f, 1.9f }) Check(Echo(game).Exists(h => (h.position - (GameCore.RootEchoPoint + new Point(x, 0))).Length < .001f), "Second pair preserves the actual middle lane");
            WaitStep(app, 2); Check(Echo(game).Count == 4 && Echo(game).TrueForAll(h => h.radius == 1.05f && h.timer > 1.2f), "Final four corners have fresh readable warnings");
            Check(!game.RootEchoResolved && game.save.runCoins == 0, "Warnings do not pay early"); WaitStep(app, 3);
            Check(game.RootEchoResolved && !game.RootEchoActive && Echo(game).Count == 0 && game.save.health == hp && game.save.profession == profession, "All unchanged starters complete through actual movement without health or invulnerability writes");
        }
        Prepare(app, 2, 0, 2); var contact = app.Game; Begin(app); float beforeHp = contact.save.health; int frames = 0;
        while (contact.save.health == beforeHp && frames++ < 100) Update(app);
        Check(frames < 100 && contact.save.health < beforeHp && contact.effects.Exists(e => e.kind == "danger" && e.radius == 1.3f && e.variant == 8), "Standing in the locked circle takes shared real damage and the matching actual area effect");
        Console.WriteLine("PASS actual E/numeric paging, nine class/risk runs, WASD and Space, 1/2/4 captured hazards, real contact damage and matching area effect; movement=" + movementFrames + ", dash=" + dashInputs + "; NATIVE_UNVERIFIED");
    }
    public static void EconomyRestBankAndImmutableFloors(FrontierRuntime app)
    {
        var topology = new GameCore(CampaignFixture.Create()); topology.NewGame(0); topology.Close(); topology.SkipTutorial(); topology.save.frontierEnding = 1; topology.StartAbyss(2);
        string[] original = File.ReadAllLines(topologyPath); Check(original.Length == 6, "Immutable pre-feature topology has six full floors");
        for (int floor = 1; floor <= 6; floor++)
        {
            Check(AbyssTopologyBaseline.Describe(topology) == original[floor - 1], "Immutable original room rows/decor/encounters/exit and gate remain exact at floor " + floor);
            // Spread topology-only traversal choices across valid boon slots;
            // the immutable fixture covers seeded rooms, not one boon build.
            if (floor < 6) { topology.enemies.Clear(); topology.Talk(Object(topology, "abyssGate")); topology.Choose((floor - 1) % 3); }
        }
        Prepare(app, 2, 1, 2); var game = app.Game; int coins = game.save.coins, shards = game.save.shards, xp = game.save.xp, kills = game.save.kills[8];
        Begin(app); EscapeFirst(app, false); WaitStep(app, 3);
        Check(game.save.runCoins == 20 && game.save.runShards == 5 && game.save.coins == coins && game.save.shards == shards && game.save.xp == xp && game.save.kills[8] == kills, "Risk-two trial pays exact unbanked bonus without money/XP/kill banking");
        Interact(app, "abyssEcho", true); Check(Array.FindIndex(game.conversation.choices, c => c.action == "echo_begin" || c.action == "echo_rest") < 0, "Resolved real menu cannot repeat or switch reward"); Choose(app, "close");
        Interact(app, "abyssGate", true); Choose(app, "boon0");
        Check(game.save.abyssFloor == 3 && game.save.boons[0] == 1 && game.save.coins == coins + 20 && game.save.shards == shards + 5 && game.save.runCoins == 0 && game.save.runShards == 0, "Original east gate numeric boon banks bonus and descends once");
        foreach (int floor in new[] { 2, 4 })
        {
            Prepare(app, floor); game = app.Game; game.save.health = 20; float expected = 20 + game.save.maxHealth * .25f;
            Interact(app, "abyssEcho"); Choose(app, "echo_rest"); Check(game.RootEchoResolved && !game.RootEchoActive && game.save.health == expected && game.save.runCoins == 0 && game.save.runShards == 0, "Alternative real rest heals exactly quarter maximum once at floor " + floor);
            game.PrepareSave(); Check(SaveStore.Write(game.save, 0), "Resolved rest writes actual checksum envelope"); string warning; var saved = SaveStore.Read(0, out warning); Check(saved != null && warning == null, "Actual checksum reader accepts rest"); game.Load(saved); game.Start(); game.Close();
            Check(game.RootEchoResolved && game.save.health == expected && !game.RootEchoActive && Echo(game).Count == 0, "Saved rest survives reconstruction without refill or pending attack");
        }
        Console.WriteLine("PASS immutable 3df9f32 six-floor rows/decor/enemies/gates and real optional bonus, manual banking/boon descent, alternative rest on floors 2/4, resolved SaveStore roundtrip; NATIVE_UNVERIFIED");
    }
    public static void PauseCancelReloadDeathAndLegacy(FrontierRuntime app)
    {
        Prepare(app); var game = app.Game; Begin(app); Key(app, KeyCode.Escape); Check(game.mode == "pause", "Actual Escape enters pause");
        float timer = game.RootEchoTimer, hazard = Echo(game)[0].timer; for (int frame = 0; frame < 40; frame++) Update(app);
        Check(game.RootEchoTimer == timer && Echo(game)[0].timer == hazard && game.RootEchoActive, "Pause freezes actual event and shared contact timers"); Key(app, KeyCode.Escape);
        Call(app, "OnApplicationPause", true); timer = game.RootEchoTimer; hazard = Echo(game)[0].timer; for (int frame = 0; frame < 40; frame++) Update(app);
        Check(game.RootEchoTimer == timer && Echo(game)[0].timer == hazard, "Application pause freezes both timers"); Call(app, "OnApplicationPause", false);
        Interact(app, "abyssEcho"); Choose(app, "echo_cancel"); Check(!game.RootEchoActive && !game.RootEchoResolved && Echo(game).Count == 0, "Real cancellation leaves no reward or owned hazard");
        Begin(app); Move(new Point(0, 1)); int moved = 0; while (game.RootEchoActive && !game.Dying && moved++ < 140) Update(app); Stop();
        Check(moved < 140 && !game.RootEchoActive && !game.RootEchoResolved && (game.Position - GameCore.RootEchoPoint).Length > GameCore.RootEchoLeash && Echo(game).Count == 0, "Actual WASD beyond leash cancels retryably");
        Interact(app, "abyssEcho", true); Choose(app, "echo_begin"); game.PrepareSave(); Check(SaveStore.Write(game.save, 1), "Active actual event writes SaveStore"); string warning; var saved = SaveStore.Read(1, out warning); Check(saved != null && warning == null, "Active event envelope reads"); game.Load(saved); game.Start(); game.Close();
        Check(!game.RootEchoActive && !game.RootEchoResolved && Echo(game).Count == 0 && game.RootEchoTimer == 0 && game.save.runCoins == 0, "Reload cancels unfinished transient state without an invisible hazard or unearned reward");
        Position(app, new Point(7, 9.5f)); Begin(app); game.save.health = 1; game.save.runCoins = 11; game.save.runShards = 2; int banked = game.save.coins, deaths = game.save.deaths; int frames = 0;
        while (!game.Dying && frames++ < 100) Update(app); Check(frames < 100 && game.Dying && game.Has("recovery_pending") && game.save.deaths == deaths + 1, "Real shared echo contact triggers mature death recovery");
        for (frames = 0; frames < 100 && game.save.room != 0; frames++) Update(app);
        Check(game.save.room == 0 && !game.InAbyss && !game.RootEchoActive && Echo(game).Count == 0 && game.save.runCoins == 0 && game.save.runShards == 0 && game.save.coins == Math.Max(0, banked - 10) && !game.Has("abyss_echo_2"), "Mature death discards only unbanked loot, preserves original penalty and awards no echo");
        for (int version = 1; version <= 2; version++)
        {
            var legacy = new SaveData { version = version, room = 1, stage = 1, x = 5, y = 14, coins = 137, flags = new List<string> { "intro", "core_1", "personal_1", "homeward_complete", "sparring_complete" } };
            File.WriteAllText(SaveStore.PathFor(2), JsonUtility.ToJson(legacy)); saved = SaveStore.Read(2, out warning); Check(saved != null && warning == null, "Actual raw legacy v" + version + " JSON loads"); game.Load(saved); game.Start(); game.Close();
            Check(game.save.coins == 137 && game.Has("core_1") && game.Has("personal_1") && game.Has("homeward_complete") && game.Has("sparring_complete") && !game.RootEchoAvailable && !game.RootEchoActive && !game.save.flags.Exists(f => f.StartsWith("abyss_echo_")), "Legacy story/identity/economy prefixes persist without inventing a challenge completion");
        }
        Console.WriteLine("PASS real menu/application pause, numeric cancellation, WASD leash, active SaveStore reload, real shared hazard death/unbanked loss and raw legacy v1/v2 retained stories; NATIVE_UNVERIFIED");
    }
    public static void BilingualGuiAndHazardReadability(FrontierRuntime app)
    {
        foreach (bool english in new[] { false, true }) foreach (bool large in new[] { false, true })
        {
            Prepare(app); var game = app.Game; game.save.english = english; game.save.largeText = large; game.save.effectsQuality = 0; game.save.reducedMotion = true;
            string suffix = (english ? "en" : "zh") + (large ? "-large" : "-normal"); Draw(app); Check(HasText(game.L("地脉回声", "ROOT ECHO")), "Actual shrine draws localized ready label under minimum effects"); Draw(app, "ready-" + suffix);
            Begin(app, "offer-" + suffix); Draw(app); Check(HasText(game.L("回声 ", "ECHO ") + "1/3"), "World shrine reports actual active step");
            for (int step = 0; step < 3; step++)
            {
                if (step == 1) { EscapeFirst(app, false); WaitStep(app, 1); } if (step == 2) WaitStep(app, 2);
                Draw(app); Check(GUI.commands.Exists(c => c.Contains(game.RootEchoStatus.Replace("\n", "\\n"))), "Actual HUD reports the localized full event status");
                foreach (var h in Echo(game))
                {
                    Point point = (Point)Call(app, "ScreenPoint", h.position); Check(HasText(h.timer.ToString("0.0")), "Each shared danger exposes actual remaining warning time");
                    Check(GUI.textureCalls.Exists(t => Math.Abs(t.rect.x - (point.x + h.radius * 36 - 2)) < .05f && Math.Abs(t.rect.y - (point.y - 2)) < .05f && t.rect.width == 4 && t.rect.height == 4), "Low effects preserve the exact actual collision-radius boundary");
                }
                Draw(app, "pulse" + (step + 1) + "-" + suffix);
            }
            WaitStep(app, 3); Position(app, new Point(7, 9.5f)); Draw(app); Check(HasText(game.L("回声已平息", "ECHO RESOLVED")), "Resolution label reflects the actual consumed choice"); Draw(app, "resolved-" + suffix);
            // A genuine first-pulse hit switches production UpdatePresentation
            // to its compact combat HUD. Do not set the HUD timer in the test.
            Prepare(app, 4); game = app.Game; game.save.english = english; game.save.largeText = large;
            Begin(app); float hp = game.save.health; int frames = 0;
            while (game.save.health == hp && frames++ < 100) Update(app);
            Check(frames < 100 && game.RootEchoActive && game.playerHitTime > 0 && (bool)typeof(FrontierRuntime).GetProperty("CombatHud", Private).GetValue(app, null), "Actual shared hazard hit activates the compact combat HUD");
            Draw(app); Check(HasText(game.L("回声挑战 ", "ROOT ECHO ") + "1/3") && HasText(game.L("观察光圈", "Read circles")), "Compact combat HUD keeps the current echo phase and actionable reading hint after a real hit");
            Draw(app, "contact-combat-" + suffix);
        }
        Console.WriteLine("PASS bilingual normal/large 960px actual paginated offer, ready/three pulse/resolved labels, low-FX exact warning radius/time, quiet and real-contact combat HUD status; captures=" + captureCount + "; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        captures = args.Length > 0 && args[0] != "-" ? args[0] : null; topologyPath = args.Length > 1 ? args[1] : null;
        string previous = Application.persistentDataPath, directory = Path.Combine(Path.GetTempPath(), "starfall-root-echo-" + Guid.NewGuid()); Application.persistentDataPath = directory;
        var app = new FrontierRuntime(); int failed = 0;
        try
        {
            Directory.CreateDirectory(directory); Call(app, "Awake"); Check(app.Game != null, "Production Awake loads campaign");
            foreach (Action<FrontierRuntime> test in new Action<FrontierRuntime>[] { ActualInputThreeClassesWarningsAndContact, EconomyRestBankAndImmutableFloors, PauseCancelReloadDeathAndLegacy, BilingualGuiAndHazardReadability })
                try { test(app); } catch (Exception error) { failed++; Console.WriteLine("FAIL " + test.Method.Name + ": " + (error.InnerException ?? error)); }
            app.EndVisualReview(); Call(app, "OnDestroy");
        }
        finally { Application.persistentDataPath = previous; Input.Clear(); GUI.queuedClick = null; GUI.matrix = Matrix4x4.identity; GUI.color = Color.white; GUI.enabled = true; if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        Console.WriteLine("Runtime Root Echo: " + (4 - failed) + "/4 groups passed; NATIVE_UNVERIFIED"); return failed == 0 ? 0 : 1;
    }
}
