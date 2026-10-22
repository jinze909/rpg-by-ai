using System;
using System.Collections.Generic;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private Rect InteractionPromptBounds(Point anchor, bool hasHint)
        {
            // Unchanged residents and other objects retain their original
            // single-line prompt. Only the new state card needs extra room.
            if (!hasHint) return new Rect(anchor.x - 125, anchor.y + 14, 250, 35);
            const float width = 320, height = 64;
            List<Rect> regions = HudRegions();
            bool exploration = !CombatHud;
            Rect best = new Rect(); float bestScore = float.MaxValue;
            for (int option = 0; option < 5; option++)
            {
                // Prefer the four positions beside the object. Consider the
                // center only when all four collide with existing HUD content.
                float x = option == 4 ? (Width - width) / 2 : option == 2 ? anchor.x - width - 24 : option == 3 ? anchor.x + 24 : anchor.x - width / 2;
                float y = option == 4 ? (Height - height) / 2 : option == 0 ? anchor.y + 14 : option == 1 ? anchor.y - height - 64 : anchor.y - height / 2;
                Rect candidate = new Rect(Math.Max(8, Math.Min(Width - width - 8, x)), Math.Max(8, Math.Min(Height - height - 8, y)), width, height);
                float score = 0;
                foreach (Rect region in regions)
                    score += InteractionPromptOverlap(candidate, region);
                if (exploration)
                {
                    // These read-only labels and the later toast have no HUD
                    // control region. Reserve their actual draw bounds locally
                    // without changing pointer ownership for the world.
                    score += InteractionPromptOverlap(candidate, new Rect(406, 28, 468, 36));
                    score += InteractionPromptOverlap(candidate, new Rect(412, 69, 456, 25));
                    score += InteractionPromptOverlap(candidate, new Rect(25, 725, 302, 24));
                    score += InteractionPromptOverlap(candidate, new Rect(25, 752, 302, 23));
                    if (Game.toastTime > 0) score += InteractionPromptOverlap(candidate, new Rect(382, 132, 516, 67));
                }
                if (score < bestScore) { best = candidate; bestScore = score; }
                if (score == 0) break;
            }
            return best;
        }
        private static float InteractionPromptOverlap(Rect a, Rect b)
        {
            return Math.Max(0, Math.Min(a.x + a.width, b.x + b.width) - Math.Max(a.x, b.x))
                * Math.Max(0, Math.Min(a.y + a.height, b.y + b.height) - Math.Max(a.y, b.y));
        }
        private void DrawInteractionPrompt(WorldObject near)
        {
            string hint = Game.InteractionHint(near, Game.save.english);
            bool hasHint = hint.Length > 0;
            Rect rect = InteractionPromptBounds(ScreenPoint(new Point(near.x, near.y)), hasHint);
            string label = ControlKey("E", "B") + "  " + Game.L(near.name, near.en);
            Panel(rect.x, rect.y, rect.width, rect.height, new Color(.04f, .075f, .12f, .94f), gold);
            if (!hasHint) Text(rect.x + 8, rect.y + 4, 234, 27, label, 14, paper, TextAnchor.MiddleCenter);
            else
            {
                Text(rect.x + 8, rect.y + 5, rect.width - 16, 24, label, 14, paper, TextAnchor.MiddleCenter);
                Text(rect.x + 8, rect.y + 33, rect.width - 16, 24, hint, 12, muted, TextAnchor.MiddleCenter);
            }
        }
    }
}
