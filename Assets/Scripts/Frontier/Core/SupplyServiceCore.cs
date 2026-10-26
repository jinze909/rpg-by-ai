namespace Starfall
{
    // Single services quote the same pantry limits as batches and save loading.
    public sealed class SupplyServiceQuote
    {
        public readonly string action, reasonZh, reasonEn;
        public readonly int item, quantity, stock, limit, herbs, fish, crystals, coins;
        internal readonly int issue;
        public bool available { get { return reasonZh.Length == 0; } }
        internal SupplyServiceQuote(string action, int item, int quantity, int stock, int limit,
            int herbs, int fish, int crystals, int coins, int issue, string zh, string en)
        {
            this.action = action; this.item = item; this.quantity = quantity; this.stock = stock; this.limit = limit;
            this.herbs = herbs; this.fish = fish; this.crystals = crystals; this.coins = coins;
            this.issue = issue; reasonZh = zh; reasonEn = en;
        }
    }

    public sealed partial class GameCore
    {
        public const int SupplyInventoryLimit = 9999, SupplyTeaLimit = 999;

        public SupplyServiceQuote GetSupplyServiceQuote(string action)
        {
            int item, quantity, herbs = 0, fish = 0, crystals = 0, coins = 0;
            switch (action)
            {
                case "cook": item = 6; quantity = 1; herbs = 2; fish = 1; break;
                case "tea": item = -1; quantity = 1; herbs = 2; crystals = 1; break;
                case "buymeal": item = 6; quantity = 1; break;
                case "buywood": item = 0; quantity = 4; break;
                case "seeds": item = 7; quantity = 3; coins = 5; break;
                default: return null;
            }
            bool valid = save != null && save.inventory != null && save.inventory.Length == 8;
            bool shop = action == "buymeal" || action == "buywood";
            if (shop && (save == null || save.reputation == null || save.reputation.Length < 2)) valid = false;
            if (valid && shop) coins = action == "buymeal" ? ShopStewCost : ShopTimberCost;
            int limit = item < 0 ? SupplyTeaLimit : SupplyInventoryLimit;
            int stock = valid ? item < 0 ? save.tea : save.inventory[item] : 0;
            if (valid && (stock < 0 || stock > limit || save.coins < 0
                || save.inventory[1] < 0 || save.inventory[2] < 0 || save.inventory[3] < 0)) valid = false;
            int issue = 0; string zh = "", en = "";
            if (!valid)
            { issue = 1; zh = "无法读取当前补给。"; en = "Current supply stock cannot be read."; }
            else if (stock > limit - quantity)
            { issue = 2; zh = "存放空间不足。"; en = "Not enough storage space."; }
            else if (save.inventory[2] < herbs || save.inventory[3] < fish || save.inventory[1] < crystals)
            { issue = 3; zh = "材料不足。"; en = "Not enough materials."; }
            else if (save.coins < coins)
            { issue = 4; zh = "硬币还不够。"; en = "Not enough coins."; }
            return new SupplyServiceQuote(action, item, quantity, stock, limit, herbs, fish, crystals, coins, issue, zh, en);
        }

        public string SupplyServiceChoiceLabel(Choice choice)
        {
            if (choice == null) return "";
            SupplyServiceQuote quote = GetSupplyServiceQuote(choice.action);
            if (quote == null) return L(choice.label, choice.en);
            // A malformed live save has no dependable shop price to advertise.
            if (quote.issue == 1)
            {
                string titleZh = quote.action == "cook" ? "制作炖汤" : quote.action == "tea" ? "制作凉茶"
                    : quote.action == "buymeal" ? "买炖汤" : quote.action == "buywood" ? "买木材 ×4" : "买种子 ×3";
                string titleEn = quote.action == "cook" ? "Cook stew" : quote.action == "tea" ? "Brew tea"
                    : quote.action == "buymeal" ? "Buy stew" : quote.action == "buywood" ? "Buy timber ×4" : "Buy seeds ×3";
                return L(titleZh + " · 暂不可用", titleEn + " · Unavailable");
            }
            string zh, en;
            switch (quote.action)
            {
                case "cook": zh = "制作炖汤 · 草2 鱼1"; en = "Cook stew · 2 herbs / 1 fish"; break;
                case "tea": zh = "制作凉茶 · 草2 晶1"; en = "Brew tea · 2 herbs / 1 crystal"; break;
                case "buymeal": zh = "买炖汤 · " + quote.coins + " 硬币"; en = "Buy stew · " + quote.coins + " coins"; break;
                case "buywood": zh = "买木材 ×4 · " + quote.coins + " 硬币"; en = "Buy timber ×4 · " + quote.coins + " coins"; break;
                default: zh = "买种子 ×3 · 5 硬币"; en = "Buy seeds ×3 · 5 coins"; break;
            }
            if (!quote.available)
            {
                zh += quote.issue == 2 ? " · 空间不足" : quote.issue == 3 ? " · 材料不足" : " · 硬币不足";
                en += quote.issue == 2 ? " · No room" : quote.issue == 3 ? " · Need materials" : " · Need coins";
            }
            return L(zh, en);
        }

        private bool SupplyServiceChoiceEnabled(string action)
        {
            SupplyServiceQuote quote = GetSupplyServiceQuote(action);
            return quote == null || quote.available;
        }

        private bool ChooseSupplyService(string action)
        {
            SupplyServiceQuote quote = GetSupplyServiceQuote(action);
            if (quote == null) return false;
            if (!quote.available) { Notify(quote.reasonZh, quote.reasonEn); return true; }
            if (!Spend(0, quote.crystals, quote.herbs, quote.fish, 0, quote.coins)) return true;
            if (quote.item < 0)
            {
                save.tea += quote.quantity; revision++;
                Notify("星薄荷凉茶 ×1", "Star-mint tea ×1");
            }
            else
            {
                save.inventory[quote.item] += quote.quantity;
                if (action == "cook") sound = "reward";
                if (quote.item == 6) Notify("暖潮炖汤 ×1", "Tide stew ×1");
                else if (quote.item == 0) Notify("木材 ×4", "Timber ×4");
                else Notify("种子 ×3", "Seeds ×3");
            }
            return true;
        }
    }
}
