// Actual Update, OnGUI and SaveStore paths with offline Unity declarations.
// Unity import, native fonts/GPU, hardware input and play feel are NATIVE_UNVERIFIED.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeBattlePlanChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string captureFolder;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
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
            var texts = GUI.commands.ToArray(); var textures = GUI.textureCalls.ToArray(); var buttons = GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder, "qa-battle-plans-" + capture);
            GUI.commands.AddRange(texts); GUI.textureCalls.AddRange(textures); GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static bool HasText(string text) { return GUI.commands.Exists(c => c.Contains("\"text\":" + GUI.S(text))); }
    private static bool HasTextPart(string text) { return GUI.commands.Exists(c => c.Contains(text)); }
    private static bool EqualRect(Rect a, Rect b) { return a.x == b.x && a.y == b.y && a.width == b.width && a.height == b.height; }
    private static Rect Button(FrontierRuntime app, string label, bool enabled = true)
    {
        Draw(app);
        foreach (string command in GUI.commands)
        {
            if (!command.Contains("\"text\":" + GUI.S(label))) continue;
            int start = command.IndexOf("\"r\":[", StringComparison.Ordinal) + 5, end = command.IndexOf(']', start);
            string[] parts = command.Substring(start, end - start).Split(',');
            var rect = new Rect(float.Parse(parts[0], CultureInfo.InvariantCulture), float.Parse(parts[1], CultureInfo.InvariantCulture),
                float.Parse(parts[2], CultureInfo.InvariantCulture), float.Parse(parts[3], CultureInfo.InvariantCulture));
            var button = GUI.buttonCalls.Find(b => EqualRect(b.rect, rect));
            if (button != null) { Check(button.enabled == enabled, "The real displayed button has the expected enabled state: " + label); return rect; }
        }
        throw new Exception("The actual GUI exposes no button labelled " + label);
    }
    private static void Click(FrontierRuntime app, Rect rect)
    { GUI.queuedClick = rect; Draw(app); Check(!GUI.queuedClick.HasValue, "The real displayed button consumes its click"); }
    private static void Click(FrontierRuntime app, string label) { Click(app, Button(app, label)); }
    private static void Update(FrontierRuntime app) { Call(app, "Update"); Input.keys.Clear(); }
    private static void Frames(FrontierRuntime app, int count) { for (int i = 0; i < count; i++) Update(app); }
    private static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key); Update(app); }
    private static void FocusActivate(FrontierRuntime app, string label, bool pad = false)
    {
        Rect rect = Button(app, label); string id = (string)Call(app, "ControlId", rect.x, rect.y, rect.width, rect.height);
        MenuNavigation nav = Get<MenuNavigation>(app, "navigation"); int attempts = 0;
        while (nav.focused != id && attempts++ < 64) { Key(app, KeyCode.Tab); Draw(app); }
        Check(nav.focused == id && nav.CanActivate(id), "Actual Tab navigation reaches the enabled displayed control: " + label);
        Key(app, pad ? KeyCode.JoystickButton0 : KeyCode.Return); Draw(app);
    }
    private static string L(FrontierRuntime app, string zh, string en) { return app.Game.L(zh, en); }
    private static void Prepare(FrontierRuntime app, int profession = 0, bool english = true, bool large = false)
    {
        Input.Clear(); GUI.queuedClick = null; Screen.width = 1280; Screen.height = 800; Time.unscaledDeltaTime = .02f;
        app.BeginVisualReview(profession, 0); app.VisualReviewPaused = false; var g = app.Game;
        // Stage mature ownership and level only. New slots, purchases and all
        // mutations under test must be earned through displayed controls.
        foreach (string flag in new[] { "core_1", "weapon_1", "weapon_2", "relic_0", "relic_3", "companion_0" })
            if (!g.Has(flag)) g.save.flags.Add(flag);
        g.save.flags.Remove("technique_" + profession); g.save.technique = 0;
        g.enemies.Clear(); g.save.level = 5; g.save.skillPoints = 3; g.save.companion = 0;
        g.save.relics = new[] { -1, -1 }; g.save.weapon = 0; g.save.weaponStyles = new int[3];
        g.save.battlePlans = new BattlePlan[3]; g.save.health = 58; g.save.energy = 47;
        g.save.coins = 313; g.save.shards = 17; g.save.xp = 9; g.save.talents = new[] { 1, 2, 1 };
        g.save.english = english; g.save.largeText = large; g.save.reducedMotion = true; g.save.effectsQuality = 0;
        Set(app, "applicationPaused", false); Set(app, "pointerSeen", false); Set(app, "controllerUsed", false);
        Set(app, "combatHudTime", 0f); Set(app, "resumeTime", 0f); Set(app, "selectedBattlePlan", 0); Call(app, "Resize");
    }
    private static void OpenPlans(FrontierRuntime app, bool focus = false)
    {
        if (app.Game.mode != "inventory") Key(app, KeyCode.I);
        Check(app.Game.mode == "inventory", "Actual I opens the mature inventory");
        if (focus) FocusActivate(app, L(app, "装备方案", "Battle plans")); else Click(app, L(app, "装备方案", "Battle plans"));
        Check(app.Game.mode == "plans", "The actual inventory entry opens plans");
    }
    private static void Select(FrontierRuntime app, int slot)
    {
        Draw(app); Rect row = new Rect(295, 192 + slot * 148, 270, 38);
        Check(GUI.buttonCalls.Exists(b => EqualRect(b.rect, row) && b.enabled), "The actual three-slot selector exposes slot " + (slot + 1));
        Click(app, row);
    }
    private static void SaveCurrent(FrontierRuntime app) { Click(app, L(app, "记下当前装备", "Store current gear")); }
    private static void Apply(FrontierRuntime app, bool pad = false) { FocusActivate(app, L(app, "应用方案", "Apply plan"), pad); }
    private static void Configure(FrontierRuntime app, int weapon, int style, bool alternate, bool relics)
    {
        Key(app, KeyCode.I); Check(app.Game.mode == "inventory", "The mature I inventory remains accessible while preparing");
        Click(app, L(app, new[] { "短剑", "猎弓", "星火杖" }[weapon], new[] { "Sword", "Bow", "Starfire staff" }[weapon]));
        Click(app, L(app, new[] { "均衡", "蓄势", "流转" }[style], new[] { "Balanced", "Focused", "Flow" }[style]));
        Click(app, L(app, "装备此打法", "Equip style"));
        Check(app.Game.save.weapon == weapon && app.Game.Style == style, "Actual old inventory controls equip the selected weapon and style");
        Key(app, KeyCode.K); bool learned = app.Game.Has("technique_" + app.Game.save.profession);
        if (app.Game.save.technique != (alternate ? 1 : 0))
        {
            int points = app.Game.save.skillPoints;
            Click(app, learned ? L(app, "切换为另一种职业战技", "Switch to the other class technique") : L(app, "学习第二战技：等级5、技能点1", "Learn alternate technique: level 5, 1 talent point"));
            Check(app.Game.save.technique == (alternate ? 1 : 0) && app.Game.save.skillPoints == points - (learned ? 0 : 1),
                "The original technique action retains exactly its once-only point purchase and free learned switch");
        }
        Key(app, KeyCode.G);
        for (int slot = 0; slot < 2; slot++)
        {
            int id = app.Game.save.relics[slot];
            Click(app, L(app, "槽 ", "Slot ") + (slot + 1) + " · " + (id < 0 ? L(app, "空", "Empty") : L(app, GameCore.RelicNames[id], GameCore.RelicNamesEn[id])));
            if (relics)
            {
                int want = slot == 0 ? 0 : 3; Rect equip = new Rect(296 + want % 3 * 318, 364 + want / 3 * 174, 268, 27);
                Draw(app); Check(GUI.buttonCalls.Exists(b => EqualRect(b.rect, equip) && b.enabled), "The mature owned relic control is present"); Click(app, equip);
            }
            else Click(app, L(app, "卸下当前槽", "Clear selected slot"));
        }
        Check(app.Game.save.relics[0] == (relics ? 0 : -1) && app.Game.save.relics[1] == (relics ? 3 : -1), "Actual mature two-slot equipment makes the requested configuration");
        Key(app, KeyCode.Escape); Check(app.Game.mode == "world", "Actual Escape exits equipment without forced travel");
    }
    private static void Config(GameCore g, int weapon, int style, int technique, int first, int second)
    { Check(g.save.weapon == weapon && g.Style == style && g.save.technique == technique && g.save.relics[0] == first && g.save.relics[1] == second, "Applying equips exactly the recorded weapon, its style, learned Q and ordered relic pair"); }
    private static string Economics(GameCore g)
    { return g.save.coins + "/" + g.save.shards + "/" + g.save.xp + "/" + g.save.skillPoints + "/" + JsonUtility.ToJson(g.save.talents) + "/" + JsonUtility.ToJson(g.save.inventory) + "/" + JsonUtility.ToJson(g.save.mastery) + "/" + JsonUtility.ToJson(g.save.kills) + "/" + g.save.companion + "/" + g.save.profession; }

    public static void PlansActualInventoryFocusConfirmationAndIsolation(FrontierRuntime app)
    {
        Prepare(app, 1); var g = app.Game; Configure(app, 1, 1, true, true); OpenPlans(app, true); SaveCurrent(app);
        Config(g, 1, 1, 1, 0, 3); Check(g.save.battlePlans.Length == 3 && g.save.battlePlans[0] != null && g.save.battlePlans[0].recorded && g.save.battlePlans[1] == null && g.save.battlePlans[2] == null,
            "Actual save records one independent plan without filling or equipping the other two slots");
        Draw(app, "first-recorded"); string recorded = JsonUtility.ToJson(g.save.battlePlans[0]);
        Button(app, L(app, "当前方案", "Current plan"), false); int revision = g.revision;
        Check(!g.ApplyBattlePlan(0) && g.revision == revision, "An already-current plan is a truthful disabled no-op, not an equipment mutation");
        Key(app, KeyCode.Escape); Check(g.mode == "inventory", "Plans Escape returns to inventory rather than silently resuming battle");
        Key(app, KeyCode.Escape); Configure(app, 0, 0, false, false);
        Check(JsonUtility.ToJson(g.save.battlePlans[0]) == recorded, "Changing live equipment cannot mutate the saved independent plan");
        OpenPlans(app); string economy = Economics(g); float health = g.save.health, energy = g.save.energy, time = g.save.totalTime;
        g.attackCooldown = .8f; g.dashCooldown = .5f; g.skillCooldown = 2.4f; g.companionCooldown = 9; g.guardTime = 1.2f;
        var shot = new Projectile { position = g.Position, velocity = new Point(1, 0), damage = 17, friendly = true, life = 3 };
        g.projectiles.Add(shot); Frames(app, 12);
        Check(g.save.totalTime == time && g.save.health == health && g.save.energy == energy && g.projectiles.Contains(shot) && shot.life == 3,
            "Reading plans pauses world resources and existing projectiles instead of simulating combat under a menu");
        Apply(app, true); Config(g, 1, 1, 1, 0, 3);
        Check(Economics(g) == economy && g.save.health == health && g.save.energy == energy && g.attackCooldown == .8f && g.dashCooldown == .5f
            && g.skillCooldown == 2.4f && g.companionCooldown == 9 && g.guardTime == 1.2f && g.projectiles.Contains(shot) && shot.life == 3,
            "Real A focus activation swaps only configuration without healing, refreshing cooldowns, paying again or retiring an existing shot");
        SaveCurrent(app); Draw(app, "overwrite-confirm"); Check(g.save.battlePlans[0] != null, "Requesting overwrite does not delete the existing slot");
        Check(Get<List<FocusControl>>(app, "drawnControls").Count == 2, "Only the modal's two actual controls can receive focus while overwrite is pending");
        Key(app, KeyCode.JoystickButton5); Draw(app);
        Check(g.mode == "plans" && Get<int>(app, "battlePlanConfirmation") == 0 && JsonUtility.ToJson(g.save.battlePlans[0]) == recorded,
            "Actual RB cannot jump to a background menu or change a pending plan");
        Key(app, KeyCode.Escape); Check(g.mode == "plans" && JsonUtility.ToJson(g.save.battlePlans[0]) == recorded, "Escape cancels the overwrite modal only");
        Key(app, KeyCode.Escape); Key(app, KeyCode.Escape); Configure(app, 0, 0, false, false); OpenPlans(app);
        SaveCurrent(app); FocusActivate(app, L(app, "确认", "Confirm")); Draw(app, "overwrite-committed");
        Check(g.save.battlePlans[0].weapon == 0 && g.save.battlePlans[0].style == 0 && g.save.battlePlans[0].technique == 0
            && g.save.battlePlans[0].relicFirst == -1 && g.save.battlePlans[0].relicSecond == -1 && Economics(g) == economy,
            "Actual confirmed overwrite stores the genuinely changed current gear without repaying talents, technique or other rewards");
        Click(app, L(app, "清除方案", "Clear plan")); Draw(app, "delete-confirm"); Check(g.save.battlePlans[0] != null, "Delete is reviewable before mutation");
        Click(app, L(app, "取消", "Cancel")); Check(g.save.battlePlans[0] != null, "Actual Cancel retains the saved plan");
        Click(app, L(app, "清除方案", "Clear plan")); FocusActivate(app, L(app, "确认", "Confirm"));
        Check(g.save.battlePlans[0] == null && Economics(g) == economy, "Only actual confirmed deletion clears the selected plan, without currency or progression changes");
        Button(app, L(app, "应用方案", "Apply plan"), false); Button(app, L(app, "清除方案", "Clear plan"), false);
        Console.WriteLine("PASS battle plans actual I entry, old equip/paid Q controls, Tab/Enter/A, independent slots, current no-op, overwrite/delete cancellation, confirmed deletion and resource/cooldown/projectile isolation; NATIVE_UNVERIFIED");
    }

    public static void PlansSaveStoreLegacyMalformedAndUnavailable(FrontierRuntime app)
    {
        Prepare(app, 2); var g = app.Game; Configure(app, 2, 2, true, true); OpenPlans(app); SaveCurrent(app); Select(app, 2); SaveCurrent(app);
        g.PrepareSave(); Check(SaveStore.Write(g.save, 0), "Actual checksum envelope writes three battle-plan positions"); string warning;
        SaveData loaded = SaveStore.Read(0, out warning); Check(loaded != null && warning == null && loaded.battlePlans[0] != null && loaded.battlePlans[0].recorded && loaded.battlePlans[1] == null && loaded.battlePlans[2] != null && loaded.battlePlans[2].recorded,
            "The actual JSON checksum reader retains explicitly recorded slots and the nullable middle position");
        string first = JsonUtility.ToJson(loaded.battlePlans[0]); g.Load(loaded); g.Start(); OpenPlans(app); Select(app, 0);
        Check(JsonUtility.ToJson(g.save.battlePlans[0]) == first, "Load sanitation retains valid paid equipment recipes");
        foreach (int version in new[] { 1, 2 })
        {
            SaveData old = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(loaded)); old.version = version; old.battlePlans = null;
            string json = JsonUtility.ToJson(old).Replace("\"battlePlans\":null,", "").Replace(",\"battlePlans\":null", "");
            Check(!json.Contains("battlePlans"), "The migration fixture genuinely lacks the new save field");
            File.WriteAllText(SaveStore.PathFor(1), json); SaveData prior = SaveStore.Read(1, out warning);
            Check(prior != null && warning == null, "The production legacy JSON reader accepts v" + version); g.Load(prior); g.Start();
            Check(g.save.battlePlans != null && g.save.battlePlans.Length == 3 && Array.TrueForAll(g.save.battlePlans, p => p == null),
                "A real pre-feature v" + version + " save gets three empty optional plans without inventing ownership or a loadout");
            Check(g.save.flags.Contains("core_1") && g.save.inventory.Length == 8 && g.save.weapon == 2 && g.save.technique == 1 && g.save.level == 5,
                "Migration preserves mature story, inventory, paid technique, level and equipment");
        }
        SaveData representation = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(loaded)); representation.battlePlans = null;
        string decodedCapture = JsonUtility.ToJson(loaded.battlePlans[2]);
        Check(decodedCapture.Contains("\"recorded\":true"), "Only the actual captured slot carries the explicit recording marker in persisted JSON");
        string rawSlots = "\"battlePlans\":[{},{\"weapon\":0,\"style\":0,\"technique\":0,\"relicFirst\":-1,\"relicSecond\":-1}," + decodedCapture + "]";
        string raw = JsonUtility.ToJson(representation).Replace("\"battlePlans\":null", rawSlots);
        File.WriteAllText(SaveStore.PathFor(1), raw); SaveData represented = SaveStore.Read(1, out warning);
        Check(represented != null && warning == null && represented.battlePlans.Length == 3 && !represented.battlePlans[0].recorded
            && !represented.battlePlans[1].recorded && represented.battlePlans[2].recorded,
            "The real raw-JSON decoder distinguishes default objects and apparently valid unrecorded gear from the actual captured third slot");
        g.Load(represented); g.Start(); Check(g.save.battlePlans.Length == 3 && g.save.battlePlans[0] == null && g.save.battlePlans[1] == null
            && g.save.battlePlans[2] != null && g.save.battlePlans[2].recorded && JsonUtility.ToJson(g.save.battlePlans[2]) == decodedCapture,
            "Default inline-object representations remain empty after Load; only the explicitly captured third plan survives unchanged");
        Check(g.save.weapon == loaded.weapon && g.save.technique == loaded.technique && g.save.level == loaded.level && g.save.flags.Contains("core_1"),
            "Discarding unrecorded default plans preserves the mature live gear, level and story");
        loaded.battlePlans = new[] { new BattlePlan { recorded = true, weapon = 2, style = 2, technique = 1, relicFirst = 0, relicSecond = 3 },
            new BattlePlan { recorded = true, weapon = 1, style = 99, technique = 0, relicFirst = -1, relicSecond = -1 },
            new BattlePlan { recorded = true, weapon = 0, style = 0, technique = 0, relicFirst = 3, relicSecond = 3 }, new BattlePlan { recorded = true, weapon = 0 } };
        Check(SaveStore.Write(loaded, 2), "Structurally malformed optional records still use a valid real save envelope");
        SaveData malformed = SaveStore.Read(2, out warning); Check(malformed != null && warning == null, "Bad optional plans do not falsely corrupt the entire valid mature save");
        g.Load(malformed); g.Start(); Check(g.save.battlePlans.Length == 3 && g.save.battlePlans[0] != null && g.save.battlePlans[1] == null && g.save.battlePlans[2] == null,
            "Sanitation bounds three slots and removes an invalid style and duplicate relic record without replacing the valid first recipe");
        Configure(app, 0, 0, false, false);
        g.save.flags.Remove("weapon_2"); OpenPlans(app); Select(app, 0); Button(app, L(app, "应用方案", "Apply plan"), false);
        int weapon = g.save.weapon, technique = g.save.technique; string economy = Economics(g);
        Check(!g.ApplyBattlePlan(0) && g.save.weapon == weapon && g.save.technique == technique && Economics(g) == economy,
            "A presently unowned weapon disables the actual plan and cannot partially spend or apply its technique/relics");
        Draw(app, "unavailable-ownership");
        Check(SaveStore.Write(loaded, 0), "A subsequent normal write creates the original valid backup");
        string corrupted = File.ReadAllText(SaveStore.PathFor(0)).Replace("\"checksum\":\"", "\"checksum\":\"BAD"); File.WriteAllText(SaveStore.PathFor(0), corrupted);
        SaveData recovered = SaveStore.Read(0, out warning);
        Check(recovered != null && warning != null && warning.Contains("backup") && recovered.battlePlans[0] != null,
            "Actual checksum rejection recovers the prior independent battle-plan save through the mature backup path");
        Console.WriteLine("PASS battle plans actual JSON/checksum roundtrip, pre-feature v1/v2 missing-field migration, unrecorded default inline objects staying empty, explicitly recorded capture retention, bounded malformed optional records, atomic unavailable ownership rejection and valid backup recovery; NATIVE_UNVERIFIED");
    }

    private static void Position(FrontierRuntime app, Point point)
    { Check(app.SetVisualReviewPosition(point), "The staged input setup uses actual walkable ground"); Set(app, "camera", app.Game.Position); }
    private static void Mouse(FrontierRuntime app, Point point)
    {
        Set(app, "camera", app.Game.Position); Point screen = (Point)Call(app, "ScreenPoint", point);
        Input.mousePosition = new Vector3(Get<float>(app, "offsetX") + screen.x * Get<float>(app, "scale"), Screen.height - Get<float>(app, "offsetY") - screen.y * Get<float>(app, "scale"), 0);
    }
    private static void Choose(FrontierRuntime app, string action)
    {
        var g = app.Game; int index = Array.FindIndex(g.conversation.choices, c => c.action == action);
        Check(g.mode == "dialog" && index >= 0 && index < 9, "The mentor's actual dialogue exposes " + action);
        for (int i = 0; i < 20; i++) { Draw(app); if ((bool)Call(app, "DialogueChoicesVisible")) break; Key(app, KeyCode.Return); }
        Check((bool)Call(app, "DialogueChoicesVisible"), "The actual mentor pages finish before the numbered action");
        Key(app, (KeyCode)((int)KeyCode.Alpha1 + index));
    }
    public static void PlansRealSparringContactsAndRetainedEquipment(FrontierRuntime app)
    {
        Prepare(app, 1); var g = app.Game; Configure(app, 1, 1, true, true); OpenPlans(app); Select(app, 1); SaveCurrent(app);
        Key(app, KeyCode.Escape); Key(app, KeyCode.Escape); Configure(app, 0, 0, false, false);
        WorldObject mentor = Array.Find(g.Map.objects, o => o.kind == "mentor"); Check(mentor != null, "Ada remains in the original town");
        Position(app, new Point(mentor.x, mentor.y - .7f)); Key(app, KeyCode.E); Choose(app, "sparring_menu"); Choose(app, "sparring_start");
        float returnHealth = g.save.sparringReturn.health, returnEnergy = g.save.sparringReturn.energy;
        Check(g.SparringActive && g.enemies.Count == 1 && g.enemies[0].id == 75000, "Actual E/Alpha3 starts Ada's mature first drill");
        OpenPlans(app); Select(app, 1); float time = g.SparringTime, health = g.save.health, energy = g.save.energy; string economy = Economics(g);
        Frames(app, 15); Check(g.SparringTime == time && g.save.health == health && g.save.energy == energy, "Reading a battle plan does not advance Ada's fight or refill resources");
        Apply(app); Config(g, 1, 1, 1, 0, 3); Key(app, KeyCode.Escape); Key(app, KeyCode.Escape);
        Check(g.mode == "world" && g.SparringActive && g.enemies.Count == 1 && Economics(g) == economy, "Preparation changes the chosen configuration while preserving the real paused drill and all reward balances");
        Enemy hunter = g.enemies[0]; Position(app, new Point(13, 13)); Mouse(app, hunter.Position); float hp = hunter.hp;
        Input.held.Add(KeyCode.J); Update(app); Input.held.Clear(); ActorAction attack = g.playerAction;
        Check(attack != null && attack.kind == "attack" && attack.weapon == 1 && attack.style == 1, "Actual J captures the plan's focused bow rather than the prior sword");
        for (int i = 0; i < 80 && hunter.hp >= hp; i++) Update(app);
        Check(hunter.hp < hp && !g.SparringAwaitingNext, "The real bow arrow contacts the first hunter through the ordinary projectile path");
        for (int i = 0; i < 80 && g.playerAction != null; i++) Update(app);
        Mouse(app, hunter.Position); Key(app, KeyCode.Q); ActorAction skill = g.playerAction;
        Check(skill != null && skill.kind == "skill" && skill.profession == 1 && skill.technique == 1 && skill.style == 1 && g.skillCooldown > 0 && g.save.energy < 100,
            "Actual Q captures the selected learned Woodpiercer and pays the original cooldown and energy cost");
        for (int i = 0; i < 120 && !g.SparringAwaitingNext; i++) Update(app);
        Check(g.SparringAwaitingNext && g.SparringStage == 0 && !g.SparringFinished && g.enemies.Count == 0 && Economics(g) == economy,
            "Real J and Q clear only the first mature drill, pause manually and cannot award a full course or farm loot, XP, mastery, kills or currency");
        Draw(app, "ada-first-drill-result"); Click(app, L(app, "返回训练场", "Return to mentor"));
        Check(!g.SparringActive && !g.practicing && g.mode == "world", "The real result safe-exit button ends the original practice");
        Config(g, 1, 1, 1, 0, 3); Check(Economics(g) == economy && g.save.health == returnHealth && g.save.energy == returnEnergy && g.save.battlePlans[1] != null,
            "Ada's original return restores pre-drill resources while retaining the explicitly chosen equipment and its saved recipe, without an extra reward");
        Console.WriteLine("PASS battle plans actual Ada E/Alpha3 preparation, paused plan switch, real focused-bow J and learned Woodpiercer Q contacts, manual first-drill result, no farming and safe exit retaining gear; NATIVE_UNVERIFIED");
    }

    public static void PlansBilingualThreeProfessionsAndTechniqueGuide(FrontierRuntime app)
    {
        for (int profession = 0; profession < 3; profession++) for (int language = 0; language < 2; language++) for (int policy = 0; policy < 2; policy++)
        {
            Prepare(app, profession, language == 1, true); var g = app.Game; Screen.width = policy == 0 ? 960 : 1920; Screen.height = policy == 0 ? 600 : 1080;
            g.save.effectsQuality = policy == 0 ? 0 : 2; g.save.reducedMotion = policy == 0; Call(app, "Resize");
            string suffix = profession + (language == 1 ? "-en" : "-zh") + (policy == 0 ? "-large960" : "-large1920");
            OpenPlans(app); Draw(app, "empty-" + suffix); SaveCurrent(app); Select(app, 1); SaveCurrent(app); Select(app, 2); SaveCurrent(app);
            Draw(app, "three-slots-" + suffix); Check(g.save.battlePlans.Length == 3 && Array.TrueForAll(g.save.battlePlans, p => p != null), "Every profession can actually record all three nullable positions");
            SaveCurrent(app); Draw(app, "overwrite-confirm-" + suffix); Key(app, KeyCode.Escape);
            Click(app, L(app, "清除方案", "Clear plan")); Draw(app, "delete-confirm-" + suffix); Click(app, L(app, "取消", "Cancel"));
            Check(g.mode == "plans" && g.save.battlePlans[2] != null, "Both actual large-text confirmations can be read and canceled without leaving the plans screen or clearing its selected slot");
            float time = g.save.totalTime, health = g.save.health, energy = g.save.energy; int points = g.save.skillPoints;
            Click(app, L(app, "战技效果与用途", "Technique field notes")); Check(g.mode == "techniques", "The actual plan link exposes the profession's Q choice without secretly learning it");
            Draw(app, "techniques-unlearned-" + suffix); Check(g.save.skillPoints == points && g.save.technique == 0 && !g.Has("technique_" + profession), "Reading the guide cannot buy or equip an unlearned technique");
            Check(HasText(g.ClassTechniqueName(0)) && HasText(g.ClassTechniqueName(1)) && HasText(g.ClassTechniqueDescription(0)) && HasText(g.ClassTechniqueDescription(1)),
                "Both actual GUI cards show the current profession's distinct authored names and effect/tradeoff descriptions");
            g.save.level = 4; Draw(app, "techniques-level-locked-" + suffix);
            Button(app, L(app, "学习第二战技", "Learn alternate technique"), false);
            g.save.level = 5; g.save.skillPoints = 0; Draw(app, "techniques-point-locked-" + suffix);
            Button(app, L(app, "学习第二战技", "Learn alternate technique"), false);
            Check(g.save.technique == 0 && !g.Has("technique_" + profession), "Both real disabled guide prerequisites retain the original base technique and cannot invent a purchase");
            g.save.skillPoints = points;
            Click(app, L(app, "学习第二战技", "Learn alternate technique"));
            Check(g.Has("technique_" + profession) && g.save.technique == 1 && g.save.skillPoints == points - 1,
                "Actual alternate-technique guide purchase uses the mature one-time level-five point rule for all three professions");
            Draw(app, "techniques-learned-" + suffix); Click(app, L(app, "使用这项战技", "Use this technique"));
            Check(g.save.technique == 0 && g.save.skillPoints == points - 1, "Actual base selection is free after the original paid unlock");
            Click(app, L(app, "使用这项战技", "Use this technique")); Check(g.save.technique == 1 && g.save.skillPoints == points - 1, "Actual learned alternate selection is free and cannot charge another point");
            Frames(app, 15); Check(g.save.totalTime == time && g.save.health == health && g.save.energy == energy, "All new guide/menu screens freeze the ordinary world resource clock");
            Draw(app, "techniques-equipped-" + suffix); Key(app, KeyCode.Escape);
            Check(g.mode == "plans", "Escape from the linked guide restores its plans parent");
            Key(app, KeyCode.Escape); Check(g.mode == "inventory", "Escape from plans retains its inventory parent");
            Key(app, KeyCode.K); Click(app, L(app, "战技效果与用途", "Technique field notes")); Check(g.mode == "techniques", "The original K page has its explicit guide link without losing the old learn button");
            Key(app, KeyCode.JoystickButton1); Check(g.mode == "talents", "Actual pad B from the talent guide returns to talents");
        }
        Console.WriteLine("PASS battle plans all three professions, both languages and large text at 960/1920, three actual slots, truthful locked/learned Q guide, original purchase/free-switch costs, paused clocks and keyboard/pad parent return; NATIVE_UNVERIFIED");
    }

    public static int Main(string[] args)
    {
        string previous = Application.persistentDataPath, directory = Path.Combine(Path.GetTempPath(), "starfall-battle-plan-check-" + Guid.NewGuid());
        Application.persistentDataPath = directory; captureFolder = args.Length > 0 ? args[0] : null;
        if (captureFolder != null) Directory.CreateDirectory(captureFolder); var app = new FrontierRuntime(); int failed = 0;
        try
        {
            Call(app, "Awake"); Check(app.Game != null, "Actual Awake loads the mature full campaign");
            foreach (Action<FrontierRuntime> test in new Action<FrontierRuntime>[] { PlansActualInventoryFocusConfirmationAndIsolation, PlansSaveStoreLegacyMalformedAndUnavailable,
                PlansRealSparringContactsAndRetainedEquipment, PlansBilingualThreeProfessionsAndTechniqueGuide })
                try { test(app); } catch (Exception error) { failed++; Console.WriteLine("FAIL " + test.Method.Name + ": " + (error.InnerException ?? error).Message); }
            app.EndVisualReview(); Call(app, "OnDestroy");
        }
        finally { Application.persistentDataPath = previous; Input.Clear(); GUI.queuedClick = null; if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        Console.WriteLine("Runtime battle plans: " + (4 - failed) + "/4 groups passed; NATIVE_UNVERIFIED"); return failed == 0 ? 0 : 1;
    }
}
