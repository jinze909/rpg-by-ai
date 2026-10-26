using System;
using System.Collections.Generic;
using System.Globalization;

namespace Starfall
{
    // An immutable pantry quote. Every payment requests a new snapshot.
    public sealed class SupplyBatchQuote
    {
        public readonly int recipe, quantity, herbs, fish, crystals, maxQuantity;
        public readonly string reasonZh, reasonEn;
        public bool available { get { return reasonZh.Length == 0; } }
        internal SupplyBatchQuote(int recipe, int quantity, int herbs, int fish, int crystals, int maximum, string zh, string en)
        {
            this.recipe = recipe; this.quantity = quantity; this.herbs = herbs;
            this.fish = fish; this.crystals = crystals; maxQuantity = maximum; reasonZh = zh; reasonEn = en;
        }
    }

    public sealed partial class GameCore
    {
        public int MaximumSupplyBatch(int recipe)
        {
            if (recipe != 0 && recipe != 1) return 0;
            int stock = recipe == 0 ? save.inventory[6] : save.tea, limit = recipe == 0 ? SupplyInventoryLimit : SupplyTeaLimit;
            int capacity = Math.Max(0, limit - Math.Max(0, stock));
            return Math.Min(capacity, Math.Min(Math.Max(0, save.inventory[2]) / 2,
                Math.Max(0, recipe == 0 ? save.inventory[3] : save.inventory[1])));
        }
        public SupplyBatchQuote GetSupplyBatchQuote(int recipe, int quantity)
        {
            bool valid = (recipe == 0 || recipe == 1) && quantity > 0 && quantity <= SupplyInventoryLimit;
            int herbs = valid ? quantity * 2 : 0, fish = valid && recipe == 0 ? quantity : 0, crystals = valid && recipe == 1 ? quantity : 0;
            string zh = "", en = ""; WorldObject host = Nearest();
            if (!valid) { zh = "请选择有效的补给与制作数量。"; en = "Choose a valid supply and quantity."; }
            else if (Dying || practicing || InAbyss || (mode != "world" && mode != "dialog"))
            { zh = "现在不能备制补给。"; en = "Cannot prepare supplies right now."; }
            else if (host == null || host.kind != "cook" && (recipe != 1 || host.kind != "campfire"))
            { zh = recipe == 0 ? "在阿茴处制作炖汤。" : "在阿茴或营地处制作凉茶。"; en = recipe == 0 ? "Visit Ari to cook stew." : "Visit Ari or a field camp to brew tea."; }
            else if ((recipe == 0 ? save.inventory[6] : save.tea) > (recipe == 0 ? SupplyInventoryLimit : SupplyTeaLimit) - quantity)
            { zh = "补给存放空间不足。"; en = "Not enough pantry capacity."; }
            else
            {
                var missingZh = new List<string>(); var missingEn = new List<string>();
                if (save.inventory[2] < herbs) { missingZh.Add("药草" + (herbs - save.inventory[2])); missingEn.Add((herbs - save.inventory[2]) + " herbs"); }
                if (save.inventory[3] < fish) { missingZh.Add("鱼" + (fish - save.inventory[3])); missingEn.Add((fish - save.inventory[3]) + " fish"); }
                if (save.inventory[1] < crystals) { missingZh.Add("星晶" + (crystals - save.inventory[1])); missingEn.Add((crystals - save.inventory[1]) + " crystals"); }
                if (missingZh.Count > 0) { zh = "还需：" + string.Join(" / ", missingZh.ToArray()); en = "Need: " + string.Join(" / ", missingEn.ToArray()); }
            }
            return new SupplyBatchQuote(recipe, quantity, herbs, fish, crystals, MaximumSupplyBatch(recipe), zh, en);
        }
        public bool CraftSupplyBatch(int recipe, int quantity)
        {
            if (mode != "world") return false;
            SupplyBatchQuote quote = GetSupplyBatchQuote(recipe, quantity);
            if (!quote.available) { Notify(quote.reasonZh, quote.reasonEn); return false; }
            if (!Spend(0, quote.crystals, quote.herbs, quote.fish, 0)) return false;
            if (recipe == 0) save.inventory[6] += quantity; else save.tea += quantity;
            revision++; sound = "reward";
            Notify((recipe == 0 ? "暖潮炖汤" : "星薄荷凉茶") + " ×" + quantity, (recipe == 0 ? "Tide stew" : "Star-mint tea") + " ×" + quantity);
            return true;
        }
        private Choice[] SupplyPreparationChoices(Choice[] original)
        {
            var choices = new List<Choice>(original);
            choices.Add(C("批量备制补给", "Prepare pantry supplies", "supply_prepare")); return choices.ToArray();
        }
        private Choice SupplyBatchChoice(int recipe, int quantity, bool maximum)
        {
            SupplyBatchQuote quote = GetSupplyBatchQuote(recipe, quantity);
            string prefixZh = maximum ? "最多 · " : "", prefixEn = maximum ? "Max · " : "";
            string zh = prefixZh + (recipe == 0 ? "炖汤" : "凉茶") + " ×" + quantity + " · 草" + quote.herbs + (recipe == 0 ? " 鱼" + quote.fish : " 晶" + quote.crystals);
            string en = prefixEn + (recipe == 0 ? "Stew" : "Tea") + " ×" + quantity + " · herbs" + quote.herbs + (recipe == 0 ? " fish" + quote.fish : " crystal" + quote.crystals);
            return C(zh, en, "supply_batch_" + recipe + "_" + quantity);
        }
        public bool ShowSupplyPreparation()
        {
            if (mode != "world" || Dying || practicing || InAbyss) return false;
            WorldObject host = Nearest(); if (host == null || host.kind != "cook" && host.kind != "campfire") return false;
            SupplyBatchQuote stew = GetSupplyBatchQuote(0, 5), tea = GetSupplyBatchQuote(1, 5);
            string zh = "材料：药草" + save.inventory[2] + " / 鱼" + save.inventory[3] + " / 星晶" + save.inventory[1]
                + "\n现有补给：炖汤" + save.inventory[6] + " / 凉茶" + save.tea
                + "\n炖汤×5：" + (stew.available ? "材料充足" : stew.reasonZh)
                + "\n凉茶×5：" + (tea.available ? "材料充足" : tea.reasonZh)
                + "\n选择总价；最多按当前材料与存放空间计算。\n只备好补给，不会自动使用或治疗。";
            string en = "Stock: herbs " + save.inventory[2] + " / fish " + save.inventory[3] + " / crystals " + save.inventory[1]
                + "\nPantry: stew " + save.inventory[6] + " / tea " + save.tea
                + "\nStew x5: " + (stew.available ? "Materials ready" : stew.reasonEn)
                + "\nTea x5: " + (tea.available ? "Materials ready" : tea.reasonEn)
                + "\nPrices are totals; Max uses current ingredients and storage.\nPreparation adds stock; it never consumes a meal or heals.";
            Dialog("远行补给", "Pantry preparation", zh, en,
                SupplyBatchChoice(0, 1, false), SupplyBatchChoice(0, 5, false), SupplyBatchChoice(0, MaximumSupplyBatch(0), true),
                SupplyBatchChoice(1, 1, false), SupplyBatchChoice(1, 5, false), SupplyBatchChoice(1, MaximumSupplyBatch(1), true),
                C("准备好了，离开", "Done preparing", "close"));
            return true;
        }
        private static bool ParseSupplyBatch(string action, out int recipe, out int quantity)
        {
            recipe = quantity = -1;
            if (action == null || !action.StartsWith("supply_batch_", StringComparison.Ordinal)) return false;
            string[] fields = action.Substring(13).Split('_');
            return fields.Length == 2 && int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out recipe)
                && int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out quantity);
        }
        public bool DialogueChoiceEnabled(string action)
        {
            if (!AbyssBoonChoiceEnabled(action)) return false;
            if (!ForgeChoiceEnabled(action)) return false;
            if (!SupplyServiceChoiceEnabled(action)) return false;
            if (action == null || !action.StartsWith("supply_batch_", StringComparison.Ordinal)) return true;
            int recipe, quantity;
            return ParseSupplyBatch(action, out recipe, out quantity) && GetSupplyBatchQuote(recipe, quantity).available;
        }
        private bool ChooseSupplyPreparation(string action)
        {
            if (action == "supply_prepare") { ShowSupplyPreparation(); return true; }
            if (action == null || !action.StartsWith("supply_batch_", StringComparison.Ordinal)) return false;
            int recipe, quantity;
            if (ParseSupplyBatch(action, out recipe, out quantity))
            { CraftSupplyBatch(recipe, quantity); ShowSupplyPreparation(); }
            return true;
        }
    }
}
