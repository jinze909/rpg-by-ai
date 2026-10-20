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
        public bool CanCycleCombatFocus
        {
            get
            {
                Enemy current = CombatFocusTarget;
                if (current == null) return false;
                foreach (Enemy enemy in enemies)
                    if (!ReferenceEquals(enemy, current) && CombatFocusInSight(enemy)) return true;
                return false;
            }
        }
        private int CompareCombatFocusOrder(Enemy left, int leftIndex, Enemy right, int rightIndex)
        {
            Point a = left.Position - Position, b = right.Position - Position;
            int angle = Math.Atan2(a.y, a.x).CompareTo(Math.Atan2(b.y, b.x));
            if (angle != 0) return angle;
            int distance = ((double)a.x * a.x + (double)a.y * a.y).CompareTo((double)b.x * b.x + (double)b.y * b.y);
            if (distance != 0) return distance;
            int identity = left.id.CompareTo(right.id);
            return identity != 0 ? identity : leftIndex.CompareTo(rightIndex);
        }
        // Cycle only an existing live lock. This explicit selection does not
        // rewrite an accepted action, queued input or a released projectile.
        public bool CycleCombatFocus(int step = 1)
        {
            if (step != 1 && step != -1) return false;
            Enemy current = CombatFocusTarget;
            if (current == null) return false;
            int currentIndex = enemies.IndexOf(current), nextIndex = -1, wrapIndex = -1;
            Enemy next = null, wrap = null;
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                if (ReferenceEquals(enemy, current) || !CombatFocusInSight(enemy)) continue;
                if (wrap == null || CompareCombatFocusOrder(enemy, i, wrap, wrapIndex) * step < 0)
                { wrap = enemy; wrapIndex = i; }
                if (CompareCombatFocusOrder(enemy, i, current, currentIndex) * step > 0 &&
                    (next == null || CompareCombatFocusOrder(enemy, i, next, nextIndex) * step < 0))
                { next = enemy; nextIndex = i; }
            }
            Enemy selected = next ?? wrap;
            if (selected == null) return false;
            combatFocusEnemy = selected;
            SetAimTarget(null);
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
