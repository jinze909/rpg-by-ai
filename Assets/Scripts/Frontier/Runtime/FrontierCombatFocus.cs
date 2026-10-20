using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private bool CombatFocusVisible
        {
            get { return Game.mode == "world" && !applicationPaused && !VisualReviewPaused && !Game.Dying && !TrainingReview &&
                (Game.CombatFocusTarget != null || Game.CanAcquireCombatFocus); }
        }
        private Rect CombatFocusBounds()
        { return CombatHud ? new Rect(1030, 105, 230, 32) : new Rect(1015, 489, 234, 32); }
        private bool CombatFocusCycleVisible { get { return CombatFocusVisible && Game.CanCycleCombatFocus; } }
        private Rect CombatFocusCycleBounds()
        { Rect r = CombatFocusBounds(); return new Rect(r.x, r.y + r.height + 6, r.width, 32); }

        private void ToggleCombatFocusFromInput()
        {
            if (Game.ToggleCombatFocus(InputAim() ?? Game.aim)) Game.sound = "ui";
        }
        private void CycleCombatFocusFromInput(int step)
        { if (Game.CycleCombatFocus(step)) Game.sound = "ui"; }
        private void DrawCombatFocusControl()
        {
            if (!CombatFocusVisible) return;
            Rect r = CombatFocusBounds();
            if (Button(r.x, r.y, r.width, r.height, ControlKey("V", "R3") + " · " +
                (Game.CombatFocusTarget == null ? Game.L("锁定目标", "Lock target") : Game.L("解除锁定", "Release target")), Game.CombatFocusTarget != null))
                ToggleCombatFocusFromInput();
            if (CombatFocusCycleVisible)
            {
                Rect next = CombatFocusCycleBounds();
                if (Button(next.x, next.y, next.width, next.height, ControlKey("X", "L3") + " · " + Game.L("切换目标", "Next target")))
                    CycleCombatFocusFromInput(1);
            }
        }
        private void DrawCombatFocusMarker()
        {
            if (!CombatFocusVisible) return;
            Enemy target = Game.CombatFocusTarget;
            if (target == null) return;
            Point p = ScreenPoint(target.Position);
            // Static corner brackets identify selection without resembling a
            // damage circle, changing authored art or obscuring the body.
            float left = p.x - 21, right = p.x + 21, top = p.y - 7, bottom = p.y + 12;
            for (int pass = 0; pass < 2; pass++)
            {
                float thick = pass == 0 ? 5 : 2;
                Color color = pass == 0 ? ink : mint;
                for (int side = 0; side < 2; side++) for (int end = 0; end < 2; end++)
                {
                    float x = side == 0 ? left : right, y = end == 0 ? top : bottom;
                    Box(side == 0 ? x - thick / 2 : x - 9 + thick / 2, y - thick / 2, 9, thick, color);
                    Box(x - thick / 2, end == 0 ? y - thick / 2 : y - 7 + thick / 2, thick, 7, color);
                }
            }
        }
    }
}
