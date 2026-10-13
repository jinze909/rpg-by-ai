using System;
using System.Collections.Generic;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private static readonly Rect SparringActiveEnd = new Rect(774,132,144,30);
        private static readonly Rect SparringNext = new Rect(366,388,172,42);
        private static readonly Rect SparringRetry = new Rect(553,388,172,42);
        private static readonly Rect SparringQuietEnd = new Rect(740,388,172,42);

        private void AddSparringPointerRegions(List<Rect> regions)
        {
            if (!Game.SparringActive || Game.mode != "world") return;
            // Status and advice pass aiming through. Only actual controls stop
            // a held mouse attack from firing through a training button.
            if (Game.SparringAwaitingNext || Game.SparringFinished)
            {
                if (!Game.SparringFinished) regions.Add(SparringNext);
                regions.Add(SparringRetry); regions.Add(SparringQuietEnd);
            }
            else regions.Add(SparringActiveEnd);
        }
        private string SparringScore()
        {
            // A clear completes each drill. These are cumulative observations,
            // never technique quotas or a required profession-specific style.
            string counters = Game.L("反击 ","Counters ")+Game.SparringCounters;
            string breaks = Game.L("破势 ","Breaks ")+Game.SparringBreaks;
            string flanks = Game.L("绕后 ","Flanks ")+Game.SparringFlanks;
            if (Game.SparringFinished || Game.SparringStage == 2) return counters+" · "+breaks+" · "+flanks;
            return Game.SparringStage == 0 ? counters : breaks+" · "+flanks;
        }
        private string SparringAttemptStats()
        {
            return Game.L("受击 ","Hits taken ")+Game.SparringHitsTaken+" · "+Game.SparringTime.ToString("0.0")+"s";
        }
        private string SparringInputTip()
        {
            if (Game.SparringStage == 1)
                return controllerUsed ? Game.L("Y职业战技 · A闪避", "Y: class technique · A: dodge") : Game.L("Q职业战技 · 空格闪避", "Q: class technique · Space: dodge");
            if (controllerUsed)
                return Game.save.controllerAimProfile == 2
                    ? Game.L("X攻击 · A闪避 · 左摇杆朝向", "X: attack · A: dodge · left stick faces")
                    : Game.L("X攻击 · A闪避 · 右摇杆瞄准", "X: attack · A: dodge · right stick aims");
            return Game.L("J / 左键攻击 · 空格闪避 · 鼠标瞄准", "J / click: attack · Space: dodge · mouse aims");
        }
        private void DrawSparringPresentation()
        {
            if (!Game.SparringActive || Game.mode != "world") return;
            bool quiet = Game.SparringAwaitingNext || Game.SparringFinished;
            if (!quiet)
            {
                Panel(344,22,592,173,panel,mint);
                Text(360,31,560,28,Game.L("训练 ","Drill ")+(Game.SparringStage+1)+"/3 · "+Game.SparringTitle,18,gold);
                Text(360,63,560,44,Game.SparringHint,14,paper);
                Text(360,107,560,21,SparringInputTip(),12,muted);
                Text(360,133,398,24,SparringScore(),13,mint);
                Text(360,166,560,23,SparringAttemptStats(),13,muted);
                if (Button(SparringActiveEnd.x,SparringActiveEnd.y,SparringActiveEnd.width,SparringActiveEnd.height,Game.L("结束训练","End drills"))) Game.EndPractice();
                return;
            }
            Panel(344,190,592,260,panel,Game.SparringFinished ? gold : mint);
            Text(360,206,560,32,Game.SparringFinished ? Game.L("战术训练完成","Tactics drills complete") : Game.L("本项完成 · ","Drill complete · ")+Game.SparringTitle,20,gold);
            Text(360,249,560,56,Game.SparringHint,15,paper);
            Text(360,312,560,27,SparringScore(),14,mint);
            Text(360,342,560,22,SparringAttemptStats(),13,muted);
            Text(360,366,560,22,controllerUsed ? Game.L("B：继续 / 返回训练场", "B: continue / return to the mentor") : Game.L("E / Enter：继续 / 返回训练场", "E / Enter: continue / return to the mentor"),13,muted);
            if (!Game.SparringFinished && Button(SparringNext.x,SparringNext.y,SparringNext.width,SparringNext.height,Game.L("下一项","Next drill"),true)) Game.ContinueSparring();
            if (Button(SparringRetry.x,SparringRetry.y,SparringRetry.width,SparringRetry.height,Game.L("重练全程","Restart all drills"))) Game.RestartSparring();
            if (Button(SparringQuietEnd.x,SparringQuietEnd.y,SparringQuietEnd.width,SparringQuietEnd.height,Game.L("返回训练场","Return to mentor"))) Game.EndPractice();
        }
    }
}
