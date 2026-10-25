using System;

namespace Starfall
{
    public enum RelicTrailAction { None, Quest, Projects, AbyssMap, AbyssRun }

    public sealed class RelicTrailStep
    {
        public readonly string text;
        public readonly bool done;
        internal RelicTrailStep(string text, bool done) { this.text = text; this.done = done; }
    }

    // A fresh, read-only view of existing progress. Viewing a source never grants it.
    public sealed class RelicTrail
    {
        public readonly int id;
        public readonly bool owned, sourceComplete;
        public readonly string source, next, questId;
        public readonly RelicTrailAction action;
        public readonly RelicTrailStep[] steps;
        internal RelicTrail(int id, bool owned, bool complete, string source, string next,
            string questId, RelicTrailAction action, RelicTrailStep[] steps)
        {
            this.id = id; this.owned = owned; sourceComplete = complete;
            this.source = source; this.next = next; this.questId = questId;
            this.action = action; this.steps = steps;
        }
    }

    public sealed partial class GameCore
    {
        private string RelicRecruitmentQuest { get { return save.stage < 1 ? "main" : "shore"; } }

        public RelicTrail GetRelicTrail(int id)
        {
            if (save == null || id < 0 || id >= RelicNames.Length) return null;
            bool owned = Has("relic_" + id), complete;
            string source, next = "", quest = "";
            RelicTrailAction action = RelicTrailAction.None;
            RelicTrailStep[] steps;
            if (id == 0)
            {
                complete = Has("harbor_report");
                source = L("雾港 · 米拉的港口调查\n解决灯塔危机后，向米拉提交水位记录。",
                    "Mistport · Mira's tide report\nResolve the lighthouse crisis, then file the tide log with Mira.");
                steps = new[] {
                    new RelicTrailStep(L("完成港口方案", "Complete the harbor plan"), Has("gate_5")),
                    new RelicTrailStep(L("击败潮壳海兽", "Defeat the tide beast"), Has("boss_5")),
                    new RelicTrailStep(L("向米拉提交调查", "File the report with Mira"), complete) };
                quest = RelicRecruitmentQuest;
            }
            else if (id == 1)
            {
                int tier = save.projects[0]; complete = tier == 2;
                source = L("余烬镇 · 水渠与药田\n把水渠升级到二级时获赠；工程计划可查看实际成本。",
                    "Emberfall · Canals & gardens\nAwarded when the canal reaches tier 2; project plans show current costs.");
                steps = new[] {
                    new RelicTrailStep(L("建设一级水渠", "Build the tier 1 canal"), tier >= 1),
                    new RelicTrailStep(L("升级为二级水渠", "Upgrade the canal to tier 2"), complete) };
                next = L("查看水渠计划；回到余烬镇后才能建设。", "Review the canal plan; building requires returning to Emberfall.");
                action = RelicTrailAction.Projects;
            }
            else if (id == 2)
            {
                complete = Has("desert_report");
                source = L("玻璃沙海 · 伊沃的调度调查\n修复光路并战胜守护者，再与伊沃决定水源分配。",
                    "Glass Desert · Ivo's allocation report\nRestore the light channel and defeat its guardian, then settle water allocation with Ivo.");
                steps = new[] {
                    new RelicTrailStep(L("修复沙海光路", "Restore the desert light channel"), Has("mirrors_lit")),
                    new RelicTrailStep(L("击败沙海守护者", "Defeat the desert guardian"), Has("boss_6")),
                    new RelicTrailStep(L("向伊沃提交调查", "File the report with Ivo"), complete) };
                quest = RelicRecruitmentQuest;
            }
            else if (id == 3)
            {
                complete = Has("mountain_report");
                source = L("霜钟高原 · 森的名字调查\n解开钟阵并战胜守护者，再与森交付高原记录。",
                    "Frostchime · Sen's record\nResolve the bells and defeat the guardian, then deliver the highland record with Sen.");
                steps = new[] {
                    new RelicTrailStep(L("解开高原钟阵", "Resolve the highland bells"), Has("gate_7")),
                    new RelicTrailStep(L("击败高原守护者", "Defeat the highland guardian"), Has("boss_7")),
                    new RelicTrailStep(L("向森提交调查", "File the report with Sen"), complete) };
                quest = RelicRecruitmentQuest;
            }
            else if (id == 4)
            {
                complete = Has("personal_0") && Has("personal_1") && Has("personal_2");
                source = L("同行者 · 三段个人故事\n完成米拉、伊沃与森的个人交付；最后一段完成时获赠。",
                    "Companions · Three personal stories\nComplete Mira's, Ivo's and Sen's personal handoffs; awarded by the last one.");
                steps = new RelicTrailStep[3];
                for (int companion = 0; companion < 3; companion++)
                {
                    QuestGuide personal = GetQuest("personal" + companion);
                    steps[companion] = new RelicTrailStep(personal.title, personal.complete);
                    if (quest.Length == 0 && !personal.complete && Has("companion_" + companion) && !personal.locked)
                        quest = personal.id;
                }
                if (quest.Length == 0) quest = RelicRecruitmentQuest;
            }
            else
            {
                complete = save.bestFloor >= 3;
                source = L("地脉深渊 · 结算第三层或更深楼层\n清层后到东侧地脉门结算返回，或选择祝福结算并深入。",
                    "Root Abyss · Bank floor 3 or deeper\nAfter clearing, use the east root gate to bank and extract, or choose a boon to bank and descend.");
                steps = new[] {
                    new RelicTrailStep(L("达成远岸协定，开放深渊", "Settle the shore covenant to unlock the abyss"), CanEnter(8)),
                    new RelicTrailStep(L("第三层结算进度 ", "Floor 3 banking progress ") + Math.Min(3, save.bestFloor) + "/3", complete) };
                if (!CanEnter(8)) quest = RelicRecruitmentQuest;
                else if (!InAbyss)
                {
                    action = RelicTrailAction.AbyssMap;
                    next = L("在余烬镇东侧的地脉裂口准备远征。", "Prepare an expedition at the eastern root fissure in Emberfall.");
                }
                else
                {
                    action = RelicTrailAction.AbyssRun;
                    next = enemies.Count > 0
                        ? L("先清理当前第", "Clear the current floor ") + save.abyssFloor + L("层，再到东侧地脉门结算。", ", then bank at the east root gate.")
                        : RootEchoActive
                            ? L("先结束回声挑战，再到东侧地脉门结算。", "Finish the echo first, then bank at the east root gate.")
                            : save.abyssFloor < 6
                                ? L("到东侧地脉门结算返回，或选择祝福结算并深入。", "At the east gate, bank and extract or choose a boon to bank and descend.")
                                : L("到东侧地脉门完成远征，结算返回。", "Complete the expedition and bank at the east root gate.");
                }
            }
            if (owned || complete)
            {
                action = RelicTrailAction.None; quest = "";
                next = owned ? L("已获得；可在遗物栏选择装备。", "Owned; select it in Relics to equip.")
                    : L("来源条件已完成；当前记录未持有，不会重复发放。", "Source complete; this record has no ownership flag. Rewards are not replayed.");
            }
            else if (quest.Length > 0)
            {
                QuestGuide guide = GetQuest(quest);
                next = guide.next;
                if (!guide.locked && !guide.complete) action = RelicTrailAction.Quest;
                else { action = RelicTrailAction.None; quest = ""; }
            }
            return new RelicTrail(id, owned, complete, source, next, quest, action, steps);
        }

        public bool TrackRelicTrail(int id)
        {
            if (mode != "relictrail" || Dying || practicing || SafeTrainingActive) return false;
            RelicTrail trail = GetRelicTrail(id);
            if (trail == null || trail.owned || trail.sourceComplete || trail.action != RelicTrailAction.Quest) return false;
            QuestGuide guide = GetQuest(trail.questId);
            if (guide.locked || guide.complete) return false;
            TrackQuest(guide.id);
            return true;
        }
    }
}
