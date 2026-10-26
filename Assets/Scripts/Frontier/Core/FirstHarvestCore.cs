using System;
using System.Collections.Generic;

namespace Starfall
{
    public sealed partial class GameCore
    {
        // Ordered flags extend old saves without adding inventory items or crop rules.
        public bool FirstHarvestReady { get { return WaterGardenComplete && Has("scout_saved"); } }
        public bool FirstHarvestAccepted { get { return FirstHarvestReady && Has("firstharvest_accept"); } }
        public bool FirstHarvestHarvested { get { return FirstHarvestAccepted && Has("firstharvest_harvested"); } }
        public bool FirstHarvestPacked { get { return FirstHarvestHarvested && Has("firstharvest_packed"); } }
        public bool FirstHarvestDelivered { get { return FirstHarvestPacked && Has("firstharvest_delivered"); } }
        public bool FirstHarvestComplete { get { return FirstHarvestDelivered && Has("firstharvest_complete"); } }

        private WorldObject FirstHarvestHost(int role)
        {
            int room = role == 1 && save.room == 9 ? 9 : 0;
            string id = role == 0 ? "gardener" : role == 1 ? (room == 9 ? "innCook" : "chef") : "returned_scout";
            string kind = role == 0 ? "garden" : role == 1 ? "cook" : "returnscout";
            return room < data.maps.Length ? Array.Find(data.maps[room].objects, o => o.id == id && o.kind == kind) : null;
        }
        private bool FirstHarvestAt(int role, bool allowDialog = false)
        {
            WorldObject host = FirstHarvestHost(role);
            int room = role == 1 && save.room == 9 ? 9 : 0;
            return host != null && save.room == room && (mode == "world" || (allowDialog && mode == "dialog"))
                && !Dying && !practicing && !InAbyss && save.health > 0 && Finite(save.health)
                && Finite(save.x) && Finite(save.y) && Fits(Position) && SafeTravel && hazards.Count == 0
                && !projectiles.Exists(p => !p.friendly && p.life > 0)
                && Array.IndexOf(Map.objects, host) >= 0 && IsObjectPresent(host)
                // NPC handoffs use the same E authority as the original conversation.
                // A house corner can block a shot while both residents can still talk.
                && ReferenceEquals(Nearest(), host) && !Blocked(host.x, host.y);
        }
        private bool FirstHarvestRewardFits
        { get { return save.coins >= 0 && save.coins <= 999974 && save.tea >= 0 && save.tea <= 998; } }
        public bool AcceptFirstHarvest()
        {
            if (!FirstHarvestReady || FirstHarvestAccepted || !FirstHarvestAt(0)) return false;
            Flag("firstharvest_accept"); TrackQuest("firstharvest");
            RecordMoment("杏想把药田的收成送给回镇的侦察员。先收获一季药草，再请阿茴包好汤料。", "An wants to share the garden's harvest with the scout you brought home. Harvest a crop, then ask Ari to pack soup herbs.");
            Notify("收获一次药田作物。已种下的作物也可以。", "Harvest a garden crop. A crop already growing counts too."); return true;
        }
        private void OnFirstHarvestCrop()
        {
            // Called only after the original mature crop has actually paid out.
            // Preserve its reward sound, toast, yield, timer and ordinary tracking.
            if (FirstHarvestAccepted && !FirstHarvestHarvested && FirstHarvestAt(0)) Flag("firstharvest_harvested");
        }
        public bool PackFirstHarvest()
        {
            if (!FirstHarvestHarvested || FirstHarvestPacked || !FirstHarvestAt(1)
                || save.inventory[2] < 2 || save.inventory[2] > 9999) return false;
            save.inventory[2] -= 2; Flag("firstharvest_packed");
            RecordMoment("阿茴收下两株药草，包成汤料。把这份药田的心意交给回镇的侦察员。", "Ari packed two herbs for soup. Take this gift from the garden to the returned scout.");
            Notify("汤料已包好。找回镇的侦察员交付。", "Soup herbs packed. Deliver them to the returned scout."); return true;
        }
        public bool DeliverFirstHarvest()
        {
            if (!FirstHarvestPacked || FirstHarvestDelivered || !FirstHarvestAt(2)) return false;
            Flag("firstharvest_delivered");
            RecordMoment("侦察员接过汤料：回来的路上一直想着镇里的炉火。现在，终于可以坐下来喝一碗热汤了。", "The scout accepted the soup herbs: on the road home, he kept thinking of the town hearth. Now he can sit down to a warm bowl.");
            Notify("汤料已送到。回药田告诉杏。", "Delivery received. Tell An at the garden."); return true;
        }
        public bool FinishFirstHarvest()
        {
            if (!FirstHarvestDelivered || FirstHarvestComplete || !FirstHarvestAt(0)) return false;
            if (!FirstHarvestRewardFits)
            { Notify("请先留出硬币25、凉茶1的存放空间。", "Make room for 25 coins and 1 tea, then return."); return false; }
            Flag("firstharvest_complete"); save.coins += 25; save.tea++;
            RecordMoment("杏听说汤料送到了，留下硬币25与凉茶1。清水流进药田，又从药田走进了镇里的生活。", "An heard the soup herbs had arrived and gave you 25 coins and a tea. Clean water reached the garden, and its harvest reached a neighbor.");
            sound = "reward"; Notify("药田的第一份收成：硬币25、凉茶1。", "First Harvest: 25 coins and 1 tea."); return true;
        }
        private void AppendFirstHarvestChoice(int role)
        {
            if (!(role == 0 ? FirstHarvestReady : FirstHarvestAccepted) || conversation == null) return;
            var choices = new List<Choice>(conversation.choices);
            choices.Add(C(FirstHarvestComplete ? "聊聊药田的收成" : "药田的第一份收成", FirstHarvestComplete ? "The garden's harvest" : "First Harvest", "firstharvest_menu"));
            conversation.choices = choices.ToArray();
        }
        private bool FirstHarvestActionEnabled(string action)
        {
            if (action == "firstharvest_accept") return FirstHarvestReady && !FirstHarvestAccepted && FirstHarvestAt(0, true);
            if (action == "firstharvest_pack") return FirstHarvestHarvested && !FirstHarvestPacked && FirstHarvestAt(1, true) && save.inventory[2] >= 2 && save.inventory[2] <= 9999;
            if (action == "firstharvest_deliver") return FirstHarvestPacked && !FirstHarvestDelivered && FirstHarvestAt(2, true);
            if (action == "firstharvest_finish") return FirstHarvestDelivered && !FirstHarvestComplete && FirstHarvestAt(0, true) && FirstHarvestRewardFits;
            return true;
        }
        private string FirstHarvestCropNext()
        {
            if (save.plantedAt < 0) return L("在杏的药田种植并收获一季作物", "Plant and harvest a crop at An's garden");
            int seconds = Math.Max(0, (int)Math.Ceiling(60 - (save.totalTime - save.plantedAt)));
            return seconds > 0 ? L("药田作物约", "Crop ready in about ") + seconds + L("秒后成熟", "s")
                : L("药田作物已成熟，找杏收获", "The crop is ready; harvest with An");
        }
        private void ShowFirstHarvest()
        {
            int role = FirstHarvestAt(0) ? 0 : FirstHarvestAt(1) ? 1 : FirstHarvestAt(2) ? 2 : -1;
            if (!FirstHarvestReady || role < 0 || (role != 0 && !FirstHarvestAccepted)) return;
            var choices = new List<Choice>();
            if (!FirstHarvestAccepted && role == 0) choices.Add(C("接下这份心意", "Help share the harvest", "firstharvest_accept"));
            if (FirstHarvestHarvested && !FirstHarvestPacked && role == 1)
                choices.Add(C("包好汤料 · 药草2（持有" + save.inventory[2] + "）", "Pack · 2 herbs (have " + save.inventory[2] + ")", "firstharvest_pack"));
            if (FirstHarvestPacked && !FirstHarvestDelivered && role == 2) choices.Add(C("交付药田的汤料", "Deliver the soup herbs", "firstharvest_deliver"));
            if (FirstHarvestDelivered && !FirstHarvestComplete && role == 0) choices.Add(C("汇报 · 硬币25、凉茶1", "Report · 25 coins, 1 tea", "firstharvest_finish"));
            if (FirstHarvestAccepted && !FirstHarvestComplete) choices.Add(C("追踪药田的收成", "Track First Harvest", "firstharvest_track"));
            choices.Add(C("稍后再来", "Later", "close"));
            string zh = FirstHarvestComplete ? "从井边到药田，再到一碗热汤。水泵修好了，人也回来了，这些小事让镇子像个家。杏托你送出的心意，已经到了邻居手里。" :
                !FirstHarvestAccepted ? "杏：清水已经流进药田了。回镇的侦察员走过了长路，我想送他一份新收成。\n\n请在这片药田实际收获一季作物，再拿其中两株药草，请阿茴包成汤料，交给侦察员，最后回来告诉我。已种下的作物也可以，不用另种。\n\n谢礼是硬币25、凉茶1，只领取一次。" :
                !FirstHarvestHarvested ? "先收获一季作物吧。" + FirstHarvestCropNext() + "。旅店休息时，作物也会继续长大。之后找阿茴包好两株药草。" :
                !FirstHarvestPacked ? "请阿茴用两株药草包成汤料，送给回镇的侦察员。这里只交药草，不会喝汤或用掉远行餐。\n\n持有药草" + save.inventory[2] + "；需要2；还需" + Math.Max(0, 2 - save.inventory[2]) + "。镇里或旅店都可以找阿茴。" :
                !FirstHarvestDelivered ? "汤料已经包好，不会用掉你的炖汤。去镇里把它交给回来的侦察员，再向杏汇报。" :
                "汤料已经交到侦察员手里。清水浇出的药草，终于变成给邻居的热汤了。回药田告诉杏这份好消息。\n\n谢礼：硬币25、凉茶1。" + (FirstHarvestRewardFits ? "" : "请先留出硬币和凉茶的存放空间，再来领取；这份谢礼会为你保留。");
            string en = FirstHarvestComplete ? "From the well to the garden, then into a warm bowl. The pump works and the scout is home. An's gift has reached her neighbor." :
                !FirstHarvestAccepted ? "An: Clean water has reached the garden. The scout you brought home had a hard journey. I'd like to share a fresh harvest.\n\nHarvest one garden crop. Ask Ari to pack two herbs for soup, deliver them to the returned scout, then tell me. A crop already growing counts; no need to replant.\n\nThanks: 25 coins and 1 tea, once." :
                !FirstHarvestHarvested ? "Harvest a garden crop first. " + FirstHarvestCropNext() + ". Crops also grow while you rest at the inn. Then take two herbs to Ari." :
                !FirstHarvestPacked ? "Ask Ari to pack two herbs for the returned scout's soup. This uses herbs only; it will not drink stew or consume your road meal.\n\nHerbs: have " + save.inventory[2] + "; need 2; still need " + Math.Max(0, 2 - save.inventory[2]) + ". Find Ari in town or at the inn." :
                !FirstHarvestDelivered ? "The soup herbs are packed, separate from your stew supplies. Deliver them to the returned scout in town, then tell An." :
                "The scout has received the soup herbs. Clean water and a garden crop have become a neighbor's warm soup. Tell An the good news.\n\nThanks: 25 coins and 1 tea." + (FirstHarvestRewardFits ? "" : " Make room for the coins and tea, then return. Your full reward will wait.");
            Dialog("药田的第一份收成", "First Harvest", zh, en, choices.ToArray());
        }
        private bool ChooseFirstHarvest(string action)
        {
            if (action == null || !action.StartsWith("firstharvest_", StringComparison.Ordinal)) return false;
            if (action == "firstharvest_menu") ShowFirstHarvest();
            else if (action == "firstharvest_accept") AcceptFirstHarvest();
            else if (action == "firstharvest_pack") PackFirstHarvest();
            else if (action == "firstharvest_deliver") DeliverFirstHarvest();
            else if (action == "firstharvest_finish") FinishFirstHarvest();
            else if (action == "firstharvest_track" && FirstHarvestAccepted && (FirstHarvestAt(0) || FirstHarvestAt(1) || FirstHarvestAt(2))) TrackQuest("firstharvest");
            return true;
        }
        private QuestGuide GetFirstHarvestQuest()
        {
            int role = !FirstHarvestHarvested || FirstHarvestDelivered ? 0 : !FirstHarvestPacked ? 1 : 2;
            WorldObject host = FirstHarvestHost(role);
            var q = new QuestGuide { id = "firstharvest", title = L("药田的第一份收成", "First Harvest"), category = L("镇中后续", "TOWN FOLLOW-UP"),
                description = L("把药田的新收成送给回镇的邻居。", "Share a garden harvest with a neighbor brought home."), reward = L("硬币25 / 凉茶1", "25 coins / 1 tea"),
                room = role == 1 && save.room == 9 ? 9 : 0, target = host == null ? new Point(17, 7) : new Point(host.x, host.y), locked = !FirstHarvestReady, complete = FirstHarvestComplete };
            q.steps.Add(new QuestStep(L("向杏接下心意", "Accept An's request"), FirstHarvestAccepted));
            q.steps.Add(new QuestStep(L("实际收获药田作物", "Harvest a garden crop"), FirstHarvestHarvested));
            q.steps.Add(new QuestStep(L("请阿茴包好药草2", "Ask Ari to pack 2 herbs"), FirstHarvestPacked));
            q.steps.Add(new QuestStep(L("送给回镇的侦察员", "Deliver to the returned scout"), FirstHarvestDelivered));
            q.steps.Add(new QuestStep(L("回药田向杏汇报", "Report to An at the garden"), FirstHarvestComplete));
            q.next = !WaterGardenComplete ? L("先完成清水到药田", "Finish Clear Water for the Garden first") : !Has("scout_saved") ? L("先护送侦察员回镇", "Bring the scout home first") :
                !FirstHarvestAccepted ? L("找杏聊聊药田的收成", "Ask An about the garden's harvest") : !FirstHarvestHarvested ? FirstHarvestCropNext() :
                !FirstHarvestPacked ? L("找阿茴包好汤料（药草2）", "Ask Ari to pack soup herbs (2 herbs)") : !FirstHarvestDelivered ? L("把汤料交给回镇的侦察员", "Deliver the soup herbs to the returned scout") :
                !FirstHarvestComplete ? L("回药田告诉杏，领取谢礼", "Tell An at the garden and collect thanks") : L("药田的心意已经送到", "The garden's gift has reached its neighbor");
            return q;
        }
    }
}
