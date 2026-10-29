using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private void DrawFishingFeedback()
        {
            FishingPullFeedback pull = Game.RecentFishingPull;
            if (pull == null) return;
            bool good = pull.outcome == FishingPullOutcome.Good;
            Color color = good ? mint : gold;
            string text = good ? Game.L("收线成功 · 成功 ", "Good pull · ") + pull.goodPulls + "/3"
                : pull.outcome == FishingPullOutcome.Left ? Game.L("收线在左侧 · 失误 ", "Left of band · miss ") + pull.misses + "/3"
                : pull.outcome == FishingPullOutcome.Right ? Game.L("收线在右侧 · 失误 ", "Right of band · miss ") + pull.misses + "/3"
                : Game.L("错过收线 · 失误 ", "Window missed · miss ") + pull.misses + "/3";
            // The small marker captures the accepted pull; the live float keeps
            // its original motion and the original band/buttons stay in place.
            float x = 335 + 826 * pull.cursor;
            Box(x - 5, 477, 10, 3, color);
            Box(x - 1, 480, 2, 4, color);
            Text(321, 665, 849, 22, text, 14, color, TextAnchor.MiddleCenter);
        }
    }
}
