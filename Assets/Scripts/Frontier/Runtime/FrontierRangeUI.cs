using System;
using System.Collections.Generic;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private static readonly Rect RangePanel = new Rect(344, 22, 592, 222);
        private static readonly Rect RangeReset = new Rect(360, 193, 268, 34);
        private static readonly Rect RangeEnd = new Rect(640, 193, 280, 34);
        private bool TrainingReview
        { get { return Game.SparringActive && (Game.SparringAwaitingNext || Game.SparringFinished) || Game.RangeActive && Game.RangeFinished; } }

        private void AddRangePointerRegions(List<Rect> regions)
        {
            if (!Game.RangeActive || Game.mode != "world" || Game.Dying) return;
            for (int i = 0; i < 3; i++) regions.Add(new Rect(360 + i * 188, 150, 176, 31));
            regions.Add(RangeReset); regions.Add(RangeEnd);
            regions.Add(RangeControlsEntry);
        }

        private void DrawRangePresentation()
        {
            if (!Game.RangeActive || Game.mode != "world") return;
            Panel(RangePanel.x, RangePanel.y, RangePanel.width, RangePanel.height, panel, Game.RangeFinished ? gold : mint);
            Text(360, 31, 560, 28, Game.L(Game.RangeFinished ? "目标已清场 · " : "试装训练场 · ", Game.RangeFinished ? "Targets cleared · " : "Loadout range · ") + Game.RangeTitle, 18, gold);
            Text(360, 65, 560, 36, Game.RangeFinished
                ? Game.L("记录已暂停。手动重置，或换一组目标再试。", "Results paused. Reset or choose another target layout.")
                : Game.RangeHint, 14, paper);
            Text(360, 106, 560, 24, Game.L("实际伤害 ", "HP damage ") + Game.RangeDamage.ToString("0.0") + "  ·  " + Game.L("命中 ", "Contacts ") + Game.RangeContacts, 14, mint);
            string rate = Game.RangeTime > .01f ? (Game.RangeDamage / Game.RangeTime).ToString("0.0") : "0.0";
            Text(360, 133, 560, 17, (Game.RangeStarted ? Game.RangeTime.ToString("0.0") + "s  ·  " + rate + "/s" : Game.L("出招后计时", "Timer starts with an attack")) + "  ·  " + Game.L("破势 ", "Breaks ") + Game.RangeBreaks + "  ·  " + Game.L("绕后 ", "Flanks ") + Game.RangeFlanks, 12, paper);
            string[] labels = { Game.L("直线目标", "Line targets"), Game.L("聚集目标", "Cluster targets"), Game.L("正面重甲", "Front armor") };
            for (int i = 0; i < 3; i++)
                if (Button(360 + i * 188, 150, 176, 31, labels[i], Game.RangeLayout == i, Game.RangeLayout != i)) Game.ChangeRangeLayout(i);
            if (Button(RangeReset.x, RangeReset.y, RangeReset.width, RangeReset.height, Game.L("重置本次记录", "Reset this attempt"))) Game.ResetRange();
            if (Button(RangeEnd.x, RangeEnd.y, RangeEnd.width, RangeEnd.height, Game.L("结束试装，返回", "Finish and return"))) Game.EndPractice();
            if (Button(RangeControlsEntry.x, RangeControlsEntry.y, RangeControlsEntry.width, RangeControlsEntry.height,
                Game.L("训练场控制 · ", "Range controls · ") + ControlKey("ESC", "START"), false, CanOpenRangeControls)) OpenRangeControls();
        }
    }
}
