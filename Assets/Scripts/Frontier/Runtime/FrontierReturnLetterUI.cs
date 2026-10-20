using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private bool ReturnLetterHudVisible
        {
            get { return Game.mode == "world" && Game.save.room == 5 && !applicationPaused && !VisualReviewPaused &&
                !Game.Dying && !Game.practicing && Game.ReturnLetterSignalActive; }
        }
        private Rect ReturnLetterHudBounds() { return new Rect(430, CombatHud ? 620 : 575, 420, 64); }
        private bool SameReturnLetterRegion(Rect rect)
        {
            Rect r = ReturnLetterHudBounds();
            return rect.x == r.x && rect.y == r.y && rect.width == r.width && rect.height == r.height;
        }
        private void DrawReturnLetterHud()
        {
            if (!ReturnLetterHudVisible) return;
            bool ready = Game.ReturnLetterSignalConfirming;
            Rect r = ReturnLetterHudBounds();
            Panel(r.x, r.y, r.width, r.height, new Color(.035f, .07f, .10f, .94f), ready ? mint : gold);
            Text(r.x + 8, r.y + 3, r.width - 16, 24,
                ready ? Game.L("灯光已亮 · 按 ", "Signal aligned · ") + ControlKey("E", "B")
                      : Game.L("等候短光 · 暂不按键", "Wait for alignment · do not press"),
                16, ready ? mint : gold, TextAnchor.MiddleCenter);
            Text(r.x + 8, r.y + 29, r.width - 16, 23,
                (ready ? Game.L("确认时间 ", "Confirm within ") : Game.L("准备 ", "Ready in ")) +
                Game.ReturnLetterSignalRemaining.ToString("0.0") + "s" +
                Game.L(" · 离开可重试，不耗材料", " · leave to retry, no cost"), 12, paper, TextAnchor.MiddleCenter);
            // This is authoritative timing information, independent of effects and motion settings.
            float duration = ready ? GameCore.ReturnLetterWindowSeconds : GameCore.ReturnLetterPrepareSeconds;
            Bar(r.x + 8, r.y + 56, r.width - 16, Game.ReturnLetterSignalRemaining / duration, ready ? mint : gold, 4);
        }
    }
}
