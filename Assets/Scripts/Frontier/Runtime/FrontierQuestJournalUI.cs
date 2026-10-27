using System;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private int journalQuestFilter;
        private GameCore questJournalOwner;
        private SaveData questJournalSave;
        private string questJournalRowsScope;

        private void ValidateQuestJournalView()
        {
            if (Game == null) return;
            if (!ReferenceEquals(questJournalOwner, Game) || !ReferenceEquals(questJournalSave, Game.save))
            {
                questJournalOwner = Game; questJournalSave = Game.save; journalQuestFilter = 0;
                if (Game.mode == "journal") confirmControl = null;
            }
            if (journalQuestFilter < 0 || journalQuestFilter > 2)
            { journalQuestFilter = 0; confirmControl = null; }
            RefreshQuestJournalRowsScope();
            // A key queued by Update belongs to the last drawn selection.
            // BeginControls must not rebind it to another task at the same rect.
            if (Game.mode == "journal" && drawnScope != InputScope()) confirmControl = null;
        }
        private void RefreshQuestJournalRowsScope()
        {
            if (Game.mode != "journal") return;
            string rows = journalTab == 0 ? string.Join(",", Game.JournalQuestRows(journalQuestFilter).ToArray()) : "";
            if (questJournalRowsScope != rows) { questJournalRowsScope = rows; confirmControl = null; }
        }
        private string QuestJournalInputScope
        {
            get { return "/quest-journal/" + journalTab + "/" + journalQuestFilter + "/" + questJournalRowsScope + "/" + selectedQuest
                + "/" + Game.GetHashCode() + "/" + Game.save.GetHashCode(); }
        }
        private void KeepQuestJournalFocus(float x, float y, float width, float height)
        {
            // An explicit selection owns its new view. Drop the previous
            // activation while retaining this control for the next fresh key.
            confirmControl = null; RefreshQuestJournalRowsScope(); drawnScope = InputScope(); navigation.Scope(drawnScope);
            navigation.SetControls(drawnControls); navigation.Select(ControlId(x, y, width, height));
        }
        private void DrawQuestJournalTasks()
        {
            const int questsPerPage = 19;
            selectedQuest = (int)GameCore.Clamp(selectedQuest, 0, GameCore.QuestIds.Length - 1);
            var rows = Game.JournalQuestRows(journalQuestFilter);
            if (rows.Count > 0 && !rows.Contains(selectedQuest)) { selectedQuest = rows[0]; confirmControl = null; }
            if (Button(279, 110, 321, 31, Game.L("定位当前追踪", "Show tracked quest")))
            {
                int tracked = Array.IndexOf(GameCore.QuestIds, Game.save.trackedQuest);
                if (tracked < 0) tracked = 0;
                if (!rows.Contains(tracked)) journalQuestFilter = 0;
                selectedQuest = tracked; KeepQuestJournalFocus(279, 110, 321, 31); return;
            }
            string[] filters = { Game.L("全部", "All"), Game.L("可进行", "Available"), Game.L("已完成", "Completed") };
            for (int filter = 0; filter < filters.Length; filter++)
            {
                float x = 625 + filter * 201;
                if (Button(x, 110, 189, 31, filters[filter], journalQuestFilter == filter) && journalQuestFilter != filter)
                {
                    journalQuestFilter = filter; rows = Game.JournalQuestRows(filter);
                    if (rows.Count > 0 && !rows.Contains(selectedQuest)) selectedQuest = rows[0];
                    KeepQuestJournalFocus(x, 110, 189, 31); return;
                }
            }
            if (rows.Count == 0)
            {
                Card(279, 226, 321, 464); Card(625, 226, 592, 464);
                Text(303, 250, 273, 100, Game.L("这里暂时没有任务。", "No quests in this view yet."), 19, muted);
                Text(650, 281, 541, 62, journalQuestFilter == 2 ? Game.L("还没有已完成的故事", "No completed stories yet")
                    : Game.L("暂时没有可进行的任务", "No available quests right now"), 25, paper);
                Text(650, 382, 541, 100, Game.L("切换“全部”查看原有故事与开启条件，或定位当前追踪。查看不会接取或更换任务。",
                    "Choose All to see every story and its requirements, or show your tracked quest. Filtering never accepts or tracks a quest."), 17, muted);
                Button(650, 647, 258, 31, Game.L("追踪任务", "Track quest"), false, false);
                Button(927, 647, 263, 31, Game.L("查看目标地图", "Show destination"), false, false); return;
            }
            int selectedRow = rows.IndexOf(selectedQuest), questPage = selectedRow / questsPerPage;
            int questPages = Math.Max(1, (rows.Count + questsPerPage - 1) / questsPerPage);
            float questSpacing = Math.Min(46, 464f / Math.Min(journalQuestFilter == 0 ? GameCore.QuestIds.Length : rows.Count, 18));
            RectOffset rowOriginalPadding = buttonStyle.padding;
            try
            {
                if (questSpacing < 39) buttonStyle.padding = questRowPadding;
                for (int cell = 0; cell < questsPerPage && questPage * questsPerPage + cell < rows.Count; cell++)
                {
                    int index = rows[questPage * questsPerPage + cell]; QuestGuide quest = Game.GetQuest(GameCore.QuestIds[index]);
                    float y = 226 + cell * questSpacing - (cell == 18 ? 3 : 0);
                    if (Button(279, y, 321, questSpacing - 7, (quest.complete ? "✓ " : Game.save.trackedQuest == quest.id ? "◆ " : "") + quest.title, selectedQuest == index))
                    { selectedQuest = index; KeepQuestJournalFocus(279, y, 321, questSpacing - 7); }
                }
            }
            finally { buttonStyle.padding = rowOriginalPadding; }
            QuestGuide q = Game.GetQuest(GameCore.QuestIds[selectedQuest]);
            Card(625, 226, 592, 464); Text(650, 244, 541, 27, q.category + (q.complete ? Game.L(" · 已完成", " · COMPLETE") : ""), 12, gold);
            Text(650, 281, 541, 45, q.title, 26, paper); Text(650, 342, 541, 62, q.description, 17, muted);
            Text(650, 410, 541, 60, Game.L("下一步：", "Next: ") + q.next, 14, gold);
            float stepSpacing = Math.Min(34, 140f / Math.Max(1, q.steps.Count));
            for (int i = 0; i < q.steps.Count; i++) Text(650, 482 + i * stepSpacing, 542, stepSpacing, (q.steps[i].done ? "✓  " : "○  ") + q.steps[i].text, 15, q.steps[i].done ? mint : paper);
            Text(650, 622, 540, 22, q.reward, 12, gold);
            if (Button(650, 647, 258, 31, Game.L("追踪任务", "Track quest"), Game.save.trackedQuest == q.id, !q.locked && !q.complete)) Game.TrackQuest(q.id);
            if (Button(927, 647, 263, 31, Game.L("查看目标地图", "Show destination"), false, !q.locked)) OpenQuestDestination(q.id);
            if (questPages > 1)
            {
                if (Button(650, 699, 258, 26, Game.L("上一页 · ", "Previous · ") + (questPage + 1) + " / " + questPages, false, questPage > 0))
                { selectedQuest = rows[(questPage - 1) * questsPerPage]; KeepQuestJournalFocus(650, 699, 258, 26); }
                if (Button(927, 699, 263, 26, Game.L("下一页", "Next page"), false, questPage < questPages - 1))
                { selectedQuest = rows[(questPage + 1) * questsPerPage]; KeepQuestJournalFocus(927, 699, 263, 26); }
            }
        }
    }
}
