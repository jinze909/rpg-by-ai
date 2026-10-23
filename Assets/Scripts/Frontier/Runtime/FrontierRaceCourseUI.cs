using System;
using System.Globalization;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private Rect RaceCourseSafeBounds
        { get { return Game.RaceCourseCancellationPending ? new Rect(345, 520, 360, 45) : new Rect(816, 601, 375, 42); } }
        private void OpenRaceCourseFromPause()
        {
            if (Game.mode != "pause" || !Game.racing || Game.save.room != 6 || Game.Dying || applicationPaused || VisualReviewPaused) return;
            Game.Close(); if (!Game.OpenRaceCourse()) OpenMenu("pause");
        }
        private void DrawRaceCourse()
        {
            Window(Game.L("风旗竞速", "Wind-flag course"), Game.L("先看清五旗顺序，再决定出发。查看路线不会计时。", "Read the five flags before launching. The clock pauses while reviewing."));
            if (!Game.RaceCourseActive)
            {
                Card(279, 190, 938, 300);
                Text(311, 230, 865, 88, Game.L("本次路线状态已改变，请返回沙舟重新查看。", "This course review has changed. Return to the skiff and open it again."), 22, paper);
                if (Button(816, 601, 375, 42, Game.L("返回", "Back"), true)) Game.Close();
                return;
            }
            if (Game.RaceCourseCancellationPending)
            {
                Card(315, 231, 815, 365);
                Text(345, 266, 740, 78, Game.L("结束本轮竞速？", "End this race attempt?"), 27, gold, TextAnchor.MiddleCenter);
                Text(345, 362, 740, 98, Game.L("本轮不会发放奖励。物资与当前位置保留，重新出发需要回到沙舟。", "This attempt grants no reward. Keep your supplies and current position; return to the skiff to launch again."), 20, paper, TextAnchor.MiddleCenter);
                // Safe action is the explicit initial keyboard/controller focus.
                if (Button(345, 520, 360, 45, Game.L("继续本轮", "Keep this attempt"), true)) Game.KeepRaceCourse();
                if (Button(725, 520, 360, 45, Game.L("确认结束本轮", "Confirm end attempt"))) Game.CancelRaceFromCourse();
                return;
            }
            Card(279, 177, 515, 430); Card(805, 177, 412, 510);
            Text(299, 186, 470, 26, Game.L("沙海风旗 · 按序通过", "Glass Dunes · pass flags in order"), 17, gold);
            DrawRaceCourseMap();
            Text(299, 548, 470, 47, Game.L("数字是经过顺序。自行选择道路，绕开地形；沙舟需要自己操作。", "Numbers show the order. Choose your path around terrain; steer the skiff yourself."), 14, muted);
            Text(816, 195, 375, 36, Game.racing ? Game.L("本轮已暂停", "ATTEMPT PAUSED") : Game.L("准备出发", "READY TO LAUNCH"), 22, gold);
            string clock = Game.racing ? Game.raceTime.ToString("0.0", CultureInfo.InvariantCulture) + " / 30s  ·  " + Game.raceCheckpoint + "/5"
                : Game.L("30秒内 · 五面风旗", "Five flags within 30 seconds");
            Text(816, 244, 375, 30, clock, 17, paper);
            Text(816, 281, 375, 31, Game.L("最快纪录 ", "Best time ") + (Game.save.bestRace > 0 ? (Game.save.bestRace / 10f).ToString("0.0", CultureInfo.InvariantCulture) + "s" : "—"), 16, muted);
            string[] zh = { "西南风旗", "西北风旗", "北侧风旗", "东侧风旗", "沙舟终点" };
            string[] en = { "Southwest flag", "Northwest flag", "Northern flag", "Eastern flag", "Skiff finish" };
            for (int i = 0; i < GameCore.RaceRoute.Length; i++)
                Text(825, 323 + i * 33, 356, 29, (i + 1) + ". " + Game.L(zh[i], en[i]) + (Game.racing && i < Game.raceCheckpoint ? "  ✓" : ""), 16,
                    Game.racing && i < Game.raceCheckpoint ? mint : Game.racing && i == Game.raceCheckpoint ? gold : paper);
            Text(816, 496, 375, 43, Game.racing ? Game.L("继续保留本轮进度；结束需要再次确认。", "Resume keeps this attempt. Ending requires confirmation.")
                : Game.RaceCourseCanLaunch ? Game.L("出发才开始计时。移动与闪避由你控制。", "Launch starts the clock. Move and dodge yourself.") : Game.L("请回到沙舟附近出发。", "Return beside the skiff to launch."), 14, muted);
            // Register the safe return before the destructive/launch control.
            if (Button(816, 601, 375, 42, Game.racing ? Game.L("继续本轮", "Resume attempt") : Game.L("返回沙舟", "Back to skiff"), true)) Game.Close();
            if (Game.mode == "racecourse" && Button(816, 550, 375, 40, Game.racing ? Game.L("结束本轮…", "End attempt…") : Game.L("出发！", "Launch!"), false, Game.racing || Game.RaceCourseCanLaunch))
            { if (Game.racing) Game.ReviewRaceCancellation(); else Game.LaunchRaceCourse(); }
            Card(279, 621, 515, 72);
            Text(294, 628, 485, 58, Game.L("首次完成：硬币25、碎片3", "First finish: 25 coins, 3 shards") + (Game.Has("race_finished") ? Game.L(" · 已领取", " · claimed") : "")
                + "\n" + Game.L("不超过16秒：碎片5、技能点1", "16s or less: 5 shards, 1 point") + (Game.Has("race_badge") ? Game.L(" · 已领取", " · claimed") : ""), 13, muted);
        }
        private void DrawRaceCourseMap()
        {
            const float x0 = 299, y0 = 217, cell = 12.5f;
            for (int y = 0; y < Game.Map.rows.Length; y++) for (int x = 0; x < Game.Map.rows[y].Length; x++)
            {
                char tile = Game.PresentationTile(x, y);
                Color color = tile == '~' ? PixelArt.C("#315c76") : tile == '#' ? PixelArt.C("#566879") : tile == '=' ? PixelArt.C("#a09877") : PixelArt.C(Game.Map.color);
                Box(x0 + x * cell, y0 + y * cell, cell, cell, color);
            }
            // No invented straight-line driving route: the five authored flags
            // are landmarks on the real terrain, not automatic waypoints.
            for (int i = 0; i < GameCore.RaceRoute.Length; i++)
            {
                Point flag = GameCore.RaceRoute[i]; float x = x0 + flag.x * cell, y = y0 + flag.y * cell;
                Color color = Game.racing && i < Game.raceCheckpoint ? mint : gold;
                Box(x - 7, y - 7, 14, 14, ink); Frame(x - 7, y - 7, 14, 14, color);
                Text(x - 16, y - 34, 32, 26, (i + 1).ToString(), 17, color, TextAnchor.MiddleCenter);
            }
            Box(x0 + Game.save.x * cell - 2, y0 + Game.save.y * cell - 2, 5, 5, paper);
        }
    }
}
