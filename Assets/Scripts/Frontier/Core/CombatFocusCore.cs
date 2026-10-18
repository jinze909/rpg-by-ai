using System;

namespace Starfall
{
    public sealed partial class GameCore
    {
        public const float CombatFocusRange = 12;
        private Enemy combatFocusEnemy;
        private SaveData combatFocusSave;
        private int combatFocusGeneration;

        // A live actor reference prevents an old drill target's reused ID from
        // becoming a new lock. This state never enters the save format.
        public Enemy CombatFocusTarget
        {
            get
            {
                return mode == "world" && !Dying && !SparringActionBlocked && !RangeActionBlocked &&
                    ReferenceEquals(combatFocusSave, save) && combatFocusGeneration == WorldGeneration &&
                    CombatFocusEligible(combatFocusEnemy) ? combatFocusEnemy : null;
            }
        }
        private bool CombatFocusEligible(Enemy enemy)
        { return enemy != null && enemies.Contains(enemy) && CombatFocusInSight(enemy); }
        private bool CombatFocusInSight(Enemy enemy)
        {
            return enemy != null && Finite(enemy.hp) && enemy.hp > 0 &&
                Finite(enemy.x) && Finite(enemy.y) &&
                (enemy.Position - Position).Length <= CombatFocusRange && ClearAttackPath(Position, enemy.Position);
        }
        private Enemy FindCombatFocus(Point direction)
        {
            if (mode != "world" || Dying || SparringActionBlocked || RangeActionBlocked) return null;
            if (!Finite(direction.x) || !Finite(direction.y) || direction.Length < .05f) direction = aim;
            direction = direction.Normalized;
            Enemy best = null; float bestScore = float.MaxValue;
            foreach (Enemy enemy in enemies)
            {
                if (!CombatFocusInSight(enemy)) continue;
                Point offset = enemy.Position - Position, facing = offset.Normalized;
                float score = (1 - facing.x * direction.x - facing.y * direction.y) * 4 + offset.Length * .2f;
                if (score < bestScore || score == bestScore && (best == null || enemy.id < best.id))
                { best = enemy; bestScore = score; }
            }
            return best;
        }
        public bool CanAcquireCombatFocus
        {
            get
            {
                if (mode != "world" || Dying || SparringActionBlocked || RangeActionBlocked) return false;
                foreach (Enemy enemy in enemies) if (CombatFocusInSight(enemy)) return true;
                return false;
            }
        }
        public bool ToggleCombatFocus(Point direction)
        {
            if (mode != "world" || Dying || SparringActionBlocked || RangeActionBlocked) return false;
            if (CombatFocusTarget != null) { ClearCombatFocus(); return true; }
            ClearCombatFocus();
            Enemy candidate = FindCombatFocus(direction);
            if (candidate == null) return false;
            combatFocusEnemy = candidate; combatFocusSave = save; combatFocusGeneration = WorldGeneration;
            return true;
        }
        public void ClearCombatFocus()
        {
            if (combatFocusEnemy != null) SetAimTarget(null);
            combatFocusEnemy = null; combatFocusSave = null;
        }
        private Point ResolveCombatFocusDirection(Point fallback)
        {
            Enemy target = CombatFocusTarget;
            if (target == null) { ClearCombatFocus(); return fallback; }
            SetAimTarget(target.Position);
            Point direction = target.Position - Position;
            return direction.Length > .05f ? direction.Normalized : aim;
        }
        private void ValidateCombatFocus()
        { if (combatFocusEnemy != null && CombatFocusTarget == null) ClearCombatFocus(); }
    }
}
