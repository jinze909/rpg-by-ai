using System;

namespace Starfall
{
    // An optional patrol route shares combat and the existing flag-based save model.
    // Reserved actor IDs identify its waves without treating them as chapter bosses.
    public sealed partial class GameCore
    {
        public const float WatchHoldSeconds = 12;
        private const int WatchEnemyBase = 72000;
        private static readonly Point[] watchFallbackPoints = { new Point(7, 12), new Point(18, 18), new Point(23, 9) };
        private static readonly Point[] watchSpawnOffsets = { new Point(-2.5f, -1.5f), new Point(2.5f, -1.5f), new Point(2.5f, 2.5f) };
        private int watchActive = -1;
        private float watchCharge;

        public bool WatchReady { get { return Has("scout_saved") && Has("core_1"); } }
        public bool WatchAccepted { get { return Has("watch_accept"); } }
        public int WatchStage
        { get { for (int i = 0; i < 3; i++) if (!WatchLampLit(i)) return i; return 3; } }
        public int WatchActiveStation { get { return watchActive; } }
        public bool WatchActive(int index) { return index >= 0 && index < 3 && watchActive == index; }
        public float WatchProgress { get { return Clamp(watchCharge / WatchHoldSeconds, 0, 1); } }
        public bool WatchLampLit(int index) { return index >= 0 && index < 3 && Has("watch_signal_" + index); }

        public Point WatchPoint(int index)
        {
            index = Math.Max(0, Math.Min(2, index));
            foreach (WorldObject o in data.maps[1].objects)
                if (o.kind == "watchSignal" + index) return new Point(o.x, o.y);
            return watchFallbackPoints[index];
        }
        private static int WatchWaveSize(int stage) { return stage == 0 ? 2 : 3; }
        private static int WatchEnemyStage(int id)
        {
            int offset = id - WatchEnemyBase;
            if (offset < 0 || offset > 22) return -1;
            int stage = offset / 10;
            return offset % 10 < WatchWaveSize(stage) ? stage : -1;
        }
        public bool IsWatchEnemyId(int id) { return WatchEnemyStage(id) >= 0; }
        public bool IsWatchEnemy(Enemy enemy) { return enemy != null && !enemy.boss && IsWatchEnemyId(enemy.id); }
        public void ResetWatchTransient() { watchActive = -1; watchCharge = 0; }

        private bool WatchUnderPressure(int stage)
        { Point p = WatchPoint(stage); return enemies.Exists(e => (e.Position - p).Length < 3); }
        public string WatchStatus
        {
            get
            {
                if (watchActive < 0) return WatchStage == 3 ? L("三处路灯已亮，回镇交付", "All signals lit; report in town") : L("与下一处路灯交互", "Start the next signal");
                if ((Position - WatchPoint(watchActive)).Length > 1.6f) return L("回到灯旁重新守灯", "Return to the signal circle");
                if (WatchUnderPressure(watchActive)) return L("先清理灯旁的敌人", "Clear foes near the signal");
                if (playerHitTime > 0) return L("受击后重新守灯", "Recover, then hold again");
                return L("守住灯旁 ", "Hold the circle ") + watchCharge.ToString("0.0") + " / " + WatchHoldSeconds + "s";
            }
        }

        public void RestoreWatchEncounter()
        {
            int stage = WatchStage;
            bool active = save.room == 1 && WatchReady && WatchAccepted && Has("cache_1") && stage < 3 && Has("watch_wave_" + stage) && !Has("watch_complete") && !practicing;
            // Discard obsolete optional actors from older snapshots, never normal enemies.
            enemies.RemoveAll(e => IsWatchEnemy(e) && (!active || WatchEnemyStage(e.id) != stage || Has("watch_defeated_" + e.id)));
            if (!active) { ResetWatchTransient(); return; }
            watchActive = stage;
            Point center = WatchPoint(stage);
            for (int slot = 0; slot < WatchWaveSize(stage); slot++)
            {
                int id = WatchEnemyBase + stage * 10 + slot;
                if (Has("watch_defeated_" + id)) continue;
                Enemy present = enemies.Find(e => e.id == id);
                if (present != null)
                {
                    // Snapshot restoration constructs a normal ID-based timer;
                    // reserved IDs must not postpone the first attack for hours.
                    if (present.timer > 2) present.timer = .65f + slot * .17f;
                    continue;
                }
                int kind = stage == 0 ? 0 : slot;
                Enemy actor = CreatePlacedEnemy(id, kind, center + watchSpawnOffsets[slot], kind == 2 ? 65 : kind == 1 ? 32 : 35);
                actor.timer = .65f + slot * .17f; actor.Face(Position - actor.Position);
                enemies.Add(actor);
            }
        }
        public void OnWatchKill(Enemy enemy)
        {
            if (!IsWatchEnemy(enemy) || save.room != 1 || !WatchAccepted) return;
            int stage = WatchEnemyStage(enemy.id);
            if (Has("watch_wave_" + stage)) Flag("watch_defeated_" + enemy.id);
        }
        public void TickWatch(float dt)
        {
            if (watchActive < 0) return;
            if (save.room != 1 || !WatchAccepted || !WatchReady || !Has("cache_1") || practicing || WatchStage != watchActive || Has("watch_complete"))
            { ResetWatchTransient(); return; }
            if (mode != "world" || Dying || !Finite(dt) || dt <= 0) return;
            if ((Position - WatchPoint(watchActive)).Length > 1.6f || WatchUnderPressure(watchActive) || playerHitTime > 0)
            { watchCharge = 0; return; }
            watchCharge = Math.Min(WatchHoldSeconds, watchCharge + dt);
            if (watchCharge + .00001f < WatchHoldSeconds) return;
            int completed = watchActive;
            Flag("watch_signal_" + completed);
            enemies.RemoveAll(e => IsWatchEnemy(e) && WatchEnemyStage(e.id) == completed);
            effects.Add(new Effect("discover", WatchPoint(completed), .6f));
            ResetWatchTransient();
            RecordMoment("旧路灯 " + (completed + 1) + " 已亮：回家的道路又近了一段。", "Patrol signal " + (completed + 1) + " restored: the road home reaches farther.");
            Notify(completed == 2 ? "三处路灯已亮！回余烬镇向侦察员交付。" : "路灯已亮！沿旧路前往下一处。", completed == 2 ? "All three signals lit! Report to the scout in Emberfall." : "Signal restored! Follow the route to the next one.");
        }

        private bool NearWatchScout()
        {
            if (save.room != 0) return false;
            foreach (WorldObject o in Map.objects)
                if (o.kind == "returnscout" && IsObjectPresent(o) && (Position - new Point(o.x, o.y)).Length < 1.75f) return true;
            return false;
        }
        public void ShowWatchCommission()
        {
            if (!WatchReady || !NearWatchScout() || practicing || Dying) return;
            if (Has("watch_complete"))
            {
                Dialog("归来的侦察员", "The returned scout", "你带来的旧信，终于成了一条能走回家的路。灯照着路边的人，也照着等他们回来的座位。巡路队的名字，会和这条路一起留下。", "The letter you carried has become a road people can walk home. Its lights serve the travelers and the seats still waiting for them. We will keep the patrol's names with their road.", C("记住这条路", "Remember the road", "close")); return;
            }
            if (WatchStage == 3 && WatchAccepted)
            {
                Dialog("归来的侦察员", "The returned scout", "三处灯都亮了。我们会沿着旧路送人回家，不再把森林根系当成要征服的地方。莉雅把巡路队留下的座位留在了旅店，我也会替下一个回来的人守着灯。", "All three signals are lit. We will guide people home around the forest's roots, rather than conquer them. Lyra kept the patrol's places at the inn. I will keep a light for whoever returns next.", C("交付巡路记录 · 领取报酬", "File the route · claim rewards", "watch_claim"), C("稍后交付", "Report later", "close")); return;
            }
            string zh = "你带来的信说，巡路队留下的不只有星核的线索，还有让人回家的路。密林西南补给箱里留着最后一段记录。读完它，再沿旧路点亮三处灯。\n\n每处灯旁守住12秒。附近3格有敌人、离开灯旁或受击都会重置这一次计时。已点亮的灯会保留，随时可以回镇休整。报酬：硬币40、碎片4、技能点1。";
            string en = "Your letter speaks of a road home as well as the starcores. The southwest forest cache holds the patrol's last note. Read it, then restore three signals along the old route.\n\nHold each circle for 12 seconds. Enemies within 3 tiles, leaving the circle or taking a hit resets that hold. Lit signals stay lit; you can return to town to rest. Rewards: 40 coins, 4 shards, 1 talent point.";
            Dialog("归来的侦察员", "The returned scout", zh, en, C(WatchAccepted ? "追踪旧路灯" : "接下最后一班巡路", WatchAccepted ? "Track the signals" : "Accept The Last Watch", WatchAccepted ? "watch_track" : "watch_accept"), C("稍后出发", "Leave later", "close"));
        }
        public bool TalkWatch(WorldObject o)
        {
            if (o == null || o.kind == null || !o.kind.StartsWith("watchSignal")) return false;
            int index;
            if (!int.TryParse(o.kind.Substring(11), out index) || index < 0 || index > 2) return true;
            if (!WatchReady || !WatchAccepted) { Notify("回镇找归来的侦察员，接取旧路委托。", "Meet the returned scout in town to accept the patrol route."); return true; }
            if (!Has("cache_1")) { Notify("先调查密林西南的旧日补给，了解巡路记录。", "First read the patrol note in the southwest forest cache."); return true; }
            if (WatchLampLit(index)) { Notify("这处路灯已亮，会替下一个回来的人照路。", "This signal is lit for the next traveler coming home."); return true; }
            if (index != WatchStage) { Notify("先点亮前一处路灯，让旧路依次接通。", "Restore the earlier signal first to connect the route."); return true; }
            Dialog(o.name, o.en, "旧灯会引来附近失控的敌人。守住灯旁12秒；附近3格有敌人、离开或受击会重置计时。先闪避反击，再回来稳住灯。点亮后进度永久保留。", "The old light draws nearby frenzied foes. Hold its circle for 12 seconds. Enemies within 3 tiles, leaving or taking a hit resets the timer. Dodge and counter, then return to stabilize it. Once lit, the signal is permanent.", C(watchActive == index ? "继续守灯" : "开始守灯", watchActive == index ? "Continue the watch" : "Start the watch", "watch_start_" + index), C("先做准备", "Prepare first", "close"));
            return true;
        }
        public bool ChooseWatch(string action)
        {
            if (action == "watch_commission" || action == "watch_menu") { ShowWatchCommission(); return true; }
            if (action == "watch_accept" || action == "watch_track")
            {
                if (!WatchReady || !NearWatchScout() || practicing || Dying || Has("watch_complete")) return true;
                if (action == "watch_accept" && !WatchAccepted)
                { Flag("watch_accept"); RecordMoment("接下最后一班巡路：找回旧记录，点亮回家的路。", "The Last Watch accepted: recover the patrol note and light a road home."); }
                if (WatchAccepted) TrackQuest("watch");
                return true;
            }
            if (action == "watch_claim")
            {
                if (!WatchReady || !WatchAccepted || WatchStage != 3 || Has("watch_complete") || !NearWatchScout() || practicing || Dying) return true;
                Flag("watch_complete"); save.coins += 40; save.shards += 4; save.skillPoints++;
                RecordMoment("最后一班巡路完成：灯为回家的人而亮。", "The Last Watch complete: these lights serve the people coming home.");
                Notify("巡路完成！硬币40、碎片4、技能点1。", "Patrol complete! 40 coins, 4 shards, 1 talent point."); sound = "reward";
                return true;
            }
            if (action == null || !action.StartsWith("watch_start_")) return false;
            int index;
            if (!int.TryParse(action.Substring(12), out index) || index < 0 || index > 2) return true;
            if (mode != "world" || Dying || practicing || save.room != 1 || !WatchReady || !WatchAccepted || !Has("cache_1") || Has("watch_complete") || index != WatchStage || (Position - WatchPoint(index)).Length >= 1.75f) return true;
            Flag("watch_wave_" + index);
            RestoreWatchEncounter(); TrackQuest("watch");
            Notify("灯光引来了敌人！先清理灯旁，再守住12秒。", "The signal draws foes! Clear its circle, then hold for 12 seconds."); sound = "windup";
            return true;
        }

        public QuestGuide GetWatchQuest()
        {
            QuestGuide q = new QuestGuide { id = "watch", title = L("最后一班巡路", "The Last Watch"), category = L("巡路故事", "PATROL STORY"),
                description = L("带着巡路队的旧信，沿密林旧路点亮三处灯，让回家的人能找到方向。", "Follow the old patrol's letter and restore three signals so travelers can find their way home."),
                reward = L("硬币40 · 碎片4 · 技能点1", "40 coins · 4 shards · 1 talent point"), locked = !WatchReady, complete = Has("watch_complete"), room = 0,
                target = ObjectPoint(0, "returnscout", new Point(13, 17)) };
            q.steps.Add(new QuestStep(L("调查密林西南的巡路补给", "Read the southwest patrol cache"), Has("cache_1")));
            for (int i = 0; i < 3; i++) q.steps.Add(new QuestStep(L("点亮旧路灯 ", "Restore patrol signal ") + (i + 1), WatchLampLit(i)));
            if (q.complete) { q.next = L("旧路已接通，巡路队的名字留在镇中", "The road is open; the town remembers the patrol"); return q; }
            if (q.locked) { q.next = L("解救侦察员，并收取第一枚星核", "Rescue the scout and recover the first starcore"); return q; }
            if (!WatchAccepted) { q.next = L("回镇与归来的侦察员交谈", "Meet the returned scout in town"); return q; }
            if (!Has("cache_1"))
            {
                q.room = 1; q.target = Has("seal_1") ? new Point(4.5f, 19.5f) : new Point(6.5f, 19.5f);
                q.next = Has("seal_1") ? L("打开西南补给箱，调查巡路记录", "Read the patrol note in the southwest cache") : L("在西南封印旁按 R 或 E 打开旧路", "Use R or E at the southwest seal to open the old path"); return q;
            }
            int stage = WatchStage;
            if (stage == 3) { q.next = L("回余烬镇向侦察员交付，领取报酬", "Report to the scout in Emberfall and claim rewards"); return q; }
            q.room = 1; q.target = WatchPoint(stage);
            q.next = watchActive == stage ? WatchStatus : L("与旧路灯 ", "Interact with patrol signal ") + (stage + 1) + L(" 交互，开始守灯", " to start the watch");
            return q;
        }
    }
}
