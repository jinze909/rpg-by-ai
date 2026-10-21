using System;
using System.Globalization;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private bool GuardStatusVisible
        {
            get { return Game.mode == "world" && !Game.practicing && !Game.Dying && !applicationPaused && !VisualReviewPaused
                && !TrainingReview && Game.guardTime > 0 && !float.IsNaN(Game.guardTime) && !float.IsInfinity(Game.guardTime); }
        }
        private Rect GuardStatusBounds()
        {
            // Use the free side-status lane below road meals and above the
            // tutorial. Keep central story, arrival and resume banners clear.
            return new Rect(24, 386, 298, 24);
        }
        private bool SameGuardStatusRegion(Rect rect)
        {
            Rect status = GuardStatusBounds();
            return rect.x == status.x && rect.y == status.y && rect.width == status.width && rect.height == status.height;
        }
        private void DrawGuardStatusHud()
        {
            if (!GuardStatusVisible) return;
            Rect rect = GuardStatusBounds();
            Color fill = new Color(.035f, .07f, .10f, .92f);
            if (ThreatOver(rect)) fill.a = .12f;
            Box(rect.x, rect.y, rect.width, rect.height, fill);
            Frame(rect.x, rect.y, rect.width, rect.height, mint);
            // Read the shared guard clock. Sources may replace or extend it;
            // presentation must never keep a separate timer or imply immunity.
            string time = (Math.Ceiling(Game.guardTime * 10) / 10).ToString("0.0", CultureInfo.InvariantCulture);
            Text(rect.x + 8, rect.y, rect.width - 16, rect.height,
                Game.L("防护 ", "GUARD ") + time + Game.L("秒 · 受到伤害 ×", "s · incoming damage ×")
                + GameCore.GuardIncomingMultiplier.ToString("0.##", CultureInfo.InvariantCulture), 12, mint, TextAnchor.MiddleCenter);
        }
    }
}
