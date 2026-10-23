using System;
using System.Collections.Generic;

namespace Starfall
{
    public sealed partial class GameCore
    {
        // A town follow-up uses the existing extensible flags. The bucket is a
        // quest handoff, never an inventory item or a new crop requirement.
        public bool WaterGardenReady { get { return Has("ally_1") && Has("ally_3"); } }
        public bool WaterGardenAccepted { get { return WaterGardenReady && Has("watergarden_accept"); } }
        public bool WaterGardenDrawn { get { return WaterGardenAccepted && Has("watergarden_drawn"); } }
        public bool WaterGardenWatered { get { return WaterGardenDrawn && Has("watergarden_watered"); } }
        public bool WaterGardenComplete { get { return WaterGardenWatered && Has("watergarden_complete"); } }

        private WorldObject WaterGardenObject(string id, string kind)
        { return data.maps.Length == 0 ? null : Array.Find(data.maps[0].objects, o => o.id == id && o.kind == kind); }
        private bool WaterGardenNear(WorldObject host)
        {
            return host != null && save.room == 0 && mode == "world" && !Dying && !practicing && !InAbyss
                && save.health > 0 && Finite(save.health) && Finite(save.x) && Finite(save.y) && Fits(Position)
                && SafeTravel && hazards.Count == 0 && !projectiles.Exists(p => !p.friendly && p.life > 0)
                && Array.IndexOf(Map.objects, host) >= 0 && IsObjectPresent(host)
                && ReferenceEquals(Nearest(), host) && ClearAttackPath(Position, new Point(host.x, host.y));
        }
        public bool AcceptWaterGarden()
        {
            if (!WaterGardenReady || WaterGardenAccepted || !WaterGardenNear(WaterGardenObject("smith", "forge"))) return false;
            Flag("watergarden_accept"); TrackQuest("watergarden");
            RecordMoment("诺尔修好的水泵开始出水了。替他送一桶清水到杏的药田。", "Noll's repaired pump is flowing. Take a bucket of clean water to An's garden.");
            Notify("到镇井东侧的取水桶取水。", "Draw water at the bucket east of the town well."); return true;
        }
        public bool DrawGardenWater()
        {
            if (!WaterGardenAccepted || WaterGardenDrawn || !WaterGardenNear(WaterGardenObject("watergarden_draw", "waterDraw"))) return false;
            Flag("watergarden_drawn"); sound = "quest";
            Notify("清水已装好，送到药田东侧的试灌苗床。", "Bucket filled. Take it to the trial bed east of An's garden."); return true;
        }
        public bool WaterGardenBed()
        {
            if (!WaterGardenDrawn || WaterGardenWatered || !WaterGardenNear(WaterGardenObject("watergarden_bed", "waterBed"))) return false;
            Flag("watergarden_watered"); sound = "quest";
            RecordMoment("清水流进药田边的苗床。修好的水泵，终于接上了镇里的日常。", "Clean water reached the trial bed. The repaired pump is part of town life again.");
            Notify("试灌完成，找杏交回水桶并领取报酬。", "Trial bed watered. Return the bucket to An for your reward."); return true;
        }
        public bool FinishWaterGarden()
        {
            if (!WaterGardenWatered || WaterGardenComplete || !WaterGardenNear(WaterGardenObject("gardener", "garden"))) return false;
            if (save.inventory[7] > 9996 || save.tea >= 999)
            { Notify("请先留出种子3和凉茶1的存放空间，再来交付。", "Make room for 3 seeds and 1 tea, then return."); return false; }
            Flag("watergarden_complete"); save.inventory[7] += 3; save.tea++; sound = "reward";
            RecordMoment("杏收回水桶，留下三粒种子和一份凉茶。药田与井，从此又近了一些。", "An took back the bucket and gave you three seeds and a tea. The well and garden feel closer now.");
            Notify("清水到药田：种子3、凉茶1。", "Clear Water for the Garden: 3 seeds and 1 tea."); return true;
        }
        private void AppendWaterGardenChoice(List<Choice> choices, bool smith)
        {
            if (WaterGardenReady && (smith || WaterGardenAccepted))
                choices.Add(C(WaterGardenComplete ? "聊聊药田的清水" : "清水到药田", WaterGardenComplete ? "The garden's clean water" : "Clear Water for the Garden", "watergarden_offer"));
        }
        private void ShowWaterGardenOffer()
        {
            bool smith = WaterGardenNear(WaterGardenObject("smith", "forge"));
            bool gardener = WaterGardenNear(WaterGardenObject("gardener", "garden"));
            if (!WaterGardenReady || (!smith && !gardener) || (!smith && !WaterGardenAccepted)) return;
            var choices = new List<Choice>();
            if (!WaterGardenAccepted && smith) choices.Add(C("替诺尔送一桶清水", "Carry Noll's first bucket", "watergarden_accept"));
            if (WaterGardenWatered && !WaterGardenComplete && gardener) choices.Add(C("交回水桶 · 领取种子3、凉茶1", "Return bucket · 3 seeds, 1 tea", "watergarden_finish"));
            if (WaterGardenAccepted && !WaterGardenComplete) choices.Add(C("追踪清水到药田", "Track the garden water", "watergarden_track"));
            choices.Add(C("稍后再来", "Later", "close"));
            string zh = WaterGardenComplete ? "杏：清水已经用上了，水桶也收好了。苗床边留着水痕，下一季可以多留几株药草给邻居。谢谢你把诺尔的心意带过来。" :
                !WaterGardenAccepted ? "诺尔：你带回来的零件已经装上了，井又能出清水了。杏的药草也已配好药。请替我从井东侧的取水桶装一桶清水，送去药田东侧的试灌苗床，再把水桶交还给杏。\n\n不用再交零件或药草。杏为你留了种子3、凉茶1，只领取一次。原来的种植与水渠收益照常。" :
                !WaterGardenDrawn ? "先到镇井东侧的取水桶装水。诺尔已经修好水泵，不用再找零件。" :
                !WaterGardenWatered ? "水桶已经装满。把清水送到药田东侧的试灌苗床，再找杏交回水桶。" :
                "试灌苗床已经浇好。杏：现在把水桶交给我吧，种子3和凉茶1是给你的谢礼。\n\n这是独立的苗床，不会改变你原来的作物、生长时间或工程收益。";
            string en = WaterGardenComplete ? "An: The water is in use and the bucket is back. The damp trial bed will leave a few more herbs for our neighbors next season. Thank you for bringing Noll's kindness here." :
                !WaterGardenAccepted ? "Noll: Your parts are fitted; the well runs clean again. An has made her medicine too. Fill the bucket east of the well, water the trial bed east of her garden, then return the bucket to An.\n\nNo more parts or herbs needed. An offers 3 seeds and 1 tea, once. Ordinary crops and canal benefits are unchanged." :
                !WaterGardenDrawn ? "Fill the bucket east of the town well. Noll has already repaired the pump; no more parts needed." :
                !WaterGardenWatered ? "Your bucket is full. Water the trial bed east of An's garden, then return the bucket to her." :
                "The trial bed is watered. An: Return the bucket and take 3 seeds and 1 tea as thanks.\n\nThis separate bed leaves your ordinary crops, growth time and town projects unchanged.";
            Dialog("清水到药田", "Clear Water for the Garden", zh, en, choices.ToArray());
        }
        private bool TalkWaterGarden(WorldObject target)
        {
            if (target.kind != "waterDraw" && target.kind != "waterBed") return false;
            bool draw = target.kind == "waterDraw";
            if (!ReferenceEquals(target, WaterGardenObject(draw ? "watergarden_draw" : "watergarden_bed", target.kind)) || !WaterGardenNear(target)) return true;
            Dialog(target.name, target.en,
                draw ? "诺尔修好的水泵把清水送到了井边。装满这只水桶，带去药田东侧的试灌苗床。不消耗物资。" : "把清水缓缓倒进这块试灌苗床，看看水能否流到根边。这里与原来的种植田分开，不消耗种子，也不改变作物的生长。",
                draw ? "The repaired pump brings clean water to the well. Fill this bucket for the trial bed east of An's garden. No supplies spent." : "Pour clean water into the trial bed and let it reach the roots. This separate bed uses no seeds and does not change ordinary crops.",
                C(draw ? "装一桶清水" : "给苗床试灌", draw ? "Fill the bucket" : "Water the trial bed", draw ? "watergarden_draw" : "watergarden_water"), C("稍后", "Later", "close")); return true;
        }
        private bool ChooseWaterGarden(string action)
        {
            if (action == null || !action.StartsWith("watergarden_", StringComparison.Ordinal)) return false;
            if (action == "watergarden_offer") ShowWaterGardenOffer();
            else if (action == "watergarden_accept") AcceptWaterGarden();
            else if (action == "watergarden_draw") DrawGardenWater();
            else if (action == "watergarden_water") WaterGardenBed();
            else if (action == "watergarden_finish") FinishWaterGarden();
            else if (action == "watergarden_track" && WaterGardenAccepted && (WaterGardenNear(WaterGardenObject("smith", "forge")) || WaterGardenNear(WaterGardenObject("gardener", "garden")))) TrackQuest("watergarden");
            return true;
        }
        private QuestGuide GetWaterGardenQuest()
        {
            WorldObject host = !WaterGardenAccepted ? WaterGardenObject("smith", "forge") : !WaterGardenDrawn ? WaterGardenObject("watergarden_draw", "waterDraw") :
                !WaterGardenWatered ? WaterGardenObject("watergarden_bed", "waterBed") : WaterGardenObject("gardener", "garden");
            var quest = new QuestGuide { id = "watergarden", title = L("清水到药田", "Clear Water for the Garden"), category = L("镇中后续", "TOWN FOLLOW-UP"),
                description = L("让修好的水泵接上药田的日常。", "Bring the repaired well into the garden's daily life."), reward = L("种子3 / 凉茶1", "3 seeds / 1 tea"),
                room = 0, target = host == null ? new Point(7, 8) : new Point(host.x, host.y), locked = !WaterGardenReady, complete = WaterGardenComplete };
            quest.steps.Add(new QuestStep(L("向诺尔接下送水", "Accept Noll's water delivery"), WaterGardenAccepted));
            quest.steps.Add(new QuestStep(L("到井东侧装水", "Fill the bucket east of the well"), WaterGardenDrawn));
            quest.steps.Add(new QuestStep(L("给药田东侧苗床试灌", "Water the eastern trial bed"), WaterGardenWatered));
            quest.steps.Add(new QuestStep(L("向杏交回水桶", "Return the bucket to An"), WaterGardenComplete));
            quest.next = !WaterGardenReady ? L("先完成杏与诺尔的委托", "Finish An's and Noll's commissions first") : !WaterGardenAccepted ? L("找诺尔聊聊修好的水泵", "Ask Noll about the repaired pump") :
                !WaterGardenDrawn ? L("到镇井东侧取水", "Draw water east of the town well") : !WaterGardenWatered ? L("将清水送到药田东侧苗床", "Carry clean water to the eastern trial bed") :
                !WaterGardenComplete ? L("找杏交回水桶并领取报酬", "Return the bucket to An for your reward") : L("清水已接上药田的日常", "Clean water is part of garden life again");
            return quest;
        }
    }
}
