using System;
using UnityEngine;

namespace Starfall
{
    public sealed partial class FrontierRuntime
    {
        private int selectedBattlePlan, battlePlanConfirmation = -1, battlePlanGeneration = -1;
        private bool battlePlanDeleting;
        private string techniqueGuideParent = "talents";
        private void OpenBattlePlans()
        { battlePlanConfirmation = -1; battlePlanGeneration = Game.WorldGeneration; OpenMenu("plans"); }
        private void OpenTechniqueGuide()
        { techniqueGuideParent = Game.mode == "plans" ? "plans" : "talents"; OpenMenu("techniques"); }
        private string PlanStyle(int style)
        { return Game.L(new[] { "均衡", "蓄势", "流转" }[style], new[] { "Balanced", "Focused", "Flow" }[style]); }
        private string PlanRelic(int id)
        { return id < 0 ? Game.L("空", "Empty") : Game.L(GameCore.RelicNames[id], GameCore.RelicNamesEn[id]); }
        private string PlanGear(BattlePlan plan)
        { return WeaponName(plan.weapon) + " · " + PlanStyle(plan.style); }
        private void DrawBattlePlans()
        {
            if(battlePlanGeneration != Game.WorldGeneration)
            { battlePlanConfirmation = -1; selectedBattlePlan = 0; battlePlanGeneration = Game.WorldGeneration; }
            if(battlePlanConfirmation >= 0) { DrawBattlePlanConfirmation(); return; }
            Window(Game.L("装备方案", "Battle plans"), Game.L("三种常用配置 · 在艾妲的对练中试用，再出发", "Three saved setups · try them in Ada's sparring before setting out"));
            if(Game.mode != "plans") return;
            for(int slot = 0; slot < GameCore.BattlePlanSlots; slot++)
            {
                float y = 178 + slot * 148; BattlePlan plan = Game.BattlePlanAt(slot);
                Card(279, y, 302, 132, selectedBattlePlan == slot);
                if(Button(295, y + 14, 270, 38, Game.L("方案 ", "Plan ") + (slot + 1), selectedBattlePlan == slot)) selectedBattlePlan = slot;
                Text(295, y + 61, 270, 57, plan == null ? Game.L("空位置 · 可记下当前装备", "Empty slot · store your current gear") : PlanGear(plan) + "\n" + (Game.BattlePlanIsCurrent(slot) ? Game.L("当前配置", "Current setup") : Game.BattlePlanAvailable(slot) ? Game.L("可应用", "Ready to apply") : Game.L("暂不可用", "Unavailable")), 14, plan == null ? muted : paper);
            }
            Card(597, 178, 620, 440); BattlePlan selected = Game.BattlePlanAt(selectedBattlePlan);
            Text(619, 195, 574, 35, Game.L("方案 ", "Plan ") + (selectedBattlePlan + 1), 23, gold);
            if(selected == null)
            {
                Text(619, 247, 574, 95, Game.L("选择左侧位置，把当前的武器、打法、战技和两件遗物记下来。换装试用后，可以完整切回这个装备。", "Choose a slot and store your current weapon, style, technique and two relics. After trying other gear, return to this setup in one step."), 18, paper);
                Text(619, 369, 574, 86, Game.L("方案只记下装备配置。天赋点数、职业与同行者仍由你原来的选择决定。", "Plans store gear choices. Your talents, class and companion keep their separate choices."), 16, muted);
            }
            else
            {
                Text(619, 247, 574, 32, PlanGear(selected), 21, paper);
                Text(619, 292, 574, 60, Game.L("职业战技：", "Class technique: ") + Game.ClassTechniqueName(selected.technique), 17, paper);
                Text(619, 370, 574, 53, Game.L("遗物：", "Relics: ") + PlanRelic(selected.relicFirst) + " / " + PlanRelic(selected.relicSecond), 17, paper);
                Text(619, 435, 574, 77, Game.BattlePlanAvailable(selectedBattlePlan) ? Game.L("应用所有配置，保留其他武器的打法。生命、耐力与冷却继续保持。", "Apply the whole setup; other weapons retain their styles. Health, energy and cooldowns stay as they are.") : Game.BattlePlanUnavailableReason(selectedBattlePlan), 16, muted);
            }
            Text(619, 525, 574, 30, Game.L("现在：", "Now: ") + WeaponName(Game.save.weapon) + " · " + PlanStyle(Game.Style) + " / " + SkillName(), 13, gold);
            if(Button(619, 572, 175, 34, Game.L("记下当前装备", "Store current gear")))
            {
                if(selected == null) { if(Game.SaveBattlePlan(selectedBattlePlan)) Save(); }
                else { battlePlanConfirmation = selectedBattlePlan; battlePlanDeleting = false; }
            }
            if(Button(813, 572, 175, 34, Game.BattlePlanIsCurrent(selectedBattlePlan) ? Game.L("当前方案", "Current plan") : Game.L("应用方案", "Apply plan"), false, Game.BattlePlanAvailable(selectedBattlePlan) && !Game.BattlePlanIsCurrent(selectedBattlePlan)))
            { if(Game.ApplyBattlePlan(selectedBattlePlan)) Save(); }
            if(Button(1008, 572, 185, 34, Game.L("清除方案", "Clear plan"), false, selected != null))
            { battlePlanConfirmation = selectedBattlePlan; battlePlanDeleting = true; }
            if(Button(279, 650, 220, 38, Game.L("返回背包", "Back to inventory"))) OpenMenu("inventory");
            if(Button(515, 650, 300, 38, Game.L("战技效果与用途", "Technique field notes"))) OpenTechniqueGuide();
            if(Button(832, 650, 385, 38, Game.L("选择遗物", "Choose relics"))) OpenMenu("loadout");
        }
        private void DrawBattlePlanConfirmation()
        {
            drawingModal = true; drawnControls.Clear(); Dim(); Card(365, 275, 550, 244);
            Text(390, 294, 500, 43, Game.L(battlePlanDeleting ? "清除这个方案？" : "覆盖这个方案？", battlePlanDeleting ? "Clear this battle plan?" : "Replace this battle plan?"), 23, gold);
            Text(390, 354, 500, 65, Game.L(battlePlanDeleting ? "只清除这个位置记下的方案。现在的装备与其他方案保留。" : "用当前装备替换这个位置。其他方案与成长保留。", battlePlanDeleting ? "Clear only the stored plan in this slot. Current gear and other plans stay." : "Replace this slot with your current gear. Other plans and your progression stay."), 17, paper);
            if(Button(390, 441, 242, 49, Game.L("确认", "Confirm"), true))
            {
                int slot = battlePlanConfirmation; battlePlanConfirmation = -1;
                bool changed = battlePlanDeleting ? Game.RemoveBattlePlan(slot) : Game.SaveBattlePlan(slot);
                if(changed) Save();
            }
            if(Button(649, 441, 241, 49, Game.L("取消", "Cancel"))) battlePlanConfirmation = -1;
        }
        private void DrawTechniqueGuide()
        {
            Window(Game.L("战技指南", "Class technique guide"), Game.L("比较两种职业战技 · 换武器不会改变职业", "Compare both class techniques · changing weapons keeps your class"));
            if(Game.mode != "techniques") return;
            for(int variant = 0; variant < 2; variant++)
            {
                float x = 279 + variant * 477; bool learned = variant == 0 || Game.Has("technique_" + Game.save.profession), current = Game.save.technique == variant;
                Card(x, 178, 460, 460, current);
                Text(x + 23, 198, 414, 27, Game.L(variant == 0 ? "基础战技" : "第二战技", variant == 0 ? "Base technique" : "Alternate technique") + (current ? Game.L(" · 当前", " · CURRENT") : ""), 14, gold);
                Text(x + 23, 244, 414, 68, Game.ClassTechniqueName(variant), 24, paper);
                Text(x + 23, 337, 414, 205, Game.ClassTechniqueDescription(variant), 17, paper);
                Text(x + 23, 555, 414, 28, learned ? Game.L("已学会，可自由选择", "Learned; freely selectable") : Game.L("学习需要等级5与技能点1", "Requires level 5 and 1 point"), 13, muted);
                string label = current ? Game.L("当前战技", "Current technique") : learned ? Game.L("使用这项战技", "Use this technique") : Game.L("学习第二战技", "Learn alternate technique");
                if(Button(x + 23, 594, 414, 32, label, current, !current && (learned || Game.save.level >= 5 && Game.save.skillPoints > 0))) { Game.LearnTechnique(); Save(); }
            }
            Text(279, 651, 625, 53, Game.L("耐力 ", "Energy ") + (Game.Relic(4) ? 50 : 40) + Game.L(" · 接触0.28秒 · 冷却3.5秒\n完整动作0.62秒；在对练中试用打法。", " · contact 0.28s · cooldown 3.5s\nFull action 0.62s; test your build in sparring."), 13, muted);
            if(Button(929, 651, 288, 38, Game.L("返回", "Back"))) OpenMenu(techniqueGuideParent);
        }
    }
}
