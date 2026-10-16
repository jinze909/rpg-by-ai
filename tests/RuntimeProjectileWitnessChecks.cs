// Calls the real OnGUI, DrawProjectileVisual, Fire, Update and Save entry points.
// Offline declarations record geometry; native GPU/import/playtest remain unverified.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Starfall;
using UnityEngine;

public static class RuntimeProjectileWitnessChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static string captureFolder,capturePrefix;
    private static int captures;
    private sealed class Sample
    {
        public string visual,name; public bool friendly,explosive;
        public Sample(string name,string visual,bool friendly,bool explosive=false){this.name=name;this.visual=visual;this.friendly=friendly;this.explosive=explosive;}
    }
    private static readonly Sample[] samples={new Sample("friendly-arrow","arrow",true),new Sample("hostile-arrow","enemyArrow",false),new Sample("friendly-starfire","starfire",true,true),new Sample("hostile-starfire","starfire",false,true),new Sample("friendly-orb","orb",true),new Sample("hostile-orb","enemyOrb",false),new Sample("default-arrow",null,true),new Sample("default-starfire",null,true,true),new Sample("default-orb",null,false),new Sample("nonexplosive-starfire","starfire",true),new Sample("explosive-orb","enemyOrb",false,true)};
    private static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
    private static object Call(object app,string method,params object[] args){var m=app.GetType().GetMethod(method,Private|BindingFlags.Public|BindingFlags.Static);Check(m!=null,"Existing production entry point: "+method);return m.Invoke(app,args);}
    private static T Get<T>(object app,string field){return (T)app.GetType().GetField(field,Private).GetValue(app);}
    private static void Set(object app,string field,object value){app.GetType().GetField(field,Private).SetValue(app,value);}
    private static bool Near(float a,float b,float tolerance=.001f){return Math.Abs(a-b)<=tolerance;}
    private static bool Same(Rect a,Rect b){return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height);}
    private static bool Same(Color a,Color b){return Near(a.r,b.r)&&Near(a.g,b.g)&&Near(a.b,b.b)&&Near(a.a,b.a);}
    private static bool Same(Matrix4x4 a,Matrix4x4 b){return Near(a.m00,b.m00)&&Near(a.m01,b.m01)&&Near(a.m03,b.m03)&&Near(a.m10,b.m10)&&Near(a.m11,b.m11)&&Near(a.m13,b.m13);}
    private static void Clear(){GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();}
    private static bool Mask(GUI.TextureCall c){return c.texture.name.StartsWith("resource:Frontier/Art/ProjectileMasks/");}
    private static List<GUI.TextureCall> Masks(){return GUI.textureCalls.FindAll(Mask);}
    private static void Prepare(FrontierRuntime app,int profession=0)
    {
        Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;Event.current.type=EventType.Repaint;
        app.BeginVisualReview(profession,1);app.VisualReviewPaused=false;var g=app.Game;g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();g.effects.Clear();g.fallen.Clear();g.save.companion=-1;g.save.reducedMotion=true;g.save.effectsQuality=0;g.save.showGuidance=false;g.save.english=true;g.toastTime=0;g.playerFlash=0;g.invulnerable=0;
        Check(app.SetVisualReviewPosition(new Point(12,14))&&(g.Position-new Point(12,14)).Length<.001f,"Witness staging uses legal mature-map feet");
        Set(app,"camera",g.Position);Set(app,"clock",0f);Set(app,"combatHudTime",3f);Set(app,"applicationPaused",false);Set(app,"pointerSeen",false);Set(app,"controllerUsed",false);Set(app,"drawingOutline",false);Call(app,"Resize");
    }
    private static string Visual(Projectile p){return p.visual??(p.friendly?p.explosive?"starfire":"arrow":"enemyOrb");}
    private static bool Arrow(Projectile p){return Visual(p)=="arrow"||Visual(p)=="enemyArrow";}
    private static bool Directional(Projectile p){return Arrow(p)||Visual(p)=="starfire";}
    private static string Shape(Projectile p){return Arrow(p)?"Arrow":Visual(p)=="starfire"?"Starfire":"Orb";}
    private static float Size(Projectile p){return Arrow(p)?30:p.explosive?28:22;}
    private static float Angle(Projectile p){return (float)(Math.Atan2(p.velocity.y,p.velocity.x)*180/Math.PI);}
    private static Point Centre(FrontierRuntime app,Projectile p){Point at=(Point)Call(app,"ScreenPoint",p.position);at.y-=p.height;return at;}
    private static Projectile Shot(FrontierRuntime app,Sample sample,Point centre,int direction=0,float age=.37f)
    {
        float angle=(float)(direction*Math.PI/4),height=direction%2==0?31:47;Point origin=(Point)Call(app,"ScreenPoint",new Point());
        var p=new Projectile{visual=sample.visual,friendly=sample.friendly,explosive=sample.explosive,height=height,age=age,position=new Point((centre.x-origin.x)/36,(centre.y+height-origin.y)/36),velocity=new Point((float)Math.Cos(angle),(float)Math.Sin(angle))*7.2f,theme=7,damage=11,life=3};
        app.Game.projectiles.Clear();app.Game.projectiles.Add(p);return p;
    }
    private static Matrix4x4 Canvas()
    {float scale=Math.Min(Screen.width/1280f,Screen.height/800f);return Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)/2,(Screen.height-800*scale)/2,0),Quaternion.identity,new Vector3(scale,scale,1));}
    private static Matrix4x4 Rotation(Matrix4x4 canvas,Point centre,float angle)
    {return canvas*Matrix4x4.TRS(new Vector3(centre.x,centre.y,0),Quaternion.Euler(0,0,angle),new Vector3(1,1,1))*Matrix4x4.TRS(new Vector3(-centre.x,-centre.y,0),Quaternion.identity,new Vector3(1,1,1));}
    private static List<GUI.TextureCall> Witness(FrontierRuntime app)
    {Clear();Call(app,"DrawHudThreatWitness");return Masks();}
    private static void VerifyPair(FrontierRuntime app,Projectile p,List<GUI.TextureCall> masks,Matrix4x4 canvas)
    {
        Check(masks.Count==2,"Exactly one dark/bright authored-alpha pair, replacing the square HUD marker");float size=Size(p)*144/128;Point centre=Centre(app,p);var rect=new Rect(centre.x-size/2,centre.y-size/2,size,size);var matrix=Rotation(canvas,centre,Directional(p)?Angle(p):0);
        Check(masks[0].texture.name.EndsWith("/dark_"+Shape(p))&&masks[1].texture.name.EndsWith("/edge_"+Shape(p)),"Projectile source category selects its matching dark/edge alpha silhouette in order");
        Color bright=p.friendly?PixelArt.C("#7acabb"):PixelArt.C("#df8a68");bright.a=.95f;Color dark=PixelArt.C("#101a2b");dark.a=.95f;
        for(int i=0;i<2;i++)Check(Same(masks[i].rect,rect)&&Same(masks[i].uv,new Rect(0,0,1,1))&&Same(masks[i].matrix,matrix)&&Same(masks[i].color,i==0?dark:bright),"Padded whole silhouette uses the original elevated centre, correct directional rotation and allegiance tint");
    }
    private static void Draw(FrontierRuntime app,string capture=null)
    {Clear();Call(app,"OnGUI");if(captureFolder!=null&&capture!=null){var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();GUI.Export(captureFolder,capturePrefix+"-"+capture);captures++;GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);}}
    private static void ShapesDirectionsAndCanvas(FrontierRuntime app)
    {
        int cases=0;
        foreach(int width in new[]{960,1280,1920})foreach(var sample in samples)for(int direction=0;direction<8;direction++)
        {
            Prepare(app);Screen.width=width;Screen.height=width==960?640:width==1920?1080:800;Call(app,"Resize");Projectile p=Shot(app,sample,new Point(120,50),direction);GUI.matrix=Canvas();
            VerifyPair(app,p,Witness(app),Canvas());cases++;
            if(direction==1){Draw(app);VerifyPair(app,p,Masks(),Canvas());int panel=GUI.textureCalls.FindLastIndex(c=>c.texture==Texture2D.whiteTexture&&Same(c.rect,new Rect(20,20,260,77))),first=GUI.textureCalls.FindIndex(Mask);Check(panel>=0&&first>panel,"Actual OnGUI submits the directional pair after the live combat HUD");}
        }
        Console.WriteLine("PASS "+cases+" real projectile witness category/direction/canvas cases and actual post-HUD order at 960/1280/1920; NATIVE_UNVERIFIED");
    }
    private static void GrazingBoundsAndModes(FrontierRuntime app)
    {
        Prepare(app);var p=Shot(app,samples[1],new Point(12,50),1);Point centre=Centre(app,p);Check(centre.x<20,"Diagonal grazing arrow centre lies outside the HUD while its visible tip crosses the edge");
        VerifyPair(app,p,Witness(app),Matrix4x4.identity);var panel=new Rect(20,20,260,77);Check((bool)Call(app,"ThreatOver",panel),"A diagonal padded silhouette grazing HUD also fades the same panel");
        Shot(app,samples[1],new Point(-5,50),1);Check(Witness(app).Count==0&&!(bool)Call(app,"ThreatOver",panel),"Beyond the rotated canvas bounds, the witness and panel fade both stop");
        foreach(var sample in new[]{samples[2],samples[9]})
        {
            float half=(sample.explosive?28:22)*144/128f/2,rotated=half*(float)Math.Sqrt(2);
            p=Shot(app,sample,new Point(20-(half+rotated)/2,50),1);
            Check(Centre(app,p).x+half<20,"Starfire canvas fixture lies beyond the old unrotated HUD envelope");
            VerifyPair(app,p,Witness(app),Matrix4x4.identity);Check((bool)Call(app,"ThreatOver",panel),"Explosive and nonexplosive Starfire share their rotated padded canvas bounds with panel fading");
            // ScreenPoint snaps the projected feet to pixels. Put the negative
            // fixture beyond that final pixel, not just a fractional input edge.
            Shot(app,sample,new Point((float)Math.Floor(20-rotated)-1,50),1);Check(Witness(app).Count==0&&!(bool)Call(app,"ThreatOver",panel),"Starfire past its rotated canvas support has no stale witness or panel fade");
        }
        p=Shot(app,samples[1],new Point(120,50),1);var region=new Rect(94,24,2,2);Check(!(bool)Call(app,"ThreatOver",region),"An unrelated tiny rectangle outside padded rotated silhouette remains opaque");
        foreach(Point clear in new[]{new Point(640,300),new Point(900,500)}){Shot(app,samples[0],clear,3);Check(Witness(app).Count==0,"Open-world missiles gain no extra HUD overlay");}
        foreach(string mode in new[]{"pause","dialog","inventory","title"}){Shot(app,samples[1],new Point(120,50),1);app.Game.mode=mode;Check(Witness(app).Count==0,"Non-world "+mode+" cannot draw witnesses over menus");app.Game.mode="world";}
        app.Game.practicing=true;Set(app.Game,"sparringSession",true);
        foreach(string property in new[]{"SparringAwaitingNext","SparringFinished"}){Set(app.Game,"<"+property+">k__BackingField",true);Check(Witness(app).Count==0&&!(bool)Call(app,"ThreatOver",panel),"Cleared/finished sparring review keeps feedback readable");Set(app.Game,"<"+property+">k__BackingField",false);}
        Console.WriteLine("PASS conservative rotated edge-grazing with centre outside HUD, shared opacity bounds, off-HUD and menu/cleared-drill suppression; NATIVE_UNVERIFIED");
    }
    private static void Record(StringBuilder text,GUI.TextureCall c)
    {text.Append(c.texture.name).Append('|');foreach(float v in new[]{c.rect.x,c.rect.y,c.rect.width,c.rect.height,c.uv.x,c.uv.y,c.uv.width,c.uv.height,c.color.r,c.color.g,c.color.b,c.color.a,c.matrix.m00,c.matrix.m01,c.matrix.m03,c.matrix.m10,c.matrix.m11,c.matrix.m13})text.Append(v.ToString("R",CultureInfo.InvariantCulture)).Append(',');text.Append(';');}
    private static string Digest(string value){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();}
    private static void Append(StringBuilder text,object value,HashSet<object> seen)
    {
        if(value==null){text.Append("null;");return;}Type type=value.GetType();if(type.IsPrimitive||type.IsEnum||value is string||value is decimal){text.Append(Convert.ToString(value,CultureInfo.InvariantCulture)).Append(';');return;}
        if(!type.IsValueType&&!seen.Add(value)){text.Append("ref;");return;}var sequence=value as System.Collections.IEnumerable;if(sequence!=null){foreach(object v in sequence)Append(text,v,seen);text.Append("end;");return;}
        var fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);Array.Sort(fields,(a,b)=>string.CompareOrdinal(a.Name,b.Name));foreach(var field in fields){text.Append(field.Name).Append('=');Append(text,field.GetValue(value),seen);}
    }
    private static string State(GameCore game){var text=new StringBuilder();Append(text,game,new HashSet<object>());return text.ToString();}
    private static string Files(){var text=new StringBuilder();var paths=Directory.GetFiles(Application.persistentDataPath,"*",SearchOption.AllDirectories);Array.Sort(paths,StringComparer.Ordinal);foreach(string path in paths)text.Append(path).Append(':').Append(Convert.ToBase64String(File.ReadAllBytes(path))).Append(';');return text.ToString();}
    private static void WorldPreservationAndIsolation(FrontierRuntime app)
    {
        Prepare(app);var preservation=new StringBuilder();int cases=0;
        foreach(var sample in samples)for(int direction=0;direction<8;direction++)foreach(float age in new[]{0f,.04f,.37f})foreach(int fx in new[]{0,2})
        {
            app.Game.save.effectsQuality=fx;Projectile p=Shot(app,sample,new Point(120,50),direction,age);Clear();Call(app,"DrawProjectileVisual",p);int tails=fx>0?Math.Min(3,(int)(age*p.velocity.Length/.18f)):0;Check(GUI.textureCalls.Count==tails+1,"Original newborn/short/mature trail call count is unchanged");
            string visual=Visual(p);int frame=Arrow(p)?8:visual=="starfire"?9:10;float size=Size(p);Color tint=p.friendly?Color.white:(Color)Call(app,"BossEffectColor",p.theme);
            for(int index=0;index<GUI.textureCalls.Count;index++)
            {
                int tail=tails-index;var call=GUI.textureCalls[index];Point at=(Point)Call(app,"ScreenPoint",tail>0?p.position-p.velocity.Normalized*tail*.18f:p.position);at.y-=p.height;int cell=tail>0?(Arrow(p)?11:12):frame;float angle=tail>0||Directional(p)?Angle(p):0;
                var rect=new Rect(at.x-size/2,at.y-size/2,size,size);var color=tail>0?new Color(tint.r,tint.g,tint.b,.42f/tail):tint;
                Check(call.texture.name=="resource:Frontier/Art/Effects"&&Same(call.rect,rect)&&Same(call.uv,new Rect(cell%4/4f,1-(cell/4+1)/4f,.25f,.25f))&&Same(call.color,color)&&Same(call.matrix,Rotation(Matrix4x4.identity,at,angle)),"Original world source frame/RGB/height/rotation/tail geometry survive for "+sample.name);
                Record(preservation,call);
            }
            cases++;
        }
        Console.WriteLine("World projectile preservation: "+cases+" cases; sha256="+Digest(preservation.ToString()));
        Prepare(app);Shot(app,samples[1],new Point(120,50),1);var art=Get<PixelArt>(app,"art");Call(art,"Glow");Draw(app);var masks=Masks();var owned=Get<List<Texture2D>>(art,"owned");var cache=Get<Dictionary<string,Texture2D>>(art,"cache");int ownedCount=owned.Count,cacheCount=cache.Count;string before=State(app.Game),files=Files();float clock=Get<float>(app,"clock");var matrix=Matrix4x4.TRS(new Vector3(19,31,0),Quaternion.Euler(0,0,17),new Vector3(1.4f,1.4f,1));var incoming=new Color(.3f,.5f,.7f,.8f);
        foreach(bool motion in new[]{false,true})foreach(int fx in new[]{0,2})foreach(EventType evt in new[]{EventType.Layout,EventType.Repaint})
        {
            app.Game.save.reducedMotion=motion;app.Game.save.effectsQuality=fx;before=State(app.Game);Event.current.type=evt;GUI.matrix=matrix;GUI.color=incoming;GUI.enabled=false;Draw(app);
            Check(State(app.Game)==before&&Near(Get<float>(app,"clock"),clock),"Repeated layout/repaint witness leaves all Core/save/encounter state and presentation clock untouched");Check(Same(GUI.matrix,matrix)&&Same(GUI.color,incoming)&&!GUI.enabled,"Actual OnGUI restores incoming matrix/color/enabled");
            Check(Masks().Count==masks.Count,"Low/rich FX and reduced motion keep the same readable witness");
            foreach(var mask in Masks())Check(masks.Exists(c=>ReferenceEquals(c.texture,mask.texture))&&!owned.Contains(mask.texture)&&mask.texture.pixelReads==0&&!mask.texture.destroyed,"Borrowed prepared resource masks retain identity, no PixelArt ownership/CPU pixels/destruction");
        }
        Check(owned.Count==ownedCount&&cache.Count==cacheCount,"Repeated draw creates no PixelArt textures or cache entries");Check((bool)Call(app,"Save")&&Files()==files,"Review drawing and Save preserve existing save/slot/backup bytes");GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;
        Console.WriteLine("PASS original source/tail rendering plus complete Core/save/GUI/clock/cache isolation and borrowed-resource reuse across settings; NATIVE_UNVERIFIED");
    }
    private static void Launch(FrontierRuntime app)
    {
        var g=app.Game;Point source=g.Position+new Point(-1.8f,0);Check(g.CanStand(source),"Hostile launch fixture starts on real walkable terrain");var enemy=new Enemy(99101,3,source.x,source.y,100,true);Call(g,"Fire",enemy,new Point(1,0),5f);Check(g.projectiles.Count==1&&!g.projectiles[0].friendly&&g.projectiles[0].visual=="enemyOrb","Existing production Fire launches the hostile projectile");
    }
    private static void ActualLaunchContactAndDodge(FrontierRuntime app)
    {
        var control=new FrontierRuntime();Call(control,"Awake");
        try
        {
            foreach(bool dodge in new[]{false,true})
            {
                Prepare(app);Prepare(control);Launch(app);Launch(control);float health=app.Game.save.health;bool contacted=false;int frames=0;
                while(app.Game.projectiles.Count>0&&frames++<80)
                {
                    Input.Clear();if(dodge&&frames==1){Input.keys.Add(KeyCode.Space);Input.axes["Horizontal"]=-1;Input.held.Add(KeyCode.A);}Call(app,"Update");Call(control,"Update");
                    string state=State(app.Game);Draw(app);Check(State(app.Game)==state&&State(control.Game)==state,"Production Update with/without OnGUI yields identical complete contact/dodge state at frame "+frames);
                    Check(app.Game.CanStand(app.Game.Position),"Actual movement retains collision legality");contacted|=app.Game.playerHitTime>0||app.Game.riposteTime>0;
                }
                Check(frames<80&&app.Game.projectiles.Count==0&&control.Game.projectiles.Count==0,"Actual flight/contact retires both missiles without stale HUD witness");
                Check(contacted&&(dodge?Near(app.Game.save.health,health)&&app.Game.riposteTime>0:app.Game.save.health<health),"Real hostile contact "+(dodge?"meets the Space dodge window and arms a counter":"deals unchanged player damage"));
                Check(Masks().Count==0,"Removed hostile shot leaves no cached stale witness");
            }
        }
        finally{control.EndVisualReview();Call(control,"OnDestroy");Input.Clear();}
        Console.WriteLine("PASS real Fire + actual Update hostile contact, Space dodge/counter and removal are identical with/without rendering; NATIVE_UNVERIFIED");
    }
    private static void Captures(FrontierRuntime app)
    {
        for(int i=0;i<6;i++){Prepare(app,i%3);Shot(app,samples[i],new Point(120,54),i);Draw(app,samples[i].name);}
        Prepare(app);Shot(app,samples[1],new Point(12,50),1);Draw(app,"diagonal-edge-graze");
        Prepare(app);Shot(app,samples[0],new Point(640,300),3);Draw(app,"off-hud-source-only");
        Prepare(app);Shot(app,samples[1],new Point(120,50),7);Screen.width=960;Screen.height=640;Draw(app,"small-lowfx");
        Prepare(app);Shot(app,samples[3],new Point(120,50),5);app.Game.save.effectsQuality=2;app.Game.save.reducedMotion=false;Screen.width=1920;Screen.height=1080;Draw(app,"wide-richfx");
        Prepare(app);Shot(app,samples[1],new Point(120,50),1);app.Game.mode="pause";Draw(app,"pause-no-witness");
        Prepare(app);Shot(app,samples[1],new Point(120,50),1);app.Game.projectiles.Add(new Projectile{visual="arrow",friendly=true,position=app.Game.projectiles[0].position+new Point(1,0),velocity=new Point(-5,-5),height=31,age=.37f});Draw(app,"mixed-allegiances");
        Check(captures==12,"Twelve comparable actual OnGUI captures");Console.WriteLine("CAPTURE 12 actual OnGUI projectile witnesses: six sources, diagonal graze, open world, small/wide settings, pause and mixed allegiance; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-projectile-witness-"+Guid.NewGuid());Application.persistentDataPath=directory;captureFolder=args.Length>0&&args[0]!="-"?args[0]:null;capturePrefix=args.Length>1?args[1]:"qa-projectile-witness";bool only=args.Length>2&&args[2]=="capture-only";int failed=0;var app=new FrontierRuntime();
        try
        {
            Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"starfall-v1.json"),"{\"version\":1,\"profession\":0}");File.WriteAllText(Path.Combine(directory,"starfall-slot-1.json.bak"),"existing-slot-backup-sentinel");Call(app,"Awake");Check(app.Game!=null,"Production Awake loads campaign");
            if(!only)foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{ShapesDirectionsAndCanvas,GrazingBoundsAndModes,WorldPreservationAndIsolation,ActualLaunchContactAndDodge})try{test(app);}catch(Exception ex){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(ex.InnerException??ex).Message);}
            if(captureFolder!=null)Captures(app);app.EndVisualReview();Call(app,"OnDestroy");
        }
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        if(only){Console.WriteLine("Capture-only: semantic checks skipped; NATIVE_UNVERIFIED");return 0;}Console.WriteLine("Runtime projectile witness: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
