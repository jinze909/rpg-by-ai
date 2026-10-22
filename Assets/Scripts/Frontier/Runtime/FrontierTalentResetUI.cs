using System;
using System.Globalization;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private TalentResetQuote talentResetConfirmation;
        private GameCore talentResetGame;
        private SaveData talentResetSave;
        private int talentResetGeneration;

        private void ClearTalentResetConfirmation()
        { talentResetConfirmation = null; talentResetGame = null; talentResetSave = null; talentResetGeneration = 0; }
        private void ValidateTalentResetConfirmation()
        {
            if (talentResetConfirmation != null && (Game == null || Game.mode != "talents" || Game.Dying
                || !ReferenceEquals(Game, talentResetGame) || !ReferenceEquals(Game.save, talentResetSave)
                || Game.WorldGeneration != talentResetGeneration)) ClearTalentResetConfirmation();
        }
        private void BeginTalentResetConfirmation()
        {
            if (Game == null || Game.mode != "talents" || Game.Dying || applicationPaused || VisualReviewPaused) return;
            talentResetConfirmation = Game.GetTalentResetQuote(); talentResetGame = Game;
            talentResetSave = Game.save; talentResetGeneration = Game.WorldGeneration;
        }
        private static string TalentHealth(float value)
        { return float.IsNaN(value) || float.IsInfinity(value) ? "-" : value.ToString("0.##", CultureInfo.InvariantCulture); }
        private void DrawTalentResetConfirmation()
        {
            TalentResetQuote quote = talentResetConfirmation;
            if (quote == null) return;
            bool current = Game.CanResetTalents(quote);
            bool enabled = current && !applicationPaused && !VisualReviewPaused;
            drawingModal = true; drawnControls.Clear(); Dim(); Card(340, 172, 600, 470);
            Text(368, 193, 544, 40, Game.L("重置三项天赋？", "Reset all talents?"), 25, gold);
            Text(368, 247, 544, 36, Game.L("费用：20硬币 · 余额 ", "Cost: 20 coins · balance ") + quote.coinsBefore
                + (quote.available ? " → " + (quote.coinsBefore - quote.cost) : ""), 17, paper);
            Text(368, 289, 544, 48, Game.L("返还技能点 ", "Refund ") + quote.refund
                + Game.L(" · 可用点数 ", " points · unused ") + quote.pointsBefore + " → " + quote.pointsAfter, 18, mint);
            Text(368, 340, 544, 36, Game.L("锋芒 / 坚韧 / 轻盈：", "Edge / Resolve / Agility: ")
                + quote.edge + " / " + quote.resolve + " / " + quote.agility + " → 0 / 0 / 0", 15, muted);
            Text(368, 385, 544, 56, Game.L("最大生命 ", "Max health ") + TalentHealth(quote.maxHealthBefore) + " → " + TalentHealth(quote.maxHealthAfter)
                + "\n" + Game.L("当前生命 ", "Current health ") + TalentHealth(quote.healthBefore) + " → " + TalentHealth(quote.healthAfter), 18, paper);
            Text(368, 453, 544, 47, Game.L("已学战技、打法、熟练度、遗物与装备方案保留。", "Learned techniques, styles, mastery, relics and plans stay."), 15, muted);
            string reason = !quote.available ? Game.L(quote.reasonZh, quote.reasonEn) : !current
                ? Game.L("成长数值已变化，请取消后重新查看。", "Values changed. Cancel and review a fresh quote.")
                : Game.L("确认后付费；取消不会改变成长。", "Confirm pays the cost; cancel keeps your build.");
            Text(368, 511, 544, 45, reason, 15, current ? paper : gold);
            // Cancellation is deliberately first in keyboard/controller focus.
            if (Button(368, 577, 260, 42, Game.L("取消", "Cancel"))) { ClearTalentResetConfirmation(); return; }
            if (Button(652, 577, 260, 42, Game.L("支付20硬币并重置", "Pay 20 coins and reset"), true, enabled))
            {
                bool changed = Game.TryResetTalents(quote); ClearTalentResetConfirmation();
                if (changed) Save();
            }
        }
    }
}
