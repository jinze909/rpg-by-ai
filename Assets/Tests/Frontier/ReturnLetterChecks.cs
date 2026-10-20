using System;
using System.Collections.Generic;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static readonly Dictionary<string, SaveData> returnLetterEarnedSaves = new Dictionary<string, SaveData>();
        private static GameCore LetterGame(Campaign data, bool family = true)
        {
            string route = family ? "archive_family" : "archive_public";
            SaveData earned;
            if (!returnLetterEarnedSaves.TryGetValue(route, out earned))
            {
                var original = ArchiveGame(data, family ? 1 : 0); ArchiveVent(original); ArchiveReadAll(original);
                Combat(original, false); ArchiveVisit(original, "archiveKeeper"); ArchiveChoose(original, route);
                Check(original.ArchiveComplete && original.ArchiveRecordsRecovered && original.ArchiveGuardsCleared,
                    "Return-letter fixtures earn the real original archive completion before saving");
                original.Close(); original.save.flags.Add("exp_intro"); original.save.flags.Add("boss_5");
                original.Enter(5); original.enemies.Clear(); LetterVisit(original, "captain"); LetterChoose(original, "harbor_report");
                Check(original.Has("harbor_report") && original.Has("companion_0"), "The existing captain files the earned harbor report");
                original.Close(); original.Enter(0); original.save.companion = -1; original.save.reducedMotion = true;
                original.PrepareSave(); earned = CloneSave(original.save); returnLetterEarnedSaves.Add(route, earned);
            }
            var g = new GameCore(data, CloneSave(earned)); g.Start(); g.Close(); g.enemies.Clear();
            Check(g.ReturnLetterReady && !g.ReturnLetterAccepted && g.ArchiveComplete && g.Has(route),
                "Authentic saved archive endings unlock a separate, unaccepted follow-up"); return g;
        }
        private static WorldObject LetterStand(GameCore g, string kind)
        {
            g.Close(); WorldObject o = Object(g, kind);
            foreach (Point offset in new[] { new Point(0, -.7f), new Point(0, .7f), new Point(-.7f, 0), new Point(.7f, 0) })
            {
                Point position = new Point(o.x, o.y) + offset;
                if (!g.CanStand(position)) continue;
                g.save.x = position.x; g.save.y = position.y;
                if (ReferenceEquals(g.Nearest(), o)) return o;
            }
            throw new Exception("No legal nearest-object standing approach for " + kind);
        }
        private static WorldObject LetterVisit(GameCore g, string kind)
        { WorldObject o = LetterStand(g, kind); g.Interact(); return o; }
        private static int LetterChoice(GameCore g, string action)
        {
            Check(g.mode == "dialog" && g.conversation != null, "Return-letter actions originate in real conversations: " + action);
            int index = Array.FindIndex(g.conversation.choices, c => c.action == action);
            Check(index >= 0, "Missing earned return-letter choice " + action); return index;
        }
        private static void LetterChoose(GameCore g, string action) { g.Choose(LetterChoice(g, action)); }
        private static void LetterCallback(GameCore g, string action)
        {
            g.conversation = new Conversation("stale callback", "stale callback", "", "", new Choice("", "", action));
            g.mode = "dialog"; g.Choose(0);
        }
        private static void LetterAccept(GameCore g)
        {
            if (g.save.room != 0) g.Enter(0); g.enemies.Clear();
            LetterVisit(g, "envoy"); LetterChoose(g, "returnletter_offer"); LetterChoose(g, "returnletter_accept");
            Check(g.ReturnLetterAccepted && g.save.trackedQuest == "returnletter", "Lune's actual local acceptance tracks the new journey");
            LetterVisit(g, "guild"); LetterChoose(g, "returnletter_review"); LetterChoose(g, "returnletter_check");
            Check(g.ReturnLetterChecked, "Lyra verifies the three actually recovered originals");
        }
        private static void LetterWitnesses(GameCore g)
        {
            LetterAccept(g); g.Enter(5); g.enemies.Clear();
            LetterVisit(g, "sluice"); LetterChoose(g, "holdtide");
            foreach (int id in new[] { 0, 1, 2 })
            {
                LetterVisit(g, "castaway" + id);
                Check(g.conversation.choices[0].action == (g.Has("rescue_" + id) ? "close" : "rescue" + id) && g.conversation.choices[1].action == "close",
                    "The old rescue and leave actions retain their first two positions for passenger " + id);
                if (!g.Has("rescue_" + id)) LetterChoose(g, "rescue" + id);
                else g.Close();
                Check(g.Has("rescue_" + id), "The existing material-cost rescue remains required for witness " + id);
            }
            LetterVisit(g, "castaway0"); LetterChoose(g, "returnletter_child"); g.Close();
            LetterVisit(g, "castaway1"); LetterChoose(g, "returnletter_ferryman"); g.Close();
            LetterVisit(g, "castaway2"); LetterChoose(g, "returnletter_lampmaker"); g.Close();
            LetterVisit(g, "relay2");
            Check(g.conversation.choices[0].action == (g.Has("relay_2") ? "close" : "relay2") && g.conversation.choices[1].action == "close",
                "The old third-relay repair and leave actions remain in place before the appended handoff option");
            if (!g.Has("relay_2")) LetterChoose(g, "relay2"); else g.Close();
            Check(g.ReturnLetterChild && g.ReturnLetterFerryman && g.Has("rescue_2") && g.Has("relay_2"),
                "The actual child, ferryman, rescued lampmaker and repaired third relay unlock a handoff");
        }
        private static void LetterBegin(GameCore g)
        {
            LetterVisit(g, "relay2"); LetterChoose(g, "returnletter_signal_menu"); LetterChoose(g, "returnletter_begin");
            Check(g.ReturnLetterSignalActive && !g.ReturnLetterSignalConfirming && Math.Abs(g.ReturnLetterSignalRemaining - GameCore.ReturnLetterPrepareSeconds) < .0001f,
                "The live third relay begins a fresh preparation window without falsely delivering");
        }
        private static void LetterDeliver(GameCore g)
        {
            LetterBegin(g); Advance(g, GameCore.ReturnLetterPrepareSeconds + .02f);
            Check(g.ReturnLetterSignalConfirming, "Real accepted Tick frames reach the short-light confirmation phase");
            g.Interact(); Check(g.ReturnLetterSignalDelivered && !g.ReturnLetterSignalActive, "Actual E at the canonical third relay receives its receipt once");
        }

        public static void ReturnLetterEarnedBranchesPreserveOriginalConversations(Campaign data)
        {
            var locked = Fresh(data); locked.SkipTutorial(); locked.save.stage = 2; LetterVisit(locked, "envoy");
            Check(!locked.ReturnLetterReady && !Array.Exists(locked.conversation.choices, c => c.action.StartsWith("returnletter_")),
                "An unearned archive never exposes the follow-up");
            foreach (bool family in new[] { true, false })
            {
                var g = LetterGame(data, family); string branch = family ? "archive_family" : "archive_public";
                LetterVisit(g, "envoy");
                Check(g.conversation.choices.Length == 4 && g.conversation.choices[0].action == "goto5" && g.conversation.choices[1].action == "camp" &&
                    g.conversation.choices[2].action == "close" && g.conversation.choices[3].action == "returnletter_offer",
                    "The old three envoy choices retain their ordering and exact actions before the appended story");
                g.Close(); g.save.flags.Remove("harbor_report"); LetterVisit(g, "envoy");
                Check(!g.ReturnLetterReady && !Array.Exists(g.conversation.choices, c => c.action == "returnletter_offer"), "An archive alone cannot skip the harbor report");
                g.save.flags.Add("harbor_report"); g.Close(); LetterVisit(g, "guild");
                string[] guildChoices = Array.ConvertAll(g.conversation.choices, c => c.action); g.Close();
                SaveData before = CloneSave(g.save); LetterAccept(g);
                Check(g.Has(branch) && !g.Has(family ? "archive_public" : "archive_family") && g.GetQuest("archive").complete &&
                    g.save.coins == before.coins && g.save.shards == before.shards && g.save.skillPoints == before.skillPoints && g.save.stage == before.stage,
                    "Starting a reply neither replaces the original archive route nor repeats its completion payout");
                LetterVisit(g, "guild");
                Check(g.conversation.choices.Length == guildChoices.Length + 1 && g.conversation.choices[guildChoices.Length].action == "returnletter_review",
                    "Lyra appends the original-record review after every mature guild action");
                for (int i = 0; i < guildChoices.Length; i++) Check(g.conversation.choices[i].action == guildChoices[i], "Every original guild choice retains its ordering: " + guildChoices[i]);
            }
        }
        public static void ReturnLetterTwoRoutesCompleteWithLocalWitnessesAndSingleReward(Campaign data)
        {
            foreach (bool family in new[] { true, false })
            {
                var g = LetterGame(data, family); LetterWitnesses(g);
                Check(g.GetQuest("returnletter").room == 5 && !g.GetQuest("returnletter").complete, "Guidance points to the remaining actual relay");
                SaveData before = CloneSave(g.save); LetterDeliver(g);
                Check(g.save.tea == before.tea && g.save.reputation[1] == before.reputation[1] && g.save.coins == before.coins,
                    "The light receipt itself pays no reward or material income");
                g.Enter(0); g.enemies.Clear(); LetterVisit(g, "envoy"); LetterChoose(g, "returnletter_offer"); LetterChoose(g, "returnletter_finish");
                Check(g.ReturnLetterComplete && g.GetQuest("returnletter").complete && g.save.tea == before.tea + 2 &&
                    g.save.reputation[1] == Math.Min(10, before.reputation[1] + 1) && g.save.coins == before.coins && g.save.xp == before.xp &&
                    g.save.shards == before.shards && g.save.skillPoints == before.skillPoints,
                    "The actual Lune receipt awards exactly two teas and one capped Ferrymen trust, with no repeated chapter rewards");
                Check(g.conversation.text.Contains(family ? "家人先核对" : "公会的公开记录") &&
                    g.conversation.textEn.Contains(family ? "Families still verify first" : "public guild copy") &&
                    g.conversation.textEn.Contains(family ? "not the missing people's return" : "no evidence that the missing returned"),
                    "The receipt preserves the chosen archive policy and does not promise missing people's return");
                SaveData paid = CloneSave(g.save); g.Close(); LetterCallback(g, "returnletter_finish"); LetterCallback(g, "returnletter_accept");
                Check(g.save.tea == paid.tea && g.save.reputation[1] == paid.reputation[1] && g.save.coins == paid.coins,
                    "Replayed completion and acceptance callbacks cannot duplicate delivery payment");
            }
        }
        public static void ReturnLetterCallbacksRecheckLocalWorldAndOriginalRecords(Campaign data)
        {
            var g = LetterGame(data); g.save.x = 18; g.save.y = 14; LetterCallback(g, "returnletter_accept");
            Check(!g.ReturnLetterAccepted, "A remote injected acceptance callback cannot begin the letter");
            LetterVisit(g, "envoy"); LetterChoose(g, "returnletter_offer"); int stale = LetterChoice(g, "returnletter_accept");
            g.save.x = 18; g.save.y = 14; g.Choose(stale); Check(!g.ReturnLetterAccepted, "A stale real Lune menu rechecks distance at acceptance");
            LetterVisit(g, "envoy"); LetterChoose(g, "returnletter_offer"); LetterChoose(g, "returnletter_accept");
            LetterVisit(g, "guild"); LetterChoose(g, "returnletter_review"); g.save.flags.Remove("archive_record_2"); LetterChoose(g, "returnletter_check");
            Check(!g.ReturnLetterChecked, "Lyra cannot verify a missing third original despite a saved completed ending");
            g.save.flags.Add("archive_record_2"); LetterVisit(g, "guild"); LetterChoose(g, "returnletter_review");
            WorldObject[] objects = g.Map.objects;
            try { g.Map.objects = Array.FindAll(objects, o => o.kind != "guild"); LetterChoose(g, "returnletter_check"); Check(!g.ReturnLetterChecked, "A removed canonical guild cannot be substituted by a stale choice"); }
            finally { g.Map.objects = objects; }
            LetterVisit(g, "guild"); LetterChoose(g, "returnletter_review"); LetterChoose(g, "returnletter_check");
            g.Enter(5); g.enemies.Clear(); LetterStand(g, "castaway0"); LetterCallback(g, "returnletter_child");
            Check(!g.ReturnLetterChild, "An unrescued child cannot be recorded as a witness");
            foreach (string action in new[] { "returnletter_ferryman", "returnletter_signal", "returnletter_finish", "returnletter_begin" }) LetterCallback(g, action);
            Check(!g.ReturnLetterFerryman && !g.ReturnLetterSignalDelivered && !g.ReturnLetterComplete && !g.ReturnLetterSignalActive,
                "Out-of-order or unknown callbacks cannot invent witnesses, a light receipt, completion or a handoff");
            g.Enter(0); LetterStand(g, "envoy"); g.save.health = 0; LetterCallback(g, "returnletter_finish");
            Check(!g.ReturnLetterComplete, "A nonliving player cannot submit a receipt");
        }
        public static void ReturnLetterHandoffRequiresCanonicalSafeUnobstructedRelay(Campaign data)
        {
            var g = LetterGame(data); LetterWitnesses(g); LetterStand(g, "relay1"); LetterCallback(g, "returnletter_begin");
            Check(!g.ReturnLetterSignalActive, "The first and second relays cannot substitute for the third");
            WorldObject relay = LetterStand(g, "relay2"); var foe = new Enemy(77411, 0, g.save.x + 2, g.save.y, 100); g.enemies.Add(foe);
            LetterCallback(g, "returnletter_begin"); Check(!g.ReturnLetterSignalActive, "A nearby real enemy prevents starting the handoff"); g.enemies.Remove(foe);
            string[] rows = g.Map.rows;
            try
            {
                g.Map.rows = (string[])rows.Clone(); char[] row = g.Map.rows[(int)relay.y].ToCharArray(); row[(int)relay.x] = '#'; g.Map.rows[(int)relay.y] = new string(row);
                LetterCallback(g, "returnletter_begin"); Check(!g.ReturnLetterSignalActive, "A wall obstructing the actual relay contact prevents handoff");
            }
            finally { g.Map.rows = rows; }
            try
            {
                g.save.x = 25; g.save.y = 17.5f;
                g.Map.rows = (string[])rows.Clone(); char[] bank = g.Map.rows[18].ToCharArray(); bank[25] = '#'; g.Map.rows[18] = new string(bank);
                Check(g.CanStand(g.Position), "The wall test preserves a legal standing body on the bank");
                LetterCallback(g, "returnletter_begin");
                Check(!g.ReturnLetterSignalActive, "The relay's water endpoint exception cannot bypass an intervening wall");
            }
            finally { g.Map.rows = rows; }
            LetterBegin(g); Advance(g, 1.3f);
            var fake = new WorldObject { id = relay.id, kind = relay.kind, name = relay.name, en = relay.en, x = relay.x, y = relay.y };
            g.Talk(fake); Check(!g.ReturnLetterSignalDelivered, "A copied relay reference cannot complete the active signal"); g.Close();
            WorldObject[] objects = g.Map.objects;
            try { g.Map.objects = Array.FindAll(objects, o => !ReferenceEquals(o, relay)); g.Tick(.02f, new Point()); Check(!g.ReturnLetterSignalActive && !g.ReturnLetterSignalDelivered, "Removing the canonical relay cancels rather than fabricates a receipt"); }
            finally { g.Map.objects = objects; }
            LetterBegin(g); g.enemies.Add(foe); g.Tick(.02f, new Point());
            Check(!g.ReturnLetterSignalActive && !g.ReturnLetterSignalDelivered, "A real foe entering the live handoff's safety radius cancels without a receipt");
            g.enemies.Clear(); LetterStand(g, "relay2"); g.save.x = float.NaN; LetterCallback(g, "returnletter_begin");
            Check(!g.ReturnLetterSignalActive && !g.ReturnLetterSignalDelivered, "Nonfinite player coordinates cannot start an apparent local handoff");
        }
        public static void ReturnLetterSignalEarlyLateRetryAndDistanceNeverSpendSupplies(Campaign data)
        {
            var g = LetterGame(data); LetterWitnesses(g); SaveData before = CloneSave(g.save); LetterBegin(g);
            g.Interact(); Check(g.ReturnLetterSignalActive && !g.ReturnLetterSignalConfirming && !g.ReturnLetterSignalDelivered, "An early E neither completes nor cancels preparation");
            Advance(g, 1.2f); Check(!g.ReturnLetterSignalConfirming, "The light remains preparing before 1.25 seconds");
            g.Interact(); Advance(g, .1f); Check(g.ReturnLetterSignalConfirming && !g.ReturnLetterSignalDelivered, "Repeated early E never restarts the independent clock or auto-confirms");
            Advance(g, 2.1f); Check(!g.ReturnLetterSignalActive && !g.ReturnLetterSignalDelivered, "An expired two-second confirmation never falsely records delivery");
            LetterBegin(g); g.save.x = 20; g.save.y = 19; g.Tick(.02f, new Point());
            Check(!g.ReturnLetterSignalActive && !g.ReturnLetterSignalDelivered, "Leaving the relay radius cancels the incomplete handoff");
            LetterDeliver(g);
            Check(g.save.tea == before.tea && g.save.coins == before.coins && TacticsState(g.save.inventory) == TacticsState(before.inventory),
                "Preparation, timeout, distance cancellation, retries and the successful handoff spend no resources");
            LetterCallback(g, "returnletter_begin"); Check(!g.ReturnLetterSignalActive && g.ReturnLetterSignalDelivered, "A completed receipt cannot start another active handoff");
        }
        public static void ReturnLetterClockPausesWithActualSimulation(Campaign data)
        {
            var g = LetterGame(data); LetterWitnesses(g); LetterBegin(g); float remaining = g.ReturnLetterSignalRemaining;
            foreach (string mode in new[] { "pause", "dialog", "inventory", "journal", "map", "plans", "fish" })
            { g.mode = mode; g.Tick(.05f, new Point()); Check(g.ReturnLetterSignalRemaining == remaining && !g.ReturnLetterSignalDelivered, "Menus pause the handoff: " + mode); }
            g.Close(); g.hitStop = .04f; g.Tick(.02f, new Point()); g.Tick(.02f, new Point());
            Check(g.ReturnLetterSignalRemaining == remaining, "Hitstop pauses the same timer as active world simulation");
            foreach (float dt in new[] { 0f, -1f, float.NaN, float.PositiveInfinity }) g.Tick(dt, new Point());
            Check(g.ReturnLetterSignalRemaining == remaining, "Invalid or nonpositive frame times cannot consume or create a handoff window");
            g.Tick(5, new Point()); Check(Math.Abs(g.ReturnLetterSignalRemaining - remaining + .05f) < .0001f, "Long frames obey the normal .05 second production simulation clamp");
            float stable = g.ReturnLetterSignalRemaining; string state = TacticsState(g); for (int i = 0; i < 100; i++)
            { g.GetQuest("returnletter"); bool read = g.ReturnLetterSignalActive && !g.ReturnLetterSignalConfirming; Check(read && g.ReturnLetterSignalRemaining == stable, "UI reads retain the exact clock"); }
            Check(TacticsState(g) == state, "Repeated journal and light HUD queries are pure reads");
        }
        public static void ReturnLetterTemporarySignalClearsAcrossWorldBoundaries(Campaign data)
        {
            foreach (string boundary in new[] { "travel", "reload", "clone", "new", "death", "practice", "foreignsave" })
            {
                var g = LetterGame(data); LetterWitnesses(g); LetterBegin(g); Advance(g, .2f);
                if (boundary == "travel") { g.Enter(0); g.Enter(5); }
                if (boundary == "reload") { g.PrepareSave(); g.Load(CloneSave(g.save)); g.Start(); }
                if (boundary == "clone") { g.PrepareSave(); g = new GameCore(data, CloneSave(g.save)); g.Start(); }
                if (boundary == "new") { g.NewGame(2); g.Close(); }
                if (boundary == "practice") { g.Enter(0); g.StartPractice(); Check(g.practicing, "Actual town training still starts"); }
                if (boundary == "foreignsave") g.save = CloneSave(g.save);
                if (boundary == "death")
                {
                    g.invulnerable = 0; g.save.health = 1; g.projectiles.Add(new Projectile { position = g.Position, damage = 999, source = "shot" }); g.Tick(.02f, new Point());
                    Check(g.Dying, "The live signal attempt can still take real lethal combat contact");
                }
                Check(!g.ReturnLetterSignalActive && !g.ReturnLetterSignalConfirming && g.ReturnLetterSignalRemaining == 0 && !g.ReturnLetterSignalDelivered,
                    "Lifecycle boundary abandons a temporary handoff without a false permanent receipt: " + boundary);
                if (boundary != "new") Check(g.ReturnLetterAccepted && g.ReturnLetterChecked && g.ReturnLetterChild && g.ReturnLetterFerryman,
                    "Existing witnessed story checkpoints survive cancellation: " + boundary);
            }
        }
        public static void ReturnLetterLegacySavesRetainCheckpointsAndNeverRepay(Campaign data)
        {
            var earned = LetterGame(data, false); LetterWitnesses(earned); LetterDeliver(earned); earned.Enter(0);
            LetterVisit(earned, "envoy"); LetterChoose(earned, "returnletter_offer"); LetterChoose(earned, "returnletter_finish"); earned.Close(); earned.PrepareSave();
            SaveData paid = CloneSave(earned.save);
            foreach (int version in new[] { 1, 2 })
            {
                var saved = CloneSave(paid); saved.version = version;
                var loaded = new GameCore(data, saved); loaded.Start(); loaded.Close(); loaded.enemies.Clear(); LetterStand(loaded, "envoy");
                Check(loaded.ReturnLetterComplete && loaded.Has("archive_public") && !loaded.Has("archive_family") && loaded.GetQuest("archive").complete &&
                    loaded.save.tea == paid.tea && loaded.save.reputation[1] == paid.reputation[1] && loaded.save.coins == paid.coins && !loaded.ReturnLetterSignalActive,
                    "Legacy reconstruction retains original policy, paid delivery, supplies and reputation without a transient window: v" + version);
                LetterVisit(loaded, "envoy"); LetterChoose(loaded, "returnletter_offer");
                Check(!Array.Exists(loaded.conversation.choices, c => c.action == "returnletter_finish" || c.action == "returnletter_accept"), "A persisted paid story offers rereading only");
                loaded.Close(); LetterCallback(loaded, "returnletter_finish");
                Check(loaded.save.tea == paid.tea && loaded.save.reputation[1] == paid.reputation[1], "A replayed paid receipt cannot reward again after migration");
            }
            foreach (FieldInfo field in typeof(SaveData).GetFields()) Check(field.Name.IndexOf("returnletter", StringComparison.OrdinalIgnoreCase) < 0,
                "Return-letter persistence is additive flags, without new legacy-incompatible serialized fields");
        }
    }
}
