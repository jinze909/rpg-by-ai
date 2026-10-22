// Shipped Update, OnGUI, input, projectile and SaveStore integration using the
// repository's offline Unity declarations. Native import/GPU/feel is unverified.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Starfall;
using UnityEngine;

public static class RuntimeArcherEvasionChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static string captureFolder,capturePrefix;
    private static int captures;
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static object Call(object app,string name,params object[] args){var m=app.GetType().GetMethod(name,Private|BindingFlags.Public|BindingFlags.Static);Check(m!=null,"Existing production entry point: "+name);return m.Invoke(app,args);}
    private static T Get<T>(object app,string name){return (T)app.GetType().GetField(name,Private).GetValue(app);}
    private static void Set(object app,string name,object value){var f=app.GetType().GetField(name,Private);Check(f!=null,"Existing fixture field: "+name);f.SetValue(app,value);}
    private static bool Near(float a,float b,float tolerance=.001f){return Math.Abs(a-b)<=tolerance;}
    private static bool Same(Point a,Point b){return (a-b).Length<.001f;}
    private static bool Same(Rect a,Rect b){return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height);}
    private static bool Same(Color a,Color b){return Near(a.r,b.r)&&Near(a.g,b.g)&&Near(a.b,b.b)&&Near(a.a,b.a);}
    private static bool Same(Matrix4x4 a,Matrix4x4 b){return Near(a.m00,b.m00)&&Near(a.m01,b.m01)&&Near(a.m03,b.m03)&&Near(a.m10,b.m10)&&Near(a.m11,b.m11)&&Near(a.m13,b.m13);}
    private static void Clear(){GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();}
    private static void Update(FrontierRuntime app){Call(app,"Update");Input.keys.Clear();Input.buttonDown.Clear();}
    private static void Frames(FrontierRuntime app,int count){for(int i=0;i<count;i++)Update(app);}
    private static void Mouse(FrontierRuntime app,Point world){Point p=(Point)Call(app,"ScreenPoint",world);float s=Get<float>(app,"scale");Input.mousePosition=new Vector3(Get<float>(app,"offsetX")+p.x*s,Screen.height-Get<float>(app,"offsetY")-p.y*s,0);}
    private static void Walk(KeyCode key)
    {
        Input.Clear();Input.held.Add(key);
        Input.axes["Horizontal"]=key==KeyCode.A?-1:key==KeyCode.D?1:0;
        Input.axes["Vertical"]=key==KeyCode.W?1:key==KeyCode.S?-1:0;
    }
    private static void Draw(FrontierRuntime app,string capture=null)
    {
        Clear();Call(app,"OnGUI");
        if(captureFolder!=null&&capture!=null){GUI.Export(captureFolder,capturePrefix+"-"+capture);captures++;}
    }
    private static Enemy Prepare(FrontierRuntime app,bool mirror=false,Point? archer=null,Point? player=null)
    {
        Input.Clear();GUI.queuedClick=null;Clear();GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;
        Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;Event.current.type=EventType.Repaint;
        app.BeginVisualReview(0,1);app.VisualReviewPaused=false;var g=app.Game;
        Enemy e=g.enemies.Find(x=>x.kind==1&&!x.boss);Check(e!=null,"Shipped map 1 supplies an ordinary archer");g.enemies.RemoveAll(x=>x!=e);
        g.projectiles.Clear();g.hazards.Clear();g.effects.Clear();g.fallen.Clear();
        g.save.companion=-1;g.save.reducedMotion=true;g.save.effectsQuality=0;g.save.showGuidance=false;g.save.english=true;
        g.save.health=g.save.maxHealth;g.save.energy=100;g.save.difficulty=1;g.save.totalTime=100;g.save.talents=new int[3];g.save.relics=new[]{-1,-1};
        g.save.foodUntil=g.save.teaUntil=g.save.mealUntil=0;g.save.technique=0;g.invulnerable=0;g.toastTime=0;
        Point foot=archer??new Point(mirror?8.775f:11.225f,9.5f),hero=player??new Point(mirror?6.775f:13.225f,9.5f);
        Check((bool)Call(g,"Fits",foot)&&g.CanStand(hero),"Exact shipped-map fixture has complete legal archer/player feet");
        Check(app.SetVisualReviewPosition(hero)&&Same(g.Position,hero),"Review cannot silently relocate the exact player fixture");
        e.x=foot.x;e.y=foot.y;e.state=0;e.timer=10;e.stun=e.flash=e.hitTime=e.releaseTime=e.stagger=e.staggerRecovery=e.moveTime=0;e.recoil=new Point();e.moving=e.backpedaling=false;e.Face(hero-foot);
        Check((bool)Call(g,"ClearAttackPath",e.Position,g.Position),"Shipped-map fixture has actual line of sight");
        Set(app,"camera",new Point(10,10));Set(app,"clock",0f);Set(app,"combatHudTime",3f);Set(app,"applicationPaused",false);Set(app,"controllerUsed",false);Set(app,"pointerSeen",false);Set(app,"pointerAimActive",false);Set(app,"arrivalTime",0f);Set(app,"resumeTime",0f);Call(app,"Resize");Call(app,"ResetControllerAim",false);Mouse(app,e.Position);return e;
    }
    private static void Gait(FrontierRuntime app,Enemy e)
    {
        Draw(app);int gait=GameCore.GaitFrame(e.moveTime,1.8f),frame=4+(4-gait)%4;
        Rect expected=new Rect((frame%4+(e.VisualFlip?1:0))/4f,1-(frame/4+1)/3f,e.VisualFlip?-.25f:.25f,1f/3);
        Check((int)Call(app,"EnemyPose",e)==1&&GUI.textureCalls.Exists(c=>c.texture.name=="resource:Frontier/Art/WalkMonsters"&&Same(c.uv,expected)),
            "Actual OnGUI uses the moving archer row and distance-driven reversed backpedal gait");
    }
    private static void ActualWasdApproachAndGait(FrontierRuntime app)
    {
        foreach(bool mirror in new[]{false,true})
        {
            Enemy e=Prepare(app,mirror);var g=app.Game;Point initial=e.Position,hero=g.Position;
            Point blocked=(Point)Call(g,"SweepActor",e.Position,(g.Position-e.Position).Normalized*(-Time.unscaledDeltaTime*1.6f));
            Check(Same(blocked,e.Position),"The real straight retreat is blocked by the shipped wall before lateral fallback");
            int moving=0;float travelled=0;
            for(int i=0;i<18;i++)
            {
                Point before=e.Position;float gait=e.moveTime;Walk(mirror?KeyCode.D:KeyCode.A);Update(app);Input.Clear();
                Point step=e.Position-before;Check(step.Length>.001f,"Visible idle wall archer escapes during actual "+(mirror?"D":"A")+" approach at frame "+i);
                Check(step.Length<=.0321f&&(bool)Call(g,"Fits",e.Position)&&g.CanStand(g.Position),"Every evasive/player movement respects original swept feet and 1.6-unit speed budget");
                Check((e.Position-g.Position).Length>(before-g.Position).Length+.00001f,"Evasion increases separation from the current approaching player");
                Check(e.state==0&&e.moving&&e.backpedaling&&Near(e.moveTime-gait,step.Length),"Actual movement owns existing idle/moving/backpedal flags and exact distance clock");
                Point face=(g.Position-e.Position).Normalized;Check(e.facing.x*face.x+e.facing.y*face.y>.999f,"Retreat keeps the bow facing the player");
                Gait(app,e);moving++;travelled+=step.Length;
            }
            Check(moving==18&&Math.Abs(e.y-initial.y)>.05f&&Math.Abs(g.Position.x-hero.x)>1&&Near(e.moveTime,travelled),"Real WASD approach produces sustained lateral wall escape and honest gait distance");
            for(int i=0;i<80;i++)
            {
                Point before=e.Position;float gait=e.moveTime;Update(app);Point step=e.Position-before;
                Check(step.Length<=.0321f&&(bool)Call(g,"Fits",e.Position)&&Near(e.moveTime-gait,step.Length),"Sustained post-approach wall escape retains swept feet, original speed and exact gait distance");
                if(step.Length>.001f)Check(e.moving&&e.backpedaling,"Sustained wall escape cannot silently slide with idle/forward flags");
            }
            Check((e.Position-g.Position).Length>2.8f,"Idle archer reaches useful separation after sustained real wall escape instead of creeping beside the player");
        }
        // Unobstructed vertical backpedal retains the same existing walk atlas.
        Enemy vertical=Prepare(app,false,new Point(12,14),new Point(12,12));Walk(KeyCode.S);Update(app);Input.Clear();
        Check(vertical.moving&&vertical.backpedaling&&Math.Abs(vertical.facing.y)>Math.Abs(vertical.facing.x),"Existing vertical retreat retains player-facing movement");Gait(app,vertical);
        foreach(float dt in new[]{.005f,.02f,.05f})foreach(bool mirror in new[]{false,true})
        {
            Enemy e=Prepare(app,mirror);var g=app.Game;Time.unscaledDeltaTime=dt;
            for(int frame=0;frame<(int)Math.Ceiling(2/dt);frame++)
            {
                Point before=e.Position;float separation=(before-g.Position).Length,gait=e.moveTime;Update(app);Point step=e.Position-before;
                Check(step.Length<=dt*1.6f+.0001f&&(bool)Call(g,"Fits",e.Position)&&Near(e.moveTime-gait,step.Length),"Two-second real Update escape preserves feet/speed/gait at dt="+dt);
                if(separation<2.99f)Check(step.Length>.001f&&e.moving&&e.backpedaling,"Visible idle archer cannot stall or creep into an idle pose at dt="+dt);
            }
            Check((e.Position-g.Position).Length>2.95f,"Two real seconds reach the original three-unit retreat band without frame-rate wall stalls at dt="+dt);
            if(e.moving)Gait(app,e);else{Draw(app);Check(!e.backpedaling&&(int)Call(app,"EnemyPose",e)==0,"Reaching the original retreat band returns the actual OnGUI archer to idle");}
        }
        Console.WriteLine("PASS actual A/D approach against both shipped wall faces and sustained 2-second .005/.02/.05s Update escape: increasing separation, swept feet, original speed/facing/moveTime and OnGUI reversed gait; vertical gait retained; NATIVE_UNVERIFIED");
    }
    private static string WarningGeometry(FrontierRuntime app,Enemy e)
    {
        Clear();Call(app,"DrawEnemyWarning",e);var text=new StringBuilder();
        foreach(var c in GUI.textureCalls)foreach(float v in new[]{c.rect.x,c.rect.y,c.rect.width,c.rect.height,c.matrix.m00,c.matrix.m01,c.matrix.m03,c.matrix.m10,c.matrix.m11,c.matrix.m13})text.Append(v.ToString("R",CultureInfo.InvariantCulture)).Append(',');
        Check(GUI.textureCalls.Count>0,"Actual archer preparation draws its captured warning ray");return text.ToString();
    }
    private static void ActualArrowsAndPlayerStops(FrontierRuntime app)
    {
        foreach(bool vertical in new[]{false,true})
        {
            Enemy e=vertical?Prepare(app,false,new Point(12,14),new Point(12,12)):Prepare(app);var g=app.Game;e.timer=0;Update(app);
            Check(e.state==1&&Near(e.timer,.65f)&&Near(e.windupDuration,.65f)&&g.projectiles.Count==0,"Actual idle update captures the unchanged .65-second arrow preparation");
            Point origin=e.Position,target=e.target,facing=e.facing;float health=g.save.health;Set(app,"camera",new Point(10,10));string geometry=WarningGeometry(app,e);
            for(int frame=0;frame<32;frame++)
            {
                // Move across and back through real input; the captured lane cannot follow.
                if(frame<6)Walk(vertical?KeyCode.A:KeyCode.W);else if(frame<12)Walk(vertical?KeyCode.D:KeyCode.S);else Input.Clear();
                Update(app);Input.Clear();Set(app,"camera",new Point(10,10));
                Check(e.state==1&&g.projectiles.Count==0&&Same(e.Position,origin)&&Same(e.target,target)&&Same(e.facing,facing)&&!e.moving&&!e.backpedaling,"Remaining real windup cannot evade, retarget, refacing or release early");
                Check(Near(e.timer,.65f-(frame+1)*.02f,.002f)&&WarningGeometry(app,e)==geometry,"Original remaining windup and captured warning geometry stay exact while the player sidesteps");
                Draw(app);Check((int)Call(app,"EnemyPose",e)==2,"Actual OnGUI shows the original preparation pose");
            }
            Check(Same(g.Position,target),"Real sidestep/return places the player back in the captured arrow lane");Update(app);
            Check(e.state==0&&Near(e.timer,1.4f)&&Near(e.releaseTime,.24f)&&g.projectiles.Count==1&&!e.moving&&!e.backpedaling&&Same(e.Position,origin),"Normal preparation boundary alone releases once and preserves original release/recovery clocks");
            Point muzzle=GameCore.WeaponOrigin(origin,1,facing),shotDirection=(target-muzzle).Normalized;
            Projectile shot=g.projectiles[0];Check(!shot.friendly&&shot.visual=="enemyArrow"&&Near(shot.damage,10)&&Near(shot.velocity.Length,5)&&Same(shot.velocity.Normalized,shotDirection),"Actual archer Fire retains hostile arrow kind, captured muzzle-to-target heading, five-unit speed and ten damage");
            bool contact=false;for(int frame=0;frame<50&&g.projectiles.Contains(shot);frame++){Update(app);Draw(app);contact|=g.save.health<health;}
            Check(contact&&!g.projectiles.Contains(shot)&&Near(health-g.save.health,10)&&g.effects.Exists(x=>x.kind=="projectileImpact"),"Actual arrow contact damages the returned player once for the original ten and retires normally");
            Check(Same(e.target,target)&&Same(e.attackOrigin,origin)&&Same(e.attackDestination,origin),"Completed warning keeps its captured lane through release and recovery");
            for(int frame=0;frame<110&&e.state==0;frame++)Update(app);
            Check(e.state==1&&Near(e.timer,.65f)&&Same(e.target,g.Position)&&Same(e.attackOrigin,e.Position)&&Same(e.attackDestination,e.Position),"Only the normal next preparation captures a new target/origin after idle evasion");
        }
        foreach(bool skill in new[]{false,true})
        {
            Enemy e=Prepare(app,false,new Point(12,14),new Point(12.95f,14));var g=app.Game;e.hp=e.maxHp=1000;if(!skill)e.stagger=29.9f;
            Update(app);Check(e.moving&&e.backpedaling,"Close idle archer is fleeing before real player interruption");float hp=e.hp;Mouse(app,e.Position);Input.keys.Add(skill?KeyCode.Q:KeyCode.J);Update(app);
            Check(g.playerAction!=null&&g.playerAction.kind==(skill?"skill":"attack")&&!g.playerAction.released&&Near(e.hp,hp),"Real "+(skill?"Q":"J")+" accepts its original preparation before contact");
            for(int frame=0;frame<60&&e.hp==hp;frame++)Update(app);
            Check(e.hp<hp&&e.hitTime>0&&e.stun>1&&(!skill?e.stagger==0&&e.staggerRecovery>0:true),"Actual "+(skill?"Q skill":"J stagger break")+" contact stops the living evasive archer");
            float gait=e.moveTime;for(int frame=0;frame<20;frame++){Update(app);Draw(app);Check(!e.moving&&!e.backpedaling&&Near(e.moveTime,gait)&&(bool)Call(g,"Fits",e.Position),"Existing hit/stun branches suppress fleeing and gait without bypassing swept recoil");}
            Point stopped=e.Position;Frames(app,8);Check(Same(e.Position,stopped)&&e.stun>0,"Real control remains stationary after brief recoil ends");
        }
        Console.WriteLine("PASS horizontal/vertical actual arrow capture, fixed .65s warning/remaining windup, unchanged release/5-speed/10-damage contact and normal next preparation; real J break and Q contact stop idle evasion; NATIVE_UNVERIFIED");
    }
    private static void Append(StringBuilder text,object value,HashSet<object> seen)
    {
        if(value==null){text.Append("null;");return;}Type type=value.GetType();if(type.IsPrimitive||type.IsEnum||value is string||value is decimal){text.Append(Convert.ToString(value,CultureInfo.InvariantCulture)).Append(';');return;}if(!type.IsValueType&&!seen.Add(value)){text.Append("ref;");return;}var seq=value as System.Collections.IEnumerable;if(seq!=null){foreach(object item in seq)Append(text,item,seen);text.Append("end;");return;}var fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);Array.Sort(fields,(a,b)=>string.CompareOrdinal(a.Name,b.Name));foreach(var f in fields){text.Append(f.Name).Append('=');Append(text,f.GetValue(value),seen);}
    }
    private static string State(GameCore g){var text=new StringBuilder();Append(text,g,new HashSet<object>());return text.ToString();}
    private static string PausedState(GameCore g)
    {
        // Existing menu Tick deliberately continues the toast presentation
        // countdown before its world guard. Compare every AI/save field plus
        // the combat clocks that actually belong to the paused simulation.
        var text=new StringBuilder();var seen=new HashSet<object>();Append(text,g.save,seen);Append(text,g.enemies,seen);Append(text,g.playerAction,seen);Append(text,g.projectiles,seen);Append(text,g.effects,seen);
        foreach(float clock in new[]{g.attackCooldown,g.skillCooldown,g.dashCooldown,g.dashTime,g.invulnerable,g.hitStop,g.guardTime,g.walkDistance})text.Append(clock.ToString("R",CultureInfo.InvariantCulture)).Append(';');return text.ToString();
    }
    private static string Files(){var text=new StringBuilder();var paths=Directory.GetFiles(Application.persistentDataPath,"*",SearchOption.AllDirectories);Array.Sort(paths,StringComparer.Ordinal);foreach(string path in paths)text.Append(path).Append(':').Append(Convert.ToBase64String(File.ReadAllBytes(path))).Append(';');return text.ToString();}
    private static void OwnershipSaveAndPause(FrontierRuntime app)
    {
        Enemy e=Prepare(app);Update(app);var g=app.Game;var art=Get<PixelArt>(app,"art");Call(art,"Glow");Draw(app);var owned=Get<List<Texture2D>>(art,"owned");var cache=Get<Dictionary<string,Texture2D>>(art,"cache");int owns=owned.Count,caches=cache.Count;string files=Files();
        var matrix=Matrix4x4.TRS(new Vector3(19,31,0),Quaternion.Euler(0,0,17),new Vector3(1.4f,1.4f,1));var tint=new Color(.3f,.5f,.7f,.8f);
        foreach(bool motion in new[]{false,true})foreach(int fx in new[]{0,2})foreach(EventType evt in new[]{EventType.Layout,EventType.Repaint})
        {
            g.save.reducedMotion=motion;g.save.effectsQuality=fx;Event.current.type=evt;string state=State(g);float clock=Get<float>(app,"clock");GUI.matrix=matrix;GUI.color=tint;GUI.enabled=false;Draw(app);
            Check(State(g)==state&&Near(Get<float>(app,"clock"),clock),"Actual repeated layout/repaint leaves complete Core/save/AI state and clock unchanged");Check(Same(GUI.matrix,matrix)&&Same(GUI.color,tint)&&!GUI.enabled,"Actual OnGUI restores incoming GUI matrix/color/enabled");
        }
        Check(owns==owned.Count&&caches==cache.Count&&(bool)Call(app,"Save")&&Files()==files,"Pure AI evasion adds no art cache allocation, review save write or drawing ownership");GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;
        foreach(string boundary in new[]{"review","application","focus","menu"})
        {
            e=Prepare(app,false,new Point(12,14),new Point(12,12));Update(app);g=app.Game;
            if(boundary=="review")app.VisualReviewPaused=true;else if(boundary=="application")Call(app,"OnApplicationPause",true);else if(boundary=="focus")Call(app,"OnApplicationFocus",false);else{Input.keys.Add(KeyCode.Escape);Update(app);}
            string before=PausedState(g);Frames(app,8);Draw(app);Check(PausedState(g)==before,"Actual "+boundary+" pause freezes every AI/save field and existing combat clock");
            if(boundary=="review")app.VisualReviewPaused=false;else if(boundary=="application")Call(app,"OnApplicationPause",false);else g.Close();
            Point prior=e.Position;Update(app);Check((e.Position-prior).Length>.001f&&e.moving&&e.backpedaling,"Actual "+boundary+" resume returns ordinary idle evasion through the existing clock");
        }
        e=Prepare(app);g=app.Game;Update(app);e.hp=31.25f;g.save.coins=313;g.save.flags.Add("archer_evasion_mature_story");g.PrepareSave();
        Check(SaveStore.Write(g.save,0),"Actual checksum writer persists moved archer feet and partial HP");string warning;SaveData saved=SaveStore.Read(0,out warning);Check(saved!=null&&warning==null&&saved.worldEnemies.Length==1,"Actual checksum envelope restores one owned archer record");
        EnemyRecord record=saved.worldEnemies[0];Check(record.id==e.id&&record.kind==1&&!record.boss&&Near(record.x,e.x)&&Near(record.y,e.y)&&Near(record.hp,31.25f),"Existing world snapshot owns only canonical identity, moved legal feet and HP");
        Check(typeof(EnemyRecord).GetFields().Length==7,"EnemyRecord retains its exact seven-field persistence schema");string raw=JsonUtility.ToJson(saved);Check(!raw.Contains("backpedaling")&&!raw.Contains("moveTime")&&!raw.Contains("staggerRecovery")&&!raw.Contains("evasion"+"Timer"),"Existing JSON contains no movement, gait, control or evasion timer state");
        foreach(int version in new[]{1,2})
        {
            SaveData old=JsonUtility.FromJson<SaveData>(raw);old.version=version;File.WriteAllText(SaveStore.PathFor(1),JsonUtility.ToJson(old));SaveData legacy=SaveStore.Read(1,out warning);Check(legacy!=null&&warning==null,"Actual raw v"+version+" SaveStore reader accepts existing schema");
            g.Load(legacy);g.Close();e=g.enemies.Find(x=>x.id==record.id);Check(e!=null&&e.kind==1&&!e.boss&&Near(e.hp,record.hp)&&Same(e.Position,new Point(record.x,record.y))&&g.CanStand(e.Position)&&g.save.coins==313&&g.Has("archer_evasion_mature_story"),"Raw legacy load preserves owned archer identity/HP/feet and mature campaign");
            Check(e.state==0&&!e.moving&&!e.backpedaling&&Near(e.moveTime,0)&&Near(e.stun,0)&&Near(e.releaseTime,0)&&e.timer>0,"Actual load resets transient preparation/movement clocks under existing policy");
        }
        var envelope=JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(SaveStore.PathFor(0)));Check(envelope.payload!=null&&envelope.checksum.Length==64,"Actual envelope uses original SHA256 checksum");envelope.checksum="BAD";File.WriteAllText(SaveStore.PathFor(2),JsonUtility.ToJson(envelope));Check(SaveStore.Read(2,out warning)==null&&warning!=null&&warning.Contains("checksum"),"Actual checksum rejects tampered saved archer state");
        Console.WriteLine("PASS full OnGUI Core/save/GUI/cache ownership, real review/menu/application/focus freeze/resume, checksum and raw v1/v2 owned archer HP/feet, unchanged seven-field snapshot and transient reset; NATIVE_UNVERIFIED");
    }
    private static void Captures(FrontierRuntime app)
    {
        foreach(bool mirror in new[]{false,true})
        {
            Prepare(app,mirror);string side=mirror?"left":"right";Draw(app,side+"-idle");
            for(int i=1;i<=24;i++){Walk(mirror?KeyCode.D:KeyCode.A);Update(app);Input.Clear();if(i==8||i==16||i==24)Draw(app,side+"-approach-"+i);}
        }
        foreach(bool vertical in new[]{false,true})
        {
            Enemy e=vertical?Prepare(app,false,new Point(12,14),new Point(12,12)):Prepare(app);e.timer=0;Update(app);Frames(app,15);Draw(app,(vertical?"vertical":"horizontal")+"-warning");Frames(app,18);Draw(app,(vertical?"vertical":"horizontal")+"-release");
        }
        Prepare(app,false,new Point(12,14),new Point(12,12));
        for(int i=1;i<=16;i++){Walk(KeyCode.S);Update(app);Input.Clear();if(i==8||i==16)Draw(app,"vertical-gait-"+i);}
        Check(captures==14,"Fourteen actual OnGUI wall approach, horizontal/vertical gait and warning/release captures");Console.WriteLine("CAPTURE 14 actual OnGUI wall approach, horizontal/vertical gait and warning/release fixtures; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-archer-evasion-"+Guid.NewGuid());Application.persistentDataPath=directory;captureFolder=args.Length>0&&args[0]!="-"?args[0]:null;capturePrefix=args.Length>1?args[1]:"qa-archer-evasion";bool only=args.Length>2&&args[2]=="capture-only";int failed=0;var app=new FrontierRuntime();
        try
        {
            Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"starfall-v1.json"),"{\"version\":1,\"profession\":0}");File.WriteAllText(Path.Combine(directory,"starfall-slot-1.json.bak"),"existing-slot-backup-sentinel");Call(app,"Awake");Check(app.Game!=null,"Production Awake loads the campaign");
            if(!only)foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{ActualWasdApproachAndGait,ActualArrowsAndPlayerStops,OwnershipSaveAndPause})try{test(app);}catch(Exception ex){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(ex.InnerException??ex).Message);}
            if(captureFolder!=null)Captures(app);app.EndVisualReview();Call(app,"OnDestroy");
        }
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        if(only){Console.WriteLine("Capture-only: semantic checks skipped; NATIVE_UNVERIFIED");return 0;}Console.WriteLine("Runtime archer evasion: "+(3-failed)+"/3 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
