// Runs shipped Update/OnGUI, action contact, training and SaveStore paths.
// Controllable Unity declarations provide offline evidence; NATIVE_UNVERIFIED.
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeSkillCooldownBufferChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string captureFolder;
    private static int captures;
    private sealed class Label { public string text = null; public float[] r = null, color = null; }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static bool Near(float a, float b, float tolerance = .003f) { return Math.Abs(a-b) <= tolerance; }
    private static bool Same(Rect a, Rect b) { return Near(a.x,b.x) && Near(a.y,b.y) && Near(a.width,b.width) && Near(a.height,b.height); }
    private static object Call(object target, string method, params object[] args)
    { var m=target.GetType().GetMethod(method,Private|BindingFlags.Public); Check(m!=null,"Production entry point exists: "+method); return m.Invoke(target,args); }
    private static void Set(FrontierRuntime app, string name, object value) { typeof(FrontierRuntime).GetField(name,Private).SetValue(app,value); }
    private static T Get<T>(FrontierRuntime app, string name) { return (T)typeof(FrontierRuntime).GetField(name,Private).GetValue(app); }
    private static void Update(FrontierRuntime app) { Call(app,"Update"); Input.keys.Clear(); }
    private static void Frames(FrontierRuntime app, int count) { for(int i=0;i<count;i++)Update(app); }
    private static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key); Update(app); }
    private static void Pointer(FrontierRuntime app, Point pixel)
    { float s=Get<float>(app,"scale"); Input.mousePosition=new Vector3(Get<float>(app,"offsetX")+pixel.x*s,Screen.height-Get<float>(app,"offsetY")-pixel.y*s,0); }
    private static void Mouse(FrontierRuntime app, Point world) { Pointer(app,(Point)Call(app,"ScreenPoint",world)); }
    private static void Draw(FrontierRuntime app, string capture=null)
    {
        GUI.commands.Clear(); GUI.textureCalls.Clear(); GUI.buttonCalls.Clear(); Call(app,"OnGUI");
        if(captureFolder!=null && capture!=null)
        {
            var commands=GUI.commands.ToArray(); var textures=GUI.textureCalls.ToArray(); var buttons=GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder,"qa-skill-cooldown-"+capture); captures++;
            GUI.commands.AddRange(commands); GUI.textureCalls.AddRange(textures); GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static void Prepare(FrontierRuntime app, int profession=1, int room=1)
    {
        Input.Clear(); GUI.queuedClick=null; GUI.matrix=Matrix4x4.identity; GUI.color=Color.white; GUI.enabled=true;
        Screen.width=1280; Screen.height=800; Time.unscaledDeltaTime=.02f; Event.current.type=EventType.Repaint;
        app.BeginVisualReview(profession,room); app.VisualReviewPaused=false; var g=app.Game;
        g.enemies.Clear(); g.projectiles.Clear(); g.hazards.Clear(); g.effects.Clear(); g.fallen.Clear();
        g.save.companion=-1; g.save.reducedMotion=true; g.save.effectsQuality=0; g.save.showGuidance=false;
        g.save.english=true; g.save.largeText=false; g.save.relics=new[]{-1,-1}; g.save.talents=new int[3];
        g.save.energy=100; g.save.health=g.save.maxHealth; g.save.totalTime=100;
        g.save.foodUntil=g.save.teaUntil=g.save.mealUntil=0; g.save.technique=0;
        g.invulnerable=0; g.toastTime=0; g.save.controllerAimProfile=0; g.save.controllerAimInvertY=false;
        Check(app.SetVisualReviewPosition(new Point(12,14)),"Fixture uses actual shipped walkable ground");
        Set(app,"camera",g.Position); Set(app,"applicationPaused",false); Set(app,"controllerUsed",false);
        Set(app,"pointerSeen",false); Set(app,"pointerAimActive",false); Set(app,"combatHudTime",0f);
        Set(app,"arrivalTime",0f); Set(app,"resumeTime",0f); Call(app,"Resize"); Call(app,"ResetControllerAim",false);
        Mouse(app,g.Position+new Point(4,0));
    }
    // The extra dt compensates for Update ticking before sampling the fresh key.
    private static void Queue(FrontierRuntime app, bool pad=false, float remaining=.12f)
    { app.Game.skillCooldown=remaining+Time.unscaledDeltaTime; Key(app,pad?KeyCode.JoystickButton3:KeyCode.Q); }
    private static ActorAction AwaitCast(FrontierRuntime app, int limit=30)
    {
        for(int i=0;i<limit;i++)
        { if(app.Game.playerAction!=null && app.Game.playerAction.kind=="skill")return app.Game.playerAction; Update(app); }
        throw new Exception("Accepted real Q/Y never reached its cast boundary; dt="+Time.unscaledDeltaTime+", cooldown="+app.Game.skillCooldown+", action="+(app.Game.playerAction==null?"null":app.Game.playerAction.kind));
    }
    private static void Release(FrontierRuntime app, ActorAction action)
    { for(int i=0;i<80 && !action.released;i++)Update(app); Check(action.released,"Actual simulation reaches the original skill contact"); }
    private static void NoLaterCast(FrontierRuntime app, string reason, int frames=35)
    {
        Input.Clear();
        for(int i=0;i<frames;i++)
        { Update(app); Check(app.Game.playerAction==null || app.Game.playerAction.kind!="skill", "No latent Q crosses "+reason); Check(app.Game.skillCooldown<1,"No hidden technique cost/cooldown crosses "+reason); }
    }
    private static void ActualKeyboardPadHeldAttackAndCapturedAim(FrontierRuntime app)
    {
        for(int profession=0;profession<3;profession++)
        foreach(bool pad in new[]{false,true})
        {
            Prepare(app,profession); var g=app.Game; Input.held.Add(KeyCode.J);
            if(pad)Input.axes["Aim Horizontal"]=1;
            Queue(app,pad); Point? target=g.aimTarget;
            Check(g.skillCooldown>0 && g.skillCooldown<=GameCore.SkillBufferWindow && g.playerAction==null,
                "Real Q/Y accepts the last cooldown frames before held J for class "+profession);
            Check(Near(g.save.energy,100) && g.guardTime<=0 && g.projectiles.Count==0 && g.effects.Count==0,
                "The accepted cooldown request creates no early cost, action, protection, projectile or effect");
            Input.held.Add(pad?KeyCode.JoystickButton3:KeyCode.Q);
            if(pad)Input.axes["Aim Horizontal"]=-1; else Mouse(app,g.Position+new Point(-4,0));
            ActorAction cast=AwaitCast(app);
            Check(cast.direction.x>.999f && Near(cast.direction.y,0),"Queued Q/Y retains its original heading after aim changes");
            Check(cast.target.HasValue==!pad && (!target.HasValue || (cast.target.Value-target.Value).Length<.003f),
                "Keyboard Q retains its captured world target while controller Y owns no stale pointer coordinate");
            Check(Near(g.skillCooldown,3.5f) && Near(g.save.energy,60) && Near(cast.elapsed,0) && !cast.released,
                "The actual start pays 40 once, starts the original 3.5s cooldown and begins unreleased");
            while(cast.elapsed+Time.unscaledDeltaTime<cast.contact-.00001f)
            { Update(app); Check(!cast.released && g.projectiles.Count==0 && g.guardTime<=0 && !g.effects.Exists(e=>e.kind=="burst"),"Preparation cannot release the technique early"); }
            Release(app,cast);
            Check(profession==0?g.guardTime>2.99f:profession==1?g.projectiles.FindAll(p=>p.friendly && p.technique).Count==5:g.effects.Exists(e=>e.kind=="burst"),
                "The mature class technique appears only at its real contact");
            int starts=1;
            for(int i=0;i<220;i++){float before=g.skillCooldown;Update(app);if(g.skillCooldown>before+1)starts++;}
            Check(starts==1,"Holding Q/Y and J cannot renew the one-shot intent after its cooldown");
        }
        Prepare(app); var game=app.Game; game.dashCooldown=.12f; Key(app,KeyCode.Space); Queue(app);
        AwaitCast(app); Check(game.dashTime<=0 && !game.effects.Exists(e=>e.kind=="dash"),"A later accepted cooldown Q supersedes the pending Dodge");
        Prepare(app); game=app.Game; game.dashCooldown=.12f; Key(app,KeyCode.Space); game.skillCooldown=.6f; Key(app,KeyCode.Q);
        for(int i=0;i<10 && game.dashTime<=0;i++)Update(app);
        Check(game.dashTime>0,"A rejected outside-window Q preserves the accepted Dodge");
        Prepare(app); game=app.Game; Queue(app); game.dashCooldown=.12f; Key(app,KeyCode.Space);
        for(int i=0;i<10 && game.dashTime<=0;i++)Update(app);
        Check(game.dashTime>0,"A later accepted Dodge starts"); NoLaterCast(app,"later Dodge");
        Console.WriteLine("PASS actual Q/Y across three classes: held-J priority, captured pointer target/direction, controller target clearing, no early effects, original release, one-shot held input and last-valid Q/Dodge priority; NATIVE_UNVERIFIED");
    }
    private static Rect SkillSlot(bool compact) { return compact?new Rect(430,732,78,50):new Rect(354,699,109,75); }
    private static Label CooldownLabel(Rect slot)
    {
        string found=GUI.commands.Find(c=>c.Contains("\"text\":\"1s\"") && c.Contains("\"r\":["+(slot.x+3).ToString(CultureInfo.InvariantCulture)+","));
        Check(found!=null,"Actual Q slot continues to display honest 1s cooldown text"); return JsonUtility.FromJson<Label>(found);
    }
    private static void BothMouseHudLayoutsAndEightBilingualCaptures(FrontierRuntime app)
    {
        foreach(bool compact in new[]{false,true})
        foreach(string state in new[]{"window","outside","energy"})
        {
            Prepare(app); var g=app.Game; Set(app,"combatHudTime",compact?3f:0f);
            g.skillCooldown=state=="outside"?.6f:.12f; g.save.energy=state=="energy"?10:100; g.SetAim(new Point(1,0));
            Rect slot=SkillSlot(compact); Pointer(app,new Point(slot.x+15,slot.y+12)); Input.buttons.Add(0); Update(app);
            Check(g.playerAction==null,"A real mouse hold on the Q HUD cannot leak a basic world attack");
            float energy=g.save.energy; GUI.queuedClick=slot; Draw(app);
            var button=GUI.buttonCalls.Find(b=>Same(b.rect,slot));
            Check(button!=null && button.enabled==(state=="window"),"Actual "+(compact?"compact":"exploration")+" Q HUD accepts only the affordable short cooldown window: "+state);
            Label label=CooldownLabel(slot); Check(Near(label.color[0],164f/255) && Near(label.color[1],180f/255),"Clickable early input retains muted cooldown text");
            Check(!GUI.textureCalls.Exists(t=>t.texture==Texture2D.whiteTexture && Same(t.rect,new Rect(slot.x+4,slot.y+slot.height-3,slot.width-8,2)) && Near(t.color.r,122f/255) && Near(t.color.g,202f/255)),"Early Q never draws the mint ready underline");
            Check(g.playerAction==null && Near(g.save.energy,energy) && g.skillCooldown<1,"OnGUI queues without early action, cost or a renewed cooldown");
            GUI.queuedClick=null;
            if(state=="window")
            { Mouse(app,g.Position+new Point(-4,0)); ActorAction cast=AwaitCast(app); Check(cast.direction.x>.999f && !cast.target.HasValue,"HUD Q retains heading and clears its live pointer target"); }
            else NoLaterCast(app,"disabled "+state+" HUD",80);
            Input.buttons.Clear();
        }
        // Eight deliberately limited display fixtures: two HUDs, zh/en, normal/large.
        foreach(bool compact in new[]{false,true})
        foreach(bool english in new[]{false,true})
        foreach(bool large in new[]{false,true})
        {
            Prepare(app); var g=app.Game; g.save.english=english; g.save.largeText=large;
            if(large){Screen.width=960;Screen.height=600;Call(app,"Resize");}
            Set(app,"combatHudTime",compact?3f:0f); Set(app,"controllerUsed",english); g.skillCooldown=.12f;
            Draw(app,(compact?"combat":"exploration")+"-"+(english?"en":"zh")+"-"+(large?"large960":"normal1280"));
            Rect slot=SkillSlot(compact); Check(GUI.buttonCalls.Exists(b=>Same(b.rect,slot)&&b.enabled),"Bilingual normal/large display retains the actual clickable early-Q slot"); CooldownLabel(slot);
        }
        Console.WriteLine("PASS both real Q HUD layouts: affordable window clicks, disabled outside/low-energy controls, honest muted 1s/no ready underline, captured direction, no mouse attack leakage; eight zh/en normal1280/large960 offline captures; NATIVE_UNVERIFIED");
    }
    private static void Mentor(FrontierRuntime app)
    {
        var g=app.Game; WorldObject mentor=Array.Find(g.Map.objects,o=>o.kind=="mentor"); Check(mentor!=null,"Town retains Ada");
        g.save.x=mentor.x;g.save.y=mentor.y-.7f;g.save.practiceBits=7;
    }
    private static void PausesSaveLifecycleProfilesAndTraining(FrontierRuntime app)
    {
        foreach(string boundary in new[]{"pause","inventory","application","focus","load","travel","weapon","profession","style","technique","practice-start","range-reset","range-end","range-clear","sparring-clear","review-replace","review-end"})
        {
            bool training=boundary.StartsWith("range") || boundary=="sparring-clear" || boundary=="practice-start";
            Prepare(app,1,training?0:1); var g=app.Game;
            if(training){Mentor(app);if(boundary.StartsWith("range"))Check(g.StartRange(0),"Actual range starts");else if(boundary=="sparring-clear")Check(g.StartSparring(),"Actual sparring starts");}
            Queue(app); Check(g.playerAction==null && g.skillCooldown>0,"Boundary fixture owns an unstarted real Q");
            if(boundary=="pause" || boundary=="inventory") { Key(app,boundary=="pause"?KeyCode.Escape:KeyCode.I);Frames(app,15);g.Close(); }
            else if(boundary=="application"){Call(app,"OnApplicationPause",true);Frames(app,15);Call(app,"OnApplicationPause",false);}
            else if(boundary=="focus"){Call(app,"OnApplicationFocus",false);Frames(app,15);g.Close();}
            else if(boundary=="load")
            {
                g.PrepareSave();string json=JsonUtility.ToJson(g.save);
                Check(!json.Contains("bufferedSkill") && !json.Contains("skillCooldown") && !json.Contains("SkillIntent"),"SaveData contains no transient cooldown intent");
                Check(SaveStore.Write(g.save,2),"Actual checksummed save writer succeeds");string warning;SaveData saved=SaveStore.Read(2,out warning);
                Check(saved!=null && warning==null,"Actual checksum reader restores the campaign");g.Load(saved);g.Close();g.enemies.Clear();
            }
            else if(boundary=="travel"){g.Enter(0);g.enemies.Clear();}
            else if(boundary=="weapon")g.save.weapon=2;
            else if(boundary=="profession")g.save.profession=2;
            else if(boundary=="style")g.save.weaponStyles[g.save.weapon]=1;
            else if(boundary=="technique")g.save.technique=1;
            else if(boundary=="practice-start")g.StartPractice();
            else if(boundary=="range-reset")Check(g.ResetRange(),"Actual range resets");
            else if(boundary=="range-end")g.EndPractice();
            else if(boundary=="range-clear"){g.enemies.Clear();Update(app);Check(g.RangeFinished && !g.RangeStarted,"Cleared-range review never starts from the pending Q");}
            else if(boundary=="sparring-clear"){g.enemies.Clear();Update(app);Check(g.SparringAwaitingNext,"Cleared drill enters its actual manual review");Check(g.ContinueSparring(),"Actual manual Continue starts the next drill");}
            else if(boundary=="review-replace"){app.BeginVisualReview(1,1);app.VisualReviewPaused=false;app.Game.enemies.Clear();}
            else {app.EndVisualReview();app.BeginVisualReview(1,1);app.VisualReviewPaused=false;app.Game.enemies.Clear();}
            NoLaterCast(app,boundary);
        }
        Prepare(app); Queue(app); var game=app.Game; float cooldown=game.skillCooldown;
        app.VisualReviewPaused=true;Frames(app,30);Check(game.skillCooldown==cooldown && game.playerAction==null,"Review lab Freeze preserves the queued Q and cooldown on one stopped clock");
        app.VisualReviewPaused=false;ActorAction resumed=AwaitCast(app);Check(Near(game.skillCooldown,3.5f) && resumed.elapsed==0,"Unfreezing the same lab simulation resumes exactly one pending cast");
        Prepare(app);Queue(app);game=app.Game;game.hitStop=.1f;cooldown=game.skillCooldown;Frames(app,4);
        Check(game.skillCooldown==cooldown && game.playerAction==null,"Existing hit stop freezes cooldown and buffer together");AwaitCast(app);
        Console.WriteLine("PASS real menu/application/focus pauses, checksummed save/load, travel, combat profile changes, practice/range/drill review/reset/exit and replacement review discard Q; same-review Freeze and hit stop preserve one paused simulation clock; NATIVE_UNVERIFIED");
    }
    private static void VariableFramesDeadlineCostHealAndActualContact(FrontierRuntime app)
    {
        foreach(float dt in new[]{1f/120,1f/60,1f/30,.04f,.05f})
        {
            // The real key is sampled after Tick. Stay one float step inside
            // .20: (.20f + .04f) - .04f is .200000018, which is outside it.
            Prepare(app);Time.unscaledDeltaTime=dt;Queue(app,false,.20f-.0000001f);var g=app.Game;
            Check(g.playerAction==null && g.skillCooldown>0 && g.skillCooldown<=GameCore.SkillBufferWindow,"Accepted final-window cooldown remains pending at dt="+dt+", cooldown="+g.skillCooldown.ToString("R"));
            ActorAction cast=AwaitCast(app,40);Check(Near(g.skillCooldown,3.5f) && Near(cast.elapsed,0),"Final valid tick dispatches without ageing the new cast at dt="+dt);
            Prepare(app);Time.unscaledDeltaTime=dt;g=app.Game;Input.held.Add(KeyCode.J);Update(app);Input.held.Clear();ActorAction attack=g.playerAction;
            Check(attack!=null && attack.kind=="attack","Actual J starts the same-clock recovery fixture");
            while(attack.contact+.1f-attack.elapsed>.18f)Update(app);
            Queue(app,false,.12f);Check(g.playerAction==attack,"Cooldown plus original contact recovery queues without cancelling its action");
            float elapsed=0;
            while(g.playerAction==attack && elapsed<.3f){Update(app);elapsed+=dt;}
            Check(g.playerAction!=null && g.playerAction.kind=="skill" && elapsed<=.20f+dt+.0001f,"Cooldown and recovery use their maximum delay rather than adding clocks at dt="+dt);
        }
        // OnGUI samples without a preceding Tick and can request exactly .20f.
        Prepare(app);Time.unscaledDeltaTime=.04f;app.Game.skillCooldown=.20f;GUI.queuedClick=SkillSlot(false);Draw(app);
        Check(app.Game.playerAction==null && Near(app.Game.skillCooldown,.20f),"Actual HUD accepts the exact .20f boundary without advancing it");AwaitCast(app);
        Prepare(app);var game=app.Game;Input.held.Add(KeyCode.J);Update(app);Input.held.Clear();Queue(app,false,.12f);
        NoLaterCast(app,"recovery beyond the .20s deadline",45);
        foreach(bool relic in new[]{false,true})
        {
            Prepare(app,0);game=app.Game;game.save.energy=70;game.save.health=50;if(relic)game.save.relics[0]=4;
            float health=game.save.health;Queue(app);Check(game.playerAction==null && game.save.health==health && !game.effects.Exists(e=>e.kind=="healthGain"),"Queued Q neither charges nor heals before it starts");
            game.save.upgrades+=1;float damage=game.Damage;ActorAction cast=null;
            for(int i=0;i<20 && cast==null;i++)
            {
                float previous=game.save.energy;Update(app);
                if(game.playerAction!=null && game.playerAction.kind=="skill")
                {
                    cast=game.playerAction;float expected=Math.Min(100,previous+Time.unscaledDeltaTime*14)-(relic?50:40);
                    Check(Near(game.save.energy,expected) && Near(game.skillCooldown,3.5f),"Actual start uses the original "+(relic?50:40)+" energy transaction and 3.5s cooldown");
                }
                else Check(game.save.health==health,"No relic recovery occurs while the request waits");
            }
            Check(cast!=null && Near(cast.damage,damage),"Actual cast captures current damage rather than the press-time value");
            Check(Near(game.save.health,health+(relic?8:0)) && game.effects.FindAll(e=>e.kind=="healthGain").Count==(relic?1:0),"Relic 4 heals exactly eight once at the original cast-start transaction");
            var target=new Enemy(59401,0,game.Position.x+2,game.Position.y,1000){timer=9999};game.enemies.Add(target);float hp=target.hp;
            while(cast.elapsed+Time.unscaledDeltaTime<cast.contact-.00001f){Update(app);Check(Near(target.hp,hp) && game.guardTime<=0,"Actual enemy health and Guard remain unchanged before Q contact");}
            Release(app,cast);Check(target.hp<hp && game.guardTime>2.99f,"The original visible contact alone damages the actual target and grants Vanguard Guard");
        }
        Console.WriteLine("PASS real 120/60/30/25/20fps Update boundaries, one maximum cooldown/recovery delay, expiry beyond .20s, unchanged 40/Relic4 50 cost and 3.5s cooldown, live damage capture, eight-point cast-start heal and actual enemy contact; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-skill-cooldown-"+Guid.NewGuid());Application.persistentDataPath=directory;
        captureFolder=args.Length>0 && args[0]!="-"?args[0]:null;if(captureFolder!=null)Directory.CreateDirectory(captureFolder);
        var app=new FrontierRuntime();int failed=0;
        try
        {
            Call(app,"Awake");Check(app.Game!=null,"Actual Awake loads the shipped campaign");
            foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{ActualKeyboardPadHeldAttackAndCapturedAim,BothMouseHudLayoutsAndEightBilingualCaptures,PausesSaveLifecycleProfilesAndTraining,VariableFramesDeadlineCostHealAndActualContact})
                try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}
        }
        finally{app.EndVisualReview();Call(app,"OnDestroy");Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime skill cooldown buffer: "+(4-failed)+"/4 groups passed; captures="+captures+"; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
