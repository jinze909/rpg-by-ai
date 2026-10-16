// Actual production OnGUI and Update against offline Unity declarations.
// This does not certify native import, GPU rendering, build or player feel.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Starfall;
using UnityEngine;

public static class RuntimeRegionalAirChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly int[] RegionalRooms={3,6,7};
    private static readonly Color Legacy=new Color(.96f,.87f,.6f,.45f),OldSnow=new Color(.9f,.96f,1,.58f);
    // Frozen from the immutable 67f55a5 OnGUI path; atmosphere calls are excluded.
    private const string WorldDigest="d1b951da4869b263f2a62a2b73342b0c4b944cd6fcf436dc190aaede221f6bac";
    private static string captureFolder,capturePrefix;
    private static int captures;
    private static bool motionCaptures;
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static object Call(object target,string method,params object[] args)
    {var m=target.GetType().GetMethod(method,Private|BindingFlags.Public);Check(m!=null,"Existing production entry point remains "+method);return m.Invoke(target,args);}
    private static void Set(object target,string field,object value){target.GetType().GetField(field,Private).SetValue(target,value);}
    private static T Get<T>(object target,string field){return (T)target.GetType().GetField(field,Private).GetValue(target);}
    private static bool Near(float a,float b,float tolerance=.0005f){return Math.Abs(a-b)<tolerance;}
    private static bool Same(Rect a,Rect b){return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height);}
    private static bool Same(Color a,Color b){return Near(a.r,b.r)&&Near(a.g,b.g)&&Near(a.b,b.b)&&Near(a.a,b.a);}
    private static bool Same(Matrix4x4 a,Matrix4x4 b)
    {return Near(a.m00,b.m00)&&Near(a.m01,b.m01)&&Near(a.m03,b.m03)&&Near(a.m10,b.m10)&&Near(a.m11,b.m11)&&Near(a.m13,b.m13);}
    private static bool Same(GUI.TextureCall a,GUI.TextureCall b)
    {return a.texture==b.texture&&Same(a.rect,b.rect)&&Same(a.uv,b.uv)&&Same(a.color,b.color)&&Same(a.matrix,b.matrix);}
    private static Color RegionalColor(int room){return room==3?new Color(.66f,.62f,.57f):room==6?new Color(.71f,.58f,.38f):new Color(.82f,.91f,.96f);}
    private static bool Palette(GUI.TextureCall c,Color tint)
    {return c.texture==Texture2D.whiteTexture&&Near(c.color.r,tint.r)&&Near(c.color.g,tint.g)&&Near(c.color.b,tint.b)&&c.rect.width<=6&&c.rect.height<=6;}
    private static bool Air(GUI.TextureCall c)
    {return Palette(c,Legacy)||Palette(c,OldSnow)||Palette(c,RegionalColor(3))||Palette(c,RegionalColor(6))||Palette(c,RegionalColor(7));}
    private static void Clear(){GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();}
    private static void Prepare(FrontierRuntime app,int room=3)
    {
        Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;
        Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;Event.current.type=EventType.Repaint;
        app.BeginVisualReview(0,room);app.VisualReviewPaused=false;var g=app.Game;
        Check(g.save.room==room,"Fixture enters the shipped region "+room);g.enemies.Clear();g.hazards.Clear();g.effects.Clear();g.projectiles.Clear();g.fallen.Clear();
        g.save.companion=-1;g.save.reducedMotion=false;g.save.effectsQuality=2;g.save.showGuidance=false;g.save.english=true;g.toastTime=0;g.invulnerable=0;
        Set(app,"clock",2.345f);Set(app,"camera",g.Position);Set(app,"combatHudTime",0f);Set(app,"applicationPaused",false);
        Set(app,"pointerSeen",false);Set(app,"controllerUsed",false);Set(app,"drawingOutline",false);Call(app,"Resize");
    }
    private static List<GUI.TextureCall> Draw(FrontierRuntime app,string capture=null)
    {
        Clear();Call(app,"OnGUI");var all=new List<GUI.TextureCall>(GUI.textureCalls);
        if(captureFolder!=null&&capture!=null)
        {var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();GUI.Export(captureFolder,capturePrefix+"-"+capture);captures++;GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);}
        return all;
    }
    private static List<GUI.TextureCall> Regional(FrontierRuntime app)
    {return Draw(app).FindAll(c=>Palette(c,RegionalColor(app.Game.save.room)));}
    private static bool Same(List<GUI.TextureCall> a,List<GUI.TextureCall> b)
    {if(a.Count!=b.Count)return false;for(int i=0;i<a.Count;i++)if(!Same(a[i],b[i]))return false;return true;}
    private static void Update(FrontierRuntime app,int frames=1){for(int i=0;i<frames;i++){Call(app,"Update");Input.keys.Clear();}}
    private static void RegionalDesignAndLegacyMotes(FrontierRuntime app)
    {
        foreach(int room in RegionalRooms)
        {
            Prepare(app,room);var all=Draw(app);var air=all.FindAll(c=>Palette(c,RegionalColor(room)));
            int expected=room==7?42:room==6?24:16;
            Check(air.Count==expected,"Actual room "+room+" submits its own sparse mineral/sand/snow layers (expected "+expected+", got "+air.Count+")");
            Check(!all.Exists(c=>Palette(c,Legacy)||Palette(c,OldSnow)),"New regions replace forest motes and the old duplicate snow rather than stacking them");
            var dimensions=new HashSet<string>();var alphas=new HashSet<string>();foreach(var c in air){dimensions.Add(c.rect.width+"x"+c.rect.height);alphas.Add(c.color.a.ToString("R",CultureInfo.InvariantCulture));}
            Check(dimensions.Count>=2&&alphas.Count>=2,"Layered regional air varies cluster size and depth opacity");
        }
        foreach(int room in new[]{0,1,2,4,5,8,9,10,GameCore.ReachRoom})foreach(float time in new[]{0f,2.345f,91.7f})
        {
            Prepare(app,room);Set(app,"clock",time);Set(app,"camera",new Point(18.125f,12.75f));var all=Draw(app);var old=all.FindAll(c=>Palette(c,Legacy));
            Check(old.Count==18,"Unchanged room "+room+" retains exactly18 authored legacy motes");
            for(int i=0;i<18;i++)
            {
                // Immutable pre-change contract, not the new region algorithm.
                float x=(i*137+Mathf.Sin(time*.35f+i)*45+1280)%1280,y=(i*83-time*6+800*100)%800;
                Check(Same(old[i].rect,new Rect(x,y,2,2))&&Same(old[i].color,Legacy),"Legacy particle rect, tint, ordering and clock remain byte-for-byte equivalent at room "+room+", particle "+i);
            }
            Check(all.FindAll(Air).Count==18,"Unchanged regions do not acquire a regional weather layer");
        }
        Console.WriteLine("PASS three distinct actual regional palettes/clusters; high budgets16/24/42; no warm/snow duplication; exact18-call legacy contract across nine other regions and three times; NATIVE_UNVERIFIED");
    }
    private static void PoliciesPixelBudgetAndEdges(FrontierRuntime app)
    {
        foreach(int room in RegionalRooms)
        {
            Prepare(app,room);var rich=Regional(app);Check(rich.Count>0,"Rich regional air exists");app.Game.save.effectsQuality=1;var low=Regional(app);
            Check(low.Count*2==rich.Count,"Low effects halves regional drawing work without removing its identity");
            for(int i=0;i<low.Count;i++)Check(Same(low[i],rich[i]),"Quality1 is the exact stable prefix of quality2, with no camera/time reseed");
            foreach(bool reduced in new[]{false,true})foreach(int fx in new[]{0,1,2})
            {app.Game.save.reducedMotion=reduced;app.Game.save.effectsQuality=fx;var calls=Draw(app).FindAll(Air);Check((calls.Count==0)==(reduced||fx==0),"Reduced motion or FX0 fully suppresses decorative air, including old snow");}
            app.Game.save.reducedMotion=false;app.Game.save.effectsQuality=2;bool faded=false,nearEdge=false;
            foreach(float time in new[]{0f,.02f,2.345f,17.25f,91.75f,508.5f,10001.25f})foreach(Point camera in new[]{new Point(0,0),new Point(18.25f,12.5f),new Point(-400,200),new Point(800,-600)})
            {
                Set(app,"clock",time);Set(app,"camera",camera);var calls=Regional(app);Check(calls.Count>0&&calls.Count<=46,"Regional air has a bounded analytic budget at any clock/camera");
                foreach(var c in calls)
                {
                    Check(c.rect.width>0&&c.rect.height>0&&c.rect.width<=6&&c.rect.height<=6,"Air is tiny clusters, not opaque screen streaks");
                    foreach(float n in new[]{c.rect.x,c.rect.y,c.rect.width,c.rect.height})Check(!float.IsNaN(n)&&!float.IsInfinity(n)&&Near(n,(float)Math.Round(n)),"All regional air geometry stays on logical integer pixels");
                    Check(c.rect.x>=-30&&c.rect.y>=-30&&c.rect.x<=1310&&c.rect.y<=830,"Wrapping remains within the viewport plus its small offscreen apron");
                    Check(c.color.a>=0&&c.color.a<=.5f,"Air alpha stays a restrained nonnegative overlay");
                    Check(Same(c.uv,new Rect(0,0,1,1)),"Regional air reuses the complete cached white texture");
                    if(c.rect.x<-18||c.rect.x>1298||c.rect.y<-18||c.rect.y>818){nearEdge=true;if(c.color.a<.12f)faded=true;}
                }
            }
            Check(nearEdge&&faded,"Actual wrapped edge samples fade before reappearing rather than popping at full opacity");
        }
        Console.WriteLine("PASS low-FX stable half prefix, FX0/reduced-motion suppression, <=46 tiny cached calls, integer geometry, bounded long-time/negative-camera wrapping and observed edge fade; NATIVE_UNVERIFIED");
    }
    private static void DeterminismCameraAndActualClock(FrontierRuntime app)
    {
        foreach(int room in RegionalRooms)
        {
            Prepare(app,room);var first=Regional(app);Check(first.Count>0,"Regional witness exists for camera/time checks");var camera=Get<Point>(app,"camera");
            Check(Same(first,Regional(app)),"Repeated OnGUI cannot accumulate particles or advance weather");Set(app,"camera",camera+new Point(2,0));var moved=Regional(app);Check(!Same(first,moved),"Real WorldOrigin camera movement produces regional parallax instead of a screen-fixed stencil");
            int modest=0;var shifts=new HashSet<string>();
            for(int i=0;i<first.Count;i++)
            {
                float dx=moved[i].rect.x-first[i].rect.x,dy=moved[i].rect.y-first[i].rect.y;if(Math.Abs(dx)>1000||Math.Abs(dy)>600)continue; // Offscreen wrapped partners.
                Check(dx>=-27&&dx<=-9&&Near(dy,0),"Horizontal camera movement shifts eligible particles left by modest10..26px parallax without vertical drift");modest++;shifts.Add(dx.ToString("R",CultureInfo.InvariantCulture));
            }
            Check(modest>=first.Count/2&&shifts.Count>=2,"Actual air retains distinct sparse near/far camera layers");
            Set(app,"camera",camera);Check(Same(first,Regional(app)),"Returning the camera restores the exact analytic field without reveal history");
            Set(app,"clock",2.845f);var later=Regional(app);int directional=0;
            for(int i=0;i<first.Count;i++)
            {
                var a=first[i];var b=later[i];bool primary=room==7?Near(a.rect.height,2):room==6?a.rect.width>=3:Near(a.rect.width,2);if(!primary)continue;
                float dx=b.rect.x-a.rect.x,dy=b.rect.y-a.rect.y;if(Math.Abs(dx)>640||Math.Abs(dy)>400)continue; // Wrapping happens wholly in the offscreen apron.
                Check(Math.Abs(dx)<25&&Math.Abs(dy)<25,"Half-second ambient motion stays restrained rather than jumping across the screen");
                if(room==7)Check(dy>0,"Snow primary clusters visibly fall downwards over a half-second interval");
                else if(room==6)Check(dx>2*Math.Abs(dy)&&dx>0,"Sand primary clusters visibly drift mostly rightwards rather than falling like snow");
                else Check(dy<0,"Mineral dust primary clusters visibly rise instead of falling like snow");directional++;
            }
            Check(directional>=(room==7?14:room==6?9:6),"A majority of unwrapped primary particles exposes the intended regional motion direction");
            Set(app,"clock",9.125f);Check(!Same(first,Regional(app)),"Presentation clock visibly moves regional air");Set(app,"clock",2.345f);Check(Same(first,Regional(app)),"Returning the clock restores the field without particle state");
            float world=app.Game.save.totalTime,clock=Get<float>(app,"clock");Update(app,50);Check(app.Game.save.totalTime>world&&Get<float>(app,"clock")>clock,"Actual world Update advances both the old presentation clock and Core time");
            app.Game.mode="pause";world=app.Game.save.totalTime;clock=Get<float>(app,"clock");var menu=Regional(app);Update(app,50);
            Check(Near(app.Game.save.totalTime,world)&&Get<float>(app,"clock")>clock&&!Same(menu,Regional(app)),"Menu backgrounds keep the original moving presentation policy while gameplay stays paused");
            Call(app,"OnApplicationPause",true);clock=Get<float>(app,"clock");var paused=Regional(app);Update(app,25);
            Check(Near(Get<float>(app,"clock"),clock)&&Same(paused,Regional(app)),"Real application pause freezes air exactly");Call(app,"OnApplicationPause",false);
            app.VisualReviewPaused=true;clock=Get<float>(app,"clock");paused=Regional(app);Update(app,25);Check(Near(Get<float>(app,"clock"),clock)&&Same(paused,Regional(app)),"The actual visual-review pause also freezes air");app.VisualReviewPaused=false;
        }
        Console.WriteLine("PASS actual Update world/menu/application/review pause semantics, deterministic repeated draws, changing/restored clock and modest camera parallax with no spawn history; NATIVE_UNVERIFIED");
    }
    private static void AppendState(StringBuilder text,object value,HashSet<object> seen)
    {
        if(value==null){text.Append("null;");return;}Type type=value.GetType();if(type.IsPrimitive||type.IsEnum||value is string||value is decimal){text.Append(Convert.ToString(value,CultureInfo.InvariantCulture)).Append(';');return;}
        if(!type.IsValueType&&!seen.Add(value)){text.Append("ref;");return;}var list=value as System.Collections.IEnumerable;if(list!=null){foreach(object item in list)AppendState(text,item,seen);text.Append("end;");return;}
        var fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);Array.Sort(fields,(a,b)=>string.CompareOrdinal(a.Name,b.Name));foreach(var field in fields){text.Append(field.Name).Append('=');AppendState(text,field.GetValue(value),seen);}
    }
    private static string State(GameCore game){var text=new StringBuilder();AppendState(text,game,new HashSet<object>());return text.ToString();}
    private static string Files(){var text=new StringBuilder();var paths=Directory.GetFiles(Application.persistentDataPath,"*",SearchOption.AllDirectories);Array.Sort(paths,StringComparer.Ordinal);foreach(string p in paths)text.Append(p).Append(':').Append(Convert.ToBase64String(File.ReadAllBytes(p))).Append(';');return text.ToString();}
    private static string Digest(string input){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(input))).Replace("-","").ToLowerInvariant();}
    private static void Number(StringBuilder b,float n){b.Append(n.ToString("R",CultureInfo.InvariantCulture)).Append(',');}
    private static void WorldWitness(FrontierRuntime app,bool validate)
    {
        var text=new StringBuilder();int count=0;
        for(int room=0;room<12;room++)
        {
            Prepare(app,room);Set(app,"camera",new Point(18.125f,12.75f));text.Append(room).Append(':');
            foreach(var c in Draw(app))if(!Air(c))
            {
                count++;text.Append(c.texture.name).Append('|');foreach(float n in new[]{c.rect.x,c.rect.y,c.rect.width,c.rect.height,c.uv.x,c.uv.y,c.uv.width,c.uv.height,c.color.r,c.color.g,c.color.b,c.color.a,c.matrix.m00,c.matrix.m01,c.matrix.m03,c.matrix.m10,c.matrix.m11,c.matrix.m13})Number(text,n);text.Append(';');
            }
        }
        string digest=Digest(text.ToString());Console.WriteLine("Non-air OnGUI witness: "+count+" texture calls; sha256="+digest);if(validate)Check(digest==WorldDigest,"All non-air world/HUD texture geometry, UV, tints, ordering and masks remain equal to immutable67f55a5");
    }
    private static void WarningFixture(FrontierRuntime app,string capture=null,bool validate=true)
    {
        Prepare(app,7);var g=app.Game;var e=new Enemy(97201,2,g.Position.x+2,g.Position.y,100){state=1,timer=.4f,windupDuration=.7f,target=g.Position,facing=new Point(-1,0)};g.enemies.Add(e);
        Set(app,"camera",g.Position-new Point((620-640)/36f,(754-390)/36f));Set(app,"combatHudTime",3f);var all=Draw(app,capture);if(!validate)return;
        int firstAir=all.FindIndex(Air),lastAir=all.FindLastIndex(Air);
        int actor=all.FindIndex(c=>c.texture.name=="resource:Frontier/Art/Monsters"||c.texture.name=="resource:Frontier/Art/MonsterWalk");
        int ambient=all.FindIndex(c=>c.texture==Texture2D.whiteTexture&&Same(c.rect,new Rect(0,0,1280,800))&&Same(c.color,new Color(.36f,.55f,.75f,.06f)));
        int dark=all.FindIndex(c=>c.texture==Texture2D.whiteTexture&&Near(c.rect.height,7)&&Same(c.color,new Color(.025f,.045f,.06f,.95f)));
        int panel=all.FindIndex(c=>c.texture==Texture2D.whiteTexture&&Same(c.rect,new Rect(426,730,428,54)));
        int outline=all.FindLastIndex(c=>c.texture.name.Contains("/ActorMasks/outline_"));
        Check(actor>=0&&firstAir>actor&&lastAir>=firstAir&&ambient>lastAir&&dark>ambient&&panel>dark&&outline>panel,"Air keeps its exact world slot: after bodies, before atmosphere, foreground danger contours and the real HUD/actor silhouettes");
    }
    private static void IsolationWorldAndThreatOrder(FrontierRuntime app)
    {
        WorldWitness(app,true);WarningFixture(app);var g=app.Game;var art=Get<PixelArt>(app,"art");Call(art,"Glow");Draw(app);
        string state=State(g),files=Files();int owned=Get<List<Texture2D>>(art,"owned").Count,cache=Get<Dictionary<string,Texture2D>>(art,"cache").Count;float clock=Get<float>(app,"clock");
        var matrix=Matrix4x4.TRS(new Vector3(19,31,0),Quaternion.Euler(0,0,17),new Vector3(1.4f,1.4f,1));var tint=new Color(.3f,.5f,.7f,.8f);
        foreach(EventType evt in new[]{EventType.Layout,EventType.Repaint,EventType.Layout,EventType.Repaint})
        {
            Event.current.type=evt;GUI.matrix=matrix;GUI.color=tint;GUI.enabled=false;Draw(app);
            Check(ReferenceEquals(g,app.Game)&&State(g)==state&&Near(Get<float>(app,"clock"),clock),"Weather OnGUI cannot change complete Core/save/encounter state or its clock");
            Check(Same(GUI.matrix,matrix)&&Same(GUI.color,tint)&&!GUI.enabled,"Weather drawing preserves incoming GUI matrix, color and enabled state");
        }
        Check(Get<List<Texture2D>>(art,"owned").Count==owned&&Get<Dictionary<string,Texture2D>>(art,"cache").Count==cache,"Repeated air creates no textures or cache entries");
        Check((bool)Call(app,"Save")&&Files()==files,"Visual-review drawing and Save preserve slot/backup bytes");
        GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;
        Console.WriteLine("PASS immutable12-map non-air texture witness, Core/save/GUI/cache isolation and actual post-air/post-HUD danger contours/actor silhouettes; NATIVE_UNVERIFIED");
    }
    private static void RegionalView(FrontierRuntime app,int room)
    {
        Prepare(app,room);
        Check(app.SetVisualReviewPosition(new Point(18,15)),"Regional preview uses a collision-valid central location");
        Set(app,"camera",Call(app,"CameraTarget"));
    }
    private static void CaptureScenes(FrontierRuntime app)
    {
        foreach(int room in RegionalRooms)foreach(int stage in new[]{0,1,2,3})
        {RegionalView(app,room);app.Game.save.effectsQuality=stage==2?1:2;app.Game.save.reducedMotion=stage==3;Set(app,"clock",stage==1?18.75f:2.345f);Screen.width=1920;Screen.height=1080;Draw(app,"region"+room+"-"+new[]{"rich-a","rich-b","low","reduced"}[stage]);}
        Prepare(app,1);Draw(app,"forest-control");Prepare(app,9);Draw(app,"inn-control");Prepare(app,7);app.Game.mode="pause";Set(app,"clock",18.75f);Draw(app,"snow-menu");WarningFixture(app,"snow-warning-hud",false);
        Check(captures==16,"Exactly16 comparable production OnGUI captures are recorded");Console.WriteLine("CAPTURE16 regional-air fixtures: three regions x four policies/time samples, two legacy controls, menu and threatened HUD; NATIVE_UNVERIFIED");
        if(motionCaptures)
        {
            foreach(int room in RegionalRooms)
            {
                RegionalView(app,room);Screen.width=1280;Screen.height=800;
                for(int phase=0;phase<6;phase++){Set(app,"clock",phase*.4f);Draw(app,"region"+room+"-phase"+phase);}
            }
            Check(captures==34,"Motion review adds exactly18 production frames");Console.WriteLine("CAPTURE18 optional motion frames: rooms3/6/7 at0/.4/.8/1.2/1.6/2 presentation seconds; staged rendering, NATIVE_UNVERIFIED");
        }
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-regional-air-"+Guid.NewGuid());Application.persistentDataPath=directory;
        captureFolder=args.Length>0&&args[0]!="-"?args[0]:null;capturePrefix=args.Length>1?args[1]:"qa-air";bool only=args.Length>2&&args[2]=="capture-only";motionCaptures=args.Length>3&&args[3]=="motion";int failed=0;var app=new FrontierRuntime();
        try
        {
            Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"starfall-v1.json"),"{\"version\":1,\"profession\":0}");File.WriteAllText(Path.Combine(directory,"starfall-slot-1.json.bak"),"existing-slot-backup-sentinel");Call(app,"Awake");Check(app.Game!=null,"Production Awake loads the shipped campaign");
            if(only)WorldWitness(app,false);else foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{RegionalDesignAndLegacyMotes,PoliciesPixelBudgetAndEdges,DeterminismCameraAndActualClock,IsolationWorldAndThreatOrder})try{test(app);}catch(Exception ex){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(ex.InnerException??ex).Message);}
            if(captureFolder!=null)CaptureScenes(app);app.EndVisualReview();Call(app,"OnDestroy");
        }
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        if(only){Console.WriteLine("Capture-only: semantic checks skipped; NATIVE_UNVERIFIED");return 0;}Console.WriteLine("Runtime regional air: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
