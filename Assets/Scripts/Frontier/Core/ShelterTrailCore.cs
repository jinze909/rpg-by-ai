using System;
using System.Collections.Generic;

namespace Starfall
{
    public sealed partial class GameCore
    {
        public const float ShelterWindRadius = .6f, ShelterWindDuration = 1.4f;
        public static readonly Point[] ShelterSourcePoints = { new Point(6, 14), new Point(19, 11), new Point(24, 6) };
        public static readonly Point[] ShelterDestinationPoints = { new Point(19, 11), new Point(24, 6), new Point(15, 6) };
        public static readonly Point[] ShelterMarkPoints = { new Point(12, 13), new Point(21, 8), new Point(18, 9) };
        private static readonly Point[] ShelterLowerRoute = { new Point(5.5f, 17.5f), new Point(6, 14), new Point(12, 13), new Point(19, 11) };
        private static readonly Point[] ShelterUpperRoute = { new Point(19, 11), new Point(21, 8), new Point(24, 6), new Point(18, 9), new Point(15, 6) };
        private int shelterLeg = -1, shelterWindCycle;
        private bool shelterWindWarning;
        private float shelterWindTimer;
        private Point shelterWindFrom, shelterWindTo, shelterWindCenter;
        public bool ShelterReady { get { return Has("mountain_report") && Has("personal_2") && Has("companion_2"); } }
        public bool ShelterAccepted { get { return ShelterReady && Has("shelter_accepted"); } }
        public int ShelterStep { get { for (int i = 0; i < 3; i++) if (!Has("shelter_leg_" + i)) return i; return 3; } }
        public bool ShelterReported { get { return ShelterAccepted && ShelterStep == 3 && Has("sheltered_reported"); } }
        public bool ShelterComplete { get { return ShelterReported; } }
        public int ShelterLeg { get { return shelterLeg; } }
        public bool ShelterWindWarning { get { return shelterWindWarning && shelterLeg >= 0; } }
        public float ShelterWindTimer { get { return shelterWindTimer; } }
        public Point ShelterWindFrom { get { return shelterWindFrom; } }
        public Point ShelterWindTo { get { return shelterWindTo; } }
        public Point ShelterWindCenter { get { return shelterWindCenter; } }
        public bool ShelterAtLitBrazier { get { return ShelterWarm; } }
        public bool ShelterMarkRecorded(int leg) { return leg >= 0 && leg < 3 && Has("shelter_mark_" + leg); }
        public bool ShelterLegDone(int leg) { return leg >= 0 && leg < 3 && Has("shelter_leg_" + leg); }
        private bool ShelterWorldAction { get { return mode == "world" && !Dying && !practicing && Finite(save.health) && save.health > 0; } }
        private bool ShelterQuiet { get { return !enemies.Exists(e => e.boss || (e.Position - Position).Length < 5); } }
        private WorldObject ShelterObject(string kind, Point point)
        {
            if (save.room != 7) return null;
            foreach (WorldObject o in Map.objects)
                if (o.kind == kind && (new Point(o.x, o.y) - point).Length < .01f) return o;
            return null;
        }
        private bool ShelterLocal(string kind, Point point)
        {
            return ShelterReady && Fits(Position) && ShelterObject(kind, point) != null
                && (Position - point).Length < 1.75f && ClearAttackPath(Position, point);
        }
        private bool ShelterLit(Point point)
        { WorldObject o = ShelterObject("brazier", point); return o != null && Has("warm_" + o.id); }
        private bool ShelterWarm
        {
            get
            {
                if (save.room != 7) return false;
                foreach (WorldObject o in Map.objects) if (o.kind == "brazier" && Has("warm_" + o.id)
                    && (new Point(o.x, o.y) - Position).Length < 3.5f) return true;
                return false;
            }
        }
        public static Point[] ShelterRoutePoints(bool upper)
        {
            return upper ? ShelterUpperRoute : ShelterLowerRoute;
        }
        public static float ShelterSegmentDistance(Point point, Point from, Point to)
        {
            Point span = to - from; float length2 = span.x * span.x + span.y * span.y;
            float t = length2 > .0001f ? Clamp(((point.x - from.x) * span.x + (point.y - from.y) * span.y) / length2, 0, 1) : 0;
            return (point - (from + span * t)).Length;
        }
        public bool ShelterColdProtected
        {
            get
            {
                if (save.room != 7 || !ShelterReported || !Fits(Position)) return false;
                Point[] points = ShelterRoutePoints(Has("sheltered_upper"));
                for (int i = 1; i < points.Length; i++)
                {
                    Point a = points[i - 1], span = points[i] - a;
                    float length2 = span.x * span.x + span.y * span.y;
                    float t = Clamp(((Position.x - a.x) * span.x + (Position.y - a.y) * span.y) / length2, 0, 1);
                    Point projection = a + span * t;
                    if ((Position - projection).Length <= .9f && Fits(projection) && ClearAttackPath(Position, projection)) return true;
                }
                return false;
            }
        }
        public bool AcceptShelterTrail()
        {
            if (!ShelterWorldAction || !ShelterLocal("keeper", ShelterDestinationPoints[2]) || ShelterAccepted || Has("sheltered_reported")) return false;
            Flag("shelter_accepted"); TrackQuest("shelter");
            Notify("森的试路已约好。先点燃三座火盆，再从西侧火盆出发。", "Sen's trail is agreed. Light all three braziers, then begin at the western one.");
            return true;
        }
        public bool StartShelterLeg(int leg)
        {
            if (leg < 0 || leg > 2 || !ShelterWorldAction || !ShelterAccepted || ShelterReported || shelterLeg >= 0 || ShelterStep != leg
                || !ShelterLocal("brazier", ShelterSourcePoints[leg]) || !ShelterQuiet || !ShelterLit(ShelterSourcePoints[leg])
                || leg < 2 && !ShelterLit(ShelterDestinationPoints[leg])) return false;
            shelterLeg = leg; shelterWindCycle = 0; shelterWindWarning = false; shelterWindTimer = 2.2f;
            shelterWindCenter = shelterWindFrom = shelterWindTo = Position; revision++;
            Notify("试路开始。沿路读路碑，再到下一处停步点；风线亮起后离开，火盆旁可避风。", "Trail begun. Read the marker, then reach the next stop. Leave a lit wind lane; a lit brazier shelters you.");
            return true;
        }
        public bool RecordShelterMark(int leg)
        {
            if (leg < 0 || leg > 2 || !ShelterWorldAction || !ShelterAccepted || shelterLeg != leg || ShelterStep != leg
                || !ShelterLocal("shelterMark" + leg, ShelterMarkPoints[leg]) || !ShelterQuiet || Has("shelter_mark_" + leg)) return false;
            Flag("shelter_mark_" + leg); sound = "quest";
            Notify("路碑已记下。继续到下一处停步点，确认这一段路。", "Marker recorded. Reach the next stop and confirm this leg."); return true;
        }
        public bool ConfirmShelterArrival(int leg)
        {
            if (leg < 0 || leg > 2 || !ShelterWorldAction || !ShelterAccepted || shelterLeg != leg || ShelterStep != leg || !Has("shelter_mark_" + leg)
                || !ShelterLocal(leg == 2 ? "keeper" : "brazier", ShelterDestinationPoints[leg]) || !ShelterQuiet
                || !ShelterLit(ShelterSourcePoints[leg]) || leg < 2 && !ShelterLit(ShelterDestinationPoints[leg])) return false;
            Flag("shelter_leg_" + leg); ResetShelterTransient(); sound = "quest";
            Notify(leg == 2 ? "三段试路完成。找森决定先让哪条路避风。" : "这一段路已确认。暖好身子，再手动开始下一段。",
                leg == 2 ? "All three legs are checked. Ask Sen which route to shelter first." : "Leg confirmed. Warm up, then begin the next leg when ready."); return true;
        }
        public bool StopShelterLeg()
        {
            if (!ShelterWorldAction || shelterLeg < 0) return false;
            ResetShelterTransient(); revision++;
            Notify("本段试路已停。读过的路碑仍保留；回到本段火盆可重新出发。", "This leg is stopped. Recorded markers remain; restart at this leg's brazier."); return true;
        }
        public bool FinishShelterTrail(bool upper)
        {
            if (!ShelterWorldAction || !ShelterAccepted || ShelterStep != 3 || Has("sheltered_reported")
                || !ShelterLocal("keeper", ShelterDestinationPoints[2]) || !ShelterQuiet) return false;
            Flag("sheltered_reported"); Flag(upper ? "sheltered_upper" : "sheltered_lower"); ResetShelterTransient();
            save.coins += 35; save.shards += 4; save.relationships[2] = Math.Min(5, save.relationships[2] + 1); sound = "reward";
            RecordMoment(upper ? "森的避风路：先护住高处的名字。硬币35、碎片4、森羁绊提升。" : "森的避风路：先护住下山的人。硬币35、碎片4、森羁绊提升。",
                upper ? "Sen's shelter trail: shelter the high memorial road first. 35 coins, 4 shards and Sen's bond grows." : "Sen's shelter trail: shelter the descent first. 35 coins, 4 shards and Sen's bond grows.");
            Notify("路碑与火盆已经接成避风路。沿选定路带行走时，寒意不会继续增长；火盆和凉茶仍能暖身。", "Markers and braziers now shelter the selected route. Cold stops growing along its band; braziers and tea still warm you."); return true;
        }
        private void ResetShelterTransient()
        { shelterLeg = -1; shelterWindCycle = 0; shelterWindWarning = false; shelterWindTimer = 0; shelterWindFrom = shelterWindTo = shelterWindCenter = new Point(); }
        private Point ShelterWindEnd(Point center, Point direction)
        {
            Point end = center;
            for (float distance = .1f; distance <= 6.001f; distance += .1f)
            { Point next = center + direction * distance; if (!ClearAttackPath(center, next)) break; end = next; }
            return end;
        }
        private void TickShelterTrail(float dt)
        {
            if (shelterLeg < 0) return;
            if (save.room != 7 || Dying || practicing || !ShelterAccepted || ShelterStep != shelterLeg || !Finite(save.health) || save.health <= 0)
            { ResetShelterTransient(); return; }
            if (mode != "world") return;
            shelterWindTimer -= dt;
            if (shelterWindTimer > 0) return;
            if (!shelterWindWarning)
            {
                shelterWindCenter = Position; Point direction = shelterWindCycle % 2 == 0 ? new Point(1, 0) : new Point(0, 1);
                shelterWindFrom = ShelterWindEnd(shelterWindCenter, direction * -1); shelterWindTo = ShelterWindEnd(shelterWindCenter, direction);
                shelterWindWarning = true; shelterWindTimer = ShelterWindDuration; sound = "windup"; revision++; return;
            }
            shelterWindWarning = false; shelterWindTimer = 2.2f; shelterWindCycle++;
            // A small release flash marks the instant of contact. The existing
            // frost effect depicts a four-tile circle and cannot represent this lane.
            effects.Add(new Effect("release", shelterWindCenter, .35f) { direction = (shelterWindTo - shelterWindFrom).Normalized }); sound = "spell";
            if (!ShelterWarm && Fits(Position) && ShelterSegmentDistance(Position, shelterWindFrom, shelterWindTo) <= ShelterWindRadius
                && ClearAttackPath(shelterWindCenter, Position))
            {
                float before = save.health; Hurt(8, "environment", (shelterWindTo - shelterWindFrom).Normalized);
                if (!Dying && save.health > 0 && save.health < before) save.cold = Clamp(save.cold + 7, 0, 100);
            }
            if (Dying) ResetShelterTransient();
        }
        private void ShowShelterOffer()
        {
            if (!ShelterWorldAction || !ShelterLocal("keeper", ShelterDestinationPoints[2])) return;
            if (ShelterStep == 3 && ShelterAccepted) { ShowShelterReport(); return; }
            Dialog("森 · 避风路", "Sen · The shelter trail",
                "森：钟舌成了路碑，可它不能只记住一个名字。我们得试一试，火盆到火盆之间，是否还有人能走回来。\n\n点燃三座火盆，再从西侧火盆开始。每段要读一块路碑，随后在下一处停步点确认。寒风会先亮出一条风线；离开风线，或回到点亮的火盆旁避风。\n\n不收新材料。三段路走完后，决定先让下山路还是高处的名字避风。",
                "Sen: The clapper is a marker now, but it must do more than hold a name. Let us test whether people can still return between the braziers.\n\nLight all three braziers and begin at the western one. Read one marker on each leg, then confirm at the next stop. Wind shows its lane before it strikes; leave the lane or shelter beside a lit brazier.\n\nNo new materials are asked. After three legs, choose to shelter the descent or the high memorial road first.",
                C(ShelterAccepted ? "继续试路准备" : "加入森的试路", ShelterAccepted ? "Continue preparations" : "Join Sen's trail", ShelterAccepted ? "close" : "shelter_accept"),
                C("追踪避风路", "Track the shelter trail", "shelter_track"), C("稍后再来", "Later", "close"));
        }
        private void ShowShelterReport()
        {
            if (!ShelterWorldAction || !ShelterLocal("keeper", ShelterDestinationPoints[2]) || !ShelterAccepted || ShelterStep != 3) return;
            if (ShelterReported)
            {
                Dialog("森 · 避风路", "Sen · The shelter trail", Has("sheltered_upper") ? "森：高处的名字有了避风路。走在路带里，寒意不会再增长。火盆仍为每一个人亮着。" : "森：下山的人有了避风路。走在路带里，寒意不会再增长。火盆仍为每一个人亮着。",
                    Has("sheltered_upper") ? "Sen: The high memorial road is sheltered. Cold no longer grows along its band. The braziers remain lit for everyone." : "Sen: The descent is sheltered. Cold no longer grows along its band. The braziers remain lit for everyone.", C("记住这条路", "Remember this route", "close")); return;
            }
            Dialog("森 · 避风路", "Sen · The shelter trail",
                "森：三段路都有了人的足迹。钟阵与旧记录仍在；这次路碑要为活着的人挡风。\n\n先护下山路，从西侧入口经过火盆到中段；或先护高处路，从中段火盆经过两处路碑回到我身边。沿选定路带行走，寒意停止增长，不会自行消退。\n\n报酬：硬币35、碎片4、森羁绊+1。",
                "Sen: All three legs hold living footsteps. The old chimes and record remain. These markers will now shelter the living.\n\nShelter the descent first, from the western entrance through the braziers to the middle stop; or the high road, from the middle brazier past both high markers to me. Cold stops growing along the selected band; it does not fall on its own.\n\nPayment: 35 coins, 4 shards and +1 Sen bond.",
                C("先护下山路", "Shelter the descent first", "shelter_lower"), C("先护高处路", "Shelter the high road first", "shelter_upper"), C("再想一想", "Decide later", "close"));
        }
        private bool TalkShelter(WorldObject o)
        {
            if (save.room != 7 || !ShelterAccepted) return false;
            for (int i = 0; i < 3; i++) if (o.kind == "shelterMark" + i && ShelterObject(o.kind, ShelterMarkPoints[i]) == o)
            {
                Dialog(o.name, o.en, new[] { "旧钟舌上的刻字朝向中段火盆。森留下一句话：路要给走回来的人看。", "高处路碑记下两座火盆之间的脚步。风线亮起时，先走到侧边；火盆旁能避风。", "最后一块路碑朝着森所在的路口。名字不是终点，回来听人说话才是。" }[i],
                    new[] { "The old clapper's marks point to the middle brazier. Sen left a line: a road is for those who return.", "The high marker records footsteps between the braziers. Step aside when a wind lane lights; lit braziers shelter you.", "The final marker points toward Sen's crossroads. Names are not the end; return and listen to the living." }[i],
                    C(Has("shelter_mark_" + i) ? "路碑已记录" : "记录这块路碑", Has("shelter_mark_" + i) ? "Marker recorded" : "Record this marker", Has("shelter_mark_" + i) ? "close" : "shelter_mark:" + i), C("继续赶路", "Keep traveling", "close")); return true;
            }
            return false;
        }
        private void AppendShelterChoices(WorldObject o)
        {
            if (!ShelterAccepted || ShelterReported || conversation == null || save.room != 7) return;
            Point point = new Point(o.x, o.y); var choices = new List<Choice>(conversation.choices);
            if (shelterLeg >= 0 && (point - ShelterDestinationPoints[shelterLeg]).Length < .01f
                && o.kind == (shelterLeg == 2 ? "keeper" : "brazier"))
                choices.Add(C("确认本段试路", "Confirm this trail leg", "shelter_arrival:" + shelterLeg));
            if (shelterLeg >= 0 && o.kind == "brazier" && (point - ShelterSourcePoints[shelterLeg]).Length < .01f)
                choices.Add(C("停止本段试路", "Stop this trail leg", "shelter_stop"));
            else if (shelterLeg < 0 && ShelterStep < 3 && o.kind == "brazier" && (point - ShelterSourcePoints[ShelterStep]).Length < .01f)
                choices.Add(C("开始下一段试路", "Begin the next trail leg", "shelter_start:" + ShelterStep));
            conversation.choices = choices.ToArray();
        }
        private bool ChooseShelter(string action)
        {
            if (!action.StartsWith("shelter_")) return false;
            int leg;
            if (action == "shelter_offer") ShowShelterOffer();
            else if (action == "shelter_accept") AcceptShelterTrail();
            else if (action == "shelter_track") { if (ShelterAccepted) TrackQuest("shelter"); }
            else if (action.StartsWith("shelter_start:") && int.TryParse(action.Substring(14), out leg)) StartShelterLeg(leg);
            else if (action.StartsWith("shelter_mark:") && int.TryParse(action.Substring(13), out leg)) RecordShelterMark(leg);
            else if (action.StartsWith("shelter_arrival:") && int.TryParse(action.Substring(16), out leg)) ConfirmShelterArrival(leg);
            else if (action == "shelter_stop") StopShelterLeg();
            else if (action == "shelter_upper") FinishShelterTrail(true);
            else if (action == "shelter_lower") FinishShelterTrail(false);
            return true;
        }
        private QuestGuide GetShelterQuest()
        {
            QuestGuide q = new QuestGuide { id = "shelter", title = L("森的避风路", "Sen's shelter trail"), category = L("同伴故事", "COMPANION"),
                description = L("把旧钟舌铸成的路碑连到火盆，让调查后的山路真正护住行人。", "Connect the old clapper's markers to braziers and make the surveyed road shelter travelers."),
                reward = L("35硬币 · 碎片4 · 森羁绊+1 · 避风路", "35 coins · 4 shards · +1 Sen bond · sheltered route"),
                room = 7, target = ShelterDestinationPoints[2], locked = !ShelterReady, complete = ShelterComplete };
            q.steps.Add(new QuestStep(L("与森约好试路", "Agree on the trail with Sen"), ShelterAccepted));
            for (int i = 0; i < 3; i++) q.steps.Add(new QuestStep(L("读路碑并确认第", "Read the marker and confirm leg ") + (i + 1) + L("段", ""), Has("shelter_leg_" + i)));
            q.steps.Add(new QuestStep(L("与森决定先护哪条路", "Choose the sheltered route with Sen"), ShelterReported));
            if (!ShelterReady) q.next = L("先完成高原调查与森的个人故事", "First finish the mountain report and Sen's personal story");
            else if (!ShelterAccepted) q.next = L("找森加入试路", "Join the trail with Sen");
            else if (ShelterStep == 3) q.next = ShelterReported ? L("避风路已连好，沿选定路带行走", "The route is sheltered; follow its band") : L("回到森身边，决定先护哪条路", "Return to Sen and choose the sheltered route");
            else
            {
                int i = ShelterStep;
                q.target = shelterLeg < 0 ? ShelterSourcePoints[i] : !Has("shelter_mark_" + i) ? ShelterMarkPoints[i] : ShelterDestinationPoints[i];
                q.next = shelterLeg < 0 ? i == 2 ? L("在高岭火盆暖身，再手动开始最后一段", "Warm at the high brazier, then start the final leg") : L("点亮两端火盆，再手动开始本段试路", "Light the braziers, then start this leg by hand")
                    : !Has("shelter_mark_" + i) ? L("到路碑按 E，记录路上的名字", "Press E at the marker and record the names") : L("到下一处停步点确认本段", "Confirm this leg at the next stop");
            }
            return q;
        }
    }
}
