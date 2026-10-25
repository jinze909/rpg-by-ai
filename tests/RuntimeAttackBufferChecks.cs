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

    private static readonly KeyCode[] OpeningMenuKeys = {
        KeyCode.Escape, KeyCode.Tab, KeyCode.I, KeyCode.K, KeyCode.M, KeyCode.B,
        KeyCode.C, KeyCode.G, KeyCode.N, KeyCode.F1, KeyCode.JoystickButton6, KeyCode.JoystickButton7 };
    private static readonly string[] OpeningMenuModes = {
        "pause", "journal", "inventory", "talents", "map", "camp", "companions", "loadout", "bounties", "help", "journal", "pause" };
    private static bool PaidIntentStarted(GameCore game, string action)
    { return action=="dodge" ? game.dashTime>0 : game.playerAction!=null && game.playerAction.kind==action; }
    private static float PaidIntentCost(string action) { return action=="attack"?4:action=="skill"?40:20; }
    private static void ReadyPaidIntent(GameCore game, string action, float remaining)
    { if(action=="attack")game.attackCooldown=remaining;else if(action=="skill")game.skillCooldown=remaining;else game.dashCooldown=remaining; }
    private static void QueuePaidIntent(FrontierRuntime app, string action, float dt)
    {
        Prepare(app); Time.unscaledDeltaTime=dt; var g=app.Game; g.save.weaponStyles[g.save.weapon]=1;
        ReadyPaidIntent(g,action,.10f+dt);
        Key(app,action=="attack"?KeyCode.J:action=="skill"?KeyCode.Q:KeyCode.Space);
        var field=typeof(GameCore).GetField(action=="attack"?"bufferedAttack":action=="skill"?"bufferedSkill":"bufferedDodge",Private);
        Check(field!=null && field.GetValue(g)!=null && !PaidIntentStarted(g,action) && Near(g.save.energy,100),
            "Actual paid "+action+" input is unstarted and queued before the opening-menu boundary");
    }
    private static void NoPaidStartAfterMenu(FrontierRuntime app, string reason)
    {
        Input.Clear(); app.Game.Close();
        for(int i=0;i<30;i++)
        {
            Update(app); Check(app.Game.playerAction==null && app.Game.dashTime<=0,
                "Opening a valid menu abandons every unstarted hero intent without delayed "+reason);
        }
    }
    // Actual Update ordering matters: a mode assigned before Tick does not
    // reproduce a menu key arriving on an intent's exact readiness frame.
    private static void OpeningMenusOwnUnstartedAndFreshCombatInput(FrontierRuntime app)
    {
        int scenarios=0;
        foreach(float dt in new[]{.01f,.02f,.05f}) foreach(string action in new[]{"attack","skill","dodge"})
        {
            for(int index=0;index<OpeningMenuKeys.Length;index++) foreach(bool exact in new[]{false,true})
            {
                scenarios++; QueuePaidIntent(app,action,dt); var g=app.Game;
                if(exact)ReadyPaidIntent(g,action,dt);
                Key(app,OpeningMenuKeys[index]);
                Check(g.mode==OpeningMenuModes[index],"Actual menu key opens its existing world destination: "+OpeningMenuKeys[index]);
                Check(Near(g.save.energy,100) && g.playerAction==null && g.dashTime<=0,
                    "Menu "+OpeningMenuKeys[index]+" wins before an unstarted paid "+action+" at dt="+dt+" exact="+exact+" can spend or begin");
                Check(g.skillCooldown<1 && g.attackCooldown<.2f && g.dashCooldown<.2f,
                    "A canceled request never starts a fresh hero cooldown");
                NoPaidStartAfterMenu(app,OpeningMenuKeys[index]+" / "+action);
            }
            // The same boundary with no opening menu still dispatches normally.
            scenarios++; QueuePaidIntent(app,action,dt); var normal=app.Game; ReadyPaidIntent(normal,action,dt); Update(app);
            Check(PaidIntentStarted(normal,action) && Near(normal.save.energy,100-PaidIntentCost(action)),
                "No-menu exact readiness preserves the original paid "+action+" transaction at dt="+dt);
            Check(action=="attack"?Near(normal.attackCooldown,normal.Profile(normal.save.weapon,normal.Style).interval)
                :action=="skill"?Near(normal.skillCooldown,3.5f):Near(normal.dashCooldown,.75f),
                "Ordinary ready dispatch preserves the original paid cooldown");
        }
        foreach(float dt in new[]{.01f,.02f,.05f}) for(int index=0;index<OpeningMenuKeys.Length;index++)
        foreach(string fresh in new[]{"fresh-attack","held-attack","skill","dodge","all"})
        {
            scenarios++; Prepare(app);Time.unscaledDeltaTime=dt;var g=app.Game;g.save.weaponStyles[g.save.weapon]=1;
            Input.keys.Add(OpeningMenuKeys[index]);
            if(fresh=="fresh-attack" || fresh=="all")Input.keys.Add(KeyCode.J);
            if(fresh=="fresh-attack" || fresh=="held-attack" || fresh=="all")Input.held.Add(KeyCode.J);
            if(fresh=="skill" || fresh=="all")Input.keys.Add(KeyCode.Q);
            if(fresh=="dodge" || fresh=="all")Input.keys.Add(KeyCode.Space);
            Update(app);Input.held.Clear();
            Check(g.mode==OpeningMenuModes[index] && Near(g.save.energy,100) && g.playerAction==null && g.dashTime<=0,
                "Same-frame "+fresh+" cannot start or spend ahead of actual menu "+OpeningMenuKeys[index]+" at dt="+dt);
            NoPaidStartAfterMenu(app,OpeningMenuKeys[index]+" / simultaneous "+fresh);
        }
        foreach(string action in new[]{"attack","skill","dodge"}) for(int index=0;index<OpeningMenuKeys.Length;index++)
        {
            scenarios++; QueuePaidIntent(app,action,.02f);var g=app.Game;ReadyPaidIntent(g,action,.02f);Update(app);
            Check(PaidIntentStarted(g,action),"Lifecycle fixture has an actually started paid action before the menu key");
            ActorAction started=g.playerAction;Key(app,OpeningMenuKeys[index]);
            float elapsed=started==null?0:started.elapsed, dash=g.dashTime, energy=g.save.energy, clock=g.save.totalTime;
            float attack=g.attackCooldown, skill=g.skillCooldown, dodge=g.dashCooldown;
            Frames(app,8);
            Check(g.mode==OpeningMenuModes[index] && Near(g.save.totalTime,clock) && Near(g.save.energy,energy)
                && ReferenceEquals(g.playerAction,started) && (started==null || Near(started.elapsed,elapsed)) && Near(g.dashTime,dash)
                && Near(g.attackCooldown,attack) && Near(g.skillCooldown,skill) && Near(g.dashCooldown,dodge),
                "A real opening menu freezes rather than cancels or repays an already accepted "+action);
            Key(app,KeyCode.Escape);
            Check(g.mode=="world" && ReferenceEquals(g.playerAction,started) && Near(g.dashTime,dash),
                "Returning from the actual menu retains the paid action and its captured preparation");
            if(started!=null)
            {
                int frames=0;while(!started.released && frames++<100)Update(app);
                Check(started.released && (action=="attack"?g.projectiles.Exists(p=>p.friendly):g.projectiles.Exists(p=>p.friendly && p.technique)),
                    "Previously accepted "+action+" reaches its original real release after menu return");
            }
            else { Update(app); Check(g.dashTime<dash && g.dashTime>0,"Paid dodge resumes the remaining original movement clock"); }
        }
        // These two existing Toggle gates can survive while the world owner is
        // restored. Staging the fields is labelled input-gate coverage, not a
        // claim that the new-game or save modal normally belongs to the world.
        foreach(string gate in new[]{"replacePrompt","saveConfirmation"}) foreach(string action in new[]{"attack","skill","dodge"})
        foreach(KeyCode ignored in new[]{KeyCode.I,KeyCode.F1})
        {
            scenarios++; QueuePaidIntent(app,action,.02f);var g=app.Game;ReadyPaidIntent(g,action,.02f);
            if(gate=="replacePrompt")Set(app,gate,true);else Set(app,gate,"read");
            Key(app,ignored);
            Check(g.mode=="world" && PaidIntentStarted(g,action) && Near(g.save.energy,100-PaidIntentCost(action)),
                "A Toggle-blocked "+ignored+" under "+gate+" is not a menu opening and cannot discard the ready request");
            if(gate=="replacePrompt")Set(app,gate,false);else Set(app,gate,null);
        }
        // Enter real non-world owners: ordinary dialogue and the original
        // fishing flow retain their keyboard semantics rather than opening I/K.
        scenarios++; Prepare(app);var dialogGame=app.Game;dialogGame.Talk(new WorldObject { kind="innGuest",name="Guest",en="Guest" });
        Conversation conversation=dialogGame.conversation;Check(dialogGame.mode=="dialog" && conversation!=null,"Actual NPC handler owns the dialogue fixture");
        float dialogClock=dialogGame.save.totalTime;Key(app,KeyCode.I);Key(app,KeyCode.K);
        Check(dialogGame.mode=="dialog" && ReferenceEquals(dialogGame.conversation,conversation) && Near(dialogGame.save.totalTime,dialogClock)
            && dialogGame.playerAction==null && dialogGame.dashTime<=0,"Dialogue ignores world menu/gameplay shortcuts without simulating a paid action");
        scenarios++; Prepare(app,1,0);var fishing=app.Game;WorldObject fisher=Array.Find(fishing.Map.objects,o=>o.kind=="fish");
        Check(fisher!=null,"Mature town fisherman remains present");fishing.Talk(fisher);
        int fishChoice=Array.FindIndex(fishing.conversation.choices,c=>c.action=="fish");Check(fishChoice>=0,"Original cast-line choice remains present");fishing.Choose(fishChoice);
        Check(fishing.mode=="fish","Actual dialogue choice starts original fishing");float fishingTime=fishing.fishTime, worldClock=fishing.save.totalTime;
        Key(app,KeyCode.I);Key(app,KeyCode.K);
        Check(fishing.mode=="fish" && fishing.fishTime>fishingTime && Near(fishing.save.totalTime,worldClock) && fishing.playerAction==null && fishing.dashTime<=0,
            "I/K leave the real fishing owner and its own clock intact");
        // Live paid preparations survive real confirmation modals, whose
        // parent mode already cleared unstarted requests on entry.
        foreach(bool town in new[]{false,true})
        {
            scenarios++; QueuePaidIntent(app,"attack",.02f);var g=app.Game;ReadyPaidIntent(g,"attack",.02f);Update(app);ActorAction paid=g.playerAction;
            Key(app,town?KeyCode.B:KeyCode.K);Call(app,"OnGUI");
            if(town)Call(app,"BeginTownProjectConfirmation",0);else Call(app,"BeginTalentResetConfirmation");
            object modal=Get<object>(app,town?"townProjectConfirmation":"talentResetConfirmation");Check(modal!=null,"Actual parent menu owns a real confirmation quote");
            float elapsed=paid.elapsed,energy=g.save.energy;Key(app,KeyCode.I);Key(app,KeyCode.F1);
            Check(g.mode==(town?"camp":"talents") && ReferenceEquals(Get<object>(app,town?"townProjectConfirmation":"talentResetConfirmation"),modal)
                && ReferenceEquals(g.playerAction,paid) && Near(paid.elapsed,elapsed) && Near(g.save.energy,energy),
                "Real confirmation blocks world menu shortcuts without resuming, canceling or paying the existing preparation");
            Key(app,KeyCode.Escape);Key(app,KeyCode.Escape);Update(app);
            Check(g.mode=="world" && ReferenceEquals(g.playerAction,paid) && paid.elapsed>elapsed,"Cancel confirmation then return resumes the owned paid preparation");
        }
        Check(scenarios==457,"Every one of the 457 menu-boundary scenarios must execute");
        Input.Clear();Time.unscaledDeltaTime=.02f;
        Console.WriteLine("PASS 457 actual opening-menu Update scenarios across 12 keyboard/pad destinations, paid J/Q/Space early/exact readiness at 10/20/50ms, fresh/held simultaneous commands, no-menu dispatch, paid action pause/resume and rejected world/non-world/modal shortcut ownership; NATIVE_UNVERIFIED");
    }

    public static int Main()
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-attack-buffer-"+Guid.NewGuid());
        Application.persistentDataPath=directory;var app=new FrontierRuntime();int failures=0;
        try
        {
            Call(app,"Awake");Check(app.Game!=null,"Actual Awake loads the shipped campaign");
            foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{FreshKeyboardPadMouseAndMatureHeldRepeat,BothHudLayoutsAndInterfaceMouseGating,QAndDodgeWinBeforeTickAndFreshBasic,PauseSaveAndTrainingBoundaries,OpeningMenusOwnUnstartedAndFreshCombatInput})
                try{test(app);}catch(Exception error){failures++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}
        }
        finally{app.EndVisualReview();Call(app,"OnDestroy");Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("Runtime attack buffer: "+(5-failures)+"/5 groups passed; NATIVE_UNVERIFIED");return failures==0?0:1;
    }
}
