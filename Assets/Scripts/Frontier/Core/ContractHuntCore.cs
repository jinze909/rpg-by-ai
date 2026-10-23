using System;

namespace Starfall
{
    public sealed partial class GameCore
    {
        private bool ContractApproachable(Point target, float distance)
        {
            if (distance < 1.8f && ClearAttackPath(Position, target)) return true;
            if (NavigationPath(target).Count > 0) return true;
            // NavigationPath legitimately returns no waypoints when its start
            // cell center is already close enough. Prove that local approach
            // with the same swept footprint, rather than calling it unreachable.
            Point center = new Point((float)Math.Floor(Position.x) + .5f, (float)Math.Floor(Position.y) + .5f);
            return CanStand(center) && (center - target).Length <= 1.2f
                && (SweepActor(Position, center - Position) - center).Length < .001f && ClearAttackPath(center, target);
        }
        private Enemy ContractEnemy(int contract)
        {
            Enemy closest = null; float distance = float.MaxValue;
            foreach (Enemy enemy in enemies)
            {
                // These encounters return before OnExpansionKill. Other real
                // task actors (watch, archive and riverbank) do count normally.
                if (enemy == null || !Finite(enemy.hp) || enemy.hp <= 0 || !CanStand(enemy.Position)
                    || IsRangeEnemyId(enemy.id) || IsSparringEnemyId(enemy.id) || IsSignalWarden(enemy) || enemy.id == CommissionGuardianId
                    || contract == 3 && !enemy.boss || contract == 4 && enemy.kind != 2) continue;
                float next = (enemy.Position - Position).Length;
                if (next >= distance) continue;
                if (!ContractApproachable(enemy.Position, next)) continue;
                closest = enemy; distance = next;
            }
            return closest;
        }
        private bool ContractGuardianGate(int room)
        { return room == 4 || room == 1 && Has("scout_saved") || Has("gate_" + room); }
        private void GuideContractGuardian(QuestGuide guide, int room)
        {
            guide.room = room;
            guide.target = ObjectPoint(room, room < 5 ? "shrine" : "expAltar", new Point(29, 8));
            if (ContractGuardianGate(room))
            {
                guide.next = room == save.room && GateBarrierActive ? L("等屏障消散，再到祭坛挑战", "Wait for the barrier, then challenge at the altar")
                    : L("到祭坛开始守护者挑战", "Start the guardian challenge at the altar"); return;
            }
            if (room == 1)
            {
                guide.target = ObjectPoint(room, "scout", guide.target);
                guide.next = L("先清理四名敌人，再解救侦察员", "Defeat four foes, then rescue the scout");
                if (room == save.room && save.kills[room] < 4)
                { Enemy enemy = ContractEnemy(0); if (enemy != null) guide.target = enemy.Position; }
            }
            else if (room == 2)
            {
                int rune = room == save.room ? Math.Min(2, Math.Max(0, runeProgress)) : 0;
                guide.target = ObjectPoint(room, !Has("lore_2") ? "lore" : "rune" + new[] { 0, 2, 1 }[rune], guide.target);
                guide.next = L("先读遗迹记录，再依线索触碰石碑", "Read the record, then solve the tablets");
            }
            else if (room == 3)
            {
                guide.target = ObjectPoint(room, "engine", guide.target);
                guide.next = L("零件3修好升降机，再挑战守护者", "Repair the elevator with 3 parts first");
                if (save.inventory[4] < 3 && GuideToResource(guide, "ore", 3))
                    guide.next = L("先采矿补充零件3，修好升降机", "Gather ore for 3 parts to repair the elevator");
            }
            else if (room == 5)
            {
                guide.target = ObjectPoint(room, "captain", guide.target);
                guide.next = L("先与米拉商定港口方案", "Agree on a harbor plan with Mira first");
                if (save.harborPledge > 0)
                {
                    bool rescue = save.harborPledge == 2 || save.harborPledge == 3 && RescueCount < 2;
                    for (int i = 0; i < 3; i++) if (!Has((rescue ? "rescue_" : "relay_") + i))
                    {
                        guide.target = ObjectPoint(room, (rescue ? "castaway" : "relay") + i, guide.target);
                        guide.next = rescue ? L("先救援乘客，打开灯塔航道", "Rescue passengers to open the lighthouse route")
                            : L("先修复灯线，打开灯塔航道", "Repair relays to open the lighthouse route"); break;
                    }
                }
            }
            else if (room == 6)
            {
                int[] wanted = { 1, 2, 1 }; int i = 0; while (i < 2 && save.mirrors[i] == wanted[i]) i++;
                guide.target = ObjectPoint(room, "mirror" + i, guide.target);
                guide.next = L("先调整镜面，让光到达蓄光池", "Align mirrors to reach the reservoir first");
            }
            else if (room == 7)
            {
                int[] wanted = { 2, 0, 1 }; int i = 0; while (i < 2 && save.bells[i] == wanted[i]) i++;
                guide.target = ObjectPoint(room, "bell" + i, guide.target);
                guide.next = L("先调查钟阵，打开高原道路", "Solve the bells to open the plateau route");
            }
        }
        private void GuideCombatContract(QuestGuide guide, int contract)
        {
            if (practicing)
            {
                guide.room = save.room; guide.target = Position;
                guide.next = L("训练不计悬赏；结束后继续冒险", "Practice does not count; resume your adventure after training"); return;
            }
            // The mature expedition route still owns the east banking gate.
            // Do not turn a contract into an exit that discards unbanked loot.
            if (InAbyss)
            {
                guide.room = 8; guide.target = new Point(31, 14);
                guide.next = contract == 3 && save.abyssFloor == 6 && Has("boss_8") ? L("守护者已平息；清层结算后再开远征", "Guardian resolved; bank this run, then start anew")
                    : contract == 3 && save.abyssFloor < 6 ? L("沿东门深入，第6层有守护者", "Descend through east gates; the guardian waits on floor 6")
                    : contract == 3 ? L("击败本层守护者，再到委托簿领奖", "Defeat this floor's guardian, then claim in Contracts")
                    : L("清理本层目标；东门结算后继续", "Defeat floor targets; bank at the east gate to continue"); return;
            }
            Enemy target = ContractEnemy(contract);
            if (target != null)
            {
                guide.room = save.room; guide.target = target.Position;
                guide.next = contract == 3 ? L("击败眼前守护者", "Defeat the active guardian") : contract == 4 ? L("击败可接近的重甲敌人", "Defeat the nearby heavy foe") : L("击败可接近的敌人", "Defeat the nearby foe"); return;
            }
            if (contract == 3)
            {
                int room = save.room >= 1 && save.room <= 7 && !Has("boss_" + save.room) && CanEnter(save.room) ? save.room : -1;
                if (room < 0) for (int i = 1; i <= 7; i++) if (CanEnter(i) && !Has("boss_" + i)) { room = i; break; }
                if (room >= 0) { GuideContractGuardian(guide, room); return; }
                if (CanEnter(8))
                {
                    guide.room = 0; guide.target = ObjectPoint(0, "abyssEntry", new Point(30, 16));
                    guide.next = L("地区守护者已平息；从裂口挑战第6层", "Guardians resolved; enter the fissure for floor 6"); return;
                }
                QuestGuide story = GetQuest("shore"); if (story.locked) story = GetQuest("main");
                guide.room = story.room; guide.target = story.target;
                guide.steps.Add(new QuestStep(L("继续故事，开放地脉远征", "Continue the story to unlock expeditions"), false));
                guide.next = story.next; return;
            }
            // Enter really repopulates regional placements. A remote guide
            // names that region's entrance, never invents an unseen live actor.
            for (int room = 1; room <= 7; room++) if (room != save.room && CanEnter(room))
            {
                guide.room = room; guide.target = new Point(data.maps[room].spawnX, data.maps[room].spawnY);
                guide.next = contract == 4 ? L("进入地区寻找重甲敌人", "Enter the region and find heavy foes") : L("进入地区寻找敌人", "Enter the region and find foes"); return;
            }
            guide.room = 0; guide.target = ObjectPoint(0, "portal1", new Point(18, 3));
            guide.next = L("本地暂无可接近目标；经余烬镇重访密林", "No local hunt targets; revisit Thornwood through town");
        }
    }
}
