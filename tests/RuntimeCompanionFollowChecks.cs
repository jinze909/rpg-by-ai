// Real Update movement/dodge/command and OnGUI paths with offline declarations.
// These checks do not establish native import, graphics or gameplay acceptance.
using System;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeCompanionFollowChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static string captureFolder,capturePrefix;
    private static int movingFrames;
    private static float travelled;
    private static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
    private static bool Near(float a,float b,float tolerance=.002f){return Math.Abs(a-b)<=tolerance;}
    private static bool Same(Point a,Point b){return (a-b).Length<.001f;}
    private static object Call(object obj,string name,params object[] args)
    {var method=obj.GetType().GetMethod(name,Private|BindingFlags.Public);Check(method!=null,"Existing production entry point: "+name);return method.Invoke(obj,args);}
    private static T Get<T>(object obj,string name){return (T)obj.GetType().GetField(name,Private).GetValue(obj);}
    private static void Set(object obj,string name,object value){obj.GetType().GetField(name,Private).SetValue(obj,value);}
    private static Point Follow(GameCore g){return (Point)Call(g,"CompanionFollowTarget");}
    private static void ClearDraw(){GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();}
    private static void Draw(FrontierRuntime app,string capture=null)
    {
        ClearDraw();Call(app,"OnGUI");
        if(captureFolder!=null&&capture!=null)
        {
            var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();GUI.Export(captureFolder,capturePrefix+"-"+capture);
            GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static void Mouse(FrontierRuntime app,Point world)
    {Point p=(Point)Call(app,"ScreenPoint",world);float scale=Get<float>(app,"scale");Input.mousePosition=new Vector3(Get<float>(app,"offsetX")+p.x*scale,Screen.height-Get<float>(app,"offsetY")-p.y*scale,0);}
    private static void Motion(Point direction,bool pad=false)
    {
        Input.held.Clear();Input.axes["Horizontal"]=direction.x;Input.axes["Vertical"]=-direction.y;
        if(!pad){if(direction.x>.1f)Input.held.Add(KeyCode.D);if(direction.x<-.1f)Input.held.Add(KeyCode.A);if(direction.y>.1f)Input.held.Add(KeyCode.S);if(direction.y<-.1f)Input.held.Add(KeyCode.W);}
    }
    private static void Gait(FrontierRuntime app)
    {
        var g=app.Game;Check(g.companionMoving&&g.companionAction==null,"Actual locomotion owns the original moving flag");
        ClearDraw();Call(app,"DrawCompanionActor");string sheet="resource:Frontier/Art/Walk"+new[]{"Mira","Ivo","Sen"}[g.save.companion];
        int row=GameCore.DirectionRow(g.companionAim),frame=row*4+GameCore.GaitFrame(g.companionMoveTime,GameCore.WalkStride);bool flip=row==0&&g.companionAim.x<0;
        var body=GUI.textureCalls.Find(c=>c.texture.name==sheet&&Near(Math.Abs(c.uv.width),.25f));
        Check(body!=null&&Near(body.uv.x,(frame%4+(flip?1:0))/4f)&&Near(body.uv.y+body.uv.height,1-row/3f),"Actual companion draw uses the existing distance-driven walk frame and direction row");
    }
    private static void Update(FrontierRuntime app,bool gait=false)
    {
        var g=app.Game;Point before=g.companionPosition;float clock=g.companionMoveTime,dt=Time.unscaledDeltaTime,speed=g.Speed;
        Call(app,"Update");Input.keys.Clear();Input.buttonDown.Clear();Point step=g.companionPosition-before;
        Check(g.CanStand(g.Position)&&g.CanStand(g.companionPosition),"Every real runtime frame preserves legal complete hero and companion feet");
        Check(step.Length<=(speed+1)*dt+.0005f,"Companion takes an original-speed swept step without teleporting or corner cutting");
        Check(Near(g.companionMoveTime-clock,step.Length,.0005f),"Companion gait advances only by actual travelled distance");travelled+=step.Length;
        if(step.Length>.001f&&g.companionAction==null){Check(g.companionMoving,"Real follow movement owns companion gait");if(gait&&movingFrames%23==0)Gait(app);movingFrames++;}
    }
    private static void Key(FrontierRuntime app,KeyCode key){Input.keys.Add(key);Update(app);}
    private static void Frames(FrontierRuntime app,int count){for(int i=0;i<count;i++)Update(app);}
    private static void Prepare(FrontierRuntime app,int companion=1,int profession=0)
    {
        Input.Clear();GUI.queuedClick=null;ClearDraw();GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;
        Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.01f;Event.current.type=EventType.Repaint;
        app.BeginVisualReview(profession,0);app.VisualReviewPaused=false;var g=app.Game;
        g.enemies.Clear();g.projectiles.Clear();g.effects.Clear();g.hazards.Clear();g.fallen.Clear();
        for(int id=0;id<3;id++)if(!g.Has("companion_"+id))g.save.flags.Add("companion_"+id);
        g.save.companion=-1;g.save.relationships=new[]{3,3,3};g.save.health=g.save.maxHealth;g.save.energy=100;g.save.frontierEnding=0;
        g.save.talents=new int[3];g.save.relics=new[]{-1,-1};g.save.foodUntil=g.save.teaUntil=g.save.mealUntil=0;g.save.heat=g.save.cold=0;
        g.save.showGuidance=false;g.save.english=true;g.save.reducedMotion=true;g.save.effectsQuality=0;g.save.seed=417;g.save.totalTime=100;g.toastTime=0;g.invulnerable=100;
        Check(app.SetVisualReviewPosition(new Point(8,6.5f))&&Same(g.Position,new Point(8,6.5f)),"Fixture starts on exact authored legal Emberfall ground");
        g.SelectCompanion(companion);Check(g.save.companion==companion&&Same(g.companionPosition,new Point(6.35f,7.05f)),"Normal companion recruitment supplies original legal starting follow offset");
        Set(app,"camera",g.Position);Set(app,"applicationPaused",false);Set(app,"controllerUsed",false);Set(app,"pointerSeen",false);Set(app,"pointerAimActive",false);
        Set(app,"resumeTime",0f);Set(app,"arrivalTime",0f);Set(app,"combatHudTime",0f);Call(app,"Resize");Call(app,"ResetControllerAim",false);Mouse(app,g.Position+new Point(4,0));movingFrames=0;travelled=0;
    }
    private static void WalkTo(FrontierRuntime app,Point goal,bool pad=false)
    {
        var g=app.Game;int frames=0;
        while((goal-g.Position).Length>.0005f&&frames++<1200)
        {
            Point delta=goal-g.Position;Motion(delta.Normalized,pad);Time.unscaledDeltaTime=Math.Min(.01f,delta.Length/(g.dashTime>0?15:g.Speed));Update(app,true);
        }
        Input.Clear();Time.unscaledDeltaTime=.01f;Check(frames<1200&&Same(g.Position,goal),"Actual held movement reaches the authored route waypoint");
    }
    private static void AroundHouse(FrontierRuntime app,bool pad=false)
    {
        var g=app.Game;WalkTo(app,new Point(9.5f,6.5f),pad);Motion(new Point(0,-1),pad);Time.unscaledDeltaTime=.01f;float energy=g.save.energy;
        Key(app,pad?KeyCode.JoystickButton0:KeyCode.Space);
        Check(g.dashTime>0&&Near(energy-g.save.energy,20)&&Near(g.dashCooldown,.75f),"Actual "+(pad?"pad A":"Space")+" starts the original charged northward dash");
        WalkTo(app,new Point(9.5f,3.63f),pad);Check(g.dashTime<=0,"North route finishes its actual dash before turning west");WalkTo(app,new Point(8,3.63f),pad);
        Point goal=Follow(g);Check(Same(goal,new Point(6.35f,4.18f))&&g.CanStand(goal)&&!g.CanStand(new Point(6.5f,4.5f)),"Unchanged exact follow foot is legal although its authored house cell center is blocked");
    }
    private static int CatchUp(FrontierRuntime app,bool gait=false)
    {
        var g=app.Game;Input.Clear();Time.unscaledDeltaTime=.02f;Point hero=g.Position;int frames=0;
        for(;frames<1000&&(Follow(g)-g.companionPosition).Length>.3001f;frames++){Update(app,gait);Check(Same(g.Position,hero),"Waiting never relocates the stationary player");}
        return frames;
    }
    private static void NaturalRouteCatchesUpForEveryCompanion(FrontierRuntime app)
    {
        int cases=0;
        for(int companion=0;companion<3;companion++)for(int profession=0;profession<2;profession++)
        {
            Prepare(app,companion,profession);var g=app.Game;AroundHouse(app,profession==1);Draw(app,"route-end-"+companion+"-"+profession);int frames=CatchUp(app,true);
            Console.WriteLine("ROUTE companion="+companion+" profession="+profession+" wait_frames="+frames+" gap="+(Follow(g)-g.companionPosition).Length+" travelled="+travelled);
            Check(frames<1000&&(Follow(g)-g.companionPosition).Length<=.3001f,"Naturally recruited companion must finish the real house detour within 20 seconds instead of stalling below the legal follow foot");
            Check(movingFrames>20&&travelled>3&&g.companionAction==null&&g.projectiles.Count==0,"Catch-up consists of real existing locomotion without invented attacks or projectiles");
            Point feet=g.companionPosition;float gait=g.companionMoveTime;Frames(app,25);Check(Same(g.companionPosition,feet)&&Near(g.companionMoveTime,gait)&&!g.companionMoving,"Arrival retains the original .3 stop radius and idle gait");Draw(app,"arrived-"+companion+"-"+profession);cases++;
        }
        Console.WriteLine("PASS "+cases+" actual WASD/left-stick routes and Space/pad-A dashes, all companions/two original player speeds, natural house-detour arrival, legal swept speed/distance gait and original idle radius; NATIVE_UNVERIFIED");
    }
    private static string State(GameCore g)
    {return JsonUtility.ToJson(g.save)+"/"+JsonUtility.ToJson(g.companionAction)+"/"+JsonUtility.ToJson(g.projectiles)+"/"+JsonUtility.ToJson(g.effects)+"/"+JsonUtility.ToJson(g.companionPosition)+"/"+JsonUtility.ToJson(g.companionAim)+"/"+g.companionMoveTime+"/"+g.companionMoving+"/"+Get<float>(g,"companionAttack")+"/"+g.companionCooldown+"/"+g.revision;}
    private static void MenusAndPauseFreezeThenResumeCatchUp(FrontierRuntime app)
    {
        foreach(string boundary in new[]{"inventory","application","review"})
        {
            Prepare(app);var g=app.Game;AroundHouse(app);Check((Follow(g)-g.companionPosition).Length>.3f,"Natural route leaves genuine follow work before pausing");
            if(boundary=="inventory")Key(app,KeyCode.I);else if(boundary=="application")Call(app,"OnApplicationPause",true);else app.VisualReviewPaused=true;
            string before=State(g);for(int frame=0;frame<25;frame++){Update(app);Event.current.type=frame%2==0?EventType.Layout:EventType.Repaint;Draw(app);Check(State(g)==before,"Real "+boundary+" pause freezes follow feet, gait, combat/world clocks and persistent state");}
            Event.current.type=EventType.Repaint;
            if(boundary=="inventory")Key(app,KeyCode.Escape);else if(boundary=="application")Call(app,"OnApplicationPause",false);else app.VisualReviewPaused=false;
            Check(CatchUp(app)<1000&&(Follow(g)-g.companionPosition).Length<=.3001f,"Existing runtime resumes the same natural detour after "+boundary+" pause without relocating the companion");
        }
        Console.WriteLine("PASS actual inventory/application/review pause freezes full follow state; the same naturally lagging companion resumes and arrives after each boundary; NATIVE_UNVERIFIED");
    }
    private static void CommandsStayPlantedBeforeFollowing(FrontierRuntime app)
    {
        for(int companion=0;companion<3;companion++)
        {
            Prepare(app,companion);var g=app.Game;AroundHouse(app);Time.unscaledDeltaTime=.02f;Input.Clear();Mouse(app,g.Position+new Point(4,0));Key(app,KeyCode.Z);
            ActorAction action=g.companionAction;Check(action!=null&&action.kind=="command"&&!action.released&&Near(action.contact,.2f)&&Near(action.duration,.52f),"Actual Z begins the original command while companion has follow work");
            Point feet=g.companionPosition,direction=action.direction;Point? target=action.target;ImpactProfile impact=action.impact;float gait=g.companionMoveTime;int frames=0;
            while(g.companionAction!=null&&frames++<40)
            {
                Motion(frames<=8?new Point(1,0):new Point());Update(app);
                Check(Same(g.companionPosition,feet)&&Near(g.companionMoveTime,gait)&&!g.companionMoving,"Automatic pathfinding cannot move planted command feet or animate walking before recovery ends");
                Check(Same(action.direction,direction)&&action.target.HasValue==target.HasValue&&(!target.HasValue||Same(action.target.Value,target.Value))&&ReferenceEquals(action.impact,impact),"Follow repair preserves captured manual direction, point and impact ownership");
            }
            Input.Clear();Check(frames<40&&action.released&&g.LastCompanionReceipt!=null&&g.LastCompanionReceipt.id==companion&&g.companionCooldown>17,"Original command release, receipt and cooldown complete before locomotion resumes");
            Point before=g.companionPosition;Update(app);Check(!Same(g.companionPosition,before)&&g.companionMoving,"Existing follow locomotion resumes after the original command duration");
        }
        Console.WriteLine("PASS actual three-companion Z commands retain .2/.52 clocks, planted feet/gait, captured direction/point/impact and genuine receipts, then resume following after recovery; NATIVE_UNVERIFIED");
    }
    private static void RepeatedOnGuiDoesNotAdvanceFollowing(FrontierRuntime app)
    {
        foreach(bool reduced in new[]{false,true})foreach(int fx in new[]{0,2})
        {
            Prepare(app);var g=app.Game;AroundHouse(app);g.save.reducedMotion=reduced;g.save.effectsQuality=fx;string before=State(g);Point feet=g.companionPosition;
            for(int draw=0;draw<12;draw++){Event.current.type=draw%2==0?EventType.Layout:EventType.Repaint;Draw(app);Check(State(g)==before&&Same(g.companionPosition,feet),"Repeated actual OnGUI cannot create effects, move feet or advance follow/gait/save state under motion/FX policy");}
            Event.current.type=EventType.Repaint;Check(CatchUp(app)<1000&&(Follow(g)-g.companionPosition).Length<=.3001f,"Actual Update still finishes the same detour after read-only draws");
        }
        Console.WriteLine("PASS repeated actual Layout/Repaint in full/reduced motion and FX0/2 is entirely follow/effects/action/save read-only; simulation alone completes catch-up; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        captureFolder=args.Length>0&&args[0]!="-"?args[0]:null;capturePrefix=args.Length>1?args[1]:"qa-companion-follow";
        string previous=Application.persistentDataPath,temporary=Path.Combine(Path.GetTempPath(),"starfall-companion-follow-save-"+Guid.NewGuid());Application.persistentDataPath=temporary;Directory.CreateDirectory(temporary);var app=new FrontierRuntime();
        try
        {
            Call(app,"Awake");if(app.Game==null){Console.WriteLine("FAIL Awake cannot load shipped campaign");return 1;}
            Action<FrontierRuntime>[] tests={NaturalRouteCatchesUpForEveryCompanion,MenusAndPauseFreezeThenResumeCatchUp,CommandsStayPlantedBeforeFollowing,RepeatedOnGuiDoesNotAdvanceFollowing};int failures=0;
            foreach(var test in tests)try{test(app);}catch(Exception error){failures++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}
            Console.WriteLine("Runtime companion follow: "+(tests.Length-failures)+"/"+tests.Length+" groups passed; NATIVE_UNVERIFIED");return failures==0?0:1;
        }
        finally{app.EndVisualReview();Call(app,"OnDestroy");Application.persistentDataPath=previous;if(Directory.Exists(temporary))Directory.Delete(temporary,true);}
    }
}
