using System;

namespace Starfall
{
    // A presentation snapshot of live HP and an already announced attack.
    public struct BossPhaseCue
    {
        public readonly int phase, preparedPhase, fromPhase;
        public readonly float remaining;
        public bool Changed { get { return remaining > 0 && fromPhase > 0 && fromPhase < phase; } }
        public bool Pending { get { return preparedPhase > 0 && preparedPhase != phase; } }
        internal BossPhaseCue(int phase, int preparedPhase, int fromPhase, float remaining)
        { this.phase = phase; this.preparedPhase = preparedPhase; this.fromPhase = fromPhase; this.remaining = remaining; }
    }

    public sealed partial class GameCore
    {
        public const float BossPhaseTwoThreshold = .66f;
        public const float BossPhaseThreeThreshold = .33f;
        public const float BossPhaseCueSeconds = 2;
        private Enemy bossPhaseActor;
        private SaveData bossPhaseOwner;
        private int bossPhaseGeneration, bossPhaseRoom, bossPhaseFrom, bossPhaseTo;
        private float bossPhaseRemaining;

        private bool IsBossPhaseActor(Enemy boss)
        {
            return save != null && mode == "world" && !Dying && !practicing && !SafeTrainingActive
                && save.room >= 1 && save.room <= 8 && boss != null && boss.boss
                && Finite(boss.hp) && boss.hp > 0 && Finite(boss.maxHp) && boss.maxHp > 0
                && !IsSignalWarden(boss) && !IsCommissionGuardian(boss) && enemies.Contains(boss);
        }
        private bool OwnsBossPhaseReceipt(Enemy boss)
        {
            return ReferenceEquals(boss, bossPhaseActor) && ReferenceEquals(save, bossPhaseOwner)
                && WorldGeneration == bossPhaseGeneration && save.room == bossPhaseRoom
                && boss.BossPhase == bossPhaseTo && bossPhaseRemaining > 0;
        }
        public BossPhaseCue GetBossPhaseCue(Enemy boss)
        {
            if (!IsBossPhaseActor(boss)) return new BossPhaseCue();
            bool changed = OwnsBossPhaseReceipt(boss);
            return new BossPhaseCue(boss.BossPhase, boss.state == 1 ? boss.AttackPhase : 0,
                changed ? bossPhaseFrom : 0, changed ? bossPhaseRemaining : 0);
        }
        // Observe actual nonlethal HP loss at the shared contact seam. This
        // receipt never changes the attack that a boss has already prepared.
        private void RecordBossPhaseChange(Enemy boss, float healthBefore)
        {
            if (!IsBossPhaseActor(boss) || !Finite(healthBefore) || healthBefore <= boss.hp) return;
            int before = healthBefore > boss.maxHp * BossPhaseTwoThreshold ? 1
                : healthBefore > boss.maxHp * BossPhaseThreeThreshold ? 2 : 3;
            int after = boss.BossPhase;
            if (before >= after) return;
            bossPhaseActor = boss; bossPhaseOwner = save;
            bossPhaseGeneration = WorldGeneration; bossPhaseRoom = save.room;
            bossPhaseFrom = before; bossPhaseTo = after; bossPhaseRemaining = BossPhaseCueSeconds;
        }
        private void ClearBossPhaseCue()
        {
            bossPhaseActor = null; bossPhaseOwner = null; bossPhaseRemaining = 0;
            bossPhaseGeneration = bossPhaseRoom = bossPhaseFrom = bossPhaseTo = 0;
        }
        private void TickBossPhaseCue(float dt)
        {
            if (bossPhaseActor == null) return;
            if (!IsBossPhaseActor(bossPhaseActor) || !OwnsBossPhaseReceipt(bossPhaseActor))
            { ClearBossPhaseCue(); return; }
            bossPhaseRemaining = Math.Max(0, bossPhaseRemaining - dt);
            if (bossPhaseRemaining <= .00001f) ClearBossPhaseCue();
        }
    }
}
