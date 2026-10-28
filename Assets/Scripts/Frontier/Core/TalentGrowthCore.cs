namespace Starfall
{
    public enum TalentGrowthStat
    {
        ContactDamage, CriticalChance, MaxHealth, IncomingTalentFactor, WalkSpeed, DodgeCooldown
    }

    public sealed class TalentGrowthValue
    {
        public readonly TalentGrowthStat stat;
        public readonly float before, after;
        internal TalentGrowthValue(TalentGrowthStat stat, float before, float after)
        { this.stat = stat; this.before = before; this.after = after; }
    }

    public sealed class TalentGrowthPreview
    {
        public readonly int talent, rank, nextRank;
        public readonly bool canLearn;
        public readonly TalentGrowthValue first, second;
        internal TalentGrowthPreview(int talent, int rank, int nextRank, bool canLearn, TalentGrowthValue first, TalentGrowthValue second)
        {
            this.talent = talent; this.rank = rank; this.nextRank = nextRank; this.canLearn = canLearn;
            this.first = first; this.second = second;
        }
    }

    public sealed partial class GameCore
    {
        private float CriticalChanceFor(int edge)
        { return Clamp(.06f + edge * .025f + (Relic(5) ? .12f : 0), 0, 1); }
        private static float ResolveIncomingFactor(int resolve)
        { return 1 - resolve * .035f; }
        private static float DodgeCooldownFor(int agility)
        { return .75f - agility * .06f; }

        // This view projects one rank through the same math used by accepted
        // combat actions. It neither spends points nor changes the live build.
        public TalentGrowthPreview GetTalentGrowthPreview(int talent)
        {
            if (talent < 0 || talent > 2 || save == null || save.talents == null || save.talents.Length != 3) return null;
            foreach (int value in save.talents) if (value < 0 || value > 5) return null;
            int rank = save.talents[talent], nextRank = rank < 5 ? rank + 1 : rank;
            TalentGrowthValue first, second;
            if (talent == 0)
            {
                int weapon = save.weapon, style = Style, relicFirst = save.relics[0], relicSecond = save.relics[1];
                first = new TalentGrowthValue(TalentGrowthStat.ContactDamage,
                    BasicContactDamage(weapon, style, ProfileFor(weapon, style, relicFirst, relicSecond, rank).damage),
                    BasicContactDamage(weapon, style, ProfileFor(weapon, style, relicFirst, relicSecond, nextRank).damage));
                second = new TalentGrowthValue(TalentGrowthStat.CriticalChance, CriticalChanceFor(rank), CriticalChanceFor(nextRank));
            }
            else if (talent == 1)
            {
                first = new TalentGrowthValue(TalentGrowthStat.MaxHealth, save.maxHealth, save.maxHealth + (nextRank - rank) * 12);
                second = new TalentGrowthValue(TalentGrowthStat.IncomingTalentFactor, ResolveIncomingFactor(rank), ResolveIncomingFactor(nextRank));
            }
            else
            {
                int relicFirst = save.relics[0], relicSecond = save.relics[1];
                first = new TalentGrowthValue(TalentGrowthStat.WalkSpeed,
                    WalkSpeedFor(relicFirst, relicSecond, rank), WalkSpeedFor(relicFirst, relicSecond, nextRank));
                second = new TalentGrowthValue(TalentGrowthStat.DodgeCooldown, DodgeCooldownFor(rank), DodgeCooldownFor(nextRank));
            }
            return new TalentGrowthPreview(talent, rank, nextRank, save.skillPoints > 0 && rank < 5, first, second);
        }
    }
}
