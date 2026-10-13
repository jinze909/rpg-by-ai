using System;

namespace Starfall
{
    public sealed partial class GameCore
    {
        private bool sparringSession;
        public bool SparringActive { get { return practicing && sparringSession; } }
        public bool SparringReady { get { return save.practiceBits == 7 || Has("core_1"); } }
        public int SparringStage { get; private set; }
        public bool SparringAwaitingNext { get; private set; }
        public bool SparringFinished { get; private set; }
        public int SparringHitsTaken { get; private set; }
        public int SparringCounters { get; private set; }
        public int SparringBreaks { get; private set; }
        public int SparringFlanks { get; private set; }
        public float SparringTime { get; private set; }
        private bool SparringActionBlocked { get { return SparringActive && (SparringAwaitingNext || SparringFinished); } }
        public string SparringTitle
        { get { return L(new[] { "追猎者 · 闪避反击", "重甲 · 绕后与破势", "双敌 · 选择目标" }[SparringStage], new[] { "Hunter · dodge and counter", "Armor · flank or break", "Mixed foes · choose your target" }[SparringStage]); } }
        public string SparringHint
        { get { return L(new[] { "观察扑击预告，向侧面闪避，再回身反击。", "正面护甲减少伤害；从背后攻击，或在预告时破势。", "先侧移避箭，选好目标；不要被近身敌人堵住退路。" }[SparringStage], new[] { "Read the lunge, dodge sideways, then turn and counter.", "Front armor reduces damage. Flank it or break its wind-up.", "Sidestep arrows and choose a target. Keep an escape route past the hunter." }[SparringStage]); } }

        public bool IsSparringEnemyId(int id) { return id == 75000 || id == 75010 || id == 75020 || id == 75021; }
        public bool IsSparringEnemy(Enemy e) { return e != null && !e.boss && IsSparringEnemyId(e.id); }
        private bool NearSparringMentor()
        {
            if (save.room != 0) return false;
            foreach (WorldObject o in Map.objects)
                if (o.kind == "mentor" && IsObjectPresent(o) && (Position - new Point(o.x, o.y)).Length < 1.75f) return true;
            return false;
        }
        public void ShowSparring()
        {
            if (mode != "world" || Dying || practicing || !NearSparringMentor()) return;
            if (!SparringReady)
            {
                Dialog("艾妲 · 战术训练", "Ada · Tactical sparring", "先完成基础木桩练习，或找回第一枚星核，再来练习真实战斗。基础教学和报酬仍在。", "Finish basic dummy practice or recover the first starcore, then return for real encounters. The original lessons and their reward remain available.", C("先完成基础练习", "Practice the basics first", "close")); return;
            }
            Dialog("艾妲 · 战术训练", "Ada · Tactical sparring",
                "巡路不是只会挥剑。试三段真实战斗：追猎者、重甲，最后同时应对射手与追猎者。清场后手动进入下一段，随时可重练或返回。\n\n看预告、试闪避反击、绕后或破势。战术记录只作提示，不要求所有职业使用同一打法。受击有真实反应，但生命与物资安全；世界时间暂停，同伴不出战。退出或读档回到开始的位置与生命、耐力。敌人不掉物资，也不增加经验或委托进度。\n\n首次完成三段：硬币25、技能点1。以后可随时再练。",
                "A road warden needs more than a sword swing. Try three real encounters: a hunter, a heavy, then an archer and hunter together. After each clear, start the next drill yourself. Restart or leave anytime.\n\nRead tells; try dodge-counters, flanks and guard breaks. Tactical counts are feedback, not a required class playstyle. Hits react normally, but health and supplies are safe. World time pauses and companions sit out. Leaving or loading returns your starting position, health and energy. Foes give no loot, XP or contract progress.\n\nFirst complete course: 25 coins and 1 talent point. Repeat it anytime.",
                C("开始三段战术训练", "Start the three drills", "sparring_start"), C("先做准备", "Prepare first", "close"));
        }
        public bool ChooseSparring(string action)
        {
            if (action == "sparring_menu") { ShowSparring(); return true; }
            if (action == "sparring_start") { StartSparring(); return true; }
            return false;
        }
        public bool StartSparring()
        {
            if (mode != "world" || Dying || practicing || !SparringReady || !NearSparringMentor()) return false;
            save.sparringReturn = new SparringReturnRecord { x = save.x, y = save.y, health = save.health, energy = save.energy, heat = save.heat, cold = save.cold };
            practicing = sparringSession = true; SparringStage = 0;
            BeginSparringStage();
            Notify("战术训练开始。清场后再前进，生命与物资安全。", "Sparring begins. Clear each drill, then continue. Health and supplies are safe.");
            return true;
        }
        private void BeginSparringStage()
        {
            int stage = SparringStage;
            ResetTransient(); practicing = sparringSession = true; SparringStage = stage;
            // ResetTransient clears the previous contact contexts before any
            // new actor exists. No old frost pulse or arrow can hit this drill.
            enemies.Clear(); save.x = 12; save.y = 14; save.energy = 100; invulnerable = 0;
            if (stage == 0) AddSparringEnemy(75000, 0, new Point(16, 13), 55, 1);
            else if (stage == 1) AddSparringEnemy(75010, 2, new Point(16, 13), 85, 1);
            else
            {
                AddSparringEnemy(75020, 1, new Point(19, 13), 45, .8f);
                AddSparringEnemy(75021, 0, new Point(15, 12), 55, 1.1f);
            }
            revision++;
        }
        private void AddSparringEnemy(int id, int kind, Point position, float health, float delay)
        { Enemy e = CreatePlacedEnemy(id, kind, position, health); e.timer = delay; e.Face(Position - e.Position); enemies.Add(e); }
        public bool ContinueSparring()
        {
            if (!SparringActive || mode != "world" || Dying || !SparringAwaitingNext || SparringFinished || SparringStage >= 2) return false;
            int hits = SparringHitsTaken, counters = SparringCounters, breaks = SparringBreaks, flanks = SparringFlanks;
            float time = SparringTime; SparringStage++; BeginSparringStage();
            SparringHitsTaken = hits; SparringCounters = counters; SparringBreaks = breaks; SparringFlanks = flanks; SparringTime = time;
            Notify("下一段训练开始。先看目标与预告，再选择打法。", "Next drill begins. Read the foes and their tells before choosing your approach."); return true;
        }
        public bool RestartSparring()
        {
            if (!SparringActive || mode != "world" || Dying) return false;
            SparringStage = 0; BeginSparringStage();
            Notify("重新开始三段训练。之前的完成报酬不会重复领取。", "Restarted all three drills. A previously earned course reward will not repeat."); return true;
        }
        private void ResetSparringTransient()
        {
            sparringSession = false; SparringStage = 0; SparringAwaitingNext = SparringFinished = false;
            SparringHitsTaken = SparringCounters = SparringBreaks = SparringFlanks = 0; SparringTime = 0;
        }
        private void OnSparringContact(Enemy e, ImpactProfile impact, bool broke, bool flank)
        {
            if (!SparringActive || !IsSparringEnemy(e) || !impact.playerOwned) return;
            if (impact.counter) SparringCounters++;
            if (broke) SparringBreaks++;
            if (flank) SparringFlanks++;
        }
        private void TickSparring(float dt)
        {
            if (!SparringActive || mode != "world" || Dying || SparringAwaitingNext || SparringFinished) return;
            SparringTime += dt;
            if (enemies.Count > 0) return;
            CancelAction(); companionAction = null; projectiles.Clear(); hazards.Clear(); ClearFrost();
            dashTime = guardTime = 0; perfectDodgeArmed = false; riposteTime = 0;
            SparringAwaitingNext = SparringStage < 2; SparringFinished = SparringStage == 2;
            if (SparringFinished)
            {
                bool first = !Has("sparring_complete");
                if (first) { Flag("sparring_complete"); save.coins += 25; save.skillPoints++; }
                RecordMoment("完成战术训练：受击 " + SparringHitsTaken + " 次，反击命中 " + SparringCounters + " 次，破势 " + SparringBreaks + " 次，绕后命中 " + SparringFlanks + " 次。", "Tactical sparring complete: " + SparringHitsTaken + " hits taken, " + SparringCounters + " counter contacts, " + SparringBreaks + " breaks, " + SparringFlanks + " flank contacts.");
                Notify(first ? "三段训练完成！硬币25、技能点1。可查看记录或返回。" : "三段训练完成！可查看记录、重练或返回。", first ? "Course complete! 25 coins, 1 point. Review your feedback or return." : "Course complete! Review, restart or return.");
            }
            else Notify("本段已清场。查看战术记录，再手动开始下一段。", "Drill cleared. Review your tactical feedback, then start the next drill yourself.");
            revision++;
        }
        private void RestoreSparringReturn()
        {
            SparringReturnRecord record = save.sparringReturn; if (record == null) return;
            save.sparringReturn = null; save.room = 0;
            if (save.abyssFloor > 0) EndAbyss(false);
            save.x = Finite(record.x) ? record.x : data.maps[0].spawnX;
            save.y = Finite(record.y) ? record.y : data.maps[0].spawnY;
            if (Finite(record.health) && record.health > 0) save.health = record.health;
            save.energy = Finite(record.energy) ? Clamp(record.energy, 0, 100) : 100;
            save.heat = Finite(record.heat) ? Clamp(record.heat, 0, 100) : 0;
            save.cold = Finite(record.cold) ? Clamp(record.cold, 0, 100) : 0;
            save.snapshotRoom = -1; save.worldEnemies = null;
        }
        private void EndSparring()
        {
            RestoreSparringReturn(); save.health = Clamp(save.health, 1, save.maxHealth); practicing = false; ResetTransient(); enemies.Clear();
            if (!CanStand(Position)) { save.x = Map.spawnX; save.y = Map.spawnY; }
            revision++;
        }
    }
}
