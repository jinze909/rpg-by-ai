using System;

namespace Starfall
{
    // Mira's follow-up uses the surveyed road and the existing companion body.
    // Checkpoints and defeats persist; her movement/actions retain their owners.
    public sealed partial class GameCore
    {
        public const int HomewardEnemyBase = 77000;
        private static readonly Point[] homewardGuardPoints = { new Point(37,32),new Point(42,28),new Point(62,15),new Point(58,11) };
        private static readonly int[] homewardGuardKinds = { 0,1,0,2 };
        private static readonly float[] homewardGuardHealth = { 55,44,55,85 };
        public bool HomewardReady { get { return ReachReady && ReachComplete && Has("personal_0") && Has("companion_0"); } }
        public bool HomewardAccepted { get { return HomewardReady && Has("homeward_accept"); } }
        public bool HomewardComplete { get { return HomewardAccepted && Has("homeward_complete") && HomewardStopComplete(0) && HomewardStopComplete(1) && HomewardRecordsFound; } }
        public bool HomewardPublicChart { get { return HomewardComplete && Has("homeward_public"); } }
        public int HomewardRecordCount { get { return (Has("history_20")||Has("lore_20")?1:0)+(Has("history_21")||Has("lore_21")?1:0); } }
        public bool HomewardRecordsFound { get { return HomewardRecordCount == 2; } }
        public bool IsHomewardEnemyId(int id) { return id>=HomewardEnemyBase && id<HomewardEnemyBase+4; }
        public bool HomewardWaveStarted(int index)
        { return index>=0 && index<2 && HomewardAccepted && Has("homeward_wave_"+index) && (index==0||HomewardStopComplete(0)); }
        public bool HomewardWaveCleared(int index)
        { return HomewardWaveStarted(index) && Has("homeward_defeated_"+(HomewardEnemyBase+index*2)) && Has("homeward_defeated_"+(HomewardEnemyBase+index*2+1)); }
        public bool HomewardStopComplete(int index)
        { return index>=0 && index<2 && HomewardWaveCleared(index) && Has("homeward_stop_"+index); }
        private bool HomewardWorldAction { get { return mode=="world" && !Dying && !practicing && Fits(Position); } }
        private bool NearHomewardCaptain
        { get { return save.room==5 && NearKind("captain",1.75f); } }
        private bool HomewardMiraPresent
        { get { return save.companion==0 && Has("companion_0") && Finite(companionPosition.x) && Finite(companionPosition.y) && Fits(companionPosition) && (companionPosition-Position).Length<=3; } }
        private bool HomewardLocal(string kind)
        {
            if(!HomewardWorldAction||save.room!=ReachRoom||!HomewardAccepted||Has("homeward_complete")||!NearReachObject(kind))return false;
            if(!HomewardMiraPresent)
            { Notify("请在同伴栏邀请米拉，并等她走到附近。","Invite Mira in Companions, then wait for her to arrive.");return false; }
            return true;
        }
        public bool AcceptHomewardRoute()
        {
            if(!HomewardWorldAction||!NearHomewardCaptain||!HomewardReady||Has("homeward_accept"))return false;
            Flag("homeward_accept");TrackQuest("homeward");
            RecordMoment("米拉开始试走旧路河岸的新航线。旧信没有答案，路图仍能交给活着的人。","Mira begins a new route through Reedwater. The old letter has no answer; the chart can still help the living.");
            Notify("在同伴栏邀请米拉，先去河心旧灯。","Invite Mira in Companions, then visit the island lamp.");return true;
        }
        public bool StartHomewardStop(int index)
        {
            if(index<0||index>1||!HomewardLocal("reachPost"+(index+1))||HomewardStopComplete(index)||(index==1&&!HomewardStopComplete(0)))return false;
            if(HomewardWaveStarted(index))return false;
            if(!ReachQuiet(Position)){Notify("先清理旧灯附近的敌人，再与米拉试走。","Clear nearby foes before checking the route with Mira.");return false;}
            Flag("homeward_wave_"+index);RestoreHomewardEncounter();
            Notify("探路开始。清理两名敌人，再与米拉记下这段路；Z可请求援助。","Route check started. Clear both foes, then record it with Mira. Z requests her aid.");sound="windup";return true;
        }
        public bool RecordHomewardStop(int index)
        {
            if(index<0||index>1||!HomewardLocal("reachPost"+(index+1))||HomewardStopComplete(index)||!HomewardWaveCleared(index)||!ReachQuiet(Position))return false;
            Flag("homeward_stop_"+index);
            RecordMoment(index==0?"米拉记下河心的南北两条路。浮桥是捷径，原来的南北路线仍可通行。":"米拉确认东岸旧灯的路线。带上河岸的路图与名册，回营地交付。",
                index==0?"Mira charts the island's two crossings. The footbridge is a shortcut, not the only road home.":"Mira confirms the east lamp's route. Bring the bank's chart and roster back to camp.");
            Notify(index==0?"继续去东岸旧灯，与米拉确认下一段路。":"两段路已确认。找回两份河岸记录，再回营地。",
                index==0?"Visit the east lamp to check the next stretch with Mira.":"Both stretches checked. Recover the two bank records, then return to camp.");return true;
        }
        public bool FinishHomewardRoute(bool publicChart)
        {
            if(!HomewardLocal("campfire")||!HomewardStopComplete(0)||!HomewardStopComplete(1)||!HomewardRecordsFound||!ReachQuiet(Position))return false;
            Flag("homeward_complete");Flag(publicChart?"homeward_public":"homeward_family");
            save.coins+=30;save.shards+=3;save.relationships[0]=Math.Min(5,save.relationships[0]+1);
            RecordMoment(publicChart?"米拉的新航线：公用路图留在营地，名字交给家人。硬币30、碎片3、米拉羁绊提升。":"米拉的新航线：先给家人送路图，营地保留不带名字的路图。硬币30、碎片3、米拉羁绊提升。",
                publicChart?"Mira's new route: the public chart stays at camp; names go to families. 30 coins, 3 shards and Mira's bond grows.":"Mira's new route: family copies go first; an anonymous chart stays at camp. 30 coins, 3 shards and Mira's bond grows.");
            Notify("新航线已记下。路图有了下一位使用它的人。","The new route is recorded. The chart has someone new to use it.");sound="reward";return true;
        }
        private bool HomewardEncounterOpen(int index)
        { return save.room==ReachRoom && HomewardWaveStarted(index) && !HomewardStopComplete(index) && !Has("homeward_complete"); }
        private bool HomewardActorAllowed(Enemy e)
        {
            if(!IsHomewardEnemyId(e.id))return false;int slot=e.id-HomewardEnemyBase;
            return HomewardEncounterOpen(slot/2)&&!e.boss&&e.kind==homewardGuardKinds[slot]&&!Has("homeward_defeated_"+e.id);
        }
        private void RestoreHomewardEncounter()
        {
            if(save.room!=ReachRoom)return;
            enemies.RemoveAll(e=>IsHomewardEnemyId(e.id)&&!HomewardActorAllowed(e));
            for(int slot=0;slot<4;slot++)
            {
                int id=HomewardEnemyBase+slot;
                if(!HomewardEncounterOpen(slot/2)||Has("homeward_defeated_"+id)||enemies.Exists(e=>e.id==id))continue;
                Enemy actor=CreatePlacedEnemy(id,homewardGuardKinds[slot],homewardGuardPoints[slot],homewardGuardHealth[slot]);
                actor.timer=.8f+(slot%2)*.18f;enemies.Add(actor);
            }
        }
        private void RestoreHomewardRecord(EnemyRecord record)
        {
            if(record==null||!IsHomewardEnemyId(record.id)||record.boss||!Finite(record.hp)||record.hp<=0||!Finite(record.x)||!Finite(record.y))return;
            int slot=record.id-HomewardEnemyBase;Point p=new Point(record.x,record.y);
            if(!HomewardEncounterOpen(slot/2)||Has("homeward_defeated_"+record.id)||record.kind!=homewardGuardKinds[slot]||!Fits(p)||((p-Position).Length>.5f&&NavigationPath(p).Count==0))return;
            Enemy actor=CreatePlacedEnemy(record.id,homewardGuardKinds[slot],p,homewardGuardHealth[slot]);
            actor.hp=Clamp(record.hp,.1f,actor.maxHp);actor.timer=.8f;enemies.Add(actor);
        }
        private void OnHomewardKill(Enemy e)
        { if(HomewardActorAllowed(e))Flag("homeward_defeated_"+e.id); }
        private Choice HomewardStopChoice(int index)
        {
            bool started=HomewardWaveStarted(index),done=HomewardStopComplete(index);
            return C(done?"新航线已记下":started?"与米拉记下这段路":"与米拉检查航路",
                done?"New route recorded":started?"Record the route with Mira":"Check the route with Mira",
                done?"close":started?"homeward_report"+index:"homeward_stop"+index);
        }
        private void ShowHomewardOffer()
        {
            if(!HomewardWorldAction||!NearHomewardCaptain||!HomewardReady)return;
            Dialog("米拉 · 新航线","Mira · A new route",
                "名册已经交还。我不再等那封回信。你找回的河岸旧路，也许能让活着的人往返。\n\n邀请我同行，去河心与东岸旧灯确认两段路。清理沿路敌人，找回河岸的两份记录，最后回营地决定路图交给谁。\n\n这不是失踪巡路队回来的证据。首次交付：硬币30、碎片3、羁绊提升。",
                "The manifest is returned. I no longer wait for that reply. Your surveyed bank may give living people a road home.\n\nInvite me along. Check the island and east lamps, clear the foes, recover both bank records, then decide who receives the chart at camp.\n\nThis does not prove the lost patrol returned. First handoff: 30 coins, 3 shards and a stronger bond.",
                C(HomewardAccepted?"查看航线":"接下这条航线",HomewardAccepted?"Track the route":"Accept the route",HomewardAccepted?"homeward_track":"homeward_accept"),
                C("稍后再来","Later","close"));
        }
        private void ShowHomewardCamp()
        {
            if(!HomewardWorldAction||save.room!=ReachRoom||!HomewardAccepted||!NearReachObject("campfire"))return;
            if(HomewardComplete)
            {
                Dialog("米拉 · 营地路图","Mira · The camp chart",HomewardPublicChart?"公用路图已经留在营地。家人的名字仍由家人保管。旧信没有新的答案，但这条路可以继续使用。":"米拉会把路图交给家人，营地保留不带名字的路线。旧信没有新的答案，但这条路可以继续使用。",
                    HomewardPublicChart?"The public chart stays at camp. Families keep the names. The old letter has no new answer, but this road can still be used.":"Mira will take the family copies; an anonymous route stays at camp. The old letter has no new answer, but this road can still be used.",C("收起路图","Close the chart","close"));return;
            }
            Dialog("米拉 · 营地路图","Mira · The camp chart",
                "两段路和两份记录都要确认；交付时，米拉也要在附近。\n\n公用路图让来往的人先有路线可走，名字仍交给家人。也可先给家人送带名字的路图，营地保留不带名字的路线。原记录始终保留。\n\n两种选择报酬相同：硬币30、碎片3、羁绊提升。",
                "Check both stretches and recover both records. Mira must be nearby for the handoff.\n\nShare a public route first, leaving names with families; or deliver named family copies first and leave an anonymous chart at camp. Keep the originals.\n\nBoth choices: 30 coins, 3 shards and a stronger bond.",
                C("留下公用路图","Leave a public chart","homeward_public"),C("先给家人送路图","Family copies first","homeward_family"),C("追踪下一步","Track missing steps","homeward_track"),C("稍后决定","Decide later","close"));
        }
        private bool ChooseHomeward(string action)
        {
            if(action=="homeward_offer"){ShowHomewardOffer();return true;}
            if(action=="homeward_accept"){AcceptHomewardRoute();return true;}
            if(action=="homeward_track"){if(HomewardWorldAction&&HomewardAccepted&&(NearHomewardCaptain||save.room==ReachRoom))TrackQuest("homeward");return true;}
            if(action=="homeward_camp"){ShowHomewardCamp();return true;}
            if(action=="homeward_public"){FinishHomewardRoute(true);return true;}
            if(action=="homeward_family"){FinishHomewardRoute(false);return true;}
            int index;
            if(action.StartsWith("homeward_stop",StringComparison.Ordinal)){if(int.TryParse(action.Substring(13),out index))StartHomewardStop(index);return true;}
            if(action.StartsWith("homeward_report",StringComparison.Ordinal)){if(int.TryParse(action.Substring(15),out index))RecordHomewardStop(index);return true;}
            return false;
        }
        private QuestGuide GetHomewardQuest()
        {
            QuestGuide q=new QuestGuide{id="homeward",room=5,target=ObjectPoint(5,"captain",new Point(15,16)),title=L("米拉的新航线","Mira's new route"),category=L("同伴故事","COMPANION"),description=L("陪米拉确认河岸的道路，把路图与名字带回营地。","Check the river road with Mira; bring the chart and names back to camp."),reward=L("硬币30 / 碎片3 / 米拉羁绊+1","30 coins / 3 shards / Mira bond +1"),locked=!HomewardReady,complete=HomewardComplete};
            q.steps.Add(new QuestStep(L("河心旧灯","Island lamp"),HomewardStopComplete(0)));
            q.steps.Add(new QuestStep(L("东岸旧灯","East-bank lamp"),HomewardStopComplete(1)));
            q.steps.Add(new QuestStep(L("河岸记录 ","Bank records ")+HomewardRecordCount+"/2",HomewardRecordsFound));
            q.steps.Add(new QuestStep(L("回营地交付路图","Hand off the chart at camp"),HomewardComplete));
            if(!HomewardReady){q.next=L("完成米拉的名册故事与旧路河岸巡查","Complete Mira's manifest story and the Reedwater survey");return q;}
            if(!HomewardAccepted){q.next=L("到雾港找米拉，接下新航线","Visit Mira in Mistport to accept the new route");return q;}
            q.room=ReachRoom;
            int stop=!HomewardStopComplete(0)?0:!HomewardStopComplete(1)?1:-1;
            if(stop>=0)
            {
                q.target=ReachPosts[stop+1];
                q.next=save.companion!=0?L("在同伴栏邀请米拉，再去旧灯","Invite Mira in Companions, then visit the lamp"):HomewardWaveStarted(stop)&&!HomewardWaveCleared(stop)?L("清理两名探路敌人，再回旧灯记录","Clear both route foes, then return to record the stretch"):HomewardWaveCleared(stop)?L("等米拉到附近，再按E记下路图","Wait for Mira nearby, then press E to record"):L("与米拉靠近旧灯，按E开始探路","Approach the lamp with Mira; press E to start");return q;
            }
            if(!HomewardRecordsFound)
            {int lore=Has("history_20")||Has("lore_20")?21:20;q.target=ObjectPoint(ReachRoom,"history"+lore,new Point(lore==20?24:65,lore==20?10:18));q.next=L("找回河岸旧记录，保留路图与名字","Recover the bank records; preserve the chart and names");return q;}
            q.target=ObjectPoint(ReachRoom,"campfire",new Point(11,38));q.next=HomewardComplete?L("路图已交付，可在营地重读","Chart delivered; reread it at camp"):L("与米拉回营地，决定路图交给谁","Return to camp with Mira and choose the chart's recipients");return q;
        }
    }
}
