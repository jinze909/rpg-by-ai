// Actual production Update/OnGUI and SaveStore against controllable Unity declarations.
// These input and renderer checks are offline evidence, never a native Unity playtest.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Starfall;
using UnityEngine;

public static class RuntimeControllerAimChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string captureFolder;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static object Call(FrontierRuntime app, string method, params object[] args)
    { var m = typeof(FrontierRuntime).GetMethod(method, Private); Check(m != null, "Missing production runtime hook " + method); return m.Invoke(app, args); }
    private static void Set(FrontierRuntime app, string field, object value)
    { typeof(FrontierRuntime).GetField(field, Private).SetValue(app, value); }
    private static T Get<T>(FrontierRuntime app, string field)
    { return (T)typeof(FrontierRuntime).GetField(field, Private).GetValue(app); }
    private static void Update(FrontierRuntime app) { Call(app, "Update"); Input.keys.Clear(); }
    private static void Frames(FrontierRuntime app, int frames) { for (int i = 0; i < frames; i++) Update(app); }
    private static void Draw(FrontierRuntime app, string capture = null)
    {
        GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app, "OnGUI");
        if (captureFolder != null && capture != null)
        {
            var commands = GUI.commands.ToArray(); var textures = GUI.textureCalls.ToArray(); var buttons = GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder, "qa-controller-" + capture);
            GUI.commands.AddRange(commands); GUI.textureCalls.AddRange(textures); GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static bool HasText(string text) { return GUI.commands.Exists(c => c.Contains("\"text\":" + GUI.S(text))); }
    private static void Click(FrontierRuntime app, Rect button)
    { GUI.queuedClick = button; Draw(app); Check(!GUI.queuedClick.HasValue, "The actual drawn controller settings button accepts its click"); }
    private static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key); Update(app); }
    private static void Stick(float x, float y, bool alternate = false)
    { Input.axes[alternate ? "Alt Aim Horizontal" : "Aim Horizontal"] = x; Input.axes[alternate ? "Alt Aim Vertical" : "Aim Vertical"] = y; }
    private static void Center(FrontierRuntime app)
    { Stick(0, 0); Stick(0, 0, true); Update(app); }
    private static void Mouse(FrontierRuntime app, Point world)
    {
        Point screen = (Point)Call(app, "ScreenPoint", world); float scale = Get<float>(app, "scale");
        Input.mousePosition = new Vector3(Get<float>(app, "offsetX") + screen.x * scale, Screen.height - Get<float>(app, "offsetY") - screen.y * scale, 0);
    }
    private static void Prepare(FrontierRuntime app, int profession = 1)
    {
        Input.Clear(); GUI.queuedClick = null; Screen.width = 1280; Screen.height = 800; Time.unscaledDeltaTime = .02f;
        app.BeginVisualReview(profession, 1); app.VisualReviewPaused = false; var g = app.Game;
        g.enemies.Clear(); g.save.companion = -1; g.save.reducedMotion = true; g.save.showGuidance = false;
        g.invulnerable = 100; g.toastTime = 0; g.save.controllerAimProfile = 0; g.save.controllerAimInvertY = false;
        Check(app.SetVisualReviewPosition(new Point(12, 14)), "The fixture starts on shipped walkable forest ground");
        Set(app, "camera", g.Position); Set(app, "pointerSeen", false); Set(app, "controllerUsed", false); Set(app, "applicationPaused", false);
        Set(app, "combatHudTime", 0f); Call(app, "Resize"); Mouse(app, g.Position + new Point(4, 0)); Center(app);
    }
    private static void Direction(Point actual, Point expected, string message)
    { Check((actual.Normalized - expected.Normalized).Length < .001f, message + " (actual " + actual.x + "," + actual.y + ")"); }
    private static void Acquire(FrontierRuntime app, float x, float y, bool alternate = false)
    { Center(app); Stick(x, y, alternate); Update(app); Check(app.ControllerAimActive, "A fresh valid right-stick deflection owns combat aim"); }
    private static void WaitActionRelease(FrontierRuntime app, ActorAction action)
    { for (int i = 0; i < 40 && !action.released; i++) Update(app); Check(action.released, "The unmodified action reaches real release"); }

    public static void TwinStickBackpedalProjectileAndProfiles(FrontierRuntime app)
    {
        for (int profession = 1; profession <= 2; profession++)
        for (int profile = 0; profile <= 1; profile++)
        foreach (bool inverted in new[] { false, true })
        {
            Prepare(app, profession); var g = app.Game; g.save.controllerAimProfile = profile; g.save.controllerAimInvertY = inverted;
            bool alternate = profile == 1; Acquire(app, 1, 1, alternate);
            Point expected = new Point(1, inverted ? 1 : -1).Normalized;
            Direction(g.aim, expected, "Both physical axis profiles and inversion steer the displayed normalized diagonal");
            Check(!g.aimTarget.HasValue, "Controller aim clears the cursor world coordinate");
            Point before = g.Position; Input.axes["Horizontal"] = -1;
            Input.held.Add(KeyCode.JoystickButton2); Update(app); Input.held.Clear(); Input.axes["Horizontal"] = 0;
            ActorAction attack = g.playerAction;
            Check(g.Position.x < before.x && attack != null && attack.kind == "attack", "Actual left-stick backpedal and held X attack share one frame");
            Direction(attack.direction, expected, "X aims independently of backward locomotion");
            Check(!attack.target.HasValue, "A new controller attack captures direction without a stale cursor target");
            WaitActionRelease(app, attack); Projectile shot = g.projectiles.Find(p => p.friendly);
            Check(shot != null, "Actual bow/staff attack creates its original friendly projectile");
            Direction(shot.velocity, expected, "Released bow and staff projectiles follow right-stick diagonal rather than backpedal direction");
        }
        Console.WriteLine("PASS actual twin-stick backpedal X shots for bow/staff, both axis profiles, inversion, normalized diagonals and no stale pointer target");
    }

    public static void DeadzoneHeadingRetentionAndExplicitHandoffs(FrontierRuntime app)
    {
        Prepare(app); var g = app.Game;
        Stick(.16f, .16f); Update(app); Check(!app.ControllerAimActive, "Diagonal stick drift inside the radial deadzone never acquires aim");
        Direction(g.aim, new Point(1, 0), "Drift preserves pointer heading");
        Stick(.2f, .2f); Update(app); Check(app.ControllerAimActive, "A diagonal outside the radial deadzone acquires aim even when each axis is below .25");
        Direction(g.aim, new Point(1, -1), "The small deliberate diagonal is normalized rather than rejected per axis");
        Acquire(app, 0, 1); Direction(g.aim, new Point(0, -1), "Fresh north stick deflection steers combat");
        Stick(0, 0); Input.axes["Horizontal"] = -1; Frames(app, 8); Input.axes["Horizontal"] = 0;
        Check(app.ControllerAimActive, "Centering the right stick retains acquired heading while the left stick moves");
        Direction(g.aim, new Point(0, -1), "Stationary mouse and backward locomotion cannot steal the retained right-stick heading");
        Stick(.08f, -.07f); Frames(app, 4); Direction(g.aim, new Point(0, -1), "Small subsequent drift does not change retained heading");
        Stick(0, 1); Input.held.Add(KeyCode.D); Input.axes["Horizontal"] = 1; Update(app);
        Check(!app.ControllerAimActive, "Deliberate keyboard movement wins over a held right stick");
        Input.held.Clear(); Input.axes["Horizontal"] = 0; Frames(app, 2);
        Check(!app.ControllerAimActive, "Releasing keyboard movement does not let the still-held stick immediately reclaim aim");
        Acquire(app, -1, 0); Direction(g.aim, new Point(-1, 0), "Center then deflect permits deliberate controller reacquisition");
        Mouse(app, g.Position + new Point(0, 4)); Update(app);
        Check(!app.ControllerAimActive && g.aimTarget.HasValue, "A real mouse move explicitly restores cursor ownership");
        Direction(g.aim, new Point(0, 1), "The explicit pointer takeover aims at its actual world position");
        Frames(app, 2); Check(!app.ControllerAimActive, "A still-deflected stick cannot immediately fight the pointer takeover");
        Acquire(app, 1, 0); Input.held.Add(KeyCode.J); Update(app); Input.held.Clear();
        Check(!app.ControllerAimActive && g.playerAction != null, "Deliberate keyboard J takes over during controller aim and still attacks");
        Prepare(app); g = app.Game; Input.axes["Horizontal"] = -1;
        Input.mousePosition = new Vector3(500, Screen.height - 745, 0);
        Input.keys.Add(KeyCode.JoystickButton2); Input.held.Add(KeyCode.JoystickButton2); Update(app); Input.held.Clear(); Input.axes["Horizontal"] = 0;
        Check(!g.aimTarget.HasValue && g.playerAction != null && g.playerAction.kind == "attack",
            "A fresh X event preserves legacy controller priority over a same-frame cursor move onto the HUD without requiring right-stick acquisition");
        Direction(g.playerAction.direction, new Point(-1, 0), "The legacy fresh-pad attack follows actual left-stick facing without shooting backward at a stale cursor world target");
        Prepare(app); g = app.Game; Acquire(app, 0, 1); Input.buttons.Add(0); Update(app); Input.buttons.Clear();
        Check(!app.ControllerAimActive && g.playerAction != null && g.playerAction.kind == "attack" && g.aimTarget.HasValue,
            "A real stationary mouse click deliberately restores cursor ownership and attacks without needing pointer motion");
        Direction(g.playerAction.direction, new Point(1, 0), "Stationary click follows its actual east cursor rather than the old controller north heading");
        Prepare(app); g = app.Game;
        foreach (Point invalid in new[] { new Point(float.NaN, 1), new Point(1, float.PositiveInfinity), new Point(float.NegativeInfinity, 0) })
        {
            Stick(invalid.x, invalid.y); Update(app); Check(!app.ControllerAimActive && !float.IsNaN(g.aim.x) && !float.IsNaN(g.aim.y),
                "Malformed axis samples cannot acquire combat aim or contaminate the finite live heading");
        }
        Prepare(app); g = app.Game; g.save.controllerAimProfile = 2; Stick(1, 1); Stick(-1, -1, true); Update(app);
        Check(!app.ControllerAimActive, "The explicit disabled profile ignores both right-stick axis sets");
        Console.WriteLine("PASS radial drift rejection, heading hold after centering/backpedal, stationary-pointer priority, keyboard and moved-pointer takeover, neutral rearming and disabled profile");
    }

    public static void ControllerDashAndQueuedTechniqueSnapshots(FrontierRuntime app)
    {
        Prepare(app); var g = app.Game; Acquire(app, 0, 1);
        Input.keys.Add(KeyCode.JoystickButton0); Update(app);
        Check(g.dashTime > 0, "Actual stationary A starts a dodge using the already acquired aim");
        Direction(g.VisualFacing, new Point(0, -1), "A stationary controller dodge keeps its original aimed-direction fallback");
        Prepare(app); g = app.Game; Acquire(app, 0, 1);
        Input.axes["Horizontal"] = 1; Input.keys.Add(KeyCode.JoystickButton0); Update(app); Input.axes["Horizontal"] = 0;
        Check(g.dashTime > 0 && Math.Abs(g.save.energy - 80) < .1f, "Actual A dash preserves its original cost");
        Direction(g.VisualFacing, new Point(1, 0), "The dash remains directed by locomotion independently of northward combat aim");
        for (int i = 0; i < 20 && g.dashTime > 0; i++) Update(app);
        Input.held.Add(KeyCode.JoystickButton2); Update(app); Input.held.Clear(); ActorAction attack = g.playerAction;
        Check(attack != null && attack.kind == "attack", "X begins an ordinary bow attack after the real dodge");
        Frames(app, 9); float energy = g.save.energy; Input.keys.Add(KeyCode.JoystickButton3); Update(app);
        Check(g.playerAction == attack && g.skillCooldown <= 0 && g.save.energy >= energy, "Actual Y queues during attack recovery without cancelling or spending early");
        Stick(0, -1); Input.held.Add(KeyCode.JoystickButton2); ActorAction cast = null; int casts = 0;
        for (int frame = 0; frame < 230; frame++)
        {
            ActorAction before = g.playerAction; Update(app); ActorAction after = g.playerAction;
            if (after != before && after != null && after.kind == "skill")
            {
                casts++; cast = after; Direction(cast.direction, new Point(0, -1), "Queued controller Y retains its pressed north heading after the live stick turns south");
                Check(!cast.target.HasValue, "Queued controller Q owns a directional snapshot without inheriting cursor coordinates");
            }
        }
        Input.held.Clear(); Check(cast != null && cast.released && casts == 1, "One actual Y input casts once even through held X beyond the whole skill cooldown");
        Console.WriteLine("PASS unchanged actual A locomotion-directed dash/cost and buffered Y direction snapshot/one-shot consumption through held X");
    }

    public static void ControllerOwnershipResetsAtWorldBoundaries(FrontierRuntime app)
    {
        foreach (string boundary in new[] { "pause", "application", "focus", "travel", "travel-same-room", "load", "load-same-instance", "death" })
        {
            Prepare(app); var g = app.Game; Acquire(app, 1, 0);
            if (boundary == "pause") { Key(app, KeyCode.Escape); Frames(app, 2); Check(g.mode == "pause", "Actual Escape opens pause"); Key(app, KeyCode.Escape); }
            else if (boundary == "application") { Call(app, "OnApplicationPause", true); Frames(app, 2); Call(app, "OnApplicationPause", false); }
            else if (boundary == "focus") { Call(app, "OnApplicationFocus", false); Frames(app, 2); Check(g.mode == "pause", "Lost application focus uses the original safe pause"); Key(app, KeyCode.Escape); }
            else if (boundary == "travel") { g.Enter(0); g.enemies.Clear(); }
            else if (boundary == "travel-same-room") { int room = g.save.room; g.Enter(room); g.enemies.Clear(); Check(g.save.room == room, "The same-room travel fixture preserves region identity"); }
            else if (boundary == "load-same-instance") { g.PrepareSave(); SaveData instance = g.save; g.Load(instance); g.Start(); g.enemies.Clear(); Check(ReferenceEquals(g.save, instance), "The load fixture exercises the same SaveData instance rather than relying on reference replacement"); }
            else if (boundary == "load") { g.PrepareSave(); SaveData copy = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(g.save)); g.Load(copy); g.Start(); g.enemies.Clear(); }
            else
            {
                g.invulnerable = 0; g.hazards.Add(new Hazard { position = g.Position, timer = .001f, radius = 1, damage = 9999, kind = "tide" });
                Update(app); Check(g.Dying, "The actual hazard produces the original death/recovery flow"); Frames(app, 60); Check(!g.Dying && g.save.room == 0, "The unchanged death recovery returns home"); g.enemies.Clear();
            }
            Update(app); Check(!app.ControllerAimActive, boundary + " clears ownership and refuses a still-held stick");
            Frames(app, 2); Check(!app.ControllerAimActive, boundary + " does not silently reactivate before neutral");
            Acquire(app, 0, -1); Direction(g.aim, new Point(0, 1), boundary + " allows a fresh centered/redeflected stick");
        }
        Prepare(app); Acquire(app, 1, 0); Key(app, KeyCode.I); int projectiles = app.Game.projectiles.Count;
        Input.keys.Add(KeyCode.JoystickButton3); Update(app); Input.held.Add(KeyCode.JoystickButton2); Frames(app, 20); Input.held.Clear();
        Check(app.Game.mode == "inventory" && app.Game.playerAction == null && app.Game.projectiles.Count == projectiles && !app.ControllerAimActive,
            "Right-stick movement and attack/technique pad inputs cannot shoot or claim world aim from a menu");
        Console.WriteLine("PASS pause, application pause, travel, load and actual death clear/rearm aim; menu inputs cannot cast or fire");
    }

    public static void ControllerSettingsProductionSaveCompatibility(FrontierRuntime app)
    {
        Prepare(app); var g = app.Game; g.save.controllerAimProfile = 1; g.save.controllerAimInvertY = true;
        g.save.flags.Add("controller_preserve_story"); g.save.coins = 137; g.save.mealKind = 1; g.save.mealUntil = g.save.totalTime + 100;
        g.PrepareSave(); Check(SaveStore.Write(g.save, 0), "Production envelope stores controller preferences"); string issue;
        SaveData saved = SaveStore.Read(0, out issue); Check(saved != null && issue == null, "Controller preference envelope verifies its real JSON checksum");
        g.Load(saved); g.Start(); Check(g.save.controllerAimProfile == 1 && g.save.controllerAimInvertY && g.save.coins == 137 && g.Has("controller_preserve_story") && g.save.mealKind == 1,
            "Actual load retains selected profile/inversion alongside mature story, resources and kitchen progress");
        string oldJson = JsonUtility.ToJson(g.save); oldJson = Regex.Replace(oldJson, ",?\"controllerAimProfile\":-?\\d+", "");
        oldJson = Regex.Replace(oldJson, ",?\"controllerAimInvertY\":(?:true|false)", "");
        File.WriteAllText(SaveStore.PathFor(1), oldJson); saved = SaveStore.Read(1, out issue);
        Check(saved != null && issue == null, "Production legacy decoder accepts actual JSON lacking the two new fields"); g.Load(saved); g.Start();
        Check(g.save.controllerAimProfile == 0 && !g.save.controllerAimInvertY && g.save.coins == 137 && g.Has("controller_preserve_story"),
            "Absent fields migrate to normal default axes without inventing inversion or losing story and resources");
        foreach (int invalid in new[] { -31, 91 })
        {
            saved = JsonUtility.FromJson<SaveData>(oldJson); saved.controllerAimProfile = invalid; g.Load(saved); g.Start();
            Check(g.save.controllerAimProfile >= 0 && g.save.controllerAimProfile <= 2 && g.save.coins == 137, "Malformed controller profiles are sanitized without losing mature save progress");
        }
        Console.WriteLine("PASS production SaveStore/checksum profile and inversion roundtrip, absent-field legacy saves and malformed-profile recovery preserve mature state");
    }

    private static double LinearChannel(float value)
    { return value <= .04045f ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4); }
    private static double Luminance(Color color)
    { return .2126 * LinearChannel(color.r) + .7152 * LinearChannel(color.g) + .0722 * LinearChannel(color.b); }
    public static void ControllerActualSettingsAndStaticReticle(FrontierRuntime app)
    {
        for (int language = 0; language < 2; language++) for (int large = 0; large < 2; large++)
        foreach (int width in new[] { 960, 1280, 1920 })
        {
            Prepare(app); var g = app.Game; g.save.english = language == 1; g.save.largeText = large == 1;
            Screen.width = width; Screen.height = width == 960 ? 600 : width == 1920 ? 1080 : 800; Call(app, "Resize");
            string suffix = (language == 1 ? "-en" : "-zh") + (large == 1 ? "-large" : "") + "-" + width;
            int coins = g.save.coins, weapon = g.save.weapon; float health = g.save.health;
            Key(app, KeyCode.Escape); Draw(app, "pause" + suffix);
            Check(g.mode == "pause" && HasText(g.L("摇杆瞄准设置", "Controller aiming")), "The actual pause screen exposes the new calibration entry in both languages");
            Click(app, new Rect(279, 590, 300, 34)); Check(g.mode == "controls", "The real pause button enters controller settings");
            Draw(app, "controls-centered" + suffix);
            Check(HasText(g.L("中心区域：等待推动摇杆", "Center zone: push the stick to aim")), "A neutral actual stick has an explicit center-zone status");
            float time = g.save.totalTime; Stick(1, 1); Input.held.Add(KeyCode.JoystickButton2); Input.keys.Add(KeyCode.JoystickButton3); Frames(app, 20); Input.held.Clear();
            Draw(app, "controls-standard" + suffix);
            Check(g.save.totalTime == time && g.playerAction == null && g.projectiles.Count == 0 && !app.ControllerAimActive,
                "Calibration really pauses the world and excludes X/Y combat even while it reads the stick");
            Check(HasText(g.L("已读到方向", "Direction detected")) && GUI.textureCalls.Exists(c => c.rect.width == 8 && c.rect.height == 8 && Math.Abs(c.rect.x - 587) < .01f && Math.Abs(c.rect.y - 379) < .01f),
                "The actual standard-axis calibration marker visibly moves right/up at full deflection");
            Click(app, new Rect(597, 180, 301, 44));
            Check(g.save.controllerAimProfile == 1, "The actual alternate-profile button changes the saved preference");
            Stick(0, 0); Stick(-1, 1, true); Update(app); Draw(app, "controls-alternate" + suffix);
            Check(GUI.textureCalls.Exists(c => c.rect.width == 8 && c.rect.height == 8 && Math.Abs(c.rect.x - 423) < .01f && Math.Abs(c.rect.y - 379) < .01f),
                "The alternate calibration reads its own physical axes, not the default pair");
            Click(app, new Rect(279, 248, 460, 44)); Draw(app, "controls-inverted" + suffix);
            Check(g.save.controllerAimInvertY && HasText(g.L("反转上下 ", "Invert vertical ") + "ON") &&
                GUI.textureCalls.Exists(c => c.rect.width == 8 && c.rect.height == 8 && Math.Abs(c.rect.x - 423) < .01f && Math.Abs(c.rect.y - 543) < .01f),
                "The actual invert control persists its state and immediately reverses the displayed vertical sample");
            Click(app, new Rect(915, 180, 301, 44)); Draw(app, "controls-off" + suffix);
            Check(g.save.controllerAimProfile == 2 && HasText(g.L("已关闭：沿移动方向瞄准", "Off: aim follows movement")) &&
                GUI.buttonCalls.Exists(b => b.rect.x == 279 && b.rect.y == 248 && !b.enabled),
                "Movement aiming visibly disables stick acquisition and the irrelevant inversion button");
            Click(app, new Rect(279, 180, 301, 44)); Click(app, new Rect(279, 248, 460, 44));
            Check(g.save.controllerAimProfile == 0 && !g.save.controllerAimInvertY, "The real controls restore standard non-inverted preferences");
            Click(app, new Rect(279, 661, 938, 45)); Check(g.mode == "pause", "Back to settings retains the pause parent"); Key(app, KeyCode.Escape);
            Check(g.mode == "world" && !app.ControllerAimActive && g.save.coins == coins && g.save.weapon == weapon && g.save.health == health,
                "Leaving calibration keeps mature resources/loadout intact and requires a centered fresh acquisition");
            Stick(0, 0, true); Acquire(app, 1, 0); g.save.effectsQuality = 0; g.save.reducedMotion = true;
            Draw(app, "world-reticle" + suffix); GUI.textureCalls.Clear(); GUI.commands.Clear(); Call(app, "DrawControllerAimReticle");
            Check(GUI.textureCalls.Count == 4, "The direction reticle keeps both dark outlines and bright marks at minimal effects/reduced motion");
            Point expected = (Point)Call(app, "ScreenPoint", g.Position + g.aim * 1.8f);
            foreach (GUI.TextureCall texture in GUI.textureCalls)
            {
                Check(Math.Abs(texture.rect.x + texture.rect.width / 2 - expected.x) < .01f && Math.Abs(texture.rect.y + texture.rect.height / 2 - expected.y) < .01f,
                    "Every outlined aim marker centers on the live fixed-distance heading, without implying a cursor lock");
                if (texture.rect.width == 12 || texture.rect.height == 12)
                    Check(texture.color.a >= .9f && Luminance(texture.color) < .01, "The permanent dark reticle backing remains opaque enough for scene contrast");
                else Check(Luminance(texture.color) > .8 && texture.color.a >= .99f, "The static aim marker remains bright at all presentation policies");
            }
            // Explicitly staged camera: exercise draw ordering under a real HUD
            // plate rather than claiming ordinary camera-follow reaches here.
            Point reticleWorld = g.Position + g.aim * 1.8f;
            Set(app, "camera", new Point(reticleWorld.x - (1110 - 640f) / 36, reticleWorld.y - (315 - 390f) / 36));
            Draw(app, "world-reticle-hud-staged" + suffix);
            Point staged = (Point)Call(app, "ScreenPoint", reticleWorld);
            int markerIndex = GUI.textureCalls.FindLastIndex(c => (c.rect.width == 8 && c.rect.height == 2 || c.rect.width == 2 && c.rect.height == 8) &&
                Math.Abs(c.rect.x + c.rect.width / 2 - staged.x) < .01f && Math.Abs(c.rect.y + c.rect.height / 2 - staged.y) < .01f && Luminance(c.color) > .8);
            int plateIndex = GUI.textureCalls.FindLastIndex(c => c.texture == Texture2D.whiteTexture && c.rect.width > 50 && c.rect.height > 30 &&
                staged.x >= c.rect.x && staged.y >= c.rect.y && staged.x < c.rect.x + c.rect.width && staged.y < c.rect.y + c.rect.height);
            Check(plateIndex >= 0 && markerIndex > plateIndex,
                "Full production OnGUI paints the bright controller direction marker after the covering HUD plate in the staged overlap policy fixture");
            Set(app, "camera", g.Position);
            Mouse(app, g.Position + new Point(0, 4)); Update(app); GUI.textureCalls.Clear(); Call(app, "DrawControllerAimReticle");
            Check(GUI.textureCalls.Count == 0, "The controller marker disappears immediately after real pointer takeover");
            Acquire(app, 1, 0); Key(app, KeyCode.Escape); GUI.textureCalls.Clear(); Call(app, "DrawControllerAimReticle");
            Check(GUI.textureCalls.Count == 0, "The controller marker never persists over pause or calibration screens");
        }
        Prepare(app); var titleGame = app.Game; titleGame.mode = "title"; Draw(app);
        Click(app, new Rect(332, 671, 252, 38)); Check(titleGame.mode == "pause", "The original title settings route still works");
        Click(app, new Rect(279, 590, 300, 34)); Check(titleGame.mode == "controls", "Title settings can open controller calibration before beginning an adventure");
        Click(app, new Rect(597, 180, 301, 44)); Click(app, new Rect(279, 248, 460, 44));
        Check(titleGame.save.controllerAimProfile == 1 && titleGame.save.controllerAimInvertY, "Title calibration can select the future adventure's physical stick layout");
        Key(app, KeyCode.Escape); Check(titleGame.mode == "pause", "Escape returns calibration to title-owned settings");
        Key(app, KeyCode.Escape); Check(titleGame.mode == "title", "The next Escape preserves the actual original title parent");
        Click(app, new Rect(64, 595, 252, 56));
        Check(titleGame.save.controllerAimProfile == 1 && titleGame.save.controllerAimInvertY && titleGame.mode == "dialog" && titleGame.Has("intro"),
            "The actual New adventure button carries chosen title calibration into a fresh story");
        Console.WriteLine("PASS actual pause/title controller settings, live axis/inversion marker, safe paused calibration, all preference buttons and static outlined world reticle across zh/en, large text and three resolutions");
    }

    public static int Main(string[] args)
    {
        string previous = Application.persistentDataPath; string directory = Path.Combine(Path.GetTempPath(), "starfall-controller-check-" + Guid.NewGuid());
        Application.persistentDataPath = directory; captureFolder = args.Length > 0 ? args[0] : null;
        if (captureFolder != null) Directory.CreateDirectory(captureFolder); var app = new FrontierRuntime(); int failed = 0;
        Action<FrontierRuntime>[] tests = { TwinStickBackpedalProjectileAndProfiles, DeadzoneHeadingRetentionAndExplicitHandoffs,
            ControllerDashAndQueuedTechniqueSnapshots, ControllerOwnershipResetsAtWorldBoundaries, ControllerSettingsProductionSaveCompatibility, ControllerActualSettingsAndStaticReticle };
        try
        {
            Call(app, "Awake"); Check(app.Game != null, "Actual runtime Awake loads the complete campaign");
            foreach (Action<FrontierRuntime> test in tests)
            { try { test(app); } catch (Exception error) { failed++; Console.WriteLine("FAIL " + test.Method.Name + ": " + (error.InnerException ?? error).Message); } }
            app.EndVisualReview(); Call(app, "OnDestroy");
        }
        finally { Application.persistentDataPath = previous; Input.Clear(); GUI.queuedClick = null; if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        Console.WriteLine("Runtime controller aim: " + (tests.Length - failed) + "/" + tests.Length + " groups passed; NATIVE_UNVERIFIED"); return failed == 0 ? 0 : 1;
    }
}
