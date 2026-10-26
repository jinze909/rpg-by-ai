// Shipped Update input integration with controllable Unity declarations.
// No native Editor, device or graphics validation is implied: NATIVE_UNVERIFIED.
using System;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeInteractionPriorityChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int scenarios;
    private static void Check(bool value,string message) { if(!value)throw new Exception(message); }
    private static bool Near(float a,float b) { return Math.Abs(a-b)<.003f; }
    private static object Call(FrontierRuntime app,string method,params object[] args)
    { return typeof(FrontierRuntime).GetMethod(method,Private).Invoke(app,args); }
    private static void Set(FrontierRuntime app,string field,object value)
    { typeof(FrontierRuntime).GetField(field,Private).SetValue(app,value); }
    private static T Get<T>(FrontierRuntime app,string field)
    { return (T)typeof(FrontierRuntime).GetField(field,Private).GetValue(app); }
    private static object Intent(GameCore game,string field)
    { return typeof(GameCore).GetField(field,Private).GetValue(game); }
    private static void Update(FrontierRuntime app)
    { Call(app,"Update");Input.keys.Clear();Input.buttonDown.Clear(); }
    private static void Key(FrontierRuntime app,KeyCode key) { Input.keys.Add(key);Update(app); }
    private static void Frames(FrontierRuntime app,int count) { for(int i=0;i<count;i++)Update(app); }
    private static void Mouse(FrontierRuntime app,Point world)
    {
        Point pixel=(Point)Call(app,"ScreenPoint",world);float s=Get<float>(app,"scale");
        Input.mousePosition=new Vector3(Get<float>(app,"offsetX")+pixel.x*s,Screen.height-Get<float>(app,"offsetY")-pixel.y*s,0);
    }
    private static void Position(FrontierRuntime app,Point point)
    {
        Check(app.SetVisualReviewPosition(point) && (app.Game.Position-point).Length<.003f,"Fixture uses exact existing walkable ground "+point.x+","+point.y);
        Set(app,"camera",app.Game.Position);Mouse(app,app.Game.Position+new Point(4,0));
    }
    private static void Prepare(FrontierRuntime app,int room=0,int profession=1)
    {
        Input.Clear();GUI.queuedClick=null;GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();
        GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;
        Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;
        app.BeginVisualReview(profession,room);app.VisualReviewPaused=false;var g=app.Game;
        g.enemies.Clear();g.projectiles.Clear();g.hazards.Clear();g.effects.Clear();g.fallen.Clear();
        g.save.companion=-1;g.save.reducedMotion=true;g.save.effectsQuality=0;g.save.showGuidance=false;
        g.save.energy=100;g.save.health=g.save.maxHealth;g.save.totalTime=100;g.save.technique=0;g.save.tea=2;
        g.save.foodUntil=g.save.teaUntil=g.save.mealUntil=0;g.save.relics=new[]{-1,-1};g.save.talents=new int[3];
        g.save.weaponStyles[g.save.weapon]=1;g.save.controllerAimProfile=0;g.save.controllerAimInvertY=false;
        g.invulnerable=0;g.toastTime=0;
        Set(app,"applicationPaused",false);Set(app,"controllerUsed",false);Set(app,"pointerSeen",false);
        Set(app,"pointerAimActive",false);Set(app,"combatHudTime",0f);Set(app,"arrivalTime",0f);Set(app,"resumeTime",0f);
        Call(app,"Resize");Call(app,"ResetControllerAim",false);
        Position(app,room==0?new Point(7,9):room==6?new Point(6,12.6f):new Point(12,14));
    }
    private static void Companion(GameCore game)
    { if(!game.Has("companion_1"))game.save.flags.Add("companion_1");game.save.companion=1;game.companionPosition=game.Position+new Point(.6f,0); }
    private static void Ready(GameCore game,string action,float remaining)
    {
        if(action=="attack")game.attackCooldown=remaining;
        else if(action=="skill")game.skillCooldown=remaining;
        else if(action=="dodge")game.dashCooldown=remaining;
        else game.companionCooldown=remaining;
    }
    private static KeyCode ActionKey(string action,bool pad)
    { return action=="attack"?(pad?KeyCode.JoystickButton2:KeyCode.J):action=="skill"?(pad?KeyCode.JoystickButton3:KeyCode.Q):action=="dodge"?(pad?KeyCode.JoystickButton0:KeyCode.Space):(pad?KeyCode.JoystickButton5:KeyCode.Z); }
    private static void Queue(FrontierRuntime app,string action,bool pad,float dt)
    {
        if(action=="companion")Companion(app.Game);
        Time.unscaledDeltaTime=dt;Ready(app.Game,action,.1f+dt);Key(app,ActionKey(action,pad));
        string field=action=="attack"?"bufferedAttack":action=="skill"?"bufferedSkill":action=="dodge"?"bufferedDodge":"bufferedCompanionCommand";
        Check(Intent(app.Game,field)!=null && app.Game.playerAction==null && app.Game.dashTime<=0 && app.Game.companionAction==null,"Actual Update queues the unpaid "+action);
        Ready(app.Game,action,dt);
    }
    private static void NoPaidAction(GameCore g,string reason)
    {
        Check(g.playerAction==null && g.dashTime<=0 && g.companionAction==null && Near(g.save.energy,100),reason+" starts no unpaid action and spends no energy");
        Check(Intent(g,"bufferedAttack")==null && Intent(g,"bufferedSkill")==null && Intent(g,"bufferedDodge")==null && !g.CompanionCommandQueued,reason+" discards every unstarted combat request");
    }
    private static void NoLaterAction(FrontierRuntime app,string reason)
    {
        Input.Clear();app.Game.Close();Frames(app,20);NoPaidAction(app.Game,"After closing "+reason);
    }
    private static void FreshAndQueuedInputYieldsToRealInteraction(FrontierRuntime app)
    {
        foreach(float dt in new[]{.01f,.02f,.05f}) foreach(bool pad in new[]{false,true})
        foreach(string fresh in new[]{"key-attack","held-key","pad-attack","held-pad","mouse-attack","held-mouse","skill","dodge","companion","all"})
        {
            scenarios++;Prepare(app);Time.unscaledDeltaTime=dt;var g=app.Game;Companion(g);
            Input.keys.Add(pad?KeyCode.JoystickButton1:KeyCode.E);
            if(fresh=="key-attack" || fresh=="all")Input.keys.Add(KeyCode.J);
            if(fresh=="held-key" || fresh=="all")Input.held.Add(KeyCode.J);
            if(fresh=="pad-attack")Input.keys.Add(KeyCode.JoystickButton2);
            if(fresh=="held-pad")Input.held.Add(KeyCode.JoystickButton2);
            if(fresh=="mouse-attack" || fresh=="all")Input.buttonDown.Add(0);
            if(fresh=="held-mouse" || fresh=="mouse-attack" || fresh=="all")Input.buttons.Add(0);
            if(fresh=="skill" || fresh=="all")Input.keys.Add(pad?KeyCode.JoystickButton3:KeyCode.Q);
            if(fresh=="dodge" || fresh=="all")Input.keys.Add(pad?KeyCode.JoystickButton0:KeyCode.Space);
            if(fresh=="companion" || fresh=="all")Input.keys.Add(pad?KeyCode.JoystickButton5:KeyCode.Z);
            if(fresh=="all") { Input.keys.Add(KeyCode.F);Input.keys.Add(KeyCode.T);Input.keys.Add(KeyCode.H);Input.keys.Add(KeyCode.R); }
            int stew=g.save.inventory[6],tea=g.save.tea;Update(app);Input.Clear();
            Check(g.mode=="dialog" && g.conversation!=null && g.conversation.speaker==Array.Find(g.Map.objects,o=>o.id=="smith").name,"Actual E/B reaches Noll before "+fresh+" at dt="+dt);
            NoPaidAction(g,"Real E/B + "+fresh);Check(g.save.inventory[6]==stew && g.save.tea==tea && g.save.room==0,"Interaction owns same-frame supplies and return/exploration shortcuts");
            NoLaterAction(app,"Noll dialogue");
        }
        foreach(float dt in new[]{.01f,.02f,.05f}) foreach(bool pad in new[]{false,true})
        foreach(string action in new[]{"attack","skill","dodge","companion"})
        {
            scenarios++;Prepare(app);Queue(app,action,pad,dt);var g=app.Game;
            Key(app,pad?KeyCode.JoystickButton1:KeyCode.E);
            Check(g.mode=="dialog","Exact-ready queued "+action+" still opens the actual Noll dialogue");
            NoPaidAction(g,"Interaction at exact-ready "+action);NoLaterAction(app,"queued "+action);
        }
        foreach(bool hitStop in new[]{false,true}) foreach(bool pad in new[]{false,true})
        foreach(string action in new[]{"attack","skill","dodge","companion"})
        {
            scenarios++;Prepare(app);Queue(app,action,pad,.02f);var g=app.Game;
            Time.unscaledDeltaTime=hitStop?.02f:0;g.hitStop=hitStop?.1f:0;
            float clock=g.save.totalTime;Key(app,pad?KeyCode.JoystickButton1:KeyCode.E);
            Check(g.mode=="dialog" && Near(g.save.totalTime,clock),"Zero-delta/contact-hit-stop interaction retains original frozen world clock");
            NoPaidAction(g,"Frozen interaction retires queued "+action);
            Time.unscaledDeltaTime=.02f;NoLaterAction(app,"frozen interaction "+action);
        }
        Console.WriteLine("PASS real E/B outranks fresh/held J/X/mouse, Q/Y, dodge and Z/RB, same-frame consumables, exact-ready queues at 10/20/50ms and zero-delta/hit-stop requests; original Noll dialogue and no latent transaction; NATIVE_UNVERIFIED");
    }

    private static void PostMovementNearestResourcesPortalsAndInvalidRequests(FrontierRuntime app)
    {
        foreach(bool approaching in new[]{true,false}) foreach(bool queued in new[]{false,true})
        {
            scenarios++;Prepare(app);var g=app.Game;Position(app,new Point(7,approaching?9.76f:9.74f));
            Check((g.Nearest()!=null)==!approaching,"Fixture straddles exact 1.75 interaction radius before moving");
            if(queued)Queue(app,"attack",false,.02f);
            Input.axes["Vertical"]=approaching?1:-1;Input.keys.Add(KeyCode.E);if(!queued)Input.keys.Add(KeyCode.J);Update(app);Input.Clear();
            if(approaching) { Check(g.mode=="dialog" && g.Nearest()!=null && g.Nearest().id=="smith","Movement into range uses the real post-movement nearest object");NoPaidAction(g,"Approach plus E"); }
            else Check(g.mode=="world" && g.Nearest()==null && g.playerAction!=null && g.playerAction.kind=="attack" && g.save.energy<100,"Moving out of range keeps original fresh/queued attack dispatch");
        }
        foreach(bool pad in new[]{false,true})
        {
            scenarios++;Prepare(app,1);var g=app.Game;Position(app,new Point(8,17.4f));WorldObject herb=Array.Find(g.Map.objects,o=>o.id=="herb1");
            g.save.flags.Remove("node_herb1");g.save.explored[1]=(1<<24)-1;string tracked=g.save.trackedQuest;g.mode="landmarks";Check(g.PinLandmark(herb),"Actual discovered herb pin is set");g.Close();
            int herbs=g.save.inventory[2];Input.keys.Add(pad?KeyCode.JoystickButton1:KeyCode.E);Input.keys.Add(pad?KeyCode.JoystickButton3:KeyCode.Q);Input.keys.Add(KeyCode.F);Input.keys.Add(KeyCode.H);Update(app);Input.Clear();
            Check(g.mode=="world" && g.save.room==1 && g.Has("node_herb1") && g.save.inventory[2]==herbs+3 && !g.HasLandmarkPin && g.save.trackedQuest==tracked,"Real gathering pays the original +3 once, retires its pin and preserves tracked story");
            NoPaidAction(g,"World-mode gathering");Key(app,pad?KeyCode.JoystickButton1:KeyCode.E);Check(g.save.inventory[2]==herbs+3,"Repeated actual E/B on the depleted node never pays twice");
            scenarios++;Prepare(app);g=app.Game;Position(app,new Point(18,3.6f));Check(g.Nearest()!=null && g.Nearest().kind=="portal1","Fixture owns the real Thornwood exit");
            Queue(app,"attack",false,.02f);Input.keys.Add(pad?KeyCode.JoystickButton1:KeyCode.E);Input.keys.Add(KeyCode.Space);Update(app);Input.Clear();
            Check(g.mode=="world" && g.save.room==1,"Real E/B enters Thornwood through the existing portal");NoPaidAction(g,"Real region transition");g.enemies.Clear();NoLaterAction(app,"travel destination");
        }
        foreach(string action in new[]{"attack","skill","dodge","companion"}) foreach(bool pad in new[]{false,true})
        {
            scenarios++;Prepare(app);var g=app.Game;Position(app,new Point(12,10));Check(g.Nearest()==null,"Invalid E fixture has no nearby object");Queue(app,action,pad,.02f);Key(app,pad?KeyCode.JoystickButton1:KeyCode.E);
            Check(g.mode=="world" && (action=="dodge"?g.dashTime>0:action=="companion"?g.companionAction!=null:g.playerAction!=null && g.playerAction.kind==action),"No nearby interaction retains original queued "+action+" dispatch");
        }
        Console.WriteLine("PASS actual movement enters/leaves the 1.75 boundary before choosing the nearest target; resource rewards/pin retirement and portal transitions use their mature handlers; unavailable E/B leaves ready combat intact; NATIVE_UNVERIFIED");
    }

    private static string WorldWitness(GameCore g,Enemy enemy,Projectile shot,Effect effect,Hazard hazard)
    {
        return string.Join("|",new[]{g.save.totalTime,g.save.energy,g.save.health,g.save.heat,g.save.cold,g.attackCooldown,g.skillCooldown,g.dashCooldown,g.guardTime,
            enemy.x,enemy.y,enemy.timer,shot.position.x,shot.position.y,shot.life,effect.life,hazard.timer});
    }
    private static void PaidContactsAndWholeWorldClocksKeepAdvancing(FrontierRuntime app)
    {
        foreach(string action in new[]{"attack","skill","companion"})
        {
            scenarios++;Prepare(app);var g=app.Game;if(action=="companion")Companion(g);
            Key(app,ActionKey(action,false));ActorAction paid=action=="companion"?g.companionAction:g.playerAction;
            Check(paid!=null && !paid.released,"Existing real "+action+" input owns an accepted preparation");float cooldown=action=="attack"?g.attackCooldown:action=="skill"?g.skillCooldown:g.companionCooldown;
            float capturedDamage=paid.damage,energy=g.save.energy;paid.elapsed=paid.contact-Time.unscaledDeltaTime;
            Input.keys.Add(KeyCode.E);Input.keys.Add(KeyCode.Q);Input.keys.Add(KeyCode.Space);Update(app);Input.Clear();
            Check(g.mode=="dialog" && ReferenceEquals(paid,action=="companion"?g.companionAction:g.playerAction) && paid.released && Near(paid.elapsed,paid.contact) && Near(paid.damage,capturedDamage),"Opening interaction preserves the accepted "+action+" owner and its original due contact");
            Check(g.projectiles.Exists(p=>p.friendly && (action!="skill" || p.technique)),"Accepted "+action+" contact emits its original projectile before dialogue pauses");
            Check(Near(g.save.energy,Math.Min(100,energy+.28f)) && Near(action=="attack"?g.attackCooldown:action=="skill"?g.skillCooldown:g.companionCooldown,cooldown-.02f),"Interaction neither repays nor replaces the accepted "+action+" cost and cooldown");
            int shots=g.projectiles.Count;float elapsed=paid.elapsed;Frames(app,5);Check(g.projectiles.Count==shots && Near(paid.elapsed,elapsed),"Real dialogue freezes the accepted contact after this frame");
            g.Close();Frames(app,45);Check(g.playerAction==null && g.companionAction==null,"Closing dialogue resumes original recovery without a fresh input");
        }
        scenarios++;Prepare(app);var dashGame=app.Game;Key(app,KeyCode.Space);float oldDash=dashGame.dashTime,oldEnergy=dashGame.save.energy;
        Key(app,KeyCode.E);Check(dashGame.mode=="dialog" && dashGame.dashTime>0 && dashGame.dashTime<oldDash && Near(dashGame.save.energy,oldEnergy+.28f),"An already paid dodge keeps moving on the interaction frame and is not canceled or paid twice");
        string control=null;
        foreach(bool interaction in new[]{false,true})
        {
            scenarios++;Prepare(app,6);var g=app.Game;g.save.energy=60;g.save.heat=40;g.save.cold=12;
            g.attackCooldown=.8f;g.skillCooldown=.9f;g.dashCooldown=.7f;g.guardTime=.6f;
            var foe=new Enemy(9001,0,11,12.6f,100){timer=.9f};g.enemies.Add(foe);
            var shot=new Projectile{friendly=false,position=new Point(12,12),velocity=new Point(1,0),life=2,damage=7};g.projectiles.Add(shot);
            var effect=new Effect("projectileFade",new Point(12,15),1);g.effects.Add(effect);
            var hazard=new Hazard{kind="rift",position=new Point(20,18),timer=.8f,radius=1,damage=4};g.hazards.Add(hazard);
            Check(g.Nearest()!=null && g.Nearest().kind=="campfire","World witness uses an actual authored camp");
            if(interaction)Input.keys.Add(KeyCode.E);Update(app);
            string witness=WorldWitness(g,foe,shot,effect,hazard);
            Check(Near(g.save.totalTime,100.02f) && g.save.energy>60 && g.save.heat>40 && g.save.cold<12 && shot.position.x>12 && shot.life<2 && effect.life<1 && hazard.timer<.8f,"Interaction frame advances world time, regen, heat/cold, hostile flight, effects and hazard clocks");
            if(!interaction)control=witness;else Check(witness==control && g.mode=="dialog","All captured world and enemy clocks equal a no-E simulation frame; only the later actual camp dialogue differs");
        }
        Console.WriteLine("PASS already accepted hero/companion releases and paid dodge survive interaction, pay only their original costs and pause afterward; no-E comparison retains world/enemy/projectile/effect/hazard/climate clocks; NATIVE_UNVERIFIED");
    }

    private static void MenuTrainingOwnerBoundariesAndUnchangedControls(FrontierRuntime app)
    {
        foreach(KeyCode menu in new[]{KeyCode.Escape,KeyCode.Tab,KeyCode.I,KeyCode.M,KeyCode.JoystickButton6,KeyCode.JoystickButton7})
        {
            scenarios++;Prepare(app);Queue(app,"attack",false,.02f);Input.keys.Add(menu);Input.keys.Add(KeyCode.E);Input.keys.Add(KeyCode.J);Update(app);Input.Clear();
            Check(app.Game.mode!="world" && app.Game.mode!="dialog","Original world menu shortcut retains priority over E and attack: "+menu);NoPaidAction(app.Game,"Original menu priority");
        }
        foreach(string boundary in new[]{"application","focus","load","travel","review"})
        {
            scenarios++;Prepare(app);Queue(app,"attack",false,.02f);var g=app.Game;
            if(boundary=="application") { Call(app,"OnApplicationPause",true);Call(app,"OnApplicationPause",false); }
            else if(boundary=="focus") { Call(app,"OnApplicationFocus",false);g.Close(); }
            else if(boundary=="load") { g.PrepareSave();Check(SaveStore.Write(g.save,2),"Actual checksum save succeeds");string warning;SaveData saved=SaveStore.Read(2,out warning);Check(saved!=null && warning==null,"Actual checksum owner can be restored");g.Load(saved);g.Close();g.enemies.Clear(); }
            else if(boundary=="travel") { g.Enter(9);g.Enter(0);Position(app,new Point(7,9)); }
            else { Prepare(app);g=app.Game; }
            Key(app,KeyCode.E);Check(g.mode=="dialog","Restored "+boundary+" owner still interacts normally");NoPaidAction(g,"Restored "+boundary+" owner");NoLaterAction(app,boundary);
        }
        scenarios++;Prepare(app);var training=app.Game;WorldObject mentor=Array.Find(training.Map.objects,o=>o.kind=="mentor");Position(app,new Point(mentor.x,mentor.y-.7f));training.save.practiceBits=7;Check(training.StartSparring(),"Actual Ada sparring starts");
        training.enemies.Clear();Update(app);Check(training.SparringAwaitingNext,"Real first stage clear awaits player continuation");Key(app,KeyCode.E);Check(training.SparringActive && training.SparringStage==1 && !training.SparringAwaitingNext,"E retains original Continue after a cleared sparring stage");
        training.enemies.Clear();Update(app);Key(app,KeyCode.E);training.enemies.Clear();Update(app);Check(training.SparringFinished,"Actual remaining stages finish normally");Key(app,KeyCode.E);Check(!training.practicing && training.mode=="world","E retains original End after completed sparring");
        scenarios++;Prepare(app);training=app.Game;Position(app,new Point(mentor.x,mentor.y-.7f));Check(training.StartSparring(),"Second real sparring fixture starts");Input.keys.Add(KeyCode.E);Input.keys.Add(KeyCode.J);Update(app);Check(training.SparringActive && training.playerAction!=null && training.playerAction.kind=="attack","Unavailable training Continue does not steal original combat input");
        scenarios++;Prepare(app);var ui=app.Game;Key(app,KeyCode.E);Conversation original=ui.conversation;string[] actions=Array.ConvertAll(original.choices,c=>c.action);Call(app,"OnGUI");
        Check(original.choices.Length>=5 && actions[0]=="upgrade" && actions[1]=="bow" && actions[2]=="staff","Existing Noll choice ordering and action identities remain unchanged");
        for(int i=0;i<original.choices.Length;i++) { Check(original.choices[i].action==actions[i],"OnGUI retains every mature dialogue action"); }
        foreach(var button in GUI.buttonCalls)if(Near(button.rect.y,539) || Near(button.rect.y,584))Check(Near(button.rect.height,37),"Original dialogue choice row heights remain 37 pixels");
        float time=ui.save.totalTime,energy=ui.save.energy;string before=JsonUtility.ToJson(ui.save);Call(app,"OnGUI");Call(app,"OnGUI");Check(JsonUtility.ToJson(ui.save)==before && Near(ui.save.totalTime,time) && Near(ui.save.energy,energy),"Repeated actual dialogue OnGUI remains read-only");
        Console.WriteLine("PASS existing world-menu priority, application/focus/checksummed load/travel/review owners, actual sparring Continue/End and unavailable-training combat fallback; mature dialogue action order/control geometry and read-only drawing retained; NATIVE_UNVERIFIED");
    }

    public static int Main()
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-interaction-priority-"+Guid.NewGuid());
        Application.persistentDataPath=directory;var app=new FrontierRuntime();int failures=0;
        try
        {
            Call(app,"Awake");Check(app.Game!=null,"Actual Awake loads the shipped campaign");
            foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{FreshAndQueuedInputYieldsToRealInteraction,PostMovementNearestResourcesPortalsAndInvalidRequests,PaidContactsAndWholeWorldClocksKeepAdvancing,MenuTrainingOwnerBoundariesAndUnchangedControls})
                try { test(app); } catch(Exception error) { failures++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message); }
        }
        finally { app.EndVisualReview();Call(app,"OnDestroy");Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;if(Directory.Exists(directory))Directory.Delete(directory,true); }
        Console.WriteLine("Runtime interaction priority: "+(4-failures)+"/4 groups passed; "+scenarios+" scenarios; NATIVE_UNVERIFIED");return failures==0?0:1;
    }
}
