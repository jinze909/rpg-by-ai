using System;
using System.Collections.Generic;

namespace Starfall
{
    // A delivered archive ending earns a new journey. Permanent progress uses
    // additive flags; the short light handoff belongs only to this live world.
    public sealed partial class GameCore
    {
        public const float ReturnLetterPrepareSeconds = 1.25f;
        public const float ReturnLetterWindowSeconds = 2f;
        private SaveData returnLetterSignalSave;
        private int returnLetterSignalGeneration;
        private float returnLetterSignalClock;
        private bool returnLetterSignalRunning;

        public bool ReturnLetterReady { get { return ArchiveComplete && Has("harbor_report"); } }
        public bool ReturnLetterAccepted { get { return ReturnLetterReady && Has("returnletter_accept"); } }
        public bool ReturnLetterChecked { get { return ReturnLetterAccepted && Has("returnletter_checked"); } }
        public bool ReturnLetterChild { get { return ReturnLetterChecked && Has("returnletter_child"); } }
        public bool ReturnLetterFerryman { get { return ReturnLetterChecked && Has("returnletter_ferryman"); } }
        public bool ReturnLetterSignalDelivered { get { return ReturnLetterChild && ReturnLetterFerryman && Has("returnletter_signal"); } }
        public bool ReturnLetterComplete { get { return ReturnLetterSignalDelivered && Has("returnletter_complete"); } }
        public Point ReturnLetterSignalPoint { get { return ObjectPoint(5, "relay2", new Point(25, 19)); } }
        public bool ReturnLetterSignalActive
        {
            get { return returnLetterSignalRunning && ReferenceEquals(save, returnLetterSignalSave) &&
                WorldGeneration == returnLetterSignalGeneration && save.room == 5 && !Dying && !practicing &&
                save.health > 0 && Finite(save.health) && ReturnLetterSignalPrerequisites && !ReturnLetterSignalDelivered; }
        }
        public bool ReturnLetterSignalConfirming
        { get { return ReturnLetterSignalActive && returnLetterSignalClock >= ReturnLetterPrepareSeconds; } }
        public float ReturnLetterSignalRemaining
        { get { return !ReturnLetterSignalActive ? 0 : Math.Max(0, (ReturnLetterSignalConfirming ? ReturnLetterPrepareSeconds + ReturnLetterWindowSeconds : ReturnLetterPrepareSeconds) - returnLetterSignalClock); } }
        private bool ReturnLetterWorldAction
        { get { return mode == "world" && !Dying && !practicing && save.health > 0 && Finite(save.health) && Finite(save.x) && Finite(save.y) && Fits(Position); } }
        private bool ReturnLetterSignalPrerequisites
        { get { return ReturnLetterChild && ReturnLetterFerryman && Has("rescue_0") && Has("rescue_1") && Has("rescue_2") && Has("relay_2"); } }
        private WorldObject ReturnLetterObject(int room, string kind)
        {
            if (save.room != room) return null;
            foreach (WorldObject obj in Map.objects) if (obj.kind == kind && IsObjectPresent(obj)) return obj;
            return null;
        }
        private bool ReturnLetterNear(int room, string kind, float radius = 1.75f)
        {
            if (!ReturnLetterWorldAction) return false;
            WorldObject obj = ReturnLetterObject(room, kind);
            return obj != null && (Position - new Point(obj.x, obj.y)).Length <= radius && ReturnLetterClearPath(obj);
        }
        private bool ReturnLetterClearPath(WorldObject obj)
        {
            Point delta = new Point(obj.x, obj.y) - Position;
            int steps = Math.Max(1, (int)Math.Ceiling(delta.Length / .12f));
            for (int i = 1; i <= steps; i++)
            {
                Point p = Position + delta * (i / (float)steps);
                if (!Blocked(p.x, p.y)) continue;
                // The authored third relay stands in its own permanent water
                // cell. Reach it from the bank without making that cell walkable
                // or ignoring a wall, shoal or other intervening blocked cell.
                if (save.room == 5 && obj.kind == "relay2" && (int)p.x == (int)obj.x && (int)p.y == (int)obj.y &&
                    Map.rows[(int)obj.y][(int)obj.x] == '~') continue;
                return false;
            }
            return true;
        }
        private void AppendReturnLetterChoice(string zh, string en, string action)
        {
            if (conversation == null || conversation.choices == null) return;
            var choices = new List<Choice>(conversation.choices); choices.Add(C(zh, en, action)); conversation.choices = choices.ToArray();
        }
        private void AppendReturnLetterEnvoyConversation()
        {
            if (ReturnLetterReady) AppendReturnLetterChoice(ReturnLetterComplete ? "重读第一封回信" : "第一封回信", ReturnLetterComplete ? "Reread the first reply" : "The first reply", "returnletter_offer");
        }
        private void AppendReturnLetterGuildConversation()
        {
            if (ReturnLetterAccepted) AppendReturnLetterChoice("核对遗潮旧册", "Verify the Tide Archive pages", "returnletter_review");
        }
        private void AppendReturnLetterCastawayConversation(int id)
        {
            if (!ReturnLetterChecked || !Has("rescue_" + id)) return;
            if (id == 0) AppendReturnLetterChoice("交给孩子核对的那一页", "Let the child check their page", "returnletter_child");
            if (id == 1) AppendReturnLetterChoice("与摆渡人核对旧路线", "Verify the old route with the ferryman", "returnletter_ferryman");
            if (id == 2) AppendReturnLetterChoice("问灯匠如何交接回信", "Ask the lampmaker about the reply", "returnletter_lampmaker");
        }
        private void AppendReturnLetterRelayConversation(int id)
        {
            if (id == 2 && ReturnLetterChecked && Has("relay_2")) AppendReturnLetterChoice("回信灯光信号交接", "Reply-light handoff", "returnletter_signal_menu");
        }
        private void ShowReturnLetterOffer()
        {
            if (!ReturnLetterReady || !ReturnLetterNear(0, "envoy")) return;
            if (ReturnLetterComplete)
            {
                Dialog("露恩 · 第一封回信", "Lune · The first reply",
                    Has("archive_family") ? "孩子认出了照顾过自己的那个人，摆渡人核对了回村支路。灯光信号只确认回信已收到，没有说失踪的人已回来。家人先核对的约定没有改变，原册仍由公会保管。\n\n露恩：这封回信终于有了收信的人。" : "孩子认出了照顾过自己的那个人，摆渡人核对了回村支路。公会的公开记录保留了核对结果，家人的回话没有被写成失踪者归来的证据。\n\n露恩：这封回信终于有了收信的人。",
                    Has("archive_family") ? "The child recognized a carer; the ferryman verified a village branch. The light confirmed receipt, not the missing people's return. Families still verify first, and the guild keeps the originals.\n\nLune: this reply finally has a reader." : "The child recognized a carer; the ferryman verified a village branch. The public guild copy keeps that account. The families' reply is no evidence that the missing returned.\n\nLune: this reply finally has a reader.", C("收好回执", "Keep the receipt", "close")); return;
            }
            var choices = new List<Choice>();
            if (!ReturnLetterAccepted) choices.Add(C("接下第一封回信", "Carry the first reply", "returnletter_accept"));
            if (ReturnLetterSignalDelivered) choices.Add(C("交回灯光信号回执", "Deliver the light receipt", "returnletter_finish"));
            if (ReturnLetterAccepted) choices.Add(C("追踪回信下一步", "Track the reply", "returnletter_track"));
            choices.Add(C("稍后再来", "Later", "close"));
            Dialog("露恩 · 第一封回信", "Lune · The first reply",
                "旧册里的名字已经上岸，还没有真正送到人手里。请先到公会找莉雅核对三份原册，再让雾港获救的孩子和摆渡人核对属于他们的那一页。\n\n救下灯匠、修好南侧第三座灯线后，在灯旁等候短光，再按E交接回信。灯光信号只能确认送达，不能证明谁已归来。最后把回执带给我。首次交付：凉茶2、摆渡盟声望+1。" + (Has("archive_family") ? "\n\n按你的选择，家人先核对名字；交接灯光不公开名字。" : "\n\n按你的选择，公会保留公开记录；亲口核对的结果一起归档。"),
                "The names are ashore, but not yet in anyone's hands. Ask Lyra at the guild to verify the three originals. Then let Mistport's rescued child and ferryman check their own page.\n\nRescue the lampmaker and repair the third, southern relay. At that light, wait for the short flash, then press E to hand off the reply. The light confirms delivery, never anyone's return. Bring me its receipt. First delivery: 2 teas and Ferrymen trust +1." + (Has("archive_family") ? "\n\nAs you chose, families verify names first. No names are broadcast by the light." : "\n\nAs you chose, the guild keeps a public copy with the verified accounts."), choices.ToArray());
        }
        private void ShowReturnLetterReview()
        {
            if (!ReturnLetterAccepted || !ReturnLetterNear(0, "guild")) return;
            Dialog("莉雅 · 遗潮旧册", "Lyra · The Tide Archive pages",
                "三份原册记着水位、撤离路线和照顾孩子的人。孩子能核对自己的那一页，摆渡人能核对旧航路；谁都不能替另一页的人作证。\n\n" + (Has("archive_family") ? "家人先核对名字。带走各自的那一页，不公开其余名字；公会继续保存原册。" : "公开记录保留原文，旁边注明是谁亲自核对。回信只代表收到了记录。"),
                "The three originals record water levels, evacuation routes and the people who cared for children. The child can check their page; the ferryman can check the old passage. Neither can speak for everyone in the record.\n\n" + (Has("archive_family") ? "Families check first. Carry each person's own page, without publishing the other names. The guild keeps every original." : "Keep the original public copy, noting who verified each account. A reply acknowledges the record, nothing more."),
                C(ReturnLetterChecked ? "记录已核对" : "核对三份原册", ReturnLetterChecked ? "Pages already verified" : "Verify the three originals", "returnletter_check"), C("稍后再读", "Read later", "close"));
        }
        private void ShowReturnLetterSignal()
        {
            if (!ReturnLetterChecked || !ReturnLetterNear(5, "relay2") || !Has("relay_2")) return;
            var choices = new List<Choice>();
            if (ReturnLetterSignalPrerequisites && !ReturnLetterSignalDelivered) choices.Add(C("开始交接 · 无物资消耗", "Begin handoff · no supply cost", "returnletter_begin"));
            choices.Add(C("追踪未完成的步骤", "Track the next step", "returnletter_track")); choices.Add(C("先作准备", "Prepare first", "close"));
            Dialog("第三灯线 · 回信", "Third relay · The reply",
                ReturnLetterSignalDelivered ? "灯光信号已被接收。把回执带回镇里，交给露恩。" : "孩子和摆渡人各自核对后，灯匠才能交接这封回信。先救下三名乘客，并修好这座灯线。附近有敌人时不能开始。\n\n开始后等候1.25秒，灯光转亮时在2秒内按E。菜单暂停计时；走远、旅行或读档会取消本次交接。错过可重试，不消耗材料，也不会误报送达。灯光只送出回执，不公开名字。",
                ReturnLetterSignalDelivered ? "The reply was received. Return its receipt to Lune in town." : "The child and ferryman must check their own pages before the lampmaker can hand off the reply. Rescue all three passengers and repair this relay. Clear nearby foes before starting.\n\nWait 1.25 seconds. When the light brightens, press E within 2 seconds. Menus pause the clock; leaving, travel or loading cancels this handoff. Retry freely if missed: no supplies or false delivery. The light carries a receipt, never names.", choices.ToArray());
        }
        private bool ChooseReturnLetter(string action)
        {
            if (action == null || !action.StartsWith("returnletter_", StringComparison.Ordinal)) return false;
            if (!ReturnLetterWorldAction) return true;
            if (action == "returnletter_offer") { ShowReturnLetterOffer(); return true; }
            if (action == "returnletter_accept")
            {
                if (!ReturnLetterReady || ReturnLetterAccepted || !ReturnLetterNear(0, "envoy")) return true;
                Flag("returnletter_accept"); TrackQuest("returnletter");
                RecordMoment("露恩托付第一封回信：先核对原册，再让雾港的人亲自回话。", "Lune entrusted a first reply: verify the originals, then hear from Mistport's people themselves.");
                Notify("先去公会找莉雅，核对三份遗潮旧册。", "Ask Lyra at the guild to verify the three Tide Archive originals."); return true;
            }
            if (action == "returnletter_review") { ShowReturnLetterReview(); return true; }
            if (action == "returnletter_check")
            {
                if (!ReturnLetterAccepted || !ArchiveRecordsRecovered || !ReturnLetterNear(0, "guild")) return true;
                Flag("returnletter_checked"); Notify("原册已核对。去雾港找获救的孩子与摆渡人。", "Originals verified. Visit the rescued child and ferryman in Mistport."); return true;
            }
            if (action == "returnletter_child" || action == "returnletter_ferryman")
            {
                bool child = action == "returnletter_child"; int id = child ? 0 : 1;
                if (!ReturnLetterChecked || !Has("rescue_" + id) || !ReturnLetterNear(5, "castaway" + id)) return true;
                Flag(action);
                Dialog(child ? "获救的孩子 · 认得的名字" : "摆渡人 · 旧航路", child ? "The rescued child · A name remembered" : "The ferryman · The old passage",
                    child ? "这个名字我认得。涨水时，她给我们每人分了一小块面包。我不知道她后来去了哪里。\n\n请在回信里写：我记得她照顾过我们。别写她已经回家。" : "这一段是从高地回村的支路，我认得。原页不改，在旁边注明是谁亲自核对。等人回来的人，也能按路图找到这里。\n\n我能核对这条路，不能替没见到的人报平安。",
                    child ? "I know that name. When the water rose, she shared a little bread with each of us. I do not know where she went.\n\nWrite that I remember her care. Do not write that she came home." : "I know this branch from the high ground back to our village. Keep the original page and note my account beside it. People waiting for someone can follow the route too.\n\nI can verify the route. I cannot speak for people I did not see.", C("记下亲口核对的回话", "Keep their own account", "close")); return true;
            }
            if (action == "returnletter_lampmaker")
            {
                if (!ReturnLetterChecked || !Has("rescue_2") || !ReturnLetterNear(5, "castaway2")) return true;
                Dialog("灯匠 · 回信的光", "The lampmaker · A reply light", "南侧第三座灯线可以送出回执。修好线路，等孩子和摆渡人核对完，再到灯旁开始。先等短光，灯亮时按E；错过可以重来。我们只说信已送达，不说人已归来。", "The third relay to the south can carry a receipt. Repair it and let the child and ferryman verify their pages. Begin beside the light: wait for the short flash, then press E when it brightens. A missed window can be retried. We report a letter received, never a person returned.", C("记住灯光信号", "Remember the light", "close")); return true;
            }
            if (action == "returnletter_signal_menu") { ShowReturnLetterSignal(); return true; }
            if (action == "returnletter_begin")
            {
                if (!ReturnLetterSignalPrerequisites || ReturnLetterSignalDelivered || ReturnLetterSignalActive || !ReturnLetterNear(5, "relay2") || !SafeTravel) return true;
                returnLetterSignalSave = save; returnLetterSignalGeneration = WorldGeneration; returnLetterSignalClock = 0; returnLetterSignalRunning = true;
                Notify("等候短光；灯亮后按E交接回信。", "Wait for the short flash; press E when the light brightens."); sound = "quest"; return true;
            }
            if (action == "returnletter_finish")
            {
                if (!ReturnLetterSignalDelivered || ReturnLetterComplete || !ReturnLetterNear(0, "envoy")) return true;
                Flag("returnletter_complete"); save.tea = Math.Min(999, save.tea + 2); Reputation(1, 1);
                RecordMoment("第一封回信送达：孩子记得照顾，摆渡人核对回村支路。凉茶2、摆渡盟声望提升；没有把回执写成归来的证明。", "The first reply arrived: a child remembers care, a ferryman verifies a village branch. Two teas and Ferrymen trust; a receipt is no proof of anyone's return.");
                Notify("第一封回信已交付：凉茶2、摆渡盟声望+1。", "First reply delivered: 2 teas and Ferrymen trust +1."); sound = "reward"; ShowReturnLetterOffer(); return true;
            }
            if (action == "returnletter_track")
            {
                if (ReturnLetterAccepted && (ReturnLetterNear(0, "envoy") || ReturnLetterNear(0, "guild") || ReturnLetterNear(5, "relay2"))) TrackQuest("returnletter");
                return true;
            }
            return true;
        }
        private bool TalkReturnLetter(WorldObject obj)
        {
            if (!returnLetterSignalRunning || obj == null || !ReferenceEquals(obj, ReturnLetterObject(5, "relay2"))) return false;
            if (!ReturnLetterSignalActive || !ReturnLetterNear(5, "relay2", 2.5f)) { ResetReturnLetterSignal(); return true; }
            if (!ReturnLetterNear(5, "relay2")) { Notify("靠近灯线后再按E。", "Move closer to the relay before pressing E."); return true; }
            if (!SafeTravel) { ResetReturnLetterSignal(); Notify("附近有敌人。清理后可重新交接。", "Foes are nearby. Clear them, then retry the handoff."); return true; }
            if (!ReturnLetterSignalConfirming) { Notify("短光还没到。等灯亮起再按E。", "The short flash is not ready. Press E when the light brightens."); return true; }
            if (returnLetterSignalClock >= ReturnLetterPrepareSeconds + ReturnLetterWindowSeconds) { ResetReturnLetterSignal(); return true; }
            Flag("returnletter_signal"); ResetReturnLetterSignal(); sound = "quest";
            RecordMoment("第三灯线收到回信回执。灯光没有送出名字，也没有许诺归来。", "The third relay received the reply's receipt. No names were broadcast; no return was promised.");
            Notify("回信已收到。回镇里把回执交给露恩。", "Reply received. Bring the receipt back to Lune in town."); return true;
        }
        private void TickReturnLetterSignal(float dt)
        {
            if (!returnLetterSignalRunning) return;
            if (!ReturnLetterSignalActive || !ReturnLetterNear(5, "relay2", 2.5f) || !SafeTravel) { ResetReturnLetterSignal(); return; }
            if (!Finite(dt) || dt <= 0 || mode != "world") return;
            bool preparing = !ReturnLetterSignalConfirming; returnLetterSignalClock += dt;
            if (returnLetterSignalClock >= ReturnLetterPrepareSeconds + ReturnLetterWindowSeconds)
            { ResetReturnLetterSignal(); Notify("错过了短光。没有消耗材料，可在灯旁重试。", "The flash passed. No supplies spent; retry beside the light."); return; }
            if (preparing && ReturnLetterSignalConfirming) { Notify("灯亮了！2秒内按E交接回信。", "The light is bright! Press E within 2 seconds."); sound = "ui"; }
        }
        private void ResetReturnLetterSignal()
        { returnLetterSignalSave = null; returnLetterSignalGeneration = -1; returnLetterSignalClock = 0; returnLetterSignalRunning = false; }
        private QuestGuide GetReturnLetterQuest()
        {
            QuestGuide q = new QuestGuide { id = "returnletter", title = L("露恩的第一封回信", "Lune's first reply"), category = L("送信之路", "A LETTER'S JOURNEY"), description = L("让遗潮旧册里的人亲自核对，把回信回执带回露恩。", "Let people verify the Tide Archive pages, then bring Lune a reply receipt."), reward = L("凉茶2 · 摆渡盟声望+1", "2 teas · Ferrymen trust +1"), locked = !ReturnLetterReady, complete = ReturnLetterComplete, room = 0, target = ObjectPoint(0, "envoy", new Point(26, 9)) };
            q.steps.Add(new QuestStep(L("莉雅核对三份原册", "Lyra verifies three originals"), ReturnLetterChecked));
            q.steps.Add(new QuestStep(L("孩子与摆渡人亲自核对", "The child and ferryman verify"), ReturnLetterChild && ReturnLetterFerryman));
            q.steps.Add(new QuestStep(L("救下灯匠，在第三灯线交接", "Rescue the lampmaker; hand off at relay III"), ReturnLetterSignalDelivered));
            q.steps.Add(new QuestStep(L("把回执交给露恩", "Give Lune the receipt"), ReturnLetterComplete));
            if (!ReturnLetterReady) { q.next = L("完成遗潮档案，并交回雾港调查报告", "Complete the Tide Archive and file Mistport's report"); return q; }
            if (!ReturnLetterAccepted) { q.next = L("到镇里找露恩，接下第一封回信", "Visit Lune in town to carry the first reply"); return q; }
            if (!ReturnLetterChecked) { q.target = ObjectPoint(0, "guild", new Point(18, 17)); q.next = L("请莉雅核对三份遗潮原册", "Ask Lyra to verify the three Tide Archive originals"); return q; }
            if (ReturnLetterSignalDelivered) { q.next = ReturnLetterComplete ? L("回信已送达，可找露恩重读回执", "Reply delivered; reread the receipt with Lune") : L("回镇里，把灯光信号回执交给露恩", "Return to town and deliver the light receipt to Lune"); return q; }
            q.room = 5;
            int witness = !ReturnLetterChild ? 0 : !ReturnLetterFerryman ? 1 : !Has("rescue_2") ? 2 : -1;
            if (witness >= 0)
            {
                q.target = ObjectPoint(5, "castaway" + witness, new Point(8, 10));
                q.next = !Has("rescue_" + witness) ? L("救援乘客：木材1、药草1", "Rescue the passenger: 1 timber and 1 herb") : L(witness == 0 ? "让孩子亲自核对照顾者的名字" : "请摆渡人核对旧路线", witness == 0 ? "Let the child verify their carer's name" : "Ask the ferryman to verify the old route"); return q;
            }
            q.target = ReturnLetterSignalPoint;
            q.next = !Has("relay_2") ? L("修复南侧第三灯线：零件1", "Repair the southern third relay: 1 part") : ReturnLetterSignalActive ? ReturnLetterSignalConfirming ? L("灯已亮起，按E交接回信", "The light is bright; press E to hand off the reply") : L("等候短光，不要离开灯旁", "Wait for the short flash; stay beside the light") : L("到第三灯线开始交接；附近须安全", "Begin at the third relay; clear nearby foes first"); return q;
        }
    }
}
