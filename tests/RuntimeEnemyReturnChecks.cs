// Shipped Update/OnGUI and checksum save paths against offline Unity declarations.
// Command captures verify existing art selection, never native GPU or player feel.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Starfall;
using UnityEngine;

public static class RuntimeEnemyReturnChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static string captureFolder,capturePrefix;
    private static int captures;
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static object Call(object value,string name,params object[] args){var m=value.GetType().GetMethod(name,Private|BindingFlags.Public|BindingFlags.Static);Check(m!=null,"Production entry point: "+name);return m.Invoke(value,args);}
    private static T Get<T>(object value,string name){var f=value.GetType().GetField(name,Private|BindingFlags.Public);Check(f!=null,"Fixture field: "+name);return (T)f.GetValue(value);}
    private static void Set(object value,string name,object field){var f=value.GetType().GetField(name,Private|BindingFlags.Public);Check(f!=null,"Fixture field: "+name);f.SetValue(value,field);}
    private static bool Near(float a,float b,float tolerance=.001f){return Math.Abs(a-b)<=tolerance;}
    private static bool Same(Point a,Point b){return (a-b).Length<.001f;}
    private static bool Same(Rect a,Rect b){return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height);}
    private static bool Same(Color a,Color b){return Near(a.r,b.r)&&Near(a.g,b.g)&&Near(a.b,b.b)&&Near(a.a,b.a);}
    private static bool Same(Matrix4x4 a,Matrix4x4 b){return Near(a.m00,b.m00)&&Near(a.m01,b.m01)&&Near(a.m03,b.m03)&&Near(a.m10,b.m10)&&Near(a.m11,b.m11)&&Near(a.m13,b.m13);}
    private static bool Returning(GameCore g,Enemy e){return (bool)Call(g,"EnemyReturningToPost",e);}
    private static void Clear(){GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();}
    private static void Update(FrontierRuntime app){Call(app,"Update");Input.keys.Clear();Input.buttonDown.Clear();}
    private static void Frames(FrontierRuntime app,int count){for(int i=0;i<count;i++)Update(app);}
    private static void Walk(KeyCode key){Input.Clear();Input.held.Add(key);Input.axes["Horizontal"]=key==KeyCode.A?-1:key==KeyCode.D?1:0;Input.axes["Vertical"]=key==KeyCode.W?1:key==KeyCode.S?-1:0;}
    private static void Press(FrontierRuntime app,KeyCode key){Input.Clear();Input.keys.Add(key);Update(app);Input.Clear();}
    private static void Draw(FrontierRuntime app,string suffix=null)
    {
        Clear();Call(app,"OnGUI");
        if(captureFolder!=null&&suffix!=null){var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();GUI.Export(captureFolder,capturePrefix+"-"+suffix);captures++;GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);}
    }
    private static void Prepare(FrontierRuntime app,int room=1,bool encounter=false)
    {
        Input.Clear();GUI.queuedClick=null;Clear();GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;
        Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;Event.current.type=EventType.Repaint;
        app.BeginVisualReview(0,room,encounter);app.VisualReviewPaused=false;var g=app.Game;
        g.projectiles.Clear();g.hazards.Clear();g.effects.Clear();g.fallen.Clear();g.save.companion=-1;
        g.save.reducedMotion=true;g.save.effectsQuality=0;g.save.showGuidance=false;g.save.english=true;
        g.save.health=g.save.maxHealth;g.save.energy=100;g.save.difficulty=1;g.save.totalTime=100;
        g.save.foodUntil=g.save.teaUntil=g.save.mealUntil=0;g.invulnerable=0;g.toastTime=0;
        Set(app,"applicationPaused",false);Set(app,"controllerUsed",false);Set(app,"pointerSeen",false);Set(app,"pointerAimActive",false);Set(app,"arrivalTime",0f);Set(app,"resumeTime",0f);Set(app,"combatHudTime",3f);Call(app,"Resize");Call(app,"ResetControllerAim",false);
    }
    private static Enemy Natural(FrontierRuntime app,int kind=0,int room=1)
    {
        Prepare(app,room);var g=app.Game;Enemy e=g.enemies.Find(x=>x.id==kind+1&&x.kind==kind&&!x.boss);
        Check(e!=null,"Shipped room supplies canonical ordinary actor kind "+kind);g.enemies.RemoveAll(x=>x!=e);
        Point post=e.Position,hero=post+new Point(kind==2?-7.5f:7.5f,0);Check((bool)Call(g,"Fits",post)&&g.CanStand(hero),"Canonical post and exact acquisition fixture use complete legal feet");
        Check(app.SetVisualReviewPosition(hero)&&Same(g.Position,hero),"Review stages only the player without silently relocating it");
        e.timer=1000;e.hp=e.maxHp*.55f;Set(app,"camera",post+new Point(4,0));return e;
    }
    private static Enemy Escaped(FrontierRuntime app,int kind,out Point post,int room=1)
    {
        Enemy e=Natural(app,kind,room);var g=app.Game;post=e.Position;
        Frames(app,70);Check((e.Position-post).Length>1.9f&&e.moving,"Natural actor physically chases the player from its original post before escape");
        int frames=0;while((e.Position-g.Position).Length<=10.3f&&frames++<250){Point player=g.Position,foot=e.Position;Walk(kind==2?KeyCode.A:KeyCode.D);Update(app);Check((g.Position-player).Length<=.0901f&&(e.Position-foot).Length<=(kind==0?.0421f:.0301f)&&g.CanStand(g.Position)&&(bool)Call(g,"Fits",e.Position),"Real held A/D escape obeys original player/enemy speeds and swept map collision");}Input.Clear();
        Check(frames<250,"Real A/D movement must escape the bounded ten-unit pursuit radius (kind="+kind+", enemy="+e.x+","+e.y+", player="+g.Position.x+","+g.Position.y+")");return e;
    }
    private static void Gait(FrontierRuntime app,Enemy e)
    {
        Set(app,"camera",e.Position+new Point(2,0));Draw(app);
        int frame=e.kind*4+GameCore.GaitFrame(e.moveTime,e.kind==0?1.35f:1.8f);
        Rect uv=new Rect((frame%4+(e.VisualFlip?1:0))/4f,1-(frame/4+1)/3f,e.VisualFlip?-.25f:.25f,1f/3);
        Check((int)Call(app,"EnemyPose",e)==1&&GUI.textureCalls.Exists(c=>c.texture.name=="resource:Frontier/Art/WalkMonsters"&&Same(c.uv,uv)),"Actual OnGUI selects the established distance-driven walking sprite and facing during return");
    }
    private static void RealReturnAndExistingWalking(FrontierRuntime app)
    {
        foreach(int kind in new[]{0,1,2})
        {
            Point post;Enemy e=Escaped(app,kind,out post);var g=app.Game;Point escaped=e.Position,hero=g.Position;
            float health=e.hp;int coins=g.save.coins,xp=g.save.xp,shards=g.save.shards;float timer=e.timer;float travelled=0;int frames=0;
            Update(app);
            // First assertion against immutable old production is behavioral:
            // old pursuit freezes here, rather than failing to compile on an API.
            Check((e.Position-escaped).Length>.001f&&(e.Position-post).Length<(escaped-post).Length,"Escaped natural enemy must physically walk back toward its original post instead of freezing where pursuit ended");
            Check(Returning(g,e),"Live returning query agrees with physical return");Gait(app,e);if(kind==0)Draw(app,"hunter-return-start");
            while(Returning(g,e)&&frames++<900)
            {
                Point before=e.Position;float gait=e.moveTime;Update(app);float step=(e.Position-before).Length;travelled+=step;
                Check(step<=(kind==0?.0421f:.0301f)&&(bool)Call(g,"Fits",e.Position)&&Near(e.moveTime-gait,step)&&Near(e.timer,timer)&&Near(e.hp,health)&&Same(g.Position,hero),"Return uses legal swept feet and original speed/gait while keeping HP, attack recovery and player feet unchanged");
                if(step>.001f){Point direction=(e.Position-before).Normalized;Check(e.moving&&!e.backpedaling&&direction.x*e.facing.x+direction.y*e.facing.y>.99f,"Physical return faces travelled direction using existing forward gait");if(frames==12){Gait(app,e);Draw(app,"kind-"+kind+"-return-walk");}}
            }
            Check(frames<900&&(e.Position-post).Length<=.081f&&travelled>1.5f&&!Returning(g,e),"Actual Updates reach the canonical post within the .08 arrival tolerance");
            Point arrived=e.Position;float gaitAtHome=e.moveTime;Frames(app,15);Check(Same(e.Position,arrived)&&Near(e.moveTime,gaitAtHome)&&!e.moving&&Near(e.hp,health)&&Near(e.timer,timer),"Arrival stops without snapping, healing, cooldown advancement or invented idle gait");
            Check(g.save.coins==coins&&g.save.xp==xp&&g.save.shards==shards&&g.fallen.Count==0&&g.CanStand(g.Position),"Returning and arrival grant no loot, XP, shards or death record");
            if(kind==0)Draw(app,"hunter-home");Console.WriteLine("RETURN kind="+kind+" frames="+frames+" travelled="+travelled.ToString("R",CultureInfo.InvariantCulture)+" residual="+(e.Position-post).Length.ToString("R",CultureInfo.InvariantCulture));
        }
        Console.WriteLine("PASS natural hunter/archer/heavy actual chase, A/D escape and physical return; swept original speed, exact distance gait, OnGUI existing walk UV/facing, damaged HP/recovery/rewards preserved; NATIVE_UNVERIFIED");
    }
    private static void ReturnRetainBandAndCommittedShot(FrontierRuntime app)
    {
        Point post;Enemy e=Escaped(app,0,out post);var g=app.Game;Check(Returning(g,e),"Escaped live actor begins returning");
        int frames=0;while((e.Position-g.Position).Length>9.5f&&frames++<180){Walk(KeyCode.A);Update(app);}Input.Clear();
        Check(frames<180&&(e.Position-g.Position).Length>9.3f&&Returning(g,e),"Actual A approach reaches the unacquired 8-to-10 band without interrupting return");
        Point before=e.Position;Frames(app,6);Check(Returning(g,e)&&e.x<before.x&&e.facing.x<0,"Stationary player in retention band cannot wake a returning actor");
        frames=0;while((e.Position-g.Position).Length>7.95f&&frames++<200){Walk(KeyCode.A);Update(app);}Input.Clear();
        Check(frames<200&&!Returning(g,e)&&(e.Position-g.Position).Length<=8,"Actual A re-entry within eight cancels return");before=e.Position;Update(app);Check(e.x>before.x&&e.moving&&e.facing.x>0,"Reacquired hunter physically turns and chases using its original attack-facing path");Draw(app,"hunter-reacquired");
        Enemy archer=Natural(app,1);g=app.Game;Point targetHero=archer.Position+new Point(6.8f,0);Check(app.SetVisualReviewPosition(targetHero)&&Same(g.Position,targetHero),"Exact legal canonical archer preparation fixture");archer.timer=0;Update(app);
        Check(archer.state==1&&Near(archer.timer,.65f),"Original archer starts its unchanged captured preparation");Draw(app,"archer-committed-warning");Point origin=archer.Position,target=archer.target,facing=archer.facing;bool beyond=false;
        for(int i=0;i<32;i++){Walk(KeyCode.D);if(i==0)Input.keys.Add(KeyCode.Space);Update(app);Input.Clear();beyond|=(archer.Position-g.Position).Length>10;Check(archer.state==1&&Same(archer.Position,origin)&&Same(archer.target,target)&&Same(archer.facing,facing)&&g.projectiles.Count==0&&!Returning(g,archer),"Real D/Space crossing ten cannot cancel or retarget a committed archer warning to return home");}
        Check(beyond&&g.save.energy<100&&g.dashCooldown>0,"Actual charged Space plus D crosses ten during the windup");Update(app);
        Check(archer.state==0&&Near(archer.releaseTime,.24f)&&Near(archer.timer,1.4f)&&g.projectiles.Count==1,"Original .65-second boundary releases exactly one committed arrow before return");Projectile shot=g.projectiles[0];
        Check(!shot.friendly&&shot.visual=="enemyArrow"&&Near(shot.damage,10)&&Near(shot.velocity.Length,5)&&Same(shot.velocity.Normalized,(target-GameCore.WeaponOrigin(origin,1,facing)).Normalized),"Committed arrow preserves captured heading, original five speed and ten damage");Draw(app,"archer-committed-release");
        for(int i=0;i<9;i++){Update(app);Check(Same(archer.Position,origin)&&!Returning(g,archer),"Original post-shot release recovery outranks return locomotion");}
        Console.WriteLine("PASS actual A return-band/reacquisition, facing reversal, and actual D/Space escape preserves captured archer windup, arrow and release recovery; NATIVE_UNVERIFIED");
    }
    private static void Append(StringBuilder text,object value,HashSet<object> seen)
    {
        if(value==null){text.Append("null;");return;}Type type=value.GetType();if(type.IsPrimitive||type.IsEnum||value is string||value is decimal){text.Append(Convert.ToString(value,CultureInfo.InvariantCulture)).Append(';');return;}if(!type.IsValueType&&!seen.Add(value)){text.Append("ref;");return;}var seq=value as System.Collections.IEnumerable;if(seq!=null){foreach(object item in seq)Append(text,item,seen);text.Append("end;");return;}var fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);Array.Sort(fields,(a,b)=>string.CompareOrdinal(a.Name,b.Name));foreach(var f in fields){text.Append(f.Name).Append('=');Append(text,f.GetValue(value),seen);}
    }
    private static string State(object value){var text=new StringBuilder();Append(text,value,new HashSet<object>());return text.ToString();}
    private static string PauseState(GameCore g){return State(g.save)+State(g.enemies)+State(g.projectiles)+State(g.playerAction)+State(g.effects)+State(new[]{g.attackCooldown,g.skillCooldown,g.dashCooldown,g.dashTime,g.invulnerable,g.hitStop,g.guardTime,g.walkDistance});}
    private static string Files(){var text=new StringBuilder();var paths=Directory.GetFiles(Application.persistentDataPath,"*",SearchOption.AllDirectories);Array.Sort(paths,StringComparer.Ordinal);foreach(string path in paths)text.Append(path).Append(':').Append(Convert.ToBase64String(File.ReadAllBytes(path))).Append(';');return text.ToString();}
    private static void PauseReadOnlyAndReactions(FrontierRuntime app)
    {
        Point post;Enemy e=Escaped(app,0,out post);var g=app.Game;Check(Returning(g,e),"Read-only fixture is a live return");var art=Get<PixelArt>(app,"art");Call(art,"Glow");Draw(app);
        int owned=Get<List<Texture2D>>(art,"owned").Count,cache=Get<Dictionary<string,Texture2D>>(art,"cache").Count;string files=Files();
        Matrix4x4 matrix=Matrix4x4.TRS(new Vector3(19,31,0),Quaternion.Euler(0,0,17),new Vector3(1.4f,1.4f,1));Color tint=new Color(.3f,.5f,.7f,.8f);
        foreach(bool motion in new[]{false,true})foreach(int fx in new[]{0,2})foreach(EventType evt in new[]{EventType.Layout,EventType.Repaint})
        {g.save.reducedMotion=motion;g.save.effectsQuality=fx;Event.current.type=evt;string state=State(g);float clock=Get<float>(app,"clock");GUI.matrix=matrix;GUI.color=tint;GUI.enabled=false;Draw(app);bool returning=Returning(g,e);Check(returning&&State(g)==state&&Near(Get<float>(app,"clock"),clock)&&Same(GUI.matrix,matrix)&&Same(GUI.color,tint)&&!GUI.enabled,"Repeated actual OnGUI and return queries remain wholly read-only and restore incoming GUI state across presentation policies");}
        Check(owned==Get<List<Texture2D>>(art,"owned").Count&&cache==Get<Dictionary<string,Texture2D>>(art,"cache").Count&&(bool)Call(app,"Save")&&files==Files(),"Return uses existing owned textures/cache and review never touches real slot/backup files");GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;
        foreach(string boundary in new[]{"menu","application","review"})
        {e=Escaped(app,0,out post);g=app.Game;if(boundary=="menu")Press(app,KeyCode.Escape);else if(boundary=="application")Call(app,"OnApplicationPause",true);else app.VisualReviewPaused=true;string state=PauseState(g);Frames(app,5);Draw(app);Check(PauseState(g)==state,"Actual "+boundary+" pause freezes return actor, HP, save and combat state");if(boundary=="menu")Press(app,KeyCode.Escape);else if(boundary=="application")Call(app,"OnApplicationPause",false);else app.VisualReviewPaused=false;Point before=e.Position;Update(app);Check((e.Position-before).Length>.001f&&Returning(g,e),"Actual "+boundary+" resume continues physical return");}
        e=Escaped(app,0,out post);g=app.Game;Point foot=e.Position;g.hitStop=.06f;Frames(app,2);Check(Same(e.Position,foot),"Actual global hit-stop freezes return locomotion");Frames(app,3);Check(!Same(e.Position,foot),"Physical return resumes after normal hit-stop expiration");
        e.stun=.3f;foot=e.Position;Frames(app,5);Check(Same(e.Position,foot),"Existing stagger stun outranks return movement");e.stun=0;e.hitTime=.2f;e.recoil=new Point(1,0);foot=e.Position;Update(app);Check(e.x>foot.x&&!e.moving,"Existing hit recoil runs before homeward facing and gait");
        Console.WriteLine("PASS full render/query state purity, GUI restore/cache/file isolation, actual menu/application/review and hit-stop freezes, original stun/recoil priority; NATIVE_UNVERIFIED");
    }
    private static void SaveCanonicalPostsAndExcludedEncounters(FrontierRuntime app)
    {
        Point post;Enemy e=Escaped(app,0,out post);var g=app.Game;int id=e.id;e.hp=23.25f;g.save.coins=313;g.save.xp=19;g.save.shards=27;g.save.flags.Add("enemy_return_legacy_sentinel");g.PrepareSave();
        Check(SaveStore.Write(g.save,0),"Existing checksum writer persists real displaced actor");string warning;SaveData snapshot=SaveStore.Read(0,out warning);Check(snapshot!=null&&warning==null&&snapshot.worldEnemies.Length==1,"Existing checksum reader restores actual actor snapshot");EnemyRecord record=snapshot.worldEnemies[0];Check(record.id==id&&Same(new Point(record.x,record.y),e.Position)&&Near(record.hp,23.25f),"Save retains actor identity, damaged health and physical feet instead of home position");
        var names=new List<string>();foreach(var f in typeof(EnemyRecord).GetFields())names.Add(f.Name);names.Sort(StringComparer.Ordinal);Check(string.Join(",",names.ToArray())=="boss,hp,id,kind,maxHp,x,y","EnemyRecord keeps the exact mature seven-field schema");string raw=JsonUtility.ToJson(snapshot);Check(!raw.Contains("returnPost")&&!raw.Contains("returnEnabled")&&!raw.Contains("returningToPost")&&!raw.Contains("returnStalledTime")&&!raw.Contains("pursuitAlerted"),"Saved JSON contains no transient return or acquisition state");
        foreach(int version in new[]{1,2})
        {
            SaveData old=JsonUtility.FromJson<SaveData>(raw);old.version=version;File.WriteAllText(SaveStore.PathFor(1),JsonUtility.ToJson(old));SaveData legacy=SaveStore.Read(1,out warning);Check(legacy!=null&&warning==null,"Actual raw v"+version+" migration reads unchanged save schema");Enemy before=e;g.Load(legacy);g.Close();e=g.enemies.Find(x=>x.id==id);Check(e!=null&&!ReferenceEquals(e,before)&&Same(e.Position,new Point(record.x,record.y))&&Near(e.hp,23.25f)&&!Returning(g,e),"Legacy load creates fresh transient actor while retaining saved feet and damaged HP");
            Point restored=e.Position;float timer=e.timer;Frames(app,8);Check(Same(e.Position,restored)&&Near(e.timer,timer)&&!Returning(g,e),"Loaded actor outside eight remains unaware instead of silently inheriting live return");Check(Same(Get<Point>(e,"returnPost"),post)&&Get<bool>(e,"returnEnabled"),"Restoration inherits the canonical original spawn post, not its saved displaced feet");
            Point hero=e.Position+new Point(7.5f,0);Check(g.CanStand(hero)&&app.SetVisualReviewPosition(hero)&&Same(g.Position,hero),"Loaded actor has legal exact reacquisition fixture");e.timer=1000;Update(app);Check(e.moving&&!Returning(g,e),"Live proximity inside eight reacquires restored actor normally");
            Check(g.save.coins==313&&g.save.xp==19&&g.save.shards==27&&g.Has("enemy_return_legacy_sentinel"),"Legacy migration and reacquisition invent no rewards or story progress");
        }
        var envelope=JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(SaveStore.PathFor(0)));Check(envelope.payload!=null&&envelope.checksum.Length==64,"Existing SHA256 envelope unchanged");envelope.checksum="BAD";File.WriteAllText(SaveStore.PathFor(2),JsonUtility.ToJson(envelope));Check(SaveStore.Read(2,out warning)==null&&warning!=null&&warning.Contains("checksum"),"Existing checksum still rejects tampering");
        Prepare(app,1,true);g=app.Game;Enemy boss=g.enemies.Find(x=>x.boss);Check(boss!=null&&!Get<bool>(boss,"returnEnabled")&&!Returning(g,boss),"Canonical shrine-spawned chapter Boss is excluded");
        Prepare(app,8);g=app.Game;Check(g.InAbyss&&g.enemies.Count>0,"Review traverses the real six-floor Abyss route");foreach(Enemy actor in g.enemies)Check(!Get<bool>(actor,"returnEnabled")&&!Returning(g,actor),"Real Abyss enemies/Boss keep authored encounter behavior");
        Prepare(app,11);g=app.Game;Check(g.enemies.Count==7,"Real Reach encounter supplies its seven guards");foreach(Enemy actor in g.enemies)Check(!Get<bool>(actor,"returnEnabled")&&!Returning(g,actor),"Canonical Reach task guards never opt in");
        Prepare(app,1);g=app.Game;foreach(string flag in new[]{"scout_saved","watch_accept","cache_1","watch_wave_0"})if(!g.Has(flag))g.save.flags.Add(flag);g.RestoreWatchEncounter();int task=0;foreach(Enemy actor in g.enemies)if(g.IsWatchEnemy(actor)){task++;Check(!Get<bool>(actor,"returnEnabled")&&!Returning(g,actor),"Canonical restored Watch wave retains authored task behavior");}Check(task==2,"Real optional Watch wave fixture supplies two actors");
        Prepare(app,0);g=app.Game;g.StartPractice();Check(g.practicing&&g.enemies.Count==1&&!Get<bool>(g.enemies[0],"returnEnabled")&&!Returning(g,g.enemies[0]),"Original teaching dummy remains excluded");g.EndPractice();g.save.practiceBits=7;Check(app.SetVisualReviewPosition(new Point(11,14))&&g.StartSparring(),"Actual Ada proximity opens original sparring");foreach(Enemy actor in g.enemies)Check(!Get<bool>(actor,"returnEnabled")&&!Returning(g,actor),"Original live sparring actor remains excluded");g.EndPractice();Check(app.SetVisualReviewPosition(new Point(11,14))&&g.StartRange(0),"Actual Ada proximity opens loadout range");foreach(Enemy actor in g.enemies)Check(!Get<bool>(actor,"returnEnabled")&&!Returning(g,actor),"Original stationary range targets remain excluded");
        Console.WriteLine("PASS actual checksum and raw v1/v2 retain physical feet/HP/rewards and original posts with fresh awareness; exact EnemyRecord schema; actual shrine Boss, Abyss, Reach, Watch, practice, sparring and range remain authored; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-enemy-return-"+Guid.NewGuid());Application.persistentDataPath=directory;captureFolder=args.Length>0&&args[0]!="-"?args[0]:null;capturePrefix=args.Length>1?args[1]:"qa-enemy-return";int failed=0;var app=new FrontierRuntime();
        try
        {
            Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"starfall-v1.json"),"{\"version\":1,\"profession\":0}");File.WriteAllText(Path.Combine(directory,"starfall-slot-1.json.bak"),"existing-slot-backup-sentinel");Call(app,"Awake");Check(app.Game!=null,"Production Awake loads real campaign");
            foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{RealReturnAndExistingWalking,ReturnRetainBandAndCommittedShot,PauseReadOnlyAndReactions,SaveCanonicalPostsAndExcludedEncounters})try{test(app);}catch(Exception ex){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(ex.InnerException??ex).Message);}
            app.EndVisualReview();Call(app,"OnDestroy");
        }
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        Console.WriteLine("CAPTURE "+captures+" actual OnGUI command streams; NATIVE_UNVERIFIED");Console.WriteLine("Runtime enemy return: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
