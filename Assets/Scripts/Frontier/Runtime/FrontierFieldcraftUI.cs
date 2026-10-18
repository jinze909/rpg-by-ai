using System;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private bool FieldcraftCounterVisible
        {
            get { return Game.mode == "world" && !Game.RangeActive && !applicationPaused && !VisualReviewPaused && !Game.Dying && Game.riposteTime > 0
                && !(TrainingReview); }
        }
        private Rect FieldcraftCounterBounds()
        { return new Rect(405, CombatHud ? 89 : 102, 470, CombatHud ? 22 : 24); }
        private bool SameFieldcraftCounterRegion(Rect rect)
        {
            Rect counter = FieldcraftCounterBounds();
            return rect.x == counter.x && rect.y == counter.y && rect.width == counter.width && rect.height == counter.height;
        }
        private void DrawFieldcraftWorld()
        {
            if (!FieldcraftCounterVisible) return;
            Rect rect = FieldcraftCounterBounds();
            Color fill = new Color(.035f, .07f, .10f, .92f);
            if (ThreatOver(rect)) fill.a = .12f;
            Box(rect.x, rect.y, rect.width, rect.height, fill);
            Frame(rect.x, rect.y, rect.width, rect.height, gold);
            string time = (Math.Ceiling(Game.riposteTime * 10) / 10).ToString("0.0");
            Text(rect.x + 8, rect.y, rect.width - 16, rect.height,
                Game.L("闪避反击 ", "COUNTER ") + time + Game.L("秒 · 下一次普通攻击 +50%", "s · next basic attack +50%"), 12, gold, TextAnchor.MiddleCenter);
        }
    }
}
