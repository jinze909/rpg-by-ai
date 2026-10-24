// Shipped Update/GetKeyDown, OnGUI and SaveStore paths with offline Unity
// declarations. No native device, Unity Player or visual acceptance is implied.
using System;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeCompanionCommandBufferChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [Serializable] public sealed class Label { public string k, text; public float[] r; }
    private static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
    private static bool Near(float a, float b, float tolerance=.003f) { return Math.Abs(a-b)<=tolerance; }
    private static object Call(object obj, string name, params object[] args)
    { var method=obj.GetType().GetMethod(name,Private|BindingFlags.Public);Check(method!=null,"Shipped entry point exists: "+name);return method.Invoke(obj,args); }
    private static void Set(object obj, string name, object value) { obj.GetType().GetField(name,Private).SetValue(obj,value); }
    private static T Get<T>(object obj, string name) { return (T)obj.GetType().GetField(name,Private).GetValue(obj); }
    // Reflection keeps the same harness compilable against the pre-feature
    // baseline. Missing buffering must fail behavior checks, not compilation.
    private static bool Queued(GameCore g)
    { var property=typeof(GameCore).GetProperty("CompanionCommandQueued");return property!=null&&(bool)property.GetValue(g,null); }
    private static void Update(FrontierRuntime app) { Call(app,"Update");Input.keys.Clear();Input.buttonDown.Clear(); }
    private static void Frames(FrontierRuntime app, int count) { for(int i=0;i<count;i++)Update(app); }
    private static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key);Update(app); }
    private static void Pointer(FrontierRuntime app, Point pixel)
    { float scale=Get<float>(app,"scale");Input.mousePosition=new Vector3(Get<float>(app,"offsetX")+pixel.x*scale,Screen.height-Get<float>(app,"offsetY")-pixel.y*scale,0); }
    private static void Mouse(FrontierRuntime app, Point world) { Pointer(app,(Point)Call(app,"ScreenPoint",world)); }
    private static void Draw(FrontierRuntime app)
    { GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI"); }
    private static bool Same(Rect a, Rect b)
    { return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height); }
    private static bool HasText(Rect rect, string text)
    {
        foreach(string command in GUI.commands)
        { var label=JsonUtility.FromJson<Label>(command);if(label.k=="text"&&label.text==text&&label.r!=null&&Same(rect,new Rect(label.r[0],label.r[1],label.r[2],label.r[3])))return true; }
        return false;
    }
    private static void Prepare(FrontierRuntime app, int companion=0)
    {
        Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;
        Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;Event.current.type=EventType.Repaint;
        app.BeginVisualReview(0,0);app.VisualReviewPaused=false;var g=app.Game;
        g.enemies.Clear();g.effects.Clear();g.projectiles.Clear();g.hazards.Clear();g.fallen.Clear();
        for(int id=0;id<3;id++)if(!g.Has("companion_"+id))g.save.flags.Add("companion_"+id);
        g.save.english=true;g.save.largeText=false;g.save.showGuidance=false;g.save.reducedMotion=true;g.save.effectsQuality=0;
        g.save.health=g.save.maxHealth;g.save.energy=100;g.save.relationships=new[]{3,3,3};g.save.frontierEnding=0;
        g.save.companion=companion;g.companionCooldown=0;g.companionAction=null;g.save.heat=g.save.cold=0;
        g.save.relics=new[]{-1,-1};g.save.talents=new int[3];g.save.foodUntil=g.save.teaUntil=g.save.mealUntil=0;
        g.save.controllerAimProfile=0;g.save.controllerAimInvertY=false;g.save.totalTime=100;g.invulnerable=100;g.toastTime=0;
        Check(app.SetVisualReviewPosition(new Point(12,14))&&(g.Position-new Point(12,14)).Length<.001f,"Fixture uses exact legal shipped town ground");
        Set(g,"companionAttack",100f);Set(app,"camera",g.Position);Set(app,"applicationPaused",false);
        Set(app,"controllerUsed",false);Set(app,"pointerSeen",false);Set(app,"pointerAimActive",false);
        Set(app,"combatHudTime",0f);Set(app,"resumeTime",0f);Set(app,"arrivalTime",0f);
        Call(app,"Resize");Call(app,"ResetControllerAim",false);Mouse(app,g.Position+new Point(4,0));Update(app);
    }
    private static void Queue(FrontierRuntime app, KeyCode key=KeyCode.Z, float remaining=.10f)
    { app.Game.companionCooldown=remaining+Time.unscaledDeltaTime;Key(app,key); }
    private static ActorAction AwaitCommand(FrontierRuntime app)
    {
        for(int frame=0;frame<20;frame++)
        { var action=app.Game.companionAction;if(action!=null&&action.kind=="command")return action;Update(app); }
        throw new Exception("A released last-window press never starts at readiness; cooldown="+app.Game.companionCooldown);
    }
    private static void NoLaterCommand(FrontierRuntime app, string boundary)
    {
        Input.Clear();
        for(int frame=0;frame<65;frame++)
        { Update(app);Check(!Queued(app.Game)&&(app.Game.companionAction==null||app.Game.companionAction.kind!="command")&&app.Game.LastCompanionReceipt==null,"No stale companion request crosses "+boundary); }
    }

    private static void FreshEdgesAcrossFrameRatesAndHeldDoesNotRetry(FrontierRuntime app)
    {
        int cases=0;
        for(int id=0;id<3;id++)foreach(KeyCode key in new[]{KeyCode.Z,KeyCode.JoystickButton5})foreach(float dt in new[]{1f/60,.02f,1f/30,.04f,.05f})
        {
            Prepare(app,id);var g=app.Game;Time.unscaledDeltaTime=dt;if(key==KeyCode.JoystickButton5)Input.axes["Aim Horizontal"]=1;
            g.save.health=g.save.maxHealth-3;float hp=g.save.health;Queue(app,key,.12f-.0000001f);
            Point? target=g.aimTarget;Point heading=target.HasValue?(target.Value-g.companionPosition).Normalized:g.aim;
            Check(g.companionAction==null&&g.companionCooldown>0&&g.companionCooldown<=.120001f&&Queued(g),"Real "+key+" tap accepts final .12s at dt="+dt+" for companion "+id);
            Check(Near(g.save.health,hp)&&Near(g.save.energy,100)&&g.LastCompanionReceipt==null&&g.CompanionFeedbackTime==0&&g.projectiles.Count==0&&g.effects.Count==0,"Acceptance has no early heal, cost, receipt, projectile or effect");
            if(key==KeyCode.JoystickButton5)Input.axes["Aim Horizontal"]=-1;else Mouse(app,g.Position+new Point(-4,0));
            ActorAction command=AwaitCommand(app);
            Check(!Queued(g)&&command.elapsed==0&&!command.released&&Near(g.companionCooldown,18),"Readiness starts one fresh command at frame end with original full cooldown");
            Check((command.direction-heading).Length<.003f&&command.target.HasValue==target.HasValue&&(!target.HasValue||(command.target.Value-target.Value).Length<.003f),"Live aim changes preserve the pressed companion heading and target");
            Frames(app,45);var receipt=g.LastCompanionReceipt;Check(receipt!=null&&receipt.id==id,"The real queued command eventually releases its own receipt");
            // Stage another near-ready interval while the same physical key is
            // held; GetKey must never manufacture another GetKeyDown edge.
            g.companionCooldown=.11f;Input.held.Add(key);Frames(app,15);Input.held.Clear();
            Check(!Queued(g)&&g.companionAction==null&&ReferenceEquals(receipt,g.LastCompanionReceipt)&&g.companionCooldown<=0,"Held Z/RB never renews or retries a released tap");cases++;
        }
        foreach(KeyCode key in new[]{KeyCode.Z,KeyCode.JoystickButton5})
        {
            Prepare(app);var g=app.Game;Queue(app,key,.1201f);Check(!Queued(g),"An edge strictly earlier than the window is rejected");
            Input.held.Add(key);Frames(app,15);Input.held.Clear();NoLaterCommand(app,"an early held-only "+key);
        }
        Console.WriteLine("PASS "+cases+" real Z/RB taps across Mira/Ivo/Sen at 60/50/30/25/20 fps: final .12s capture, no early transaction, frame-end elapsed zero, original aim/target and one release; early/held input never retries; NATIVE_UNVERIFIED");
    }

    private static Rect CommandSlot(bool compact) { return compact?new Rect(685,732,78,50):new Rect(702,699,109,75); }
    private static void BothHudLayoutsAcceptWithoutPretendingReady(FrontierRuntime app)
    {
        foreach(bool compact in new[]{false,true})foreach(bool english in new[]{false,true})
        {
            Prepare(app,1);var g=app.Game;g.save.english=english;Set(app,"combatHudTime",compact?3f:0f);Rect slot=CommandSlot(compact);
            g.companionCooldown=.1201f;Draw(app);Check(GUI.buttonCalls.Exists(b=>Same(b.rect,slot)&&!b.enabled),"Real companion HUD rejects a click outside the early window");
            g.companionCooldown=.10f;Point? target=g.aimTarget;Pointer(app,new Point(slot.x+15,slot.y+12));Draw(app);
            Check(GUI.buttonCalls.Exists(b=>Same(b.rect,slot)&&b.enabled),"Actual "+(compact?"combat":"exploration")+" HUD opts into last-window clicks");
            Check(HasText(new Rect(slot.x+3,slot.y+(compact?26:41),slot.width-6,compact?19:27),"1s"),"The clickable near-ready command keeps its real cooldown label");
            // Buttons retain their existing mint hover border. The separate
            // full-width bottom accent belongs only to actual ready actions.
            Color ready=PixelArt.C("#7acabb");Check(!GUI.textureCalls.Exists(t=>Same(t.rect,new Rect(slot.x+4,slot.y+slot.height-3,slot.width-8,2))&&Near(t.color.r,ready.r)&&Near(t.color.g,ready.g)&&Near(t.color.b,ready.b)),"Near-ready clickability does not draw the mint ready accent");
            GUI.queuedClick=slot;Draw(app);GUI.queuedClick=null;
            Check(Queued(g)&&g.companionAction==null&&Near(g.companionCooldown,.10f)&&g.LastCompanionReceipt==null,"Actual OnGUI click stores one pending command without ticking or releasing");
            for(int draw=0;draw<4;draw++){Event.current.type=draw%2==0?EventType.Layout:EventType.Repaint;Draw(app);}
            Event.current.type=EventType.Repaint;
            Check(Queued(g)&&Near(g.companionCooldown,.10f)&&g.companionAction==null&&GUI.commands.Exists(c=>english?c.IndexOf("queued",StringComparison.OrdinalIgnoreCase)>=0:c.Contains("等待指令")),"Repeated Layout/Repaint shows truthful queued feedback without simulating preparation");
            Mouse(app,g.Position+new Point(-4,0));ActorAction action=AwaitCommand(app);
            Check(action.target.HasValue==target.HasValue&&(!target.HasValue||(action.target.Value-target.Value).Length<.003f),"HUD-queued command keeps the world target sampled before its click");
            Check(action.elapsed==0&&!action.released&&Near(g.companionCooldown,18),"HUD queue uses the same fresh command start transaction");
        }
        Console.WriteLine("PASS both actual HUD layouts in English/Chinese: early-only enabled click, 1s cooldown label and no ready accent, truthful queued feedback, read-only Layout/Repaint and captured world target; NATIVE_UNVERIFIED");
    }

    private static void ExistingContactsReceiptsAndCapturedStartValues(FrontierRuntime app)
    {
        for(int id=0;id<3;id++)foreach(bool pact in new[]{false,true})
        {
            Prepare(app,id);var g=app.Game;g.save.frontierEnding=pact?3:0;g.save.health=g.save.maxHealth-3;
            Enemy foe=null;if(id==2){foe=new Enemy(86001,0,g.save.x+1,g.save.y,500,false){timer=100,staggerRecovery=100};g.enemies.Add(foe);}
            Queue(app,KeyCode.Z,.08f);Check(Queued(g)&&g.companionAction==null,"Contact fixture queues before readiness");
            // Damage/bond are captured at action start, not while waiting.
            g.save.talents[0]=2;float damage=g.Damage;ActorAction action=AwaitCommand(app);float total=pact?12:18;
            Check(action.weapon==id&&action.combo==3&&Near(action.damage,damage)&&Near(action.contact,.2f)&&Near(action.duration,.52f)&&action.elapsed==0&&Near(g.companionCooldown,total),"Start captures current damage/bond and retains original .2/.52 and 18/12s rules");
            Check(action.impact!=null&&!action.impact.playerOwned&&action.impact.weapon==(id==1?1:2),"Queued start retains a captured companion impact");
            g.save.relationships[id]=0;g.save.talents[0]=0;Frames(app,9);
            Check(!action.released&&g.LastCompanionReceipt==null&&g.projectiles.Count==0&&g.effects.Count==0,"Nine real .02s frames do not release a buffered command early");
            if(id==0)g.save.energy=98;Update(app);var receipt=g.LastCompanionReceipt;
            Check(action.released&&Near(action.elapsed,.2f)&&receipt!=null&&receipt.id==id&&receipt.bond==3&&Near(g.companionCooldown,total-.2f),"Tenth simulation frame releases once with captured bond and exact cooldown accounting");
            if(id==0)Check(Near(receipt.actualHeal,3)&&Near(receipt.actualEnergy,1.72f)&&Near(g.save.health,g.save.maxHealth)&&Near(g.guardTime,3)&&g.effects.Exists(e=>e.kind=="heal"),"Mira retains actual capped healing/energy and original three-second guard");
            else if(id==1)Check(receipt.arrowCount==3&&Near(receipt.arrowBaseDamage,damage*.95f)&&g.projectiles.Count==3&&g.projectiles.TrueForAll(p=>p.friendly&&p.piercing&&!p.explosive&&Near(p.damage,damage*.95f)&&!p.impact.playerOwned),"Ivo releases exactly three original piercing arrows with captured start damage");
            else Check(receipt.controlCount==1&&Near(receipt.frostPulseDamage,damage*.4f)&&Near(foe.hp,500-damage*.4f)&&Near(foe.stun,1.4f)&&g.effects.Exists(e=>e.kind=="frost"),"Sen retains the original field, .4 damage and genuine nearby 1.4s control receipt");
            Frames(app,35);Check(g.companionAction==null&&ReferenceEquals(receipt,g.LastCompanionReceipt)&&!Queued(g),"Original command recovery ends without a second action or receipt");
        }
        Console.WriteLine("PASS queued Mira/Ivo/Sen real .2s releases, .52s recovery, original 18/12s cooldowns, action-start damage/bond/ally impact snapshots, capped heal/energy/guard, three piercing arrows and genuine frost/control receipts; NATIVE_UNVERIFIED");
    }

    private static void Mentor(FrontierRuntime app)
    { var g=app.Game;WorldObject mentor=Array.Find(g.Map.objects,o=>o.kind=="mentor");Check(mentor!=null,"Shipped mentor exists");g.save.x=mentor.x;g.save.y=mentor.y-.7f;g.save.practiceBits=7; }
    private static void IndependentPlayerInputsAndSessionBoundaries(FrontierRuntime app)
    {
        foreach(KeyCode player in new[]{KeyCode.J,KeyCode.Q,KeyCode.Space})
        {
            Prepare(app);var g=app.Game;Queue(app);Input.axes["Horizontal"]=1;Key(app,player);Input.axes["Horizontal"]=0;
            Check(Queued(g)&&g.companionAction==null,"Fresh player "+player+" preserves independent companion intent");
            Check(player==KeyCode.Space?g.dashTime>0:g.playerAction!=null&&g.playerAction.kind==(player==KeyCode.J?"attack":"skill"),"Actual player input still starts its own original action");
            ActorAction action=AwaitCommand(app);Check(action.elapsed==0&&Near(g.companionCooldown,18),"Companion command starts independently after "+player);
        }
        KeyCode[] menuKeys={KeyCode.I,KeyCode.Escape,KeyCode.Tab,KeyCode.K,KeyCode.M,KeyCode.B,KeyCode.C,KeyCode.G,KeyCode.N,KeyCode.F1,KeyCode.JoystickButton7,KeyCode.JoystickButton6};
        string[] menuModes={"inventory","pause","journal","talents","map","camp","companions","loadout","bounties","help","pause","journal"};
        foreach(bool pact in new[]{false,true})for(int index=0;index<menuKeys.Length;index++)
        {
            Prepare(app);var g=app.Game;g.save.frontierEnding=pact?3:0;Queue(app);
            Check(Queued(g)&&g.companionAction==null,"Same-readiness menu fixture owns an unstarted request");
            // This actual Update would start the intent at its frame-end
            // readiness boundary unless the fresh menu edge cancels it first.
            g.companionCooldown=Time.unscaledDeltaTime;Key(app,menuKeys[index]);
            Check(g.mode==menuModes[index],"Actual "+menuKeys[index]+" opens its original "+menuModes[index]+" menu");
            Check(!Queued(g)&&g.companionAction==null&&g.LastCompanionReceipt==null&&g.companionCooldown<=Time.unscaledDeltaTime,
                "Fresh "+menuKeys[index]+" cancels before the exact-ready Tick creates a command or spends the "+(pact?12:18)+"s cooldown");
            g.Close();NoLaterCommand(app,"same-readiness "+menuKeys[index]+" menu");
        }
        foreach(string boundary in new[]{"pause","inventory","companions","application","focus","load","travel","switch","death","practice-start","range-reset","review-replace","review-end"})
        {
            Prepare(app);var g=app.Game;if(boundary=="practice-start"||boundary=="range-reset")Mentor(app);
            Queue(app);Check(Queued(g)&&g.companionAction==null,"Boundary fixture owns an unstarted request: "+boundary);
            if(boundary=="pause"||boundary=="inventory"||boundary=="companions")
            { Key(app,boundary=="pause"?KeyCode.Escape:boundary=="inventory"?KeyCode.I:KeyCode.C);Frames(app,10);g.Close(); }
            else if(boundary=="application"){Call(app,"OnApplicationPause",true);Frames(app,10);Call(app,"OnApplicationPause",false);}
            else if(boundary=="focus"){Call(app,"OnApplicationFocus",false);Frames(app,10);g.Close();}
            else if(boundary=="load")
            {
                g.PrepareSave();string json=JsonUtility.ToJson(g.save);
                Check(!json.Contains("CommandIntent")&&!json.Contains("bufferedCompanion")&&!json.Contains("companionCooldown")&&!json.Contains("CompanionCommandQueued"),"SaveData has no transient companion request or cooldown");
                Check(SaveStore.Write(g.save,2),"Actual checksummed save writer succeeds");string warning;SaveData saved=SaveStore.Read(2,out warning);
                Check(saved!=null&&warning==null,"Actual checksum reader restores the campaign");g.Load(saved);g.Close();g.enemies.Clear();
            }
            else if(boundary=="travel"){g.Enter(1);g.enemies.Clear();}
            else if(boundary=="switch"){g.SelectCompanion(1);Check(g.save.companion==1,"Actual safe-travel companion switch succeeds");}
            else if(boundary=="death"){g.invulnerable=0;Call(g,"Hurt",99999f,"environment",(Point?)null);Check(g.Dying,"Actual damage enters death recovery");Frames(app,48);g.enemies.Clear();}
            else if(boundary=="practice-start"){g.StartPractice();Check(g.practicing,"Actual practice starts");g.EndPractice();}
            else if(boundary=="range-reset"){Check(g.StartRange(0)&&g.ResetRange(),"Actual range start and reset succeed");g.EndPractice();}
            else if(boundary=="review-replace"){app.BeginVisualReview(0,0);app.VisualReviewPaused=false;app.Game.enemies.Clear();}
            else{app.EndVisualReview();app.BeginVisualReview(0,0);app.VisualReviewPaused=false;app.Game.enemies.Clear();}
            NoLaterCommand(app,boundary);
        }
        foreach(bool review in new[]{false,true})
        {
            Prepare(app);Queue(app);var g=app.Game;float cooldown=g.companionCooldown,world=g.save.totalTime;
            if(review)app.VisualReviewPaused=true;else g.hitStop=.10f;Frames(app,review?20:4);
            Check(Queued(g)&&Near(g.companionCooldown,cooldown)&&Near(g.save.totalTime,world)&&g.companionAction==null,"Review Freeze/hit stop freezes both pending intent and command cooldown");
            app.VisualReviewPaused=false;ActorAction action=AwaitCommand(app);Check(action.elapsed==0,"The same frozen intent resumes on simulation time");
        }
        // Exercise the live automatic Save path, which review sessions skip.
        Prepare(app);SaveData campaign=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(app.Game.save));app.EndVisualReview();
        var live=app.Game;live.Load(campaign);live.Close();live.enemies.Clear();Set(live,"companionAttack",100f);
        Set(app,"camera",live.Position);Set(app,"controllerUsed",false);Set(app,"pointerSeen",false);Call(app,"ResetControllerAim",false);Mouse(app,live.Position+new Point(4,0));
        Queue(app);Check(Queued(live)&&!app.VisualReviewActive,"Live campaign owns an automatic paused-save request");float remaining=live.companionCooldown;
        Call(app,"OnApplicationPause",true);Frames(app,10);Check(Near(live.companionCooldown,remaining)&&!Queued(live),"Live application pause freezes cooldown and discards pending command");
        string issue;SaveData pausedSave=SaveStore.Read(out issue);Check(pausedSave!=null&&issue==null&&pausedSave.room==live.save.room,"Actual pause callback writes the live campaign through SaveStore");
        Call(app,"OnApplicationPause",false);NoLaterCommand(app,"live paused-save resume");live.Load(pausedSave);live.Close();live.enemies.Clear();NoLaterCommand(app,"automatic paused-save reload");
        Console.WriteLine("PASS real J/Q/dodge independence; 24 exact-ready keyboard/START/View menu edges cancel before dispatch or 18/12s spending; menus, application/focus, checksummed load, travel, companion switch, death, practice/range reset and review replacement discard stale intent; Freeze/hit stop preserve simulation clocks; live automatic paused save/reload stays compatible; NATIVE_UNVERIFIED");
    }

    public static int Main()
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-companion-command-buffer-"+Guid.NewGuid());
        Application.persistentDataPath=directory;var app=new FrontierRuntime();int failures=0;
        try
        {
            Call(app,"Awake");Check(app.Game!=null,"Actual Awake loads the shipped campaign");
            foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{FreshEdgesAcrossFrameRatesAndHeldDoesNotRetry,BothHudLayoutsAcceptWithoutPretendingReady,ExistingContactsReceiptsAndCapturedStartValues,IndependentPlayerInputsAndSessionBoundaries})
                try{test(app);}catch(Exception error){failures++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}
        }
        finally{app.EndVisualReview();Call(app,"OnDestroy");Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime companion command buffer: "+(4-failures)+"/4 groups passed; NATIVE_UNVERIFIED");return failures==0?0:1;
    }
}
