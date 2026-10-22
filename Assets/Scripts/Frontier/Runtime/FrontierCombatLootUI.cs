using System;
using System.Globalization;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private static readonly int[] CombatLootItemOrder = { 1, 2, 4, 5 };
        private static readonly string[] CombatLootItemIcons = { "crystal", "herb", "gear", "relic" };
        private bool CombatLootVisible
        {
            get { return Game.mode == "world" && !Game.practicing && !Game.Dying && !applicationPaused && !VisualReviewPaused
                && !TrainingReview && Game.RecentCombatLoot != null; }
        }
        private Rect CombatLootBounds() { return new Rect(24, 418, 298, 48); }
        private bool SameCombatLootRegion(Rect rect)
        {
            Rect loot = CombatLootBounds();
            return rect.x == loot.x && rect.y == loot.y && rect.width == loot.width && rect.height == loot.height;
        }
        private static string CombatLootCount(long count)
        {
            // A compact lower bound keeps every material distinguishable even
            // during a large aggregate; it never rounds a gain upward.
            return count > 9999 ? "9999+" : count.ToString(CultureInfo.InvariantCulture);
        }
        private void DrawCombatLootHud()
        {
            if (!CombatLootVisible) return;
            CombatLootReceipt receipt = Game.RecentCombatLoot;
            Rect rect = CombatLootBounds();
            float alpha = Math.Min(1, Math.Max(0, Game.CombatLootTimeRemaining / .35f));
            Color edge = gold; edge.a *= alpha;
            Color body = paper; body.a *= alpha;
            Color icon = new Color(1, 1, 1, alpha);
            Color fill = new Color(.035f, .07f, .10f, (ThreatOver(rect) ? .12f : .92f) * alpha);
            Box(rect.x, rect.y, rect.width, rect.height, fill);
            Frame(rect.x, rect.y, rect.width, rect.height, edge);
            string heading = receipt.unbanked ? Game.L("未结算", "UNBANKED") : Game.L("战利品", "LOOT");
            Text(rect.x + 8, rect.y + 2, rect.width - 16, 20,
                heading + "  +" + CombatLootCount(receipt.coins) + Game.L(" 硬币", " coins")
                + " · +" + CombatLootCount(receipt.xp) + " XP", 12, edge);
            if (receipt.unbanked)
            {
                Text(rect.x + 8, rect.y + 24, rect.width - 16, 22,
                    "+" + CombatLootCount(receipt.shards)
                    + Game.L(" 碎片 · 到东门结算", " shards · bank at east gate"), 12, body);
                return;
            }
            int kinds = 0, single = -1;
            for (int i = 0; i < CombatLootItemOrder.Length; i++)
                if (receipt.ItemCount(CombatLootItemOrder[i]) > 0) { kinds++; single = i; }
            if (kinds == 1)
            {
                int item = CombatLootItemOrder[single];
                Icon(CombatLootItemIcons[single], rect.x + 8, rect.y + 25, 18, icon);
                Text(rect.x + 31, rect.y + 24, rect.width - 39, 22,
                    Game.L(Game.data.items[item].name, Game.data.items[item].en) + " ×" + CombatLootCount(receipt.ItemCount(item)), 12, body);
            }
            else if (kinds > 1)
            {
                // Keep every gained material distinguishable in 70px cells.
                int cell = 0;
                for (int i = 0; i < CombatLootItemOrder.Length; i++)
                {
                    if (receipt.ItemCount(CombatLootItemOrder[i]) <= 0) continue;
                    float x = rect.x + 8 + cell++ * 70;
                    Icon(CombatLootItemIcons[i], x, rect.y + 26, 16, icon);
                    Text(x + 17, rect.y + 24, 54, 22, "×" + CombatLootCount(receipt.ItemCount(CombatLootItemOrder[i])), 12, body);
                }
            }
        }
    }
}
