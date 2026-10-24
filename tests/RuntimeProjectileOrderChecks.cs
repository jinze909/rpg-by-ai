// Real FrontierRuntime Update/input, action release, contact and OnGUI with
// offline Unity declarations. Isolated stunned foes are not natural-AI play QA.
using System;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeProjectileOrderChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
    private static bool Near(float a, float b) { return Math.Abs(a - b) < .003f; }
    private static bool Same(Point a, Point b) { return (a - b).Length < .001f; }
    private static object Call(object value, string method, params object[] args)
    {
        var entry = value.GetType().GetMethod(method, Private | BindingFlags.Public);
        Check(entry != null, "Existing production entry point: " + method); return entry.Invoke(value, args);
    }
    private static void Set(object value, string field, object data) { value.GetType().GetField(field, Private).SetValue(value, data); }
    private static T Get<T>(object value, string field) { return (T)value.GetType().GetField(field, Private).GetValue(value); }
    private static void Update(FrontierRuntime app) { Call(app, "Update"); Input.keys.Clear(); Input.buttonDown.Clear(); }
    private static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key); Update(app); }
    private static void Draw(FrontierRuntime app)
    { GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app, "OnGUI"); }
    private static AudioSource Audio(FrontierRuntime app) { return Get<AudioSource>(Get<FrontierAudio>(app, "audioSystem"), "effects"); }
    private static void Mouse(FrontierRuntime app, Point world)
    {
        Point p = (Point)Call(app, "ScreenPoint", world); float scale = Get<float>(app, "scale");
        Input.mousePosition = new Vector3(Get<float>(app, "offsetX") + p.x * scale,
            Screen.height - Get<float>(app, "offsetY") - p.y * scale, 0);
    }
    private static void Prepare(FrontierRuntime app, int weapon = 1, int style = 0)
    {
        Input.Clear(); GUI.queuedClick = null; GUI.matrix = Matrix4x4.identity; GUI.color = Color.white; GUI.enabled = true;
        Screen.width = 1280; Screen.height = 800; Time.unscaledDeltaTime = .02f; Event.current.type = EventType.Repaint;
        app.BeginVisualReview(weapon, 1); app.VisualReviewPaused = false; var g = app.Game;
        g.enemies.Clear(); g.hazards.Clear(); g.projectiles.Clear(); g.effects.Clear(); g.fallen.Clear();
        g.save.weapon = weapon; g.save.weaponStyles = new int[3]; g.save.weaponStyles[weapon] = style;
        g.save.companion = -1; g.save.reducedMotion = true; g.save.effectsQuality = 0; g.save.showGuidance = false;
        g.save.english = true; g.save.difficulty = 1; g.save.talents = new int[3]; g.save.relics = new[] { -1, -1 };
        g.save.health = g.save.maxHealth; g.save.energy = 100; g.save.totalTime = 100; g.save.mealUntil = g.save.foodUntil = g.save.teaUntil = 0;
        g.save.seed = 1; g.invulnerable = 0; g.toastTime = 0; g.save.controllerAimProfile = 0; g.save.controllerAimInvertY = false;
        Point feet = new Point(12, 14);
        Check(g.save.room == 1 && g.CanStand(feet) && app.SetVisualReviewPosition(feet) && Same(g.Position, feet),
            "Fixture uses exact legal player feet on the unchanged shipped forest map");
        Set(app, "camera", feet); Set(app, "applicationPaused", false); Set(app, "pointerSeen", false);
        Set(app, "pointerAimActive", false); Set(app, "controllerUsed", false); Set(app, "resumeTime", 0f);
        Set(app, "arrivalTime", 0f); Set(app, "combatHudTime", 3f); Call(app, "Resize");
        Mouse(app, feet + new Point(5, 0)); Update(app); g.effects.Clear(); g.sounds.Clear(); Audio(app).heard.Clear();
    }
    private static Enemy Foe(FrontierRuntime app, int id, Point p)
    {
        var g = app.Game; Check(g.CanStand(p), "Every isolated overlapping actor retains legal shipped forest feet");
        var enemy = new Enemy(id, 0, p.x, p.y, 500) { stun = 100, timer = 9999 };
        g.enemies.Add(enemy); return enemy;
    }
    private static ActorAction Start(FrontierRuntime app, Point direction, bool controller)
    {
        var g = app.Game;
        if (controller)
        {
            Input.axes["Aim Horizontal"] = direction.x; Input.axes["Aim Vertical"] = -direction.y; Update(app);
            Check(app.ControllerAimActive && !g.aimTarget.HasValue, "Actual fresh right stick owns free aim");
        }
        else { Mouse(app, g.Position + direction * 5); Update(app); }
        WeaponProfile profile = g.Profile(g.save.weapon, g.Style); float energy = g.save.energy;
        Key(app, controller ? KeyCode.JoystickButton2 : KeyCode.J); ActorAction action = g.playerAction;
        Check(action != null && action.kind == "attack" && !action.released && action.elapsed == 0,
            "Actual keyboard J/controller X starts one unreleased ordinary action");
        Check(Near(energy - g.save.energy, profile.energy) && Near(g.attackCooldown, profile.interval)
            && Near(action.contact, profile.interval * (action.weapon == 1 ? .55f : .45f)) && Near(action.damage, profile.damage),
            "Actual input keeps the original immediate energy payment, cooldown, damage and contact time");
        Check(Same(action.direction, direction) && action.target.HasValue != controller && action.impact.playerOwned,
            "Actual device captures its original target policy, direction and player ownership"); return action;
    }
    private static void Release(FrontierRuntime app, ActorAction action, Enemy a = null, Enemy b = null)
    {
        int frames = 0;
        while (!action.released && frames < 50)
        {
            Check(GoodWindup(app.Game, a, b), "Prepared action cannot emit a projectile or early overlapping-target damage");
            Update(app); frames++;
        }
        Check(action.released && frames == (int)Math.Ceiling((action.contact - .00001f) / .02f)
            && Near(action.elapsed, frames * .02f), "Actual simulation releases at exactly the original first .02s contact frame");
    }
    private static bool GoodWindup(GameCore g, Enemy a, Enemy b)
    { return g.projectiles.Count == 0 && (a == null || a.hp == 500) && (b == null || b.hp == 500); }
    private static void HitPair(GameCore g, Enemy front, Enemy rear, float damage, bool explosive, string reason)
    {
        Check(Near(500 - front.hp, damage) && Near(500 - rear.hp, explosive ? damage * .6f : 0),
            reason + "; front loss=" + (500 - front.hp) + ", rear loss=" + (500 - rear.hp));
        Check(g.projectiles.Count == 0 && g.effects.FindAll(e => e.kind == (explosive ? "magicImpact" : "projectileImpact")).Count == 1,
            "One nonpiercing shot is consumed with exactly one existing impact");
    }
    private static void ActualKeyboardAndControllerFlightOrder(FrontierRuntime app)
    {
        int cases = 0;
        for (int weapon = 1; weapon <= 2; weapon++) foreach (bool pad in new[] { false, true })
            foreach (bool reverse in new[] { false, true }) foreach (bool offAxis in new[] { false, true })
            {
                // These centers first enter the same existing .08m sample.
                // The off-axis rear center is closer in projection but its
                // circle boundary is later than the on-axis front boundary.
                Prepare(app, weapon, weapon == 2 && pad ? 1 : 0); var g = app.Game;
                float frontX = weapon == 1 ? 15.08f : 15.12f;
                Point rearFeet = offAxis ? new Point(weapon == 1 ? 15.02f : 15.08f, 14.35f) : new Point(frontX + .04f, 14);
                Enemy rear = Foe(app, 99801, rearFeet), front = Foe(app, 99802, new Point(frontX, 14));
                if (reverse) g.enemies.Reverse();
                ActorAction action = Start(app, new Point(1, 0), pad); Point? target = action.target; ImpactProfile impact = action.impact;
                if (!pad) Mouse(app, g.Position + new Point(-5, 0));
                Release(app, action, front, rear);
                Check(g.projectiles.Count == 1 && ReferenceEquals(g.projectiles[0].impact, impact), "Original release emits one captured ordinary projectile");
                Projectile shot = g.projectiles[0]; Point velocity = shot.velocity;
                Check(!shot.piercing && shot.explosive == (weapon == 2) && Near(shot.damage, action.damage)
                    && Same(action.direction, new Point(1, 0)) && (!target.HasValue || Same(action.target.Value, target.Value)),
                    "Released shot retains the committed ordinary type, direction, damage and target after live pointer turns");
                int frames = 0; while (g.projectiles.Contains(shot) && frames++ < 60) Update(app);
                Effect contact = g.effects.Find(e => e.kind == (weapon == 2 ? "magicImpact" : "projectileImpact"));
                Check(contact != null, "Actual flight reaches one sampled contact");
                float radius = .5f + shot.Radius;
                Point previousSample = contact.position + velocity.Normalized * -.08f;
                Check((front.Position - contact.position).Length < radius && (rear.Position - contact.position).Length < radius
                    && (front.Position - previousSample).Length >= radius && (rear.Position - previousSample).Length >= radius,
                    "Both targets are admitted together for the first time at the original .08m flight sample");
                Check(!offAxis || rear.Position.x < front.Position.x, "Off-axis case defeats simple center-projection ordering");
                HitPair(g, front, rear, action.damage, weapon == 2, "Actual " + (pad ? "controller" : "keyboard") + " flight selects front independently of list order and center projection");
                Check(shot.hits.Count == 1 && shot.hits[0] == front.id && Same(shot.velocity, velocity)
                    && ReferenceEquals(shot.impact, impact), "Ordered contact retains one direct-hit identity, nonhoming flight and captured impact ownership"); cases++;
            }
        Console.WriteLine("PASS " + cases + " real keyboard J/right-stick-X bow-staff flight cases, exact original wind-up/payment/cooldown, aligned/off-axis first-sample front victim in both list orders and captured impact; NATIVE_UNVERIFIED");
    }
    private static void ExactTwentyDamageAndFeedback(FrontierRuntime app)
    {
        int cases = 0;
        for (int weapon = 1; weapon <= 2; weapon++) foreach (bool reverse in new[] { false, true }) foreach (bool owned in new[] { false, true })
        {
            Prepare(app, weapon); var g = app.Game;
            Enemy rear = Foe(app, 99811, new Point(15.12f, 14)), front = Foe(app, 99812, new Point(15.08f, 14));
            if (reverse) g.enemies.Reverse();
            var impact = new ImpactProfile(weapon, 0, weapon, 0, 0, owned);
            var shot = new Projectile { friendly = true, visual = weapon == 1 ? "arrow" : "starfire", explosive = weapon == 2,
                position = new Point(14.42f, 14), velocity = new Point(8, 0), damage = 20, impact = impact };
            g.projectiles.Add(shot); Time.unscaledDeltaTime = .01f; Update(app);
            HitPair(g, front, rear, 20, weapon == 2, "Controlled damage-20 runtime shot keeps exact full direct damage and 60-percent splash");
            Check(shot.hits.Count == 1 && shot.hits[0] == front.id && ReferenceEquals(shot.impact, impact) && shot.impact.playerOwned == owned,
                "Direct-hit identity and captured ally/player ownership survive contact sorting");
            var numbers = g.effects.FindAll(e => e.kind == "number");
            Check(numbers.Count == (weapon == 2 ? 2 : 1) && numbers.Exists(e => e.text == "20")
                && (weapon != 2 || numbers.Exists(e => e.text == "12")) && Audio(app).heard.Contains("hit"),
                "Real Hit/audio produces exactly the original direct/splash numbers and accepted-hit sound");
            if (weapon == 2) Check(g.effects.Exists(e => e.kind == "magicImpact" && Near(e.radius, 1.8f)), "Explosion retains the original 1.8m feedback radius");
            Draw(app); Check(GUI.commands.Exists(c => c.Contains("\"text\":\"20\""))
                && (weapon != 2 || GUI.commands.Exists(c => c.Contains("\"text\":\"12\""))), "Actual OnGUI draws both existing full and splash numbers");
            for (int i = 0; i < 12; i++) Update(app);
            Check(Near(front.hp, 480) && Near(rear.hp, weapon == 2 ? 488 : 500) && g.projectiles.Count == 0,
                "Consumed ordinary/explosive shot cannot hit again during later Updates"); cases++;
        }
        Console.WriteLine("PASS " + cases + " controlled real-Update damage20 ordinary/explosive contacts, direct20/splash12, both list orders and ally/player ownership, one impact/audio receipt and actual OnGUI numbers; NATIVE_UNVERIFIED");
    }
    private static void ActualAttachmentOrderKeepsCapturedApproach(FrontierRuntime app)
    {
        int cases = 0;
        for (int weapon = 1; weapon <= 2; weapon++) foreach (bool pad in new[] { false, true }) foreach (bool reverse in new[] { false, true })
        {
            Prepare(app, weapon); var g = app.Game; Point feet = g.Position, north = new Point(0, -1);
            // Both overlap the feet and satisfy the original north-facing
            // admission. Their order differs along north and the actual
            // northwest attachment; ranking by prepared aim picks the rear.
            Enemy rear = Foe(app, 99821, feet + new Point(-.30f, -.02f));
            Enemy front = Foe(app, 99822, feet + new Point(-.02f, -.20f));
            if (reverse) g.enemies.Reverse(); ActorAction action = Start(app, north, pad);
            Point? target = action.target; ImpactProfile impact = action.impact;
            if (!pad) Mouse(app, feet + new Point(0, 5));
            Release(app, action, front, rear);
            HitPair(g, front, rear, action.damage, weapon == 2, "Actual attachment ray chooses front rather than prepared-aim/list order");
            Check(Same(front.recoil, north) && Same(action.direction, north) && ReferenceEquals(action.impact, impact)
                && (!target.HasValue || Same(action.target.Value, target.Value)),
                "Sorting along the attachment preserves captured northward Hit approach, immutable impact and target");
            Point muzzle = GameCore.WeaponOrigin(feet, weapon, north);
            Check(g.effects.Exists(e => e.kind == "release" && Same(e.position, muzzle))
                && Audio(app).heard.Contains(weapon == 1 ? "arrow" : "spell") && Audio(app).heard.Contains("hit"),
                "Contact keeps the existing visible muzzle release and actual launch/hit audio dispatch"); cases++;
        }
        Console.WriteLine("PASS " + cases + " actual northward keyboard/controller launch contacts in both list orders, actual attachment-delta ranking and unchanged captured Hit approach/target/ownership/muzzle/audio; NATIVE_UNVERIFIED");
    }
    private static string Simulation(GameCore g)
    {
        return JsonUtility.ToJson(g.save) + "/" + JsonUtility.ToJson(g.playerAction) + "/" + JsonUtility.ToJson(g.projectiles)
            + "/" + JsonUtility.ToJson(g.enemies) + "/" + JsonUtility.ToJson(g.effects) + "/" + g.attackCooldown + "/" + g.revision;
    }
    private static void PauseRenderingAndTravelRemainReadOnly(FrontierRuntime app)
    {
        int cases = 0;
        foreach (bool flying in new[] { false, true }) foreach (string boundary in new[] { "draw", "inventory", "application", "review" })
        {
            Prepare(app); var g = app.Game; ActorAction action = Start(app, new Point(1, 0), false);
            if (flying) Release(app, action);
            if (boundary == "inventory") Key(app, KeyCode.I);
            else if (boundary == "application") Call(app, "OnApplicationPause", true);
            else if (boundary == "review") app.VisualReviewPaused = true;
            string before = Simulation(g);
            for (int i = 0; i < 10; i++)
            {
                Event.current.type = i % 2 == 0 ? EventType.Layout : EventType.Repaint; Draw(app);
                if (boundary != "draw") Update(app);
                Check(Simulation(g) == before, "Repeated real OnGUI and " + boundary + " pause preserve full save, action, enemy/feedback and projectile clocks");
            }
            Event.current.type = EventType.Repaint;
            if (boundary == "inventory") Key(app, KeyCode.Escape);
            else if (boundary == "application") Call(app, "OnApplicationPause", false);
            else if (boundary == "review") app.VisualReviewPaused = false;
            float elapsed = action.elapsed, age = g.projectiles.Count == 0 ? 0 : g.projectiles[0].age;
            Update(app);
            Check(flying ? g.projectiles.Count == 1 && g.projectiles[0].age > age : action.elapsed > elapsed && !action.released,
                "Only resumed simulation advances the same prepared action or released flight");
            g.save.coins = 137; g.save.inventory[3] = 7; g.Enter(0);
            Check(g.projectiles.Count == 0 && g.playerAction == null && g.effects.Count == 0 && g.save.coins == 137 && g.save.inventory[3] == 7,
                "Real travel retires pending/flying contact state and preserves accepted economy"); cases++;
        }
        Console.WriteLine("PASS " + cases + " preparing/flying actual Layout-Repaint, inventory/application/review pause-resume cases and real travel retirement with full simulation snapshots and retained economy; NATIVE_UNVERIFIED");
    }
    public static int Main()
    {
        string previous = Application.persistentDataPath, directory = Path.Combine(Path.GetTempPath(), "starfall-projectile-order-" + Guid.NewGuid());
        Application.persistentDataPath = directory; var app = new FrontierRuntime(); int failed = 0;
        Action<FrontierRuntime>[] tests = { ActualKeyboardAndControllerFlightOrder, ExactTwentyDamageAndFeedback,
            ActualAttachmentOrderKeepsCapturedApproach, PauseRenderingAndTravelRemainReadOnly };
        try
        {
            Directory.CreateDirectory(directory); Call(app, "Awake"); Check(app.Game != null, "Actual Awake loads shipped campaign");
            foreach (var test in tests) try { test(app); }
                catch (Exception error) { failed++; Console.WriteLine("FAIL " + test.Method.Name + ": " + (error.InnerException ?? error).Message); }
        }
        finally
        {
            app.EndVisualReview(); Call(app, "OnDestroy"); Application.persistentDataPath = previous; Input.Clear();
            GUI.queuedClick = null; GUI.matrix = Matrix4x4.identity; GUI.color = Color.white; GUI.enabled = true;
            Event.current.type = EventType.Repaint; if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        Console.WriteLine("Runtime projectile order: " + (tests.Length - failed) + "/" + tests.Length + " groups passed; NATIVE_UNVERIFIED");
        return failed == 0 ? 0 : 1;
    }
}
