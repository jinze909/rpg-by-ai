using System;

namespace Starfall
{
    public sealed partial class GameCore
    {
        private bool AbyssDescentReady
        {
            get { return !Dying && Finite(save.health) && save.health > 0 && !SafeTrainingActive
                && (mode == "world" || mode == "dialog") && !RootEchoActive && InAbyss && save.abyssFloor < 6
                && enemies.Count == 0 && Has("abyss_clear_" + save.abyssFloor); }
        }
        private bool AbyssBoonsFull
        { get { return save.boons[0] == 4 && save.boons[1] == 4 && save.boons[2] == 4; } }

        // Read by the dialog before it closes, and checked again by direct APIs.
        // Unrelated choices retain their own existing availability rules.
        public bool AbyssBoonChoiceEnabled(string action)
        {
            if (action == "abyssRetain") return AbyssDescentReady && AbyssBoonsFull;
            int id = action == "boon0" ? 0 : action == "boon1" ? 1 : action == "boon2" ? 2 : -1;
            return id < 0 || AbyssDescentReady && save.boons[id] < 4;
        }

        public string AbyssBoonSummary(int id, bool english)
        {
            if (id < 0 || id > 2) return "";
            int count = InAbyss ? save.boons[id] : 0;
            return AbyssBoonName(id, english) + " " + count + "/4 · " + AbyssBoonEffect(id, count, english);
        }
        private static string AbyssBoonName(int id, bool english)
        { return id == 0 ? (english ? "Edge" : "利刃") : id == 1 ? (english ? "Spring" : "泉涌") : (english ? "Stone" : "磐石"); }
        private static string AbyssBoonEffect(int id, int count, bool english)
        {
            if (id == 0) return (english ? "damage +" : "攻击+") + count * 15 + "%";
            if (id == 1) return (english ? "energy regen +" : "耐力回复+") + count * 4 + (english ? "/s" : "/秒");
            return (english ? "damage taken -" : "受到伤害-") + count * 12 + "%";
        }
        private string AbyssBoonChoiceLabel(int id, bool english)
        {
            int count = save.boons[id]; string name = AbyssBoonName(id, english);
            if (count >= 4) return name + (english ? " 4/4 full" : " 4/4 已满");
            return name + " " + count + "→" + (count + 1) + "/4 · "
                + AbyssBoonEffect(id, 1, english) + (english ? " · descend" : " · 深入");
        }

        public string AbyssBankingSummary(bool english)
        {
            if (!InAbyss) return "";
            int covenant = save.frontierEnding == 3 ? 2 : 0;
            int firstClear = save.abyssFloor == 6 && !Has("abyss_complete") ? 12 : 0;
            int shards = save.runShards + covenant + firstClear;
            string text = english
                ? "Bank now: " + save.runCoins + " coins, " + shards + " shards, " + save.runXp + " XP."
                : "现在结算：硬币" + save.runCoins + "、碎片" + shards + "、经验" + save.runXp + "。";
            if (covenant > 0) text += english ? " Includes covenant +2 shards." : "协定增加碎片2。";
            if (firstClear > 0) text += english ? " Includes first-clear +12 shards; also gain 2 talent points." : "首次完成增加碎片12；另得技能点2。";
            if (save.abyssFloor >= 3 && !Has("relic_5")) text += english ? " Unlock Rift Stone." : "解锁裂隙石。";
            return text;
        }

        public string AbyssNextFloorHint(bool english)
        {
            if (!InAbyss) return "";
            if (save.abyssFloor >= 6) return english ? "Final floor: bank and return; there is no further descent." : "本层已是最后一层：结算返回，不能继续深入。";
            int next = save.abyssFloor + 1;
            string text = english ? "Next: floor " + next + "/6." : "下一层：" + next + "/6。";
            if (next == 2 || next == 4) text += english ? " Optional northwest root: supplies or echo trial." : "西北根系可选补给或回声挑战。";
            if (next >= 3) text += english ? " Rift marks warn before striking until the floor is clear." : "清层前会有落印，命中前有光圈预告。";
            if (next == 6) text += english ? " The Root Reaper waits here." : "地脉收割者在此等候。";
            return text;
        }

        public string AbyssDecisionText(bool english)
        {
            if (!InAbyss) return "";
            string pending = english
                ? "Unbanked floor loot: " + save.runCoins + " coins, " + save.runShards + " shards, " + save.runXp + " XP."
                : "本层未结算：硬币" + save.runCoins + "、碎片" + save.runShards + "、经验" + save.runXp + "。";
            string text = pending + "\n" + AbyssBankingSummary(english) + "\n\n"
                + AbyssBoonSummary(0, english) + "\n" + AbyssBoonSummary(1, english) + "\n" + AbyssBoonSummary(2, english);
            text += english
                ? "\nBoons add up to 4 stacks and last only this run. Full boons cannot be selected."
                : "\n祝福每次远征有效，同类相加，最多4层；已满的祝福不能选择。";
            text += "\n\n" + AbyssNextFloorHint(english);
            if (save.abyssFloor < 6)
                text += english ? "\nDescending banks this loot, heals 50% maximum health and refills energy."
                    : "\n深入会结算本层，回复最大生命的50%，耐力回满。";
            text += english ? "\nBank and extract to keep all floor loot. Earlier banked rewards stay safe; boons end on return."
                : "\n结算返回可带走本层全部收益，已结算物品不会丢失；返回后祝福结束。";
            return text;
        }

        private void RetainAbyssBoons()
        {
            if (!AbyssBoonChoiceEnabled("abyssRetain")) return;
            DescendAbyss(-1);
        }
    }
}
