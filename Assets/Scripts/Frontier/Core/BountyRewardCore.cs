namespace Starfall
{
    // A read-only view of this claim. ClaimBounty always recomputes it before payment.
    public sealed class BountyRewardQuote
    {
        public readonly int contract, target, progress, coinReward, shardReward, experienceReward;
        public readonly int coinsBefore, shardsBefore, experienceBefore, skillPointsBefore, levelBefore, levelAfter;
        public readonly long coinsAfter, shardsAfter, experienceAfter, skillPointsAfter;
        public readonly bool complete, canClaim;
        public readonly string reasonZh, reasonEn;

        internal BountyRewardQuote(GameCore game)
        {
            SaveData s = game.save;
            contract = s.bounty; target = GameCore.BountyTargets[contract]; progress = s.bountyProgress;
            coinReward = GameCore.BountyCoinReward; shardReward = game.BountyShardReward; experienceReward = GameCore.BountyExperienceReward;
            coinsBefore = s.coins; shardsBefore = s.shards; experienceBefore = s.xp;
            skillPointsBefore = s.skillPoints; levelBefore = s.level;
            coinsAfter = (long)coinsBefore + coinReward; shardsAfter = (long)shardsBefore + shardReward;
            long xp = (long)experienceBefore + experienceReward, points = skillPointsBefore;
            int level = levelBefore;
            if (level >= 1 && level <= 30)
                while (level < 30 && xp >= level * 40) { xp -= level * 40; level++; points++; }
            experienceAfter = xp; skillPointsAfter = points; levelAfter = level;
            complete = progress >= target;
            reasonZh = ""; reasonEn = "";
            if (!complete) { reasonZh = "完成目标后领取报酬。"; reasonEn = "Complete the objective to claim."; }
            else if (coinsBefore < 0 || shardsBefore < 0 || experienceBefore < 0 || skillPointsBefore < 0 || levelBefore < 1 || levelBefore > 30)
            { reasonZh = "当前报酬状态无法结算；委托已保留。"; reasonEn = "Reward state is invalid; contract retained."; }
            else if (coinsAfter > 999999)
            { reasonZh = "硬币上限999999；先使用一些，已完成委托保留。"; reasonEn = "Coin limit 999999: spend some first. Completed contract retained."; }
            else if (shardsAfter > 99999)
            { reasonZh = "碎片上限99999；先使用一些，已完成委托保留。"; reasonEn = "Shard limit 99999: use some first. Completed contract retained."; }
            else if (skillPointsAfter > 99)
            { reasonZh = "技能点上限99；先投入技能点，已完成委托保留。"; reasonEn = "Point limit 99: spend points first. Completed contract retained."; }
            else if ((long)experienceBefore + experienceReward > int.MaxValue)
            { reasonZh = "经验已达存储上限；已完成委托保留。"; reasonEn = "XP storage limit reached; completed contract retained."; }
            canClaim = complete && reasonZh.Length == 0;
        }
    }

    public sealed partial class GameCore
    {
        public const int BountyCoinReward = 35, BountyExperienceReward = 30;
        public int BountyShardReward { get { return 3 + (save.frontierEnding == 3 ? 1 : 0); } }
        public BountyRewardQuote GetBountyRewardQuote()
        { return save.bounty >= 0 && save.bounty < BountyTargets.Length ? new BountyRewardQuote(this) : null; }
    }
}
