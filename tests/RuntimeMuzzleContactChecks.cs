// Actual Update input, action release, projectile contact, feedback and OnGUI.
// Offline Unity declarations are semantic evidence, never native play acceptance.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeMuzzleContactChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string captureFolder, capturePrefix;
    private static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
    private static bool Near(float a, float b, float tolerance = .002f) { return Math.Abs(a - b) <= tolerance; }
    private static object Call(object value, string method, params object[] args)
    {
        var entry = value.GetType().GetMethod(method, Private | BindingFlags.Public);
        Check(entry != null, "Existing production entry point: " + method); return entry.Invoke(value, args);
    }
    private static void Set(object value, string field, object data) { value.GetType().GetField(field, Private).SetValue(value, data); }
    private static T Get<T>(object value, string field) { return (T)value.GetType().GetField(field, Private).GetValue(value); }
    private static void Update(FrontierRuntime app) { Call(app, "Update"); Input.keys.Clear(); Input.buttonDown.Clear(); }
    private static void Frames(FrontierRuntime app, int count) { for (int i = 0; i < count; i++) Update(app); }
    private static void Draw(FrontierRuntime app, string capture = null)
    {
        GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app, "OnGUI");
        if (captureFolder != null && capture != null)
        {
            var commands = GUI.commands.ToArray(); var textures = GUI.textureCalls.ToArray(); var buttons = GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder, capturePrefix + "-" + capture);
            GUI.commands.AddRange(commands); GUI.textureCalls.AddRange(textures); GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static AudioSource Audio(FrontierRuntime app) { return Get<AudioSource>(Get<FrontierAudio>(app, "audioSystem"), "effects"); }
    private static void Mouse(FrontierRuntime app, Point world)
    {
        Point screen = (Point)Call(app, "ScreenPoint", world); float scale = Get<float>(app, "scale");
        Input.mousePosition = new Vector3(Get<float>(app, "offsetX") + screen.x * scale,
            Screen.height - Get<float>(app, "offsetY") - screen.y * scale, 0);
    }
    private static void Prepare(FrontierRuntime app, int weapon)
    {
        Input.Clear(); GUI.queuedClick = null; GUI.matrix = Matrix4x4.identity; GUI.color = Color.white; GUI.enabled = true;
        Screen.width = 1280; Screen.height = 800; Time.unscaledDeltaTime = .02f; Event.current.type = EventType.Repaint;
        app.BeginVisualReview(weapon, 0); app.VisualReviewPaused = false; var g = app.Game;
        g.enemies.Clear(); g.hazards.Clear(); g.projectiles.Clear(); g.effects.Clear(); g.fallen.Clear();
        g.save.weapon = weapon; g.save.weaponStyles = new int[3]; g.save.companion = -1; g.save.reducedMotion = true;
        g.save.effectsQuality = 0; g.save.showGuidance = false; g.save.english = true; g.save.music = true;
        g.save.difficulty = 1; g.save.talents = new int[3]; g.save.relics = new[] { -1, -1 };
        g.save.health = g.save.maxHealth; g.save.energy = 100; g.save.totalTime = 100; g.save.mealUntil = 0;
        g.save.seed = 417; g.invulnerable = 0; g.toastTime = 0; g.save.controllerAimProfile = 0; g.save.controllerAimInvertY = false;
        Point feet = new Point(15, 14);
        Check(g.CanStand(feet) && app.SetVisualReviewPosition(feet) && (g.Position - feet).Length < .001f,
            "Fixture uses exact legal shipped town feet");
        Set(app, "camera", feet); Set(app, "clock", 0f); Set(app, "combatHudTime", 3f); Set(app, "applicationPaused", false);
        Set(app, "pointerSeen", false); Set(app, "pointerAimActive", false); Set(app, "controllerUsed", false);
        Set(app, "resumeTime", 0f); Set(app, "arrivalTime", 0f); Call(app, "Resize");
        Mouse(app, feet + new Point(4, 0)); Update(app); g.effects.Clear(); g.sounds.Clear(); Audio(app).heard.Clear();
    }
    private static Point Heading(int direction)
    { double angle = direction * Math.PI / 4; return new Point((float)Math.Cos(angle), (float)Math.Sin(angle)).Normalized; }
    private static Enemy ContactEnemy(FrontierRuntime app, Point heading)
    {
        Point p = app.Game.Position + heading * .10f;
        var enemy = new Enemy(99701, 0, p.x, p.y, 500) { stun = 100, timer = 9999 };
        Check(app.Game.CanStand(p), "Close actor center remains on real walkable ground"); app.Game.enemies.Add(enemy); return enemy;
    }
    private static ActorAction Start(FrontierRuntime app, Point heading, bool controller)
    {
        var g = app.Game;
        if (controller)
        {
            Input.axes["Aim Horizontal"] = heading.x; Input.axes["Aim Vertical"] = -heading.y; Update(app);
            Check(app.ControllerAimActive && !g.aimTarget.HasValue && g.CombatFocusTarget == null,
                "Actual fresh right-stick Update owns free aim without cursor or focus");
            Input.keys.Add(KeyCode.JoystickButton2); Input.held.Add(KeyCode.JoystickButton2); Update(app); Input.held.Clear();
        }
        else
        {
            Mouse(app, g.Position + heading * 4); Input.buttonDown.Add(0); Update(app);
            Check(!app.ControllerAimActive && g.CombatFocusTarget == null, "Actual world mouse press uses free pointer aim");
        }
        ActorAction action = g.playerAction;
        Check(action != null && action.kind == "attack" && action.weapon == g.save.weapon && !action.released,
            "Actual " + (controller ? "X" : "world click") + " begins the original unreleased bow/staff wind-up");
        Check((action.direction - heading).Length < .02f && action.target.HasValue != controller,
            "Prepared free-aim action captures the real device's direction and target policy");
        if (!controller) Check((action.target.Value - g.Position).Length > 3.8f, "Distant pointer does not auto-target the overlapping actor");
        return action;
    }
    private static void Release(FrontierRuntime app, ActorAction action, Action pending)
    {
        int frames = 0; pending();
        while (!action.released && frames++ < 60) { Update(app); if (!action.released) pending(); }
        Check(action.released, "The production action clock reaches its original contact frame");
    }
    private static void FriendlyReceipt(FrontierRuntime app, Enemy enemy, ActorAction action, Point feet, float hp, string name)
    {
        var g = app.Game;
        Check(enemy.hp < hp, "Released free-aim " + name + " must hit the actor between caster feet and visible muzzle (hp=" + enemy.hp + ")");
        Check(g.projectiles.Count == 0, "An ordinary close-contact shot is retired in its release Update; no ghost projectile");
        var impact = g.effects.FindAll(e => e.kind == (action.weapon == 2 ? "magicImpact" : "projectileImpact"));
        var numbers = g.effects.FindAll(e => e.kind == "number");
        Check(impact.Count == 1 && impact[0].variant == 0 && Near(impact[0].life, .28f),
            "Accepted close contact uses exactly one mature friendly projectile impact");
        Check(action.weapon != 2 || Near(impact[0].radius, 1.8f), "Staff close contact retains its original explosion radius");
        Check(numbers.Count == 1 && g.effects.Exists(e => e.kind == "impact") && enemy.hitTime > 0,
            "Real Hit produces the existing outgoing number, impact and recoil state");
        var release = g.effects.FindAll(e => e.kind == "release");
        Point muzzle = GameCore.WeaponOrigin(feet, action.weapon, action.direction);
        Check(release.Count == 1 && (release[0].position - muzzle).Length < .001f &&
            Near(release[0].height, GameCore.WeaponHeight(action.weapon, action.direction)),
            "Original elevated release spark stays at the visible muzzle after close contact");
        Check(Audio(app).heard.Contains(action.weapon == 1 ? "arrow" : "spell") && Audio(app).heard.Contains("hit"),
            "Actual runtime audio dispatch retains both launch and accepted-hit events");
        Draw(app, name);
        Check(GUI.commands.Exists(c => c.Contains("\"text\":" + GUI.S(numbers[0].text))),
            "Actual OnGUI draws the accepted outgoing damage number");
        float after = enemy.hp; Frames(app, 16);
        Check(Near(enemy.hp, after) && g.projectiles.Count == 0 && g.effects.FindAll(e => e.kind == "number").Count == 1,
            "Consumed close-contact projectile cannot deliver a second hidden hit during recovery");
    }
    private static void ControllerReleaseContacts(FrontierRuntime app)
    {
        int cases = 0;
        for (int weapon = 1; weapon <= 2; weapon++) for (int direction = 0; direction < 8; direction++)
        {
            Prepare(app, weapon); var g = app.Game; Point heading = Heading(direction), feet = g.Position;
            Enemy enemy = ContactEnemy(app, heading); float hp = enemy.hp; ActorAction action = Start(app, heading, true);
            Release(app, action, () => Check(Near(enemy.hp, hp) && g.projectiles.Count == 0 && !g.effects.Exists(e => e.kind == "number"),
                "Controller wind-up cannot damage, spawn or report a hit before its real release"));
            FriendlyReceipt(app, enemy, action, feet, hp, "controller-" + weapon + "-" + direction); cases++;
        }
        Console.WriteLine("PASS " + cases + " actual right-stick/X bow-staff close contacts over eight headings, original release clock and muzzle/audio/OnGUI feedback, one consumed shot; NATIVE_UNVERIFIED");
    }
    private static void PointerReleaseContacts(FrontierRuntime app)
    {
        int cases = 0;
        for (int weapon = 1; weapon <= 2; weapon++) for (int direction = 0; direction < 8; direction++)
        {
            Prepare(app, weapon); var g = app.Game; Point heading = Heading(direction), feet = g.Position;
            Enemy enemy = ContactEnemy(app, heading); float hp = enemy.hp; ActorAction action = Start(app, heading, false);
            Point target = action.target.Value; Mouse(app, feet + heading * -4);
            Release(app, action, () => Check(Near(enemy.hp, hp) && g.projectiles.Count == 0 && (action.target.Value - target).Length < .001f,
                "Pointer wind-up keeps HP untouched and its accepted distant target snapshot before release"));
            FriendlyReceipt(app, enemy, action, feet, hp, "pointer-" + weapon + "-" + direction); cases++;
        }
        Console.WriteLine("PASS " + cases + " actual distant-pointer bow-staff close contacts over eight headings, committed aim after cursor turn and existing release/impact/number/audio rendering; NATIVE_UNVERIFIED");
    }
    private static void HostileBowReleaseContacts(FrontierRuntime app)
    {
        int cases = 0;
        for (int direction = 0; direction < 8; direction++)
        {
            Prepare(app, 1); var g = app.Game; Point heading = Heading(direction), source = g.Position - heading * .10f;
            var enemy = new Enemy(99711, 1, source.x, source.y, 500)
            { state = 1, timer = .12f, windupDuration = .12f, target = source + heading * 4, facing = heading };
            g.enemies.Add(enemy); float hp = g.save.health;
            int frames = 0; while (enemy.releaseTime <= 0 && frames++ < 30)
            {
                Check(Near(g.save.health, hp) && g.projectiles.Count == 0, "Committed hostile bow warning deals no early HP loss");
                Update(app);
            }
            Check(enemy.releaseTime > 0 && Near(hp - g.save.health, 10),
                "Actual enemy bow release must hit player between enemy feet and muzzle (lost=" + (hp - g.save.health) + ")");
            Check(g.projectiles.Count == 0 && g.effects.FindAll(e => e.kind == "projectileImpact" && e.variant == 1).Count == 1,
                "Hostile close-contact flight emits one original impact and leaves no ghost arrow");
            var incoming = g.effects.FindAll(e => e.kind == "damageTaken");
            Check(incoming.Count == 1 && incoming[0].text == "-10" && g.effects.Exists(e => e.kind == "hurt") &&
                g.effects.Exists(e => e.kind == "enemyRelease") && Audio(app).heard.Contains("arrow") && Audio(app).heard.Contains("hurt"),
                "Hostile release keeps original ten-HP transaction, incoming number, hurt/release rings and actual audio delivery");
            Draw(app, "hostile-" + direction);
            Check(GUI.commands.Exists(c => c.Contains("\"text\":\"-10\"")), "Actual OnGUI renders hostile close-contact HP loss"); cases++;
        }
        Console.WriteLine("PASS " + cases + " actual committed hostile bow warnings/releases through Update, real ten-HP contact and original hurt/number/impact/audio, no surviving arrow; NATIVE_UNVERIFIED");
    }
    private static void PausedWindupAndReadOnlyDrawing(FrontierRuntime app)
    {
        foreach (int weapon in new[] { 1, 2 })
        {
            Prepare(app, weapon); var g = app.Game; Point heading = new Point(1, 0), feet = g.Position;
            Enemy enemy = ContactEnemy(app, heading); float hp = enemy.hp; ActorAction action = Start(app, heading, true);
            Input.keys.Add(KeyCode.Escape); Update(app); Check(g.mode == "pause" && !action.released, "Actual Escape pauses before the original release frame");
            float elapsed = action.elapsed, energy = g.save.energy;
            for (int frame = 0; frame < 20; frame++) { Update(app); Draw(app); }
            Check(Near(action.elapsed, elapsed) && Near(enemy.hp, hp) && Near(g.save.energy, energy) && g.projectiles.Count == 0,
                "Paused real Update/OnGUI freezes pending contact, HP and energy without spawning a hidden shot");
            Input.keys.Add(KeyCode.Escape); Update(app); Check(g.mode == "world" && !action.released, "Actual Escape resumes the same unreleased action");
            Release(app, action, () => Check(Near(enemy.hp, hp) && g.projectiles.Count == 0, "Resumed preparation retains original no-early-contact rule"));
            FriendlyReceipt(app, enemy, action, feet, hp, "resumed-" + weapon);
            float after = enemy.hp; int effects = g.effects.Count; string saved = JsonUtility.ToJson(g.save);
            for (int draw = 0; draw < 8; draw++) Draw(app);
            Check(Near(enemy.hp, after) && effects == g.effects.Count && saved == JsonUtility.ToJson(g.save) && g.projectiles.Count == 0,
                "Repeated actual presentation cannot repeat the consumed launch contact or mutate campaign state");
        }
        Console.WriteLine("PASS actual Escape wind-up pause/resume for both weapons, frozen contact clocks and read-only repeated OnGUI after one real resumed impact; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous = Application.persistentDataPath, directory = Path.Combine(Path.GetTempPath(), "starfall-muzzle-contact-" + Guid.NewGuid());
        Application.persistentDataPath = directory; captureFolder = args.Length > 0 && args[0] != "-" ? args[0] : null;
        capturePrefix = args.Length > 1 ? args[1] : "qa-muzzle-contact"; int failed = 0; var app = new FrontierRuntime();
        Action<FrontierRuntime>[] tests = { ControllerReleaseContacts, PointerReleaseContacts, HostileBowReleaseContacts, PausedWindupAndReadOnlyDrawing };
        try
        {
            Directory.CreateDirectory(directory); Call(app, "Awake"); Check(app.Game != null, "Actual production Awake loads campaign");
            foreach (var test in tests) try { test(app); } catch (Exception error) { failed++; Console.WriteLine("FAIL " + test.Method.Name + ": " + (error.InnerException ?? error)); }
            app.EndVisualReview(); Call(app, "OnDestroy");
        }
        finally
        {
            Application.persistentDataPath = previous; Input.Clear(); GUI.queuedClick = null; GUI.matrix = Matrix4x4.identity; GUI.color = Color.white;
            GUI.enabled = true; Event.current.type = EventType.Repaint; if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        Console.WriteLine("Runtime muzzle contact: " + (tests.Length - failed) + "/" + tests.Length + " groups passed; NATIVE_UNVERIFIED"); return failed == 0 ? 0 : 1;
    }
}
