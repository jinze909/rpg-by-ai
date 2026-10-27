using System.Collections.Generic;

namespace Starfall
{
    public sealed partial class GameCore
    {
        // Rows retain the canonical quest index used by tracking and detail
        // selection. Filtering is a read-only view, never new quest state.
        public List<int> JournalQuestRows(int filter)
        {
            var rows = new List<int>();
            for (int index = 0; index < QuestIds.Length; index++)
            {
                if (filter == 1 || filter == 2)
                {
                    QuestGuide guide = GetQuest(QuestIds[index]);
                    if (filter == 1 ? guide.locked || guide.complete : !guide.complete) continue;
                }
                rows.Add(index);
            }
            return rows;
        }
    }
}
