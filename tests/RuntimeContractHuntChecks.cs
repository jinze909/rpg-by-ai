// Shipped Update/OnGUI, keyboard combat, SaveStore and authored world data.
// Offline declarations do not establish native import/GPU/input/player feel.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeContractHuntChecks
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static string captureFolder,capturePrefix; static int captures,walkingFrames,contacts;
    static readonly Rect Claim=new Rect(310,458,271,46),TrackContract=new Rect(602,458,271,46),TrackQuest=new Rect(650,647,258,31);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static object Call(object target,string name,params object[] args){var m=target.GetType().GetMethod(name,Private|BindingFlags.Public);Check(m!=null,"Existing production method: "+name);return m.Invoke(target,args);}
    static T Get<T>(object target,string name){return(T)target.GetType().GetField(name,Private).GetValue(target);}
    static void Set(object target,string name,object value){target.GetType().GetField(name,Private).SetValue(target,value);}
    static bool Near(float a,float b){return Math.Abs(a-b)<.003f;}
    static bool Same(Point a,Point b){return(a-b).Length<.003f;}
    static bool Same(Rect a,Rect b){return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height);}
    static void Update(FrontierRuntime app){Call(app,"Update");Input.keys.Clear();}
    static void Key(FrontierRuntime app,KeyCode key){Input.keys.Add(key);Update(app);}
    static void Frames(FrontierRuntime app,int count){for(int n=0;n<count;n++)Update(app);}
    static bool Text(string s){return GUI.commands.Exists(c=>c.Contains("\"text\":"+GUI.S(s)));}
    static void Draw(FrontierRuntime app,string name=null)
    {
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        if(captureFolder!=null&&name!=null){var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();GUI.Export(captureFolder,capturePrefix+"-"+name);captures++;GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);}
    }
    static void Click(FrontierRuntime app,Rect rect){Draw(app);Check(GUI.buttonCalls.Exists(b=>Same(b.rect,rect)&&b.enabled),"Actual enabled button at "+rect.x+","+rect.y);GUI.queuedClick=rect;Draw(app);Check(!GUI.queuedClick.HasValue,"Production button consumes actual click");Draw(app);}
    static WorldObject Obj(GameCore g,int room,string kind){var o=Array.Find(g.data.maps[room].objects,x=>x.kind==kind);Check(o!=null,"Authored "+room+" retains "+kind);return o;}
    static Point P(WorldObject o){return new Point(o.x,o.y);}
    static void Position(FrontierRuntime app,Point p){Check(app.Game.CanStand(p)&&app.SetVisualReviewPosition(p)&&Same(app.Game.Position,p),"Exact legal fixture feet "+p.x+","+p.y);Set(app,"camera",p);}
    static void Flag(GameCore g,string name){if(!g.Has(name))g.save.flags.Add(name);}
    static void Prepare(FrontierRuntime app,int room=1,bool retainEnemies=false)
    {
        Input.Clear();GUI.queuedClick=null;GUI.enabled=true;GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;Event.current.type=EventType.Repaint;Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;
        app.BeginVisualReview(0,room);app.VisualReviewPaused=false;var g=app.Game;if(!retainEnemies)g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();g.effects.Clear();g.fallen.Clear();
        g.save.flags.RemoveAll(f=>f.StartsWith("landmark_pin:"));Flag(g,"contract_hunt_mature_story");g.save.companion=-1;g.save.english=true;g.save.largeText=false;g.save.showGuidance=true;g.save.reducedMotion=true;g.save.effectsQuality=0;g.save.bounty=-1;g.save.bountyProgress=0;g.save.bountyCycle=0;g.save.trackedQuest="main";g.save.level=20;g.save.xp=0;g.save.talents=new int[3];g.save.relics=new[]{-1,-1};g.save.explored[room]=16777215;
        Set(app,"applicationPaused",false);Set(app,"pointerSeen",false);Set(app,"pointerAimActive",false);Set(app,"controllerUsed",false);Set(app,"arrivalTime",0f);Set(app,"resumeTime",0f);Set(app,"combatHudTime",0f);Set(app,"journalTab",0);Set(app,"selectedQuest",0);Call(app,"Resize");
    }
    static void Accept(FrontierRuntime app,int contract)
    {
        var g=app.Game;Check(g.mode=="world"&&g.save.bounty<0,"Accept begins with no existing contract");g.save.bountyCycle=contract;Key(app,KeyCode.N);Check(g.mode=="bounties","Actual N opens Contracts");Click(app,new Rect(999,207,192,43));
        Check(g.save.bounty==contract&&g.save.bountyProgress==0&&g.save.trackedQuest=="bounty","Actual Accept resets only the new contract progress and tracks bounty");Click(app,TrackContract);Key(app,KeyCode.Escape);Check(g.mode=="world","Actual Escape returns from Contracts");
    }
    static void Journal(FrontierRuntime app,bool track=true)
    {
        var g=app.Game;Key(app,KeyCode.Tab);Check(g.mode=="journal","Actual Tab opens journal");Check(Array.IndexOf(GameCore.QuestIds,"bounty")==8,"Mature bounty row remains exactly eight");float spacing=Math.Min(46,464f/Math.Min(GameCore.QuestIds.Length,18));Click(app,new Rect(279,226+8*spacing,321,spacing-7));
        Check(Get<int>(app,"selectedQuest")==8&&Text(g.CurrentGuide.title),"Actual bounty row selects the contract title");if(track){Click(app,TrackQuest);Check(g.save.trackedQuest=="bounty","Actual journal Track owns quest selection");}
    }
    static Enemy EnemyAt(FrontierRuntime app,int id,int kind,Point p,float hp=100,bool boss=false){Check(app.Game.CanStand(p),"Enemy fixture occupies unchanged legal ground");var e=new Enemy(id,kind,p.x,p.y,hp,boss){stun=1000,timer=9999};app.Game.enemies.Add(e);return e;}
    static void Goal(FrontierRuntime app,Point p,string message)
    {
        var g=app.Game;Check(g.CurrentGuide.room==g.save.room&&Same(g.CurrentGuide.target,p),message+" guide");Update(app);Point? nav=g.NavigationTarget(),cached=Get<Point?>(app,"routeGoal");Check(nav.HasValue&&cached.HasValue&&Same(nav.Value,p)&&Same(cached.Value,p),message+" actual world routeGoal");Check(Get<List<Point>>(app,"route").Count>0||(g.Position-p).Length<1.8f,message+" actionable route");
    }
    static void Aim(FrontierRuntime app,Point p){Point s=(Point)Call(app,"ScreenPoint",p);Input.mousePosition=new Vector3(s.x,Screen.height-s.y,0);Set(app,"pointerAimActive",true);}
    static void Kill(FrontierRuntime app,Enemy enemy,bool walk=false)
    {
        var g=app.Game;enemy.stun=1000;enemy.timer=9999;
        if(walk)
        {
            Goal(app,enemy.Position,"Physical hunt");var path=new List<Point>(Get<List<Point>>(app,"route"));int steps=0;
            while((g.Position-enemy.Position).Length>1.15f&&steps++<3000)
            {
                while(path.Count>0&&(path[0]-g.Position).Length<.12f)path.RemoveAt(0);Point goal=path.Count>0?path[0]:enemy.Position;Point move=(goal-g.Position).Normalized,before=g.Position;
                Input.axes["Horizontal"]=move.x;Input.axes["Vertical"]=-move.y;Input.held.Clear();if(move.x>.01f)Input.held.Add(KeyCode.D);if(move.x<-.01f)Input.held.Add(KeyCode.A);if(move.y>.01f)Input.held.Add(KeyCode.S);if(move.y<-.01f)Input.held.Add(KeyCode.W);Update(app);walkingFrames++;Check(g.CanStand(g.Position)&&(g.Position-before).Length<=g.Speed*.0201f+.001f,"Actual WASD remains legal and continuous");
            }
            Input.Clear();Check(steps>0&&steps<3000,"Real held WASD reaches the contract foe");
        }
        else
        {
            bool staged=false;foreach(Point d in new[]{new Point(-1,0),new Point(1,0),new Point(0,-1),new Point(0,1)}){Point p=enemy.Position+d;if(g.CanStand(p)){Position(app,p);staged=true;break;}}Check(staged,"Real actor has legal sword approach");
        }
        Aim(app,enemy.Position);Input.held.Add(KeyCode.J);int frames=0;float hp=enemy.hp;while(g.enemies.Contains(enemy)&&frames++<1800)Update(app);Input.Clear();contacts+=frames;Check(frames<1800&&enemy.hp<hp&&!g.enemies.Contains(enemy),"Actual held J crosses production preparation/contact and defeats target");Frames(app,45);
    }
    static void ActualAcceptanceJournalAndMovingTargets(FrontierRuntime app)
    {
        foreach(int contract in new[]{0,3,4})
        {
            Prepare(app);Position(app,new Point(12,14));var g=app.Game;
            var valid=EnemyAt(app,90010,contract==4?2:0,new Point(16,14),100,contract==3);var other=EnemyAt(app,90011,contract==4?0:1,new Point(14,14));
            if(contract==0)other.hp=0;
            foreach(int id in new[]{79000,75000,GameCore.SignalWardenId,GameCore.CommissionGuardianId})EnemyAt(app,id,id==GameCore.SignalWardenId||id==GameCore.CommissionGuardianId?3:2,new Point(13,14),100,id==GameCore.SignalWardenId||id==GameCore.CommissionGuardianId);
            EnemyAt(app,90012,2,new Point(13,14),0,true);Accept(app,contract);Journal(app);Check(Text("○  0/"+GameCore.BountyTargets[contract]),"Actual row draws exact live incomplete count");Key(app,KeyCode.Escape);
            // Semantic RED: no new production API is referenced by this harness.
            Goal(app,valid.Position,"Combat contract selects counted living reachable actor, not fallback altar");
            valid.x=17;Goal(app,valid.Position,"Moving live actor immediately invalidates cached goal");if(contract==0){Point before=valid.Position;valid.stun=0;Frames(app,12);Check(!Same(before,valid.Position),"Production enemy AI actually moves the living hunt target");Check(Same(g.CurrentGuide.target,valid.Position)&&Same(g.NavigationTarget().Value,valid.Position),"Actual moving AI updates live guide and navigation coordinate every Tick");Check(Get<Point?>(app,"routeGoal").HasValue&&(Get<Point?>(app,"routeGoal").Value-valid.Position).Length<.6f,"Original route cache stays within its mature 0.6-unit refresh tolerance");valid.stun=1000;Frames(app,36);Goal(app,valid.Position,"Actual AI pursuit refreshes cached world goal after its original timer");}
            var replacement=EnemyAt(app,90013,contract==4?2:0,new Point(19,14),100,contract==3||contract==4);valid.hp=0;Goal(app,replacement.Position,"Dead target immediately yields to next eligible actor");Check(g.save.bountyProgress==0,"Reading moving/dead candidates cannot invent a kill");
        }
        Console.WriteLine("PASS real N accept/Track, Tab bounty row 8/Track, exact journal progress, live world routeGoal/route, movement/death retarget and heavy/boss/training/special-encounter filters; NATIVE_UNVERIFIED");
    }
    static void PhysicalCombatProgressAndOriginalReward(FrontierRuntime app)
    {
        foreach(int contract in new[]{0,3,4})
        {
            Prepare(app);Position(app,new Point(12,14));var g=app.Game;Accept(app,contract);int kills=g.save.kills[1],coins=g.save.coins,shards=g.save.shards,points=g.save.skillPoints,cycle=g.save.bountyCycle;int ordinary=0,bosses=0;var drops=(int[])g.save.inventory.Clone();
            Key(app,KeyCode.N);Draw(app);Check(GUI.buttonCalls.Exists(b=>Same(b.rect,Claim)&&!b.enabled),"Incomplete contract exposes disabled original Claim");GUI.queuedClick=Claim;Draw(app);Check(GUI.queuedClick.HasValue&&g.save.bountyProgress==0,"Real disabled Claim rejects mouse without granting progress");GUI.queuedClick=null;Key(app,KeyCode.Escape);
            for(int i=0;i<GameCore.BountyTargets[contract];i++)
            {
                bool boss=contract==3||contract==0&&i==3||contract==4&&i==3;int kind=contract==4?2:i%3;Point p=i==0?new Point(16,14):g.Position+new Point(1,0);if(!g.CanStand(p))p=new Point(16,14);var e=EnemyAt(app,90100+i,kind,p,1,boss);int progress=g.save.bountyProgress;Kill(app,e,i==0);Check(g.save.bountyProgress==progress+1,"Each actual qualifying kill grants exactly one original contract event");if(boss)bosses++;else ordinary++;drops[boss?5:kind==2?4:kind==1?1:2]++;
            }
            Check(g.save.kills[1]==kills+ordinary+bosses&&g.save.coins==coins+ordinary*6+bosses*70&&g.save.xp==ordinary*12+bosses*80,"Actual combat retains original regional kills and normal/boss coin/XP drops before claim");for(int i=0;i<drops.Length;i++)Check(g.save.inventory[i]==drops[i],"Original loot item ownership remains exact");
            Check(g.save.shards==shards&&g.save.skillPoints==points&&g.save.bounty==contract,"Completion alone grants no contract shards/points or automatic handoff");Check(g.CurrentGuide.steps[0].done&&g.CurrentGuide.next=="Open N Contracts to claim","Completed guide retains original manual Contracts claim prompt");
            int paidCoins=g.save.coins,paidXp=g.save.xp;Key(app,KeyCode.N);Click(app,Claim);Check(g.save.coins==paidCoins+35&&g.save.shards==shards+3&&g.save.xp==paidXp+30&&g.save.skillPoints==points&&g.save.bounty==-1&&g.save.bountyProgress==0&&g.save.bountyCycle==cycle+1,"Actual original Claim grants exactly 35 coins/30 XP/3 shards and retires contract once");string paid=JsonUtility.ToJson(g.save);GUI.queuedClick=Claim;Draw(app);Check(GUI.queuedClick.HasValue,"Retired claim has no active mouse target");GUI.queuedClick=null;Check(!g.ClaimBounty()&&JsonUtility.ToJson(g.save)==paid,"Repeated claim cannot duplicate payment");Check(g.Has("contract_hunt_mature_story"),"Hunt retains unrelated mature story");
        }
        // Existing task actors pass through the same original OnExpansionKill.
        foreach(string owner in new[]{"watch","reach","homeward","archive"})
        {
            int room=owner=="watch"?1:owner=="archive"?10:11;Prepare(app,room,true);var g=app.Game;
            if(owner=="watch"){g.enemies.Clear();Flag(g,"scout_saved");Flag(g,"watch_accept");Flag(g,"cache_1");Flag(g,"watch_wave_0");g.RestoreWatchEncounter();}
            if(owner=="homeward"){g.enemies.Clear();foreach(string f in new[]{"reach_complete","personal_0","homeward_accept","homeward_wave_0"})Flag(g,f);g.Enter(11);g.Close();}
            if(owner=="archive"){foreach(string f in new[]{"archive_accept","archive_drain","archive_vent","archive_wave","archive_record_0","archive_record_1","archive_record_2"})Flag(g,f);g.RestoreArchiveEncounter();}
            Enemy e=g.enemies.Find(x=>owner=="watch"?g.IsWatchEnemy(x):owner=="archive"?g.IsArchiveEnemy(x):owner=="homeward"?g.IsHomewardEnemyId(x.id):g.IsReachEnemyId(x.id));Check(e!=null,"Production spawns canonical "+owner+" counted actor");g.enemies.RemoveAll(x=>!ReferenceEquals(x,e));Accept(app,0);Kill(app,e);Check(g.save.bountyProgress==1,"Canonical "+owner+" combat retains original count event");
        }
        Console.WriteLine("PASS actual route walking and J contact for all three combat contracts, exact normal/boss/heavy counts/loot, unchanged manual reward and repeat rejection; canonical Watch/Reach/Homeward/Archive units still count; NATIVE_UNVERIFIED");
    }
    static void Target(GameCore g,int room,string kind,string message){var q=g.CurrentGuide;Check(q.room==room&&Same(q.target,P(Obj(g,room,kind))),message+" resolves actual authored "+kind);}
    static void EmptyRegionsAndGuardianPrerequisites(FrontierRuntime app)
    {
        foreach(int contract in new[]{0,4})
        {
            Prepare(app);var g=app.Game;Accept(app,contract);var q=g.CurrentGuide;Check(q.room==2&&g.CanEnter(2)&&Same(q.target,new Point(g.data.maps[2].spawnX,g.data.maps[2].spawnY)),"No-live contract leads to another genuinely enterable region spawn");Check(Same(g.NavigationTarget().Value,P(Obj(g,1,"portal0"))),"Remote spawn uses actual return portal in current region");
            g.save.stage=0;g.save.flags.RemoveAll(f=>f=="exp_intro"||f=="harbor_report"||f=="desert_report");Target(g,0,"portal1","Only current region enterable: route via town before respawn");g.Enter(0);g.Close();g.Enter(1);g.Close();Check(g.enemies.Count>0,"Original re-entry really repopulates the region");Check(g.CurrentGuide.room==1&&g.enemies.Exists(e=>Same(e.Position,g.CurrentGuide.target)&&(contract!=4||e.kind==2)),"Repopulation automatically resumes an actual counted live target");
        }
        for(int room=1;room<=7;room++)
        {
            Prepare(app,room);var g=app.Game;g.save.flags.Remove("gate_"+room);g.save.flags.Remove("scout_saved");Accept(app,3);
            if(room==1){g.save.kills[1]=4;Target(g,1,"scout","Unopened forest guardian requires rescued scout");Flag(g,"scout_saved");}
            if(room==2){Target(g,2,"lore","Unopened ruins guardian reads record first");Flag(g,"lore_2");Target(g,2,"rune0","Read ruins follows actual first tablet");g.runeProgress=1;Target(g,2,"rune2","Tablet order follows original 0/2/1 puzzle");}
            if(room==3){g.save.inventory[4]=0;Check(g.CurrentGuide.next.Contains("Gather ore"),"Elevator shortage first sources real ore");g.save.inventory[4]=3;Target(g,3,"engine","Funded elevator remains manual prerequisite");}
            if(room==4)Target(g,4,"shrine","Citadel has no extra invented lock");
            if(room==5){Target(g,5,"captain","Harbor first requires actual plan");g.save.harborPledge=1;Target(g,5,"relay0","Relay pledge leads to first unresolved relay");g.save.harborPledge=2;Target(g,5,"castaway0","Rescue pledge leads to actual passenger");}
            if(room==6){Target(g,6,"mirror0","Desert first requires actual mirror");g.save.mirrors[0]=1;Target(g,6,"mirror1","Mirror guidance advances over aligned first mirror");}
            if(room==7){Target(g,7,"bell0","Plateau first requires actual bell");g.save.bells[0]=2;Target(g,7,"bell2","Bell guidance advances over both already-correct first bells");}
            Flag(g,"gate_"+room);Target(g,room,room<5?"shrine":"expAltar","Unlocked guardian starts at real altar");Flag(g,"boss_"+room);Check(g.CurrentGuide.room!=room,"Defeated regional guardian never sends another boss contract to its spent altar");
        }
        Prepare(app);var game=app.Game;Accept(app,3);for(int i=1;i<=7;i++)Flag(game,"boss_"+i);Target(game,0,"abyssEntry","All regional guardians resolved use actual town fissure");game.save.frontierEnding=0;var story=game.GetQuest("shore");if(story.locked)story=game.GetQuest("main");Check(game.CurrentGuide.room==story.room&&Same(game.CurrentGuide.target,story.target)&&game.CurrentGuide.next.Contains(story.next),"Locked expedition follows actual shore/main unlock route");
        Console.WriteLine("PASS empty/only-enterable regional repopulation, all seven real guardian gates and authored prerequisite locations/tablet/mirror/bell order, spent altar rejection, final town fissure and locked shore/main story route; NATIVE_UNVERIFIED");
    }
    static void SaveOwnershipReadOnlyAndBilingualCaptures(FrontierRuntime app)
    {
        Prepare(app);Position(app,new Point(12,14));var g=app.Game;var e=EnemyAt(app,90200,0,new Point(16,14),91);Accept(app,0);g.save.bountyProgress=2;g.PrepareSave();Check(SaveStore.Write(g.save,0),"Actual checksum save writes active hunt");string warning;var saved=SaveStore.Read(0,out warning);Check(saved!=null&&warning==null,"Actual checksum reader accepts active hunt");
        foreach(int version in new[]{1,2}){var old=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(saved));old.version=version;File.WriteAllText(SaveStore.PathFor(1),JsonUtility.ToJson(old));var legacy=SaveStore.Read(1,out warning);Check(legacy!=null&&warning==null,"Actual raw v"+version+" reader accepts prior save");g.Load(legacy);g.Start();g.Close();Check(g.Has("contract_hunt_mature_story")&&g.enemies.Exists(x=>x.id==e.id&&Near(x.hp,91)),"Legacy migration preserves earned story and original active enemy snapshot");Check(version==1?g.save.bounty==-1&&g.save.bountyProgress==0:g.save.bounty==0&&g.save.bountyProgress==2,"Original v1 reset/v2 live-contract migration stays unchanged");}
        g.Load(SaveStore.Read(0,out warning));g.Start();g.Close();Check(g.save.bounty==0&&g.save.bountyProgress==2&&Same(g.CurrentGuide.target,g.enemies.Find(x=>x.id==e.id).Position),"Checksum restores original active count and live snapshot destination");
        g.save.explored[1]=16777215;var herb=Obj(g,1,"shrine");Key(app,KeyCode.M);Click(app,new Rect(913,692,304,30));int pinIndex=g.DiscoveredLandmarks().IndexOf(herb);Check(pinIndex>=0,"Original discovered altar appears in canonical landmark list");for(int page=0;page<pinIndex/6;page++)Click(app,new Rect(654,625,160,32));Click(app,new Rect(279,225+(pinIndex%6)*64,535,54));Click(app,new Rect(866,572,351,42));Key(app,KeyCode.Escape);Key(app,KeyCode.Escape);Check(g.PinnedLandmark==herb,"Original explicit local waypoint can be pinned");Check(Same(g.NavigationTarget().Value,P(herb))&&!Same(g.CurrentGuide.target,P(herb))&&g.save.trackedQuest=="bounty","Explicit pin owns navigation while bounty retains live target and tracking");
        Prepare(app,0);g=app.Game;Accept(app,3);g.StartPractice();Check(g.practicing&&g.CurrentGuide.next.Contains("Practice does not count")&&Same(g.NavigationTarget().Value,g.enemies[0].Position),"Original practice dummy owns navigation and contract honestly explains no count");g.EndPractice();Check(g.save.bountyProgress==0,"Practice exit invents no contract event");
        Prepare(app,8);g=app.Game;Accept(app,3);Check(g.InAbyss&&g.save.abyssFloor==6&&Same(g.NavigationTarget().Value,new Point(31,14))&&Same(g.CurrentGuide.target,new Point(31,14))&&g.CurrentGuide.next.Contains("this floor"),"Final-floor contract preserves actual banking east gate");g.save.abyssFloor=2;Check(g.CurrentGuide.next.Contains("floor 6")&&Same(g.NavigationTarget().Value,new Point(31,14)),"Earlier abyss floors explain sixth guardian without replacing east gate");
        Prepare(app,8,true);g=app.Game;var clearedBoss=g.enemies.Find(x=>x.boss);Check(clearedBoss!=null,"Final floor retains canonical guardian before late acceptance");g.enemies.RemoveAll(x=>!ReferenceEquals(x,clearedBoss));Kill(app,clearedBoss);Check(g.Has("boss_8")&&g.save.bounty<0,"Actual sixth-floor guardian can fall before contract acceptance");Frames(app,100);Accept(app,3);Check(g.save.bountyProgress==0&&g.CurrentGuide.next.Contains("Guardian resolved")&&Same(g.CurrentGuide.target,new Point(31,14))&&Same(g.NavigationTarget().Value,new Point(31,14)),"Late acceptance honestly banks and restarts instead of naming a dead guardian");Set(app,"combatHudTime",0f);Draw(app);Check(Text(g.Objective())&&g.CurrentGuide.steps[0].text=="0/1","Actual abyss HUD retains original expedition objective and live bounty progress");Journal(app);Check(Text("Next: "+g.CurrentGuide.next)&&g.CurrentGuide.next.Contains("Guardian resolved"),"Actual journal exposes late-accept guardian resolution hint");Key(app,KeyCode.Escape);
        // Prepared action and its immutable impact survive all read-only menus.
        Prepare(app,0);g=app.Game;Position(app,new Point(18,15));g.save.weapon=1;var target=EnemyAt(app,90201,0,new Point(22.5f,15),999);Accept(app,0);Aim(app,target.Position);Key(app,KeyCode.J);var action=g.playerAction;Check(action!=null&&!action.released,"Actual J prepares original bow action");Key(app,KeyCode.N);float elapsed=action.elapsed,damage=action.damage;var impact=action.impact;string state=JsonUtility.ToJson(g.save);for(int i=0;i<12;i++){Update(app);Draw(app);}Check(ReferenceEquals(g.playerAction,action)&&ReferenceEquals(action.impact,impact)&&Near(action.elapsed,elapsed)&&Near(action.damage,damage)&&JsonUtility.ToJson(g.save)==state,"Contracts GUI and paused Updates preserve prepared action/impact/count/economy snapshots");Key(app,KeyCode.Escape);int ticks=0;while(!action.released&&ticks++<80)Update(app);Check(action.released&&g.projectiles.Count>0,"Original bow really launches physical projectile after menu");var shot=g.projectiles[0];Key(app,KeyCode.Tab);float life=shot.life;state=JsonUtility.ToJson(g.save);Frames(app,15);Draw(app);Check(g.projectiles.Contains(shot)&&Near(shot.life,life)&&ReferenceEquals(shot.impact,impact)&&JsonUtility.ToJson(g.save)==state,"Journal preserves actual flying projectile life and immutable impact");
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})
        {
            Prepare(app);g=app.Game;Position(app,new Point(12,14));EnemyAt(app,90300,2,new Point(16,14),91);g.save.english=english;g.save.largeText=large;Screen.width=large?960:1280;Screen.height=large?600:800;Call(app,"Resize");Accept(app,4);Goal(app,g.enemies[0].Position,"Bilingual live heavy hunt");string suffix=(english?"en":"zh")+(large?"-large960":"-normal1280");Draw(app,"hud-"+suffix);Journal(app);Check(Text(g.L("下一步：","Next: ")+g.CurrentGuide.next)&&Text("○  0/4"),"Actual bilingual journal draws exact hunt source/count");state=JsonUtility.ToJson(g.save);int revision=g.revision;float clock=Get<float>(app,"clock");foreach(EventType evt in new[]{EventType.Layout,EventType.Repaint}){Event.current.type=evt;for(int n=0;n<5;n++)Draw(app);}Event.current.type=EventType.Repaint;Check(JsonUtility.ToJson(g.save)==state&&g.revision==revision&&Near(Get<float>(app,"clock"),clock),"Actual repeated GUI Layout/Repaint is read-only");Draw(app,"journal-"+suffix);
            if(large){Click(app,new Rect(927,647,263,31));Check(g.mode=="map"&&Get<int>(app,"selectedRegion")==g.CurrentGuide.room,"Actual journal Show destination selects live hunt region");Draw(app,"map-"+suffix);Key(app,KeyCode.Escape);Key(app,KeyCode.N);Draw(app,"contracts-"+suffix);}
        }
        if(captureFolder!=null)Check(captures==12,"Exactly twelve fresh actual bilingual HUD/journal/map/Contracts captures");
        Console.WriteLine("PASS checksum/raw v1-v2 migration and active enemy snapshot, explicit pin/practice/abyss east-gate priorities, prepared/flying immutable impact ownership, bilingual 960/1280 normal/large HUD/journal/map/Contracts and read-only GUI; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,temporary=Path.Combine(Path.GetTempPath(),"starfall-contract-hunt-"+Guid.NewGuid());Application.persistentDataPath=temporary;captureFolder=args.Length>0&&args[0]!="-"?args[0]:null;capturePrefix=args.Length>1?args[1]:"qa-contract-hunt";int failed=0;var app=new FrontierRuntime();
        try{Directory.CreateDirectory(temporary);Call(app,"Awake");Check(app.Game!=null,"Actual Awake loads campaign");foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{ActualAcceptanceJournalAndMovingTargets,PhysicalCombatProgressAndOriginalReward,EmptyRegionsAndGuardianPrerequisites,SaveOwnershipReadOnlyAndBilingualCaptures})try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}app.EndVisualReview();Call(app,"OnDestroy");}
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.enabled=true;GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;if(Directory.Exists(temporary))Directory.Delete(temporary,true);}
        Console.WriteLine("CAPTURE "+captures+" actual OnGUI command streams; WASD frames="+walkingFrames+"; physical contact frames="+contacts+"; NATIVE_UNVERIFIED");Console.WriteLine("Runtime contract hunt: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
