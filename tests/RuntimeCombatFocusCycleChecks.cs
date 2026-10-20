// Actual Update/OnGUI/SaveStore paths using offline Unity declarations.
// These checks do not establish native import, GPU rendering or play feel.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeCombatFocusCycleChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string captureFolder;
    private static int captures;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static bool Near(float a, float b, float tolerance=.003f) { return Math.Abs(a-b)<=tolerance; }
    private static bool Same(Rect a, Rect b) { return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height); }
    private static bool Same(Color a, Color b) { return Near(a.r,b.r)&&Near(a.g,b.g)&&Near(a.b,b.b)&&Near(a.a,b.a); }
    private static object Call(object obj, string name, params object[] args)
    { var method=obj.GetType().GetMethod(name,Private|BindingFlags.Public); Check(method!=null,"Production entry point exists: "+name); return method.Invoke(obj,args); }
    private static void Set(object obj, string name, object value)
    { var field=obj.GetType().GetField(name,Private); Check(field!=null,"Fixture field exists: "+name); field.SetValue(obj,value); }
    private static T Get<T>(object obj, string name) { return (T)obj.GetType().GetField(name,Private).GetValue(obj); }
    // Reflection permits the same harness to compile against the prior checkout;
    // a missing feature then fails its real input contract, not C# compilation.
    private static Enemy Target(GameCore g)
    { var property=typeof(GameCore).GetProperty("CombatFocusTarget"); return property==null?null:(Enemy)property.GetValue(g,null); }
    private static void Update(FrontierRuntime app) { Call(app,"Update"); Input.keys.Clear(); }
    private static void Frames(FrontierRuntime app,int count) { for(int i=0;i<count;i++)Update(app); }
    private static void Key(FrontierRuntime app,KeyCode key) { Input.keys.Add(key);Update(app); }
    private static void Pointer(FrontierRuntime app,Point point)
    { float scale=Get<float>(app,"scale"); Input.mousePosition=new Vector3(Get<float>(app,"offsetX")+point.x*scale,Screen.height-Get<float>(app,"offsetY")-point.y*scale,0); }
    private static Point ScreenPoint(FrontierRuntime app,Point point) { return (Point)Call(app,"ScreenPoint",point); }
    private static void Mouse(FrontierRuntime app,Point point) { Pointer(app,ScreenPoint(app,point)); }
    private static void Draw(FrontierRuntime app,string capture=null)
    {
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        if(captureFolder!=null&&capture!=null)
        {
            var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder,"qa-focus-cycle-"+capture);captures++;
            GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static Rect FocusButton(bool combat) { return combat?new Rect(1030,105,230,32):new Rect(1015,489,234,32); }
    private static void Click(FrontierRuntime app,Rect rect)
    { Draw(app);Check(GUI.buttonCalls.Exists(b=>Same(b.rect,rect)&&b.enabled),"Actual focus control is displayed and enabled");GUI.queuedClick=rect;Draw(app);Check(!GUI.queuedClick.HasValue,"Actual focus button consumes its click"); }
    private static void Prepare(FrontierRuntime app,int profession=1,int room=1)
    {
        Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;
        Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;Event.current.type=EventType.Repaint;
        app.BeginVisualReview(profession,room);app.VisualReviewPaused=false;var g=app.Game;
        g.enemies.Clear();g.effects.Clear();g.projectiles.Clear();g.hazards.Clear();g.fallen.Clear();g.save.companion=-1;
        g.save.english=true;g.save.largeText=false;g.save.showGuidance=false;g.save.reducedMotion=true;g.save.effectsQuality=0;
        g.save.x=12;g.save.y=14;g.save.energy=100;g.save.health=g.save.maxHealth;g.save.heat=g.save.cold=0;
        g.save.relics=new[]{-1,-1};g.save.technique=0;g.save.seed=1;g.save.totalTime=100;g.invulnerable=100;g.toastTime=0;
        Set(app,"camera",g.Position);Set(app,"applicationPaused",false);Set(app,"controllerUsed",false);Set(app,"pointerSeen",false);
        Set(app,"pointerAimActive",false);Set(app,"combatHudTime",0f);Set(app,"resumeTime",0f);Set(app,"arrivalTime",0f);
        Call(app,"Resize");Mouse(app,g.Position+new Point(4,0));
    }
    private static Enemy Enemy(FrontierRuntime app,float x=4,float y=0,int id=93001)
    { var p=app.Game.Position+new Point(x,y);var e=new Enemy(id,0,p.x,p.y,500,false){stun=100,timer=9999};app.Game.enemies.Add(e);return e; }
    private static Enemy Lock(FrontierRuntime app)
    { Enemy e=Enemy(app);Key(app,KeyCode.V);Check(ReferenceEquals(Target(app.Game),e),"Actual V Update acquires the live eligible target");return e; }
    private static void Released(FrontierRuntime app,ActorAction action)
    { for(int i=0;i<45&&!action.released;i++)Update(app);Check(action.released,"Actual simulation reaches the existing contact boundary"); }
    private static string SaveState(GameCore g) { return JsonUtility.ToJson(g.save); }

    private static Rect CycleButton(bool combat) { return combat?new Rect(1030,143,230,32):new Rect(1015,527,234,32); }
    private static bool CycleVisible(FrontierRuntime app)
    { var p=typeof(FrontierRuntime).GetProperty("CombatFocusCycleVisible",Private);return p!=null&&(bool)p.GetValue(app,null); }
    private static Enemy[] Pair(FrontierRuntime app,float distance=6)
    {
        Enemy east=Enemy(app,distance,0,94001),south=Enemy(app,distance,2,94002);Key(app,KeyCode.V);
        Check(ReferenceEquals(Target(app.Game),east),"Fixture acquires east through actual V");return new[]{east,south};
    }
    private static void ActualCycleEdgesAndReleasePriority(FrontierRuntime app)
    {
        Prepare(app);Enemy[] pair=Pair(app);var g=app.Game;string before=SaveState(g);float beforeTime=g.save.totalTime;int revision=g.revision;
        Key(app,KeyCode.X);Check(ReferenceEquals(Target(g),pair[1]),"Actual fresh X advances the live actor without releasing");
        Input.held.Add(KeyCode.X);Frames(app,24);Input.held.Clear();Check(ReferenceEquals(Target(g),pair[1]),"Held X never repeatedly cycles");
        Input.held.Add(KeyCode.LeftShift);Key(app,KeyCode.X);Input.held.Clear();Check(ReferenceEquals(Target(g),pair[0]),"Shift+X reverses to the previous actor");
        Input.held.Add(KeyCode.RightShift);Key(app,KeyCode.X);Input.held.Clear();Check(ReferenceEquals(Target(g),pair[1]),"Right Shift also reverses, wrapping safely");
        Key(app,KeyCode.JoystickButton8);Check(ReferenceEquals(Target(g),pair[0])&&Get<bool>(app,"controllerUsed"),"Actual L3 cycles and takes controller presentation ownership");
        Input.held.Add(KeyCode.JoystickButton8);Frames(app,24);Input.held.Clear();Check(ReferenceEquals(Target(g),pair[0]),"Held L3 never repeatedly cycles");
        Input.keys.Add(KeyCode.V);Input.keys.Add(KeyCode.X);Update(app);Check(Target(g)==null,"Simultaneous V+X preserves explicit release priority");
        Key(app,KeyCode.X);Check(Target(g)==null,"X alone never acquires from free aim");
        Key(app,KeyCode.JoystickButton9);Check(Target(g)!=null,"Original R3 can acquire after release");Input.keys.Add(KeyCode.JoystickButton9);Input.keys.Add(KeyCode.JoystickButton8);Update(app);
        Check(Target(g)==null,"Simultaneous R3+L3 preserves original release priority");
        Check(g.playerAction==null&&g.skillCooldown<=0&&g.dashTime<=0&&g.save.energy==100&&g.revision==revision,"Cycling and releasing never cast, charge or alter campaign revision");
        // Tick legitimately advances total time; compare the unchanged campaign payload after restoring that observation only.
        float now=g.save.totalTime;g.save.totalTime=beforeTime;Check(SaveState(g)==before,"Input-only selection leaves every persistent campaign field unchanged");g.save.totalTime=now;
        Prepare(app);Enemy single=Lock(app);Key(app,KeyCode.X);Check(ReferenceEquals(Target(app.Game),single)&&!CycleVisible(app),"One eligible actor neither releases nor exposes a no-op cycle control");
        Console.WriteLine("PASS actual X/left-right Shift+X/L3 edges and held input, release priority, no acquisition/one-target no-op and no action/resource/progress mutation; NATIVE_UNVERIFIED");
    }
    private static void AcceptedQueuedAndFlyingActionsKeepTheirSnapshots(FrontierRuntime app)
    {
        for(int profession=0;profession<3;profession++)
        {
            Prepare(app,profession);Enemy[] pair=Pair(app);var g=app.Game;Input.held.Add(KeyCode.J);Update(app);Input.held.Clear();
            ActorAction accepted=g.playerAction;Check(accepted!=null&&accepted.kind=="attack"&&accepted.target.HasValue,"Actual J accepts the original focused action for class "+profession);
            Point direction=accepted.direction,target=accepted.target.Value;float energy=g.save.energy;Key(app,KeyCode.X);
            Check(ReferenceEquals(Target(g),pair[1])&&g.aim.y>.2f,"Live aim follows the explicitly cycled actor during the accepted swing");
            Check(ReferenceEquals(g.playerAction,accepted)&&(accepted.direction-direction).Length<.0001f&&(accepted.target.Value-target).Length<.0001f&&(g.VisualFacing-direction).Length<.0001f,"Cycling cannot rewrite prepared action, target or authored facing");
            Check(g.save.energy>=energy,"Cycling does not charge an accepted attack twice");Released(app,accepted);
            if(profession>0)
            {
                Check(g.projectiles.Count==1,"Original ranged contact emits one actual missile");Projectile shot=g.projectiles[0];Point velocity=shot.velocity;Key(app,KeyCode.X);pair[0].y-=1;Frames(app,3);
                Check(g.projectiles.Contains(shot)&&(shot.velocity-velocity).Length<.0001f,"Already flying missile never homes after target cycling");
            }
            Frames(app,40);Input.held.Add(KeyCode.J);Update(app);Input.held.Clear();ActorAction next=g.playerAction;Enemy chosen=Target(g);
            Check(next!=null&&next!=accepted&&chosen!=null&&next.target.HasValue&&(next.target.Value-chosen.Position).Length<.0001f,"Next actual J captures the newly selected actor");
        }
        Prepare(app,1);Enemy[] queuePair=Pair(app);var game=app.Game;Input.held.Add(KeyCode.J);Update(app);Input.held.Clear();ActorAction attack=game.playerAction;Frames(app,9);Key(app,KeyCode.Q);
        Check(ReferenceEquals(game.playerAction,attack)&&game.skillCooldown<=0&&Near(game.save.energy,100),"Actual near-recovery Q queues without early resource charge");
        Key(app,KeyCode.X);Check(ReferenceEquals(Target(game),queuePair[1]),"Fresh X changes live lock while Q is queued");ActorAction cast=null;
        for(int i=0;i<20&&cast==null;i++){Update(app);if(game.playerAction!=null&&game.playerAction.kind=="skill")cast=game.playerAction;}
        Check(cast!=null&&cast.direction.x>.999f&&Math.Abs(cast.direction.y)<.001f&&cast.target.HasValue&&(cast.target.Value-queuePair[0].Position).Length<.0001f,"Buffered Q keeps its pressed target and direction across cycling");
        Check(Near(game.save.energy,60,.1f)&&game.skillCooldown>3.4f,"Queued Q starts once for the existing 40 energy and 3.5 second cooldown");
        Console.WriteLine("PASS three-class real J prepared snapshots, next action selection, nonhoming missiles and actual buffered Q identity/cost/cooldown; NATIVE_UNVERIFIED");
    }
    private static bool Overlap(Rect a,Rect b) { return a.x<b.x+b.width&&a.x+a.width>b.x&&a.y<b.y+b.height&&a.y+a.height>b.y; }
    private static void ActualHudPointerDangerAndCompleteHelp(FrontierRuntime app)
    {
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(bool combat in new[]{false,true})
        {
            Prepare(app);Enemy[] pair=Pair(app,9);var g=app.Game;g.save.english=english;g.save.largeText=large;Set(app,"combatHudTime",combat?3f:0f);Rect rect=CycleButton(combat),old=FocusButton(combat);string before=SaveState(g);float beforeTime=g.save.totalTime;
            Draw(app,(english?"en":"zh")+(large?"-large":"-normal")+(combat?"-combat":"-explore"));
            Check(CycleVisible(app)&&GUI.buttonCalls.Exists(b=>Same(b.rect,rect)&&b.enabled)&&GUI.buttonCalls.Exists(b=>Same(b.rect,old)&&b.enabled),"Actual HUD exposes separate cycle and unchanged release controls");
            Check(!Overlap(rect,old)&&GUI.buttonCalls.TrueForAll(b=>Same(b.rect,rect)||!Overlap(rect,b.rect)),"Cycle control does not overlap another enabled HUD control");
            string caption=english?"Next target":"切换目标";Check(GUI.commands.Exists(c=>c.Contains("\"k\":\"text\"")&&c.Contains(caption)&&c.Contains("\"size\":16")),"Cycle caption uses the shipped readable bilingual button font");
            Point inside=new Point(rect.x+10,rect.y+10),beside=new Point(rect.x-2,rect.y+10);
            Check((bool)Call(app,"PointerOverInterface",inside)&&!(bool)Call(app,"PointerOverInterface",beside),"Actual cycle pointer blocker matches its exact visible bounds");
            Pointer(app,inside);Input.buttons.Add(0);Update(app);Input.buttons.Clear();Check(g.playerAction==null,"Actual mouse hold over cycle control cannot also attack");
            Set(app,"combatHudTime",combat?3f:0f);GUI.queuedClick=rect;Draw(app);Check(!GUI.queuedClick.HasValue&&ReferenceEquals(Target(g),pair[1]),"Actual OnGUI cycle click chooses the next actor");
            float totalTime=g.save.totalTime;g.save.totalTime=beforeTime;Check(SaveState(g)==before,"Rendering and clicking selection preserve every persistent campaign field");g.save.totalTime=totalTime;
            g.enemies.Remove(pair[0]);Set(app,"combatHudTime",combat?3f:0f);Draw(app);Check(!CycleVisible(app)&&!GUI.buttonCalls.Exists(b=>Same(b.rect,rect))&&!(bool)Call(app,"PointerOverInterface",inside),"Hidden single-target cycle has no stale button or world blocker");
        }
        foreach(bool combat in new[]{false,true})
        {
            Prepare(app);Enemy[] pair=Pair(app,9);Enemy threat=pair[0];threat.kind=2;threat.state=1;threat.target=threat.Position;threat.stun=0;threat.timer=.7f;threat.windupDuration=.9f;Rect rect=CycleButton(combat);
            Set(app,"combatHudTime",combat?3f:0f);Set(app,"camera",threat.Position-new Point((rect.x+rect.width/2-640)/36f,(rect.y+rect.height/2-390)/36f));Draw(app,combat?"danger-combat":"danger-explore");
            Check((bool)Call(app,"ThreatOver",rect),"Actual enemy warning overlaps the added cycle control");int fill=GUI.textureCalls.FindLastIndex(c=>c.texture==Texture2D.whiteTexture&&Same(c.rect,rect)&&c.color.a<=.121f);
            Check(fill>=0,"Cycle background actually fades beneath the threat");int contour=GUI.textureCalls.FindLastIndex(c=>c.texture==Texture2D.whiteTexture&&Near(c.rect.height,3)&&Same(c.color,PixelArt.C("#df8a68")));
            Check(contour>fill,"Actual danger contour is submitted above the new cycle panel");
        }
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(bool pad in new[]{false,true})
        {
            Prepare(app);app.Game.save.english=english;app.Game.save.largeText=large;Key(app,KeyCode.F1);GUI.queuedClick=new Rect(279,177,160,66);Draw(app);Check(!GUI.queuedClick.HasValue&&app.Game.mode=="help","Actual F1 controls topic opens");if(pad)Key(app,KeyCode.JoystickButton8);
            Draw(app,"help-"+(pad?"pad-":"keyboard-")+(english?"en":"zh")+(large?"-large":"-normal"));string cycle=pad?(english?"L3: next target":"L3：切换目标"):(english?"Shift+X: previous":"Shift+X：反向");string body=GUI.commands.Find(c=>c.Contains("\"k\":\"text\"")&&c.Contains(cycle));
            Check(body!=null,"Actual bilingual normal/large controls help includes new cycling binding");
            string[] retained=pad?(english?new[]{"Left stick: move","X: attack","A: dodge","Y: technique","B: talk, gather and operate","LB: stew","RB: companion command","VIEW: journal","START: pause","R3: lock / release target","A turns dialogue pages, then selects a choice."}:new[]{"左摇杆：移动","X：攻击","A：闪避","Y：战技","B：交谈、采集与机关","LB：炖汤","RB：同伴指令","VIEW：手帐","START：暂停","R3：锁定 / 解除目标","长对话用A翻页，再确认选项。"}):(english?new[]{"WASD / arrows: move","J / left click: attack","V: lock / release target","Space: dodge","Q: class technique","E: talk, gather and operate","F: stew","T: tea","Z: companion command","I inventory","G relics","K talents","C companions","Tab journal","M map","B projects","N contracts","F1 guide","Esc closes menus or pauses.","Space or Enter turns dialogue pages."}:new[]{"WASD / 方向键：移动","J / 鼠标左键：攻击","V：锁定 / 解除目标","空格：闪避","Q：职业技能","E：交谈、采集与机关","F：炖汤","T：凉茶","Z：同伴指令","I背包","G遗物","K成长","C同伴","Tab任务","M地图","B建设","N悬赏","F1指南","Esc关闭菜单或暂停。","长对话可用空格或Enter翻页。"});
            foreach(string binding in retained)Check(body.Contains(binding),"Existing help binding retained: "+binding);Check(Target(app.Game)==null&&app.Game.playerAction==null,"Reading help cannot cycle, lock or cast");
        }
        Console.WriteLine("PASS bilingual normal/large two-HUD cycle clicks, exact pointer ownership, no overlap/hidden blockers, actual fade and foreground threat witness, keyboard/pad controls retained; captures="+captures+"; NATIVE_UNVERIFIED");
    }
    private static void SessionSaveTravelAndReusedTrainingBoundaries(FrontierRuntime app)
    {
        foreach(string boundary in new[]{"inventory","pause","application","travel","save-load","reused-id","range","occluded","removed","death"})
        {
            Prepare(app);var g=app.Game;Pair(app);Key(app,KeyCode.X);Enemy selected=Target(g);
            if(boundary=="inventory"||boundary=="pause")Key(app,boundary=="inventory"?KeyCode.I:KeyCode.Escape);
            else if(boundary=="application")Call(app,"OnApplicationPause",true);
            else if(boundary=="travel")g.Enter(0);
            else if(boundary=="save-load")
            {
                g.PrepareSave();Check(SaveStore.Write(g.save,2),"Actual SaveStore accepts campaign with transient cycled focus");string warning;SaveData loaded=SaveStore.Read(2,out warning);Check(loaded!=null&&warning==null&&!File.ReadAllText(SaveStore.PathFor(2)).ToLowerInvariant().Contains("combatfocus"),"Checksummed schema contains no selected actor");g.Load(loaded);g.Start();
            }
            else if(boundary=="reused-id"){g.enemies.Remove(selected);Enemy(app,6,2,selected.id);}
            else if(boundary=="range")selected.x=g.Position.x+12.1f;
            else if(boundary=="occluded"){g.save.x=4;g.save.y=5;selected.x=8;selected.y=5;}
            else if(boundary=="removed")g.enemies.Remove(selected);
            else{g.invulnerable=0;Call(g,"Hurt",10000f,"environment",null);Check(g.Dying,"Real lethal event starts death boundary");}
            Key(app,KeyCode.X);Check(Target(g)==null&&!CycleVisible(app),"Cycling never reacquires or retains invalid lock across "+boundary);
            if(boundary=="application")Call(app,"OnApplicationPause",false);
            if(!g.Dying){g.Close();Key(app,KeyCode.X);Check(Target(g)==null,"Returning after "+boundary+" requires a new explicit lock");}
        }
        foreach(string boundary in new[]{"reset","layout","end"})
        {
            Prepare(app,1,0);var g=app.Game;WorldObject mentor=Array.Find(g.Map.objects,o=>o.kind=="mentor");g.save.x=mentor.x;g.save.y=mentor.y-.7f;Check(g.StartRange(0),"Original mentor range starts");Mouse(app,g.Position+new Point(4,0));Key(app,KeyCode.V);Check(Target(g)!=null,"Actual training actor can be locked");Key(app,KeyCode.X);
            if(boundary=="reset")Check(g.ResetRange(),"Range resets normally");else if(boundary=="layout")Check(g.ChangeRangeLayout(1),"Range changes normally");else g.EndPractice();Key(app,KeyCode.X);Check(Target(g)==null&&!CycleVisible(app),"Training "+boundary+" cannot cycle into reused target IDs");
        }
        Console.WriteLine("PASS menu/application/travel/checksummed saves, reused IDs/range/LOS/removal/death and actual mentor range reset/layout/end discard cycling without acquisition; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        captureFolder=args.Length>0?args[0]:null;string previous=Application.persistentDataPath;
        string temporary=Path.Combine(Path.GetTempPath(),"starfall-focus-cycle-save-"+Guid.NewGuid());Application.persistentDataPath=temporary;Directory.CreateDirectory(temporary);var app=new FrontierRuntime();
        try
        {
            Call(app,"Awake");if(app.Game==null){Console.WriteLine("FAIL Awake cannot load shipped campaign");return 1;}
            Action<FrontierRuntime>[] tests={ActualCycleEdgesAndReleasePriority,AcceptedQueuedAndFlyingActionsKeepTheirSnapshots,ActualHudPointerDangerAndCompleteHelp,SessionSaveTravelAndReusedTrainingBoundaries};int failures=0;
            foreach(var test in tests)try{test(app);}catch(Exception error){failures++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}
            Console.WriteLine("Runtime combat focus cycling "+(tests.Length-failures)+"/"+tests.Length+"; NATIVE_UNVERIFIED");return failures==0?0:1;
        }
        finally{app.EndVisualReview();Call(app,"OnDestroy");Application.persistentDataPath=previous;if(Directory.Exists(temporary))Directory.Delete(temporary,true);}
    }
}
