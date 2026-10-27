using System;
using System.Collections.Generic;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static readonly string[] QuestJournalCanonicalPrefix = {
            "main", "shore", "ally1", "ally2", "ally3", "personal0", "personal1", "personal2", "bounty", "secrets", "watch", "signal",
            "archive", "reach", "homeward", "commission", "shelter", "fieldcraft", "supper", "returnletter", "riverlesson", "watergarden", "firstharvest"
        };

        // The archived production baseline has no API. Reflection lets it
        // compile and fail this behavior assertion instead of failing C#.
        private static List<int> QuestJournalRows(GameCore game, int filter)
        {
            MethodInfo method = typeof(GameCore).GetMethod("JournalQuestRows", new[] { typeof(int) });
            Check(method != null && method.ReturnType == typeof(List<int>), "Read-only journal rows API returns canonical quest indices");
            return (List<int>)method.Invoke(game, new object[] { filter });
        }

        private static bool QuestJournalContains(GameCore game, int filter, string id)
        {
            int index = Array.IndexOf(GameCore.QuestIds, id);
            Check(index >= 0, "The existing canonical quest is still registered: " + id);
            return QuestJournalRows(game, filter).Contains(index);
        }

        private static void QuestJournalSameRows(List<int> actual, List<int> expected, string reason)
        {
            Check(actual.Count == expected.Count, reason + " (row count)");
            for (int index = 0; index < expected.Count; index++) Check(actual[index] == expected[index], reason + " (canonical index " + index + ")");
        }

        private static void QuestJournalStatusOracle(GameCore game)
        {
            List<int> all = QuestJournalRows(game, 0), available = QuestJournalRows(game, 1), completed = QuestJournalRows(game, 2);
            Check(all.Count == GameCore.QuestIds.Length, "All includes every original locked, available and completed quest");
            int previousAvailable = -1, previousCompleted = -1;
            foreach (int row in available) { Check(row > previousAvailable && row < all.Count, "Available retains unique canonical order"); previousAvailable = row; }
            foreach (int row in completed) { Check(row > previousCompleted && row < all.Count, "Completed retains unique canonical order"); previousCompleted = row; }
            for (int index = 0; index < all.Count; index++)
            {
                QuestGuide guide = game.GetQuest(GameCore.QuestIds[index]);
                Check(all[index] == index && available.Contains(index) == (!guide.locked && !guide.complete) && completed.Contains(index) == guide.complete,
                    "The row view follows the original guide's live status and canonical identity: " + guide.id);
            }
        }

        private static void QuestJournalRead(GameCore game)
        {
            for (int repeat = 0; repeat < 8; repeat++)
                foreach (int filter in new[] { 0, 1, 2, -1, 3, int.MinValue, int.MaxValue })
                {
                    List<int> rows = QuestJournalRows(game, filter);
                    rows.Clear();
                    Check(QuestJournalRows(game, 0).Count == GameCore.QuestIds.Length, "Caller-owned row lists cannot change canonical quests or later queries");
                }
        }

        public static void QuestJournalRowsRetainCanonicalOrderAndFilterSemantics(Campaign data)
        {
            var game = Fresh(data); List<int> all = QuestJournalRows(game, 0);
            Check(GameCore.QuestIds.Length >= QuestJournalCanonicalPrefix.Length, "All twenty-three existing quests retain their registration");
            for (int index = 0; index < QuestJournalCanonicalPrefix.Length; index++)
                Check(GameCore.QuestIds[index] == QuestJournalCanonicalPrefix[index] && all[index] == index,
                    "Every established quest keeps its original index: " + QuestJournalCanonicalPrefix[index]);
            Check(!ReferenceEquals(all, QuestJournalRows(game, 0)), "Each journal query owns a separate row list");
            foreach (int invalid in new[] { -1, 3, 99, int.MinValue, int.MaxValue })
                QuestJournalSameRows(QuestJournalRows(game, invalid), all, "Invalid filters safely preserve the full original list");
            Check(QuestJournalContains(game, 1, "main") && QuestJournalContains(game, 1, "ally1") && QuestJournalContains(game, 1, "ally2") &&
                QuestJournalContains(game, 1, "ally3") && !QuestJournalContains(game, 1, "shore") && !QuestJournalContains(game, 1, "personal0") &&
                !QuestJournalContains(game, 1, "firstharvest") && !QuestJournalContains(game, 1, "bounty") && QuestJournalRows(game, 2).Count == 0,
                "A genuine new character sees the main story and original town requests as available while later stories stay locked");
            QuestJournalStatusOracle(game);
            game.save.flags.Add("watch_complete");
            Check(game.GetQuest("watch").complete && game.GetQuest("watch").locked && QuestJournalContains(game, 2, "watch") && !QuestJournalContains(game, 1, "watch"),
                "An existing saved completion remains in Completed even when its original guide also reports missing readiness prerequisites");
            QuestJournalStatusOracle(game);
        }

        public static void QuestJournalRefreshesAfterOriginalStoryTransitions(Campaign data)
        {
            var town = Fresh(data); QuestJournalRows(town, 0);
            Check(!QuestJournalContains(town, 1, "watergarden") && !QuestJournalContains(town, 2, "ally1"), "The optional garden follow-up starts locked");
            town.save.inventory[2] = 6; WaterGardenVisit(town, "gardener"); WaterGardenChoose(town, "ally1");
            Check(QuestJournalContains(town, 2, "ally1") && !QuestJournalContains(town, 1, "ally1") && !QuestJournalContains(town, 1, "watergarden"),
                "The actual original herb payment immediately moves An's request to Completed without unlocking the unfinished pump story");
            town.save.inventory[4] = 5; WaterGardenVisit(town, "smith"); WaterGardenChoose(town, "ally3");
            Check(QuestJournalContains(town, 2, "ally3") && QuestJournalContains(town, 1, "watergarden") && !town.WaterGardenAccepted,
                "The original parts payment unlocks the real follow-up as Available without accepting or tracking it through a query");
            WaterGardenStage(town, 4);
            Check(QuestJournalContains(town, 2, "watergarden") && !QuestJournalContains(town, 1, "watergarden") && !QuestJournalContains(town, 1, "firstharvest"),
                "Actual bucket handoffs complete the garden task while First Harvest still needs its own returned-scout prerequisite");
            var harvest = FirstHarvestGame(data);
            Check(QuestJournalContains(harvest, 1, "firstharvest") && !FirstHarvestRead(harvest, "Accepted"), "Earned First Harvest prerequisites disclose an available unaccepted story");
            FirstHarvestAccept(harvest);
            Check(QuestJournalContains(harvest, 1, "firstharvest") && !QuestJournalContains(harvest, 2, "firstharvest"), "Acceptance remains Available until the original story is actually complete");
            FirstHarvestStage(harvest, 5);
            Check(QuestJournalContains(harvest, 2, "firstharvest") && !QuestJournalContains(harvest, 1, "firstharvest"),
                "Real timed crop harvest, packing, delivery and final report move First Harvest to Completed");
            QuestJournalStatusOracle(harvest);

            var chapter = Fresh(data); Check(!QuestJournalContains(chapter, 1, "shore") && !QuestJournalContains(chapter, 1, "reach"), "A new main story has not opened the distant or riverbank stories");
            chapter.Enter(1); Combat(chapter, false); Action(chapter, "scout", "scout"); Gather(chapter); AwaitArenaOpening(chapter);
            chapter.save.x = 28; chapter.save.y = 10; Action(chapter, "shrine", "boss"); Combat(chapter, true);
            Check(chapter.Has("boss_1") && !chapter.Has("core_1") && !QuestJournalContains(chapter, 1, "reach"),
                "Defeating the guardian alone does not falsely disclose quests which require actual altar recovery");
            Action(chapter, "shrine", "goto0");
            Check(chapter.Has("core_1") && QuestJournalContains(chapter, 1, "reach") && QuestJournalContains(chapter, 1, "fieldcraft"),
                "A genuinely recovered first core opens its existing riverbank survey and field notes");
            Action(chapter, "guild", "deliver");
            Check(chapter.save.stage == 1 && QuestJournalContains(chapter, 1, "shore") && QuestJournalContains(chapter, 1, "main") && !QuestJournalContains(chapter, 2, "main"),
                "The real guild delivery opens the shore story without incorrectly completing the continuing main quest");
            QuestJournalStatusOracle(chapter);

            var contract = Fresh(data); contract.AcceptBounty(2);
            Check(QuestJournalContains(contract, 1, "bounty"), "Original contract acceptance makes its canonical row available");
            for (int caught = 0; caught < 3; caught++) RiverLakeCatch(contract);
            Check(contract.save.bountyProgress == 3 && QuestJournalContains(contract, 1, "bounty") && !QuestJournalContains(contract, 2, "bounty"),
                "Three genuine catches make the original claim ready without inventing guide completion from completed steps");
            Check(contract.ClaimBounty() && !QuestJournalContains(contract, 1, "bounty") && !QuestJournalContains(contract, 2, "bounty"),
                "The original repeating contract returns to its canonical inactive state after its actual payout");
            QuestJournalStatusOracle(contract);
        }

        public static void QuestJournalQueriesPreserveCombatTrackingPinsAndSimulation(Campaign data)
        {
            var game = MaterialSourceGame(data, 1); QuestJournalRows(game, 0); MaterialSource destination = MaterialSourceFind(game, 0, 1, "wood");
            Check(game.PinMaterialSource(destination), "An existing personal waypoint owns navigation before journal reads");
            game.Close(); game.TrackQuest("ally1"); game.save.weapon = 1; game.save.x = 12; game.save.y = 14; game.Attack(new Point(1, 0));
            ActorAction action = game.playerAction; Check(action != null && !action.released, "A genuine paid arrow preparation exists before opening the journal");
            string tracked = game.save.trackedQuest; Point pin = new Point(game.PinnedLandmark.x, game.PinnedLandmark.y); game.mode = "journal";
            string before = PlanStatsState(game); QuestJournalRead(game); QuestJournalStatusOracle(game);
            Check(PlanStatsState(game) == before && ReferenceEquals(action, game.playerAction) && game.save.trackedQuest == tracked &&
                ReferenceEquals(game.PinnedLandmark, destination.target) && (new Point(game.PinnedLandmark.x, game.PinnedLandmark.y) - pin).Length == 0,
                "Filtering preserves complete save/RNG/revision/clock state, accepted action identity, tracked story and independent personal pin");
            float elapsed = action.elapsed; game.Tick(.05f, new Point(1, 0));
            Check(ReferenceEquals(action, game.playerAction) && action.elapsed == elapsed && !action.released, "The original journal pause keeps the paid preparation planted");
            game.Close(); for (int frame = 0; frame < 80 && !action.released; frame++) game.Tick(.01f, new Point());
            Check(action.released && game.projectiles.Count > 0, "Closing the journal resumes the same original projectile release");
            Projectile shot = game.projectiles[0]; before = PlanStatsState(game); QuestJournalRead(game);
            Check(PlanStatsState(game) == before && ReferenceEquals(shot, game.projectiles[0]), "Read-only filters cannot advance, consume or recapture an airborne arrow");
            foreach (string kind in new[] { "attack", "skill", "dodge", "companion" })
            {
                var queued = Quiet(data); string field;
                if (kind == "attack") { queued.attackCooldown = .08f; queued.PressAttack(new Point(0, 1)); field = "bufferedAttack"; }
                else if (kind == "skill") { queued.skillCooldown = .08f; queued.Skill(new Point(0, 1)); field = "bufferedSkill"; }
                else if (kind == "dodge") { queued.dashCooldown = .08f; queued.Dash(new Point(0, 1)); field = "bufferedDodge"; }
                else { queued.save.flags.Add("companion_1"); queued.SelectCompanion(1); queued.companionCooldown = .08f; queued.CommandCompanion(); field = "bufferedCompanionCommand"; }
                object intent = PlanStatsIntent(queued, field); Check(intent != null, "Real unpaid buffered input exists: " + kind);
                before = PlanStatsState(queued); QuestJournalRead(queued);
                Check(PlanStatsState(queued) == before && ReferenceEquals(intent, PlanStatsIntent(queued, field)), "Queries preserve the captured input and lifetime: " + kind);
                Advance(queued, .12f); Check(kind == "dodge" ? queued.dashTime > 0 : kind == "companion" ? queued.companionAction != null : queued.playerAction != null,
                    "The unchanged buffered action still dispatches after journal reads: " + kind);
            }
            foreach (string mode in new[] { "world", "inventory", "journal", "map", "dialog", "pause", "title" })
            {
                var paused = Fresh(data); paused.mode = mode; paused.deathTime = .3f; before = PlanStatsState(paused); QuestJournalRead(paused);
                Check(PlanStatsState(paused) == before && paused.deathTime == .3f, "Status queries preserve even defeat and unowned mode " + mode);
            }
        }

        public static void QuestJournalReconstructsLegacyStatusesWithoutNewSaveState(Campaign data)
        {
            var original = FirstHarvestGame(data); QuestJournalRows(original, 0); FirstHarvestStage(original, 5);
            original.save.explored[0] = 16777215; original.mode = "landmarks";
            WorldObject garden = Object(original, "garden"); Check(original.PinLandmark(garden), "Existing canonical garden pin is saved independently of the completed quest");
            original.TrackQuest("firstharvest"); original.save.coins = 191; original.save.health = 73; original.save.energy = 47;
            original.save.flags.Add("journal_mature_unknown"); original.PrepareSave();
            List<int> available = QuestJournalRows(original, 1), completed = QuestJournalRows(original, 2);
            foreach (int version in new[] { 1, 2 })
            {
                SaveData saved = CloneSave(original.save); saved.version = version;
                var resumed = new GameCore(data, saved); resumed.Start(); string before = PlanStatsState(resumed);
                QuestJournalSameRows(QuestJournalRows(resumed, 1), available, "Version " + version + " reconstructs available status from mature story state");
                QuestJournalSameRows(QuestJournalRows(resumed, 2), completed, "Version " + version + " reconstructs completed status without cached filter state");
                QuestJournalRead(resumed);
                Check(PlanStatsState(resumed) == before && resumed.save.version == 2 && resumed.save.coins == 191 && resumed.save.health == 73 &&
                    resumed.save.energy == 47 && resumed.Has("journal_mature_unknown") && resumed.save.trackedQuest == "firstharvest" &&
                    ReferenceEquals(resumed.PinnedLandmark, garden), "Version " + version + " retains its original value, tracked identity, independent waypoint and unknown story flags");
                resumed.PrepareSave(); var again = new GameCore(data, CloneSave(resumed.save)); again.Start();
                QuestJournalSameRows(QuestJournalRows(again, 2), completed, "A normal second save/load retains completed story rows");
            }
            foreach (int version in new[] { 1, 2 })
            {
                var legacy = new SaveData { version = version, presentationVersion = 0, frontierFocus = true, stage = 1,
                    trackedQuest = "not_a_real_quest", coins = 83, health = 69, energy = 41 };
                legacy.flags.Add("intro"); legacy.flags.Add("core_1"); legacy.flags.Add("exp_intro");
                var resumed = new GameCore(data, legacy); resumed.Start(); string before = PlanStatsState(resumed); QuestJournalRead(resumed);
                Check(PlanStatsState(resumed) == before && resumed.save.trackedQuest == "shore" && resumed.save.presentationVersion == 1 &&
                    resumed.save.coins == 83 && resumed.save.health == 69 && resumed.save.energy == 41 && QuestJournalContains(resumed, 1, "shore") &&
                    QuestJournalContains(resumed, 1, "reach") && !resumed.Has("firstharvest_accept") && !resumed.HasLandmarkPin,
                    "Original v" + version + " presentation migration selects earned shore focus without autoacceptance, completion or a new pin");
                QuestJournalStatusOracle(resumed);
            }
            foreach (FieldInfo field in typeof(SaveData).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Check(field.Name.IndexOf("journalfilter", StringComparison.OrdinalIgnoreCase) < 0 && field.Name.IndexOf("questrows", StringComparison.OrdinalIgnoreCase) < 0,
                    "Optional journal views add no save field");
        }
    }
}
