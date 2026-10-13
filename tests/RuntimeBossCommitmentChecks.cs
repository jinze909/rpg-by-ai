// Production Update, OnGUI and SaveStore paths using offline Unity declarations.
// Initial encounter/HP staging is distinct from actual J/Q threshold crossings.
// These checks are not a native Unity Player build, render or playtest.
using System;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeBossCommitmentChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string captureFolder;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static object Call(FrontierRuntime app, string name, params object[] args)
    { return typeof(FrontierRuntime).GetMethod(name, Private).Invoke(app, args); }
    private static void Set(FrontierRuntime app, string name, object value)
    { typeof(FrontierRuntime).GetField(name, Private).SetValue(app, value); }
    private static void Update(FrontierRuntime app) { Call(app, "Update"); Input.keys.Clear(); }
    private static void Mouse(FrontierRuntime app, Point target)
    {
        Point screen = (Point)Call(app, "ScreenPoint", target);
        Input.mousePosition = new Vector3(screen.x, Screen.height - screen.y, 0);
    }
    private static void Draw(FrontierRuntime app, string capture = null)
    {
        GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app, "OnGUI");
        if (captureFolder != null && capture != null)
        {
            var commands = GUI.commands.ToArray(); var textures = GUI.textureCalls.ToArray(); var buttons = GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder, "qa-boss-commitment-" + capture);
            GUI.commands.AddRange(commands); GUI.textureCalls.AddRange(textures); GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static Enemy Prepare(FrontierRuntime app, int profession, int phase, int room = 2)
    {
        Input.Clear(); GUI.queuedClick = null; Screen.width = 1280; Screen.height = 800; Time.unscaledDeltaTime = .02f;
        app.BeginVisualReview(profession, room, true); app.VisualReviewPaused = false;
        var g = app.Game; Enemy boss = g.enemies.Find(e => e.boss);
        Check(boss != null, "The production altar encounter creates its mature guardian");
        g.enemies.RemoveAll(e => e != boss); // Isolate this mature guardian's prepared attack.
        g.save.companion = -1; g.save.difficulty = 1; g.save.reducedMotion = true;
        g.save.talents[1] = 0; g.save.foodUntil = 0; g.invulnerable = 0;
        boss.hp = boss.maxHp * (phase == 1 ? .66f : .33f) + 1;
        Check(boss.BossPhase == phase, "Initial HP staging starts just above the mature phase threshold");
        // The western bank is beside water: ordinary Boss recoil can legally
        // obstruct its line of attack there. Approach along the southern lane
        // to exercise an accepted contact without removing terrain protection.
        Check(app.SetVisualReviewPosition(boss.Position + new Point(0, 2.4f)), "The real encounter has valid close-range player footing");
        Set(app, "camera", g.Position); Set(app, "applicationPaused", false); Set(app, "pointerSeen", false);
        Set(app, "controllerUsed", false); Set(app, "combatHudTime", 0f); Call(app, "Resize");
        for (int frame = 0; frame < 400 && boss.state != 1; frame++) { Mouse(app, boss.Position); Update(app); }
        Check(boss.state == 1 && boss.AttackPhase == phase && boss.windupDuration > .8f,
            "Actual production AI prepares a fresh full warning before any player strike");
        return boss;
    }
    private static GUI.TextureCall WarningDisc(FrontierRuntime app, Enemy boss, float expectedRadius)
    {
        Point center = (Point)Call(app, "ScreenPoint", boss.target);
        GUI.TextureCall disc = GUI.textureCalls.Find(c => c.texture.name == "resource:Frontier/Art/Effects"
            && Math.Abs(c.uv.x - .5f) < .001f && Math.Abs(c.uv.y) < .001f
            && Math.Abs(c.rect.x - (center.x - expectedRadius * 36)) < .01f
            && Math.Abs(c.rect.y - (center.y - expectedRadius * 36)) < .01f
            && Math.Abs(c.rect.width - expectedRadius * 72) < .01f && Math.Abs(c.rect.height - expectedRadius * 72) < .01f);
        Check(disc != null, "Actual OnGUI draws the warning atlas at the prepared collision radius, " + expectedRadius);
        Rect bounds = (Rect)Call(app, "EnemyWarningBounds", boss);
        Check(Math.Abs(bounds.x - disc.rect.x) < .01f && Math.Abs(bounds.y - disc.rect.y) < .01f
            && Math.Abs(bounds.width - disc.rect.width) < .01f && Math.Abs(bounds.height - disc.rect.height) < .01f,
            "HUD threat geometry uses the same prepared boundary as the actual world warning");
        return disc;
    }
    private static void CrossByRealInput(FrontierRuntime app, Enemy boss, int phase, bool technique)
    {
        float hp = boss.hp; float duration = boss.windupDuration; Point target = boss.target;
        Mouse(app, boss.Position);
        if (technique) Input.keys.Add(KeyCode.Q); else Input.held.Add(KeyCode.J);
        Update(app); Input.held.Clear();
        ActorAction action = app.Game.playerAction;
        Check(action != null && action.kind == (technique ? "skill" : "attack"), "Actual " + (technique ? "Q" : "J") + " starts the production attack");
        for (int frame = 0; frame < 50 && boss.hp == hp; frame++) { Mouse(app, boss.Position); Update(app); }
        Check(action.released && boss.hp < hp && boss.hp > 0 && boss.BossPhase == phase + 1,
            "The actual input's released contact crosses exactly one living guardian HP threshold");
        Check(boss.state == 1 && boss.stagger < 95 && boss.AttackPhase == phase,
            "A non-breaking real player strike cannot retarget the prepared attack to the new HP phase");
        Check(Math.Abs(boss.windupDuration - duration) < .001f && (boss.target - target).Length < .001f,
            "The already announced windup duration and locked target survive a real threshold crossing");
        Draw(app); WarningDisc(app, boss, 2.2f + phase * .2f);
    }
    public static void ActualJQCrossingReleaseAndNextWarning(FrontierRuntime app)
    {
        for (int phase = 1; phase <= 2; phase++) foreach (bool technique in new[] { false, true })
        {
            Enemy boss = Prepare(app, technique ? 2 : 1, phase);
            CrossByRealInput(app, boss, phase, technique); var g = app.Game;
            float health = g.save.health; int frames = 0;
            while (boss.state == 1 && frames++ < 120) { Mouse(app, boss.Position); Update(app); }
            Check(frames < 120 && !g.Dying && boss.state == 0 && boss.releaseTime > 0,
                "The non-breaking threshold crossing still reaches the original guardian release");
            Check(Math.Abs(health - g.save.health - (22 + phase * 3)) < .001f,
                "Real accepted slam contact uses the prepared phase damage, without test invulnerability or health replacement: phase=" + phase + ", Q=" + technique + ", loss=" + (health - g.save.health) + ", expected=" + (22 + phase * 3));
            Effect contact = g.effects.Find(e => e.kind == "danger");
            Check(contact != null && Math.Abs(contact.radius - (2.2f + phase * .2f)) < .001f,
                "The release effect retains the originally warned radius");
            int count = 0;
            foreach (Projectile shot in g.projectiles) if (!shot.friendly)
            {
                count++; Check(Math.Abs(shot.velocity.Length - (3.5f + phase * .4f)) < .001f,
                    "The released mature radial volley retains prepared speed");
            }
            Check(count == 8 + phase * 2, "Room two's real radial volley retains its prepared spoke count: phase=" + phase + ", Q=" + technique + ", count=" + count);
            Check(Math.Abs(boss.timer - (2.2f - (phase + 1) * .3f)) < .001f,
                "Recovery uses the live phase after contact rather than delaying legitimate phase progression");
            frames = 0;
            while (boss.state != 1 && frames++ < 200) { Mouse(app, boss.Position); Update(app); }
            Check(frames < 200 && !g.Dying && boss.AttackPhase == phase + 1
                && Math.Abs(boss.windupDuration - (1.1f - (phase + 1) * .08f)) < .001f,
                "The next actual AI windup captures the updated phase and its normal shorter warning");
            Draw(app); WarningDisc(app, boss, 2.2f + (phase + 1) * .2f);
        }
        Console.WriteLine("PASS Actual J/Q cross both mature HP thresholds during AI windup; locked tell, accepted slam damage, radial count/speed and next fresh warning; NATIVE_UNVERIFIED");
    }
    public static void ActualWarningRenderingPoliciesAndOtherEnemies(FrontierRuntime app)
    {
        Enemy boss = Prepare(app, 1, 1); CrossByRealInput(app, boss, 1, false); var g = app.Game;
        for (int language = 0; language < 2; language++) for (int policy = 0; policy < 4; policy++)
        {
            g.save.english = language == 1; g.save.largeText = policy >= 2;
            g.save.reducedMotion = (policy & 1) != 0; g.save.effectsQuality = policy & 1;
            Screen.width = policy >= 2 ? 960 : 1920; Screen.height = policy >= 2 ? 600 : 1080; Call(app, "Resize");
            string suffix = (language == 1 ? "en" : "zh") + "-policy" + policy;
            Draw(app, "old-warning-new-hp-" + suffix); GUI.TextureCall disc = WarningDisc(app, boss, 2.4f);
            Check(disc.color.a >= .1f && g.enemies.Contains(boss) && boss.BossPhase == 2 && boss.AttackPhase == 1,
                "Language, text size, resolution and effects policy cannot resize a prepared warning or hide the living guardian");
            Check(GUI.commands.Exists(c => c.Contains(GUI.S(g.L(g.CurrentBoss.name, g.CurrentBoss.en) + "  /  2"))),
                "The actual boss HUD reports the new live HP phase while the committed tell remains phase one");
        }
        // These are staged boundary comparisons, separate from the actual input above.
        foreach (int kind in new[] { 0, 1, 2 })
        {
            Enemy normal = new Enemy(40 + kind, kind, g.Position.x + 2, g.Position.y, 80);
            normal.state = 1; normal.target = g.Position; normal.windupDuration = .65f; normal.timer = .4f;
            normal.attackPhase = 3;
            Check(Math.Abs(GameCore.EnemyAreaRadius(normal) - GameCore.EnemyMeleeRadius) < .001f,
                "Ordinary enemy warning geometry stays independent of transient boss phase staging");
        }
        Enemy signal = new Enemy(GameCore.SignalWardenId, 3, g.Position.x + 2, g.Position.y, GameCore.SignalWardenHealth, true);
        signal.state = 1; signal.target = g.Position; signal.attackOrigin = signal.Position; signal.attackDestination = g.Position;
        signal.windupDuration = 1.1f; signal.timer = .7f;
        for (int pattern = 0; pattern < 3; pattern++)
        {
            signal.cycle = pattern; signal.hp = signal.maxHp; signal.attackPhase = 1;
            Rect before = (Rect)Call(app, "EnemyWarningBounds", signal);
            GUI.commands.Clear(); GUI.textureCalls.Clear(); Call(app, "DrawEnemyWarning", signal);
            string[] geometry = GUI.commands.ToArray();
            Check(geometry.Length > 0, "The optional Warden emits its actual authored warning geometry");
            signal.hp = signal.maxHp * .2f; signal.attackPhase = 3;
            Rect after = (Rect)Call(app, "EnemyWarningBounds", signal);
            GUI.commands.Clear(); GUI.textureCalls.Clear(); Call(app, "DrawEnemyWarning", signal);
            Check(before.x == after.x && before.y == after.y && before.width == after.width && before.height == after.height
                && string.Join("\n", geometry) == string.Join("\n", GUI.commands.ToArray()),
                "All three existing optional Warden lane/mark/fan warning draws stay byte-identical across boss phase staging");
        }
        Console.WriteLine("PASS Actual OnGUI committed disc and HUD bounds across eight bilingual/display policies, live phase HUD, and ordinary/optional warning dispatch; NATIVE_UNVERIFIED");
    }
    public static void ActualSaveStoreLegacyAndFreshWarning(FrontierRuntime app)
    {
        for (int version = 1; version <= 2; version++)
        {
            Enemy boss = Prepare(app, 1, 1); CrossByRealInput(app, boss, 1, false); var g = app.Game;
            float hp = boss.hp; float timer = boss.timer; float duration = boss.windupDuration;
            Call(app, "OnApplicationPause", true); for (int frame = 0; frame < 12; frame++) Update(app);
            Check(boss.timer == timer && boss.windupDuration == duration && boss.AttackPhase == 1,
                "Application pause retains the real prepared warning without advancing or changing its phase");
            Call(app, "OnApplicationPause", false); g.PrepareSave(); g.save.version = version;
            string json = JsonUtility.ToJson(g.save);
            Check(!json.Contains("attackPhase") && !json.Contains("windupDuration"),
                "Transient attack commitment does not add a field to the mature save payload");
            if (version == 1) File.WriteAllText(SaveStore.PathFor(1), json);
            else Check(SaveStore.Write(g.save, 2), "Production checksum envelope stores the current guardian HP");
            string issue; SaveData saved = SaveStore.Read(version, out issue);
            Check(saved != null && issue == null && saved.version == version,
                "Actual SaveStore reads both legacy raw v1 and checksum-backed v2 saves");
            g.Load(saved); g.Start(); boss = g.enemies.Find(e => e.boss);
            Check(boss != null && Math.Abs(boss.hp - hp) < .001f && boss.BossPhase == 2
                && boss.state == 0 && boss.releaseTime == 0 && boss.attackPhase == 0,
                "Production load preserves real post-strike HP but discards the old partial warning");
            for (int frame = 0; frame < 1500 && boss.state != 1 && !g.Dying; frame++) { Mouse(app, boss.Position); Update(app); }
            Check(!g.Dying && boss.state == 1 && boss.AttackPhase == 2
                && Math.Abs(boss.windupDuration - .94f) < .001f && boss.timer > .9f,
                "The restored guardian starts a complete new warning at its saved live phase");
            Draw(app, "fresh-warning-save-v" + version); WarningDisc(app, boss, 2.6f);
        }
        Console.WriteLine("PASS Real application pause, legacy v1/raw and v2/checksum SaveStore retain post-input HP while transient commitment resets to a fresh safe warning; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous = Application.persistentDataPath; string directory = Path.Combine(Path.GetTempPath(), "starfall-boss-commitment-" + Guid.NewGuid());
        Application.persistentDataPath = directory; captureFolder = args.Length > 0 ? args[0] : null;
        var app = new FrontierRuntime(); int failed = 0;
        try
        {
            Directory.CreateDirectory(directory);
            Call(app, "Awake"); Check(app.Game != null, "Actual runtime Awake loads the shipped mature campaign");
            foreach (Action<FrontierRuntime> test in new Action<FrontierRuntime>[]
                { ActualJQCrossingReleaseAndNextWarning, ActualWarningRenderingPoliciesAndOtherEnemies, ActualSaveStoreLegacyAndFreshWarning })
            {
                try { test(app); }
                catch (Exception error) { failed++; Console.WriteLine("FAIL " + test.Method.Name + ": " + (error.InnerException ?? error).Message); }
            }
            app.EndVisualReview(); Call(app, "OnDestroy");
        }
        finally { Application.persistentDataPath = previous; Input.Clear(); GUI.queuedClick = null; if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        Console.WriteLine("Runtime Boss commitment: " + (3 - failed) + "/3 groups passed; NATIVE_UNVERIFIED"); return failed == 0 ? 0 : 1;
    }
}
