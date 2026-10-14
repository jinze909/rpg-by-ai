// Exercise shipped Update, OnGUI and SaveStore with offline Unity declarations.
// Native import, rendering, audio, hardware and play feel are NATIVE_UNVERIFIED.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeHomewardChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string captureFolder;
    private static SaveData earnedHandoff;
    private static int walkFrames, battleFrames, skills, dodges, commands;
    private static readonly HashSet<ActorAction> skillContacts = new HashSet<ActorAction>(), commandContacts = new HashSet<ActorAction>();
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
            GUI.Export(captureFolder, "qa-homeward-" + capture);
            GUI.commands.AddRange(texts); GUI.textureCalls.AddRange(textures); GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static bool HasText(string value)
    { return GUI.commands.Exists(c => c.Contains("\"text\":" + GUI.S(value))); }
    private static void Update(FrontierRuntime app) { Call(app, "Update"); Input.keys.Clear(); }
    private static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key); Update(app); }
    private static void Click(FrontierRuntime app, Rect rect)
    { GUI.queuedClick = rect; Draw(app); Check(!GUI.queuedClick.HasValue, "The real displayed control accepts its click: " + rect.x + "," + rect.y); }
    private static WorldObject Object(GameCore g, string kind)
    { var o = Array.Find(g.Map.objects, x => x.kind == kind); Check(o != null, "The mature interaction remains: " + kind); return o; }
    private static void Position(FrontierRuntime app, Point point)
    {
        Check(app.Game.CanStand(point), "Staged fixture uses an exact legal hero footprint");
        Check(app.SetVisualReviewPosition(point) && (app.Game.Position - point).Length < .001f, "Visual staging cannot silently repair feet");
        Set(app, "camera", app.Game.Position);
    }
    private static void Interact(FrontierRuntime app, string kind, bool stage = true)
    {
        var o = Object(app.Game, kind);
        if (stage) Position(app, new Point(o.x, o.y - .7f));
        Check(app.Game.Nearest() == o, "Actual E targets the intended nearest interaction " + kind);
        Key(app, KeyCode.E);
    }
    private static void Choose(FrontierRuntime app, string action, string capture = null)
    {
        var g = app.Game;
        Check(g.mode == "dialog" && g.conversation != null, "A number belongs to a real displayed dialogue: " + action);
        int index = Array.FindIndex(g.conversation.choices, c => c.action == action);
        Check(index >= 0 && index < 9, "The production dialogue exposes " + action);
        var pages = DialogueLayout.Pages(g.L(g.conversation.text, g.conversation.textEn), g.save.largeText);
        int page = 0;
        do
        {
            Draw(app, capture == null ? null : capture + "-page" + page);
            Check(HasText(pages[page]), "The actual GUI draws every complete authored narrative page before its choices");
            if ((bool)Call(app, "DialogueChoicesVisible")) break;
            Key(app, KeyCode.Return); page++;
        } while (page < pages.Count);
        Check(page == pages.Count - 1 && (bool)Call(app, "DialogueChoicesVisible"), "Return exposes numbers only after the last narrative page");
        if (g.conversation.choices.Length >= 7)
        {
            var last = GUI.buttonCalls.Find(b => b.rect.y >= 674 && b.rect.x == 320);
            Check(last != null && GUI.textureCalls.Exists(c => c.rect.x == 88 && c.rect.width == 1104 && c.rect.height > 400 && c.rect.y + c.rect.height >= last.rect.y + last.rect.height),
                "The actual dialogue card contains its appended seventh choice, rather than leaving it outside the panel");
        }
        Key(app, (KeyCode)((int)KeyCode.Alpha1 + index));
    }
    private static void Original(GameCore g, params string[] actions)
    {
        Check(g.conversation != null && g.conversation.choices.Length >= actions.Length, "Mature choices remain available");
        for (int i = 0; i < actions.Length; i++) Check(g.conversation.choices[i].action == actions[i], "Mature numbered choice is preserved: " + actions[i]);
    }
    private static void Prepare(FrontierRuntime app)
    {
        Input.Clear(); GUI.queuedClick = null; Screen.width = 1280; Screen.height = 800; Time.unscaledDeltaTime = .02f;
        app.BeginVisualReview(0, 5); app.VisualReviewPaused = false; var g = app.Game;
        // Only fixture the mature prerequisites, never new quest progress or victories.
        foreach (var f in new[] { "personal_0", "manifest_found", "reach_visit", "reach_complete", "reach_post_0", "reach_post_1", "reach_post_2", "companion_1", "homeward_runtime_mature_story" })
            if (!g.Has(f)) g.save.flags.Add(f);
        for (int id = 76000; id <= 76006; id++) if (!g.Has("reach_defeated_" + id)) g.save.flags.Add("reach_defeated_" + id);
        g.enemies.Clear(); // Isolate the completed Mistport chapter only.
        g.save.companion = 1; g.save.english = true; g.save.largeText = false; g.save.showGuidance = true;
        g.save.reducedMotion = true; g.save.effectsQuality = 0;
        Set(app, "applicationPaused", false); Set(app, "pointerSeen", false); Set(app, "controllerUsed", false);
        Set(app, "combatHudTime", 0f); Set(app, "resumeTime", 0f); Set(app, "journalTab", 0); Set(app, "recordsPage", 0); Set(app, "selectedRegion", -1); Call(app, "Resize");
    }
    private static void InviteMira(FrontierRuntime app)
    {
        Key(app, KeyCode.C); Draw(app, "explicit-mira-invitation");
        Check(app.Game.mode == "companions" && app.Game.save.companion == 1, "Opening C does not silently replace selected Ivo");
        Click(app, new Rect(296, 536, 268, 34)); Check(app.Game.save.companion == 0, "Only the real Mira invitation button selects Mira");
        Key(app, KeyCode.Escape); Check(app.Game.mode == "world", "Actual Escape returns from companions");
    }
    private static void Accept(FrontierRuntime app)
    {
        Interact(app, "captain"); Original(app.Game, "gift0", "join0", "close");
        Choose(app, "homeward_offer", "captain-new-route");
        Check(!app.Game.HomewardAccepted && app.Game.save.companion == 1, "The offer neither accepts nor swaps Ivo for Mira");
        Choose(app, "homeward_accept", "offer");
        Check(app.Game.HomewardAccepted && app.Game.save.companion == 1 && app.Game.save.trackedQuest == "homeward", "Actual numbered acceptance records and tracks the new route without companion substitution");
        InviteMira(app);
    }
    private static void EnterReachByMap(FrontierRuntime app)
    {
        Key(app, KeyCode.M); Draw(app);
        if (Get<int>(app, "mapPage") == 0) Click(app, new Rect(685, 633, 149, 24));
        Draw(app); Click(app, new Rect(667, 224, 156, 80)); Draw(app);
        Check(Get<int>(app, "selectedRegion") == 11, "The actual map card selects the preserved 72x48 riverbank");
        Click(app, new Rect(888, 616, 306, 47));
        Check(app.Game.save.room == 11 && app.Game.save.companion == 0 && app.Game.enemies.Count == 0, "Real fast travel resumes Mira's accepted route without an automatically spawned wave");
        Set(app, "camera", app.Game.Position);
    }
    private static void Walk(FrontierRuntime app, Point point, string label)
    {
        var g = app.Game; int frames = 0; float started = g.save.totalTime;
        while ((g.Position - point).Length > .17f && frames++ < 6000)
        {
            Check(g.mode == "world" && !g.Dying && g.save.room == 11, "Actual walking stays alive in the riverbank: " + label);
            Point movement = g.DirectionTo(g.Position, point);
            Input.axes["Horizontal"] = movement.x; Input.axes["Vertical"] = -movement.y; Input.held.Clear();
            if (movement.x > .05f) Input.held.Add(KeyCode.D); if (movement.x < -.05f) Input.held.Add(KeyCode.A);
            if (movement.y > .05f) Input.held.Add(KeyCode.S); if (movement.y < -.05f) Input.held.Add(KeyCode.W);
            Update(app); Check(g.CanStand(g.Position) && g.CanStand(g.companionPosition), "Viola and Mira retain legal feet on " + label);
        }
        Input.axes.Clear(); Input.held.Clear();
        Check(frames < 6000 && g.save.totalTime > started, "Held WASD and real collision reach " + label + " without feet writes");
        for (int wait = 0; wait < 200 && (g.companionPosition - g.Position).Length > 2.5f; wait++) Update(app);
        Check((g.companionPosition - g.Position).Length < 3, "Mira physically arrives for " + label);
        walkFrames += frames;
    }
    private static void Aim(FrontierRuntime app, Point point)
    {
        Set(app, "camera", app.Game.Position); Point screen = (Point)Call(app, "ScreenPoint", point);
        float scale = Get<float>(app, "scale"), x = Get<float>(app, "offsetX"), y = Get<float>(app, "offsetY");
        Input.mousePosition = new Vector3(x + screen.x * scale, Screen.height - y - screen.y * scale, 0);
    }
    private static bool ClearShot(GameCore g, Point point)
    {
        Point delta = point - g.Position; int samples = Math.Max(1, (int)Math.Ceiling(delta.Length / .12f));
        for (int i = 1; i <= samples; i++) if (g.Blocked((g.Position + delta * (i / (float)samples)).x, (g.Position + delta * (i / (float)samples)).y)) return false;
        return true;
    }
    private static void Fight(FrontierRuntime app, int stage)
    {
        var g = app.Game; int frames = 0, beforeKills = g.save.kills[11];
        while (g.enemies.Count > 0 && frames++ < 10000)
        {
            Check(!g.Dying && g.mode == "world", "The ordinary encounter uses real survival and clock rules");
            Enemy target = null; float distance = float.MaxValue;
            foreach (var e in g.enemies) if ((e.Position - g.Position).Length < distance) { target = e; distance = (e.Position - g.Position).Length; }
            bool contact = distance < 1.6f && ClearShot(g, target.Position);
            Point movement = contact ? new Point() : g.DirectionTo(g.Position, target.Position);
            if (target.kind == 2 && target.state == 1 && (g.Position - target.target).Length < 2)
            { movement = (g.Position - target.target).Length > .05f ? (g.Position - target.target).Normalized : new Point(-1, 0); }
            Input.axes["Horizontal"] = movement.x; Input.axes["Vertical"] = -movement.y; Input.held.Clear();
            if (contact) Input.held.Add(KeyCode.J); Aim(app, target.Position);
            if (frames > 12 && contact && g.skillCooldown <= 0 && g.save.energy >= 40) { Input.keys.Add(KeyCode.Q); skills++; }
            if (frames == 8 && g.dashCooldown <= 0 && g.save.energy >= 20) { Input.keys.Add(KeyCode.Space); dodges++; }
            if (g.companionCooldown <= 0) { Input.keys.Add(KeyCode.Z); commands++; }
            if (g.save.health < g.save.maxHealth * .6f && g.save.inventory[6] > 0) Input.keys.Add(KeyCode.F);
            Update(app);
            if (g.playerAction != null && g.playerAction.kind == "skill" && g.playerAction.released) skillContacts.Add(g.playerAction);
            if (g.companionAction != null && g.companionAction.kind == "command" && g.companionAction.released) commandContacts.Add(g.companionAction);
        }
        Input.axes.Clear(); Input.held.Clear();
        Check(frames < 10000 && !g.Dying && g.enemies.Count == 0 && g.save.kills[11] == beforeKills + 2, "Real J/Q/Space/Z clears exactly two ordinary foes with normal drops and kill credit");
        Check(!g.HomewardStopComplete(stage), "Defeating foes does not silently record the route checkpoint");
        battleFrames += frames;
    }
    private static void Stop(FrontierRuntime app, int stage)
    {
        Point point = GameCore.ReachPosts[stage + 1]; Walk(app, point + new Point(0, -.7f), "manual stop " + stage);
        Interact(app, "reachPost" + (stage + 1), false); Choose(app, "homeward_stop" + stage, "stop" + stage + "-start");
        Check(app.Game.enemies.Count == 2 && app.Game.enemies.TrueForAll(e => !e.boss && e.id >= 77000 + stage * 2 && e.id <= 77001 + stage * 2), "Only the explicit stop choice spawns its two canonical ordinary foes");
        Draw(app, "stop" + stage + "-combat"); Fight(app, stage);
        Walk(app, point + new Point(0, -.7f), "checkpoint return " + stage);
        Interact(app, "reachPost" + (stage + 1), false); Choose(app, "homeward_report" + stage, "stop" + stage + "-record");
        Check(app.Game.HomewardStopComplete(stage), "Only the real follow-up number records the cleared checkpoint");
    }
    private static SaveData Copy(SaveData s) { return JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s)); }
    private static SaveData RoundTrip(GameCore g, int slot)
    {
        g.PrepareSave(); Check(SaveStore.Write(g.save, slot), "The production save envelope writes the earned route");
        string warning; SaveData s = SaveStore.Read(slot, out warning);
        Check(s != null && warning == null, "The actual JSON/checksum reader validates the route"); return s;
    }
    private static void Camp(FrontierRuntime app, bool publicChart, string capture)
    {
        Interact(app, "campfire", false); Original(app.Game, "fieldrest", "tea", "drinktea", "companions", "close", "kitchen");
        Check(app.Game.conversation.choices.Length == 7 && app.Game.conversation.choices[6].action == "homeward_camp", "The new seventh camp choice preserves all six mature numbers");
        Choose(app, "homeward_camp", capture + "-camp-menu");
        if (app.Game.HomewardComplete)
        {
            Check(!Array.Exists(app.Game.conversation.choices, c => c.action == "homeward_public" || c.action == "homeward_family"), "A completed chart opens a truthful reread, not another mutable ending choice");
            Choose(app, "close", capture + "-reread"); return;
        }
        Choose(app, publicChart ? "homeward_public" : "homeward_family", capture + "-choice");
        if (app.Game.mode == "dialog") Choose(app, "close", capture + "-response");
    }
    private static void GuildCallback(FrontierRuntime app, string suffix)
    {
        var g = app.Game;
        Check(g.HomewardComplete && earnedHandoff != null, "Guild callback fixtures retain the actually earned route and final choice");
        SaveData completed = RoundTrip(g, 2), control = Copy(earnedHandoff);
        // Fixture the established chapter and old discoveries, never Homeward wins.
        // The incomplete control is the real pre-handoff save earned above.
        foreach (SaveData save in new[] { control, completed })
        {
            save.stage = 3; save.ending = 0;
            foreach (string flag in new[] { "history_10", "history_16", "scout_saved" })
                if (!save.flags.Contains(flag)) save.flags.Add(flag);
            save.english = completed.english; save.largeText = completed.largeText;
            save.effectsQuality = completed.effectsQuality; save.reducedMotion = completed.reducedMotion;
        }
        g.Load(control); g.Start(); g.Enter(0); Interact(app, "guild");
        Original(g, "routes", "close"); Choose(app, "guild_letter", "guild-before-handoff-menu" + suffix);
        string originalZh = g.conversation.text, originalEn = g.conversation.textEn;
        Check(!g.HomewardComplete && !originalEn.Contains("Mira brought the new chart") && !originalZh.Contains("米拉已经把新路图"),
            "The real incomplete-route letter cannot claim an unchosen handoff");
        Check(originalEn.Contains("The patrol never returned") && originalEn.Contains("note in the bark") && originalEn.Contains("inn ledger") && originalEn.Contains("brought the scout home"),
            "Original patrol, bark note, inn ledger and scout callbacks remain in the production letter");
        Choose(app, "close", "guild-before-handoff" + suffix);

        g.Load(completed); g.Start(); g.Enter(0); Interact(app, "guild");
        Original(g, "routes", "close"); Choose(app, "guild_letter", "guild-earned-menu" + suffix);
        const string appendedZh = "\n\n米拉已经把新路图带回营地。那不是失踪者回来的答案，是活着的人下一次出发的路线。";
        const string appendedEn = "\n\nMira brought the new chart to camp. It does not answer the missing names; it gives the living a route for their next journey.";
        Check(g.conversation.text == originalZh + appendedZh && g.conversation.textEn == originalEn + appendedEn,
            "Each earned ending appends the truthful Mira callback while retaining the entire mature letter verbatim");
        Original(g, "guild_letter_record", "close");
        int coins = g.save.coins, shards = g.save.shards, xp = g.save.xp, points = g.save.skillPoints, bond = g.save.relationships[0];
        JournalMoment[] moments = g.save.moments.ToArray(); bool publicChart = g.HomewardPublicChart;
        Choose(app, "guild_letter_record", "guild-earned-letter" + suffix);
        Check(g.GuildLetterReviewed && g.mode == "world" && g.save.moments.Count == Math.Min(8, moments.Length + 1),
            "Actual final-page number records the original once-only guild reply in the mature eight-moment journal without forcing travel or a new route ending");
        int retained = Math.Min(7, moments.Length);
        for (int i = 0; i < retained; i++) Check(g.save.moments[i] == moments[moments.Length - retained + i],
            "The original bounded journal retains its seven newest preceding moments in their exact order");
        Check(g.save.moments[g.save.moments.Count - 1].en == "Lyra's reply to the patrol: finish the list later; bring people home first.",
            "The new chart acknowledges the original guild letter rather than substituting a new reward or moment");
        JournalMoment[] recorded = g.save.moments.ToArray();
        Interact(app, "guild"); Choose(app, "guild_letter");
        Check(g.conversation.text == originalZh + appendedZh && g.conversation.textEn == originalEn + appendedEn,
            "The completed new chart can be truthfully reread with its original guild story intact");
        Choose(app, "guild_letter_record");
        Check(g.save.moments.Count == recorded.Length && g.HomewardComplete && g.HomewardPublicChart == publicChart && g.save.stage == 3 && g.save.ending == 0,
            "Repeated real guild readings retain the once-only moment, route choice and mature chapter");
        for (int i = 0; i < recorded.Length; i++) Check(g.save.moments[i] == recorded[i], "Rereading leaves every existing journal moment unchanged");
        Check(g.save.coins == coins && g.save.shards == shards && g.save.xp == xp && g.save.skillPoints == points && g.save.relationships[0] == bond,
            "Original and repeated guild letter acknowledgments add no currency, skill points, XP or companion bond");
    }
    public static void HomewardCaptainAndExplicitCompanion(FrontierRuntime app)
    {
        Prepare(app); var g = app.Game; g.save.flags.Remove("personal_0");
        Check(!g.HomewardReady && !g.AcceptHomewardRoute(), "The old personal story remains required without fabricated acceptance");
        g.save.flags.Add("personal_0"); Check(g.HomewardReady, "The completed old personal story and riverbank survey make this optional follow-up ready");
        int coins = g.save.coins, shards = g.save.shards; Accept(app);
        Check(g.save.coins == coins && g.save.shards == shards && !g.HomewardComplete, "Offer, acceptance and explicit invitation invent neither a reward nor an ending");
        Interact(app, "captain"); Original(g, "gift0", "join0", "close"); Choose(app, "close");
        Console.WriteLine("PASS real captain E/new numbered offer/accept, original personal choices, no automatic Ivo swap and explicit C-panel Mira invitation; NATIVE_UNVERIFIED");
    }
    public static void HomewardActualRouteCombatRecordsAndPayout(FrontierRuntime app)
    {
        Prepare(app); Accept(app); EnterReachByMap(app); var g = app.Game;
        walkFrames = battleFrames = skills = dodges = commands = 0;
        skillContacts.Clear(); commandContacts.Clear();
        Update(app); Check(g.CurrentGuide.id == "homeward" && g.NavigationTarget().HasValue && Get<List<Point>>(app, "route").Count > 0, "Production Update draws the accepted route's gold walking path");
        Check(!g.StartHomewardStop(1), "The later manual stop cannot start out of order or away from its lamp");
        Stop(app, 0);
        // Physically visit both mature optional records, without inventing their flags.
        foreach (int lore in new[] { 20, 21 })
        {
            var o = Object(g, "history" + lore); Walk(app, new Point(o.x, o.y - .7f), "old record " + lore);
            Interact(app, o.kind, false); Choose(app, "close", "record" + lore);
            Check(g.Has("history_" + lore), "Actual E recovers the mature record " + lore);
        }
        Stop(app, 1); Check(skills > 0 && dodges > 0 && commands > 0 && skillContacts.Count > 0 && commandContacts.Count > 0, "The real fights exercised Space and actual Q/Z release contacts, not just canceled key presses");
        Walk(app, new Point(11, 37.3f), "final camp");
        earnedHandoff = Copy(RoundTrip(g, 2)); Check(!g.HomewardComplete, "The genuinely earned save waits for the explicit final choice");
        int coins = g.save.coins, shards = g.save.shards, points = g.save.skillPoints, bond = g.save.relationships[0];
        Camp(app, true, "public-handoff");
        Check(g.HomewardComplete && g.HomewardPublicChart && g.save.coins == coins + 30 && g.save.shards == shards + 3 && g.save.skillPoints == points && g.save.relationships[0] == Math.Min(5, bond + 1), "Real public handoff grants the exact one-time route reward and capped bond");
        coins = g.save.coins; shards = g.save.shards; bond = g.save.relationships[0]; Camp(app, true, "public-reread");
        Check(g.save.coins == coins && g.save.shards == shards && g.save.relationships[0] == bond && g.HomewardPublicChart, "Reading the completed route cannot pay again or switch its settled outcome");
        Console.WriteLine("PASS actual riverbank route: WASD=" + walkFrames + ", battle=" + battleFrames + ", Q contacts=" + skillContacts.Count + " (inputs=" + skills + "), Space=" + dodges + ", Z contacts=" + commandContacts.Count + "; manual waves/checkpoints, actual record20/21 E and one-time public camp handoff; NATIVE_UNVERIFIED");
    }
    public static void HomewardBilingualGuiMapsAndFamilyChoice(FrontierRuntime app)
    {
        Check(earnedHandoff != null, "Bilingual ending fixtures come from a genuinely completed runtime route");
        for (int language = 0; language < 2; language++) for (int large = 0; large < 2; large++) for (int policy = 0; policy < 2; policy++)
        {
            Prepare(app); var g = app.Game;
            g.save.english = language == 1; g.save.largeText = large == 1; g.save.effectsQuality = policy == 0 ? 0 : 2; g.save.reducedMotion = policy == 0;
            Screen.width = policy == 0 ? 960 : 1920; Screen.height = policy == 0 ? 600 : 1080; Set(app, "camera", g.Position); Set(app, "journalTab", 0); Call(app, "Resize");
            string suffix = (language == 1 ? "-en" : "-zh") + (large == 1 ? "-large" : "-normal") + (policy == 0 ? "-960-minimal" : "-1920-rich");
            Interact(app, "captain"); Original(g, "gift0", "join0", "close"); Choose(app, "homeward_offer", "offer-menu" + suffix);
            Choose(app, "homeward_accept", "offer" + suffix); Check(g.HomewardAccepted && g.save.companion == 1, "Every display policy preserves voluntary companion selection on acceptance");
            g.Load(Copy(earnedHandoff)); g.Start();
            g.toastTime = 0; // Discard the unrelated offer toast from this reused GUI replay fixture.
            g.save.english = language == 1; g.save.largeText = large == 1; g.save.effectsQuality = policy == 0 ? 0 : 2; g.save.reducedMotion = policy == 0;
            if (policy == 1) g.save.relationships[0] = 5; // A mature max-bond fixture, never route progress.
            Set(app, "camera", g.Position);
            Draw(app, "camp-world" + suffix); Key(app, KeyCode.Tab); Draw(app);
            int quest = Array.IndexOf(GameCore.QuestIds, "homeward"); float spacing = Math.Min(46, 464f / GameCore.QuestIds.Length);
            Check(quest == 14 && GameCore.QuestIds.Length == 15, "The new route appends after all fourteen mature quest rows");
            Click(app, new Rect(279, 226 + quest * spacing, 321, spacing - 7)); Draw(app, "journal" + suffix);
            Check(Get<int>(app, "selectedQuest") == 14 && HasText(g.L("下一步：", "Next: ") + g.GetQuest("homeward").next), "The actual fifteenth row exposes the earned camp objective");
            Click(app, new Rect(927, 647, 263, 31)); Draw(app, "destination" + suffix);
            Check(g.mode == "map" && Get<int>(app, "selectedRegion") == 11 && Get<int>(app, "mapPage") == 1, "The actual quest destination opens the large local map on its second page");
            Key(app, KeyCode.Escape); Camp(app, language == 1, "ending" + suffix);
            Check(g.HomewardComplete && g.HomewardPublicChart == (language == 1), "Both genuine final choices resolve through actual numbers under every text/display policy");
            Check(g.save.relationships[0] <= 5, "The actual ending cannot overflow a mature maximum bond");
            Draw(app, "finished" + suffix);
            // The settled chart choice is preserved when the other action is replayed directly.
            int coins = g.save.coins, shards = g.save.shards; Check(!g.FinishHomewardRoute(language != 1), "A repeated final action is refused after completion");
            Check(g.save.coins == coins && g.save.shards == shards && g.HomewardPublicChart == (language == 1), "Repeat attempts preserve both one-time payoff and the chosen chart");
            GuildCallback(app, suffix);
        }
        // Presentation-only fixtures protect every mature <=6-choice card and
        // exercise the new extra rows through the actual shared renderer.
        var game = app.Game; game.toastTime = 0;
        for (int count = 1; count <= 9; count++)
        {
            var choices = new Choice[count];
            for (int i = 0; i < count; i++) choices[i] = new Choice("选项 " + (i + 1), "Choice " + (i + 1), "close");
            game.conversation = new Conversation("界面核查", "Layout check", "每个选项都应留在对话框内。", "Every choice should stay inside its dialogue card.", choices); game.mode = "dialog";
            Draw(app, "choice-card-" + count);
            var card = GUI.textureCalls.Find(c => c.rect.x == 88 && c.rect.y == 180 && c.rect.width == 1104 && c.rect.height > 400);
            Check(card != null && (count > 6 || card.rect.height == 506), "Every mature one-to-six-choice card keeps its exact existing footprint");
            for (int i = 0; i < count; i++)
            {
                int columns = count > 2 ? 2 : count; float width = (832 - (columns - 1) * 14) / Math.Max(1, columns);
                float x = 320 + i % columns * (width + 14), y = 539 + i / columns * 45;
                var button = GUI.buttonCalls.Find(b => Math.Abs(b.rect.x - x) < .001f && Math.Abs(b.rect.y - y) < .001f);
                Check(button != null && Math.Abs(button.rect.width - width) < .001f && button.rect.height == 37, "The shared renderer retains every original numbered choice coordinate");
                Check(button.rect.y + button.rect.height <= card.rect.y + card.rect.height && card.rect.y + card.rect.height < 800, "Seven-to-nine choices fit their recorded card and virtual viewport");
            }
            game.Close();
        }
        Console.WriteLine("PASS actual15-row journal/map destination, all six mature camp choices plus appended seventh, complete family/public and earned guild-letter pages with intact original story/once-only no-economy acknowledgment across Chinese/English, normal/large960/1920 and effects/motion policies; NATIVE_UNVERIFIED");
    }
    public static void HomewardSaveStorePauseTravelDeathAndLegacy(FrontierRuntime app)
    {
        Prepare(app); Accept(app); EnterReachByMap(app); var g = app.Game;
        Walk(app, GameCore.ReachPosts[1] + new Point(0, -.7f), "save-check stop"); Interact(app, "reachPost1", false); Choose(app, "homeward_stop0");
        Enemy hunter = g.enemies.Find(e => e.id == 77000); float full = hunter.hp;
        Position(app, hunter.Position + new Point(-1.5f, 0)); Aim(app, hunter.Position); Input.held.Add(KeyCode.J);
        for (int frame = 0; frame < 80 && hunter.hp == full; frame++) Update(app); Input.held.Clear();
        Check(hunter.hp > 0 && hunter.hp < full, "Real J creates the partial-HP fixture without foe HP writes");
        float remaining = hunter.hp; var saved = RoundTrip(g, 0); g.Load(saved); g.Start();
        Check(g.HomewardAccepted && !g.HomewardStopComplete(0) && g.enemies.Count == 2 && g.enemies.Find(e => e.id == 77000).hp == remaining, "Same-room real envelope restores canonical unresolved wave HP");
        Key(app, KeyCode.Escape); float time = g.save.totalTime; Point ally = g.companionPosition;
        for (int frame = 0; frame < 20; frame++) Update(app);
        Check(g.mode == "pause" && g.save.totalTime == time && (g.companionPosition - ally).Length == 0, "Actual pause freezes both wave and Mira"); Key(app, KeyCode.Escape);
        Call(app, "OnApplicationPause", true); time = g.save.totalTime; for (int frame = 0; frame < 20; frame++) Update(app);
        Check(g.save.totalTime == time, "Application pause does not advance the wave clock"); Call(app, "OnApplicationPause", false);
        saved = RoundTrip(g, 1);
        saved.worldEnemies = new[] { new EnemyRecord { id = 77000, kind = 3, boss = true, x = 37, y = 32, hp = 999, maxHp = 999 }, new EnemyRecord { id = 77001, kind = 2, boss = false, x = 42, y = 28, hp = 999, maxHp = 999 }, new EnemyRecord { id = 77002, kind = 0, x = 62, y = 15, hp = 55, maxHp = 55 } };
        Check(SaveStore.Write(saved, 1), "The real envelope carries hostile cross-wave/wrong-kind/Boss snapshot records"); string warning; saved = SaveStore.Read(1, out warning);
        Check(saved != null && warning == null, "Hostile gameplay records still have a valid real checksum"); g.Load(saved); g.Start();
        Check(g.enemies.Count == 2 && g.enemies.TrueForAll(e => !e.boss && e.id >= 77000 && e.id <= 77001) && g.enemies.Find(e => e.id == 77000).maxHp == 55 && g.enemies.Find(e => e.id == 77001).kind == 1, "Foreign stage, Boss and mismatched kind cannot alter the canonical active wave");
        g.Enter(0); Check(g.HomewardAccepted && !g.HomewardComplete && !g.enemies.Exists(e => e.id >= 77000 && e.id <= 77003), "Travel cannot leak reserved actors into town"); g.Enter(11);
        Check(g.enemies.Count == 2 && g.enemies.Find(e => e.id == 77000).hp == 55 && g.enemies.Find(e => e.id == 77001).hp == 44, "Travel rebuilds only the unresolved canonical stop");
        Position(app, new Point(37, 32)); g.save.health = 1; g.invulnerable = 0;
        for (int frame = 0; frame < 1000 && !g.Dying; frame++) Update(app);
        Check(g.Dying && g.Has("recovery_pending"), "Real ordinary enemy damage enters mature death recovery"); saved = RoundTrip(g, 0); g.Load(saved); g.Start();
        Check(g.save.room == 0 && g.HomewardAccepted && !g.HomewardComplete && g.save.health == g.save.maxHealth && !g.Has("recovery_pending"), "Saving during the actual death pose keeps route progress and safely resumes in town");
        for (int version = 1; version <= 2; version++)
        {
            var legacy = new SaveData { version = version, room = 9, stage = 1, x = 11, y = 16, coins = 137, kills = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, explored = new[] { 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22 }, flags = new List<string> { "intro", "core_1", "watch_complete", "signal_complete", "sparring_complete", "guild_letter_reviewed", "reach_complete", "personal_0" } };
            File.WriteAllText(SaveStore.PathFor(2), JsonUtility.ToJson(legacy)); saved = SaveStore.Read(2, out warning); Check(saved != null && warning == null, "Actual legacy JSON reader accepts v" + version); g.Load(saved); g.Start();
            Check(g.save.room == 9 && g.save.coins == 137 && !g.HomewardAccepted && !g.HomewardComplete && g.save.kills.Length == 12 && g.save.explored.Length == 12, "The new route invents no progress or map migration in old saves");
            for (int i = 0; i < 12; i++) Check(g.save.kills[i] == i + 1 && g.save.explored[i] == i + 11, "Every old map counter retains its exact prefix");
            Check(g.Has("watch_complete") && g.Has("signal_complete") && g.Has("sparring_complete") && g.Has("guild_letter_reviewed") && g.ReachComplete && g.Has("personal_0"), "Both save versions keep all mature optional story value");
            Draw(app, "legacy-v" + version);
        }
        Console.WriteLine("PASS actual SaveStore partial-J HP/checksum, canonical wave ownership, shared menu/application pause, travel/death recovery and unchanged v1/v2 story/map prefixes; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous = Application.persistentDataPath, directory = Path.Combine(Path.GetTempPath(), "starfall-homeward-check-" + Guid.NewGuid());
        Application.persistentDataPath = directory; captureFolder = args.Length > 0 ? args[0] : null; if (captureFolder != null) Directory.CreateDirectory(captureFolder);
        var app = new FrontierRuntime(); int failed = 0;
        try
        {
            Call(app, "Awake"); Check(app.Game != null, "Actual Awake loads the shipped full campaign");
            foreach (Action<FrontierRuntime> test in new Action<FrontierRuntime>[] { HomewardCaptainAndExplicitCompanion, HomewardActualRouteCombatRecordsAndPayout, HomewardBilingualGuiMapsAndFamilyChoice, HomewardSaveStorePauseTravelDeathAndLegacy })
                try { test(app); } catch (Exception error) { failed++; Console.WriteLine("FAIL " + test.Method.Name + ": " + (error.InnerException ?? error).Message); }
            app.EndVisualReview(); Call(app, "OnDestroy");
        }
        finally { Application.persistentDataPath = previous; Input.Clear(); GUI.queuedClick = null; if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        Console.WriteLine("Runtime Homeward route: " + (4 - failed) + "/4 groups passed; NATIVE_UNVERIFIED"); return failed == 0 ? 0 : 1;
    }
}
