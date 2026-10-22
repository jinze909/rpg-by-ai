using System;

namespace Starfall
{
    // A transient review of the original reset transaction, never save data.
    public sealed class TalentResetQuote
    {
        internal readonly GameCore owner;
        internal readonly SaveData saveOwner;
        internal readonly int generation, profession, upgrades;
        public readonly int cost = 20, refund, pointsBefore, pointsAfter, coinsBefore, edge, resolve, agility;
        public readonly float healthBefore, healthAfter, maxHealthBefore, maxHealthAfter;
        public readonly string reasonZh, reasonEn;
        public bool available { get { return reasonZh.Length == 0; } }
        internal TalentResetQuote(GameCore game, string zh, string en, int refund, int after, float maximum)
        {
            owner = game; saveOwner = game.save; generation = game.WorldGeneration;
            profession = saveOwner.profession; upgrades = saveOwner.upgrades;
            coinsBefore = saveOwner.coins; pointsBefore = saveOwner.skillPoints; pointsAfter = after; this.refund = refund;
            bool ranks = saveOwner.talents != null && saveOwner.talents.Length == 3;
            edge = ranks ? saveOwner.talents[0] : -1; resolve = ranks ? saveOwner.talents[1] : -1; agility = ranks ? saveOwner.talents[2] : -1;
            healthBefore = saveOwner.health; maxHealthBefore = saveOwner.maxHealth;
            maxHealthAfter = maximum; healthAfter = Math.Min(healthBefore, maximum);
            reasonZh = zh; reasonEn = en;
        }
    }

    public sealed partial class GameCore
    {
        public TalentResetQuote GetTalentResetQuote()
        {
            bool ranks = save.talents != null && save.talents.Length == 3;
            if (ranks) foreach (int rank in save.talents) ranks &= rank >= 0 && rank <= 5;
            int refund = ranks ? save.talents[0] + save.talents[1] + save.talents[2] : 0;
            long projected = (long)save.skillPoints + refund;
            int after = (int)Math.Min(int.MaxValue, Math.Max(int.MinValue, projected));
            float maximum = 100f + save.upgrades * 20f + (save.profession == 0 ? 20 : 0);
            string zh = "", en = "";
            if (!ranks || save.skillPoints < 0 || save.skillPoints > 99 || save.upgrades < 0 || save.upgrades > 5
                || save.profession < 0 || save.profession > 2 || !Finite(save.health) || !Finite(save.maxHealth)
                || save.health <= 0 || save.maxHealth <= 0 || !Finite(maximum) || maximum <= 0 || Dying)
            { zh = "成长状态无法重置。"; en = "This growth state cannot be reset."; }
            else if (refund == 0)
            { zh = "三项天赋尚未投入点数。"; en = "No points invested in these talents."; }
            else if (projected > 99)
            { zh = "返还后可用点数将超过99，本次不会支付费用。"; en = "The refund would exceed 99 unused points. No coins or points spent."; }
            else if (save.coins < 20)
            { zh = "需要20硬币才能重置。"; en = "Resetting requires 20 coins."; }
            return new TalentResetQuote(this, zh, en, refund, after, maximum);
        }
        public bool CanResetTalents(TalentResetQuote quote)
        {
            if (quote == null || !quote.available || !ReferenceEquals(quote.owner, this) || !ReferenceEquals(quote.saveOwner, save)
                || quote.generation != WorldGeneration || quote.profession != save.profession || quote.upgrades != save.upgrades
                || quote.coinsBefore != save.coins || quote.pointsBefore != save.skillPoints || quote.healthBefore != save.health
                || quote.maxHealthBefore != save.maxHealth || save.talents == null || save.talents.Length != 3
                || quote.edge != save.talents[0] || quote.resolve != save.talents[1] || quote.agility != save.talents[2]) return false;
            return GetTalentResetQuote().available;
        }
        public bool TryResetTalents(TalentResetQuote quote)
        {
            if (!CanResetTalents(quote)) return false;
            save.coins -= quote.cost; save.skillPoints = quote.pointsAfter; Array.Clear(save.talents, 0, 3);
            save.maxHealth = quote.maxHealthAfter; save.health = quote.healthAfter;
            revision++; RecordMoment("重新分配成长方向：返还 " + quote.refund + " 技能点。", "Build reconsidered: refunded " + quote.refund + " talent points.");
            return true;
        }
    }
}
