using System;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private static readonly Color bossVolleyColor = new Color(.95f, .72f, .46f, 1);
        private const float BossVolleyWarningStart = 1.15f;
        private bool BossVolleyWarningVisible(Enemy enemy)
        {
            return DangerContoursVisible && enemy != null && Game.enemies.Contains(enemy) && enemy.hp > 0
                && enemy.timer > 0 && enemy.windupDuration > 0 && !float.IsInfinity(enemy.timer) && !float.IsInfinity(enemy.windupDuration)
                && FiniteContourPoint(enemy.Position) && FiniteContourPoint(enemy.facing)
                && GameCore.PendingBossVolley(enemy, Game.save.room).Count > 0;
        }
        private Rect BossVolleyWarningBounds(Enemy enemy)
        {
            if (!BossVolleyWarningVisible(enemy)) return new Rect();
            Point center = ScreenPoint(enemy.Position); center.y -= GameCore.BossEmissionHeight(Game.save.room);
            float reach = GameCore.BossVolleyWarningReach * Tile + 10;
            return new Rect(center.x - reach, center.y - reach, reach * 2, reach * 2);
        }
        private void DrawBossVolleyWarning(Enemy enemy)
        {
            if (drawingOutline || !BossVolleyWarningVisible(enemy) || !ContourInViewport(BossVolleyWarningBounds(enemy))) return;
            BossVolleyPlan plan = GameCore.PendingBossVolley(enemy, Game.save.room);
            float lift = GameCore.BossEmissionHeight(Game.save.room);
            float progress = Mathf.Clamp01(1 - enemy.timer / enemy.windupDuration);
            Color color = bossVolleyColor; color.a = .42f + progress * .48f;
            for (int shot = 0; shot < plan.Count; shot++)
            {
                Point endpoint = Game.BossVolleyWarningEnd(enemy, shot);
                // Leave the body/emission center clear even for twenty spokes.
                if ((endpoint - plan.origin).Length <= BossVolleyWarningStart + .1f) continue;
                Point from = ScreenPoint(plan.origin + plan.Direction(shot).Normalized * BossVolleyWarningStart), to = ScreenPoint(endpoint);
                from.y -= lift; to.y -= lift;
                DrawDangerSegment(from, to, 7, dangerContourShadow);
                DrawDangerSegment(from, to, 3, color);
                Point direction = (to - from).Normalized, side = new Point(-direction.y, direction.x);
                Point left = to - direction * 8 + side * 4, right = to - direction * 8 - side * 4;
                DrawDangerSegment(left, to, 5, dangerContourShadow); DrawDangerSegment(right, to, 5, dangerContourShadow);
                DrawDangerSegment(left, to, 2, color); DrawDangerSegment(right, to, 2, color);
            }
        }
    }
}
