using System;
using System.Collections.Generic;

namespace Starfall
{
    public sealed partial class GameCore
    {
        public bool CommissionReady { get { return Has("desert_report") && Has("personal_1") && Has("companion_1"); } }
        public bool CommissionAccepted { get { return CommissionReady && Has("commission_accepted"); } }
        public bool CommissionTested { get { return CommissionAccepted && Has("commission_tested"); } }
        public bool CommissionReported { get { return CommissionTested && Has("commission_reported"); } }
        public bool CommissionComplete { get { return CommissionReported && Has("commission_water_first"); } }
        public Point CommissionWaterPoint { get { return ObjectPoint(6, Has("commission_oasis") ? "shade" : "campfire", Has("commission_oasis") ? new Point(8, 17) : new Point(6, 12)); } }
        private bool CommissionWorldAction { get { return mode == "world" && !Dying && !practicing; } }
        private bool CommissionQuiet(Point point) { return !enemies.Exists(e => (e.Position - point).Length < 6); }
        private bool CommissionLocal(string kind)
        { return save.room == 6 && CommissionReady && Fits(Position) && NearKind(kind, 1.75f); }
        public bool AcceptCommission()
        {
            if (!CommissionWorldAction || !CommissionLocal("tinker") || CommissionAccepted || Has("commission_reported")) return false;
            Flag("commission_accepted"); TrackQuest("commission");
            Notify("试机已约好。去东侧蓄光池，准备好后再开始。", "The trial is agreed. Go to the eastern receiver and start when ready.");
            return true;
        }
        public bool FinishCommission(bool oasis)
        {
            if (!CommissionWorldAction || !CommissionLocal("tinker") || !CommissionTested || Has("commission_reported")
                || !CommissionQuiet(Position) || enemies.Exists(e => e.boss)) return false;
            Flag("commission_reported"); Flag(oasis ? "commission_oasis" : "commission_caravan");
            save.coins += 35; save.shards += 4; save.relationships[1] = Math.Min(5, save.relationships[1] + 1);
            RecordMoment(oasis ? "伊沃与明天：第一班供水留在绿洲树荫。硬币35、碎片4、伊沃羁绊提升。" : "伊沃与明天：第一班供水送到沙海营地。硬币35、碎片4、伊沃羁绊提升。",
                oasis ? "Ivo and Tomorrow: the first water shift serves the oasis shade. 35 coins, 4 shards and Ivo's bond grows." : "Ivo and Tomorrow: the first water shift serves the desert camp. 35 coins, 4 shards and Ivo's bond grows.");
            Notify("第一班供水已安排。去选定的地点，领取今天的一杯凉茶。", "The first water shift is arranged. Visit the chosen point for today's tea.");
            sound = "reward"; return true;
        }
        public bool CollectCommissionWater()
        {
            string kind = Has("commission_oasis") ? "shade" : "campfire";
            if (!CommissionWorldAction || !CommissionReported || !CommissionLocal(kind) || (Position - CommissionWaterPoint).Length >= 1.75f || !CommissionQuiet(Position)
                || save.tea >= 999 || Has("commission_water_day_" + Day)) return false;
            save.tea++; Flag("commission_water_day_" + Day); Flag("commission_water_first");
            Notify("明天的供水到了：凉茶 ×1。每天可在这里领取一杯。", "Tomorrow's water is here: tea ×1. Collect one here each day.");
            sound = "reward"; revision++; return true;
        }
        private void ShowCommissionOffer()
        {
            if (!CommissionWorldAction || !CommissionLocal("tinker") || !CommissionReady) return;
            if (CommissionTested) { ShowCommissionReport(); return; }
            Dialog("伊沃 · 明天的第一班", "Ivo · Tomorrow's first shift",
                "伊沃：你修好了明天。可让一台没有主人的机器替人供水，还得试一次真正的压力。\n\n去东侧蓄光池按 E，准备好后手动开始。试机守卫会锁定一条光路或一个落点；离开预告再反击。蓄压检查时，靠近蓄光池按 E 可以排压，让它停机一会儿。\n\n不需要再交材料。结束后我们一起决定第一班供水送到哪里。",
                "Ivo: Tomorrow is repaired. A machine with no master still needs a real pressure test before it shares water.\n\nPress E at the eastern receiver and begin when ready. The trial guardian locks a beam or a mark; leave its warning, then counter. During a charge check, press E near the receiver to vent pressure and pause it.\n\nNo more materials are needed. Afterwards, we will choose where the first water shift goes.",
                C(CommissionAccepted ? "继续试机准备" : "加入明天的试机", CommissionAccepted ? "Continue preparations" : "Join Tomorrow's trial", CommissionAccepted ? "close" : "commission_accept"),
                C("追踪试机路线", "Track the trial route", "commission_track"), C("稍后再来", "Later", "close"));
        }
        private void ShowCommissionReport()
        {
            if (!CommissionWorldAction || !CommissionLocal("tinker") || !CommissionTested) return;
            if (CommissionReported)
            {
                Dialog("伊沃 · 明天的第一班", "Ivo · Tomorrow's first shift",
                    Has("commission_oasis") ? "伊沃：明天把第一班水留在绿洲树荫，给穿过沙海的人休息。每天去那里，都能领一杯凉茶。原来的供水约定仍记在机器里。" : "伊沃：明天把第一班水送到沙海营地，给赶路的商队。每天去那里，都能领一杯凉茶。原来的供水约定仍记在机器里。",
                    Has("commission_oasis") ? "Ivo: Tomorrow's first water shift serves the oasis shade, where travelers can rest. Collect a tea there each day. The machine still keeps the original water covenant." : "Ivo: Tomorrow's first water shift serves the desert camp and its caravans. Collect a tea there each day. The machine still keeps the original water covenant.",
                    C("记住供水地点", "Remember the water point", "close")); return;
            }
            Dialog("伊沃 · 明天的第一班", "Ivo · Tomorrow's first shift",
                "伊沃：试机通过了。它能在压力失控前停下来，也能重新开始。机器的第一条新记录，应该是谁在用水。\n\n第一班供水可以留在绿洲树荫，也可以送到沙海营地。两边的人都有理由；你选哪里，它就记住哪里。原来的沙海供水约定仍然有效。\n\n试机报酬：硬币35、碎片4、伊沃羁绊+1；选定地点每天可领取凉茶1。",
                "Ivo: The trial passed. It can stop before pressure runs away, and it can begin again. Its first new record should name the people using its water.\n\nThe first shift can serve the oasis shade or the desert camp. Both have people waiting; choose one, and it will remember. The original desert water covenant still stands.\n\nTrial payment: 35 coins, 4 shards, +1 Ivo bond; one tea per day at the selected point.",
                C("先送到绿洲树荫", "Serve the oasis shade", "commission_oasis"), C("先送到沙海营地", "Serve the desert camp", "commission_caravan"), C("再想一想", "Decide later", "close"));
        }
        private bool TalkCommission(WorldObject o)
        {
            if (save.room != 6 || o.kind != "receiver" || !CommissionAccepted || !CommissionLocal("receiver")) return false;
            if (!CommissionActive) return false;
            if (!TryCommissionVent()) Notify("现在无法排压。蓄压检查的预告时，靠近蓄光池按 E。",
                "The valve is not open. Press E near the receiver during a charge-check warning.");
            return true;
        }
        private void AppendCommissionReceiverChoice()
        {
            if (!CommissionAccepted) return;
            var choices = new List<Choice>(conversation.choices);
            choices.Add(C(CommissionTested ? "试机已通过" : "开始明天的试机", CommissionTested ? "Trial passed" : "Begin Tomorrow's trial", CommissionTested ? "close" : "commission_start"));
            conversation.choices = choices.ToArray();
        }
        private void ShowCommissionWater(WorldObject o)
        {
            string zh = "明天把共享的水送到了这里。每天可领取一杯凉茶；满杯时可先喝掉一杯，今天的份额会等你。";
            string en = "Tomorrow shares water here. Collect one tea per day; if your pack is full, drink a tea first. Today's portion will wait.";
            if (o.kind == "shade")
            {
                zh += "\n\n绿洲树荫能迅速降温。凉茶可抗暑与抗寒。";
                en += "\n\nOasis shade cools you quickly. Tea protects against heat and cold.";
            }
            Dialog(o.name, o.en, zh, en, C("领取今日凉茶", "Collect today's tea", "commission_water"), C("继续赶路", "Keep traveling", "close"));
        }
        private bool ChooseCommission(string action)
        {
            if (!action.StartsWith("commission_")) return false;
            if (action == "commission_offer") ShowCommissionOffer();
            else if (action == "commission_accept") AcceptCommission();
            else if (action == "commission_track") { if (CommissionAccepted) TrackQuest("commission"); }
            else if (action == "commission_start") StartCommissionEncounter();
            else if (action == "commission_oasis") FinishCommission(true);
            else if (action == "commission_caravan") FinishCommission(false);
            else if (action == "commission_water") CollectCommissionWater();
            else if (action == "commission_supply")
            {
                string kind = Has("commission_oasis") ? "shade" : "campfire";
                if (CommissionWorldAction && CommissionReported && CommissionLocal(kind))
                    foreach (WorldObject o in Map.objects) if (o.kind == kind) { ShowCommissionWater(o); break; }
            }
            return true;
        }
        private QuestGuide GetCommissionQuest()
        {
            QuestGuide q = new QuestGuide { id = "commission", title = L("明天的第一班", "Tomorrow's first shift"), category = L("同伴故事", "COMPANION"),
                description = L("与伊沃试开共享供水机器，让修好的机器真正给人送水。", "Test Ivo's shared water machine and put the repair to work for people."),
                reward = L("35硬币 · 碎片4 · 伊沃羁绊+1 · 每日凉茶1", "35 coins · 4 shards · +1 Ivo bond · 1 daily tea"), room = 6,
                target = ObjectPoint(6, "tinker", new Point(22, 18)), locked = !CommissionReady, complete = CommissionComplete };
            q.steps.Add(new QuestStep(L("与伊沃约好试机", "Agree on the trial with Ivo"), CommissionAccepted));
            q.steps.Add(new QuestStep(L("在蓄光池完成试机", "Complete the receiver trial"), CommissionTested));
            q.steps.Add(new QuestStep(L("与伊沃安排第一班供水", "Arrange the first water shift with Ivo"), CommissionReported));
            q.steps.Add(new QuestStep(L("到选定地点领取第一杯凉茶", "Collect the first tea at the chosen point"), CommissionComplete));
            if (!CommissionReady) q.next = L("先完成沙海调查与伊沃的修理", "First finish the desert report and Ivo's repair");
            else if (!CommissionAccepted) q.next = L("找伊沃加入试机", "Join the trial with Ivo");
            else if (!CommissionTested)
            {
                q.target = CommissionReceiverPoint;
                q.next = CommissionActive ? L("避开预告；蓄压时到蓄光池按 E 排压", "Evade warnings; E at the receiver vents a charge") : L("到蓄光池按 E，手动开始试机", "Press E at the receiver, then start the trial");
            }
            else if (!CommissionReported) q.next = L("回伊沃身边安排第一班供水", "Return to Ivo to arrange the first water shift");
            else { q.target = CommissionWaterPoint; q.next = CommissionComplete ? L("供水已开始，可每天回来领取凉茶", "Water is flowing; return each day for tea") : L("到选定地点领取第一杯凉茶", "Collect the first tea at the selected point"); }
            return q;
        }
    }
}
