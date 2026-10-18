using System;

namespace Starfall
{
    // An optional room interaction, not a replacement for any of the six floors.
    // Only the resolved choice is saved; reloading cancels an unfinished pulse.
    public sealed partial class GameCore
    {
        public static readonly Point RootEchoPoint = new Point(7, 9);
        public const float RootEchoLeash = 4.5f;
        public const float RootEchoWarning = 1.25f;
        private const float RootEchoRecovery = .65f;
        public bool RootEchoActive { get; private set; }
        public int RootEchoStep { get; private set; }
        public float RootEchoTimer { get; private set; }
        public bool RootEchoAvailable { get { return InAbyss && (save.abyssFloor == 2 || save.abyssFloor == 4); } }
        public bool RootEchoResolved { get { return RootEchoAvailable && Has("abyss_echo_" + save.abyssFloor); } }
        public int RootEchoShards { get { return 3 + save.abyssRisk; } }
        private bool NearRootEcho { get { return RootEchoAvailable && (Position - RootEchoPoint).Length < 1.75f; } }
        private bool RootEchoReady { get { return NearRootEcho && !RootEchoResolved && !practicing && !Dying
            && enemies.Count == 0 && Has("abyss_clear_" + save.abyssFloor); } }

        public string RootEchoStatus
        {
            get
            {
                string instruction = RootEchoStep == 0 ? L("离开锁定光圈", "Leave the locked circle")
                    : RootEchoStep == 1 ? L("双圈之间安全", "The middle gap is safe") : L("四圈之间安全", "Use the gaps between circles");
                return L("回声 ", "ECHO ") + (RootEchoStep + 1) + "/3 · "
                    + Math.Max(0, RootEchoTimer - RootEchoRecovery).ToString("0.0") + "s\n" + instruction;
            }
        }

        private void ResetRootEcho()
        {
            RootEchoActive = false; RootEchoStep = 0; RootEchoTimer = 0;
            hazards.RemoveAll(h => h.kind == "echo");
        }
        private void EmitRootEcho()
        {
            float damage = 12 + save.abyssRisk * 4;
            if (RootEchoStep == 0) AddHazard(Position, 1.3f, RootEchoWarning, damage, "echo");
            else if (RootEchoStep == 1)
            {
                AddHazard(RootEchoPoint + new Point(-1.9f, 0), 1.15f, RootEchoWarning, damage, "echo");
                AddHazard(RootEchoPoint + new Point(1.9f, 0), 1.15f, RootEchoWarning, damage, "echo");
            }
            else for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2)
                AddHazard(RootEchoPoint + new Point(x * 1.8f, y * 1.8f), 1.05f, RootEchoWarning, damage, "echo");
            RootEchoTimer = RootEchoWarning + RootEchoRecovery;
            sound = "windup";
        }
        private void TickRootEcho(float dt)
        {
            if (!RootEchoActive) return;
            if (!RootEchoAvailable || RootEchoResolved || practicing || enemies.Count > 0 || Dying)
            { ResetRootEcho(); return; }
            if (mode != "world" || !Finite(dt) || dt <= 0) return;
            if ((Position - RootEchoPoint).Length > RootEchoLeash)
            {
                ResetRootEcho(); Notify("已离开回声范围，可回到根系重新开始。", "Echo cancelled. Return to the root to retry."); return;
            }
            RootEchoTimer = Math.Max(0, RootEchoTimer - dt);
            // Let the actual shared hazard path resolve before advancing/rewarding.
            if (RootEchoTimer > 0 || hazards.Exists(h => h.kind == "echo")) return;
            RootEchoStep++;
            if (RootEchoStep < 3) { EmitRootEcho(); return; }
            RootEchoActive = false; Flag("abyss_echo_" + save.abyssFloor);
            save.runCoins += 20; save.runShards += RootEchoShards;
            effects.Add(new Effect("discover", RootEchoPoint, .6f));
            RecordMoment("地脉回声已稳定；带着记录去东门结算。", "The root echo is stable. Carry its record to the east gate.");
            Notify("回声完成：未结算硬币20、碎片" + RootEchoShards + "。到东门结算后保留。",
                "Echo complete: 20 coins and " + RootEchoShards + " shards unbanked. Keep them by banking at the east gate.");
        }
        private bool TalkRootEcho(WorldObject o)
        {
            if (o == null || o.kind != "abyssEcho") return false;
            if (!NearRootEcho || Dying || practicing) return true;
            if (RootEchoResolved)
            {
                Dialog("地脉回声", "The root echo", "这处根系已经回应了你的选择。本层回声不会再次提供补给；东门仍通向更深处，西侧仍可返回。",
                    "This root remembers your choice. It offers no second reward on this floor. The east gate leads deeper; the west exit still leads home.", C("继续远征", "Keep exploring", "close")); return true;
            }
            if (RootEchoActive)
            {
                Dialog("地脉回声", "The root echo", "挑战在交谈时暂停。继续观察三轮光圈，或取消后再来；取消没有奖励，不影响本层战利品。",
                    "The trial pauses during this conversation. Continue reading its three pulses, or cancel and retry later. Cancelling grants no reward and leaves this floor's loot intact.",
                    C("继续挑战", "Continue the trial", "close"), C("取消本次挑战", "Cancel this trial", "echo_cancel")); return true;
            }
            if (!RootEchoReady)
            { Notify("清理本层敌人后，根系会提供补给或挑战。", "Clear this floor first; the root will offer rest or a trial."); return true; }
            Dialog("地脉回声", "The root echo",
                "地层留下了旧日调查者的脚步。你可以取走一次补给，回复最大生命的25%；也可以自愿开始三轮回声。两者只能选择一种，可直接跳过。\n\n第一轮光圈锁定起步位置，立刻离开；第二轮双圈留出中路；第三轮四圈留出中间空隙。每轮预告1.25秒，之后有移动空隙。留在根系4.5格内，走远即取消，可无损重试。伤害随远征风险提高。\n\n挑战完成：额外硬币20、碎片" + RootEchoShards + "，计入本层未结算收益。倒下或从西侧离开仍会失去本层收益。读档会取消未完成的挑战，已作出的选择保留。",
                "Old surveyors left their footsteps in the stone. Take a single supply to recover 25% of maximum health, or freely attempt three echo pulses. Choose only one; both are optional.\n\nThe first circle locks your starting position: move out. The paired circles leave a middle lane. Four circles then leave a cross-shaped gap. Each warns for 1.25 seconds, followed by a movement window. Stay within 4.5 tiles of the root; leaving cancels the trial for a free retry. Higher expedition risk raises its damage.\n\nFinish for 20 extra coins and " + RootEchoShards + " shards in this floor's unbanked loot. Falling or leaving west still loses that loot. Loading cancels unfinished pulses; resolved choices remain.",
                C("开始三轮回声", "Begin the three pulses", "echo_begin"),
                C("领取补给 · 生命+25%", "Take supplies · +25% health", "echo_rest"), C("跳过，继续远征", "Skip and keep exploring", "close"));
            return true;
        }
        private bool ChooseRootEcho(string action)
        {
            if (action != "echo_begin" && action != "echo_rest" && action != "echo_cancel") return false;
            if (!NearRootEcho || Dying || practicing) return true;
            if (action == "echo_cancel") { ResetRootEcho(); return true; }
            if (!RootEchoReady || RootEchoActive) return true;
            if (action == "echo_rest")
            {
                Flag("abyss_echo_" + save.abyssFloor);
                save.health = Math.Min(save.maxHealth, save.health + save.maxHealth * .25f);
                effects.Add(new Effect("heal", Position, .5f)); sound = "reward";
                Notify("回声补给已领取：生命回复25%。", "Echo supplies taken: recovered 25% health."); return true;
            }
            RootEchoActive = true; RootEchoStep = 0; EmitRootEcho();
            Notify("第一轮已锁定：离开光圈。", "First pulse locked: move out of its circle.");
            return true;
        }
    }
}
