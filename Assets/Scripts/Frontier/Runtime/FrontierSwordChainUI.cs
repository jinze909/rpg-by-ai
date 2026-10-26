using System;
using System.Globalization;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private bool SwordChainVisible
        {
            get { return Game.mode == "world" && !Game.practicing && !Game.SafeTrainingActive && !Game.Dying
                && !applicationPaused && !VisualReviewPaused && !TrainingReview && Game.save.weapon == 0
                && (Game.SwordChainRemaining > 0 || Game.SwordFinisherCommitted); }
        }
        private Rect SwordChainBounds()
        { return new Rect(CombatHud ? 20 : 24, 207, CombatHud ? 260 : 298, 20); }
        private bool SameSwordChainRegion(Rect rect)
        {
            Rect chain = SwordChainBounds();
            return rect.x == chain.x && rect.y == chain.y && rect.width == chain.width && rect.height == chain.height;
        }
        private void DrawSwordChainHud()
        {
            if (!SwordChainVisible) return;
            Rect rect = SwordChainBounds();
            Color color = Game.SwordFinisherCommitted || Game.SwordChainStep == 2 ? gold : mint;
            Box(rect.x, rect.y, rect.width, rect.height, new Color(.035f, .07f, .10f, ThreatOver(rect) ? .12f : .85f));
            Frame(rect.x, rect.y, rect.width, rect.height, color);
            string multiplier = GameCore.SwordFinisherMultiplier.ToString("0.##", CultureInfo.InvariantCulture);
            string text;
            if (Game.SwordFinisherCommitted)
                text = (Game.SwordFinisherReleased ? Game.L("重斩已释放 · 招式 ×", "HEAVY RELEASED · move ×")
                    : Game.L("重斩起手 · 招式 ×", "HEAVY WINDUP · move ×")) + multiplier;
            else
            {
                int step = Game.SwordChainStep, next = step % 3 + 1;
                string remaining = (Math.Ceiling(Game.SwordChainRemaining * 10) / 10).ToString("0.0", CultureInfo.InvariantCulture);
                text = Game.L("连段 ", "CHAIN ") + step + Game.L("/3 · 下一招", "/3 · next ") + next
                    + (next == 3 ? " ×" + multiplier : "") + " · " + remaining + Game.L("秒", "s");
            }
            Text(rect.x + 6, rect.y, rect.width - 12, 18, text, 11, color, TextAnchor.MiddleCenter);
            Bar(rect.x + 2, rect.y + 18, rect.width - 4, Math.Min(1, Game.SwordChainRemaining / GameCore.ComboWindow), color, 2);
        }
    }
}
