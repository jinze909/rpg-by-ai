using System;
using System.Collections.Generic;

namespace Starfall
{
    public sealed partial class GameCore
    {
        private static readonly string[] FieldcraftFlagNames = { "fieldcraft_sword", "fieldcraft_bow", "fieldcraft_staff" };
        // Witnesses belong to a real basic action, never to the currently held gear.
        // Only the finished demonstrations are saved; flying shots and their contacts are not.
        private sealed class FieldcraftRecord
        {
            public int weapon, style, room, generation;
            public bool counter;
            public float expires;
            public readonly HashSet<Enemy> contacts = new HashSet<Enemy>();
        }
        private readonly Dictionary<ImpactProfile, FieldcraftRecord> fieldcraftActions = new Dictionary<ImpactProfile, FieldcraftRecord>();
        private readonly List<ImpactProfile> expiredFieldcraftActions = new List<ImpactProfile>();
        public bool FieldcraftReady { get { return Has("core_1"); } }
        public bool FieldcraftAccepted { get { return FieldcraftReady && Has("fieldcraft_accepted"); } }
        public bool FieldcraftComplete { get { return FieldcraftAccepted && Has("fieldcraft_reported"); } }
        public bool FieldcraftRecorded(int weapon)
        { return FieldcraftAccepted && weapon >= 0 && weapon < 3 && Has(FieldcraftFlagNames[weapon]); }
        public int FieldcraftProgress { get { int count = 0; for (int i = 0; i < 3; i++) if (FieldcraftRecorded(i)) count++; return count; } }
        public bool FieldcraftDemonstrationsComplete { get { return FieldcraftProgress == 3; } }
        private bool FieldcraftWorldAction { get { return mode == "world" && !Dying && !practicing && Finite(save.health) && save.health > 0; } }
        private bool FieldcraftCombatRoom { get { return save.room >= 1 && save.room <= 8 || save.room == ArchiveRoom || save.room == ReachRoom; } }
        private bool FieldcraftLocalMentor()
        {
            if (!NearSparringMentor() || !Fits(Position)) return false;
            foreach (WorldObject o in Map.objects)
                if (o.kind == "mentor" && IsObjectPresent(o) && (Position - new Point(o.x, o.y)).Length < 1.75f
                    && ClearAttackPath(Position, new Point(o.x, o.y))) return true;
            return false;
        }
        private void ClearFieldcraftTransient()
        { fieldcraftActions.Clear(); expiredFieldcraftActions.Clear(); }
        private void PruneFieldcraftActions()
        {
            expiredFieldcraftActions.Clear();
            foreach (var pair in fieldcraftActions)
                if (!Finite(pair.Value.expires) || pair.Value.expires <= save.totalTime || pair.Value.room != save.room || pair.Value.generation != WorldGeneration)
                    expiredFieldcraftActions.Add(pair.Key);
            foreach (ImpactProfile profile in expiredFieldcraftActions) fieldcraftActions.Remove(profile);
            expiredFieldcraftActions.Clear();
        }
        private void RegisterFieldcraftAction(ActorAction action)
        {
            if (action == null || action.kind != "attack" || action.impact == null || !action.impact.playerOwned
                || !FieldcraftWorldAction || !FieldcraftCombatRoom || !FieldcraftAccepted || FieldcraftDemonstrationsComplete || !Finite(save.totalTime)) return;
            PruneFieldcraftActions();
            if (fieldcraftActions.Count >= 32) return;
            ImpactProfile impact = action.impact;
            fieldcraftActions[impact] = new FieldcraftRecord { weapon = impact.weapon, style = impact.style, counter = impact.counter,
                expires = save.totalTime + 5, room = save.room, generation = WorldGeneration };
        }
        private void OnFieldcraftContact(Enemy enemy, ImpactProfile impact, float healthBefore)
        {
            if (!FieldcraftWorldAction || !FieldcraftCombatRoom || !FieldcraftAccepted || FieldcraftDemonstrationsComplete
                || impact == null || !impact.playerOwned || enemy == null || IsSparringEnemyId(enemy.id) || !enemies.Contains(enemy)
                || !Finite(healthBefore) || healthBefore <= 0 || !Finite(enemy.hp) || !Finite(healthBefore - enemy.hp) || healthBefore - enemy.hp <= 0) return;
            PruneFieldcraftActions(); FieldcraftRecord record;
            if (!fieldcraftActions.TryGetValue(impact, out record) || FieldcraftRecorded(record.weapon)) return;
            if (record.weapon == 0 && !record.counter || record.weapon == 1 && record.style != 1 || record.weapon < 0 || record.weapon > 2) return;
            if (!record.contacts.Add(enemy)) return;
            int target = record.weapon == 0 ? 1 : record.weapon == 1 ? 2 : 3;
            if (record.contacts.Count < target) return;
            Flag(FieldcraftFlagNames[record.weapon]);
            string zh = new[] { "短剑反击", "猎弓贯穿", "星火范围命中" }[record.weapon];
            string en = new[] { "Sword counter", "Bow piercing shot", "Staff blast" }[record.weapon];
            RecordMoment("实战记录：" + zh + "。", "Field note: " + en + ".");
            Notify(FieldcraftDemonstrationsComplete ? "三项实战记录已完成，回镇找艾妲交付。" : "实战记录已完成：" + zh + "。",
                FieldcraftDemonstrationsComplete ? "All three field notes are complete. Report to Ada in town." : "Field note recorded: " + en + ".");
            if (FieldcraftDemonstrationsComplete) ClearFieldcraftTransient();
        }
        public int FieldcraftContactCount(int weapon)
        {
            if (weapon < 0 || weapon > 2 || !FieldcraftAccepted) return 0;
            int target = weapon == 0 ? 1 : weapon == 1 ? 2 : 3;
            if (FieldcraftRecorded(weapon)) return target;
            int best = 0;
            foreach (FieldcraftRecord record in fieldcraftActions.Values)
                if (record.weapon == weapon && record.expires > save.totalTime && record.room == save.room && record.generation == WorldGeneration
                    && (weapon != 0 || record.counter) && (weapon != 1 || record.style == 1)) best = Math.Max(best, record.contacts.Count);
            return Math.Min(target, best);
        }
        public bool AcceptFieldcraft()
        {
            if (!FieldcraftWorldAction || !FieldcraftReady || FieldcraftAccepted || !FieldcraftLocalMentor()) return false;
            ClearFieldcraftTransient(); Flag("fieldcraft_accepted"); TrackQuest("fieldcraft");
            RecordMoment("艾妲请你把训练带到路上，记录三种武器的实战用途。", "Ada asks you to take the lessons onto the road and record three weapons in action.");
            Notify("实战记录已接取。普通攻击才计入，训练不计入。", "Field notes accepted. Basic attacks in the world count; practice does not."); return true;
        }
        public bool FinishFieldcraft()
        {
            if (!FieldcraftWorldAction || !FieldcraftAccepted || FieldcraftComplete || !FieldcraftDemonstrationsComplete || !FieldcraftLocalMentor()
                || enemies.Exists(e => e.boss || (e.Position - Position).Length < 5)) return false;
            Flag("fieldcraft_reported"); ClearFieldcraftTransient(); save.coins += 40; save.shards += 5; save.skillPoints++;
            RecordMoment("艾妲收下三份实战记录：让每把武器在合适的地方出手。硬币40、碎片5、技能点1。",
                "Ada accepts the three field notes: choose the right weapon for the moment. 40 coins, 5 shards and 1 talent point.");
            Notify("实战记录已交付：硬币40、碎片5、技能点1。", "Field notes delivered: 40 coins, 5 shards, 1 talent point."); sound = "reward"; return true;
        }
        private void ShowFieldcraft()
        {
            if (!FieldcraftWorldAction || !FieldcraftLocalMentor()) return;
            if (!FieldcraftReady)
            { Dialog("艾妲 · 实战记录", "Ada · Field notes", "找回第一枚星核后，再来把训练带到路上。原有练习与战术训练仍可使用。", "Recover the first starcore, then take the lessons onto the road. The original practice and sparring remain available.", C("知道了", "Understood", "close")); return; }
            string zh = "艾妲：在训练场学会出手，再到路上找到每把武器的用途。接取后，记录三项普通攻击；职业技能、同伴与训练命中不计入。\n\n短剑：闪避穿过攻击，出现反击提示后，用普通攻击命中一名敌人。\n猎弓：选择蓄势打法，让同一发贯穿箭命中两名敌人。\n星火杖：同一次星火爆炸命中三名敌人，直接命中的目标也计入。\n\n第一枚星核解锁打法。铁匠制作猎弓需木材4、零件2；星火杖需星晶4、零件3。可以自由换武器，职业不变。\n\n完成后回到这里：硬币40、碎片5、技能点1，只领取一次。";
            string en = "Ada: learn the swing here, then see what it can do on the road. After accepting, record three basic attacks. Class skills, companions and practice do not count.\n\nSword: dodge through an attack; when COUNTER appears, land a basic hit.\nBow: choose Focused style; hit two foes with one piercing arrow.\nStarfire staff: hit three foes with one blast, including the direct target.\n\nThe first core unlocks styles. The blacksmith crafts a bow for 4 timber and 2 parts, or a staff for 4 crystals and 3 parts. Switch weapons freely; your class stays the same.\n\nReturn here when done: 40 coins, 5 shards and 1 talent point, once.";
            zh += "\n\n记录 " + FieldcraftProgress + "/3"; en += "\n\nNotes " + FieldcraftProgress + "/3";
            var choices = new List<Choice>();
            if (!FieldcraftAccepted) choices.Add(C("接取实战记录", "Accept field notes", "fieldcraft_accept"));
            else if (FieldcraftDemonstrationsComplete && !FieldcraftComplete) choices.Add(C("交付实战记录", "Deliver field notes", "fieldcraft_report"));
            else if (!FieldcraftComplete) choices.Add(C("追踪实战记录", "Track field notes", "fieldcraft_track"));
            if (FieldcraftComplete) { zh += "\n艾妲：记录已经收好了。愿你在路上记得，武器是为了让人平安回来。"; en += "\nAda: the notes are safely filed. Remember why you carry a weapon: to bring people home."; }
            choices.Add(C("继续冒险", "Keep adventuring", "close"));
            Dialog("艾妲 · 实战记录", "Ada · Field notes", zh, en, choices.ToArray());
        }
        private bool ChooseFieldcraft(string action)
        {
            if (action == null || !action.StartsWith("fieldcraft_", StringComparison.Ordinal)) return false;
            if (action == "fieldcraft_offer") ShowFieldcraft();
            else if (action == "fieldcraft_accept") AcceptFieldcraft();
            else if (action == "fieldcraft_report") FinishFieldcraft();
            else if (action == "fieldcraft_track" && FieldcraftWorldAction && FieldcraftAccepted && FieldcraftLocalMentor()) TrackQuest("fieldcraft");
            return true;
        }
        private QuestGuide GetFieldcraftQuest()
        {
            var q = new QuestGuide { id = "fieldcraft", title = L("带到路上的训练", "Lessons for the road"), category = L("实战记录", "FIELD NOTES"),
                description = L("把艾妲的训练用在真实敌人身上，记录三把武器各自的用途。", "Take Ada's lessons into real encounters and record what each weapon does well."),
                reward = L("硬币40 · 碎片5 · 技能点1", "40 coins · 5 shards · 1 talent point"), locked = !FieldcraftReady,
                complete = FieldcraftComplete, room = 0, target = ObjectPoint(0, "mentor", new Point(11, 14)) };
            q.steps.Add(new QuestStep(L("与艾妲接取实战记录", "Accept field notes from Ada"), FieldcraftAccepted));
            q.steps.Add(new QuestStep(L("短剑普通攻击：闪避反击命中", "Sword basic attack: land a dodge-counter"), FieldcraftRecorded(0)));
            q.steps.Add(new QuestStep(L("蓄势猎弓：同一发箭命中两名敌人", "Focused bow: one arrow hits two foes"), FieldcraftRecorded(1)));
            q.steps.Add(new QuestStep(L("星火杖：同一次爆炸命中三名敌人", "Staff: one blast hits three foes"), FieldcraftRecorded(2)));
            q.steps.Add(new QuestStep(L("返回艾妲处交付", "Report back to Ada"), FieldcraftComplete));
            if (!FieldcraftReady) q.next = L("先找回第一枚星核", "Recover the first starcore first");
            else if (!FieldcraftAccepted) q.next = L("到镇中训练场找艾妲接取", "Meet Ada at the town training yard");
            else if (FieldcraftDemonstrationsComplete) q.next = FieldcraftComplete ? L("记录已交付，自由探索边境", "Notes delivered; explore the frontier") : L("回镇找艾妲交付三份记录", "Return to Ada with all three field notes");
            else
            {
                int missing = !FieldcraftRecorded(0) ? 0 : !FieldcraftRecorded(1) ? 1 : 2;
                if (missing > 0 && !Has("weapon_" + missing))
                {
                    q.target = ObjectPoint(0, "forge", new Point(12, 14));
                    q.next = missing == 1 ? L("找铁匠制作猎弓：木材4、零件2", "Craft a bow at the forge: 4 timber, 2 parts") : L("找铁匠制作星火杖：星晶4、零件3", "Craft a staff at the forge: 4 crystals, 3 parts");
                }
                else
                {
                    Enemy target = FieldcraftCombatRoom ? enemies.Find(e => e.hp > 0 && !IsSparringEnemyId(e.id)) : null;
                    if (target != null) { q.room = save.room; q.target = target.Position; }
                    else { q.room = 1; q.target = ObjectPoint(1, "exit", new Point(data.maps[1].spawnX, data.maps[1].spawnY)); }
                    q.next = missing == 0 ? L("闪避真实攻击后，用短剑普攻反击", "Dodge a hit; land a sword counter.")
                        : missing == 1 ? L("蓄势猎弓：同一发箭贯穿两名敌人", "Focused bow: pierce two foes in one shot.")
                        : L("星火杖：一次爆炸命中三名敌人", "Staff: one blast hits three foes.");
                }
            }
            return q;
        }
    }
}
