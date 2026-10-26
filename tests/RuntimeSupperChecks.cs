// Shipped Update, dialogue, guidance, OnGUI and SaveStore using offline Unity
// declarations. Fixture staging is explicit. NATIVE_UNVERIFIED remains in force.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeSupperChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static string captures;
    static int captureCount, walkingFrames;
    static readonly string[] PrepKinds = { "garden", "fish", "forge" };
    static void Check(bool value, string text) { if (!value) throw new Exception(text); }
    static object Call(FrontierRuntime app, string name, params object[] args) { return typeof(FrontierRuntime).GetMethod(name, Private).Invoke(app, args); }
    static void Set(FrontierRuntime app, string name, object value) { typeof(FrontierRuntime).GetField(name, Private).SetValue(app, value); }
    static void Update(FrontierRuntime app) { Call(app, "Update"); Input.keys.Clear(); }
    static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key); Update(app); }
    static bool HasText(string value) { return GUI.commands.Exists(c => c.Contains("\"text\":" + GUI.S(value))); }
    static SaveData Copy(SaveData save) { return JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save)); }
    static void Draw(FrontierRuntime app, string name = null)
    {
        GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app, "OnGUI");
        if (name != null && captures != null) { var commands = GUI.commands.ToArray(); var textures = GUI.textureCalls.ToArray(); var buttons = GUI.buttonCalls.ToArray(); GUI.Export(captures, "qa-supper-" + name); GUI.commands.AddRange(commands); GUI.textureCalls.AddRange(textures); GUI.buttonCalls.AddRange(buttons); captureCount++; }
    }
    static WorldObject Object(GameCore game, string kind)
    { var o = Array.Find(game.Map.objects, x => x.kind == kind); Check(o != null, "Authored interaction exists: " + kind); return o; }
    static void Position(FrontierRuntime app, Point point)
    {
        Check(app.Game.CanStand(point), "Explicit fixture staging uses exact legal feet " + point.x + "," + point.y);
        Check(app.SetVisualReviewPosition(point) && (app.Game.Position - point).Length < .001f, "Staged feet must not silently repair");
        Set(app, "camera", app.Game.Position);
    }
    static void Stop() { Input.axes.Clear(); Input.held.Clear(); }
    static void Move(Point direction)
    {
        Input.axes["Horizontal"] = direction.x; Input.axes["Vertical"] = -direction.y; Input.held.Clear();
        if (direction.x > .01f) Input.held.Add(KeyCode.D); if (direction.x < -.01f) Input.held.Add(KeyCode.A);
        if (direction.y > .01f) Input.held.Add(KeyCode.S); if (direction.y < -.01f) Input.held.Add(KeyCode.W);
    }
    static void Walk(FrontierRuntime app, WorldObject target)
    {
        var game = app.Game; Point point = new Point(target.x, target.y); int startRoom = game.save.room;
        // Use the actual navigation search, followed by real WASD updates. The
        // final direct approach is short and checked against actual collision.
        var path = game.NavigationPath(point); Check(path.Count > 0 || (game.Position - point).Length < 1.5f, "The authored interaction has a real navigation path: " + target.kind);
        foreach (Point waypoint in path)
        {
            int frames = 0;
            while ((game.Position - waypoint).Length > .11f && frames++ < 200)
            {
                Point before = game.Position; Move((waypoint - before).Normalized); Update(app); walkingFrames++;
                Check(game.mode == "world" && game.save.room == startRoom && game.CanStand(game.Position) && (game.Position - before).Length < .3f, "Real WASD preserves collision, room and continuous movement");
            }
            Stop(); Check(frames < 200, "Actual movement reaches each navigation waypoint: " + target.kind);
        }
        int approach = 0;
        while (game.Nearest() != target && (game.Position - point).Length > .3f && approach++ < 40)
        { Move((point - game.Position).Normalized); Update(app); walkingFrames++; }
        Stop(); Check(game.Nearest() == target, "The actual nearest-object interaction selects " + target.kind);
    }
    static void Interact(FrontierRuntime app, string kind, bool walk = false)
    {
        var o = Object(app.Game, kind);
        if (walk) Walk(app, o); else Position(app, new Point(o.x + .5f, o.y + .5f));
        Check(app.Game.Nearest() == o, "Production E targets exactly " + kind); Key(app, KeyCode.E);
    }
    static void Choose(FrontierRuntime app, string action, string capture = null)
    {
        var game = app.Game; Check(game.mode == "dialog" && game.conversation != null, "Choice belongs to actual dialogue: " + action);
        int index = Array.FindIndex(game.conversation.choices, c => c.action == action); Check(index >= 0 && index < 9, "Numbered action is available: " + action);
        var pages = DialogueLayout.Pages(game.L(game.conversation.text, game.conversation.textEn), game.save.largeText); int page = 0;
        while (true)
        {
            Draw(app, capture == null ? null : capture + "-page" + page);
            Check(HasText(pages[page]), "Actual GUI shows complete authored narrative page before selection");
            if ((bool)Call(app, "DialogueChoicesVisible")) break;
            Key(app, KeyCode.Return); page++; Check(page < pages.Count, "Narrative pagination terminates");
        }
        Check(page == pages.Count - 1, "Actual numbered choices wait until the final narrative page");
        int columns = game.conversation.choices.Length > 2 ? 2 : game.conversation.choices.Length;
        for (int i = 0; i < game.conversation.choices.Length; i++)
        {
            float x = 320 + i % columns * ((832 - (columns - 1) * 14) / columns + 14), y = 539 + i / columns * 45;
            var button = GUI.buttonCalls.Find(b => Math.Abs(b.rect.x - x) < .01f && Math.Abs(b.rect.y - y) < .01f);
            Check(button != null, "Every original and appended numbered choice retains its actual control");
            string offered = game.conversation.choices[i].action;
            bool enabled = offered == "cook" ? game.save.inventory[6] < 9999 && game.save.inventory[2] >= 2 && game.save.inventory[3] >= 1
                : offered == "tea" ? game.save.tea < 999 && game.save.inventory[2] >= 2 && game.save.inventory[1] >= 1
                : offered == "buymeal" ? game.save.inventory[6] < 9999 && game.save.coins >= (game.save.reputation[1] >= 3 ? 15 : 18)
                : offered == "buywood" ? game.save.inventory[0] <= 9995 && game.save.coins >= (game.save.reputation[1] >= 3 ? 8 : 10)
                : offered == "seeds" ? game.save.inventory[7] <= 9996 && game.save.coins >= 5
                : offered == "plant" ? game.save.plantedAt < 0 && game.save.inventory[7] >= 1
                : offered == "ally1" ? !game.Has("ally_1") && game.save.inventory[2] >= 6
                : offered == "ally2" ? !game.Has("ally_2") && game.save.inventory[3] >= 3
                : offered == "ally3" ? !game.Has("ally_3") && game.save.inventory[4] >= 5
                : offered == "engine" ? !game.Has("gate_3") && game.save.inventory[4] >= 3 : true;
            Check(button.enabled == enabled, "Paid services and material handoffs match actual stock; free story choices stay clickable: " + offered);
            Check(GUI.textureCalls.Exists(t => t.rect.x == 88 && t.rect.y == 180 && t.rect.width == 1104 && t.rect.y + t.rect.height >= button.rect.y + button.rect.height),
                "The actual dialogue card encloses every original and appended choice, including the fourth row");
        }
        Check(game.DialogueChoiceEnabled(action), "The intended mature narrative action remains available: " + action);
        Key(app, (KeyCode)((int)KeyCode.Alpha1 + index));
    }
    static void Prepare(FrontierRuntime app, int room = 0, bool ready = true)
    {
        Input.Clear(); GUI.queuedClick = null; Screen.width = 960; Screen.height = 640; Time.unscaledDeltaTime = .02f;
        app.BeginVisualReview(0, room); app.VisualReviewPaused = false; var game = app.Game;
        game.save.flags.RemoveAll(f => f.StartsWith("supper_"));
        for (int i = 1; i <= 3; i++) { game.save.flags.Remove("ally_" + i); if (ready) game.save.flags.Add("ally_" + i); }
        game.save.flags.Add("supper_runtime_mature_story"); game.save.companion = -1;
        game.save.english = true; game.save.largeText = false; game.save.reducedMotion = true; game.save.effectsQuality = 0; game.save.showGuidance = true;
        Set(app, "applicationPaused", false); Set(app, "pointerSeen", false); Set(app, "controllerUsed", false);
        Set(app, "arrivalTime", 0f); Set(app, "resumeTime", 0f); Set(app, "combatHudTime", 0f); Set(app, "journalTab", 0); Set(app, "selectedQuest", 0);
        Update(app); Set(app, "arrivalTime", 0f); Set(app, "resumeTime", 0f); game.toastTime = 0; Call(app, "Resize");
    }
    static void Accept(FrontierRuntime app, bool walk = false, string capture = null)
    { Interact(app, "cook", walk); Choose(app, "supper_menu", capture == null ? null : "chef-" + capture); Choose(app, "supper_accept", capture); Check(app.Game.SupperAccepted, "Actual E and numbered acceptance begin supper"); }
    static void Help(FrontierRuntime app, int index, bool walk = false, string capture = null)
    { Interact(app, PrepKinds[index], walk); Choose(app, "supper_prep_" + index); Choose(app, "supper_help_" + index, capture); Check(app.Game.SupperPrepared(index), "Actual resident contribution is recorded " + index); }
    static void Cook(FrontierRuntime app, string capture = null)
    { Interact(app, "cook"); Choose(app, "supper_menu"); Choose(app, "supper_cook", capture); Check(app.Game.SupperCooked, "The actual inn chef cooks only after contributions"); }
    static void Serve(FrontierRuntime app, int index, bool walk = false, string capture = null)
    { Interact(app, "supperBowl" + index, walk); Choose(app, "supper_serve_" + index, capture); Check(app.Game.SupperServed(index), "Actual bowl is served once " + index); }
    static void Finish(FrontierRuntime app, string capture = null)
    { Interact(app, "cook"); Choose(app, "supper_menu"); Choose(app, "supper_finish", capture); Check(app.Game.SupperComplete, "Actual inn return completes the shared supper"); }
    static void BowlDrawing(FrontierRuntime app, int index, bool visible)
    {
        var game = app.Game; var bowl = Object(game, "supperBowl" + index);
        GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app, "DrawObject", bowl);
        if (!visible) { Check(GUI.commands.Count == 0 && GUI.textureCalls.Count == 0, "Uncooked physical bowl produces no world submissions"); return; }
        string label = game.L("桌边 ", "TABLE PLACE ") + (index + 1) + (game.SupperServed(index) ? " +" : "");
        Check(HasText(label), "Each physical table shows the actual localized serving state");
        Point screen = (Point)Call(app, "ScreenPoint", new Point(bowl.x, bowl.y)); Color tint = PixelArt.C(game.SupperServed(index) ? "#7acabb" : "#ebc58b");
        Check(GUI.textureCalls.Count == 21 && GUI.textureCalls.FindAll(t => t.rect.width == 28 && t.rect.height == 28 && Math.Abs(t.rect.x - (screen.x - 14)) < .001f && Math.Abs(t.rect.y - (screen.y - 31)) < .001f).Count == 1,
            "A table draws one existing stew icon and twenty ring marks, without cloning an NPC or any new atlas");
        var marks = GUI.textureCalls.FindAll(t => t.rect.width == 4 && t.rect.height == 4);
        Check(marks.Count == 20, "A table has exactly twenty readable ring marks");
        foreach (var mark in marks)
        {
            Point center = new Point(mark.rect.x + 2, mark.rect.y + 2);
            Check(Math.Abs((center - screen).Length - 14.4f) < .01f && Math.Abs(mark.color.r - tint.r) < .001f && Math.Abs(mark.color.g - tint.g) < .001f && Math.Abs(mark.color.b - tint.b) < .001f,
                "Actual low-FX ring keeps its .4 tile radius and gold-to-mint serving feedback");
        }
        Check(GUI.color.r == 1 && GUI.color.g == 1 && GUI.color.b == 1 && GUI.color.a == 1, "Table drawing restores GUI tint for the mature scene");
    }
    static void Load(FrontierRuntime app, SaveData save)
    { app.Game.Load(save); app.Game.Start(); app.Game.Close(); Set(app, "camera", app.Game.Position); Set(app, "arrivalTime", 0f); Set(app, "resumeTime", 0f); }
    static SaveData Roundtrip(FrontierRuntime app, int slot)
    {
        app.Game.PrepareSave(); Check(SaveStore.Write(app.Game.save, slot), "Actual SaveStore envelope writes"); string warning;
        var save = SaveStore.Read(slot, out warning); Check(save != null && warning == null, "Actual checksum read accepts its envelope"); Load(app, save); return save;
    }
    static void Replay(GameCore game, string action)
    {
        game.mode = "dialog"; game.conversation = new Conversation("QA replay", "QA replay", "stale action", "stale action", new Choice("replay", "replay", action)); game.Choose(0);
    }
    public static void ActualWalkInputsAndPreservedMenus(FrontierRuntime app)
    {
        Prepare(app); var game = app.Game; int[] items = (int[])game.save.inventory.Clone(); int coins = game.save.coins, tea = game.save.tea;
        Interact(app, "cook", true); string[] prefix = { "cook", "buymeal", "buywood", "tea", "close", "kitchen" };
        for (int i = 0; i < prefix.Length; i++) Check(game.conversation.choices[i].action == prefix[i], "Every original town cooking choice retains its numeric position");
        Choose(app, "supper_menu"); Choose(app, "supper_accept"); Check(game.SupperAccepted && game.SupperPreparedCount == 0, "A real accepted event starts with no invented contributions");
        for (int i = 0; i < 3; i++)
        {
            Interact(app, PrepKinds[i], true);
            string[][] originals = { new[] { "plant", "seeds", "close" }, new[] { "fish", "close" }, new[] { "upgrade", "bow", "staff", "close" } };
            for (int k = 0; k < originals[i].Length; k++) Check(game.conversation.choices[k].action == originals[i][k], "Original resident menu prefix stays in place for " + PrepKinds[i]);
            Choose(app, "supper_prep_" + i); Choose(app, "supper_help_" + i); Check(game.SupperPreparedCount == i + 1, "Each real contribution increments exactly once");
        }
        Interact(app, "portal9", true); Check(game.save.room == 9 && game.mode == "world", "Actual town E portal enters the intact Hearth Inn");
        Interact(app, "cook", true); for (int i = 0; i < prefix.Length; i++) Check(game.conversation.choices[i].action == prefix[i], "Original inn cooking menu prefix remains"); Choose(app, "supper_menu"); Choose(app, "supper_cook");
        for (int i = 0; i < 3; i++) { Check(game.IsObjectPresent(Object(game, "supperBowl" + i)), "Cooking reveals each real table bowl"); Serve(app, i, true); }
        Check(game.SupperServedCount == 3 && !game.SupperComplete, "Serving three tables still requires returning to the chef");
        Interact(app, "cook", true); Choose(app, "supper_menu"); Choose(app, "supper_finish");
        Check(game.SupperComplete && game.save.inventory[6] == items[6] + 2 && game.save.tea == tea + 1 && game.save.coins == coins, "Only the final real chef interaction pays two stew and one tea once");
        for (int i = 0; i < items.Length; i++) if (i != 6) Check(game.save.inventory[i] == items[i], "Residents share supplies without taking the player's original materials");
        Check(game.save.moments.Exists(m => m.en.IndexOf("hearth", StringComparison.OrdinalIgnoreCase) >= 0), "The shared event leaves an actual journal moment");
        Console.WriteLine("PASS actual town/inn continuous WASD navigation, E and paginated numeric choices, all original cooking/resident prefixes, contributions, three table servings and exact one-time reward; movement frames=" + walkingFrames + "; NATIVE_UNVERIFIED");
    }
    public static void SaveStagesLegacyAndDuplicateDefense(FrontierRuntime app)
    {
        Prepare(app); var game = app.Game; Accept(app); Help(app, 0); Roundtrip(app, 0);
        Check(game.SupperAccepted && game.SupperPreparedCount == 1 && game.Has("supper_runtime_mature_story"), "Accepted/prepared flags and existing story survive actual SaveStore reconstruction");
        Help(app, 1); Help(app, 2); game.Enter(9); Cook(app); Serve(app, 0); Roundtrip(app, 1);
        Check(game.SupperCooked && game.SupperServedCount == 1 && game.IsObjectPresent(Object(game, "supperBowl1")), "Cooked partial table stage survives real inn snapshot restore");
        int stew = game.save.inventory[6], tea = game.save.tea; Serve(app, 1); Serve(app, 2); Finish(app); Roundtrip(app, 2);
        Check(game.SupperComplete && game.save.inventory[6] == stew + 2 && game.save.tea == tea + 1, "Actual completed envelope preserves exact final payout");
        for (int i = 0; i < 3; i++) Replay(game, "supper_serve_" + i); Replay(game, "supper_finish"); Replay(game, "supper_cook"); Replay(game, "supper_accept");
        Check(game.SupperComplete && game.save.inventory[6] == stew + 2 && game.save.tea == tea + 1, "Replayed obsolete choices cannot duplicate payout or reset the story");
        foreach (int version in new[] { 1, 2 })
        {
            Prepare(app); game = app.Game; var old = Copy(game.save); old.version = version; old.flags.RemoveAll(f => f.StartsWith("supper_")); old.flags.Add("homeward_complete"); old.flags.Add("shelter_complete"); old.coins = 137;
            File.WriteAllText(SaveStore.PathFor(2), JsonUtility.ToJson(old)); string warning; var loaded = SaveStore.Read(2, out warning); Check(loaded != null && warning == null, "Actual raw legacy JSON loads v" + version); Load(app, loaded);
            Check(game.save.coins == 137 && game.Has("homeward_complete") && game.Has("shelter_complete") && game.SupperReady && !game.SupperAccepted && !game.SupperCooked && !game.SupperComplete, "Legacy earned allies unlock the new optional event without invented progress or lost mature stories");
        }
        Console.WriteLine("PASS actual checksum envelopes across accepted/prepared/cooked/served/complete stages, obsolete-action duplicate protection and raw legacy v1/v2 story/economy retention; NATIVE_UNVERIFIED");
    }
    public static void LockedRemotePauseAndQuestNavigation(FrontierRuntime app)
    {
        Prepare(app, 0, false); var game = app.Game; Check(!game.SupperReady && game.GetQuest("supper").locked, "Missing original resident commissions lock the optional shared supper");
        int stew = game.save.inventory[6], tea = game.save.tea; Interact(app, "cook"); Check(Array.FindIndex(game.conversation.choices, c => c.action == "supper_menu") < 0, "Locked original chef menu does not invent the event"); game.Close();
        Replay(game, "supper_accept"); Check(!game.SupperAccepted && game.save.inventory[6] == stew && game.save.tea == tea, "Forged locked accept cannot mutate story or economy");
        Prepare(app); game = app.Game; Accept(app); game.TrackQuest("supper"); Check(game.CurrentGuide.id == "supper" && game.CurrentGuide.room == 0 && game.NavigationTarget().HasValue && game.NavigationPath(game.NavigationTarget().Value).Count > 0, "Tracked event has an actual walkable town destination");
        Position(app, new Point(18, 19)); Replay(game, "supper_help_0"); Check(game.SupperPreparedCount == 0, "Stale remote resident action cannot contribute");
        Interact(app, "garden"); Choose(app, "supper_prep_0"); game.save.room = 9; Choose(app, "supper_help_0"); Check(game.SupperPreparedCount == 0, "Room changed during a dialogue cannot commit a stale contribution"); game.Enter(0);
        Help(app, 0); Help(app, 1); Help(app, 2); Check(game.GetQuest("supper").room == 9 && game.NavigationTarget().HasValue, "After contributions the guide routes through the actual inn portal");
        var townPortal = Object(game, "portal9"); Check((game.NavigationTarget().Value - new Point(townPortal.x, townPortal.y)).Length < .001f, "The town guide targets the authored inn doorway rather than an unreachable indoor chef");
        game.Enter(9); Cook(app); Key(app, KeyCode.Escape); Check(game.mode == "pause", "Actual Escape pauses the ongoing social event"); int prepared = game.SupperPreparedCount, served = game.SupperServedCount; float time = game.save.totalTime;
        for (int i = 0; i < 50; i++) Update(app); Check(game.SupperPreparedCount == prepared && game.SupperServedCount == served && game.save.totalTime == time, "Real pause advances neither the social stage nor world time"); Key(app, KeyCode.Escape);
        Call(app, "OnApplicationPause", true); time = game.save.totalTime; for (int i = 0; i < 30; i++) Update(app); Check(game.save.totalTime == time && game.SupperServedCount == served, "Application pause preserves unfinished bowls"); Call(app, "OnApplicationPause", false);
        for (int i = 0; i < 3; i++)
        {
            var q = game.GetQuest("supper"); var bowl = Object(game, "supperBowl" + i); Check(q.room == 9 && (q.target - new Point(bowl.x, bowl.y)).Length < .001f && game.NavigationPath(q.target).Count > 0, "Quest guide advances to each actual unserved table " + i); Serve(app, i);
        }
        Check((game.GetQuest("supper").target - new Point(12, 10)).Length < .001f, "After all bowls the guide returns to the actual inn chef"); Finish(app);
        Check(game.GetQuest("supper").complete && !game.NavigationTarget().HasValue, "Completed tracked story does not keep a false gold route");
        Console.WriteLine("PASS prerequisite lock, forged/remote/changed-room action rejection, real pause/application pause, actual town-to-inn and table-by-table quest navigation with no stale completion target; NATIVE_UNVERIFIED");
    }
    public static void BilingualNarrativeBowlsAndJournal(FrontierRuntime app)
    {
        foreach (bool english in new[] { false, true }) foreach (bool large in new[] { false, true })
        {
            Prepare(app); var game = app.Game; game.save.english = english; game.save.largeText = large; string suffix = (english ? "en" : "zh") + (large ? "-large" : "-normal");
            Accept(app, false, "accept-" + suffix); Help(app, 0, false, "an-" + suffix); Help(app, 1, false, "tide-" + suffix); Help(app, 2, false, "noll-" + suffix);
            game.TrackQuest("supper"); Key(app, KeyCode.Tab); Check(game.mode == "journal", "Actual Tab opens the complete adventure journal"); Draw(app);
            Check(GameCore.QuestIds.Length >= 19 && Array.IndexOf(GameCore.QuestIds, "supper") == 18, "Social story keeps its nineteenth position without removing or renumbering mature quest rows as later stories add pages");
            const int pageSize = 19; int pages = (GameCore.QuestIds.Length + pageSize - 1) / pageSize, checkedRows = 0;
            while ((int)typeof(FrontierRuntime).GetField("selectedQuest", Private).GetValue(app) / pageSize > 0) { GUI.queuedClick = new Rect(650, 699, 258, 26); Draw(app); Check(!GUI.queuedClick.HasValue, "Actual previous-page control returns to the mature story list"); }
            for (int page = 0; page < pages; page++)
            {
                Draw(app); int count = Math.Min(pageSize, GameCore.QuestIds.Length - page * pageSize);
                var visibleRows = GUI.buttonCalls.FindAll(b => b.rect.x == 279 && b.rect.width == 321 && b.rect.y >= 226 && b.rect.y < 710);
                Check(visibleRows.Count == count, "Every page draws exactly its current quests");
                for (int cell = 0; cell < count; cell++)
                {
                    int index = page * pageSize + cell; string id = GameCore.QuestIds[index], title = game.GetQuest(id).title;
                    Check(HasText(title) || HasText("◆ " + title) || HasText("✓ " + title), "Actual visible journal page retains the full label for " + id);
                    Check(visibleRows[cell].enabled, "Every visible journal row remains selectable: " + id); GUI.queuedClick = visibleRows[cell].rect; Draw(app);
                    Check(!GUI.queuedClick.HasValue && (int)typeof(FrontierRuntime).GetField("selectedQuest", Private).GetValue(app) == index, "Actual page row clicks select the correct current quest: " + id); checkedRows++;
                }
                if (page + 1 < pages) { GUI.queuedClick = new Rect(927, 699, 263, 26); Draw(app); Check(!GUI.queuedClick.HasValue && (int)typeof(FrontierRuntime).GetField("selectedQuest", Private).GetValue(app) == (page + 1) * pageSize, "Actual next-page control exposes appended quest labels"); }
            }
            Check(checkedRows == GameCore.QuestIds.Length, "Every current quest keeps its full label and an actual clickable row across visible pages");
            while ((int)typeof(FrontierRuntime).GetField("selectedQuest", Private).GetValue(app) / pageSize > 0) { GUI.queuedClick = new Rect(650, 699, 258, 26); Draw(app); Check(!GUI.queuedClick.HasValue, "Return through the actual previous-page control before testing mature geometry"); }
            Draw(app);
            Check(GUI.buttonCalls.FindAll(b => b.rect.x == 279 && b.rect.width == 321 && b.rect.y >= 226 && b.rect.y < 710).Count == 19, "All nineteen displayed quest rows retain actual clickable button bounds");
            float rowPitch = 464f / 18;
            for (int i = 0; i < 18; i++)
            {
                var row = GUI.buttonCalls.Find(b => b.rect.x == 279 && b.rect.width == 321 && Math.Abs(b.rect.y - (226 + i * rowPitch)) < .01f);
                Check(row != null && Math.Abs(row.rect.height - (rowPitch - 7)) < .01f && row.rect.height >= row.style.fontSize && row.rect.y + row.rect.height < 733,
                    "The old eighteen quest rows keep their exact full-glyph geometry, and the appended row fits before the footer");
            }
            var supperRow = GUI.buttonCalls.FindAll(b => b.rect.x == 279 && b.rect.width == 321 && b.rect.y >= 226 && b.rect.y < 710)[18];
            Check(supperRow.rect.y + supperRow.rect.height <= 706 && Math.Abs(supperRow.rect.height - (rowPitch - 7)) < .01f,
                "The appended row keeps full height and stays entirely above the actual menu toast region");
            GUI.queuedClick = supperRow.rect; Draw(app, "journal-" + suffix);
            Check(!GUI.queuedClick.HasValue && (int)typeof(FrontierRuntime).GetField("selectedQuest", Private).GetValue(app) == 18 && HasText(game.GetQuest("supper").title),
                "The actual appended row accepts a real GUI click and displays the supper details");
            Key(app, KeyCode.Escape); game.Enter(9); for (int i = 0; i < 3; i++) BowlDrawing(app, i, false);
            Cook(app, "cook-" + suffix); Position(app, new Point(18, 15)); Draw(app, "tables-" + suffix);
            for (int i = 0; i < 3; i++) { Check(HasText(game.L("桌边 ", "TABLE PLACE ") + (i + 1)), "Actual OnGUI draws every visible table before serving"); }
            for (int i = 0; i < 3; i++) { BowlDrawing(app, i, true); Serve(app, i, false, "table" + i + "-" + suffix); BowlDrawing(app, i, true); }
            Finish(app, "finish-" + suffix); Draw(app, "complete-" + suffix); Check(game.SupperComplete, "All localized narrative/input variants can complete the actual event");
        }
        Console.WriteLine("PASS zh/en normal/large 960px actual paginated invitation, all resident and table narratives, low-FX world bowls, complete paginated journal with preserved nineteen mature rows and completion GUI; captures=" + captureCount + "; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        captures = args.Length > 0 && args[0] != "-" ? args[0] : null; string previous = Application.persistentDataPath;
        string directory = Path.Combine(Path.GetTempPath(), "starfall-supper-" + Guid.NewGuid()); Application.persistentDataPath = directory; var app = new FrontierRuntime(); int failed = 0;
        try
        {
            Directory.CreateDirectory(directory); Call(app, "Awake"); Check(app.Game != null, "Production Awake loads the authored campaign");
            foreach (Action<FrontierRuntime> test in new Action<FrontierRuntime>[] { ActualWalkInputsAndPreservedMenus, SaveStagesLegacyAndDuplicateDefense, LockedRemotePauseAndQuestNavigation, BilingualNarrativeBowlsAndJournal })
                try { test(app); } catch (Exception error) { failed++; Console.WriteLine("FAIL " + test.Method.Name + ": " + (error.InnerException ?? error)); }
            app.EndVisualReview(); Call(app, "OnDestroy");
        }
        finally { Application.persistentDataPath = previous; Input.Clear(); GUI.queuedClick = null; GUI.matrix = Matrix4x4.identity; GUI.color = Color.white; GUI.enabled = true; if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        Console.WriteLine("Runtime Shared Supper: " + (4 - failed) + "/4 groups passed; NATIVE_UNVERIFIED"); return failed == 0 ? 0 : 1;
    }
}
