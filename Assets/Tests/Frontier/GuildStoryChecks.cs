using System;
using System.Reflection;

namespace Starfall.Tests
{
    public static partial class CoreChecks
    {
        private static void GuildStand(GameCore g)
        {
            g.Close(); if (g.save.room != 0) g.Enter(0);
            WorldObject guild = Object(g, "guild"); g.save.x = guild.x; g.save.y = guild.y + 1;
        }
        private static void GuildChoose(GameCore g, string action)
        {
            Check(g.mode == "dialog" && g.conversation != null, "Guild choices require the actual conversation");
            int index = Array.FindIndex(g.conversation.choices, c => c.action == action);
            Check(index >= 0, "Missing earned guild choice " + action); g.Choose(index);
        }
        private static bool GuildHasChoice(GameCore g, string action)
        { return g.conversation != null && Array.Exists(g.conversation.choices, c => c.action == action); }
        private static bool GuildTextContains(string text, string expected)
        { return text.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0; }
        private static void GuildSameProgress(GameCore g, SaveData expected, string message)
        {
            foreach (FieldInfo field in typeof(SaveData).GetFields())
            {
                // The feature owns only append-only story flags and journey moments.
                if (field.Name == "flags" || field.Name == "moments") continue;
                object actual = field.GetValue(g.save), prior = field.GetValue(expected);
                if (actual is Array && prior is Array)
                {
                    Array a = (Array)actual, b = (Array)prior;
                    Check(a.Length == b.Length, message + ": " + field.Name + " length");
                    for (int i = 0; i < a.Length; i++) Check(object.Equals(a.GetValue(i), b.GetValue(i)), message + ": " + field.Name + "[" + i + "]");
                }
                else Check(object.Equals(actual, prior), message + ": " + field.Name);
            }
            foreach (string flag in expected.flags) Check(g.Has(flag), message + ": preserves flag " + flag);
        }
        private static int GuildMomentCount(GameCore g, string english)
        { int count = 0; foreach (JournalMoment moment in g.save.moments) if (moment.en == english) count++; return count; }

        public static void GuildChapterBriefingsFollowEarnedRoutesAndOriginalChoices(Campaign data)
        {
            for (int stage = 0; stage <= 3; stage++)
            {
                var g = Fresh(data); g.save.stage = stage; GuildStand(g); SaveData before = CloneSave(g.save);
                g.Talk(Object(g, "guild"));
                Check(g.conversation.choices[0].action == "routes" && g.conversation.choices[1].action == "close" && g.conversation.choices[2].action == "guild_evidence", "The original route and departure choices keep their first two positions");
                Check(GuildHasChoice(g, "guild_letter") == (stage == 3), "Only three delivered cores unlock the patrol reply");
                string destination = stage == 3 ? "Orrin" : data.maps[stage + 1].en;
                Check(GuildTextContains(g.conversation.textEn, destination), "The idle guild names the current earned destination at stage " + stage);
                if (stage == 1 || stage == 2) Check(!GuildTextContains(g.conversation.textEn, "rescue the scout"), "Later chapters do not send the player back to the completed forest rescue");
                GuildSameProgress(g, before, "Reading the chapter briefing preserves gameplay progress");
                if (stage == 3) continue;
                g.Close(); g.save.flags.Add("core_" + (stage + 1)); before = CloneSave(g.save); g.Talk(Object(g, "guild"));
                Check(g.conversation.choices[0].action == "deliver" && g.conversation.choices[1].action == "close" && g.conversation.choices[2].action == "guild_evidence", "Pending deliveries preserve the original delivery/later numbering");
                if (stage > 0) Check(GuildTextContains(g.conversation.textEn, data.maps[stage + 2].en), "Second and third core returns name the correct next destination");
                GuildSameProgress(g, before, "Reading a pending-core briefing does not pre-deliver the core");
            }
        }

        public static void GuildCoreDeliveriesKeepTransactionsAndCampaignGates(Campaign data)
        {
            for (int profession = 0; profession < 3; profession++)
            {
                var g = Fresh(data, profession); GuildStand(g);
                for (int chapter = 1; chapter <= 3; chapter++)
                {
                    g.save.flags.Add("core_" + chapter); SaveData expected = CloneSave(g.save);
                    expected.stage++; expected.coins += 40; expected.inventory[6]++; expected.inventory[0] += 3; expected.inventory[1] += 2;
                    g.Talk(Object(g, "guild")); GuildChoose(g, "deliver");
                    Check(g.mode == "world" && g.conversation == null && g.save.stage == chapter, "A delivery keeps the original world-mode transaction without a forced follow-up dialogue");
                    GuildSameProgress(g, expected, "Each class receives exactly the mature core-return transaction");
                    Check(g.Has("guild_delivery_" + chapter) && GuildTextContains(g.toast, data.maps[chapter + 1].name), "A delivered core records its chapter and announces the actual newly unlocked route");
                    Check(g.CanEnter(chapter + 1) && !g.GuildEvidenceReviewed && !g.GuildLetterReviewed && g.GuildEvidenceCount == 0, "Main progression never requires or invents a discovered record or acknowledgement");
                    int coins = g.save.coins, moments = g.save.moments.Count; g.Talk(Object(g, "guild"));
                    Check(!GuildHasChoice(g, "deliver") && g.save.coins == coins && g.save.moments.Count == moments, "Returning to the guild cannot replay the previous delivery or its journey moment"); g.Close();
                }
                Check(g.save.profession == profession && g.save.weapon == profession && g.save.ending == 0 && !g.Has("boss_4"), "Chapter briefings preserve the protagonist's class and do not resolve the final guardian");
            }
        }

        public static void GuildEvidenceUsesRealDiscoveryAndTruthfulGaps(Campaign data)
        {
            var g = Fresh(data); g.save.flags.Add("lore_0"); g.save.flags.Add("history_10"); g.save.flags.Add("history_16"); GuildStand(g);
            Check(g.GuildEvidenceCount == 0 && !g.GuildEvidenceFound(-1) && !g.GuildEvidenceFound(3), "Unrelated patrol notes and invalid main-record indices cannot count as the three shield records");
            g.ShowGuildEvidence();
            for (int record = 0; record < 3; record++) Check(!g.conversation.textEn.Contains(data.lore[record].text), "The evidence listing cannot reveal an undiscovered record's body");
            Check(!GuildHasChoice(g, "guild_evidence_record"), "Missing records never offer a completed-investigation acknowledgement"); g.Close();
            for (int room = 1; room <= 3; room++)
            {
                g.save.stage = room - 1; g.Enter(room); g.enemies.Clear(); WorldObject note = Object(g, "lore"); g.save.x = note.x; g.save.y = note.y + 1; g.Interact();
                Check(g.Has("lore_" + room) && g.GuildEvidenceCount == room, "Actual E-style discovery in each mature map adds exactly its existing main record");
                g.Close(); g.Enter(0); GuildStand(g); SaveData before = CloneSave(g.save); g.ShowGuildEvidence();
                for (int record = 0; record < 3; record++) Check(g.conversation.textEn.Contains(data.lore[record].text) == (record < room), "The guild quotes only the records already recovered at discovery " + room);
                Check(!GuildHasChoice(g, "guild_evidence_record"), "Three discoveries alone cannot invent three delivered cores");
                GuildSameProgress(g, before, "Evidence reading preserves existing discovery rewards and gameplay state"); g.Close();
            }
            var aliases = Fresh(data); aliases.save.flags.Add("history_0"); aliases.save.flags.Add("history_1"); aliases.save.flags.Add("history_2"); aliases.save.flags.Add("lore_1");
            Check(aliases.GuildEvidenceCount == 3 && aliases.GuildEvidenceFound(0) && aliases.GuildEvidenceFound(1) && aliases.GuildEvidenceFound(2), "Existing codex history aliases count the same record once, matching the mature journal");
        }

        public static void GuildEvidenceReviewIsOptionalOnceAndPersistent(Campaign data)
        {
            foreach (int version in new[] { 1, 2 })
            {
                var source = Fresh(data).save; source.version = version; source.stage = 3; source.coins = 147; source.shards = 13; source.skillPoints = 4;
                source.flags.Add("lore_1"); source.flags.Add("lore_2"); source.flags.Add("lore_3"); source.flags.Add("archive_family");
                var g = new GameCore(data, CloneSave(source)); g.Start(); g.Close(); GuildStand(g); SaveData before = CloneSave(g.save);
                Check(g.CanReviewGuildEvidence && !g.GuildEvidenceReviewed && !g.Has("core_1"), "An old stage-three save is proof of delivered cores without requiring retroactive core flags");
                g.Talk(Object(g, "guild")); GuildChoose(g, "guild_evidence"); GuildChoose(g, "guild_evidence_record");
                Check(g.GuildEvidenceReviewed && g.mode == "dialog" && g.conversation.textEn.Contains("decision remains yours"), "A real complete-evidence acknowledgement records a response while keeping the choice with the player");
                GuildSameProgress(g, before, "Recording evidence grants no coins, supplies, XP, talents, ending, gate or other gameplay mutation");
                string moment = g.save.moments[g.save.moments.Count - 1].en; Check(GuildMomentCount(g, moment) == 1, "The first acknowledgement adds one distinct investigation moment");
                g.Close(); g.ShowGuildEvidence(); GuildChoose(g, "guild_evidence_record"); Check(GuildMomentCount(g, moment) == 1, "Rereading the response does not append or reward repeatedly");
                g.Close(); g.PrepareSave(); var resumed = new GameCore(data, CloneSave(g.save)); resumed.Start(); resumed.Close(); GuildStand(resumed); before = CloneSave(resumed.save);
                Check(resumed.GuildEvidenceReviewed && resumed.CanReviewGuildEvidence && !resumed.Has("completed"), "Acknowledgement flags survive reconstruction without completing the campaign");
                resumed.ShowGuildEvidence(); GuildChoose(resumed, "guild_evidence_record"); GuildSameProgress(resumed, before, "Saved rereads preserve all mature progress"); Check(GuildMomentCount(resumed, moment) == 1, "The saved response remains once-only");
            }
        }

        public static void GuildStoryAccessRevalidatesLocationModesAndDefeat(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; foreach (string flag in new[] { "lore_1", "lore_2", "lore_3" }) g.save.flags.Add(flag); GuildStand(g);
            foreach (string mode in new[] { "title", "dialog", "pause", "inventory", "journal", "settings" })
            {
                g.mode = mode; g.conversation = null; g.ShowGuildEvidence(); g.ShowGuildLetter(); g.ChooseGuildStory("guild_evidence_record"); g.ChooseGuildStory("guild_letter_record");
                Check(g.mode == mode && g.conversation == null && !g.GuildEvidenceReviewed && !g.GuildLetterReviewed, "Menus cannot silently open or acknowledge guild story content");
            }
            GuildStand(g); g.ShowGuildEvidence(); g.save.x += 3; GuildChoose(g, "guild_evidence_record"); Check(!g.GuildEvidenceReviewed && g.mode == "world", "Moving away after reading revalidates the actual guild location before recording");
            GuildStand(g); g.ShowGuildLetter(); g.Enter(1); Check(g.ChooseGuildStory("guild_letter_record") && !g.GuildLetterReviewed, "A previously offered reply cannot be acknowledged from a mature combat map");
            GuildStand(g); g.practicing = true; g.ShowGuildEvidence(); g.ShowGuildLetter(); g.ChooseGuildStory("guild_evidence_record"); g.ChooseGuildStory("guild_letter_record");
            Check(g.conversation == null && !g.GuildEvidenceReviewed && !g.GuildLetterReviewed, "Training cannot execute a guild review or alter its journal state"); g.practicing = false;
            g.ShowGuildLetter(); g.deathTime = .5f; GuildChoose(g, "guild_letter_record");
            Check(g.Dying && g.mode == "world" && !g.GuildLetterReviewed, "Actual choice handling cancels a pending story acknowledgement during defeat");
            g.ShowGuildEvidence(); g.ShowGuildLetter(); Check(g.conversation == null, "A dying hero cannot open either guild story page"); g.deathTime = 0;
            GuildStand(g); g.ShowGuildEvidence(); g.save.flags.Remove("lore_3"); GuildChoose(g, "guild_evidence_record"); Check(!g.GuildEvidenceReviewed, "Losing a prerequisite before acknowledgement cannot credit an incomplete investigation");
            GuildStand(g); g.ShowGuildLetter(); g.save.stage = 2; GuildChoose(g, "guild_letter_record"); Check(!g.GuildLetterReviewed, "The reply rechecks delivered chapter progress instead of trusting a stale offered choice");
        }

        public static void GuildLetterReactsToEarnedStoriesAndProtectsEndings(Campaign data)
        {
            var g = Fresh(data); g.save.stage = 3; GuildStand(g); SaveData before = CloneSave(g.save); g.ShowGuildLetter();
            Check(!g.conversation.textEn.Contains("note in the bark") && !g.conversation.textEn.Contains("inn ledger") && !g.conversation.textEn.Contains("brought the scout home"), "The patrol reply does not claim optional discoveries or a rescue missing from the save");
            GuildChoose(g, "guild_letter_record"); Check(g.GuildLetterReviewed && g.GuildEvidenceCount == 0 && !g.GuildEvidenceReviewed, "The letter closes the opening promise independently of optional record collection"); GuildSameProgress(g, before, "Keeping the reply leaves gameplay and the final decision unchanged");
            string moment = g.save.moments[g.save.moments.Count - 1].en; g.Close(); g.ShowGuildLetter(); GuildChoose(g, "guild_letter_record"); Check(GuildMomentCount(g, moment) == 1, "The reply's journey moment cannot be farmed");
            g.save.flags.Add("history_10"); g.save.flags.Add("history_16"); g.save.flags.Add("scout_saved"); g.Close(); g.ShowGuildLetter();
            Check(g.conversation.textEn.Contains("note in the bark") && g.conversation.textEn.Contains("inn ledger") && g.conversation.textEn.Contains("brought the scout home"), "Actually earned patrol, inn and rescue memories receive their specific reply");
            g.Close(); g.PrepareSave(); g = new GameCore(data, CloneSave(g.save)); g.Start(); g.Close(); GuildStand(g); Check(g.GuildLetterReviewed, "A retained reply survives the same legacy-compatible save reconstruction");
            foreach (string flag in new[] { "lore_1", "lore_2", "lore_3" }) g.save.flags.Add(flag);
            g.ShowGuildEvidence(); GuildChoose(g, "guild_evidence_record"); g.Close();
            Check(g.GuildEvidenceReviewed && g.GuildLetterReviewed, "Both optional acknowledgements are present when checking the untouched final-story gates");
            foreach (int ending in new[] { 1, 2, 3 })
            {
                g.save.ending = ending; g.Talk(Object(g, "guild"));
                Check(g.conversation.choices[0].action == "routes" && g.conversation.choices[1].action == "epilogue" && g.conversation.choices[2].action == "close" && GuildHasChoice(g, "guild_evidence") && GuildHasChoice(g, "guild_letter"), "All mature postgame choices retain their numbering with optional story reviews appended");
                GuildChoose(g, "guild_letter"); Check(g.conversation.textEn.Contains("after the crisis"), "The reply recognizes an earned ending instead of asking the player to repeat it"); GuildChoose(g, "guild_letter_record"); Check(g.save.ending == ending && GuildMomentCount(g, moment) == 1, "Postgame rereads preserve each selected ending and its once-only reply");
            }
            g.save.ending = 0; g.Enter(4); g.Talk(Object(g, "shrine"));
            Check(GuildHasChoice(g, "boss") && !GuildHasChoice(g, "parley") && !g.Has("boss_4"), "The guild's main-record discussion is not a substitute for the distant-shores testimony and working covenant required for peace"); g.Close();
            g.save.flags.Add("boss_4"); g.Talk(Object(g, "shrine"));
            Check(GuildHasChoice(g, "ending1") && GuildHasChoice(g, "ending2") && !GuildHasChoice(g, "ending3") && !g.Has("warden_stood_down"), "Guild acknowledgements cannot substitute for allies or the peaceful-confrontation conditions"); g.Close();
            foreach (string flag in new[] { "ally_1", "ally_2", "ally_3" }) g.save.flags.Add(flag); g.Talk(Object(g, "shrine")); Check(GuildHasChoice(g, "ending3"), "The original three-allies condition still unlocks reconstruction");
        }
    }
}
