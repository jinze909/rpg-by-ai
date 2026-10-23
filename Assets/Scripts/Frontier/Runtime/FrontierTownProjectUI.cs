using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private TownProjectQuote townProjectConfirmation;
        private GameCore townProjectGame;
        private SaveData townProjectSave;
        private int townProjectGeneration;
        private void ClearTownProjectConfirmation()
        { townProjectConfirmation = null; townProjectGame = null; townProjectSave = null; townProjectGeneration = 0; }
        private void ValidateTownProjectConfirmation()
        {
            TownProjectQuote quote = townProjectConfirmation;
            if (quote != null && (Game == null || Game.mode != "camp" || Game.Dying || Game.practicing
                || !ReferenceEquals(townProjectGame, Game) || !ReferenceEquals(townProjectSave, Game.save)
                || townProjectGeneration != Game.WorldGeneration)) ClearTownProjectConfirmation();
        }
        private void BeginTownProjectConfirmation(int project)
        {
            if (Game == null || Game.mode != "camp" || Game.Dying || Game.practicing || applicationPaused || VisualReviewPaused) return;
            townProjectConfirmation = Game.GetTownProjectQuote(project);
            townProjectGame = Game; townProjectSave = Game.save; townProjectGeneration = Game.WorldGeneration;
        }
        private string TownProjectStock(string zh, string en, int before, int cost)
        {
            return Game.L(zh, en) + ": " + before + (before >= cost ? " → " + (before - cost) + Game.L(" · 费用", " · cost ") + cost
                : " / " + cost + Game.L(" · 还需", " · need ") + (cost - before));
        }
        private string TownProjectEffect(TownProjectQuote quote, bool next)
        {
            if (quote.project == 0) return Game.L("每次收获药草", "Herbs per harvest: ") + (next ? quote.harvestAfter : quote.harvestBefore)
                + ((next ? quote.nextTier : quote.tier) == 2 ? Game.L("；获赠琥珀种遗物。", "; grants Amber Seed.") : Game.L("。", "."));
            return Game.TownProjectBenefit(quote.project, next ? quote.nextTier : quote.tier);
        }
        private void DrawTownProjectConfirmation()
        {
            TownProjectQuote quote = townProjectConfirmation; if (quote == null) return;
            bool current = Game.CanBuildProject(quote), enabled = current && !applicationPaused && !VisualReviewPaused;
            drawingModal = true; drawnControls.Clear(); Dim(); Card(310, 145, 660, 555);
            Text(338, 164, 604, 50, Game.L("建设计划 · ", "Project plan · ") + Game.TownProjectName(quote.project), 25, gold);
            Text(338, 218, 604, 30, Game.L("等级 ", "Tier ") + quote.tier + " → " + quote.nextTier
                + Game.L(" · 山野议会 ", " · Wild Assembly ") + quote.trustBefore + " → " + quote.trustAfter, 17, mint);
            Text(338, 253, 604, 38, Game.L("当前：", "Now: ") + TownProjectEffect(quote, false), 16, muted);
            Text(338, 296, 604, 50, Game.L("升级后：", "After: ") + TownProjectEffect(quote, true), 18, paper);
            Text(338, 352, 604, 26, Game.L("确认前查看材料与余额", "Review materials and balance before confirming"), 16, gold);
            Text(338, 386, 604, 27, TownProjectStock("木材", "Timber", quote.woodBefore, quote.wood), 17, paper);
            Text(338, 416, 604, 27, TownProjectStock("星晶", "Crystals", quote.crystalsBefore, quote.crystals), 17, paper);
            Text(338, 446, 604, 27, TownProjectStock("零件", "Parts", quote.partsBefore, quote.parts), 17, paper);
            Text(338, 476, 604, 27, TownProjectStock("硬币", "Coins", quote.coinsBefore, quote.coins), 17, paper);
            string reason = !quote.available ? Game.L(quote.reasonZh, quote.reasonEn) : !current
                ? Game.L("材料或设施已变化。请取消后重新查看。", "Materials or facilities changed. Cancel and review a fresh plan.")
                : Game.L("确认后支付并建设；取消不消耗。设施总等级 ", "Confirm pays and builds; cancel spends nothing. Total tiers ") + quote.tiersBefore + " → " + quote.tiersAfter;
            Text(338, 516, 604, 70, reason, 16, current ? paper : gold);
            // Cancellation owns the initial keyboard/controller focus.
            if (Button(338, 626, 288, 46, Game.L("取消", "Cancel"))) { ClearTownProjectConfirmation(); return; }
            if (Button(654, 626, 288, 46, Game.L("确认建设", "Confirm construction"), true, enabled))
            {
                bool built = Game.TryBuildProject(quote); ClearTownProjectConfirmation(); if (built) Save();
            }
        }
    }
}
