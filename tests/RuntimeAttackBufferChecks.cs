// Runs the shipped Update/OnGUI, save and training paths with controllable
// Unity declarations. This is offline integration evidence; NATIVE_UNVERIFIED.
using System;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeAttackBufferChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static bool Near(float a, float b) { return Math.Abs(a-b)<.003f; }
    private static object Call(FrontierRuntime app, string method, params object[] args)
    { return typeof(FrontierRuntime).GetMethod(method,Private).Invoke(app,args); }
    private static void Set(FrontierRuntime app, string field, object value)
    { typeof(FrontierRuntime).GetField(field,Private).SetValue(app,value); }
    private static T Get<T>(FrontierRuntime app, string field)
    { return (T)typeof(FrontierRuntime).GetField(field,Private).GetValue(app); }
    private static void Update(FrontierRuntime app)
    { Call(app,"Update"); Input.keys.Clear(); Input.buttonDown.Clear(); }
    private static void Frames(FrontierRuntime app, int count) { for(int i=0;i<count;i++)Update(app); }
    private static void Pointer(FrontierRuntime app, Point pixel)
    { float s=Get<float>(app,"scale"); Input.mousePosition=new Vector3(Get<float>(app,"offsetX")+pixel.x*s,Screen.height-Get<float>(app,"offsetY")-pixel.y*s,0); }
    private static void Mouse(FrontierRuntime app, Point world) { Pointer(app,(Point)Call(app,"ScreenPoint",world)); }
    private static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key);Update(app); }
    private static void Press(FrontierRuntime app, string device)
    {
        if(device=="mouse") { Input.buttons.Add(0);Input.buttonDown.Add(0); }
        else { KeyCode key=device=="pad"?KeyCode.JoystickButton2:KeyCode.J;Input.keys.Add(key);Input.held.Add(key); }
        Update(app);Input.held.Clear();Input.buttons.Clear();
    }
    private static void Queue(FrontierRuntime app, string device="keyboard", float remaining=.10f)
    { app.Game.attackCooldown=remaining+Time.unscaledDeltaTime;Press(app,device); }
    private static void Prepare(FrontierRuntime app, int profession=1, int room=1)
    {
        Input.Clear();GUI.queuedClick=null;GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();
        GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;
        Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;
        app.BeginVisualReview(profession,room);app.VisualReviewPaused=false;var g=app.Game;
        g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();g.effects.Clear();g.fallen.Clear();
        g.save.companion=-1;g.save.reducedMotion=true;g.save.effectsQuality=0;g.save.showGuidance=false;
        g.save.energy=100;g.save.health=g.save.maxHealth;g.save.totalTime=100;g.save.technique=0;
        g.save.foodUntil=g.save.teaUntil=g.save.mealUntil=0;g.save.relics=new[]{-1,-1};g.save.talents=new int[3];
        g.save.controllerAimProfile=0;g.save.controllerAimInvertY=false;g.invulnerable=0;g.toastTime=0;
        Check(app.SetVisualReviewPosition(new Point(12,14)),"Fixture uses shipped walkable ground");
        Set(app,"camera",g.Position);Set(app,"applicationPaused",false);Set(app,"controllerUsed",false);
        Set(app,"pointerSeen",false);Set(app,"pointerAimActive",false);Set(app,"combatHudTime",0f);
        Set(app,"arrivalTime",0f);Set(app,"resumeTime",0f);Call(app,"Resize");Call(app,"ResetControllerAim",false);
        Mouse(app,g.Position+new Point(4,0));
    }
    private static ActorAction AwaitAttack(FrontierRuntime app, int limit=20)
    {
        for(int i=0;i<limit;i++)
        { if(app.Game.playerAction!=null && app.Game.playerAction.kind=="attack")return app.Game.playerAction;Update(app); }
        throw new Exception("Accepted fresh press never starts after cooldown; remaining="+app.Game.attackCooldown);
    }
    private static void NoLaterAttack(FrontierRuntime app, string reason, int count=45)
    {
        Input.Clear();for(int i=0;i<count;i++)
        { Update(app);Check(app.Game.playerAction==null || app.Game.playerAction.kind!="attack","No latent basic attack crosses "+reason); }
    }

    private static void FreshKeyboardPadMouseAndMatureHeldRepeat(FrontierRuntime app)
    {
        for(int profession=0;profession<3;profession++)
        foreach(string device in new[]{"keyboard","pad","mouse"})
        {
            Prepare(app,profession);var g=app.Game;g.save.weaponStyles[g.save.weapon]=1;
            if(device=="pad")Input.axes["Aim Horizontal"]=1;
            Queue(app,device,.12f-.0000001f);Point? target=g.aimTarget;
            Check(g.playerAction==null && g.attackCooldown>0 && g.attackCooldown<=.12f,
                "Real "+device+" release captures the final .12s for class "+profession);
            Check(Near(g.save.energy,100) && g.projectiles.Count==0 && g.effects.Count==0,
                "Accepted press creates no early action, energy cost, projectile or effect");
            if(device=="pad")Input.axes["Aim Horizontal"]=-1;else Mouse(app,g.Position+new Point(-4,0));
            ActorAction action=AwaitAttack(app);
            Check(action.direction.x>.999f && Near(action.direction.y,0),"Queued attack keeps its pressed heading after live aim changes");
            Check(action.target.HasValue==target.HasValue && (!target.HasValue || (action.target.Value-target.Value).Length<.003f),
                "Queued attack keeps the original pointer target; controller owns no pointer target");
            Check(Near(action.elapsed,0) && !action.released && Near(g.attackCooldown,g.Profile(g.save.weapon,g.Style).interval) && Near(g.save.energy,96),
                "Ready boundary starts the existing fresh action, interval and four-energy transaction once");
            while(action.elapsed+Time.unscaledDeltaTime<action.contact-.00001f)
            { Update(app);Check(!action.released && g.projectiles.Count==0 && g.effects.Count==0,"Actual contact cannot release early"); }
            for(int i=0;i<60 && !action.released;i++)Update(app);
            Check(action.released && (g.save.weapon==0?g.effects.Exists(e=>e.kind=="slash"):g.projectiles.Exists(p=>p.friendly)),
                "Original basic contact alone creates the slash or projectile");
            Frames(app,70);Check(g.playerAction==null,"Released fresh input cannot repeat after its first attack");
        }
        foreach(string device in new[]{"keyboard","pad","mouse"})
        {
            Prepare(app);var g=app.Game;Queue(app,device,.50f);
            // Held state enters the short window without generating a second edge.
            g.attackCooldown=.11f;
            if(device=="mouse")Input.buttons.Add(0);else Input.held.Add(device=="pad"?KeyCode.JoystickButton2:KeyCode.J);
            Frames(app,2);Input.held.Clear();Input.buttons.Clear();NoLaterAttack(app,"held-only near-ready "+device);
            Prepare(app);g=app.Game;
            if(device=="mouse")Input.buttons.Add(0);else Input.held.Add(device=="pad"?KeyCode.JoystickButton2:KeyCode.J);
            int starts=0;ActorAction prior=null;
            for(int i=0;i<130;i++){Update(app);if(g.playerAction!=null && g.playerAction!=prior)starts++;prior=g.playerAction;}
            Check(starts>=4,"Mature held "+device+" continues repeating basic attacks without fresh edges");
        }
        Console.WriteLine("PASS real J/X/left-click press-release across three classes: .12s cooldown capture, original aim/target, one cost/action/contact, no repeat; held-only never queues but mature held attacks still repeat; NATIVE_UNVERIFIED");
    }

    private static Rect SkillSlot(bool compact) { return compact?new Rect(430,732,78,50):new Rect(354,699,109,75); }
    private static void BothHudLayoutsAndInterfaceMouseGating(FrontierRuntime app)
    {
        foreach(bool compact in new[]{false,true})
        foreach(bool click in new[]{false,true})
        {
            Prepare(app);var g=app.Game;Set(app,"combatHudTime",compact?3f:0f);g.skillCooldown=.8f;
            Rect slot=SkillSlot(compact);Pointer(app,new Point(slot.x+15,slot.y+12));g.attackCooldown=.10f+Time.unscaledDeltaTime;
            // Exercise the actual hover/control draw, including a disabled Q click.
            Input.buttons.Add(0);if(click)Input.buttonDown.Add(0);Update(app);
            GUI.queuedClick=click?(Rect?)slot:null;Call(app,"OnGUI");GUI.queuedClick=null;Input.buttons.Clear();
            Check(g.playerAction==null,"Actual "+(compact?"combat":"exploration")+" HUD blocks held and fresh mouse world input");
            Check(GUI.buttonCalls.Exists(b=>Near(b.rect.x,slot.x) && Near(b.rect.y,slot.y) && !b.enabled),"Real cooldown control remains disabled");
            Mouse(app,g.Position+new Point(4,0));NoLaterAttack(app,"HUD hover or interface click");
            Prepare(app);g=app.Game;Set(app,"combatHudTime",compact?3f:0f);Pointer(app,new Point(slot.x+15,slot.y+12));
            g.attackCooldown=.10f+Time.unscaledDeltaTime;Press(app,"keyboard");AwaitAttack(app);
            Check(g.playerAction!=null,"J retains its existing keyboard ownership while pointer is over the HUD");
        }
        Prepare(app);Pointer(app,new Point(-5,400));Queue(app,"mouse");Mouse(app,app.Game.Position+new Point(4,0));
        NoLaterAttack(app,"outside the virtual viewport");
        Console.WriteLine("PASS both real HUD layouts: hover/held/fresh disabled-control clicks never queue world attacks, viewport bounds block mouse, keyboard J remains valid over UI; NATIVE_UNVERIFIED");
    }

    private static void QAndDodgeWinBeforeTickAndFreshBasic(FrontierRuntime app)
    {
        foreach(bool dodge in new[]{false,true})
        foreach(bool pad in new[]{false,true})
        foreach(bool unavailable in new[]{false,true})
        {
            Prepare(app);var g=app.Game;g.save.weaponStyles[g.save.weapon]=1;Queue(app);
            g.attackCooldown=Time.unscaledDeltaTime; // The next real Tick reaches readiness.
            if(dodge){g.dashCooldown=unavailable?.8f:0;Input.axes["Horizontal"]=1;}
            else g.skillCooldown=unavailable?.8f:0;
            Key(app,dodge?(pad?KeyCode.JoystickButton0:KeyCode.Space):(pad?KeyCode.JoystickButton3:KeyCode.Q));Input.axes["Horizontal"]=0;
            Check(Near(g.save.energy,unavailable?100:dodge?80:60),"Fresh deliberate command reaches the readiness frame without paying for a queued basic first");
            Check(g.playerAction==null || g.playerAction.kind=="skill","Fresh Q/dodge wins before a ready queued attack can start");
            NoLaterAttack(app,"fresh "+(unavailable?"unavailable ":"")+(dodge?"dodge":"Q"));
        }
        foreach(bool dodge in new[]{false,true})
        {
            Prepare(app);var g=app.Game;
            if(dodge){g.dashCooldown=.12f;Key(app,KeyCode.Space);}else{g.skillCooldown=.12f;Key(app,KeyCode.Q);}
            Input.held.Add(KeyCode.J);Input.keys.Add(KeyCode.J);Update(app);Input.held.Clear();
            Check(g.playerAction==null,"A fresh rejected J never falls through to mature held Attack ahead of a pending Q/dodge");
            for(int i=0;i<10 && (dodge?g.dashTime<=0:g.playerAction==null);i++)Update(app);
            Check(dodge?g.dashTime>0:g.playerAction!=null && g.playerAction.kind=="skill","The higher priority command still starts");
            NoLaterAttack(app,"basic press behind pending "+(dodge?"dodge":"Q"));
            Prepare(app);g=app.Game;Queue(app);Input.keys.Add(KeyCode.J);Input.held.Add(KeyCode.J);
            if(dodge){g.dashCooldown=.12f;Input.keys.Add(KeyCode.Space);}else{g.skillCooldown=.12f;Input.keys.Add(KeyCode.Q);}
            Update(app);Input.held.Clear();NoLaterAttack(app,"same-frame deliberate plus basic input");
        }
        Console.WriteLine("PASS Q/Y and Space/A supersede queued basics before exact-ready Tick, including unavailable fresh commands; pending Q/dodge rejects a fresh basic without held fallback; same-frame deliberate action priority; NATIVE_UNVERIFIED");
    }

    private static void Mentor(FrontierRuntime app)
    {
        var g=app.Game;WorldObject mentor=Array.Find(g.Map.objects,o=>o.kind=="mentor");Check(mentor!=null,"Town retains Ada");
        g.save.x=mentor.x;g.save.y=mentor.y-.7f;g.save.practiceBits=7;
    }
    private static void PauseSaveAndTrainingBoundaries(FrontierRuntime app)
    {
        foreach(string boundary in new[]{"pause","inventory","application","focus","load","travel","practice-start","range-reset","range-end","range-clear","sparring-clear","review-replace","review-end"})
        {
            bool training=boundary.StartsWith("range") || boundary=="sparring-clear" || boundary=="practice-start";
            Prepare(app,1,training?0:1);var g=app.Game;
            if(training){Mentor(app);if(boundary.StartsWith("range"))Check(g.StartRange(0),"Actual range starts");else if(boundary=="sparring-clear")Check(g.StartSparring(),"Actual sparring starts");}
            Queue(app);Check(g.playerAction==null,"Boundary fixture has one unstarted basic press");
            if(boundary=="pause" || boundary=="inventory"){Key(app,boundary=="pause"?KeyCode.Escape:KeyCode.I);Frames(app,15);g.Close();}
            else if(boundary=="application"){Call(app,"OnApplicationPause",true);Frames(app,15);Call(app,"OnApplicationPause",false);}
            else if(boundary=="focus"){Call(app,"OnApplicationFocus",false);Frames(app,15);g.Close();}
            else if(boundary=="load")
            {
                g.PrepareSave();string json=JsonUtility.ToJson(g.save);
                Check(!json.Contains("AttackIntent") && !json.Contains("bufferedAttack") && !json.Contains("attackCooldown"),"Save data contains no transient attack intent or cooldown");
                Check(SaveStore.Write(g.save,2),"Actual checksummed save writer succeeds");string warning;SaveData saved=SaveStore.Read(2,out warning);
                Check(saved!=null && warning==null,"Actual checksum reader restores campaign");g.Load(saved);g.Close();g.enemies.Clear();
            }
            else if(boundary=="travel"){g.Enter(0);g.enemies.Clear();}
            else if(boundary=="practice-start")g.StartPractice();
            else if(boundary=="range-reset")Check(g.ResetRange(),"Actual range resets");
            else if(boundary=="range-end")g.EndPractice();
            else if(boundary=="range-clear"){g.enemies.Clear();Update(app);Check(g.RangeFinished && !g.RangeStarted,"Pending attack cannot start a cleared range timer");}
            else if(boundary=="sparring-clear"){g.enemies.Clear();Update(app);Check(g.SparringAwaitingNext,"Actual cleared drill waits for manual review");Check(g.ContinueSparring(),"Manual Continue starts next drill");}
            else if(boundary=="review-replace"){app.BeginVisualReview(1,1);app.VisualReviewPaused=false;app.Game.enemies.Clear();}
            else{app.EndVisualReview();app.BeginVisualReview(1,1);app.VisualReviewPaused=false;app.Game.enemies.Clear();}
            NoLaterAttack(app,boundary);
        }
        Prepare(app);Queue(app);var game=app.Game;float cooldown=game.attackCooldown;
        app.VisualReviewPaused=true;Frames(app,25);Check(Near(game.attackCooldown,cooldown) && game.playerAction==null,"Review Freeze stops both attack clocks");
        app.VisualReviewPaused=false;AwaitAttack(app);
        Prepare(app);Queue(app);game=app.Game;cooldown=game.attackCooldown;game.hitStop=.1f;Frames(app,4);
        Check(Near(game.attackCooldown,cooldown) && game.playerAction==null,"Hit stop freezes cooldown and intent on the same simulation clock");AwaitAttack(app);
        // Leave the review lab so the real application-pause callback must
        // execute Save(), rather than taking its review-only persistence skip.
        SaveData campaign=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(game.save));app.EndVisualReview();
        game=app.Game;game.Load(campaign);game.Close();game.enemies.Clear();Input.Clear();
        Set(app,"camera",game.Position);Set(app,"controllerUsed",false);Set(app,"pointerSeen",false);Call(app,"ResetControllerAim",false);
        Mouse(app,game.Position+new Point(4,0));Queue(app);cooldown=game.attackCooldown;
        Check(!app.VisualReviewActive && game.playerAction==null,"Live campaign owns the actual paused save fixture");
        Call(app,"OnApplicationPause",true);Frames(app,15);
        Check(Near(game.attackCooldown,cooldown),"Application pause does not tick the live campaign");
        string issue;SaveData pausedSave=SaveStore.Read(out issue);
        Check(pausedSave!=null && issue==null && pausedSave.room==game.save.room,"Real pause callback writes the live owner through automatic SaveStore");
        Call(app,"OnApplicationPause",false);NoLaterAttack(app,"live paused campaign save");
        game.Load(pausedSave);game.Close();game.enemies.Clear();NoLaterAttack(app,"automatic paused-save reload");
        Console.WriteLine("PASS real menu/application/focus pauses, live automatic paused save owner/reload, checksummed save/load, travel, practice/range/drill and replacement-review boundaries discard basic presses; same-review Freeze and hit stop preserve one paused intent; NATIVE_UNVERIFIED");
    }

    public static int Main()
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-attack-buffer-"+Guid.NewGuid());
        Application.persistentDataPath=directory;var app=new FrontierRuntime();int failures=0;
        try
        {
            Call(app,"Awake");Check(app.Game!=null,"Actual Awake loads the shipped campaign");
            foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{FreshKeyboardPadMouseAndMatureHeldRepeat,BothHudLayoutsAndInterfaceMouseGating,QAndDodgeWinBeforeTickAndFreshBasic,PauseSaveAndTrainingBoundaries})
                try{test(app);}catch(Exception error){failures++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}
        }
        finally{app.EndVisualReview();Call(app,"OnDestroy");Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime attack buffer: "+(4-failures)+"/4 groups passed; NATIVE_UNVERIFIED");return failures==0?0:1;
    }
}
