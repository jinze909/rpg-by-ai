namespace Starfall
{
    // A live comparison snapshot, never part of the persisted equipment recipe.
    public sealed class BattlePlanCombatStats
    {
        public readonly float attackDamage, attackInterval, attackEnergy, skillEnergy, dodgeEnergy, walkSpeed;
        internal BattlePlanCombatStats(float attackDamage, float attackInterval, float attackEnergy, float skillEnergy, float dodgeEnergy, float walkSpeed)
        {
            this.attackDamage = attackDamage; this.attackInterval = attackInterval; this.attackEnergy = attackEnergy;
            this.skillEnergy = skillEnergy; this.dodgeEnergy = dodgeEnergy; this.walkSpeed = walkSpeed;
        }
    }

    public sealed partial class GameCore
    {
        private static bool RelicInPair(int id, int first, int second) { return first == id || second == id; }
        private float BaseDamageFor(int weapon, int first, int second)
        { return BaseDamageFor(weapon, first, second, save.talents[0]); }
        private float BaseDamageFor(int weapon, int first, int second, int edge)
        {
            return (19 + save.upgrades * 4 + (save.level - 1) * 2 + edge * 3 + MasteryRank(weapon) * 2 +
                (first >= 0 || second >= 0 ? save.relicRank * 2 : 0) + (save.foodUntil > save.totalTime ? StewAttackBonus : 0) +
                (RelicInPair(1, first, second) && save.health >= save.maxHealth * .8f ? 6 : 0)) *
                (InAbyss ? 1 + save.boons[0] * .15f : 1);
        }
        private float WalkSpeedFor(int first, int second)
        { return WalkSpeedFor(first, second, save.talents[2]); }
        private float WalkSpeedFor(int first, int second, int agility)
        {
            return 4.5f + (save.profession == 1 ? .45f : 0) + agility * .12f +
                (RelicInPair(2, first, second) ? .4f : 0) + (racing ? .65f : 0) -
                (save.cold > 70 ? .55f : 0) + RoadMealWalkBonus;
        }
        private static float SkillEnergyCostFor(int first, int second) { return RelicInPair(4, first, second) ? 50 : 40; }
        private static float DodgeEnergyCostFor(int first, int second) { return RelicInPair(2, first, second) ? 25 : 20; }
        public float CurrentSkillEnergyCost { get { return SkillEnergyCostFor(save.relics[0], save.relics[1]); } }
        public float CurrentDodgeEnergyCost { get { return DodgeEnergyCostFor(save.relics[0], save.relics[1]); } }

        // A Flow bow releases three contacts; each arrow carries this fraction.
        // Combo, earned counters, criticals, armor and splash are separate rules.
        private static float BasicContactDamage(int weapon, int style, float damage)
        { return damage * (weapon == 1 && style == 2 ? .65f : 1); }

        public float WeaponContactDamage(int weapon, int style)
        {
            if (weapon < 0 || weapon > 2 || style < 0 || style > 2) return 0;
            return BasicContactDamage(weapon, style, Profile(weapon, style).damage);
        }

        private BattlePlanCombatStats BattlePlanStats(BattlePlan plan)
        {
            if (!ValidBattlePlan(plan)) return null;
            WeaponProfile profile = ProfileFor(plan.weapon, plan.style, plan.relicFirst, plan.relicSecond);
            return new BattlePlanCombatStats(BasicContactDamage(plan.weapon, plan.style, profile.damage),
                profile.interval, profile.energy, SkillEnergyCostFor(plan.relicFirst, plan.relicSecond),
                DodgeEnergyCostFor(plan.relicFirst, plan.relicSecond), WalkSpeedFor(plan.relicFirst, plan.relicSecond));
        }
        public BattlePlanCombatStats CurrentBattlePlanStats() { return BattlePlanStats(CurrentBattlePlan()); }
        public BattlePlanCombatStats BattlePlanStatsAt(int slot) { return BattlePlanStats(StoredBattlePlan(slot)); }
    }
}
