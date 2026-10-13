// Production E/numbered dialogue, OnGUI and SaveStore checks against offline
// Unity declarations. Native import, font rendering and playback remain unrun.
using System;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeGuildStoryChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string captureFolder;
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static object Call(FrontierRuntime app, string method, params object[] args)
    { return typeof(FrontierRuntime).GetMethod(method, Private).Invoke(app, args); }
    private static void Set(FrontierRuntime app, string field, object value)
    { typeof(FrontierRuntime).GetField(field, Private).SetValue(app, value); }
    private static void Draw(FrontierRuntime app, string capture = null)
    {
        GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app, "OnGUI");
        if (captureFolder != null && capture != null)
        {
            var commands = GUI.commands.ToArray(); var textures = GUI.textureCalls.ToArray(); var buttons = GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder, "qa-guild-" + capture);
            GUI.commands.AddRange(commands); GUI.textureCalls.AddRange(textures); GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static bool HasText(string value)
    { return GUI.commands.Exists(c => c.Contains("\"text\":" + GUI.S(value))); }
    private static void Key(FrontierRuntime app, KeyCode key)
    { Input.keys.Add(key); Call(app, "Update"); Input.keys.Clear(); }
    private static void Position(FrontierRuntime app, Point point)
    { Check(app.SetVisualReviewPosition(point), "The dialogue fixture uses actual walkable ground"); Set(app, "camera", app.Game.Position); }
    private static WorldObject Object(GameCore g, string kind)
    {
        WorldObject o = Array.Find(g.Map.objects, x => x.kind == kind);
        Check(o != null, "The mature campaign retains its actual interaction: " + kind); return o;
    }
    private static void Interact(FrontierRuntime app, string kind, bool pad = false)
    {
        var o = Object(app.Game, kind); Position(app, new Point(o.x, o.y - .7f));
        Check(app.Game.Nearest() == o, "Actual E/B is nearest the intended shipped " + kind + " object");
        Key(app, pad ? KeyCode.JoystickButton1 : KeyCode.E);
    }
    private static int ReadPages(FrontierRuntime app, string capture = null, KeyCode turn = KeyCode.Return)
    {
        var g = app.Game; Check(g.mode == "dialog" && g.conversation != null, "Actual narrative pages belong to a displayed dialogue");
        var pages = DialogueLayout.Pages(g.L(g.conversation.text, g.conversation.textEn), g.save.largeText);
        int page = 0;
        do
        {
            Draw(app, capture == null ? null : capture + "-page" + page);
            Check(HasText(pages[page]), "Actual DrawDialogue renders the entire authored page without substituting a summary");
            Check(GUI.buttonCalls.Exists(b => b.enabled), "A displayed narrative always leaves an enabled reading or choice control");
            if ((bool)Call(app, "DialogueChoicesVisible")) break;
            Check(page < pages.Count - 1, "Only the final narrative page may expose its choices");
            Key(app, turn); page++;
        } while (page < 30);
        Check(page == pages.Count - 1 && (bool)Call(app, "DialogueChoicesVisible"),
            "Real Return/Space/A reaches every authored page before exposing numbered choices");
        return pages.Count;
    }
    private static void Choose(FrontierRuntime app, string action, string capture = null)
    {
        var g = app.Game; Check(g.mode == "dialog" && g.conversation != null, "A numbered guild choice belongs to the actual conversation");
        int index = Array.FindIndex(g.conversation.choices, c => c.action == action);
        Check(index >= 0 && index < 9, "The actual dialogue exposes the numbered choice " + action);
        ReadPages(app, capture); Key(app, (KeyCode)((int)KeyCode.Alpha1 + index));
    }
    private static void Prepare(FrontierRuntime app, int stage = 0, bool english = true, bool large = false)
    {
        Input.Clear(); GUI.queuedClick = null; Screen.width = 1280; Screen.height = 800; Time.unscaledDeltaTime = .02f;
        app.BeginVisualReview(0, 0); app.VisualReviewPaused = false; var g = app.Game;
        g.enemies.Clear(); g.save.stage = stage; g.save.ending = 0; g.save.companion = -1; g.save.reducedMotion = true;
        g.save.english = english; g.save.largeText = large; g.save.coins = 313; g.save.xp = 9; g.save.shards = 17; g.save.skillPoints = 2;
        g.save.health = 58; g.save.energy = 42; g.save.totalTime = 100; g.save.frontierFocus = false;
        g.save.flags.RemoveAll(f => f.StartsWith("core_") || f.StartsWith("lore_") || f.StartsWith("history_") || f.StartsWith("guild_"));
        g.save.flags.Add("guild_runtime_mature_story"); g.invulnerable = 0;
        Set(app, "applicationPaused", false); Set(app, "pointerSeen", false); Set(app, "controllerUsed", false);
        Set(app, "combatHudTime", 0f); Set(app, "resumeTime", 0f); Call(app, "Resize");
    }
    private static void OriginalChoices(GameCore g, params string[] actions)
    {
        Check(g.conversation != null && g.conversation.choices.Length >= actions.Length, "The original guild choices remain available");
        for (int i = 0; i < actions.Length; i++) Check(g.conversation.choices[i].action == actions[i],
            "The original guild choice keeps its number: " + actions[i]);
    }
    private sealed class Economy
    {
        private readonly int coins, xp, shards, points; private readonly int[] inventory, mastery, kills;
        public Economy(GameCore g)
        { coins = g.save.coins; xp = g.save.xp; shards = g.save.shards; points = g.save.skillPoints; inventory = (int[])g.save.inventory.Clone(); mastery = (int[])g.save.mastery.Clone(); kills = (int[])g.save.kills.Clone(); }
        public void Unchanged(GameCore g, string context)
        {
            Check(g.save.coins == coins && g.save.xp == xp && g.save.shards == shards && g.save.skillPoints == points, context + " grants no currency, XP, shards or talent points");
            for (int i = 0; i < inventory.Length; i++) Check(g.save.inventory[i] == inventory[i], context + " preserves every material and consumable");
            for (int i = 0; i < mastery.Length; i++) Check(g.save.mastery[i] == mastery[i], context + " preserves weapon mastery");
            for (int i = 0; i < kills.Length; i++) Check(g.save.kills[i] == kills[i], context + " preserves map and boss kill progress");
        }
    }
    public static void GuildChapterInputsRoutesAndOriginalRewards(FrontierRuntime app)
    {
        for (int language = 0; language < 2; language++)
        {
            Prepare(app, 0, language == 1); var g = app.Game;
            for (int stage = 0; stage < 4; stage++)
            {
                Interact(app, "guild", stage % 2 == 1); OriginalChoices(g, "routes", "close");
                Check(g.conversation.choices[2].action == "guild_evidence" && g.conversation.choices.Length == (stage == 3 ? 4 : 3),
                    "Evidence appends Alpha3 and the earned letter appends Alpha4 without replacing route or departure");
                string destination = stage == 3 ? g.L("厄洛", "Orrin") : g.L(g.data.maps[stage + 1].name, g.data.maps[stage + 1].en);
                Check(g.L(g.conversation.text, g.conversation.textEn).IndexOf(destination, StringComparison.OrdinalIgnoreCase) >= 0,
                    "The actual chapter departure names its current next region, not an obsolete forest route");
                Choose(app, "routes", "chapter" + stage + "-routes-" + language);
                Check(g.mode == "map", "Original Alpha1 route selection still opens the actual map"); Key(app, KeyCode.Escape);
                Interact(app, "guild"); Choose(app, "close"); Check(g.mode == "world", "Original Alpha2 leaves the guild without a new story gate");
                if (stage == 3) break;
                g.save.flags.Add("core_" + (stage + 1)); Interact(app, "guild"); OriginalChoices(g, "deliver", "close");
                int coins = g.save.coins, stew = g.save.inventory[6], timber = g.save.inventory[0], crystals = g.save.inventory[1], moments = g.save.moments.Count;
                Choose(app, "deliver", "chapter" + stage + "-delivery-" + language);
                Check(g.mode == "world" && g.save.stage == stage + 1 && g.save.coins == coins + 40 && g.save.inventory[6] == stew + 1
                    && g.save.inventory[0] == timber + 3 && g.save.inventory[1] == crystals + 2,
                    "Actual numbered delivery retains the mature immediate world transition and exact 40 coins/one stew/three wood/two crystals");
                Check(g.Has("guild_delivery_" + (stage + 1)) && g.save.moments.Count == moments + 1,
                    "The delivered chapter leaves exactly one truthful journey record");
                Check(g.toast.Contains(g.L(g.data.maps[stage + 2].name, g.data.maps[stage + 2].en)), "The real delivery notification tells the player which road opened");
                Draw(app, "chapter" + stage + "-delivered-" + language);
                var economy = new Economy(g); Interact(app, "guild"); Choose(app, "close"); economy.Unchanged(g, "Revisiting a delivered chapter");
            }
        }
        Console.WriteLine("PASS guild actual E/B and numbered route/departure/delivery, all four accurate chapter briefings, exact mature rewards/world mode, and once-only journey records in Chinese/English; NATIVE_UNVERIFIED");
    }
    public static void GuildActualLoreTruthAndLegacyAliases(FrontierRuntime app)
    {
        Prepare(app, 3); var g = app.Game; Interact(app, "guild"); Choose(app, "guild_evidence", "missing");
        Check(g.GuildEvidenceCount == 0 && !g.CanReviewGuildEvidence && g.conversation.choices.Length == 1, "Three missing records cannot fabricate a conclusion choice");
        for (int i = 0; i < 3; i++) Check(g.conversation.textEn.Contains(g.data.maps[i + 1].en) && !g.conversation.textEn.Contains(g.data.lore[i].text),
            "A missing record names its actual region while withholding its undiscovered source text"); Choose(app, "close");
        for (int room = 1; room <= 3; room++)
        {
            g.Enter(room); g.enemies.Clear(); Interact(app, "lore");
            Check(g.Has("lore_" + room) && g.GuildEvidenceFound(room - 1) && g.GuildEvidenceCount == room,
                "Actual E at the shipped region relic supplies its existing one-based discovery flag to the guild");
            Choose(app, "close", "relic" + room); g.Enter(0); Interact(app, "guild"); Choose(app, "guild_evidence", "evidence" + room);
            for (int i = 0; i < 3; i++) Check(g.conversation.textEn.Contains(g.data.lore[i].text) == (i < room),
                "The guild's source text matches only the discoveries the player really made");
            Check(g.CanReviewGuildEvidence == (room == 3), "Conclusion remains optional and only becomes available after all three records and chapters");
            if (room < 3) Choose(app, "close");
        }
        var economy = new Economy(g); int moments = g.save.moments.Count; Choose(app, "guild_evidence_record", "full-review");
        Check(g.GuildEvidenceReviewed && g.save.moments.Count == moments + 1 && g.mode == "dialog", "The real final-page choice records one conclusion and opens Lyra's response");
        Choose(app, "close", "response"); economy.Unchanged(g, "The optional three-record conclusion");
        Interact(app, "guild"); Choose(app, "guild_evidence"); Choose(app, "guild_evidence_record"); Choose(app, "close");
        Check(g.save.moments.Count == moments + 1, "Re-reading the real guild response cannot duplicate the journey moment"); economy.Unchanged(g, "Repeated review");
        g.save.flags.RemoveAll(f => f.StartsWith("lore_"));
        for (int i = 0; i < 3; i++) g.save.flags.Add("history_" + i);
        Interact(app, "guild"); Choose(app, "guild_evidence", "legacy-history-aliases");
        Check(g.GuildEvidenceCount == 3 && g.CanReviewGuildEvidence, "Existing zero-based codex history aliases count as the same recovered records");
        for (int i = 0; i < 3; i++) Check(g.conversation.textEn.Contains(g.data.lore[i].text), "Legacy codex discovery displays its retained source text"); Choose(app, "close");
        Console.WriteLine("PASS guild production relic E discoveries, missing-location truth without spoilers, one-based lore/zero-based history compatibility, optional complete review and once-only no-economy response; NATIVE_UNVERIFIED");
    }
    public static void GuildPaginationLetterGuiAndMatureEndings(FrontierRuntime app)
    {
        for (int language = 0; language < 2; language++) for (int large = 0; large < 2; large++) for (int policy = 0; policy < 2; policy++)
        {
            Prepare(app, 3, language == 1, large == 1); var g = app.Game;
            Screen.width = policy == 0 ? 960 : 1920; Screen.height = policy == 0 ? 600 : 1080; Call(app, "Resize");
            string suffix = (language == 1 ? "-en" : "-zh") + (large == 1 ? "-large" : "") + (policy == 0 ? "-960" : "-1920");
            for (int i = 1; i <= 3; i++) g.save.flags.Add("lore_" + i);
            g.save.flags.Add("history_10"); g.save.flags.Add("history_16"); g.save.flags.Add("scout_saved");
            var economy = new Economy(g); int moments = g.save.moments.Count;
            Interact(app, "guild"); Choose(app, "guild_evidence", "menu" + suffix); Draw(app, "full-evidence-first" + suffix);
            var conversation = g.conversation; var pages = DialogueLayout.Pages(g.L(conversation.text, conversation.textEn), g.save.largeText);
            Check(pages.Count > 1 && !(bool)Call(app, "DialogueChoicesVisible"), "The complete evidence trail uses real narrative pages before its choice row");
            Key(app, KeyCode.Alpha1); Check(g.conversation == conversation && !g.GuildEvidenceReviewed && g.save.moments.Count == moments,
                "A premature numeric key cannot record a response before the full evidence is displayed");
            Check(ReadPages(app, "full-evidence" + suffix, KeyCode.Space) == pages.Count, "Actual Space retains the same complete paginated trail");
            Key(app, KeyCode.Alpha1); Check(g.GuildEvidenceReviewed, "Alpha1 on the real last page records the available evidence response");
            Choose(app, "close", "guild-response" + suffix); Interact(app, "guild"); Choose(app, "guild_letter", "letter-menu" + suffix);
            Check(g.conversation.textEn.Contains("note in the bark") && g.conversation.textEn.Contains("inn ledger") && g.conversation.textEn.Contains("scout") && g.conversation.textEn.Contains("home"),
                "The letter acknowledges actual patrol note, inn ledger and scout discoveries");
            var letterPages = DialogueLayout.Pages(g.L(g.conversation.text, g.conversation.textEn), g.save.largeText);
            Check(letterPages[letterPages.Count - 1].Trim().Length >= 20,
                "The final reply page contains meaningful narrative rather than an orphan trailing word");
            ReadPages(app, "letter" + suffix, KeyCode.JoystickButton0); Key(app, KeyCode.Alpha1);
            Check(g.mode == "world" && g.GuildLetterReviewed && g.save.moments.Count == moments + 2,
                "Actual A page turns and the final numbered acknowledgment record the reply without forcing travel or combat");
            Interact(app, "guild"); Choose(app, "guild_letter"); Choose(app, "guild_letter_record");
            Check(g.save.moments.Count == moments + 2, "The real letter can be reread without repeating its personal record"); economy.Unchanged(g, "Both story acknowledgments and their re-readings");
            for (int ending = 1; ending <= 3; ending++)
            {
                g.save.ending = ending; Interact(app, "guild"); OriginalChoices(g, "routes", "epilogue", "close");
                Check(g.conversation.choices.Length == 5 && g.conversation.choices[3].action == "guild_evidence" && g.conversation.choices[4].action == "guild_letter",
                    "All mature endings retain route, epilogue and goodbye first, before optional story re-readings");
                Choose(app, "epilogue", "ending" + ending + "-guild" + suffix);
                Check(g.mode == "dialog" && Array.Exists(g.conversation.choices, c => c.action == "continue"), "Original Alpha2 still opens the selected mature ending with its continuation");
                Choose(app, "continue", "ending" + ending + "-epilogue" + suffix); Check(g.save.ending == ending && g.mode == "world" && g.save.room == 0,
                    "The actual ending continuation returns to Town and keeps the player's chosen ending");
                Interact(app, "guild"); Choose(app, "guild_letter", "ending" + ending + "-letter-menu" + suffix);
                Check(g.conversation.textEn.Contains("after the crisis"), "Post-ending letter adds its earned epilogue rather than replacing the original outcome");
                letterPages = DialogueLayout.Pages(g.L(g.conversation.text, g.conversation.textEn), g.save.largeText);
                Check(letterPages[letterPages.Count - 1].Trim().Length >= 20,
                    "Each mature ending's reply retains a meaningful final page rather than an orphan trailing word");
                Choose(app, "guild_letter_record", "ending" + ending + "-letter" + suffix);
            }
            economy.Unchanged(g, "Original ending and personal letter re-readings");
        }
        Console.WriteLine("PASS guild actual Return/Space/A pagination and premature-number guard, evidence/letter controls and earned patrol callbacks, all original ending continuations, and Chinese/English ordinary/large text at 960/1920; NATIVE_UNVERIFIED");
    }
    public static void GuildSaveStoreLegacyAndRepeatBoundaries(FrontierRuntime app)
    {
        Prepare(app, 3); var g = app.Game;
        for (int i = 0; i < 3; i++) g.save.flags.Add("history_" + i);
        Interact(app, "guild"); Choose(app, "guild_evidence"); Choose(app, "guild_evidence_record"); Choose(app, "close");
        Interact(app, "guild"); Choose(app, "guild_letter"); Choose(app, "guild_letter_record");
        var economy = new Economy(g); int moments = g.save.moments.Count; g.PrepareSave();
        Check(SaveStore.Write(g.save, 0), "The real checksum envelope writes established flag-based story progress"); string issue;
        var saved = SaveStore.Read(0, out issue);
        Check(saved != null && issue == null && saved.flags.Contains("guild_evidence_reviewed") && saved.flags.Contains("guild_letter_reviewed"),
            "The production serializer/checksum preserves both optional story records");
        g.Load(saved); g.Start(); Interact(app, "guild"); Choose(app, "guild_evidence"); Choose(app, "guild_evidence_record"); Choose(app, "close");
        Interact(app, "guild"); Choose(app, "guild_letter"); Choose(app, "guild_letter_record");
        Check(g.GuildEvidenceReviewed && g.GuildLetterReviewed && g.save.moments.Count == moments && g.Has("guild_runtime_mature_story"),
            "Loading and re-reading retains established story without new moments or duplicate rewards"); economy.Unchanged(g, "Saved review repetitions");
        Prepare(app, 3); g = app.Game; g.save.version = 1;
        Check(!g.Has("core_1") && !g.Has("core_2") && !g.Has("core_3"), "The legacy fixture has delivered chapters but no retrospective delivery/core flags");
        string legacy = JsonUtility.ToJson(g.save); Check(!legacy.Contains("guild_letter_reviewed") && !legacy.Contains("guild_evidence_reviewed"),
            "The old payload actually predates the optional story flags");
        Directory.CreateDirectory(Application.persistentDataPath); File.WriteAllText(SaveStore.PathFor(1), legacy);
        saved = SaveStore.Read(1, out issue); Check(saved != null && issue == null, "The production raw version-one decoder accepts the mature legacy save");
        g.Load(saved); g.Start(); var oldEconomy = new Economy(g); int oldMoments = g.save.moments.Count;
        Interact(app, "guild"); OriginalChoices(g, "routes", "close"); Choose(app, "guild_letter", "legacy-letter");
        Check(!g.conversation.textEn.Contains("note in the bark") && !g.conversation.textEn.Contains("inn ledger") && !g.conversation.textEn.Contains("scout"),
            "A legacy letter does not invent optional discoveries that the player never made");
        Choose(app, "guild_letter_record"); Check(g.GuildLetterReviewed && g.save.stage == 3 && g.save.moments.Count == oldMoments + 1,
            "Earned legacy stage alone unlocks one personal reply without requiring retroactive flags or a replayed chapter"); oldEconomy.Unchanged(g, "Legacy story acknowledgment");
        Interact(app, "guild"); Choose(app, "guild_evidence", "legacy-missing-evidence");
        Check(g.GuildEvidenceCount == 0 && !g.GuildEvidenceReviewed && g.conversation.choices.Length == 1,
            "Delivered legacy chapters do not imply discovery of optional records"); Choose(app, "close");
        Console.WriteLine("PASS guild production SaveStore/checksum, both established story flags and once-only moments, no-economy re-read after load, raw version-one delivered-stage compatibility and truthful missing optional callbacks; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous = Application.persistentDataPath; string directory = Path.Combine(Path.GetTempPath(), "starfall-guild-check-" + Guid.NewGuid());
        Application.persistentDataPath = directory; captureFolder = args.Length > 0 ? args[0] : null;
        if (captureFolder != null) Directory.CreateDirectory(captureFolder);
        var app = new FrontierRuntime(); int failed = 0;
        Action<FrontierRuntime>[] tests = { GuildChapterInputsRoutesAndOriginalRewards, GuildActualLoreTruthAndLegacyAliases,
            GuildPaginationLetterGuiAndMatureEndings, GuildSaveStoreLegacyAndRepeatBoundaries };
        try
        {
            Call(app, "Awake"); Check(app.Game != null, "The actual runtime Awake loads the shipped complete campaign");
            foreach (var test in tests)
            {
                try { test(app); }
                catch (Exception error) { failed++; Console.WriteLine("FAIL " + test.Method.Name + ": " + (error.InnerException ?? error).Message); }
            }
            app.EndVisualReview(); Call(app, "OnDestroy");
        }
        finally { Application.persistentDataPath = previous; Input.Clear(); GUI.queuedClick = null; if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        Console.WriteLine("Runtime guild evidence trail: " + (tests.Length - failed) + "/" + tests.Length + " groups passed; NATIVE_UNVERIFIED"); return failed == 0 ? 0 : 1;
    }
}
