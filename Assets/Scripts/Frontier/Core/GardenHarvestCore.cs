using System;
using System.Collections.Generic;

namespace Starfall
{
    public sealed class GardenHarvestQuote
    {
        public readonly bool planted, ready, canHarvest;
        public readonly float remaining;
        public readonly int herbs, seeds, coins, herbsBefore, seedsBefore, coinsBefore;
        public readonly long herbsAfter, seedsAfter, coinsAfter;
        public readonly string reasonZh, reasonEn;
        internal GardenHarvestQuote(GameCore game)
        {
            SaveData s = game.save;
            planted = s.plantedAt >= 0; remaining = planted ? Math.Max(0, 60 - (s.totalTime - s.plantedAt)) : 60;
            ready = planted && remaining <= 0; herbs = game.GardenHerbYield; seeds = 2; coins = 8;
            herbsBefore = s.inventory[2]; seedsBefore = s.inventory[7]; coinsBefore = s.coins;
            herbsAfter = (long)herbsBefore + herbs; seedsAfter = (long)seedsBefore + seeds; coinsAfter = (long)coinsBefore + coins;
            reasonZh = ""; reasonEn = "";
            if (!planted) { reasonZh = "先种下一粒种子。"; reasonEn = "Plant one seed first."; }
            else if (!ready) { reasonZh = "还需" + (int)Math.Ceiling(remaining) + "秒。"; reasonEn = "Ready in " + (int)Math.Ceiling(remaining) + "s."; }
            else if (herbsBefore < 0 || seedsBefore < 0 || coinsBefore < 0)
            { reasonZh = "物资状态无法收获，作物保留。"; reasonEn = "Invalid stock; crop retained."; }
            else if (herbsAfter > 9999)
            { reasonZh = "药草上限9999；先使用一些，成熟作物保留。"; reasonEn = "Herb limit 9999: use some first. Ripe crop retained."; }
            else if (seedsAfter > 9999)
            { reasonZh = "种子上限9999；可在杏处归还多余种子，成熟作物保留。"; reasonEn = "Seed limit 9999: return spare seeds to An. Ripe crop retained."; }
            else if (coinsAfter > 999999)
            { reasonZh = "硬币上限999999；先使用一些，成熟作物保留。"; reasonEn = "Coin limit 999999: spend some first. Ripe crop retained."; }
            canHarvest = ready && reasonZh.Length == 0;
        }
    }
    public sealed partial class GameCore
    {
        private Conversation gardenSeedReview;
        public int GardenHerbYield { get { return 5 + save.projects[0] * 2 + (save.frontierEnding == 1 ? 2 : 0); } }
        public GardenHarvestQuote GetGardenHarvestQuote() { return new GardenHarvestQuote(this); }
        private bool HarvestGarden()
        {
            GardenHarvestQuote quote = GetGardenHarvestQuote();
            if (!quote.canHarvest) { Notify(quote.reasonZh, quote.reasonEn); return false; }
            save.inventory[2] += quote.herbs; save.inventory[7] += quote.seeds; save.coins += quote.coins;
            save.plantedAt = -1; revision++; sound = "reward";
            Notify("收获：月光草 ×" + quote.herbs + "，种子 ×2，硬币 +8。", "Harvest: " + quote.herbs + " Moonherbs, 2 seeds, 8 coins.");
            OnFirstHarvestCrop(); return true;
        }
        private string GardenHarvestSummary(bool english)
        {
            GardenHarvestQuote q = GetGardenHarvestQuote();
            string state = !q.planted ? (english ? "Not planted; grows in 60s." : "尚未种植；生长60秒。")
                : !q.ready ? (english ? q.reasonEn : q.reasonZh) : (english ? "Ripe crop ready." : "作物已成熟。");
            string text = english ? "Garden: " + state + "\nHarvest: herbs +" + q.herbs + ", seeds +2, coins +8."
                : "药田：" + state + "\n收成：药草+" + q.herbs + "、种子+2、硬币+8。";
            if (q.planted)
                text += english ? "\nAfter harvest: herbs " + q.herbsBefore + " → " + q.herbsAfter + "; seeds " + q.seedsBefore + " → " + q.seedsAfter + "; coins " + q.coinsBefore + " → " + q.coinsAfter + "."
                    : "\n收获后：药草" + q.herbsBefore + " → " + q.herbsAfter + "；种子" + q.seedsBefore + " → " + q.seedsAfter + "；硬币" + q.coinsBefore + " → " + q.coinsAfter + "。";
            else text += english ? "\nPlanting costs 1 seed. Yield uses the canal and covenant at harvest time." : "\n种植消耗种子1，收成按收获时的水渠和协定计算。";
            if (q.ready && !q.canHarvest) text += "\n" + (english ? q.reasonEn : q.reasonZh);
            return text;
        }
        private void AppendGardenHarvestConversation()
        {
            if (conversation == null) return;
            conversation.text += "\n\n" + GardenHarvestSummary(false);
            conversation.textEn += "\n\n" + GardenHarvestSummary(true);
            var choices = new List<Choice>(conversation.choices);
            choices.Add(C("整理多余种子", "Spare seeds", "garden_seed_menu")); conversation.choices = choices.ToArray();
        }
        private WorldObject GardenSeedHost
        { get { return save.room == 0 ? Array.Find(Map.objects, o => o.kind == "garden") : null; } }
        private bool AtGardenSeedHost
        { get { WorldObject host = GardenSeedHost; return host != null && (mode == "world" || mode == "dialog") && !Dying && !practicing && !InAbyss && save.health > 0 && ReferenceEquals(Nearest(), host); } }
        public bool ShowGardenSeedReturn()
        {
            if (mode != "world" || !AtGardenSeedHost) return false;
            int stock = save.inventory[7];
            string balance = stock >= 3 ? stock + " → " + (stock - 3) : stock + " / 3";
            Dialog("杏 · 多余种子", "An · spare seeds", "归还3粒种子，不获得硬币或其他报酬。可为下一份收成留出空间。\n\n种子 " + balance + (stock < 3 ? "，种子不足。" : "。") + "\n未确认前不会使用种子，田里作物保留。",
                "Return 3 seeds with no coins or other reward. This can make room for the next harvest.\n\nSeeds " + balance + (stock < 3 ? "; need 3." : ".") + "\nNo seeds are used before confirmation; your crop stays planted.",
                C("先不归还", "Keep seeds", "garden_back"), C("确认归还种子 ×3 · 无报酬", "Return 3 seeds · no reward", "garden_return3")); gardenSeedReview = conversation; return true;
        }
        public bool ReturnGardenSeeds()
        {
            if (mode != "dialog" || gardenSeedReview == null || !ReferenceEquals(conversation, gardenSeedReview) || !AtGardenSeedHost || save.inventory[7] < 3) return false;
            gardenSeedReview = null;
            save.inventory[7] -= 3; revision++; sound = "reward";
            Notify("已归还种子3，未获得报酬。", "Returned 3 seeds; no reward."); return true;
        }
        private bool GardenHarvestChoiceEnabled(string action)
        {
            if (action == "harvest") { GardenHarvestQuote q = GetGardenHarvestQuote(); return !q.ready || q.canHarvest; }
            if (action == "garden_return3") return gardenSeedReview != null && ReferenceEquals(conversation, gardenSeedReview) && AtGardenSeedHost && save.inventory[7] >= 3;
            return true;
        }
        private bool ChooseGardenHarvest(string action)
        {
            if (action == "garden_seed_menu") { ShowGardenSeedReturn(); return true; }
            if (action == "garden_back") { WorldObject host = GardenSeedHost; if (AtGardenSeedHost && host != null) Talk(host); return true; }
            return false;
        }
        private string GardenHarvestChoiceLabel(Choice choice)
        {
            GardenHarvestQuote q = GetGardenHarvestQuote();
            return q.ready ? L("收获 · 药草" + q.herbs + " / 种子2 / 硬币8", "Harvest · " + q.herbs + " herbs / 2 seeds / 8 coins")
                : L("查看生长 · " + (int)Math.Ceiling(q.remaining) + "秒", "Check growth · " + (int)Math.Ceiling(q.remaining) + "s");
        }
    }
}
