// Real Update, OnGUI and SaveStore paths against offline Unity declarations.
// Native import, rendering, hardware input and play feel are NATIVE_UNVERIFIED.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeRangeChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string captureFolder;
    private static readonly Rect Reset = new Rect(360, 193, 268, 34), End = new Rect(640, 193, 280, 34);
    private static Rect Layout(int index) { return new Rect(360 + index * 188, 150, 176, 31); }
    private static void Check(bool value, string text) { if (!value) throw new Exception(text); }
    private static object Call(FrontierRuntime app, string method, params object[] args)
    { return typeof(FrontierRuntime).GetMethod(method, Private).Invoke(app, args); }
    private static void Set(FrontierRuntime app, string field, object value) { typeof(FrontierRuntime).GetField(field, Private).SetValue(app, value); }
    private static T Get<T>(FrontierRuntime app, string field) { return (T)typeof(FrontierRuntime).GetField(field, Private).GetValue(app); }
    private static void Update(FrontierRuntime app) { Call(app, "Update"); Input.keys.Clear(); }
    private static void Frames(FrontierRuntime app, int count) { for (int i = 0; i < count; i++) Update(app); }
    private static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key); Update(app); }
    private static bool Same(Rect a, Rect b) { return a.x == b.x && a.y == b.y && a.width == b.width && a.height == b.height; }
    private static bool Overlap(Rect a, Rect b) { return a.x < b.x + b.width && b.x < a.x + a.width && a.y < b.y + b.height && b.y < a.y + a.height; }
    private static void Draw(FrontierRuntime app, string capture = null)
    {
        GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app, "OnGUI");
        if (captureFolder != null && capture != null)
        {
            var texts = GUI.commands.ToArray(); var textures = GUI.textureCalls.ToArray(); var buttons = GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder, "qa-range-" + capture);
            GUI.commands.AddRange(texts); GUI.textureCalls.AddRange(textures); GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static bool Has(string text) { return GUI.commands.Exists(c => c.Contains("\"text\":" + GUI.S(text))); }
    private static Rect TextRect(string label)
    {
        foreach (string command in GUI.commands) if (command.Contains("\"text\":" + GUI.S(label)))
        {
            int start = command.IndexOf("\"r\":[", StringComparison.Ordinal) + 5, end = command.IndexOf(']', start);
            string[] p = command.Substring(start, end - start).Split(',');
            return new Rect(float.Parse(p[0], CultureInfo.InvariantCulture), float.Parse(p[1], CultureInfo.InvariantCulture),
                float.Parse(p[2], CultureInfo.InvariantCulture), float.Parse(p[3], CultureInfo.InvariantCulture));
        }
        throw new Exception("Actual GUI lacks " + label);
    }
    private static void Click(FrontierRuntime app, Rect rect)
    {
        Draw(app); Check(GUI.buttonCalls.Exists(b => Same(b.rect, rect) && b.enabled), "Displayed range/menu control is enabled at " + rect.x + "," + rect.y);
        GUI.queuedClick = rect; Draw(app); Check(!GUI.queuedClick.HasValue, "The actual displayed control consumes its click");
    }
    private static void Click(FrontierRuntime app, string label) { Draw(app); Click(app, TextRect(label)); }
    private static string L(FrontierRuntime app, string zh, string en) { return app.Game.L(zh, en); }
    private static void Position(FrontierRuntime app, Point point)
    {
        Check(app.Game.CanStand(point) && app.SetVisualReviewPosition(point) && (app.Game.Position - point).Length < .001f,
            "Input fixture uses exact accepted walkable feet " + point.x + "," + point.y);
        Set(app, "camera", app.Game.Position);
    }
    private static void Aim(FrontierRuntime app, Point target)
    {
        Set(app, "camera", app.Game.Position); Point screen = (Point)Call(app, "ScreenPoint", target); float scale = Get<float>(app, "scale");
        Input.mousePosition = new Vector3(Get<float>(app, "offsetX") + screen.x * scale, Screen.height - Get<float>(app, "offsetY") - screen.y * scale, 0);
    }
    private static void Pointer(FrontierRuntime app, Point point)
    {
        float scale = Get<float>(app, "scale"); Input.mousePosition = new Vector3(Get<float>(app, "offsetX") + point.x * scale,
            Screen.height - Get<float>(app, "offsetY") - point.y * scale, 0);
    }
    private static void Prepare(FrontierRuntime app, int profession = 0, bool english = true, bool large = false)
    {
        Input.Clear(); GUI.queuedClick = null; Screen.width = 1280; Screen.height = 800; Time.unscaledDeltaTime = .02f;
        app.BeginVisualReview(profession, 0); app.VisualReviewPaused = false; var g = app.Game; g.enemies.Clear();
        // Fixture represents earned gear and completed basic instruction. All
        // range entry, loadout controls, damage and resets use production paths.
        foreach (string flag in new[] { "core_1", "weapon_1", "weapon_2", "technique_" + profession, "companion_0", "fieldcraft_accepted", "range_runtime_mature_story" })
            if (!g.Has(flag)) g.save.flags.Add(flag);
        g.save.weapon = profession; g.save.weaponStyles = new int[3]; g.save.technique = 0; g.save.relics = new[] { -1, -1 };
        g.save.battlePlans = new BattlePlan[3]; g.save.level = 5; g.save.health = 58; g.save.energy = 42; g.save.heat = 33; g.save.cold = 27;
        g.save.english = english; g.save.largeText = large; g.save.effectsQuality = 0; g.save.reducedMotion = true;
        g.save.coins = 313; g.save.xp = 9; g.save.skillPoints = 2; g.save.shards = 17; g.save.tea = 4; g.save.inventory[6] = 3;
        g.save.companion = 0; g.save.totalTime = 100; g.save.foodUntil = 180; g.save.teaUntil = 165; g.save.mealKind = 3; g.save.mealUntil = 190;
        g.save.bounty = 0; g.save.bountyProgress = 0; g.save.seed = 1; g.save.practiceBits = 7;
        Set(app, "applicationPaused", false); Set(app, "pointerSeen", false); Set(app, "controllerUsed", false);
        Set(app, "combatHudTime", 0f); Set(app, "resumeTime", 0f); Set(app, "selectedBattlePlan", 0); Call(app, "Resize");
    }
    private static void Mentor(FrontierRuntime app, bool pad = false)
    {
        var g = app.Game; WorldObject mentor = Array.Find(g.Map.objects, o => o.kind == "mentor");
        Check(mentor != null, "Ada's original mentor object remains in the real town"); Position(app, new Point(mentor.x, mentor.y - .7f));
        Check(g.Nearest() == mentor, "Actual interaction targets Ada"); Key(app, pad ? KeyCode.JoystickButton1 : KeyCode.E);
        string[] old = { "practice", "close", "sparring_menu", "fieldcraft_offer" };
        Check(g.mode == "dialog" && g.conversation.choices.Length >= 5, "Ada exposes a real appended equipment-range choice");
        for (int i = 0; i < old.Length; i++) Check(g.conversation.choices[i].action == old[i], "Mature Ada choice retains its original number: " + old[i]);
        Check(g.conversation.choices[4].action == "range_menu", "Range is appended after the four mature choices");
    }
    private static void Choose(FrontierRuntime app, string action, string capture = null)
    {
        var g = app.Game; Check(g.mode == "dialog" && g.conversation != null, "Numbered action belongs to a real dialogue: " + action);
        int index = Array.FindIndex(g.conversation.choices, c => c.action == action); Check(index >= 0 && index < 9, "Actual authored numeric action exists: " + action);
        int page = 0;
        while (true)
        {
            Draw(app, capture == null ? null : capture + "-page" + page);
            if ((bool)Call(app, "DialogueChoicesVisible")) break;
            Key(app, KeyCode.Return); Check(++page < 30, "Actual dialogue completes before numeric activation");
        }
        Key(app, (KeyCode)((int)KeyCode.Alpha1 + index));
    }
    private static Point Start(FrontierRuntime app, int layout = 0, string capture = null, bool pad = false)
    {
        Mentor(app, pad); Point origin = app.Game.Position; Choose(app, "range_menu", capture == null ? null : capture + "-offer");
        Check(app.Game.mode == "dialog" && !app.Game.RangeActive, "Reading the range offer does not begin training");
        Choose(app, "range_start_" + layout, capture == null ? null : capture + "-instructions");
        var g = app.Game;
        Check(g.mode == "world" && g.RangeActive && g.SafeTrainingActive && g.practicing && !g.SparringActive && g.RangeLayout == layout,
            "Actual E/B and numbered instruction enter the selected safe equipment layout");
        Check(!g.RangeStarted && !g.RangeFinished && g.RangeTime == 0 && g.RangeDamage == 0 && g.RangeContacts == 0,
            "New target layout begins with truthful unstarted zero statistics");
        Set(app, "camera", g.Position); return origin;
    }
    private static void Equip(FrontierRuntime app, int weapon, int style)
    {
        Key(app, KeyCode.I); Check(app.Game.mode == "inventory", "Actual I opens original equipment while range remains active");
        Click(app, L(app, new[] { "短剑", "猎弓", "星火杖" }[weapon], new[] { "Sword", "Bow", "Starfire staff" }[weapon]));
        Click(app, L(app, new[] { "均衡", "蓄势", "流转" }[style], new[] { "Balanced", "Focused", "Flow" }[style]));
        Click(app, L(app, "装备此打法", "Equip style"));
        Check(app.Game.save.weapon == weapon && app.Game.Style == style, "Original inventory controls equip the selected earned weapon/style");
        Key(app, KeyCode.Escape); Check(app.Game.mode == "world", "Original Escape resumes the same world attempt");
    }
    private static void Technique(FrontierRuntime app, int technique)
    {
        Key(app, KeyCode.K); Check(app.Game.mode == "talents", "Original K opens class preparation");
        if (app.Game.save.technique != technique) Click(app, L(app, "切换为另一种职业战技", "Switch to the other class technique"));
        Check(app.Game.save.technique == technique, "Actual learned-technique control keeps its normal free switching"); Key(app, KeyCode.Escape);
    }
    private static ActorAction Attack(FrontierRuntime app, bool skill = false, bool pad = false)
    {
        if (skill) Key(app, pad ? KeyCode.JoystickButton3 : KeyCode.Q);
        else { Input.held.Add(pad ? KeyCode.JoystickButton2 : KeyCode.J); Update(app); Input.held.Clear(); }
        ActorAction action = app.Game.playerAction;
        Check(action != null && action.kind == (skill ? "skill" : "attack"), "Actual J/X or Q/Y creates the normal captured combat action"); return action;
    }
    private static void Settle(FrontierRuntime app, int frames = 125) { Frames(app, frames); Check(!app.Game.Dying, "Safe test input cannot create defeat"); }
    private static float Health(GameCore g) { float hp = 0; foreach (Enemy enemy in g.enemies) hp += Math.Max(0, enemy.hp); return hp; }
    private static string Progress(GameCore g)
    {
        var s = g.save; return s.coins + "/" + s.xp + "/" + s.shards + "/" + s.skillPoints + "/" + s.level + "/" + s.deaths + "/" +
            s.totalTime + "/" + s.foodUntil + "/" + s.teaUntil + "/" + s.mealUntil + "/" + s.mealKind + "/" + s.plantedAt + "/" +
            s.bounty + "/" + s.bountyProgress + "/" + s.bountyCycle + "/" + s.companion + "/" + s.profession + "/" + s.tutorialBits + "/" + s.practiceBits + "/" +
            JsonUtility.ToJson(s.inventory) + "/" + JsonUtility.ToJson(s.mastery) + "/" + JsonUtility.ToJson(s.kills) + "/" +
            JsonUtility.ToJson(s.flags) + "/" + JsonUtility.ToJson(s.moments);
    }
    private static void ResetStats(FrontierRuntime app, int layout)
    {
        if (app.Game.RangeLayout == layout) Click(app, Reset); else Click(app, Layout(layout));
        Check(app.Game.RangeLayout == layout && !app.Game.RangeStarted && !app.Game.RangeFinished && app.Game.RangeTime == 0 &&
            app.Game.RangeDamage == 0 && app.Game.RangeContacts == 0 && app.Game.RangeBreaks == 0 && app.Game.RangeFlanks == 0,
            "Deliberate actual layout/reset control starts a clean manual attempt");
    }

    public static void RangeMentorPreservesLessonsAndStationaryBodies(FrontierRuntime app)
    {
        Prepare(app); var g = app.Game; g.save.flags.Remove("core_1"); g.save.practiceBits = 0;
        Check(!g.RangeReady, "Early characters need original basic training or first core"); Mentor(app); Choose(app, "range_menu", "locked");
        Check(!g.RangeActive && !g.practicing && !g.StartRange(0), "Reading or stale callback cannot bypass a real locked range");
        g.Close(); Mentor(app); Choose(app, "practice");
        Check(g.practicing && !g.RangeActive && !g.SparringActive && g.enemies[0].id == 90001, "Original Ada Alpha1 still starts mature basics");
        g.EndPractice(); g.save.practiceBits = 7; Mentor(app); Choose(app, "sparring_menu"); Choose(app, "sparring_start");
        Check(g.SparringActive && !g.RangeActive && g.enemies.Count == 1 && g.enemies[0].id == 75000, "Original Ada sparring path and authored hunter remain available"); g.EndPractice();
        Point origin = Start(app, 0, "ready", true); float hp = g.save.health; Point[] bodies = g.enemies.ConvertAll(e => e.Position).ToArray();
        Frames(app, 150); Check(!g.RangeStarted && g.RangeTime == 0 && g.save.health == hp && g.projectiles.Count == 0,
            "Waiting before attack starts neither stopwatch nor incoming enemy attacks");
        for (int i = 0; i < bodies.Length; i++) Check((g.enemies[i].Position - bodies[i]).Length < .001f && g.enemies[i].state == 0 && !g.enemies[i].moving,
            "Real accepted enemy body stays at its exact station without chase, recoil or manufactured tells");
        Draw(app, "ready-stationary"); Texture2D monsters = Get<Texture2D>(app, "monsterSheet");
        Check(GUI.textureCalls.Exists(c => c.texture == monsters && Math.Abs(c.uv.y - 2f / 3) < .001f), "Hunters draw accepted hunter rows instead of basic heavy-dummy art");
        Key(app, KeyCode.E); Key(app, KeyCode.Return); Check(g.RangeActive && g.RangeLayout == 0 && !g.RangeStarted, "E/Enter cannot secretly reset, advance or exit the manual equipment trial");
        Click(app, End); Check(!g.RangeActive && !g.practicing && (g.Position - origin).Length < .001f, "Actual Return restores exact original mentor feet");
        Console.WriteLine("PASS range actual E/B and appended numeric entry, locked/basic/sparring preservation, normal hunter rendering, stationary harmless targets, unstarted clock and deliberate exit; NATIVE_UNVERIFIED");
    }

    public static void RangeActualWeaponsStylesAndSixClassTechniques(FrontierRuntime app)
    {
        for (int profession = 0; profession < 3; profession++)
        {
            Prepare(app, profession); var g = app.Game; Start(app); string progress = Progress(g); float safeHealth = g.save.health;
            for (int weapon = 0; weapon < 3; weapon++) for (int style = 0; style < 3; style++)
            {
                Equip(app, weapon, style); ResetStats(app, 0); Position(app, weapon == 0 ? new Point(15.15f, 14) : new Point(12, 14));
                Aim(app, g.enemies[0].Position); float hp = Health(g); int contacts = g.RangeContacts; ActorAction action = Attack(app, false, (profession + weapon + style) % 2 == 1);
                Check(action.weapon == weapon && action.style == style && g.RangeStarted && g.RangeContacts == contacts,
                    "Accepted real wind-up starts time with captured owned gear before any real impact");
                Settle(app); float loss = hp - Health(g);
                Check(loss > 0 && g.RangeContacts > contacts && Math.Abs(g.RangeDamage - loss) < .01f,
                    "All three classes and all nine weapon/style setups record actual accepted HP loss, including flying arrows and Starfire");
                foreach (Enemy e in g.enemies) Check(!e.moving && (e.Position - new Point(16 + (e.id % 10) * 2, 14)).Length < .001f,
                    "Normal accepted hit reactions do not move fixed range target feet");
            }
            Equip(app, profession, 0);
            for (int technique = 0; technique < 2; technique++)
            {
                Technique(app, technique); ResetStats(app, 1); Position(app, new Point(14.3f, 14)); Aim(app, new Point(16.5f, 14));
                float hp = Health(g); ActorAction action = Attack(app, true, profession == 1);
                Check(action.profession == profession && action.technique == technique && g.skillCooldown > 0 && g.save.energy < 100,
                    "Actual Q/Y preserves the earned class technique and its real energy/cooldown costs");
                Settle(app, 235); float loss = hp - Health(g);
                Check(loss > 0 && g.RangeContacts > 0 && Math.Abs(g.RangeDamage - loss) < .02f, "Both actual techniques for each class count only genuine direct, projectile or frost-pulse damage");
                if (profession == 2 && technique == 1) Check(g.RangeContacts >= 9, "Mage alternate's real repeated frost pulses contribute accepted separate contacts");
                Draw(app, "class-" + profession + "-technique-" + technique);
            }
            Key(app, KeyCode.F); Key(app, KeyCode.T); Key(app, KeyCode.Z);
            Check(Progress(g) == progress && g.save.health == safeHealth && g.companionAction == null && g.FieldcraftProgress == 0,
                "Actual repeated attacks/skills/supply/companion inputs cannot farm rewards, Fieldcraft or tutorial progress and cannot spend safe inventory");
            Click(app, End);
        }
        Console.WriteLine("PASS range actual 27 class/weapon/style J/X setups and six Q/Y techniques, real arrows/Starfire/frost and exact HP-loss statistics, fixed feet and campaign/progression/supply isolation; NATIVE_UNVERIFIED");
    }

    private static void RearSwordHit(FrontierRuntime app)
    {
        // The real runtime reads Unity's Horizontal axis, whose default positive
        // keyboard binding is D. Supply that actual input contract offline.
        Input.axes["Horizontal"] = 1;
        for (int frame = 0; frame < 30 && app.Game.Position.x < 16.85f; frame++) Update(app);
        Input.axes["Horizontal"] = 0;
        Check(app.Game.Position.x >= 16.85f && app.Game.Position.x < 17,
            "Actual movement restores sword reach behind the stationary heavy after each authored forward step: x=" + app.Game.Position.x);
        Aim(app, app.Game.enemies[0].Position); Attack(app); Settle(app, 40);
    }

    public static void RangeArmorOverkillManualClearAndStaleFlights(FrontierRuntime app)
    {
        Prepare(app); var g = app.Game; Start(app, 2); Equip(app, 0, 0); Position(app, new Point(15.15f, 14)); Aim(app, g.enemies[0].Position);
        Attack(app); Settle(app); float front = g.RangeDamage;
        Check(front > 0 && g.RangeFlanks == 0, "Stationary west-facing heavy retains its actual front armor rule");
        ResetStats(app, 2); Position(app, new Point(16.85f, 14)); Aim(app, g.enemies[0].Position); Attack(app); Settle(app);
        Check(g.RangeDamage > front * 2 && g.RangeFlanks == 1, "Real sword from behind deals normal larger loss and records one true flank");
        for (int swing = 0; swing < 12 && g.RangeBreaks == 0 && !g.RangeFinished; swing++)
        { RearSwordHit(app); }
        Check(g.RangeBreaks > 0, "Repeated real hits cross the normal stagger threshold and record an actual guard break");
        // Every sword step advances the player. Retreat using actual D input
        // before the next strike rather than attacking through the target at
        // point-blank range and incorrectly expecting that miss to score.
        // Only real attacks deal damage; overkill must cap at authored HP.
        for (int swing = 0; swing < 80 && !g.RangeFinished; swing++)
        { RearSwordHit(app); }
        Check(g.RangeFinished && g.enemies.Count == 0 && Math.Abs(g.RangeDamage - 300) < .01f, "Actual final kill caps accepted aggregate loss at exactly one 300-HP heavy; finished=" + g.RangeFinished + ", remaining=" + g.enemies.Count + ", damage=" + g.RangeDamage + ", health=" + Health(g));
        float time = g.RangeTime, damage = g.RangeDamage; int contacts = g.RangeContacts; Frames(app, 80); Key(app, KeyCode.E); Key(app, KeyCode.Return);
        Check(g.RangeFinished && g.RangeTime == time && g.RangeDamage == damage && g.RangeContacts == contacts && g.enemies.Count == 0,
            "Clear freezes results and E/Enter cannot auto-respawn targets or create new attempt damage"); Draw(app, "heavy-clear");
        ResetStats(app, 0); Equip(app, 1, 1); Position(app, new Point(12, 14)); Aim(app, g.enemies[0].Position); Attack(app);
        for (int i = 0; i < 25 && g.projectiles.Count == 0; i++) Update(app);
        Check(g.projectiles.Exists(p => p.friendly) && g.RangeContacts == 0, "Actual bow launches a shot which has not yet reached the first target");
        Click(app, Layout(1)); Check(g.projectiles.Count == 0 && g.playerAction == null, "Actual layout button retires in-flight arrow and action before new target bodies exist");
        Frames(app, 180); Check(!g.RangeStarted && g.RangeDamage == 0 && g.RangeContacts == 0 && Health(g) == 900, "Retired arrow cannot damage replacement layout or start its timer");
        Click(app, End);
        Prepare(app, 2); g = app.Game; Start(app, 1); Technique(app, 1); Position(app, new Point(14.3f, 14)); Aim(app, new Point(16, 14)); Attack(app, true);
        for (int i = 0; i < 40 && g.RangeContacts == 0; i++) Update(app);
        Check(g.RangeContacts > 0, "Real alternate Q established a live pulsing frost field before reset"); Click(app, Reset); Frames(app, 235);
        Check(g.RangeContacts == 0 && g.RangeDamage == 0 && !g.RangeStarted && Health(g) == 900 && g.projectiles.Count == 0 && g.hazards.Count == 0,
            "Manual reset clears persistent frost/contact state instead of letting old Q hit or score against fresh targets"); Click(app, End);
        Console.WriteLine("PASS range true front armor/rear flank/stagger, real 300HP overkill cap, manual clear results and stale arrow/frost retirement across layout/reset; NATIVE_UNVERIFIED");
    }

    public static void RangePausePlansAndChecksumReturnRecovery(FrontierRuntime app)
    {
        Prepare(app, 1); var g = app.Game; Point origin = Start(app); var record = g.save.sparringReturn;
        Check(record != null, "Range reuses the existing canonical practice return record");
        Equip(app, 1, 1); Position(app, new Point(12, 14)); Aim(app, g.enemies[0].Position); Attack(app); Settle(app, 80);
        float worldTime = g.save.totalTime, attempt = g.RangeTime; string progress = Progress(g); float health = g.save.health, energy = g.save.energy;
        Call(app, "OnApplicationPause", true); Frames(app, 35); Check(g.RangeTime == attempt && g.save.totalTime == worldTime, "Actual application pause freezes attempt and campaign time"); Call(app, "OnApplicationPause", false);
        Key(app, KeyCode.Escape); Check(g.mode == "pause" && g.RangeActive, "Actual Escape pauses rather than ending the live range"); attempt = g.RangeTime; Frames(app, 35);
        Check(g.RangeTime == attempt && Progress(g) == progress, "Actual pause menu advances no training timer or campaign preparations"); Draw(app, "pause"); Key(app, KeyCode.Escape);
        Key(app, KeyCode.I); Click(app, L(app, "装备方案", "Battle plans")); Check(g.mode == "plans", "Existing battle plans are usable during trial without new equipment UI");
        Click(app, L(app, "记下当前装备", "Store current gear")); Check(g.BattlePlanAt(0) != null && g.BattlePlanAt(0).recorded && g.BattlePlanAt(0).weapon == 1 && g.BattlePlanAt(0).style == 1,
            "Actual plan button records the tested earned bow setup while range remains live");
        attempt = g.RangeTime; Frames(app, 35); Check(g.RangeTime == attempt && g.save.health == health && g.save.energy == energy, "Inspecting plans freezes range/vitals instead of refilling energy");
        Key(app, KeyCode.Escape); Key(app, KeyCode.Escape); Equip(app, 0, 0); Key(app, KeyCode.I); Click(app, L(app, "装备方案", "Battle plans"));
        Click(app, L(app, "应用方案", "Apply plan")); Check(g.save.weapon == 1 && g.Style == 1 && g.RangeActive, "Actual apply restores the tried loadout without ending range or touching reward state");
        Key(app, KeyCode.Escape); Key(app, KeyCode.Escape); Check(Progress(g) == progress, "Plan save/apply preserves all non-equipment campaign progress");
        Point live = g.Position; int enemies = g.enemies.Count; g.PrepareSave();
        Check(g.RangeActive && g.enemies.Count == enemies && (g.Position - live).Length < .001f && g.save.snapshotRoom == -1 && g.save.worldEnemies == null,
            "Real PrepareSave excludes trial actors while leaving the live attempt and player position intact");
        Check(SaveStore.Write(g.save, 0), "Actual checksummed SaveStore writes the existing practice boundary"); string issue; SaveData loaded = SaveStore.Read(0, out issue);
        Check(loaded != null && issue == null && loaded.sparringReturn != null, "Actual envelope checksum retains canonical return record without a range save schema");
        g.Load(loaded); g.Start(); Check(!g.RangeActive && !g.practicing && g.enemies.Count == 0 && g.save.sparringReturn == null && (g.Position - origin).Length < .001f &&
            g.save.health == record.health && g.save.energy == record.energy && g.save.heat == record.heat && g.save.cold == record.cold && g.save.weapon == 1 && g.Style == 1 && g.BattlePlanAt(0) != null,
            "Load ends unsaved trial, restores original feet/vitals/climate, keeps tested gear and stored plan, and creates no phantom actors");
        Check(Progress(g) == progress, "Checksum roundtrip preserves mature story, inventory, teaching records and clocks"); Draw(app, "save-resume");
        foreach (int version in new[] { 1, 2 })
        {
            SaveData old = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(g.save)); old.version = version; old.sparringReturn = null;
            string json = JsonUtility.ToJson(old).Replace("\"sparringReturn\":null,", "").Replace(",\"sparringReturn\":null", "");
            Check(!json.Contains("sparringReturn"), "Old fixture actually omits optional return record"); File.WriteAllText(SaveStore.PathFor(1), json);
            loaded = SaveStore.Read(1, out issue); Check(loaded != null && issue == null, "Actual SaveStore reads raw mature version " + version); g.Load(loaded); g.Start();
            Check(!g.RangeActive && !g.practicing && g.Has("range_runtime_mature_story") && g.save.weapon == 1 && g.BattlePlanAt(0) != null,
                "Version " + version + " compatibility retains story and earned equipment without creating trial state");
        }
        Start(app); g.Enter(1); Check(g.save.room == 1 && !g.RangeActive && !g.practicing && g.save.sparringReturn == null && g.enemies.TrueForAll(e => e.id < 79000 || e.id > 79002),
            "Mature map travel exits the range before spawning only the original forest roster");
        Console.WriteLine("PASS range application/Escape/plans pause, actual plan save/apply, untouched clocks and economy, PrepareSave, checksum return recovery, v1/v2 omitted-field saves, retained gear and travel cleanup; NATIVE_UNVERIFIED");
    }

    public static void RangeBilingualControlsPointerPoliciesAndNotification(FrontierRuntime app)
    {
        for (int language = 0; language < 2; language++) for (int large = 0; large < 2; large++) for (int policy = 0; policy < 2; policy++)
        {
            Prepare(app, (language + large + policy) % 3, language == 1, large == 1); var g = app.Game;
            Screen.width = policy == 0 ? 960 : 1920; Screen.height = policy == 0 ? 600 : 1080; g.save.effectsQuality = policy == 0 ? 0 : 2; g.save.reducedMotion = policy == 0; Call(app, "Resize");
            string suffix = (language == 0 ? "zh" : "en") + (large == 0 ? "" : "-large") + (policy == 0 ? "-low960" : "-full1920");
            Start(app, 0, suffix); Draw(app, "ready-" + suffix);
            Check(Has(g.L("试装训练场 · ", "Loadout range · ") + g.RangeTitle) && Has(g.RangeHint), "Real compact range HUD renders its truthful current layout and advice");
            for (int layout = 0; layout < 3; layout++) Check(GUI.buttonCalls.Exists(b => Same(b.rect, Layout(layout)) && b.enabled == (layout != g.RangeLayout)), "All actual layout choices are present and current layout is an honest disabled no-op");
            Check(GUI.buttonCalls.Exists(b => Same(b.rect, Reset) && b.enabled) && GUI.buttonCalls.Exists(b => Same(b.rect, End) && b.enabled), "Explicit reset/return controls remain visible at every language/large-text/graphics policy");
            Check(!(bool)Call(app, "PointerOverInterface", new Point(400, 70)), "Read-only compact title/advice allow world aiming");
            foreach (Rect r in new[] { Layout(1), Layout(2), Reset, End })
            {
                Check((bool)Call(app, "PointerOverInterface", new Point(r.x + 8, r.y + 8)), "Every actual compact trial control protects real pointer input");
                Pointer(app, new Point(r.x + 8, r.y + 8)); Input.buttons.Add(0); Update(app); Input.buttons.Clear();
                Check(g.playerAction == null && !g.RangeStarted && g.RangeDamage == 0, "Actual held mouse attack over trial controls cannot fire through UI");
            }
            Click(app, Layout(1)); Check(g.RangeLayout == 1 && g.enemies.Count == 3, "Actual cluster control produces its three accepted bodies"); Draw(app, "cluster-" + suffix);
            Click(app, Layout(2)); Check(g.RangeLayout == 2 && g.enemies.Count == 1 && g.enemies[0].kind == 2, "Actual armor control produces the original ordinary Heavy type");
            g.Notify("试场记录保留在本次练习。", "Range feedback belongs to this attempt."); Draw(app, "heavy-toast-" + suffix);
            Rect toast = TextRect(L(app, "试场记录保留在本次练习。", "Range feedback belongs to this attempt."));
            foreach (Rect r in new[] { Layout(0), Layout(1), Layout(2), Reset, End }) Check(!Overlap(r, toast), "Real current notification cannot overlap or hide a manual layout/reset/return control");
            Click(app, Reset); Check(!g.RangeStarted && g.RangeDamage == 0 && Health(g) == 300, "Actual reset explicitly restores chosen heavy layout and clean stats");
            Click(app, End); Check(!g.RangeActive && g.mode == "world", "Actual End returns safely in all graphics/language policies");
        }
        Console.WriteLine("PASS range actual bilingual/large 960/1920 low/full HUD and controls, truthful disabled layout, protected pointer attacks, nonblocking read-only advice, notification separation and manual layout/reset/return; NATIVE_UNVERIFIED");
    }

    public static int Main(string[] args)
    {
        string previous = Application.persistentDataPath, directory = Path.Combine(Path.GetTempPath(), "starfall-range-check-" + Guid.NewGuid());
        Application.persistentDataPath = directory; captureFolder = args.Length > 0 ? args[0] : null; if (captureFolder != null) Directory.CreateDirectory(captureFolder);
        var app = new FrontierRuntime(); int failed = 0; Action<FrontierRuntime>[] tests = { RangeMentorPreservesLessonsAndStationaryBodies,
            RangeActualWeaponsStylesAndSixClassTechniques, RangeArmorOverkillManualClearAndStaleFlights, RangePausePlansAndChecksumReturnRecovery, RangeBilingualControlsPointerPoliciesAndNotification };
        try
        {
            Call(app, "Awake"); Check(app.Game != null, "Actual Awake loads the mature full campaign");
            foreach (Action<FrontierRuntime> test in tests)
                try { test(app); } catch (Exception error) { failed++; Console.WriteLine("FAIL " + test.Method.Name + ": " + (error.InnerException ?? error).Message); }
            app.EndVisualReview(); Call(app, "OnDestroy");
        }
        finally { Application.persistentDataPath = previous; Input.Clear(); GUI.queuedClick = null; if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        Console.WriteLine("Runtime equipment range: " + (tests.Length - failed) + "/" + tests.Length + " groups passed; NATIVE_UNVERIFIED"); return failed == 0 ? 0 : 1;
    }
}
