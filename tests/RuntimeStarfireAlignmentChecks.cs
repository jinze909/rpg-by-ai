// Real J/Z input, automatic companion actions and production Update/OnGUI.
// The affine recorder is offline evidence, not native GPU or Player validation.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Starfall;
using UnityEngine;

public static class RuntimeStarfireAlignmentChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static string captureFolder,capturePrefix;
    private static int captures;
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    private static object Call(object target,string name,params object[] args)
    {var method=target.GetType().GetMethod(name,Private|BindingFlags.Public|BindingFlags.Static);Check(method!=null,"Existing production hook: "+name);return method.Invoke(target,args);}
    private static T Get<T>(object target,string field){return (T)target.GetType().GetField(field,Private).GetValue(target);}
    private static void Set(object target,string field,object value){target.GetType().GetField(field,Private).SetValue(target,value);}
    private static bool Near(float a,float b){return Math.Abs(a-b)<.001f;}
    private static bool Same(Rect a,Rect b){return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height);}
    private static bool Same(Color a,Color b){return Near(a.r,b.r)&&Near(a.g,b.g)&&Near(a.b,b.b)&&Near(a.a,b.a);}
    private static bool Same(Matrix4x4 a,Matrix4x4 b){return Near(a.m00,b.m00)&&Near(a.m01,b.m01)&&Near(a.m03,b.m03)&&Near(a.m10,b.m10)&&Near(a.m11,b.m11)&&Near(a.m13,b.m13);}
    private static Point Direction(int heading){double a=heading*Math.PI/4;return new Point((float)Math.Cos(a),(float)Math.Sin(a));}
    private static float Angle(Point direction){return (float)(Math.Atan2(direction.y,direction.x)*180/Math.PI);}
    private static string Visual(Projectile p){return p.visual??(p.friendly?p.explosive?"starfire":"arrow":"enemyOrb");}
    private static bool Arrow(Projectile p){return Visual(p)=="arrow"||Visual(p)=="enemyArrow";}
    private static Point Centre(FrontierRuntime app,Projectile p){Point at=(Point)Call(app,"ScreenPoint",p.position);at.y-=p.height;return at;}
    private static Matrix4x4 Rotation(Point at,float angle)
    {return Matrix4x4.TRS(new Vector3(at.x,at.y,0),Quaternion.Euler(0,0,angle),new Vector3(1,1,1))*Matrix4x4.TRS(new Vector3(-at.x,-at.y,0),Quaternion.identity,new Vector3(1,1,1));}
    private static void Clear(){GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();}
    private static void Draw(FrontierRuntime app,string capture=null)
    {
        Clear();Call(app,"OnGUI");
        if(captureFolder!=null&&capture!=null){var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();GUI.Export(captureFolder,capturePrefix+"-"+capture);captures++;GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);}
    }
    private static void Update(FrontierRuntime app){Call(app,"Update");Input.keys.Clear();}
    private static void Mouse(FrontierRuntime app,Point target)
    {Point at=(Point)Call(app,"ScreenPoint",target);Input.mousePosition=new Vector3(at.x,Screen.height-at.y,0);}
    private static Point ClearFoot(GameCore game)
    {
        for(int y=5;y<game.Map.rows.Length-5;y++)for(int x=5;x<game.Map.rows[0].Length-5;x++)
        {
            var foot=new Point(x+.5f,y+.5f);bool clear=game.CanStand(foot);
            for(int heading=0;clear&&heading<8;heading++)for(float step=.25f;clear&&step<=3.5f;step+=.25f)clear=game.CanStand(foot+Direction(heading)*step);
            if(clear)return foot;
        }
        throw new Exception("No shipped town footprint with clear three-tile rays for actual launches");
    }
    private static void Prepare(FrontierRuntime app)
    {
        Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;Event.current.type=EventType.Repaint;
        app.BeginVisualReview(2,0);app.VisualReviewPaused=false;var g=app.Game;g.enemies.Clear();g.hazards.Clear();g.projectiles.Clear();g.effects.Clear();g.fallen.Clear();g.save.companion=-1;g.save.reducedMotion=true;g.save.effectsQuality=2;g.save.showGuidance=false;g.save.english=true;g.save.health=g.save.maxHealth;g.toastTime=0;g.invulnerable=0;g.playerFlash=0;
        Point foot=ClearFoot(g);Check(app.SetVisualReviewPosition(foot)&&(g.Position-foot).Length<.001f,"Real town collision accepts the clear launch footprint");
        Set(app,"camera",g.Position);Set(app,"clock",0f);Set(app,"combatHudTime",3f);Set(app,"applicationPaused",false);Set(app,"controllerUsed",false);Set(app,"pointerSeen",false);Set(app,"pointerAimActive",false);Set(app,"drawingOutline",false);Call(app,"Resize");Mouse(app,g.Position+new Point(3,0));
    }
    private static Projectile PlayerShot(FrontierRuntime app,int heading)
    {
        Prepare(app);var g=app.Game;Mouse(app,g.Position+Direction(heading)*4);Input.held.Add(KeyCode.J);Update(app);Input.Clear();
        ActorAction action=g.playerAction;Check(action!=null&&action.kind=="attack"&&action.weapon==2&&!action.released,"Real J input starts a prepared staff action, without an early missile");
        for(int frame=0;frame<40&&g.projectiles.Count==0;frame++)Update(app);
        Check(g.projectiles.Count==1&&action.released,"Production action contact emits exactly one staff projectile");
        Projectile shot=g.projectiles[0];Check(shot.visual=="starfire"&&shot.friendly&&shot.explosive&&Near(shot.velocity.Length,8)&&!shot.blockedAtSpawn,"Actual staff launch preserves accepted Starfire source, explosion, speed and valid muzzle");
        Check(shot.velocity.Normalized.x*Direction(heading).x+shot.velocity.Normalized.y*Direction(heading).y>.85f,"The emitted muzzle-to-pointer flight retains the requested heading hemisphere");
        return shot;
    }
    private static void VerifyBody(FrontierRuntime app,Projectile p)
    {
        GUI.matrix=Matrix4x4.identity;Clear();Call(app,"DrawProjectileVisual",p);int tails=app.Game.save.effectsQuality>0?Math.Min(3,(int)(p.age*p.velocity.Length/.18f)):0;
        Check(GUI.textureCalls.Count==tails+1,"Actual launch keeps its age-limited trail submission count");
        float size=Arrow(p)?30:p.explosive?28:22;Point centre=Centre(app,p);var body=GUI.textureCalls[tails];int frame=Arrow(p)?8:Visual(p)=="starfire"?9:10;
        Check(body.texture.name=="resource:Frontier/Art/Effects"&&Same(body.uv,new Rect(frame%4/4f,1-(frame/4+1)/4f,.25f,.25f))&&Same(body.rect,new Rect(centre.x-size/2,centre.y-size/2,size,size)),"The real projectile uses its accepted source cell, continuous flight height and unshifted centre");
        float angle=Arrow(p)||Visual(p)=="starfire"?Angle(p.velocity):0;
        Check(Same(body.matrix,Rotation(centre,angle)),"Actual "+Visual(p)+" authored head and tail must face its emitted velocity, not always right");
        for(int index=0;index<tails;index++)
        {
            int distance=tails-index;Point at=(Point)Call(app,"ScreenPoint",p.position-p.velocity.Normalized*distance*.18f);at.y-=p.height;var trail=GUI.textureCalls[index];int cell=Arrow(p)?11:12;
            Check(Same(trail.uv,new Rect(cell%4/4f,1-(cell/4+1)/4f,.25f,.25f))&&Same(trail.rect,new Rect(at.x-size/2,at.y-size/2,size,size))&&Same(trail.matrix,Rotation(at,Angle(p.velocity))),"Existing trail cells remain behind the real flight at the unchanged direction and height");
        }
    }
    private static void HudCamera(FrontierRuntime app,Projectile p)
    {
        // Only the offline QA camera is staged: the live emitted projectile is not moved.
        Point before=Centre(app,p);Set(app,"camera",Get<Point>(app,"camera")+(before-new Point(120,50))*(1f/36));Set(app,"combatHudTime",3f);
    }
    private static void VerifyHud(FrontierRuntime app,Projectile p)
    {
        HudCamera(app,p);Draw(app);var masks=GUI.textureCalls.FindAll(c=>c.texture.name.StartsWith("resource:Frontier/Art/ProjectileMasks/"));Point centre=Centre(app,p);string shape=Arrow(p)?"Arrow":Visual(p)=="starfire"?"Starfire":"Orb";
        Check(masks.Count==2&&masks[0].texture.name.EndsWith("/dark_"+shape)&&masks[1].texture.name.EndsWith("/edge_"+shape),"Actual OnGUI shows exactly the matching dark and bright HUD silhouettes for a real emitted shot");
        float angle=Arrow(p)||Visual(p)=="starfire"?Angle(p.velocity):0;
        foreach(var mask in masks)Check(Same(mask.matrix,Rotation(centre,angle)),"The actual post-HUD silhouette must agree with its real projectile direction");
        Check((bool)Call(app,"ThreatOver",new Rect(20,20,260,77)),"The emitted missile and HUD fade share their visible overlap");
    }
    private static Effect Expire(FrontierRuntime app,Projectile p,bool alreadySpent=false)
    {
        // A short remaining lifetime is explicitly staged; actual Update owns removal and fade creation.
        p.life=alreadySpent?0:.005f;Input.Clear();Update(app);Check(!app.Game.projectiles.Contains(p),"Actual projectile update retires the staged short-life missile");
        int frame=Arrow(p)?8:Visual(p)=="starfire"?9:10;
        Effect fade=app.Game.effects.Find(e=>e.kind=="projectileFade");Check(fade!=null,"Actual expiry creates its existing fade effect");
        Check(fade.variant==frame&&(fade.direction-p.velocity.Normalized).Length<.001f&&Near(fade.height,p.height),"Actual "+Visual(p)+" expiry must preserve source cell "+frame+" (observed "+fade.variant+"), emitted direction and height; explosive="+p.explosive);return fade;
    }
    private static void PlayerReleaseAndExpiry(FrontierRuntime app)
    {
        for(int heading=0;heading<8;heading++)
        {
            Projectile shot=PlayerShot(app,heading);VerifyBody(app,shot);VerifyHud(app,shot);Effect fade=Expire(app,shot);Clear();Call(app,"DrawCombatEffect",fade);
            var body=GUI.textureCalls.Find(c=>c.texture.name=="resource:Frontier/Art/Effects");Point centre=(Point)Call(app,"ScreenPoint",fade.position);centre.y-=fade.height;
            Check(body!=null&&Same(body.matrix,Rotation(centre,Angle(shot.velocity))),"A live Starfire and its actual expiry must have identical heading, without a last-frame snap");
        }
        Console.WriteLine("PASS real J staff releases at eight headings: actual muzzle velocities, source cells, height, body/trails/HUD and Update-owned expiry heading; NATIVE_UNVERIFIED");
    }
    private static Projectile CompanionShot(FrontierRuntime app,int companion,int heading)
    {
        Prepare(app);var g=app.Game;if(!g.Has("companion_"+companion))g.save.flags.Add("companion_"+companion);g.SelectCompanion(companion);g.companionPosition=g.Position;Set(g,"companionAttack",0f);
        Point target=g.companionPosition+Direction(heading)*3;Check(g.CanStand(target),"Automatic companion target is on real clear ground");g.enemies.Add(new Enemy(98010,0,target.x,target.y,9999){stun=100,timer=9999});
        for(int frame=0;frame<45&&g.projectiles.Count==0;frame++)Update(app);
        Check(g.projectiles.Count==1&&g.companionAction!=null&&g.companionAction.released&&g.companionAction.kind=="attack","The actual companion AI finds a target, prepares, and releases its own projectile");
        Projectile p=g.projectiles[0];Check(p.friendly&&p.visual==(companion==1?"arrow":"starfire")&&p.explosive==(companion==2)&&Near(p.velocity.Length,9),"Mira nonexplosive Starfire, Sen explosive Starfire and Ivor arrow retain their real source rules");return p;
    }
    private static void CompanionRoutesAndCommands(FrontierRuntime app)
    {
        for(int companion=0;companion<3;companion++)foreach(int heading in new[]{2,4,6})
        {
            Projectile shot=CompanionShot(app,companion,heading);VerifyBody(app,shot);VerifyHud(app,shot);Effect fade=Expire(app,shot,heading==4);Clear();Call(app,"DrawCombatEffect",fade);Point centre=(Point)Call(app,"ScreenPoint",fade.position);centre.y-=fade.height;
            Check(Same(GUI.textureCalls[0].matrix,Rotation(centre,Angle(shot.velocity))),"Automatic companion projectile and both expiry branches retain a continuous heading");
        }
        for(int companion=0;companion<3;companion++)
        {
            Prepare(app);var g=app.Game;if(!g.Has("companion_"+companion))g.save.flags.Add("companion_"+companion);g.SelectCompanion(companion);g.companionPosition=g.Position;Set(g,"companionAttack",9999f);g.save.health=20;Mouse(app,g.Position+new Point(-3,0));Input.keys.Add(KeyCode.Z);Update(app);
            ActorAction command=g.companionAction;Check(command!=null&&command.kind=="command"&&!command.released,"Actual Z input begins the existing companion command");
            for(int frame=0;frame<30&&!command.released;frame++)Update(app);Check(command.released,"The real command reaches its unchanged contact window");
            if(companion==0)Check(g.projectiles.Count==0&&g.save.health>20&&g.effects.Exists(e=>e.kind=="heal"),"Mira command remains healing, with no invented Starfire projectile");
            else if(companion==2)Check(g.projectiles.Count==0&&Get<object>(g,"frost")!=null,"Sen command remains the existing frost field, with no invented projectile");
            else{Check(g.projectiles.Count==3&&g.projectiles.TrueForAll(p=>p.visual=="arrow"&&p.piercing),"Ivor command retains exactly three real piercing arrows");foreach(var p in g.projectiles)VerifyBody(app,p);}
        }
        // These are labelled metadata fixtures, not claims that hostile Starfire is spawned by current AI.
        // Retiring hostile arrows/orbs must keep existing frames; an explicit visual remains authoritative.
        foreach(string visual in new[]{"arrow","enemyArrow","starfire","orb","enemyOrb",null})foreach(bool explosive in new[]{false,true})
        {
            Prepare(app);var g=app.Game;var p=new Projectile{visual=visual,friendly=false,explosive=explosive,position=g.Position,velocity=new Point(-4,3),height=41,life=.73f,damage=17};g.projectiles.Add(p);Call(g,"RetireHostileProjectiles",null,0f);
            Effect fade=g.effects.Find(e=>e.kind=="projectileFade");int expected=Arrow(p)?8:Visual(p)=="starfire"?9:10;
            Check(!g.projectiles.Contains(p)&&fade!=null&&fade.variant==expected&&(fade.direction-p.velocity.Normalized).Length<.001f&&Near(fade.height,41),"Hostile retirement selects the resolved source frame, including unchanged Arrow/Orb cases");
            Check(p.visual==visual&&p.explosive==explosive&&Near(p.damage,17)&&Near(p.life,.73f)&&Near(p.velocity.x,-4)&&Near(p.velocity.y,3),"Fade metadata selection does not rewrite projectile damage, lifespan, collision/explosion flags or velocity");
        }
        Console.WriteLine("PASS real automatic Mira/Sen Starfire and Ivor arrows, both Update expiry branches, actual Z heal/frost/three-arrow commands and labelled hostile retirement metadata fixtures; NATIVE_UNVERIFIED");
    }
    private static void Record(StringBuilder text,GUI.TextureCall c)
    {text.Append(c.texture.name).Append('|');foreach(float v in new[]{c.rect.x,c.rect.y,c.rect.width,c.rect.height,c.uv.x,c.uv.y,c.uv.width,c.uv.height,c.color.r,c.color.g,c.color.b,c.color.a,c.matrix.m00,c.matrix.m01,c.matrix.m03,c.matrix.m10,c.matrix.m11,c.matrix.m13})text.Append(v.ToString("R",CultureInfo.InvariantCulture)).Append(',');text.Append(';');}
    private static string Digest(string value){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();}
    private static void Append(StringBuilder text,object value,HashSet<object> seen)
    {
        if(value==null){text.Append("null;");return;}Type type=value.GetType();if(type.IsPrimitive||type.IsEnum||value is string||value is decimal){text.Append(Convert.ToString(value,CultureInfo.InvariantCulture)).Append(';');return;}
        if(!type.IsValueType&&!seen.Add(value)){text.Append("ref;");return;}var list=value as System.Collections.IEnumerable;if(list!=null){foreach(object item in list)Append(text,item,seen);text.Append("end;");return;}
        var fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);Array.Sort(fields,(a,b)=>string.CompareOrdinal(a.Name,b.Name));foreach(var field in fields){text.Append(field.Name).Append('=');Append(text,field.GetValue(value),seen);}
    }
    private static string State(GameCore game){var text=new StringBuilder();Append(text,game,new HashSet<object>());return text.ToString();}
    private static string Files(){var text=new StringBuilder();var paths=Directory.GetFiles(Application.persistentDataPath,"*",SearchOption.AllDirectories);Array.Sort(paths,StringComparer.Ordinal);foreach(string path in paths)text.Append(path).Append(':').Append(Convert.ToBase64String(File.ReadAllBytes(path))).Append(';');return text.ToString();}
    private static void PreservationAndIsolation(FrontierRuntime app)
    {
        Prepare(app);var projectiles=new StringBuilder();var effects=new StringBuilder();int cases=0;
        foreach(string visual in new[]{"arrow","enemyArrow","orb","enemyOrb","starfire"})foreach(bool explosive in new[]{false,true})for(int heading=0;heading<8;heading++)
        {
            var p=new Projectile{visual=visual,explosive=explosive,friendly=visual=="arrow"||visual=="starfire"||visual=="orb",velocity=Direction(heading)*8,position=app.Game.Position,height=43,age=.37f,theme=7};Clear();Call(app,"DrawProjectileVisual",p);Check(GUI.textureCalls.Count==4,"Rich FX retains all three existing trail cells and one source body");
            for(int index=0;index<4;index++)if(index<3||visual!="starfire")Record(projectiles,GUI.textureCalls[index]);cases++;
        }
        string[] kinds={"slash","spark","impact","hurt","projectileImpact","projectileFade","magicImpact","release","frost","burst","heal","defeat","discover","danger","enemyRelease","dash","loot","number","break","enemyWindup","gateOpen"};
        foreach(string kind in kinds)for(int variant=0;variant<13;variant++)
        {
            if(kind=="projectileFade"&&variant==9)continue;
            var e=new Effect(kind,app.Game.Position,.6f,"23"){variant=variant,direction=Direction(variant%8),height=37,radius=1.2f};e.life=.37f;Clear();Call(app,"DrawCombatEffect",e);foreach(var draw in GUI.textureCalls)Record(effects,draw);
        }
        Console.WriteLine("UNCHANGED projectile bodies except Starfire + ALL trails: "+cases+" cases; sha256="+Digest(projectiles.ToString()));Console.WriteLine("UNCHANGED non-Starfire effect source/variant/transform records: sha256="+Digest(effects.ToString()));
        Prepare(app);Projectile live=PlayerShot(app,6);HudCamera(app,live);Call(Get<PixelArt>(app,"art"),"Glow");Draw(app);var art=Get<PixelArt>(app,"art");int owned=Get<List<Texture2D>>(art,"owned").Count,cache=Get<Dictionary<string,Texture2D>>(art,"cache").Count;string before=State(app.Game),files=Files();float clock=Get<float>(app,"clock");var matrix=Matrix4x4.TRS(new Vector3(9,21,0),Quaternion.Euler(0,0,13),new Vector3(1.2f,1.2f,1));var color=new Color(.3f,.4f,.7f,.6f);
        foreach(int fx in new[]{0,2})foreach(bool motion in new[]{false,true})foreach(EventType evt in new[]{EventType.Layout,EventType.Repaint})
        {
            app.Game.save.effectsQuality=fx;app.Game.save.reducedMotion=motion;before=State(app.Game);GUI.matrix=matrix;GUI.color=color;GUI.enabled=false;Event.current.type=evt;Draw(app);
            Check(State(app.Game)==before&&Near(Get<float>(app,"clock"),clock),"Layout/Repaint leaves all Core, save, projectile, action and presentation-clock state untouched");Check(Same(GUI.matrix,matrix)&&Same(GUI.color,color)&&!GUI.enabled,"Actual OnGUI restores the incoming matrix, color and enabled state");
            foreach(var draw in GUI.textureCalls)if(draw.texture.name.StartsWith("resource:Frontier/Art/ProjectileMasks/"))Check(draw.texture.pixelReads==0&&!draw.texture.destroyed,"Borrowed prepared masks require no CPU pixel access or destruction");
        }
        Check(Get<List<Texture2D>>(art,"owned").Count==owned&&Get<Dictionary<string,Texture2D>>(art,"cache").Count==cache,"Repeated Starfire presentation allocates no PixelArt textures or cache entries");Check((bool)Call(app,"Save")&&Files()==files,"Review draws and Save preserve real slot and backup bytes");
        GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;
        var control=new FrontierRuntime();Call(control,"Awake");try
        {
            PlayerShot(app,4);PlayerShot(control,4);Check(State(app.Game)==State(control.Game),"Paired actual J releases start from identical full gameplay state");
            for(int frame=0;frame<12;frame++){Input.Clear();Update(app);Update(control);before=State(app.Game);Draw(app);Check(State(app.Game)==before&&State(control.Game)==before,"Actual Update with and without rendering preserves the full flight/combat/save state at frame "+frame);}
        }
        finally{control.EndVisualReview();Call(control,"OnDestroy");Input.Clear();}
        Console.WriteLine("PASS immutable unchanged-render digests, GUI/Core/save/cache isolation and actual J flight Update with/without OnGUI; NATIVE_UNVERIFIED");
    }
    private static void Captures(FrontierRuntime app)
    {
        foreach(int heading in new[]{0,2,4,6}){PlayerShot(app,heading);Draw(app,"actual-j-world-"+heading);}
        foreach(int heading in new[]{2,4,6})
        {
            Projectile p=PlayerShot(app,heading);HudCamera(app,p);Draw(app,"actual-j-hud-"+heading);Expire(app,p);Draw(app,"update-expiry-short-life-"+heading);
        }
        foreach(int companion in new[]{0,2}){CompanionShot(app,companion,4);Draw(app,"auto-companion-"+companion);}
        Check(captures==12,"Twelve actual OnGUI fixtures: four J world headings, three HUD, three explicitly short-life expiry, two automatic companions");Console.WriteLine("CAPTURE 12 actual J/automatic companion/short-life-expiry OnGUI fixtures; staged QA camera/lifetime only; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-starfire-"+Guid.NewGuid());Application.persistentDataPath=directory;captureFolder=args.Length>0&&args[0]!="-"?args[0]:null;capturePrefix=args.Length>1?args[1]:"qa-starfire";bool only=args.Length>2&&args[2]=="capture-only";int failed=0;var app=new FrontierRuntime();
        try
        {
            Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"starfall-v1.json"),"{\"version\":1,\"profession\":0}");File.WriteAllText(Path.Combine(directory,"starfall-slot-1.json.bak"),"existing-slot-backup-sentinel");Call(app,"Awake");Check(app.Game!=null,"Actual production Awake loads the mature campaign");
            if(!only)foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{PlayerReleaseAndExpiry,CompanionRoutesAndCommands,PreservationAndIsolation})try{test(app);}catch(Exception ex){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(ex.InnerException??ex).Message);}
            if(captureFolder!=null)Captures(app);app.EndVisualReview();Call(app,"OnDestroy");
        }
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;if(Directory.Exists(directory))Directory.Delete(directory,true);}
        if(only){Console.WriteLine("Capture-only: semantic checks skipped; NATIVE_UNVERIFIED");return 0;}Console.WriteLine("Runtime Starfire alignment: "+(3-failed)+"/3 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
