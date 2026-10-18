// Exercise production Update, OnGUI and SaveStore using offline Unity declarations.
// Native import, hardware, audio, rendering and play feel are NATIVE_UNVERIFIED.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeCommissionChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string captureFolder;
    private static SaveData earnedTested, partial;
    private static int walkFrames, fightFrames, ventInputs, dodges, foodInputs;
    private static readonly HashSet<ActorAction> skillContacts = new HashSet<ActorAction>();
    private static void Check(bool value, string text) { if (!value) throw new Exception(text); }
    private static object Call(FrontierRuntime app, string name, params object[] args)
    { return typeof(FrontierRuntime).GetMethod(name, Private).Invoke(app, args); }
    private static void Set(FrontierRuntime app, string name, object value)
    { typeof(FrontierRuntime).GetField(name, Private).SetValue(app, value); }
    private static T Get<T>(FrontierRuntime app, string name)
    { return (T)typeof(FrontierRuntime).GetField(name, Private).GetValue(app); }
    private static void Update(FrontierRuntime app) { Call(app, "Update"); Input.keys.Clear(); }
    private static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key); Update(app); }
    private static void Draw(FrontierRuntime app, string capture = null)
    {
        GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app, "OnGUI");
        if (capture != null && captureFolder != null)
        {
            var texts = GUI.commands.ToArray(); var textures = GUI.textureCalls.ToArray(); var buttons = GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder, "qa-commission-" + capture);
            GUI.commands.AddRange(texts); GUI.textureCalls.AddRange(textures); GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static bool HasText(string value) { return GUI.commands.Exists(c => c.Contains("\"text\":" + GUI.S(value))); }
    private static bool HasTextPart(string value) { return GUI.commands.Exists(c => c.Contains(value)); }
    private static void Position(FrontierRuntime app, Point point)
    {
        Check(app.Game.CanStand(point), "Fixture uses exact legal feet at " + point.x + "," + point.y);
        Check(app.SetVisualReviewPosition(point) && (app.Game.Position - point).Length < .001f, "Fixture staging cannot repair invalid feet silently"); Set(app, "camera", app.Game.Position);
    }
    private static WorldObject Object(GameCore g, string kind)
    { var o = Array.Find(g.Map.objects, x => x.kind == kind); Check(o != null, "Original map keeps interaction " + kind); return o; }
    private static void Interact(FrontierRuntime app, string kind, bool stage = true)
    {
        var o = Object(app.Game, kind); if (stage) Position(app, kind == "shade" ? new Point(o.x + 1.3f, o.y) : new Point(o.x, o.y - .7f));
        Check(app.Game.Nearest() == o, "Actual E targets the intended original object " + kind); Key(app, KeyCode.E);
    }
    private static void Choose(FrontierRuntime app, string action, string capture = null)
    {
        var g = app.Game; Check(g.mode == "dialog" && g.conversation != null, "A numeric action belongs to a real dialogue " + action);
        int index = Array.FindIndex(g.conversation.choices, c => c.action == action); Check(index >= 0 && index < 9, "Actual authored dialogue exposes " + action);
        var pages = DialogueLayout.Pages(g.L(g.conversation.text, g.conversation.textEn), g.save.largeText); int page = 0;
        do { Draw(app, capture == null ? null : capture + "-page" + page); Check(HasText(pages[page]), "Actual GUI renders every complete narrative page"); if ((bool)Call(app, "DialogueChoicesVisible")) break; Key(app, KeyCode.Return); page++; } while (page < pages.Count);
        Check(page == pages.Count - 1 && (bool)Call(app, "DialogueChoicesVisible"), "Numbers become visible only on the final page"); Key(app, (KeyCode)((int)KeyCode.Alpha1 + index));
    }
    private static void Original(GameCore g, params string[] actions)
    { Check(g.conversation != null && g.conversation.choices.Length >= actions.Length, "Original numbered choices remain"); for (int i = 0; i < actions.Length; i++) Check(g.conversation.choices[i].action == actions[i], "Original numbered action stays " + actions[i]); }
    private static void Prepare(FrontierRuntime app, int profession = 1)
    {
        Input.Clear(); GUI.queuedClick = null; Screen.width = 1280; Screen.height = 800; Time.unscaledDeltaTime = .02f;
        app.BeginVisualReview(profession, 6); app.VisualReviewPaused = false; var g = app.Game;
        // Fixture only existing chapter ownership and completed original desert foes.
        foreach (string flag in new[] { "personal_1", "companion_1", "boss_6", "mirrors_lit" }) if (!g.Has(flag)) g.save.flags.Add(flag);
        g.save.mirrors = new[] { 1, 2, 1 }; g.enemies.Clear(); g.save.companion = 0;
        g.save.english = true; g.save.largeText = false; g.save.reducedMotion = true; g.save.effectsQuality = 0;
        g.save.teaUntil = g.save.totalTime + 180; g.save.heat = 0;
        Set(app, "applicationPaused", false); Set(app, "pointerSeen", false); Set(app, "controllerUsed", false);
        Set(app, "combatHudTime", 0f); Set(app, "resumeTime", 0f); Set(app, "journalTab", 0); Set(app, "selectedRegion", -1); Call(app, "Resize");
    }
    private static void Accept(FrontierRuntime app)
    {
        Interact(app, "tinker"); Original(app.Game, "gift1", "join1", "close"); Choose(app, "commission_offer", "ivo-offer-menu");
        Check(!app.Game.CommissionAccepted && app.Game.save.companion == 0, "Offer has no acceptance or automatic companion substitution"); Choose(app, "commission_accept", "ivo-offer");
        Check(app.Game.CommissionAccepted && app.Game.save.companion == 0 && app.Game.save.trackedQuest == "commission", "Real acceptance records the quest without replacing Mira");
        Interact(app, "receiver"); Original(app.Game, "close"); Choose(app, "commission_start", "receiver-start");
        Check(app.Game.CommissionActive && app.Game.enemies.Count == 1 && app.Game.enemies[0].id == 78000 && app.Game.enemies[0].kind == 3 && app.Game.enemies[0].boss, "Only explicit receiver choice spawns the canonical test guardian");
    }
    private static void Movement(FrontierRuntime app, Point movement)
    {
        Input.axes["Horizontal"] = movement.x; Input.axes["Vertical"] = -movement.y; Input.held.Clear();
        if (movement.x > .05f) Input.held.Add(KeyCode.D); if (movement.x < -.05f) Input.held.Add(KeyCode.A);
        if (movement.y > .05f) Input.held.Add(KeyCode.S); if (movement.y < -.05f) Input.held.Add(KeyCode.W);
    }
    private static void Walk(FrontierRuntime app, Point destination, string label)
    {
        var g = app.Game; int frames = 0; float before = g.save.totalTime;
        while ((g.Position - destination).Length > .17f && frames++ < 5000)
        { Check(!g.Dying && g.mode == "world", "Real walking survives " + label); Movement(app, g.DirectionTo(g.Position, destination)); Update(app); Check(g.CanStand(g.Position), "Real collision preserves feet " + label); }
        Input.axes.Clear(); Input.held.Clear(); Check(frames < 5000 && g.save.totalTime > before, "WASD reaches " + label + " without feet writes"); walkFrames += frames;
    }
    private static void Aim(FrontierRuntime app, Point target)
    {
        Set(app, "camera", app.Game.Position); Point p = (Point)Call(app, "ScreenPoint", target); float scale = Get<float>(app, "scale");
        Input.mousePosition = new Vector3(Get<float>(app, "offsetX") + p.x * scale, Screen.height - Get<float>(app, "offsetY") - p.y * scale, 0);
    }
    private static SaveData Copy(SaveData save) { return JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save)); }
    private static SaveData RoundTrip(GameCore g, int slot)
    {
        g.PrepareSave(); Check(SaveStore.Write(g.save, slot), "Production envelope writes the actual event"); string warning; var save = SaveStore.Read(slot, out warning);
        Check(save != null && warning == null, "Production checksum/JSON reader restores the event"); return save;
    }
    private static Enemy Guardian(GameCore g) { var e = g.enemies.Find(x => x.id == 78000); Check(e != null, "Canonical guardian exists"); return e; }
    private static void WaitWarning(FrontierRuntime app, int pattern)
    {
        int frames = 0; var g = app.Game;
        while (frames++ < 1500)
        {
            var e = Guardian(g); if (e.state == 1 && GameCore.CommissionPattern(e) == pattern && e.timer > .7f) break;
            // Remain at the receiver, step off the locked line/circle when needed.
            Point movement = new Point();
            if (e.state == 1 && GameCore.CommissionPattern(e) == 0 && e.timer < .7f) movement = new Point(-1, 0);
            if (e.state == 1 && GameCore.CommissionPattern(e) == 1 && (g.Position - e.target).Length < 1.6f) movement = new Point(-1, 0);
            if ((g.Position - new Point(29, 16.3f)).Length > 1.2f && e.state == 0) movement = g.DirectionTo(g.Position, new Point(29, 16.3f));
            Movement(app, movement); Update(app); Check(!g.Dying, "Reading natural warnings survives without invulnerability");
        }
        Input.axes.Clear(); Input.held.Clear(); Check(frames < 1500, "Production AI naturally announces pattern " + pattern);
    }
    private static void WarningGeometry(FrontierRuntime app, Enemy e, string capture)
    {
        Draw(app, capture); Rect bounds = (Rect)Call(app, "EnemyWarningBounds", e); int pattern = GameCore.CommissionPattern(e);
        if (pattern == 0)
        {
            Point a = (Point)Call(app, "ScreenPoint", e.attackOrigin), b = (Point)Call(app, "ScreenPoint", GameCore.CommissionBeamEnd(e));
            float radius = GameCore.CommissionRadius(e) * 36 + 3;
            Check(Math.Abs(bounds.x - (Math.Min(a.x, b.x) - radius)) < .02f && Math.Abs(bounds.y - (Math.Min(a.y, b.y) - radius)) < .02f
                && Math.Abs(bounds.width - (Math.Abs(a.x - b.x) + radius * 2)) < .02f && Math.Abs(bounds.height - (Math.Abs(a.y - b.y) + radius * 2)) < .02f, "HUD bounds use the exact locked beam endpoints and collision width");
            Check(GUI.textureCalls.Exists(c => c.rect.width > 100 && c.rect.height <= 3), "Actual renderer draws the announced beam under minimum effects");
        }
        else
        {
            Point center = (Point)Call(app, "ScreenPoint", e.target); float radius = GameCore.CommissionRadius(e);
            foreach (Point offset in new[] { new Point(radius * 36, 0), new Point(-radius * 36, 0), new Point(0, radius * 36), new Point(0, -radius * 36) })
                Check(GUI.textureCalls.Exists(c => c.rect.width == 4 && c.rect.height == 4 && Math.Abs(c.rect.x - (center.x + offset.x - 2)) < .02f && Math.Abs(c.rect.y - (center.y + offset.y - 2)) < .02f), "Actual circle draws all four exact collision-radius boundary contacts");
            Check(bounds.x <= center.x - radius * 36 && bounds.y <= center.y - radius * 36 && bounds.x + bounds.width >= center.x + radius * 36 && bounds.y + bounds.height >= center.y + radius * 36,
                "HUD avoidance bounds contain the complete collision circle, plus the charge operation card when present");
        }
        Check(HasText(GameCore.CommissionHint(e, app.Game.save.english)), "Actual combat HUD exposes the exact announced operation hint");
    }
    public static void ActualOfferManualStartAndVent(FrontierRuntime app)
    {
        Prepare(app); var g = app.Game; Check(g.CommissionReady && !g.CommissionAccepted, "Completed original personal story naturally unlocks the offer");
        g.CompanionStory(1); Original(g, "gift1", "join1", "close"); Check(Array.FindIndex(g.conversation.choices, c => c.action == "commission_offer") < 0, "Synthetic roster story cannot remotely accept Ivo's physical offer"); g.Close();
        Accept(app); var e = Guardian(g); Check(e.hp == 260 && (e.Position - new Point(29, 13)).Length < .001f, "Fixed guardian uses authored starting HP/footing");
        Interact(app, "receiver"); Check(g.mode == "world" && g.CommissionActive && Guardian(g) == e, "Early receiver E stays in the live test without restarting or pausing it");
        WaitWarning(app, 0); WarningGeometry(app, e, "beam-minimal"); WaitWarning(app, 1); WarningGeometry(app, e, "lock-minimal"); WaitWarning(app, 2); WarningGeometry(app, e, "check-minimal");
        Walk(app, new Point(29, 16.3f), "announced receiver operation");
        if (e.state != 1 || GameCore.CommissionPattern(e) != 2 || e.timer <= .35f) WaitWarning(app, 2);
        Check(g.Nearest() == Object(g, "receiver"), "Real E can reach the receiver within the check window");
        float before = e.hp; int revision = g.revision; Key(app, KeyCode.E); ventInputs++;
        Check(g.mode == "world" && e.state == 0 && e.stun > 1 && e.timer > 1.9f && e.hp == before && g.revision >= revision, "Real receiver E during check directly vents without damage or dialogue pause");
        Check(!g.TryCommissionVent(), "Repeated vent outside the operation window is refused");
        Console.WriteLine("PASS real Ivo E/numbers and original choices, explicit guardian start, three natural warning geometry/HUD paths and actual timed receiver E; NATIVE_UNVERIFIED");
    }
    private static void Fight(FrontierRuntime app)
    {
        var g = app.Game; int frames = 0; bool food = false, dashed = false; ActorAction pendingSkill = null;
        Movement(app, new Point(-1, 0)); Key(app, KeyCode.Space); dodges++;
        Input.axes.Clear(); Input.held.Clear();
        int coins = g.save.coins, xp = g.save.xp, shards = g.save.shards, kills = g.save.kills[6], mastery = g.save.mastery[g.save.weapon], bounty = g.save.bountyProgress;
        int[] inventory = (int[])g.save.inventory.Clone();
        while (g.CommissionActive && frames++ < 6000)
        {
            Enemy e = Guardian(g); Point movement = new Point(); bool attack = (g.Position - e.Position).Length < 5.2f;
            if (e.state == 1)
            {
                int pattern = GameCore.CommissionPattern(e);
                if (pattern == 0 && e.timer < .9f) movement = new Point(-1, 0);
                else if (pattern == 1 && (g.Position - e.target).Length < 1.65f) movement = new Point(-1, 0);
                else if (pattern == 2 && e.timer > .4f) movement = g.DirectionTo(g.Position, new Point(29, 16.3f));
                else if (pattern == 2 && (g.Position - e.Position).Length < 2) movement = new Point(0, 1);
            }
            else if ((g.Position - new Point(28, 16.3f)).Length > .8f) movement = g.DirectionTo(g.Position, new Point(28, 16.3f));
            Movement(app, movement); Aim(app, e.Position); if (attack && movement.Length < .1f) Input.held.Add(KeyCode.J);
            if (attack && g.skillCooldown <= 0 && g.save.energy >= 40 && movement.Length < .1f) Input.keys.Add(KeyCode.Q);
            if (!dashed && frames > 20 && movement.Length > .1f && g.dashCooldown <= 0 && g.save.energy >= 20) { Input.keys.Add(KeyCode.Space); dodges++; dashed = true; }
            if (!food && g.save.health < g.save.maxHealth * .9f && g.save.inventory[6] > 0) { Input.keys.Add(KeyCode.F); foodInputs++; food = true; }
            if (e.state == 1 && GameCore.CommissionPattern(e) == 2 && e.timer > .4f && (g.Position - new Point(29, 17)).Length < 1.6f) { Input.keys.Add(KeyCode.E); ventInputs++; }
            Update(app); Check(!g.Dying && g.mode == "world" && g.CanStand(g.Position), "The live J/Q battle survives with ordinary damage, collision and energy");
            if (g.playerAction != null && g.playerAction.kind == "skill") pendingSkill = g.playerAction;
            if (pendingSkill != null && pendingSkill.released) skillContacts.Add(pendingSkill);
        }
        Input.axes.Clear(); Input.held.Clear(); Check(frames < 6000 && g.CommissionTested && !g.CommissionActive && !g.CommissionReported && !g.CommissionComplete, "Real contact defeats the machine only, leaving report and water collection manual");
        Check(g.save.coins == coins && g.save.xp == xp && g.save.shards == shards && g.save.kills[6] == kills && g.save.mastery[g.save.weapon] == mastery && g.save.bountyProgress == bounty, "The test cannot award ordinary guardian money, XP, kill, mastery or bounty credit");
        for (int i = 0; i < inventory.Length; i++) Check(g.save.inventory[i] == inventory[i] - (i == 6 && food ? 1 : 0), "Test guardian cannot create ordinary loot");
        Check(skillContacts.Count > 0 && dodges > 0, "Real released Q contacts and Space were exercised: Q=" + skillContacts.Count + ", Space=" + dodges + ", frames=" + frames); fightFrames += frames;
    }
    private static void Report(FrontierRuntime app, bool oasis, string capture, bool stage = true)
    {
        Interact(app, "tinker", stage); Original(app.Game, "gift1", "join1", "close"); Choose(app, "commission_offer", capture + "-ivo");
        Choose(app, oasis ? "commission_oasis" : "commission_caravan", capture + "-choice");
    }
    private static void Collect(FrontierRuntime app, bool oasis, string capture, bool stage = true)
    {
        Interact(app, oasis ? "shade" : "campfire", stage);
        if (!oasis) Original(app.Game, "fieldrest", "tea", "drinktea", "companions", "close", "kitchen");
        if (!oasis) Choose(app, "commission_supply", capture + "-camp-menu");
        Choose(app, "commission_water", capture + "-source");
    }
    public static void ActualCombatReportAndDailyWater(FrontierRuntime app)
    {
        Prepare(app); Accept(app); var g = app.Game;
        // First take one genuine accepted warning, then use the real F item.
        WaitWarning(app, 0); var e = Guardian(g); float before = g.save.health;
        while (e.state == 1) Update(app);
        Check(g.save.health < before, "Real locked beam can damage a present player without health writes");
        Fight(app); earnedTested = Copy(RoundTrip(g, 1));
        int coins = g.save.coins, shards = g.save.shards, bond = g.save.relationships[1]; Walk(app, new Point(22, 17.3f), "Ivo report"); Report(app, true, "oasis-report", false);
        Check(g.CommissionReported && !g.CommissionComplete && g.save.coins == coins + 35 && g.save.shards == shards + 4 && g.save.relationships[1] == Math.Min(5, bond + 1), "Real report grants exact one-time commissioning reward and waits for a first cup");
        coins = g.save.coins; shards = g.save.shards; bond = g.save.relationships[1]; Check(!g.FinishCommission(false), "Outcome cannot switch or pay twice");
        Check(g.save.coins == coins && g.save.shards == shards && g.save.relationships[1] == bond, "Repeat report preserves existing economy and selected water point");
        int tea = g.save.tea, herbs = g.save.inventory[4], crystal = g.save.inventory[1]; Walk(app, new Point(9.3f, 17), "first water at the oasis"); Collect(app, true, "first-water", false);
        Check(g.CommissionComplete && g.save.tea == tea + 1 && g.save.inventory[4] == herbs && g.save.inventory[1] == crystal, "First actual cup completes the promise without charging mature brewing resources");
        Collect(app, true, "same-day-water"); Check(g.save.tea == tea + 1, "Same-day repeats cannot farm a second cup");
        Draw(app, "oasis-finished");
        // Existing tier-one waystation ownership is a separate mature fixture;
        // use its actual numbered rest action to reach tomorrow, never write time.
        g.save.projects[1] = 1; int day = g.Day, rests = 0, restTea = g.save.tea;
        while (g.Day == day && rests++ < 13)
        {
            Interact(app, "campfire"); Original(g, "fieldrest", "tea", "drinktea", "companions", "close", "kitchen");
            float beforeTime = g.save.totalTime; Choose(app, "fieldrest");
            Check(g.save.totalTime >= beforeTime + 20 && g.save.health == g.save.maxHealth && g.save.energy == 100 && g.save.tea == restTea, "Original tier-one field rest keeps its twenty-second recovery and separate tea rules");
        }
        Check(g.Day > day && !g.Has("commission_water_day_" + g.Day), "Only actual rest actions unlock the next daily water allocation");
        g.save.tea = 999; Collect(app, true, "full-pack-water");
        Check(g.save.tea == 999 && !g.Has("commission_water_day_" + g.Day), "A full pack cannot overflow or consume today's waiting cup");
        Key(app, KeyCode.T); Check(g.save.tea == 998 && g.save.teaUntil > g.save.totalTime, "Original actual T drink frees space and grants normal climate protection");
        Collect(app, true, "next-day-water"); Check(g.save.tea == 999 && g.Has("commission_water_day_" + g.Day), "Actual next-day E/number collects the waiting cup once after T");
        Position(app, new Point(23, 4.3f)); Check(g.Nearest().id == "oasis1", "The other original shade shelter remains a distinct interaction"); Key(app, KeyCode.E);
        Check(g.mode == "world" && g.save.tea == 999, "Unselected shade retains its original notice and no event reward"); Draw(app, "original-shade-shelter");
        Check(g.LightRays().Count == 4 && (g.LightRays()[3].to - GameCore.CommissionReceiverPoint).Length < .3f && g.Has("boss_6") && g.Has("desert_report") && g.Has("personal_1"), "Trial preserves the completed original mirror route, chapter guardian, report and personal repair");
        Console.WriteLine("PASS actual J/Q/Space/F contacts, isolated event victory without generic boss rewards, manual Ivo report, chosen oasis first cup, actual rest next day/full-pack/T and original shade/mirror behavior; WASD=" + walkFrames + ", battle=" + fightFrames + ", Q contacts=" + skillContacts.Count + ", E vent=" + ventInputs + ", Space=" + dodges + ", F=" + foodInputs + "; NATIVE_UNVERIFIED");
    }
    public static void BilingualGuiWarningsJournalAndCampChoice(FrontierRuntime app)
    {
        Check(earnedTested != null, "Ending GUI replays genuinely earned contact victory");
        for (int language = 0; language < 2; language++) for (int large = 0; large < 2; large++) for (int policy = 0; policy < 2; policy++)
        {
            Prepare(app); var g = app.Game; g.Load(Copy(earnedTested)); g.Start();
            g.save.english = language == 1; g.save.largeText = large == 1; g.save.effectsQuality = policy == 0 ? 0 : 2; g.save.reducedMotion = policy == 0; g.toastTime = 0;
            Screen.width = policy == 0 ? 960 : 1920; Screen.height = policy == 0 ? 600 : 1080; Call(app, "Resize"); Set(app, "camera", g.Position);
            string suffix = (language == 1 ? "-en" : "-zh") + (large == 1 ? "-large" : "-normal") + (policy == 0 ? "-960-minimal" : "-1920-rich");
            Key(app, KeyCode.Tab); Draw(app); int quest = Array.IndexOf(GameCore.QuestIds, "commission"); float spacing = Math.Min(46, 464f / 18);
            string[] original = { "main", "shore", "ally1", "ally2", "ally3", "personal0", "personal1", "personal2", "bounty", "secrets", "watch", "signal", "archive", "reach", "homeward", "commission" };
            Check(quest == 15 && GameCore.QuestIds.Length >= original.Length, "Commission keeps its sixteenth journal row when new content appends");
            for (int i = 0; i < original.Length; i++) Check(GameCore.QuestIds[i] == original[i], "Every original journal id keeps its exact index: " + original[i]);
            GUI.queuedClick = new Rect(279, 226 + quest * spacing, 321, spacing - 7); Draw(app, "journal" + suffix); Check(!GUI.queuedClick.HasValue, "Actual sixteenth journal row accepts its click"); Draw(app, "objective" + suffix);
            Check(Get<int>(app, "selectedQuest") == 15 && HasText(g.L("下一步：", "Next: ") + g.GetQuest("commission").next), "Actual journal shows the earned report objective");
            Key(app, KeyCode.Escape); int coins = g.save.coins; Report(app, language == 1, "report" + suffix);
            Check(g.CommissionReported && g.save.coins == coins + 35, "Either numbered settlement earns exact same once reward across display policies");
            Collect(app, language == 1, "water" + suffix); Check(g.CommissionComplete, "Both physical water-point outcomes pay their first daily cup");
            Draw(app, "finished" + suffix);
            Interact(app, "campfire"); Original(g, "fieldrest", "tea", "drinktea", "companions", "close", "kitchen");
            // Preserve first six mature numbers even when the selected camp appends a seventh.
            Choose(app, "close", "original-camp" + suffix);
            Interact(app, "receiver"); Original(g, "close"); Choose(app, "close", "original-receiver" + suffix);
        }
        // Actual naturally prepared AI warnings on both presentation policies,
        // with no enemy state, cycle, HP or locked-target writes.
        foreach (bool english in new[] { false, true }) foreach (bool minimal in new[] { false, true })
        {
            Prepare(app); app.Game.save.english = english; app.Game.save.largeText = true; app.Game.save.effectsQuality = minimal ? 0 : 2; app.Game.save.reducedMotion = minimal;
            Screen.width = minimal ? 960 : 1920; Screen.height = minimal ? 600 : 1080; Call(app, "Resize"); Accept(app);
            for (int pattern = 0; pattern < 3; pattern++) { WaitWarning(app, pattern); WarningGeometry(app, Guardian(app.Game), "warning" + pattern + (english ? "-en" : "-zh") + (minimal ? "-960-minimal" : "-1920-rich")); }
        }
        Console.WriteLine("PASS actual sixteenth journal click and complete original sixteen-id prefix, all paged authored settlement/cup dialogues, both outcomes and preserved mature receiver/camp numbers under Chinese/English normal/large960/1920; real AI beam/circle/check warning geometry at minimal/full effects and reduced motion; NATIVE_UNVERIFIED");
    }
    public static void SaveStorePauseRetryDeathAndLegacy(FrontierRuntime app)
    {
        Prepare(app); Accept(app); var g = app.Game; var e = Guardian(g); float full = e.hp; Aim(app, e.Position); Input.held.Add(KeyCode.J);
        for (int frame = 0; frame < 100 && e.hp == full; frame++) Update(app); Input.held.Clear();
        Check(e.hp > 0 && e.hp < full, "Real J creates partial HP without enemy-health writes"); partial = Copy(RoundTrip(g, 0)); float hp = e.hp;
        g.Load(Copy(partial)); g.Start(); e = Guardian(g); Check(g.CommissionActive && e.hp == hp && e.state == 0 && e.windupDuration == 0, "Actual JSON/envelope restores valid HP with a fresh unannounced AI state");
        Key(app, KeyCode.Escape); float time = g.save.totalTime; float timer = e.timer; for (int frame = 0; frame < 20; frame++) Update(app);
        Check(g.mode == "pause" && g.save.totalTime == time && e.timer == timer, "Actual pause freezes machine and clock"); Key(app, KeyCode.Escape);
        Call(app, "OnApplicationPause", true); time = g.save.totalTime; timer = e.timer; for (int frame = 0; frame < 20; frame++) Update(app);
        Check(g.save.totalTime == time && e.timer == timer, "Application pause freezes the machine"); Call(app, "OnApplicationPause", false);
        var hostile = Copy(partial); hostile.worldEnemies = new[] { new EnemyRecord { id = 78000, kind = 1, boss = false, x = 29, y = 13, hp = 9999, maxHp = 9999 }, new EnemyRecord { id = 77000, kind = 3, boss = true, x = 28, y = 13, hp = 999, maxHp = 999 } };
        Check(SaveStore.Write(hostile, 2), "Hostile cross-role/cross-system actor snapshot has an actual envelope"); string warning; hostile = SaveStore.Read(2, out warning); Check(hostile != null && warning == null, "Hostile gameplay actor records retain a valid checksum"); g.Load(hostile); g.Start();
        Check(!g.CommissionActive && !g.enemies.Exists(x => x.id == 78000 || x.id == 77000), "Invalid roles cannot create a machine or foreign Homeward guardian");
        Interact(app, "receiver"); Choose(app, "commission_start"); Check(g.CommissionActive && Guardian(g).hp == 260, "Invalid actor save supports an explicit safe canonical retry");
        g.Enter(0); Check(g.CommissionAccepted && !g.CommissionTested && !g.enemies.Exists(x => x.id == 78000), "Travel never leaks commission actor into town"); g.Enter(6);
        Check(!g.CommissionActive, "Returning cannot silently restart an abandoned live test");
        g.enemies.Clear(); Interact(app, "receiver"); Choose(app, "commission_start");
        Position(app, new Point(29, 16.3f)); g.save.health = 1; g.invulnerable = 0;
        for (int frame = 0; frame < 1000 && !g.Dying; frame++) Update(app);
        Check(g.Dying && g.Has("recovery_pending"), "Actual machine damage enters mature death recovery"); var death = RoundTrip(g, 1); g.Load(death); g.Start();
        Check(g.save.room == 0 && g.CommissionAccepted && !g.CommissionTested && g.save.health == g.save.maxHealth && !g.CommissionActive, "Death save keeps acceptance and restores safely without completing or retrying the test");
        for (int version = 1; version <= 2; version++)
        {
            var legacy = new SaveData { version = version, room = 9, stage = 1, x = 11, y = 16, coins = 137, kills = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, explored = new[] { 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22 }, flags = new List<string> { "intro", "core_1", "desert_report", "personal_1", "companion_1", "homeward_complete", "signal_complete", "sparring_complete" }, battlePlans = new[] { new BattlePlan { recorded = true, weapon = 0, relicFirst = -1, relicSecond = -1 } } };
            File.WriteAllText(SaveStore.PathFor(2), JsonUtility.ToJson(legacy)); var saved = SaveStore.Read(2, out warning); Check(saved != null && warning == null, "Actual legacy JSON accepts v" + version); g.Load(saved); g.Start();
            Check(g.save.room == 9 && g.save.coins == 137 && !g.CommissionAccepted && !g.CommissionComplete && g.save.kills.Length == 12 && g.save.explored.Length == 12, "Old saves invent no machine flags or migrations");
            for (int i = 0; i < 12; i++) Check(g.save.kills[i] == i + 1 && g.save.explored[i] == i + 11, "Every mature map-counter prefix survives v" + version);
            Check(g.Has("homeward_complete") && g.Has("personal_1") && g.Has("signal_complete") && g.Has("sparring_complete") && g.save.battlePlans[0].recorded, "Legacy loader retains existing stories and recorded battle plans"); Draw(app, "legacy-v" + version);
        }
        Console.WriteLine("PASS actual SaveStore partial-J HP/checksum/fresh warning, invalid foreign IDs/roles, manual retry, shared menu/application pause, travel/death recovery and v1/v2 mature story/map/plan prefixes; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous = Application.persistentDataPath, directory = Path.Combine(Path.GetTempPath(), "starfall-commission-check-" + Guid.NewGuid());
        Application.persistentDataPath = directory; captureFolder = args.Length > 0 ? args[0] : null; if (captureFolder != null) Directory.CreateDirectory(captureFolder);
        var app = new FrontierRuntime(); int failed = 0;
        try
        {
            Call(app, "Awake"); Check(app.Game != null, "Production Awake loads the full original campaign");
            foreach (Action<FrontierRuntime> test in new Action<FrontierRuntime>[] { ActualOfferManualStartAndVent, ActualCombatReportAndDailyWater, BilingualGuiWarningsJournalAndCampChoice, SaveStorePauseRetryDeathAndLegacy })
                try { test(app); } catch (Exception error) { failed++; Console.WriteLine("FAIL " + test.Method.Name + ": " + (error.InnerException ?? error).Message); }
            app.EndVisualReview(); Call(app, "OnDestroy");
        }
        finally { Application.persistentDataPath = previous; Input.Clear(); GUI.queuedClick = null; if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        Console.WriteLine("Runtime Tomorrow commissioning: " + (4 - failed) + "/4 groups passed; NATIVE_UNVERIFIED"); return failed == 0 ? 0 : 1;
    }
}
