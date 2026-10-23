using System;
using System.Collections.Generic;

namespace Starfall
{
    // A transient cost/effect review; permanent progress stays in save.projects.
    public sealed class TownProjectQuote
    {
        internal readonly GameCore owner;
        internal readonly SaveData saveOwner;
        internal readonly int generation, canal, waystation, workshop, ending;
        public readonly int project, tier, nextTier, wood, crystals, parts, coins;
        public readonly int woodBefore, crystalsBefore, partsBefore, coinsBefore;
        public readonly int trustBefore, trustAfter, tiersBefore, tiersAfter, harvestBefore, harvestAfter;
        public readonly string reasonZh, reasonEn;
        public bool available { get { return reasonZh.Length == 0; } }
        public bool capped { get { return tier == 2; } }
        internal TownProjectQuote(GameCore game, int project, bool valid, string zh, string en)
        {
            owner = game; saveOwner = game.save; generation = game.WorldGeneration; this.project = project;
            bool projects = saveOwner.projects != null && saveOwner.projects.Length == 3;
            canal = projects ? saveOwner.projects[0] : -1; waystation = projects ? saveOwner.projects[1] : -1; workshop = projects ? saveOwner.projects[2] : -1;
            tier = valid ? saveOwner.projects[project] : -1; nextTier = valid ? Math.Min(2, tier + 1) : -1;
            bool payment = valid && tier < 2;
            wood = payment ? 4 + tier * 2 : 0; crystals = payment ? 2 + tier : 0;
            parts = payment ? 3 + tier : 0; coins = payment ? 30 + tier * 20 : 0;
            bool stock = saveOwner.inventory != null && saveOwner.inventory.Length >= 5;
            woodBefore = stock ? saveOwner.inventory[0] : -1; crystalsBefore = stock ? saveOwner.inventory[1] : -1;
            partsBefore = stock ? saveOwner.inventory[4] : -1; coinsBefore = saveOwner.coins;
            trustBefore = saveOwner.reputation != null && saveOwner.reputation.Length == 3 ? saveOwner.reputation[2] : -1;
            trustAfter = payment ? Math.Min(10, trustBefore + 1) : trustBefore;
            tiersBefore = projects ? canal + waystation + workshop : 0; tiersAfter = tiersBefore + (payment ? 1 : 0);
            ending = saveOwner.frontierEnding;
            harvestBefore = 5 + Math.Max(0, canal) * 2 + (ending == 1 ? 2 : 0);
            harvestAfter = harvestBefore + (payment && project == 0 ? 2 : 0);
            reasonZh = zh; reasonEn = en;
        }
    }

    public sealed partial class GameCore
    {
        public string TownProjectName(int project)
        { return project < 0 || project > 2 ? L("城镇工程", "Town project") : L(new[] { "水渠与药田", "远行驿站", "记忆工坊" }[project], new[] { "Canals & gardens", "Waystation", "Memory workshop" }[project]); }
        public string TownProjectBenefit(int project, int tier)
        {
            if (project < 0 || project > 2 || tier < 0 || tier > 2) return L("设施状态无法使用。", "This facility state is invalid.");
            if (project == 0) return L("每次收获药草", "Herbs per harvest: ") + (5 + tier * 2 + (save.frontierEnding == 1 ? 2 : 0))
                + (tier == 2 ? L("；获赠琥珀种遗物。", "; grants Amber Seed.") : L("。", "."));
            if (project == 1) return tier == 0 ? L("野外营地休整尚未开放。", "Field-camp rest is locked.")
                : tier == 1 ? L("开放野外营地免费休整。", "Free field-camp rest enabled.") : L("免费休整；每天赠一杯凉茶。", "Free rest; one camp tea each day.");
            return tier == 0 ? L("遗物锻印尚未开放。", "Relic refining is locked.")
                : tier == 1 ? L("开放遗物锻印；每次硬币费20。", "Relic refining enabled; 20 coins per refine.") : L("开放遗物锻印；免除硬币费。", "Relic refining enabled; no coin fee.");
        }
        public TownProjectQuote GetTownProjectQuote(int project)
        {
            bool valid = project >= 0 && project <= 2 && save.projects != null && save.projects.Length == 3;
            if (valid) foreach (int tier in save.projects) valid &= tier >= 0 && tier <= 2;
            bool stock = save.inventory != null && save.inventory.Length >= 5 && save.inventory[0] >= 0 && save.inventory[1] >= 0 && save.inventory[4] >= 0 && save.coins >= 0;
            bool trust = save.reputation != null && save.reputation.Length == 3 && save.reputation[2] >= 0 && save.reputation[2] <= 10;
            string zh = "", en = "";
            if (!valid || !stock || !trust || Dying || practicing || !Finite(save.health) || save.health <= 0)
            { zh = "当前状态无法建设。"; en = "This state cannot build a project."; }
            else if (save.projects[project] == 2)
            { zh = "设施已完全升级，不再支付费用。"; en = "Fully upgraded; no further payment."; }
            else if (save.room != 0)
            { zh = "回到余烬镇才能建设。"; en = "Return to Emberfall to build."; }
            else
            {
                int tier = save.projects[project]; var missingZh = new List<string>(4); var missingEn = new List<string>(4);
                int[] need = { 4 + tier * 2, 2 + tier, 3 + tier, 30 + tier * 20 };
                int[] have = { save.inventory[0], save.inventory[1], save.inventory[4], save.coins };
                string[] names = { "木材", "星晶", "零件", "硬币" }, english = { " timber", " crystals", " parts", " coins" };
                for (int i = 0; i < need.Length; i++) if (have[i] < need[i])
                { missingZh.Add(names[i] + (need[i] - have[i])); missingEn.Add((need[i] - have[i]) + english[i]); }
                if (missingZh.Count > 0) { zh = "还需：" + string.Join(" / ", missingZh.ToArray()); en = "Need: " + string.Join(" / ", missingEn.ToArray()); }
            }
            return new TownProjectQuote(this, project, valid, zh, en);
        }
        public bool CanBuildProject(TownProjectQuote quote)
        {
            if (quote == null || !quote.available || mode != "camp" || !ReferenceEquals(quote.owner, this) || !ReferenceEquals(quote.saveOwner, save)
                || quote.generation != WorldGeneration || save.projects == null || save.projects.Length != 3
                || quote.canal != save.projects[0] || quote.waystation != save.projects[1] || quote.workshop != save.projects[2]
                || save.inventory == null || save.inventory.Length < 5 || quote.woodBefore != save.inventory[0]
                || quote.crystalsBefore != save.inventory[1] || quote.partsBefore != save.inventory[4] || quote.coinsBefore != save.coins
                || save.reputation == null || save.reputation.Length != 3 || quote.trustBefore != save.reputation[2]
                || quote.ending != save.frontierEnding) return false;
            return GetTownProjectQuote(quote.project).available;
        }
        public bool TryBuildProject(TownProjectQuote quote)
        { return CanBuildProject(quote) && BuildProject(quote.project); }
    }
}
