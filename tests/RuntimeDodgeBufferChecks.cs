// Executes the shipped Update/OnGUI paths with offline Unity declarations.
// This checks input and simulation behavior; it is not native Player validation.
using System;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeDodgeBufferChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static object Call(FrontierRuntime app, string method, params object[] args)
    { return typeof(FrontierRuntime).GetMethod(method, Private).Invoke(app, args); }
    private static void Set(FrontierRuntime app, string field, object value)
    { typeof(FrontierRuntime).GetField(field, Private).SetValue(app, value); }
    private static void Update(FrontierRuntime app) { Call(app, "Update"); Input.keys.Clear(); }
    private static void Frames(FrontierRuntime app, int count) { for (int i = 0; i < count; i++) Update(app); }
    private static int DashEffects(FrontierRuntime app)
    { return app.Game.effects.FindAll(e => e.kind == "dash").Count; }
    private static void Mouse(FrontierRuntime app, Point target)
    { Point screen = (Point)Call(app, "ScreenPoint", target); Input.mousePosition = new Vector3(screen.x, Screen.height - screen.y, 0); }
    private static void Prepare(FrontierRuntime app, int profession = 1, int room = 1)
    {
        Input.Clear(); GUI.queuedClick = null; GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear();
        Screen.width = 1280; Screen.height = 800; Time.unscaledDeltaTime = .02f;
        app.BeginVisualReview(profession, room); app.VisualReviewPaused = false;
        var g = app.Game; g.enemies.Clear(); g.save.companion = -1; g.save.reducedMotion = true;
        g.save.x = 12; g.save.y = 14; g.save.energy = 100; g.invulnerable = 0; g.toastTime = 0;
        Set(app, "camera", g.Position); Set(app, "controllerUsed", false); Set(app, "pointerSeen", false);
        Set(app, "combatHudTime", 0f); Set(app, "applicationPaused", false); Call(app, "Resize"); Mouse(app, g.Position + new Point(4, 0));
    }
    private static void QueueKey(FrontierRuntime app, bool pad = false)
    { app.Game.dashCooldown = .10f; Input.axes["Horizontal"] = 1; Input.keys.Add(pad ? KeyCode.JoystickButton0 : KeyCode.Space); Update(app); Input.axes["Horizontal"] = 0; }
    private static void AwaitDash(FrontierRuntime app)
    { for (int i = 0; i < 8 && app.Game.dashTime <= 0; i++) Update(app); }

    private static void KeyboardAndPadCaptureOneDodgeForEveryClass(FrontierRuntime app)
    {
        for (int profession = 0; profession < 3; profession++)
        foreach (bool pad in new[] { false, true })
        {
            Prepare(app, profession); Input.held.Add(KeyCode.J); Update(app);
            ActorAction attack = app.Game.playerAction; Check(attack != null, "Actual held J starts the setup attack");
            float energy = app.Game.save.energy; QueueKey(app, pad);
            Check(app.Game.playerAction == attack && app.Game.dashTime <= 0 && app.Game.invulnerable <= 0
                && app.Game.save.energy >= energy, "Early dodge queues without cancelling contact, charging energy or granting invulnerability");
            Input.held.Add(pad ? KeyCode.JoystickButton0 : KeyCode.Space);
            Mouse(app, app.Game.Position + new Point(-4, 0)); AwaitDash(app);
            Check(app.Game.dashTime > 0 && app.Game.playerAction == null && app.Game.PlayerPose == 8,
                "The captured dodge starts before held J and selects the existing whole-body dodge pose for class " + profession);
            Point before = app.Game.Position; Update(app);
            Check(app.Game.Position.x > before.x + .29f && Math.Abs(app.Game.Position.y - before.y) < .001f
                && app.Game.VisualFacing.x > .999f, "Captured rightward dodge keeps the original 15 tiles/s displacement despite a new mouse aim");
            float health = app.Game.save.health;
            app.Game.projectiles.Add(new Projectile { position = app.Game.Position, velocity = new Point(), damage = 10, source = "shot", life = .2f });
            Update(app); Check(Math.Abs(app.Game.save.health - health) < .001f, "A real enemy projectile is avoided only after the dodge starts");
            int starts = 1;
            for (int i = 0; i < 65; i++) { float cooldown = app.Game.dashCooldown; Update(app); if (app.Game.dashCooldown > cooldown + .1f) starts++; }
            Check(starts == 1, "Held Space/A cannot renew the captured one-shot dodge after its cooldown");
        }
        foreach (float dt in new[] { 1f / 30, .04f, .05f })
        {
            Prepare(app); Time.unscaledDeltaTime = dt; app.Game.dashCooldown = dt + .02f;
            Input.axes["Horizontal"] = 1; Input.keys.Add(KeyCode.Space); Update(app); Input.axes["Horizontal"] = 0;
            Check(app.Game.dashTime <= 0, "The coarse-frame fixture leaves a real positive cooldown");
            Point before = app.Game.Position; Update(app);
            Check(app.Game.dashTime > 0 && (app.Game.Position - before).Length < .001f
                && Math.Abs(app.Game.dashTime - .18f) < .001f && Math.Abs(app.Game.invulnerable - .32f) < .001f,
                "Crossing zero at 30/25/20 fps starts the original timers without crediting a whole tick of movement before readiness");
            before = app.Game.Position; Update(app);
            Check(Math.Abs((app.Game.Position - before).Length - dt * 15) < .001f,
                "The first actual buffered dodge step has the original speed at 30/25/20 fps");
        }
        Console.WriteLine("PASS three classes: real Space/A Update input, no early cancellation/cost/invulnerability, captured direction, unchanged dash motion/pose, projectile avoidance and held-J/held-dodge priority");
    }

    private static void RejectedInputsDoNotWaitForReadiness(FrontierRuntime app)
    {
        foreach (string reason in new[] { "outside", "energy" })
        {
            Prepare(app); app.Game.dashCooldown = reason == "outside" ? .50f : .10f;
            app.Game.save.energy = reason == "energy" ? 10 : 100;
            Input.keys.Add(KeyCode.Space); Update(app);
            int starts = DashEffects(app);
            for (int i = 0; i < 90; i++) { float cooldown = app.Game.dashCooldown; Update(app); if (app.Game.dashCooldown > cooldown + .1f) starts++; }
            Check(starts == 0 && app.Game.save.energy > 20, "A press rejected for " + reason + " never launches later when time or regeneration makes it affordable");
        }
        Prepare(app); app.Game.dashCooldown = .10f; Input.keys.Add(KeyCode.Space); Update(app);
        Check(app.Game.invulnerable <= 0, "Queued request alone grants no immunity");
        app.Game.projectiles.Add(new Projectile { position = app.Game.Position, velocity = new Point(), damage = 10, source = "shot", life = .2f });
        float beforeHealth = app.Game.save.health; Update(app);
        Check(app.Game.save.health < beforeHealth, "A real enemy projectile still hurts during the unstarted input buffer");
        Console.WriteLine("PASS rejected outside-window and low-energy presses never wait for readiness; unstarted dodge does not evade actual incoming damage");
    }

    private static Rect DodgeSlot(bool compact) { return compact ? new Rect(770, 732, 78, 50) : new Rect(818, 699, 109, 75); }
    private static void HudWindowIsClickableWithoutPretendingReady(FrontierRuntime app)
    {
        foreach (bool compact in new[] { false, true })
        foreach (string state in new[] { "window", "far", "energy" })
        {
            Prepare(app); var g = app.Game; g.dashCooldown = state == "far" ? .50f : .10f; g.save.energy = state == "energy" ? 10 : 100;
            g.SetAim(new Point(1, 0)); Set(app, "combatHudTime", compact ? 3f : 0f);
            Rect slot = DodgeSlot(compact); Input.mousePosition = new Vector3(slot.x + 10, Screen.height - slot.y - 10, 0);
            GUI.queuedClick = slot; GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app, "OnGUI");
            var button = GUI.buttonCalls.Find(b => Math.Abs(b.rect.x - slot.x) < .001f && Math.Abs(b.rect.y - slot.y) < .001f && b.rect.width == slot.width);
            Check(button != null && button.enabled == (state == "window"), "Actual " + (compact ? "compact" : "exploration") + " Dodge HUD enables only the affordable short window: " + state);
            Check(g.dashTime <= 0 && g.invulnerable <= 0 && Math.Abs(g.save.energy - (state == "energy" ? 10 : 100)) < .001f,
                "A cooldown HUD click cannot charge or grant a dodge early");
            bool cooldownText = GUI.commands.Exists(c => c.Contains("\"text\":\"1s\"") && c.Contains("\"r\":[" + (slot.x + 3).ToString(System.Globalization.CultureInfo.InvariantCulture) + ","));
            Check(cooldownText, "The early-input HUD still displays the cooldown instead of falsely showing ready");
            GUI.queuedClick = null;
            if (state == "window")
            { Mouse(app, g.Position + new Point(-4, 0)); AwaitDash(app); Check(g.dashTime > 0 && g.VisualFacing.x > .999f, "The actual HUD queue starts in its captured world direction"); }
            else { Frames(app, 40); Check(DashEffects(app) == 0 && g.dashTime <= 0, "Disabled HUD controls cannot leave a future dodge intent"); }
        }
        Console.WriteLine("PASS both real Dodge HUD layouts accept only affordable near-ready clicks, retain cooldown presentation, and preserve captured direction");
    }

    private static void LastValidIntentWinsWithoutChangingSameFrameCast(FrontierRuntime app)
    {
        Prepare(app); QueueKey(app); Input.keys.Add(KeyCode.Q); Update(app);
        ActorAction cast = app.Game.playerAction; Check(cast != null && cast.kind == "skill", "A fresh affordable Q is accepted after the dodge request");
        Frames(app, 12); Check(app.Game.dashTime <= 0 && DashEffects(app) == 0 && app.Game.playerAction == cast, "A later accepted technique cancels the unstarted dodge");

        Prepare(app); Input.held.Add(KeyCode.J); Update(app); Input.held.Clear(); Frames(app, 9);
        ActorAction attack = app.Game.playerAction; Input.keys.Add(KeyCode.Q); Update(app);
        Check(app.Game.playerAction == attack && app.Game.skillCooldown <= 0, "The reverse-priority fixture has an unstarted real Q intent");
        QueueKey(app); AwaitDash(app); Check(app.Game.dashTime > 0, "A later valid dodge starts"); Frames(app, 25);
        Check(app.Game.skillCooldown <= 0 && app.Game.playerAction == null, "The accepted dodge removes the old queued Q instead of casting it afterward");

        Prepare(app); Input.axes["Horizontal"] = 1; Input.keys.Add(KeyCode.Space); Input.keys.Add(KeyCode.Q); Input.held.Add(KeyCode.J); Update(app);
        Check(app.Game.dashTime > 0 && app.Game.playerAction == null && Math.Abs(app.Game.save.energy - 80) < .001f, "An immediately started dodge plus same-frame Q keeps the existing cost and priority");
        Input.axes["Horizontal"] = 0;
        for (int i = 0; i < 15 && app.Game.playerAction == null; i++) Update(app);
        Check(app.Game.playerAction != null && app.Game.playerAction.kind == "skill" && app.Game.skillCooldown > 0, "Same-frame Q still casts after the active dodge before held J");
        Console.WriteLine("PASS later accepted Q/dodge supersede only unstarted opposite intents; the existing same-frame started-dodge plus Q behavior remains intact");
    }

    private static void PausesTravelLoadAndRangeBoundariesDiscardDodge(FrontierRuntime app)
    {
        foreach (string boundary in new[] { "pause", "inventory", "application", "load", "travel", "range-reset", "range-end", "range-clear" })
        {
            Prepare(app, 1, boundary.StartsWith("range") ? 0 : 1); var g = app.Game;
            if (boundary.StartsWith("range"))
            {
                WorldObject mentor = Array.Find(g.Map.objects, o => o.kind == "mentor"); Check(mentor != null, "Town retains Ada");
                g.save.x = mentor.x; g.save.y = mentor.y - .7f; Check(g.StartRange(0), "The boundary fixture starts the real unlocked range");
            }
            QueueKey(app); Check(g.dashTime <= 0, "The boundary fixture leaves an unstarted request");
            if (boundary == "pause" || boundary == "inventory")
            { Input.keys.Add(boundary == "pause" ? KeyCode.Escape : KeyCode.I); Update(app); Frames(app, 15); g.Close(); }
            else if (boundary == "application")
            { Call(app, "OnApplicationPause", true); Frames(app, 15); Call(app, "OnApplicationPause", false); }
            else if (boundary == "load") { g.Load(g.save); g.Close(); g.enemies.Clear(); }
            else if (boundary == "travel") { g.Enter(0); g.enemies.Clear(); }
            else if (boundary == "range-reset") Check(g.ResetRange(), "Actual reset accepts the live range");
            else if (boundary == "range-end") g.EndPractice();
            else { g.enemies.Clear(); Update(app); Check(g.RangeFinished, "Actual empty-target review finishes the range"); }
            int launches = 0;
            for (int i = 0; i < 35; i++) { float cooldown = g.dashCooldown; Update(app); if (g.dashCooldown > cooldown + .1f) launches++; }
            Check(launches == 0 && g.dashTime <= 0 && DashEffects(app) == 0, "No latent dodge can cross " + boundary + " back into controllable gameplay");
        }
        Console.WriteLine("PASS real Escape/inventory/application pause, Load/travel and range reset/exit/cleared-review discard pending dodge input");
    }

    public static int Main()
    {
        var app = new FrontierRuntime(); Call(app, "Awake");
        if (app.Game == null) { Console.WriteLine("FAIL Runtime Awake could not load the shipped campaign"); return 1; }
        Action<FrontierRuntime>[] cases = { KeyboardAndPadCaptureOneDodgeForEveryClass, RejectedInputsDoNotWaitForReadiness,
            HudWindowIsClickableWithoutPretendingReady, LastValidIntentWinsWithoutChangingSameFrameCast, PausesTravelLoadAndRangeBoundariesDiscardDodge };
        int failures = 0;
        foreach (var test in cases) try { test(app); } catch (Exception error) { failures++; Console.WriteLine("FAIL " + test.Method.Name + ": " + (error.InnerException ?? error).Message); }
        Call(app, "OnDestroy"); Console.WriteLine("Runtime dodge buffer " + (cases.Length - failures) + "/" + cases.Length + "; NATIVE_UNVERIFIED"); return failures == 0 ? 0 : 1;
    }
}
