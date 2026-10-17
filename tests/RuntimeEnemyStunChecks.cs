// Production Update, Hit, enemy/world/HUD drawing and Save using offline Unity
// declarations. These observations do not certify native GPU/import/play feel.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Starfall;
using UnityEngine;

public static class RuntimeEnemyStunChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static string captureFolder,capturePrefix;
    private static int captures;
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static object Call(object app,string name,params object[] args){var m=app.GetType().GetMethod(name,Private|BindingFlags.Public|BindingFlags.Static);Check(m!=null,"Existing production entry point: "+name);return m.Invoke(app,args);}
    private static T Get<T>(object app,string name){return (T)app.GetType().GetField(name,Private).GetValue(app);}
    private static void Set(object app,string name,object value){var field=app.GetType().GetField(name,Private);Check(field!=null,"Existing private fixture field: "+name);field.SetValue(app,value);}
    private static bool Near(float a,float b,float tolerance=.001f){return Math.Abs(a-b)<=tolerance;}
    private static bool Same(Rect a,Rect b){return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height);}
    private static bool Same(Color a,Color b){return Near(a.r,b.r)&&Near(a.g,b.g)&&Near(a.b,b.b)&&Near(a.a,b.a);}
    private static bool Same(Matrix4x4 a,Matrix4x4 b){return Near(a.m00,b.m00)&&Near(a.m01,b.m01)&&Near(a.m03,b.m03)&&Near(a.m10,b.m10)&&Near(a.m11,b.m11)&&Near(a.m13,b.m13);}
    private static bool Overlap(Rect a,Rect b){return a.x<b.x+b.width&&a.x+a.width>b.x&&a.y<b.y+b.height&&a.y+a.height>b.y;}
    private static bool Same(GUI.TextureCall a,GUI.TextureCall b){return a.texture.name==b.texture.name&&Same(a.rect,b.rect)&&Same(a.uv,b.uv)&&Same(a.color,b.color)&&Same(a.matrix,b.matrix);}
    private static void Clear(){GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();}
    private static Enemy Prepare(FrontierRuntime app,int route=0)
    {
        Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;Event.current.type=EventType.Repaint;
        int room=route<3?1:route<11?route-2:route==11?1:6;
        app.BeginVisualReview(0,room,route>=3&&route<11);app.VisualReviewPaused=false;var g=app.Game;
        Enemy e=route<3?g.enemies.Find(x=>!x.boss&&x.kind==route):g.enemies.Find(x=>x.boss);
        if(route>=11)
        {
            g.enemies.Clear();
            if(route==11){g.save.flags.Add("watch_complete");Check(app.SetVisualReviewPosition(g.SignalPoint),"Canonical Warden starts on legal signal feet");Check(g.StartSignalEncounter(),"Actual optional Warden start spawns its canonical identity");e=g.enemies.Find(GameCore.IsSignalWarden);}
            else{foreach(string flag in new[]{"personal_1","companion_1","boss_6","mirrors_lit","commission_accepted"})if(!g.Has(flag))g.save.flags.Add(flag);g.save.mirrors=new[]{1,2,1};Check(app.SetVisualReviewPosition(GameCore.CommissionReceiverPoint),"Canonical machine trial starts on legal receiver feet");Check(g.StartCommissionEncounter(),"Actual machine trial start spawns its canonical identity");e=g.enemies.Find(GameCore.IsCommissionGuardian);}
        }
        Check(e!=null,"Genuine existing encounter supplies route "+route);g.enemies.RemoveAll(x=>x!=e);g.hazards.Clear();g.projectiles.Clear();g.effects.Clear();g.fallen.Clear();g.save.companion=-1;g.save.reducedMotion=true;g.save.effectsQuality=0;g.save.showGuidance=false;g.save.english=true;g.save.teaUntil=g.save.totalTime+180;g.toastTime=0;g.invulnerable=100;g.playerFlash=0;
        e.state=0;e.timer=1000;e.stun=e.flash=e.hitTime=e.releaseTime=e.stagger=e.staggerRecovery=0;e.moving=e.backpedaling=false;
        Set(app,"camera",e.Position);Set(app,"clock",0f);Set(app,"combatHudTime",3f);Set(app,"applicationPaused",false);Set(app,"pointerSeen",false);Set(app,"controllerUsed",false);Set(app,"drawingOutline",false);Call(app,"Resize");return e;
    }
    // Expected geometry is independent of any newly added helper/API, allowing
    // the same assertions to compile against the immutable pre-change runtime.
    private static Rect Bounds(FrontierRuntime app,Enemy e){Point p=(Point)Call(app,"ScreenPoint",e.Position);return new Rect(p.x+(e.boss?52:26)+4,p.y-(e.boss?135:e.kind==0?54:79)-6,26,12);}
    private static List<GUI.TextureCall> Expected(FrontierRuntime app,Enemy e,Matrix4x4 matrix)
    {
        Rect b=Bounds(app,e);var result=new List<GUI.TextureCall>();Color dark=PixelArt.C("#101a2b");dark.a=.95f;Color bright=PixelArt.C("#ebc58b");
        for(int i=0;i<3;i++){float x=b.x+4+i*9,y=b.y+8-(i==1?4:0);Rect[] rects={new Rect(x-2,y-4,4,8),new Rect(x-4,y-2,8,4),new Rect(x-1,y-3,2,6),new Rect(x-3,y-1,6,2)};for(int j=0;j<4;j++)result.Add(new GUI.TextureCall{texture=Texture2D.whiteTexture,rect=rects[j],uv=new Rect(0,0,1,1),matrix=matrix,color=j<2?dark:bright});}return result;
    }
    private static List<GUI.TextureCall> Markers(FrontierRuntime app,Enemy e){var expected=Expected(app,e,GUI.matrix);return GUI.textureCalls.FindAll(c=>expected.Exists(x=>Same(c,x)));}
    private static void Verify(FrontierRuntime app,Enemy e,int copies=1)
    {
        var expected=Expected(app,e,GUI.matrix);var actual=Markers(app,e);Check(actual.Count==12*copies,"Three complete dark-backed pixel stars must describe the actual remaining stun window (calls="+actual.Count+")");
        for(int i=0;i<actual.Count;i++)Check(Same(actual[i],expected[i%12]),"Whole three-star glyph keeps stable feet/bar anchor, dark-before-gold order and exact coverage");
    }
    private static void EnemyDraw(FrontierRuntime app,Enemy e){Clear();Call(app,"DrawEnemyWithBars",e);}
    private static void Draw(FrontierRuntime app,string capture=null){Clear();Call(app,"OnGUI");if(captureFolder!=null&&capture!=null){var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();GUI.Export(captureFolder,capturePrefix+"-"+capture);captures++;GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);}}
    private static void Update(FrontierRuntime app){Call(app,"Update");Input.keys.Clear();}
    private static void Hit(GameCore game,Enemy enemy,float damage=1){Call(game,"Hit",enemy,damage,false,(Point?)null,null);}
    private static void ActualBreakContactAndLifetime(FrontierRuntime app)
    {
        foreach(int route in new[]{0,4})
        {
            Enemy e=Prepare(app,route);var g=app.Game;e.stagger=(e.boss?95:30)-.1f;float health=e.hp;Hit(g,e);
            Check(e.hp<health&&e.stagger==0&&e.stun>1&&e.hitTime>0&&g.effects.Exists(x=>x.kind=="break"),"Actual Hit creates real break, damage, recovery and the existing brief word");
            for(int i=0;i<40;i++)Update(app);
            Check(e.stun>0&&e.hitTime<=0&&e.flash<=0&&!g.effects.Exists(x=>x.kind=="break")&&(int)Call(app,"EnemyPose",e)==0,"After .8 seconds real break remains active beyond both hit pose and old break word");EnemyDraw(app,e);Verify(app,e);
            float remaining=e.stun;Check(remaining>0&&remaining<(e.boss?1.1f:1.7f),"Core retains original Boss/ordinary break duration");int frames=0;
            while(e.stun>0&&frames++<100){Update(app);EnemyDraw(app,e);if(e.stun>0)Verify(app,e);else Check(Markers(app,e).Count==0,"The same update that expires control removes every star");}Check(frames<100,"Real control reaches its existing recovery boundary");
        }
        Enemy target=Prepare(app);var game=app.Game;Point foot=target.Position+new Point(-.9f,0);Check(game.CanStand(foot)&&app.SetVisualReviewPosition(foot)&&(game.Position-foot).Length<.001f,"Sword contact uses exact legal existing terrain feet");target.stagger=29.9f;float hp=target.hp;
        Point screen=(Point)Call(app,"ScreenPoint",target.Position);Input.mousePosition=new Vector3(screen.x,Screen.height-screen.y,0);Input.held.Add(KeyCode.J);Update(app);Input.held.Clear();Check(game.playerAction!=null&&!game.playerAction.released&&target.hp==hp,"Actual J accepts the original sword preparation before damage");int count=0;while(target.hp==hp&&count++<50)Update(app);Check(count<50&&game.enemies.Contains(target)&&target.hp<hp&&target.stun>0,"Real accepted sword contact, not a synthetic timer write, breaks a living enemy");EnemyDraw(app,target);Verify(app,target);
        Console.WriteLine("PASS genuine ordinary/Boss Hit breaks remain visible after .8 s and expire with Core; actual J preparation/contact also creates the marker; NATIVE_UNVERIFIED");
    }
    private static void RoutesPoliciesAndOriginalRendering(FrontierRuntime app)
    {
        int routes=0;
        for(int route=0;route<13;route++)
        {
            Enemy e=Prepare(app,route);e.stun=.4f;EnemyDraw(app,e);Verify(app,e);var expected=Expected(app,e,GUI.matrix);var retained=GUI.textureCalls.FindAll(c=>!expected.Exists(x=>Same(x,c)));e.stun=0;EnemyDraw(app,e);
            var original=new List<GUI.TextureCall>(GUI.textureCalls);
            // Removing stun now legitimately restores the new frontal guard.
            // Subtract only its exact owned submissions, then retain every
            // original body/shadow/bar assertion. Older archives have no cue.
            var guard=app.GetType().GetMethod("DrawEnemyGuard",Private);
            if(guard!=null)
            {
                Clear();guard.Invoke(app,new object[]{e});
                foreach(var mark in GUI.textureCalls){int index=original.FindIndex(c=>Same(c,mark));Check(index>=0,"Every guard-owned submission exists in the actual unstunned actor draw");original.RemoveAt(index);}
            }
            Check(original.Count==retained.Count,"Status cues leave the original enemy/body/shadow/bars draw count unchanged");for(int i=0;i<retained.Count;i++)Check(Same(retained[i],original[i]),"All original enemy/body/shadow/bar frame, UV, color, geometry and order stay exact");
            foreach(float stun in new[]{-.01f,0f,.00001f,.4f,2.2f}){e.stun=stun;EnemyDraw(app,e);if(stun>0)Verify(app,e);else Check(Markers(app,e).Count==0,"Zero/negative remaining control has no marker");}e.stun=.4f;
            foreach(bool motion in new[]{false,true})foreach(int fx in new[]{0,1,2})foreach(float clock in new[]{0f,91.2f}){app.Game.save.reducedMotion=motion;app.Game.save.effectsQuality=fx;Set(app,"clock",clock);EnemyDraw(app,e);Verify(app,e);}
            foreach(string mode in new[]{"pause","dialog","inventory","title"}){app.Game.mode=mode;EnemyDraw(app,e);Check(Markers(app,e).Count==0,"World-only marker cannot cover "+mode);}app.Game.mode="world";e.hp=0;EnemyDraw(app,e);Check(Markers(app,e).Count==0,"A dead actor cannot retain stars");e.hp=e.maxHp;EnemyDraw(app,e);Verify(app,e);
            app.Game.practicing=true;EnemyDraw(app,e);Check(Markers(app,e).Count==0,"Safe dummy never reports a live stun");Set(app.Game,"sparringSession",true);EnemyDraw(app,e);Verify(app,e);
            foreach(string prop in new[]{"SparringAwaitingNext","SparringFinished"}){Set(app.Game,"<"+prop+">k__BackingField",true);EnemyDraw(app,e);Check(Markers(app,e).Count==0,"Cleared/finished actual sparring review suppresses marker");Set(app.Game,"<"+prop+">k__BackingField",false);}app.Game.practicing=false;Set(app.Game,"sparringSession",false);
            app.Game.enemies.Remove(e);Draw(app);Check(Markers(app,e).Count==0,"Removed encounter actor leaves no stale world or HUD status");routes++;
        }
        Check(routes==13,"Three archetypes, eight mature Bosses and two genuine optional guardians covered");Console.WriteLine("PASS 13 canonical encounter/art routes, unchanged original body/shadow/bar submissions, exact expiry/life/menu/practice/drill guards and static low/rich-FX/reduced-motion policy; NATIVE_UNVERIFIED");
    }
    private static Matrix4x4 Canvas(){float scale=Math.Min(Screen.width/1280f,Screen.height/800f);return Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)/2,(Screen.height-800*scale)/2,0),Quaternion.identity,new Vector3(scale,scale,1));}
    private static void ScreenFoot(FrontierRuntime app,Enemy e,Point point){Set(app,"camera",new Point(e.x-(point.x-640)/36,e.y-(point.y-390)/36));}
    private static void HudGrazingAndOrder(FrontierRuntime app)
    {
        var panel=new Rect(20,20,260,77);
        foreach(int width in new[]{960,1280,1920})
        {
            Enemy e=Prepare(app);Screen.width=width;Screen.height=width==960?640:width==1920?1080:800;Call(app,"Resize");e.stun=.8f;bool found=false;
            for(int x=-45;x<30&&!found;x++)for(int y=70;y<160&&!found;y++){ScreenFoot(app,e,new Point(x,y));if(Overlap(panel,Bounds(app,e))&&!Overlap(panel,(Rect)Call(app,"EnemyHudBounds",e)))found=true;}
            Check(found,"A star-only grazing fixture is outside the original enemy body envelope");Check((bool)Call(app,"ThreatOver",panel),"The actual star-only graze fades the same HUD panel");GUI.matrix=Canvas();Clear();Call(app,"DrawHudThreatWitness");Verify(app,e);Check(GUI.textureCalls.Count==12,"Only status glyph is redrawn once when original body lies outside every HUD region");
            Draw(app);GUI.matrix=Canvas();Verify(app,e,2);var expected=Expected(app,e,GUI.matrix);int hud=GUI.textureCalls.FindIndex(c=>c.texture==Texture2D.whiteTexture&&Same(c.rect,panel)),last=GUI.textureCalls.FindLastIndex(c=>expected.Exists(v=>Same(c,v)));Check(hud>=0&&last>hud,"Actual OnGUI repeats the status after, never below, its overlapping combat HUD");GUI.matrix=Matrix4x4.identity;
            ScreenFoot(app,e,new Point(640,390));Clear();Call(app,"DrawHudThreatWitness");Check(Markers(app,e).Count==0,"Open-world status is not redundantly copied onto HUD");
        }
        Enemy target=Prepare(app);target.stun=.8f;ScreenFoot(app,target,new Point(-10,110));app.Game.mode="pause";Clear();Call(app,"DrawHudThreatWitness");Check(Markers(app,target).Count==0,"Paused HUD never redraws status over a menu");
        Console.WriteLine("PASS real star-only HUD grazing beyond body bounds, shared panel fading and exactly one post-HUD redraw at 960/1280/1920; NATIVE_UNVERIFIED");
    }
    private static void Append(StringBuilder text,object value,HashSet<object> seen)
    {
        if(value==null){text.Append("null;");return;}Type type=value.GetType();if(type.IsPrimitive||type.IsEnum||value is string||value is decimal){text.Append(Convert.ToString(value,CultureInfo.InvariantCulture)).Append(';');return;}if(!type.IsValueType&&!seen.Add(value)){text.Append("ref;");return;}var seq=value as System.Collections.IEnumerable;if(seq!=null){foreach(object item in seq)Append(text,item,seen);text.Append("end;");return;}var fields=type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);Array.Sort(fields,(a,b)=>string.CompareOrdinal(a.Name,b.Name));foreach(var f in fields){text.Append(f.Name).Append('=');Append(text,f.GetValue(value),seen);}
    }
    private static string State(GameCore g){var text=new StringBuilder();Append(text,g,new HashSet<object>());return text.ToString();}
    private static string Files(){var text=new StringBuilder();var paths=Directory.GetFiles(Application.persistentDataPath,"*",SearchOption.AllDirectories);Array.Sort(paths,StringComparer.Ordinal);foreach(string path in paths)text.Append(path).Append(':').Append(Convert.ToBase64String(File.ReadAllBytes(path))).Append(';');return text.ToString();}
    private static void IsolationAndRealUpdateControl(FrontierRuntime app)
    {
        Enemy e=Prepare(app);e.stun=.8f;var art=Get<PixelArt>(app,"art");Call(art,"Glow");Draw(app);var owned=Get<List<Texture2D>>(art,"owned");var cache=Get<Dictionary<string,Texture2D>>(art,"cache");int owns=owned.Count,caches=cache.Count;string files=Files();float clock=Get<float>(app,"clock");var matrix=Matrix4x4.TRS(new Vector3(19,31,0),Quaternion.Euler(0,0,17),new Vector3(1.4f,1.4f,1));var tint=new Color(.3f,.5f,.7f,.8f);
        foreach(bool motion in new[]{false,true})foreach(int fx in new[]{0,2})foreach(EventType evt in new[]{EventType.Layout,EventType.Repaint}){app.Game.save.reducedMotion=motion;app.Game.save.effectsQuality=fx;Event.current.type=evt;string state=State(app.Game);GUI.matrix=matrix;GUI.color=tint;GUI.enabled=false;Draw(app);Check(State(app.Game)==state&&Near(Get<float>(app,"clock"),clock),"Actual layout/repaint leaves every Core/save/encounter field and presentation clock untouched");Check(Same(GUI.matrix,matrix)&&Same(GUI.color,tint)&&!GUI.enabled,"Actual OnGUI restores incoming GUI matrix/color/enabled");}
        Check(owns==owned.Count&&caches==cache.Count,"Static white-texture markers allocate no PixelArt texture or cache entry");Check((bool)Call(app,"Save")&&Files()==files,"Review Save and drawing preserve existing save/slot/backup bytes");GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;
        var control=new FrontierRuntime();Call(control,"Awake");try{e=Prepare(app);Enemy other=Prepare(control);e.stagger=other.stagger=29.9f;Hit(app.Game,e);Hit(control.Game,other);int frames=0;bool recovered=false;while(frames++<110){Input.Clear();Update(app);Update(control);string state=State(app.Game);Check(State(control.Game)==state,"Actual rendered/unrendered Update has identical complete Hit/control/recovery state at frame "+frames);Draw(app);Check(State(app.Game)==state,"OnGUI cannot change subsequent combat progression");recovered|=e.stun<=0;}Check(recovered,"Unchanged real AI recovers after the original control window");}finally{control.EndVisualReview();Call(control,"OnDestroy");}
        Console.WriteLine("PASS full Core/save/GUI/clock/cache isolation and 110 real rendered-vs-unrendered Update frames from Hit through stun and recovery; NATIVE_UNVERIFIED");
    }
    private static void Captures(FrontierRuntime app)
    {
        foreach(int route in new[]{0,1,2,3,7,10,11,12}){Enemy e=Prepare(app,route);e.stun=.8f;Draw(app,"route-"+route);}
        Enemy target=Prepare(app);target.stun=.8f;ScreenFoot(app,target,new Point(-10,110));Draw(app,"hud-graze");target=Prepare(app);target.stun=.8f;Screen.width=960;Screen.height=640;Draw(app,"small-lowfx");target=Prepare(app);target.stun=.8f;Screen.width=1920;Screen.height=1080;app.Game.save.effectsQuality=2;app.Game.save.reducedMotion=false;Draw(app,"wide-richfx");target=Prepare(app);target.stun=0;Draw(app,"recovered");Check(captures==12,"Twelve comparable actual OnGUI status fixtures");Console.WriteLine("CAPTURE 12 actual OnGUI stun fixtures; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-enemy-stun-"+Guid.NewGuid());Application.persistentDataPath=directory;captureFolder=args.Length>0&&args[0]!="-"?args[0]:null;capturePrefix=args.Length>1?args[1]:"qa-enemy-stun";bool only=args.Length>2&&args[2]=="capture-only";int failed=0;var app=new FrontierRuntime();
        try{Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"starfall-v1.json"),"{\"version\":1,\"profession\":0}");File.WriteAllText(Path.Combine(directory,"starfall-slot-1.json.bak"),"existing-slot-backup-sentinel");Call(app,"Awake");Check(app.Game!=null,"Production Awake loads campaign");if(!only)foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{ActualBreakContactAndLifetime,RoutesPoliciesAndOriginalRendering,HudGrazingAndOrder,IsolationAndRealUpdateControl})try{test(app);}catch(Exception ex){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(ex.InnerException??ex).ToString());}if(captureFolder!=null)Captures(app);app.EndVisualReview();Call(app,"OnDestroy");}
        finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;if(Directory.Exists(directory))Directory.Delete(directory,true);}if(only){Console.WriteLine("Capture-only: semantic checks skipped; NATIVE_UNVERIFIED");return 0;}Console.WriteLine("Runtime enemy stun: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
