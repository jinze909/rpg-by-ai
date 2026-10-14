using System;
using System.Collections.Generic;

namespace Starfall
{
    // An appended, larger exploration region keeps mature map coordinates intact.
    public sealed partial class GameCore
    {
        public const int ReachRoom = 11;
        private const int ReachEnemyBase = 76000;
        public static readonly Point[] ReachPosts = { new Point(17,16), new Point(39,29), new Point(61,12) };
        private static readonly Point[] reachGuardPoints = { new Point(17,20),new Point(19,15),new Point(37,32),new Point(42,28),new Point(62,15),new Point(58,11),new Point(51,7) };
        private static readonly int[] reachGuardKinds = { 0,1,2,0,1,2,0 };
        private static readonly float[] reachGuardHealth = { 55,44,85,55,44,85,55 };
        public bool ReachReady { get { return Has("core_1"); } }
        public bool ReachVisited { get { return Has("reach_visit"); } }
        public bool ReachComplete { get { return Has("reach_complete"); } }
        public bool ReachBridgeOpen { get { return Has("reach_bridge"); } }
        public bool ReachRootsOpen { get { return Has("reach_roots"); } }
        public bool ReachCacheRecovered { get { return Has("reach_cache"); } }
        public bool ReachPostRecorded(int index) { return index >= 0 && index < 3 && Has("reach_post_"+index); }
        public bool IsReachEnemyId(int id) { return id >= ReachEnemyBase && id < ReachEnemyBase + reachGuardPoints.Length; }
        private bool ReachWorldAction { get { return save.room == ReachRoom && ReachReady && mode == "world" && !Dying && !practicing; } }
        private bool NearReachObject(string kind,float distance=1.75f)
        { foreach(WorldObject o in Map.objects) if(o.kind==kind && IsObjectPresent(o) && (Position-new Point(o.x,o.y)).Length<distance) return true; return false; }
        private bool ReachQuiet(Point point) { return !enemies.Exists(e=>(e.Position-point).Length<5); }
        private static bool ReachBridgeCell(int x,int y) { return x>=30&&x<=33&&y>=28&&y<=30; }
        private static bool ReachRootCell(int x,int y) { return x>=45&&x<=47&&y>=11&&y<=13; }
        private bool ReachClosedCell(int x,int y)
        { return save.room==ReachRoom && ((!ReachBridgeOpen&&ReachBridgeCell(x,y))||(!ReachRootsOpen&&ReachRootCell(x,y))); }
        private void VisitReach()
        {
            if(save.room!=ReachRoom || !ReachReady || ReachVisited) return;
            Flag("reach_visit"); TrackQuest("reach");
            RecordMoment("走进旧路河岸：找回三处旧灯的路线，看看回家的人还能走哪条路。", "Entered Reedwater Reach: survey three old lamps and recover a road home.");
        }
        public bool RecordReachPost(int index)
        {
            if(!ReachWorldAction||index<0||index>2||!NearReachObject("reachPost"+index)||ReachPostRecorded(index)) return false;
            if(!ReachQuiet(ReachPosts[index])) { Notify("先清理旧灯旁的敌人，再记录路线。", "Clear the foes beside the lamp before surveying."); return false; }
            Flag("reach_post_"+index);
            RecordMoment("河岸旧灯 "+(index+1)+" 的路线已记录。", "Reedwater lamp "+(index+1)+" surveyed.");
            Notify(ReachPostRecorded(0)&&ReachPostRecorded(1)&&ReachPostRecorded(2)?"三处路线已记录，返回入口巡查图交付。":"路线已记录。继续调查其他旧灯，也可找隐藏旧箱。", ReachPostRecorded(0)&&ReachPostRecorded(1)&&ReachPostRecorded(2)?"All routes surveyed. Report at the entrance board.":"Route surveyed. Find the other lamps and the hidden cache."); return true;
        }
        public bool OpenReachBridge()
        {
            if(!ReachWorldAction||ReachBridgeOpen||!NearReachObject("reachBridge")) return false;
            Flag("reach_bridge"); sound="quest";
            RecordMoment("旧路浮桥已经放下，河心的往返路线更短了。", "The old footbridge is lowered, shortening the island crossing.");
            Notify("浮桥已放下，可以直接往返河心。", "Bridge lowered. The island shortcut is open."); return true;
        }
        public bool ClearReachRoots()
        {
            if(!ReachWorldAction||ReachRootsOpen||!Ability(1)||!NearReachObject("reachRoot",3)) return false;
            Flag("reach_roots"); effects.Add(new Effect("discover",new Point(46,12),.6f));
            RecordMoment("藤封已经打开，河岸北侧的旧箱可以找回了。", "The roots open a path to the old cache on the northern bank.");
            Notify("藤封已打开，沿北侧小路调查。", "Roots released. Follow the northern path."); return true;
        }
        public bool RecoverReachCache()
        {
            if(!ReachWorldAction||!ReachRootsOpen||ReachCacheRecovered||!NearReachObject("reachCache")) return false;
            if(!ReachQuiet(new Point(52,7))) { Notify("先清理旧箱旁的敌人。", "Clear the foe beside the cache first."); return false; }
            Flag("reach_cache"); save.coins+=20; save.shards+=2; save.inventory[6]++;
            RecordMoment("找回河岸旧箱：硬币20、碎片2、炖汤1。", "Recovered the riverbank cache: 20 coins, 2 shards, 1 stew."); sound="reward"; return true;
        }
        public bool ClaimReachSurvey()
        {
            if(!ReachWorldAction||ReachComplete||!NearReachObject("reachBoard")||!ReachPostRecorded(0)||!ReachPostRecorded(1)||!ReachPostRecorded(2)||!ReachQuiet(Position)) return false;
            Flag("reach_complete"); save.coins+=40; save.shards+=4; save.skillPoints++;
            RecordMoment("河岸巡查完成：旧灯之间的回家路线已找回。硬币40、碎片4、技能点1。", "Reedwater survey complete: the road between the old lamps is charted. 40 coins, 4 shards, 1 talent point.");
            Notify("河岸巡查完成！可继续采集、找旧箱或返回密林。", "Survey complete! Explore, gather or return to Thornwood."); sound="reward"; return true;
        }
        private bool SpawnReachRoom()
        { if(save.room!=ReachRoom) return false; RestoreReachEncounter(); return true; }
        private void RestoreReachEncounter()
        {
            if(save.room!=ReachRoom || !ReachReady) return;
            enemies.RemoveAll(e=>IsHomewardEnemyId(e.id)?!HomewardActorAllowed(e):!IsReachEnemyId(e.id)||e.boss||e.kind!=reachGuardKinds[e.id-ReachEnemyBase]||Has("reach_defeated_"+e.id));
            for(int i=0;i<reachGuardPoints.Length;i++)
            {
                int id=ReachEnemyBase+i; if(Has("reach_defeated_"+id)||enemies.Exists(e=>e.id==id)) continue;
                Enemy actor=CreatePlacedEnemy(id,reachGuardKinds[i],reachGuardPoints[i],reachGuardHealth[i]); actor.timer=.7f+i*.09f; enemies.Add(actor);
            }
            RestoreHomewardEncounter();
        }
        private void RestoreReachRecord(EnemyRecord record)
        {
            if(record==null||!ReachReady||!IsReachEnemyId(record.id)||record.boss||Has("reach_defeated_"+record.id)||!Finite(record.hp)||record.hp<=0||!Finite(record.x)||!Finite(record.y)) return;
            int slot=record.id-ReachEnemyBase; if(record.kind!=reachGuardKinds[slot]||!CanStand(new Point(record.x,record.y))) return;
            Enemy e=CreatePlacedEnemy(record.id,reachGuardKinds[slot],new Point(record.x,record.y),reachGuardHealth[slot]); e.hp=Clamp(record.hp,.1f,e.maxHp); e.timer=.7f; enemies.Add(e);
        }
        private void OnReachKill(Enemy e)
        { if(save.room==ReachRoom&&IsReachEnemyId(e.id)&&!e.boss&&e.kind==reachGuardKinds[e.id-ReachEnemyBase]) Flag("reach_defeated_"+e.id); }
        private bool TalkReach(WorldObject o)
        {
            if(save.room!=ReachRoom||!o.kind.StartsWith("reach",StringComparison.Ordinal)) return false;
            if(!ReachWorldAction||!NearReachObject(o.kind)) return true;
            if(o.kind=="reachExit")
            {
                Enter(1); Point entrance=ObjectPoint(1,"portal11",new Point(Map.spawnX,Map.spawnY)); Point foot=entrance+new Point(0,1);
                if(CanStand(foot)){save.x=foot.x;save.y=foot.y;} return true;
            }
            if(o.kind=="reachBoard")
            {
                Dialog(o.name,o.en,"旧巡路队在河岸留了三处旧灯。沿西岸、河心和东岸调查，清理灯旁敌人后记录路线，再回这里交付。\n\n水轮可放下浮桥，缩短往返。北侧藤封后还有旧箱；第一枚星核的能力能打开它。两份旧记录也在河岸，可在手帐重读。\n\n首次巡查：硬币40、碎片4、技能点1。", "Survey the patrol's three old lamps on the west bank, river island and east bank. Clear nearby foes, chart each route, then report here.\n\nThe winch lowers a shortcut. Core one's power opens the roots on the northern cache path. Two old records can be reread in the journal.\n\nFirst survey: 40 coins, 4 shards, 1 talent point.", C(ReachComplete?"巡查已完成":"交付三处路线",ReachComplete?"Survey completed":"Report the three routes",ReachComplete?"close":"reach_claim"),C("追踪河岸巡查","Track the survey","reach_track"),C("继续探索","Keep exploring","close")); return true;
            }
            if(o.kind.StartsWith("reachPost",StringComparison.Ordinal))
            {
                int index=int.Parse(o.kind.Substring(9)); bool done=ReachPostRecorded(index);
                Dialog(o.name,o.en,index==0?"退水后，西岸旧路露了出来。这里的路标朝河心，不朝矿场。有人想让回家的人绕开采矿的道路。":index==1?"河心旧灯旁有两条旧路。水轮放下浮桥后，可以从西岸直接回来；旧灯的路线不必只剩一条。":"东岸旧灯能看到密林，也能看到河上的河心。巡路队留下它，不是为了让城墙更亮，是为了让河岸的人找到回家的方向。",index==0?"The west-bank marks point toward the island, away from the mine. Someone left a safer way home.":index==1?"Two paths meet at the island lamp. Lowering the footbridge gives a shorter return; the old road need not have only one route.":"The east lamp overlooks the woods and islands. It was left to guide people home, not to make the citadel walls brighter.", C(done?"路线已记录":"清理敌人后记录",done?"Route recorded":"Survey after clearing foes",done?"close":"reach_post"+index),C("稍后再来","Later","close")); if(HomewardAccepted&&index>0){var choices=new List<Choice>(conversation.choices);choices.Add(HomewardStopChoice(index-1));conversation.choices=choices.ToArray();} return true;
            }
            string action=o.kind=="reachBridge"?"reach_bridge":o.kind=="reachRoot"?"reach_roots":"reach_cache";
            bool opened=o.kind=="reachBridge"?ReachBridgeOpen:o.kind=="reachRoot"?ReachRootsOpen:ReachCacheRecovered;
            Dialog(o.name,o.en,o.kind=="reachBridge"?"转动旧水轮，放下河心浮桥。原来的南北两条路仍可通行。":o.kind=="reachRoot"?"藤脉封住了北侧旧箱的道路。在这里使用第一枚星核的能力；R 也可以解除藤封。":"巡路队留下的食物还在旧箱里。先清理旁边的敌人，再收起补给。首次可得硬币20、碎片2、炖汤1。",o.kind=="reachBridge"?"Turn the winch to lower the island shortcut. The two original crossings remain open.":o.kind=="reachRoot"?"Use core one's exploration power here to open the northern cache route. R works too.":"Clear the nearby foe, then recover the patrol's supplies: 20 coins, 2 shards and 1 stew once.",C(opened?"已经找回":"开始调查",opened?"Already recovered":"Investigate",opened?"close":action),C("稍后再来","Later","close")); return true;
        }
        private bool ChooseReach(string action)
        {
            if(action=="reach_track"){if(ReachWorldAction&&NearReachObject("reachBoard"))TrackQuest("reach");return true;}
            if(action=="reach_claim"){ClaimReachSurvey();return true;}
            if(action=="reach_bridge"){OpenReachBridge();return true;}
            if(action=="reach_roots"){ClearReachRoots();return true;}
            if(action=="reach_cache"){RecoverReachCache();return true;}
            if(action.StartsWith("reach_post",StringComparison.Ordinal)){int index;if(int.TryParse(action.Substring(10),out index))RecordReachPost(index);return true;}
            return false;
        }
        private QuestGuide GetReachQuest()
        {
            QuestGuide q=new QuestGuide{id="reach",room=ReachRoom,target=ObjectPoint(ReachRoom,"reachBoard",new Point(8,38)),title=L("旧路河岸巡查","Reedwater survey"),category=L("探索发现","DISCOVERY"),description=L("沿河岸调查三处旧灯，找回巡路队的往返路线。","Chart three old lamps and recover the patrol's routes home."),reward=L("硬币40 / 碎片4 / 技能点1","40 coins / 4 shards / 1 talent point"),complete=ReachComplete,locked=!ReachReady};
            int next=-1;float best=float.MaxValue;
            for(int i=0;i<3;i++){bool done=ReachPostRecorded(i);q.steps.Add(new QuestStep(L("旧灯 ","Lamp ")+(i+1),done));float distance=save.room==ReachRoom?(Position-ReachPosts[i]).Length:i;if(!done&&distance<best){best=distance;next=i;}}
            q.steps.Add(new QuestStep(L("返回巡查图交付","Report at the survey board"),ReachComplete));
            if(next>=0){q.target=ReachPosts[next];q.next=L("清理旧灯旁敌人，再按 E 记录路线","Clear nearby foes, then press E to survey the lamp");}
            else q.next=ReachComplete?L("巡查已完成；可继续探索隐藏旧箱","Survey complete; the hidden cache remains optional"):L("返回入口巡查图，交付三处路线","Return to the entrance board and report the routes");
            if(!ReachReady){q.room=1;q.target=ObjectPoint(1,"portal11",new Point(31,15));q.next=L("先找回第一枚星核，再进入河岸","Recover the first starcore to enter the riverbank");}
            return q;
        }
    }
}
