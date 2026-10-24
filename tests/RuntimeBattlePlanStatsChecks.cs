// Production Update/OnGUI battle-plan comparison, with offline Unity declarations.
// Native Unity import, fonts/GPU, controller hardware and play feel are NATIVE_UNVERIFIED.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeBattlePlanStatsChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly string[] Fields = { "attackDamage", "attackInterval", "attackEnergy", "skillEnergy", "dodgeEnergy", "walkSpeed" };
    static readonly string[] English = { "Opening hit / arrow", "Attack interval", "Attack energy", "Skill energy", "Dodge energy", "Walk speed" };
    static readonly string[] Chinese = { "首击 / 每箭", "普攻间隔", "普攻耗能", "战技耗能", "闪避耗能", "步行速度" };
    static readonly Rect CompareRect = new Rect(1018, 195, 175, 30);
    static string captures;
    [Serializable] public sealed class Label { public string k, text; public float[] r, m; public int size; public int[] padding; }

    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static object Call(object target, string name, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(name, Private | BindingFlags.Public);
        Check(method != null, "Production query exists: " + name); return method.Invoke(target, args);
    }
    static T Get<T>(object target, string name) { return (T)target.GetType().GetField(name, Private).GetValue(target); }
    static void Set(object target, string name, object value) { target.GetType().GetField(name, Private).SetValue(target, value); }
    static bool Near(float a, float b) { return Math.Abs(a - b) < .0001f; }
    static bool Same(Rect a, Rect b) { return Near(a.x, b.x) && Near(a.y, b.y) && Near(a.width, b.width) && Near(a.height, b.height); }
    static Rect Bounds(Label label) { return new Rect(label.r[0], label.r[1], label.r[2], label.r[3]); }
    static string L(FrontierRuntime app, string zh, string en) { return app.Game.L(zh, en); }
    static string Toggle(FrontierRuntime app, bool comparing) { return L(app, comparing ? "装备详情" : "数值对比", comparing ? "Gear details" : "Compare stats"); }
    static void Draw(FrontierRuntime app, string capture = null)
    {
        GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app, "OnGUI");
        if (captures != null && capture != null)
        {
            var commands = GUI.commands.ToArray(); var textures = GUI.textureCalls.ToArray(); var buttons = GUI.buttonCalls.ToArray();
            GUI.Export(captures, "qa-battle-plan-stats-" + capture);
            GUI.commands.AddRange(commands); GUI.textureCalls.AddRange(textures); GUI.buttonCalls.AddRange(buttons);
        }
    }
    static List<Label> Labels()
    {
        var result = new List<Label>();
        foreach (string command in GUI.commands)
            if (command.StartsWith("{\"k\":\"text\"", StringComparison.Ordinal)) result.Add(JsonUtility.FromJson<Label>(command));
        return result;
    }
    static Rect Button(FrontierRuntime app, string text, bool enabled = true)
    {
        Draw(app);
        foreach (Label label in Labels()) if (label.text == text)
        {
            Rect rect = Bounds(label); var button = GUI.buttonCalls.Find(b => Same(b.rect, rect));
            if (button != null) { Check(button.enabled == enabled, "Actual displayed control has truthful enabled state: " + text); return rect; }
        }
        throw new Exception("Actual OnGUI is missing the " + text + " button");
    }
    static void Click(FrontierRuntime app, Rect rect)
    { GUI.queuedClick = rect; Draw(app); Check(!GUI.queuedClick.HasValue, "Actual displayed button consumes its click"); }
    static void Click(FrontierRuntime app, string text) { Click(app, Button(app, text)); }
    static void Update(FrontierRuntime app) { Call(app, "Update"); Input.keys.Clear(); }
    static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key); Update(app); }
    static void Activate(FrontierRuntime app, string text, bool controller = false)
    {
        Rect rect = Button(app, text); var nav = Get<MenuNavigation>(app, "navigation");
        string id = (string)Call(app, "ControlId", rect.x, rect.y, rect.width, rect.height); int attempts = 0;
        while (nav.focused != id && attempts++ < 64) { Key(app, KeyCode.Tab); Draw(app); }
        Check(nav.focused == id && nav.CanActivate(id), "Actual Tab reaches the enabled comparison/selection control: " + text);
        Key(app, controller ? KeyCode.JoystickButton0 : KeyCode.Return); Draw(app);
    }
    static void Open(FrontierRuntime app)
    {
        if (app.Game.mode != "inventory") Key(app, KeyCode.I);
        Check(app.Game.mode == "inventory", "Actual I opens the existing inventory");
        Click(app, L(app, "装备方案", "Battle plans")); Check(app.Game.mode == "plans", "Actual inventory control opens the existing plans mode");
    }
    static void Select(FrontierRuntime app, int slot, bool focus = false)
    {
        string label = L(app, "方案 ", "Plan ") + (slot + 1);
        Rect rect = Button(app, label); Check(Same(rect, new Rect(295, 192 + slot * 148, 270, 38)), "All three original selector rectangles stay unchanged");
        if (focus) Activate(app, label, slot == 2); else Click(app, rect);
        Check(Get<int>(app, "selectedBattlePlan") == slot, "Real selection changes only the requested stored slot");
    }
    static BattlePlan Plan(int weapon, int style, int first = -1, int second = -1, int technique = 0)
    { return new BattlePlan { recorded = true, weapon = weapon, style = style, technique = technique, relicFirst = first, relicSecond = second }; }
    static void Prepare(FrontierRuntime app, bool english = true, bool large = false)
    {
        Input.Clear(); GUI.queuedClick = null; GUI.enabled = true; GUI.color = Color.white; GUI.matrix = Matrix4x4.identity;
        Screen.width = 1280; Screen.height = 800; Time.unscaledDeltaTime = .02f; Event.current.type = EventType.Repaint;
        app.BeginVisualReview(1, 0); app.VisualReviewPaused = false; var g = app.Game;
        g.enemies.Clear(); g.projectiles.Clear(); g.hazards.Clear();
        foreach (string flag in new[] { "core_1", "weapon_1", "weapon_2", "relic_0", "relic_1", "relic_2", "relic_3", "relic_4", "relic_5", "technique_1" }) if (!g.Has(flag)) g.save.flags.Add(flag);
        g.save.weapon = 0; g.save.weaponStyles = new[] { 0, 0, 0 }; g.save.technique = 0; g.save.relics = new[] { -1, -1 };
        g.save.level = 5; g.save.skillPoints = 3; g.save.talents = new[] { 1, 2, 1 }; g.save.mastery = new[] { 22, 84, 45 }; g.save.relicRank = 2;
        g.save.health = 58; g.save.energy = 47; g.save.coins = 313; g.save.shards = 17; g.save.xp = 9; g.save.companion = -1;
        g.save.english = english; g.save.largeText = large; g.save.reducedMotion = true; g.save.effectsQuality = 0; g.save.showGuidance = false;
        g.save.battlePlans = new[] { Plan(1, 2, 2, 4, 1), Plan(2, 1, 1, 5), Plan(0, 1, 0, 3) };
        Set(app, "selectedBattlePlan", 0); Set(app, "battlePlanConfirmation", -1); Set(app, "applicationPaused", false);
        Set(app, "pointerSeen", false); Set(app, "controllerUsed", false); Set(app, "combatHudTime", 0f); Set(app, "resumeTime", 0f);
        Set(app, "saveTimer", -1000f); Set(app, "lastRevision", g.revision); Call(app, "Resize");
    }
    static void RequireCompare(FrontierRuntime app, bool enabled = true)
    { Check(Same(Button(app, Toggle(app, false), enabled), CompareRect), "Comparison uses its single agreed control inside the plans card"); }
    static object Stats(GameCore g, int slot) { return slot < 0 ? Call(g, "CurrentBattlePlanStats") : Call(g, "BattlePlanStatsAt", slot); }
    static float Value(object stats, string field)
    { Check(stats != null, "Valid configuration has comparison numbers"); FieldInfo info = stats.GetType().GetField(field); Check(info != null && info.IsInitOnly, "Comparison snapshot exposes an immutable " + field); return (float)info.GetValue(stats); }
    static string Format(float value, int row) { return value.ToString("0.##", CultureInfo.InvariantCulture) + (row == 1 ? "s" : ""); }
    static void Table(FrontierRuntime app, int slot)
    {
        Draw(app); var labels = Labels(); object current = Stats(app.Game, -1), selected = Stats(app.Game, slot);
        Check(labels.Exists(l => l.text == L(app, "当前", "Current") && l.r[1] >= 240 && l.r[1] < 260), "Visible table labels its current configuration column");
        Check(labels.Exists(l => l.text == L(app, "所选方案", "Selected") && l.r[1] >= 240 && l.r[1] < 260), "Visible table labels its selected saved configuration column");
        for (int row = 0; row < Fields.Length; row++)
        {
            float y = 279 + row * 33, a = Value(current, Fields[row]), b = Value(selected, Fields[row]);
            var rowLabels = labels.FindAll(l => Near(l.r[1], y) && (Near(l.r[0], 619) || Near(l.r[0], 810) || Near(l.r[0], 988)));
            Check(rowLabels.Count == 3, "Each of the six comparison rows has exactly one label and two values");
            Check(rowLabels.Exists(l => Near(l.r[0], 619) && l.text == L(app, Chinese[row], English[row])), "Row states the agreed combat measure: " + Fields[row]);
            Check(rowLabels.Exists(l => Near(l.r[0], 810) && l.text == Format(a, row)), "Displayed current value equals the pure current query: " + Fields[row]);
            float delta = b - a;
            string candidate = Format(b, row) + " (" + (delta >= 0 ? "+" : "") + delta.ToString("0.##", CultureInfo.InvariantCulture) + ")";
            Check(rowLabels.Exists(l => Near(l.r[0], 988) && (l.text == candidate || Math.Abs(delta) < .005f && l.text == Format(b, row))), "Displayed selected value and signed difference equal the pure candidate query: " + Fields[row]);
        }
        Check(app.Game.mode == "plans", "Comparison remains inline in the existing plans mode");
    }
    static string FilesSnapshot()
    {
        if (!Directory.Exists(Application.persistentDataPath)) return "";
        string[] files = Directory.GetFiles(Application.persistentDataPath, "*", SearchOption.AllDirectories); Array.Sort(files, StringComparer.Ordinal);
        string result = ""; foreach (string file in files) result += file + ":" + Convert.ToBase64String(File.ReadAllBytes(file)) + "\n"; return result;
    }
    sealed class Snapshot
    {
        readonly string save, files, actionJson, projectileJson; readonly int revision; readonly float attack, dash, skill, companion, guard, comboTimer, riposte;
        readonly ActorAction action; readonly ImpactProfile impact; readonly Projectile projectile;
        public Snapshot(FrontierRuntime app)
        {
            GameCore g = app.Game; save = JsonUtility.ToJson(g.save); files = FilesSnapshot(); revision = g.revision;
            attack = g.attackCooldown; dash = g.dashCooldown; skill = g.skillCooldown; companion = g.companionCooldown; guard = g.guardTime; comboTimer = g.comboTimer; riposte = g.riposteTime;
            action = g.playerAction; impact = action == null ? null : action.impact; actionJson = JsonUtility.ToJson(action);
            projectile = g.projectiles.Count == 0 ? null : g.projectiles[0]; projectileJson = JsonUtility.ToJson(projectile);
        }
        public void Assert(FrontierRuntime app, string context)
        {
            GameCore g = app.Game;
            Check(JsonUtility.ToJson(g.save) == save && g.revision == revision && FilesSnapshot() == files, context + ": save, equipment, economy, resources, buffs, seed, progression, stored plans, revision and real disk bytes remain exact");
            Check(g.attackCooldown == attack && g.dashCooldown == dash && g.skillCooldown == skill && g.companionCooldown == companion && g.guardTime == guard && g.comboTimer == comboTimer && g.riposteTime == riposte, context + ": all combat cooldowns/combo/counter clocks remain exact");
            Check(ReferenceEquals(g.playerAction, action) && JsonUtility.ToJson(g.playerAction) == actionJson && (action == null || ReferenceEquals(action.impact, impact)), context + ": accepted action and captured impact remain unchanged");
            Check(projectile == null ? g.projectiles.Count == 0 : g.projectiles.Count > 0 && ReferenceEquals(g.projectiles[0], projectile) && JsonUtility.ToJson(projectile) == projectileJson && ReferenceEquals(projectile.impact, impact), context + ": released projectile identity, captured impact, position and lifetime remain unchanged");
            Check(g.mode == "plans", context + ": comparison never opens another game mode");
        }
    }

    static void RealControlsAndSixRows(FrontierRuntime app)
    {
        Prepare(app); Open(app); RequireCompare(app); string before = JsonUtility.ToJson(app.Game.save); int revision = app.Game.revision;
        Activate(app, Toggle(app, false)); Table(app, 0); Check(Get<bool>(app, "battlePlanComparing"), "Actual Enter toggles the comparison view");
        Select(app, 1, true); Table(app, 1); Select(app, 2, true); Table(app, 2);
        Activate(app, Toggle(app, true), true); Check(!Get<bool>(app, "battlePlanComparing"), "Actual controller A returns to gear details");
        RequireCompare(app); Check(JsonUtility.ToJson(app.Game.save) == before && app.Game.revision == revision, "Actual keyboard/controller comparisons and selection cannot equip or mutate a plan");
        Click(app, Toggle(app, false)); Table(app, 2); Click(app, Toggle(app, true));
        app.Game.save.battlePlans[1] = null; Select(app, 1); RequireCompare(app, false);
        GUI.queuedClick = CompareRect; Draw(app); Check(GUI.queuedClick.HasValue && !Get<bool>(app, "battlePlanComparing"), "Empty plan cannot activate disabled comparison"); GUI.queuedClick = null;
        Check(Stats(app.Game, 1) == null, "Empty plan has no candidate numbers");
        app.Game.save.battlePlans[1] = Plan(1, 99); Draw(app); RequireCompare(app, false); Check(Stats(app.Game, 1) == null, "Malformed plan cannot display invented numbers or activate comparison");
        Check(!Labels().Exists(l => l.r[1] >= 279 && l.r[1] <= 444 && Near(l.r[0], 988)), "Empty/invalid gear detail view contains no candidate numeric cells");
        Console.WriteLine("PASS actual I/inventory entry, mouse and Tab/Enter/controller A comparison/slot controls, six visible current/candidate values and signed deltas equal pure queries, empty/malformed controls truthfully disabled; NATIVE_UNVERIFIED");
    }

    static void ReadOnlyPreparedReleasedAndPaused(FrontierRuntime app)
    {
        foreach (bool released in new[] { false, true })
        {
            Prepare(app); var g = app.Game; g.save.weapon = 1; g.save.weaponStyles[1] = 1; g.save.energy = 83;
            Check(app.SetVisualReviewPosition(new Point(13, 13)), "Combat staging uses real legal ground"); Set(app, "camera", g.Position); g.aim = new Point(1, 0);
            Key(app, KeyCode.J); ActorAction action = g.playerAction; Check(action != null && !action.released && action.weapon == 1, "Actual J starts an accepted focused bow windup");
            if (released)
            {
                int count = 0; while (!action.released && count++ < 50) Update(app);
                Check(action.released && g.projectiles.Count > 0 && ReferenceEquals(g.projectiles[0].impact, action.impact), "Actual runtime frames release a real bow contact with its captured impact");
            }
            Open(app); RequireCompare(app);
            // Leave the visual lab's no-save shortcut so an accidental Save() is
            // observable in actual disk bytes and runtime save bookkeeping.
            Set(app, "reviewOriginal", null); Set(app, "saveTimer", -1000f); Set(app, "lastRevision", g.revision);
            Check(SaveStore.Write(g.save, 0), "Purity fixture has a real checksum save to protect");
            var state = new Snapshot(app); float timer = Get<float>(app, "saveTimer");
            for (int i = 0; i < 8; i++)
            {
                Click(app, Toggle(app, i % 2 != 0)); Stats(g, -1); Stats(g, 0); Update(app); timer += Time.unscaledDeltaTime; Draw(app); state.Assert(app, "Repeated real comparison while " + (released ? "released" : "prepared"));
            }
            Check(Get<float>(app, "saveTimer") == timer, "Read-only toggles never call Save or reset its timer");
            Call(app, "OnApplicationPause", true); var paused = new Snapshot(app); float clock = Get<float>(app, "clock");
            Click(app, Toggle(app, false)); Table(app, 0);
            for (int i = 0; i < 12; i++) { Update(app); Draw(app); }
            paused.Assert(app, "Actual application pause plus comparison draws"); Check(Get<float>(app, "clock") == clock, "Actual application pause freezes runtime presentation as well as combat");
            Call(app, "OnApplicationPause", false); paused.Assert(app, "Actual resume before another world frame");
        }
        Console.WriteLine("PASS read-only actual comparison never equips, spends, saves, rerolls, cancels or ages prepared/released J attacks, captured impact/projectile/resource/cooldown identities; real application pause/resume stays frozen; NATIVE_UNVERIFIED");
    }

    static void ApplyAvailabilityAndOwnerReset(FrontierRuntime app)
    {
        Prepare(app); Open(app); RequireCompare(app); Click(app, Toggle(app, false)); Table(app, 0); var g = app.Game;
        g.save.flags.Remove("weapon_1"); Draw(app); Table(app, 0); string reason = g.BattlePlanUnavailableReason(0);
        Check(reason.Length > 0 && Labels().Exists(l => l.text == reason && Near(l.r[1], 532)), "Valid locked plan retains its original exact availability reason alongside visible statistics");
        Button(app, L(app, "应用方案", "Apply plan"), false); Button(app, Toggle(app, true));
        string before = JsonUtility.ToJson(g.save); int revision = g.revision;
        GUI.queuedClick = new Rect(813, 572, 175, 34); Draw(app); Check(GUI.queuedClick.HasValue, "Disabled Apply cannot consume a click"); GUI.queuedClick = null;
        Check(!g.ApplyBattlePlan(0) && JsonUtility.ToJson(g.save) == before && g.revision == revision, "Locked comparison is read-only and never bypasses mature apply ownership");
        g.save.flags.Add("weapon_1"); object expected = Stats(g, 0);
        g.attackCooldown = .8f; g.dashCooldown = .5f; g.skillCooldown = 2.4f; g.companionCooldown = 9; g.guardTime = 1.2f;
        float health = g.save.health, energy = g.save.energy; int points = g.save.skillPoints; int seed = g.save.seed; float coins = g.save.coins, shards = g.save.shards;
        string plans = JsonUtility.ToJson(g.save.battlePlans); int[] styles = (int[])g.save.weaponStyles.Clone();
        Activate(app, L(app, "应用方案", "Apply plan"), true);
        Check(g.save.weapon == 1 && g.Style == 2 && g.save.technique == 1 && g.save.relics[0] == 2 && g.save.relics[1] == 4, "Existing Apply equips exactly the reviewed configuration");
        Check(g.save.health == health && g.save.energy == energy && g.save.skillPoints == points && g.save.coins == coins && g.save.shards == shards && g.save.seed == seed && JsonUtility.ToJson(g.save.battlePlans) == plans && g.save.weaponStyles[0] == styles[0] && g.save.weaponStyles[2] == styles[2], "Apply preserves health, paid energy, progression, stored recipes, RNG and other weapons' styles");
        Check(g.attackCooldown == .8f && g.dashCooldown == .5f && g.skillCooldown == 2.4f && g.companionCooldown == 9 && g.guardTime == 1.2f, "Existing Apply never refreshes combat cooldowns");
        object current = Stats(g, -1); foreach (string field in Fields) Check(Near(Value(expected, field), Value(current, field)), "Applied current statistics equal the previously reviewed candidate: " + field);
        Table(app, 0); Button(app, L(app, "当前方案", "Current plan"), false);
        Key(app, KeyCode.Escape); Check(g.mode == "inventory", "Comparison uses the original Escape parent"); Open(app); RequireCompare(app); Check(!Get<bool>(app, "battlePlanComparing"), "Reopening plans always starts on gear details");
        Select(app, 2); Click(app, Toggle(app, false)); Check(Get<bool>(app, "battlePlanComparing"), "Third slot comparison is active before a world replacement");
        int generation = g.WorldGeneration; g.Enter(0); Check(g.WorldGeneration != generation, "Fixture actually replaces world generation"); g.mode = "plans"; Draw(app);
        Check(Get<int>(app, "selectedBattlePlan") == 0 && !Get<bool>(app, "battlePlanComparing") && Get<int>(app, "battlePlanConfirmation") == -1, "Actual world generation replacement clears stale selected slot, comparison and confirmation owners"); RequireCompare(app);
        Console.WriteLine("PASS visible valid-but-locked stats keep original disabled Apply/reason, actual controller Apply preserves resources/cooldowns and matches reviewed numbers, current-plan no-op, inventory return/reopen and world-generation owner safeguards; NATIVE_UNVERIFIED");
    }

    static void Readable(Label label)
    {
        Rect r = Bounds(label); int left = label.padding[0], right = label.padding[1], top = label.padding[2], bottom = label.padding[3];
        Check(r.x >= 597 && r.x + r.width <= 1217 && r.y >= 178 && r.y + r.height <= 572, "Comparison ink rectangles stay inside their card above existing actions: " + label.text + " @ " + JsonUtility.ToJson(label.r));
        Check(r.height - top - bottom >= label.size, "Comparison text retains at least one full glyph height");
        // Conservative offline estimate: full-em CJK, 0.6-em Latin. Native
        // shaping and exact glyph rasterization are explicitly unverified.
        float width = 0; foreach (char c in label.text) width += label.size * (c > 127 ? 1 : .6f);
        int lines = (int)Math.Ceiling(width / Math.Max(1, r.width - left - right));
        Check(lines * (label.size + 2) <= r.height - top - bottom + 2, "Complete comparison label has estimated wrapped glyph room: " + label.text);
        Check(label.m[2] >= 0 && label.m[5] >= 0, "Screen policy keeps comparison content inside its positive viewport transform");
    }
    static void BilingualLayoutsAndOriginalConfirmations(FrontierRuntime app)
    {
        foreach (bool english in new[] { false, true }) foreach (bool large in new[] { false, true }) foreach (int width in new[] { 960, 1920 })
        {
            Prepare(app, english, large); Screen.width = width; Screen.height = width == 960 ? 600 : 1080; Call(app, "Resize");
            Open(app); RequireCompare(app); Click(app, Toggle(app, false)); Table(app, 0);
            string suffix = (english ? "en" : "zh") + (large ? "-large-" : "-normal-") + width;
            Draw(app, "compare-" + suffix);
            int checkedRows = 0, checkedNotes = 0;
            foreach (Label label in Labels())
            {
                if (!(Near(label.r[0], 619) || Near(label.r[0], 810) || Near(label.r[0], 988)) || label.r[1] < 240 || label.r[1] >= 572) continue;
                Readable(label); if (label.r[1] >= 279 && label.r[1] <= 444) checkedRows++; if (label.r[1] >= 480) checkedNotes++;
            }
            Check(checkedRows == 18 && checkedNotes >= 2, "Every bilingual normal/large 960/1920 table has all six rows and both explanatory notes");
            Rect store = Button(app, L(app, "记下当前装备", "Store current gear")); Check(Same(store, new Rect(619, 572, 175, 34)), "Original Store action keeps its rectangle");
            Check(Same(Button(app, L(app, "应用方案", "Apply plan")), new Rect(813, 572, 175, 34)), "Original Apply action keeps its rectangle");
            Check(Same(Button(app, L(app, "清除方案", "Clear plan")), new Rect(1008, 572, 185, 34)), "Original Clear action keeps its rectangle");
            string saved = JsonUtility.ToJson(app.Game.save); Click(app, store); Draw(app, "overwrite-" + suffix);
            Check(Get<int>(app, "battlePlanConfirmation") == 0 && Get<List<FocusControl>>(app, "drawnControls").Count == 2, "Original overwrite remains a two-control modal during comparison");
            Check(!Labels().Exists(l => l.text == Toggle(app, true) || l.text == Toggle(app, false)), "Compare cannot bypass the actual overwrite modal");
            Key(app, KeyCode.Escape); Draw(app); Check(Get<bool>(app, "battlePlanComparing"), "Canceling overwrite retains the comparison view");
            Click(app, L(app, "清除方案", "Clear plan")); Draw(app, "clear-" + suffix); Activate(app, L(app, "取消", "Cancel"), true); Check(JsonUtility.ToJson(app.Game.save) == saved, "Actual controller Cancel retains all saved plans and live gear");
            Click(app, Toggle(app, true)); RequireCompare(app); Draw(app, "gear-" + suffix); Click(app, Toggle(app, false)); Table(app, 0);
            Check(Same(Button(app, L(app, "返回背包", "Back to inventory")), new Rect(279, 650, 220, 38)), "Original inventory back control keeps its rectangle");
            Click(app, L(app, "返回背包", "Back to inventory")); Check(app.Game.mode == "inventory", "Actual original Back works from comparison in every display policy");
            Open(app); RequireCompare(app); Click(app, Toggle(app, false)); Key(app, KeyCode.JoystickButton1); Check(app.Game.mode == "inventory", "Actual controller B preserves the original parent from comparison");
        }
        Console.WriteLine("PASS bilingual normal/large 960/1920 six-row table/notes with offline glyph-room bounds, Compare/Gear toggles, original Store/Apply/Clear rectangles, two-control overwrite/clear modal isolation/cancel and mouse/Escape/controller B inventory return; NATIVE_UNVERIFIED");
    }

    public static int Main(string[] args)
    {
        string previous = Application.persistentDataPath, directory = Path.Combine(Path.GetTempPath(), "starfall-battle-plan-stats-" + Guid.NewGuid());
        Application.persistentDataPath = directory; captures = args.Length > 0 ? args[0] : null; int failed = 0; var app = new FrontierRuntime();
        try
        {
            Call(app, "Awake"); Check(app.Game != null, "Actual Awake loads the full campaign");
            foreach (Action<FrontierRuntime> test in new Action<FrontierRuntime>[] { RealControlsAndSixRows, ReadOnlyPreparedReleasedAndPaused, ApplyAvailabilityAndOwnerReset, BilingualLayoutsAndOriginalConfirmations })
                try { test(app); } catch (Exception error) { failed++; Console.WriteLine("FAIL " + test.Method.Name + ": " + (error.InnerException ?? error).Message); }
            app.EndVisualReview(); Call(app, "OnDestroy");
        }
        finally { Application.persistentDataPath = previous; Input.Clear(); GUI.queuedClick = null; if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        Console.WriteLine("Runtime battle-plan stats: " + (4 - failed) + "/4 groups passed; NATIVE_UNVERIFIED"); return failed == 0 ? 0 : 1;
    }
}
