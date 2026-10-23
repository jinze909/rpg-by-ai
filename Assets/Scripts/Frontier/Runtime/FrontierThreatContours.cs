using System;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private static readonly Color dangerContourShadow = new Color(.025f, .045f, .06f, .95f);
        private static readonly Color enemyDangerContour = new Color(223f / 255f, 138f / 255f, 104f / 255f, 1);
        private bool DangerContoursVisible
        {
            get
            {
                return Game != null && Game.mode == "world" && !Game.Dying
                    && !(TrainingReview);
            }
        }
        private static bool FiniteContourPoint(Point point)
        { return !float.IsNaN(point.x) && !float.IsInfinity(point.x) && !float.IsNaN(point.y) && !float.IsInfinity(point.y); }
        private static bool FiniteContourRadius(float radius)
        { return radius > 0 && !float.IsNaN(radius) && !float.IsInfinity(radius); }
        private static bool ContourInViewport(Rect bounds)
        { return Overlap(bounds, new Rect(-8, -8, Width + 16, Height + 16)); }

        private void DrawForegroundThreatContours()
        {
            if (!DangerContoursVisible) return;
            // The simulation owns warning lifetime. No cached geometry or
            // presentation clock can survive a cancelled or completed attack.
            foreach (Enemy enemy in Game.enemies) { DrawEnemyDangerContour(enemy); DrawBossVolleyWarning(enemy); }
            foreach (Hazard hazard in Game.hazards) DrawHazardDangerContour(hazard);
        }
        private void DrawEnemyDangerContour(Enemy enemy)
        {
            if (!DangerContoursVisible || enemy == null || enemy.state != 1
                || GameCore.IsSignalWarden(enemy) || GameCore.IsCommissionGuardian(enemy)
                || !enemy.boss && (enemy.kind < 0 || enemy.kind > 2)
                || !FiniteContourPoint(enemy.Position) || !FiniteContourPoint(enemy.target)
                || !FiniteContourPoint(enemy.facing) || !ContourInViewport(EnemyWarningBounds(enemy))) return;
            if (enemy.boss || enemy.kind == 2)
            {
                DrawDangerCircle(enemy.target, GameCore.EnemyAreaRadius(enemy), enemyDangerContour);
                return;
            }
            if (enemy.kind == 1)
            {
                // Keep the locked bow attachment and elevated flight plane used
                // by the existing warning, including its vertical directions.
                Point lift = new Point(0, GameCore.WeaponHeight(1, enemy.facing) / Tile);
                Point from = ScreenPoint(GameCore.WeaponOrigin(enemy.Position, 1, enemy.facing) - lift);
                Point to = ScreenPoint(enemy.target - lift);
                DrawDangerSegment(from, to, 7, dangerContourShadow);
                DrawDangerSegment(from, to, 3, enemyDangerContour);
                return;
            }
            float angle = (float)Math.Atan2(enemy.facing.y, enemy.facing.x);
            float half = (float)Math.Acos(GameCore.EnemyMeleeCosine);
            Point center = ScreenPoint(enemy.Position);
            float radius = GameCore.EnemyMeleeRadius * Tile;
            DrawDangerSector(center, radius, angle, half, 7, dangerContourShadow);
            DrawDangerSector(center, radius, angle, half, 3, enemyDangerContour);
        }
        private void DrawHazardDangerContour(Hazard hazard)
        {
            if (!DangerContoursVisible || hazard == null || !FiniteContourPoint(hazard.position) || !FiniteContourRadius(hazard.radius)) return;
            Point center = ScreenPoint(hazard.position); float radius = hazard.radius * Tile;
            if (!ContourInViewport(new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2))) return;
            Color color = HazardWarningColor(hazard); color.a = 1;
            DrawDangerCircle(hazard.position, hazard.radius, color);
        }
        private void DrawDangerCircle(Point position, float radius, Color color)
        {
            if (!FiniteContourRadius(radius)) return;
            Point center = ScreenPoint(position);
            DrawDangerArc(center, radius * Tile, 0, (float)Math.PI * 2, 48, 7, dangerContourShadow);
            DrawDangerArc(center, radius * Tile, 0, (float)Math.PI * 2, 48, 3, color);
        }
        private void DrawDangerSector(Point center, float radius, float angle, float half, float width, Color color)
        {
            DrawDangerArc(center, radius, angle - half, half * 2, 32, width, color);
            DrawDangerSegment(center, center + new Point((float)Math.Cos(angle - half), (float)Math.Sin(angle - half)) * radius, width, color);
            DrawDangerSegment(center, center + new Point((float)Math.Cos(angle + half), (float)Math.Sin(angle + half)) * radius, width, color);
        }
        private void DrawDangerArc(Point center, float radius, float start, float sweep, int segments, float width, Color color)
        {
            Point previous = center + new Point((float)Math.Cos(start), (float)Math.Sin(start)) * radius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = start + sweep * i / segments;
                Point next = center + new Point((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius;
                DrawDangerSegment(previous, next, width, color); previous = next;
            }
        }
        private void DrawDangerSegment(Point from, Point to, float width, Color color)
        {
            Point delta = to - from; float length = delta.Length;
            if (length <= .001f) return;
            float angle = (float)(Math.Atan2(delta.y, delta.x) * 180 / Math.PI);
            Rect rect = new Rect((from.x + to.x - length) / 2, (from.y + to.y - width) / 2, length, width);
            // Atlas restores both GUI.matrix and GUI.color, and rotates before
            // applying the existing viewport scale and letterbox translation.
            Atlas(Texture2D.whiteTexture, 1, 1, 0, rect, color, false, angle);
        }
    }
}
