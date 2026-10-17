using System;
using System.Collections.Generic;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private struct CombatTextPlacement
        {
            public Effect effect;
            public Point offset;
        }
        private readonly List<CombatTextPlacement> combatTextPlacements = new List<CombatTextPlacement>(32);
        private readonly List<Rect> combatTextHudRegions = new List<Rect>(16);
        private GameCore combatTextGame;
        private int combatTextGeneration = -1;

        private static bool IsCombatText(Effect effect)
        { return effect.life > 0 && (effect.kind == "number" || effect.kind == "break"); }

        private Rect CombatTextEnvelope(Effect effect, Point offset)
        {
            Point p = ScreenPoint(effect.position) + offset;
            // Reserve the complete existing 22px rise and 1px shadow. A new
            // label cannot enter an older label's path later in its lifetime.
            return new Rect(p.x - 60, p.y - 77, 121, 55);
        }
        private bool CombatTextBlocked(Rect candidate)
        {
            foreach (CombatTextPlacement placement in combatTextPlacements)
                if (Overlap(candidate, CombatTextEnvelope(placement.effect, placement.offset))) return true;
            return false;
        }
        private Point PlaceCombatText(Effect effect)
        {
            Rect original = CombatTextEnvelope(effect, new Point());
            // A remote hit must remain clipped. Packing two invisible labels
            // into the canvas would invent a visible hit at the wrong source.
            if (!Overlap(original, new Rect(0, 0, Width, Height))) return new Point();
            if (!CombatTextBlocked(original)) return new Point();

            // Only colliding labels move. Prefer the nearest clear lane inside
            // the canvas; the existing singleton position stays pixel-exact.
            float best = float.MaxValue;
            Point chosen = new Point();
            int columns = (int)Math.Ceiling(Width / 124f) + 1;
            int rows = (int)Math.Ceiling(Height / 58f) + 1;
            for (int row = -rows; row <= rows; row++)
                for (int column = -columns; column <= columns; column++)
                {
                    float x = column * 124, y = row * 58;
                    float distance = x * x + y * y + (y > 0 ? .25f : 0);
                    if (distance >= best) continue;
                    Rect candidate = new Rect(original.x + x, original.y + y, original.width, original.height);
                    if (candidate.x < 4 || candidate.y < 4 || candidate.x + candidate.width > Width - 4 || candidate.y + candidate.height > Height - 4) continue;
                    if (OverlapsRegions(combatTextHudRegions, candidate)) continue;
                    if (CombatTextBlocked(candidate)) continue;
                    best = distance; chosen = new Point(x, y);
                }
            if (best < float.MaxValue) return chosen;

            // If the canvas is saturated, retain every original message rather
            // than replacing, merging or shortening the gameplay feedback.
            float lift = 0;
            for (int pass = 0; pass <= combatTextPlacements.Count; pass++)
            {
                Rect candidate = new Rect(original.x, original.y + lift, original.width, original.height);
                if (!CombatTextBlocked(candidate)) return new Point(0, lift);
                foreach (CombatTextPlacement placement in combatTextPlacements)
                {
                    Rect other = CombatTextEnvelope(placement.effect, placement.offset);
                    if (Overlap(candidate, other)) lift = Math.Min(lift, other.y - original.y - original.height - 3);
                }
            }
            return new Point(0, lift);
        }
        private void PrepareCombatTextLayout()
        {
            if (combatTextGame != Game || combatTextGeneration != Game.WorldGeneration)
            {
                ResetCombatTextLayout(); combatTextGame = Game; combatTextGeneration = Game.WorldGeneration;
            }
            combatTextHudRegions.Clear();
            if (Game.mode == "world") combatTextHudRegions.AddRange(HudRegions());
            for (int i = combatTextPlacements.Count - 1; i >= 0; i--)
                if (!IsCombatText(combatTextPlacements[i].effect) || !Game.effects.Contains(combatTextPlacements[i].effect)) combatTextPlacements.RemoveAt(i);
            foreach (Effect effect in Game.effects)
            {
                if (!IsCombatText(effect)) continue;
                bool found = false;
                foreach (CombatTextPlacement placement in combatTextPlacements)
                    if (placement.effect == effect) { found = true; break; }
                if (!found) combatTextPlacements.Add(new CombatTextPlacement { effect = effect, offset = PlaceCombatText(effect) });
            }
        }
        private void ResetCombatTextLayout()
        {
            combatTextPlacements.Clear(); combatTextHudRegions.Clear();
            combatTextGame = null; combatTextGeneration = -1;
        }
        private Rect CombatTextRect(Effect effect)
        {
            Point p = ScreenPoint(effect.position);
            float progress = 1 - effect.life / effect.maxLife;
            foreach (CombatTextPlacement placement in combatTextPlacements)
                if (placement.effect == effect) { p = p + placement.offset; break; }
            return new Rect(p.x - 60, p.y - 55 - progress * 22, 120, 32);
        }
    }
}
