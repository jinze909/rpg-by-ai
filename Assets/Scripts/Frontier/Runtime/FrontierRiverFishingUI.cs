using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private bool DrawRiverFishingObject(WorldObject obj)
        {
            if (obj.kind != "riverFishing0" && obj.kind != "riverFishing1") return false;
            Point p = ScreenPoint(new Point(obj.x, obj.y));
            Color color = Game.RiverLessonRecorded(obj.kind == "riverFishing0" ? 0 : 1) ? mint : gold;
            Ring(new Point(obj.x, obj.y), .42f, color, 16);
            Icon("fish", p.x - 20, p.y - 46, 40, color);
            return true;
        }

        private void DrawRiverFishing()
        {
            bool reeling = Game.RiverPhase == RiverFishingPhase.Reeling;
            bool waiting = Game.RiverPhase == RiverFishingPhase.Waiting;
            Window(Game.L("河岸练钓", "River fishing"),
                Game.RiverFishingSite == 0 ? Game.L("西侧水边 · 老潮的练习", "Western pond · Old Tide's lesson")
                                          : Game.L("河岸流水 · 老潮的练习", "River current · Old Tide's lesson"));
            Card(279, 179, 938, 510);
            Icon("fish", 704, 195, 82);
            Text(307, 282, 875, 39,
                waiting ? Game.L("等候浮标", "Wait for the float") : reeling ? Game.L("选择时机收线", "Choose when to reel") : Game.L("放松钓线", "Let the line rest"),
                25, reeling ? mint : gold, TextAnchor.MiddleCenter);
            Text(307, 327, 875, 31,
                Game.L("阶段剩余 ", "Phase remaining ") + Game.RiverPhaseRemaining.ToString("0.0") + "s",
                16, muted, TextAnchor.MiddleCenter);
            Text(307, 369, 875, 36,
                Game.L("成功 ", "Good pulls ") + Game.fishHits + "/3   ·   " + Game.L("失误 ", "Misses ") + Game.fishMisses + "/3",
                22, paper, TextAnchor.MiddleCenter);
            const float bandX = 335, bandWidth = 826;
            Box(bandX, 436, bandWidth, 37, ink);
            Box(bandX + bandWidth * (Game.RiverFishingTarget - GameCore.RiverFishingHalfWidth), 436,
                bandWidth * GameCore.RiverFishingHalfWidth * 2, 37, mint);
            Box(bandX + Game.fishCursor * bandWidth - 3, 425, 6, 60, gold);
            string hint = waiting ? Game.L("等候时无需按键。浮标移动后，每轮只收线一次。", "Wait without pressing. When the float moves, reel once per round.")
                : reeling ? Game.L("浮标进入青绿色区时收线一次；时间用完算一次失误。", "Reel once in the teal band. Running out of time counts as one miss.")
                          : Game.L("本轮已经结束。放松钓线，等候下一轮。", "This round is over. Let the line rest before the next round.");
            Text(321, 500, 849, 55, hint, 16, paper, TextAnchor.MiddleCenter);
            Text(321, 560, 849, 30, Game.L("三次成功获得银鳍鱼；三次失误结束。无需鱼饵。", "Three good pulls earn Silverfin; three misses end the cast. No bait needed."),
                14, muted, TextAnchor.MiddleCenter);
            if (Button(473, 611, 543, 52, Game.L("收线  ", "Reel  ") + ControlKey("E / SPACE", "A"), true, reeling)) Game.FishHit();
        }
    }
}
