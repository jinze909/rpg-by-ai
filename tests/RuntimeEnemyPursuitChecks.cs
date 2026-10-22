// Real production Update/OnGUI, input and SaveStore with offline Unity
// declarations. Captures are command streams; native GPU/feel is unverified.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Starfall;
using UnityEngine;

public static class RuntimeEnemyPursuitChecks
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
    private static void Mouse(FrontierRuntime app,Point world){Point p=(Point)Call(app,"ScreenPoint",world);float scale=Get<float>(app,"scale");Input.mousePosition=new Vector3(Get<float>(app,"offsetX")+p.x*scale,Screen.height-Get<float>(app,"offsetY")-p.y*scale,0);}
    private static void Walk(KeyCode key){Input.Clear();Input.held.Add(key);Input.axes["Horizontal"]=key==KeyCode.A?-1:key==KeyCode.D?1:0;Input.axes["Vertical"]=key==KeyCode.W?1:key==KeyCode.S?-1:0;}
    private static void Press(FrontierRuntime app,KeyCode key){Input.Clear();Input.keys.Add(key);Update(app);Input.Clear();}
    private static void Draw(FrontierRuntime app,string capture=null)
    {
        Clear();Call(app,"OnGUI");
        if(captureFolder!=null&&capture!=null){var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();GUI.Export(captureFolder,capturePrefix+"-"+capture);captures++;GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);}
    }
    private static Enemy Prepare(FrontierRuntime app,int room=1,int kind=0,Point? actor=null,Point? hero=null,int actorId=0)
    {
        Input.Clear();GUI.queuedClick=null;Clear();GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;
        Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;Event.current.type=EventType.Repaint;
        app.BeginVisualReview(0,room);app.VisualReviewPaused=false;var g=app.Game;
        Enemy e=g.enemies.Find(x=>x.kind==kind&&!x.boss&&(actorId==0||x.id==actorId));Check(e!=null,"Shipped map supplies ordinary kind "+kind);g.enemies.RemoveAll(x=>x!=e);
        g.projectiles.Clear();g.hazards.Clear();g.effects.Clear();g.fallen.Clear();
        g.save.companion=-1;g.save.reducedMotion=true;g.save.effectsQuality=0;g.save.showGuidance=false;g.save.english=true;
        g.save.health=g.save.maxHealth;g.save.energy=100;g.save.difficulty=1;g.save.totalTime=100;g.save.talents=new int[3];g.save.relics=new[]{-1,-1};
        g.save.foodUntil=g.save.teaUntil=g.save.mealUntil=0;g.save.technique=0;g.invulnerable=0;g.toastTime=0;
        Point foot=actor??new Point(12,12),player=hero??new Point(19.5f,12);
        Check((bool)Call(g,"Fits",foot)&&g.CanStand(player),"Exact shipped-map fixture has legal complete actor/player feet");
        Check(app.SetVisualReviewPosition(player)&&Same(g.Position,player),"Review cannot silently relocate exact player fixture");
        e.x=foot.x;e.y=foot.y;e.state=0;e.timer=1000;e.stun=e.flash=e.hitTime=e.releaseTime=e.stagger=e.staggerRecovery=e.moveTime=0;e.recoil=new Point();e.moving=e.backpedaling=false;e.Face(player-foot);
        Set(app,"camera",room==2?new Point(7,7):new Point(18,12));Set(app,"clock",0f);Set(app,"combatHudTime",3f);Set(app,"applicationPaused",false);Set(app,"controllerUsed",false);Set(app,"pointerSeen",false);Set(app,"pointerAimActive",false);Set(app,"arrivalTime",0f);Set(app,"resumeTime",0f);Call(app,"Resize");Call(app,"ResetControllerAim",false);return e;
    }
    private static void Gait(FrontierRuntime app,Enemy e)
    {
        Draw(app);int frame=e.kind*4+GameCore.GaitFrame(e.moveTime,e.kind==0?1.35f:1.8f);
        Rect expected=new Rect((frame%4+(e.VisualFlip?1:0))/4f,1-(frame/4+1)/3f,e.VisualFlip?-.25f:.25f,1f/3);
        Point foot=(Point)Call(app,"ScreenPoint",e.Position);
        Check(foot.x>0&&foot.x<1280&&foot.y>0&&foot.y<800,"Pursuing actor is on the actual rendered viewport");
        Check((int)Call(app,"EnemyPose",e)==1&&GUI.textureCalls.Exists(c=>c.texture.name=="resource:Frontier/Art/WalkMonsters"&&Same(c.uv,expected)),"Actual OnGUI draws existing distance-driven ordinary pursuit gait");
    }
    private static string WarningGeometry(FrontierRuntime app,Enemy e)
    {
        Clear();Call(app,"DrawEnemyWarning",e);var text=new StringBuilder();
        foreach(var c in GUI.textureCalls)foreach(float v in new[]{c.rect.x,c.rect.y,c.rect.width,c.rect.height,c.matrix.m00,c.matrix.m01,c.matrix.m03,c.matrix.m10,c.matrix.m11,c.matrix.m13})text.Append(v.ToString("R",CultureInfo.InvariantCulture)).Append(',');
        Check(GUI.textureCalls.Count>0,"Actual preparation draws original warning geometry");return text.ToString();
    }
    private static void WaterDetourGaitAndMelee(FrontierRuntime app)
    {
        Enemy e=Prepare(app,2,0,new Point(3.5f,10.5f),new Point(8.5f,4.5f));var g=app.Game;e.timer=0;
        // This isolated path fixture stages only temporary invulnerability. It
        // is removed before checking real original melee contact below.
        g.invulnerable=1000;Point hero=g.Position,start=e.Position;float travelled=0,maxDistance=(e.Position-hero).Length;int walking=0,frames=0;
        Check(maxDistance<8&&!(bool)Call(g,"ClearAttackPath",e.Position,hero),"Archived legal water fixture starts inside eight behind shipped water");Draw(app,"water-start");
        while((e.Position-hero).Length>1.15f&&frames++<800)
        {
            Point before=e.Position;float gait=e.moveTime;Update(app);Point step=e.Position-before;float distance=(e.Position-hero).Length;maxDistance=Math.Max(maxDistance,distance);travelled+=step.Length;
            Check(Same(g.Position,hero)&&(bool)Call(g,"Fits",e.Position)&&step.Length<=.0421f&&Near(e.moveTime-gait,step.Length),"Actual water detour preserves stationary player, swept legal feet, original 2.1 speed and exact gait distance");
            if(e.state==0&&e.releaseTime<=0&&step.Length>.001f){Check(e.moving&&!e.backpedaling,"Real water path movement owns existing forward movement flags");if(walking++%25==0)Gait(app,e);}
            if(frames==8||frames==30||frames==90||frames==150||frames==230)Draw(app,"water-frame-"+frames);
        }
        Console.WriteLine("WATER frames="+frames+" max_separation="+maxDistance.ToString("R",CultureInfo.InvariantCulture)+" travelled="+travelled.ToString("R",CultureInfo.InvariantCulture)+" end="+e.x.ToString("R",CultureInfo.InvariantCulture)+","+e.y.ToString("R",CultureInfo.InvariantCulture));
        Check(maxDistance>8&&maxDistance<10&&walking>80&&travelled>8&&(e.Position-hero).Length<=1.15f,"Idle ordinary hunter must finish the real shipped water detour after separation briefly exceeds eight (ended at "+(e.Position-hero).Length+")");
        Check(Near(e.moveTime,travelled)&&!Same(e.Position,start),"Whole detour uses actual travelled distance for the established gait clock");g.invulnerable=0;
        float health=g.save.health;bool warning=false,contact=false;Point target=new Point(),origin=new Point(),facing=new Point();string geometry=null;
        for(int i=0;i<220&&!contact;i++)
        {
            Update(app);Set(app,"camera",new Point(7,7));
            if(e.state==1)
            {
                if(!warning){warning=true;target=e.target;origin=e.Position;facing=e.facing;geometry=WarningGeometry(app,e);Check(Near(e.windupDuration,.65f),"Pursuit retains original .65-second preparation");Draw(app,"water-melee-warning");}
                else Check(Same(e.target,target)&&Same(e.Position,origin)&&Same(e.facing,facing)&&WarningGeometry(app,e)==geometry,"Original committed melee target/body/facing/warning cannot follow or slide during preparation");
                Draw(app);Check((int)Call(app,"EnemyPose",e)==2,"OnGUI retains original ordinary preparation pose");
            }
            if(g.save.health<health){contact=true;Check(Near(health-g.save.health,11)&&e.state==0&&Near(e.releaseTime,.24f)&&Near(e.timer,1.4f),"After honest pursuit, original melee contact still deals eleven with unchanged release/recovery");Draw(app,"water-melee-contact");}
        }
        Check(warning&&contact,"Real Update must reach original warning and damage after the water detour with staged protection removed");
        // The original .18-second hit reaction exceeds the .12-second fresh
        // attack buffer. Let real Updates finish it; do not clear combat clocks.
        int recoilFrames=0;while(g.playerHitTime>0&&recoilFrames++<30)Update(app);Console.WriteLine("PLAYER RECOVERY frames="+recoilFrames+" hit="+g.playerHitTime+" distance="+(e.Position-g.Position).Length);Check(recoilFrames<30&&g.playerHitTime==0,"Authored player recoil naturally finishes before fresh J");float enemyHealth=e.hp;Mouse(app,e.Position);Press(app,KeyCode.J);Check(g.playerAction!=null&&g.playerAction.kind=="attack"&&!g.playerAction.released&&Near(e.hp,enemyHealth),"Original J still accepts its real preparation after pursuit contact");
        for(int i=0;i<60&&Near(e.hp,enemyHealth);i++)Update(app);Check(e.hp<enemyHealth,"Real prepared J contact still damages the pursuing hunter");Draw(app,"water-player-j-contact");
        Console.WriteLine("PASS actual shipped water detour beyond eight, legal swept 2.1-speed movement and distance gait, original OnGUI walk/preparation, real eleven-damage melee and original J contact; NATIVE_UNVERIFIED");
    }
    private static void AcquisitionReleaseAndCommittedArrow(FrontierRuntime app)
    {
        foreach(int kind in new[]{0,1,2})
        {
            Enemy e=Prepare(app,1,kind,new Point(12,12),new Point(20.2f,12));var g=app.Game;Point initial=e.Position;float timer=e.timer;
            Frames(app,12);Check(Same(e.Position,initial)&&Near(e.timer,timer)&&!e.moving,"Unaware ordinary kind "+kind+" beyond eight remains idle without spending attack recovery");
            int approach=0;while((e.Position-g.Position).Length>7.95f&&approach++<10){Walk(KeyCode.A);Update(app);}Input.Clear();
            Check(approach<10&&e.moving&&(e.Position-initial).Length>.001f,"Real A movement inside eight acquires ordinary kind "+kind);
            int escape=0;while((e.Position-g.Position).Length<=10.2f&&escape++<150){Point before=e.Position,player=g.Position;Walk(KeyCode.D);Update(app);if((before-g.Position).Length<=10)Check((e.Position-before).Length>.001f&&e.moving,"Acquired ordinary kind "+kind+" must keep pursuing throughout the 8-to-10 retain band");Check((e.Position-before).Length<=(kind==0?.0421f:.0301f)&&(g.Position-player).Length<=.0901f&&(bool)Call(g,"Fits",e.Position)&&g.CanStand(g.Position),"Actual D escape retains both original speeds and swept terrain");}Input.Clear();
            Check(escape<150,"Actual held D can leave the finite ten-unit pursuit band");Point released=e.Position;timer=e.timer;Frames(app,12);Check(Same(e.Position,released)&&Near(e.timer,timer)&&!e.moving,"Beyond ten, awareness releases and idle clocks freeze");Draw(app,"kind-"+kind+"-released");
            int back=0;while((e.Position-g.Position).Length>9.5f&&back++<30){Walk(KeyCode.A);Update(app);Check(Same(e.Position,released),"Returning from release cannot reacquire while still beyond eight");}Input.Clear();
            float separation=(e.Position-g.Position).Length;Check(separation>9.3f&&separation<=9.5f,"Real A return stops within the unacquired 8-to-10 band");Frames(app,12);Check(Same(e.Position,released)&&Near(e.timer,timer),"Released ordinary kind "+kind+" stays unaware at 9.5 instead of reactivating inside retain radius");
            back=0;while((e.Position-g.Position).Length>7.95f&&back++<30){Walk(KeyCode.A);Update(app);}Input.Clear();Check(back<30&&e.moving&&!Same(e.Position,released),"Only a real return inside eight reacquires ordinary kind "+kind);Gait(app,e);
            Draw(app);Check(GUI.commands.Exists(x=>x.Contains("\"text\":\"SPACE\""))&&GUI.commands.Exists(x=>x.Contains("\"text\":\"Q\"")),"Actual existing combat HUD retains Q/SPACE action input labels during reacquired pursuit");
        }
        Enemy archer=Prepare(app,1,1,new Point(12,12),new Point(18.8f,12));var game=app.Game;archer.timer=0;Update(app);
        Check(archer.state==1&&Near(archer.timer,.65f),"Original seven-unit archer preparation captures normally");Point origin=archer.Position,target=archer.target,facing=archer.facing;Set(app,"camera",new Point(18,12));string lane=WarningGeometry(app,archer);bool crossed=false;
        for(int i=0;i<32;i++)
        {
            Walk(KeyCode.D);if(i==0)Input.keys.Add(KeyCode.Space);Update(app);Input.Clear();Set(app,"camera",new Point(18,12));crossed|=(archer.Position-game.Position).Length>10;
            Check(archer.state==1&&game.projectiles.Count==0&&Same(archer.Position,origin)&&Same(archer.target,target)&&Same(archer.facing,facing)&&Near(archer.timer,.65f-(i+1)*.02f,.002f)&&WarningGeometry(app,archer)==lane,"Real D/Space crossing of retention radius cannot interrupt, retarget, refacing, move or release an already committed warning");
            if(i==20)Draw(app,"archer-committed-beyond-ten");
        }
        Check(crossed&&game.save.energy<100&&game.dashCooldown>0,"Actual charged Space and held D cross ten during captured windup");Update(app);
        Check(archer.state==0&&Near(archer.timer,1.4f)&&Near(archer.releaseTime,.24f)&&game.projectiles.Count==1,"Original .65-second boundary releases exactly one committed arrow outside pursuit band");Projectile shot=game.projectiles[0];Point muzzle=GameCore.WeaponOrigin(origin,1,facing);
        Check(!shot.friendly&&shot.visual=="enemyArrow"&&Near(shot.velocity.Length,5)&&Near(shot.damage,10)&&Same(shot.velocity.Normalized,(target-muzzle).Normalized),"Committed arrow retains original captured heading, five speed and ten damage");Draw(app,"archer-committed-release");
        Console.WriteLine("PASS actual A/D acquisition/release/re-entry for all three ordinary kinds, unchanged real Q/SPACE HUD path, and real D/Space crossing ten preserves committed archer warning/release/heading/speed/damage; NATIVE_UNVERIFIED");
    }
    private static void Append(StringBuilder text,object value,HashSet<object> seen)
    {
        if(value==null){text.Append("null;");return;}Type type=value.GetType();if(type.IsPrimitive||type.IsEnum||value is string||value is decimal){text.Append(Convert.ToString(value,CultureInfo.InvariantCulture)).Append(';');return;}if(!type.IsValueType&&!seen.Add(value)){text.Append("ref;");return;}var seq=value as System.Collections.IEnumerable;if(seq!=null){foreach(object item in seq)Append(text,item,seen);text.Append("end;");return;}var fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);Array.Sort(fields,(a,b)=>string.CompareOrdinal(a.Name,b.Name));foreach(var f in fields){text.Append(f.Name).Append('=');Append(text,f.GetValue(value),seen);}
    }
    private static string State(object value){var text=new StringBuilder();Append(text,value,new HashSet<object>());return text.ToString();}
    private static string PausedState(GameCore g){return State(g.save)+State(g.enemies)+State(g.projectiles)+State(g.playerAction)+State(g.effects)+State(new[]{g.attackCooldown,g.skillCooldown,g.dashCooldown,g.dashTime,g.invulnerable,g.hitStop,g.guardTime,g.walkDistance});}
    private static string Files(){var text=new StringBuilder();var paths=Directory.GetFiles(Application.persistentDataPath,"*",SearchOption.AllDirectories);Array.Sort(paths,StringComparer.Ordinal);foreach(string path in paths)text.Append(path).Append(':').Append(Convert.ToBase64String(File.ReadAllBytes(path))).Append(';');return text.ToString();}
    private static Enemy AcquiredOutsideEight(FrontierRuntime app,int actorId=0)
    {
        Enemy e=Prepare(app,1,0,null,null,actorId);Update(app);int frames=0;while((e.Position-app.Game.Position).Length<9.3f&&frames++<80){Walk(KeyCode.D);Update(app);}Input.Clear();Check(frames<80&&(e.Position-app.Game.Position).Length>9.2f&&(e.Position-app.Game.Position).Length<9.5f&&e.moving,"Actual D creates a still-pursuing actor in the retain-only band");return e;
    }
    private static void SavePauseAndWorldReplacement(FrontierRuntime app)
    {
        Enemy e=AcquiredOutsideEight(app);var g=app.Game;var art=Get<PixelArt>(app,"art");Call(art,"Glow");Draw(app);var owned=Get<List<Texture2D>>(art,"owned");var cache=Get<Dictionary<string,Texture2D>>(art,"cache");int owns=owned.Count,caches=cache.Count;string files=Files();
        Matrix4x4 matrix=Matrix4x4.TRS(new Vector3(19,31,0),Quaternion.Euler(0,0,17),new Vector3(1.4f,1.4f,1));Color tint=new Color(.3f,.5f,.7f,.8f);
        foreach(bool motion in new[]{false,true})foreach(int fx in new[]{0,2})foreach(EventType evt in new[]{EventType.Layout,EventType.Repaint})
        {
            g.save.reducedMotion=motion;g.save.effectsQuality=fx;Event.current.type=evt;string before=State(g);float clock=Get<float>(app,"clock");GUI.matrix=matrix;GUI.color=tint;GUI.enabled=false;Draw(app);
            Check(State(g)==before&&Near(Get<float>(app,"clock"),clock)&&Same(GUI.matrix,matrix)&&Same(GUI.color,tint)&&!GUI.enabled,"Actual repeated OnGUI restores incoming GUI state and cannot alter any Core/save/awareness field or simulation clock");
        }
        Check(owns==owned.Count&&caches==cache.Count&&(bool)Call(app,"Save")&&Files()==files,"Pursuit uses existing art ownership and review save isolation without files/cache growth");GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;
        foreach(string boundary in new[]{"menu","application","focus","review"})
        {
            e=AcquiredOutsideEight(app);g=app.Game;
            if(boundary=="menu")Press(app,KeyCode.Escape);else if(boundary=="application")Call(app,"OnApplicationPause",true);else if(boundary=="focus")Call(app,"OnApplicationFocus",false);else app.VisualReviewPaused=true;
            string before=PausedState(g);Frames(app,8);Draw(app);Check(PausedState(g)==before,"Actual "+boundary+" pause freezes all enemy/awareness/save fields and combat clocks");
            if(boundary=="menu")Press(app,KeyCode.Escape);else if(boundary=="application")Call(app,"OnApplicationPause",false);else if(boundary=="focus")Press(app,KeyCode.Escape);else app.VisualReviewPaused=false;
            Point foot=e.Position;Update(app);Check(!Same(e.Position,foot)&&e.moving&&(e.Position-g.Position).Length>8,"Actual "+boundary+" resume retains this live actor's awareness outside acquisition range");
        }
        e=AcquiredOutsideEight(app);g=app.Game;e.hp=31.25f;g.save.coins=313;g.save.shards=27;g.save.xp=19;g.save.flags.Add("old_campaign_sentinel");g.PrepareSave();Check(SaveStore.Write(g.save,0),"Actual checksum writer persists legal moved feet and staged partial HP");string warning;SaveData saved=SaveStore.Read(0,out warning);Check(saved!=null&&warning==null&&saved.worldEnemies.Length==1,"Actual checksum reader restores exactly one world actor");EnemyRecord record=saved.worldEnemies[0];
        Check(record.id==e.id&&record.kind==0&&!record.boss&&Near(record.x,e.x)&&Near(record.y,e.y)&&Near(record.hp,31.25f),"Canonical snapshot retains identity, legal moved feet and HP");
        var fields=typeof(EnemyRecord).GetFields();var names=new List<string>();foreach(var f in fields)names.Add(f.Name);names.Sort(StringComparer.Ordinal);Check(string.Join(",",names.ToArray())=="boss,hp,id,kind,maxHp,x,y","EnemyRecord retains exact existing seven-field schema");string raw=JsonUtility.ToJson(saved);Check(!raw.Contains("pursu")&&!raw.Contains("aware")&&!raw.Contains("moveTime")&&!raw.Contains("backpedaling"),"Actual saved JSON adds no awareness, pursuit or gait state");
        foreach(int version in new[]{1,2})
        {
            SaveData old=JsonUtility.FromJson<SaveData>(raw);old.version=version;File.WriteAllText(SaveStore.PathFor(1),JsonUtility.ToJson(old));SaveData legacy=SaveStore.Read(1,out warning);Check(legacy!=null&&warning==null,"Actual raw v"+version+" reader accepts unchanged schema");Enemy prior=e;int generation=g.WorldGeneration;g.Load(legacy);g.Close();e=g.enemies.Find(x=>x.id==record.id);
            Check(e!=null&&!object.ReferenceEquals(e,prior)&&g.WorldGeneration>generation&&Same(e.Position,new Point(record.x,record.y))&&Near(e.hp,record.hp)&&e.state==0&&!e.moving&&Near(e.moveTime,0),"Actual legacy load replaces actor identity and preserves HP/feet with fresh transient state");Point foot=e.Position;float timer=e.timer;Frames(app,10);Check(Same(e.Position,foot)&&Near(e.timer,timer)&&(e.Position-g.Position).Length>8,"Fresh loaded actor does not inherit old live actor awareness in retain-only range");Check(g.save.coins==313&&g.save.shards==27&&g.save.xp==19&&g.Has("old_campaign_sentinel"),"Load and reset grant no extra rewards or invented story progress");
        }
        var envelope=JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(SaveStore.PathFor(0)));Check(envelope.payload!=null&&envelope.checksum.Length==64,"Existing checksum envelope remains SHA256");envelope.checksum="BAD";File.WriteAllText(SaveStore.PathFor(2),JsonUtility.ToJson(envelope));Check(SaveStore.Read(2,out warning)==null&&warning!=null&&warning.Contains("checksum"),"Existing checksum still rejects tampered state");
        // Reuse a genuinely acquired shipped hunter ID whose canonical spawn
        // is outside ten. Actual D input then approaches its replacement into
        // the retain-only band without first acquiring that new actor.
        Enemy discarded=AcquiredOutsideEight(app,4);g=app.Game;g.Enter(0);Check(!g.enemies.Contains(discarded),"Actual travel discards previous world actors");g.Enter(1);Check(g.enemies.Count>0&&!g.enemies.Contains(discarded),"Actual room re-entry creates new actors rather than reviving previous awareness");Enemy fresh=g.enemies.Find(x=>x.id==discarded.id);Check(fresh!=null&&(fresh.Position-g.Position).Length>10,"Shipped re-entry supplies same enemy ID beyond both awareness radii");g.enemies.RemoveAll(x=>x!=fresh);Point freshFoot=fresh.Position;float freshTimer=fresh.timer;int approach=0;
        while((fresh.Position-g.Position).Length>9.5f&&approach++<250){Walk(KeyCode.D);Update(app);Check((fresh.Position-g.Position).Length>8&&Same(fresh.Position,freshFoot)&&Near(fresh.timer,freshTimer),"Actual held D must approach fresh travelled actor without entering acquisition radius or waking it");}Input.Clear();Console.WriteLine("TRAVEL REENTRY approach_frames="+approach+" player="+g.Position.x+","+g.Position.y+" enemy="+fresh.x+","+fresh.y+" distance="+(fresh.Position-g.Position).Length);Check(approach<250&&(fresh.Position-g.Position).Length>9.3f&&(fresh.Position-g.Position).Length<=9.5f,"Actual post-travel input reaches retain-only band");Frames(app,10);Check(Same(fresh.Position,freshFoot)&&Near(fresh.timer,freshTimer),"Fresh travelled actor cannot inherit discarded actor awareness outside eight");
        Console.WriteLine("PASS full OnGUI Core/save/GUI/cache ownership; actual Escape/application/focus/review freeze and live resume; checksum/raw v1v2 preserve seven-field actor HP/feet and rewards while new load/travel actors reset awareness; NATIVE_UNVERIFIED");
    }
    private static void CaptureFixtures(FrontierRuntime app)
    {
        Enemy e=Prepare(app,2,0,new Point(3.5f,10.5f),new Point(8.5f,4.5f));app.Game.invulnerable=1000;e.timer=0;Draw(app,"water-start");
        for(int i=1;i<=350;i++){Update(app);Set(app,"camera",new Point(7,7));if(i==8||i==30||i==90||i==150||i==230||i==350)Draw(app,"water-frame-"+i);}
        // Captures alone are deliberately available against old production even
        // when the semantic regression fails. They do not assert new behavior.
        e=Prepare(app,1,1,new Point(12,12),new Point(18.8f,12));e.timer=0;Update(app);Draw(app,"archer-warning");
        for(int i=0;i<33;i++){Walk(KeyCode.D);if(i==0)Input.keys.Add(KeyCode.Space);Update(app);Input.Clear();Set(app,"camera",new Point(18,12));if(i==20)Draw(app,"archer-committed-beyond-ten");if(i==32)Draw(app,"archer-committed-release");}
        Console.WriteLine("CAPTURE "+captures+" actual OnGUI command streams; legal feet are staged once, then real Update advances shipped water and captured-arrow fixtures; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-enemy-pursuit-"+Guid.NewGuid());Application.persistentDataPath=directory;captureFolder=args.Length>0&&args[0]!="-"?args[0]:null;capturePrefix=args.Length>1?args[1]:"qa-enemy-pursuit";bool only=args.Length>2&&args[2]=="capture-only";int failed=0;var app=new FrontierRuntime();
        try
        {
            Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"starfall-v1.json"),"{\"version\":1,\"profession\":0}");File.WriteAllText(Path.Combine(directory,"starfall-slot-1.json.bak"),"existing-slot-backup-sentinel");Call(app,"Awake");Check(app.Game!=null,"Production Awake loads campaign");
            if(!only)foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{WaterDetourGaitAndMelee,AcquisitionReleaseAndCommittedArrow,SavePauseAndWorldReplacement})try{test(app);}catch(Exception ex){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(ex.InnerException??ex).Message);}
            if(captureFolder!=null&&only)CaptureFixtures(app);app.EndVisualReview();Call(app,"OnDestroy");
        }
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        if(only){Console.WriteLine("Capture-only: semantic checks skipped; NATIVE_UNVERIFIED");return 0;}Console.WriteLine("Runtime enemy pursuit: "+(3-failed)+"/3 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
