using System;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private static readonly Color heavyGuardEdge = new Color(145f / 255f, 179f / 255f, 204f / 255f, 1);
        private const float HeavyGuardRadius = .8f;

        private bool EnemyGuardVisible(Enemy enemy)
        {
            return DangerContoursVisible && enemy != null && enemy.hp > 0
                && Game.enemies.Contains(enemy) && GameCore.EnemyHasFrontalArmor(enemy)
                && !(Game.practicing && !Game.SafeTrainingActive)
                && FiniteContourPoint(enemy.Position) && FiniteContourPoint(enemy.facing)
                && enemy.facing.Length > .001f && ContourInViewport(EnemyGuardBounds(enemy));
        }
        private Rect EnemyGuardBounds(Enemy enemy)
        {
            Point center = ScreenPoint(enemy.Position); float half = HeavyGuardRadius * Tile + 4;
            // Conservative visual support, including the 7px dark backing.
            // This small shield cue is not an attack radius or collision shape.
            return new Rect(center.x - half, center.y - half, half * 2, half * 2);
        }
        private void DrawEnemyGuard(Enemy enemy)
        {
            if (drawingOutline || !EnemyGuardVisible(enemy)) return;
            Point center = ScreenPoint(enemy.Position);
            float angle = (float)Math.Atan2(enemy.facing.y, enemy.facing.x);
            float half = (float)Math.Acos(GameCore.EnemyArmorCosine);
            // Same facing and strict frontal threshold as actual Hit. Hurt and
            // release poses do not disable armor in the existing simulation.
            DrawDangerArc(center, HeavyGuardRadius * Tile, angle - half, half * 2, 24, 7, dangerContourShadow);
            DrawDangerArc(center, HeavyGuardRadius * Tile, angle - half, half * 2, 24, 3, heavyGuardEdge);
        }
    }
}
