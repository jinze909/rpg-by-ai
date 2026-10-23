// Actual Update/OnGUI integration with offline Unity declarations, not native acceptance.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeBossVolleyChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
    private static string folder, prefix;
    private static int captures;
    private static void Check(bool value, string why) { if (!value) throw new Exception(why); }
    private static object Call(object app, string name, params object[] args)
    { return app.GetType().GetMethod(name, Private | BindingFlags.Public).Invoke(app, args); }
    private static void Set(object app, string name, object value) { app.GetType().GetField(name, Private).SetValue(app, value); }
    private static T Get<T>(object app, string name) { return (T)app.GetType().GetField(name, Private).GetValue(app); }
    private static bool Near(float a, float b, float epsilon = .04f) { return Math.Abs(a - b) < epsilon; }
    private static void Update(FrontierRuntime app) { Call(app, "Update"); Input.keys.Clear(); }
    private static void Clear() { GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); }
    private static void Draw(FrontierRuntime app, string capture = null)
    {
        Clear(); Call(app, "OnGUI");
        if (folder != null && capture != null)
        {
            var commands = GUI.commands.ToArray(); var textures = GUI.textureCalls.ToArray(); var buttons = GUI.buttonCalls.ToArray();
            GUI.Export(folder, prefix + "-" + capture); captures++;
            GUI.commands.AddRange(commands); GUI.textureCalls.AddRange(textures); GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static Enemy Prepare(FrontierRuntime app, int phase = 1, int room = 2, int profession = 1)
    {
        var method = typeof(RuntimeBossCommitmentChecks).GetMethod("Prepare", StaticPrivate);
        var boss = (Enemy)method.Invoke(null, new object[] { app, profession, phase, room });
        GUI.color = Color.white; GUI.matrix = Matrix4x4.identity; GUI.enabled = true; Event.current.type = EventType.Repaint;
        app.Game.toastTime = 0; return boss;
    }
    private static bool Ink(Color color)
    { return Near(color.r, .95f, .00001f) && Near(color.g, .72f, .00001f) && Near(color.b, .46f, .00001f) && color.a > .35f; }
    private static List<GUI.TextureCall> Lines()
    { return GUI.textureCalls.FindAll(c => c.texture == Texture2D.whiteTexture && Near(c.rect.height, 3, .001f) && Ink(c.color)); }
    private static Point ScreenPoint(FrontierRuntime app, Point point) { return (Point)Call(app, "ScreenPoint", point); }
    private static Point Endpoint(GUI.TextureCall call, bool end)
    {
        Vector3 physical = call.matrix.MultiplyPoint3x4(new Vector3(call.rect.x + (end ? call.rect.width : 0), call.rect.y + call.rect.height / 2, 0));
        float scale = Math.Min(Screen.width / 1280f, Screen.height / 800f);
        return new Point((physical.x - (Screen.width - 1280 * scale) / 2) / scale, (physical.y - (Screen.height - 800 * scale) / 2) / scale);
    }
    private static bool Segment(GUI.TextureCall line, Point from, Point to)
    { return (Endpoint(line, false) - from).Length < .06f && (Endpoint(line, true) - to).Length < .06f; }
    private static object Plan(FrontierRuntime app, Enemy boss)
    { return typeof(GameCore).GetMethod("PendingBossVolley", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { boss, app.Game.save.room }); }
    private static T Field<T>(object plan, string name) { return (T)plan.GetType().GetField(name).GetValue(plan); }
    private static int Count(object plan) { return (int)plan.GetType().GetProperty("Count").GetValue(plan, null); }
    private static Point Direction(object plan, int shot) { return (Point)plan.GetType().GetMethod("Direction").Invoke(plan, new object[] { shot }); }
    private static float Speed(object plan, int shot) { return (float)plan.GetType().GetMethod("Speed").Invoke(plan, new object[] { shot }); }
    private static Point End(FrontierRuntime app, Enemy boss, int shot)
    { return (Point)typeof(GameCore).GetMethod("BossVolleyWarningEnd").Invoke(app.Game, new object[] { boss, shot }); }
    private static void RequireTells(FrontierRuntime app)
    { Draw(app); Check(Lines().Count > 0, "Actual OnGUI must draw the prepared Boss volley before release; the old stomp circle alone is insufficient"); }
    private static int CheckGeometry(FrontierRuntime app, Enemy boss)
    {
        object plan = Plan(app, boss); Point origin = Field<Point>(plan, "origin"); float lift = GameCore.BossEmissionHeight(app.Game.save.room);
        var expected = new List<Point[]>();
        for (int shot = 0; shot < Count(plan); shot++)
        {
            Point end = End(app, boss, shot); if ((end - origin).Length <= 1.25f) continue;
            Point from = ScreenPoint(app, origin + Direction(plan, shot).Normalized * 1.15f), to = ScreenPoint(app, end);
            from.y -= lift; to.y -= lift; expected.Add(new[] { from, to });
        }
        var lines = Lines(); Check(expected.Count > 0 && lines.Count >= expected.Count, "Real free flight directions have visible pre-release center lines");
        foreach (var pair in expected) Check(lines.Exists(c => Segment(c, pair[0], pair[1])), "Each submitted tell matches the terrain-clipped physical orb direction and elevated launch plane");
        foreach (var line in lines)
        {
            Check(expected.Exists(pair => Segment(line, pair[0], pair[1])), "No extra or stale volley direction is announced");
            Check(GUI.textureCalls.Exists(c => c.texture == Texture2D.whiteTexture && Near(c.rect.height, 7, .001f)
                && Near(c.color.r, .025f, .00001f) && Segment(c, Endpoint(line, false), Endpoint(line, true))), "Every bright prefire line has its matching readable thick shadow");
        }
        return expected.Count;
    }
    private static void GenuineInputsAndReleasedShots(FrontierRuntime app)
    {
        foreach (int room in new[] { 2, 3 }) foreach (int phase in new[] { 1, 2 }) foreach (bool skill in new[] { false, true })
        {
            Enemy boss = Prepare(app, phase, room, skill ? 2 : 1); RequireTells(app); CheckGeometry(app, boss);
            typeof(RuntimeBossCommitmentChecks).GetMethod("CrossByRealInput", StaticPrivate).Invoke(null, new object[] { app, boss, phase, skill });
            Draw(app); CheckGeometry(app, boss); object plan = Plan(app, boss);
            Check(Field<int>(plan, "phase") == phase && boss.BossPhase == phase + 1, "A real J/Q threshold crossing cannot silently replace the already announced volley");
            int cycle = boss.cycle, frames = 0; while (boss.cycle == cycle && frames++ < 150) Update(app);
            Check(frames < 150 && boss.cycle == cycle + 1 && boss.state == 0, "Actual Update reaches the unchanged mature release and recovery");
            int observed = 0;
            foreach (Projectile shot in app.Game.projectiles) if (!shot.friendly)
            {
                bool match = false; for (int i = 0; i < Count(plan); i++) if ((shot.velocity - Direction(plan, i).Normalized * Speed(plan, i)).Length < .002f) match = true;
                Check(match && shot.damage == 14, "Each physically surviving released orb retains one announced direction, speed and original damage"); observed++;
            }
            Check(observed > 0, "The integration observes genuine released missiles rather than a draw-only synthetic volley");
            Draw(app); Check(Lines().Count == 0, "Release immediately retires its transient preparation marks");
        }
        Console.WriteLine("PASS actual J/Q threshold crossings, captured volley tells, terrain/elevation geometry and unchanged released projectile speeds/damage; NATIVE_UNVERIFIED");
    }
    private static void HudWitnessAndInput(FrontierRuntime app)
    {
        Enemy boss = Prepare(app, 1, 3); RequireTells(app); app.Game.invulnerable = 1000;
        foreach (Point screen in new[] { new Point(145, 75), new Point(1140, 65), new Point(620, 755), new Point(650, 45) })
        {
            Set(app, "camera", boss.Position - new Point((screen.x - 640) / 36, (screen.y + GameCore.BossEmissionHeight(3) - 390) / 36));
            Draw(app); CheckGeometry(app, boss); Rect bounds = (Rect)Call(app, "BossVolleyWarningBounds", boss);
            Check((bool)Call(app, "ThreatOver", bounds), "A volley brushing existing HUD backgrounds participates in the original fading policy");
            Clear(); Call(app, "DrawForegroundThreatContours"); int world = Lines().Count;
            Clear(); Call(app, "DrawHudThreatWitness"); Check(Lines().Count == world && world > 0, "The real HUD witness repeats every prefire line above overlapping status, journal, Boss header or hotbar");
            var pointers = (List<Rect>)Call(app, "HudPointerRegions");
            Check(!pointers.Exists(r => Near(r.x, bounds.x) && Near(r.y, bounds.y) && Near(r.width, bounds.width)), "Read-only ray bounds never become an invisible world-input blocker");
        }
        Set(app, "camera", app.Game.Position); Draw(app);
        Point target = boss.Position; Point pixel = ScreenPoint(app, target); Input.mousePosition = new Vector3(pixel.x, Screen.height - pixel.y, 0);
        Input.keys.Add(KeyCode.J); Update(app); Check(app.Game.playerAction != null, "J still accepts a deliberate world action while a volley is announced");
        Console.WriteLine("PASS independent elevated bounds, actual four-edge HUD witnesses, original warning geometry and read-only input pass-through; NATIVE_UNVERIFIED");
    }
    private static void LifecycleSavesAndSpecialBosses(FrontierRuntime app)
    {
        Enemy boss = Prepare(app); RequireTells(app); var g = app.Game; float timer = boss.timer;
        Call(app, "OnApplicationPause", true); for (int i = 0; i < 12; i++) Update(app);
        Check(boss.timer == timer, "Application pause does not advance this prepared attack or create another warning clock");
        Call(app, "OnApplicationPause", false); app.VisualReviewPaused = true; for (int i = 0; i < 12; i++) Update(app);
        Check(boss.timer == timer, "Visual review freeze preserves the same pending volley"); app.VisualReviewPaused = false;
        boss.state = 0; Draw(app); Check(Lines().Count == 0, "Breaking/cancelling an attack retires the warning without a cached lifetime");
        boss.state = 1; Draw(app); Check(Lines().Count > 0, "A newly prepared attack derives its marks from live state");
        g.PrepareSave(); string json = JsonUtility.ToJson(g.save); Check(!json.Contains("BossVolley") && !json.Contains("releaseCycle"), "Prepared volley state never enters the save schema");
        foreach (int version in new[] { 1, 2 })
        {
            g.save.version = version; File.WriteAllText(SaveStore.PathFor(1), JsonUtility.ToJson(g.save)); string warning; SaveData old = SaveStore.Read(1, out warning);
            Check(old != null && warning == null, "Raw legacy saves remain readable through actual SaveStore");
            Check(SaveStore.Write(old, 2), "Original checksum writer accepts the same legacy game state"); var loaded = SaveStore.Read(2, out warning);
            Check(loaded != null && warning == null, "Checksummed legacy state resumes without new fields");
            g.Load(loaded); g.Start(); Draw(app); Check(Lines().Count == 0, "Reload starts safe idle enemies rather than reviving a stale warning");
        }
        boss = Prepare(app); RequireTells(app); g = app.Game;
        foreach (int id in new[] { GameCore.SignalWardenId, GameCore.CommissionGuardianId })
        {
            g.enemies.Clear(); var special = new Enemy(id, 3, boss.x, boss.y, 300, true) { state = 1, windupDuration = 1, timer = .6f, target = g.Position, attackOrigin = boss.Position, attackDestination = g.Position };
            g.enemies.Add(special); Draw(app); Check(Lines().Count == 0, "Authored Signal/Commission warnings never acquire a fictitious generic volley");
        }
        boss = Prepare(app); RequireTells(app); g = app.Game; g.enemies.Remove(boss); Draw(app); Check(Lines().Count == 0, "Death/removal leaves no detached marks over the fallen body");
        g.Enter(0); Draw(app); Check(Lines().Count == 0, "Travel clears transient warning ownership");
        Console.WriteLine("PASS application/review pause, live cancellation/removal/travel, legacy raw/checksum saves and authored optional Boss exclusions; NATIVE_UNVERIFIED");
    }
    private static void LayoutPoliciesAndReadOnly(FrontierRuntime app)
    {
        Enemy boss = Prepare(app, 2, 3); RequireTells(app);
        foreach (bool english in new[] { false, true }) foreach (bool large in new[] { false, true }) foreach (int width in new[] { 960, 1280, 1920 })
        {
            var g = app.Game; g.save.english = english; g.save.largeText = large; Screen.width = width; Screen.height = width == 1920 ? 1080 : width * 5 / 8; Call(app, "Resize");
            foreach (int quality in new[] { 0, 2 })
            {
                g.save.effectsQuality = quality; g.save.reducedMotion = quality == 0;
                Draw(app, (english ? "en" : "zh") + (large ? "-large-" : "-normal-") + width + "-fx" + quality); CheckGeometry(app, boss);
            }
        }
        var art = Get<PixelArt>(app, "art"); Call(art, "Glow"); Draw(app);
        int owned = Get<List<Texture2D>>(art, "owned").Count, cache = Get<Dictionary<string, Texture2D>>(art, "cache").Count; float clock = Get<float>(app, "clock");
        MethodInfo state = typeof(RuntimeCounterHitChecks).GetMethod("State", StaticPrivate); string before = (string)state.Invoke(null, new object[] { app.Game });
        var tint = new Color(.2f, .3f, .4f, .5f); var matrix = Matrix4x4.TRS(new Vector3(4, 7, 0), Quaternion.Euler(0, 0, 11), new Vector3(1.3f, 1.3f, 1));
        foreach (EventType type in new[] { EventType.Layout, EventType.Repaint })
        {
            Event.current.type = type; GUI.color = tint; GUI.matrix = matrix; GUI.enabled = false; Draw(app);
            Check((string)state.Invoke(null, new object[] { app.Game }) == before && Get<float>(app, "clock") == clock, "Repeated actual GUI leaves complete Core/save/RNG/action and presentation clocks unchanged");
            Check(Near(GUI.color.r, tint.r) && Near(GUI.color.a, tint.a) && Near(GUI.matrix.m00, matrix.m00) && Near(GUI.matrix.m03, matrix.m03) && !GUI.enabled, "Actual GUI restores incoming color, matrix and enabled state");
        }
        Check(Get<List<Texture2D>>(art, "owned").Count == owned && Get<Dictionary<string, Texture2D>>(art, "cache").Count == cache, "Warnings allocate no art textures or growing draw caches");
        Event.current.type = EventType.Repaint; GUI.color = Color.white; GUI.matrix = Matrix4x4.identity; GUI.enabled = true;
        Console.WriteLine("PASS actual bilingual normal/large three viewport widths, effects/reduced-motion policies, transformed flight-plane rays and read-only full Core/GUI/art state; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous = Application.persistentDataPath, temp = Path.Combine(Path.GetTempPath(), "starfall-boss-volley-" + Guid.NewGuid().ToString("N"));
        Application.persistentDataPath = temp; folder = args.Length > 0 && args[0] != "-" ? args[0] : null; prefix = args.Length > 1 ? args[1] : "qa-boss-volley";
        var app = new FrontierRuntime(); int failed = 0;
        try
        {
            Directory.CreateDirectory(temp); if (folder != null) Directory.CreateDirectory(folder); Call(app, "Awake");
            foreach (Action<FrontierRuntime> test in new Action<FrontierRuntime>[] { GenuineInputsAndReleasedShots, HudWitnessAndInput, LifecycleSavesAndSpecialBosses, LayoutPoliciesAndReadOnly })
                try { test(app); } catch (Exception error) { failed++; Console.WriteLine("FAIL " + test.Method.Name + ": " + (error.InnerException ?? error)); }
            app.EndVisualReview(); Call(app, "OnDestroy");
        }
        finally { Application.persistentDataPath = previous; Input.Clear(); GUI.queuedClick = null; if (Directory.Exists(temp)) Directory.Delete(temp, true); }
        Console.WriteLine("Runtime boss-volley: " + (4 - failed) + "/4 groups passed; captures=" + captures + "; NATIVE_UNVERIFIED"); return failed == 0 ? 0 : 1;
    }
}
