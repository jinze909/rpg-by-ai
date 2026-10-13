// Production Update, OnGUI and SaveStore checks with offline Unity declarations.
// Actual input/save paths and staged GUI evidence remain NATIVE_UNVERIFIED.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeSparringChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string captureFolder;
    private static readonly Rect ActiveEnd = new Rect(774,132,144,30);
    private static readonly Rect Next = new Rect(366,388,172,42);
    private static readonly Rect Retry = new Rect(553,388,172,42);
    private static readonly Rect QuietEnd = new Rect(740,388,172,42);
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
            GUI.Export(captureFolder, "qa-sparring-" + capture);
            GUI.commands.AddRange(commands); GUI.textureCalls.AddRange(textures); GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static bool HasText(string text) { return GUI.commands.Exists(c => c.Contains("\"text\":" + GUI.S(text))); }
    private static void Update(FrontierRuntime app) { Call(app, "Update"); Input.keys.Clear(); }
    private static void Frames(FrontierRuntime app, int count) { for (int frame = 0; frame < count; frame++) Update(app); }
    private static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key); Update(app); }
    private static void Click(FrontierRuntime app, Rect rect)
    { GUI.queuedClick = rect; Draw(app); Check(!GUI.queuedClick.HasValue, "The actual displayed sparring button accepts its click"); }
    private static void Position(FrontierRuntime app, Point point)
    { Check(app.SetVisualReviewPosition(point), "The staged input fixture places Viola on actual walkable ground"); Set(app, "camera", app.Game.Position); }
    private static void Mouse(FrontierRuntime app, Point target)
    {
        Point screen = (Point)Call(app, "ScreenPoint", target);
        float scale = Get<float>(app, "scale"), ox = Get<float>(app, "offsetX"), oy = Get<float>(app, "offsetY");
        Input.mousePosition = new Vector3(screen.x * scale + ox, Screen.height - screen.y * scale - oy, 0);
    }
    private static WorldObject Mentor(GameCore g)
    { var mentor = Array.Find(g.Map.objects, o => o.kind == "mentor"); Check(mentor != null, "The original town mentor remains in the shipped campaign"); return mentor; }
    private static void InteractMentor(FrontierRuntime app, bool pad = false)
    { var mentor = Mentor(app.Game); Position(app, new Point(mentor.x, mentor.y - .7f)); Key(app, pad ? KeyCode.JoystickButton1 : KeyCode.E); }
    private static void Choose(FrontierRuntime app, string action, string capture = null)
    {
        var g = app.Game; Check(g.mode == "dialog" && g.conversation != null, "A numbered sparring choice belongs to an actual dialogue");
        int index = Array.FindIndex(g.conversation.choices, c => c.action == action);
        Check(index >= 0 && index < 9, "The real dialogue exposes a keyboard choice: " + action);
        int page = 0;
        do
        {
            Draw(app, capture == null ? null : capture + "-page" + page++);
            if ((bool)Call(app, "DialogueChoicesVisible")) break;
            Key(app, KeyCode.Return);
        } while (page < 20);
        Check((bool)Call(app, "DialogueChoicesVisible"), "The mentor's actual instruction pages finish before an action");
        Key(app, (KeyCode)((int)KeyCode.Alpha1 + index));
    }
    private static void Prepare(FrontierRuntime app, int profession = 0, bool english = true, bool large = false)
    {
        Input.Clear(); GUI.queuedClick = null; Screen.width = 1280; Screen.height = 800; Time.unscaledDeltaTime = .02f;
        app.BeginVisualReview(profession, 0); app.VisualReviewPaused = false; var g = app.Game;
        g.enemies.Clear(); g.save.companion = 0; g.save.reducedMotion = true; g.save.english = english; g.save.largeText = large;
        g.save.showGuidance = true; g.save.health = 58; g.save.energy = 42; g.save.heat = 33; g.save.cold = 27; g.save.totalTime = 100;
        g.save.coins = 313; g.save.xp = 9; g.save.skillPoints = 2; g.save.tea = 4; g.save.inventory[6] = 3;
        g.save.foodUntil = 180; g.save.teaUntil = 165; g.save.mealKind = 3; g.save.mealUntil = 190;
        g.save.flags.Add("sparring_runtime_mature_story"); g.invulnerable = 0;
        Set(app, "applicationPaused", false); Set(app, "pointerSeen", false); Set(app, "controllerUsed", false);
        Set(app, "combatHudTime", 0f); Set(app, "resumeTime", 0f); Call(app, "Resize");
    }
    private static Point Start(FrontierRuntime app, string suffix = "", bool pad = false)
    {
        InteractMentor(app, pad); var g = app.Game;
        Check(g.conversation.choices.Length == 3 && g.conversation.choices[0].action == "practice" && g.conversation.choices[1].action == "close"
            && g.conversation.choices[2].action == "sparring_menu", "The two original mentor choices keep their actions and numbers before the appended Alpha3 option");
        Point origin = g.Position; Choose(app, "sparring_menu", "mentor" + suffix); Choose(app, "sparring_start", "instructions" + suffix);
        Check(g.mode == "world" && g.SparringActive && g.practicing && g.SparringStage == 0 && !g.SparringAwaitingNext && !g.SparringFinished,
            "The actual E/B, Alpha3 and numbered instruction choice starts the safe first drill");
        Check(g.enemies.Count == 1 && g.enemies[0].kind == 0 && !g.enemies[0].boss && g.enemies[0].id == 75000,
            "Sparring starts its authored hunter rather than a basic-practice dummy or mature boss");
        Set(app, "camera", g.Position); return origin;
    }
    private static void ClearWaveThroughInput(FrontierRuntime app, bool pad = false)
    {
        var g = app.Game; int stage = g.SparringStage;
        for (int frame = 0; frame < 1500 && !g.SparringAwaitingNext && !g.SparringFinished; frame++)
        {
            Check(g.SparringActive && g.enemies.Count > 0, "The current sparring wave remains active until its real enemies are defeated");
            Enemy target = g.enemies[0]; Point behind = target.Position - target.facing.Normalized * .9f;
            Position(app, behind); Mouse(app, target.Position); g.SetAim((target.Position - g.Position).Normalized);
            if (pad)
            {
                Point direction = (target.Position - g.Position).Normalized;
                Input.axes["Aim Horizontal"] = direction.x; Input.axes["Aim Vertical"] = -direction.y;
                Input.held.Add(KeyCode.JoystickButton2);
            }
            else Input.held.Add(KeyCode.J);
            Update(app);
        }
        Input.held.Clear(); Input.axes.Clear();
        Check(g.SparringStage == stage && (g.SparringAwaitingNext || g.SparringFinished) && g.enemies.Count == 0,
            "Real held J/X attacks clear the authored wave and pause at a manual result rather than auto-spawning another encounter");
        Check(g.projectiles.Count == 0 && g.hazards.Count == 0 && g.playerAction == null,
            "The actual last death retires shots, warnings and the attack before displaying the result");
    }
    public static void SparringMentorUnlockAndOriginalPractice(FrontierRuntime app)
    {
        Prepare(app); var g = app.Game; g.save.flags.Remove("core_1"); g.save.practiceBits = 0;
        Check(!g.SparringReady, "An untrained early adventurer needs basic practice or the first core");
        InteractMentor(app); Choose(app, "sparring_menu", "locked");
        Check(!g.SparringActive && !g.practicing, "The actual locked Alpha3 choice cannot start advanced drills");
        g.Close(); InteractMentor(app); Choose(app, "practice", "basic");
        Check(g.practicing && !g.SparringActive && g.practiceStage == 0 && g.enemies[0].id == 90001,
            "The original mentor Alpha1 still starts its original safe dummy lesson");
        Draw(app, "basic-preserved"); g.EndPractice(); g.save.practiceBits = 7;
        Check(g.SparringReady && !g.Has("core_1"), "Completing the original basics unlocks advanced practice without a story gate");
        Start(app, "-basics-unlock");
        g.save.controllerAimProfile = 2; Input.axes["Horizontal"] = .6f; Update(app); Input.axes.Clear();
        Draw(app, "left-stick-only-controls");
        Check(HasText(g.L("X攻击 · A闪避 · 左摇杆朝向", "X: attack · A: dodge · left stick faces")),
            "Actual controller movement with right-stick aiming disabled displays the configured left-stick facing hint");
        g.save.controllerAimProfile = 0; Call(app, "ResetControllerAim", true);
        float scale = Get<float>(app, "scale"), ox = Get<float>(app, "offsetX"), oy = Get<float>(app, "offsetY");
        Input.mousePosition = new Vector3((ActiveEnd.x + 8) * scale + ox, Screen.height - (ActiveEnd.y + 8) * scale - oy, 0);
        Input.buttons.Add(0); float enemyHp = g.enemies[0].hp; Update(app);
        Check(g.playerAction == null && g.attackCooldown <= 0 && g.enemies[0].hp == enemyHp,
            "An actual held mouse attack over the displayed End control cannot fire into the practice world; action=" + (g.playerAction == null ? "none" : g.playerAction.kind) + ", cooldown=" + g.attackCooldown + ", hp=" + g.enemies[0].hp + "/" + enemyHp + ", pointer=" + ((Point)Call(app,"MouseVirtual")).x + "," + ((Point)Call(app,"MouseVirtual")).y + ", protected=" + Call(app,"PointerOverInterface",(Point)Call(app,"MouseVirtual")));
        Click(app, ActiveEnd); Input.buttons.Clear();
        Check(!g.practicing && !g.SparringActive && g.mode == "world", "The actual live End button returns safely to the mentor");
        g.save.practiceBits = 0; g.save.flags.Add("core_1"); Check(g.SparringReady, "Mature first-core saves can use advanced practice without replaying the tutorial");
        Console.WriteLine("PASS sparring actual mentor E/Alpha3 gating, original numbered basic practice, completed-basics and mature first-core unlock, and live End button; NATIVE_UNVERIFIED");
    }
    public static void SparringProductionAttacksDodgeSkillAndIsolation(FrontierRuntime app)
    {
        for (int profession = 0; profession < 3; profession++) for (int source = 0; source < 2; source++)
        {
            Prepare(app, profession); var g = app.Game; Start(app, "-input-" + profession + "-" + source, source == 1);
            float health = g.save.health; int food = g.save.inventory[6], tea = g.save.tea, coins = g.save.coins, xp = g.save.xp;
            var inventory = (int[])g.save.inventory.Clone(); var mastery = (int[])g.save.mastery.Clone(); var kills = (int[])g.save.kills.Clone();
            Mouse(app, g.enemies[0].Position); g.SetAim((g.enemies[0].Position - g.Position).Normalized);
            KeyCode attackKey = source == 0 ? KeyCode.J : KeyCode.JoystickButton2;
            Input.held.Add(attackKey); Update(app); Input.held.Clear();
            Check(g.playerAction != null && g.playerAction.kind == "attack", "Real J/X begins the profession's normal attack in advanced practice");
            Input.axes["Horizontal"] = 1; Input.held.Add(source == 0 ? KeyCode.D : KeyCode.JoystickButton2);
            Input.keys.Add(source == 0 ? KeyCode.Space : KeyCode.JoystickButton0);
            Input.keys.Add(source == 0 ? KeyCode.Q : KeyCode.JoystickButton3); Update(app); Input.held.Clear(); Input.axes.Clear();
            Check(g.dashTime > 0 && g.playerAction == null && g.skillCooldown <= 0 && g.save.energy < 81,
                "Real Space/A and Q/Y share a frame: dodge takes priority and buffers the original profession technique");
            ActorAction technique = null;
            for (int frame = 0; frame < 20 && technique == null; frame++)
            { Update(app); if (g.playerAction != null && g.playerAction.kind == "skill") technique = g.playerAction; }
            Check(technique != null && g.dashTime <= 0 && g.skillCooldown > 0,
                "The real input buffer starts Q/Y once after the ordinary dash finishes");
            Key(app, KeyCode.F); Key(app, KeyCode.T); Key(app, KeyCode.Z); Frames(app, 30);
            Check(g.save.health == health && g.save.inventory[6] == food && g.save.tea == tea && g.save.companion == 0 && g.companionAction == null,
                "Advanced practice protects actual health and F/T supplies and isolates the selected companion without unequipping them");
            Check(g.save.coins == coins && g.save.xp == xp, "Sparring hits cannot award ordinary enemy currency or experience");
            for (int i = 0; i < inventory.Length; i++) Check(g.save.inventory[i] == inventory[i], "The actual combat input path cannot farm resource drops");
            for (int i = 0; i < mastery.Length; i++) Check(g.save.mastery[i] == mastery[i], "Advanced practice does not farm weapon mastery");
            for (int i = 0; i < kills.Length; i++) Check(g.save.kills[i] == kills[i], "Practice combat preserves every mature map's kill counter");
            g.EndPractice(); Check(g.enemies.Count == 0 && g.projectiles.Count == 0 && g.playerAction == null,
                "Ending the actual input sequence removes all practice actors and pending contact");
        }
        Console.WriteLine("PASS sparring production J/X, same-frame Space/A + Q/Y and buffered technique for all three professions, safe health/supplies, companion isolation and no loot/mastery/progress farming; NATIVE_UNVERIFIED");
    }
    public static void SparringManualResultsAcceptedArtAndGui(FrontierRuntime app)
    {
        for (int language = 0; language < 2; language++) for (int large = 0; large < 2; large++) for (int policy = 0; policy < 2; policy++)
        {
            int profession = (language + large + policy) % 3; Prepare(app, profession, language == 1, large == 1); var g = app.Game;
            Screen.width = policy == 0 ? 960 : 1920; Screen.height = policy == 0 ? 600 : 1080;
            g.save.effectsQuality = policy == 0 ? 0 : 2; g.save.reducedMotion = policy == 0; Call(app, "Resize");
            string suffix = (language == 1 ? "-en" : "-zh") + (large == 1 ? "-large" : "") + (policy == 0 ? "-low960" : "-full1920");
            Point origin = Start(app, suffix); int coins = g.save.coins, points = g.save.skillPoints;
            for (int stage = 0; stage < 3; stage++)
            {
                Check(g.SparringStage == stage && !g.SparringAwaitingNext, "Each real manual transition starts the next authored drill");
                Check(g.enemies.Count == (stage == 2 ? 2 : 1) && g.enemies.TrueForAll(e => !e.boss), "The advanced wave preserves its real hunter/heavy/archer roster");
                for (int frame = 0; frame < 200 && !g.enemies.Exists(e => e.state == 1); frame++) Update(app);
                Check(g.enemies.Exists(e => e.state == 1), "Normal accepted enemy AI produces an actual live warning"); Draw(app, "stage" + stage + "-warning" + suffix);
                Check(HasText(g.L("训练 ", "Drill ") + (stage + 1) + "/3 · " + g.SparringTitle),
                    "The actual compact HUD identifies the current sparring drill");
                Texture2D monster = Get<Texture2D>(app, "monsterSheet"), walk = Get<Texture2D>(app, "monsterWalkSheet"), vertical = Get<Texture2D>(app, "enemyArcherVertical");
                foreach (Enemy enemy in g.enemies)
                {
                    float row = (2 - enemy.kind) / 3f;
                    bool authoredBody = GUI.textureCalls.Exists(c => c.texture == monster && Math.Abs(Math.Abs(c.uv.width) - 1f/6) < .001f && Math.Abs(c.uv.y - row) < .001f)
                        || GUI.textureCalls.Exists(c => c.texture == walk && Math.Abs(Math.Abs(c.uv.width) - .25f) < .001f && Math.Abs(c.uv.y - row) < .001f)
                        || enemy.kind == 1 && GUI.textureCalls.Exists(c => c.texture == vertical && Math.Abs(Math.Abs(c.uv.width) - 1f/3) < .001f);
                    Check(authoredBody, "The actual renderer uses each roster member's accepted monster/walk/archer frames rather than relabeling the heavy training rig");
                }
                Check(GUI.buttonCalls.Exists(b => b.rect.x == ActiveEnd.x && b.rect.y == ActiveEnd.y && b.enabled), "The live safe-exit control remains present at every graphics policy");
                Check((bool)Call(app, "PointerOverInterface", new Point(ActiveEnd.x + 5, ActiveEnd.y + 5)), "The actual sparring exit button protects aim and firing from its click");
                Check(!(bool)Call(app, "PointerOverInterface", new Point(400, 70)), "Read-only drill hints allow actual world aiming through the compact HUD");
                ClearWaveThroughInput(app, policy == 1); Draw(app, "stage" + stage + "-result" + suffix);
                Check(GUI.textureCalls.Exists(c => c.rect.x == 344 && c.rect.y == 190 && c.rect.width == 592 && c.rect.height == 260 && c.color.a >= .95f),
                    "The cleared result panel remains opaque and readable over the resting hero instead of fading as a combat threat");
                int quietDrawCalls = GUI.textureCalls.Count; Call(app, "DrawHudThreatWitness");
                Check(GUI.textureCalls.Count == quietDrawCalls,
                    "A cleared result never redraws the resting hero over its tactical feedback");
                Check(g.SparringFinished == (stage == 2), "Only the real third result marks the whole practice clear");
                Check(HasText(stage == 2 ? g.L("战术训练完成", "Tactics drills complete") : g.L("本项完成 · ", "Drill complete · ") + g.SparringTitle),
                    "The actual manual result panel renders its current encounter or full-clear title");
                float elapsed = g.SparringTime; int hits = g.SparringHitsTaken; Frames(app, 30);
                Check(g.SparringTime == elapsed && g.SparringHitsTaken == hits && g.enemies.Count == 0, "Reading a result advances neither practice timing nor combat or enemies");
                if (stage < 2)
                {
                    if (stage == 0) { Click(app, Next); Check(!g.SparringAwaitingNext && g.SparringStage == 1, "The actual result Next button starts drill two"); }
                    else
                    {
                        KeyCode nextKey = policy == 1 ? KeyCode.JoystickButton1 : KeyCode.Return;
                        Key(app, nextKey);
                        Check(!g.SparringAwaitingNext && g.SparringStage == 2,
                            "Actual B/Enter on a cleared result starts drill three without opening a resident conversation");
                    }
                }
            }
            Check(g.Has("sparring_complete") && g.save.coins == coins + 25 && g.save.skillPoints == points + 1,
                "The first actual full clear grants exactly the authored one-time 25 coins and one point");
            Draw(app, "full-clear" + suffix); Click(app, Retry);
            Check(g.SparringActive && g.SparringStage == 0 && !g.SparringAwaitingNext && !g.SparringFinished && g.SparringHitsTaken == 0
                && g.SparringCounters == 0 && g.SparringBreaks == 0 && g.SparringFlanks == 0, "The actual Retry button restarts all drills and resets the attempt's feedback");
            for (int stage = 0; stage < 3; stage++)
            {
                ClearWaveThroughInput(app);
                if (stage < 2) Key(app, KeyCode.E);
            }
            Check(g.save.coins == coins + 25 && g.save.skillPoints == points + 1, "The actual repeated full attempt cannot farm the one-time lesson reward");
            Draw(app, "repeat-clear" + suffix);
            if (policy == 0) Click(app, QuietEnd);
            else Key(app, large == 0 ? KeyCode.JoystickButton1 : KeyCode.Return);
            Check(!g.SparringActive && !g.practicing && (g.Position - origin).Length < .001f, "The actual result Return button or B/Enter restores the mentor origin after repeated practice");
        }
        Console.WriteLine("PASS sparring production three-wave clears, accepted monster warnings, protected actual HUD buttons, manual Next/E/B/Enter, full-round Retry and result B/Enter exit, once-only reward, and GUI across both languages/text sizes and 960/1920 low/full policies; NATIVE_UNVERIFIED");
    }
    public static void SparringSaveStorePauseAndTravelBoundaries(FrontierRuntime app)
    {
        Prepare(app, 2); var g = app.Game; Point origin = Start(app, "-persistence");
        var baseline = g.save.sparringReturn; Check(baseline != null, "Starting the real lesson records its exact pre-training return resources");
        float worldTime = g.save.totalTime, foodUntil = g.save.foodUntil, teaUntil = g.save.teaUntil, mealUntil = g.save.mealUntil;
        Frames(app, 70); Check(g.save.totalTime == worldTime && g.SparringTime > 1 && g.save.foodUntil == foodUntil && g.save.teaUntil == teaUntil && g.save.mealUntil == mealUntil,
            "Real sparring Update advances only practice time and leaves world meal/tea/food duration intact");
        float sparringTime = g.SparringTime; Call(app, "OnApplicationPause", true); Frames(app, 30);
        Check(g.SparringTime == sparringTime, "Actual application pause freezes advanced practice"); Call(app, "OnApplicationPause", false);
        Key(app, KeyCode.Escape); Check(g.mode == "pause" && g.SparringActive, "The real Escape pause keeps the live attempt intact");
        sparringTime = g.SparringTime; Frames(app, 30); Check(g.SparringTime == sparringTime, "The actual pause menu freezes practice timing"); Draw(app, "pause"); Key(app, KeyCode.Escape);
        Check(g.mode == "world" && g.SparringActive, "Leaving the actual pause menu resumes the same attempt");
        Point livePosition = g.Position; int stage = g.SparringStage, enemies = g.enemies.Count; g.PrepareSave();
        Check(g.SparringActive && g.enemies.Count == enemies && g.SparringStage == stage && (g.Position - livePosition).Length < .001f,
            "Preparing the real autosave/manual payload does not exit or teleport the live practice");
        Check(g.save.snapshotRoom == -1 && g.save.worldEnemies == null && g.save.sparringReturn != null,
            "Practice actor snapshots are excluded while the canonical return record remains available");
        Check(SaveStore.Write(g.save, 0), "The production envelope saves the active training boundary"); string issue;
        SaveData saved = SaveStore.Read(0, out issue); Check(saved != null && issue == null && saved.sparringReturn != null,
            "The real JSON envelope and checksum retain the nullable practice return record");
        float health = baseline.health, energy = baseline.energy, heat = baseline.heat, cold = baseline.cold;
        g.Load(saved); g.Start();
        Check(g.save.room == 0 && !g.SparringActive && !g.practicing && g.save.sparringReturn == null && g.enemies.Count == 0
            && (g.Position - origin).Length < .001f && g.save.health == health && g.save.energy == energy && g.save.heat == heat && g.save.cold == cold,
            "Actual load stops the unsaved drill and restores the exact town origin and original health/energy/climate without phantom actors");
        Check(g.Has("sparring_runtime_mature_story") && g.save.totalTime == worldTime && g.save.foodUntil == foodUntil && g.save.teaUntil == teaUntil && g.save.mealUntil == mealUntil,
            "The production roundtrip retains mature story and every real preparation timer"); Draw(app, "save-resume");
        Start(app, "-travel"); g.Enter(1);
        Check(!g.SparringActive && !g.practicing && g.save.sparringReturn == null && g.save.room == 1 && g.enemies.TrueForAll(e => e.id < 75000 || e.id > 75099),
            "The actual map travel ends training before spawning only the mature forest roster");
        Prepare(app); g = app.Game; string legacy = JsonUtility.ToJson(g.save);
        legacy = System.Text.RegularExpressions.Regex.Replace(legacy, ",\"sparringReturn\":null", "");
        Check(!legacy.Contains("\"sparringReturn\""), "The legacy fixture actually omits the optional return field rather than only assigning null");
        Directory.CreateDirectory(Application.persistentDataPath); File.WriteAllText(SaveStore.PathFor(1), legacy);
        saved = SaveStore.Read(1, out issue); Check(saved != null && issue == null && saved.sparringReturn == null,
            "The production legacy decoder accepts a save without the new optional return record");
        g.Load(saved); g.Start(); Check(!g.SparringActive && !g.practicing && g.Has("sparring_runtime_mature_story"),
            "A legacy save preserves story without inventing a practice attempt"); Draw(app, "legacy");
        Console.WriteLine("PASS sparring actual application/Escape pause, frozen world preparations, live PrepareSave without teleport, SaveStore/checksum return-record recovery, real travel boundary and omitted-field legacy decoding; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous = Application.persistentDataPath; string directory = Path.Combine(Path.GetTempPath(), "starfall-sparring-check-" + Guid.NewGuid());
        Application.persistentDataPath = directory; captureFolder = args.Length > 0 ? args[0] : null;
        if (captureFolder != null) Directory.CreateDirectory(captureFolder);
        var app = new FrontierRuntime(); int failed = 0;
        Action<FrontierRuntime>[] tests = { SparringMentorUnlockAndOriginalPractice, SparringProductionAttacksDodgeSkillAndIsolation,
            SparringManualResultsAcceptedArtAndGui, SparringSaveStorePauseAndTravelBoundaries };
        try
        {
            Call(app, "Awake"); Check(app.Game != null, "Actual runtime Awake loads the shipped complete campaign");
            foreach (var test in tests)
            {
                try { test(app); }
                catch (Exception error) { failed++; Console.WriteLine("FAIL " + test.Method.Name + ": " + (error.InnerException ?? error).Message); }
            }
            app.EndVisualReview(); Call(app, "OnDestroy");
        }
        finally { Application.persistentDataPath = previous; Input.Clear(); GUI.queuedClick = null; if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        Console.WriteLine("Runtime mentor sparring: " + (tests.Length - failed) + "/" + tests.Length + " groups passed; NATIVE_UNVERIFIED"); return failed == 0 ? 0 : 1;
    }
}
