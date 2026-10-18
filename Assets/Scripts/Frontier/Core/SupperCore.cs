using System;
using System.Collections.Generic;

namespace Starfall
{
    // A finite follow-up to the three original town requests. Residents share
    // their contribution; the original deliveries and rewards are not replayed.
    public sealed partial class GameCore
    {
        private static readonly string[] supperKinds = { "garden", "fish", "forge" };
        private static readonly string[] supperNamesZh = { "杏", "老潮", "诺尔" };
        private static readonly string[] supperNamesEn = { "An", "Old Tide", "Noll" };
        public bool SupperReady { get { return Has("ally_1") && Has("ally_2") && Has("ally_3"); } }
        public bool SupperAccepted { get { return Has("supper_accept"); } }
        public bool SupperCooked { get { return Has("supper_cooked"); } }
        public bool SupperComplete { get { return Has("supper_complete"); } }
        public bool SupperPrepared(int index) { return index >= 0 && index < 3 && Has("supper_prep_" + index); }
        public bool SupperServed(int index) { return index >= 0 && index < 3 && Has("supper_served_" + index); }
        public int SupperPreparedCount { get { int count = 0; for (int i = 0; i < 3; i++) if (SupperPrepared(i)) count++; return count; } }
        public int SupperServedCount { get { int count = 0; for (int i = 0; i < 3; i++) if (SupperServed(i)) count++; return count; } }
        private bool SupperNear(int room, string kind)
        {
            if (save.room != room) return false;
            foreach (WorldObject o in Map.objects)
                if (o.kind == kind && IsObjectPresent(o) && (Position - new Point(o.x, o.y)).Length < 1.75f) return true;
            return false;
        }
        private bool SupperAtCook { get { return (save.room == 0 || save.room == 9) && SupperNear(save.room, "cook"); } }
        private bool SupperAtInn { get { return SupperNear(9, "cook"); } }
        private bool SupperCanAct { get { return mode == "world" && !Dying && !practicing && SupperReady; } }
        private void AppendSupperChoice(List<Choice> choices, int role)
        {
            if (!SupperReady) return;
            choices.Add(C("炉火共餐", "A place at the hearth", role < 0 ? "supper_menu" : "supper_prep_" + role));
        }
        private Choice[] SupperCookChoices(params Choice[] original)
        {
            var choices = new List<Choice>(original); AppendSupperChoice(choices, -1); return choices.ToArray();
        }
        public void ShowSupper()
        {
            if (!SupperCanAct || !SupperAtCook) return;
            if (SupperComplete)
            {
                Dialog("阿茴 · 炉火共餐", "Ari · A place at the hearth",
                    "桌边的水杯来自诺尔修好的水泵，汤里有杏的药草和老潮留的鱼。大家没有把还没回来的人写成已经归来，只是先给等候的人留一份热饭。\n\n你带回来的不只有物资，还有一张能坐在一起的桌子。汤茶、远行餐和客房照常开放。",
                    "Noll's repaired pump fills the cups. An's herbs and Old Tide's fish are in the soup. Nobody writes the missing patrol as returned. They simply keep a hot meal for those still waiting.\n\nYou brought back more than supplies: a table people can share. Stew, tea, road meals and guest rooms remain open.", C("记住这顿晚饭", "Remember this supper", "close")); return;
            }
            if (!SupperAccepted)
            {
                Dialog("阿茴 · 炉火共餐", "Ari · A place at the hearth",
                    "你帮杏配好了药，替老潮送来了鱼，也让诺尔修好了水泵。现在，让这些帮助成为一顿能吃到的晚饭。\n\n先去镇中与三位居民确认准备，再到炉火旅店找我备餐，给三处桌边送汤。不用再交材料，没有时间限制，可随时离开或保存。完成后留给你炖汤2、凉茶1，只领一次。",
                    "You helped An prepare medicine, brought Old Tide's fish, and gave Noll the parts for the pump. Let those separate kindnesses become a supper people can share.\n\nAsk all three residents in town to prepare, then meet me inside Hearth Inn and bring soup to three places at the tables. No further materials or deadline; leave or save at any stage. Afterwards, take 2 stews and 1 tea for the road, once.",
                    C("一起准备晚饭", "Help prepare supper", "supper_accept"), C("稍后再来", "Come back later", "close")); return;
            }
            var choices = new List<Choice>();
            if (SupperPreparedCount == 3 && !SupperCooked && SupperAtInn)
                choices.Add(C("在旅店备好共餐", "Prepare supper inside the inn", "supper_cook"));
            if (SupperCooked && SupperServedCount == 3 && SupperAtInn)
                choices.Add(C("坐下共餐 · 留下远行汤茶", "Share supper · take road supplies", "supper_finish"));
            choices.Add(C("追踪共餐准备", "Track the supper", "supper_track")); choices.Add(C("稍后继续", "Continue later", "close"));
            Dialog("阿茴 · 炉火共餐", "Ari · A place at the hearth",
                "三位居民准备：" + SupperPreparedCount + "/3；送汤：" + SupperServedCount + "/3。\n\n" + GetSupperQuest().next + "\n\n备餐与共餐都在炉火旅店里进行。大家分享的是自己的补给，不需要你再交一份委托材料。每处桌边送一份后回来，我们也给出门的人留一点热汤。",
                "Residents prepared: " + SupperPreparedCount + "/3; places served: " + SupperServedCount + "/3.\n\n" + GetSupperQuest().next + "\n\nPrepare and finish inside Hearth Inn. The residents share their own supplies; your original request materials are not charged again. Serve each place, then return so we can save some soup for your next journey.", choices.ToArray());
        }
        private void ShowSupperResident(int index)
        {
            if (!SupperCanAct || index < 0 || index > 2 || !SupperNear(0, supperKinds[index])) return;
            if (!SupperAccepted) { Notify("先找阿茴，一起准备炉火共餐。", "Ask Ari about preparing the shared supper first."); return; }
            string[] zh = {
                "你送来的药草已经给受伤的人配上了药。能坐到桌边的人多了，我也留了适合煮汤的草。晚饭不用等所有伤口都好，能一起吃一点，就是日子在继续。",
                "三条鱼的汤已经让孩子们吃到了。我把今天的收成也留了一份。不是再请你钓三条，是想请你帮着把桌子连起来，让大家说说路上的事。",
                "水泵能用了，今晚不需要从远处运水回来。我洗好了锅，也备好了杯子。机器修好以后，最好听见的声音是有人在用它。" };
            string[] en = {
                "Your herbs already became medicine for the wounded. More people can sit at the table now. I saved herbs for the soup too. Supper need not wait for every wound to heal; sharing a little means life goes on.",
                "The three fish already fed the children. I saved some of today's catch as well. I am not asking you to catch three more. Help bring the tables together so people can tell their road stories.",
                "The pump works. Tonight's water need not be hauled from far away. I washed the pot and set out cups. The best sound after repairing a machine is hearing people use it." };
            Dialog(supperNamesZh[index] + " · 炉火共餐", supperNamesEn[index] + " · Supper", zh[index], en[index],
                SupperPrepared(index) ? C("准备已确认", "Already prepared", "close") : C("确认这份准备", "Confirm the contribution", "supper_help_" + index), C("稍后继续", "Continue later", "close"));
        }
        private bool TalkSupper(WorldObject o)
        {
            if (o == null || o.kind == null || !o.kind.StartsWith("supperBowl", StringComparison.Ordinal)) return false;
            bool canonical = false; foreach (WorldObject present in Map.objects) if (ReferenceEquals(present, o)) { canonical = true; break; }
            if (!canonical) return true;
            int index;
            if (!int.TryParse(o.kind.Substring(10), out index) || index < 0 || index > 2 || !SupperCanAct || !SupperCooked || !SupperNear(9, "supperBowl" + index)) return true;
            string[] zh = {
                "桌边有人把一个空座位留了下来。那不是在宣布谁已经回来，只是说：等候的人也该先喝一碗热汤。",
                "水杯装满了，锅也洗净了。有人轻轻转着杯子，说水泵终于不用等一位冒险者路过才有用了。",
                "孩子们把种子的样子画在桌边的纸上。明天还要去药田和湖边；今晚先把故事讲完。" };
            string[] en = {
                "Someone keeps an empty chair beside the table. It is no announcement that anyone has returned. It means the people still waiting deserve a hot bowl too.",
                "The cups are full and the pot is clean. Someone turns a cup in their hands: the repaired pump finally serves a day that needs no passing adventurer.",
                "Children draw seeds on a scrap beside the table. Tomorrow still needs the garden and lake. Tonight they can finish their stories." };
            Dialog("炉火桌边 " + (index + 1), "Hearth table " + (index + 1), zh[index], en[index],
                SupperServed(index) ? C("听桌边的故事", "Listen to the table", "close") : C("送上一份热汤", "Serve a bowl of soup", "supper_serve_" + index), C("稍后继续", "Continue later", "close"));
            return true;
        }
        private bool ChooseSupper(string action)
        {
            if (action == null || !action.StartsWith("supper_", StringComparison.Ordinal)) return false;
            if (action == "supper_menu") { ShowSupper(); return true; }
            if (!SupperCanAct) return true;
            if (action.StartsWith("supper_prep_", StringComparison.Ordinal))
            { int index; if (int.TryParse(action.Substring(12), out index)) ShowSupperResident(index); return true; }
            if (action == "supper_accept" && SupperAtCook && !SupperAccepted && !SupperComplete)
            { Flag("supper_accept"); TrackQuest("supper"); Notify("去镇中找杏、老潮和诺尔，确认晚饭准备。", "Ask An, Old Tide and Noll in town about supper preparations."); return true; }
            if (action == "supper_track" && SupperAtCook && SupperAccepted)
            { TrackQuest("supper"); return true; }
            if (!SupperAccepted || SupperComplete) return true;
            int role;
            if (action.StartsWith("supper_help_", StringComparison.Ordinal) && int.TryParse(action.Substring(12), out role)
                && role >= 0 && role < 3 && SupperNear(0, supperKinds[role]) && !SupperPrepared(role))
            { Flag("supper_prep_" + role); sound = "quest"; return true; }
            if (action == "supper_cook" && SupperAtInn && SupperPreparedCount == 3 && !SupperCooked)
            { Flag("supper_cooked"); sound = "reward"; Notify("共餐已备好，给三处桌边送汤，再回阿茴身边。", "Supper is ready. Serve the three places at the tables, then return to Ari."); return true; }
            if (action.StartsWith("supper_serve_", StringComparison.Ordinal) && int.TryParse(action.Substring(13), out role)
                && role >= 0 && role < 3 && SupperCooked && SupperPreparedCount == 3 && !SupperServed(role) && SupperNear(9, "supperBowl" + role))
            { Flag("supper_served_" + role); effects.Add(new Effect("discover", Position, .5f)); sound = "quest"; return true; }
            if (action == "supper_finish" && SupperAtInn && SupperCooked && SupperPreparedCount == 3 && SupperServedCount == 3)
            {
                Flag("supper_complete"); save.inventory[6] += 2; save.tea++;
                RecordMoment("炉火共餐：修好的水泵、药田与湖边，终于把人带到同一张桌前。", "A place at the hearth: the repaired pump, garden and lake bring people to one table.");
                ShowSupper(); sound = "reward";
            }
            return true;
        }
        public QuestGuide GetSupperQuest()
        {
            var q = new QuestGuide { id = "supper", title = L("炉火共餐", "A place at the hearth"), category = L("镇民故事", "TOWN STORY"),
                description = L("三份居民委托之后，把分开的帮助变成一顿能分享的晚饭。", "After the three town requests, turn separate kindnesses into a supper people can share."),
                reward = L("炖汤2 · 凉茶1 · 共餐记录", "2 stews · 1 tea · a supper memory"), room = 0, target = ObjectPoint(0, "cook", new Point(8, 17)), locked = !SupperReady, complete = SupperComplete };
            q.steps.Add(new QuestStep(L("完成三份镇民委托", "Complete all three town requests"), SupperReady));
            q.steps.Add(new QuestStep(L("与阿茴一起准备晚饭", "Plan supper with Ari"), SupperAccepted));
            q.steps.Add(new QuestStep(L("确认居民准备 ", "Contributions ") + SupperPreparedCount + "/3", SupperPreparedCount == 3));
            q.steps.Add(new QuestStep(L("在炉火旅店备餐", "Prepare inside Hearth Inn"), SupperCooked));
            q.steps.Add(new QuestStep(L("送汤到桌边 ", "Tables served ") + SupperServedCount + "/3", SupperServedCount == 3));
            q.steps.Add(new QuestStep(L("回阿茴身边一起共餐", "Return to Ari and share supper"), SupperComplete));
            if (q.complete) { q.next = L("晚饭已结束，桌边的故事仍可重读", "Supper is shared; the table stories remain"); return q; }
            if (q.locked) { q.next = L("先完成杏、老潮和诺尔的委托", "First finish An, Old Tide and Noll's requests"); return q; }
            if (!SupperAccepted) { q.next = L("找阿茴，一起准备炉火共餐", "Ask Ari to plan the shared supper"); return q; }
            for (int i = 0; i < 3; i++) if (!SupperPrepared(i))
            { q.target = ObjectPoint(0, supperKinds[i], new Point(18, 14)); q.next = L("与", "Ask ") + L(supperNamesZh[i], supperNamesEn[i]) + L("确认晚饭准备", " about supper preparations"); return q; }
            q.room = 9; q.target = ObjectPoint(9, "cook", new Point(12, 10));
            if (!SupperCooked) { q.next = L("进入炉火旅店，找阿茴备餐", "Meet Ari inside Hearth Inn to prepare supper"); return q; }
            for (int i = 0; i < 3; i++) if (!SupperServed(i))
            { q.target = ObjectPoint(9, "supperBowl" + i, new Point(16, 14)); q.next = L("给第", "Serve place ") + (i + 1) + L("处桌边送汤", ""); return q; }
            q.next = L("回旅店阿茴身边，共餐并领取汤茶", "Return to Ari inside the inn to share supper and take supplies"); return q;
        }
    }
}
