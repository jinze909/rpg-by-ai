// Shipped Update/OnGUI and checksum saves with offline Unity declarations.
// Captures describe command streams, never native import/GPU/input or player feel.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeRaceCourseChecks
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static string captureFolder,capturePrefix;
    static int captures,stickMoves;
    static readonly Rect Entry=new Rect(594,590,623,34),Back=new Rect(816,601,375,42),Launch=new Rect(816,550,375,40),Keep=new Rect(345,520,360,45),Cancel=new Rect(725,520,360,45);
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
    static object Call(object obj,string name,params object[] args){var m=obj.GetType().GetMethod(name,Private|BindingFlags.Public);Check(m!=null,"Production method exists: "+name);return m.Invoke(obj,args);}
    static T Get<T>(object obj,string name){var f=obj.GetType().GetField(name,Private|BindingFlags.Public);Check(f!=null,"Fixture field exists: "+name);return (T)f.GetValue(obj);}
    static void Set(object obj,string name,object value){obj.GetType().GetField(name,Private|BindingFlags.Public).SetValue(obj,value);}
    static bool Property(GameCore g,string name){var p=g.GetType().GetProperty(name);Check(p!=null,"Production property exists: "+name);return (bool)p.GetValue(g,null);}
    static bool Near(float a,float b){return Math.Abs(a-b)<.001f;}
    static bool Same(Point a,Point b){return (a-b).Length<.001f;}
    static bool Same(Rect a,Rect b){return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height);}
    static string Id(FrontierRuntime app,Rect r){return (string)Call(app,"ControlId",r.x,r.y,r.width,r.height);}
    static void Update(FrontierRuntime app){Call(app,"Update");Input.keys.Clear();Input.buttonDown.Clear();}
    static void Frames(FrontierRuntime app,int count){for(int i=0;i<count;i++)Update(app);}
    static void Key(FrontierRuntime app,KeyCode key){Input.Clear();Input.keys.Add(key);Update(app);Input.Clear();}
    static bool Text(string text){return GUI.commands.Exists(c=>c.Contains("\"text\":"+GUI.S(text)));}
    static void Draw(FrontierRuntime app,string suffix=null)
    {
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        if(captureFolder!=null&&suffix!=null){var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();GUI.Export(captureFolder,capturePrefix+"-"+suffix);captures++;GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);}
    }
    static void Click(FrontierRuntime app,Rect rect){Draw(app);Check(GUI.buttonCalls.Exists(b=>Same(b.rect,rect)&&b.enabled),"Actual enabled button exists at "+rect.x+","+rect.y);GUI.queuedClick=rect;Draw(app);Check(!GUI.queuedClick.HasValue,"Actual button consumes click");Draw(app);}
    static void Focus(FrontierRuntime app,Rect target,bool pad=false)
    {
        Draw(app);var nav=Get<MenuNavigation>(app,"navigation");string wanted=Id(app,target);
        if(!pad){int n=0;while(nav.focused!=wanted&&n++<50){Key(app,KeyCode.Tab);Draw(app);}Check(nav.focused==wanted&&nav.CanActivate(wanted),"Actual Tab reaches enabled page button");return;}
        var controls=Get<List<FocusControl>>(nav,"controls");var planner=new MenuNavigation();planner.SetControls(controls);string start=nav.focused;var queue=new Queue<string>();var prior=new Dictionary<string,string>();var edge=new Dictionary<string,Point>();queue.Enqueue(start);prior[start]=null;
        Point[] directions={new Point(-1,0),new Point(1,0),new Point(0,-1),new Point(0,1)};
        while(queue.Count>0&&!prior.ContainsKey(wanted)){string from=queue.Dequeue();foreach(Point direction in directions){planner.focused=from;planner.Move(direction);string next=planner.focused;if(prior.ContainsKey(next))continue;prior[next]=from;edge[next]=direction;queue.Enqueue(next);}}
        Check(prior.ContainsKey(wanted),"Actual production focus geometry has a stick route to the target");var path=new List<string>();for(string at=wanted;at!=start;at=prior[at])path.Add(at);path.Reverse();
        foreach(string at in path){Input.Clear();Frames(app,11);Input.axes["Debug Horizontal"]=edge[at].x;Input.axes["Debug Vertical"]=-edge[at].y;Update(app);Input.Clear();Draw(app);stickMoves++;Check(nav.focused==at&&Get<bool>(app,"controllerUsed"),"Real stick axes follow each production geometry edge");}
        Check(nav.CanActivate(wanted),"Stick reaches an enabled real control");
    }
    static void Activate(FrontierRuntime app,Rect target,bool pad=false){Focus(app,target,pad);Key(app,pad?KeyCode.JoystickButton0:KeyCode.Return);Draw(app);}
    static void Prepare(FrontierRuntime app,int role=0,bool english=true,bool large=false,int width=1280)
    {
        Input.Clear();GUI.queuedClick=null;GUI.enabled=true;GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;Event.current.type=EventType.Repaint;Screen.width=width;Screen.height=width*5/8;Time.unscaledDeltaTime=.02f;
        app.BeginVisualReview(role,6);app.VisualReviewPaused=false;var g=app.Game;g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();g.effects.Clear();g.fallen.Clear();g.save.flags.RemoveAll(f=>f=="race_finished"||f=="race_badge");g.save.bestRace=0;
        g.save.english=english;g.save.largeText=large;g.save.showGuidance=false;g.save.reducedMotion=true;g.save.effectsQuality=0;g.save.companion=-1;g.save.talents=new int[3];g.save.relics=new[]{-1,-1};g.save.coins=313;g.save.shards=17;g.save.skillPoints=2;g.save.energy=100;g.save.health=73;g.save.heat=40;g.save.cold=0;g.save.totalTime=100;g.save.foodUntil=g.save.teaUntil=g.save.mealUntil=0;g.save.mealKind=0;g.save.flags.Add("race_course_mature_story");
        Set(app,"applicationPaused",false);Set(app,"controllerUsed",false);Set(app,"pointerSeen",false);Set(app,"pointerAimActive",false);Set(app,"arrivalTime",0f);Set(app,"resumeTime",0f);Set(app,"combatHudTime",0f);Set(app,"dialogParent",null);Call(app,"Resize");
        Position(app,new Point(14,17.3f));Check(g.Nearest()!=null&&g.Nearest().kind=="skiff","Exact legal origin belongs to original sand skiff");
    }
    static void Position(FrontierRuntime app,Point p){Check(app.Game.CanStand(p)&&app.SetVisualReviewPosition(p)&&Same(app.Game.Position,p),"Fixture uses exact legal feet at "+p.x+","+p.y);Set(app,"camera",p);}
    static void Choice(FrontierRuntime app,string action)
    {
        var g=app.Game;Check(g.mode=="dialog"&&g.conversation!=null,"Real interaction opens dialogue");int choice=Array.FindIndex(g.conversation.choices,c=>c.action==action);Check(choice>=0&&choice<9,"Real numbered dialogue contains "+action);Draw(app);int pages=0;while(!(bool)Call(app,"DialogueChoicesVisible")){Key(app,KeyCode.Return);Draw(app);Check(pages++<20,"Actual skiff dialogue pages end");}Key(app,(KeyCode)((int)KeyCode.Alpha1+choice));Draw(app);
    }
    static void Preview(FrontierRuntime app){Key(app,KeyCode.E);Choice(app,"race_course");Check(app.Game.mode=="racecourse","Actual Study the route opens its five-flag preview instead of closing the skiff conversation");Draw(app);}
    static void Start(FrontierRuntime app,bool preview=true)
    {if(preview){Preview(app);Activate(app,Launch);}else{Key(app,KeyCode.E);Choice(app,"race");}Check(app.Game.racing&&app.Game.mode=="world"&&app.Game.raceCheckpoint==0&&Near(app.Game.raceTime,0),"Original Launch begins exactly one clean real attempt");}
    static void PauseCourse(FrontierRuntime app,bool pad=false,string capture=null)
    {Key(app,pad?KeyCode.JoystickButton7:KeyCode.Escape);Draw(app,capture);Check(app.Game.mode=="pause"&&GUI.buttonCalls.Exists(b=>Same(b.rect,Entry)&&b.enabled),"Actual active-race Pause exposes its optional course/exit button");Activate(app,Entry,pad);Check(app.Game.mode=="racecourse","Actual Pause input opens the same active route page");}
    static string Rewards(GameCore g){return g.save.coins+"/"+g.save.shards+"/"+g.save.skillPoints+"/"+g.save.bestRace+"/"+g.Has("race_finished")+"/"+g.Has("race_badge");}
    static string Attempt(GameCore g){return g.racing+"/"+g.raceCheckpoint+"/"+g.raceTime+"/"+g.Position.x+"/"+g.Position.y+"/"+g.save.health+"/"+g.save.energy+"/"+g.save.heat+"/"+g.save.totalTime+"/"+Rewards(g);}
    static void PhysicalFlag(FrontierRuntime app)
    {
        var g=app.Game;int checkpoint=g.raceCheckpoint;Point target=GameCore.RaceRoute[checkpoint];List<Point> path=g.NavigationPath(target);Check(path.Count>0,"Original numbered flag has a real walk path");int frames=0;
        while(g.racing&&g.raceCheckpoint==checkpoint&&frames++<1600)
        {
            while(path.Count>0&&(path[0]-g.Position).Length<.13f)path.RemoveAt(0);Point goal=path.Count>0?path[0]:target;Point delta=goal-g.Position,move=delta.Normalized;Point before=g.Position;float clock=g.raceTime,heat=g.save.heat,speed=g.Speed;
            Input.axes["Horizontal"]=move.x;Input.axes["Vertical"]=-move.y;Input.held.Clear();if(move.x>.05f)Input.held.Add(KeyCode.D);if(move.x<-.05f)Input.held.Add(KeyCode.A);if(move.y>.05f)Input.held.Add(KeyCode.S);if(move.y<-.05f)Input.held.Add(KeyCode.W);Update(app);
            Check(g.CanStand(g.Position)&&(g.Position-before).Length<=speed*.0201f+.0001f,"Real held WASD obeys full-footprint collision and unchanged race walking speed");Check(g.raceTime>=clock&&g.save.heat<=heat+.001f,"Real race clock advances while racing never adds desert heat");
        }
        Input.Clear();Check(frames<1600&&g.raceCheckpoint==checkpoint+1,"Real movement reaches each original strict 1.35-radius flag in sequence: "+checkpoint);Set(app,"camera",g.Position);
    }
    static void StudyLaunchPauseAndCancellation(FrontierRuntime app)
    {
        Prepare(app);var g=app.Game;Key(app,KeyCode.E);Check(g.conversation.choices[0].action=="race","Original immediate Launch remains numbered first");Check(g.conversation.choices.Length>=3&&g.conversation.choices[1].action=="race_course"&&g.conversation.choices[2].action=="close","Study route receives a real page while independent goodbye remains");Choice(app,"race_course");
        // Semantic RED uses actual old Study-route input before referencing any new API.
        Check(g.mode=="racecourse","Actual Study the route must open a useful route page rather than merely close");Draw(app);Check(Property(g,"RaceCourseActive")&&Property(g,"RaceCourseCanLaunch"),"Physical local preview has live owner and honest launch availability");string state=Attempt(g);Frames(app,80);Draw(app);Check(Attempt(g)==state,"Preview actual Updates freeze world/race/HP/energy/heat/clocks");Activate(app,Launch);Check(g.racing&&g.mode=="world","Actual Tab/Enter launches from preview");PhysicalFlag(app);
        PauseCourse(app,true);state=Attempt(g);Frames(app,100);Draw(app);Check(Attempt(g)==state,"Pause course freezes an existing partial race without time, position or reward loss");Activate(app,Launch,true);Check(Property(g,"RaceCourseCancellationPending"),"Actual stick/A arms a separate cancellation review");Draw(app);Check(Get<MenuNavigation>(app,"navigation").focused==Id(app,Keep),"Cancellation defaults to safe Continue rather than destructive End");Key(app,KeyCode.JoystickButton1);Draw(app);Check(g.mode=="racecourse"&&!Property(g,"RaceCourseCancellationPending")&&Attempt(g)==state,"Actual B declines review without resuming or resetting attempt");Activate(app,Launch);Key(app,KeyCode.Escape);Draw(app);Check(g.mode=="racecourse"&&!Property(g,"RaceCourseCancellationPending")&&Attempt(g)==state,"Actual Escape declines only the confirmation");Activate(app,Launch,true);Activate(app,Keep,true);Check(g.mode=="racecourse"&&!Property(g,"RaceCourseCancellationPending")&&Attempt(g)==state,"Explicit safe Continue keeps the course page and exact attempt");Key(app,KeyCode.JoystickButton1);Draw(app);Check(g.mode=="world"&&Attempt(g)==state,"B from unarmed page resumes unchanged race directly");
        PauseCourse(app);Activate(app,Launch);state=Attempt(g);Point feet=g.Position;string rewards=Rewards(g);Activate(app,Cancel,true);Check(g.mode=="world"&&!g.racing&&Same(g.Position,feet)&&Rewards(g)==rewards,"Explicit confirmed ending stops racing once without teleport, fee or reward");Check(!(bool)Call(g,"CancelRaceFromCourse")&&Rewards(g)==rewards,"Repeated cancellation has no economic effect");Prepare(app);Start(app,false);Check(app.Game.racing,"Mature first numbered direct launch still works independently of preview");
        Console.WriteLine("PASS actual E/numeric useful study page, preserved first Launch, Tab/Enter, real START/stick/A course/review/Continue/End, default safe focus, B/Esc decline/resume and no teleport/fee/reward; NATIVE_UNVERIFIED");
    }
    static void RealWalkingThreeProfessionsAndRewards(FrontierRuntime app)
    {
        foreach(int role in new[]{0,1,2})
        {
            Prepare(app,role);Start(app);var g=app.Game;float expected=4.5f+(role==1?.45f:0)+.65f;Check(Near(g.Speed,expected),"Existing role and race speed bonus remain exact");int coins=g.save.coins,shards=g.save.shards,skills=g.save.skillPoints;for(int flag=0;flag<5;flag++)PhysicalFlag(app);
            Check(!g.racing&&g.Has("race_finished")&&g.raceTime<=30&&g.save.bestRace==Math.Max(1,(int)(g.raceTime*10))&&g.save.coins==coins+25,"Each profession completes all five original flags through held movement and earns canonical first 25 coins and best tenths");bool badge=g.raceTime<=16;Check(g.Has("race_badge")==badge&&g.save.shards==shards+3+(badge?5:0)&&g.save.skillPoints==skills+(badge?1:0),"Original <=16 badge and exactly-once first/badge shard/point reward thresholds remain");Check(g.Has("race_course_mature_story")&&g.save.health==73,"Noncombat physical course preserves mature story and health");Console.WriteLine("WALK role="+role+" time="+g.raceTime.ToString("R",CultureInfo.InvariantCulture)+" badge="+badge);
            if(role==0){Draw(app,"world-finished");Position(app,new Point(14,17.3f));Start(app,false);for(int flag=0;flag<5;flag++)PhysicalFlag(app);Check(g.save.coins==coins+25&&g.save.shards==shards+3+(g.Has("race_badge")?5:0)&&g.save.skillPoints==skills+(g.Has("race_badge")?1:0),"Actual second physical finish never duplicates mature first or badge grants");}
        }
        Console.WriteLine("PASS actual five-flag held-WASD completion for all three professions, canonical movement/climate and original first/badge/best-time/repeat rewards; NATIVE_UNVERIFIED");
    }
    static void DeadlineOwnershipAndLegacySaves(FrontierRuntime app)
    {
        foreach(float elapsed in new[]{29.98f,29.99f,30f})
        {Prepare(app);Start(app,false);var g=app.Game;Position(app,new Point(14,18));g.raceCheckpoint=4;g.raceTime=elapsed;string rewards=Rewards(g);Update(app);if(elapsed>29.98f)Check(!g.racing&&!g.Has("race_finished")&&Rewards(g)==rewards,"Real 30.01/30.02 tick expires before a coincident finish can grant late rewards");else Check(!g.racing&&g.Has("race_finished")&&g.raceTime<=30.001f,"Exact 30-second boundary remains a valid finish");}
        foreach(string cause in new[]{"load","travel","save replacement","new review"})
        {
            Prepare(app);Start(app);PauseCourse(app);var g=app.Game;
            if(cause=="load"){SaveData copy=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(g.save));g.Load(copy);g.Close();}
            else if(cause=="travel")g.Enter(0);
            else if(cause=="save replacement")g.save=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(g.save));
            else{app.BeginVisualReview(0,6);app.VisualReviewPaused=false;g=app.Game;}
            g.mode="racecourse";string state=JsonUtility.ToJson(g.save),rewards=Rewards(g);Draw(app);Check(!Property(g,"RaceCourseActive")&&!Property(g,"RaceCourseCanLaunch"),"Stale "+cause+" page honestly rejects old owner");Check(!GUI.buttonCalls.Exists(b=>Same(b.rect,Launch)&&b.enabled),"Actual stale page never advertises an enabled launch/cancel transaction");GUI.queuedClick=Launch;Draw(app);Check(GUI.queuedClick.HasValue,"Actual stale transaction rejects a queued mouse click");GUI.queuedClick=null;
            foreach(string action in new[]{"LaunchRaceCourse","ReviewRaceCancellation","CancelRaceFromCourse"})Check(!(bool)Call(g,action),"Stale "+cause+" rejects "+action);Check(JsonUtility.ToJson(g.save)==state&&Rewards(g)==rewards,"Stale ownership cannot spend, grant, reset or mutate saved progress");Key(app,KeyCode.Escape);Check(g.mode=="world","Stale preview retains actual Escape exit");
        }
        Prepare(app);Start(app);PhysicalFlag(app);PauseCourse(app);var game=app.Game;game.save.bestRace=147;game.save.flags.Add("race_finished");game.save.flags.Add("race_badge");game.PrepareSave();Check(SaveStore.Write(game.save,0),"Actual checksum writer accepts an active-course save");string warning;SaveData saved=SaveStore.Read(0,out warning);Check(saved!=null&&warning==null,"Actual checksum reader returns the unchanged save schema");
        foreach(int version in new[]{1,2}){SaveData old=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(saved));old.version=version;File.WriteAllText(SaveStore.PathFor(1),JsonUtility.ToJson(old));SaveData legacy=SaveStore.Read(1,out warning);Check(legacy!=null&&warning==null,"Actual raw legacy v"+version+" remains readable");game.Load(legacy);game.Close();Check(!game.racing&&!Property(game,"RaceCourseActive")&&game.save.bestRace==147&&game.Has("race_finished")&&game.Has("race_badge")&&game.Has("race_course_mature_story"),"Legacy load retains best record and once-only story/reward flags but no live race or stale menu");}
        var envelope=JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(SaveStore.PathFor(0)));Check(envelope.checksum.Length==64&&!envelope.payload.Contains("raceCourseOwner")&&!envelope.payload.Contains("raceCourseCancellation"),"Existing SHA256 envelope never persists transient preview ownership/review");envelope.checksum="BAD";File.WriteAllText(SaveStore.PathFor(2),JsonUtility.ToJson(envelope));Check(SaveStore.Read(2,out warning)==null&&warning!=null&&warning.Contains("checksum"),"Mature checksum still rejects corrupted race save");
        Console.WriteLine("PASS actual world-tick exact/late deadline ordering, load/travel/save/review stale rejection, checksum tamper protection and raw v1/v2 best record/flags compatibility without live race; NATIVE_UNVERIFIED");
    }
    static void BilingualReadOnlyMapAndFocus(FrontierRuntime app)
    {
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(int width in new[]{960,1280})
        {
            Prepare(app,0,english,large,width);Preview(app);var g=app.Game;string suffix=(english?"en":"zh")+(large?"-large":"-normal")+"-"+width;Draw(app,"ready-"+suffix);Check(Text("1")&&Text("2")&&Text("3")&&Text("4")&&Text("5"),"Actual course map shows all five original numbered flags simultaneously");Check(GUI.buttonCalls.Exists(b=>Same(b.rect,Back)&&b.enabled)&&GUI.buttonCalls.Exists(b=>Same(b.rect,Launch)&&b.enabled),"Actual local preview presents launch and safe exit controls");
            foreach(Rect rect in new[]{Back,Launch}){float scale=Get<float>(app,"scale"),x=Get<float>(app,"offsetX")+rect.x*scale,y=Get<float>(app,"offsetY")+rect.y*scale;Check(x>=0&&y>=0&&x+rect.width*scale<=Screen.width+.1f&&y+rect.height*scale<=Screen.height+.1f,"Actual route controls scale fully inside "+width+" screen");}
            string state=Attempt(g),save=JsonUtility.ToJson(g.save);float clock=Get<float>(app,"clock");foreach(EventType evt in new[]{EventType.Layout,EventType.Repaint}){Event.current.type=evt;Draw(app);Draw(app);}Event.current.type=EventType.Repaint;Check(Attempt(g)==state&&JsonUtility.ToJson(g.save)==save&&Near(clock,Get<float>(app,"clock")),"Repeated actual Layout/Repaint map/record text does not tick or mutate world, race, save or runtime clock");
            Activate(app,Launch);PhysicalFlag(app);g.save.bestRace=157;PauseCourse(app,false,large&&width==960?"pause-"+suffix:null);Draw(app);Check(Text("1")&&Text("2")&&Text("3")&&Text("4")&&Text("5")&&GUI.commands.Exists(c=>c.Contains("15.7")),"Actual mid-race preview retains all five flags and honest best record");
            if(large&&width==960){Draw(app,"partial-"+suffix);Activate(app,Launch);Draw(app,"confirm-"+suffix);Check(Get<MenuNavigation>(app,"navigation").focused==Id(app,Keep),"Actual confirmation focus remains on safe Continue in both languages");Key(app,KeyCode.Escape);}
            Key(app,KeyCode.Escape);Check(g.mode=="world"&&g.racing,"Actual page Back resumes race across bilingual text sizes");
        }
        Prepare(app);Key(app,KeyCode.Escape);Draw(app);Check(!GUI.buttonCalls.Exists(b=>Same(b.rect,Entry)),"Ordinary non-race Pause has no misplaced race-only entry");
        Console.WriteLine("PASS actual bilingual normal/large 960/1280 five-flag terrain/record preview, partial and safe confirmation captures="+captures+", control bounds, read-only Layout/Repaint and race-only Pause entry; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-race-course-"+Guid.NewGuid());Application.persistentDataPath=directory;captureFolder=args.Length>0&&args[0]!="-"?args[0]:null;capturePrefix=args.Length>1?args[1]:"qa-race-course";int failed=0;var app=new FrontierRuntime();
        try{Directory.CreateDirectory(directory);Call(app,"Awake");Check(app.Game!=null,"Actual Awake loads campaign");foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{StudyLaunchPauseAndCancellation,RealWalkingThreeProfessionsAndRewards,DeadlineOwnershipAndLegacySaves,BilingualReadOnlyMapAndFocus})try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}app.EndVisualReview();Call(app,"OnDestroy");}
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.enabled=true;GUI.color=Color.white;GUI.matrix=Matrix4x4.identity;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("CAPTURE "+captures+" actual OnGUI command streams; pad stick edges="+stickMoves+"; NATIVE_UNVERIFIED");Console.WriteLine("Runtime race course: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
