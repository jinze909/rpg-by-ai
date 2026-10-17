// Actual Hit / J / Update / OnGUI using offline declarations. Positions, short
// phase boundaries and HUD camera are explicitly staged fixtures, not playtests.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Starfall;
using UnityEngine;

public static class RuntimeHeavyGuardChecks
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly Color Dark=new Color(.025f,.045f,.06f,.95f),Bright=new Color(145f/255,179f/255,204f/255,1);
    private const float Radius=.8f*36,Padding=4;
    private static string captureFolder,capturePrefix; private static int captures;
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static object Call(object app,string name,params object[] args){var m=app.GetType().GetMethod(name,Private|BindingFlags.Public|BindingFlags.Static);Check(m!=null,"Existing production entry point: "+name);return m.Invoke(app,args);}
    private static T Get<T>(object app,string name){return (T)app.GetType().GetField(name,Private).GetValue(app);}
    private static void Set(object app,string name,object value){var field=app.GetType().GetField(name,Private);Check(field!=null,"Existing fixture field "+name);field.SetValue(app,value);}
    private static bool Near(float a,float b,float tolerance=.003f){return Math.Abs(a-b)<=tolerance;}
    private static bool Same(Rect a,Rect b){return Near(a.x,b.x)&&Near(a.y,b.y)&&Near(a.width,b.width)&&Near(a.height,b.height);}
    private static bool Same(Color a,Color b){return Near(a.r,b.r)&&Near(a.g,b.g)&&Near(a.b,b.b)&&Near(a.a,b.a);}
    private static bool Same(Matrix4x4 a,Matrix4x4 b){return Near(a.m00,b.m00)&&Near(a.m01,b.m01)&&Near(a.m03,b.m03)&&Near(a.m10,b.m10)&&Near(a.m11,b.m11)&&Near(a.m13,b.m13);}
    private static bool Same(GUI.TextureCall a,GUI.TextureCall b){return a.texture.name==b.texture.name&&Same(a.rect,b.rect)&&Same(a.uv,b.uv)&&Same(a.color,b.color)&&Same(a.matrix,b.matrix);}
    private static bool Overlap(Rect a,Rect b){return a.x<b.x+b.width&&a.x+a.width>b.x&&a.y<b.y+b.height&&a.y+a.height>b.y;}
    private static void Clear(){GUI.commands.Clear();GUI.textureCalls.Clear();GUI.buttonCalls.Clear();}
    private static Enemy Prepare(FrontierRuntime app,int kind=2,bool boss=false)
    {
        Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Screen.width=1280;Screen.height=800;Time.unscaledDeltaTime=.02f;Event.current.type=EventType.Repaint;
        app.BeginVisualReview(0,1,boss);app.VisualReviewPaused=false;var g=app.Game;Enemy e=g.enemies.Find(x=>x.boss==boss&&(boss||x.kind==kind));Check(e!=null,"Canonical shipped forest encounter provides Heavy/archetype/Boss");g.enemies.RemoveAll(x=>x!=e);g.hazards.Clear();g.projectiles.Clear();g.effects.Clear();g.fallen.Clear();g.save.companion=-1;g.save.reducedMotion=true;g.save.effectsQuality=0;g.save.showGuidance=false;g.save.english=true;g.save.largeText=false;g.toastTime=0;g.invulnerable=100;g.playerFlash=0;
        e.state=0;e.timer=1000;e.stun=e.flash=e.hitTime=e.releaseTime=e.stagger=e.staggerRecovery=0;e.moving=e.backpedaling=false;e.facing=new Point(1,0);
        Set(app,"camera",e.Position);Set(app,"clock",0f);Set(app,"combatHudTime",3f);Set(app,"applicationPaused",false);Set(app,"pointerSeen",false);Set(app,"pointerAimActive",false);Set(app,"controllerUsed",false);Set(app,"resumeTime",0f);Set(app,"drawingOutline",false);Call(app,"Resize");return e;
    }
    private static Rect Bounds(FrontierRuntime app,Enemy e){Point p=(Point)Call(app,"ScreenPoint",e.Position);return new Rect(p.x-Radius-Padding,p.y-Radius-Padding,(Radius+Padding)*2,(Radius+Padding)*2);}
    private static List<GUI.TextureCall> Marks(){return GUI.textureCalls.FindAll(c=>c.texture==Texture2D.whiteTexture&&(Same(c.color,Dark)&&Near(c.rect.height,7)||Same(c.color,Bright)&&Near(c.rect.height,3)));}
    private static void Verify(FrontierRuntime app,Enemy e,int copies=1)
    {
        var marks=Marks();Check(marks.Count==48*copies,"A ready Heavy must show exactly one dark-backed open frontal arc BEFORE contact; calls="+marks.Count);Point p=(Point)Call(app,"ScreenPoint",e.Position);float angle=(float)Math.Atan2(e.facing.y,e.facing.x),half=(float)Math.Acos(.45f);
        for(int copy=0;copy<copies;copy++)for(int layer=0;layer<2;layer++)for(int segment=0;segment<24;segment++)
        {
            var line=marks[copy*48+layer*24+segment];float width=layer==0?7:3;float a=angle-half+2*half*segment/24,b=angle-half+2*half*(segment+1)/24;Point from=p+new Point((float)Math.Cos(a),(float)Math.Sin(a))*Radius,to=p+new Point((float)Math.Cos(b),(float)Math.Sin(b))*Radius;float length=(to-from).Length;Rect rect=new Rect((from.x+to.x-length)/2,(from.y+to.y-width)/2,length,width);float rotation=(float)(Math.Atan2(to.y-from.y,to.x-from.x)*180/Math.PI);var matrix=GuiGeometry.RotateInCanvas(GUI.matrix,rect,rotation);
            Check(Same(line.rect,rect)&&Same(line.matrix,matrix)&&Same(line.uv,new Rect(0,0,1,1))&&Same(line.color,layer==0?Dark:Bright),"24 exact arc chords share armor .45 cone, feet anchor, radius and dark-before-bright canvas transform; no radial/fill wedge");
        }
    }
    private static void EnemyDraw(FrontierRuntime app,Enemy e){Clear();Call(app,"DrawEnemyWithBars",e);}
    private static void Draw(FrontierRuntime app,string capture=null){Clear();Call(app,"OnGUI");if(captureFolder!=null&&capture!=null){var commands=GUI.commands.ToArray();var textures=GUI.textureCalls.ToArray();var buttons=GUI.buttonCalls.ToArray();GUI.Export(captureFolder,capturePrefix+"-"+capture);captures++;GUI.commands.AddRange(commands);GUI.textureCalls.AddRange(textures);GUI.buttonCalls.AddRange(buttons);}}
    private static void Update(FrontierRuntime app){Call(app,"Update");Input.Clear();}
    private static void Hit(GameCore game,Enemy enemy,bool technique=false,Point? incoming=null){Call(game,"Hit",enemy,10f,technique,incoming,new ImpactProfile(0,0,0,0,0,true));}
    private static float Contact(FrontierRuntime app,bool front,bool actualJ=false)
    {
        Enemy e=Prepare(app);var g=app.Game;Point foot=e.Position+new Point(-.9f,0);Check(g.CanStand(foot)&&app.SetVisualReviewPosition(foot),"Actual Heavy has legal sword-contact feet");e.facing=new Point(front?-1:1,0);float hp=e.hp;e.staggerRecovery=100;EnemyDraw(app,e);Verify(app,e);
        if(actualJ){Point p=(Point)Call(app,"ScreenPoint",e.Position);Input.mousePosition=new Vector3(p.x,Screen.height-p.y,0);Input.held.Add(KeyCode.J);Update(app);Check(g.playerAction!=null&&!g.playerAction.released&&e.hp==hp,"Accepted real J preserves preparation without early damage");int n=0;while(e.hp==hp&&n++<60)Update(app);Check(n<60&&e.hp>0&&e.hp<hp,"Actual J reaches still-living canonical Heavy");}
        else Hit(g,e);
        Check(g.effects.Exists(x=>x.kind=="number"&&x.text=="GUARDED")==front,"Real front/flank contact guard text exactly matches the displayed armor facing");return hp-e.hp;
    }
    private static void RealContactsAndPhases(FrontierRuntime app)
    {
        Check(Near(Contact(app,true),Contact(app,false)*.4f),"Original frontal .4 damage multiplier and flank full damage stay exact");float front=Contact(app,true,true),flank=Contact(app,false,true);Check(Near(front,flank*.4f),"Real prepared J produces the same original front/flank damage ratio");
        foreach(float cosine in new[]{.4499f,.4501f}){Enemy edge=Prepare(app);edge.facing=new Point(1,0);edge.staggerRecovery=100;float hp=edge.hp;Hit(app.Game,edge,false,new Point(-cosine,-(float)Math.Sqrt(1-cosine*cosine)));Check(Near(hp-edge.hp,cosine>.45f?4:10),"Actual directional Hit retains strict .45 frontal boundary");}
        foreach(int policy in new[]{0,1,2}){Enemy open=Prepare(app);open.staggerRecovery=100;open.facing=new Point(-1,0);Point contact=open.Position+new Point(-.9f,0);Check(app.SetVisualReviewPosition(contact),"Legal bypass fixture feet");if(policy==0)open.state=1;if(policy==1)open.stun=.4f;float hp=open.hp;Hit(app.Game,open,policy==2);Check(Near(hp-open.hp,10)&&!app.Game.effects.Exists(x=>x.text=="GUARDED"),"Existing wind-up, stun and technique each bypass frontal reduction");}
        Enemy e=Prepare(app);var g=app.Game;Point foot=e.Position+new Point(-.9f,0);Check(app.SetVisualReviewPosition(foot),"Legal warning approach");e.timer=0;Update(app);Check(e.state==1,"Real AI enters its existing wind-up");EnemyDraw(app,e);Check(Marks().Count==0,"Real wind-up opens the visible armor");int frames=0;while(e.state==1&&frames++<60)Update(app);Check(frames<60&&e.state==0&&e.releaseTime>0,"Real AI releases the original slam");EnemyDraw(app,e);Verify(app,e);e.hitTime=.1f;EnemyDraw(app,e);Verify(app,e);
        e.stagger=29.9f;e.staggerRecovery=0;Hit(g,e);Check(e.stun>0&&g.effects.Exists(x=>x.kind=="break"),"Genuine Hit creates original stagger break");EnemyDraw(app,e);Check(Marks().Count==0,"Real stun opens armor despite resting state0");frames=0;while(e.stun>0&&frames++<120)Update(app);Check(frames<120,"Real stun expires normally");e.timer=1000;EnemyDraw(app,e);Verify(app,e);
        Console.WriteLine("PASS actual shipped Heavy Hit and J front/flank damage, genuine AI windup/release and break/stun recovery; hit/release retain armor; NATIVE_UNVERIFIED");
    }
    private static void GeometryPoliciesAndPreservation(FrontierRuntime app)
    {
        Enemy e=Prepare(app);for(int heading=0;heading<8;heading++){float a=(float)(heading*Math.PI/4);e.Face(new Point((float)Math.Cos(a),(float)Math.Sin(a)));EnemyDraw(app,e);Verify(app,e);}
        e.facing=new Point(1,0);e.hp=e.maxHp-1;e.stagger=1;EnemyDraw(app,e);var preserved=GUI.textureCalls.FindAll(c=>!Marks().Exists(m=>Same(c,m)));e.stun=0;e.state=2;EnemyDraw(app,e);Check(GUI.textureCalls.Count==preserved.Count,"Removing only guard leaves original body/shadow/HP/stagger bar submission count");for(int i=0;i<preserved.Count;i++)Check(Same(preserved[i],GUI.textureCalls[i]),"Original body source, UV, tint, transform and all bars retained exactly");e.state=0;
        foreach(bool motion in new[]{false,true})foreach(int fx in new[]{0,1,2})foreach(float clock in new[]{0f,73f}){gset(app,motion,fx);Set(app,"clock",clock);EnemyDraw(app,e);Verify(app,e);}
        foreach(string mode in new[]{"pause","dialog","inventory","title"}){app.Game.mode=mode;EnemyDraw(app,e);Check(Marks().Count==0,"No world armor over "+mode);}app.Game.mode="world";
        foreach(float stun in new[]{.00001f,.4f}){e.stun=stun;EnemyDraw(app,e);Check(Marks().Count==0,"Positive actual stun disables armor");}e.stun=0;
        e.hp=0;EnemyDraw(app,e);Check(Marks().Count==0,"Dead Heavy has no armor cue");e.hp=e.maxHp;
        foreach(Point facing in new[]{new Point(0,0),new Point(float.NaN,0),new Point(0,float.PositiveInfinity)}){e.facing=facing;EnemyDraw(app,e);Check(Marks().Count==0,"Invalid/zero facing never submits invalid armor geometry");}e.facing=new Point(1,0);
        app.Game.practicing=true;EnemyDraw(app,e);Check(Marks().Count==0,"Safe practice dummy does not impersonate armor");Set(app.Game,"sparringSession",true);EnemyDraw(app,e);Verify(app,e);foreach(string prop in new[]{"SparringAwaitingNext","SparringFinished"}){Set(app.Game,"<"+prop+">k__BackingField",true);EnemyDraw(app,e);Check(Marks().Count==0,"Cleared or finished drill suppresses guard");Set(app.Game,"<"+prop+">k__BackingField",false);}app.Game.practicing=false;Set(app.Game,"sparringSession",false);
        Set(app,"camera",e.Position+new Point(100,100));EnemyDraw(app,e);Check(Marks().Count==0,"Offscreen guard is culled");
        foreach(int kind in new[]{0,1}){e=Prepare(app,kind);EnemyDraw(app,e);Check(Marks().Count==0,"Other canonical archetypes do not acquire Heavy armor");}e=Prepare(app,0,true);EnemyDraw(app,e);Check(Marks().Count==0,"Existing Boss has no ordinary Heavy armor");
        app.BeginVisualReview(0,0);app.VisualReviewPaused=false;var drill=app.Game;drill.enemies.Clear();var mentor=Array.Find(drill.Map.objects,x=>x.kind=="mentor");Check(mentor!=null&&app.SetVisualReviewPosition(new Point(mentor.x,mentor.y+1)),"Actual Ada mentor is reachable");drill.save.practiceBits=7;Check(drill.StartSparring()&&drill.SparringActive,"Original Ada API starts a genuine live drill");Enemy hunter=drill.enemies.Find(x=>x.kind==0);Check(hunter!=null,"Actual first drill owns its Hunter");Call(drill,"Hit",hunter,10000f,true,(Point?)null,new ImpactProfile(0,0,0,0,0,true));int settle=0;while(!drill.SparringAwaitingNext&&settle++<30)Update(app);Check(drill.SparringAwaitingNext&&drill.ContinueSparring(),"Actual clear and manual Continue create the second drill");e=drill.enemies.Find(x=>x.kind==2);Check(e!=null&&drill.IsSparringEnemy(e),"Actual second drill owns a canonical Heavy");Set(app,"camera",e.Position);EnemyDraw(app,e);Verify(app,e);
        Console.WriteLine("PASS eight facing cones, original body/shadow/bar preservation, low/rich FX and reduced motion, life/menu/practice/drill/facing/viewport guards; NATIVE_UNVERIFIED");
    }
    private static void gset(FrontierRuntime app,bool motion,int fx){app.Game.save.reducedMotion=motion;app.Game.save.effectsQuality=fx;}
    private static Matrix4x4 Canvas(){float scale=Math.Min(Screen.width/1280f,Screen.height/800f);return Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)/2,(Screen.height-800*scale)/2,0),Quaternion.identity,new Vector3(scale,scale,1));}
    private static void ScreenFoot(FrontierRuntime app,Enemy e,Point p){Set(app,"camera",new Point(e.x-(p.x-640)/36,e.y-(p.y-390)/36));}
    private static void HudBoundsAndOrder(FrontierRuntime app)
    {
        Rect panel=new Rect(20,20,260,77);
        foreach(int width in new[]{960,1280,1920})
        {
            Enemy e=Prepare(app);e.facing=new Point(0,-1);Screen.width=width;Screen.height=width==960?640:width==1920?1080:800;Call(app,"Resize");bool found=false;
            for(int x=-45;x<45&&!found;x++)for(int y=0;y<160&&!found;y++){ScreenFoot(app,e,new Point(x,y));Rect b=Bounds(app,e);if(Overlap(b,panel)&&!Overlap((Rect)Call(app,"EnemyHudBounds",e),panel)&&b.x+b.width>20&&b.x<280&&b.y+b.height>20&&b.y<97)found=true;}
            Check(found,"Guard-only shared-bounds graze exists outside actual body envelope");Check((bool)Call(app,"ThreatOver",panel),"Guard-only graze fades overlapping actual HUD panel");GUI.matrix=Canvas();Clear();Call(app,"DrawHudThreatWitness");Verify(app,e);Check(GUI.textureCalls.Count==48,"A guard-only graze redraws exactly one arc, not an opaque body");Draw(app);GUI.matrix=Canvas();Verify(app,e,2);int hud=GUI.textureCalls.FindIndex(c=>c.texture==Texture2D.whiteTexture&&Same(c.rect,panel));var marks=Marks();int last=GUI.textureCalls.FindLastIndex(c=>marks.Exists(m=>Same(c,m)));Check(hud>=0&&last>hud,"Actual OnGUI repeats armor after the overlapping HUD");GUI.matrix=Matrix4x4.identity;
            ScreenFoot(app,e,new Point(640,390));Clear();Call(app,"DrawHudThreatWitness");Check(Marks().Count==0,"No whole-scene HUD guard pass in open world");
        }
        Console.WriteLine("PASS shared guard-only HUD grazing, one post-HUD arc and no whole-scene duplicate at 960/1280/1920; NATIVE_UNVERIFIED");
    }
    private static void Append(StringBuilder text,object value,HashSet<object> seen){if(value==null){text.Append("null;");return;}Type t=value.GetType();if(t.IsPrimitive||t.IsEnum||value is string||value is decimal){text.Append(Convert.ToString(value,CultureInfo.InvariantCulture)).Append(';');return;}if(!t.IsValueType&&!seen.Add(value)){text.Append("ref;");return;}var seq=value as System.Collections.IEnumerable;if(seq!=null){foreach(object item in seq)Append(text,item,seen);text.Append("end;");return;}var fields=t.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);Array.Sort(fields,(a,b)=>string.CompareOrdinal(a.Name,b.Name));foreach(var f in fields){text.Append(f.Name).Append('=');Append(text,f.GetValue(value),seen);}}
    private static string State(GameCore game){var s=new StringBuilder();Append(s,game,new HashSet<object>());return s.ToString();}
    private static string Files(){var s=new StringBuilder();var paths=Directory.GetFiles(Application.persistentDataPath,"*",SearchOption.AllDirectories);Array.Sort(paths,StringComparer.Ordinal);foreach(var p in paths)s.Append(p).Append(':').Append(Convert.ToBase64String(File.ReadAllBytes(p))).Append(';');return s.ToString();}
    private static void Isolation(FrontierRuntime app)
    {
        Enemy e=Prepare(app);var art=Get<PixelArt>(app,"art");Call(art,"Glow");Draw(app);int own=Get<List<Texture2D>>(art,"owned").Count,cache=Get<Dictionary<string,Texture2D>>(art,"cache").Count;string files=Files();float clock=Get<float>(app,"clock");var matrix=Matrix4x4.TRS(new Vector3(17,23,0),Quaternion.Euler(0,0,13),new Vector3(1.3f,1.3f,1));var tint=new Color(.3f,.5f,.7f,.8f);
        foreach(bool motion in new[]{false,true})foreach(int fx in new[]{0,2})foreach(EventType evt in new[]{EventType.Layout,EventType.Repaint}){gset(app,motion,fx);Event.current.type=evt;string state=State(app.Game);GUI.matrix=matrix;GUI.color=tint;GUI.enabled=false;Draw(app);Check(State(app.Game)==state&&Near(Get<float>(app,"clock"),clock),"Actual layout/repaint is completely Core/save/clock read-only");Check(Same(GUI.matrix,matrix)&&Same(GUI.color,tint)&&!GUI.enabled,"Actual OnGUI restores GUI matrix/color/enabled");}Check(own==Get<List<Texture2D>>(art,"owned").Count&&cache==Get<Dictionary<string,Texture2D>>(art,"cache").Count,"Armor adds no PixelArt texture/cache allocation");Check((bool)Call(app,"Save")&&Files()==files,"Visual review Save and drawing retain all existing save and backup bytes");GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;
        var control=new FrontierRuntime();Call(control,"Awake");try{e=Prepare(app);Enemy other=Prepare(control);e.timer=other.timer=0;for(int frame=0;frame<90;frame++){Update(app);Update(control);string state=State(app.Game);Check(State(control.Game)==state,"Rendered/unrendered full real combat state matches frame "+frame);Draw(app);Check(State(app.Game)==state,"Guard cannot change subsequent combat progression");}}finally{control.EndVisualReview();Call(control,"OnDestroy");}
        Console.WriteLine("PASS Core/save/GUI/clock/cache isolation and 90 real rendered-vs-unrendered combat Updates; NATIVE_UNVERIFIED");
    }
    private static void Captures(FrontierRuntime app)
    {
        foreach(int heading in new[]{0,1,2,3,4,6}){Enemy e=Prepare(app);float a=(float)(heading*Math.PI/4);e.Face(new Point((float)Math.Cos(a),(float)Math.Sin(a)));Draw(app,"facing-"+heading);}
        Enemy target=Prepare(app);target.state=1;target.timer=.4f;target.windupDuration=.65f;target.target=app.Game.Position;Draw(app,"windup-open");target=Prepare(app);target.releaseTime=.2f;Draw(app,"release-guard");target=Prepare(app);target.stun=.4f;Draw(app,"stun-open");target=Prepare(app);target.facing=new Point(1,0);ScreenFoot(app,target,new Point(-10,110));Draw(app,"hud-graze");target=Prepare(app);Screen.width=960;Screen.height=640;Draw(app,"small-lowfx");target=Prepare(app);Screen.width=1920;Screen.height=1080;gset(app,false,2);Draw(app,"wide-richfx");foreach(bool english in new[]{false,true})foreach(bool large in new[]{false,true}){Prepare(app);Screen.width=960;Screen.height=640;app.Game.save.english=english;app.Game.save.largeText=large;Call(app,"OpenMenu","help");Set(app,"helpTopic",1);Draw(app,"help-"+(english?"en":"zh")+(large?"-large":"-normal"));}Check(captures==16,"Sixteen comparable actual OnGUI armor and field-guide fixtures");Console.WriteLine("CAPTURE 16 actual OnGUI fixtures: 12 armor/facing/phase/camera plus 4 bilingual normal/large-font field guides at 960px; NATIVE_UNVERIFIED");
    }
    public static int Main(string[] args)
    {
        string previous=Application.persistentDataPath,directory=Path.Combine(Path.GetTempPath(),"starfall-heavy-guard-"+Guid.NewGuid());Application.persistentDataPath=directory;captureFolder=args.Length>0&&args[0]!="-"?args[0]:null;capturePrefix=args.Length>1?args[1]:"qa-heavy-guard";bool only=args.Length>2&&args[2]=="capture-only";int failed=0;var app=new FrontierRuntime();try{Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"starfall-v1.json"),"{\"version\":1,\"profession\":0}");File.WriteAllText(Path.Combine(directory,"starfall-slot-1.json.bak"),"existing-backup-sentinel");Call(app,"Awake");Check(app.Game!=null,"Production Awake loads campaign");if(!only)foreach(Action<FrontierRuntime> test in new Action<FrontierRuntime>[]{RealContactsAndPhases,GeometryPoliciesAndPreservation,HudBoundsAndOrder,Isolation})try{test(app);}catch(Exception ex){failed++;Console.WriteLine("FAIL "+test.Method.Name+": "+(ex.InnerException??ex));}if(captureFolder!=null)Captures(app);app.EndVisualReview();Call(app,"OnDestroy");}finally{Application.persistentDataPath=previous;Input.Clear();GUI.queuedClick=null;GUI.matrix=Matrix4x4.identity;GUI.color=Color.white;GUI.enabled=true;Event.current.type=EventType.Repaint;if(Directory.Exists(directory))Directory.Delete(directory,true);}if(only){Console.WriteLine("Capture-only semantic checks skipped; NATIVE_UNVERIFIED");return 0;}Console.WriteLine("Runtime Heavy guard: "+(4-failed)+"/4 groups passed; NATIVE_UNVERIFIED");return failed==0?0:1;
    }
}
