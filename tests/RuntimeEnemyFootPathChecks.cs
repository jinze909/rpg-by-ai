// Shipped FrontierRuntime.Update/input, OnGUI and SaveStore with offline Unity
// declarations. This is not native import, rendering, hardware input or play QA.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Starfall;
using UnityEngine;

public static class RuntimeEnemyFootPathChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly Point Goal=new Point(17,5.1f);
    private static float frameTime=1f/60;
    private static int checkedFrames;
    private static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
    private static bool Near(float a,float b,float tolerance=.001f){return Math.Abs(a-b)<=tolerance;}
    private static bool Same(Point a,Point b){return (a-b).Length<.001f;}
    private static string Number(float value){return value.ToString("R",CultureInfo.InvariantCulture);}
    private static object Call(object value,string name,params object[] args)
    {var method=value.GetType().GetMethod(name,Private|BindingFlags.Public);Check(method!=null,"Existing production entry point: "+name);return method.Invoke(value,args);}
    private static T Get<T>(object value,string name){return (T)value.GetType().GetField(name,Private).GetValue(value);}
    private static void Set(object value,string name,object field){value.GetType().GetField(name,Private).SetValue(value,field);}
    private static void ClearDraw(){GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();}
    private static void Draw(FrontierRuntime app){ClearDraw();Call(app,"OnGUI");}
    private static void Motion(Point direction,bool pad=false)
    {
        Input.held.Clear();Input.axes["Horizontal"]=direction.x;Input.axes["Vertical"]=-direction.y;
        if(!pad){if(direction.x>.1f)Input.held.Add(KeyCode.D);if(direction.x<-.1f)Input.held.Add(KeyCode.A);if(direction.y>.1f)Input.held.Add(KeyCode.S);if(direction.y<-.1f)Input.held.Add(KeyCode.W);}
    }
    private static void Mouse(FrontierRuntime app,Point world)
    {Point screen=(Point)Call(app,"ScreenPoint",world);float scale=Get<float>(app,"scale");Input.mousePosition=new Vector3(Get<float>(app,"offsetX")+screen.x*scale,Screen.height-Get<float>(app,"offsetY")-screen.y*scale,0);}
    private static void Update(FrontierRuntime app)
    {
        var g=app.Game;bool tide=g.LowTide;var actors=g.enemies.ToArray();var feet=new Point[actors.Length];var gait=new float[actors.Length];
        for(int i=0;i<actors.Length;i++){feet[i]=actors[i].Position;gait[i]=actors[i].moveTime;}
        Call(app,"Update");Input.keys.Clear();Input.buttonDown.Clear();
        Check(g.CanStand(g.Position),"Every actual Update preserves the player's legal complete foot");
        for(int i=0;i<actors.Length;i++)if(g.enemies.Contains(actors[i]))
        {
            Enemy enemy=actors[i];Point start=feet[i];
            // Existing high tide repairs only now-invalid feet before AI.
            // That authored recovery is not a pathfinding step or gait input.
            if(g.save.room==5&&tide!=g.LowTide&&!g.CanStand(start))start=(Point)Call(g,"SafeActorFoot",start);
            float step=(enemy.Position-start).Length;
            Check(g.CanStand(enemy.Position),"Every authored actor retains legal complete feet, id="+enemy.id);
            if(!enemy.boss&&enemy.hitTime<=0)
            {
                float speed=enemy.kind==0?2.1f:enemy.backpedaling?1.6f:1.5f;
                Check(step<=speed*Time.unscaledDeltaTime+.0005f,"Actual ordinary pursuit/archer retreat keeps its original swept speed, id="+enemy.id+" step="+Number(step));
                Check(Near(enemy.moveTime-gait[i],step>.001f?step:0,.0005f),"Enemy gait advances only for the original >.001 travelled-distance threshold, id="+enemy.id+" step="+Number(step)+" gait="+Number(enemy.moveTime-gait[i]));
                if(step>.001f)Check(enemy.moving,"Actual travelled enemy feet own the existing moving flag, id="+enemy.id);
            }
        }
        checkedFrames++;
    }
    private static void Key(FrontierRuntime app,KeyCode key){Input.keys.Add(key);Update(app);}
    private static void Prepare(FrontierRuntime app,float dt=1f/60)
    {
        Input.Clear();GUI.queuedClick=null;ClearDraw();GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;
        Screen.width=1280;Screen.height=800;frameTime=dt;Time.unscaledDeltaTime=dt;Event.current.type=EventType.Repaint;
        app.BeginVisualReview(0,0);app.VisualReviewPaused=false;var g=app.Game;
        // Load a minimally progressed, ordinary save instead of the review
        // lab's unlocked campaign. Enter supplies all seven authored actors.
        var save=new SaveData{stage=1,health=100,english=true,showGuidance=false,reducedMotion=true,effectsQuality=0};
        save.flags.Add("intro");save.flags.Add("exp_intro");g.Load(save);g.Enter(5);
        g.invulnerable=0;g.toastTime=0; // No test protection, actor relocation, cooldown reset or enemy pruning.
        Check(g.mode=="world"&&g.save.room==5&&Same(g.Position,new Point(5,14))&&g.enemies.Count==7,"Real stage-one Enter(5) supplies the original player spawn and all seven authored foes");
        Check(g.save.companion==-1&&g.save.flags.Count==2&&Near(g.save.health,100),"Natural reproduction has no ally, bonus story unlocks or injected health/protection");
        for(int i=0;i<7;i++)Check(g.enemies[i].id==i+1&&!g.enemies[i].boss&&Near(g.enemies[i].timer,1.3f+(i+1)*.13f)&&Get<bool>(g.enemies[i],"returnEnabled"),"Original regional placement, identity and initial attack clock remain intact");
        Set(app,"camera",g.Position);Set(app,"applicationPaused",false);Set(app,"controllerUsed",false);Set(app,"pointerSeen",false);Set(app,"pointerAimActive",false);Set(app,"arrivalTime",0f);Set(app,"resumeTime",0f);Set(app,"combatHudTime",0f);Call(app,"Resize");Call(app,"ResetControllerAim",false);Mouse(app,g.Position+new Point(4,0));
    }
    private static void WalkTo(FrontierRuntime app,Point goal,bool pad=false)
    {
        var g=app.Game;int frames=0;
        while((goal-g.Position).Length>.0005f&&frames++<1200)
        {
            Point before=g.Position,delta=goal-before;Motion(delta.Normalized,pad);Time.unscaledDeltaTime=Math.Min(frameTime,delta.Length/g.Speed);Update(app);
            Check((g.Position-before).Length<=g.Speed*Time.unscaledDeltaTime+.0005f&&g.playerAction==null&&g.dashTime<=0,"Real held walking reaches the route without attacks, dashes or teleports");
        }
        Input.Clear();Time.unscaledDeltaTime=frameTime;Check(frames<1200&&Same(g.Position,goal),"Real held input reaches authored route waypoint "+Number(goal.x)+","+Number(goal.y));
    }
    private static Enemy Route(FrontierRuntime app,bool pad=false)
    {
        foreach(Point waypoint in new[]{new Point(14,14),new Point(14,8),new Point(17,8),new Point(20,8),new Point(20,5.1f),Goal})WalkTo(app,waypoint,pad);
        var g=app.Game;Check(Same(g.Position,Goal)&&g.CanStand(Goal)&&!g.CanStand(new Point(17.5f,5.5f)),"The exact unchanged player foot is legal although its house-cell center is blocked");
        Check(g.enemies.Count==7&&Near(g.save.health,100)&&g.playerAction==null&&g.projectiles.TrueForAll(p=>!p.friendly),"Natural route preserves all seven foes, original 100 health and hostile-only combat");return g.enemies.Find(e=>e.id==1);
    }
    private static void ReachMelee(FrontierRuntime app,Enemy hunter,string label)
    {
        var g=app.Game;Input.Clear();Time.unscaledDeltaTime=frameTime;Point initial=Goal,origin=new Point(),target=new Point(),facing=new Point();
        bool detoured=false,warning=false,contact=false;int frames=0;float travelled=0;
        while(frames<(int)Math.Ceiling(20/frameTime)&&!contact&&!g.Dying)
        {
            frames++;
            Point before=hunter.Position;float health=g.save.health,immunity=g.invulnerable;int state=hunter.state;float remaining=hunter.timer;
            bool canContact=state==1&&remaining<=frameTime+.00001f&&GameCore.InEnemyMeleeArc(hunter,g.Position)&&(bool)Call(g,"ClearAttackPath",hunter.Position,g.Position)&&immunity<=frameTime;
            Update(app);travelled+=(hunter.Position-before).Length;detoured|=(hunter.x<16.1f||hunter.x>18.9f)&&hunter.y<6;
            if(health>=100)Check(Same(g.Position,initial),"Idle input never relocates the exact player goal before genuine enemy damage");
            if(hunter.state==1)
            {
                if(state!=1){warning=true;origin=hunter.Position;target=hunter.target;facing=hunter.facing;Check(Near(hunter.timer,.65f)&&Near(hunter.windupDuration,.65f),"Reacquired hunter keeps its original .65-second warning");}
                else if(warning)Check(Same(hunter.Position,origin)&&Same(hunter.target,target)&&Same(hunter.facing,facing),"Exact-foot navigation cannot move or retarget a committed melee warning");
            }
            if(canContact&&hunter.state==0&&Near(health-g.save.health,11))
            {
                contact=true;Check(Near(hunter.releaseTime,.24f)&&Near(hunter.timer,1.4f),"Honest detour ends in original eleven-damage melee with .24 release and 1.4 recovery");
            }
        }
        Console.WriteLine("ROUTE "+label+" idle_frames="+frames+" hunter="+Number(hunter.x)+","+Number(hunter.y)+" gap="+Number((hunter.Position-g.Position).Length)+" health="+Number(g.save.health)+" travelled="+Number(travelled)+" detoured="+detoured+" warning="+warning+" contact="+contact);
        // This is the semantic RED against old production: its real hunter
        // freezes near (17,6.998), never finding the legal exact foot above it.
        Check(detoured&&warning&&contact,"Naturally spawned hunter must finish the house detour and deliver its original melee within 20 idle seconds instead of stalling below the legal exact player foot");
        Check(g.enemies.Count==7&&g.playerAction==null&&g.save.coins==25&&g.save.xp==0&&g.save.shards==0,"Navigation and enemy contact invent no player attack, removed actor or reward");
    }
    private static void NaturalRouteAndOriginalContact(FrontierRuntime app)
    {
        foreach(bool pad in new[]{false,true}){Prepare(app,pad?.02f:1f/60);Enemy hunter=Route(app,pad);ReachMelee(app,hunter,pad?"left-stick-50Hz":"WASD-60Hz");}
        Console.WriteLine("PASS real WASD/left-stick natural house routes at 60/50 Hz, all seven authored foes, exact legal goal, swept original speed/distance gait and original hunter warning/melee; NATIVE_UNVERIFIED");
    }
    private static string State(GameCore g)
    {
        string state=JsonUtility.ToJson(g.save)+JsonUtility.ToJson(g.enemies)+JsonUtility.ToJson(g.projectiles)+JsonUtility.ToJson(g.effects)+JsonUtility.ToJson(g.hazards)+JsonUtility.ToJson(g.fallen)+JsonUtility.ToJson(g.playerAction)+JsonUtility.ToJson(new[]{g.attackCooldown,g.skillCooldown,g.dashCooldown,g.dashTime,g.invulnerable,g.guardTime,g.hitStop,g.walkDistance});
        foreach(Enemy enemy in g.enemies)state+="/"+Get<bool>(enemy,"pursuitAlerted")+Get<bool>(enemy,"returnEnabled")+Get<bool>(enemy,"returningToPost")+JsonUtility.ToJson(Get<Point>(enemy,"returnPost"))+Get<float>(enemy,"returnStalledTime");return state;
    }
    private static void PausesAndDrawsPreserveTheSameLivePursuit(FrontierRuntime app)
    {
        foreach(string boundary in new[]{"inventory","application","review"})
        {
            Prepare(app);Enemy hunter=Route(app);var g=app.Game;
            if(boundary=="inventory")Key(app,KeyCode.I);else if(boundary=="application")Call(app,"OnApplicationPause",true);else app.VisualReviewPaused=true;
            string state=State(g);Point foot=hunter.Position;
            for(int i=0;i<12;i++){Update(app);Event.current.type=i%2==0?EventType.Layout:EventType.Repaint;Draw(app);Check(State(g)==state&&Same(hunter.Position,foot),"Actual "+boundary+" pause and Layout/Repaint freeze enemy feet, gait, captured attacks, clocks and saved progress");}
            Event.current.type=EventType.Repaint;
            if(boundary=="inventory")Key(app,KeyCode.Escape);else if(boundary=="application")Call(app,"OnApplicationPause",false);else app.VisualReviewPaused=false;
            foreach(bool reduced in new[]{false,true})foreach(int fx in new[]{0,2})
            {
                g.save.reducedMotion=reduced;g.save.effectsQuality=fx;state=State(g);
                for(int draw=0;draw<4;draw++){Event.current.type=draw%2==0?EventType.Layout:EventType.Repaint;Draw(app);Check(State(g)==state,"Repeated actual OnGUI is entirely navigation/action/effects/save read-only under the existing motion and FX policies");}
            }
            g.save.reducedMotion=true;g.save.effectsQuality=0;Event.current.type=EventType.Repaint;ReachMelee(app,hunter,"resumed-"+boundary);
        }
        Console.WriteLine("PASS actual inventory/application/review pauses freeze the live natural pursuit; repeated OnGUI stays read-only in motion/FX variants; the same hunter resumes its detour and original contact; NATIVE_UNVERIFIED");
    }
    private static void LegacySaveLoadKeepsFeetAndResumes(FrontierRuntime app)
    {
        Prepare(app);Route(app);var g=app.Game;g.PrepareSave();Check(SaveStore.Write(g.save,0),"Existing checksum writer saves the real seven-actor detour");string warning;SaveData snapshot=SaveStore.Read(0,out warning);
        Check(snapshot!=null&&warning==null&&snapshot.worldEnemies.Length==7&&snapshot.snapshotRoom==5,"Existing checksum reader keeps the same regional snapshot");
        var names=new List<string>();foreach(var field in typeof(EnemyRecord).GetFields())names.Add(field.Name);names.Sort(StringComparer.Ordinal);Check(string.Join(",",names.ToArray())=="boss,hp,id,kind,maxHp,x,y","EnemyRecord retains its exact mature seven-field schema");
        string raw=JsonUtility.ToJson(snapshot);foreach(string transient in new[]{"returnPost","returnEnabled","returningToPost","returnStalledTime","pursuitAlerted","pathParents","pathQueue","actorFoot","footPath"})Check(raw.IndexOf(transient,StringComparison.OrdinalIgnoreCase)<0,"Save JSON has no navigation/transient field: "+transient);
        var envelope=JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(SaveStore.PathFor(0)));Check(envelope.format==1&&envelope.payload!=null&&envelope.checksum.Length==64,"Mature SHA256 save envelope is unchanged");
        app.EndVisualReview();
        foreach(int version in new[]{1,2})
        {
            SaveData old=JsonUtility.FromJson<SaveData>(raw);old.version=version;File.WriteAllText(SaveStore.PathFor(1),JsonUtility.ToJson(old));Call(app,"LoadSlot",1);g=app.Game;
            Check(g.mode=="world"&&g.save.version==2&&Same(g.Position,Goal)&&Near(g.save.health,100)&&g.enemies.Count==7,"Actual Runtime.LoadSlot accepts raw v"+version+" through mature v2 migration without moving player feet or changing health/actor count (version="+g.save.version+", mode="+g.mode+", player="+Number(g.Position.x)+","+Number(g.Position.y)+", health="+Number(g.save.health)+", enemies="+g.enemies.Count+")");
            foreach(EnemyRecord record in snapshot.worldEnemies)
            {
                Enemy restored=g.enemies.Find(e=>e.id==record.id);Check(restored!=null&&restored.kind==record.kind&&Near(restored.hp,record.hp)&&Near(restored.maxHp,record.maxHp)&&Same(restored.Position,new Point(record.x,record.y)),"Legacy runtime load preserves physical actor feet, identity and HP");
                Check(Get<bool>(restored,"returnEnabled")&&!Get<bool>(restored,"pursuitAlerted")&&!Get<bool>(restored,"returningToPost"),"Restoration preserves authored eligibility with fresh transient awareness");
            }
            Check(g.save.stage==1&&g.Has("intro")&&g.Has("exp_intro")&&g.save.coins==25&&g.save.xp==0&&g.save.shards==0,"Old save story flags and rewards survive without invented progress");
            Input.Clear();Time.unscaledDeltaTime=frameTime;ReachMelee(app,g.enemies.Find(e=>e.id==1),"raw-v"+version);
            Check((bool)Call(app,"Save"),"Existing Runtime.Save still writes after restored real pursuit");SaveData reread=SaveStore.Read(out warning);Check(reread!=null&&warning==null&&reread.worldEnemies.Length==7&&Near(reread.health,g.save.health),"Post-contact autosave still uses the existing readable checksum snapshot");
        }
        Console.WriteLine("PASS actual checksum/raw v1/v2 Runtime.LoadSlot and Runtime.Save preserve seven actor feet/HP, exact EnemyRecord schema, original story/rewards and fresh awareness; restored hunters finish the same detour; NATIVE_UNVERIFIED");
    }
    private static void CommittedArcherStillOwnsItsOriginalShot(FrontierRuntime app)
    {
        Prepare(app);app.BeginVisualReview(0,1);app.VisualReviewPaused=false;var g=app.Game;Enemy archer=g.enemies.Find(e=>e.id==2&&e.kind==1&&!e.boss);
        Check(archer!=null,"Canonical original region supplies its natural archer");g.enemies.RemoveAll(e=>e!=archer);g.save.companion=-1;g.invulnerable=0;
        Point hero=archer.Position+new Point(6.8f,0);Check(g.CanStand(hero)&&app.SetVisualReviewPosition(hero)&&Same(g.Position,hero),"Only the player is staged on legal open ground for an isolated committed-shot regression");
        Input.Clear();Time.unscaledDeltaTime=.02f;int frames=0;while(archer.state!=1&&frames++<180)Update(app);
        Check(frames<180&&Near(archer.timer,.65f)&&Near(archer.windupDuration,.65f),"Unmodified natural archer cooldown starts the original .65-second warning");Point origin=archer.Position,target=archer.target,facing=archer.facing,player=g.Position;float gait=archer.moveTime,energy=g.save.energy;
        for(int i=0;i<32;i++)
        {
            Motion(new Point(1,0));if(i==0)Input.keys.Add(KeyCode.Space);Update(app);
            if(i==0)Check(g.dashTime>0&&Near(energy-g.save.energy,20)&&Near(g.dashCooldown,.75f),"Real D/Space starts the original charged dash while the archer is committed");
            Check(archer.state==1&&Same(archer.Position,origin)&&Same(archer.target,target)&&Same(archer.facing,facing)&&Near(archer.moveTime,gait)&&Near(archer.timer,.65f-(i+1)*.02f,.002f),"Actual D/Space moving aim cannot relocate, retarget, refacing or re-time a committed archer warning");
        }
        Check((g.Position-player).Length>3&&g.save.energy<100,"Actual held D and charged Space physically move the player away from the captured archer target");
        Input.Clear();var oldShots=new List<Projectile>(g.projectiles);Update(app);Projectile shot=g.projectiles.Find(p=>!oldShots.Contains(p)&&p.visual=="enemyArrow");
        Check(archer.state==0&&Near(archer.releaseTime,.24f)&&Near(archer.timer,1.4f)&&shot!=null,"Original warning releases its genuine arrow with unchanged release/recovery");
        Check(!shot.friendly&&Near(shot.damage,10)&&Near(shot.velocity.Length,5)&&Same(shot.velocity.Normalized,(target-GameCore.WeaponOrigin(origin,1,facing)).Normalized),"Committed arrow retains its captured target heading, original five speed and ten damage");
        for(int i=0;i<8;i++){Update(app);Check(Same(archer.Position,origin)&&Near(archer.moveTime,gait),"Original release recovery keeps archer feet and gait planted before any new pursuit");}
        Console.WriteLine("PASS actual D/Space preserves natural archer captured target/facing/planted feet, .65 warning, genuine 5-speed/10-damage arrow and .24/1.4 release/recovery; NATIVE_UNVERIFIED");
    }
    public static int Main()
    {
        string previous=Application.persistentDataPath,temporary=Path.Combine(Path.GetTempPath(),"starfall-enemy-foot-save-"+Guid.NewGuid());Application.persistentDataPath=temporary;Directory.CreateDirectory(temporary);var app=new FrontierRuntime();
        try
        {
            Call(app,"Awake");Check(app.Game!=null,"Awake loads the shipped campaign");Action<FrontierRuntime>[] tests={NaturalRouteAndOriginalContact,PausesAndDrawsPreserveTheSameLivePursuit,LegacySaveLoadKeepsFeetAndResumes,CommittedArcherStillOwnsItsOriginalShot};int failures=0;
            foreach(var test in tests)try{test(app);}catch(Exception error){failures++;Console.WriteLine("FAIL "+test.Method.Name+": "+(error.InnerException??error).Message);}
            Console.WriteLine("Runtime enemy foot path: "+(tests.Length-failures)+"/"+tests.Length+" groups passed; frames="+checkedFrames+"; NATIVE_UNVERIFIED");return failures==0?0:1;
        }
        finally{app.EndVisualReview();Call(app,"OnDestroy");Application.persistentDataPath=previous;if(Directory.Exists(temporary))Directory.Delete(temporary,true);}
    }
}
