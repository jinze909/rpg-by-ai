// Offline production Update/OnGUI and SaveStore checks. Native Unity remains unverified.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Starfall;
using UnityEngine;

public static class RuntimeTideForecastChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string captureFolder, capturePrefix;
    private sealed class Label { public string text = null; public float[] r = null; public int size = 0; }
    private static void Check(bool value,string message) { if(!value)throw new Exception(message); }
    private static bool Near(float a,float b,float tolerance=.004f) { return Math.Abs(a-b)<=tolerance; }
    private static object Call(object target,string name,params object[] args)
    { var method=target.GetType().GetMethod(name,Private|BindingFlags.Public);Check(method!=null,"Actual production entry point exists: "+name);return method.Invoke(target,args); }
    private static void Set(FrontierRuntime app,string name,object value) { typeof(FrontierRuntime).GetField(name,Private).SetValue(app,value); }
    private static T Get<T>(FrontierRuntime app,string name) { return (T)typeof(FrontierRuntime).GetField(name,Private).GetValue(app); }
    private static void Update(FrontierRuntime app) { Call(app,"Update");Input.keys.Clear(); }
    private static void Frames(FrontierRuntime app,int n) { for(int i=0;i<n;i++)Update(app); }
    private static void Key(FrontierRuntime app,KeyCode key) { Input.keys.Add(key);Update(app); }
    private static void Draw(FrontierRuntime app,string capture=null)
    {
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        if(captureFolder!=null&&capture!=null)
        {
            var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder,capturePrefix+"-"+capture);
            GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static string Expected(GameCore g)
    {
        float time=g.save.totalTime,deadline=Math.Max(time,g.save.tideHeldUntil),phase=time%36;
        bool low=time<g.save.tideHeldUntil||phase<22;
        float remaining=low?deadline-time+(deadline%36<22?22-deadline%36:0):36-phase;
        string seconds=Math.Ceiling(remaining).ToString("0",CultureInfo.InvariantCulture);
        return low?g.L("低潮 · 距涨潮 ","LOW TIDE · rises in ")+seconds+g.L("秒\n潮滩可通行","s\nShoals open"):
            g.L("高潮 · 距退潮 ","HIGH TIDE · ebb in ")+seconds+g.L("秒\n等待或使用闸门","s\nWait or use sluice");
    }
    private static Label AssertForecast(FrontierRuntime app)
    {
        string expected=Expected(app.Game);
        string command=GUI.commands.Find(c=>c.Contains("\"text\":"+GUI.S(expected)));
        Check(command!=null,"Actual exploration HUD exposes numeric time to the next real tide transition: "+expected);
        var label=JsonUtility.FromJson<Label>(command);
        Check(Near(label.r[0],39)&&Near(label.r[1],156)&&Near(label.r[2],271)&&Near(label.r[3],42),"Forecast retains the original conditions panel text bounds");
        Check(app.Game.Conditions()==expected,"Visible countdown and authoritative Conditions agree");return label;
    }
    private static void Prepare(FrontierRuntime app)
    {
        Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;
        Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;Event.current.type=EventType.Repaint;
        app.BeginVisualReview(1,5);app.VisualReviewPaused=false;var g=app.Game;
        g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();g.effects.Clear();g.fallen.Clear();
        g.save.companion=-1;g.save.english=true;g.save.largeText=false;g.save.showGuidance=false;g.save.reducedMotion=true;g.save.effectsQuality=0;
        g.save.energy=100;g.save.health=g.save.maxHealth;g.save.tideHeldUntil=0;g.save.totalTime=10;g.save.relics=new[]{-1,-1};g.save.talents=new int[3];
        g.save.foodUntil=g.save.teaUntil=g.save.mealUntil=0;g.toastTime=g.hitStop=0;
        Set(app,"applicationPaused",false);Set(app,"combatHudTime",0f);Set(app,"resumeTime",0f);Set(app,"arrivalTime",0f);
        Set(app,"pointerSeen",false);Set(app,"pointerAimActive",false);Set(app,"controllerUsed",false);
        Check(app.SetVisualReviewPosition(new Point(15,14)),"Harbor fixture uses real safe footing");Set(app,"camera",g.Position);Call(app,"Resize");Pointer(app,new Point(900,390));
    }
    private static void Pointer(FrontierRuntime app,Point p)
    {float scale=Get<float>(app,"scale");Input.mousePosition=new Vector3(Get<float>(app,"offsetX")+p.x*scale,Screen.height-Get<float>(app,"offsetY")-p.y*scale,0);}
    private static void NaturalTransitionsAndPausedClock(FrontierRuntime app)
    {
        Prepare(app);var g=app.Game;Draw(app);AssertForecast(app); // Semantic guard also compiles against old production.
        g.save.totalTime=21.97f;Draw(app);AssertForecast(app);Check(Expected(g).Contains("rises in 1s"),"Positive fractional low water never displays zero");
        Frames(app,2);Draw(app);AssertForecast(app);Check(!g.LowTide&&Expected(g).Contains("ebb in 14s"),"Real Update crosses the original 22-second high-water boundary");
        g.save.totalTime=35.97f;Frames(app,2);Draw(app);AssertForecast(app);Check(g.LowTide&&Expected(g).Contains("rises in 22s"),"Real Update crosses the original 36-second ebb boundary");
        g.save.totalTime=35.97f;g.save.tideHeldUntil=36;Frames(app,2);Draw(app);AssertForecast(app);
        Check(g.LowTide&&Expected(g).Contains("rises in 22s"),"Hold expiry during natural low water truthfully extends to the next high-water boundary");
        string before=Expected(g);Key(app,KeyCode.I);float time=g.save.totalTime;Frames(app,20);Check(g.mode=="inventory"&&g.save.totalTime==time&&Expected(g)==before,"Real inventory pauses authoritative tide clock");Key(app,KeyCode.Escape);
        Call(app,"OnApplicationPause",true);time=g.save.totalTime;Frames(app,5);Check(g.save.totalTime==time,"Application pause freezes tide countdown");Call(app,"OnApplicationPause",false);
        app.VisualReviewPaused=true;Frames(app,5);Check(g.save.totalTime==time,"Paused review freezes tide countdown");app.VisualReviewPaused=false;
        g.hitStop=.12f;Frames(app,3);Check(g.save.totalTime==time,"Actual hit stop leaves tide time frozen");g.hitStop=0;Frames(app,1);Draw(app);AssertForecast(app);Check(g.save.totalTime>time,"Countdown resumes with world simulation");
        Console.WriteLine("PASS real Update/OnGUI natural 22/36 boundaries, positive ceiling countdown, hold expiry during natural low water, inventory/application/review/hit-stop pause and resumed clock; NATIVE_UNVERIFIED");
    }
    private static void Interact(FrontierRuntime app,string kind)
    {var target=Array.Find(app.Game.Map.objects,o=>o.kind==kind);Check(target!=null&&app.SetVisualReviewPosition(new Point(target.x,target.y-1)),"Real harbor interaction has valid footing: "+kind);Key(app,KeyCode.E);Check(app.Game.mode=="dialog"&&app.Game.conversation!=null,"Real E opens original harbor dialogue");}
    private static void Choose(FrontierRuntime app,string action)
    {
        int index=Array.FindIndex(app.Game.conversation.choices,c=>c.action==action);Check(index>=0&&index<9,"Original numbered option exists: "+action);
        for(int i=0;i<20;i++){Draw(app);if((bool)Call(app,"DialogueChoicesVisible"))break;Key(app,KeyCode.Return);}
        Check((bool)Call(app,"DialogueChoicesVisible"),"Original pages finish before selecting option");Key(app,(KeyCode)((int)KeyCode.Alpha1+index));
    }
    private static void OriginalSluicePowerAndSavedContinuity(FrontierRuntime app)
    {
        Prepare(app);var g=app.Game;Draw(app);AssertForecast(app);g.save.totalTime=30;g.save.inventory[1]=7;g.save.coins=73;g.save.inventory[0]=11;
        Interact(app,"sluice");float at=g.save.totalTime;Choose(app,"holdtide");
        Check(g.save.inventory[1]==5&&Near(g.save.tideHeldUntil-at,90)&&g.LowTide,"Actual E/numbered sluice keeps exactly two crystals and original 90-second hold");
        g.Close();Draw(app);AssertForecast(app);Check(Expected(g).Contains("rises in 100s"),"Sluice ending in natural low tide advertises the actual continuous open interval");
        float deadline=g.save.tideHeldUntil;g.PrepareSave();Check(SaveStore.Write(g.save,0),"Original checksum writer accepts tide hold");string warning;var saved=SaveStore.Read(0,out warning);Check(saved!=null&&warning==null,"Original checksum reader accepts tide hold");string raw=JsonUtility.ToJson(saved);
        Check(!raw.Contains("TideChangeRemaining"),"Derived forecast adds no save fields");g.Load(saved);g.Start();g.enemies.Clear();g.hazards.Clear();Draw(app);AssertForecast(app);
        Check(Near(g.save.tideHeldUntil,deadline,.0001f)&&g.save.inventory[1]==5&&g.save.coins==73&&g.save.inventory[0]==11,"Checksum restoration preserves original deadline and balances: hold="+g.save.tideHeldUntil+"/"+deadline+", crystal="+g.save.inventory[1]+", coins="+g.save.coins+", wood="+g.save.inventory[0]);
        foreach(int version in new[]{1,2})
        {
            var old=JsonUtility.FromJson<SaveData>(raw);old.version=version;old.totalTime=30;old.tideHeldUntil=120;
            File.WriteAllText(SaveStore.PathFor(1),JsonUtility.ToJson(old));var loaded=SaveStore.Read(1,out warning);Check(loaded!=null&&warning==null,"Raw legacy v"+version+" loads through original SaveStore");
            g.Load(loaded);g.Start();g.enemies.Clear();g.hazards.Clear();Draw(app);AssertForecast(app);
            Check(g.save.totalTime==30&&g.save.tideHeldUntil==120&&Expected(g).Contains("rises in 100s")&&g.save.inventory[1]==5,"Legacy v"+version+" retains original hold and materials");
        }
        Prepare(app);g=app.Game;g.save.totalTime=30;int crystals=g.save.inventory[1];var seal=Array.Find(g.Map.objects,o=>o.kind=="seal2");
        Check(seal!=null&&app.SetVisualReviewPosition(new Point(seal.x+1,seal.y)),"Power fixture uses original harbor seal and legal footing");Check(!g.Has("seal_5")&&g.Ability(2),"Original second-core ability is owned and seal unopened");
        Key(app,KeyCode.R);Draw(app);AssertForecast(app);
        Check(g.Has("seal_5")&&Near(g.save.tideHeldUntil-g.save.totalTime,60)&&g.save.inventory[1]==crystals,"Real R keeps original seal unlock, 60-second tide hold and no crystal cost");
        Console.WriteLine("PASS actual E/numbered sluice keeps 2-crystal/90s rule, natural-low extension, real R keeps 60s/core-two unlock, original checksum and raw legacy v1/v2 deadlines/materials/schema continuity; NATIVE_UNVERIFIED");
    }
    private static void Append(StringBuilder text,object value,HashSet<object> seen)
    {
        if(value==null){text.Append("null;");return;}Type type=value.GetType();if(type.IsPrimitive||type.IsEnum||value is string||value is decimal){text.Append(Convert.ToString(value,CultureInfo.InvariantCulture)).Append(';');return;}
        if(!type.IsValueType&&!seen.Add(value)){text.Append("ref;");return;}var sequence=value as IEnumerable;if(sequence!=null){foreach(object item in sequence)Append(text,item,seen);text.Append("end;");return;}
        var fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);Array.Sort(fields,(a,b)=>string.CompareOrdinal(a.Name,b.Name));foreach(var field in fields){text.Append(field.Name).Append('=');Append(text,field.GetValue(value),seen);}
    }
    private static string State(GameCore g) {var text=new StringBuilder();Append(text,g,new HashSet<object>());return text.ToString();}
    private static void BilingualLayoutsPurityAndInput(FrontierRuntime app)
    {
        Prepare(app);Draw(app);AssertForecast(app);
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(int width in new[]{960,1280})foreach(int state in new[]{0,1,2})
        {
            Prepare(app);var g=app.Game;g.save.english=english;g.save.largeText=large;g.save.totalTime=state==0?21.5f:30;g.save.tideHeldUntil=state==2?120:0;
            Screen.width=width;Screen.height=width==960?600:800;Call(app,"Resize");Draw(app,"layout-"+(english?"en":"zh")+(large?"-large":"-normal")+"-"+width+"-"+state);var label=AssertForecast(app);
            Check(label.size==(large?14:13),"Forecast respects original large-text policy");Check(GUI.buttonCalls.FindAll(b=>b.rect.x==24&&b.rect.y==145).Count==0,"Forecast invents no action control");
        }
        Prepare(app);var game=app.Game;Key(app,KeyCode.J);ActorAction action=game.playerAction;Check(action!=null&&!action.released,"Real paid attack is prepared");game.skillCooldown=.12f;game.Skill(new Point(1,0));
        string before=State(game);float clock=Get<float>(app,"clock");var matrix=Matrix4x4.TRS(new Vector3(13,21,0),Quaternion.Euler(0,0,11),new Vector3(1.25f,1.25f,1));var tint=new Color(.3f,.4f,.5f,.6f);
        foreach(EventType evt in new[]{EventType.Layout,EventType.Repaint})
        {Event.current.type=evt;GUI.matrix=matrix;GUI.color=tint;GUI.enabled=false;Draw(app);AssertForecast(app);Check(State(game)==before&&Get<float>(app,"clock")==clock&&ReferenceEquals(action,game.playerAction),"Repeated actual GUI preserves all Core/save/paid-action/queue state and presentation clock");Check(GUI.matrix.Equals(matrix)&&GUI.color.Equals(tint)&&!GUI.enabled,"Actual GUI restores caller state");}
        Event.current.type=EventType.Repaint;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;
        Prepare(app);game=app.Game;Draw(app);Point panel=new Point(100,170);Check((bool)Call(app,"PointerOverInterface",panel),"Original conditions-panel pointer protection remains unchanged");Pointer(app,panel);Input.buttons.Add(0);Update(app);Input.buttons.Clear();Check(game.playerAction==null,"Original climate panel still blocks held attacks");
        Point world=new Point(900,390);Check(!(bool)Call(app,"PointerOverInterface",world),"Unobstructed world pointer remains available");Pointer(app,world);Input.buttons.Add(0);Update(app);Input.buttons.Clear();Check(game.playerAction!=null&&game.playerAction.kind=="attack","Real outside-panel mouse attack is preserved");
        Set(app,"combatHudTime",3f);Draw(app);Check(!GUI.commands.Exists(c=>c.Contains("\"text\":"+GUI.S(Expected(game)))),"New exploration forecast does not replace compact combat climate");
        Console.WriteLine("PASS 24 zh/en normal/large 960/1280 actual HUD captures in low/high/held phases, original text geometry and font policy, pure repeated GUI with paid action, original pointer protection/world attack and unchanged combat climate; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-tide-forecast-"+Guid.NewGuid());Application.persistentDataPath=directory;
        captureFolder=args.Length>0&&args[0]!="-"?args[0]:null;capturePrefix=args.Length>1?args[1]:"qa-tide-forecast";if(captureFolder!=null)Directory.CreateDirectory(captureFolder);
        var app=new FrontierRuntime();int failed=0;
        try{Call(app,"Awake");Check(app.Game!=null,"Actual Awake loads shipped campaign");foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{NaturalTransitionsAndPausedClock,OriginalSluicePowerAndSavedContinuity,BilingualLayoutsPurityAndInput})try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}}
        finally{app.EndVisualReview();Call(app,"OnDestroy");Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime tide forecast: "+(3-failed)+"/3 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
