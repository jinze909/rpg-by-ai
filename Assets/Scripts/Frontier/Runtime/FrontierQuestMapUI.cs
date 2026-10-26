using System;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private string mapQuestId;
        private GameCore mapQuestOwner;
        private SaveData mapQuestSave;
        private int mapQuestGeneration, mapQuestSerial;
        private bool mapQuestPracticing, mapQuestDying;

        private QuestGuide MapPreviewQuest
        {
            get
            {
                if (Game == null || Game.mode != "map" || mapQuestId == null ||
                    !ReferenceEquals(Game, mapQuestOwner) || !ReferenceEquals(Game.save, mapQuestSave) ||
                    Game.WorldGeneration != mapQuestGeneration || Game.practicing != mapQuestPracticing || Game.Dying != mapQuestDying ||
                    Array.IndexOf(GameCore.QuestIds, mapQuestId) < 0) return null;
                // Resolve live quest state, never a cached destination or an
                // assignment to the player's saved tracking/landmark choices.
                return Game.GetQuest(mapQuestId);
            }
        }
        private void ClearQuestMapPreview()
        {
            if (mapQuestId != null) confirmControl = null;
            mapQuestId = null; mapQuestOwner = null; mapQuestSave = null; mapQuestSerial++;
        }
        private void ValidateQuestMapPreview()
        { if (mapQuestId != null && MapPreviewQuest == null) ClearQuestMapPreview(); }
        private string QuestMapInputScope
        {
            get
            {
                return mapQuestId == null ? "" : "/quest-map/" + mapQuestSerial + "/" + Game.GetHashCode()
                    + "/" + Game.save.GetHashCode() + "/" + Game.WorldGeneration;
            }
        }
        private void OpenQuestDestination(string id)
        {
            if (Game.mode != "journal" || Array.IndexOf(GameCore.QuestIds, id) < 0) return;
            QuestGuide quest = Game.GetQuest(id);
            if (quest.locked || quest.room < 0 || quest.room >= Game.data.maps.Length) return;
            OpenMenu("map"); selectedRegion = quest.room;
            mapQuestId = id; mapQuestOwner = Game; mapQuestSave = Game.save; mapQuestGeneration = Game.WorldGeneration;
            mapQuestPracticing = Game.practicing; mapQuestDying = Game.Dying;
        }
        private bool QuestMapTargetVisible(QuestGuide quest, int room)
        {
            if (quest == null || quest.locked || quest.complete || quest.room != room) return false;
            MapData map = Game.data.maps[room]; Point target = quest.target;
            return !float.IsNaN(target.x) && !float.IsInfinity(target.x) && !float.IsNaN(target.y) && !float.IsInfinity(target.y)
                && target.y >= 0 && target.y < map.rows.Length && target.x >= 0 && target.x < map.rows[(int)target.y].Length;
        }
        private void DrawQuestMapReturn(QuestGuide quest)
        {
            if (Button(279, 690, 300, 30, Game.L("返回任务手帐", "Back to journal")))
            { OpenMenu("journal"); journalTab = 0; return; }
            string hint = !Game.SafeTravel ? Game.L("附近有敌人 · 暂不能快速旅行", "Enemies nearby; fast travel locked")
                : quest.complete ? Game.L("已完成 · 可在手帐重读", "Complete · reread in journal")
                : quest.room != selectedRegion && quest.room >= 0 && quest.room < Game.data.maps.Length
                ? Game.L("目标：", "Target: ") + Game.L(Game.data.maps[quest.room].name, Game.data.maps[quest.room].en)
                : Game.L("查看 · 任务追踪保留", "Preview · tracking kept");
            Text(591, 690, 243, 28, hint, 12, muted);
        }
    }
}
