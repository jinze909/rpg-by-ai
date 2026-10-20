using System;
using System.Collections.Generic;

namespace Starfall
{
    // A read-only menu snapshot. Payment always requests a fresh quote.
    public sealed class RelicForgeQuote
    {
        public readonly int currentRank, nextRank, shards, crystals, coins;
        public readonly string reasonZh, reasonEn;
        public bool available { get { return reasonZh.Length == 0; } }
        public int currentBonus { get { return currentRank * 2; } }
        public int nextBonus { get { return nextRank * 2; } }
        internal RelicForgeQuote(int rank, int next, int shards, int crystals, int coins, string zh, string en)
        {
            currentRank = rank; nextRank = next; this.shards = shards;
            this.crystals = crystals; this.coins = coins; reasonZh = zh; reasonEn = en;
        }
    }

    public sealed partial class GameCore
    {
        public RelicForgeQuote GetRelicForgeQuote()
        {
            int rank = save.relicRank, next = Math.Min(3, rank + 1);
            int shards = rank < 3 ? next * 8 : 0, crystals = rank < 3 ? 2 : 0;
            int coins = rank < 3 && save.projects[2] != 2 ? 20 : 0;
            string zh = "", en = "";
            if (rank >= 3) { zh = "锻印已达三级上限。"; en = "Memory forging is already at tier 3."; }
            else if (save.room != 0) { zh = "回到余烬镇才能锻印。"; en = "Return to Emberfall to refine relics."; }
            else if (save.projects[2] == 0) { zh = "先建设记忆工坊，再进行锻印。"; en = "Build the memory workshop before refining."; }
            else
            {
                var missingZh = new List<string>(3); var missingEn = new List<string>(3);
                if (save.shards < shards) { missingZh.Add("记忆碎片" + (shards - save.shards)); missingEn.Add((shards - save.shards) + " memory shards"); }
                if (save.inventory[1] < crystals) { missingZh.Add("星晶" + (crystals - save.inventory[1])); missingEn.Add((crystals - save.inventory[1]) + " crystals"); }
                if (save.coins < coins) { missingZh.Add("硬币" + (coins - save.coins)); missingEn.Add((coins - save.coins) + " coins"); }
                if (missingZh.Count > 0) { zh = "还需：" + string.Join(" / ", missingZh.ToArray()); en = "Need: " + string.Join(" / ", missingEn.ToArray()); }
            }
            return new RelicForgeQuote(rank, next, shards, crystals, coins, zh, en);
        }
    }
}
