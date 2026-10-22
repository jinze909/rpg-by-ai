using System;

namespace Starfall
{
    // A receipt observes rewards already applied by combat. It owns no save
    // balance and exposes no mutable inventory collection.
    public sealed class CombatLootReceipt
    {
        public readonly long coins, xp, shards;
        public readonly bool unbanked;
        private readonly long[] items;
        internal CombatLootReceipt(CombatLootReceipt previous, int coins, int xp, int item, int quantity, int shards, bool unbanked)
        {
            this.coins = (previous == null ? 0 : previous.coins) + coins;
            this.xp = (previous == null ? 0 : previous.xp) + xp;
            this.shards = (previous == null ? 0 : previous.shards) + shards;
            this.unbanked = unbanked;
            items = previous == null ? new long[8] : (long[])previous.items.Clone();
            if (item >= 0 && item < items.Length) items[item] += quantity;
        }
        public long ItemCount(int item) { return item >= 0 && item < items.Length ? items[item] : 0; }
    }

    public sealed partial class GameCore
    {
        public const float CombatLootReceiptDuration = 3;
        private CombatLootReceipt combatLootReceipt;
        private SaveData combatLootOwner;
        private int combatLootGeneration, combatLootRoom;
        private float combatLootRemaining;
        public CombatLootReceipt RecentCombatLoot
        {
            get
            {
                return combatLootRemaining > 0 && combatLootReceipt != null && !practicing && !Dying
                    && ReferenceEquals(save, combatLootOwner) && WorldGeneration == combatLootGeneration
                    && save.room == combatLootRoom && combatLootReceipt.unbanked == InAbyss ? combatLootReceipt : null;
            }
        }
        public float CombatLootTimeRemaining { get { return RecentCombatLoot == null ? 0 : combatLootRemaining; } }
        private void ClearCombatLoot()
        { combatLootReceipt = null; combatLootOwner = null; combatLootRemaining = 0; }
        private void RecordCombatLoot(int coins, int xp, int item = -1, int quantity = 0, int shards = 0, bool unbanked = false)
        {
            CombatLootReceipt previous = RecentCombatLoot;
            if (previous != null && previous.unbanked != unbanked) previous = null;
            combatLootReceipt = new CombatLootReceipt(previous, coins, xp, item, quantity, shards, unbanked);
            combatLootOwner = save; combatLootGeneration = WorldGeneration; combatLootRoom = save.room;
            combatLootRemaining = CombatLootReceiptDuration;
        }
        private void TickCombatLoot(float dt)
        {
            if (combatLootReceipt == null) return;
            if (RecentCombatLoot == null) { ClearCombatLoot(); return; }
            combatLootRemaining = Math.Max(0, combatLootRemaining - dt);
            if (combatLootRemaining <= .00001f) ClearCombatLoot();
        }
    }
}
