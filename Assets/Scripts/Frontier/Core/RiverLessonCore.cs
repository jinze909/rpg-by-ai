using System;
using System.Collections.Generic;

namespace Starfall
{
    public enum RiverFishingPhase { Waiting, Reeling, Resting }

    public sealed partial class GameCore
    {
        public const float RiverFishingWaitSeconds = 1f, RiverFishingReelSeconds = 1.6f,
            RiverFishingRestSeconds = .55f, RiverFishingHalfWidth = .14f;
        private sealed class RiverFishingSession
        {
            public SaveData owner;
            public int generation, site;
            public WorldObject spot;
            public Point position;
            public RiverFishingPhase phase;
            public float remaining;
        }
        private RiverFishingSession riverFishing;
        public bool RiverLessonReady { get { return Has("ally_2") && ReachReady; } }
        public bool RiverLessonAccepted { get { return RiverLessonReady && Has("riverlesson_accept"); } }
        public bool RiverLessonRecorded(int site)
        { return site >= 0 && site < 2 && RiverLessonAccepted && Has("riverlesson_site_" + site); }
        public bool RiverLessonComplete
        { get { return RiverLessonRecorded(0) && RiverLessonRecorded(1) && Has("riverlesson_complete"); } }
        private bool RiverQuiet
        { get { return SafeTravel && hazards.Count == 0 && !projectiles.Exists(p => !p.friendly && p.life > 0); } }
        private bool RiverLivingWorld(string expectedMode)
        { return mode == expectedMode && !Dying && !practicing && !InAbyss && save.health > 0 && Finite(save.health)
                && Finite(save.x) && Finite(save.y) && Fits(Position) && RiverQuiet; }
        private WorldObject RiverSpot(int site)
        {
            if (site < 0 || site > 1 || data.maps.Length <= ReachRoom) return null;
            string id = site == 0 ? "riverlesson_pond" : "riverlesson_river";
            return Array.Find(data.maps[ReachRoom].objects, o => o.id == id && o.kind == "riverFishing" + site);
        }
        public Point RiverFishingPoint(int site)
        {
            WorldObject spot = RiverSpot(site);
            return spot == null ? new Point() : new Point(spot.x, spot.y);
        }
        private bool RiverNear(WorldObject host, string expectedMode)
        {
            return host != null && RiverLivingWorld(expectedMode) && Array.IndexOf(Map.objects, host) >= 0
                && IsObjectPresent(host) && ReferenceEquals(Nearest(), host)
                && ClearAttackPath(Position, new Point(host.x, host.y));
        }
        private bool RiverAtOldTide()
        {
            return save.room == 0 && RiverNear(Array.Find(data.maps[0].objects, o => o.id == "fisher" && o.kind == "fish"), "world");
        }
        public bool RiverFishingActive
        {
            get
            {
                RiverFishingSession session = riverFishing;
                return session != null && ReferenceEquals(save, session.owner) && WorldGeneration == session.generation
                    && save.room == ReachRoom && RiverLessonAccepted && ReferenceEquals(RiverSpot(session.site), session.spot)
                    && session.spot.x == session.position.x && session.spot.y == session.position.y
                    && RiverNear(session.spot, "fish");
            }
        }
        public int RiverFishingSite { get { return RiverFishingActive ? riverFishing.site : -1; } }
        public RiverFishingPhase RiverPhase { get { return RiverFishingActive ? riverFishing.phase : RiverFishingPhase.Waiting; } }
        public float RiverPhaseRemaining { get { return RiverFishingActive ? Math.Max(0, riverFishing.remaining) : 0; } }
        public float RiverFishingTarget { get { return RiverFishingActive && riverFishing.site == 1 ? .65f : .45f; } }
        public bool RiverCanReel { get { return RiverFishingActive && riverFishing.phase == RiverFishingPhase.Reeling; } }
        private void ResetRiverFishing()
        {
            if (riverFishing == null) return;
            riverFishing = null;
            // A load/travel must not turn an interrupted river attempt into an
            // ordinary lake catch. Other mode transitions keep their new mode.
            if (currentMode == "fish") { currentMode = "world"; ResetFishingPullFeedback(); }
        }
        public bool AcceptRiverLesson()
        {
            if (!RiverLessonReady || RiverLessonAccepted || !RiverAtOldTide()) return false;
            Flag("riverlesson_accept"); TrackQuest("riverlesson");
            RecordMoment("接下老潮的河岸记录：先看清水流，再决定何时收线。", "Accepted Old Tide's river notes: read the water before reeling.");
            return true;
        }
        public bool FinishRiverLesson()
        {
            if (!RiverLessonRecorded(0) || !RiverLessonRecorded(1) || RiverLessonComplete || !RiverAtOldTide()) return false;
            if (save.inventory[6] > 9997)
            { Notify("炖汤存放空间不足，先用一些再来。", "Make room for two stews, then return."); return false; }
            Flag("riverlesson_complete"); save.inventory[6] += 2; Reputation(2, 1); sound = "reward";
            RecordMoment("把两处河岸记录交给老潮。他为远行的人留了两份炖汤。", "Returned both river notes to Old Tide. He saved two stews for the road.");
            Notify("河岸记录完成：炖汤2、山野声望+1。", "River notes complete: 2 stews and Assembly trust +1."); return true;
        }
        public bool StartRiverFishing(int site)
        {
            WorldObject spot = RiverSpot(site);
            if (save.room != ReachRoom || !RiverLessonAccepted || !RiverNear(spot, "world")) return false;
            if (save.inventory[3] >= 9999)
            { Notify("鱼的存放空间不足，先用一些再来。", "Make room for a fish, then return."); return false; }
            ResetFishingPullFeedback(); fishTime = fishCursor = 0; fishHits = fishMisses = 0;
            riverFishing = new RiverFishingSession { owner = save, generation = WorldGeneration, site = site, spot = spot, position = new Point(spot.x, spot.y),
                phase = RiverFishingPhase.Waiting, remaining = RiverFishingWaitSeconds };
            mode = "fish"; return true;
        }
        private bool TickRiverFishing(float dt)
        {
            RiverFishingSession session = riverFishing;
            if (session == null) return false;
            if (!RiverFishingActive) { ResetRiverFishing(); return true; }
            float left = dt;
            while (left > 0 && ReferenceEquals(riverFishing, session))
            {
                float step = Math.Min(left, session.remaining);
                session.remaining = Math.Max(0, session.remaining - step); left -= step; fishTime += step;
                if (session.phase == RiverFishingPhase.Reeling) fishCursor = Clamp(1 - session.remaining / RiverFishingReelSeconds, 0, 1);
                if (session.remaining > 0) break;
                if (session.phase == RiverFishingPhase.Waiting)
                { session.phase = RiverFishingPhase.Reeling; session.remaining = RiverFishingReelSeconds; fishCursor = 0; }
                else if (session.phase == RiverFishingPhase.Reeling) MissRiverPull(session, true);
                else { session.phase = RiverFishingPhase.Waiting; session.remaining = RiverFishingWaitSeconds; fishCursor = 0; }
            }
            return true;
        }
        private void RestRiverLine(RiverFishingSession session)
        { session.phase = RiverFishingPhase.Resting; session.remaining = RiverFishingRestSeconds; }
        private void MissRiverPull(RiverFishingSession session, bool timeout = false)
        {
            fishMisses++; sound = "hurt";
            float lower = RiverFishingTarget - RiverFishingHalfWidth, upper = RiverFishingTarget + RiverFishingHalfWidth;
            RecordFishingPull(timeout ? FishingPullOutcome.Timeout : fishCursor < lower ? FishingPullOutcome.Left : FishingPullOutcome.Right,
                lower, upper, true);
            if (fishMisses >= 3)
            { Close(); Notify("鱼游走了。记录与物资没有损失，再试一次吧。", "The fish slipped away. Notes and supplies are safe; try again."); RecordFishingRetry(true, false, session.site, session.spot); }
            else RestRiverLine(session);
        }
        private bool ReelRiverFishing()
        {
            RiverFishingSession session = riverFishing;
            if (session == null) return false;
            if (!RiverFishingActive) { ResetRiverFishing(); return true; }
            if (session.phase != RiverFishingPhase.Reeling) return true;
            // Keep the painted inclusive band edges stable under float clock
            // residue; the tolerance is under one thousandth of a screen pixel.
            if (Math.Abs(fishCursor - RiverFishingTarget) > RiverFishingHalfWidth + .000001f) { MissRiverPull(session); return true; }
            fishHits++; sound = "reward";
            RecordFishingPull(FishingPullOutcome.Good, RiverFishingTarget - RiverFishingHalfWidth, RiverFishingTarget + RiverFishingHalfWidth, true);
            if (fishHits < 3) { RestRiverLine(session); return true; }
            if (save.inventory[3] >= 9999)
            { Close(); Notify("鱼的存放空间不足，这次没有结算。", "No room for this catch; nothing was paid out."); return true; }
            save.inventory[3]++; save.coins = Math.Min(999999, save.coins + 4); BountyEvent(2, 1);
            Flag("riverlesson_site_" + session.site); revision++; Close();
            Notify("河岸银鳍鱼 ×1！这一处已记录。", "River Silverfin x1! This site is recorded."); RecordFishingRetry(true, true, session.site, session.spot); return true;
        }
        private void AppendRiverLessonChoice(List<Choice> choices)
        { if (RiverLessonReady) choices.Add(C(RiverLessonComplete ? "重读河岸记录" : "河岸练钓", RiverLessonComplete ? "Reread river notes" : "River fishing lesson", "river_offer")); }
        private void ShowRiverLessonOffer()
        {
            if (!RiverLessonReady || !RiverAtOldTide()) return;
            var choices = new List<Choice>();
            if (!RiverLessonAccepted) choices.Add(C("接下河岸记录", "Accept river notes", "river_accept"));
            if (RiverLessonRecorded(0) && RiverLessonRecorded(1) && !RiverLessonComplete) choices.Add(C("交回两处记录", "Return both notes", "river_finish"));
            if (RiverLessonAccepted && !RiverLessonComplete) choices.Add(C("追踪河岸记录", "Track river notes", "river_track"));
            choices.Add(C("稍后再来", "Later", "close"));
            Dialog("老潮 · 河岸练钓", "Old Tide · River fishing",
                RiverLessonComplete ? "两处水边的记录已经收好。老潮：静水与流水各有自己的时机，别把一次收线当成所有答案。\n\n河岸钓点仍可练习；镇里的湖畔垂钓和孩子们的鱼汤都没有改变。" :
                "孩子们的鱼汤已经有人照顾了。你要去旧路河岸，请替我看看西侧水边和河岸流水。每处亲手钓成一次，再回来讲讲水流。\n\n先等浮标入水，再在青绿色区选择一次时机收线；随后放松钓线。同一轮只能收一次，三次成功钓到一条鱼，三次失误就重新试。没有鱼饵或物资消耗。\n\n两处记录只交付一次：炖汤2、山野声望+1。不收走你钓到的鱼。",
                RiverLessonComplete ? "Both river notes are safe. Old Tide: still water and running water have their own rhythm. One pull is never every answer.\n\nThe river sites remain open for practice. Lake fishing and the children's soup are unchanged." :
                "The children's soup is taken care of. Visit Reedwater's western pond and riverbank. Land one catch at each, then tell me what the water taught you.\n\nWait for the marker, reel once in the teal band, then relax the line. Each pass accepts one pull. Three good pulls land a fish; three misses end the attempt. No bait or supplies spent.\n\nReturn both notes once: 2 stews and Assembly trust +1. Keep the fish you caught.", choices.ToArray());
        }
        private bool TalkRiverLesson(WorldObject target)
        {
            if (target.kind == null || !target.kind.StartsWith("riverFishing", StringComparison.Ordinal)) return false;
            int site = target.kind == "riverFishing0" ? 0 : target.kind == "riverFishing1" ? 1 : -1;
            if (!RiverLessonAccepted || !ReferenceEquals(RiverSpot(site), target) || !RiverNear(target, "world")) return true;
            Dialog(target.name, target.en, "先等浮标入水，进入青绿色区时只收一次，再放松钓线。三次成功钓到银鳍鱼；鱼与硬币照常获得，记录只需完成一次。",
                "Wait for the marker, reel once in the teal band, then relax. Three good pulls land one Silverfin and up to 4 coins. One catch records this site.",
                C("开始河岸垂钓", "Cast by the river", "river_cast_" + site), C("稍后", "Later", "close")); return true;
        }
        private bool ChooseRiverLesson(string action)
        {
            if (action == null || !action.StartsWith("river_", StringComparison.Ordinal)) return false;
            if (action == "river_offer") ShowRiverLessonOffer();
            else if (action == "river_accept") { if (AcceptRiverLesson()) ShowRiverLessonOffer(); }
            else if (action == "river_track" && RiverLessonAccepted && RiverAtOldTide()) TrackQuest("riverlesson");
            else if (action == "river_finish") { if (FinishRiverLesson()) ShowRiverLessonOffer(); }
            else if (action == "river_cast_0") StartRiverFishing(0);
            else if (action == "river_cast_1") StartRiverFishing(1);
            return true;
        }
        private QuestGuide GetRiverLessonQuest()
        {
            var quest = new QuestGuide { id = "riverlesson", title = L("老潮的河岸记录", "Old Tide's river notes"), category = L("河岸练钓", "RIVER LESSON"),
                description = L("看清两处水流，亲手收线，再把记录带回老潮。", "Read two waters, land the catches, and return to Old Tide."),
                reward = L("炖汤2 / 山野声望+1", "2 stews / Assembly trust +1"), room = 0,
                target = ObjectPoint(0, "fish", new Point(29, 19)), locked = !RiverLessonReady, complete = RiverLessonComplete };
            quest.steps.Add(new QuestStep(L("向老潮接取记录", "Accept Old Tide's notes"), RiverLessonAccepted));
            quest.steps.Add(new QuestStep(L("西侧水边钓成一次", "Land a catch at the western pond"), RiverLessonRecorded(0)));
            quest.steps.Add(new QuestStep(L("河岸流水钓成一次", "Land a catch at the riverbank"), RiverLessonRecorded(1)));
            quest.steps.Add(new QuestStep(L("回老潮处交付记录", "Return both notes to Old Tide"), RiverLessonComplete));
            if (!RiverLessonReady) quest.next = L("完成老潮的鱼汤委托，并找回第一枚星核", "Finish Old Tide's fish commission and recover the first core");
            else if (!RiverLessonAccepted) quest.next = L("到镇中湖畔找老潮", "Meet Old Tide by the town lake");
            else if (!RiverLessonRecorded(0) || !RiverLessonRecorded(1))
            {
                int site = RiverLessonRecorded(0) ? 1 : 0; quest.room = ReachRoom; quest.target = RiverFishingPoint(site);
                quest.next = site == 0 ? L("在河岸西侧水边练习收线", "Practice reeling at the western pond") : L("在河岸流水处练习收线", "Practice reeling at the riverbank");
            }
            else quest.next = RiverLessonComplete ? L("记录已交付，仍可自由练钓", "Notes delivered; keep practicing freely") : L("回镇把两处记录交给老潮", "Return both notes to Old Tide in town");
            return quest;
        }
    }
}
