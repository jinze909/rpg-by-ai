using System;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private int selectedCompanionNote;
        private readonly RectOffset companionNotePadding = new RectOffset(6, 6, 2, 2);
        private bool CompanionNoteButton(float x, float y, float width, float height, string label)
        {
            RectOffset normal = buttonStyle.padding, disabled = disabledButtonStyle.padding;
            try
            {
                // The shipped 16px Chinese font can have 18px of visible ink.
                // Reserve it only for these new compact controls; keep old styles intact.
                buttonStyle.padding = disabledButtonStyle.padding = companionNotePadding;
                return Button(x, y, width, height, label);
            }
            finally { buttonStyle.padding = normal; disabledButtonStyle.padding = disabled; }
        }
        private string CompanionTacticName(int id)
        {
            return id == 0 ? Game.L("治疗守护", "Heal & guard") : id == 1 ? Game.L("贯穿三箭", "Piercing volley") : Game.L("寒星领域", "Frost field");
        }
        private string CompanionActionLabel
        {
            get { return Game.save.companion == 0 ? Game.L("守护", "Guard") : Game.save.companion == 1 ? Game.L("三箭", "Volley") : Game.save.companion == 2 ? Game.L("寒星", "Frost") : Game.L("同伴", "Command"); }
        }
        private void OpenCompanionNotes(int id)
        { if (id < 0 || id > 2) return; selectedCompanionNote = id; OpenMenu("companionnotes"); }
        private bool CompanionPreparing
        {
            get { return Game.companionAction != null && Game.companionAction.kind == "command" && Game.companionAction.weapon == Game.save.companion && !Game.companionAction.released; }
        }
        private bool CompanionFeedbackVisible
        {
            get
            {
                return Game.mode == "world" && !applicationPaused && !VisualReviewPaused && !Game.Dying && !Game.practicing
                    && Game.save.companion >= 0 && Game.save.companion <= 2
                    && (CompanionPreparing || Game.CompanionFeedbackTime > 0 && Game.LastCompanionReceipt != null && Game.LastCompanionReceipt.id == Game.save.companion);
            }
        }
        private Rect CompanionFeedbackBounds()
        { return CombatHud ? new Rect(430, 690, 420, 32) : new Rect(342, 653, 596, 33); }
        private bool SameCompanionFeedbackRegion(Rect rect)
        {
            Rect feedback = CompanionFeedbackBounds();
            return rect.x == feedback.x && rect.y == feedback.y && rect.width == feedback.width && rect.height == feedback.height;
        }
        private string CompanionReceiptText(CompanionCommandReceipt receipt)
        {
            if (receipt.id == 0) return "+" + receipt.actualHeal.ToString("0.##") + Game.L("生命 · +", " HP · +") + receipt.actualEnergy.ToString("0.##") + Game.L("耐力", " EN");
            if (receipt.id == 1) return Game.L("已射出", "") + receipt.arrowCount + Game.L("发贯穿箭", " arrows released");
            return Game.L("领域已生成 · 控制", "Field active · controlled ") + receipt.controlCount;
        }
        private void DrawCompanionFeedbackHud()
        {
            if (!CompanionFeedbackVisible) return;
            Rect rect = CompanionFeedbackBounds();
            Panel(rect.x, rect.y, rect.width, rect.height, new Color(.035f, .07f, .10f, .92f), CompanionPreparing ? gold : mint);
            string name = Game.L(GameCore.CompanionNames[Game.save.companion], GameCore.CompanionNamesEn[Game.save.companion]);
            string state = CompanionPreparing ? Game.L("准备指令", "Preparing command") : CompanionReceiptText(Game.LastCompanionReceipt);
            Text(rect.x + 8, rect.y + 3, rect.width - 16, rect.height - 6, name + " · " + state, 13, CompanionPreparing ? gold : mint, TextAnchor.MiddleCenter);
        }
        private string CompanionNoteStatus(int id)
        {
            if (!Game.Has("companion_" + id)) return Game.L("尚未招募 · 可先查看指令", "Not recruited · preview only");
            if (Game.save.companion != id) return Game.L("当前未同行", "Not currently travelling with you");
            if (Game.practicing) return Game.L("训练中不出战", "Sits out during training");
            if (CompanionPreparing) return Game.L("准备中，尚未释放", "Preparing; not released yet");
            if (Game.companionCooldown > 0) return Game.L("指令冷却 ", "Command cooldown ") + Game.companionCooldown.ToString("0.0") + "s";
            return Game.L("指令可用 · Z / RB", "Command ready · Z / RB");
        }
        private void DrawCompanionNotes()
        {
            Window(Game.L("同伴战术手册", "Companion command notes"), Game.L("只读说明 · 不会发出指令、切换同伴或消耗物资", "Read only · no commands, companion switches or supplies spent"));
            if (Game.mode != "companionnotes") return;
            for (int id = 0; id < 3; id++)
                if (Button(279 + id * 318, 174, 302, 36, Game.L(GameCore.CompanionNames[id], GameCore.CompanionNamesEn[id]), selectedCompanionNote == id)) selectedCompanionNote = id;
            CompanionTacticInfo info = Game.CompanionTactic(selectedCompanionNote);
            if (info == null) return;
            Card(279, 224, 938, 469); Portrait(info.id + 3, 302, 244, 146, 146);
            Text(472, 241, 720, 41, CompanionTacticName(info.id), 24, gold);
            string numbers = info.id == 0 ? Game.L("生命 +", "Health +") + info.heal.ToString("0.#") + Game.L(" · 耐力 +", " · energy +") + info.energy + Game.L(" · 防护 ", " · guard ") + info.guardSeconds + "s"
                : info.id == 1 ? info.arrows + Game.L("发贯穿箭 · 单箭基础伤害 ", " piercing arrows · base damage per arrow ") + info.arrowBaseDamage.ToString("0.0")
                : Game.L("领域半径 ", "Field radius ") + info.frostRadius + " · " + info.frostDuration + "s · " + info.frostPulseCount + Game.L("次脉冲 · 每次基础伤害 ", " pulses · base damage ") + info.frostPulseDamage.ToString("0.0");
            Text(472, 295, 720, 56, numbers, 16, paper);
            Text(472, 360, 720, 45, Game.L("羁绊 ", "Bond ") + info.bond + "/5 · " + Game.L("准备 ", "Prepare ") + info.contact.ToString("0.00") + "s · " + Game.L("冷却 ", "Cooldown ") + info.cooldown + "s", 15, mint);
            string explanation = info.id == 0
                ? Game.L("释放时回复生命与耐力，不会超过上限。防护至少", "On release, restore health and energy up to their caps. Guard lasts at least ") + info.guardSeconds + Game.L("秒，伤害乘", "s; damage multiplier ") + info.incomingMultiplier + Game.L("，即减伤", ", a ") + ((1 - info.incomingMultiplier) * 100).ToString("0") + Game.L("%；不是额外生命值。\n自动支援与原有防护机制保留。", "% reduction, not extra health.\nAutomatic support and existing guard rules remain.")
                : info.id == 1 ? Game.L("按起手时的方向与目标点发射，从同伴位置出箭，墙体仍会挡住箭。\n显示当前配置下的单箭基础伤害；实际接触还会计算暴击、护甲与剩余生命。射出三箭不等于三箭均命中。", "Arrows leave the companion along the direction and target captured at preparation. Walls still block them.\nThis is base damage for your current gear; actual contacts still use critical, armor and remaining-health rules. Three arrows released does not mean three hits.")
                : Game.L("释放时以主角当时的位置生成领域，之后固定。\n另对", "The field forms at the hero's position on release, then stays there.\nInitial control reaches visible enemies within ") + info.controlRadius + Game.L("格内且未被墙体挡住的敌人施加初始控制。领域命中会改变受控时间，不能保证所有目标连续冻结", " tiles. Field hits refresh control; not every target stays continuously frozen for ") + info.controlSeconds + Game.L("秒。", "s.");
            explanation += Game.L("\n自动攻击优先跟随同伴7格内且可见的锁定目标，否则选择最近敌人；主角生命低于80%时，米拉优先治疗。", "\nAutomatic attacks prefer your lock within the companion's 7-tile sight/range; otherwise the nearest foe. Mira heals you first below 80% HP.");
            Text(302, 424, 891, 132, explanation, 16, paper);
            Text(302, 568, 891, 42, CompanionNoteStatus(info.id), 15, gold);
            CompanionCommandReceipt receipt = Game.LastCompanionReceipt;
            Text(302, 625, 891, 45, receipt != null && receipt.id == info.id ? Game.L("上次实际施放：", "Last actual release: ") + CompanionReceiptText(receipt) : Game.L("本次还没有这位同伴的施放记录。", "No release recorded for this companion in this session."), 14, muted);
            if (CompanionNoteButton(929, 704, 288, 25, Game.L("返回同行伙伴", "Back to companions"))) OpenMenu("companions");
        }
    }
}
