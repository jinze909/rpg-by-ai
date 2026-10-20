namespace Starfall
{
    public sealed partial class GameCore
    {
        private bool NearSignalContact()
        {
            if (NearWatchScout()) return true;
            if (save.room != 1 || !WatchLampLit(2)) return false;
            foreach (WorldObject o in Map.objects)
                if (o.kind == "watchSignal2" && IsObjectPresent(o) && (Position - new Point(o.x, o.y)).Length < 1.75f) return true;
            return false;
        }

        public void ShowSignalTrial()
        {
            if (!SignalReady || !NearSignalContact() || practicing || Dying) return;
            if (SignalComplete)
            {
                if (SignalRepriseActive) { ShowSignalReprise(); return; }
                Dialog("旧路守望", "The Signal Warden", "旧灯中的守望者收起了武器。它守着的是根系之间的路，不是对森林的征服。巡路队已经不在，回家的人仍会看见他们留下的光。", "The warden in the old light lowers its weapon. It guards a path between the roots, rather than a claim over the forest. The patrol is gone, but travelers still see the light it left behind.", C("让灯继续照路", "Keep the signal burning", "close"), C("旧路复试 · 无重复报酬", "Warden reprise · no repeat reward", "signal_reprise_menu"));
                return;
            }
            string zh = "三处旧灯相连，留下了巡路队守望者的回声。它不再伤害森林，只愿试一试下一个守路的人。去密林第三处灯，自愿开始挑战；主线不需要完成这场战斗。\n\n它依次使出三招：直线冲锋锁定方向后，向侧面闪开；两处地面光圈亮起时，中间留有空隙；四发散射留出正面中路，观察弹道再接近。每招之后都有反击空隙。\n\n胜利自动获得碎片6、技能点1，报酬只领取一次。离开密林或倒下会结束本次挑战，可回第三处灯重新开始。";
            string en = "The three signals carry an echo of the patrol's old warden. It harms no forest; it asks whether the next traveler can guard the road. At the third forest signal, begin its optional trial when you are ready. The main story does not require this fight.\n\nIts three moves repeat in order: step sideways after the straight rush locks its direction; the paired ground circles leave a gap between them; four fan shots leave the front center lane open, so read their paths before approaching. Each move offers time to counterattack.\n\nVictory awards 6 shards and 1 talent point automatically, once. Leaving the forest or falling ends the attempt. Return to the third signal to try again.";
            if (save.room == 1 && !SignalActive)
                Dialog("旧路守望", "The Signal Warden", zh, en, C("开始守望挑战", "Begin the warden trial", "signal_start"), C("追踪旧路守望", "Track the warden", "signal_track"), C("先做准备", "Prepare first", "close"));
            else
                Dialog("旧路守望", "The Signal Warden", zh, en, C("追踪旧路守望", "Track the warden", "signal_track"), C("先做准备", "Prepare first", "close"));
        }

        public bool TalkSignal(WorldObject o)
        {
            if (o == null || o.kind != "watchSignal2" || save.room != 1 || !WatchLampLit(2) || !SignalReady) return false;
            ShowSignalTrial();
            return true;
        }

        public bool ChooseSignal(string action)
        {
            if (ChooseSignalReprise(action)) return true;
            if (action == "signal_menu") { ShowSignalTrial(); return true; }
            if (action != "signal_track" && action != "signal_start") return false;
            if (!SignalReady || SignalComplete || !NearSignalContact() || practicing || Dying) return true;
            if (action == "signal_start") { StartSignalEncounter(); return true; }
            TrackQuest("signal");
            Notify("去密林第三处旧灯，准备好后开始守望挑战。", "Visit the third forest signal and begin the warden trial when ready.");
            return true;
        }

        public QuestGuide GetSignalQuest()
        {
            QuestGuide q = new QuestGuide { id = "signal", title = L("旧路守望", "The Signal Warden"), category = L("守路挑战", "WARDEN TRIAL"),
                description = L("巡路队的回声在第三处旧灯中守着根系之间的路。观察三招，在反击空隙中完成自愿的挑战。", "The patrol's echo guards a path between the roots at the third signal. Read its three moves and use their openings in an optional trial."),
                reward = L("碎片6 · 技能点1 · 胜利自动领取", "6 shards · 1 talent point · awarded on victory"), locked = !SignalReady, complete = SignalComplete, room = 1, target = SignalPoint };
            q.steps.Add(new QuestStep(L("完成最后一班巡路并交付", "Complete and report The Last Watch"), SignalReady));
            q.steps.Add(new QuestStep(L("在第三处旧灯开始守望挑战", "Begin the trial at the third signal"), Has("signal_started")));
            q.steps.Add(new QuestStep(L("观察三招并完成挑战", "Read the three moves and finish the trial"), SignalComplete));
            if (q.complete) { q.next = L("守望者收起武器，旧灯继续照路", "The warden rests; the signal still guides travelers"); return q; }
            if (q.locked)
            {
                q.room = 0; q.target = ObjectPoint(0, "returnscout", new Point(13, 17));
                q.next = L("先完成巡路委托，再与侦察员交谈", "Finish the patrol request, then ask the scout about the signals"); return q;
            }
            Enemy warden = enemies.Find(IsSignalWarden);
            if (SignalActive && warden != null)
            {
                q.target = warden.Position;
                q.next = L("侧闪冲锋，走双圈中间，沿散射中路反击", "Sidestep the rush; use the gaps between circles and fan shots"); return q;
            }
            q.next = Has("signal_started") ? L("回第三处旧灯，自愿重新开始挑战", "Return to the third signal to try the trial again") : L("与第三处旧灯交互，了解招式并开始挑战", "Read the rules at the third signal and begin the trial");
            return q;
        }
    }
}
