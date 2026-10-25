using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private int relicTrailId;
        private readonly RectOffset relicSourcePadding = new RectOffset(4, 4, 2, 2);

        private bool RelicSourceButton(float x, float y)
        {
            RectOffset normal = buttonStyle.padding, disabled = disabledButtonStyle.padding;
            try
            {
                buttonStyle.padding = disabledButtonStyle.padding = relicSourcePadding;
                return Button(x, y, 69, 26, Game.L("来源", "Source"));
            }
            finally { buttonStyle.padding = normal; disabledButtonStyle.padding = disabled; }
        }

        private void OpenRelicTrail(int id)
        {
            if (Game.GetRelicTrail(id) == null) return;
            relicTrailId = id; OpenMenu("relictrail");
        }

        private void DrawRelicTrail()
        {
            RelicTrail trail = Game.GetRelicTrail(relicTrailId);
            if (trail == null) { OpenMenu("loadout"); return; }
            Window(Game.L("遗物来源追寻", "Relic source trails"), Game.L("查看真实来源与当前进度；主动追踪后沿已有任务前进。", "Review actual sources and progress; track an existing quest when ready."));
            Card(279, 177, 938, 100);
            Icon(trail.id == 0 ? "tea" : trail.id == 1 ? "seed" : trail.id == 2 ? "map" : trail.id == 3 ? "gear" : trail.id == 4 ? "shield" : "relic", 298, 196, 54);
            Text(371, 190, 620, 33, Game.L(GameCore.RelicNames[trail.id], GameCore.RelicNamesEn[trail.id]), 23, gold);
            Text(371, 232, 814, 32, Game.L(GameCore.RelicText[trail.id], GameCore.RelicTextEn[trail.id]), 15, muted);
            Text(1001, 192, 191, 29, trail.owned ? Game.L("已获得", "Owned") : Game.L("尚未获得", "Not owned"), 15, trail.owned ? mint : muted, TextAnchor.MiddleCenter);
            Card(279, 294, 938, 130);
            Text(302, 309, 889, 27, Game.L("永久来源", "Permanent source"), 18, gold);
            Text(302, 348, 889, 63, trail.source, 16, paper);
            for (int i = 0; i < trail.steps.Length; i++)
                Text(302, 444 + i * 30, 889, 29, (trail.steps[i].done ? "✓ " : "· ") + trail.steps[i].text, 16, trail.steps[i].done ? mint : muted);
            Card(279, 550, 938, 88);
            Text(302, 561, 889, 25, Game.L("下一步", "Next step"), 17, gold);
            Text(302, 591, 889, 39, trail.next, 15, paper);
            string action = trail.action == RelicTrailAction.Quest ? Game.L("追踪当前前置任务", "Track current prerequisite")
                : trail.action == RelicTrailAction.Projects ? Game.L("查看水渠建设计划", "Review canal project")
                : trail.action == RelicTrailAction.AbyssMap ? Game.L("查看深渊地图", "View abyss map")
                : trail.action == RelicTrailAction.AbyssRun ? Game.L("返回当前远征", "Return to expedition")
                : Game.L("无需追踪", "No tracking needed");
            bool safe = !Game.Dying && !Game.practicing && !Game.SafeTrainingActive && !applicationPaused && !VisualReviewPaused;
            if (Button(279, 655, 454, 38, action, true, safe && trail.action != RelicTrailAction.None))
            {
                if (trail.action == RelicTrailAction.Quest) Game.TrackRelicTrail(trail.id);
                else if (trail.action == RelicTrailAction.Projects) OpenMenu("camp");
                else if (trail.action == RelicTrailAction.AbyssMap) { selectedRegion = 8; OpenMenu("map"); }
                else if (trail.action == RelicTrailAction.AbyssRun) Game.Close();
            }
            if (Button(750, 655, 467, 38, Game.L("返回遗物栏", "Back to relics"))) OpenMenu("loadout");
            Text(282, 704, 925, 25, Game.L("查看不领取奖励；建设与远征仍使用原有确认和结算方式。", "Viewing grants nothing; projects and expeditions keep their existing payment and banking rules."), 13, muted);
        }
    }
}
