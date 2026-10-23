using System;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private static readonly Color hitDirectionColor = new Color(244f / 255, 170f / 255, 165f / 255, 1);

        private Effect LatestHitDirection()
        {
            if (Game == null || Game.mode != "world" || applicationPaused || VisualReviewPaused
                || Game.practicing || TrainingReview || !FiniteContourPoint(Game.Position)) return null;
            for (int i = Game.effects.Count - 1; i >= 0; i--)
            {
                Effect effect = Game.effects[i];
                if (effect.kind != "damageTaken" || effect.life <= 0) continue;
                // The newest accepted loss owns the cue. A new directionless
                // loss must not revive an older, unrelated incoming heading.
                float length = effect.direction.Length;
                return FiniteContourPoint(effect.direction) && length > .0001f && !float.IsInfinity(length)
                    && !float.IsNaN(length) && effect.maxLife > 0 && !float.IsInfinity(effect.maxLife)
                    && !float.IsNaN(effect.maxLife) && !float.IsInfinity(effect.life) && !float.IsNaN(effect.life)
                    ? effect : null;
            }
            return null;
        }

        private bool HitDirectionPoints(Effect effect, out Point tip, out Point left, out Point right)
        {
            tip = left = right = new Point();
            if (effect == null) return false;
            Point incomingSide = effect.direction.Normalized * -1;
            Rect body = PlayerHudBounds();
            Point center = new Point(body.x + body.width / 2, body.y + body.height / 2);
            float horizontal = Math.Abs(incomingSide.x) > .0001f ? body.width / (2 * Math.Abs(incomingSide.x)) : float.PositiveInfinity;
            float vertical = Math.Abs(incomingSide.y) > .0001f ? body.height / (2 * Math.Abs(incomingSide.y)) : float.PositiveInfinity;
            tip = center + incomingSide * (Math.Min(horizontal, vertical) + 18);
            Point tail = tip - incomingSide * 9, side = new Point(-incomingSide.y, incomingSide.x);
            left = tail + side * 5; right = tail - side * 5;
            // Keep the complete outlined mark on screen without changing its
            // captured orientation, even at a camera-clamped map edge.
            float minX = Math.Min(tip.x, Math.Min(left.x, right.x)), maxX = Math.Max(tip.x, Math.Max(left.x, right.x));
            float minY = Math.Min(tip.y, Math.Min(left.y, right.y)), maxY = Math.Max(tip.y, Math.Max(left.y, right.y));
            Point shift = new Point(minX < 8 ? 8 - minX : maxX > Width - 8 ? Width - 8 - maxX : 0,
                minY < 8 ? 8 - minY : maxY > Height - 8 ? Height - 8 - maxY : 0);
            tip += shift; left += shift; right += shift;
            return true;
        }

        private Rect HitDirectionCueBounds()
        {
            Point tip, left, right;
            if (!HitDirectionPoints(LatestHitDirection(), out tip, out left, out right)) return new Rect();
            float x = Math.Min(tip.x, Math.Min(left.x, right.x)) - 4, y = Math.Min(tip.y, Math.Min(left.y, right.y)) - 4;
            return new Rect(x, y, Math.Max(tip.x, Math.Max(left.x, right.x)) - x + 4,
                Math.Max(tip.y, Math.Max(left.y, right.y)) - y + 4);
        }

        private void DrawHitDirectionCue()
        {
            Effect effect = LatestHitDirection(); Point tip, left, right;
            if (!HitDirectionPoints(effect, out tip, out left, out right)) return;
            float alpha = Mathf.Clamp01(effect.life / .2f);
            Color shadow = ink, color = hitDirectionColor; shadow.a *= alpha; color.a *= alpha;
            DrawDangerSegment(left, tip, 5, shadow); DrawDangerSegment(right, tip, 5, shadow);
            DrawDangerSegment(left, tip, 2, color); DrawDangerSegment(right, tip, 2, color);
        }
    }
}
