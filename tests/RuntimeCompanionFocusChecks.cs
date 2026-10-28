// Real Update input, automatic companion actions, projectiles and OnGUI paths.
// Offline Unity declarations do not establish native rendering or play acceptance.
using System;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeCompanionFocusChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static string captureFolder, capturePrefix;
    [Serializable] public sealed class Label { public string k, text; public float[] r; public int size; }
    private static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
    private static bool Near(float a, float b, float tolerance=.003f) { return Math.Abs(a-b)<=tolerance; }
    private static object Call(object obj, string name, params object[] args)
    { var entry=obj.GetType().GetMethod(name,Private|BindingFlags.Public);Check(entry!=null,"Existing production entry point: "+name);return entry.Invoke(obj,args); }
    private static void Set(object obj, string name, object value) { obj.GetType().GetField(name,Private).SetValue(obj,value); }
    private static T Get<T>(object obj, string name) { return (T)obj.GetType().GetField(name,Private).GetValue(obj); }
    private static void Update(FrontierRuntime app) { Call(app,"Update");Input.keys.Clear();Input.buttonDown.Clear(); }
    private static void Frames(FrontierRuntime app, int count) { for(int i=0;i<count;i++)Update(app); }
    private static void Key(FrontierRuntime app, KeyCode key) { Input.keys.Add(key);Update(app); }
    private static void Mouse(FrontierRuntime app, Point world)
    { Point p=(Point)Call(app,"ScreenPoint",world);float scale=Get<float>(app,"scale");Input.mousePosition=new Vector3(Get<float>(app,"offsetX")+p.x*scale,Screen.height-Get<float>(app,"offsetY")-p.y*scale,0); }
    private static void Draw(FrontierRuntime app, string capture=null)
    {
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        if(captureFolder!=null&&capture!=null)
        {
            var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();
            GUI.Export(captureFolder,capturePrefix+"-"+capture);
            GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);
        }
    }
    private static void Prepare(FrontierRuntime app, int companion=1)
    {
        Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;
        Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;Event.current.type=EventType.Repaint;
        app.BeginVisualReview(0,0);app.VisualReviewPaused=false;var g=app.Game;
        g.enemies.Clear();g.effects.Clear();g.projectiles.Clear();g.hazards.Clear();g.fallen.Clear();
        for(int id=0;id<3;id++)if(!g.Has("companion_"+id))g.save.flags.Add("companion_"+id);
        g.save.english=true;g.save.largeText=false;g.save.showGuidance=false;g.save.reducedMotion=true;g.save.effectsQuality=0;
        g.save.health=g.save.maxHealth;g.save.energy=100;g.save.relationships=new[]{3,3,3};g.save.frontierEnding=0;
        g.save.companion=companion;g.companionCooldown=0;g.companionAction=null;g.save.heat=g.save.cold=0;
        g.save.relics=new[]{-1,-1};g.save.seed=417;g.save.totalTime=100;g.invulnerable=100;g.toastTime=0;
        Check(app.SetVisualReviewPosition(new Point(12,14))&&(g.Position-new Point(12,14)).Length<.001f,"Fixture uses exact legal shipped town ground");
        Set(g,"companionAttack",100f);
        Set(app,"camera",g.Position);Set(app,"applicationPaused",false);Set(app,"controllerUsed",false);Set(app,"pointerSeen",false);
        Set(app,"pointerAimActive",false);Set(app,"combatHudTime",0f);Set(app,"resumeTime",0f);Set(app,"arrivalTime",0f);
        Call(app,"Resize");Mouse(app,g.Position+new Point(4,0));Update(app);
    }
    private static Enemy Foe(FrontierRuntime app, Point offset, int id)
    {
        var g=app.Game;Point p=g.Position+offset;var e=new Enemy(id,0,p.x,p.y,500,false){stun=100,timer=9999};
        Check(g.CanStand(p)&&(bool)Call(g,"ClearAttackPath",g.Position,p)&&(bool)Call(g,"ClearAttackPath",g.companionPosition,p),"Fixture actors are visible on real legal ground");
        g.enemies.Add(e);return e;
    }
    private static Enemy[] Targets(FrontierRuntime app)
    {
        // The first and nearest actor is deliberately not any selected target.
        Enemy decoy=Foe(app,new Point(-1.5f,2),98101),east=Foe(app,new Point(4,0),98102),south=Foe(app,new Point(2,3),98103),north=Foe(app,new Point(0,-3),98104);
        return new[]{decoy,east,south,north};
    }
    private static void Acquire(FrontierRuntime app, Enemy enemy, bool pad=false, bool ready=false)
    {
        Mouse(app,enemy.Position);Update(app); // Sample the same aim before pad ownership changes.
        if(ready)Set(app.Game,"companionAttack",0f);
        Key(app,pad?KeyCode.JoystickButton9:KeyCode.V);
        Check(ReferenceEquals(app.Game.CombatFocusTarget,enemy),"Actual "+(pad?"R3":"V")+" acquires the intended live actor");
    }
    private static ActorAction Automatic(FrontierRuntime app, Enemy enemy)
    {
        var g=app.Game;ActorAction action=g.companionAction;
        Check(action!=null&&action.kind=="attack"&&!action.released&&action.elapsed==0,"Actual readiness frame prepares one automatic attack");
        Check(action.target.HasValue&&(action.target.Value-enemy.Position).Length<.001f,"Automatic companion preparation uses the focused actor rather than first/nearest decoy");
        Check((action.direction-(enemy.Position-g.companionPosition).Normalized).Length<.001f,"Automatic companion aim originates at its own feet");
        Check(Near(action.contact,.16f)&&Near(action.duration,.42f)&&Near(action.damage,15)&&action.combo==3&&action.weapon==g.save.companion,"Automatic preparation preserves original timing, bond, damage and companion");
        Check(action.impact!=null&&!action.impact.playerOwned&&action.impact.weapon==(g.save.companion==1?1:2),"Automatic impact retains captured companion ownership/weapon");
        Check(Near(Get<float>(g,"companionAttack"),g.save.companion==1?2.8f:6)&&g.companionCooldown==0&&g.save.energy==100&&g.playerAction==null,"Focus selection preserves original automatic interval and spends no energy/command cooldown");
        return action;
    }
    private static void ActualAcquireAndCycleReadiness(FrontierRuntime app)
    {
        int cases=0;
        for(int companion=0;companion<3;companion++)for(int path=0;path<5;path++)
        {
            Prepare(app,companion);Enemy[] foes=Targets(app);var g=app.Game;int revision=g.revision;Enemy chosen;
            if(path<2){chosen=foes[1];Acquire(app,chosen,path==1,true);}
            else
            {
                Acquire(app,foes[1],path==4);Set(g,"companionAttack",0f);
                if(path==3)Input.held.Add(KeyCode.LeftShift);
                Key(app,path==4?KeyCode.JoystickButton8:KeyCode.X);Input.held.Clear();chosen=foes[path==3?3:2];
                Check(ReferenceEquals(g.CombatFocusTarget,chosen),"Actual X/Shift+X/L3 changes focus before the same readiness Tick");
            }
            Automatic(app,chosen);Check(g.revision==revision,"Selecting automatic support does not change campaign revision");cases++;
        }
        Console.WriteLine("PASS "+cases+" actual V/R3 and same-readiness X/Shift+X/L3 focused automatic preparations across Mira/Ivo/Sen, own-foot aim and original timing/damage/resources; NATIVE_UNVERIFIED");
    }
    private static void PreparedAndFlyingShotsKeepSnapshots(FrontierRuntime app)
    {
        for(int companion=0;companion<3;companion++)
        {
            Prepare(app,companion);Enemy[] foes=Targets(app);var g=app.Game;Enemy target=foes[1];Acquire(app,target,false,true);ActorAction action=Automatic(app,target);
            Point direction=action.direction,point=action.target.Value,feet=g.companionPosition;ImpactProfile impact=action.impact;float hp=target.hp;
            Key(app,KeyCode.X);Check(ReferenceEquals(g.CombatFocusTarget,foes[2]),"Actual X changes live focus during automatic preparation");
            Check(ReferenceEquals(g.companionAction,action)&&(action.direction-direction).Length<.001f&&(action.target.Value-point).Length<.001f&&ReferenceEquals(action.impact,impact),"Cycling preserves prepared automatic direction, target and impact identity");
            for(int frame=0;!action.released&&frame<12;frame++)
            {
                Check(g.projectiles.Count==0&&Near(target.hp,hp),"Automatic wind-up never releases or damages early");
                Check((g.companionPosition-feet).Length<.001f&&(g.companionAim-direction).Length<.001f,"Automatic wind-up retains planted feet and authored companion facing");Update(app);
            }
            Check(action.released&&Near(action.elapsed,.16f)&&g.projectiles.Count==1,"Actual eighth .02s frame releases exactly one original automatic shot");
            Projectile shot=g.projectiles[0];Point velocity=shot.velocity;
            Check((velocity-GameCore.AimFromWeapon(feet,companion==1?1:2,point)*9).Length<.001f&&shot.age==0&&Near(shot.life,3),"Released shot uses the captured point from the actual muzzle at original speed/lifetime");
            Check(shot.friendly&&shot.explosive==(companion==2)&&!shot.piercing&&Near(shot.damage,15)&&ReferenceEquals(shot.impact,impact)&&!shot.impact.playerOwned,"Released automatic projectile preserves type, damage and captured ally ownership");
            Key(app,KeyCode.X);Check((shot.velocity-velocity).Length<.001f&&g.projectiles.Contains(shot),"Cycling live focus after release cannot home the flying shot");
            Draw(app,"automatic-shot-"+companion);
            for(int frame=0;g.projectiles.Contains(shot)&&frame<90;frame++)Update(app);
            Check(!g.projectiles.Contains(shot)&&target.hp<hp&&foes[0].hp==500&&foes[2].hp==500&&foes[3].hp==500,"The real shot contacts its captured actor without redirecting damage to the new focus or decoy");
            Check(g.projectiles.Count==0&&Near(g.save.energy,100)&&g.companionCooldown==0,"Original automatic release emits no extra projectile or resource charge");
        }
        Console.WriteLine("PASS actual three-companion automatic .16s releases, prepared target/direction/facing/impact snapshots, nonhoming flight and real captured-actor damage after cycling; NATIVE_UNVERIFIED");
    }
    private static void HealingAndManualCommandsKeepTheirRules(FrontierRuntime app)
    {
        foreach(bool below in new[]{true,false})
        {
            Prepare(app,0);Enemy[] foes=Targets(app);var g=app.Game;g.save.health=g.save.maxHealth*(below?.79f:.8f);float hp=g.save.health;
            Acquire(app,foes[1],false,true);ActorAction action=g.companionAction;
            Check(action!=null&&action.kind==(below?"heal":"attack"),"Mira heals strictly below 80% and attacks at exactly 80%, despite a valid focus");
            if(below){Frames(app,8);Check(action.released&&Near(g.save.health,Math.Min(g.save.maxHealth,hp+9))&&g.projectiles.Count==0,"Mira retains the original automatic 6+bond heal at .16s");}
        }
        for(int companion=0;companion<3;companion++)
        {
            Prepare(app,companion);Enemy[] foes=Targets(app);var g=app.Game;Acquire(app,foes[1]);float damage=g.Damage;g.save.health=g.save.maxHealth-3;
            Key(app,KeyCode.Z);ActorAction command=g.companionAction;Point target=command.target.Value,direction=command.direction;
            Check(command.kind=="command"&&!command.released&&Near(command.contact,.2f)&&Near(command.duration,.52f)&&Near(g.companionCooldown,18),"Actual Z retains original manual command preparation and cooldown");
            Key(app,KeyCode.X);Check(command.target.HasValue&&(command.target.Value-target).Length<.001f&&(command.direction-direction).Length<.001f,"Manual command retains its captured target and direction through focus cycling");Frames(app,9);
            Check(command.released&&ReferenceEquals(g.companionAction,command)&&Near(g.companionCooldown,17.8f)&&g.LastCompanionReceipt!=null&&g.LastCompanionReceipt.id==companion,"Original command releases once at .2s with its own receipt");
            if(companion==0)Check(Near(g.save.health,g.save.maxHealth)&&g.guardTime>2.99f&&g.projectiles.Count==0,"Mira manual command still heals and guards without an automatic shot");
            else if(companion==1)Check(g.projectiles.Count==3&&g.projectiles.TrueForAll(p=>p.piercing&&Near(p.damage,damage*.95f)&&!p.impact.playerOwned),"Ivo manual command retains three original piercing ally arrows");
            else Check(Get<object>(g,"frost")!=null&&g.LastCompanionReceipt.controlCount==4&&g.projectiles.Count==0,"Sen manual command retains the player-centered field and original visible-enemy control");
        }
        Console.WriteLine("PASS unchanged strict-below-80% Mira heal, exact-threshold attack, actual Z three-companion command clocks/captured intent/heal/guard/volley/field and receipts; NATIVE_UNVERIFIED");
    }
    private static void SelectThroughMenu(FrontierRuntime app, int companion)
    {
        // The ordinary menu owns the original one-second readiness clock.
        // Never manufacture readiness by writing its private countdown here.
        Key(app,KeyCode.C);Check(app.Game.mode=="companions","Actual C opens the companion menu");
        // A fresh invitation starts from no active companion; reconfirming the
        // current companion deliberately preserves its existing readiness now.
        if(app.Game.save.companion==companion)
        {
            Rect alone=new Rect(279,652,938,38);Draw(app);
            Check(GUI.buttonCalls.Exists(b=>b.rect.x==alone.x&&b.rect.y==alone.y&&b.enabled),"Original travel-alone control starts a genuine fresh invitation");
            GUI.queuedClick=alone;Draw(app);Check(!GUI.queuedClick.HasValue&&app.Game.save.companion==-1,"Actual travel-alone selection clears the previous companion");
        }
        Rect invite=new Rect(296+companion*318,536,268,34);Draw(app);
        Check(GUI.buttonCalls.Exists(b=>b.rect.x==invite.x&&b.rect.y==invite.y&&b.enabled),"Existing invite remains available in safe town");
        GUI.queuedClick=invite;Draw(app);Check(!GUI.queuedClick.HasValue&&app.Game.save.companion==companion&&Near(Get<float>(app.Game,"companionAttack"),1),"Actual invite selects the companion with the original one-second initial delay");
        Key(app,KeyCode.Escape);Check(app.Game.mode=="world"&&Near(Get<float>(app.Game,"companionAttack"),1),"Closing the menu does not spend the initial simulation delay");
    }
    private static void OriginalAutomaticClock(FrontierRuntime app, ActorAction first, string kind)
    {
        var g=app.Game;float interval=g.save.companion==1?2.8f:6,energy=g.save.energy;int frames=0;
        Check(first!=null&&first.kind==kind&&!first.released&&first.elapsed==0&&Near(first.contact,.16f)&&Near(first.duration,.42f),"New eligibility prepares the original automatic action without early contact");
        float health=g.save.health;int projectiles=g.projectiles.Count;
        Frames(app,7);frames+=7;Check(!first.released&&Near(first.elapsed,.14f)&&Near(g.save.health,health)&&g.projectiles.Count==projectiles,"Automatic support cannot release before its original .16-second contact");
        Update(app);frames++;Check(first.released&&Near(first.elapsed,.16f),"Original eighth .02-second frame releases exactly once");
        if(kind=="heal")Check(Near(g.save.health,health+9)&&g.projectiles.Count==projectiles,"Mira still restores the original 6 + bond health on actual release");
        else Check(g.projectiles.Count==projectiles+1&&g.projectiles[g.projectiles.Count-1].friendly&&Near(g.projectiles[g.projectiles.Count-1].damage,15),"Original prepared attack releases one ally projectile at its captured damage");
        if(kind=="attack")
        {
            // The first legitimate hit pushes this boundary-range target just
            // beyond seven units. Walk farther in while the prepared ally keeps
            // its captured shot, so the repeated-action clock has a valid target.
            Input.axes["Horizontal"]=1;Input.held.Add(KeyCode.D);
            for(int i=0;i<8;i++){Point before=g.Position;Update(app);frames++;Check(g.CanStand(g.Position)&&(g.Position-before).Length<=g.Speed*.0201f+.001f,"Natural follow-through walking keeps the repeat target available without rewriting actor feet");}
            Input.axes.Clear();Input.held.Clear();
        }
        while((g.companionAction==null||ReferenceEquals(g.companionAction,first))&&frames<330)
        {
            Update(app);frames++;
            if(g.companionAction!=null&&!ReferenceEquals(g.companionAction,first))break;
            Check(frames*.02f<interval+.021f,"An actual action must not consume a second empty-scan interval (frames="+frames+", clock="+Get<float>(g,"companionAttack")+", target distance="+(g.enemies.Count==0?-1:(g.enemies[0].Position-g.companionPosition).Length)+")");
        }
        Check(g.companionAction!=null&&!ReferenceEquals(g.companionAction,first)&&frames*.02f>=interval-.001f&&frames*.02f<=interval+.021f,"The next actual preparation retains the original full "+interval+"-second interval from the first real preparation");
        Check(Near(g.save.energy,energy)&&g.companionCooldown==0&&g.playerAction==null,"Automatic readiness and real repeated actions spend no player energy or command cooldown");
    }
    private static void IdleReadinessFollowsRealEligibility(FrontierRuntime app)
    {
        var failures=new System.Collections.Generic.List<string>();
        for(int companion=0;companion<3;companion++)
        {
            try
            {
                Prepare(app,companion);SelectThroughMenu(app,companion);var g=app.Game;
                Enemy target=Foe(app,new Point(6,0),98400+companion);
                Check((target.Position-g.companionPosition).Length>7,"The existing visible target starts outside companion range");
                Frames(app,60);Check(g.companionAction==null&&g.projectiles.Count==0,"Idle scans have no fake attack, heal or projectile");
                // Application and review pauses must not spend readiness even when idle.
                foreach(bool review in new[]{false,true})
                {
                    float before=Get<float>(g,"companionAttack"),time=g.save.totalTime;
                    if(review)app.VisualReviewPaused=true;else Call(app,"OnApplicationPause",true);
                    Frames(app,8);Draw(app);Check(Get<float>(g,"companionAttack")==before&&g.save.totalTime==time&&g.companionAction==null,"Paused idle readiness and repeated drawing remain simulation-free");
                    if(review)app.VisualReviewPaused=false;else Call(app,"OnApplicationPause",false);
                }
                int walked=0,eligibleFrame=-1,preparedFrame=-1;
                for(int frame=0;frame<360&&g.companionAction==null;frame++)
                {
                    Point before=g.Position;
                    if(eligibleFrame<0){Input.axes["Horizontal"]=1;Input.held.Add(KeyCode.D);walked++;}
                    else {Input.axes.Clear();Input.held.Clear();}
                    Update(app);
                    Check(g.CanStand(g.Position)&&(g.Position-before).Length<=g.Speed*.0201f+.001f,"Real held D walking preserves legal continuous player feet");
                    if(eligibleFrame<0&&(target.Position-g.companionPosition).Length<7&&(bool)Call(g,"ClearAttackPath",g.companionPosition,target.Position))eligibleFrame=frame;
                    if(g.companionAction!=null)preparedFrame=frame;
                }
                Input.axes.Clear();Input.held.Clear();
                Console.WriteLine("TRACE companion "+companion+" naturally walked "+walked+" frames; first eligible frame="+eligibleFrame+", prepared="+preparedFrame+", extra wait="+((preparedFrame-eligibleFrame)*.02f).ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+"s");
                Check(walked>0&&eligibleFrame>=0&&preparedFrame>=eligibleFrame&&preparedFrame-eligibleFrame<=1,"A ready companion must prepare within one Update of first actual visible in-range eligibility, not wait through an empty-scan cooldown");
                ActorAction first=g.companionAction;Check(first.target.HasValue&&(first.target.Value-target.Position).Length<.001f,"Natural approach captures the actual newly eligible actor");OriginalAutomaticClock(app,first,"attack");
            }
            catch(Exception error){failures.Add("companion "+companion+": "+(error.InnerException??error).Message);}
        }
        try
        {
            Prepare(app,0);SelectThroughMenu(app,0);var g=app.Game;Frames(app,60);
            Check(g.companionAction==null&&g.save.health==g.save.maxHealth,"Healthy Mira idles without a fabricated action");
            g.invulnerable=0;g.save.difficulty=1;g.save.talents=new int[3];g.guardTime=0;g.save.mealUntil=0;
            float health=g.save.health;var shot=new Projectile{position=g.Position+new Point(.8f,0),velocity=new Point(-6,0),damage=g.save.maxHealth*.4f,source="shot",visual="enemyArrow"};g.projectiles.Add(shot);
            int hurtFrame=-1,preparedFrame=-1;
            for(int frame=0;frame<340&&g.companionAction==null;frame++)
            {
                Update(app);if(hurtFrame<0&&g.save.health<health)hurtFrame=frame;if(g.companionAction!=null)preparedFrame=frame;
            }
            Console.WriteLine("TRACE Mira real hostile contact frame="+hurtFrame+", prepared="+preparedFrame+", extra wait="+((preparedFrame-hurtFrame)*.02f).ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+"s");
            Check(hurtFrame>=0&&!g.projectiles.Contains(shot)&&Near(g.save.health,health-g.save.maxHealth*.4f)&&g.save.health<g.save.maxHealth*.8f,"A genuine incoming projectile, not a direct health write, makes Mira healing eligible");
            Check(preparedFrame>=hurtFrame&&preparedFrame-hurtFrame<=1,"Idle-ready Mira must prepare within one Update of genuine sub-80% injury rather than an empty six-second scan interval");
            OriginalAutomaticClock(app,g.companionAction,"heal");
        }
        catch(Exception error){failures.Add("Mira injury: "+(error.InnerException??error).Message);}
        Check(failures.Count==0,string.Join(" | ",failures.ToArray()));
        Console.WriteLine("PASS actual C/invite/escape one-second initial ownership, three naturally walked in-range preparations, real incoming-arrow Mira heal, original .16-second contacts/full next-action intervals and idle application/review pauses; NATIVE_UNVERIFIED");
    }
    private static string Simulation(GameCore g)
    {
        return JsonUtility.ToJson(g.save)+"/"+JsonUtility.ToJson(g.companionAction)+"/"+JsonUtility.ToJson(g.projectiles)+"/"+JsonUtility.ToJson(g.companionPosition)+"/"+JsonUtility.ToJson(g.companionAim)+"/"+Get<float>(g,"companionAttack")+"/"+g.companionCooldown+"/"+g.revision;
    }
    private static void PauseAndDrawingStayReadOnly(FrontierRuntime app)
    {
        foreach(bool flying in new[]{false,true})foreach(string boundary in new[]{"draw","inventory","application","review"})
        {
            Prepare(app);Enemy[] foes=Targets(app);var g=app.Game;Acquire(app,foes[1],false,true);ActorAction action=Automatic(app,foes[1]);if(flying)Frames(app,8);
            if(boundary=="inventory")Key(app,KeyCode.I);else if(boundary=="application")Call(app,"OnApplicationPause",true);else if(boundary=="review")app.VisualReviewPaused=true;
            string before=Simulation(g);Enemy focus=g.CombatFocusTarget;
            for(int frame=0;frame<12;frame++)
            {
                Event.current.type=frame%2==0?EventType.Layout:EventType.Repaint;Draw(app);if(boundary!="draw")Update(app);
                Check(Simulation(g)==before&&ReferenceEquals(g.CombatFocusTarget,focus),"Repeated OnGUI and "+boundary+" pause do not advance or rewrite prepared/flying automatic support");
            }
            Event.current.type=EventType.Repaint;
            if(boundary=="inventory")Key(app,KeyCode.Escape);else if(boundary=="application")Call(app,"OnApplicationPause",false);else if(boundary=="review")app.VisualReviewPaused=false;
            float elapsed=action.elapsed,age=g.projectiles.Count==0?0:g.projectiles[0].age;Update(app);
            Check(flying?g.projectiles.Count==1&&g.projectiles[0].age>age:ReferenceEquals(g.companionAction,action)&&action.elapsed>elapsed,"The same automatic action/shot resumes only on simulation time");
        }
        Console.WriteLine("PASS repeated real Layout/Repaint draws plus inventory/application/review pauses preserve full save and automatic timers, action snapshots, companion feet and projectile flight; resumed simulation advances them; NATIVE_UNVERIFIED");
    }
    private static void NotesRemainBilingualAndReadOnly(FrontierRuntime app)
    {
        int cases=0;
        foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true})foreach(int width in new[]{960,1280})for(int id=0;id<3;id++)
        {
            Prepare(app);var g=app.Game;g.save.english=english;g.save.largeText=large;Screen.width=width;Screen.height=width==960?600:800;Call(app,"Resize");Key(app,KeyCode.C);Draw(app);
            Rect notes=new Rect(279+id*318,704,302,25);Check(GUI.buttonCalls.Exists(b=>b.rect.x==notes.x&&b.rect.y==notes.y&&b.enabled),"Existing companion menu exposes actual notes button");GUI.queuedClick=notes;Draw(app);Check(g.mode=="companionnotes"&&!GUI.queuedClick.HasValue,"Actual notes click opens read-only selected companion page");
            string before=Simulation(g);
            foreach(EventType evt in new[]{EventType.Layout,EventType.Repaint})
            {
                Event.current.type=evt;Draw(app);Frames(app,4);Check(Simulation(g)==before,"Reading and drawing notes cannot command, switch, spend or advance companion support");
            }
            Draw(app,"notes-"+id+"-"+(english?"en":"zh")+(large?"-large":"-normal")+"-"+width);
            string command=GUI.commands.Find(c=>c.Contains("\"k\":\"text\"")&&c.Contains("\"r\":[302,424,891,132]"));Check(command!=null,"Notes retain the established explanation bounds above status and receipt");Label label=JsonUtility.FromJson<Label>(command);
            Check(label.size==(large?17:16)&&label.r[1]+label.r[3]<568,"Actual notes preserve normal/large readable font and separate status row");
            Check(english?label.text.Contains("Automatic attacks")&&label.text.Contains("7")&&label.text.Contains("nearest")&&label.text.Contains("Mira"):label.text.Contains("自动攻击")&&label.text.Contains("7")&&label.text.Contains("最近")&&label.text.Contains("米拉"),"Both languages describe focused automatic range/sight, nearest fallback and Mira priority");
            string retained=english?(id==0?"On release, restore health and energy":id==1?"Three arrows released does not mean three hits.":"The field forms at the hero's position on release"):(id==0?"释放时回复生命与耐力":id==1?"射出三箭不等于三箭均命中。":"释放时以主角当时的位置生成领域");
            Check(label.text.Contains(retained),"Appended targeting policy retains the selected companion's original command explanation");Check(Simulation(g)==before,"Capture/export and actual page drawing remain read-only");cases++;
        }
        Event.current.type=EventType.Repaint;
        Console.WriteLine("PASS "+cases+" bilingual normal/large 960/1280 actual companion-note pages, retained command explanation/bounds/font and repeated read-only draws; offline captures do not establish native font fit; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        captureFolder=args.Length>0&&args[0]!="-"?args[0]:null;capturePrefix=args.Length>1?args[1]:"qa-companion-focus";
        string previous=Application.persistentDataPath,temporary=Path.Combine(Path.GetTempPath(),"starfall-companion-focus-save-"+Guid.NewGuid());Application.persistentDataPath=temporary;Directory.CreateDirectory(temporary);var app=new FrontierRuntime();
        try
        {
            Call(app,"Awake");if(app.Game==null){Console.WriteLine("FAIL Awake cannot load shipped campaign");return 1;}
            Action<FrontierRuntime>[] tests={ActualAcquireAndCycleReadiness,PreparedAndFlyingShotsKeepSnapshots,HealingAndManualCommandsKeepTheirRules,PauseAndDrawingStayReadOnly,NotesRemainBilingualAndReadOnly,IdleReadinessFollowsRealEligibility};int failures=0;
            foreach(var test in tests)try{test(app);}catch(Exception error){failures++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}
            Console.WriteLine("Runtime companion focus: "+(tests.Length-failures)+"/"+tests.Length+" groups passed; NATIVE_UNVERIFIED");return failures==0?0:1;
        }
        finally{app.EndVisualReview();Call(app,"OnDestroy");Application.persistentDataPath=previous;if(Directory.Exists(temporary))Directory.Delete(temporary,true);}
    }
}
