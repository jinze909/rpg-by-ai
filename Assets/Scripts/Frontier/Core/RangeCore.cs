using System;

namespace Starfall
{
    public sealed partial class GameCore
    {
        private bool rangeSession;
        public bool RangeActive { get { return practicing && rangeSession; } }
        public bool SafeTrainingActive { get { return SparringActive || RangeActive; } }
        public bool RangeReady { get { return SparringReady; } }
        public int RangeLayout { get; private set; }
        public bool RangeStarted { get; private set; }
        public bool RangeFinished { get; private set; }
        public float RangeTime { get; private set; }
        public float RangeDamage { get; private set; }
        public int RangeContacts { get; private set; }
        public int RangeBreaks { get; private set; }
        public int RangeFlanks { get; private set; }
        private bool RangeActionBlocked { get { return RangeActive && RangeFinished; } }
        private static readonly string[] RangeTitles = { "一线目标", "聚集目标", "正面重甲" };
        private static readonly string[] RangeTitlesEn = { "Targets in a line", "Clustered targets", "Frontal armor" };
        private static readonly string[] RangeHints = { "试试贯穿与射线，墙体仍会挡住攻击。", "试试范围与散射，观察实际命中。", "正面护甲减少伤害；绕到背后，或用战技破势。" };
        private static readonly string[] RangeHintsEn = { "Compare piercing and straight shots. Walls still block attacks.", "Compare area attacks and spread. Only real contacts count.", "Front armor reduces damage. Flank it or try a technique and break." };
        public string RangeTitle { get { return L(RangeTitles[RangeLayout], RangeTitlesEn[RangeLayout]); } }
        public string RangeHint { get { return L(RangeHints[RangeLayout], RangeHintsEn[RangeLayout]); } }
        public bool IsRangeEnemyId(int id) { return id >= 79000 && id <= 79002; }
        public bool IsRangeEnemy(Enemy enemy) { return enemy != null && !enemy.boss && IsRangeEnemyId(enemy.id); }

        public void ShowRange()
        {
            if (mode != "world" || Dying || !Finite(save.health) || save.health <= 0 || practicing || !NearSparringMentor()) return;
            if (!RangeReady)
            {
                Dialog("艾妲 · 试装训练场", "Ada · Loadout range", "先完成基础练习，或找回第一枚星核，再来比较装备与打法。原有练习和战术训练仍在。", "Finish basic practice or recover the first starcore, then compare your equipment and styles here. The original lessons and sparring remain available.", C("先做准备", "Prepare first", "close")); return;
            }
            Dialog("艾妲 · 试装训练场", "Ada · Loadout range",
                "选择不会移动的目标，试用自己的武器、打法、战技与遗物；背包里的装备方案也可切换。第一下有效出手开始计时，目标清场后停止。伤害记录以目标实际损失的生命为准，不计超出生命的伤害。\n\n可随时重置、换目标或离开；没有报酬，也不增加实战记录、熟练度或委托。世界时间暂停，同伴不出战。离开或读档会恢复开始时的位置、生命与耐力；自行调整的装备与成长保留。",
                "Choose stationary targets and try your own weapons, styles, techniques and relics. You can also switch saved battle plans in Inventory. Your first accepted attack starts the clock; clearing the targets stops it. Damage counts actual health lost, excluding overkill.\n\nReset, change targets or leave anytime. No rewards, field notes, mastery or bounty progress. World time pauses and companions sit out. Leaving or loading restores your starting position, health and energy; deliberate equipment and growth changes stay.",
                C("一线目标", "Targets in a line", "range_start_0"), C("聚集目标", "Clustered targets", "range_start_1"), C("正面重甲", "Frontal armor", "range_start_2"), C("稍后", "Later", "close"));
        }
        private bool ChooseRange(string action)
        {
            if (action == "range_menu") { ShowRange(); return true; }
            if (action == "range_start_0" || action == "range_start_1" || action == "range_start_2") { StartRange(action[action.Length - 1] - '0'); return true; }
            return false;
        }
        public bool StartRange(int layout)
        {
            if (layout < 0 || layout > 2 || mode != "world" || Dying || !Finite(save.health) || save.health <= 0 || practicing || !RangeReady || !NearSparringMentor()) return false;
            save.sparringReturn = new SparringReturnRecord { x = save.x, y = save.y, health = save.health, energy = save.energy, heat = save.heat, cold = save.cold };
            BeginRangeLayout(layout); return true;
        }
        private void BeginRangeLayout(int layout)
        {
            // Sever every prepared action, projectile and persistent field before
            // creating new actors, even when their reserved IDs are reused.
            ResetTransient(); practicing = rangeSession = true; RangeLayout = layout;
            enemies.Clear(); save.x = 12; save.y = 14; save.energy = 100; invulnerable = 0;
            if (layout == 0)
                for (int i = 0; i < 3; i++) AddRangeEnemy(i, 0, new Point(16 + i * 2, 14));
            else if (layout == 1)
            { AddRangeEnemy(0, 0, new Point(16, 13)); AddRangeEnemy(1, 0, new Point(17, 14)); AddRangeEnemy(2, 0, new Point(16, 15)); }
            else AddRangeEnemy(0, 2, new Point(16, 14));
            revision++; Notify("试装训练：首次出手开始计时，可随时重置或返回。", "Loadout range: your first attack starts the clock. Reset or return anytime.");
        }
        private void AddRangeEnemy(int index, int kind, Point position)
        { Enemy enemy = CreatePlacedEnemy(79000 + index, kind, position, 300); enemy.facing = new Point(-1, 0); enemy.timer = 9999; enemies.Add(enemy); }
        public bool ResetRange()
        { if (!RangeActive || mode != "world" || Dying) return false; int layout = RangeLayout; BeginRangeLayout(layout); return true; }
        public bool ChangeRangeLayout(int layout)
        { if (!RangeActive || mode != "world" || Dying || layout < 0 || layout > 2 || layout == RangeLayout) return false; BeginRangeLayout(layout); return true; }
        private void ResetRangeTransient()
        { rangeSession = false; RangeLayout = 0; RangeStarted = RangeFinished = false; RangeTime = RangeDamage = 0; RangeContacts = RangeBreaks = RangeFlanks = 0; }
        private void BeginRangeAttempt()
        { if (RangeActive && !RangeFinished) RangeStarted = true; }
        private void OnRangeContact(Enemy enemy, ImpactProfile impact, float healthBefore, bool broke, bool flank)
        {
            if (!RangeActive || RangeFinished || !IsRangeEnemy(enemy) || impact == null || !impact.playerOwned || !Finite(healthBefore) || healthBefore <= 0 || !Finite(enemy.hp)) return;
            float lost = Math.Min(healthBefore, Math.Max(0, healthBefore - enemy.hp)); if (lost <= 0) return;
            RangeDamage += lost; RangeContacts++; if (broke) RangeBreaks++; if (flank) RangeFlanks++;
        }
        private void TickRange(float dt)
        {
            if (!RangeActive || mode != "world" || Dying || RangeFinished) return;
            if (RangeStarted) RangeTime += dt;
            foreach (Enemy enemy in enemies) if (IsRangeEnemy(enemy)) return;
            RangeFinished = true; CancelAction(); companionAction = null; projectiles.Clear(); hazards.Clear(); ClearFrost();
            dashTime = guardTime = 0; perfectDodgeArmed = false; riposteTime = 0; revision++;
            Notify("目标已清场。可查看记录、手动重置或返回，没有报酬。", "Targets cleared. Review the result, reset or return. No reward is awarded.");
        }
        private void EndRange()
        {
            RestoreSparringReturn(); save.health = Clamp(save.health, 1, save.maxHealth); practicing = false; ResetTransient(); enemies.Clear();
            if (!CanStand(Position)) { save.x = Map.spawnX; save.y = Map.spawnY; } revision++;
        }
    }
}
