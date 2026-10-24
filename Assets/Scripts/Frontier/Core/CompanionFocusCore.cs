namespace Starfall
{
    public sealed partial class GameCore
    {
        private bool CompanionCanAttack(Enemy enemy)
        {
            return enemy != null && Finite(enemy.hp) && enemy.hp > 0 && Finite(enemy.x) && Finite(enemy.y)
                && (enemy.Position - companionPosition).Length < 7 && ClearAttackPath(companionPosition, enemy.Position);
        }
        private Enemy CompanionAttackTarget()
        {
            // This preference is consulted only before automatic preparation.
            // A later lock change must not rewrite a draw or released missile.
            Enemy focused = CombatFocusTarget;
            if (CompanionCanAttack(focused)) return focused;
            Enemy nearest = null; double best = double.MaxValue;
            foreach (Enemy enemy in enemies)
            {
                if (!CompanionCanAttack(enemy)) continue;
                Point offset = enemy.Position - companionPosition;
                double distance = (double)offset.x * offset.x + (double)offset.y * offset.y;
                if (distance < best || distance == best && (nearest == null || enemy.id < nearest.id))
                { nearest = enemy; best = distance; }
            }
            return nearest;
        }
    }
}
