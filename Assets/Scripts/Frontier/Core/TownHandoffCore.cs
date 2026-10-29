using System;

namespace Starfall
{
    // A live observation of an original handoff, never payment authority.
    public sealed class TownHandoffQuote
    {
        public readonly string action, reasonZh, reasonEn;
        public readonly int item, required, stock, missing;
        internal readonly int issue;
        public bool available { get { return issue == 0; } }
        internal TownHandoffQuote(string action, int item, int required, int stock, int missing,
            int issue, string reasonZh, string reasonEn)
        {
            this.action = action; this.item = item; this.required = required; this.stock = stock; this.missing = missing;
            this.issue = issue; this.reasonZh = reasonZh; this.reasonEn = reasonEn;
        }
    }

    public sealed partial class GameCore
    {
        public TownHandoffQuote GetTownHandoffQuote(string action)
        {
            int item, required;
            switch (action)
            {
                case "plant": item = 7; required = 1; break;
                case "ally1": item = 2; required = 6; break;
                case "ally2": item = 3; required = 3; break;
                case "ally3": item = 4; required = 5; break;
                case "engine": item = 4; required = 3; break;
                default: return null;
            }
            bool valid = save != null && save.inventory != null && save.inventory.Length == 8;
            int stock = valid ? save.inventory[item] : 0;
            valid = valid && stock >= 0;
            int missing = valid ? Math.Max(0, required - stock) : 0;
            int issue = 0; string zh = "", en = "";
            if (!valid)
            { issue = 1; zh = "无法读取当前材料。"; en = "Current materials cannot be read."; }
            else if (action == "engine" ? Has("gate_3") : action != "plant" && Has("ally_" + action.Substring(4)))
            { issue = 2; zh = "已经完成。"; en = "Already completed."; }
            else if (action == "plant" && save.plantedAt >= 0)
            { issue = 3; zh = "田里已有作物。"; en = "A crop is already planted."; }
            else if (missing > 0)
            { issue = 4; zh = "还需材料 " + missing + "。"; en = "Need " + missing + " more."; }
            return new TownHandoffQuote(action, item, required, stock, missing, issue, zh, en);
        }

        private bool TownHandoffChoiceEnabled(string action)
        {
            TownHandoffQuote quote = GetTownHandoffQuote(action);
            return quote == null || quote.available;
        }

        public string DialogueChoiceLabel(Choice choice)
        {
            if (choice == null) return "";
            if (choice.action == "harvest") return GardenHarvestChoiceLabel(choice);
            TownHandoffQuote quote = GetTownHandoffQuote(choice.action);
            if (quote == null) return SupplyServiceChoiceLabel(choice);
            string zh = choice.action == "plant" ? "种植 · 种子" : choice.action == "engine" ? "修复 · 零件"
                : choice.action == "ally1" ? "交药草" : choice.action == "ally2" ? "交鱼" : "交零件";
            string en = choice.action == "plant" ? "Plant · seeds" : choice.action == "engine" ? "Repair · parts"
                : choice.action == "ally1" ? "Deliver herbs" : choice.action == "ally2" ? "Deliver fish" : "Deliver parts";
            if (quote.issue == 1) return L(zh + " · 暂不可用", en + " · Unavailable");
            if (quote.issue == 2) return L(zh + " · 已完成", en + " · Done");
            if (quote.issue == 3) return L(zh + " · 已种植", en + " · Planted");
            string balance = " " + quote.stock + "/" + quote.required;
            if (quote.missing > 0)
                return L(zh + balance + " · 还需" + quote.missing, en + balance + " · Need " + quote.missing);
            return L(zh + balance, en + balance);
        }
    }
}
