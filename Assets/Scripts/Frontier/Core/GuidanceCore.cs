using System;
using System.Collections.Generic;
namespace Starfall
{
    public sealed class QuestStep
    {
        public string text; public bool done;
        public QuestStep(string text, bool done) { this.text = text; this.done = done; }
    }
    public sealed class QuestGuide
    {
        public string id, title, category, description, next, reward;
        public int room; public Point target; public bool complete, locked;
        public List<QuestStep> steps = new List<QuestStep>();
        public float Progress { get { if (complete) return 1; int n = 0; foreach (var s in steps) if (s.done) n++; return steps.Count == 0 ? 0 : n / (float)steps.Count; } }
    }
    public sealed partial class GameCore
    {
        public static readonly string[] QuestIds = { "main", "shore", "ally1", "ally2", "ally3", "personal0", "personal1", "personal2", "bounty", "secrets", "watch", "signal", "archive", "reach", "homeward", "commission", "shelter", "fieldcraft", "supper", "returnletter", "riverlesson", "watergarden" };
        private void SanitizeGuidance()
        {
            if (save.presentationVersion == 0)
            { save.trackedQuest = save.frontierFocus ? "shore" : "main"; save.tutorialBits = Has("intro") ? 255 : 0; save.showGuidance = true; save.effectsQuality = 2; save.presentationVersion = 1; }
            if (Array.IndexOf(QuestIds, save.trackedQuest) < 0) save.trackedQuest = "main";
            save.explored = BoundedArray(save.explored, data.maps.Length, 0, 16777215);
            save.tutorialBits = (int)Clamp(save.tutorialBits, 0, 255); save.effectsQuality = (int)Clamp(save.effectsQuality, 0, 2);
            save.tutorialDistance = Finite(save.tutorialDistance) ? Clamp(save.tutorialDistance, 0, 4) : 0;
        }
        public int TutorialStep { get { for (int i = 0; i < 8; i++) if ((save.tutorialBits & (1 << i)) == 0) return i; return 8; } }
        public void TutorialAction(int step)
        { if (step < 0 || step > 7 || (save.tutorialBits & (1 << step)) != 0) return; save.tutorialBits |= 1 << step; revision++; }
        public void RestartTutorial() { save.tutorialBits = 0; save.tutorialDistance = 0; save.showGuidance = true; revision++; }
        public void SkipTutorial() { save.tutorialBits = 255; revision++; }
        private void TickGuidance(Point before)
        {
            save.tutorialDistance = Math.Min(4, save.tutorialDistance + (Position - before).Length);
            if (save.tutorialDistance >= 3) TutorialAction(0); if (save.room == 1) TutorialAction(7);
            int bit = 1 << Sector(save.room,save.x, save.y);
            if ((save.explored[save.room] & bit) == 0) { save.explored[save.room] |= bit; revision++; }
        }
        private int Sector(int room,float x, float y) { MapData map=room==save.room?Map:data.maps[room]; return (int)Clamp(x*6/map.rows[0].Length,0,5)+(int)Clamp(y*4/map.rows.Length,0,3)*6; }
        public bool Explored(int room, float x, float y) { return room>=0&&room<data.maps.Length&&(save.explored[room] & (1 << Sector(room,x, y))) != 0; }
        public void TrackQuest(string id)
        { if (Array.IndexOf(QuestIds, id) < 0 || GetQuest(id).locked) return; save.trackedQuest = id; save.frontierFocus = id == "shore"; TutorialAction(2); revision++; }
        private Point ObjectPoint(int room, string kind, Point fallback)
        { foreach (var o in data.maps[room].objects) if (o.kind == kind) return new Point(o.x, o.y); return fallback; }
        private bool TryResourcePoint(int room, string kind, out Point target)
        {
            target = new Point(); float best = float.MaxValue;
            if (room == 8 || !CanEnter(room)) return false;
            foreach (var o in data.maps[room].objects) if (o.kind == kind && !Has("node_" + o.id))
            { float d = room == save.room ? (Position - new Point(o.x, o.y)).Length : 0; if (d < best) { best = d; target = new Point(o.x, o.y); } }
            return best < float.MaxValue;
        }
        private bool GuideToResource(QuestGuide q, string kind, int preferredRoom)
        {
            // Find a real remaining node; reading a guide never renews resources.
            int room = save.room; Point target;
            if (!TryResourcePoint(room, kind, out target))
            {
                room = preferredRoom;
                if (!TryResourcePoint(room, kind, out target))
                {
                    room = -1;
                    for (int candidate = 0; candidate < data.maps.Length; candidate++)
                        if (TryResourcePoint(candidate, kind, out target)) { room = candidate; break; }
                }
            }
            if (room >= 0)
            {
                q.room = room; q.target = target;
                q.next = L(kind == "herb" ? "采集月光草" : "采集矿石，补充星晶与零件", kind == "herb" ? "Gather Moonherbs" : "Gather ore for crystals and parts");
                return true;
            }
            // The inn interior has its own rest interaction; other maps return
            // through the existing town routes. No new refresh or reward rule.
            q.room = save.room == 9 ? 9 : 0;
            q.target = ObjectPoint(q.room, "rest", new Point(data.maps[q.room].spawnX, data.maps[q.room].spawnY));
            q.next = L("可采资源已用完；到旅店休息更新", "Available nodes depleted; rest at the inn to renew them");
            return false;
        }
        public QuestGuide CurrentGuide { get { return GetQuest(save.trackedQuest); } }
        public string GuidanceHint()
        { QuestGuide q = CurrentGuide; return q.room != save.room && !q.complete && !q.locked ? L("前往", "Travel to ") + L(data.maps[q.room].name, data.maps[q.room].en) + " · " + q.next : q.next; }
        public QuestGuide GetQuest(string id)
        {
            if (Array.IndexOf(QuestIds, id) < 0) id = "main";
            if (id == "watch") return GetWatchQuest();
            if (id == "signal") return GetSignalQuest();
            if (id == "archive") return GetArchiveQuest();
            if (id == "reach") return GetReachQuest();
            if (id == "homeward") return GetHomewardQuest();
            if (id == "commission") return GetCommissionQuest();
            if (id == "shelter") return GetShelterQuest();
            if (id == "fieldcraft") return GetFieldcraftQuest();
            if (id == "supper") return GetSupperQuest();
            if (id == "returnletter") return GetReturnLetterQuest();
            if (id == "riverlesson") return GetRiverLessonQuest();
            if (id == "watergarden") return GetWaterGardenQuest();
            QuestGuide q = new QuestGuide { id = id, room = 0, target = new Point(18, 17), category = L("主线", "STORY"), reward = L("硬币、经验与新的路线", "Coins, experience and new routes") };
            if (id == "secrets")
            {
                q.title = L("旧路的新光", "Light on Old Roads"); q.category = L("探索发现", "DISCOVERY"); q.description = L("巡路队的补给留在六条封闭支路里。用星核能力重访旧地，把他们的故事带回来。", "Six sealed paths hold road-warden caches. Return with core powers and recover their stories.");
                int[] rooms = { 1, 2, 3, 5, 6, 7 }, powers = { 1, 2, 3, 2, 1, 3 }; int count = 0, next = -1;
                for (int i = 0; i < rooms.Length; i++) { if (Has("cache_" + rooms[i])) count++; else if (next < 0 && Ability(powers[i]) && CanEnter(rooms[i])) next = i; }
                q.complete = count == 6; q.locked = !Ability(1); q.room = next < 0 ? save.stage + 1 : rooms[next]; q.room = Math.Min(7, q.room);
                q.target = next < 0 ? new Point(29, 8) : Has("seal_" + q.room) ? new Point(4.5f, 19.5f) : new Point(6.5f, 19.5f);
                q.next = q.complete ? L("六份巡路记忆已找回", "All six road memories recovered") : next < 0 ? L("推进主线或地区调查，解锁更多能力与路线", "Advance the story to gain powers and routes") : Has("seal_" + q.room) ? L("走入西南支路，打开旧日补给", "Enter the southwest path and open the cache") : L("在西南封印附近按 R 或 E", "Press R or E near the southwest seal");
                q.steps.Add(new QuestStep(L("找回补给 ", "Caches recovered ") + count + "/6", count == 6));
                q.steps.Add(new QuestStep(L("获得前三枚星核的能力", "Gain the first three core powers"), Ability(1) && Ability(2) && Ability(3)));
                q.reward = L("每箱：硬币30 / 碎片3 / 炖汤1 / 熟练度8", "Per cache: 30 coins / 3 shards / 1 stew / 8 mastery"); return q;
            }
            if (id.StartsWith("ally"))
            {
                int n = int.Parse(id.Substring(4)), item = n == 1 ? 2 : n == 2 ? 3 : 4, count = n == 1 ? 6 : n == 2 ? 3 : 5;
                q.category = L("镇民委托", "TOWN REQUEST"); q.title = new[] { "", L("杏的药草", "An's Moonherbs"), L("老潮的鲜鱼", "Old Tide's Silverfin"), L("诺尔的零件", "Noll's Spare Parts") }[n];
                q.description = L("为镇里的居民准备一份补给，帮助边境恢复日常生活。", "Gather supplies for a resident and help the town return to everyday life."); q.complete = Has("ally_" + n);
                q.steps.Add(new QuestStep(L(GameItemsZh(item), GameItemsEn(item)) + " " + Math.Min(count, save.inventory[item]) + "/" + count, save.inventory[item] >= count || q.complete));
                q.steps.Add(new QuestStep(L("返回余烬镇交付", "Deliver in Emberfall"), q.complete)); q.reward = L("镇民支持 · 硬币与经验", "Town support · coins and experience");
                q.target = ObjectPoint(0, n == 1 ? "garden" : n == 2 ? "fish" : "forge", new Point(18, 14));
                q.next = q.complete ? L("委托已完成", "Request complete") : save.inventory[item] >= count ? L("返回镇民处交付材料", "Deliver your supplies in town") : n == 2 ? L("到镇东湖边钓鱼", "Fish at the eastern lake") : L("采集材料；旅店休息可更新采集点", "Gather supplies; inn rest renews nodes");
                if (!q.complete && save.inventory[item] < count && n != 2) GuideToResource(q, n == 1 ? "herb" : "ore", 1);
                return q;
            }
            if (id.StartsWith("personal"))
            {
                int n = int.Parse(id.Substring(8)); q.room = n + 5; q.locked = !Has("companion_" + n); q.complete = Has("personal_" + n);
                q.category = L("同伴故事", "COMPANION"); q.title = new[] { L("未寄出的回信", "The Unsent Reply"), L("没有主人的机器", "A Machine Without a Master"), L("钟声下的名字", "Names Beneath the Bells") }[n];
                q.description = L("倾听同行者的故事，完成一件属于他们自己的事。", "Listen to your companion and finish something that belongs to them.");
                bool ready = n == 0 ? Has("manifest_found") : n == 1 ? save.inventory[4] >= 4 && save.inventory[1] >= 4 : Has("lore_1") && save.inventory[2] >= 4;
                q.target = ObjectPoint(q.room, n == 0 && !ready ? "manifest" : n == 0 ? "captain" : n == 1 ? "tinker" : "keeper", new Point(18, 14));
                if (n == 2 && !Has("lore_1")) { q.room = 1; q.target = ObjectPoint(1, "lore", new Point(12, 5)); }
                q.steps.Add(new QuestStep(new[] { L("找回乘客名册", "Recover the passenger manifest"),
                    L("零件 ", "Parts ") + (q.complete ? 4 : Math.Min(4, save.inventory[4])) + "/4 · " + L("星晶 ", "Crystals ") + (q.complete ? 4 : Math.Min(4, save.inventory[1])) + "/4",
                    L("密林记录与药草 ", "Forest record and herbs ") + (q.complete ? 4 : Math.Min(4, save.inventory[2])) + "/4" }[n], ready || q.complete));
                q.steps.Add(new QuestStep(L("与同伴交谈并交付", "Talk to your companion and deliver"), q.complete)); q.reward = L("羁绊+2 · 35硬币 · 碎片3 · 技能点1", "+2 bond · 35 coins · 3 shards · 1 point");
                q.next = q.complete ? L("同伴故事已完成", "Personal story complete") : q.locked ? L("先完成地区调查，招募同伴", "Finish the regional report to recruit them") : q.steps[ready ? 1 : 0].text;
                if (!q.locked && !q.complete && !ready)
                {
                    // Preserve Sen's record-first story beat; supplies only replace
                    // the handoff destination while a recruited companion needs them.
                    if (n == 1) GuideToResource(q, "ore", 1);
                    else if (n == 2 && Has("lore_1")) GuideToResource(q, "herb", 1);
                }
                return q;
            }
            if (id == "bounty")
            {
                q.category = L("公会悬赏", "CONTRACT"); q.locked = save.bounty < 0; int n = Math.Max(0, save.bounty);
                q.title = q.locked ? L("暂无悬赏", "No active contract") : L(BountyNames[n], BountyNamesEn[n]); q.description = L("接取后才计算进度，完成后在委托簿领取报酬。", "Only actions after acceptance count. Claim rewards in Contracts.");
                bool done = !q.locked && save.bountyProgress >= BountyTargets[n]; q.steps.Add(new QuestStep(save.bountyProgress + "/" + BountyTargets[n], done));
                q.room = done || n == 2 ? 0 : n == 5 ? 8 : save.room > 0 && save.room < 8 ? save.room : 1;
                q.target = ObjectPoint(q.room, done ? "contract" : n == 2 ? "fish" : n == 1 ? "ore" : n == 5 ? "abyssExit" : "altar", new Point(21, 19));
                q.next = done ? L("打开 N 委托簿领取报酬", "Open N Contracts to claim") : q.locked ? L("在 N 委托簿选择一项悬赏", "Accept a contract in N Contracts") : q.title; q.reward = L("35硬币 · 30经验 · 碎片", "35 coins · 30 XP · shards");
                if (!q.locked && !done && n == 1) GuideToResource(q, "ore", 1);
                if (!q.locked && !done && (n == 0 || n == 3 || n == 4)) GuideCombatContract(q, n);
                return q;
            }
            if (id == "shore")
            {
                q.title = L("远岸来信", "Letters from Afar"); q.category = L("远岸剧情", "DISTANT SHORES"); q.description = L("港口的水位、沙海的调度、高原的名字。把三份原始记录带到同一张桌前。", "Tide levels, allocations and names. Bring the original records to one council table."); q.locked = save.stage < 1; q.complete = save.frontierEnding > 0;
                foreach (string flag in new[] { "harbor_report", "desert_report", "mountain_report", "frontier_settled" }) q.steps.Add(new QuestStep(L(new[] { "雾港调查", "沙海调查", "高原调查", "达成三方协定" }[q.steps.Count], new[] { "Mistport report", "Desert report", "Mountain report", "Council covenant" }[q.steps.Count]), Has(flag)));
                if (q.complete) { q.next = L("协定已达成，自由探索远岸", "Covenant settled; explore the distant shores"); return q; }
                if (!Has("exp_intro")) { q.target = new Point(26, 9); q.next = L("找露恩接取调查；先交回第一枚星核", "Meet Lune; deliver the first starcore first"); return q; }
                if (Proofs == 3) { q.room = 5; q.target = new Point(18, 10); q.next = L("去雾港议事厅达成协定", "Settle the covenant at Mistport's council"); return q; }
                q.room = !Has("harbor_report") ? 5 : !Has("desert_report") ? 6 : 7; int region = q.room;
                q.target = ObjectPoint(region, region == 5 ? "captain" : region == 6 ? "tinker" : "keeper", new Point(18, 14));
                if (!Has("gate_" + region))
                {
                    if (region == 5 && save.harborPledge > 0)
                    {
                        bool rescue = save.harborPledge == 2 || save.harborPledge == 3 && RescueCount < 2;
                        for (int i = 0; i < 3; i++) if (!Has((rescue ? "rescue_" : "relay_") + i)) { q.target = ObjectPoint(region, (rescue ? "castaway" : "relay") + i, q.target); break; }
                        q.next = rescue ? L("救援乘客：每人木材1、药草1", "Rescue passengers: 1 timber and 1 herb each") : L("修复信号塔：每座零件1", "Repair relays: 1 part each");
                    }
                    else if (region == 6) { int[] wanted = { 1, 2, 1 }; int i = 0; while (i < 2 && save.mirrors[i] == wanted[i]) i++; q.target = ObjectPoint(region, "mirror" + i, q.target); q.next = L("旋转镜面，让光线抵达东侧蓄光池", "Rotate mirrors to reach the eastern receiver"); }
                    else if (region == 7) { int[] wanted = { 2, 0, 1 }; int i = 0; while (i < 2 && save.bells[i] == wanted[i]) i++; q.target = ObjectPoint(region, "bell" + i, q.target); q.next = L("调查钟阵；注意寒意，靠近火盆取暖", "Investigate the bells; warm up by braziers"); }
                    else q.next = L("先与米拉商定港口方案", "Agree on a harbor plan with Mira");
                }
                else if (!Has("boss_" + region)) { q.target = new Point(30, 8); q.next = L("前往祭坛挑战地区守护者", "Challenge the regional guardian at the altar"); }
                else q.next = L("与当地同伴交谈，提交调查", "Speak to the local companion and file the report"); return q;
            }
            q.title = L("星陨主线", "The Fallen Star"); q.description = L("找到边境的星核，追查堡垒屏障背后的代价。", "Recover the frontier's starcores and uncover the shield's cost."); q.complete = save.ending > 0;
            int chapter = save.stage + 1; q.room = chapter; q.target = new Point(29, 8);
            bool gateOpen = chapter == 4 || chapter == 1 && Has("scout_saved") || Has("gate_" + chapter);
            q.steps.Add(new QuestStep(L(chapter == 1 ? "解救侦察员" : chapter == 2 ? "破解遗迹石碑" : chapter == 3 ? "修复矿场升降机" : "抵达统领所在祭坛", chapter == 1 ? "Rescue the scout" : chapter == 2 ? "Solve the tablets" : chapter == 3 ? "Repair the elevator" : "Reach the warden's altar"), gateOpen));
            q.steps.Add(new QuestStep(L("击败守护者或达成和平解决", "Defeat the guardian or negotiate peace"), Has("boss_" + chapter)));
            q.steps.Add(new QuestStep(L(chapter == 4 ? "决定边境的未来" : "回收星核并交回公会", chapter == 4 ? "Choose the frontier's future" : "Recover the core and deliver to the guild"), q.complete));
            if (!gateOpen)
            {
                q.target = ObjectPoint(chapter, chapter == 1 ? "scout" : chapter == 2 ? !Has("lore_2") ? "lore" : "rune" + new[] { 0, 2, 1 }[Math.Min(2, runeProgress)] : "engine", q.target);
                q.next = L(chapter == 1 ? "清理敌人，再解救侦察员" : chapter == 2 ? "调查记录，依线索触碰石碑" : "收集零件3，修复升降机", chapter == 1 ? "Clear enemies, then rescue the scout" : chapter == 2 ? "Read the record, then touch the tablets" : "Gather 3 parts and repair the elevator");
                if (chapter == 1 && save.room == 1 && save.kills[1] < 4) { Enemy e = enemies.Find(a => !a.boss); if (e != null) q.target = e.Position; q.next += " " + save.kills[1] + "/4"; }
                if (chapter == 3 && save.inventory[4] < 3 && GuideToResource(q, "ore", 3))
                    q.next = L("采集矿石，收集零件3修复升降机", "Gather ore for 3 parts to repair the elevator");
            }
            else if (Has("core_" + chapter) && chapter < 4) { q.room = 0; q.target = new Point(18, 17); q.next = L("把星核交给莉雅", "Deliver the starcore to Lyra"); }
            else q.next = Has("boss_" + chapter) ? L("与祭坛互动，完成本章", "Interact with the altar to finish this chapter") : L("前往祭坛，准备守护者挑战", "Approach the altar and prepare for the guardian");
            if (q.complete) { q.room = 0; q.target = new Point(10, 11); q.next = L("故事已完成，自由探索边境", "Story complete; explore the frontier freely"); } return q;
        }
        private string GameItemsZh(int item) { return data.items[item].name; }
        private string GameItemsEn(int item) { return data.items[item].en; }
        public Point? NavigationTarget()
        {
            if (practicing) return enemies.Count > 0 ? (Point?)enemies[0].Position : null;
            if (InAbyss) return new Point(31, 14);
            WorldObject pin = PinnedLandmark;
            if (pin != null) return new Point(pin.x, pin.y);
            if (save.showGuidance && save.room == 0 && TutorialStep >= 3 && TutorialStep <= 5 && !practicing) return new Point(11, 14);
            QuestGuide q = CurrentGuide; if (q.complete || q.locked) return null;
            if (q.room == save.room) return q.target;
            foreach (var o in Map.objects) if (o.kind == "portal" + q.room) return new Point(o.x, o.y);
            if(save.room==ReachRoom)return ObjectPoint(ReachRoom,"reachExit",new Point(6,41));
            if(save.room==0&&q.room==ReachRoom)return ObjectPoint(0,"portal1",new Point(18,3));
            if (save.room == ArchiveRoom) return ObjectPoint(ArchiveRoom, "portal2", new Point(3, 18));
            if (save.room == 0 && q.room == ArchiveRoom) return ObjectPoint(0, "portal2", new Point(3, 14));
            foreach (var o in Map.objects) if (o.kind == "portal0") return new Point(o.x, o.y);
            if (save.room == 0 && q.room == 8) return ObjectPoint(0, "abyssEntry", new Point(31, 14));
            if (save.room == 0 && q.room >= 5) return new Point(26, 9); return null;
        }
        // Guidance to an object must finish where the normal E interaction
        // selects that exact object. Generic coordinates retain their original
        // path rules for combat objectives and checkpoint recovery.
        public List<Point> GuidancePath(Point target)
        {
            if (!Finite(target.x) || !Finite(target.y)) return new List<Point>();
            if (practicing || InAbyss) return NavigationPath(target);
            WorldObject pin = PinnedLandmark;
            if (pin != null && (new Point(pin.x, pin.y) - target).Length < .001f) return InteractionPath(pin);
            if (enemies.Exists(e => (e.Position - target).Length < .001f)) return NavigationPath(target);
            bool objectCoordinate = false;
            foreach (WorldObject o in Map.objects)
                if ((new Point(o.x, o.y) - target).Length < .001f)
                {
                    objectCoordinate = true;
                    if (IsObjectPresent(o)) return InteractionPath(o);
                }
            return objectCoordinate ? new List<Point>() : NavigationPath(target);
        }
        public List<Point> InteractionPath(WorldObject target)
        {
            var path = new List<Point>();
            if (target == null || !Array.Exists(Map.objects, o => ReferenceEquals(o, target)) || !IsObjectPresent(target)
                || !Finite(target.x) || !Finite(target.y) || !Finite(Position.x) || !Finite(Position.y)) return path;
            if (ReferenceEquals(Nearest(), target)) return path;
            int w = Map.rows[0].Length, h = Map.rows.Length, sx = (int)Position.x, sy = (int)Position.y;
            if (sx < 0 || sx >= w || sy < 0 || sy >= h) return path;
            int start = sy * w + sx, end = -1, head = 0, tail = 0;
            Point destination = new Point(target.x, target.y);
            PreparePathSearch(w * h, -2);
            int[] parent = pathParents; pathQueue[tail++] = start; parent[start] = -1;
            while (head < tail)
            {
                int n = pathQueue[head++], x = n % w, y = n / w;
                Point center = new Point(x + .5f, y + .5f);
                if ((center - destination).Length < 1.75f && Fits(center) && ReferenceEquals(NearestAt(center), target))
                { end = n; break; }
                for (int i = 0; i < 4; i++)
                {
                    int xx = x + guidanceSteps[i, 0], yy = y + guidanceSteps[i, 1];
                    if (xx < 0 || xx >= w || yy < 0 || yy >= h) continue;
                    int next = yy * w + xx;
                    if (parent[next] != -2 || !Fits(new Point(xx + .5f, yy + .5f))) continue;
                    parent[next] = n; pathQueue[tail++] = next;
                }
            }
            if (end < 0) return path;
            for (int n = end; n != start && n >= 0; n = parent[n]) path.Add(new Point(n % w + .5f, n / w + .5f));
            path.Reverse();
            Point startCenter = new Point(sx + .5f, sy + .5f);
            if (Fits(startCenter) && (startCenter - Position).Length > .001f) path.Insert(0, startCenter);
            return path;
        }
        public List<Point> NavigationPath(Point target)
        {
            int w = Map.rows[0].Length, h = Map.rows.Length, sx = (int)Position.x, sy = (int)Position.y, start = sy * w + sx;
            var path = new List<Point>(); if (sx < 0 || sx >= w || sy < 0 || sy >= h) return path;
            PreparePathSearch(w * h, -2);
            int[] parent = pathParents; int head = 0, tail = 0; pathQueue[tail++] = start; parent[start] = -1; int end = start; float best = (Position - target).Length;
            while (head < tail)
            {
                int n = pathQueue[head++], x = n % w, y = n / w; float d = (new Point(x + .5f, y + .5f) - target).Length;
                if (d < best) { best = d; end = n; } if (d <= 1.2f) { end = n; break; }
                for (int i = 0; i < 4; i++) { int xx = x + guidanceSteps[i, 0], yy = y + guidanceSteps[i, 1]; if (xx < 0 || xx >= w || yy < 0 || yy >= h) continue; int next = yy * w + xx; if (parent[next] != -2 || !Fits(new Point(xx + .5f, yy + .5f))) continue; parent[next] = n; pathQueue[tail++] = next; }
            }
            if (best > 1.8f && (new Point(end % w + .5f, end / w + .5f) - target).Length > 1.8f) return path;
            for (int n = end; n != start && n >= 0; n = parent[n]) path.Add(new Point(n % w + .5f, n / w + .5f)); path.Reverse(); return path;
        }
    }
}
