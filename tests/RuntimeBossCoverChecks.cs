// Production DrawEnemyActor, OnGUI, Update and SaveStore using offline Unity
// declarations. Captures cannot certify native import, GPU playback or feel.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Starfall;
using UnityEngine;

public static class RuntimeBossCoverChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    // Frozen from an immutable cafe792 archive; omit only the targeted live
    // Boss body/flash alpha in covered samples, never other tint/geometry.
    private const string PreservationDigest="ff8e4108ee705d3d434429363ef1e0a4e8412904645ae65801c013b2d4a0c7bc";
    private static string captureFolder,capturePrefix;
    private static int captures;
    private static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
    private static object Call(object app,string name,params object[] args)
    {var method=app.GetType().GetMethod(name,Private|BindingFlags.Public);Check(method!=null,"Existing production entry point: "+name);return method.Invoke(app,args);}
    private static T Get<T>(object app,string name){return (T)app.GetType().GetField(name,Private).GetValue(app);}
    private static void Set(object app,string name,object value){app.GetType().GetField(name,Private).SetValue(app,value);}
    private static bool Near(float a,float b,float tolerance=.001f){return Math.Abs(a-b)<tolerance;}
    private static bool Same(Rect a,Rect b){return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height);}
    private static bool Same(Color a,Color b){return Near(a.r,b.r)&&Near(a.g,b.g)&&Near(a.b,b.b)&&Near(a.a,b.a);}
    private static bool Same(Matrix4x4 a,Matrix4x4 b){return Near(a.m00,b.m00)&&Near(a.m01,b.m01)&&Near(a.m03,b.m03)&&Near(a.m10,b.m10)&&Near(a.m11,b.m11)&&Near(a.m13,b.m13);}
    private static void Clear(){GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();}
    private static Enemy Prepare(FrontierRuntime app,int room=2,int profession=0)
    {
        Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;
        Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;Event.current.type=EventType.Repaint;
        app.BeginVisualReview(profession,room,true);app.VisualReviewPaused=false;var g=app.Game;
        Enemy boss=g.enemies.Find(e=>e.boss);Check(boss!=null,"Shipped mature Boss encounter spawns in room "+room);g.enemies.RemoveAll(e=>e!=boss);
        g.save.companion=-1;g.save.effectsQuality=0;g.save.reducedMotion=true;g.save.showGuidance=false;g.save.english=true;
        g.hazards.Clear();g.projectiles.Clear();g.effects.Clear();g.fallen.Clear();g.toastTime=0;g.playerFlash=0;
        Set(app,"camera",boss.Position);Set(app,"clock",0f);Set(app,"combatHudTime",0f);Set(app,"drawingOutline",false);
        Set(app,"applicationPaused",false);Set(app,"pointerSeen",false);Set(app,"controllerUsed",false);Call(app,"Resize");
        Pose(boss,0,false);return boss;
    }
    private static void Pose(Enemy e,int pose,bool flip)
    {
        e.Face(new Point(flip?-1:1,0));e.moving=pose==1;e.state=pose==2?1:0;e.moveTime=.9f;
        e.hitTime=pose==4?.12f:0;e.flash=pose==4?.06f:0;e.releaseTime=pose==3?.2f:0;e.timer=.4f;e.windupDuration=.7f;e.target=e.Position;
    }
    private static void Player(FrontierRuntime app,Enemy boss,float x,float y){app.Game.save.x=boss.x+x;app.Game.save.y=boss.y+y;}
    private static List<GUI.TextureCall> EnemyDraw(FrontierRuntime app,Enemy boss)
    {Clear();Call(app,"DrawEnemyActor",boss);return new List<GUI.TextureCall>(GUI.textureCalls);}
    private static bool Body(GUI.TextureCall c)
    {return c.texture.name=="resource:Frontier/Art/Bosses"||c.texture.name=="resource:Frontier/Art/WalkBossA"||c.texture.name=="resource:Frontier/Art/WalkBossB";}
    private static bool Flash(GUI.TextureCall c){return c.texture.name.Contains("/ActorMasks/flash_Bosses");}
    private static float Alpha(FrontierRuntime app,Enemy e)
    {
        var calls=EnemyDraw(app,e);var body=calls.Find(Body);Check(body!=null,"Actual authored Boss body is submitted");
        Check(body.color.a>=.5499f&&body.color.a<=1.0001f,"Boss opacity is bounded at retained .55 floor..1");return body.color.a;
    }
    private static float At(FrontierRuntime app,Enemy e,float x,float y){Player(app,e,x,y);return Alpha(app,e);}
    private static void Boundary(FrontierRuntime app,Enemy e,float x1,float y1,float x2,float y2,string label)
    {float a=At(app,e,x1,y1),b=At(app,e,x2,y2);Check(Math.Abs(a-b)<.007f,"A .002-world "+label+" step cannot pop Boss alpha ("+a+" -> "+b+")");}
    private static void BoundariesAndWitnesses(FrontierRuntime app)
    {
        int bosses=0;
        for(int room=1;room<=8;room++)
        {
            Enemy e=Prepare(app,room);bosses++;
            Check(Near(At(app,e,0,-1),.55f),"Existing fully covered minimum survives");
            Boundary(app,e,1.399f,-1,1.401f,-1,"lateral core");Boundary(app,e,0,-2.699f,0,-2.701f,"rear core");
            Boundary(app,e,0,-.011f,0,-.009f,"front sorting line");Boundary(app,e,0,-.209f,0,-.211f,"front feather core");
            Boundary(app,e,1.3993f,-2.6993f,1.4007f,-2.7007f,"radial rear corner");
            Check(Near(At(app,e,1.8f,-1),.775f,.0002f)&&Near(At(app,e,0,-3.1f),.775f,.0002f),"Lateral/rear .8 support has a cubic halfway alpha");
            Check(Near(At(app,e,1.6f,-1),.6203125f,.0002f)&&Near(At(app,e,2,-1),.9296875f,.0002f),"Cubic quarter/three-quarter samples preserve the retained .55 core");
            float diagonal=.4f/(float)Math.Sqrt(2);Check(Near(At(app,e,1.4f+diagonal,-2.7f-diagonal),.775f,.0002f),"Rear corner support is radial, not square");
            Check(Near(At(app,e,0,-.11f),.775f,.0002f)&&Near(At(app,e,1.8f,-.11f),.8875f,.0002f),"Front .2 inward smoothstep multiplies the outer support");
            foreach(float front in new[]{-.01f,0f,.1f,1f})Check(Near(At(app,e,0,front),1),"A hero already in front/equal sorting depth cannot fade the Boss");
            Check(Near(At(app,e,2.201f,-1),1)&&Near(At(app,e,0,-3.501f),1),"Outer support endpoints restore full opacity");
            Player(app,e,20,20);app.Game.companionPosition=e.Position+new Point(0,-1);app.Game.save.companion=-1;
            Check(Near(Alpha(app,e),1),"Inactive companion's stale feet cannot fade a Boss");app.Game.save.companion=0;
            Check(Near(Alpha(app,e),.55f),"Actually selected companion alone receives the same .55 cover floor");
            app.Game.companionPosition=e.Position+new Point(1.8f,-1);Player(app,e,1.8f,-1);Check(Near(Alpha(app,e),.775f,.0002f),"Two witnesses choose max, never stack alpha");
            app.Game.companionPosition=e.Position+new Point(0,-1);Check(Near(Alpha(app,e),.55f),"Stronger companion wins");
            Player(app,e,20,20);app.Game.companionPosition=e.Position+new Point(0,0);Check(Near(Alpha(app,e),1),"An active companion already in front cannot reveal a Boss");
        }
        Check(bosses==8,"All eight mature Boss arts are exercised");
        Console.WriteLine("PASS eight mature Bosses: .002 lateral/rear/front/corner continuity, .55 minimum, .8 radial/.2 inward cubic support and max nonstacking active companion witnesses; NATIVE_UNVERIFIED");
    }
    private static void PosesRoutingAndPolicies(FrontierRuntime app)
    {
        int fixtures=0;
        for(int route=0;route<10;route++)
        {
            int room=route<8?route+1:route==8?1:6;Enemy e=Prepare(app,room);
            if(route>=8){e.id=route==8?GameCore.SignalWardenId:GameCore.CommissionGuardianId;e.kind=3;}
            for(int pose=0;pose<5;pose++)foreach(bool flip in new[]{false,true})foreach(int phase in new[]{1,2,3})
            {
                Pose(e,pose,flip);e.hp=e.maxHp*(phase==1?.9f:phase==2?.5f:.2f);Player(app,e,1.8f,-1);var calls=EnemyDraw(app,e);var body=calls.Find(Body);fixtures++;
                Check(body!=null&&Near(body.color.a,.775f,.0002f),"Actual idle/walk/windup/release/hit frame uses one cover tint across facing/HP phases");
                Check(body.color.r==(pose==4?1.55f:route==8?.8f:1)&&Near(body.color.b,pose==4?1.55f:route==8?.88f:1),"Original hit and Signal RGB tint survive");
                var flash=calls.Find(Flash);Check((pose==4)==(flash!=null),"Only original hit state submits its authored flash mask");
                if(flash!=null)Check(Near(flash.color.a,.06f/.09f*.86f*.775f,.0002f),"Hit mask alpha multiplies actual Boss body cover exactly");
                if(pose==2){var emission=calls.Find(c=>c.texture.name=="resource:Frontier/Art/Effects");Check(emission!=null&&Near(emission.color.a,(1-.4f/.7f)*.6f,.0002f),"Original windup emission alpha is independent of cover");}
                Set(app,"drawingOutline",true);calls=EnemyDraw(app,e);Set(app,"drawingOutline",false);
                Check(calls.Count==1&&calls[0].texture.name.Contains("/ActorMasks/outline_")&&Near(calls[0].color.a,.9f),"HUD silhouette retains fixed .9 opacity without shadow/emission/flash");
            }
            Pose(e,0,false);Player(app,e,1.8f,-1);float reference=Alpha(app,e);
            foreach(bool reduced in new[]{false,true})foreach(int fx in new[]{0,2})foreach(float time in new[]{0f,9.7f})foreach(string mode in new[]{"world","pause"})
            {app.Game.save.reducedMotion=reduced;app.Game.save.effectsQuality=fx;app.Game.mode=mode;Set(app,"clock",time);Check(Near(Alpha(app,e),reference),"Cover has no time/FX/reduced-motion/pause dependency");}
            Player(app,e,20,20);Check(Near(Alpha(app,e),1),"Leaving cover restores alpha without reveal history");Player(app,e,1.8f,-1);Check(Near(Alpha(app,e),reference),"Re-entry is stateless");
        }
        Console.WriteLine("PASS "+fixtures+" actual Boss pose/facing/HP draw cases, Signal/Commission art routing, flash multiplication, unchanged emission, fixed .9 HUD outline and time/FX/motion/pause/history independence; NATIVE_UNVERIFIED");
    }
    private static void Draw(FrontierRuntime app,string capture=null)
    {
        Clear();Call(app,"OnGUI");if(captureFolder!=null&&capture!=null)
        {var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();GUI.Export(captureFolder,capturePrefix+"-"+capture);captures++;GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);}
    }
    private static bool SameCall(GUI.TextureCall a,GUI.TextureCall b,bool alpha=true)
    {return a.texture.name==b.texture.name&&Same(a.rect,b.rect)&&Same(a.uv,b.uv)&&Same(a.matrix,b.matrix)&&Near(a.color.r,b.color.r)&&Near(a.color.g,b.color.g)&&Near(a.color.b,b.color.b)&&(!alpha||Near(a.color.a,b.color.a));}
    private static void Update(FrontierRuntime app){Call(app,"Update");Input.keys.Clear();}
    private static void ActualInputRoute(FrontierRuntime app,bool validate=true,bool capture=false)
    {
        Enemy e=Prepare(app,2);var g=app.Game;e.stun=1000; // Explicit staging freezes genuine spawned AI, never terrain or movement.
        // The shrine's eastern side is water. Its real northern approach is
        // walkable; cross the rear feather there without bypassing collision.
        Point start=e.Position+new Point(0,-3.7f),end=e.Position+new Point(0,-2.2f);
        Check(g.CanStand(start)&&g.CanStand(end)&&app.SetVisualReviewPosition(start)&&(g.Position-start).Length<.001f,"Staged genuine Boss route begins at exact legal terrain feet");
        Point bossPosition=e.Position;float total=g.save.totalTime,maxJump=0,previous=Alpha(app,e);int frames=0;bool intermediate=false;
        if(capture)Draw(app,"walk-before");
        while((g.Position-end).Length>.1f&&frames++<500)
        {
            Point direction=g.DirectionTo(g.Position,end);Input.axes["Horizontal"]=direction.x;Input.axes["Vertical"]=-direction.y;Input.held.Clear();
            if(direction.x<-.05f)Input.held.Add(KeyCode.A);if(direction.x>.05f)Input.held.Add(KeyCode.D);if(direction.y<-.05f)Input.held.Add(KeyCode.W);if(direction.y>.05f)Input.held.Add(KeyCode.S);
            Point before=g.Position;Update(app);Check(g.CanStand(g.Position)&&(g.Position-before).Length<.2f&&g.mode=="world","Actual held WASD/Update preserves legal collision and movement speed");
            Check((e.Position-bossPosition).Length<.001f,"Only the explicitly staged genuine Boss remains frozen");
            float alpha=Alpha(app,e);var submitted=EnemyDraw(app,e);Draw(app);var body=submitted.Find(Body);
            Check(GUI.textureCalls.Exists(c=>SameCall(c,body)),"Actual ordered OnGUI submits the same covered Boss frame during walking");
            intermediate|=alpha>.58f&&alpha<.97f;maxJump=Math.Max(maxJump,Math.Abs(alpha-previous));previous=alpha;
        }
        Input.Clear();Check(frames<500&&(g.Position-end).Length<=.1f&&g.save.totalTime>total,"Actual held WASD reaches the waypoint without any player position writes");
        if(validate)Check(intermediate&&maxJump<.14f&&Near(Alpha(app,e),.55f),"Ordinary movement crosses cubic intermediate alpha instead of old .45 whole-Boss pop");
        if(capture)Draw(app,"walk-covered");Console.WriteLine((validate?"PASS":"CAPTURE")+" actual held WASD/Update beside explicitly frozen genuine room2 Boss: "+frames+" legal frames, intermediate="+intermediate+", largest adjacent alpha step="+maxJump+"; NATIVE_UNVERIFIED");
    }
    private static void KeyboardMovement(FrontierRuntime app){ActualInputRoute(app);}
    private static void N(StringBuilder text,float n){text.Append(n.ToString("R",CultureInfo.InvariantCulture)).Append(',');}
    private static void Record(StringBuilder text,string name,List<GUI.TextureCall> calls,bool excludeBodyAlpha=false)
    {
        text.Append(name).Append(':');foreach(var c in calls)
        {text.Append(c.texture.name).Append('|');foreach(float n in new[]{c.rect.x,c.rect.y,c.rect.width,c.rect.height,c.uv.x,c.uv.y,c.uv.width,c.uv.height,c.color.r,c.color.g,c.color.b,c.matrix.m00,c.matrix.m01,c.matrix.m03,c.matrix.m10,c.matrix.m11,c.matrix.m13})N(text,n);if(!excludeBodyAlpha||!Body(c)&&!Flash(c))N(text,c.color.a);text.Append(';');}text.AppendLine();
    }
    private static string Digest(string text){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}
    private static void Preservation(FrontierRuntime app,bool validate)
    {
        var witness=new StringBuilder();int cases=0;
        for(int room=1;room<=8;room++)
        {
            Enemy e=Prepare(app,room);var g=app.Game;
            for(int pose=0;pose<5;pose++)foreach(bool flip in new[]{false,true})foreach(bool covered in new[]{false,true})
            {
                Pose(e,pose,flip);Player(app,e,covered?1.8f:20,covered?-1:20);Record(witness,"boss/"+room+"/"+pose+"/"+flip+"/"+covered,EnemyDraw(app,e),covered);cases++;
                Set(app,"drawingOutline",true);Record(witness,"outline",EnemyDraw(app,e));Set(app,"drawingOutline",false);
                Clear();Call(app,"DrawFallen",new FallenActor{actor=e,room=room,life=.9f,duration=1.4f});Record(witness,"fallen",new List<GUI.TextureCall>(GUI.textureCalls));
            }
        }
        Prepare(app);for(int kind=0;kind<3;kind++)for(int pose=0;pose<5;pose++)foreach(bool flip in new[]{false,true})
        {var e=new Enemy(94000+kind,kind,15,14,100);Pose(e,pose,flip);Player(app,e,0,-1);Record(witness,"ordinary/"+kind+"/"+pose+"/"+flip,EnemyDraw(app,e));cases++;}
        // Archive has ordinary guardians, so it must not use the Boss fixture.
        app.BeginVisualReview(0,GameCore.ArchiveRoom);app.Game.enemies.Clear();app.Game.save.companion=-1;
        Check(app.Game.save.room==GameCore.ArchiveRoom&&!app.Game.practicing,"Ordinary Archive guardian fixture enters the actual dungeon");
        Set(app,"camera",new Point(15,14));Set(app,"drawingOutline",false);
        for(int kind=0;kind<3;kind++){var e=new Enemy(74000+kind,kind,15,14,100);Check(app.Game.IsArchiveEnemyId(e.id),"Fixture uses a real reserved Archive guardian identity");Player(app,e,0,-1);Record(witness,"archive/"+kind,EnemyDraw(app,e));cases++;}
        Enemy dummy=Prepare(app);app.Game.practicing=true;Player(app,dummy,0,-1);Record(witness,"safe-dummy",EnemyDraw(app,dummy));
        string digest=Digest(witness.ToString());Console.WriteLine("Boss/ordinary/fallen/outline/shadow/emission preservation witness: "+cases+" cases; sha256="+digest);
        if(validate)Check(digest==PreservationDigest,"Non-target geometry/UV/frame/RGB/order, ordinary/fallen/outline/shadow/emission or full-opacity Boss alpha differs from frozen cafe792");
    }
    private static void AppendState(StringBuilder text,object value,HashSet<object> seen)
    {
        if(value==null){text.Append("null;");return;}Type type=value.GetType();if(type.IsPrimitive||type.IsEnum||value is string||value is decimal){text.Append(Convert.ToString(value,CultureInfo.InvariantCulture)).Append(';');return;}
        if(!type.IsValueType&&!seen.Add(value)){text.Append("ref;");return;}var sequence=value as System.Collections.IEnumerable;if(sequence!=null){foreach(object item in sequence)AppendState(text,item,seen);text.Append("end;");return;}
        var fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);Array.Sort(fields,(a,b)=>string.CompareOrdinal(a.Name,b.Name));foreach(var field in fields){text.Append(field.Name).Append('=');AppendState(text,field.GetValue(value),seen);}
    }
    private static string State(GameCore game){var text=new StringBuilder();AppendState(text,game,new HashSet<object>());return text.ToString();}
    private static string Files(){var text=new StringBuilder();var paths=Directory.GetFiles(Application.persistentDataPath,"*",SearchOption.AllDirectories);Array.Sort(paths,StringComparer.Ordinal);foreach(string p in paths)text.Append(p).Append(':').Append(Convert.ToBase64String(File.ReadAllBytes(p))).Append(';');return text.ToString();}
    private static void HudFixture(FrontierRuntime app,string capture=null,bool validate=true)
    {
        Enemy e=Prepare(app);Player(app,e,0,-3.1f);Check(app.Game.CanStand(app.Game.Position),"HUD witness uses real northern approach terrain");Pose(e,2,false);Point pixel=new Point(620,754);Set(app,"camera",e.Position-new Point((pixel.x-640)/36,(pixel.y-390)/36));Set(app,"combatHudTime",3f);
        var expected=EnemyDraw(app,e).Find(Body);Draw(app,capture);if(!validate)return;
        int body=GUI.textureCalls.FindIndex(c=>SameCall(c,expected)),panel=GUI.textureCalls.FindIndex(c=>c.texture==Texture2D.whiteTexture&&Same(c.rect,new Rect(426,730,428,54)));
        int outline=GUI.textureCalls.FindLastIndex(c=>c.texture.name.Contains("/ActorMasks/outline_Bosses")&&Near(c.color.a,.9f));
        Check(body>=0&&panel>body&&outline>panel,"Covered world Boss retains its fixed .9 silhouette above the actual combat HUD");
    }
    private static void IsolationPreservationAndHud(FrontierRuntime app)
    {
        Preservation(app,true);HudFixture(app);var g=app.Game;var art=Get<PixelArt>(app,"art");Call(art,"Glow");Draw(app);
        string before=State(g),files=Files();float clock=Get<float>(app,"clock");int owned=Get<List<Texture2D>>(art,"owned").Count,cache=Get<Dictionary<string,Texture2D>>(art,"cache").Count;
        var matrix=Matrix4x4.TRS(new Vector3(19,31,0),Quaternion.Euler(0,0,17),new Vector3(1.4f,1.4f,1));var color=new Color(.3f,.5f,.7f,.8f);
        foreach(EventType evt in new[]{EventType.Layout,EventType.Repaint,EventType.Layout,EventType.Repaint})
        {Event.current.type=evt;GUI.matrix=matrix;GUI.color=color;GUI.enabled=false;Draw(app);Check(State(g)==before&&ReferenceEquals(app.Game,g)&&Near(Get<float>(app,"clock"),clock),"Drawing leaves complete Core/save/encounter state and presentation clock unchanged");Check(Same(GUI.matrix,matrix)&&Same(GUI.color,color)&&!GUI.enabled,"OnGUI restores incoming matrix/color/enabled state");}
        Check(Get<List<Texture2D>>(art,"owned").Count==owned&&Get<Dictionary<string,Texture2D>>(art,"cache").Count==cache,"Repeated cover draw cannot create owned textures or cache entries");
        Check((bool)Call(app,"Save")&&Files()==files,"Visual-review drawing/Save preserves original save/slot/backup bytes");
        GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;
        Console.WriteLine("PASS frozen Boss/ordinary/fallen/dummy/outline/shadow/emission witness, real post-HUD fixed silhouette, complete Core/save/GUI/cache/clock isolation; NATIVE_UNVERIFIED");
    }
    private static void CapturePosition(FrontierRuntime app,Enemy boss)
    {
        // Use the same legal candidate on baseline and current code, rather
        // than choosing a position from the new alpha and biasing the pair.
        foreach(Point offset in new[]{new Point(1.8f,-1),new Point(-1.8f,-1),new Point(0,-3.1f),new Point(1,-.11f),new Point(-1,-.11f)})
        {
            Point foot=boss.Position+offset;if(!app.Game.CanStand(foot))continue;
            Check(app.SetVisualReviewPosition(foot)&&(app.Game.Position-foot).Length<.001f,"Preview preserves exact legal foot position");return;
        }
        throw new Exception("No legal representative Boss cover sample in room "+app.Game.save.room);
    }
    private static void Captures(FrontierRuntime app)
    {
        for(int room=1;room<=8;room++){Enemy e=Prepare(app,room);CapturePosition(app,e);Draw(app,"mature-"+room);}
        foreach(bool companion in new[]{false,true}){Enemy e=Prepare(app);Player(app,e,0,companion?-4.5f:1.5f);app.Game.save.companion=companion?0:-1;app.Game.companionPosition=e.Position+new Point(0,-1);Check(app.Game.CanStand(app.Game.Position)&&(!companion||app.Game.CanStand(app.Game.companionPosition)),"Companion/front previews retain legal terrain feet");Draw(app,companion?"companion-only":"already-in-front");}
        foreach(int route in new[]{8,9}){Enemy e=Prepare(app,route==8?1:6);e.id=route==8?GameCore.SignalWardenId:GameCore.CommissionGuardianId;e.kind=3;CapturePosition(app,e);Draw(app,route==8?"signal":"commission");}
        ActualInputRoute(app,false,true);HudFixture(app,"hud-lowfx",false);app.Game.save.effectsQuality=2;app.Game.save.reducedMotion=false;Screen.width=1920;Screen.height=1080;Draw(app,"hud-rich");
        Check(captures==16,"Exactly16 comparable production OnGUI captures");Console.WriteLine("CAPTURE16 actual OnGUI fixtures: eight mature Boss arts, selected companion, front exclusion, two routed Boss arts, staged genuine encounter walking and two HUD policies; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-boss-cover-"+Guid.NewGuid());Application.persistentDataPath=directory;
        captureFolder=args.Length>0&&args[0]!="-"?args[0]:null;capturePrefix=args.Length>1?args[1]:"qa-boss-cover";bool only=args.Length>2&&args[2]=="capture-only";int failed=0;var app=new FrontierRuntime();
        try
        {
            Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"starfall-v1.json"),"{\"version\":1,\"profession\":0}");File.WriteAllText(Path.Combine(directory,"starfall-slot-1.json.bak"),"existing-slot-backup-sentinel");Call(app,"Awake");Check(app.Game!=null,"Production Awake loads the campaign");
            if(only)Preservation(app,false);else foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{BoundariesAndWitnesses,PosesRoutingAndPolicies,KeyboardMovement,IsolationPreservationAndHud}){try{test(app);}catch(Exception ex){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(ex.InnerException??ex).Message);}}
            if(captureFolder!=null)Captures(app);app.EndVisualReview();Call(app,"OnDestroy");
        }
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        if(only){Console.WriteLine("Capture-only: semantic checks skipped; NATIVE_UNVERIFIED");return 0;}Console.WriteLine("Runtime Boss cover: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
