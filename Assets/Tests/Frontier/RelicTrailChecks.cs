using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Starfall.Tests
{
    // These checks stage already-earned prerequisites, then use the mature
    // report, personal-story, project and banking transactions for the reward.
    public static partial class CoreChecks
    {
        private static GameCore RelicTrailReady(Campaign data)
        {
            var game = Fresh(data); game.SkipTutorial(); game.save.stage = 3;
            game.save.flags.Add("exp_intro"); game.save.coins = 500;
            game.save.inventory = new[] { 40, 40, 40, 5, 40, 2, 4, 3 };
            return game;
        }
        private static void RelicTrailFlag(GameCore game, params string[] flags)
        { foreach (string flag in flags) if (!game.Has(flag)) game.save.flags.Add(flag); }
        private static void RelicTrailAppend(StringBuilder text, object value)
        {
            if (value == null) { text.Append("null;"); return; }
            if (value is string) { string s = (string)value; text.Append(s.Length).Append(':').Append(s).Append(';'); return; }
            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum)
            { text.Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append(';'); return; }
            var sequence = value as IEnumerable;
            if (sequence != null)
            { text.Append('['); foreach (object item in sequence) RelicTrailAppend(text, item); text.Append(']'); return; }
            text.Append('{'); foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            { text.Append(field.Name).Append('='); RelicTrailAppend(text, field.GetValue(value)); } text.Append('}');
        }
        private static string RelicTrailSave(SaveData save)
        { var text = new StringBuilder(); RelicTrailAppend(text, save); return text.ToString(); }
        private static string RelicTrailState(GameCore game)
        {
            var text = new StringBuilder();
            foreach (FieldInfo field in typeof(GameCore).GetFields(BindingFlags.Public | BindingFlags.Instance))
            { text.Append(field.Name).Append('='); RelicTrailAppend(text, field.GetValue(game)); }
            text.Append("mode=").Append(game.mode).Append("/generation=").Append(game.WorldGeneration);
            return text.ToString();
        }
        private static void RelicTrailExpectReward(GameCore game, int id, bool expected, string cause)
        {
            RelicTrail trail = game.GetRelicTrail(id);
            Check(game.Has("relic_" + id) == expected && trail.owned == expected && trail.sourceComplete == expected,
                "The trail agrees with the real permanent reward after " + cause);
            Check(trail.id == id && !string.IsNullOrEmpty(trail.source) && !string.IsNullOrEmpty(trail.next),
                "Every real source has a named, readable result rather than an empty tooltip");
        }
        private static void RelicTrailFileReport(GameCore game, int region, int choice)
        {
            if (region > 5) RelicTrailFlag(game, "harbor_report");
            if (region > 6) RelicTrailFlag(game, "desert_report");
            RelicTrailFlag(game, "boss_" + region, "gate_" + region);
            if (region == 6) RelicTrailFlag(game, "mirrors_lit");
            game.Close(); game.Enter(region); game.enemies.Clear();
            Action(game, region == 5 ? "captain" : region == 6 ? "tinker" : "keeper",
                region == 5 ? "harbor_report" : (region == 6 ? "desert" : "mountain") + choice);
        }

        public static void RelicTrailsFollowActualPermanentRewardSources(Campaign data)
        {
            for (int region = 5; region <= 7; region++) for (int choice = 1; choice <= (region == 5 ? 1 : 2); choice++)
            {
                var game = RelicTrailReady(data); int id = region == 5 ? 0 : region == 6 ? 2 : 3;
                RelicTrailExpectReward(game, id, false, "before the earned regional report is filed");
                int coins = game.save.coins, shards = game.save.shards;
                RelicTrailFileReport(game, region, choice);
                RelicTrailExpectReward(game, id, true, "the actual regional dialogue action");
                Check(game.save.coins == coins + (region == 5 ? 65 : region == 6 ? 70 : 75)
                    && game.save.shards == shards + (region == 5 ? 5 : 6),
                    "Filing the report keeps its original coins and shards for each story branch");
                Check(game.save.relics[0] == -1 && game.save.relics[1] == -1,
                    "An earned source grants ownership without silently equipping either relic slot");
            }
            var town = RelicTrailReady(data); int[] inventory = (int[])town.save.inventory.Clone(); int money = town.save.coins;
            RelicTrailExpectReward(town, 1, false, "before any canal is built");
            Check(town.BuildProject(0), "The existing affordable canal I transaction remains usable");
            RelicTrailExpectReward(town, 1, false, "only the first canal tier");
            Check(town.BuildProject(0), "The existing affordable canal II transaction remains usable");
            RelicTrailExpectReward(town, 1, true, "the real second canal build");
            Check(town.save.inventory[0] == inventory[0] - 10 && town.save.inventory[1] == inventory[1] - 5
                && town.save.inventory[4] == inventory[4] - 7 && town.save.coins == money - 80,
                "Amber Seed still costs the two original project recipes, with no trail surcharge or free build");

            var personal = RelicTrailReady(data); RelicTrailFlag(personal, "harbor_report", "desert_report", "mountain_report",
                "companion_0", "companion_1", "companion_2", "manifest_found", "lore_1");
            int parts = personal.save.inventory[4], crystals = personal.save.inventory[1], herbs = personal.save.inventory[2];
            int points = personal.save.skillPoints, cash = personal.save.coins, memories = personal.save.shards;
            for (int id = 0; id < 3; id++)
            {
                RelicTrailExpectReward(personal, 4, false, "before the final personal handoff");
                personal.Close(); personal.Enter(5 + id); personal.enemies.Clear();
                Action(personal, id == 0 ? "captain" : id == 1 ? "tinker" : "keeper", "personal" + id);
                RelicTrailExpectReward(personal, 4, id == 2, "personal story " + id + " delivered through its real companion");
            }
            Check(personal.save.inventory[4] == parts - 4 && personal.save.inventory[1] == crystals - 4
                && personal.save.inventory[2] == herbs - 4 && personal.save.coins == cash + 105
                && personal.save.shards == memories + 9 && personal.save.skillPoints == points + 3,
                "Three genuine personal stories retain all original materials, three skill points and one-time rewards");

            var abyss = RelicTrailReady(data); abyss.save.frontierEnding = 1; abyss.StartAbyss(0);
            for (int floor = 1; floor <= 3; floor++)
            {
                Check(abyss.save.abyssFloor == floor, "The expedition reaches each floor through the existing descent");
                RelicTrailExpectReward(abyss, 5, false, "merely entering floor " + floor);
                AbyssPlanGate(abyss);
                RelicTrailExpectReward(abyss, 5, false, "clearing floor " + floor + " before banking");
                abyss.TakeBoon((floor - 1) % 3);
                RelicTrailExpectReward(abyss, 5, floor == 3, "the actual gate transaction banking floor " + floor);
            }
            Check(abyss.save.abyssFloor == 4 && abyss.save.bestFloor == 3,
                "Rift Stone unlocks when third-floor loot is banked while descending to four, not only on extraction");
        }

        public static void RelicTrailsExposeActionablePrerequisitesWithoutLockedTravel(Campaign data)
        {
            foreach (bool english in new[] { false, true })
            {
                var game = Fresh(data); game.SkipTutorial(); game.save.english = english;
                for (int id = 0; id < 6; id++)
                {
                    RelicTrail trail = game.GetRelicTrail(id);
                    Check(trail != null && trail.id == id && !trail.owned && !trail.sourceComplete
                        && !string.IsNullOrEmpty(trail.source) && !string.IsNullOrEmpty(trail.next)
                        && trail.steps != null && trail.steps.Length > 0,
                        "Every locked relic explains a concrete source and prerequisites in either selected language");
                    if (trail.action == RelicTrailAction.Quest)
                    {
                        QuestGuide guide = game.GetQuest(trail.questId);
                        Check(!guide.locked && game.CanEnter(guide.room),
                            "An offered quest opens a presently available prerequisite rather than routing behind a locked region");
                    }
                    Check(game.save.room == 0 && !game.Has("relic_" + id), "Reading a locked source never travels or grants it");
                }
                RelicTrailFlag(game, "core_1"); game.save.stage = 1;
                RelicTrail shore = game.GetRelicTrail(0);
                Check(shore.action == RelicTrailAction.Quest && shore.questId == "shore",
                    "After the first starcore, regional relics lead to the existing distant-shores quest");
                RelicTrailFlag(game, "exp_intro", "harbor_report", "companion_0");
                RelicTrail hearth = game.GetRelicTrail(4);
                Check(hearth.action == RelicTrailAction.Quest && hearth.questId == "personal0",
                    "A recruited companion's unfinished personal story wins over unavailable later handoffs");
                RelicTrailFlag(game, "personal_0", "desert_report", "companion_1");
                game.save.inventory[1] = game.save.inventory[4] = 0;
                hearth = game.GetRelicTrail(4); Check(hearth.questId == "personal1", "Ivo's unfinished machine is the next real personal source");
                QuestGuide materials = game.GetQuest(hearth.questId);
                Check(!materials.locked && game.CanEnter(materials.room) && GuideTargetsObject(game, materials, "ore"),
                    "The existing personal resource guide leads to accessible ore rather than an unaffordable delivery");
            }
            var invalid = RelicTrailReady(data);
            foreach (int id in new[] { -1, 6, int.MinValue, int.MaxValue })
            {
                string before = RelicTrailState(invalid); RelicTrail trail = invalid.GetRelicTrail(id);
                Check(trail == null && !invalid.TrackRelicTrail(id) && RelicTrailState(invalid) == before,
                    "Invalid relic IDs have no invented source or actionable route and cannot mutate the world");
            }
            // A migrated or hand-edited record can carry one half of the old
            // progression. Explain that state without replaying its reward.
            for (int id = 0; id < 6; id++)
            {
                var recorded = RelicTrailReady(data);
                if (id == 0) RelicTrailFlag(recorded, "harbor_report");
                else if (id == 1) recorded.save.projects[0] = 2;
                else if (id == 2) RelicTrailFlag(recorded, "desert_report");
                else if (id == 3) RelicTrailFlag(recorded, "mountain_report");
                else if (id == 4) RelicTrailFlag(recorded, "personal_0", "personal_1", "personal_2");
                else recorded.save.bestFloor = 3;
                recorded.mode = "relictrail"; string before = RelicTrailState(recorded);
                RelicTrail trail = recorded.GetRelicTrail(id);
                Check(!trail.owned && trail.sourceComplete && trail.action == RelicTrailAction.None
                    && !recorded.TrackRelicTrail(id) && RelicTrailState(recorded) == before,
                    "A completed source without ownership is explained, never silently granted or routed to repeat its reward");
                var recovered = RelicTrailReady(data); RelicTrailFlag(recovered, "relic_" + id);
                recovered.mode = "relictrail"; before = RelicTrailState(recovered); trail = recovered.GetRelicTrail(id);
                Check(trail.owned && !trail.sourceComplete && trail.action == RelicTrailAction.None
                    && !recovered.TrackRelicTrail(id) && RelicTrailState(recovered) == before,
                    "Existing ownership remains independent of source history without inventing a new permanent ledger");
            }
        }

        public static void RelicTrailQueriesRemainFreshReadOnlyAndLegacyCompatible(Campaign data)
        {
            foreach (FieldInfo field in typeof(RelicTrail).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Check(field.IsInitOnly, "A source snapshot cannot acquire mutable authority: " + field.Name);
            foreach (FieldInfo field in typeof(RelicTrailStep).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Check(field.IsInitOnly, "A displayed source step cannot acquire mutable authority: " + field.Name);
            var game = RelicTrailReady(data); game.Enter(1); game.enemies.Clear(); game.save.x = 12; game.save.y = 14;
            var target = new Enemy(99781, 0, 13.1f, 14, 500) { stun = 99 }; game.enemies.Add(target);
            game.Attack(new Point(1, 0)); ActorAction action = game.playerAction;
            Check(action != null && !action.released, "A real accepted attack exists before reading source information");
            string before = RelicTrailState(game); int seed = game.save.seed; ImpactProfile impact = action.impact;
            for (int repeat = 0; repeat < 20; repeat++) for (int id = 0; id < 6; id++)
            {
                RelicTrail a = game.GetRelicTrail(id), b = game.GetRelicTrail(id);
                Check(!ReferenceEquals(a, b) && !ReferenceEquals(a.steps, b.steps),
                    "Each query returns a fresh snapshot and step array, not shared mutable UI state");
                if (a.steps.Length > 0) { a.steps[0] = null; Check(game.GetRelicTrail(id).steps[0] != null,
                    "Mutating a caller's step array cannot corrupt later source snapshots"); }
            }
            Check(RelicTrailState(game) == before && game.save.seed == seed && ReferenceEquals(game.playerAction, action)
                && ReferenceEquals(action.impact, impact),
                "Repeated source queries leave all public state, permanent fields, RNG and captured attack untouched");
            for (int frame = 0; frame < 200 && target.hp == target.maxHp; frame++) game.Tick(.01f, new Point());
            Check(action.released && target.hp < target.maxHp, "Reading the guide cannot cancel the accepted attack's actual contact");
            RelicTrail old = game.GetRelicTrail(1); game.Close(); game.Enter(0);
            Check(game.BuildProject(0) && game.BuildProject(0), "The snapshot is followed by genuine permanent progress");
            Check(!old.owned && !old.sourceComplete && game.GetRelicTrail(1).owned && game.GetRelicTrail(1).sourceComplete,
                "Old snapshots remain stable while fresh snapshots immediately reflect an earned relic");
            for (int version = 1; version <= 2; version++)
            {
                SaveData saved = CloneSave(game.save); saved.version = version; saved.flags.Add("legacy_relic_trail_sentinel");
                saved.relics = new[] { 1, -1 }; saved.relicRank = 2;
                var resumed = new GameCore(data, saved); resumed.Start(); string migrated = RelicTrailState(resumed);
                for (int id = 0; id < 6; id++) resumed.GetRelicTrail(id);
                Check(RelicTrailState(resumed) == migrated && resumed.Has("legacy_relic_trail_sentinel")
                    && resumed.Has("relic_1") && resumed.save.projects[0] == 2 && resumed.save.relicRank == 2,
                    "Reading v" + version + " migrated saves preserves every field and independent source/ownership state");
                resumed.PrepareSave(); var roundtrip = new GameCore(data, CloneSave(resumed.save)); roundtrip.Start();
                Check(roundtrip.GetRelicTrail(1).owned && roundtrip.GetRelicTrail(1).sourceComplete
                    && roundtrip.Has("legacy_relic_trail_sentinel") && roundtrip.save.projects[0] == 2,
                    "Existing PrepareSave/Load round-trips need no new save field to retain relic provenance");
            }
        }

        public static void RelicTrailTrackingOnlyOpensExistingSourcePagesWithoutTransactions(Campaign data)
        {
            // The explicit action may change only navigation/presentation and
            // tracking fields. It must never impersonate a paid/reward handler.
            for (int id = 0; id < 6; id++)
            {
                var game = RelicTrailReady(data); game.mode = "relictrail"; game.save.health = 23; game.save.energy = 41;
                if (id == 5) game.save.frontierEnding = 1;
                RelicTrail trail = game.GetRelicTrail(id); SaveData expected = CloneSave(game.save);
                bool accepted = game.TrackRelicTrail(id);
                Check(accepted == (trail.action == RelicTrailAction.Quest), "Only an offered quest source can change Core tracking");
                if (trail.action == RelicTrailAction.Quest)
                { expected.trackedQuest = trail.questId; expected.frontierFocus = trail.questId == "shore"; expected.tutorialBits |= 4;
                    Check(game.mode == "relictrail" && game.save.trackedQuest == trail.questId,
                        "An explicit quest source selects its mature prerequisite without taking control of UI navigation"); }
                else if (trail.action == RelicTrailAction.Projects)
                    Check(game.mode == "relictrail" && !accepted, "The project page is a UI action, never a Core construction shortcut");
                else if (trail.action == RelicTrailAction.AbyssMap)
                    Check(game.mode == "relictrail" && !accepted && game.save.room == expected.room && game.save.abyssFloor == 0,
                        "The route map is a UI action, never a Core expedition-start shortcut");
                Check(RelicTrailSave(game.save) == RelicTrailSave(expected),
                    "Source tracking cannot equip, heal, spend, collect rewards, resolve a project or move the player");
            }
            var run = RelicTrailReady(data); run.save.frontierEnding = 1; run.StartAbyss(0); run.mode = "relictrail";
            string unbanked = RelicTrailSave(run.save); RelicTrail rift = run.GetRelicTrail(5);
            Check(rift.action == RelicTrailAction.AbyssRun && !run.TrackRelicTrail(5) && run.mode == "relictrail"
                && RelicTrailSave(run.save) == unbanked, "The run-return affordance cannot bank, extract or alter the active floor through Core tracking");
            var owned = RelicTrailReady(data); owned.BuildProject(0); owned.BuildProject(0); owned.mode = "relictrail";
            string complete = RelicTrailState(owned);
            Check(owned.GetRelicTrail(1).action == RelicTrailAction.None && !owned.TrackRelicTrail(1)
                && RelicTrailState(owned) == complete, "An owned relic has no misleading repeatable reward or automatic equip action");
            foreach (string mode in new[] { "title", "dialog", "fish", "world", "inventory" })
            {
                var guarded = RelicTrailReady(data); guarded.mode = mode; string before = RelicTrailState(guarded);
                Check(!guarded.TrackRelicTrail(0) && RelicTrailState(guarded) == before,
                    "Stale source callbacks outside the source-review owner state are ignored safely");
            }
            var stale = RelicTrailReady(data); stale.mode = "relictrail";
            Check(stale.GetRelicTrail(0).action == RelicTrailAction.Quest, "An unfinished report initially offers real quest tracking");
            RelicTrailFlag(stale, "harbor_report"); string advanced = RelicTrailState(stale);
            Check(!stale.TrackRelicTrail(0) && RelicTrailState(stale) == advanced,
                "A stale source button rechecks live completion and cannot rewrite tracking after the report is filed");
            var rehearsal = RangeGame(data); Check(rehearsal.StartRange(0), "Begin a real safe training session for the ownership guard");
            rehearsal.mode = "relictrail"; string practice = RelicTrailState(rehearsal);
            Check(!rehearsal.TrackRelicTrail(0) && RelicTrailState(rehearsal) == practice,
                "A stale source action cannot alter saved tracking during an active safe rehearsal");
        }
    }
}
