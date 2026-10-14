using System;

namespace Starfall
{
    public sealed partial class GameCore
    {
        public const int BattlePlanSlots = 3;
        private bool CanManageBattlePlans { get { return mode == "plans" && !Dying; } }

        private static bool ValidBattlePlan(BattlePlan plan)
        {
            return plan != null && plan.recorded && plan.weapon >= 0 && plan.weapon <= 2 && plan.style >= 0 && plan.style <= 2 &&
                plan.technique >= 0 && plan.technique <= 1 && plan.relicFirst >= -1 && plan.relicFirst <= 5 &&
                plan.relicSecond >= -1 && plan.relicSecond <= 5 &&
                (plan.relicFirst < 0 || plan.relicFirst != plan.relicSecond);
        }
        private static BattlePlan CopyBattlePlan(BattlePlan plan)
        {
            return new BattlePlan { recorded = plan.recorded, weapon = plan.weapon, style = plan.style, technique = plan.technique,
                relicFirst = plan.relicFirst, relicSecond = plan.relicSecond };
        }
        private void SanitizeBattlePlans()
        {
            BattlePlan[] previous = save.battlePlans;
            save.battlePlans = new BattlePlan[BattlePlanSlots];
            if (previous == null) return;
            for (int i = 0; i < Math.Min(previous.Length, BattlePlanSlots); i++)
                if (ValidBattlePlan(previous[i])) save.battlePlans[i] = CopyBattlePlan(previous[i]);
        }
        private BattlePlan StoredBattlePlan(int slot)
        {
            return slot >= 0 && slot < BattlePlanSlots && save.battlePlans != null && slot < save.battlePlans.Length
                ? save.battlePlans[slot] : null;
        }
        private BattlePlan CurrentBattlePlan()
        {
            if (save.weapon < 0 || save.weapon > 2 || save.weaponStyles == null || save.weaponStyles.Length != 3 ||
                save.relics == null || save.relics.Length != 2) return null;
            var plan = new BattlePlan { recorded = true, weapon = save.weapon, style = save.weaponStyles[save.weapon], technique = save.technique,
                relicFirst = save.relics[0], relicSecond = save.relics[1] };
            return ValidBattlePlan(plan) ? plan : null;
        }
        private static bool SameBattlePlan(BattlePlan first, BattlePlan second)
        {
            return first != null && second != null && first.weapon == second.weapon && first.style == second.style &&
                first.technique == second.technique && first.relicFirst == second.relicFirst && first.relicSecond == second.relicSecond;
        }
        private string UnavailableBattlePlan(BattlePlan plan)
        {
            if (!ValidBattlePlan(plan)) return L("方案内容无法使用。", "This plan is invalid.");
            if (plan.weapon > 0 && !Has("weapon_" + plan.weapon))
                return L("先在铁匠处制作这把武器。", "Craft this weapon at the blacksmith first.");
            // Style ownership belongs to the plan's weapon, just as in the
            // existing weapon comparison, rather than the currently held one.
            if (plan.style > 0 && !Has("core_1") && MasteryRank(plan.weapon) < 1)
                return L("第一星核或这把武器熟练度1解锁打法。", "First core or this weapon's mastery rank 1 unlocks its style.");
            if (plan.technique == 1 && !Has("technique_" + save.profession))
                return L("先在职业成长中学习第二战技。", "Learn the alternate class technique in Talents first.");
            if (plan.relicFirst >= 0 && !Has("relic_" + plan.relicFirst) ||
                plan.relicSecond >= 0 && !Has("relic_" + plan.relicSecond))
                return L("先找到方案中的遗物。", "Discover this plan's relics first.");
            return "";
        }
        public BattlePlan BattlePlanAt(int slot)
        {
            BattlePlan plan = StoredBattlePlan(slot);
            return ValidBattlePlan(plan) ? CopyBattlePlan(plan) : null;
        }
        public string BattlePlanUnavailableReason(int slot)
        {
            if (slot < 0 || slot >= BattlePlanSlots) return L("这个方案位置无法使用。", "This plan slot is invalid.");
            BattlePlan plan = StoredBattlePlan(slot);
            return plan == null ? L("尚未记录方案。", "No plan saved here.") : UnavailableBattlePlan(plan);
        }
        public bool BattlePlanAvailable(int slot) { return BattlePlanUnavailableReason(slot) == ""; }
        public bool BattlePlanIsCurrent(int slot) { return SameBattlePlan(BattlePlanAt(slot), CurrentBattlePlan()); }
        public bool SaveBattlePlan(int slot)
        {
            if (!CanManageBattlePlans || slot < 0 || slot >= BattlePlanSlots) return false;
            BattlePlan plan = CurrentBattlePlan();
            if (plan == null || UnavailableBattlePlan(plan) != "") return false;
            if (save.battlePlans == null || save.battlePlans.Length != BattlePlanSlots) SanitizeBattlePlans();
            save.battlePlans[slot] = plan; revision++; sound = "ui"; return true;
        }
        public bool ApplyBattlePlan(int slot)
        {
            if (!CanManageBattlePlans || !BattlePlanAvailable(slot)) return false;
            BattlePlan current = CurrentBattlePlan();
            if (current == null) return false;
            BattlePlan plan = BattlePlanAt(slot);
            if (SameBattlePlan(plan, current)) return false;
            // Cancel only a prepared action/intent. Already released contacts,
            // paid energy and every combat clock retain their old identity.
            CancelAction();
            save.weapon = plan.weapon; save.weaponStyles[plan.weapon] = plan.style; save.technique = plan.technique;
            save.relics[0] = plan.relicFirst; save.relics[1] = plan.relicSecond;
            revision++; sound = "equip"; return true;
        }
        public bool RemoveBattlePlan(int slot)
        {
            if (!CanManageBattlePlans || StoredBattlePlan(slot) == null) return false;
            save.battlePlans[slot] = null; revision++; sound = "ui"; return true;
        }
        public string ClassTechniqueName(int variant)
        {
            if (variant < 0 || variant > 1) return "";
            return variant == 0
                ? L(new[] { "护盾震波", "扇形箭雨", "星火新星" }[save.profession], new[] { "Shield pulse", "Arrow fan", "Star nova" }[save.profession])
                : L(new[] { "震地反击", "穿林一矢", "寒星领域" }[save.profession], new[] { "Seismic counter", "Woodpiercer", "Frost field" }[save.profession]);
        }
        public string ClassTechniqueDescription(int variant)
        {
            if (variant < 0 || variant > 1) return "";
            if (save.profession == 0)
                return variant == 0
                    ? L("周围3.1格，伤害×1.9；护盾3秒。近身爆发，护盾减少受到的伤害。", "3.1-tile pulse, damage ×1.9; guard for 3s. Close-range burst; guard reduces incoming damage.")
                    : L("周围3.8格，伤害×1.65；护盾4秒，清除4格内敌方弹。范围和控制更强，单次伤害较低。", "3.8-tile pulse, damage ×1.65; guard for 4s; clears hostile shots within 4 tiles. Wider control, less burst.");
            if (save.profession == 1)
                return variant == 0
                    ? L("扇形五箭，每箭伤害×1.25。覆盖多个方向；每箭命中一个目标，墙体挡箭。", "Five arrows in a fan, damage ×1.25 each. Covers several angles; each arrow hits one foe; walls block shots.")
                    : L("一发贯穿箭，伤害×3.4。适合一线敌人；方向集中，墙体仍会挡箭。", "One piercing arrow, damage ×3.4. Strong against enemies in a line; narrow coverage; walls still block it.");
            return variant == 0
                ? L("周围4.5格，伤害×2.3。一次范围爆发；需要让敌人靠近，墙体挡住伤害。", "4.5-tile nova, damage ×2.3. One area burst; foes must be nearby; walls block damage.")
                : L("脚下4格寒星领域，持续4秒，五次伤害各×0.4并短暂冻结。让敌人留在领域内，减少即时爆发。", "Fixed 4-tile frost field for 4s; five pulses at damage ×0.4, each briefly freezing foes. Hold enemies inside; less instant burst.");
        }
    }
}
