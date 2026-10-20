using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private bool SignalRepriseHudVisible
        {
            get { return Game.mode == "world" && Game.save.room == 1 && !applicationPaused && !VisualReviewPaused && !Game.Dying && !Game.practicing &&
                (Game.SignalRepriseActive || Game.LastSignalReprise != null); }
        }
        private Rect SignalRepriseHudBounds() { return new Rect(430, CombatHud ? 620 : 575, 420, 64); }
        private bool SameSignalRepriseRegion(Rect rect)
        {
            Rect r = SignalRepriseHudBounds();
            return rect.x == r.x && rect.y == r.y && rect.width == r.width && rect.height == r.height;
        }
        private bool HasFallenSignalReprise
        { get { return Game.fallen.Exists(f => f.room == Game.save.room && Game.IsFallenSignalRepriseActor(f.actor)); } }
        private void DrawSignalRepriseHud()
        {
            if (!SignalRepriseHudVisible) return;
            bool active = Game.SignalRepriseActive;
            SignalRepriseRecord record = Game.LastSignalReprise, previous = active ? record : Game.PreviousSignalReprise;
            float time = active ? Game.SignalRepriseTime : record.seconds;
            float damage = active ? Game.SignalRepriseDamage : record.damage;
            int contacts = active ? Game.SignalRepriseContacts : record.contacts, breaks = active ? Game.SignalRepriseBreaks : record.breaks;
            int hurt = active ? Game.SignalRepriseHitsTaken : record.hitsTaken;
            Rect r = SignalRepriseHudBounds();
            Panel(r.x, r.y, r.width, r.height, new Color(.035f, .07f, .10f, .92f), active ? gold : mint);
            string title = active ? Game.L("旧路复试", "Warden reprise") : record.won ? Game.L("复试完成", "Reprise won") : Game.L("复试结束", "Reprise ended");
            title += " · " + time.ToString("0.0") + "s";
            if (previous != null) title += Game.L(" · 上次", " · last ") + (previous.won ? Game.L("胜利 ", "win ") : Game.L("结束 ", "end ")) + previous.seconds.ToString("0.0") + "s";
            Text(r.x + 8, r.y + 3, r.width - 16, 24, title, 16, active ? gold : mint, TextAnchor.MiddleCenter);
            Text(r.x + 8, r.y + 27, r.width - 16, 20, Game.L("对守望伤害 ", "Warden dmg ") + damage.ToString("0") +
                Game.L(" · 命中 ", " · hits ") + contacts + Game.L(" · 破势 ", " · breaks ") + breaks + Game.L(" · 受击 ", " · hurt ") + hurt, 12, paper, TextAnchor.MiddleCenter);
            Text(r.x + 8, r.y + 47, r.width - 16, 17, active ? Game.L("回第三处旧灯可结束 · 不重复发奖", "Return to signal 3 to stop · no repeat reward")
                : Game.L("本次会话记录 · 回第三处旧灯可再试", "Session record · return to signal 3 to retry"), 11, muted, TextAnchor.MiddleCenter);
        }
    }
}
