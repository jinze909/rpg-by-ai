// Shipped Update and OnGUI integration; offline Unity declarations are not a native Player.
using System;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeDodgeDistanceChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int scenarios;
    private static readonly float[][] Schedules = {
        new[] { .05f }, new[] { 1f / 30 }, new[] { .02f }, new[] { 1f / 60 }, new[] { 1f / 120 },
        new[] { .017f, .041f, .009f, .032f }
    };
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static bool Near(float a, float b, float tolerance = .002f) { return Math.Abs(a - b) < tolerance; }
    private static object Call(FrontierRuntime app, string method, params object[] args)
    { return typeof(FrontierRuntime).GetMethod(method, Private).Invoke(app, args); }
    private static void Set(FrontierRuntime app, string field, object value)
    { typeof(FrontierRuntime).GetField(field, Private).SetValue(app, value); }
    private static T Get<T>(FrontierRuntime app, string field)
    { return (T)typeof(FrontierRuntime).GetField(field, Private).GetValue(app); }
    private static void Update(FrontierRuntime app, float dt)
    { Time.unscaledDeltaTime = dt; Call(app, "Update"); Input.keys.Clear(); Input.buttonDown.Clear(); }
    private static void Mouse(FrontierRuntime app, Point world)
    {
        Point pixel = (Point)Call(app, "ScreenPoint", world); float scale = Get<float>(app, "scale");
        Input.mousePosition = new Vector3(Get<float>(app, "offsetX") + pixel.x * scale,
            Screen.height - Get<float>(app, "offsetY") - pixel.y * scale, 0);
    }
    private static void Position(FrontierRuntime app, Point point)
    {
        Check(app.SetVisualReviewPosition(point) && (app.Game.Position - point).Length < .002f,
            "The fixture stands at the exact authored walkable point " + point.x + "," + point.y);
        Set(app, "camera", app.Game.Position); Mouse(app, point + new Point(5, 0));
    }
    private static void Prepare(FrontierRuntime app, int profession = 1, int room = 1)
    {
        Input.Clear(); GUI.queuedClick = null; GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear();
        GUI.matrix = Matrix4x4.identity; GUI.color = Color.white; GUI.enabled = true; Event.current.type = EventType.Repaint;
        Screen.width = 1280; Screen.height = 800; Time.unscaledDeltaTime = .02f;
        app.BeginVisualReview(profession, room); app.VisualReviewPaused = false; GameCore g = app.Game;
        g.enemies.Clear(); g.projectiles.Clear(); g.hazards.Clear(); g.effects.Clear(); g.fallen.Clear();
        g.save.companion = -1; g.save.reducedMotion = true; g.save.effectsQuality = 0; g.save.showGuidance = false;
        g.save.energy = 100; g.save.health = g.save.maxHealth; g.save.totalTime = 100; g.save.difficulty = 1;
        g.save.relics = new[] { -1, -1 }; g.save.talents = new int[3]; g.save.foodUntil = g.save.teaUntil = g.save.mealUntil = 0;
        g.save.heat = g.save.cold = 0; g.invulnerable = 0; g.toastTime = 0; g.save.controllerAimProfile = 0;
        Set(app, "applicationPaused", false); Set(app, "controllerUsed", false); Set(app, "pointerSeen", false);
        Set(app, "pointerAimActive", false); Set(app, "combatHudTime", 0f); Set(app, "arrivalTime", 0f); Set(app, "resumeTime", 0f);
        Call(app, "Resize"); Call(app, "ResetControllerAim", false); Position(app, new Point(12, 14));
    }
    private static Point Start(FrontierRuntime app, int input, Point direction, float dt)
    {
        GameCore g = app.Game; Input.Clear();
        if (input < 2)
        {
            Input.axes["Horizontal"] = direction.x; Input.axes["Vertical"] = -direction.y;
            Input.keys.Add(input == 0 ? KeyCode.Space : KeyCode.JoystickButton0); Update(app, dt);
        }
        else
        {
            g.SetAim(direction); Set(app, "combatHudTime", input == 3 ? 3f : 0f);
            Rect slot = input == 3 ? new Rect(770, 732, 78, 50) : new Rect(818, 699, 109, 75);
            Input.mousePosition = new Vector3(slot.x + 10, Screen.height - slot.y - 10, 0);
            GUI.queuedClick = slot; Call(app, "OnGUI"); GUI.queuedClick = null;
            var button = GUI.buttonCalls.Find(b => Near(b.rect.x, slot.x) && Near(b.rect.y, slot.y) && Near(b.rect.width, slot.width));
            Check(button != null && button.enabled, "The original real HUD dodge control is enabled at its retained location");
        }
        Input.Clear();
        Check(Near(g.dashTime, .18f) && Near(g.invulnerable, .32f) && Near(g.dashCooldown, .75f) && Near(g.save.energy, 80),
            "Actual Space / A / HUD pays the original 20 energy and starts the original .18 / .32 / .75 clocks");
        Check(g.PlayerPose == 8 && (g.VisualFacing - direction.Normalized).Length < .002f,
            "Accepted input enters the original whole-body dodge with its captured heading");
        return g.Position;
    }
    private static float Finish(FrontierRuntime app, Point origin, Point direction, float[] schedule, bool reverseMovement)
    {
        GameCore g = app.Game; int frames = 0; float simulated = 0; Point captured = direction.Normalized;
        Mouse(app, origin - captured * 5);
        if (reverseMovement) { Input.axes["Horizontal"] = -captured.x; Input.axes["Vertical"] = captured.y; }
        while (g.dashTime > 0 && frames < 100)
        {
            float remaining = g.dashTime, dt = schedule[frames % schedule.Length]; Point before = g.Position;
            Update(app, dt); simulated += dt; frames++;
            float expectedStep = Math.Min(dt, remaining) * 15;
            Check(Near((g.Position - before).Length, expectedStep),
                "Every Update moves only its remaining accepted dodge time; dt=" + dt + " remaining=" + remaining);
            Check((g.Position - origin - captured * (g.Position - origin).Length).Length < .003f,
                "Later mouse, stick and movement input cannot rotate the accepted dodge");
            Check(g.PlayerPose == 8 && (g.VisualFacing - captured).Length < .002f,
                "The final nonzero displacement retains the captured dodge pose and facing even when the timer ends");
        }
        Check(frames < 100 && g.dashTime == 0 && Near((g.Position - origin).Length, 2.7f),
            "A complete unobstructed accepted dodge travels exactly .18 seconds at 15 tiles/s = 2.7 tiles");
        Check(simulated < .231f && g.playerAction == null && g.CanStand(g.Position),
            "Completion adds no attack or illegal position and consumes no extra full movement tick");
        Input.Clear(); return simulated;
    }
    private static void FreshInputsKeepOneDistanceAndTerminalPose(FrontierRuntime app)
    {
        foreach (float[] schedule in Schedules) for (int profession = 0; profession < 3; profession++)
        for (int input = 0; input < 4; input++) foreach (Point direction in new[] { new Point(1, 0), new Point(.6f, .8f) })
        {
            scenarios++; Prepare(app, profession); Point origin = Start(app, input, direction, schedule[0]);
            float walked = app.Game.walkDistance; Finish(app, origin, direction, schedule, true);
            Check(Near(app.Game.walkDistance, walked), "Dodge displacement cannot mint normal walk-stride contacts");
            Point endpoint = app.Game.Position; Update(app, schedule[0]);
            Check((app.Game.Position - endpoint).Length < .001f && app.Game.PlayerPose != 8,
                "One stationary Update retires the final dodge sample without a float-residue pose or extra dash step");
            Input.axes["Horizontal"] = -direction.x; Input.axes["Vertical"] = direction.y;
            Update(app, schedule[0]); Input.Clear();
            Check(Near((app.Game.Position - endpoint).Length, app.Game.Speed * schedule[0])
                && (app.Game.VisualFacing + direction.Normalized).Length < .002f,
                "Normal movement resumes at its original speed only on a later ordinary frame");
        }
        Console.WriteLine("PASS actual Space / Pad A / two original HUD layouts, three classes, cardinal and diagonal capture, 20/30/50/60/120 FPS and jittered frames: 2.7 tiles, terminal pose/facing and no walk remainder; NATIVE_UNVERIFIED");
    }
    private static void QueuedReadinessAndHeldCombatKeepOriginalOwnership(FrontierRuntime app)
    {
        foreach (float[] schedule in Schedules) foreach (bool pad in new[] { false, true }) foreach (bool compass in new[] { false, true })
        {
            scenarios++; Prepare(app); GameCore g = app.Game; float dt = schedule[0];
            if (compass) { g.save.relics[0] = 2; g.save.talents[2] = 5; } float cost = compass ? 25 : 20;
            g.dashCooldown = .1f + dt; Input.axes["Horizontal"] = 1; Input.held.Add(pad ? KeyCode.JoystickButton2 : KeyCode.J);
            Input.keys.Add(pad ? KeyCode.JoystickButton0 : KeyCode.Space); Update(app, dt); Input.axes["Horizontal"] = 0;
            Check(g.dashTime == 0 && g.playerAction == null && Near(g.save.energy, 100),
                "An actual near-ready press reserves no energy and held paid attack yields to its unstarted intent");
            Point waiting = g.Position; Mouse(app, waiting + new Point(-5, 0)); int frame = 0;
            while (g.dashTime <= 0 && frame < 30) { Update(app, dt); frame++; }
            Check(frame < 30 && Near(g.dashTime, .18f) && Near(g.invulnerable, .32f)
                && Near(g.dashCooldown, .75f - g.save.talents[2] * .06f)
                && (g.Position - waiting).Length < .001f && Near(g.save.energy, 100 - cost) && g.playerAction == null,
                "Readiness accepts exactly one original-cost dodge with full timers and no credited pre-readiness displacement");
            Input.held.Clear(); Point origin = g.Position; Finish(app, origin, new Point(1, 0), schedule, false);
            // The next actual held input resumes the mature attack, rather than replaying the old dodge.
            Input.held.Add(pad ? KeyCode.JoystickButton2 : KeyCode.J); Mouse(app, g.Position + new Point(-5, 0)); Update(app, dt);
            Check(g.playerAction != null && g.playerAction.kind == "attack" && g.dashTime == 0 && g.playerAction.direction.x < -.99f,
                "After the completed captured dodge, held J / X resumes its normal paid attack with current aim");
            Input.Clear();
        }
        Console.WriteLine("PASS real near-ready Space / A versus held J / X, 20/25 original costs and 0/5 talent cooldowns, no early displacement/protection, preserved captured direction, readiness then ordinary attack ownership at every tested rate; NATIVE_UNVERIFIED");
    }
    private static void PauseSaveAndTravelKeepTransientBoundaries(FrontierRuntime app)
    {
        foreach (float[] schedule in Schedules) foreach (string boundary in new[] { "pause", "application", "save-load", "travel" })
        {
            scenarios++; Prepare(app); GameCore g = app.Game; float dt = schedule[0]; Point origin = Start(app, 0, new Point(1, 0), dt);
            Update(app, dt); Check(g.dashTime > 0, "A real accepted dash has a partial displacement before its boundary");
            if (boundary == "pause")
            {
                Input.keys.Add(KeyCode.Escape); Update(app, dt); Check(g.mode == "pause", "Actual Esc opens the existing pause menu");
                Point paused = g.Position; float timer = g.dashTime, protection = g.invulnerable, clock = g.save.totalTime;
                for (int i = 0; i < 5; i++) Update(app, .05f);
                Check((g.Position - paused).Length < .001f && g.dashTime == timer && g.invulnerable == protection && g.save.totalTime == clock,
                    "The actual menu freezes accepted movement, protection and the original world clock");
                Input.keys.Add(KeyCode.Escape); Update(app, dt); Check(g.mode == "world", "Actual second Esc resumes the existing world");
                Finish(app, origin, new Point(1, 0), schedule, false);
            }
            else if (boundary == "application")
            {
                Call(app, "OnApplicationPause", true); Point paused = g.Position; float timer = g.dashTime, clock = g.save.totalTime;
                for (int i = 0; i < 5; i++) Update(app, .05f);
                Check((g.Position - paused).Length < .001f && g.dashTime == timer && g.save.totalTime == clock,
                    "Application suspension cannot consume or relocate an accepted dash");
                Call(app, "OnApplicationPause", false); Finish(app, origin, new Point(1, 0), schedule, false);
            }
            else if (boundary == "save-load")
            {
                g.PrepareSave(); Point savedAt = g.Position; Check(SaveStore.Write(g.save, 2), "The actual checksum save stores the partial world position");
                string warning; SaveData restored = SaveStore.Read(2, out warning); Check(restored != null && warning == null, "The actual save reader restores its compatible saved owner");
                g.Load(restored); g.Close(); g.enemies.Clear();
                Check(g.dashTime == 0 && (g.Position - savedAt).Length < .001f, "Loading preserves the saved foot position and retires transient dodge time");
                for (int i = 0; i < 8; i++) Update(app, dt);
                Check((g.Position - savedAt).Length < .001f && g.dashTime == 0, "No saved or discarded dash restarts on subsequent real Updates");
            }
            else
            {
                g.Enter(0); g.enemies.Clear(); Point arrived = g.Position;
                Check(g.dashTime == 0 && g.save.room == 0, "Mature travel clears the old region's accepted dash");
                for (int i = 0; i < 8; i++) Update(app, dt);
                Check((g.Position - arrived).Length < .001f && g.dashTime == 0, "No old region movement carries into the new map");
            }
        }
        Console.WriteLine("PASS real pause and application suspension preserve accepted remaining time; checksum SaveStore restore retains position but no transient movement; travel discards old-region dash at all tested rates; NATIVE_UNVERIFIED");
    }
    private static void CollisionDamageAndProtectionKeepOriginalRules(FrontierRuntime app)
    {
        foreach (float[] schedule in Schedules)
        {
            scenarios++; Prepare(app); GameCore g = app.Game; float dt = schedule[0]; Point origin = Start(app, 0, new Point(1, 0), dt);
            float health = g.save.health;
            g.hazards.Add(new Hazard { kind = "rift", position = origin + new Point(15 * dt, 0), radius = 1, timer = 0, damage = 13 });
            Update(app, dt); Check(g.save.health == health && g.invulnerable > 0,
                "A real resolving hazard cannot damage the accepted original invulnerability window");
            Check(g.save.energy >= 88, "The original one-time perfect-dodge energy recovery remains active");
            Finish(app, origin, new Point(1, 0), schedule, false);
            Check(g.invulnerable > 0, "Original .32 second protection survives the end of .18 second displacement");
            while (g.invulnerable > 0) Update(app, .01f);
            g.hazards.Add(new Hazard { kind = "rift", position = g.Position, radius = 1, timer = 0, damage = 13 }); Update(app, .01f);
            Check(Near(g.save.health, health - 13) && g.playerHitTime > 0,
                "After original protection expires, the same real hazard deals its original normal-difficulty damage");

            scenarios++; Prepare(app); g = app.Game; g.save.energy = 10; Input.keys.Add(KeyCode.Space); Update(app, dt);
            Check(g.dashTime == 0 && g.invulnerable <= 0, "An unaffordable real press grants no distance or protection");
            g.hazards.Add(new Hazard { kind = "rift", position = g.Position, radius = 1, timer = 0, damage = 13 }); health = g.save.health; Update(app, dt);
            Check(Near(g.save.health, health - 13), "Actual incoming damage still applies when the low-energy request never started");

            scenarios++; Prepare(app, 1, 0); g = app.Game; Point start = new Point(); Point direction = new Point(); bool found = false;
            foreach (Point heading in new[] { new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1) })
            {
                for (float y = 4; y < 24 && !found; y += .5f) for (float x = 4; x < 31 && !found; x += .5f)
                {
                    Point candidate = new Point(x, y);
                    if (g.CanStand(candidate) && g.CanStand(candidate + heading * .5f) && !g.CanStand(candidate + heading * 1.5f))
                    { start = candidate; direction = heading; found = true; }
                }
                if (found) break;
            }
            Check(found, "The shipped town supplies a real collision surface inside the intended dash distance");
            Position(app, start); origin = Start(app, 0, direction, dt); int frames = 0;
            while (g.dashTime > 0 && frames++ < 100) { Update(app, schedule[frames % schedule.Length]); Check(g.CanStand(g.Position), "Every real dash sweep keeps the hero on legal authored ground"); }
            Check(g.dashTime == 0 && (g.Position - origin).Length < 1.6f,
                "Original terrain sweep blocks the dodge rather than tunneling or granting the unclipped 2.7 tile distance");
        }
        Console.WriteLine("PASS actual hazard resolution, original perfect-dodge refund and .32 protection, later 13 damage, low-energy rejection and existing town collision sweep remain intact across rates; NATIVE_UNVERIFIED");
    }
    public static int Main()
    {
        string previous = Application.persistentDataPath, directory = Path.Combine(Path.GetTempPath(), "starfall-dodge-distance-" + Guid.NewGuid());
        Application.persistentDataPath = directory; var app = new FrontierRuntime(); int failures = 0;
        try
        {
            Call(app, "Awake"); Check(app.Game != null, "Actual Awake loads the shipped campaign");
            foreach (Action<FrontierRuntime> test in new Action<FrontierRuntime>[] { FreshInputsKeepOneDistanceAndTerminalPose, QueuedReadinessAndHeldCombatKeepOriginalOwnership, PauseSaveAndTravelKeepTransientBoundaries, CollisionDamageAndProtectionKeepOriginalRules })
                try { test(app); } catch (Exception error) { failures++; Console.WriteLine("FAIL " + test.Method.Name + ": " + (error.InnerException ?? error).Message); }
        }
        finally
        {
            app.EndVisualReview(); Call(app, "OnDestroy"); Application.persistentDataPath = previous;
            Input.Clear(); GUI.queuedClick = null; if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        Console.WriteLine("Runtime dodge distance: " + (4 - failures) + "/4 groups passed; " + scenarios + " scenarios; NATIVE_UNVERIFIED");
        return failures == 0 ? 0 : 1;
    }
}
