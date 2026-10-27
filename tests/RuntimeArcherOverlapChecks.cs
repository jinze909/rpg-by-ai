// Real Update, OnGUI, paid input, projectile contact and SaveStore integration.
// These are offline production-path checks; native/GPU/gameplay is unverified.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Starfall;
using UnityEngine;

public static class RuntimeArcherOverlapChecks
{
    const BindingFlags All=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    static string folder,prefix="qa-archer-overlap";static int captures;
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static bool Near(float a,float b,float tolerance=.00005f){return Math.Abs(a-b)<=tolerance;}
    static bool Same(Point a,Point b,float tolerance=.00005f){return(a-b).Length<=tolerance;}
    static float Dot(Point a,Point b){return a.x*b.x+a.y*b.y;}
    static object Call(object target,string name,params object[] args){var method=target.GetType().GetMethod(name,All);Check(method!=null,"Existing production entry: "+name);return method.Invoke(target,args);}
    static object Helper(string name,params object[] args){return typeof(RuntimeArcherEvasionChecks).GetMethod(name,All).Invoke(null,args);}
    static void Set(object target,string name,object value){var field=target.GetType().GetField(name,All);Check(field!=null,"Existing runtime fixture field: "+name);field.SetValue(target,value);}
    static T Get<T>(object target,string name){return(T)target.GetType().GetField(name,All).GetValue(target);}
    static void Update(FrontierRuntime app){Call(app,"Update");Input.Clear();}
    static void Key(FrontierRuntime app,KeyCode key){Input.keys.Add(key);Update(app);}
    static void Move(Point direction)
    {
        Input.Clear();Input.axes["Horizontal"]=direction.x;Input.axes["Vertical"]=-direction.y;
        if(direction.x<-.05f)Input.held.Add(KeyCode.A);if(direction.x>.05f)Input.held.Add(KeyCode.D);if(direction.y<-.05f)Input.held.Add(KeyCode.W);if(direction.y>.05f)Input.held.Add(KeyCode.S);
    }
    static void Draw(FrontierRuntime app,string capture=null)
    {
        GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();Call(app,"OnGUI");
        if(folder!=null&&capture!=null){var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();GUI.Export(folder,prefix+"-"+capture);captures++;GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);}
    }
    static Enemy Prepare(FrontierRuntime app,Point foot,Point? player=null,Point? facing=null,int room=1,float dt=.02f)
    {
        Enemy enemy=(Enemy)Helper("Prepare",app,false,(Point?)new Point(12,14),(Point?)new Point(12,14));var game=app.Game;
        if(room!=1){game.Enter(room);game.enemies.Clear();game.enemies.Add(enemy);}
        Point hero=player??foot;Check(game.CanStand(foot)&&game.CanStand(hero),"Exact shipped terrain supports complete player/archer feet at "+foot.x+","+foot.y);
        Check(app.SetVisualReviewPosition(hero)&&Same(game.Position,hero),"Review staging preserves the exact legal player feet");enemy.x=foot.x;enemy.y=foot.y;enemy.state=0;enemy.timer=10;enemy.hitTime=enemy.stun=enemy.releaseTime=enemy.flash=enemy.stagger=enemy.staggerRecovery=enemy.moveTime=0;enemy.recoil=new Point();enemy.moving=enemy.backpedaling=false;enemy.Face(facing??new Point(1,0));
        game.save.coins=313;game.save.xp=9;game.save.shards=17;game.save.flags.Add("archer_overlap_mature_story");game.hitStop=0;game.invulnerable=0;game.playerHitTime=0;Call(game,"CancelAction");game.attackCooldown=game.skillCooldown=game.dashCooldown=game.dashTime=0;
        Time.unscaledDeltaTime=dt;Set(app,"camera",foot);Set(app,"clock",0f);Set(app,"saveTimer",-1000f);Set(app,"lastRevision",game.revision);Call(app,"Resize");return enemy;
    }
    static Point RetreatFrame(FrontierRuntime app,Enemy enemy,Point expectedAway,bool exactBudget=true)
    {
        Point before=enemy.Position;float gait=enemy.moveTime;Update(app);Point step=enemy.Position-before;float budget=Time.unscaledDeltaTime*1.6f;
        // Semantic RED assertion uses only the pre-existing production model.
        Check(step.Length>budget*.99f,"A live idle archer must leave exact/sub-deadband player overlap instead of remaining motionless");
        Check(step.Length<=budget+.00005f&&(!exactBudget||Near(step.Length,budget)),"Overlap recovery keeps the original 1.6-unit movement budget");
        Check((bool)Call(app.Game,"Fits",enemy.Position)&&app.Game.CanStand(app.Game.Position),"Overlap recovery sweeps the unchanged complete actor/player footprints");
        Check(Dot(step,expectedAway)>budget*.98f,"Overlap retreat uses the preserved facing only at zero, and the real delta whenever separation is nonzero");
        Check(enemy.moving&&enemy.backpedaling&&Near(enemy.moveTime-gait,step.Length),"Real displacement owns moving/backpedal state and exact distance-driven gait");
        Point toward=app.Game.Position-enemy.Position;Check(Dot(enemy.facing,toward.Normalized)>.999f,"Retreating bow keeps facing the player after actual movement");Helper("Gait",app,enemy);return step;
    }
    static void SemanticGuard(FrontierRuntime app){var enemy=Prepare(app,new Point(12,14));RetreatFrame(app,enemy,new Point(-1,0));}
    static string Foot(Enemy enemy){return enemy.x.ToString("R",CultureInfo.InvariantCulture)+","+enemy.y.ToString("R",CultureInfo.InvariantCulture);}
    static string PauseState(GameCore game){return(string)Helper("PausedState",game);}
    static string State(GameCore game){return(string)Helper("State",game);}
    static string Files(){var paths=Directory.GetFiles(Application.persistentDataPath,"*",SearchOption.AllDirectories);Array.Sort(paths,StringComparer.Ordinal);var text=new StringBuilder();foreach(string path in paths)text.Append(path).Append(':').Append(Convert.ToBase64String(File.ReadAllBytes(path))).Append(';');return text.ToString();}

    static void RealWalkIntoReleaseAndTinyDeltaGait(FrontierRuntime app)
    {
        SemanticGuard(app);
        Point[] directions={new Point(1,0),new Point(-1,0),new Point(0,1),new Point(0,-1)};string[] names={"east","west","south","north"};
        for(int index=0;index<directions.Length;index++)
        {
            Point facing=directions[index],origin=new Point(12,14);Enemy enemy=Prepare(app,origin,facing:facing);var game=app.Game;float dt=Time.unscaledDeltaTime;Point start=origin+facing*(game.Speed*dt);
            Check(app.SetVisualReviewPosition(start)&&Same(game.Position,start),"One ordinary movement step begins at exact original speed distance");enemy.releaseTime=.03f;Point initial=game.Position;Move(facing*-1);Update(app);
            Check((game.Position-origin).Length<.000001f&&(game.Position-initial).Length>.05f,"Real held WASD reaches identical legal player/archer feet during release");Check(Same(enemy.Position,origin)&&enemy.releaseTime>0&&!enemy.moving&&!enemy.backpedaling&&Near(enemy.moveTime,0),"Original short release owns the stationary actor until its remaining clock expires");Draw(app,names[index]+"-overlap-release");Check((int)Call(app,"EnemyPose",enemy)==3,"Actual OnGUI retains original release pose at overlap");
            RetreatFrame(app,enemy,facing*-1);Check(enemy.releaseTime<=0&&enemy.state==0,"Release boundary resumes ordinary idle retreat without new attack state");for(int frame=0;frame<9;frame++)Update(app);Helper("Gait",app,enemy);Draw(app,names[index]+"-retreat-walk");Check((enemy.Position-game.Position).Length>.3f,"Retreat remains sustained after first nonzero recovery, instead of a cosmetic pose change");
        }
        foreach(Point delta in new[]{new Point(.00005f,0),new Point(-.00005f,0),new Point(0,.00005f),new Point(0,-.00005f)})
        {
            Point origin=new Point(12,14),unit=delta*(1f/delta.Length);Enemy enemy=Prepare(app,origin,origin+delta,new Point(unit.y,-unit.x));RetreatFrame(app,enemy,unit*-1);Check((app.Game.Position-origin).Length>0&&(app.Game.Position-origin).Length<=.0001f,"Tiny-control fixture genuinely remains inside Point.Normalized's old deadband");
        }
        Check(new Point(.00005f,0).Normalized.Length==0,"Global Point.Normalized retains its original deadband; only retreat resolves tiny nonzero deltas");
        Console.WriteLine("PASS actual four-direction held-WASD contact during short release, exact overlap exit, sustained original-speed player-facing reversed walk sprites, and signed tiny-delta controls without global normalization change; NATIVE_UNVERIFIED");
    }
    static void CommittedArrowsPaidInputsAndExistingStops(FrontierRuntime app)
    {
        SemanticGuard(app);
        Enemy archer=Prepare(app,new Point(12,14),new Point(14,14));var game=app.Game;archer.timer=0;Update(app);Check(archer.state==1&&Near(archer.timer,.65f)&&Near(archer.windupDuration,.65f)&&game.projectiles.Count==0,"Original idle transition captures a .65-second arrow windup");Point origin=archer.Position,target=archer.target,face=archer.facing;Set(app,"camera",new Point(12,14));string geometry=(string)Helper("WarningGeometry",app,archer);float health=game.save.health;
        for(int frame=0;frame<32;frame++)
        {
            if(frame<6)Move(new Point(0,-1));else if(frame<12)Move(new Point(0,1));Update(app);Set(app,"camera",new Point(12,14));Check(archer.state==1&&Same(archer.Position,origin)&&Same(archer.target,target)&&Same(archer.facing,face)&&!archer.moving&&!archer.backpedaling&&game.projectiles.Count==0,"Committed windup cannot retreat, recapture a sidestepping player or fire early");Check(Near(archer.timer,.65f-(frame+1)*.02f,.002f)&&(string)Helper("WarningGeometry",app,archer)==geometry,"Captured warning ray and original .65-second clock remain exact through real movement");
        }
        Draw(app,"committed-warning");Check((int)Call(app,"EnemyPose",archer)==2&&Same(game.Position,target),"Actual warning pose remains and actual sidestep/return restores captured lane");Update(app);Check(archer.state==0&&Near(archer.releaseTime,.24f)&&Near(archer.timer,1.4f)&&game.projectiles.Count==1&&Same(archer.Position,origin),"Only original windup boundary releases once with unchanged .24/1.4-second clocks");Projectile shot=game.projectiles[0];Check(!shot.friendly&&shot.visual=="enemyArrow"&&Near(shot.damage,10)&&Near(shot.velocity.Length,5),"Released arrow preserves hostile identity, speed five and damage ten");Point expected=(target-GameCore.WeaponOrigin(origin,1,face)).Normalized;Check(Same(shot.velocity.Normalized,expected),"Actual shot keeps captured muzzle-to-target heading");Draw(app,"committed-release");Check((int)Call(app,"EnemyPose",archer)==3,"Actual OnGUI retains release sprite");int steps=0;while(game.projectiles.Contains(shot)&&steps++<60)Update(app);Check(steps<60&&!game.projectiles.Contains(shot)&&Near(health-game.save.health,10)&&game.effects.Exists(effect=>effect.kind=="projectileImpact"),"Actual projectile contact still loses ten health exactly once and retires normally");
        foreach(bool skill in new[]{false,true})
        {
            Enemy enemy=Prepare(app,new Point(12,14));game=app.Game;enemy.hp=enemy.maxHp=1000;if(!skill)enemy.stagger=29.9f;RetreatFrame(app,enemy,new Point(-1,0));int approach=0;while((enemy.Position-game.Position).Length<.9f&&approach++<50)Update(app);Check((enemy.Position-game.Position).Length>=.9f&&enemy.backpedaling,"Real retreat first gives the original forward sword step room to contact from its front arc");float hp=enemy.hp,cost=skill?game.CurrentSkillEnergyCost:game.Profile(game.save.weapon,game.Style).energy;int coins=game.save.coins,xp=game.save.xp;string stock=string.Join(",",game.save.inventory);Helper("Mouse",app,enemy.Position+new Point(-2,0));Key(app,skill?KeyCode.Q:KeyCode.J);Check(game.playerAction!=null&&game.playerAction.kind==(skill?"skill":"attack")&&!game.playerAction.released&&Near(game.save.energy,100-cost),"Real J/Q pays original profile cost and begins its unchanged preparation");int frames=0;while(enemy.hp==hp&&frames++<60)Update(app);Check(frames<60&&enemy.hp<hp&&enemy.hitTime>0&&enemy.stun>1,"Paid "+(skill?"Q":"J")+" contact still interrupts the living retreating archer; frames="+frames+", hp="+enemy.hp+", hit="+enemy.hitTime+", stun="+enemy.stun);float gait=enemy.moveTime;for(int frame=0;frame<15;frame++){Update(app);Check(!enemy.moving&&!enemy.backpedaling&&Near(enemy.moveTime,gait)&&(bool)Call(game,"Fits",enemy.Position),"Existing real hit/stun suppresses evasion and gait while recoil remains swept");}Check(game.save.coins==coins&&game.save.xp==xp&&string.Join(",",game.save.inventory)==stock,"Nonlethal overlap/player control creates no loot or progression");
        }
        foreach(string exclusion in new[]{"hit","stun","release","windup","boss","hunter","heavy"})
        {
            Enemy enemy=Prepare(app,new Point(12,14));game=app.Game;if(exclusion=="hit")enemy.hitTime=.2f;else if(exclusion=="stun")enemy.stun=.2f;else if(exclusion=="release")enemy.releaseTime=.2f;else if(exclusion=="windup"){enemy.state=1;enemy.timer=.3f;enemy.windupDuration=.65f;enemy.target=game.Position;enemy.attackOrigin=enemy.attackDestination=enemy.Position;}else if(exclusion=="boss")enemy.boss=true;else if(exclusion=="hunter")enemy.kind=0;else enemy.kind=2;Point foot=enemy.Position;Update(app);Check(Same(enemy.Position,foot)&&!enemy.moving&&!enemy.backpedaling&&Near(enemy.moveTime,0),"Existing "+exclusion+" branch does not receive the ordinary idle archer escape");Draw(app);
        }
        Console.WriteLine("PASS real fixed .65s captured warning/release/5-speed/10-damage arrow contact, original paid J/Q interruption and hit/stun/release/windup/boss/other-kind movement exclusions; NATIVE_UNVERIFIED");
    }
    static string CollisionTrace(FrontierRuntime app,int room,Point foot,Point facing,float dt,bool capture,string name)
    {
        Enemy enemy=Prepare(app,foot,facing:facing,room:room,dt:dt);var game=app.Game;Point straight=(Point)Call(game,"SweepActor",foot,facing*(-dt*1.6f));Check((straight-foot).Length<dt*1.6f-.00001f,"Authored wall/boundary/house really clips the fallback's straight step");int seed=game.save.seed;var text=new StringBuilder();int frames=(int)Math.Ceiling(1.5f/dt);
        for(int frame=0;frame<frames;frame++)
        {
            Point before=enemy.Position;float gait=enemy.moveTime,distance=(before-game.Position).Length;Update(app);Point step=enemy.Position-before;Check((bool)Call(game,"Fits",enemy.Position)&&game.CanStand(game.Position)&&step.Length<=dt*1.6f+.0001f,"Every live collision recovery step retains full swept feet and original speed budget");Check((enemy.Position-game.Position).Length>=distance-.00001f&&Near(enemy.moveTime-gait,step.Length),"Collision fallback cannot reduce separation or manufacture gait distance");if(frame==0)Check(step.Length>dt*1.6f*.9f&&enemy.moving&&enemy.backpedaling,"Exact overlap beside collision must find the existing legal lateral retreat");if(step.Length>.001f)Check(enemy.moving&&enemy.backpedaling,"Visible collision recovery cannot slide in an idle pose");text.Append(Foot(enemy)).Append(';');
        }
        Check((enemy.Position-game.Position).Length>1.7f&&game.save.seed==seed,"Real sustained collision escape reaches useful separation without consuming randomness");if(enemy.moving)Helper("Gait",app,enemy);if(capture)Draw(app,name);return text.ToString();
    }
    static void CollisionFramerateAndDeterministicRecovery(FrontierRuntime app)
    {
        SemanticGuard(app);
        Point[] feet={new Point(11.225f,9.5f),new Point(8.775f,9.5f),new Point(2.225f,14),new Point(4.975f,5)};Point[] faces={new Point(1,0),new Point(-1,0),new Point(1,0),new Point(-1,0)};
        for(int fixture=0;fixture<feet.Length;fixture++)foreach(float dt in new[]{.005f,.02f,.05f})
        {
            int room=fixture==3?0:1;string first=CollisionTrace(app,room,feet[fixture],faces[fixture],dt,dt==.02f&&(fixture==0||fixture==3),fixture==0?"wall-tangent":"house-tangent");string second=CollisionTrace(app,room,feet[fixture],faces[fixture],dt,false,"");Check(first==second,"Same authored collision/facing/dt yields identical per-frame feet, without random overlap escape");
        }
        foreach(float dt in new[]{.005f,.02f,.05f})foreach(Point facing in new[]{new Point(1,0),new Point(0,-1)})
        {
            Enemy enemy=Prepare(app,new Point(12,14),facing:facing,dt:dt);var game=app.Game;float clock=enemy.moveTime;int frames=(int)Math.Round(1/dt);for(int frame=0;frame<frames;frame++)Update(app);Check(Near((enemy.Position-game.Position).Length,1.6f,.001f)&&Near(enemy.moveTime-clock,1.6f,.001f),"One second of clear-ground real Update recovery preserves 1.6-unit distance across frame rates");
        }
        Console.WriteLine("PASS actual .005/.02/.05s Update recovery beside both shipped wall faces, map boundary and house; swept collision, nondecreasing separation, exact gait, repeated deterministic traces and unchanged clear-ground speed; NATIVE_UNVERIFIED");
    }
    static void SavesPauseTrainingAndWorldOwners(FrontierRuntime app)
    {
        SemanticGuard(app);
        Enemy original=Prepare(app,new Point(12,14));var game=app.Game;original.hp=31.25f;original.releaseTime=.15f;game.PrepareSave();Check(Same(original.Position,game.Position),"Persistence fixture intentionally contains legal overlapping feet");Check(SaveStore.Write(game.save,0),"Actual checksum writer saves the overlapping encounter");string warning;SaveData saved=SaveStore.Read(0,out warning);Check(saved!=null&&warning==null&&saved.worldEnemies.Length==1,"Actual checksum reader restores one owned encounter record");string raw=JsonUtility.ToJson(saved);Check(typeof(EnemyRecord).GetFields().Length==7&&!raw.Contains("backpedaling")&&!raw.Contains("moveTime")&&!raw.Contains("retreat"),"Original seven-field enemy schema receives no overlap/gait/fallback state");
        foreach(string format in new[]{"checksum","v1","v2"})
        {
            SaveData loaded;if(format=="checksum")loaded=SaveStore.Read(0,out warning);else{var legacy=JsonUtility.FromJson<SaveData>(raw);legacy.version=format=="v1"?1:2;File.WriteAllText(SaveStore.PathFor(1),JsonUtility.ToJson(legacy));loaded=SaveStore.Read(1,out warning);}Check(loaded!=null&&warning==null,"Original raw/checksum SaveStore read accepts "+format);Enemy stale=game.enemies[0];Point oldFoot=stale.Position;game.Load(loaded);game.Close();Enemy current=game.enemies.Find(enemy=>enemy.id==original.id);Check(current!=null&&!ReferenceEquals(current,stale)&&current.kind==1&&!current.boss&&Same(current.Position,game.Position)&&Near(current.hp,31.25f),"Actual "+format+" load preserves legitimate overlap, identity and partial HP instead of teleporting either body");Check(current.state==0&&current.releaseTime==0&&current.moveTime==0&&!current.moving&&!current.backpedaling&&current.timer>0,"Load retains original reset policy for transient warning/release/gait state");Set(app,"camera",new Point(12,14));RetreatFrame(app,current,new Point(-1,0));Check(Same(stale.Position,oldFoot)&&game.Has("archer_overlap_mature_story")&&game.save.coins==313&&game.save.xp==9&&game.save.shards==17,"Fresh restored actor alone moves while old owner and mature economy/story remain intact");
        }
        foreach(string pause in new[]{"menu","application","focus","review"})
        {
            Enemy enemy=Prepare(app,new Point(12,14));game=app.Game;if(pause=="menu")Key(app,KeyCode.Escape);else if(pause=="application")Call(app,"OnApplicationPause",true);else if(pause=="focus")Call(app,"OnApplicationFocus",false);else app.VisualReviewPaused=true;string state=PauseState(game),files=Files();for(int frame=0;frame<6;frame++){Update(app);Draw(app);Check(PauseState(game)==state&&Files()==files,"Actual "+pause+" pause freezes every owned enemy/action/save field and disk while bodies overlap");}if(pause=="application")Call(app,"OnApplicationPause",false);else if(pause=="review")app.VisualReviewPaused=false;else game.Close();RetreatFrame(app,enemy,(game.Position-enemy.Position).Length>0?(enemy.Position-game.Position).Normalized:new Point(-1,0));
        }
        Enemy retired=Prepare(app,new Point(12,14));game=app.Game;Point retiredFoot=retired.Position;game.Enter(0);Update(app);Check(!game.enemies.Contains(retired)&&Same(retired.Position,retiredFoot),"Original world transition retires the old overlapping archer reference");game.StartPractice();Check(game.practicing&&game.enemies.Count==1,"Actual town practice creates its own original training actor");Enemy dummy=game.enemies[0];dummy.kind=1;dummy.x=game.save.x;dummy.y=game.save.y;dummy.timer=10;dummy.Face(new Point(1,0));Point trainingFoot=dummy.Position;for(int frame=0;frame<5;frame++)Update(app);Check(Same(dummy.Position,trainingFoot)&&!dummy.moving&&!dummy.backpedaling&&dummy.moveTime==0,"Classic safe training's existing non-evasive rule retains priority even for an archer-shaped overlap fixture");game.EndPractice();Check(!game.practicing&&!game.enemies.Contains(dummy)&&Same(retired.Position,retiredFoot),"Actual practice exit cannot reanimate stale encounter or training actors");app.EndVisualReview();app.BeginVisualReview(0,1);app.VisualReviewPaused=false;Update(app);Check(!app.Game.enemies.Contains(retired)&&Same(retired.Position,retiredFoot),"Replacing the actual review/Game owner cannot dispatch recovery into a retired actor");
        Enemy pure=Prepare(app,new Point(12,14));RetreatFrame(app,pure,new Point(-1,0));Draw(app);game=app.Game;string all=State(game),disk=Files();foreach(EventType type in new[]{EventType.Layout,EventType.Repaint}){Event.current.type=type;Draw(app);Check(State(game)==all&&Files()==disk,"Actual actor/HUD OnGUI drawing cannot advance recovery, save or change the encounter");}Event.current.type=EventType.Repaint;
        Console.WriteLine("PASS actual checksum/raw-v1/v2 overlapping enemy restoration with original schema/HP/reset policy, pause/resume, ordinary world/review ownership, safe-practice exclusion/exit and complete OnGUI encounter/save purity; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,path=Path.Combine(Path.GetTempPath(),"starfall-archer-overlap-"+Guid.NewGuid());Application.persistentDataPath=path;folder=args.Length>0&&args[0]!="-"?args[0]:null;if(args.Length>1)prefix=args[1];if(folder!=null)Directory.CreateDirectory(folder);Directory.CreateDirectory(path);var app=new FrontierRuntime();int failed=0;
        try{Call(app,"Awake");Check(app.Game!=null,"Actual Awake loads complete shipped campaign");foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{RealWalkIntoReleaseAndTinyDeltaGait,CommittedArrowsPaidInputsAndExistingStops,CollisionFramerateAndDeterministicRecovery,SavesPauseTrainingAndWorldOwners})try{test(app);}catch(Exception error){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error));}}
        finally{app.EndVisualReview();Call(app,"OnDestroy");Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;if(Directory.Exists(path))Directory.Delete(path,true);}
        if(folder!=null&&failed==0&&captures!=12){failed++;Console.WriteLine("FAIL Captures: expected twelve directional/release/wall/house production OnGUI captures");}Console.WriteLine("Runtime archer overlap: "+(4-failed)+"/4 groups passed; captures="+captures+"; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
