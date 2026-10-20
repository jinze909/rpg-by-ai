// Actual G, OnGUI, focus input and SaveStore using offline Unity declarations.
// Editor import, native font/GPU and physical-controller feel remain NATIVE_UNVERIFIED.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeRelicForgeChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Rect Forge = new Rect(597, 614, 620, 39);
    private static string captureFolder;
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static object Call(object obj, string name, params object[] args)
    { var m = obj.GetType().GetMethod(name, Private | BindingFlags.Public); Check(m != null, "Production method exists: " + name); return m.Invoke(obj, args); }
    private static void Set(FrontierRuntime app, string name, object value) { typeof(FrontierRuntime).GetField(name, Private).SetValue(app, value); }
    private static T Get<T>(object obj, string name) { return (T)obj.GetType().GetField(name, Private).GetValue(obj); }
    private static bool Same(Rect a, Rect b) { return a.x == b.x && a.y == b.y && a.width == b.width && a.height == b.height; }
    private static void Update(FrontierRuntime app) { Call(app, "Update"); Input.keys.Clear(); }
    private static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key); Update(app); }
    private static void Draw(FrontierRuntime app, string capture = null)
    {
        GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app, "OnGUI");
        if (captureFolder != null && capture != null)
        {
            var text = GUI.commands.ToArray(); var textures = GUI.textureCalls.ToArray(); var buttons = GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder, "qa-relic-forge-" + capture); GUI.commands.AddRange(text); GUI.textureCalls.AddRange(textures); GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static bool Text(string value) { return GUI.commands.Exists(c => c.Contains("\"text\":" + GUI.S(value))); }
    private static string Label(FrontierRuntime app)
    {
        var g = app.Game; int next = g.save.relicRank + 1, coins = g.save.projects[2] == 2 ? 0 : 20;
        return g.save.relicRank >= 3 ? g.L("记忆锻印 3/3 · 已满级", "Memory forging 3/3 · Maximum tier") :
            g.L("锻印 ", "Refine ") + next + "/3 · " + next * 8 + g.L("记忆碎片 / ", " memory shards / ") + "2" + g.L("星晶 / ", " crystals / ") + coins + g.L("硬币", " coins");
    }
    private static string ForgeId(FrontierRuntime app) { return (string)Call(app, "ControlId", Forge.x, Forge.y, Forge.width, Forge.height); }
    private static void ForgeState(FrontierRuntime app, bool enabled)
    {
        Draw(app); var button = GUI.buttonCalls.Find(b => Same(b.rect, Forge));
        Check(button != null && button.enabled == enabled, "The real G-page forge button is " + (enabled ? "enabled" : "disabled"));
        Check(Text(Label(app)), "The displayed full price exactly matches the current rank and workshop, including all currencies");
        Check(Get<MenuNavigation>(app, "navigation").CanActivate(ForgeId(app)) == enabled, "Actual focus navigation agrees with the displayed forge availability");
    }
    private static void Click(FrontierRuntime app, Rect rect)
    { GUI.queuedClick = rect; Draw(app); Check(!GUI.queuedClick.HasValue, "Enabled displayed control consumes the actual mouse click"); }
    private static void Activate(FrontierRuntime app, int input)
    {
        if (input == 0) { Click(app, Forge); return; }
        string id = ForgeId(app); var nav = Get<MenuNavigation>(app, "navigation"); int count = 0;
        while (nav.focused != id && count++ < 64) { Key(app, KeyCode.Tab); Draw(app); }
        Check(nav.focused == id && nav.CanActivate(id), "Actual Tab reaches the available forge");
        Key(app, input == 1 ? KeyCode.Return : KeyCode.JoystickButton0); Draw(app);
    }
    private static void Prepare(FrontierRuntime app, int workshop = 1, int rank = 0)
    {
        Input.Clear(); GUI.queuedClick = null; GUI.enabled = true; GUI.color = Color.white; GUI.matrix = Matrix4x4.identity;
        Screen.width = 1280; Screen.height = 800; Time.unscaledDeltaTime = .02f; Event.current.type = EventType.Repaint;
        app.BeginVisualReview(0, 0); app.VisualReviewPaused = false; var g = app.Game; g.enemies.Clear(); g.projectiles.Clear();
        g.save.english = true; g.save.largeText = false; g.save.showGuidance = false; g.save.reducedMotion = true; g.save.effectsQuality = 0;
        g.save.projects[2] = workshop; g.save.relicRank = rank; g.save.shards = 100; g.save.inventory[1] = 20; g.save.coins = 200;
        g.save.relics = new[] { -1, -1 }; g.save.companion = -1;
        Set(app, "applicationPaused", false); Set(app, "pointerSeen", false); Set(app, "controllerUsed", false); Set(app, "resumeTime", 0f); Set(app, "arrivalTime", 0f); Call(app, "Resize");
        Key(app, KeyCode.G); Check(g.mode == "loadout", "Actual G opens the mature relic/loadout page"); Draw(app);
    }
    private static object Quote(GameCore g)
    {
        // Reflection keeps the immutable pre-feature source compilable; absent
        // quote behavior is a semantic RED, never a missing-type compiler error.
        return Call(g, "GetRelicForgeQuote");
    }
    private static T Member<T>(object obj, string name)
    {
        var f = obj.GetType().GetField(name); if (f != null) return (T)f.GetValue(obj);
        var p = obj.GetType().GetProperty(name); Check(p != null, "Quote member exists: " + name); return (T)p.GetValue(obj, null);
    }
    private static void UnavailableMouseFocusAndTruthfulDeficits(FrontierRuntime app)
    {
        for (int mask = 1; mask < 8; mask++)
        {
            Prepare(app); var g = app.Game; g.save.shards = (mask & 1) != 0 ? 3 : 8; g.save.inventory[1] = (mask & 2) != 0 ? 1 : 2; g.save.coins = (mask & 4) != 0 ? 7 : 20;
            ForgeState(app, false); object q = Quote(g); string reason = Member<string>(q, "reasonEn");
            Check(reason.Contains("5 memory shards") == ((mask & 1) != 0) && reason.Contains("1 crystals") == ((mask & 2) != 0) && reason.Contains("13 coins") == ((mask & 4) != 0),
                "Every real shortage, and only its exact deficit, is listed together");
            Check(Text(reason), "The real page actually displays the quote's reason");
            string before = JsonUtility.ToJson(g.save); int revision = g.revision;
            GUI.queuedClick = Forge; Draw(app); Check(GUI.queuedClick.HasValue, "The disabled forge cannot consume a mouse click"); GUI.queuedClick = null;
            var nav = Get<MenuNavigation>(app, "navigation"); string id = ForgeId(app);
            foreach (KeyCode key in new[] { KeyCode.Return, KeyCode.JoystickButton0 }) { nav.focused = id; Key(app, key); Draw(app); }
            for (int i = 0; i < 24; i++) { Key(app, KeyCode.Tab); Draw(app); Check(nav.focused != id, "Tab skips disabled forge"); }
            Check(JsonUtility.ToJson(g.save) == before && g.revision == revision, "Disabled mouse, forged focus, Enter, pad A and Tab cannot charge any resource or rank");
        }
        foreach (string gate in new[] { "away", "unbuilt", "maximum" })
        {
            Prepare(app); var g = app.Game; if (gate == "away") g.save.room = 1; if (gate == "unbuilt") g.save.projects[2] = 0; if (gate == "maximum") g.save.relicRank = 3;
            ForgeState(app, false); var q = Quote(g); Check(!Member<bool>(q, "available") && Text(Member<string>(q, "reasonEn")), "Actual page explains the mature " + gate + " gate");
        }
        Console.WriteLine("PASS actual G-page exact all-resource deficits, town/workshop/cap reasons, disabled mouse/Enter/pad A, Tab exclusion and no-charge/save/revision isolation; NATIVE_UNVERIFIED");
    }
    private static void ExactPaidUpgradesAndConditionalDamage(FrontierRuntime app)
    {
        for (int workshop = 1; workshop <= 2; workshop++)
        {
            Prepare(app, workshop); var g = app.Game; if (workshop == 2) g.save.coins = 0;
            float bare = g.Damage; if (!g.Has("relic_0")) g.save.flags.Add("relic_0"); if (!g.Has("relic_2")) g.save.flags.Add("relic_2");
            for (int rank = 0; rank < 3; rank++)
            {
                ForgeState(app, true); int coins = g.save.coins, shards = g.save.shards, crystals = g.save.inventory[1]; float health = g.save.health, energy = g.save.energy;
                Activate(app, rank); Check(g.save.relicRank == rank + 1 && g.save.shards == shards - (rank + 1) * 8 && g.save.inventory[1] == crystals - 2 && g.save.coins == coins - (workshop == 2 ? 0 : 20),
                    "Actual mouse/Tab-Enter/Tab-pad purchase charges precisely one original upgrade at each tier");
                Check(g.save.health == health && g.save.energy == energy && g.mode == "loadout", "Forging neither heals nor changes menus");
                if (rank == 0) { Draw(app, "success-en-normal-workshop" + workshop); g.save.largeText = true; Screen.width = 960; Screen.height = 600; Call(app, "Resize"); Draw(app, "success-en-large-workshop" + workshop); g.save.largeText = false; Screen.width = 1280; Screen.height = 800; Call(app, "Resize"); }
                Check(Math.Abs(g.Damage - bare) < .001f, "An unequipped relic slot does not grant a forge bonus");
                g.EquipRelic(0, 0); Check(Math.Abs(g.Damage - bare - (rank + 1) * 2) < .001f, "Wearing a genuine relic activates the displayed flat base bonus");
                g.EquipRelic(1, 2); Check(Math.Abs(g.Damage - bare - (rank + 1) * 2) < .001f, "Two equipped slots do not double forging's global bonus"); g.EquipRelic(0, -1); g.EquipRelic(1, -1);
            }
            ForgeState(app, false); var q = Quote(g); Check(Member<int>(q, "nextRank") == 3 && Member<int>(q, "currentBonus") == 6 && Member<int>(q, "nextBonus") == 6,
                "Maximum rank offers no imaginary tier four or extra damage");
            string before = JsonUtility.ToJson(g.save); for (int i = 0; i < 10; i++) Draw(app); Check(before == JsonUtility.ToJson(g.save), "Repeated rendering is read-only");
        }
        Console.WriteLine("PASS both workshop prices and all three paid ranks through actual mouse/keyboard/pad controls, refreshed quote, no fourth tier and genuine equipped-only/nonstacking damage; NATIVE_UNVERIFIED");
    }
    private static void ActualLegacySaveStoreAndRangeGrowth(FrontierRuntime app)
    {
        for (int version = 1; version <= 2; version++)
        {
            Prepare(app, 2, 1); var g = app.Game; g.save.version = version; g.save.stage = 2; g.save.flags.Add("return_letter_complete"); g.save.inventory[3] = 7; g.save.kills[1] = 9;
            string raw = JsonUtility.ToJson(g.save); Check(!raw.Contains("reasonEn") && !raw.Contains("nextBonus"), "Quotes remain transient and add no save fields");
            File.WriteAllText(SaveStore.PathFor(1), raw); string warning; SaveData old = SaveStore.Read(1, out warning); Check(old != null && warning == null, "Actual SaveStore reads mature raw v" + version);
            g.Load(old); g.Start(); Key(app, KeyCode.G); Draw(app); Check(g.mode == "loadout", "G still reaches forging after legacy load"); int shards = g.save.shards, crystals = g.save.inventory[1], coins = g.save.coins;
            ForgeState(app, true); Activate(app, 0); Check(g.save.relicRank == 2 && g.save.shards == shards - 16 && g.save.inventory[1] == crystals - 2 && g.save.coins == coins, "Restored upgraded workshop purchases exact second rank");
            g.PrepareSave(); Check(SaveStore.Write(g.save, 0), "Actual checksum envelope writes upgraded relic state"); SaveData loaded = SaveStore.Read(0, out warning);
            Check(loaded != null && warning == null && loaded.relicRank == 2 && loaded.shards == shards - 16 && loaded.inventory[1] == crystals - 2 && loaded.coins == coins && loaded.flags.Contains("return_letter_complete") && loaded.stage == 2 && loaded.inventory[3] == 7 && loaded.kills[1] == 9,
                "Actual checksummed roundtrip retains payment, mature story, inventory and combat history from v" + version);
        }
        Prepare(app); var game = app.Game; Key(app, KeyCode.Escape); game.save.practiceBits = 7; WorldObject mentor = Array.Find(game.Map.objects, o => o.kind == "mentor"); Check(mentor != null, "Original Ada remains");
        game.save.x = mentor.x; game.save.y = mentor.y - .7f; game.save.health = 57; game.save.energy = 43;
        Check(game.StartRange(0) && game.RangeActive, "Actual original loadout range starts"); float health = game.save.sparringReturn.health, energy = game.save.sparringReturn.energy;
        Key(app, KeyCode.G); ForgeState(app, true); Activate(app, 0); Check(game.RangeActive && game.save.relicRank == 1, "Town forging retains the mature training growth policy instead of adding a new restriction");
        int remaining = game.save.shards; Key(app, KeyCode.Escape); game.EndPractice();
        Check(!game.RangeActive && game.save.relicRank == 1 && game.save.shards == remaining && game.save.health == health && game.save.energy == energy,
            "Real range return restores only safe-training resources while keeping the explicit paid forge growth");
        Console.WriteLine("PASS actual v1/v2 raw SaveStore plus checksummed write/read, preserved story/loot/currency/history, unchanged save schema and paid forge growth retained by the real safe range return; NATIVE_UNVERIFIED");
    }
    private static void BilingualNormalLargeAndReadOnlyRendering(FrontierRuntime app)
    {
        foreach (bool english in new[] { false, true }) foreach (bool large in new[] { false, true }) foreach (string state in new[] { "ready", "short", "upgraded", "maximum" })
        {
            Prepare(app, state == "upgraded" ? 2 : 1, state == "maximum" ? 3 : 0); var g = app.Game; g.save.english = english; g.save.largeText = large;
            Screen.width = large ? 960 : 1280; Screen.height = large ? 600 : 800; Call(app, "Resize"); if (state == "short") { g.save.shards = 0; g.save.inventory[1] = 0; g.save.coins = 0; }
            Draw(app); ForgeState(app, state != "short" && state != "maximum"); var q = Quote(g); string reason = Member<string>(q, english ? "reasonEn" : "reasonZh");
            if (state == "short" || state == "maximum") Check(Text(reason), "Both languages draw actual complete gate explanation");
            string bonus = g.L("装备遗物时基础攻击加成：+", "Base damage bonus while wearing a relic: +") + Member<int>(q, "currentBonus") + " → +" + Member<int>(q, "nextBonus") + g.L("（两槽不重复加成）。", " (once, across both slots).");
            Check(Text(bonus), "Both languages explicitly retain equipped-only and once-across-two-slots conditions");
            string before = JsonUtility.ToJson(g.save); int revision = g.revision; var art = Get<PixelArt>(app, "art"); int owned = Get<List<Texture2D>>(art, "owned").Count, cache = Get<Dictionary<string, Texture2D>>(art, "cache").Count;
            for (int i = 0; i < 12; i++) Draw(app);
            Check(before == JsonUtility.ToJson(g.save) && revision == g.revision && owned == Get<List<Texture2D>>(art, "owned").Count && cache == Get<Dictionary<string, Texture2D>>(art, "cache").Count,
                "Repeated read-only quote/UI rendering neither mutates the save nor allocates art textures");
            Draw(app, state + (english ? "-en" : "-zh") + (large ? "-large960" : "-normal1280"));
        }
        Console.WriteLine("PASS actual bilingual normal/large G-page captures with full prices/reasons and conditional bonus; repeated draws preserve all save state and art caches; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous = Application.persistentDataPath, directory = Path.Combine(Path.GetTempPath(), "starfall-relic-forge-" + Guid.NewGuid()); Application.persistentDataPath = directory;
        captureFolder = args.Length > 0 && args[0] != "-" ? args[0] : null; var app = new FrontierRuntime(); int failed = 0;
        try
        {
            Directory.CreateDirectory(directory); if (captureFolder != null) Directory.CreateDirectory(captureFolder); Call(app, "Awake"); Check(app.Game != null, "Actual Awake loads the mature campaign");
            foreach (Action<FrontierRuntime> test in new Action<FrontierRuntime>[] { UnavailableMouseFocusAndTruthfulDeficits, ExactPaidUpgradesAndConditionalDamage, ActualLegacySaveStoreAndRangeGrowth, BilingualNormalLargeAndReadOnlyRendering })
                try { test(app); } catch (Exception error) { failed++; Console.WriteLine("FAIL " + test.Method.Name + ": " + (error.InnerException ?? error)); }
            app.EndVisualReview(); Call(app, "OnDestroy");
        }
        finally { Application.persistentDataPath = previous; Input.Clear(); GUI.queuedClick = null; GUI.enabled = true; GUI.color = Color.white; GUI.matrix = Matrix4x4.identity; if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        Console.WriteLine("Runtime relic forge: " + (4 - failed) + "/4 groups passed; NATIVE_UNVERIFIED"); return failed == 0 ? 0 : 1;
    }
}
