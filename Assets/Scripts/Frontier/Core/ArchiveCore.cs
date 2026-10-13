using System;

namespace Starfall
{
    // Optional Ruins investigation. Permanent flags extend v1/v2 saves without
    // renumbering mature maps, lore, chapter bosses or the ordinary actor allocator.
    public sealed partial class GameCore
    {
        public const int ArchiveRoom = 10;
        private const int ArchiveEnemyBase = 74000;
        public static readonly Point[] ArchiveValvePoints = { new Point(7, 6), new Point(7, 10), new Point(10, 10) };
        private static readonly int[] archiveSolution = { 2, 0, 3 };
        private static readonly Point[] archiveGuardPoints = { new Point(27, 11), new Point(31, 8), new Point(30, 15) };
        private static readonly float[] archiveGuardHealth = { 45, 40, 70 };
        private static readonly Point[] archivePulsePoints = { new Point(18.5f, 12.5f), new Point(18.5f, 8.5f) };
        private float archivePulseClock = 1;
        private int archivePulseLane;

        public bool ArchiveReady { get { return Has("core_2"); } }
        public bool ArchiveAccepted { get { return Has("archive_accept"); } }
        public bool ArchiveDrained { get { return Has("archive_drain"); } }
        public bool ArchiveVented { get { return Has("archive_vent"); } }
        public bool ArchiveComplete { get { return Has("archive_complete"); } }
        public bool ArchiveRecordsRecovered
        { get { return Has("archive_record_0") && Has("archive_record_1") && Has("archive_record_2"); } }
        public bool ArchiveGuardsCleared
        { get { return Has("archive_wave") && Has("archive_defeated_74000") && Has("archive_defeated_74001") && Has("archive_defeated_74002"); } }
        // Select accepted Ruins textures only; gameplay continues to own room 10.
        public int ArtRoom { get { return save.room == ArchiveRoom ? 2 : save.room; } }
        private bool ArchiveClosedCell(int x, int y)
        {
            return save.room == ArchiveRoom &&
                ((!ArchiveDrained && x >= 9 && x <= 12 && y >= 13 && y <= 14) ||
                 (!ArchiveVented && x == 23 && y >= 6 && y <= 7));
        }
        public char PresentationTile(int x, int y)
        {
            if (y < 0 || y >= Map.rows.Length || x < 0 || x >= Map.rows[y].Length) return '#';
            if (ArchiveClosedCell(x, y)) return x == 23 ? '#' : '~';
            return Map.rows[y][x];
        }
        public int ArchiveValve(int index)
        {
            if (index < 0 || index > 2) return 0;
            for (int r = 0; r < 4; r++) if (Has("archive_valve_" + index + "_" + r)) return r;
            return 0;
        }
        private bool ArchivePort(int valve, int port)
        { int rotation = ArchiveValve(valve); return rotation == port || (rotation + 1) % 4 == port; }
        public int ArchiveFlowSegments
        {
            get
            {
                if (ArchiveDrained) return 4;
                if (!ArchivePort(0, 3)) return 0;
                if (!ArchivePort(0, 2) || !ArchivePort(1, 0)) return 1;
                if (!ArchivePort(1, 1) || !ArchivePort(2, 3)) return 2;
                return ArchivePort(2, 0) ? 4 : 3;
            }
        }
        private bool NearArchiveObject(string kind)
        {
            foreach (WorldObject o in Map.objects)
                if (o.kind == kind && IsObjectPresent(o) && (Position - new Point(o.x, o.y)).Length < 1.75f) return true;
            return false;
        }
        private bool ArchiveWorldAction
        { get { return save.room == ArchiveRoom && ArchiveReady && mode == "world" && !Dying && !practicing; } }
        public bool RotateArchiveValve(int index)
        {
            if (!ArchiveWorldAction || index < 0 || index > 2 || ArchiveDrained || !NearArchiveObject("archiveValve" + index)) return false;
            int next = (ArchiveValve(index) + 1) % 4;
            string prefix = "archive_valve_" + index + "_";
            save.flags.RemoveAll(f => f.StartsWith(prefix, StringComparison.Ordinal));
            Flag(prefix + next); sound = "quest";
            if (ArchiveFlowSegments == 4)
            {
                Flag("archive_drain");
                RecordMoment("遗潮水路已接通，第一道旧门打开。", "The archive waterway connects; its first passage is open.");
                Notify("水路已接通！穿过中间小道，到远处水轮放水。", "Waterway connected! Cross the catwalk and release the far pressure wheel.");
            }
            else Notify("水轮转动：看两端方向与接通的水路。", "Valve turned: check its two ports and the connected pipe segments.");
            return true;
        }
        public bool VentArchive()
        {
            if (!ArchiveWorldAction || !ArchiveDrained || ArchiveVented || !NearArchiveObject("archiveVent")) return false;
            Flag("archive_vent"); ResetArchiveTransient(); sound = "quest";
            RecordMoment("遗潮水压已解除，记录厅重新开放。", "Archive pressure released; the record vault opens again.");
            Notify("水压已解除。记录厅开放，可以安全往返。", "Pressure released. The record vault is open for safe return journeys.");
            return true;
        }
        private void VisitArchive()
        {
            if (save.room != ArchiveRoom || !ArchiveReady || ArchiveAccepted) return;
            Flag("archive_accept"); TrackQuest("archive");
            RecordMoment("走进遗潮档案，找回水下留下的名字。", "Entered the Tide Archive to recover the names left beneath the water.");
        }
        public void ResetArchiveTransient()
        { archivePulseClock = 1; archivePulseLane = 0; hazards.RemoveAll(h => h.kind == "archiveTide"); }
        public void TickArchive(float dt)
        {
            if (save.room != ArchiveRoom || !ArchiveDrained || ArchiveVented || practicing || Dying)
            { ResetArchiveTransient(); return; }
            if (mode != "world" || !Finite(dt) || dt <= 0) return;
            if (Position.x < 13 || Position.x >= 23 || Position.y < 4 || Position.y >= 16)
            { ResetArchiveTransient(); return; }
            archivePulseClock -= dt;
            if (archivePulseClock > .00001f) return;
            AddHazard(archivePulsePoints[archivePulseLane], 1.35f, 1.2f, 14, "archiveTide");
            archivePulseLane = 1 - archivePulseLane;
            archivePulseClock += 3.5f;
        }
        public bool IsArchiveEnemyId(int id) { return id >= ArchiveEnemyBase && id < ArchiveEnemyBase + 3; }
        public bool IsArchiveEnemy(Enemy e)
        { return e != null && IsArchiveEnemyId(e.id) && !e.boss && e.kind == e.id - ArchiveEnemyBase; }
        private bool ArchiveDefenceActive
        { get { return save.room == ArchiveRoom && ArchiveReady && ArchiveVented && ArchiveRecordsRecovered && Has("archive_wave") && !ArchiveComplete && !practicing; } }
        private bool SpawnArchiveRoom()
        {
            if (save.room != ArchiveRoom) return false;
            RestoreArchiveEncounter(); return true;
        }
        public void RestoreArchiveEncounter()
        {
            bool active = ArchiveDefenceActive;
            enemies.RemoveAll(e => IsArchiveEnemyId(e.id) && (!active || !IsArchiveEnemy(e) || Has("archive_defeated_" + e.id)));
            if (!active) return;
            for (int slot = 0; slot < 3; slot++)
            {
                int id = ArchiveEnemyBase + slot;
                if (Has("archive_defeated_" + id)) continue;
                Enemy e = enemies.Find(actor => actor.id == id);
                if (e == null)
                {
                    e = CreatePlacedEnemy(id, slot, archiveGuardPoints[slot], archiveGuardHealth[slot]);
                    e.Face(Position - e.Position); enemies.Add(e);
                }
                if (e.timer > 2) e.timer = .65f + slot * .17f;
            }
        }
        private void OnArchiveKill(Enemy e)
        {
            if (ArchiveDefenceActive && IsArchiveEnemy(e)) Flag("archive_defeated_" + e.id);
            if (ArchiveDefenceActive && ArchiveGuardsCleared)
                Notify("记录守卫已平息。到档案台决定如何送回名字。", "The record guardians are quiet. Decide how to return the names at the archive desk.");
        }
        public void ShowArchiveEntry()
        {
            if (save.room != 2 || !NearArchiveObject("portal10") || mode != "world" || Dying || practicing) return;
            if (!ArchiveReady)
            {
                Dialog("遗潮档案入口", "Tide Archive entrance", "旧门仍被水封住。先找回第二枚星核，水路才会回应。", "Water seals the old entrance. Recover the second starcore before its channels will answer.", C("稍后回来", "Return later", "close")); return;
            }
            Dialog("遗潮档案入口", "Tide Archive entrance",
                "水下记录厅留下了三份旧册。先接通三处水轮，再沿小道放水。小道两处水压轮流亮起：亮圈后1.2秒冲水，随后可通行；两侧有等候处。\n\n带回记录后，决定把名字交给公会，还是亲自送给家人。报酬相同：硬币50、碎片6、技能点1。随时可从入口返回遗迹。",
                "Three old records remain below. Connect three elbow valves, then cross the catwalk to release pressure. Two circles alternate: water strikes 1.2 seconds after a warning, then the lane is quiet. Side alcoves offer safe waiting ground.\n\nRecover the records, then entrust the names to the guild or take them to their families. Both choices pay 50 coins, 6 shards and 1 talent point. The entrance always returns to Ruins.",
                C("进入遗潮档案", "Enter the Tide Archive", "archive_enter"), C("追踪遗潮档案", "Track the Tide Archive", "archive_track"), C("先做准备", "Prepare first", "close"));
        }
        private void ShowArchiveKeeper(WorldObject o)
        {
            if (ArchiveComplete)
            {
                Dialog(o.name, o.en, Has("archive_family") ? "你将带着名字走访家人。旧册记录的是照顾与离别，不能替任何人许诺归来。" : "公会将保管一份可查的旧册。每个名字都能被家人找到，记录没有替任何人许诺归来。",
                    Has("archive_family") ? "You will carry the names to their families. The records hold care and separation; they cannot promise anyone's return." : "The guild will keep an accessible copy. Families can find every name, without the records promising anyone's return.", C("记住这些名字", "Remember the names", "close")); return;
            }
            if (!ArchiveRecordsRecovered || !ArchiveGuardsCleared)
            {
                Dialog(o.name, o.en, "先找全三份旧册，再让守卫平息。水下的名字应该一起带回，不能从旧页断定谁已归来。", "Recover all three records and quiet their guardians. Bring the names back together; a surviving page cannot prove who has returned.", C("继续调查", "Continue investigating", "close")); return;
            }
            Dialog(o.name, o.en, "旧册记录了水位、撤离路线与照顾失散孩子的家人。没有一页能证明失踪的人已经归来。\n\n把记录交给公会，让更多人查到名字；或亲自带给家人，让他们先核对。两种选择都保留原册，报酬相同：硬币50、碎片6、技能点1。",
                "The records hold water levels, evacuation routes and families who cared for separated children. No page proves that the missing have returned.\n\nFile a public guild copy so more people can find the names, or carry them to the families for verification first. Both preserve the originals and pay 50 coins, 6 shards and 1 talent point.",
                C("交给公会，公开记录", "File a public guild copy", "archive_public"), C("送给家人，先做核对", "Take names to families first", "archive_family"), C("稍后决定", "Decide later", "close"));
        }
        public bool TalkArchive(WorldObject o)
        {
            if (o == null || o.kind == null) return false;
            if (save.room == 2 && o.kind == "portal10") { ShowArchiveEntry(); return true; }
            if (save.room != ArchiveRoom || !o.kind.StartsWith("archive", StringComparison.Ordinal)) return false;
            if (!ArchiveWorldAction || !NearArchiveObject(o.kind)) return true;
            if (o.kind == "archivePlan")
            {
                Flag("archive_plan");
                Dialog(o.name, o.en, "进水在左，出水在水轮III上方。\n\n水轮I接左与下；II接上与右；III接左与上。看水轮的两端方向，每次转动四分之一圈，亮起的管道表示已接通。排水后旧门永久打开。\n\n小道两处水压交替冲水，亮圈后1.2秒落水。两侧等候处安全；到远处水轮放水即可永久停止。",
                    "The inlet is west; the outlet sits north of valve III.\n\nI joins west/south; II joins north/east; III joins west/north. Each turn rotates an elbow a quarter turn. Bright pipe segments show connected water. The drained passage stays open permanently.\n\nTwo catwalk circles alternate, striking 1.2 seconds after warning. Wait in the side alcoves; releasing the far wheel stops the pressure permanently.", C("记住水路", "Remember the waterway", "close")); return true;
            }
            int index;
            if (o.kind.StartsWith("archiveValve", StringComparison.Ordinal) && int.TryParse(o.kind.Substring(12), out index) && index >= 0 && index < 3)
            {
                if (ArchiveDrained) { Notify("水路已接通，旧门保持开放。", "The waterway is connected; the passage stays open."); return true; }
                string[] ports = { "N + E", "E + S", "S + W", "W + N" };
                Dialog(o.name, o.en, "当前方向：" + ports[ArchiveValve(index)] + "。转动水轮，让两端连接进水与出水。亮起的管道表示已接通。", "Current ports: " + ports[ArchiveValve(index)] + ". Turn the elbow to connect inlet to outlet. Bright segments show connected flow.", C("转动四分之一圈", "Turn a quarter turn", "archive_rotate_" + index), C("查看水路", "Inspect the pipes", "close")); return true;
            }
            if (o.kind == "archiveVent")
            {
                if (!ArchiveDrained || ArchiveVented) { Notify(ArchiveVented ? "水压已解除，记录厅保持开放。" : "先接通入口的三处水轮。", ArchiveVented ? "Pressure is released; the vault stays open." : "Connect the three entry valves first."); return true; }
                Dialog(o.name, o.en, "打开水轮，让小道的水压流入旧渠。冲水将永久停止，记录厅的门也会打开。", "Release the wheel into the old channel. Pressure stops permanently and the record-vault gate opens.", C("解除水压", "Release the pressure", "archive_vent"), C("先看看", "Inspect first", "close")); return true;
            }
            if (!ArchiveVented) { Notify("先解除小道水压，打开记录厅。", "Release the gallery pressure to open the vault first."); return true; }
            if (o.kind.StartsWith("archiveRecord", StringComparison.Ordinal) && int.TryParse(o.kind.Substring(13), out index) && index >= 0 && index < 3)
            {
                Flag("archive_record_" + index); Flag("history_" + (17 + index));
                if (ArchiveRecordsRecovered && !Has("archive_wave"))
                {
                    Flag("archive_wave"); RestoreArchiveEncounter();
                    Notify("三份旧册已全，守卫苏醒！先让它们平息。", "All three records recovered. The guardians awaken! Quiet them first.");
                }
                LoreData book = data.lore[17 + index];
                Dialog(book.title, book.en, book.zh, book.text, C("记入手帐", "Record in journal", "close")); return true;
            }
            if (o.kind == "archiveKeeper") { ShowArchiveKeeper(o); return true; }
            return true;
        }
        public bool ChooseArchive(string action)
        {
            if (action == null || !action.StartsWith("archive_", StringComparison.Ordinal)) return false;
            if (mode != "world" || Dying || practicing) return true;
            if (action == "archive_enter" || action == "archive_track")
            {
                if (!ArchiveReady || save.room != 2 || !NearArchiveObject("portal10")) return true;
                if (action == "archive_enter") Enter(ArchiveRoom); else TrackQuest("archive");
                return true;
            }
            int index;
            if (action.StartsWith("archive_rotate_", StringComparison.Ordinal) && int.TryParse(action.Substring(15), out index))
            { RotateArchiveValve(index); return true; }
            if (action == "archive_vent") { VentArchive(); return true; }
            if (action != "archive_public" && action != "archive_family") return true;
            if (!ArchiveWorldAction || !ArchiveVented || !ArchiveRecordsRecovered || !ArchiveGuardsCleared || ArchiveComplete || !NearArchiveObject("archiveKeeper")) return true;
            Flag(action); Flag("archive_complete"); save.coins += 50; save.shards += 6; save.skillPoints++;
            RecordMoment(action == "archive_family" ? "遗潮档案已带回：先让家人核对名字，保留每一份旧册。" : "遗潮档案已带回：公会公开记录，保留每一个名字。", action == "archive_family" ? "Tide Archive recovered: families will verify the names first; every original is preserved." : "Tide Archive recovered: the guild opens a public copy, preserving every name.");
            Notify("遗潮档案完成！硬币50、碎片6、技能点1。", "Tide Archive complete! 50 coins, 6 shards, 1 talent point."); sound = "reward";
            return true;
        }
        public QuestGuide GetArchiveQuest()
        {
            QuestGuide q = new QuestGuide { id = "archive", title = L("遗潮档案", "The Tide Archive"), category = L("遗迹调查", "RUINS INVESTIGATION"),
                description = L("接通旧水路，找回三份记录，决定如何把名字带回家。", "Connect the old waterway, recover three records and decide how to bring the names home."), reward = L("硬币50 · 碎片6 · 技能点1", "50 coins · 6 shards · 1 talent point"),
                locked = !ArchiveReady, complete = ArchiveComplete, room = 2, target = ObjectPoint(2, "portal10", new Point(10, 14)) };
            q.steps.Add(new QuestStep(L("接通三处水轮，排水开门", "Connect the three valves and drain the gate"), ArchiveDrained));
            q.steps.Add(new QuestStep(L("穿过小道，解除水压", "Cross the gallery and release pressure"), ArchiveVented));
            q.steps.Add(new QuestStep(L("找回三份旧册，让守卫平息", "Recover three records and quiet the guardians"), ArchiveRecordsRecovered && ArchiveGuardsCleared));
            q.steps.Add(new QuestStep(L("决定如何送回名字", "Decide how to return the names"), ArchiveComplete));
            if (q.complete) { q.next = L("旧册已保留，名字有了回家的道路", "The records are preserved; the names have a road home"); return q; }
            if (q.locked) { q.next = L("先找回第二枚星核", "Recover the second starcore first"); return q; }
            if (!ArchiveAccepted) { q.next = L("到沉水遗迹入口，调查遗潮档案", "Visit the Tide Archive entrance in Drowned Ruins"); return q; }
            q.room = ArchiveRoom;
            if (!ArchiveDrained)
            {
                q.target = ObjectPoint(ArchiveRoom, "archivePlan", new Point(4, 8)); q.next = L("阅读水路图，再转动三处水轮", "Read the waterworks plan, then turn three valves");
                if (Has("archive_plan")) for (int i = 0; i < 3; i++) if (ArchiveValve(i) != archiveSolution[i])
                { q.target = ArchiveValvePoints[i]; q.next = L("转动水轮 ", "Turn valve ") + (i + 1) + L("，接通两端水路", " to join its two pipe ends"); break; }
                return q;
            }
            if (!ArchiveVented)
            { q.target = ObjectPoint(ArchiveRoom, "archiveVent", new Point(21, 6)); q.next = L("避开亮圈，到远处水轮解除水压", "Avoid warning circles; release the far pressure wheel"); return q; }
            for (int i = 0; i < 3; i++) if (!Has("archive_record_" + i))
            { q.target = ObjectPoint(ArchiveRoom, "archiveRecord" + i, new Point(27, 5)); q.next = L("阅读旧册 ", "Read record ") + (i + 1); return q; }
            if (!ArchiveGuardsCleared)
            {
                for (int i = 0; i < 3; i++) if (!Has("archive_defeated_" + (ArchiveEnemyBase + i)))
                { Enemy e = enemies.Find(actor => actor.id == ArchiveEnemyBase + i); q.target = save.room == ArchiveRoom && e != null ? e.Position : archiveGuardPoints[i]; break; }
                q.next = L("让记录守卫平息，再到档案台交付", "Quiet the record guardians, then report at the archive desk"); return q;
            }
            q.target = ObjectPoint(ArchiveRoom, "archiveKeeper", new Point(31, 19)); q.next = L("到档案台，决定如何送回名字", "Decide how to return the names at the archive desk"); return q;
        }
    }
}
