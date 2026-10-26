using System;
using System.Globalization;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private bool ProvisionStatusVisible
        {
            get { return Game.mode == "world" && !Game.practicing && !Game.SafeTrainingActive && !Game.Dying
                && !applicationPaused && !VisualReviewPaused && !TrainingReview
                && (Game.StewBoostRemaining > 0 || Game.TeaBoostRemaining > 0); }
        }
        private Rect ProvisionStatusBounds()
        {
            // Below focus controls, on the opposite side from enemy attack
            // hints. Keep their original left-hand lane unobstructed.
            return new Rect(CombatHud ? 1030 : 1008, CombatHud ? 184 : 568, CombatHud ? 230 : 248,
                Game.StewBoostRemaining > 0 && Game.TeaBoostRemaining > 0 ? 94 : 44);
        }
        private bool SameProvisionStatusRegion(Rect rect)
        {
            Rect status = ProvisionStatusBounds();
            return rect.x == status.x && rect.y == status.y && rect.width == status.width && rect.height == status.height;
        }
        private void DrawProvisionStatusHud()
        {
            if (!ProvisionStatusVisible) return;
            Rect rect = ProvisionStatusBounds();
            if (Game.StewBoostRemaining > 0)
            {
                DrawProvisionStatusRow(rect.x, rect.y, rect.width, "stew", Game.L("炖汤强化 ", "STEW BOOST "),
                    Game.StewBoostRemaining, Game.L("基础攻击 +", "Base attack +")
                    + GameCore.StewAttackBonus.ToString("0.##", CultureInfo.InvariantCulture), gold);
                rect.y += 50;
            }
            if (Game.TeaBoostRemaining > 0)
                DrawProvisionStatusRow(rect.x, rect.y, rect.width, "tea", Game.L("凉茶保护 ", "TEA WARD "),
                    Game.TeaBoostRemaining, Game.L("耐力回复 +", "EN +")
                    + GameCore.TeaRecoveryBonus.ToString("0.##", CultureInfo.InvariantCulture)
                    + Game.L("/秒 · 抗暑寒", "/s · heat/cold ward"), mint);
        }
        private void DrawProvisionStatusRow(float x, float y, float width, string icon, string title, float remaining, string benefit, Color color)
        {
            Rect row = new Rect(x, y, width, 44);
            Color fill = new Color(.035f, .07f, .10f, ThreatOver(row) ? .12f : .85f);
            Box(x, y, width, 44, fill); Frame(x, y, width, 44, color);
            Icon(icon, x + 8, y + 9, 24);
            // Round up only for display: a live fraction never reads as zero.
            Text(x + 41, y + 3, width - 49, 20,
                title + Math.Ceiling(remaining).ToString("0", CultureInfo.InvariantCulture) + Game.L("秒", "s"), 12, color);
            Text(x + 41, y + 23, width - 49, 18, benefit, 11, muted);
        }
    }
}
