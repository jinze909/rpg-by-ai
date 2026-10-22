using System;
using System.Globalization;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private static readonly Rect RangeControlsEntry = new Rect(344, 250, 592, 28);
        private GameCore rangeControlsGame;
        private SaveData rangeControlsSave;
        private SparringReturnRecord rangeControlsReturn;
        private int rangeControlsGeneration;

        private bool RangeControlSessionLive
        {
            get { return Game != null && Game.RangeActive && !Game.Dying && Game.save.room == 0
                && Game.save.health > 0 && !float.IsNaN(Game.save.health) && !float.IsInfinity(Game.save.health)
                && Game.save.sparringReturn != null && !applicationPaused && !VisualReviewPaused; }
        }
        private bool CanOpenRangeControls
        { get { return RangeControlSessionLive && (Game.mode == "world" || Game.mode == "pause" && settingsParent != "title"); } }
        private bool OwnsRangeControls
        {
            get { return RangeControlSessionLive && Game.mode == "rangecontrols"
                && ReferenceEquals(Game, rangeControlsGame) && ReferenceEquals(Game.save, rangeControlsSave)
                && ReferenceEquals(Game.save.sparringReturn, rangeControlsReturn) && Game.WorldGeneration == rangeControlsGeneration; }
        }
        private void ClearRangeControlOwner()
        { rangeControlsGame = null; rangeControlsSave = null; rangeControlsReturn = null; rangeControlsGeneration = 0; }
        private void OpenRangeControls()
        {
            if (!CanOpenRangeControls) return;
            rangeControlsGame = Game; rangeControlsSave = Game.save;
            rangeControlsReturn = Game.save.sparringReturn; rangeControlsGeneration = Game.WorldGeneration;
            OpenMenu("rangecontrols");
        }
        private bool ResumeOwnedRange()
        {
            // The menu owns exactly this attempt. Reused target IDs, a loaded
            // save or another visual-review world cannot inherit old controls.
            if (!OwnsRangeControls) return false;
            ClearRangeControlOwner(); Game.Close(); return true;
        }
        private void DrawRangeControls()
        {
            Window(Game.L("试装训练场控制", "Loadout range controls"),
                Game.L("记录暂停 · 选择目标、重置记录或返回", "Record paused · choose targets, reset or return"));
            if (Game.mode != "rangecontrols") return;
            bool owned = OwnsRangeControls;
            Card(279, 178, 938, 260);
            Text(301, 194, 894, 35, owned ? Game.RangeTitle : Game.L("这次试装已结束", "This attempt is no longer active"), 24, gold);
            Text(301, 244, 894, 36, owned
                ? Game.L("实际伤害 ", "HP damage ") + Game.RangeDamage.ToString("0.0", CultureInfo.InvariantCulture)
                    + Game.L(" · 命中 ", " · contacts ") + Game.RangeContacts + Game.L(" · 用时 ", " · time ")
                    + Game.RangeTime.ToString("0.0", CultureInfo.InvariantCulture) + "s"
                : Game.L("返回暂停菜单，再进入当前训练的控制页。", "Return to Pause, then open controls for the current range."), 17, paper);
            Text(301, 292, 894, 53, owned ? Game.RangeHint : "", 17, muted);
            string[] layouts = { Game.L("直线目标", "Line targets"), Game.L("聚集目标", "Cluster targets"), Game.L("正面重甲", "Front armor") };
            for (int i = 0; i < layouts.Length; i++)
                if (Button(279 + i * 318, 365, 301, 46, layouts[i], owned && Game.RangeLayout == i, owned && Game.RangeLayout != i)
                    && ResumeOwnedRange()) Game.ChangeRangeLayout(i);
            Text(279, 447, 938, 35, Game.L("换目标或重置会清空本次记录与已出手的攻击。", "Changing targets or resetting clears this attempt and its live attacks."), 15, muted);
            if (Button(279, 493, 938, 44, Game.L("重置本次记录", "Reset this attempt"), false, owned)
                && ResumeOwnedRange()) Game.ResetRange();
            Text(279, 555, 938, 62, Game.L("继续试装会保留当前目标与记录。结束后恢复进入前的位置、生命与耐力，自行调整的装备保留。训练没有报酬。",
                "Resume keeps the targets and record. Finishing restores your starting position, health and energy while keeping deliberate equipment changes. Training gives no rewards."), 16, paper);
            if (Button(279, 642, 460, 45, Game.L("继续试装", "Resume the range"), true, owned)) ResumeOwnedRange();
            if (Button(756, 642, 461, 45, Game.L("结束试装，返回", "Finish and return"), false, owned)
                && ResumeOwnedRange()) Game.EndPractice();
        }
    }
}
