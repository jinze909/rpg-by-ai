using System;
using System.Collections.Generic;
using System.Globalization;

namespace Starfall
{
    // Read-only workshop information; every purchase obtains a fresh quote.
    public sealed class ForgeQuote
    {
        public readonly int recipe, wood, crystals, parts, levelBefore, levelAfter, damageBonusBefore, damageBonusAfter;
        public readonly bool owned, capped;
        public readonly float healthBefore, healthAfter, maxHealthBefore, maxHealthAfter;
        public readonly string reasonZh, reasonEn;
        public bool available { get { return reasonZh.Length == 0; } }
        internal ForgeQuote(int recipe, SaveData save, int wood, int crystals, int parts, bool owned, bool capped, string zh, string en)
        {
            this.recipe = recipe; this.wood = wood; this.crystals = crystals; this.parts = parts;
            this.owned = owned; this.capped = capped; reasonZh = zh; reasonEn = en;
            levelBefore = save.upgrades; levelAfter = recipe == 0 && !capped ? Math.Min(5, save.upgrades + 1) : save.upgrades;
            damageBonusBefore = levelBefore * 4; damageBonusAfter = levelAfter * 4;
            healthBefore = save.health; maxHealthBefore = save.maxHealth;
            maxHealthAfter = recipe == 0 && !capped ? save.maxHealth + 20 : save.maxHealth;
            healthAfter = recipe == 0 && !capped ? maxHealthAfter : save.health;
        }
    }

    public sealed partial class GameCore
    {
        public ForgeQuote GetForgeQuote(int recipe)
        {
            bool valid = recipe >= 0 && recipe <= 2;
            bool owned = valid && recipe > 0 && Has("weapon_" + recipe);
            bool capped = recipe == 0 && save.upgrades >= 5;
            int wood = !valid || owned || capped ? 0 : recipe == 0 ? 3 : recipe == 1 ? 4 : 0;
            int crystals = !valid || owned || capped ? 0 : recipe == 0 ? 2 : recipe == 2 ? 4 : 0;
            int parts = !valid || owned || capped ? 0 : recipe == 2 ? 3 : 2;
            string zh = "", en = "";
            if (!valid || save.upgrades < 0 || save.upgrades > 5 || save.inventory == null || save.inventory.Length < 5
                || !Finite(save.health) || !Finite(save.maxHealth) || save.maxHealth <= 0)
            { zh = "无法读取这份工坊配方。"; en = "This workshop recipe cannot be read."; }
            else if (capped)
            { zh = "装备已达五级上限。"; en = "Equipment is already at tier 5."; }
            else
            {
                var missingZh = new List<string>(3); var missingEn = new List<string>(3);
                if (save.inventory[0] < wood) { missingZh.Add("木材" + (wood - save.inventory[0])); missingEn.Add((wood - save.inventory[0]) + " timber"); }
                if (save.inventory[1] < crystals) { missingZh.Add("星晶" + (crystals - save.inventory[1])); missingEn.Add((crystals - save.inventory[1]) + " crystals"); }
                if (save.inventory[4] < parts) { missingZh.Add("零件" + (parts - save.inventory[4])); missingEn.Add((parts - save.inventory[4]) + " parts"); }
                if (missingZh.Count > 0) { zh = "还需：" + string.Join(" / ", missingZh.ToArray()); en = "Need: " + string.Join(" / ", missingEn.ToArray()); }
            }
            return new ForgeQuote(recipe, save, wood, crystals, parts, owned, capped, zh, en);
        }
        private Choice ForgeChoice(int recipe)
        {
            ForgeQuote quote = GetForgeQuote(recipe);
            if (recipe == 0) return C(quote.capped ? "强化 5/5 · 已满级" : "强化 +" + quote.levelAfter + " · 木3 晶2 零件2",
                quote.capped ? "Upgrade 5/5 · Maximum tier" : "Upgrade +" + quote.levelAfter + " · 3 timber / 2 crystal / 2 parts", "upgrade");
            string zh = recipe == 1 ? "猎弓" : "星火杖", en = recipe == 1 ? "bow" : "staff";
            return C(quote.owned ? "装备" + zh + " · 免费" : "制作" + zh + (recipe == 1 ? " · 木4 零件2" : " · 晶4 零件3"),
                quote.owned ? "Equip " + en + " · Free" : "Craft " + en + (recipe == 1 ? " · 4 timber / 2 parts" : " · 4 crystal / 3 parts"), recipe == 1 ? "bow" : "staff");
        }
        private bool ForgeChoiceEnabled(string action)
        { return action == "upgrade" ? GetForgeQuote(0).available : action == "bow" ? GetForgeQuote(1).available : action == "staff" ? GetForgeQuote(2).available : true; }
        private string ForgeState(ForgeQuote quote, bool english)
        { return !quote.available ? english ? quote.reasonEn : quote.reasonZh : quote.owned ? english ? "Owned; equip for free" : "已拥有，免费装备" : english ? "Materials ready" : "材料充足"; }
        private string ForgeRecipeLine(ForgeQuote quote, bool english)
        {
            string title = quote.recipe == 0 ? english ? "Upgrade" : "强化" : quote.recipe == 1 ? english ? "Bow" : "猎弓" : english ? "Staff" : "星火杖";
            string price = quote.owned || quote.capped ? "" : english
                ? " · " + quote.wood + " timber / " + quote.crystals + " crystals / " + quote.parts + " parts"
                : " · 木材" + quote.wood + " / 星晶" + quote.crystals + " / 零件" + quote.parts;
            return title + price + ": " + ForgeState(quote, english);
        }
        private static string ForgeHealth(float value)
        { return value.ToString("0.##", CultureInfo.InvariantCulture); }
        public bool ShowForgeQuotes()
        {
            if (Dying || practicing || InAbyss || mode != "world" && mode != "dialog") return false;
            WorldObject host = Nearest(); if (host == null || host.kind != "forge") return false;
            ForgeQuote upgrade = GetForgeQuote(0), bow = GetForgeQuote(1), staff = GetForgeQuote(2);
            string zh = "现有材料：木材" + save.inventory[0] + " / 星晶" + save.inventory[1] + " / 零件" + save.inventory[4]
                + "\n" + ForgeRecipeLine(upgrade, false)
                + "\n" + ForgeRecipeLine(bow, false)
                + "\n" + ForgeRecipeLine(staff, false)
                + "\n\n强化等级 " + upgrade.levelBefore + " → " + upgrade.levelAfter + "/5 · 装备攻击加成 +" + upgrade.damageBonusBefore + " → +" + upgrade.damageBonusAfter
                + "\n最大生命 " + ForgeHealth(upgrade.maxHealthBefore) + " → " + ForgeHealth(upgrade.maxHealthAfter)
                + " · 当前生命 " + ForgeHealth(upgrade.healthBefore) + " → " + ForgeHealth(upgrade.healthAfter)
                + "\n" + (upgrade.capped ? "强化已满级，不会支付材料或治疗。" : "强化会回满生命；攻击加成另受打法与状态影响。")
                + "查看不消耗材料，也不会治疗。";
            string en = "Stock: timber " + save.inventory[0] + " / crystals " + save.inventory[1] + " / parts " + save.inventory[4]
                + "\n" + ForgeRecipeLine(upgrade, true)
                + "\n" + ForgeRecipeLine(bow, true)
                + "\n" + ForgeRecipeLine(staff, true)
                + "\n\nUpgrade tier " + upgrade.levelBefore + " → " + upgrade.levelAfter + "/5 · Equipment damage bonus +" + upgrade.damageBonusBefore + " → +" + upgrade.damageBonusAfter
                + "\nMax health " + ForgeHealth(upgrade.maxHealthBefore) + " → " + ForgeHealth(upgrade.maxHealthAfter)
                + " · Current health " + ForgeHealth(upgrade.healthBefore) + " → " + ForgeHealth(upgrade.healthAfter)
                + "\n" + (upgrade.capped ? "Maximum upgrade tier; no payment or healing. " : "Upgrading refills health; styles and effects modify damage. ")
                + "Reading spends nothing and never heals.";
            Dialog(host.name + " · 工坊报价", host.en + " · Workshop quotes", zh, en,
                ForgeChoice(0), ForgeChoice(1), ForgeChoice(2), C("返回诺尔", "Back to Noll", "forge_back"));
            return true;
        }
        private bool ChooseForgeQuotes(string action)
        {
            if (action == "forge_quotes") { ShowForgeQuotes(); return true; }
            if (action != "forge_back") return false;
            WorldObject host = Nearest(); if (host != null && host.kind == "forge") Talk(host);
            return true;
        }
    }
}
